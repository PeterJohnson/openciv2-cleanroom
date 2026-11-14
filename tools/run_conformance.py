#!/usr/bin/env python3
"""Execute bounded F01/F02/W01 foundation operations from X02 v3 through C++.

Python is a developer-only host driver. It validates supplied sampling laws but
gets actual operation results from C++; expected results never enter the adapter.
F02 city dispatch supports scripted supplied-child inputs and synchronous reentry.
Other composite/provider boundaries fail explicitly until registered.
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
import conformance_random as randomness
import conformance_values as logical
import conformance_script as scripted
import conformance_session as session

ROOT = Path(__file__).resolve().parents[1]
OPERATIONS = {
    "F01.primaryModuloFromCode": {"code": "i32", "n": "i32"},
    "F01.secondaryRangeFromCode": {"lo": "i32", "hi": "i32", "U": "u32"},
    "F02.assignUnitToken": {},
    "F02.assignCityToken": {},
    "F02.reserveUnitSlot": {"owner": "u8"},
    "W01.wrapX": {"x": "i32"},
    "W01.stepDistance": {"x1": "i32", "y1": "i32", "x2": "i32", "y2": "i32"},
    "W01.weightedDistance": {"x1": "i32", "y1": "i32", "x2": "i32", "y2": "i32"},
    "W01.tileIndex": {"x": "i32", "y": "i32"},
}
OPERATIONS.update({operation: {"domain": "text", **schema}
                   for operation, (_, schema) in randomness.SAMPLERS.items()})
OPERATIONS[scripted.OPERATION] = scripted.ARGUMENTS
OPERATIONS[session.OPERATION] = {}
SCOPE = "foundation-session-v3"
TOKEN_STATE = {"F02.nextUnitToken": "u32", "F02.nextCityToken": "u32"}
RESERVATION_STATE = {"F02.unitEnd": "u32", "F02.unitTokenScan": "u32[]",
                     "F02.militaryCounts": "i16[]", "F02.effectiveHumanSet": "u8"}
MAP_DIMENSIONS = {"W01.width": "i32", "W01.height": "i32"}
GEOMETRY_STATE = {**MAP_DIMENSIONS, "W01.flat": "bool"}
STATES = {op: (TOKEN_STATE if op in ("F02.assignUnitToken", "F02.assignCityToken") else
               RESERVATION_STATE if op == "F02.reserveUnitSlot" else
               MAP_DIMENSIONS if op == "W01.tileIndex" else
               GEOMETRY_STATE if op.startswith("W01.") else {}) for op in OPERATIONS}
RESULTS = {op: ("unit-slot-or-null" if op == "F02.reserveUnitSlot" else
                "tile-index-or-null" if op == "W01.tileIndex" else
                "u32" if op.startswith("F02.") else "i32") for op in OPERATIONS}
RESULTS["F01.secondaryUnit"] = "f64"
RESULTS[scripted.OPERATION] = "i32"
STATES[scripted.OPERATION] = scripted.STATES
STATES[session.OPERATION] = session.STATES
RESULTS[session.OPERATION] = "null"
WRITES = {op: (["F02.nextUnitToken"] if op == "F02.assignUnitToken" else
               ["F02.nextCityToken"] if op == "F02.assignCityToken" else
               ["F02.unitEnd"] if op == "F02.reserveUnitSlot" else []) for op in OPERATIONS}
WRITES[scripted.OPERATION] = list(scripted.STATES)
WRITES[session.OPERATION] = session.WRITES
TARGET = {"game": "Civilization II Multiplayer Gold Edition", "version": "5.4.0f patch 3",
          "originalImageSha256": "edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c"}
EVENTS = {"root.enter": "enter", "root.return": "return", "root.exit": "snapshot"}
ADAPTER_SOURCES = ("CMakeLists.txt", "dependencies.json", "cmake/Toolchain.cmake",
                   "cmake/ConformanceIdentity.cmake", "src/base/arithmetic.hpp",
                   "src/base/arithmetic.cpp", "src/base/random.hpp", "src/base/random.cpp",
                   "src/base/random_source.hpp", "src/base/random_source.cpp",
                   "src/base/choices.hpp", "src/base/choices.cpp",
                   "src/base/hash.hpp", "src/base/hash.cpp",
                   "src/state/identities.hpp", "src/state/identities.cpp",
                   "src/state/records.hpp", "src/state/records.cpp",
                   "src/world/geometry.hpp", "src/world/geometry.cpp",
                   "src/world/map.hpp", "src/world/map.cpp",
                   "tools/conformance_adapter.cpp", "tools/conformance_io.hpp",
                   "tools/conformance_random.hpp", "tools/conformance_random.cpp",
                   "tools/conformance_script.hpp", "tools/conformance_script.cpp",
                   "tools/conformance_session.hpp", "tools/conformance_session.cpp",
                   "src/base/session_ids.hpp", "src/query/fields.hpp",
                   "src/query/live.hpp", "src/query/live.cpp",
                   "src/session/runtime.hpp", "src/session/runtime.cpp",
                   "src/session/runtime_internal.hpp", "src/session/command_runtime.cpp",
                   "src/session/commands.hpp", "src/session/commands.cpp")
# Include every state declaration and constructor dependency of the fresh-world
# boundary. Broader than a leaf adapter's inputs; never report a stale reset.
ADAPTER_SOURCES = tuple(dict.fromkeys((*ADAPTER_SOURCES, *sorted(
    path.relative_to(ROOT).as_posix() for path in (ROOT / "src/state").iterdir()
    if path.suffix in (".hpp", ".cpp")))))
RUNNER_SOURCES = ("tools/run_conformance.py", "tools/check_conformance.py",
                  "tools/conformance_values.py", "tools/conformance_random.py",
                  "tools/conformance_script.py", "tools/conformance_session.py",
                  randomness.POLICY_FILE)
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
    return logical.integer(value, kind)


def state_values(state, operation, scan_length=None, case=None):
    """Exact registered closure; arrays preserve every supplied slot in order."""
    require(isinstance(state, dict), "state must be an object")
    if operation == scripted.OPERATION:
        return scripted.state_values(state)
    if operation == session.OPERATION:
        return session.state_values(state)
    if operation in randomness.SAMPLERS:
        replay = case["randomness"]["mode"] == "implementation-replay"
        object_keys(state, randomness.STATE if replay else "", "random state closure")
        if replay:
            domain = logical.scalar(case["boundary"]["arguments"]["domain"], "text")
            randomness.context_values(state[randomness.STATE], domain)
        return state
    fields = STATES[operation]
    supported(set(state) <= set(fields), "state outside supported operation closure")
    require(set(state) == set(fields), "missing registered state input")
    result = {}
    for key, kind in fields.items():
        if kind.endswith("[]"):
            values = state[key]
            require(isinstance(values, list), "state array must be an array")
            expected_length = 8 if key == "F02.militaryCounts" else scan_length
            require(len(values) <= 2048 and (expected_length is None or len(values) == expected_length),
                    "state array length does not match registered view")
            result[key] = [integer(value, kind[:-2]) for value in values]
        elif kind == "bool":
            object_keys(state[key], "$type value", "typed boolean")
            require(state[key]["$type"] == "bool" and type(state[key]["value"]) is bool,
                    "boolean type mismatch")
            result[key] = state[key]["value"]
        else:
            result[key] = integer(state[key], kind)
    if operation == "F02.reserveUnitSlot":
        require(result["F02.unitEnd"] <= 2048, "unit scan bound exceeds capacity")
    if operation.startswith("W01."):
        width, height = result["W01.width"], result["W01.height"]
        require(0 < width <= 32767 and width % 2 == 0 and 0 < height <= 32767
                and width // 2 * height < 32768, "unsupported map dimensions/backing")
    return result


def result_value(value, operation):
    kind = RESULTS[operation]
    if kind == "null":
        require(value == {"$type": "null"}, "void operation must return typed null")
        return None
    if kind in ("unit-slot-or-null", "tile-index-or-null"):
        if value == {"$type": "null"}:
            return None
        result = integer(value, "u16" if kind == "unit-slot-or-null" else "u32")
        require(result < (2048 if kind == "unit-slot-or-null" else 32768), "slot/index exceeds capacity")
        return result
    return logical.scalar(value, kind)


def entry_scan_length(case):
    if case["boundary"]["operation"] == "F02.reserveUnitSlot":
        return integer(case["entry"]["state"]["F02.unitEnd"], "u32")
    return None


def checked_file(root, directory, relative, digest):
    require(isinstance(relative, str) and not Path(relative).is_absolute() and "\\" not in relative
            and ":" not in relative, "invalid specification path")
    path = (directory / relative).resolve()
    require(path.is_relative_to(root.resolve()) and path.is_file(), "specification outside repository or missing")
    require(isinstance(digest, str) and re.fullmatch(r"[0-9a-f]{64}", digest), "invalid SHA-256")
    require(inventory.digest(path) == digest, f"specification hash mismatch: {relative}")
    return path


def selected_events(case):
    if case["boundary"]["operation"] == scripted.OPERATION:
        return {event["id"]: event["kind"] for event in case["expected"]["trace"]}
    events = dict(EVENTS)
    if randomness.sampling(case) and randomness.nonconstant(case["boundary"]["operation"], case["boundary"]["arguments"]):
        events["root.sample"] = "random"
    return events


def trace_events(trace, operation, scan_length, case):
    require(isinstance(trace, list), "trace must be an array")
    by_id = {}
    selected = selected_events(case)
    for seq, event in enumerate(trace):
        object_keys(event, "seq id kind call payload", "trace event")
        metadata_integer(event["seq"], "seq")
        require(event["seq"] == seq, "nonconsecutive trace sequence")
        require(isinstance(event["id"], str) and event["id"] in selected, "unknown event identity")
        require(event["id"] not in by_id, "duplicate event identity")
        require(event["kind"] == selected[event["id"]] and event["call"] == "root", "event kind/call mismatch")
        payload = event["payload"]
        if event["kind"] == "enter":
            object_keys(payload, "operation parent arguments", "enter payload")
            require(payload["operation"] in OPERATIONS and payload["parent"] is None, "invalid invocation")
            arguments = OPERATIONS[payload["operation"]]
            object_keys(payload["arguments"], " ".join(arguments), "enter arguments")
            for key, kind in arguments.items():
                logical.scalar(payload["arguments"][key], kind)
        elif event["kind"] == "random":
            replay = case["randomness"]["mode"] == "implementation-replay"
            object_keys(payload, "site attempt law arguments code result" + (" context" if replay else ""), "random payload")
            require(payload["site"] == operation and type(payload["attempt"]) is int and payload["attempt"] == 0,
                    "random event identity mismatch")
            law, schema = randomness.SAMPLERS[operation]
            require(payload["law"] == law, "random event law mismatch")
            require(payload["result"] == randomness.mapped(law, payload["arguments"], payload["code"]),
                    "random trace violates sampling law")
            if replay:
                domain = logical.scalar(case["boundary"]["arguments"]["domain"], "text")
                require(logical.scalar(payload["context"], "text") == randomness.context_key(domain), "random event context mismatch")
        elif event["kind"] == "return":
            object_keys(payload, "result", "return payload")
            result_value(payload["result"], operation)
        else:
            object_keys(payload, "label state", "snapshot payload")
            require(payload["label"] == "exit", "invalid exit snapshot")
            state_values(payload["state"], operation, scan_length, case)
        by_id[event["id"]] = event
    require(set(by_id) == set(selected), "missing selected event")
    return by_id


def validate_order(order, events, registered):
    object_keys(order, "before independent", "comparison.order")
    require(isinstance(order["before"], list), "before must be an array")
    supported(order["independent"] == [], "foundation operations have no independent events")
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


def validate_randomness(case, root):
    value = case["randomness"]
    object_keys(value, "mode policy contexts choices", "randomness")
    if not randomness.sampling(case):
        supported(value == {"mode": "controlled-choices", "policy": None, "contexts": {}, "choices": []},
                  "this operation has no random input closure")
        return
    domain = logical.scalar(case["boundary"]["arguments"]["domain"], "text")
    require(domain in randomness.DOMAINS, "unknown random domain")
    if value["mode"] == "controlled-choices":
        require(value["policy"] is None and value["contexts"] == {}, "controlled choices cannot install generator state")
        randomness.choices(value["choices"])
        require(len(value["choices"]) <= 100000, "choice count exceeds adapter input bound")
    else:
        supported(value["mode"] == "implementation-replay", "unsupported random mode")
        require(value["policy"] == {"id": randomness.POLICY, "version": "2",
                                    "sha256": inventory.digest(root / randomness.POLICY_FILE)}, "random policy identity mismatch")
        require(value["choices"] == [], "implementation replay cannot inject choices")
        randomness.context_values(value["contexts"], domain)
        require(case["entry"]["state"] == {randomness.STATE: value["contexts"]}, "entry/random context disagreement")


def consumed_inputs(case):
    if case["boundary"]["operation"] == scripted.OPERATION:
        return scripted.consumed(case["expected"])
    return {"providers": 0, "choices": len(case["randomness"]["choices"]),
            "events": len(selected_events(case))}


def validate_script_fixture(case, registry, root, pins, directory):
    boundary = case["boundary"]
    require(set(pins) == {"F01", "F02", "C03", "X01", "X02"}, "script contract pins incomplete")
    require(boundary["level"] == "subsystem" and boundary["suppliedChildren"] == [scripted.PROVIDER] and
            boundary["start"] == "shared city workspace entry with supplied pipeline" and
            boundary["stop"] == "normal return or diagnostic stop with complete workspace snapshot", "script boundary mismatch")
    operation = next((row for row in registry["operations"] if row["id"] == scripted.OPERATION), None)
    require(operation and operation["owner"] == "F02" and operation["arguments"] == scripted.ARGUMENTS and
            operation["reads"] == operation["writes"] == list(scripted.STATES) and operation["result"] == "i32" and
            operation["suppliedChildren"] == [scripted.PROVIDER] and operation["callbacks"] == [scripted.OPERATION] and
            operation.get("eventSchema") == "shared-workspace-script-v1", "script adapter/registry mismatch")
    providers = {row["id"]: row for row in registry.get("providers", [])}
    require(scripted.PROVIDER in providers and providers[scripted.PROVIDER]["role"] == "supplied-child" and
            providers[scripted.PROVIDER]["outputs"] == list(scripted.STATES) and
            providers[scripted.PROVIDER]["callbacks"] == [scripted.OPERATION] and
            providers[scripted.PROVIDER]["arguments"] == {"city": "city-slot-or-null"} and
            providers[scripted.PROVIDER]["result"] == "i32", "unregistered script provider")
    state_registry = {row["id"]: row for row in registry["states"]}
    require(all(key in state_registry and state_registry[key]["valueType"] == kind for key, kind in scripted.STATES.items()), "script state registry mismatch")
    scripted.arguments(boundary["arguments"])
    object_keys(case["profile"], "domain safety restrictions deviations", "profile")
    require(case["profile"]["domain"] in ("nominal", "accepted-off-nominal", "restricted"), "unknown profile")
    name(case["profile"]["safety"], "safety")
    supported(case["profile"]["restrictions"] == case["profile"]["deviations"] == [], "script profile deviations unsupported")
    object_keys(case["entry"], "state retainedInputs blobs", "entry")
    supported(case["entry"]["retainedInputs"] == case["entry"]["blobs"] == [], "script retained-input/blob installation unsupported")
    scripted.state_values(case["entry"]["state"])
    validate_randomness(case, root)
    scripted.environment(case["environment"])
    events = scripted.validate_expected(case)
    comparison = case["comparison"]
    object_keys(comparison, "stateScope traceKinds order exclusions normalizations", "comparison")
    require(comparison["stateScope"] == "complete-boundary" and comparison["exclusions"] == comparison["normalizations"] == [], "script requires complete unnormalized closure")
    kinds = {event["kind"] for event in events.values()}
    require(isinstance(comparison["traceKinds"], list) and len(comparison["traceKinds"]) == len(kinds) and
            set(comparison["traceKinds"]) == kinds, "script trace kinds missing or duplicated")
    # External script order and synchronous nesting form a required chain here;
    # no independent private implementation events are registered.
    chronology = list(events)
    validate_order(comparison["order"], events, list(zip(chronology, chronology[1:])))
    evidence = case["evidence"]
    object_keys(evidence, "kind sources limitations", "evidence")
    require(evidence["kind"] == "specification-vector" and isinstance(evidence["sources"], list) and evidence["sources"], "script evidence missing")
    require(isinstance(evidence["limitations"], list) and evidence["limitations"], "supplied-child evidence limits required")
    for limitation in evidence["limitations"]:
        name(limitation, "evidence limitation")
    specs = {row["id"]: row for row in registry["specifications"]}
    for source in evidence["sources"]:
        object_keys(source, "file section sha256", "evidence source")
        resolved = checked_file(root, directory, source["file"], source["sha256"])
        require(resolved in {(root / specs[key]["path"]).resolve() for key in pins}, "evidence outside pinned specifications")
        inventory.anchor(root, f"{resolved.relative_to(root).as_posix()}#{source['section']}")
    object_keys(case["limits"], "events providerRequests hostMilliseconds", "limits")
    for key, value in case["limits"].items():
        metadata_integer(value, key, positive=True)


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
    boundary = case["boundary"]
    object_keys(boundary, "level operation start stop arguments suppliedChildren", "boundary")
    name(boundary["operation"], "operation")
    supported(boundary["operation"] in OPERATIONS, "operation has no executable adapter")
    op_id = boundary["operation"]
    if op_id == scripted.OPERATION:
        validate_script_fixture(case, registry, root, pins, path.parent)
        return case
    owner = op_id.split(".")[0]
    require(set(pins) == {"F01", "X01", "X02", owner}, "foundation contract pins incomplete")
    supported(boundary["level"] == ("subsystem" if op_id == session.OPERATION else "primitive")
              and boundary["suppliedChildren"] == [], "unsupported boundary or supplied children")
    start = ("fresh-game boundary with explicit projection inputs" if op_id == session.OPERATION else
             "semantic sampler entry with explicit random inputs" if randomness.sampling(case) else
             "pure mapping entry with explicit code argument" if owner == "F01" else
             "state primitive entry with complete registered inputs")
    require(boundary["start"] == start and
            boundary["stop"] == "normal return followed by exit snapshot", "unsupported start/stop boundary")
    arguments = OPERATIONS[boundary["operation"]]
    operation = next((row for row in registry["operations"] if row["id"] == boundary["operation"]), None)
    require(operation is not None and operation["owner"] == owner and
            operation["arguments"] == arguments and operation["result"] == RESULTS[op_id], "adapter/registry contract mismatch")
    registered_states = ({randomness.STATE: "random-contexts-v2"} if randomness.sampling(case) else STATES[op_id])
    if op_id == session.OPERATION:
        registered_states = {**registered_states, **session.EXCLUDED}
    require(operation["reads"] == list(registered_states) and
            operation["writes"] == ([randomness.STATE] if randomness.sampling(case) else WRITES[op_id]),
            "adapter/registry state closure mismatch")
    state_registry = {row["id"]: row for row in registry["states"]}
    require(all(key in state_registry and state_registry[key]["valueType"] == kind
                for key, kind in registered_states.items()), "adapter/registry state type mismatch")
    possible_events = set(EVENTS) | ({"root.sample"} if randomness.sampling(case) else set())
    require(isinstance(operation["dependencies"], list) and all(
        isinstance(edge, list) and len(edge) == 2 and all(isinstance(key, str) and key in possible_events for key in edge)
        for edge in operation["dependencies"]), "unknown registered dependency")
    require(all(operation[key] == [] for key in ("callbacks", "suppliedChildren", "acceptedFaults", "partialEffects", "independenceClaims"))
            and operation["invocation"] == "root" and len(operation["events"]) == len(possible_events) and
            set(operation["events"]) == possible_events and
            operation["stopBoundaries"] == ["normal return followed by exit snapshot"], "adapter cannot satisfy registry closure")
    if randomness.sampling(case):
        require(operation.get("conditionalEvents") == {"root.sample": "nonconstant decision"} and
                operation.get("randomStateModes") == ["implementation-replay"], "sampler conditional closure mismatch")
    object_keys(boundary["arguments"], " ".join(arguments), "arguments")
    values = {key: logical.scalar(boundary["arguments"][key], kind) for key, kind in arguments.items()}
    if boundary["operation"] == "F01.primaryModuloFromCode":
        require(0 <= values["code"] <= 32767 and values["n"] > 0, "primary mapping precondition")
    if op_id == "F01.primaryModulo":
        require(values["n"] > 0, "primary divisor must be positive")
    if op_id == "F02.reserveUnitSlot":
        require(values["owner"] < 8, "civilization owner outside 0..7")
    profile = case["profile"]
    object_keys(profile, "domain safety restrictions deviations", "profile")
    require(profile["domain"] in ("nominal", "accepted-off-nominal", "restricted"), "unknown profile")
    name(profile["safety"], "safety policy")
    supported(profile["restrictions"] == profile["deviations"] == [], "restricted/deviating comparisons not implemented")
    object_keys(case["entry"], "state retainedInputs blobs", "entry")
    supported(case["entry"]["retainedInputs"] == case["entry"]["blobs"] == [], "retained-input/blob installation not implemented")
    validate_randomness(case, root)
    state_values(case["entry"]["state"], op_id, case=case)
    scan_length = entry_scan_length(case)
    state_values(case["entry"]["state"], op_id, scan_length, case)
    object_keys(case["environment"], "protocol inputs unexpected", "environment")
    supported(case["environment"] == {"protocol": "scripted-v1", "inputs": [], "unexpected": "fail"}, "providers/reentry not implemented")
    expected = case["expected"]
    object_keys(expected, "outcome result state trace", "expected")
    supported(expected["outcome"] == "return", "only normal returns supported")
    state_values(expected["state"], op_id, scan_length, case)
    result_value(expected["result"], op_id)
    events = trace_events(expected["trace"], op_id, scan_length, case)
    require(events["root.enter"]["payload"] == {"operation": boundary["operation"], "parent": None, "arguments": boundary["arguments"]}, "entry trace does not match boundary")
    require(events["root.return"]["payload"]["result"] == expected["result"], "return trace/result disagreement")
    require(events["root.exit"]["payload"]["state"] == expected["state"], "exit trace/state disagreement")
    if "root.sample" in events:
        sample = events["root.sample"]["payload"]
        require(sample["arguments"] == {key: boundary["arguments"][key] for key in randomness.SAMPLERS[op_id][1]},
                "sample arguments disagree with operation boundary")
        require(sample["result"] == expected["result"], "sample/return disagreement")
    comparison = case["comparison"]
    object_keys(comparison, "stateScope traceKinds order exclusions normalizations", "comparison")
    if op_id == session.OPERATION:
        require(comparison["stateScope"] == "projection" and
                comparison["exclusions"] == session.EXCLUSIONS and comparison["normalizations"] == [],
                "fresh-game projection exclusions must be explicit and exact")
    else:
        supported(comparison["stateScope"] == "complete-boundary" and comparison["exclusions"] == comparison["normalizations"] == [], "projected/normalized comparisons not implemented")
    required_kinds = set(selected_events(case).values())
    require(isinstance(comparison["traceKinds"], list) and len(comparison["traceKinds"]) == len(required_kinds) and
            all(isinstance(kind, str) for kind in comparison["traceKinds"]) and set(comparison["traceKinds"]) == required_kinds, "mandatory trace kinds missing or duplicated")
    validate_order(comparison["order"], events,
                   [edge for edge in operation["dependencies"] if all(key in events for key in edge)])
    evidence = case["evidence"]
    object_keys(evidence, "kind sources limitations", "evidence")
    supported(evidence["kind"] in ("specification-vector", "implementation-regression"), "unsupported fixture provenance")
    require(isinstance(evidence["sources"], list) and evidence["sources"], "missing evidence sources")
    require(isinstance(evidence["limitations"], list), "limitations must be an array")
    for limitation in evidence["limitations"]:
        name(limitation, "evidence limitation")
    for source in evidence["sources"]:
        object_keys(source, "file section sha256", "evidence source")
        resolved = checked_file(root, path.parent, source["file"], source["sha256"])
        allowed_sources = {(root / specs[key]["path"]).resolve() for key in pins}
        if evidence["kind"] == "implementation-regression" and randomness.sampling(case):
            allowed_sources.add((root / randomness.POLICY_FILE).resolve())
        require(resolved in allowed_sources, "evidence outside pinned contracts")
        name(source["section"], "source section")
        inventory.anchor(root, f"{resolved.relative_to(root).as_posix()}#{source['section']}")
    object_keys(case["limits"], "events providerRequests hostMilliseconds", "limits")
    for key, value in case["limits"].items():
        metadata_integer(value, key, positive=True)
    return case


def compare(case, actual):
    """X02 §6: typed equality, exact semantic IDs and required dependencies."""
    object_keys(actual, "outcome result state trace", "adapter output")
    operation = case["boundary"]["operation"]
    if operation == scripted.OPERATION:
        return scripted.compare(case, actual)
    result_value(actual["result"], operation)
    state_values(actual["state"], operation, entry_scan_length(case), case)
    events = trace_events(actual["trace"], operation, entry_scan_length(case), case)
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
                      randomness=case["randomness"],
                      comparison=case["comparison"], limits=case["limits"])
    except UnsupportedFixture as error:
        return {**report, "status": "unsupported-fixture", "error": str(error)}
    except (ValueError, KeyError, TypeError, OSError, RecursionError) as error:
        return {**report, "status": "invalid-fixture", "error": str(error)}
    is_script = case["boundary"]["operation"] == scripted.OPERATION
    if case["limits"]["events"] < (2 if is_script else len(selected_events(case))):
        return {**report, "status": "budget-exhausted", "error": "required events exceed event limit"}
    operation = case["boundary"]["operation"]
    if is_script:
        slot, full = scripted.arguments(case["boundary"]["arguments"])
        args = [str(slot), str(int(full))]
    else:
        args = [case["boundary"]["arguments"][key]["value"] for key in OPERATIONS[operation]]
    state = state_values(case["entry"]["state"], operation, entry_scan_length(case), case)
    # The adapter receives only actual inputs. State travels on stdin to avoid
    # platform command-line limits; field order belongs to this test protocol.
    words = []
    for key in ([] if is_script or operation == session.OPERATION else STATES[operation]):
        value = state[key]
        if isinstance(value, list):
            words.extend(str(item) for item in value)
        elif type(value) is bool:
            words.append("1" if value else "0")
        else:
            words.append(str(value))
    if randomness.sampling(case):
        words = randomness.input_words(case)
    elif is_script:
        words = scripted.input_words(case)
    elif operation == session.OPERATION:
        words = session.input_words(case)
    try:
        process = subprocess.run([*command, operation, *args], capture_output=True, text=True,
                                 encoding="utf-8", input="\n".join(words) + ("\n" if words else ""),
                                 timeout=case["limits"]["hostMilliseconds"] / 1000)
        if process.returncode != 0:
            return {**report, "status": "adapter-failure", "exitCode": process.returncode, "error": process.stderr}
        actual = json.loads(process.stdout, object_pairs_hook=inventory.unique_object,
                            parse_constant=reject_nonfinite)
        mismatch = compare(case, actual)
        status = "failed" if mismatch else "passed"
        if actual["outcome"] in ("unexpected-input", "budget-exhausted", "unavailable-evidence"):
            status = actual["outcome"]
        consumed = scripted.consumed(actual) if is_script else {**consumed_inputs(case), "events": len(actual["trace"])}
        return {**report, "status": status, "firstMismatch": mismatch, "actual": actual, "consumed": consumed}
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
    report = {"format": "openciv2-conformance-run/2", "runnerScope": SCOPE,
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
        with args.report.open("w", encoding="utf-8", newline="\n") as output:
            output.write(json.dumps(report, indent=2) + "\n")
    return 0 if all(case["status"] == "passed" for case in report["cases"]) else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, OSError) as error:
        print(f"Conformance runner: {error}", file=sys.stderr)
        sys.exit(2)
