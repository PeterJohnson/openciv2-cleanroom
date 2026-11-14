# M1 gameplay content loading

The portable C++ loader now validates all six version-1 gameplay components and
returns an owned `shared_ptr<const GameplayBundle>`. It contains typed calendar,
spaceship, government, difficulty, combat and scoring operands, source receipts,
manifest identity and the effective-value fingerprint. No game/session state is
installed while reading. A failure returns no partial bundle.

This implements the bundle boundary in [gameplay-format.md](content/gameplay-format.md).
It does not yet bind the owning mechanics, resolve advance references against
selected rules, load complete content packs/overlays, or admit checkpoints and
original peers. Those remain requirements in the [M1 audit](m1-validation.md).
The immutable result supplies the operands those bindings need; it is not proof
that a custom bundle is playable or interoperable.

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
references pending the full rule resolver; they are not guessed from display names.

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
