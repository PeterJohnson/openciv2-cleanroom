# M1 stored-save records

M1 remains in progress. This slice adds the complete **stored-file view for
version `0x2c`** and its lossless re-encoding. It does not implement F06's ordered
session load, live save operation or resumed gameplay. Separate ordered
persistent-record, leader and census operations are implemented below.

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

## Ordered persistent records and load census

`formats/world_record_io.hpp` implements F06 §6 stages 5–6 and §7 over supplied
live global/name/civilization records, after the parent's header and rule setup.
It accepts the core reader's `0x26`, `0x27`, `0x28` and `0x2c` versions. This
does not expand the complete stored-file decoder's `0x2c` admission boundary.

Current records arrive as three items: globals, eight name records, then eight
civilization records. Legacy records use the specified four global fragments,
name-array item and nine items per civilization, including the required discarded
byte. The old path first clears the specified technology and census arrays;
omitted tails remain zero, while the unrepresented civilization byte at `0x65`
retains its incoming value. Each item publishes its transferred prefix, including
a full transfer reported as failed. Untouched bytes retain live values after the
item service returns. No later item is attempted after failure.

Enemy count is recomputed modulo 256 immediately after successful global input,
before names. Scenario capture, cheat/reveal/history changes and clearing the
saved-as-scenario flag occur only after all civilization input succeeds. A failed
read preserves the caller's prior scenario output. `write_persistent_items`
emits the three current blocks using values observed at their individual write
boundaries; the parent still owns scenario-export preparation and other blocks.

`state::rebuild_loaded_unit_census` implements the later F06 §6 stage 17. For
each civilization, it clears active and in-production counts, then scans live
unit slots and nonnegative city production. Counts wrap modulo 256. Free slots,
records beyond the scan bound and owners outside 0…7 contribute nothing. Losses
and other retained summaries survive. A live matching-owner type outside the
62-counter backing stops before that increment, retaining prior effects.

Eleven authored native/WASM cases cover all supported record versions, every
partial global prefix, partial array records, failure at each legacy item,
preclear/retained tails, live callbacks, scenario flag combinations, census wrap
and safe stops. An integration case reads persistent records and entities into
`GameState`, renews tokens through the existing entity reader, and verifies that
stored counts survive until the explicit census phase. In particular, renewal
to token zero affects the later count. These are specification/regression tests,
not original execution or X02 fixtures. The parent must still establish pool
scan backing and sequence the intervening map, leader, multiplayer and event
stages; these APIs do not admit a running session.

## Ordered leader and name operations

`formats/leader_io.hpp` supplies F06 §6 stage 8 and the name phases of §§5–6.
These are callable stages over already established world/rule state, not a
transactional publication of a decoded save or a complete session loader.

- `read_leader_items` reads each of the 21 triplets as three individual bytes,
  cursor x/y as separate words, then one selected Leader item per player. Only
  the triplet fields update live rule leaders. Saved records populate scratch
  indexed by tribe; the last repeated tribe record wins. Scratch records track
  their established byte prefix so a first short read cannot fabricate a known
  zero remainder. Partial reads retain transferred bytes and prior state.
- `write_leader_items` reads live triplet/cursor/leader values at each item
  boundary. Both paths select one player in mode zero, otherwise human bits
  0…7 in ascending order, including repeated tribes and an empty mask. Temporary
  `player_civ` changes restore on ordinary failure and safe stops. Item services
  may change later fields or the live mask; a selected read destination remains
  the tribe captured before that item call.
- `restore_leader_names` normalizes all 21 leaders, then runs the caller-selected
  ordinary or scenario branch. Ordinary loading uses saved negative fields as
  sign markers, not replacement rule data. Negation reduces to signed 16 bits,
  keeps −32768 unchanged, and can toggle shared tribes repeatedly. Scenario
  loading sets gender/portrait, compares plural bytes case-sensitively and resets
  all title pairs only on equality. It then negates the three name labels, without
  reselecting the leader name after changing gender or applying saved title signs.
- `prepare_scenario_names` updates adjective, plural and leader override fields
  in that order for scenario export or an active scenario. Nonnegative labels
  copy through NUL, preserving tail bytes; negative labels retain custom text.
  The declared safe boundary rejects an unbacked label or a string that cannot
  fit its 24-byte field before writing that field, preserving earlier effects.
  This is a safe stop, not a newly invented ordinary save-failure status.

Ten authored native/WASM cases exercise item order, every truncated prefix of a
two-player stream, repeated tribes, callbacks, selector restoration, signed
boundaries, scenario equality/gender ordering, missing backing and fixed-text
tails. They are specification/regression evidence, not X02 fixtures or original
I/O traces. A [retained original-data check](conformance/reports/original-rule-labels-windows.json)
also loads the pinned LABELS/RULES inputs, supplies the saved triplets/sign scratch
and applies the ordinary name phase. The resulting complete 48-byte English
leader record matches the original save exactly. This extends the earlier 18
label-magnitude comparison; it does not execute the original again or run the
remaining ordered load stages. The report retains the earlier LABELS-provenance
limitation and pins the comparison executable and input bytes.

Full path/rule reload orchestration, remaining stream blocks, passwords,
events, rule-reload/layout completion, mode adaptation and live-session
admission remain open. No loaded-world claim follows from these isolated stages.

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

The native comparison driver can be run with:

```powershell
.\build\headless-fetch\oc2_save_roundtrip.exe 'C:\Games\Civ2\EXAMPLE.SAV'
.\build\headless-fetch\oc2_save_roundtrip.exe 'C:\Games\Civ2\EXAMPLE.SAV' 'C:\Scratch\NEW_OUTPUT.SAV'
```

It reads the input, decodes/re-encodes it in C++, compares every byte and prints
structural metadata. With no output argument it writes no file. The optional
output uses the native atomic publication adapter and refuses an existing
destination, including the input itself. It reports `outputWritten`; malformed
input produces no export. The driver requires native tools and tests enabled;
it is separate from the [implemented inspection CLI](m1-inspection.md).

Against the original installation at `C:\Projects\civ2\Civ2`, all 21 current
saved games in that directory reproduce their input bytes exactly, with source
SHA-256 values unchanged. The original executable's hash matches the prior
handoff. `ROME.SCN`, `TUTORIAL.SAV` and `WWII.SCN` are explicitly rejected as
legacy versions. The [retained comparison report](conformance/reports/original-saves-windows.json)
records versions, hashes, driver/build identity, source hashes and diagnostics.
Original file contents remain external. None of these comparisons executes the
original program or establishes that it accepted an edited export.

A later [normal-UI Windows run](m1-original-text-observations.md) closes a bounded
original-resume check: the original accepted a C++ byte-exact export of a fresh
single-player save, founded a city, processed a turn and saved at 3980 B.C. That
save also reproduced exactly through C++, and the original accepted the second
export with the city present. The report keeps full source/output identities
and limitations. Eight inspection/driver CLI cases now include authored-file
output, source preservation, existing-destination refusal and malformed-input
cleanup. These checks do not implement the ordered load or live save operation.

## Remaining M1 work

The [completion audit](m1-validation.md) still governs the milestone. Next are
semantic content and classic text adapters, explicit process/session
lifecycles and ordered loading, commands/continuations/authority, generalized
X02 execution and choice replay, and integration of the
[N05/N02 primitives](m1-network-foundations.md). Legacy stored-file coverage,
broader original-program resume/edit checks and full original-peer sessions remain open.
Do not advertise a loaded-world view based on these stored records.
