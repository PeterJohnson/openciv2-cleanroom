#!/usr/bin/env python3
"""Execute the registered pure F01 subset of X02 v3 against a C++ adapter.

Python is a developer-only host driver. It never calculates the operation result
and never sends expectations to the implementation. Unsupported case features
fail explicitly; this is not yet the general state/provider/reentry runner.
"""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import platform
import re
import subprocess
import sys

import check_conformance as inventory

ROOT = Path(__file__).resolve().parents[1]
OPERATIONS = {
    "F01.primaryModuloFromCode": {"code": "i32", "n": "i32"},
    "F01.secondaryRangeFromCode": {"lo": "i32", "hi": "i32", "U": "u32"},
}
TARGET = {"game": "Civilization II Multiplayer Gold Edition", "version": "5.4.0f patch 3",
          "originalImageSha256": "edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c"}
EVENTS = {"root.enter": "enter", "root.return": "return", "root.exit": "snapshot"}
ADAPTER_SOURCES = ("CMakeLists.txt", "dependencies.json", "cmake/Toolchain.cmake",
                   "cmake/ConformanceIdentity.cmake", "src/base/arithmetic.hpp",
                   "src/base/arithmetic.cpp", "src/base/random.hpp", "src/base/random.cpp",
                   "tools/conformance_adapter.cpp")
RUNNER_SOURCES = ("tools/run_conformance.py", "tools/check_conformance.py")
SOURCES = ADAPTER_SOURCES + RUNNER_SOURCES


class UnsupportedFixture(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise ValueError(message)


def supported(condition, message):
    if not condition:
        raise UnsupportedFixture(message)


def reject_nonfinite(value):
    raise ValueError(f"non-JSON number: {value}")


def object_keys(value, keys, label, extensions=False):
    require(isinstance(value, dict), f"{label}: expected object")
    actual = set(value)
    if extensions and "extensions" in actual:
        require(isinstance(value["extensions"], dict), "extensions must be an object")
        actual.remove("extensions")
    require(actual == set(keys.split()), f"{label}: missing or unknown fields")


def name(value, label):
    require(isinstance(value, str) and bool(value), f"{label}: expected nonempty text")


def metadata_integer(value, label, positive=False):
    require(type(value) is int and (1 if positive else 0) <= value <= 2147483647,
            f"{label}: invalid metadata integer")


def integer(value, kind):
    object_keys(value, "$type value", "typed integer")
    require(value["$type"] == kind and isinstance(value["value"], str), "integer type mismatch")
    text = value["value"]
    require(re.fullmatch(r"0|-?[1-9][0-9]*", text) is not None, "noncanonical integer")
    require(len(text) <= 11, "integer out of range")
    number = int(text)
    low, high = (-2147483648, 2147483647) if kind == "i32" else (0, 4294967295)
    require(low <= number <= high, "integer out of range")
    return number


def checked_file(root, directory, relative, digest):
    require(isinstance(relative, str) and not Path(relative).is_absolute() and "\\" not in relative
            and ":" not in relative, "invalid specification path")
    path = (directory / relative).resolve()
    require(path.is_relative_to(root.resolve()) and path.is_file(), "specification outside repository or missing")
    require(isinstance(digest, str) and re.fullmatch(r"[0-9a-f]{64}", digest), "invalid SHA-256")
    require(inventory.digest(path) == digest, f"specification hash mismatch: {relative}")
    return path


def trace_events(trace):
    require(isinstance(trace, list), "trace must be an array")
    by_id = {}
    for seq, event in enumerate(trace):
        object_keys(event, "seq id kind call payload", "trace event")
        metadata_integer(event["seq"], "seq")
        require(event["seq"] == seq, "nonconsecutive trace sequence")
        require(isinstance(event["id"], str) and event["id"] in EVENTS, "unknown event identity")
        require(event["id"] not in by_id, "duplicate event identity")
        require(event["kind"] == EVENTS[event["id"]] and event["call"] == "root", "event kind/call mismatch")
        payload = event["payload"]
        if event["kind"] == "enter":
            object_keys(payload, "operation parent arguments", "enter payload")
            require(payload["operation"] in OPERATIONS and payload["parent"] is None, "invalid invocation")
            arguments = OPERATIONS[payload["operation"]]
            object_keys(payload["arguments"], " ".join(arguments), "enter arguments")
            for key, kind in arguments.items():
                integer(payload["arguments"][key], kind)
        elif event["kind"] == "return":
            object_keys(payload, "result", "return payload")
            integer(payload["result"], "i32")
        else:
            object_keys(payload, "label state", "snapshot payload")
            require(payload["label"] == "exit" and payload["state"] == {}, "invalid exit snapshot")
        by_id[event["id"]] = event
    require(set(by_id) == set(EVENTS), "missing selected event")
    return by_id


def validate_order(order, events, registered):
    object_keys(order, "before independent", "comparison.order")
    require(isinstance(order["before"], list), "before must be an array")
    supported(order["independent"] == [], "pure F01 operations have no independent events")
    edges = set()
    for edge in order["before"]:
        require(isinstance(edge, list) and len(edge) == 2 and
                all(isinstance(key, str) and key in events for key in edge), "unknown dependency event")
        a, b = edge
        require(a != b and (a, b) not in edges, "self or duplicate dependency")
        require(events[a]["seq"] < events[b]["seq"], "expected chronology violates dependency")
        edges.add((a, b))
    closure = set(edges)
    while True:
        expanded = closure | {(a, d) for a, b in closure for c, d in closure if b == c}
        if expanded == closure:
            break
        closure = expanded
    require(all(a != b for a, b in closure), "cyclic dependency")
    require(all(tuple(edge) in closure for edge in registered), "missing registered dependency")
    for a in events:
        for b in events:
            require(a == b or (a, b) in closure or (b, a) in closure, "unexplained incomparable events")


def load_fixture(path, root=ROOT):
    path = Path(path).resolve()
    root = Path(root).resolve()
    require(path.is_relative_to(root), "fixture must be inside repository bundle")
    case = inventory.read_json(path)
    object_keys(case, "format id revision target specifications boundary profile entry randomness environment expected comparison evidence limits",
                "fixture", extensions=True)
    require(case["format"] == "openciv2-conformance/3", "unsupported fixture version")
    name(case["id"], "fixture id")
    metadata_integer(case["revision"], "revision", positive=True)
    require(case["target"] == TARGET, "unknown target identity")
    registry = inventory.read_json(root / "docs/conformance/registry.json")
    specs = {entry["id"]: entry for entry in registry["specifications"]}
    pins = {}
    require(isinstance(case["specifications"], list), "specifications must be an array")
    for pin in case["specifications"]:
        object_keys(pin, "id file sha256", "specification pin")
        require(isinstance(pin["id"], str) and pin["id"] in specs and pin["id"] not in pins, "unknown/duplicate specification")
        resolved = checked_file(root, path.parent, pin["file"], pin["sha256"])
        registered = specs[pin["id"]]
        require(resolved == (root / registered["path"]).resolve() and pin["sha256"] == registered["sha256"], "registry/specification mismatch")
        pins[pin["id"]] = pin["sha256"]
    require(set(pins) == {"F01", "X01", "X02"}, "pure F01 contract pins incomplete")
    boundary = case["boundary"]
    object_keys(boundary, "level operation start stop arguments suppliedChildren", "boundary")
    name(boundary["operation"], "operation")
    supported(boundary["operation"] in OPERATIONS, "operation has no executable adapter")
    supported(boundary["level"] == "primitive" and boundary["suppliedChildren"] == [], "only pure primitive boundaries supported")
    require(boundary["start"] == "pure mapping entry with explicit code argument" and
            boundary["stop"] == "normal return followed by exit snapshot", "unsupported start/stop boundary")
    arguments = OPERATIONS[boundary["operation"]]
    operation = next((row for row in registry["operations"] if row["id"] == boundary["operation"]), None)
    require(operation is not None and operation["owner"] == "F01" and
            operation["arguments"] == arguments and operation["result"] == "i32", "adapter/registry contract mismatch")
    require(all(operation[key] == [] for key in ("reads", "writes", "callbacks", "suppliedChildren", "acceptedFaults", "partialEffects", "independenceClaims"))
            and operation["invocation"] == "root" and len(operation["events"]) == 3 and
            set(operation["events"]) == set(EVENTS) and
            operation["stopBoundaries"] == ["normal return followed by exit snapshot"], "adapter cannot satisfy registry closure")
    object_keys(boundary["arguments"], " ".join(arguments), "arguments")
    values = {key: integer(boundary["arguments"][key], kind) for key, kind in arguments.items()}
    if boundary["operation"] == "F01.primaryModuloFromCode":
        require(0 <= values["code"] <= 32767 and values["n"] > 0, "primary mapping precondition")
    profile = case["profile"]
    object_keys(profile, "domain safety restrictions deviations", "profile")
    require(profile["domain"] in ("nominal", "accepted-off-nominal", "restricted"), "unknown profile")
    name(profile["safety"], "safety policy")
    supported(profile["restrictions"] == profile["deviations"] == [], "restricted/deviating comparisons not implemented")
    object_keys(case["entry"], "state retainedInputs blobs", "entry")
    supported(case["entry"] == {"state": {}, "retainedInputs": [], "blobs": []}, "nonempty state/blob closure not implemented")
    object_keys(case["randomness"], "mode policy contexts choices", "randomness")
    supported(case["randomness"] == {"mode": "controlled-choices", "policy": None, "contexts": {}, "choices": []}, "only explicit-code pure mappings supported; no RNG fallback")
    object_keys(case["environment"], "protocol inputs unexpected", "environment")
    supported(case["environment"] == {"protocol": "scripted-v1", "inputs": [], "unexpected": "fail"}, "providers/reentry not implemented")
    expected = case["expected"]
    object_keys(expected, "outcome result state trace", "expected")
    supported(expected["outcome"] == "return" and expected["state"] == {}, "only normal pure returns supported")
    integer(expected["result"], "i32")
    events = trace_events(expected["trace"])
    require(events["root.enter"]["payload"] == {"operation": boundary["operation"], "parent": None, "arguments": boundary["arguments"]}, "entry trace does not match boundary")
    require(events["root.return"]["payload"]["result"] == expected["result"], "return trace/result disagreement")
    comparison = case["comparison"]
    object_keys(comparison, "stateScope traceKinds order exclusions normalizations", "comparison")
    supported(comparison["stateScope"] == "complete-boundary" and comparison["exclusions"] == comparison["normalizations"] == [], "projected/normalized comparisons not implemented")
    require(isinstance(comparison["traceKinds"], list) and len(comparison["traceKinds"]) == 3 and
            all(isinstance(kind, str) for kind in comparison["traceKinds"]) and set(comparison["traceKinds"]) == set(EVENTS.values()), "mandatory trace kinds missing or duplicated")
    validate_order(comparison["order"], events, operation["dependencies"])
    evidence = case["evidence"]
    object_keys(evidence, "kind sources limitations", "evidence")
    supported(evidence["kind"] == "specification-vector", "only specification-vector provenance supported")
    require(isinstance(evidence["sources"], list) and evidence["sources"], "missing evidence sources")
    require(isinstance(evidence["limitations"], list), "limitations must be an array")
    for limitation in evidence["limitations"]:
        name(limitation, "evidence limitation")
    for source in evidence["sources"]:
        object_keys(source, "file section sha256", "evidence source")
        resolved = checked_file(root, path.parent, source["file"], source["sha256"])
        require(resolved in {(root / specs[key]["path"]).resolve() for key in pins}, "evidence outside pinned specifications")
        name(source["section"], "source section")
        inventory.anchor(root, f"{resolved.relative_to(root).as_posix()}#{source['section']}")
    object_keys(case["limits"], "events providerRequests hostMilliseconds", "limits")
    for key, value in case["limits"].items():
        metadata_integer(value, key, positive=True)
    return case


def compare(case, actual):
    """X02 §6: typed equality, exact semantic IDs and required dependencies."""
    object_keys(actual, "outcome result state trace", "adapter output")
    integer(actual["result"], "i32")
    events = trace_events(actual["trace"])
    for key in ("outcome", "result", "state"):
        if actual[key] != case["expected"][key]:
            return {"surface": key, "expected": case["expected"][key], "actual": actual[key]}
    expected_events = {event["id"]: event for event in case["expected"]["trace"]}
    for identity, expected in expected_events.items():
        for key in ("kind", "call", "payload"):
            if events[identity][key] != expected[key]:
                return {"surface": f"trace.{identity}.{key}", "expected": expected[key], "actual": events[identity][key]}
    for a, b in case["comparison"]["order"]["before"]:
        if events[a]["seq"] >= events[b]["seq"]:
            return {"surface": "trace.order", "edge": [a, b], "positions": [events[a]["seq"], events[b]["seq"]]}
    return None


def execute(path, command, root=ROOT):
    report = {"path": str(path)}
    try:
        report.update(path=Path(path).resolve().relative_to(root.resolve()).as_posix(),
                      sha256=inventory.digest(path))
        case = load_fixture(path, root)
        report.update(id=case["id"], revision=case["revision"], operation=case["boundary"]["operation"],
                      evidenceKind=case["evidence"]["kind"], specifications=case["specifications"],
                      target=case["target"], profile=case["profile"], evidence=case["evidence"],
                      comparison=case["comparison"], limits=case["limits"])
    except UnsupportedFixture as error:
        return {**report, "status": "unsupported-fixture", "error": str(error)}
    except (ValueError, KeyError, TypeError, OSError, RecursionError) as error:
        return {**report, "status": "invalid-fixture", "error": str(error)}
    if case["limits"]["events"] < 3:
        return {**report, "status": "budget-exhausted", "error": "three required events exceed event limit"}
    operation = case["boundary"]["operation"]
    args = [case["boundary"]["arguments"][key]["value"] for key in OPERATIONS[operation]]
    try:
        process = subprocess.run([*command, operation, *args], capture_output=True, text=True,
                                 encoding="utf-8", timeout=case["limits"]["hostMilliseconds"] / 1000)
        if process.returncode != 0:
            return {**report, "status": "adapter-failure", "exitCode": process.returncode, "error": process.stderr}
        actual = json.loads(process.stdout, object_pairs_hook=inventory.unique_object,
                            parse_constant=reject_nonfinite)
        mismatch = compare(case, actual)
        return {**report, "status": "failed" if mismatch else "passed", "firstMismatch": mismatch,
                "actual": actual, "consumed": {"providers": 0, "choices": 0, "events": len(actual["trace"])}}
    except subprocess.TimeoutExpired:
        return {**report, "status": "budget-exhausted", "error": "host watchdog expired"}
    except (OSError, ValueError, KeyError, TypeError) as error:
        return {**report, "status": "adapter-failure", "error": str(error)}


def validate_adapter_identity(identity, sources):
    object_keys(identity, "format sources buildIdentity", "adapter identity")
    require(identity["format"] == "openciv2-conformance-adapter/1", "unsupported adapter identity")
    require(isinstance(identity["buildIdentity"], str) and identity["buildIdentity"],
            "missing adapter build identity")
    require(identity["sources"] == {path: sources[path] for path in ADAPTER_SOURCES},
            "stale or incomplete adapter identity; rebuild the conformance adapter")


def read_adapter_identity(command, sources):
    try:
        process = subprocess.run([*command, "--identity"], capture_output=True, text=True,
                                 encoding="utf-8", timeout=5)
    except subprocess.TimeoutExpired as error:
        raise ValueError("adapter identity watchdog expired") from error
    require(process.returncode == 0, "adapter identity query failed; rebuild the conformance adapter")
    identity = json.loads(process.stdout, object_pairs_hook=inventory.unique_object,
                          parse_constant=reject_nonfinite)
    validate_adapter_identity(identity, sources)
    return identity


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    parser.add_argument("--fixture", type=Path, action="append")
    parser.add_argument("--report", type=Path)
    parser.add_argument("--build-identity", type=Path)
    parser.add_argument("--adapter", nargs=argparse.REMAINDER, required=True)
    args = parser.parse_args()
    root = args.root.resolve()
    require(bool(args.adapter), "missing adapter command")
    paths = [p.resolve() for p in args.fixture] if args.fixture else sorted((root / "docs/algorithms/conformance").glob("*.json"))
    require(bool(paths), "no fixtures selected")
    if args.report:
        require(not args.report.resolve().is_relative_to(root / "docs/algorithms"), "report cannot overwrite specification/fixture inputs")
    revision = subprocess.run(["git", "rev-parse", "HEAD"], cwd=root, capture_output=True, text=True)
    binary = Path(args.adapter[-1]).resolve()
    artifacts = {str(binary): inventory.digest(binary)}
    if binary.suffix == ".js" and binary.with_suffix(".wasm").exists():
        artifacts[str(binary.with_suffix(".wasm"))] = inventory.digest(binary.with_suffix(".wasm"))
    sources = {path: inventory.digest(root / path) for path in SOURCES}
    identity = read_adapter_identity(args.adapter, sources)
    if args.build_identity:
        require(args.build_identity.read_text() == identity["buildIdentity"],
                "build identity does not match the adapter; rebuild the conformance adapter")
    changed = subprocess.run(["git", "status", "--porcelain", "--", *SOURCES], cwd=root,
                             capture_output=True, text=True)
    report = {"format": "openciv2-conformance-run/2", "runnerScope": "pure-f01-v1",
              "createdUtc": datetime.now(timezone.utc).isoformat(), "implementationRevision": revision.stdout.strip() or None,
              "uncommittedImplementationChanges": bool(changed.stdout),
              "implementationSources": {**identity["sources"], **{path: sources[path] for path in RUNNER_SOURCES}},
              "adapterIdentity": identity,
              "adapterArtifacts": artifacts, "host": platform.platform(),
              "buildIdentity": identity["buildIdentity"],
              "cases": [execute(path, args.adapter, root) for path in paths]}
    require(all(inventory.digest(root / path) == digest for path, digest in sources.items()),
            "implementation sources changed during the run; rebuild and rerun")
    require(all(inventory.digest(Path(path)) == digest for path, digest in artifacts.items()),
            "adapter artifacts changed during the run; rerun with a stable build")
    for case in report["cases"]:
        print(f"{case.get('id', case['path'])}: {case['status']}")
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return 0 if all(case["status"] == "passed" for case in report["cases"]) else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError) as error:
        print(f"Conformance runner: {error}", file=sys.stderr)
        sys.exit(2)
