"""X02 fresh-game projection: input validation/transport, no expected results."""
import conformance_values as logical

OPERATION = "F02.prepareNewGame"
STATES = {
    "F02.nextUnitToken": "u32", "F02.nextCityToken": "u32",
    "F02.unitEnd": "u32", "F02.cityEnd": "u32",
    "F02.unitDamage": "bytes", "F02.cityShields": "i16[]",
    "F02.futureTechnologies": "bytes", "F02.peaceTurns": "i16",
    "F02.goalRoles": "bytes", "F02.leaderCityNameCursors": "bytes",
    "F02.resumeEvent": "i16", "F02.passwords": "text[]",
    "F02.mapAllocated": "bool", "F02.fallbackRegion": "u8",
    "F02.textLine": "text", "F02.preferenceGameOptions": "u32",
    "F02.preferenceTimer": "i16", "F02.doubleMovementApplied": "bool",
    "F02.previousClientValid": "bool",
}
LENGTHS = {"F02.unitDamage": 2048, "F02.futureTechnologies": 8,
           "F02.goalRoles": 512, "F02.leaderCityNameCursors": 21}
PRESERVED = {"F02.nextUnitToken", "F02.nextCityToken", "F02.preferenceGameOptions",
             "F02.preferenceTimer"}
OWNER = "docs/algorithms/F02-game-state-and-invariants.md#2-state-layers-and-lifetimes"
EXCLUSIONS = [
    {"surface": "state", "selector": "F02.otherFreshWorldState", "owner": OWNER,
     "reason": "This projection observes every unit damage and city shield slot, all goal roles and listed counters, not the remaining world fields enumerated in the registry."},
    {"surface": "state", "selector": "F02.otherFreshProcessState", "owner": OWNER,
     "reason": "Rule definitions, text/name storage and remaining preference fields have separate content and world-entry tests; this projection only observes the listed cursors, flags, substitutions and preferences."},
    {"surface": "state", "selector": "F02.otherFreshSessionState", "owner": OWNER,
     "reason": "The admitted runtime is quiescent. Pending-operation and transport-output rejection, RNG preservation and identity generations have separate runtime tests."},
]
EXCLUDED = {item["selector"]: "documented-record" for item in EXCLUSIONS}
WRITES = [key for key in STATES if key not in PRESERVED] + list(EXCLUDED)


def state_values(state):
    logical.require(isinstance(state, dict) and set(state) == set(STATES),
                    "fresh-game state projection has missing or unknown fields")
    result = {}
    for key, kind in STATES.items():
        value = state[key]
        if kind == "bytes":
            logical.logical(value)
            logical.require(isinstance(value, dict) and value.get("$type") == "bytes" and
                            len(value["hex"]) == LENGTHS[key] * 2,
                            "fresh-game byte array has wrong type or length")
            result[key] = value["hex"]
        elif kind == "text[]":
            logical.require(isinstance(value, list) and len(value) == 8,
                            "fresh-game passwords must cover all eight civilizations")
            result[key] = [logical.scalar(item, "text") for item in value]
            logical.require(all(len(item.encode("utf-8")) <= 1024 for item in result[key]),
                            "fresh-game password exceeds harness bound")
        elif kind == "i16[]":
            logical.require(isinstance(value, list) and len(value) == 256,
                            "fresh-game city shield array must cover all 256 slots")
            result[key] = [logical.integer(item, "i16") for item in value]
        else:
            result[key] = logical.scalar(value, kind)
    logical.require(result["F02.unitEnd"] <= 2048 and result["F02.cityEnd"] <= 256,
                    "fresh-game scan bound exceeds capacity")
    logical.require(len(result["F02.textLine"].encode("utf-8")) <= 65536,
                    "fresh-game text exceeds harness bound")
    return result


def input_words(case):
    values = state_values(case["entry"]["state"])
    words = []
    for key, kind in STATES.items():
        value = values[key]
        if kind == "text":
            words.append(value.encode("utf-8").hex() or "-")
        elif kind == "text[]":
            words.extend(item.encode("utf-8").hex() or "-" for item in value)
        elif kind == "bool":
            words.append(str(int(value)))
        elif isinstance(value, list):
            words.extend(map(str, value))
        else:
            words.append(str(value))
    return words
