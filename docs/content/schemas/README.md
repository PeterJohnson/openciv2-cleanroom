# Native content JSON Schemas

These [JSON Schema Draft 2020-12](https://json-schema.org/draft/2020-12/json-schema-validation)
documents describe the unreleased version 1 [content contract](../data-format.md).
They validate document structure and expressible field constraints. Passing a
schema does **not** establish a complete playable pack or classic compatibility.
The C++ loaders now admit gameplay bundles, pack selections and
[typed native rules](../../m1-rule-content.md); remaining component semantics and
session admission are implementation work. The schemas and format specification
evolve together until publication.

## Schema inventory

Each file schema selects exactly one `format` value, shown below without the
common `openciv2-` prefix and `/1` suffix.

| Format | Schema | Example / data |
| --- | --- | --- |
| `content-pack` | [content-pack.schema.json](content-pack.schema.json) | [content.json](../examples/content.json) |
| `rules` | [rules.schema.json](rules.schema.json) | [rules.fragment.json](../examples/rules.fragment.json), intentionally incomplete |
| `city-names` | [city-names.schema.json](city-names.schema.json) | [city-names.json](../examples/city-names.json) |
| `city-preferences` | [city-preferences.schema.json](city-preferences.schema.json) | [city-preferences.json](../examples/city-preferences.json) |
| `text` | [text.schema.json](text.schema.json) | [text.en.json](../examples/text.en.json) |
| `menus` | [menus.schema.json](menus.schema.json) | [menus.en.json](../examples/menus.en.json) |
| `documents` | [documents.schema.json](documents.schema.json) | [documents.en.json](../examples/documents.en.json) |
| `council` | [council.schema.json](council.schema.json) | [council.en.json](../examples/council.en.json), intentionally incomplete |
| `encyclopedia` | [encyclopedia.schema.json](encyclopedia.schema.json) | [encyclopedia.en.json](../examples/encyclopedia.en.json) |
| `events` | [events.schema.json](events.schema.json) | [events.json](../examples/events.json) |
| `classic-compatibility` | [classic-compatibility.schema.json](classic-compatibility.schema.json) | [classic-compatibility.json](../examples/classic-compatibility.json) |
| `gameplay` | [gameplay.schema.json](gameplay.schema.json) | [Shipped manifest](../../../data/gameplay/classic-mge-5.4.0f/gameplay.json) |
| `calendar` | [calendar.schema.json](calendar.schema.json) | [Classic periods](../../../data/gameplay/classic-mge-5.4.0f/calendar.json) |
| `spaceship` | [spaceship.schema.json](spaceship.schema.json) | [Classic parts](../../../data/gameplay/classic-mge-5.4.0f/spaceship.json) |
| `governments` | [governments.schema.json](governments.schema.json) | [Classic policy](../../../data/gameplay/classic-mge-5.4.0f/governments.json) |
| `difficulty` | [difficulty.schema.json](difficulty.schema.json) | [Classic modifiers](../../../data/gameplay/classic-mge-5.4.0f/difficulty.json) |
| `combat` | [combat.schema.json](combat.schema.json) | [Classic bonuses](../../../data/gameplay/classic-mge-5.4.0f/combat.json) |
| `scoring` | [scoring.schema.json](scoring.schema.json) | [Classic score operands](../../../data/gameplay/classic-mge-5.4.0f/scoring.json) |
| `gameplay-defaults` | [gameplay-defaults.schema.json](gameplay-defaults.schema.json) | [Source ledger](../gameplay-defaults.json), repository evidence only |
| `text-resource-inventory` | [text-resource-inventory.schema.json](text-resource-inventory.schema.json) | [text-resources.json](../text-resources.json), repository evidence only |

[common.schema.json](common.schema.json) supplies reusable `$defs` for IDs,
paths, numbers, references, text runs and other shared concepts. It intentionally
rejects standalone instances. The inventory is metadata, not a runtime pack
component. Existing asset, build and conformance JSON contracts are separate from
this content-format schema family. The [built-in gameplay contract](../gameplay-format.md)
defines the six additional operands documents, bundle selection and provenance.
There are 21 schemas: 18 content formats, two metadata formats and shared definitions.

Objects reject unknown fields, including nested records, except within the
explicit namespaced metadata `extensions`. Alternatives such as literal versus
parameter, base versus special terrain, and event trigger kinds are exclusive.
Schemas enforce rule-table counts, eleven base-terrain shapes, all three council
eras and each of their 34 advisor/opinion pairs. An overlay may omit inherited
manifest components; a root manifest must declare all components.

## Local validation

Use Python **3.11+** and the pinned developer dependencies. From the repository
root, in your development virtual environment:

```sh
python -m pip install -r tools/requirements-schemas.txt
python tools/check_content_schemas.py
python tools/check_gameplay.py
python -m unittest discover -s tests -p 'test_content_schemas.py' -v
```

The [checker](../../../tools/check_content_schemas.py) validates the schemas
against the draft meta-schema, resolves every `$ref` locally, checks the nine
complete example documents and inventory, and verifies the exact expected
omissions in the two fragments. The rules fragment lacks the full identity
tables; the council fragment has one of 102 required era/opinion combinations.
Other errors in those fragments fail the check. This exception belongs only to
the repository's example check; there is no permissive fragment file format.

The separate [gameplay checker](../../../tools/check_gameplay.py) loads the six
shipped components, checks hashes/source pins and validates their specific
semantic constraints and classic-value fingerprint. Its bounded bundle checks
do not implement the complete content-pack semantic resolver.

Supply explicit paths to check ordinary documents instead:

```sh
python tools/check_content_schemas.py docs/content/examples/events.json
python tools/check_content_schemas.py docs/content/examples/rules.fragment.json
```

The first command passes structural validation. The second returns a nonzero
status and reports the incomplete tables. The checker chooses a schema by the
exact `format` value. It reads only the files requested and the local schemas;
it does not open manifest components, resolve parents, load resources or change
content. Passing a manifest alone does not check its referenced files.

[CI](../../../.github/workflows/build.yml) runs these checks and the
[regression tests](../../../tests/test_content_schemas.py) on Linux, macOS and
Windows. The Python packages are developer dependencies only; ordinary CMake
builds and the existing stdlib metadata checks do not require them.

## Editor and validator integration

Schema `$id` values use
`https://openciv2.invalid/schemas/content/v1/<name>.schema.json` as stable
identifiers. The reserved `.invalid` host is intentional: these are **not**
download URLs. Preload every schema into an in-memory registry keyed by `$id`,
then associate a content file with its appropriate schema. The checker uses
[python-jsonschema's registry API](https://python-jsonschema.readthedocs.io/en/stable/referencing/)
with retrieval disabled. Relative `$ref` values resolve within this registered
set, including recursive menu definitions. No network access is needed after
installing the developer packages.

Configure editor associations externally. Do not add `$schema` to a content
instance: it is not a version 1 content field. Schemas do have their own `$schema`
declaration. Validators must support Draft 2020-12, including `minContains` and
`maxContains`; an older draft can miss required coverage checks.

JSON Schema tests mathematical values after decoding. The checker additionally
rejects duplicate keys, non-JSON numbers, invalid UTF-8 and unpaired surrogates.
It decodes fractional/exponent number tokens exactly: `2`, `2.0` and `2e0` are
integers, but `2.00000000000000000001` is not. Another decoder must preserve this
distinction before validation. Gameplay input bounds come from the native
contract, not a required internal storage type; metadata byte counts and manifest
revisions do not acquire an arbitrary gameplay integer bound.

## Validation beyond JSON Schema

The following remain required by the format contract and owning algorithms.
They need a semantic resolver, source-byte checks, a filesystem boundary or
runtime context, and are **not** implemented by this structural checker:

| Area | Additional required checks |
| --- | --- |
| Pack selection | Parent identity/hash and cycle checks; exact file hashes; symlink containment; unique resource paths/IDs; overlay replacement and complete locale selection. |
| Identities and references | Unique IDs by collection/scope, including menu trees; fixed rule identity coverage and slot mapping; all rule, topic, text, action, scenario and media references with correct target kinds. Array counts alone do not prove identity coverage. |
| Rules and names | Rational terrain multipliers must have doubled values integral in 0…255; normalization and owner conversions; base terrain identities must match the eleven base-shaped records; classic encoding, byte lengths, delimiters, name comparison/collisions and title-override behavior. |
| Text and interaction | Declared parameter references/types, no boolean text runs, correct input initial types when parameter-bound, valid default choices, registered action/control/choice coverage and owner-specific admission/publication. |
| Council | Resolved, reachable, acyclic dialogue; probability within 0…1 and representable in tenths; video resource admission; required runtime selection, response limit and branching effects. |
| Documents and encyclopedia | Required credits/topic identities for supported operations; duplicate subject/scenario associations; links and information-policy checks. |
| Events | Consumer-specific selector admission, unit/terrain/technology identity kinds, resource availability and owner-defined execution/publication order. |
| Classic interoperability | Encodability and exact compatibility mappings, source-address uniqueness, scenario resource/policy agreement, permitted reload effects and original save/scenario/peer exchanges. |

Some of these checks are within one document, but compare identities or values
across its records. Standard JSON Schema cannot express them merely by adding
`uniqueItems` (which compares whole values) or numeric field bounds. Keep these
gaps explicit when adding the runtime validator; do not treat schema acceptance
as evidence of algorithm conformance.
