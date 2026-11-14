# M1 gameplay loading and content-pack resolution

The portable C++ loader now validates all six version-1 gameplay components and
returns an owned `shared_ptr<const GameplayBundle>`. It contains typed calendar,
spaceship, government, difficulty, combat and scoring operands, source receipts,
manifest identity and the effective-value fingerprint. No game/session state is
installed while reading. A failure returns no partial bundle.

This implements the bundle boundary in [gameplay-format.md](content/gameplay-format.md).
It does not yet bind the owning mechanics, validate complete content semantics,
or admit checkpoints and original peers. Those remain requirements in the
[M1 audit](m1-validation.md).
The immutable result supplies the operands those bindings need; it is not proof
that a custom bundle is playable or interoperable.
The separate [rule decoder](m1-rule-content.md) now resolves the selected
gameplay government advance references against fixed identities and native rules.
The [classic text/rule readers and city-preference validator](m1-classic-text.md)
add ordered rule ingestion, preservation policies, paired native/classic rule
checks, and preference admission/application through both paths.
The [city-name providers](m1-city-names.md) add native list validation and shared
C01 cursor/fallback/publication behavior with the classic text path.
The [native rule installer](m1-rule-content.md#retained-rule-installation) now
applies admitted definitions and ordered operands to shared rule/label/leader
state. Complete content and gameplay-profile installation remains separate.

## JSON and operand admission

The user-selected [jart/json.cpp](https://github.com/jart/json.cpp/tree/ccb9195e6abe9eb060e338a04f43e8d2e67b45ea)
is vendored at a fixed commit with its bundled double-conversion. CMake verifies
the exact upstream files against `dependencies.json`. The owned wrapper enforces
the repository's stricter [native JSON conventions](content/data-format.md#2-common-json-conventions):
duplicate decoded keys, invalid UTF-8, unpaired surrogate escapes, nonstandard
escapes and non-JSON number literals fail. JSON grammar follows
[RFC 8259](https://www.rfc-editor.org/rfc/rfc8259).

The wrapper retains numeric lexemes in a parallel `jt::Json` tree. Integer fields
are admitted using decimal arithmetic on those tokens, never rounded doubles.
Thus `8.000e0` is an integer, while `8.0000000000000000000001` is rejected.
Positive revision metadata is retained as exact canonical decimal text, including
values above 2^53. It has no artificial signed32 gameplay limit. Error records
identify the source and field, or byte position where available.

Every required component field is checked for type/domain; unknown fields and
missing records fail. Names map explicitly to fixed profile identities, regardless
of object member order. Calendar continuity/endpoints, retirement ordering,
spaceship group/mass/count constraints and exact doubled defense factors are
checked. Rational operands are reduced once, masses become hundreds of tons once,
and defense multipliers become exact doubled factors. Advance IDs remain named
references in the bundle; the separate rule decoder binds them without guessing
from display names.

The fingerprint uses the versioned compact ASCII envelope, sorted object keys,
canonical integer tokens, reduced rationals and no root extensions. The wrapper
accounts for the library's solidus escaping to match the existing Python ledger.
Classic mode requires the trusted baseline digest compiled from the repository
contract. A user-supplied manifest cannot replace that baseline. Custom mode can
contain baseline values, but changed operands fail `--require-classic`.

Admission is bounded: each JSON source and expanded integer is limited to 16 MiB;
the pinned parser limits nesting to 20 levels. Limit failures are diagnosed,
without truncated data or fallback values. The sources contain no local patches.
MSVC builds use `/Zc:__cplusplus` so the dependency selects 64-bit checked
arithmetic correctly on LLP64. Regression cases cover multi-digit negative and
64-bit boundary integers on native/WASM.

## Explicit sources and native command

`oc2_formats` receives bytes and an injected resolver. It checks every descriptor
before making resolver calls, then verifies each exact byte hash before decoding.
Paths and stable backing identities must be distinct. It never scans neighboring
files, fetches URLs, performs field merges, installs defaults or writes inputs.

The native `ContentDirectory` adapter mounts the selected manifest's directory.
It checks canonical-path containment and rejects escapes through symlinks. It
recognizes hard-link aliases through filesystem equivalence. On Windows it also
rejects alternate data streams. Containment is checked when each source is
resolved.

```powershell
build/headless-fetch/oc2.exe content validate data/gameplay/classic-mge-5.4.0f/gameplay.json --json
build/headless-fetch/oc2.exe content validate path/to/gameplay.json --require-classic
```

The JSON report format is `openciv2-gameplay-validation/1`. Success reports scope
`gameplay-bundle-only`, exact revision text, manifest/component hashes, semantic
digest, mode, baseline equality and remaining admission limitations. It does not
claim a live-state load or create a conversion output. Exit codes are 0 for a
validated bundle, 1 for content/gate rejection, 2 for usage/source failure, and 4
for invalid JSON. Human output states the same bounded result.

Eight portable Catch2 cases cover the parser and bundle contracts. Six native CLI
cases check read-only behavior, byte hashes, independent decimal fingerprint
calculation, exact revision reporting, malformed input and path/backing aliases.
The symlink case is skipped where the OS does not grant creation permission.
These are specification-derived and dependency integration tests, not
original-game observations or independent fixture review.

## Pack inheritance and mounted inputs

`resolve_pack` implements the manifest and selection contract in
[data-format.md section 3](content/data-format.md#3-pack-manifest). It takes the
selected manifest and explicitly registered parent mounts, with an injected
scoped file reader. It hashes parent bytes before decoding, checks the requested
ID/revision, diagnoses missing/ambiguous parents and cycles, and resolves the
chain parent-first. Registration order cannot change selection. Unused registered
manifests are not decoded and overridden component files are not opened.

Each retained descriptor keeps its declaring pack and mount. An overlay replaces
a whole component or locale catalog; it replaces the whole scenarios/resources
list when present. Omission inherits, while explicit `compatibility: null` clears
that descriptor. No file-body, row or message merging occurs. Every advertised
locale must supply all five catalogs, and the default must exist. The requested
locale is selected for all five together, or the default is selected for all five.
Each selected catalog's format and locale envelope must match its descriptor.

Selected JSON, world and media inputs have exact byte hashes and owned storage.
The result preserves manifest provenance, source ownership and hashes, array
ordering and external-world null entries. It does not decode world/media payloads
or certify the rule/text/event bodies and cross-references. Complete semantic
admission remains required before a session can use a pack. This resolution
stage can therefore inspect the intentionally incomplete format example without
claiming that it is valid playable content.

The builtin gameplay revision is embedded from the seven owned JSON files. CMake
verifies their ledger/manifest byte hashes; the runtime uses the same loader and
classic fingerprint check. It does not search the current directory or require a
checkout at runtime. Explicit gameplay selections inherit their declaring mount
and resolve components within the gameplay manifest's directory. Native reads
check both that narrower directory and the enclosing pack, including symlinks.

Default resolution budgets are 64 manifests in a chain, 4,096 opened file scopes
and 256 MiB of selected manifest/file bytes. The portable API accepts explicit
limits; JSON documents still have the 16 MiB/20-level parser limits above.
Exceeding a limit fails without returning a partial resolved pack.

```powershell
build/headless-fetch/oc2.exe content resolve docs/content/examples/content.json --json
build/headless-fetch/oc2.exe content resolve path/to/overlay/content.json --parent path/to/base/content.json --locale fr --require-classic --json
```

`--parent` is repeatable and supplies exact manifests; there is no neighboring
directory or installation discovery. The report is
`openciv2-content-resolution/1`, status `resolved`, scope
`pack-selection-and-gameplay`, with the remaining semantic checks stated in both
human and JSON output. File paths in receipts are relative to their owning pack;
the six gameplay component paths are relative to their gameplay manifest, or
the identified builtin. Exit codes match the gameplay command above.

Eight portable pack cases cover roots, overlays, dependency diagnostics, locale
selection, source hashes, nested custom gameplay, exact revisions and limits.
Six CLI cases check actual independent mounts and receipts, with one symlink
case skipped on hosts lacking permission. A native I/O case always exercises
the nested-directory and byte limits without requiring a symlink. These checks
extend the earlier gameplay cases; they do not replace body-level admission.
