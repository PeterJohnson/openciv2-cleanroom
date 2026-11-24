#!/usr/bin/env python3
"""Package original-game F04 save observations using the F06 stored inspector.

This records supplied evidence, not expected gameplay or inferred formulas.
See docs/testing/f04-original-observations.md for the capture protocol.
"""

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys


def identity(path):
    digest = hashlib.sha256()
    size = 0
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
            size += len(block)
    return {"sha256": digest.hexdigest(), "byteCount": size}


def write_json(path, value):
    with path.open("x", encoding="utf-8") as stream:
        json.dump(value, stream, indent=2, ensure_ascii=False)
        stream.write("\n")


def summarize(snapshot):
    if (snapshot.get("schema") != "openciv2-inspection/1"
            or snapshot.get("view") != "stored"
            or not snapshot.get("decoded")):
        raise ValueError("Expected a decoded openciv2-inspection/1 stored view")
    result = {}
    for record in snapshot["records"]:
        if record["section"] != "civilizations":
            continue
        fields = {field["name"]: field["value"] for field in record["fields"]}
        slot = record["slot"]
        if slot in result:
            raise ValueError("Duplicate civilization slot")
        result[slot] = {
            "government": int(fields["government"]),
            "bestShieldSurplus": int(fields["best_shield_surplus"]),
            "governmentScore": [int(fields[f"government_score[{i}]"])
                                for i in range(7)],
        }
    if set(result) != {str(i) for i in range(8)}:
        raise ValueError("Expected all eight stored civilization records")
    return result


def capture(args):
    # Validate inputs before reserving a new destination; never overwrite a run.
    inputs = [args.before, args.after, args.executable, args.rules, args.notes]
    for path in inputs:
        if not path.is_file():
            raise ValueError(f"Not a file: {path}")
    if not args.adapter:
        raise ValueError("--adapter requires the inspector command")
    if not args.notes.read_text(encoding="utf-8").strip():
        raise ValueError("Capture notes must be nonempty")
    args.output.mkdir(parents=True, exist_ok=False)
    manifest = {
        "schema": "openciv2-f04-observation/1",
        "createdUtc": datetime.now(timezone.utc).isoformat(),
        "status": "incomplete",
        "evidence": "operator-supplied saves; no formula or compatibility conclusion",
        "adapter": args.adapter,
        "executable": identity(args.executable),
        "files": {},
    }
    try:
        for name, source in [("before.sav", args.before), ("after.sav", args.after),
                             ("rules.txt", args.rules), ("notes.txt", args.notes)]:
            target = args.output / name
            shutil.copyfile(source, target)
            manifest["files"][name] = identity(target)
        values = {}
        for label in ["before", "after"]:
            save = args.output / f"{label}.sav"
            output = args.output / f"{label}.inspection.json"
            errors = args.output / f"{label}.stderr.txt"
            command = args.adapter + ["inspect", str(save.resolve()), "--json",
                                      "--civilizations", "--cities", "--units",
                                      "--section", "globals"]
            with output.open("xb") as stdout, errors.open("xb") as stderr:
                completed = subprocess.run(command, stdout=stdout, stderr=stderr,
                                           timeout=120, check=False)
            if completed.returncode != 0:
                raise ValueError(f"{label} inspector exited {completed.returncode}; "
                                 "raw output retained; capture remains incomplete")
            snapshot = json.loads(output.read_text(encoding="utf-8"))
            if snapshot["source"]["sha256"] != manifest["files"][f"{label}.sav"]["sha256"]:
                raise ValueError("Inspector input identity mismatch")
            values[label] = summarize(snapshot)
            manifest["files"][output.name] = identity(output)
            manifest["files"][errors.name] = identity(errors)
        write_json(args.output / "summary.json", values)
        manifest["files"]["summary.json"] = identity(args.output / "summary.json")
        manifest["status"] = "packaged"
    except Exception as error:
        manifest["error"] = str(error)
        raise
    finally:
        write_json(args.output / "manifest.json", manifest)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ["before", "after", "executable", "rules", "notes", "output"]:
        parser.add_argument(f"--{name}", type=Path, required=True)
    parser.add_argument("--adapter", nargs=argparse.REMAINDER, required=True,
                        help="Inspector command, last option (e.g. dotnet oc2.dll)")
    args = parser.parse_args()
    try:
        capture(args)
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        print(f"Capture failed: {error}", file=sys.stderr)
        return 1
    print(f"Packaged observations in {args.output}; no formula inferred")
    return 0


if __name__ == "__main__":
    sys.exit(main())
