"""X02 §2 choice-input validation and policy v2 replay context schema."""
import struct
from conformance_values import integer, scalar, keys, require, text

DOMAINS = ("gameplay", "presentation-content", "cosmetic")
SAMPLERS = {
    "F01.primaryCode": ("primary-code", {}),
    "F01.primaryModulo": ("primary-modulo", {"n": "i32"}),
    "F01.secondaryUnit": ("secondary-unit", {}),
    "F01.secondaryRange": ("secondary-range", {"lo": "i32", "hi": "i32"}),
}
POLICY = "oc2-mt19937-64"
POLICY_FILE = "docs/random-policy.md"
STATE = "F01.randomContexts"


def bits(value):
    return struct.pack(">d", value).hex()


def signed32(value):
    return (value + (1 << 31)) % (1 << 32) - (1 << 31)


def mapped(law, arguments, code):
    """Validate a supplied choice result by F01, never compute operation output."""
    require(isinstance(law, str) and law in {row[0] for row in SAMPLERS.values()}, "unknown sampling law")
    schema = next(row[1] for row in SAMPLERS.values() if row[0] == law)
    keys(arguments, schema)
    args = {name: integer(arguments[name], kind) for name, kind in schema.items()}
    if law.startswith("primary-"):
        value = integer(code, "i32")
        require(0 <= value <= 32767, "primary code outside 0..32767")
        if law == "primary-modulo":
            require(args["n"] > 0, "primary divisor must be positive")
            value %= args["n"]
        return {"$type": "i32", "value": str(value)}
    value = integer(code, "u32")
    fraction = value / 4294967295.0
    if law == "secondary-unit":
        return {"$type": "f64", "bits": bits(fraction)}
    lo, hi = sorted((args["lo"], args["hi"]))
    result = lo if lo == hi else signed32(int(fraction *
        (signed32(hi - lo) + float.fromhex("0x1.ffffde7210be9p-1"))) + lo)
    return {"$type": "i32", "value": str(result)}


def choices(records):
    require(isinstance(records, list), "choices must be an array")
    identifiers, decision_keys = set(), set()
    for record in records:
        keys(record, ("id", "site", "call", "attempt", "law", "arguments", "code", "result"))
        for field in ("id", "site", "call"):
            require(text(record[field]), "empty choice identity")
        attempt = record["attempt"]
        require(type(attempt) is int and 0 <= attempt <= 2147483647, "invalid choice attempt")
        key = (record["site"], record["call"], attempt)
        require(record["id"] not in identifiers and key not in decision_keys, "duplicate choice identity/key")
        identifiers.add(record["id"])
        decision_keys.add(key)
        require(record["result"] == mapped(record["law"], record["arguments"], record["code"]),
                "choice result disagrees with F01 mapping")
    return records


def context_key(domain):
    require(domain in DOMAINS, "unknown random domain")
    return domain + "/fixture"


def context_values(contexts, domain):
    key = context_key(domain)
    keys(contexts, (key,))
    value = contexts[key]
    keys(value, ("domain", "name", "seed", "draws"))
    require(scalar(value["domain"], "text") == domain and
            scalar(value["name"], "text") == "fixture", "random context identity mismatch")
    return integer(value["seed"], "u64"), integer(value["draws"], "u64")


def sampling(case):
    return case["boundary"]["operation"] in SAMPLERS


def nonconstant(operation, arguments):
    return not ((operation == "F01.primaryModulo" and integer(arguments["n"], "i32") == 1) or
                (operation == "F01.secondaryRange" and arguments["lo"] == arguments["hi"]))


def input_words(case):
    """Only inputs go to C++; supplied mapped results are already validated here."""
    mode = case["randomness"]["mode"]
    domain = scalar(case["boundary"]["arguments"]["domain"], "text")
    words = [mode, str(DOMAINS.index(domain))]
    if mode == "implementation-replay":
        seed, draws = context_values(case["randomness"]["contexts"], domain)
        return words + [str(seed), str(draws)]
    records = case["randomness"]["choices"]
    words.append(str(len(records)))
    for record in records:
        law_index = next(i for i, row in enumerate(SAMPLERS.values()) if row[0] == record["law"])
        words.extend([record["site"].encode("utf-8").hex(), record["call"].encode("utf-8").hex(),
                      str(record["attempt"]), str(law_index)])
        schema = list(SAMPLERS.values())[law_index][1]
        words.extend(record["arguments"][key]["value"] for key in schema)
        words.append(record["code"]["value"])
    return words
