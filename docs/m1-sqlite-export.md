# Optional SQLite analysis export

`oc2-sqlite export` writes an analysis database from the same immutable stored snapshot
used by inspection. It supports the current save/raw-map domain and limitations
in [m1-inspection.md](m1-inspection.md). The database is a disposable analysis
artifact; the game never loads it as a save, checkpoint or replay. The implemented domain is bounded by M1.

## Build and run

SQLite is an optional native-only project. Core, Formats, Query, Session and the
base CLI do not depend on it. Build from the repository root:

```sh
python3 tools/dotnet/build_sqlite.py
dotnet restore solutions/OpenCiv2.Sqlite.slnx --locked-mode
dotnet build solutions/OpenCiv2.Sqlite.slnx -c Release --no-restore
dotnet src/OpenCiv2.Cli.Sqlite/bin/Release/net11.0/oc2-sqlite.dll export path/to/game.sav --sqlite analysis.sqlite
```

Use `python` on Windows and a native C compiler environment for the dependency.
`--archive PATH` selects an offline archive with the same mandatory hash check.
See [packaging](packaging.md) for self-contained archives, notices and extracted
package tests. The exporter rejects an existing destination and preserves source
files; cancellation/publication rules below remain part of its contract.

## Schema 1

The analysis schema is independently versioned as `openciv2-analysis/1`.
`PRAGMA user_version` is 1 and `PRAGMA application_id` is `0x4f433253`.
There is exactly one snapshot per export, identified by the SHA-256 of its input
bytes. All record and field keys include that snapshot identity.

| Table | Contents |
| --- | --- |
| `snapshots` | Source format/version/size, stored view, offline access, validation status and incomplete coverage flag. |
| `metadata` | Schema, completion, integer/text encodings, SQLite version, missing rules, identity meaning and absence of computed fields. |
| `records` | Section, numeric slot, physical ordinal, source offset, exact raw BLOB and stored provenance. Includes holes and empty trailing records. |
| `fields` | Stable field ordinal/name/type, value kind, exact integer text or raw BLOB. |
| `coverage` | Every present section, record count and exported flag. All decoded sections are exported. |
| `validation_checks` | Ordered names of checks actually performed. |
| `limitations` | Unavailable rules, loaded state, gameplay compatibility, code-page interpretation and retained/unknown semantics. |
| `diagnostics` | Ordered category/severity/code/field/absolute offset/message, matching inspection. |

`unit_slots` and `city_slots` views join each raw record to its exact token text.
`tiles` and `civilizations` views select their record sections. Other fields are
queried by joining `fields` with `records` on `(snapshot_id, section, slot)`.
These tables are stored projections: no token renewal, census rebuilding,
normalization or calculated gameplay columns run during export.

Every logical integer uses `fields.integer_decimal TEXT`, even when it fits
SQLite's signed integer type. This retains full `u64` values and makes the
encoding consistent. `type` retains the logical width/sign; `value_kind` is
`signed`, `unsigned`, `bytes` or `null`. A null optional field has SQL NULL in
both value columns. An empty byte field is a zero-length BLOB, distinct from
NULL. Classic strings remain BLOBs, including fixed-capacity NUL/tail bytes;
their contents are not declared UTF-8. Structural counts, ordinals, offsets and
slots fit signed SQLite INTEGER. No real-number conversion is used.

For example, with any independent SQLite client:

```sql
SELECT slot, token FROM unit_slots ORDER BY slot;
SELECT slot, integer_decimal AS retained_military_count
FROM fields WHERE section='civilizations' AND name='military_count' ORDER BY slot;
SELECT category, severity, field, byte_offset, message FROM diagnostics ORDER BY ordinal;
PRAGMA integrity_check;
```

Use a decimal/integer library for unsigned 64-bit analysis outside SQLite. Cast
only fields whose domain is known to fit its signed 64-bit INTEGER. Opening an
export for analysis neither requires nor changes the original installation.

## Failure and cancellation boundary

The export library accepts only an owned immutable snapshot, a stop token and
an optional progress callback. It builds a private in-memory transaction and
returns an owned SQLite image only after completion, using SQLite's
[serialization API](https://www.sqlite.org/c3ref/serialize.html). It has no live
world, game callback or filesystem access. Memory use includes the snapshot,
database and serialized image; streaming export is not implemented.

The native I/O adapter writes that image to an exclusively created sibling
staging file, flushes/closes it, checks cancellation and publishes it without
replacement. Windows uses a move without `REPLACE_EXISTING`; POSIX uses a hard
link that fails if the destination exists. A competing creation therefore wins
without being overwritten. Unsupported filesystems or write failures return an
error. Tests inject failures and cancellation after partial staging writes.

The CLI handles interrupt/termination requests cooperatively while exporting and
writing. Normal failure/cancellation removes the staging file and publishes no
destination. A forced process kill or power loss may leave an explicitly named
`.oc2-tmp-*` staging file; it never constitutes a published export. There is no
claim of directory-entry durability across power loss. Cancellation observed
after the atomic publication point cannot unpublish a completed file. A later
standard-output error can also leave the completed database with exit 2.

## Evidence and remaining scope

The Python SQLite reader compares every exported field with lossless query JSON,
including full `u64`, negative/retained counts, high-bit tokens, holes, nulls and
empty BLOBs. It independently checks source bytes/hash, diagnostics, coverage,
foreign keys and database integrity. Tests cover existing/source/alias destinations,
failed paths, unsupported/malformed input and retained semantic errors.

The [original-file report](validation/README.md) records
28 exports (21 current saves and seven raw maps), exact byte reconstruction,
matching source hashes, unchanged inputs and independent SQLite integrity checks.
Three legacy files publish no output. Proprietary contents and derived databases
remain outside the repository. These are file observations, not original-program
execution or gameplay compatibility evidence. Live observer-filtered export and
loaded-state analysis await their owning session/query implementations.


Current C# sources and tests are mapped in [implementation coverage](testing/coverage.md).
Run the [native/browser and CLI checks](testing/README.md);
[validation status](validation/README.md) records executed evidence and remaining gates.
