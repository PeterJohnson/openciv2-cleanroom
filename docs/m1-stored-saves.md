# M1 stored-save records

M1 is complete within its [finite scope](m1-validation.md). The complete **stored-file view for version `0x2c`**
supports lossless re-encoding without load transformations. The separate ordered
core loader and live writer now compose the stream/name/census and save-preparation
stages below over `SessionRuntime` with required external service bindings.
Native file services and the F03 reload context are implemented below. Application
admission, browser persistence and resumed gameplay remain open.

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

The decoder accepts `0x26`, `0x27`, `0x28` and `0x2c`. Compact legacy records
are expanded into owned values with explicit zero defaults for omitted storage;
conversion supplies explicit occupancy for records without stored lifetime tokens. The encoder emits `0x2c` only.
Lossless byte-for-byte round trips apply to current-version stored views.

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

`decode_stored_save` reads persistent globals, names and civilization records
without changing a world. `formats::game_input` converts the owned record tree
into common restoration operands. `session::install_game` applies scenario flag
normalization, enemy count, loaded entity lifetimes and unit production/active
censuses. These transformations have one owner regardless of the source format.
No synthetic file callback publishes partial records or selects simulation work.

## Ordered leader and name operations

Stored leader records retain their signed markers for lossless codec output.
The common input contains customization flags indexed by tribe; repeated saved
tribes select the last record, while installation still applies the specified
per-civilization toggles. Scenario names and portraits use the installed rules.
`prepare_scenario_names` belongs to session save preparation. Export never
substitutes a temporary player selector into the authoritative globals.

## Passwords and multiplayer load/save stages

Password encoding, decoding, reset and mode adaptation remain state operations.
The installer selects legacy/single-player defaults or an admitted multiplayer
block, then owns password randomness, resume selection and flag refresh. Decoders
consume no random choices. Live export substitutes the network-turn selector in
a local encoded value. Password and mode tests cover the underlying semantic
operations independently of file chunking.

## Ordered events and string ownership

`EventValues` contains semantic fields. The stored `EventRecord` additionally
contains presence words and opaque bytes. Import resolves strings before they
enter the live collection. `EventCollection` assigns immutable IDs and exposes
const events through const access. Copying preserves the specified text-hole
aliases; compaction prepares one detached copy and publishes its strings into
the same live events. Failure during preparation retains the collection.

Opaque imported event bytes remain in `ClassicFileState` adapter metadata,
weakly associated with the actual event object. They survive compaction and
export, but cannot leak into a new event merely because its numeric ID matches.
Live presence markers derive from strings; stored-view encoding preserves the
original marker bits. Resource hooks express admission failures, not arbitrary
gameplay changes during allocation.

## Ordered map summaries, history, layout and scenario parameters

Stored map records convert to `world::MapInput`. Geometry and vector lengths are
validated before allocation. The installer prepares new map storage and exchanges
it only on success, preserving the target's fallback binding. Legacy stored
layout coordinates widen during conversion; screen/scenario validity belongs to
the session installer. Export uses a layout snapshot and does not modify live
window geometry. Scenario default resets and elimination history installation
also belong to the installer.

## Composed core load

`ClassicLoadOperation` copies and validates its services and acquires a
`SessionActivity`. It reads bounded owned bytes, validates the supported header,
closes input once, decodes and admits the input, and asks one content factory for
`LoadedContent`. `RuleInstaller` installs that content and reuses the same input
for final name-preserving table updates. Parsing does not keep a reader open
across gameplay installation.

Malformed or truncated input and close failure leave the previous world, rules,
tokens and RNG unchanged. Semantic installation children retain completed effects
if they fail; this is not a promise that every installation effect is transactional.
The activity prevents callbacks from replacing the world. A dialog wrapper keeps
the same operation alive through event, sound, presentation and cleanup callbacks.

Tests cover all four admitted versions, staged failure, ownership, single-close
behavior, real classic content loading, scenario/multiplayer transforms, live
edit/export/reload, and nonmutation of stored inputs. Private failed-I/O memory
traces are replaced by explicit admission and installation checks.

## Composed live save

`session/classic_save.hpp` implements F06's production save boundary. It prepares
an owned byte image before opening output. Accepted preparation updates the
saved-current civilization and scenario knowledge/flags/names. Temporary
hotseat masks, selected-player and resume values stay in the export context.
No output provider observes those substitutions in the live session, and no file
write is a game-event boundary. Census, regions, lifetimes and RNG are unchanged.

All output bytes, including the header, are covered by the write result. Success
requires successful final close/flush/commit. Opened outputs close exactly once
on returned failure or exception. Preparation failures open no output; later I/O
failures retain accepted preparation effects. Native output currently uses
replacement files and can retain partial bytes; atomic providers are permitted.
No original incoming-stack cleanup mask or per-item mutation contract exists.

Tests compare complete decoded files, scenario/ordinary/event behavior, hotseat
encoding with unchanged live selectors, header/body partial-output failures,
close failures, exceptions, preparation errors and unchanged RNG/client identity.
Historical ordered-call and partial-damage tests have been replaced. The retained
original live-save evidence predates this refactor; it is not a new interoperability
claim for this revision.

## Native files and rule context

Native files remain owned noncopyable `CoreInput`/`CoreOutput` services, with
exactly-once close and destructor cleanup. `io::ClassicFiles` provides binary
files, case-independent lookup and an explicit working-directory context without
changing the process directory. Low-level read/write tests still verify short
prefixes and actual I/O errors independently of gameplay installation.

`ClassicRuleContext` owns its reader and borrows file-selection/workspace state.
Each `load()` returns independent rules and labels, restores temporary search
selection, and closes resources on success or failure. Resource presentation is
an explicit post-install notification rather than a parser child. Native tests
exercise real labels/rules files and load-edit-save-reload on disk.

## Save-menu caller and file-choice boundary

`session/classic_file_choice.hpp` exchanges semantic file types, a suggested path,
title, extension and open/save requirements. Results distinguish accepted paths,
cancellation and failure with an owned diagnostic. Only accepted valid paths are
published. Native flags, handles, NUL-separated filters, capacities, return-byte
conventions and extended-error queries are absent from the portable API.
`FileChooser` is an owned virtual service with nonblocking begin/poll/cancel
operations. M2 supplies ImGui and browser adapters for its semantic requests.

`session/classic_save_menu.hpp` invokes the real writer only after acceptance.
It supplies a familiar ruler/year suggestion and scenario/email notices as a UI
choice, not a compatibility gate. Cancellation exits in every mode without saving
or publishing draft paths; N04 owns any mandatory email-turn save flow. Active
entry stops N03 countdown and attempts one start on every exit, including errors.
`ClassicSaveOperation` retains the workflow across event-loop iterations and
revalidates/captures the live game at acceptance. Its `SessionActivity` guard
requires cancellation before session replacement, without blocking command or
deadline processing. The existing synchronous helper is an immediate-provider
adapter over the same phases; F05 setup migration follows its eventual UI.

Text services are pure presentation queries on captured values. Notices own their
arguments, and neither filename suggestions nor notices write process text scratch.
Tests cover accepted/cancelled/failed selection, paths beyond original capacities,
coherent file bytes under changed formatting, scenario/email meaning, close errors,
exceptional cleanup and unchanged unrelated presentation/RNG state. They do not
require native UI traces or synthetic changes during formatting.

U01 post-load cleanup still uses real traversal, repair, placement and deletion.
Diagnostics are passive output; only admitted game/network children can change
simulation state. Graphical chooser binding, full S02/P10/V01 children and
application admission remain separate integration work.

## Accepted-file load caller

`load_classic_dialog` keeps its guarded core-load operation alive through
post-load event, sound, title and unit cleanup stages. For scenario text events,
a source factory returns an optional owned `EventCatalog`. The common event
installer prepares fresh strings/events and commits the collection on success;
parsing and failed allocation cannot publish a partial replacement. Both saved
and text-sourced events use this installation path. The source owns syntax and
file selection; the session owns the loaded indicator and subsequent execution.

Preference application, conditional scenario-loaded execution, audio/title
notifications, home-directory return and real U01 cleanup keep their specified
order. The owning S02 interpreter and N02/P10/V01 children remain separate work;
these interfaces do not claim that those algorithms are complete.

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

A subsequent [live-session Windows exchange](m1-original-live-save.md) uses
`oc2_live_save HOME INPUT.SAV NEW_OUTPUT.SAV [--gold CIV VALUE]` with the real
F03 wrapper and F06 core. The original accepted a live export with a declared
treasury edit, saved it, advanced a turn and reopened a second C++ live export
of its resave. This closes a bounded current single-player edit/resume case;
post-load admission, graphical resources and broader file/mode coverage remain
open. The driver requires native tools/tests and a new explicit `.sav` or `.SAV`
destination; its headless inputs and four CLI cases are documented separately.

## Remaining M1 work

Only the seven [closeout gates](m1-closeout.md) govern remaining M1 work;
the [audit](m1-validation.md) records their status. This slice contributes existing
codec/load/save regressions and the bounded M1-04 original-file checks. Legacy
stored views, broader resumed gameplay edits, full application admission and
original-peer sessions join M3–M6. Do not advertise a loaded-world view based on
stored records or turn historical next steps into new M1 requirements.
