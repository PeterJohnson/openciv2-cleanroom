#!/usr/bin/env python3
"""Check native content document structure; never load a pack or fetch schemas."""

import argparse
from collections import Counter
from decimal import Decimal
import json
from pathlib import Path
import sys

try:
    from jsonschema import Draft202012Validator, validators
    from jsonschema.exceptions import SchemaError
    from referencing import Registry, Resource
    from referencing.exceptions import Unresolvable
except ModuleNotFoundError as exc:
    raise SystemExit(
        "Schema checks need the developer dependencies: "
        "python -m pip install -r tools/requirements-schemas.txt"
    ) from exc

ROOT = Path(__file__).resolve().parents[1]
DRAFT = "https://json-schema.org/draft/2020-12/schema"
SCHEMA_BASE = "https://openciv2.invalid/schemas/content/v1/"


def read_json(path):
    """Preserve exact JSON numbers and reject ambiguous/non-Unicode input."""
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ValueError(f"duplicate object key: {key!r}")
            result[key] = value
        return result

    def constant(value):
        raise ValueError(f"non-JSON number: {value}")

    def check_strings(value):
        if isinstance(value, str):
            if any(0xD800 <= ord(character) <= 0xDFFF for character in value):
                raise ValueError("unpaired Unicode surrogate")
        elif isinstance(value, dict):
            for key, item in value.items():
                check_strings(key)
                check_strings(item)
        elif isinstance(value, list):
            for item in value:
                check_strings(item)

    value = json.loads(
        Path(path).read_text(encoding="utf-8"), object_pairs_hook=pairs,
        parse_float=Decimal, parse_constant=constant,
    )
    check_strings(value)
    return value


def is_integer(checker, value):
    # JSON Schema's integer is mathematical: 2, 2.0 and 2e0 are equivalent.
    # Decimal avoids turning a precise fractional input into an integer float.
    return (
        isinstance(value, int) and not isinstance(value, bool)
    ) or (
        isinstance(value, Decimal) and value.is_finite()
        and value == value.to_integral_value()
    )


ContentValidator = validators.extend(
    Draft202012Validator,
    type_checker=Draft202012Validator.TYPE_CHECKER.redefine("integer", is_integer),
)


def references(value):
    if isinstance(value, dict):
        if "$ref" in value:
            yield value["$ref"]
        for item in value.values():
            yield from references(item)
    elif isinstance(value, list):
        for item in value:
            yield from references(item)


class SchemaSet:
    def __init__(self, directory):
        self.schemas = {}
        self.formats = {}
        for path in sorted(Path(directory).glob("*.schema.json")):
            schema = read_json(path)
            if schema.get("$schema") != DRAFT:
                raise ValueError(f"{path.name}: expected Draft 2020-12")
            if schema.get("$id") != SCHEMA_BASE + path.name:
                raise ValueError(f"{path.name}: unexpected schema identity")
            Draft202012Validator.check_schema(schema)
            self.schemas[schema["$id"]] = schema
            if "properties" in schema:
                name = schema["properties"]["format"]["const"]
                if name in self.formats:
                    raise ValueError(f"duplicate format schema: {name}")
                self.formats[name] = schema
        if not self.formats:
            raise ValueError("no content schemas found")
        # Registry's default retrieval rejects unknown resources; no HTTP/file I/O.
        self.registry = Registry().with_resources(
            (key, Resource.from_contents(schema))
            for key, schema in self.schemas.items()
        )
        for key, schema in self.schemas.items():
            for reference in references(schema):
                try:
                    self.registry.resolver(key).lookup(reference)
                except Unresolvable as exc:
                    raise ValueError(f"{key}: unresolved schema reference {reference}") from exc

    def errors(self, document):
        name = document.get("format") if isinstance(document, dict) else None
        if not isinstance(name, str) or name not in self.formats:
            raise ValueError(f"unknown or missing content format: {name!r}")
        validator = ContentValidator(self.formats[name], registry=self.registry)
        try:
            return list(validator.iter_errors(document))
        except ArithmeticError as exc:
            raise ValueError("numeric value cannot satisfy the schema's arithmetic constraints") from exc


def signature(error):
    return (tuple(error.absolute_path), error.validator, tuple(error.absolute_schema_path))


def expected_fragment_errors(filename):
    """Exact omissions in the two authored fragments, not a permissive schema."""
    expected = []
    if filename == "rules.fragment.json":
        for collection in (
            "advances", "improvements", "units", "terrain", "governments", "tribes",
            "commodities", "orders", "difficulties", "diplomaticAttitudes", "wonderObsolescence",
        ):
            expected.append(((collection,), "minItems", ("properties", collection, "minItems")))
        expected.append((("terrain",), "minContains", ("properties", "terrain", "contains")))
    elif filename == "council.en.json":
        opinion_path = ("eras", 0, "opinions")
        opinion_schema = ("properties", "eras", "items", "properties", "opinions")
        expected.append((opinion_path, "minItems", (*opinion_schema, "minItems")))
        # Only military/land-capability-lag (coverage index 1) is supplied.
        for index in range(34):
            if index != 1:
                expected.append((opinion_path, "contains", (*opinion_schema, "allOf", index, "contains")))
        expected.append((("eras",), "minItems", ("properties", "eras", "minItems")))
        for index in (1, 2):
            expected.append((("eras",), "contains", ("properties", "eras", "allOf", index, "contains")))
    return Counter(expected)


def check_examples(schemas, root):
    directory = Path(root) / "docs/content"
    paths = sorted((directory / "examples").glob("*.json")) + [directory / "text-resources.json"]
    if len(paths) != 12:
        raise ValueError("expected eleven authored example files and the text-resource inventory")
    for path in paths:
        errors = schemas.errors(read_json(path))
        actual = Counter(signature(error) for error in errors)
        expected = expected_fragment_errors(path.name)
        if actual != expected:
            details = "; ".join(describe(error) for error in errors[:5]) or "no errors"
            raise ValueError(f"{path.name}: unexpected validation result: {details}")
    return "Validated 13 schemas, 9 complete example documents and the inventory; 2 fragments rejected as expected."


def describe(error):
    pointer = "/" + "/".join(str(part).replace("~", "~0").replace("/", "~1") for part in error.absolute_path)
    return f"{pointer}: {error.message} [{error.validator}]"


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("files", nargs="*", type=Path, help="validate these files instead of the repository examples")
    parser.add_argument("--root", type=Path, default=ROOT, help="repository containing docs/content/schemas")
    args = parser.parse_args(argv)
    try:
        schemas = SchemaSet(args.root / "docs/content/schemas")
        if not args.files:
            if len(schemas.schemas) != 13:
                raise ValueError("expected twelve file-format schemas and shared definitions")
            print(check_examples(schemas, args.root))
        else:
            failures = 0
            for path in args.files:
                errors = schemas.errors(read_json(path))
                if errors:
                    failures += 1
                    for error in errors:
                        print(f"{path}: {describe(error)}", file=sys.stderr)
                else:
                    print(f"{path}: document structure valid (pack semantics not checked)")
            return int(bool(failures))
    except (OSError, ValueError, RecursionError, SchemaError) as exc:
        print(f"content schema check failed: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
