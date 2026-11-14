# M1 stored-save records

M1 remains in progress. The complete **stored-file view for version `0x2c`**
supports lossless re-encoding without load transformations. The separate ordered
core loader and live writer now compose the stream/name/census and save-preparation
stages below over `SessionRuntime` with required external service bindings.
Application file/rule adapters, post-load admission and resumed gameplay remain open.

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
separate live save operation below.

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
item service returns. No later item is attempted after failure. An optional
publication binding mirrors global scan bounds into the runtime pools after each
global fragment, including a failed prefix, before any following service call.

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
not original execution or X02 fixtures. The composed core parent now binds scan
publication and sequences the intervening map, leader, multiplayer and event
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

## Passwords and multiplayer load/save stages

`state/multiplayer.hpp` owns the logical saved multiplayer values, separate
runtime local-human/resume selectors and the two password flags per civilization.
`GameState` retains these values; `ProcessContext` retains the four menu preference
fields currently consumed by F06. Construction and client-session replacement
do not execute F03 preference defaults or reset this state. The existing
`formats::MultiplayerRecord` is an alias for the shared logical saved values;
its exact 1172-byte codec and stored inspection contract are unchanged.

F06 §9 encoding rotates the complete 256-byte password array, including row and
array seams. Refresh derives both flags from each decoded row's first byte and
re-encodes without changing the encoded bytes or drawing randomness. Reset
consumes 255 primary codes, stores their low bytes, retains incoming byte 255,
then clears row starts/flags and encodes. An unresolved choice retains the
sampled prefix before those later effects. This serialized session state uses
`GameplayRandom`; presentation/cosmetic contexts are inaccessible to the operation.
The owner supplies a distinct semantic reset call identity. The decision site
is `F06.password-reset.byte`, with call suffix `/row/<civ>/byte/<column>` and
attempt zero. Controlled choices and generated traces use the same identities.
This binds the existing policy; it does not add RNG data to classic saves.

`formats::read_multiplayer_items` implements F06 §6 stages 11–12. It first
sign-extends the saved-current selector into runtime local human. Old versions
or mode zero reset passwords and only the specified defaults, retaining all
other multiplayer payload. Current multiplayer input clears the saved block,
then publishes its transferred prefix. Failure retains that prefix and skips
resume fallback and flag refresh. A successful read uses the saved resume byte,
then turn civ, then the first alive civ in 1…7 as specified, with no draws.
Menu simultaneous and saved simultaneous remain separate fields.

`write_multiplayer_item` implements F06 §5 stage 8: substitute the supplied
network-turn low byte into saved resume, write one complete block, and restore
the prior resume byte only on success. A failed or throwing service leaves the
temporary substitution and any callback effects. `state::adapt_loaded_mode`
is the completion phase, callable only after successful load and file close.
Equal modes do nothing; entry mode zero retains the loaded mode, while a nonzero
entry replaces it. Human-mask fallback, doubling and menu preference changes
follow F06; variable resume-bit shifts use F01's modulo-32 count rule.

Thirteen authored native/WASM cases check all password bit positions, controlled
and generated reset/replay, missing-choice prefixes, every partial multiplayer
item, retained defaults, callbacks, successful/failed writes, mode adaptation
and process/world ownership through `SessionRuntime`. These are specification
and regression evidence, not original password captures or full mixed-peer
sessions. The owning loader must still sequence these callable stages with
all surrounding input, cleanup, rule and event operations.

## Ordered events and string ownership

`state/events.hpp` supplies the F02/S02 ordered collection and separate loaded
indicator now owned by `GameState`. Events have typed logical identities,
retained payload and owned string references. Absent, present-empty, backed and
unresolved saved references remain distinct. Serialized presence words are
retained bits, never dereferenced or used as live identities; `EventRecord`
continues to use the existing 444-byte codec. Stored-file inspection retains its
separate immutable string representation and admission limits.

`formats::read_event_items` implements F06 §8 with separate fixed-item and
character services. An absent, partial or mismatched tag clears only loaded;
the old list survives. A recognized tag requires a complete signed count before
resetting the collection. Zero and negative counts yield loaded emptiness after
completion. All fixed records precede all strings. Partial fixed records retain
their transferred prefix; unresolved string markers do not fabricate empty text.
String reads distinguish EOF, NUL and the consumed 256th byte: overlong input
retains its first 255 bytes without scanning to a later terminator. Each field
publishes only after successful string creation. A text hole stops text-line
reads, retaining later unresolved markers; accessing those strings stops safely.

S02 §2 copying first appends payloads, then duplicates strings in the specified
order. It supports a nonempty destination's old-head rewrite behavior, shared
source strings and text holes. Duplicated fields acquire distinct identities;
fields after a hole retain their prior reference relationships. Compaction uses
two such copies, preserving event identities, payload/order, consumed flags and
bound selectors. Failure during the first copy leaves the main collection in
place; failure after replacement retains the second copy's published prefix.
Logical references keep their backing alive without relying on native addresses.

`EventServices` supplies optional event/string resource outcomes and error
reporting. String failure reports −9/category 3 and does not publish an empty
replacement. Copy requests retain the 15/24/1 minimum string capacities, without
truncating longer content. This is not an emulation of original pool addresses
or block-allocation traces. Node-invalidating callbacks, an allocation callback
that changes its captured destination tail, self-copy and copying a source that
grows beyond its admitted capacity stop with prior effects intact. Record and
string-content callbacks otherwise remain live at their documented boundaries.

`write_event_items` emits the tag/count, all fixed records, then each selected
string through its first NUL, with text output and the terminating NUL as separate
calls. It does not impose the reader's 255-byte retention limit. Presence tokens
may be canonicalized to nonzero values; logical string identity never enters
the file. The parent owns the loaded/scenario-export gate. Loaded becomes true
on read only after successful compaction.

Fifteen authored native/WASM cases cover all fields, partial fixed records,
optional tags, negative counts, empty/overlong/unterminated strings, text holes,
allocation/reporting failures, copy aliases, compaction replacement boundaries,
callbacks, ordered write failures and runtime ownership without RNG changes.
These are specification/regression evidence, not original event captures or X02
fixtures. Event parsing, trigger/action execution, editor integration and complete
F06 orchestration remain separate work.

## Ordered map summaries, history, layout and scenario parameters

`formats/save_aux_io.hpp` supplies the remaining fixed auxiliary stream stages
from F06 §6 stages 7, 9–10 and the final layout decision in stage 19. These
helpers operate on supplied live state and retained transfer scratch; the parent
still sequences the complete file operation and its rule/lifecycle boundaries.

- Map summaries read coarse grids 0 and 1 as separate items, flush, then read
  the 64-region table as one item. A short grid read establishes only the
  transferred cells; other cells retain their values or explicit unknown state.
  Grids 2 and 3 remain untouched. Region input retains untransferred fields after
  callbacks. Replacing/releasing captured map backing during its read stops
  safely before publishing to invalid storage. Output rejects an unknown cell
  at that grid's boundary, preserving earlier writes.
- Power history is one 1,200-byte item. The layout has separate packed transfer
  scratch and live working coordinates. A failed layout read retains its packed
  prefix without unpacking; successful input sign-extends all coordinates and
  publishes the file's valid marker before the flush. Each of the forty map-view
  fields is then read individually. None of these operations creates windows.
- Saving writes power history first, then copies the live current screen into
  the saved screen, reduces coordinates modulo 65,536, packs and writes layout,
  then writes each view field. Preparation effects survive later write failure.
  The final load decision separately replaces layout-valid based on matching
  screen dimensions and captured scenario status, allowing different origins.
- Scenario input uses captured scenario status or the live Scenario flag.
  Ordinary input calls S01's field-specific reset: flags/name-first-byte zero,
  technology paradigm 10 and the eight other numeric fields zero. The name tail,
  world flags and events remain intact. Elimination history always follows;
  all twelve rows survive regardless of its count. Output uses explicit scenario
  export or the live Scenario flag, then writes elimination history.

`GameState` now retains power/elimination history, scenario parameters, logical
layout and map views across client-session replacement. Existing stored codecs
alias the shared map-view/scenario/elimination types without changing bytes.
Fourteen authored native/WASM cases cover every failed fixed-block prefix,
individual view fields, live callbacks, packing boundaries, scenario gates,
unknown backing and runtime ownership without RNG changes.

The native comparison driver additionally reads/writes these isolated stages
against original bytes and compares their fields with the stored decoder.
All 21 admitted original `0x2c` saves match exactly and remain unchanged; the
[auxiliary comparison report](conformance/reports/original-save-aux-windows.json)
pins the inputs, implementation and driver. Those files are all ordinary mode-zero
saves; scenario evidence here is authored tests. Three legacy files remain outside
the driver's complete stored-view domain. This is original-data interoperability
evidence, not original execution, observed failure traces or a full session load.

## Composed core load

`session/classic_load.hpp` implements F06 §6's core orchestration over an existing
`SessionRuntime` and caller-retained `ClassicFileState`. It selects the entry-mode
extension, uses the any-period rule, opens input, reads the character header and
version gate, selects the requested path's scenario/GAME context, updates the
remembered basename/subdirectory and invokes full rule loading. It then runs the
record, map, entity, leader, history/layout, multiplayer and event stages in order,
restores names, rebuilds the census, invalidates the city-cache identity, requests
the three final rule reloads, decides layout validity, closes input and adapts mode.

File, directory, probe, comparison, flush and owning F03 operations are required
service bindings. A normal F03 callback return means that operation completed;
an absent binding rejects before file effects. Resource hooks remain optional.
The composition root retains path/cursor/packed-layout/leader scratch across calls,
supplies current screen state and initializes the runtime's random contexts before
classic import. No RNG metadata is required from or added to the classic file.
The core entry requires settled commands and leaves participant/client identities
alone; it is not the post-load wrapper or a connection/admission procedure.

Early input, unsupported and newer-version results are distinct. Required later
item failure returns data status with earlier effects retained; input is closed
exactly once, and close failure is ignored. Exceptions from rule services or safe
backing checks close input without rolling back state. An unterminated 79-byte
header still consumes its extra separator read before stopping; a missing final
backslash stops after the initial flush. Neither case invents an ordinary original
error result. Paths use supplied case comparison, not the host's locale or native
path parser. Same-runtime query revisions publish at service and completion
boundaries; the core loader does not grant new client authority.

Fourteen authored native/WASM cases cover complete `0x26`, `0x27`, `0x28` and
`0x2c` flows, every required item failure, header/version/path gates, retained
directories, scan publication before names, post-close mode adaptation,
scenario/event/name/census ordering,
rule failures, random ownership and query access through an unchanged client.
One case invokes the actual F03 full-table and name-preserving final parsers.
The other ordering cases declare supplied F03 child effects; they do not establish
the production rule/resource wrapper's behavior. Composition also fixes older
map/entity helpers to preserve untouched post-callback fields on partial input
and stop before using map storage invalidated by a callback.

This is authored integration evidence, not an original-game core-load comparison
or an X02 registration. The 21 original-save auxiliary comparisons still pass but
do not run this orchestrator. Concrete native/web file and F03 resource-context
bindings, full reload-wrapper callbacks, post-load actions/admission, application saving
and original resumed-state comparisons remain required M1 work. Inspection's
stored view has not been relabeled as loaded-state analysis.

## Composed live save

`session/classic_save.hpp` implements F06 §5 over the same runtime and retained
file context. It swaps the entry hotseat human mask, selects the extension and
opens binary replacement through a required output service. Header characters
are separate unchecked writes; saved-current civilization is narrowed from the
local-human selector before the checked version item. Each later block observes
live state at its own boundary. There is no whole-world snapshot, census or region
rebuild, lifetime renewal or direct random draw.

Scenario export rebuilds advance known-by masks and first discoverers through
V03 §2's knowledge query, clears the four specified civilization flags and
prepares names before writing world records. The knowledge leaf handles negative
sentinels before civilization access, excludes barbarian knowledge and always
returns false for Future Technology, even with its raw membership bit set.
Ordinary active-scenario saves prepare names without rebuilding knowledge.
Events are emitted only when loaded and not exporting a scenario.

Layout preparation follows the successful power-history item and remains after
later failure. The multiplayer resume byte is temporarily replaced from the
distinct network-turn selector and restored only after its item succeeds.
Common cleanup uses the live mode, restores the hotseat mask when applicable,
then closes once. A callback entering hotseat from another entry mode requires
explicit incoming cleanup-mask backing; absent backing closes the output and
stops safely. Close failure does not change the original success status, so this
core result alone is not a durable browser-save acknowledgement.

Required output failures leave partial bytes and earlier world effects intact.
Unbacked names or map data stop safely rather than inventing ordinary save-error
results. The supported runtime domain requires matching global and pool scan
bounds; this is checked before persistent output and again before entity output.
A mismatch stops at that boundary without silently repairing either value.
Production output providers must implement truncating binary replacement and
preserve partial files. The inspection export's atomic no-overwrite provider has
a different contract and is not used here.

Thirteen new native/WASM cases cover the knowledge leaf and save composition:
the complete ordered stream, every required fixed-item failure (including active
scenario, hotseat and event blocks), header-character failures, signed narrowing,
scenario discovery/flag/name effects, extension selection, live mode transitions,
callback changes to later blocks, event text/terminator failures, backing stops,
empty entity ranges, unchanged client/RNG/token ownership and load-edit-save-reload.
The latter uses declared F03 child effects; the separate core-load integration
case exercises the real F03 parsers. These are independently authored contract
tests, not X02 registrations or original-game acceptance of live exports.

The full Windows run passes 308 default-off and 310 SQLite-enabled CTests;
WebAssembly passes six CTests including 296 portable cases (165,640 assertions).
All 54 existing X02 observations and consumed prefixes match across native/WASM.
The 21 original-save auxiliary comparisons and original label checks still pass,
but their retained scope excludes this live writer. Production native/web file
adapters, application save/admission integration and original edited-export/resume
checks remain open.

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
structural metadata. It also compares the isolated auxiliary load/write stages
above, reporting `orderedAuxiliaryEqual`. With no output argument it writes no file. The optional
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
