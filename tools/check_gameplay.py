#!/usr/bin/env python3
"""Load and validate first-wave gameplay data offline; does not run the game."""

import argparse
from decimal import Decimal
import hashlib
import json
from math import gcd
from pathlib import Path
import re
import sys

from check_content_schemas import ROOT, SchemaSet, SchemaError, decode_json, describe, read_json

COMPONENTS = ("calendar", "spaceship", "governments", "difficulty", "combat", "scoring")
TARGET = "classic-mge-5.4.0f"
LEDGER = "docs/content/gameplay-defaults.json"


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def check_document(document, schemas, expected):
    if not isinstance(document, dict) or document.get("format") != expected:
        raise ValueError(f"expected document format {expected}")
    errors = schemas.errors(document)
    if errors:
        raise ValueError(describe(errors[0]))


def contained(root, relative):
    root = Path(root).resolve()
    target = (root / relative).resolve()
    if not target.is_relative_to(root):
        raise ValueError(f"path leaves selected root: {relative}")
    return target


def read_descriptor(root, descriptor):
    # Descriptor shape/path syntax is checked by its parent schema first.
    path = contained(root, descriptor["path"])
    raw = path.read_bytes()
    if digest(raw) != descriptor["sha256"]:
        raise ValueError(f"{descriptor['path']}: file hash mismatch")
    return path, decode_json(raw)


def normalize(value):
    """Canonical integer/rational operands, after schema validation."""
    if isinstance(value, dict):
        result = {key: normalize(item) for key, item in value.items()}
        if set(result) == {"numerator", "denominator"}:
            factor = gcd(result["numerator"], result["denominator"])
            result = {"numerator": result["numerator"] // factor,
                      "denominator": result["denominator"] // factor}
        return result
    if isinstance(value, list):
        return [normalize(item) for item in value]
    if isinstance(value, Decimal):
        if not value.is_finite() or value != value.to_integral_value():
            raise ValueError("gameplay values must have exact integer operands")
        return int(value)
    return value


def semantic_values(components):
    return {name: normalize({key: value for key, value in document.items() if key != "extensions"})
            for name, document in components.items()}


def fingerprint(components):
    envelope = {"format": "openciv2-gameplay-values/1", "target": TARGET,
                "components": semantic_values(components)}
    raw = json.dumps(envelope, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode("ascii")
    return digest(raw)


def check_semantics(components):
    calendar = components["calendar"]
    for name, profile in calendar["profiles"].items():
        previous_end = None
        for period in profile["periods"]:
            if previous_end is not None and period["startYear"] != previous_end:
                raise ValueError(f"calendar/{name}: discontinuous period boundary")
            previous_end = period["startYear"] + period["turnCount"] * period["yearStep"]
            if not -(2**31) <= previous_end < 2**31:
                raise ValueError(f"calendar/{name}: period endpoint outside authoring domain")
    retirement = calendar["retirement"]
    if retirement["warningYear"] >= retirement["endYear"]:
        raise ValueError("calendar: retirement warning must precede end year")
    for name, multiplier in components["combat"]["defenseMultipliers"].items():
        doubled = 2 * multiplier["numerator"]
        denominator = multiplier["denominator"]
        if doubled % denominator or not 1 <= doubled // denominator <= 2147483647:
            raise ValueError(f"combat/{name}: doubled defense multiplier must be a positive authoring integer")


def load_gameplay(manifest_path, schemas, baseline_fingerprint, require_classic=False):
    manifest_path = Path(manifest_path).resolve()
    manifest = read_json(manifest_path)
    return load_gameplay_document(manifest, manifest_path.parent, schemas, baseline_fingerprint, require_classic)


def load_gameplay_document(manifest, directory, schemas, baseline_fingerprint, require_classic=False):
    check_document(manifest, schemas, "openciv2-gameplay/1")
    components = {}
    seen = set()
    for name in COMPONENTS:
        path, document = read_descriptor(directory, manifest["components"][name])
        if path in seen:
            raise ValueError("gameplay components must use distinct files")
        seen.add(path)
        check_document(document, schemas, "openciv2-" + name + "/1")
        components[name] = document
    components = semantic_values(components)
    check_semantics(components)
    actual = fingerprint(components)
    classic_values = actual == baseline_fingerprint
    if (manifest["mode"] == "classic" or require_classic) and not classic_values:
        raise ValueError("changed built-in gameplay values require custom mode and cannot pass the classic-values gate")
    return {"manifest": manifest, "components": components,
            "semanticSha256": actual, "classicValues": classic_values}


def pointer_value(document, pointer):
    value = document
    for part in pointer.split("/")[1:]:
        part = part.replace("~1", "/").replace("~0", "~")
        try:
            value = value[int(part)] if isinstance(value, list) else value[part]
        except (ValueError, KeyError, IndexError, TypeError) as exc:
            raise ValueError(f"unresolved provenance pointer: {pointer}") from exc
    return value


def leaf_paths(value, prefix=""):
    if isinstance(value, dict):
        for key, item in value.items():
            if prefix == "" and key in ("format", "extensions"):
                continue
            escaped = key.replace("~", "~0").replace("/", "~1")
            yield from leaf_paths(item, prefix + "/" + escaped)
    elif isinstance(value, list):
        for index, item in enumerate(value):
            yield from leaf_paths(item, prefix + "/" + str(index))
    else:
        yield prefix


def heading_anchors(text):
    # The source pins use ordinary ATX Markdown headings without duplicate names.
    for line in text.splitlines():
        if re.match(r"^#{1,6} ", line):
            title = re.sub(r"^#+ | +#+$", "", line).lower()
            yield re.sub(r"[^\w\- ]", "", title).replace(" ", "-")


def check_repository(root, schemas):
    """Audit shipped defaults against the trusted, source-controlled ledger."""
    root = Path(root)
    ledger = read_json(root / LEDGER)
    check_document(ledger, schemas, "openciv2-gameplay-defaults/1")
    baseline = ledger["baseline"]
    path, manifest = read_descriptor(root, baseline["manifest"])
    check_document(manifest, schemas, "openciv2-gameplay/1")
    if (manifest.get("id"), manifest.get("revision"), manifest.get("mode")) != (baseline["id"], baseline["revision"], "classic"):
        raise ValueError("shipped gameplay identity/revision/mode differs from its ledger")
    result = load_gameplay_document(manifest, path.parent, schemas, baseline["semanticSha256"], True)
    specifications = {}
    for spec in ledger["specifications"]:
        if spec["id"] in specifications:
            raise ValueError("duplicate provenance specification")
        spec_path = contained(root, spec["path"])
        if not spec["path"].startswith("docs/algorithms/" + spec["id"] + "-"):
            raise ValueError("specification identity/path mismatch")
        raw = spec_path.read_bytes()
        if digest(raw) != spec["sha256"]:
            raise ValueError(f"{spec['id']}: source pin changed; review gameplay defaults and provenance")
        specifications[spec["id"]] = set(heading_anchors(raw.decode("utf-8")))
    coverage = {name: set() for name in COMPONENTS}
    used_specs = set()
    seen_bindings = set()
    for binding in ledger["bindings"]:
        name, pointer = binding["component"], binding["pointer"]
        if (name, pointer) in seen_bindings:
            raise ValueError("duplicate gameplay provenance binding")
        seen_bindings.add((name, pointer))
        pointer_value(result["components"][name], pointer)
        for source in binding["sources"]:
            spec_id, separator, anchor = source.partition("#")
            if not separator or anchor not in specifications.get(spec_id, set()):
                raise ValueError(f"unknown provenance source section: {source}")
            used_specs.add(spec_id)
        coverage[name].update(leaf for leaf in leaf_paths(result["components"][name])
                              if leaf == pointer or leaf.startswith(pointer + "/"))
    for name in COMPONENTS:
        if coverage[name] != set(leaf_paths(result["components"][name])):
            raise ValueError(f"{name}: gameplay values missing source bindings")
    if used_specs != set(specifications):
        raise ValueError("unused gameplay specification pin")
    return ledger, result


def load_from_content_pack(path, root, schemas, ledger, require_classic=False):
    """Resolve only gameplay from a root pack; not a full pack/overlay loader."""
    path = Path(path).resolve()
    pack = read_json(path)
    check_document(pack, schemas, "openciv2-content-pack/1")
    if pack["base"] is not None:
        raise ValueError("resolve content-pack inheritance before validating its gameplay selection")
    selection = pack["gameplay"]
    baseline = ledger["baseline"]
    if "builtin" in selection:
        manifest_path, manifest = read_descriptor(root, baseline["manifest"])
    else:
        manifest_path, manifest = read_descriptor(path.parent, selection)
    return load_gameplay_document(manifest, manifest_path.parent, schemas,
                                  baseline["semanticSha256"], require_classic)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--manifest", type=Path, help="explicit gameplay manifest")
    group.add_argument("--content-pack", type=Path, help="check only a root content pack's gameplay selection")
    parser.add_argument("--require-classic", action="store_true", help="reject changed built-in values, including custom mode")
    args = parser.parse_args(argv)
    try:
        schemas = SchemaSet(args.root / "docs/content/schemas")
        ledger, result = check_repository(args.root, schemas)
        if args.manifest:
            result = load_gameplay(args.manifest, schemas, ledger["baseline"]["semanticSha256"], args.require_classic)
        elif args.content_pack:
            result = load_from_content_pack(args.content_pack, args.root, schemas, ledger, args.require_classic)
        status = "classic values" if result["classicValues"] else "custom values; original interoperability is not supported"
        print(f"Validated six gameplay components ({status}).")
        print(f"Semantic SHA-256: {result['semanticSha256']}")
        print("Data validation only; gameplay consumers, full content references and original-peer execution remain unimplemented.")
    except (OSError, ValueError, ArithmeticError, RecursionError, SchemaError) as exc:
        print(f"gameplay check failed: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
