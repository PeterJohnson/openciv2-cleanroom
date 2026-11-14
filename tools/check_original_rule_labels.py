"""Read-only consistency check of a retained original save and startup text data.

F02 section 8, F03 Appendix A, P04 section 2, F06 leader records. This independently
authored observation helper does not run the original or import reconstruction
code. It accepts the simple decimal counts/title indexes in the supplied English
text files, not arbitrary F03 grammar. It is not a gameplay implementation input.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess


def receipt(path):
    raw = path.read_bytes()
    return {"path": path.as_posix(), "bytes": len(raw),
            "sha256": hashlib.sha256(raw).hexdigest()}


def lines(path):
    raw = path.read_bytes().replace(b"\r\n", b"\n")
    if any(byte in raw for byte in (b"\r", b"\0", b"\x1a")):
        raise ValueError("This observation helper requires LF/CRLF without control EOF bytes")
    return raw.split(b"\n")


def following(rows, header, count):
    matches = [i for i, row in enumerate(rows) if row.strip(b" \t").upper() == header]
    if len(matches) != 1:
        raise ValueError(f"Expected one {header!r} section")
    result = rows[matches[0] + 1:matches[0] + 1 + count]
    if len(result) != count or any(len(row) + 1 > 254 for row in result):
        raise ValueError("Observation rows must fit the supported single-read domain")
    return [row.strip(b" \t") for row in result]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--labels", type=Path, required=True)
    parser.add_argument("--rules", type=Path, required=True)
    parser.add_argument("--save", type=Path, required=True)
    parser.add_argument("--tribe", type=int, required=True)
    parser.add_argument("--cli", type=Path, required=True)
    parser.add_argument("--bindings", type=Path,
                        help="Optionally compare the portable C++ label/rule loader too")
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()
    if not 0 <= args.tribe < 21:
        parser.error("tribe must be 0..20, as independently identified for the saved player")
    labels = lines(args.labels)
    declared = int(following(labels, b"@LABELS", 1)[0])
    if not 1 <= declared <= 10000:
        raise ValueError("Label count outside this bounded observation domain")
    label_rows = following(labels, b"@LABELS", declared + 1)[1:]
    popup_rows = following(labels, b"@POPUPS", 3)
    prefix = 1 + sum(bool(row) for row in label_rows) + sum(bool(row) for row in popup_rows)
    # Padded allocations: 100 advances, 67 improvements, 62 units, 33 terrains,
    # then 7 government names and 14 titles. Defaults also allocate padded names.
    next_id = prefix + 283
    predicted = None
    for slot, row in enumerate(following(lines(args.rules), b"@LEADERS", 21)):
        fields = [field.strip(b" \t") for field in row.split(b",")]
        if len(fields) < 10:
            raise ValueError("Incomplete leader observation row")
        values = {"names[0]": next_id, "names[1]": next_id + 1,
                  "plural": next_id + 2, "adjective": next_id + 3}
        next_id += 4
        for government in range(7):
            for sex in range(2):
                values[f"titles[{government}][{sex}]"] = prefix + 263 + 3 * government + sex
        cursor = 10
        while cursor < len(fields) and fields[cursor]:
            government = int(fields[cursor])
            if government <= 0:
                break
            if government > 6 or cursor + 2 >= len(fields):
                raise ValueError("Unsupported leader title triple")
            for sex in range(2):
                present = bool(fields[cursor + 1 + sex])
                values[f"titles[{government}][{sex}]"] = next_id if present else 0
                next_id += present
            cursor += 3
        if slot == args.tribe:
            predicted = values
    result = subprocess.run([str(args.cli.resolve()), "inspect", str(args.save.resolve()),
                             "--section", "leaders", "--json"], check=True,
                            capture_output=True, text=True, encoding="utf-8", timeout=30)
    inspection = json.loads(result.stdout)
    records = inspection["records"]
    if len(records) != 1 or records[0]["section"] != "leaders":
        raise ValueError("Expected the single-player save's one leader record")
    fields = {field["name"]: int(field["value"]) for field in records[0]["fields"]}
    comparisons = [{"field": key, "predictedOrdinal": value, "stored": fields[key],
                    "magnitudeMatches": abs(fields[key]) == value}
                   for key, value in predicted.items()]
    passed = all(row["magnitudeMatches"] for row in comparisons)
    implementation = None
    if args.bindings:
        loaded = subprocess.run([str(args.bindings.resolve()), str(args.labels.resolve()),
                                 str(args.rules.resolve()), str(args.save.resolve()), str(args.tribe)],
                                capture_output=True, text=True, encoding="utf-8", timeout=30)
        if loaded.returncode not in (0, 1):
            raise RuntimeError(loaded.stderr)
        implementation = json.loads(loaded.stdout)
        loaded_fields = {row["field"]: row for row in implementation["comparisons"]}
        if set(loaded_fields) != set(predicted):
            raise ValueError("C++ comparison did not cover the same 18 fields")
        passed = passed and loaded.returncode == 0 and all(
            abs(loaded_fields[key]["loaded"]) == value and loaded_fields[key]["stored"] == fields[key]
            for key, value in predicted.items())
    report = {
        "format": "openciv2-original-rule-label-consistency/1",
        "createdUtc": datetime.now(timezone.utc).isoformat(),
        "scope": "retained-original-save-and-text-consistency; no new original execution",
        "inputs": {"labels": receipt(args.labels), "rules": receipt(args.rules),
                   "originalSave": receipt(args.save)},
        "driver": receipt(Path(__file__)), "decoder": receipt(args.cli),
        "tribe": args.tribe, "declaredLabels": declared,
        "nonemptyLabels": sum(bool(row) for row in label_rows),
        "nonemptyPopups": sum(bool(row) for row in popup_rows),
        "preRuleStringCount": prefix,
        "assumedSequence": "ordinal 0 <nil>; nonempty LABELS; three POPUPS; F03 ordered tables",
        "comparisons": comparisons, "status": "matched" if passed else "mismatched",
        "limitations": [
            "The Python comparison checks saved sign markers by magnitude; it does not normalize or write a save.",
            "The earlier original capture pinned RULES and the save, but not LABELS; current retained LABELS bytes are pinned here.",
            "This corroborates these 18 bindings and the zero-based ordinal sequence; it is not a new startup trace or complete pool/peer conformance.",
            ("The optional C++ comparison runs P04 label rebuild, F03 rules and, for matching ordinary single-player inputs, F06 name-sign restoration; it does not run GAME-cache construction or complete session loading."
             if implementation else "The C++ RuleLoader is not executed by this observation helper.")]}
    if implementation:
        report["implementation"] = implementation
        report["implementationDriver"] = receipt(args.bindings)
    args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"{len(comparisons)} saved label magnitudes: {report['status']}; pre-rule count {prefix}")
    return 0 if passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
