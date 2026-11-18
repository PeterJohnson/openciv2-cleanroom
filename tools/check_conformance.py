#!/usr/bin/env python3
"""Check M0 inventory integrity, not X02 gameplay conformance (M1). Stdlib only."""

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
DISPOSITIONS = {
    "game-algorithm", "platform-contract", "library-delegation", "unreachable-target",
    "excluded-write-bug", "legacy-random-reference", "presentation-reference",
    "off-nominal-reference", "optional-transport", "unresolved-read", "evidence-gap",
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, f"duplicate JSON key: {key}")
        result[key] = value
    return result


def read_json(path):
    def reject_constant(value):
        raise ValueError(f"non-JSON numeric constant: {value}")

    return json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=unique_object,
                      parse_constant=reject_constant)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def local_file(root, name):
    require(isinstance(name, str) and name and "\\" not in name, "invalid file name")
    path = (root / name).resolve()
    require(path.is_relative_to(root.resolve()) and path.is_file(), f"not a local file: {name}")
    return path


def indexed(rows, label):
    result = {row["id"]: row for row in rows}
    require(len(result) == len(rows), f"duplicate {label} id")
    return result


def anchor(root, reference):
    name, separator, fragment = reference.partition("#")
    require(separator and fragment, f"missing specification anchor: {reference}")
    headings = []
    for line in local_file(root, name).read_text(encoding="utf-8").splitlines():
        if line.startswith("#"):
            heading = line.lstrip("#").strip().lower()
            headings.append(re.sub(r"[^\w\- ]", "", heading).replace(" ", "-"))
    require(fragment in headings, f"missing heading: {reference}")


def validate(root=ROOT):
    registry = read_json(root / "docs/conformance/registry.json")
    ledger = read_json(root / "docs/conformance/coverage.json")
    require(registry["schemaVersion"] == ledger["schemaVersion"] == 1, "unknown schema")
    require(registry["inventoryVersion"] == ledger["inventoryVersion"], "inventory version mismatch")
    specs = indexed(registry["specifications"], "specification")
    actual = {p.name[:3] for p in (root / "docs/algorithms").glob("[A-Z][0-9][0-9]-*.md")}
    require(set(specs) == actual, "specification inventory is incomplete or stale")
    for spec in specs.values():
        require(digest(local_file(root, spec["path"])) == spec["sha256"],
                f"specification hash changed: {spec['id']}; review contracts before updating")
        require(spec["specificationStatus"] == "specified", "invalid specification status")
        require(spec["registrationStatus"] in {"not-registered", "partial", "complete"},
                "invalid registration status")

    operations = indexed(registry["operations"], "operation")
    states = indexed(registry["states"], "state")
    providers = indexed(registry.get("providers", []), "provider")
    require(not (providers.keys() & operations.keys()), "supplied provider cannot also claim an implemented operation")
    for row in [*operations.values(), *states.values(), *providers.values()]:
        require(row["owner"] in specs and row["id"].startswith(row["owner"] + "."), "invalid owner")
        require(row["specificationAnchor"].startswith(specs[row["owner"]]["path"] + "#"),
                "anchor does not belong to owner")
        anchor(root, row["specificationAnchor"])
        if row["implementation"] is not None:
            local_file(root, row["implementation"])
    for state in states.values():
        for field in ("valueType", "lifetime", "collectionOrder", "publication"):
            require(bool(state[field]), f"state missing {field}")
        require(set(state["crossReferences"]) <= states.keys(), "unknown state cross reference")
    for op in operations.values():
        require(set(op["reads"] + op["writes"]) <= states.keys(), "unknown state closure")
        require(set(op["suppliedChildren"]) <= providers.keys(), "unknown supplied child")
        require(set(op["callbacks"]) <= operations.keys(), "unknown callback")
        for field in ("arguments", "result", "preconditions", "callbacks", "suppliedChildren",
                      "acceptedFaults", "partialEffects", "aliasing", "stopBoundaries",
                      "invocation", "events", "dependencies", "independenceClaims"):
            require(field in op, f"operation missing {field}")
        require(len(set(op["events"])) == len(op["events"]), "duplicate event")
        edges = op["dependencies"]
        require(all(len(e) == 2 and set(e) <= set(op["events"]) for e in edges), "unknown event edge")
        remaining = set(op["events"])
        while remaining:
            ready = {e for e in remaining if not any(b == e and a in remaining for a, b in edges)}
            require(ready, "cyclic event dependencies")
            remaining -= ready
    for provider in providers.values():
        require(provider["role"] == "supplied-child" and provider["implementation"] is None,
                "supplied child must not claim a gameplay implementation")
        require(set(provider["outputs"]) <= states.keys() and set(provider["callbacks"]) <= operations.keys(),
                "unknown provider output/callback")
        require(provider["arguments"] and provider["result"] and provider["limitations"], "incomplete provider contract")

    fixtures = indexed(registry["fixtures"], "fixture")
    actual_files = {p.relative_to(root).as_posix() for p in (root / "docs/algorithms/conformance").glob("*.json")}
    require({f["path"] for f in fixtures.values()} == actual_files, "fixture inventory mismatch")
    for item in fixtures.values():
        path = local_file(root, item["path"])
        require(digest(path) == item["sha256"], f"fixture bytes changed: {item['id']}")
        fixture = read_json(path)
        require(fixture["format"] == "openciv2-conformance/3", "unsupported fixture format")
        require((fixture["id"], fixture["revision"]) == (item["id"], item["revision"]), "fixture identity mismatch")
        require(item["operation"] in operations, "unregistered fixture operation")
        op = operations[item["operation"]]
        require(fixture["boundary"]["operation"] == op["id"], "fixture operation mismatch")
        arguments = fixture["boundary"]["arguments"]
        require(arguments.keys() == op["arguments"].keys() and all(
            v["$type"] in ({"u16", "null"} if op["arguments"][k] == "city-slot-or-null" else {op["arguments"][k]})
            for k, v in arguments.items()), "fixture argument contract mismatch")
        for spec in fixture["specifications"]:
            require(spec["id"] in specs and spec["sha256"] == specs[spec["id"]]["sha256"], "fixture spec hash mismatch")
            require((path.parent / spec["file"]).resolve() == (root / specs[spec["id"]]["path"]).resolve(),
                    "fixture spec path mismatch")
        require(item["provenance"] == fixture["evidence"]["kind"], "fixture provenance mismatch")
        require(item["executionStatus"] == "not-run" and item["runReport"] is None,
                "Execution claims require a source-bound report from tools/dotnet/run_conformance.py")

    gaps = indexed(ledger["gaps"], "gap")
    for gap in gaps.values():
        require(all(gap[k] for k in ("milestone", "domain", "knownPrefix", "closureEvidence")), "unbounded gap")
    rows = indexed(ledger["rows"], "coverage")
    require(set(rows) == set(operations), "coverage/operation mismatch")
    for row in rows.values():
        op = operations[row["operation"]]
        require(row["id"] == op["id"] and row["owner"] == op["owner"], "coverage owner mismatch")
        require(row["specificationAnchor"] == op["specificationAnchor"], "coverage anchor mismatch")
        require(row["disposition"] in DISPOSITIONS, "unknown disposition")
        require(row["domain"] and row["comparisonSurfaces"] and row["evidenceEntries"], "incomplete coverage")
        require(row["implementationStatus"] == ("implemented" if op["implementation"] else "not-implemented"),
                "implementation status mismatch")
        require(set(row["caseIds"]) == {f["id"] for f in fixtures.values() if f["operation"] == op["id"]},
                "coverage fixture mismatch")
        for evidence in row["evidenceEntries"]:
            require(digest(local_file(root, evidence["path"])) == evidence["sha256"], "evidence hash mismatch")
    integrations = indexed(ledger["integrationFamilies"], "integration")
    x02 = local_file(root, specs["X02"]["path"]).read_text(encoding="utf-8")
    require(set(integrations) == set(re.findall(r"^\| (G\d\d) \|", x02, re.M)), "integration inventory mismatch")
    for row in [*rows.values(), *integrations.values()]:
        anchor(root, row["specificationAnchor"])
        require(set(row["gaps"]) <= gaps.keys(), "unknown gap")
        require(set(row["caseIds"]) <= fixtures.keys(), "unknown case")
        if row["lastRun"] is not None:
            local_file(root, row["lastRun"])
            require(row["caseIds"] and all(fixtures[c]["executionStatus"] != "not-run" for c in row["caseIds"]),
                    "run claimed without executed cases")
    return (f"{len(specs)} specification families; {len(operations)} registered operations; "
            f"{len(states)} state keys; {len(fixtures)} inventoried vectors; "
            f"{len(integrations)} integration families. Execution evidence is recorded separately by the C# native/browser runner. "
            "Metadata integrity only; no gameplay executed by this check.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    args = parser.parse_args()
    try:
        print(validate(args.root.resolve()))
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"Conformance inventory error: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
