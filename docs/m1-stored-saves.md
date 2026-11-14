# M1 stored-save records

M1 remains in progress. This slice adds the complete **stored-file view for
version `0x2c`** and its lossless re-encoding. It does not implement F06's ordered
session load, live save operation, rule restoration or resumed gameplay.

## Record and file boundary

`state/civilization.hpp` supplies F02 §4 logical civilization/global values,
directed relationships, retained censuses, goals, names and leaders. Their
construction defaults do not claim original initialization behavior. They have
no packed host layout, database dependency or implicit repair operations.

`formats/save_records.hpp` and `classic_save.hpp` implement F06 §§2–4,8:

- All current fixed records, including global/civilization/name/leader records,
  window layout, map views, scenario parameters, elimination history,
  multiplayer payload and event records.
- The complete current stream: embedded map memory/tiles/coarse grids/regions,
  unit/city slots including holes, tribe triplets, cursor, selected leader
  records, power history and conditional trailing blocks.
- Optional `EVNT` records followed by their strings in the specified order.
  Presence words are retained as integers, never followed as pointers. Empty
  strings and absent strings remain distinct. Arbitrary nonzero presence values,
  classic text bytes, retained payload and trailing bytes survive re-encoding.
- Scenario flags and the multiplayer mode/human mask select the file blocks.
  Repeated tribes keep separate leader records in file order; decoding does not
  apply the loader's last-tribe-wins scratch assignment.

The decoder owns all results and accepts immutable bytes. It neither accesses
process token counters nor allocates a live world. It preserves saved tokens,
selected slots, retained counts, stored layout validity and map-header sizes.
Dimension-derived sizes determine the map read windows, while inconsistent
stored size fields remain available for later diagnostics. No enemy recount,
option merge, name restoration, password decoding or event compaction runs.

The encoder checks vector lengths, conditional block presence and event
marker/string agreement before returning any output. It preserves an admitted
nonstandard header separator and existing presence words. This API re-encodes a
stored view; F06 §5's save-as-scenario flag/event transformations belong to the
future live save operation.

## Declared admission limits

Only `0x2c` is supported by this stored-file API. The existing version gate and
entity item helper still describe their broader separate contracts; they do not
make this API support `0x26`, `0x27` or `0x28`.

The backing domain is W01's positive even i16 width, positive i16 height and
area below 32768; unit scan bounds 0..2048 and city scan bounds 0..256. The
ordinary city creation limit remains 255. These are declared implementation
backing limits, not claimed original rejection rules. Other scalar values are
decoded losslessly, including values outside nominal gameplay domains.

Event counts must be nonnegative and fit the supplied fixed-record bytes before
allocation. Event text markers must form a contiguous present prefix. Strings
must terminate within 255 content bytes. The original's off-nominal negative
count and overlong-string behavior is not implemented by this stored-view API.
Malformed input throws `DecodeError` with a file-wide byte offset and leaves
input and live state unchanged. Successful structural decoding is not semantic
validation; rules and cross-record consistency still need their own checks.

## Windows evidence — 2026-11-13 Pacific

Visual Studio 2026 / MSVC 19.51.36260.0, Windows SDK 10.0.26100.0 and CMake 4.4.3
build the headless targets. This is a supported developer toolchain, **not** the
pinned Windows CI clang-cl baseline. All 60 CTests pass: 57 Catch2 cases, two
metadata checks and the twenty existing X02 vectors. The ten new Catch2 cases
use independently authored specification-derived bytes and cover field offsets,
signed boundaries, retained values, every required truncation prefix, optional
blocks, repeated tribe records, event presence/order and inconsistent encodings.
These are not newly registered X02 execution fixtures.

The pinned Emscripten 4.0.23 / Clang 22.0.0 / CMake 3.31.10 toolchain passes all
four headless CTests under Node 22.16.0 on this Windows host, including the same
57 Catch2 cases and twenty vectors. Catch2 comes from the hash-verified native
dependency download via an explicit source override. The retained native and
WASM X02 reports have been refreshed from their rebuilt adapters. The existing
Catch2 `__COUNTER__` warnings remain. The new tests exposed excessive stack use
from several stored-save copies; the civilization collection now owns heap
storage, copies remain independent, and the default 64 KiB WASM stack passes.

The Python suite runs 99 tests successfully with two Windows symlink tests
skipped because this host cannot create those links. Schema and gameplay-data
checks pass. Neither these checks nor structural codec tests prove gameplay
compatibility.

A separate library-only build passes with tests, GUI, dependency fetching and
Python discovery disabled, and an intentionally missing asset directory.
Formatting uses the pinned clang-format 20.1.8. The repository hook is enabled
in this clone. The Windows sandbox prevents MSVC's PDB service and temporary
Git-hook commits; those validation commands run outside that sandbox.

The native read-only comparison driver can be run with:

```powershell
.\build\headless-fetch\oc2_save_roundtrip.exe 'C:\Games\Civ2\EXAMPLE.SAV'
```

It reads the input, decodes/re-encodes it in C++, compares every byte and prints
structural metadata. It never creates or overwrites a file. It is a test driver,
separate from the [implemented inspection CLI](m1-inspection.md).

Against the original installation at `C:\Projects\civ2\Civ2`, all 21 current
saved games in that directory reproduce their input bytes exactly, with source
SHA-256 values unchanged. The original executable's hash matches the prior
handoff. `ROME.SCN`, `TUTORIAL.SAV` and `WWII.SCN` are explicitly rejected as
legacy versions. The [retained comparison report](conformance/reports/original-saves-windows.json)
records versions, hashes, driver/build identity, source hashes and diagnostics.
Original file contents remain external. None of these comparisons executes the
original program or establishes that it accepted an edited export.

## Remaining M1 work

The [completion audit](m1-validation.md) still governs the milestone. Next are
semantic content and classic text adapters, explicit process/session
lifecycles and ordered loading, commands/continuations/authority, generalized
X02 execution and choice replay, and N05/N02 foundations. Legacy stored-file
coverage, original-program resume/edit checks and original-peer captures remain
open. Do not advertise a loaded-world view based on these stored records.
