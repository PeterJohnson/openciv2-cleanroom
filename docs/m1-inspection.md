# M1 stored inspection

The native `oc2` CLI and portable `OpenCiv2.Query` library implement offline inspection
of version `0x2c` saves and classic raw maps. M1 is complete within its [finite scope](m1-validation.md); this remains a
stored observation, not a loaded session or a gameplay compatibility claim.
Optional [SQLite export](m1-sqlite-export.md) consumes the same owned snapshots.

## Build and use

Build the root solution with the pinned .NET SDK. The CLI needs no display,
assets, Lua or SQLite. For example:

```sh
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet run --project src/OpenCiv2.Cli -c Release --no-build -- inspect path/to/game.sav --cities --json
dotnet run --project src/OpenCiv2.Cli -c Release --no-build -- validate path/to/game.sav
dotnet run --project src/OpenCiv2.Cli -c Release --no-build -- diff before.sav after.sav --json
```

The portable query library is tested in native and browser hosts; process CLI and
native filesystem adapters are desktop-only. The executable accepts Unicode Windows
paths, spaces, and `--` before paths that begin with option characters.

`inspect` prints a human summary by default, or all records with `--json`.
`--cities`, `--units`, `--tiles`, `--civilizations`, and `--section NAME` select
record sections and can be combined. Human output includes selected fields when
a section is selected. JSON always identifies sections omitted by selection.
`validate` emits diagnostics and coverage without records. `diff` emits separate
byte-range and decoded-field changes; entity records match by section and file
slot, so inserted earlier records and changed lifetime tokens do not misidentify
later records. It does not infer gameplay effects from a byte difference.

`.mp` (case-insensitive) selects a raw map; other extensions select a save.
`--format map|save` overrides this. The sole supported view is `--view stored`.
Legacy versions, loaded/live views, and cross-format decoded comparisons are
explicitly unsupported. See [stored-save admission limits](m1-stored-saves.md).
No command writes to either input. Output goes to standard output and can be
redirected by the caller; ordinary usage/I/O failures go to standard error.

| Exit | Meaning |
| --- | --- |
| 0 | Available checks passed, or identical diff inputs passed those checks. |
| 1 | Semantic diagnostic errors, or different diff inputs. |
| 2 | Usage, file I/O, output I/O or host execution failure. |
| 3 | Unsupported version, view or decoded comparison. |
| 4 | Malformed input within the supported format. |

For a diff with multiple conditions, malformed takes precedence over unsupported,
which takes precedence over semantic errors/differences. Exit 0 never means all
semantic checks are implemented. Informational diagnostics and warnings do not
change that exit code.

## Owned query and output contract

`StoredInspection.Inspect` takes immutable bytes and explicit
`FileAccess.OfflineOmniscient`. Its owned `StoredSnapshot` exposes immutable
records, diagnostics, source bytes and SHA-256 observation identity. No API takes
a mutable world, process lifetime counter, gameplay RNG or live-session grant.
The existing live own-entity queries and session bindings are separate regression
coverage under [M1-05](m1-closeout.md#m1-05-and-m1-06-regression-and-build-matrix).
Broader observer/remembered-state and loaded-view semantics join M3–M5.

JSON schemas are `openciv2-inspection/1` and `openciv2-diff/1`. They identify source
format/version/hash, stored provenance, offline access, validation scope and
uninterpreted data. Every integer, including offsets, slots and counts, is an
exact decimal **string**; field types such as `i16` and `u64` specify interpretation.
Booleans use JSON booleans. A present optional field with no value contains
`"value": null`; an absent field in a diff is a null field object. Bytes and
classic text use lowercase hex, with no assumed Unicode/code-page conversion.
Fixed text includes its NUL and retained tail; variable event text excludes its
terminator in the field value, while the enclosing raw record retains it.

Records follow physical file order with stable numeric slots, including unused
unit/city slots and retained census values. Each record supplies its absolute
offset, raw bytes and named fields. Concatenating all raw record bytes from an
unfiltered successful inspection reproduces the source exactly. Raw-map starts
use separate x/y records; remembered map slots are civilizations 1..7; repeated
leader records keep their file order. Raw diff ranges use absolute byte offsets,
while decoded changes use section, slot and field name.

## Validation and unresolved domains

Diagnostics separate structural failures, compatibility limitations and semantic
errors. Implemented checks cover map-size consistency and terrain/owner ranges;
fixed-string termination; documented mode, primary civilization selectors,
gender, zoom and elimination ranges; and live entity ownership, home/route
references, reciprocal stack links, exact stack coordinates and stack cycles.
Hole payloads are retained without live-entity validation. No census is rebuilt,
cyclic stock clamped, stack repaired or saved token renewed.

Coverage is always marked incomplete. External rules and language mappings are
not loaded. Rule-dependent unit/production/event interpretation, complete scenario
and multiplayer semantics, legacy decoding, loaded state and gameplay compatibility
remain unavailable. `checks` lists the checks actually run, rather than implying
that all fields have been semantically validated.

F06 §3 catalogs `globals.view_civ` as i8 without defining its sentinel domain.
All 21 current saves in the supplied original installation store `-1` at file
offset 42. This is independent file evidence, not an observation of the original
program consuming the field. The inspector preserves every i8 value and emits
`view-civ-domain-unresolved`; it does not invent a meaning or a rejection rule.
Resolving the sentinel's runtime meaning needs a behavioral contract or an
original-program observation before a consumer depends on it.

## Verification

xUnit covers ownership, stable slots, retained/unsigned extremes, exact byte
coverage, semantic diagnostic offsets, null/absence, optional blocks and diff
matching. Eight CLI/driver integration tests use independently authored F06 bytes and
Python's JSON/hash libraries, including Unicode paths, broken output pipes and source preservation.
SHA-256 tests include published vectors and independent padding-boundary results.
These checks run independently of graphics and gameplay content.

The [Windows original-file report](validation/README.md)
records each source hash, exit code, diagnostics,
exact raw reconstruction and source preservation. It contains no proprietary
file contents. This check does not run the original executable, verify original
acceptance of edited exports or claim resumed gameplay compatibility.
All 21 current saves and seven raw maps reconstruct exactly and pass the available
checks; all sources remain unchanged. Three legacy files return unsupported.


Current C# sources and tests are mapped in [implementation coverage](testing/coverage.md).
Run the [native/browser and CLI checks](testing/README.md);
[validation status](validation/README.md) records executed evidence and remaining gates.
