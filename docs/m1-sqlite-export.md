# Optional SQLite analysis export

`oc2 export` writes an analysis database from the same immutable stored snapshot
used by inspection. It supports the current save/raw-map domain and limitations
in [m1-inspection.md](m1-inspection.md). The database is a disposable analysis
artifact; the game never loads it as a save, checkpoint or replay. M1 is still in
progress.

## Build and run

SQLite export defaults OFF. Enabling it creates `oc2_export_sqlite` and its private
SQLite dependency, with no SQLite link from `oc2_base`, game, formats, query, GUI
or session targets. It is native-only; ordinary WASM builds retain portable
queries and do not configure SQLite.

```powershell
cmake -S . -B build/sqlite-export -G Ninja -DOC2_BUILD_GUI=OFF -DBUILD_TESTING=OFF -DOC2_ENABLE_SQLITE_EXPORT=ON -DOC2_FETCH_DEPENDENCIES=ON
cmake --build build/sqlite-export
.\build\sqlite-export\oc2.exe export 'C:\Games\Civ2\EXAMPLE.SAV' --sqlite analysis.sqlite
```

For an offline build, supply `FETCHCONTENT_SOURCE_DIR_SQLITE` pointing to the
extracted pinned amalgamation. The build checks its version; the explicit source
override has the same trust boundary as other library overrides. Downloads use
the SHA-256 pin in `dependencies.json`. SQLite 3.54.1's official amalgamation was
also checked against the publisher's SHA3-256 digest before pinning it:
`92281c86e2957f9b9305890f9ac04204caa7b677cc868dc326d942689df28e30`.
The [official download](https://www.sqlite.org/download.html) and
[public-domain notice](https://www.sqlite.org/copyright.html) describe the upstream
dependency. Extension loading is disabled. Source archives are not checked in.

`--json` produces an `openciv2-export-receipt/1` receipt after publication, with
source hash, analysis schema, output byte count and `complete: true`. This means
the export is complete, not that gameplay validation is complete. The command
retains inspection's exit codes: 0 available checks passed; 1 semantic errors
retained in a completed analysis; 2 usage/I/O failure; 3 unsupported feature or
version; 4 malformed input. Cancellation before publication returns 130.
Unsupported/malformed inputs produce diagnostics and no database. Export disabled
at build time returns 3 without reading the source or creating the destination.

The destination must be new. Existing files, including the source itself or a
hard-link alias, are never overwritten. The exporter has no replacement flag.
Use another destination for another observation. The usual `--format` and
`--view stored` options apply; loaded/live views and section-filtered export are
not implemented.

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

The Windows MSVC SQLite-enabled build passes 72 CTests, including 67 Catch2 cases,
inspection CLI tests, five independent Python SQLite tests, metadata and twenty
conformance vectors. The default-off native build passes 70 CTests; its tools-only
build graph contains no SQLite target/source and still passes inspection tests.
Pinned WASM passes four CTests. Native CI headless configurations now enable the
exporter on Linux, macOS and Windows; desktop configurations keep it disabled.
Hosted results for this change remain pending.

The Python SQLite reader compares every exported field with lossless query JSON,
including full `u64`, negative/retained counts, high-bit tokens, holes, nulls and
empty BLOBs. It independently checks source bytes/hash, diagnostics, coverage,
foreign keys and database integrity. Tests cover existing/source/alias destinations,
failed paths, unsupported/malformed input and retained semantic errors.

The [original-file report](conformance/reports/original-sqlite-windows.json) records
28 exports (21 current saves and seven raw maps), exact byte reconstruction,
matching source hashes, unchanged inputs and independent SQLite integrity checks.
Three legacy files publish no output. Proprietary contents and derived databases
remain outside the repository. These are file observations, not original-program
execution or gameplay compatibility evidence. Live observer-filtered export and
loaded-state analysis await their owning session/query implementations.
