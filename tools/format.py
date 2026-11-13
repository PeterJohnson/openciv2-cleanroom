#!/usr/bin/env python3
"""Format owned C/C++/Objective-C sources, or check working-tree/index contents."""

import argparse
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile


ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOTS = {"src", "include", "tests", "tools"}
EXTENSIONS = {".c", ".cc", ".cpp", ".cxx", ".h", ".hh", ".hpp", ".hxx",
              ".m", ".mm", ".inl", ".ipp", ".tpp"}
POLICY_FILES = {".clang-format", "dependencies.json", "tools/format.py"}


def git(*args):
    return subprocess.run(
        ["git", *args], cwd=ROOT, check=True, stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    ).stdout


def paths(data):
    return {os.fsdecode(value) for value in data.split(b"\0") if value}


def is_source(name):
    path = Path(name)
    return path.parts[0] in SOURCE_ROOTS and path.suffix in EXTENSIONS


def indexed_files():
    result = set()
    for entry in git("ls-files", "--stage", "-z").split(b"\0"):
        if not entry:
            continue
        metadata, name = entry.split(b"\t", 1)
        mode, _, stage = metadata.split()
        if stage != b"0":
            raise ValueError("Resolve the index's merge conflicts before formatting.")
        if mode in (b"100644", b"100755"):
            result.add(os.fsdecode(name))
    return result


def source_files(staged):
    indexed = indexed_files()
    if staged:
        changed = paths(git("diff", "--cached", "--name-only", "--diff-filter=ACMRDT", "-z"))
        # A policy update must also check unchanged sources against the new policy.
        names = indexed if changed & POLICY_FILES else indexed & changed
    else:
        names = indexed | paths(git("ls-files", "--others", "--exclude-standard", "-z"))
        names = {name for name in names if (ROOT / name).is_file()
                 and not (ROOT / name).is_symlink()}
        if any(not (ROOT / name).resolve().is_relative_to(ROOT) for name in names):
            raise ValueError("Refusing to format a file outside this checkout.")
    return sorted(name for name in names if is_source(name))


def contents(name, staged):
    return git("show", f":{name}") if staged else (ROOT / name).read_bytes()


def formatter(version, override):
    candidates = [override] if override else ["clang-format", f"clang-format-{version.split('.')[0]}"]
    found = []
    for candidate in candidates:
        executable = shutil.which(candidate)
        if not executable:
            continue
        result = subprocess.run([executable, "--version"], check=True, capture_output=True)
        output = result.stdout.decode("utf-8", errors="replace").strip()
        match = re.search(r"\bversion (\d+\.\d+\.\d+)\b", output)
        if match and match.group(1) == version:
            return executable
        found.append(output)
    detail = f" Found: {'; '.join(found)}." if found else ""
    raise ValueError(
        f"clang-format {version} is required.{detail} Install that version or set "
        "CLANG_FORMAT to its executable; see docs/BUILDING.md."
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true", help="check all owned working-tree sources")
    mode.add_argument("--staged", action="store_true", help="check staged sources and staged policy only")
    parser.add_argument("--clang-format", default=os.environ.get("CLANG_FORMAT"), help="formatter executable")
    args = parser.parse_args()
    files = source_files(args.staged)
    if not files:
        print("No source files to format.")
        return 0
    version = json.loads(contents("dependencies.json", args.staged))["tools"]["clang-format"]
    executable = formatter(version, args.clang_format)
    check = args.check or args.staged
    changed = []
    # An explicit temporary style file prevents unstaged/nested config from
    # influencing an index check. All modes use the repository's root policy.
    with tempfile.TemporaryDirectory(prefix="oc2-format-") as directory:
        style = Path(directory) / ".clang-format"
        style.write_bytes(contents(".clang-format", args.staged))
        for name in files:
            before = contents(name, args.staged)
            result = subprocess.run(
                [executable, f"--style=file:{style}", f"--assume-filename={name}",
                 "--Werror", "--fail-on-incomplete-format"],
                input=before, cwd=ROOT, check=True, capture_output=True,
            )
            if result.stdout != before:
                changed.append(name)
                if not check:
                    (ROOT / name).write_bytes(result.stdout)
                print(f"{'Needs formatting' if check else 'Formatted'}: {name}")
    if changed and check:
        print("Run python3 tools/format.py, review the changes, then stage the intended files.",
              file=sys.stderr)
        return 1
    print(f"clang-format {version}: {'checked' if check else 'formatted'} {len(files)} files.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except subprocess.CalledProcessError as error:
        print(error.stderr.decode("utf-8", errors="replace") if error.stderr else str(error),
              file=sys.stderr)
        sys.exit(1)
    except (OSError, ValueError, KeyError) as error:
        print(f"Formatting error: {error}", file=sys.stderr)
        sys.exit(1)
