"""X02 §3 canonical logical values. No game operation or expectation generation."""
import re


def require(condition, message):
    if not condition:
        raise ValueError(message)


def keys(value, required, optional=()):
    require(isinstance(value, dict), "logical value must be an object")
    require(set(required) <= set(value) <= set(required) | set(optional),
            "logical value has missing or unknown fields")


def text(value):
    require(isinstance(value, str), "text value must be Unicode text")
    try:
        value.encode("utf-8")
    except UnicodeEncodeError as error:
        raise ValueError("text contains an unpaired surrogate") from error
    return value


def decimal(value, signed, bits):
    require(isinstance(value, str) and len(value) <= 21 and
            re.fullmatch(r"0|-?[1-9][0-9]*", value) is not None, "noncanonical integer")
    result = int(value)
    low, high = (-(1 << (bits - 1)), (1 << (bits - 1)) - 1) if signed else (0, (1 << bits) - 1)
    require(low <= result <= high, "integer out of range")
    return result


def integer(value, kind):
    keys(value, ("$type", "value"))
    require(value["$type"] == kind and re.fullmatch(r"[iu](8|16|32|64)", kind),
            "integer type mismatch")
    return decimal(value["value"], kind[0] == "i", int(kind[1:]))


def scalar(value, kind):
    if re.fullmatch(r"[iu](8|16|32|64)", kind):
        return integer(value, kind)
    if kind == "f64":
        keys(value, ("$type", "bits"))
        require(value["$type"] == kind and isinstance(value["bits"], str) and
                re.fullmatch(r"[0-9a-f]{16}", value["bits"]), "invalid binary64 bits")
        return value["bits"]
    keys(value, ("$type", "value"))
    require(value["$type"] == kind, "logical scalar type mismatch")
    if kind == "text":
        return text(value["value"])
    require(kind == "bool" and type(value["value"]) is bool, "boolean type mismatch")
    return value["value"]


def logical(value, blobs=None, *, allow_unavailable=False, depth=0):
    """Validate recursively without normalizing order, bytes, types or f64 bits.

    Blob IDs map to validated descriptor lengths; fetching/hash validation belongs
    to the bundle loader. Consumers must supply the owner-specific ref domains.
    """
    require(depth <= 128, "logical value nesting exceeds harness bound")
    if isinstance(value, list):
        for item in value:
            logical(item, blobs, allow_unavailable=allow_unavailable, depth=depth + 1)
        return
    require(isinstance(value, dict), "game scalars must be typed values")
    if "$type" not in value:
        for key, item in value.items():
            text(key)
            logical(item, blobs, allow_unavailable=allow_unavailable, depth=depth + 1)
        return
    kind = value["$type"]
    require(isinstance(kind, str), "logical type must be text")
    if re.fullmatch(r"[iu](8|16|32|64)", kind) or kind in ("text", "bool", "f64"):
        scalar(value, kind)
    elif kind == "null":
        keys(value, ("$type",))
    elif kind == "bytes":
        keys(value, ("$type", "hex"))
        require(isinstance(value["hex"], str) and re.fullmatch(r"(?:[0-9a-f]{2})*", value["hex"]),
                "noncanonical byte string")
    elif kind == "blob":
        keys(value, ("$type", "id", "offset", "length"))
        require(isinstance(value["id"], str) and blobs is not None and value["id"] in blobs,
                "unknown blob reference")
        offset, length = (decimal(value[key], False, 64) for key in ("offset", "length"))
        require(offset <= blobs[value["id"]] and length <= blobs[value["id"]] - offset,
                "blob view exceeds backing")
    elif kind == "ref":
        keys(value, ("$type", "space", "id"), ("slot", "token"))
        require(text(value["space"]) and text(value["id"]), "empty reference identity")
        for field in ("slot", "token"):
            if field in value:
                require(isinstance(value[field], dict), "reference slot/token must be typed")
                field_kind = value[field].get("$type")
                require(isinstance(field_kind, str) and re.fullmatch(r"[iu](8|16|32|64)", field_kind),
                        "reference slot/token must be an integer")
                integer(value[field], field_kind)
    elif kind == "unavailable":
        keys(value, ("$type", "reason"), ("evidenceId",))
        require(allow_unavailable and text(value["reason"]), "unavailable input cannot be consumed")
        if "evidenceId" in value:
            require(text(value["evidenceId"]), "empty evidence identity")
    else:
        raise ValueError("unknown logical value type")
