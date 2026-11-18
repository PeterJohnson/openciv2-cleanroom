#!/usr/bin/env python3
"""Build the pinned SQLite C library for the optional native managed exporter.

Run from a native compiler environment
(on Windows, a Developer Command Prompt). The checked archive supplies sqlite3.c;
--archive permits an offline build with the same mandatory SHA-256 verification.
"""
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tempfile
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path)
    parser.add_argument("--verify", action="store_true", help="Verify an existing native build receipt and library without compiling")
    parser.add_argument("--output", type=Path, default=ROOT / "build/sqlite/native")
    parser.add_argument("--cc", default=os.environ.get("CC", "cl" if os.name == "nt" else "cc"))
    args = parser.parse_args()
    pin = json.loads((ROOT / "dependencies.json").read_text())["libraries"]["SQLite"]
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    if args.verify:
        report = json.loads((output / "oc2-sqlite-build.json").read_text())
        expected_name = {"Windows": "oc2_sqlite.dll", "Darwin": "liboc2_sqlite.dylib", "Linux": "liboc2_sqlite.so"}.get(platform.system())
        if (report.get("format"), report.get("version"), report.get("archiveSha256"), report.get("library"), report.get("platform"), report.get("machine"), report.get("extensionLoading")) != (
                "openciv2-managed-sqlite-build/1", pin["version"], pin["sha256"], expected_name, platform.system(), platform.machine(), False):
            raise SystemExit("SQLite native receipt does not match the pinned host build")
        installed = output / expected_name
        if report["librarySha256"] != sha(installed):
            raise SystemExit("SQLite native library hash does not match its build receipt")
        library = ctypes.CDLL(str(installed))
        library.sqlite3_libversion.restype = ctypes.c_char_p
        library.sqlite3_compileoption_used.argtypes = [ctypes.c_char_p]
        if library.sqlite3_libversion().decode("ascii") != pin["version"] or library.sqlite3_compileoption_used(b"OMIT_LOAD_EXTENSION") != 1:
            raise SystemExit("SQLite native runtime does not match the required version/configuration")
        print("Verified pinned SQLite native artifact: " + str(installed))
        return
    archive = args.archive or output / Path(pin["url"]).name
    if not archive.exists():
        if args.archive:
            raise SystemExit("Offline archive does not exist")
        with urllib.request.urlopen(pin["url"], timeout=60) as response:
            archive.write_bytes(response.read())
    if sha(archive) != pin["sha256"]:
        raise SystemExit("SQLite archive does not match dependencies.json SHA-256")
    system = platform.system()
    filename = {"Windows": "oc2_sqlite.dll", "Darwin": "liboc2_sqlite.dylib", "Linux": "liboc2_sqlite.so"}.get(system)
    if filename is None:
        raise SystemExit("Optional SQLite export supports native Windows, macOS and Linux")
    compiler = shutil.which(args.cc)
    if not compiler:
        raise SystemExit("Native C compiler not found: " + args.cc)
    with tempfile.TemporaryDirectory(prefix="sqlite-build-", dir=output) as directory:
        work = Path(directory)
        with zipfile.ZipFile(archive) as bundle:
            entries = [name for name in bundle.namelist() if name.endswith("/sqlite3.c")]
            if len(entries) != 1:
                raise SystemExit("Pinned archive must contain exactly one amalgamation")
            source = work / "sqlite3.c"
            source.write_bytes(bundle.read(entries[0]))
        target = work / filename
        defines = ["SQLITE_THREADSAFE=1", "SQLITE_OMIT_LOAD_EXTENSION=1"]
        if system == "Windows":
            command = [compiler, "/nologo", "/O2", "/LD", *["/D" + d for d in defines],
                       "/DSQLITE_API=__declspec(dllexport)", str(source), "/link", "/OUT:" + str(target)]
            version = subprocess.run([compiler], capture_output=True, text=True)
        else:
            command = [compiler, "-O2", "-fPIC", "-dynamiclib" if system == "Darwin" else "-shared",
                       *["-D" + d for d in defines], str(source), "-o", str(target), "-lm", "-pthread"]
            if system == "Darwin":
                command += ["-Wl,-install_name,@rpath/" + filename]
            version = subprocess.run([compiler, "--version"], capture_output=True, text=True, check=True)
        subprocess.run(command, cwd=work, check=True)
        installed = output / filename
        shutil.copyfile(target, installed)
        source_hash = sha(source)
    # Validate the actual native artifact, not just its filename or header.
    library = ctypes.CDLL(str(installed))
    library.sqlite3_libversion.restype = ctypes.c_char_p
    actual = library.sqlite3_libversion().decode("ascii")
    library.sqlite3_compileoption_used.argtypes = [ctypes.c_char_p]
    if actual != pin["version"] or library.sqlite3_compileoption_used(b"OMIT_LOAD_EXTENSION") != 1:
        raise SystemExit("Built SQLite version or extension-loading configuration is wrong")
    report = {"format": "openciv2-managed-sqlite-build/1", "version": actual,
              "archiveSha256": pin["sha256"], "amalgamationSha256": source_hash,
              "library": filename, "librarySha256": sha(installed),
              "platform": system, "machine": platform.machine(), "compiler": compiler,
              "compilerVersion": (version.stdout + version.stderr).strip(), "command": command,
              "extensionLoading": False}
    (output / "oc2-sqlite-build.json").write_text(json.dumps(report, indent=2) + "\n")
    (output / "SQLITE-NOTICE.txt").write_text(
        f"SQLite {actual}\nPublic domain: https://www.sqlite.org/copyright.html\n"
        f"Source: {pin['url']}\nSHA-256: {pin['sha256']}\n")
    print(f"Built and verified SQLite {actual}: {installed}")


if __name__ == "__main__":
    main()
