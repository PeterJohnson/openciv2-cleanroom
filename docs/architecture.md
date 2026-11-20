# Architecture

The application uses C#/.NET 11, Avalonia desktop/browser hosts and portable
core libraries. The bounded foundations, CLI, session application and Lua bridge
are implemented; [validation](validation/README.md) records native and browser
evidence and the remaining desktop gates. The [implementation plan](implementation-plan.md)
distinguishes current functionality from later gameplay and platform work.

## 1. Cleanroom and compatibility boundary

The behavioral source of truth is this repository's
[algorithm specifications](algorithms/README.md), interpreted under
[X01](algorithms/X01-classic-compatibility-contract.md). The target is Civilization II
Multiplayer Gold Edition **5.4.0f, patch 3**. Architecture decisions cannot weaken
that contract; discrepancies must be resolved in the owning specification.

Implementation rules:

- Write new code from these specifications and their declared input formats.
  Do not read, copy, translate, link, generate from, or otherwise derive game code
  from `openciv2` or the Civ2 decompilation/reconstruction work. This includes
  their renderers, build helpers, tests, and historical execution harnesses.
  External code links, filenames and source-derived harness notes belong only
  in clearly marked validation/provenance sections. They record historical
  research and do not authorize consulting those sources for implementation,
  tests or build integration. Keep implementation catalogs and algorithm prose
  free of source-navigation shortcuts; specify required formats and behavior here.
- Use the original game as an external comparison and interoperability target.
  Independently authored drivers may supply inputs and capture observable state,
  files, packets, and interaction outcomes. Original executable code is never
  linked into the game or its portable tests.
- Record behavioral ambiguities as specification gaps, with the smallest
  affected operation and input domain. Obtain an original-game observation or
  clarify the specification before depending on an invented behavior. Keep
  provisional behavior and restricted prototype domains explicitly labeled.
- Each implemented operation and behavioral test cites its specification ID and
  section. Expected results distinguish specification-derived expectations,
  independent original-game captures, and implementation regression snapshots.
  A snapshot produced by our own implementation does not establish compatibility.
- General-purpose dependencies such as .NET, Avalonia, Skia, Lua, SQLite and independent
  codecs are permitted. Record pinned versions and notices. The designated
  `PeterJohnson/openciv2-assets` runtime content is an allowed data dependency;
  its comparison tools and old-game source integration are outside the game build.

Required compatibility includes simulation, classic AI and map generation,
scenario-authored content, bidirectional original save/map/scenario interchange,
hotseat, and original-peer TCP/IP multiplayer with either engine hosting.
Presentation has broad design freedom: preserve game rules, meaningful
information, privacy, accepted commands and actual protocol timing, while loading
original configuration/scenario content and retaining a familiar overall feel.
Exact prompt operation, navigation, sorting, formatting, drawing and native UI
behavior are reference only. IPX, modem and serial remain optional under X01.

F01 permits a new reproducible RNG policy. It does **not** permit changed
sampling laws, AI heuristics, map-generation rules, arithmetic, deterministic
ties, or rerolling a result received from another peer. Classic file/wire formats
must work without new RNG metadata.

### Fresh-game boundary and bug repairs

X01's fresh-game isolation and documented safety repairs override historical
retention and call-order descriptions. Starting a new game is an explicit session
operation before chosen content is loaded, not a menu-rendering callback. Replace
the world, derived/workspace state, mutable rules/text/naming state and file scratch;
preserve only user preferences, application configuration, immutable content caches
and identity generators. Complete/cancel admitted operations and drain required
network output, revoke old clients and discard their queues before replacement.
Load/resume and within-game civilization creation retain their distinct contracts.

`SessionRuntime.PrepareNewGame` establishes the implemented boundary;
`enter_new_world` invokes it before rules and setup, then resets common start
positions and its selected source. `enter_classic_new_world` composes the classic
file-scratch reset at that boundary.
The rule provider keeps its identity for adapters but its contents are fresh. The
caller supplies the chosen RNG context explicitly; no hidden draw/reseed occurs.
Cold-start and previously used runtimes must produce equivalent gameplay with the
same inputs. Do not require old AI-goal refresh, old free-slot damage or old naming
cursors to initialize a game. References into replaced state are invalidated.

Within a game retain state that affects later decisions, ties, files or messages.
Pure queries have no invented callback/mutation contract. Confirmed invalid-memory
or uninitialized-local behavior uses a documented narrow repair; diagnostic
original-stack cases are not production inputs. Missing behavior remains an owner
gap until resolved from the contract or independent original-game observations.

## Compatibility surfaces in the implementation

X01 and each owner's Production contract scope distinguish game behavior,
external interchange and optional executable comparison. The core owns logical
rules/state and meaningful retained inputs. Format/protocol adapters own external
layouts, opaque imported fields and valid translations. Comparison tools own
original private memory/ABI/native traces; production does not depend on them.
Independent original-game file/peer tests remain required evidence for supported
interchange, separate from optional trace matching.

Architecture choices here (libraries, records, services and APIs) are project
design decisions, not claims that classic compatibility requires those shapes.
Pass operation contexts explicitly; preserve true data flow rather than globals
or aliases. Only real admitted commands/events/responses constrain live reads.
Allocation, drawing, formatting and diagnostic callbacks do not create game
interleavings. Codecs/checksums use defined byte images rather than native structs.
Staged import and atomic output replacement are allowed; accepted game/peer
effects remain committed and all real failures are reported truthfully.

## Production and reference boundary

Production code implements semantic information, choices, authored content and
required game/file/network effects. Original storage addresses, pool ordinals,
allocation extents and padding, heap capacities, handle ownership, widget flags,
private buffers, native-control traces and allocator call order are comparison
details only unless an owning file/wire/resource contract explicitly consumes
them. Do not add them to live state or service interfaces to match a reference
snapshot. Original trace emulation belongs in optional tools/tests which
production targets neither link nor depend on.

Classic adapters remain production code: they translate externally meaningful
indices, encodings and results into the semantic model. For each representation
detail, identify its actual external consumer; compare resolved values or encode
through that adapter. Allocation-failure tests may check resource safety, but
cannot invent gameplay callbacks, per-allocation publication or reentry. Harmless
UI failures do not prescribe original fatal-error timing. This boundary overrides
mixed historical excerpts and stronger storage/trace language below.

### Logical text and classic name markers

Live names use full-width implementation-local references or owned strings and
explicit custom-name flags, never signed 16-bit pool ordinals. UI label rebuilds
must not rebind retained rule names. Reload policies preserve or replace semantic
names/flags; preserved fields need no discarded allocations. Original padding,
last-allocation pointers, minimum extents and heap-size hints are comparison-only.
Empty padded-name input still means one space; ordinary empty classic pooled
input resolves to `<nil>`. These content rules do not require padded storage.

F06 stored leader records retain their exact 48-byte encoding in a separate codec
type. Live loading rebuilds names from chosen rules and consumes saved signs at
the specified stages; saved magnitudes do not index replacement text storage.
Live export writes custom/noncustom markers as -1/+1 independently of local IDs.
Lossless stored-file decode/re-encode preserves original magnitudes, including
zero and -32768. This canonical live encoding follows F06's sign-only restoration
contract; it does not claim equality with original allocation-dependent exports.
Repeated selected-tribe effects retain their specified behavior.

Narrowing a text ID into an unrelated earlier name or an accidental custom-name
marker is a repaired storage defect. Preserve intended names and flags beyond
32767/65535 entries. Reference tools may report original ordinals/aliasing;
production tests assert text, reload behavior, bounds and file-marker semantics.
Unrelated scalar arithmetic and external field widths remain unchanged. This
contract overrides original-storage interpretations elsewhere in this document.

## 2. Dependency structure

Arrows below mean “uses.” Platform implementations fulfill interfaces declared
in the portable libraries; they are selected by the executable, not constructed
by the game.

```mermaid
flowchart TD
    app[Graphical application] --> frontend[Avalonia / custom Skia frontend]
    app --> platform[Native menus / storage / transport adapters]
    frontend --> views[Presentation models]
    views --> ports[Participant and controller API]
    session[Embedded SessionRuntime] -. implements .-> ports
    app --> session
    frontend --> console[Portable console]
    console --> ports
    console -. optional .-> automation[Lua automation]
    automation --> ports
    cli[Save inspection CLI] --> formats[Rules / save / scenario codecs]
    cli --> query
    cli -. optional .-> export[SQLite snapshot exporter]
    export --> query
    export --> sqlite[SQLite]
    runner[Headless runner / managed tests] --> session
    runner -. optional .-> automation
    runner -. optional .-> export
    session --> game[Game state / mechanics / AI / events]
    session --> query[Read-only queries]
    session --> formats
    session --> network[Classic protocol and network sessions]
    network --> io[Portable byte and file contracts]
    network --> game
    formats --> game
    query --> game
    query --> formats
    frontend --> content[Asset package / image / font / media data]
    content --> base[Types / arithmetic / randomness / service contracts]
    game --> base
    platform --> base
```

The participant/controller contracts are portable public APIs; Session implements
them and the application composition root constructs the runtime. View models,
console and scripts receive scoped clients, never mutable world access. Classic
AI policies remain Core operations scheduled by Session. Local play opens no
transport and needs no separate server process.

Use a small number of projects with internal subsystem directories; the 65
specifications are ownership boundaries, not 65 assemblies. The root
`OpenCiv2.slnx` builds the core and CLI. Optional solutions live in `solutions/`.
The source guide identifies existing projects; Content and browser platform
adapters are introduced when their M2 responsibilities require them:

| Project | Responsibility and allowed dependencies |
| --- | --- |
| `OpenCiv2.Base` | Logical IDs, explicit arithmetic, RNG policy, clocks and diagnostics; .NET only. |
| `OpenCiv2.IO` | Byte codecs and storage contracts; Base only, no game records. |
| `OpenCiv2.Contracts` | Public player identities, requests, observations, scoped client and setup/configuration interfaces; Base only, no mutable engine types. |
| `OpenCiv2.Core` | State, rules, world/unit/city/civilization mechanics, AI and events; Base and Contracts, no database or GUI. |
| `OpenCiv2.Formats` | Native content and classic save/map/text/scenario codecs into common owned models; Core and IO, no filesystem calls. |
| `OpenCiv2.Query` | Stored/live projections, diagnostics and stable output; Core and Formats. Pure queries do not load sessions or recalculate gameplay. |
| `OpenCiv2.Network` | Classic encodings, protocol state machines and synchronization; Core and IO, with injected transport/time. |
| `OpenCiv2.Session` | Runtime, authority, scoped clients, command dispatch, setup/load/save, continuations, AI scheduling and effects; composes Core, Formats, Query and Network. |
| `OpenCiv2.ViewModels` | Observer-filtered map/dialog/report/menu models and intent mapping; Contracts, with no mutable engine dependency or Avalonia types. |
| `OpenCiv2.Console` | Parsing, help/completion and structured results through a scoped client; Contracts, no required GUI or scripting runtime. |
| `OpenCiv2.Automation` | Optional Lua adapter, task scheduling and versioned task data through Contracts; native/WASM bridge isolated here. |
| `OpenCiv2.Content` | CPU asset data, identities, lookup and independent decoders; no GPU objects. |
| `OpenCiv2.MapRendering` | Optional read-only Skia map renderer, scene/camera/minimap geometry and cosmetic timelines; Base, Contracts, Formats and SkiaSharp. No Avalonia, Session, Query or Network dependency. Formats currently supplies graphics data and transitively references Core. |
| `OpenCiv2.IO.Native`, browser adapters | Storage, time and transport implementations; native headless I/O has no Avalonia dependency. Browser-specific JS interop stays in the host adapter. |
| `OpenCiv2.Platform.Native` | Native adapters that compose Formats with IO.Native, including classic byte-text streams. No graphical dependency; excluded from portable/browser engine graphs. |
| Avalonia frontend and desktop/browser hosts | Dialogs, console panel, map controls/input and platform menus; MapRendering, view models, content and console. Hosts lend their current Skia context to the renderer. Only composition roots construct Session and platform services. |
| `OpenCiv2.Cli` (`oc2`) | Native inspection, validation, diff and content tools over Formats, Query and native I/O; explicitly supported live analysis additionally uses Session. |
| `OpenCiv2.Export.Sqlite` | Optional native snapshot exporter over Query and SQLite; never a dependency of Core, Formats, Query or Session. |
| Managed tests, conformance adapter and later headless runner | Test/CLI composition roots over portable libraries; no graphical dependency. |
| Native browser transport bridge | Separate optional service carrying transport bytes/events, not game simulation. |

The [source guide](code-guide.md) maps projects to entry points and test hosts.
`tools/dotnet/check_graph.py` checks project/package boundaries and locked graphs.

Keep interfaces small, constructors explicit and internal state private. Use
interfaces for cohesive service boundaries, not for every algorithm. Avoid a
general ECS, reflection-based plugin framework or global service locator.
Project/API dependency checks must prevent UI code from reaching mutable state;
namespace conventions alone do not enforce that boundary.

### Managed interface and ownership choices

Use small value types for IDs and numeric records, and owned records/classes for
larger decoded values, requests, results, rules and observations. Use static
functions for algorithms whose state and dependencies are explicit operands.
Keep format records separate from live state and encode fields explicitly.
Read-only properties do not make a shared array immutable; snapshots must copy or
otherwise own their backing data. Keep borrowed spans within synchronous calls.

Live text uses managed strings with explicit classic encoding conversions. Binary
flags use `bool`, and choices with specified meanings use named enums. Counts, bitmasks, indices, sentinels and
specified wrapping arithmetic retain their distinct semantics. Classic field
widths, terminators, padding and noncanonical flag words belong to stored codec
types. Explicit conversions normalize nonzero flags to true and export them as
0/1, omit text padding and reject unrepresentable text instead of truncating it.
Lossless stored decode/re-encode remains separate from a live import/export.

Resource exhaustion is fatal at the application boundary. Allocation
failure propagates; command/content recovery must not convert `OutOfMemoryException`
into a recoverable game result. Production interfaces have no simulated allocation
admission or release callbacks. Real I/O, malformed-input and admitted interaction
failures retain their own error contracts.

Different content formats converge on common owned domain data before gameplay
behavior begins. Keep classic and native loaders in separate components and source
files; neither loader depends on the other. Use a format-specific runtime service
only when a required ongoing interaction cannot be represented as data. Codec
syntax, scanner state, file cleanup and provenance do not belong in gameplay
interfaces. An individual name-comparison policy may be injected independently
of storage; it does not own a reader or a naming protocol.

City-name loaders produce `NameCatalog`, whose ordered layers keep each
source's civilization lists and fallback together. `CityNames.DefaultName` owns the
shared counter/retry/publication behavior. City-preference loaders produce
`CityPreferenceUpdate`, applied by `CityPreferences.Apply`.
Classic and native rule loaders produce `RuleInput`; `RuleInstaller`
owns installation, name retention, live prerequisite fixups, movement scaling and
ordered title overrides. Decoding may stage unpublished inputs and must not
mutate a live `RuleState`. Load selected rules once and supply them explicitly
for dependent component validation; those components do not retain the whole
rule pack. Native provenance stays outside the common inputs. Dependency checks
prevent gameplay from depending on formats and native loaders from depending on
classic parsers.

Classic save decoding produces owned `StoredSave` records for versions 0x26,
0x27, 0x28 and 0x2c. Conversion produces `GameInput`; `GameInstallation.Install`
owns derived values, password randomness, entity lifetimes, name restoration and
mode adaptation. Raw maps similarly converge on `MapInput`. Decoding and
file close complete before installation; short files do not publish record prefixes.
Map allocation is staged before replacing existing storage. Live exports preserve
opaque imported fields in adapter-owned metadata tied to world, entity and event
lifetimes. Reusing a slot or replacing a world cannot inherit old opaque bytes.
Stored codec round trips retain the complete admitted representation. Unknown
legacy preference settings stay in stored records; live preference exports use
canonical zero-filled legacy fields and text tails.

`ClassicRuleContext` is an input source, returning owned `LoadedContent` once.
The installer reuses its `RuleInput` for final name-preserving updates. There is
no parser reload/close protocol in the load workflow. Classic label parsing
returns `LabelCatalog`; pool installation and language/cache setup are separate.
New-world setup uses the same `LoadedContent` factory and installer, including
final name-preserving table updates from that one parsed input. Scenario event
sources return `EventCatalog`; shared installation prepares the replacement
before publishing it. No event parser receives a mutable live collection.
Preference loading snapshots its data operands, borrows the storage provider,
reads and closes both items, and only then installs the values/defaults. Close
failure leaves prior preferences intact.

Events contain semantic values and owned string identities. Only the codec has
serialized presence words. Collection-controlled IDs and read-only access protect
identity; compaction prepares detached strings once and commits them to the same
live events. Required text-hole aliases survive. Resource admission hooks are
not gameplay reentry points or original allocator trace emulators.

Use classes to enforce invariants, resource ownership and state transitions.
`PlayerClient` protects authority; `SessionRuntime` owns execution;
`ClassicSaveOperation` owns a suspended save; `ClassicLoadOperation` guards a
load through its post-load callbacks; `UnitOperation` owns pending unit requests
and whole-stack continuations; `SessionActivity` pins the world during those
operations and an active option vote. `pin_world()` supplies a nested storage
guard for synchronous configuration/setup, including before client admission;
it does not admit a second owner workflow. Commands already protect their world
while pending. Their unit children take `OperationContext` and do not attempt
standalone workflow admission.
Public client/value contracts do not expose mutable state or engine operations.
The existing setup/configuration value families live in Contracts, available
to view models without linking Core or Formats. Identity values and capacities
remain separate from process token generation and slot reservation.

A scoped client checks session identity, lease and revocation on every operation.
Copying a managed reference does not extend authority. Dispose/revoke explicitly;
do not wait for garbage collection to retire a client, channel or world guard.
Internal runtime state stays internal, and public API checks prevent accidental
exposure through return types or generic arguments.

Use interfaces for providers with stable implementation identity: storage,
movement/deletion services, transport, music, countdown, broadcasts and setup
interactions. Validate required providers at construction or selected-branch
admission. A delegate can represent an individual policy/notification; retain its
owner for the operation lifetime and preserve live reads. Do not accidentally
replace a shared provider with a copied snapshot or capture a stale world.
Dependency descriptors contain provider references, not cloned service state.

Suspended operations own requests, results, chooser/channel and continuation data.
They retain dependencies for their whole lifetime, including cancellation and
cleanup. Use explicit `IDisposable`/`IAsyncDisposable` lifetimes for resource and
operation guards, and owned handles for native resources. Finalizers are not a
semantic completion mechanism. Cleanup must not mask an earlier failure. An
explicit close reports its own error and is attempted only once, even if it fails.

Common new-world setup consumes decoded `PremadeWorldInput`, common starting
positions, and a `PreferenceWriter` taking an owned preference snapshot. Classic
raw-map decoding, CIV2.DAT encoding, and path/scratch reset are separate adapters.
Preference writers invoke the borrowed publication notification at actual I/O
boundaries. Source reset follows runtime isolation and precedes content loading;
selected dependencies are validated before either reset.

Setup and configuration validate selected dependency handles before changing
state. World entry validates the selected branch before replacing the old game
or revoking clients. Missing handles are composition errors; a failure raised
by an implemented child retains its specified completed prefix. Snapshot data
when a coherent input/export is required, independently of provider lifetimes.

Generic byte streams and storage belong to `OpenCiv2.IO` alongside byte codecs.
They must not import game records,
format adapters, or sessions. Classic/native I/O adapters depend on these ports;
the ports never depend on their adapters. `CoreInput` and `CoreOutput` designate the
existing file contracts to port.
Their shared close state ensures finalization is attempted once, including a
throwing close; implementations release resources through explicit disposal.
Codecs accept bounded read/write operations, with no host-path knowledge.
`MultiplayerOptionOperation` owns an
`OptionVoteChannel` and yields after proposal delivery. Each subsequent poll does
bounded transport work and evaluates the wrapping deadline. Presentation observes
owned proposal data; it does not own a blocking modal protocol or vote callback.
Cancellation, failure and completion release the channel before the world guard.

Local stack/deletion mechanics never wait for transport. `UnitOperation` selects
local authority or owns a `UnitRequestChannel`, and yields after send.
Each poll performs bounded work, tracks the wrapping deadline and drains queued
replies without an internal message loop. Cancellation releases the channel and
session guard. Clocks and timeout notifications remain individual policies.

Transport send exceptions mean no bytes were accepted. The send queue retains
its bytes and offset until it receives a result; an adapter that accepted bytes
must report that outcome rather than throw. These ownership rules do not alter
wire bytes, response deadlines or the different immediate/queued error results.

## 3. State and deterministic execution

Use ordinary C# records/classes, value types and arrays as the authoritative
runtime state.
Start with typed `Unit`, `City`, `Civilization` and `Tile` records, arrays or
stable slot pools for entities, and contiguous map storage. Reference entities
through typed identities instead of long-lived object references that can
outlive an entity or world. Keep visibility/remembered layers, retained caches and
operation workspaces explicit. Choose storage layout for clarity and measured
access patterns while preserving the specified logical behavior.

`GameState` contains logical world state; F06/N02 codecs own classic file/wire
layouts separately. SQLite is only an optional derived export for inspection
and analysis, never the live state store or a second authoritative copy. Game
operations, AI and user automation access managed state through their scoped
interfaces. No SQL statements, database transactions or schema constraints
govern gameplay mutations, iteration or publication boundaries.

Separate process-retained state, session state, world state, retained
derived/scratch state, and presentation-local state according to
[F02](algorithms/F02-game-state-and-invariants.md). A new game resets simulation and mutable content state; loading imports explicit
saved state. Preferences and identity generators have separate process lifetimes.

Use typed civilization, unit, city, rule, and connection identities. Preserve
slot allocation, holes, scan bounds, lifetime tokens, list order, sentinels, and
retained values where specified. Do not compact pools or eagerly repair/recompute
caches. Distinguish the turn civilization, local player, viewing civilization,
human roster, and network executor. Validate a slot/token pair only where that
operation's contract calls for lifetime validation; bare classic references
retain their specified meaning.

Retained summaries remain explicit fields even when they could be recomputed
from other records. A computed count or analysis query must not replace a stale
census that a later operation is specified to read. Preserve live reads and
scan-bound changes where required; a snapshot is an observation, not a general
replacement for the algorithm's state access.

One execution lane owns mutation of each local runtime's session state; N01–N03
determine which peer may execute an operation in a network game. The initial
application runs the lane cooperatively on the main thread; tests drive it
directly. Background work may read/decode immutable data or move bytes, but does
not mutate game state. AI
uses the same mechanics as human play with its own specified decision and
authority paths. UI availability rules must not accidentally become AI rules.

Implement F01 conversions, signed views, wrapping, division, and rounding
explicitly. Do not depend on signed overflow, struct layout, host endianness,
unordered-container traversal, or library-specific random distributions. Preserve
floating-point evaluation order and verify exact downstream bits across JIT,
ARM64 and browser builds; do not introduce fused or reassociated operations.
Use the specification's reference evaluation order until an owner-backed equivalence
argument and tests justify a change.

Use `WrappingI32` for expressions whose specified intermediate domain is signed
32-bit arithmetic. It delegates to the F01 helpers, preserves wrapping through
operators and requires explicit native-integer conversions. Keep the distinct
clamps and signed-count shifts as named operations; other numeric domains and
file/wire encodings retain their own explicit boundaries. See the
[implemented arithmetic interface](m1-foundations.md#wrapping-integer-expressions).

### Randomness ownership

Presentation-only randomness **must be independent of gameplay randomness**.
Use versioned contexts and semantic sampling sites, with explicit ownership:

| Randomness | Owner and consumers | Examples |
| --- | --- | --- |
| Gameplay | Session-owned contexts supplied only to the executing mechanics, AI, map generation and events, under the existing authority rules. A dialog does not change that ownership. | Combat, AI choices, world generation, diplomatic decisions that affect offers, treaties or other game state. |
| Presentation content | A separate session-owned context for portable semantic report/dialog operations. Resolve choices once at their specified operation boundary and publish them in observer-filtered models. | P07 historian name/ranking kind, P09 council response continuations, V05 message variants that only select text, F04 council invitation location. |
| Cosmetic presentation | Frontend-owned contexts, or equivalent injected contexts for optional presentation tests. These cannot advance either of the other contexts. | City-view scenery, spaceship stars and typing delays, reveal effects, stock sound variants. |

The distinction depends on every consumer of a sampled value, not its caller's
name or source file. If one choice affects both game state and presentation,
sample once in the gameplay operation and pass the resolved result to the view;
do not split it into independent rolls. A report's required city recalculation,
AI invocation or other gameplay callback still executes as a named session
operation with its own applicable gameplay context. Opening a council or report
is therefore not automatically a pure query. Rendering never executes that
operation implicitly.

Enforce the separation in the service interfaces: renderers, media callbacks and
view-model refreshes cannot access gameplay samplers or reseed gameplay contexts.
Presentation content selection is also independent of cosmetic draws, so changing
frame rate or skipping an animation cannot change the next historian or council
response. Initialize the contexts independently, or derive their initial state
from declared seed material and stable domain labels without drawing from another
context. Do not seed presentation by consuming gameplay RNG, copy presentation
state back into gameplay, or reproduce the original shared-stream/reseed coupling.
Context identities cannot depend on native handles, allocation addresses, clocks
sampled during rendering, or worker scheduling. Frame rate and worker scheduling
cannot choose which gameplay decision receives a sample.

The managed generator is PCG32 under the versioned policy below. Each retained selection keeps
its owning specification's sampling law, including F01's range/modulo biases,
thresholds, eligibility, retries, deterministic ties and reuse of sampled values.
This includes meaningful report content and scenario-authored response choices;
isolation does not authorize replacing their selection rules. Purely cosmetic
behavior may change under X01's presentation policy. Omitting an unused sample
must not omit required callbacks, mutations, resource effects or decision points.
Original "primary" and "secondary" names identify sampling laws, not these new
ownership domains. See [F01's random-context contract](algorithms/F01-determinism-and-arithmetic.md)
and the [X01 presentation policy](algorithms/X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy).

Tests inject controlled choices by semantic site and domain. Gameplay replay
records complete gameplay contexts and ordered external inputs, including player
responses and required callbacks. Preserve resolved interaction content and any
presentation-content state needed to resume a pending operation or reproduce its
semantic trace. Full visual replay may additionally record cosmetic contexts;
gameplay continuation cannot depend on them. Headless execution resolves required
semantic interactions and effects without running animation or sound samplers.
Original-peer results remain authoritative and must never be rerolled by a local
presentation consumer. Separation adds no required classic save or wire fields.

Contexts use explicit PCG32 from
`RandN.Pcg32` under [policy version 1](random-policy-pcg32.md), including controlled
choices, traces, bounded version-2 checkpoints and versioned X02 replay fixtures.
The policy defines seed/stream derivation, raw-code projections, counters and
bounded replay restoration. F01 sampling laws and context separation are preserved;
general-game checkpoint performance and remaining platform checks stay open. A fixed seed
under the new policy must reproduce across desktop, browser and future runtime
upgrades. `System.Random` does not supply that versioned contract.

The [MT19937-64 policy v2 document](random-policy.md) and its hashes remain
historical evidence. Update policy-dependent replay fixtures with explicit new
provenance; controlled-choice checks do not change their laws. Compare old/new
mechanics using supplied semantic choices, not equal-seed stream equality.
Version RNG-bearing checkpoints and reject unsupported old-policy inputs rather
than silently resuming them under a different engine. Classic saves and wire
formats remain unchanged.

### Saves and checkpoints

Classic saves and new-engine checkpoints serve different purposes. F06 codecs
preserve classic state and load transformations. A separately versioned replay
checkpoint records additional RNG/process/continuation state needed at its
supported checkpoint boundaries. Optional sidecar data is tied to the exact
classic file hash and policy version; absent or stale metadata never prevents
classic import. Define and record a fresh-context import policy in that case.

## 4. Commands, interactions, and event delivery

### Embedded session service

Make `SessionRuntime` in `OpenCiv2.Session` the service
boundary for gameplay clients.
It owns the local session's managed world state, turn/mode continuation, command
routing and pending interactions, and uses an explicit process context for F02
state that outlives a game. Starting another game reuses that context where
required; independent peers in one test process receive separate contexts.
The frontend owns windows, input and presentation models, and communicates with
the runtime using semantic managed values through a scoped client API.

Single-player and hotseat embed the service in the graphical process. Use direct
calls and owned queues as appropriate; no loopback sockets, RPC serialization,
extra world replica or worker thread is required for local controllers. The same
runtime can be driven without Avalonia by tests and headless tools. This decision
does not introduce a dedicated server executable or a new network protocol;
separating processes later requires its own interaction and compatibility work.

| Mode | Runtime and controller arrangement | Scheduling and authority owner |
| --- | --- | --- |
| Single-player | One runtime, a human participant's frontend/automation bindings, and local classic AI policies | F04/F05; engine schedules upkeep and AI phases |
| Hotseat | One runtime and world, multiple participant contexts, one frontend attached to the permitted view at a time | N04 handoff/concealment with F04 children |
| Classic network | One local runtime per peer, local controller bindings and remote connection mappings | N01–N03/N05 assign execution, result application, barriers and host transfer |
| Headless | The same runtime with scripted decision providers and injected services; multiple isolated runtimes can represent peers | The selected mode's rules, driven by a deterministic test scheduler |

Mode adapters implement their specified entry, continuation, resume and failure
paths. They share mechanics and client-facing contracts but do not replace
F04, N04 and N03's distinct loops with a generic round-robin `takeTurn()` loop.
The runtime schedules global/civilization upkeep, scenario effects and AI work;
controllers supply decisions only where their operation admits them.

### Participants, controllers, and access

Keep the following identities separate and map them explicitly:

| Concept | Meaning |
| --- | --- |
| Civilization | F02's stable game slot and its world records; distinct from current actor/observer roles |
| Participant | A session seat controlling a civilization under the mode's roster/claim rules; can be human or AI controlled |
| Controller | A decision source or attached policy: frontend input, user automation, scripted test driver or classic AI policy; changing it does not replace the civilization |
| Connection | An optional transport endpoint with N01/N05 identity and mapping; local humans and AIs need no network connection |

The runtime issues a `PlayerContext`/binding with participant, observer scope,
permitted command class and binding lifetime. Clients cannot gain authority by
supplying another civilization ID. Multiple input sources for one participant,
such as a console and an automation task, share that participant's admission
rules and explicit command ordering. The effective human set, hotseat roster,
turn actor and network executor retain their specification-defined state;
do not derive them solely from a controller's implementation type. Assisting a human with
Lua does not turn that civilization into a classic AI.

The service API exposes command submission, correlated results, scoped read-only
queries/events and interaction replies. `OpenCiv2.Query` supplies projection logic;
public observation values belong to Contracts. Live clients obtain records
through their session binding, not
by passing themselves unrestricted world state. Offline inspection uses its
separate explicit access context. No mutable `GameState` escapes to a client.

Bind requests and notifications to the session and controller lifetimes as well
as the operation's target identities. On handoff, disconnect, controller
replacement or session replacement, revoke stale input access and clear or
isolate affected presentation/subscription data. Rebinding requires current
authority checks. Previously admitted operations retain their specified
continuations, effects and mandatory replies; revoking a controller is not a
rollback or permission to abandon network obligations.

Classic AI participates in this control model as an engine-scheduled policy,
not as an external snapshot-only bot. A01 assessment mutates shared retained
state; A02 advice/production selection can invoke recalculation, sales,
irrigation and airlift operations. Preserve those live reads, side effects and
the specified AI access/admission paths. Share action vocabulary and mechanics
with human controllers where behavior agrees, but do not force AI through human
menu availability or observer restrictions. Human advice may call an AI-owned
operation without changing who controls the civilization. User scripts continue
to use the ordinary player's capabilities.

Session creation, setup/load and shutdown use explicit lifecycle operations with
their F05/F06/mode admission rules. They do not require inventing a civilization
binding before a game exists or granting a player unrestricted lifecycle access.

### Command execution and interaction delivery

Portable interfaces carry semantic data, for example `MoveUnit`, `ChangeProduction`,
`OpenCity`, `ChooseTechnology`, and `ReplyToOffer`, with explicit actors and
targets. Platform events, widget IDs, and mouse pixels never enter mechanics.
Map hit testing converts pixels to W01 tile identities in the frontend.

The runtime's mode/authority adapter resolves a command's route, authority, and
current execution checks, then invokes its owning operation. Menu availability
and execution validation remain distinct P05 contracts. Network commands distinguish
requests that execute work from messages that apply already-resolved results.
Internal AI and scenario callers retain their own admission paths.

The client contract permits pending results and decisions even in local play.
Immediate completion is allowed at a valid execution boundary; callers never
assume submission means completion or poll mutable state to infer success.
Network operation requests and received resolved results have separate internal
dispatch paths: a result is not resubmitted as a new player command. Local
bindings do not bypass actor, busy-state or execution checks.

Long-lived operations use explicit continuation/state-machine records. They can
request a decision, wait for I/O or a peer, publish an effect, or complete.
An interaction carries a stable identity, recipient, target, allowed choices,
result mapping, deadline/default policy, and continuation. A headless driver can
answer the same interaction as a dialog. Completion, cancellation, and timeout
are distinct inputs; they do not imply rollback of previously committed edits.

Service relevant incoming events at the owning specification's publication and
reentry boundaries, preserving captured values and rereading live values when
required. Do not replace all interleavings with one end-of-frame queue drain or
make every command atomic. Conversely, arbitrary paint calls do not introduce
new semantic callbacks. Revalidate pending targets according to their contracts
after deletion, ownership changes, remote results, or turn expiry.

Avalonia hosts collect input, advance eligible session work, obtain presentation
models and invalidate drawing as needed. The core owns dispatch boundaries and
has no dependency on Avalonia's dispatcher. Tests and CLI drivers advance the
same runtime directly. Async I/O can deliver bytes but cannot mutate the world
concurrently. Yield expensive work without exposing new intermediate state or
admitting events at invented boundaries. Browser execution must cooperate with
the browser event loop.

The network service and deadlines must remain responsive during dialogs,
animations, native menu tracking, and file selection. Native menu tracking loops
need an explicit integration test and service strategy; an I/O worker alone
does not prove that protocol dispatch or deadlines remain correct. Browser
suspension is an external interruption handled through N01/N03 timeout/reconnect
rules, not a promise of background execution.

## 5. Inspection tools, console, and user automation

These include planned engine features and the implemented
[stored inspection subset](m1-inspection.md), not changes to classic simulation
rules or claims that the current shell supports gameplay commands. Keep user
automation distinct from the specified classic AI and S02 scenario event system.

### Shared queries and command contracts

Expose a portable query service independently of display-specific view models.
Return owned, read-only records with stable IDs, ordering, observation revision,
and provenance (stored, remembered, currently visible, or explicitly computed).
Do not expose mutable engine objects or Lua userdata that can modify world state.
The session supplies the caller's observer and authority context; scripts cannot
select an unrestricted observer or grant themselves editor privileges.

Ordinary player queries expose only that player's permitted knowledge under
W03/N04. Apply the same filtering to events, errors, completion suggestions,
and retained console output. Omniscient offline inspection and developer/editor
diagnostics use separate, explicit access contexts. Inspection of a save does
not grant privileges in a running session. A query must not secretly run a city
recalculation or consume gameplay RNG; required report/dialog effects remain
named session operations with their specified publication boundaries.

Add a versioned command descriptor registry alongside session dispatch: semantic
name, typed arguments and target identities, help text, access class, and result
schema. Console parsing, completion, scripts and tests consume these descriptors;
the owning operation still implements validation. Availability is a hint with
P05 semantics, not authorization to execute later. Player automation uses the
player admission path, never the internal AI or scenario path.

Each submission gets a correlation identity and explicit lifecycle results:
rejected, pending, awaiting interaction, completed, cancelled, or failed.
Distinguish acceptance from completion and disclose any effects already committed.
Bind interaction replies to their current identity and recipient. Revalidate
stale targets and authority when execution resumes, following the operation's
contract. Console batches and scripts are sequences of ordinary commands, not
transactions; cancellation or a script error does not undo earlier commands.

### Saved-game inspection CLI

Provide a native `oc2` executable that works without Avalonia, Skia, graphical assets,
or a Lua runtime. These commands are implemented for current stored saves and
raw maps; their [output contract and limits](m1-inspection.md) are explicit:

```sh
oc2 inspect game.sav --cities --json
oc2 validate game.sav
oc2 diff before.sav after.sav --json
```

The default view is the decoded file as stored. F06's record codecs preserve
field widths, retained bytes, slot holes and raw identifiers without executing
the gameplay load sequence. Report source format/version, supported sections,
unknown or uninterpreted data, and field/offset diagnostics. Missing external
rules leave rule-dependent interpretation unavailable rather than fabricated.
Separate structural errors, compatibility limitations and semantic diagnostics;
a successful parse alone does not certify a playable save or conformance.

Offer an explicit loaded-state mode only when the relevant F06/session path is
implemented. It runs in an isolated session with declared rules, entry state,
RNG policy and supplied services, and labels transformations separately from
stored values. Actual loading renews lifetimes, normalizes fields, rebuilds
selected counters and can consume randomness; it is not a neutral decode.
An incomplete load produces an explicit partial result, never a successful view.

Inspection, validation and diff never rewrite their inputs or repair state.
Provide stable, versioned JSON output with lossless integer representation,
deterministic ordering, diagnostics separate from data, and documented exit
statuses. Diffs distinguish byte changes from decoded field changes and match
records by file slot/identity semantics, not newly allocated runtime lifetimes.
Reuse the pure decoding/query APIs for future browser file inspection; the
native CLI itself is not a browser requirement.

### Optional SQLite analysis export

Add a native CLI export over the same decoded records and query projections:

```sh
# Stored domain; requires the optional SQLite-enabled CLI package.
# Build solutions/OpenCiv2.Sqlite.slnx; invoke the oc2-sqlite executable.
oc2 export game.sav --sqlite analysis.sqlite
```

The implemented [schema, publication boundary and evidence](m1-sqlite-export.md)
cover stored current saves and raw maps; loaded/live export remains future work.

Default to stored-file data, with the same explicit opt-in for loaded-state
interpretation as inspection. Later, the headless runner can export selected
session snapshots for cross-turn comparisons and conformance diagnostics. Capture
live state on its owning execution lane at a declared observation boundary into
owned immutable records; database writing may then proceed independently. Do
not read a changing world piecemeal while inserting rows or add gameplay callbacks
merely to service an export.

Version the analysis schema independently of classic codecs and command APIs.
Include snapshot identity, source file hash or session/revision identity, view
mode (stored, loaded or live), observer/access scope, relevant rules/input
provenance, and supported/omitted sections. Tables may expose civilizations,
city/unit slots, tiles and retained summaries. Preserve slots, holes, scan bounds,
raw identifiers and lifetime meaning where included; scope keys to their source
snapshot. Keep retained values separate from calculated analysis fields and mark
unknown or unavailable values explicitly. Export exact logical integers and
document encodings; analysis must not silently repair or normalize state.

Player-session exports contain only observer-permitted data, including metadata
and diagnostics. Offline save inspection or explicit developer captures may use
their separate broader access contexts. Export a separate destination without
overwriting the source. A failed or cancelled write must not publish an apparently
complete snapshot or alter gameplay. Transactions may make export publication
atomic; they do not define the game operation's mutation semantics.

An exported database is a disposable analysis artifact, not a classic save,
replay checkpoint, or automatic input to the simulation. Editing it has no
effect on the game. Consumers can use SQL tools for joins, aggregation and
comparisons without keeping a database synchronized with every live mutation.
Basic text/JSON inspection and all runtime features remain available without
SQLite. Browser export and an in-game SQL interface are outside the initial
scope; this decision adds neither as a runtime requirement.

### In-game command console

Build an Avalonia panel for input, history, completion and output on top of the
portable console. Start with help, queries and commands from the playable slice;
unsupported operations remain unavailable. Keep basic commands usable when Lua
is disabled. Text input follows the same focus/capture rules as dialogs.

Route commands through the same session dispatch as menus and other controls.
An operation requiring a choice can use the existing dialog or a typed console
reply to that interaction; neither path bypasses busy states or network routing.
Clear or isolate history, results, completion caches and task output on hotseat
handoff, observer changes and session replacement.

### Lua user automation

Use an optional embedded stock Lua interpreter for high-level player automation.
Pin one runtime and binding API version for native and WASM builds when this
target is introduced; do not depend on JIT availability. C# owns
state, arithmetic, mechanics, persistence and authority. Moving classic AI or
map-generation algorithms into scripts is a separate design decision requiring
its own conformance evidence. Lua's
[embedding API](https://www.lua.org/manual/5.5/manual.html#1) supports host-provided
functions; the interface below is our engine contract.

Expose permitted queries (own cities/units, known tiles, available production),
semantic events (turn start, production completion, command result), and normal
commands (production choices, worker assignments, movement, interaction replies).
For example, a production policy handles an own-city completion event, queries
current permitted choices, submits a choice and waits for its result. It does
not write the city's production field or reproduce city mechanics.

Run tasks on the session's mutation lane at declared command/event boundaries.
Queue script notifications rather than reentering scripts from a gameplay
callback. Specify event ordering, subscription lifetime, delivery cursor and
overflow behavior; never silently drop events needed to resume a task. A script
can wait for a command, interaction or semantic event without blocking rendering,
protocol dispatch or deadlines. Preserve existing compatibility boundaries even
when the runtime needs to yield for responsiveness.

Bound instruction work, memory, host-call work, command queues and output. Expose
cancel/pause controls and useful script/line diagnostics. Lua instruction hooks
alone cannot bound time spent inside a host function: keep host calls bounded
or asynchronous. Contain Lua errors within protected native bridge calls; no native long jump
may cross a managed frame, and no managed exception may escape a native callback.
Keep callback roots and interpreter handles alive until explicit disposal. Disable
the failed task and report committed command effects.
Stop further submissions on cancellation; in-flight operations still follow
their normal completion/cancel and mandatory-reply contracts.

Load scripts only through explicit user selection, not automatically from a
classic save or scenario. Give the runtime a restricted library set and explicit
capabilities; no default filesystem, process, native-module, debug or network
access. Route script import/export through platform adapters. Treat this as a
bounded extension environment, not a claim of strong isolation for hostile code.
Developer/editor capabilities are separately enabled and cannot be reached from
normal player scripts.

Record submitted commands, ordered external inputs and RNG context for gameplay
replay; replay does not rerun scripts and duplicate their commands. Reproducing
automation decisions additionally needs the script hash, runtime/API versions,
ordered observations and any script-private RNG state. Script randomness never
draws from gameplay RNG. Persist only explicit versioned task data at supported
boundaries, not VM stacks, closures or suspended coroutines. Store it with task
identities, pending command/event cursors and script identity in new-engine
checkpoints or hash-bound sidecars, outside classic saves and wire messages.
Without matching metadata, load the classic game normally with automation
stopped; require an explicit restart instead of guessing task state.

In multiplayer, scripts run locally on behalf of an authorized player and emit
the same requests as manual play. Original peers require no interpreter or new
message format. Remote results remain authoritative under N02 and are never
reapplied or rerolled by a script. Suspend tasks on loss of player authority or
hotseat handoff, and revalidate pending work on resume/reconnect.

## 6. Presentation and platform menus

Use Avalonia for the application shell, conventional dialogs, focus, input and
layout, with a custom batched Skia map surface. Keep tiles/sprites out of the
control tree. Use explicit texture/atlas lifetimes, visible-region culling and
measured terrain/overlay caches. A small drawing boundary keeps backend-specific
objects out of view models and mechanics. Preserve full redraw, cached pan and
dirty update cases in performance checks, including half-world views at 4K/5K.
Measure model preparation, allocations and GC stalls as well as drawing time.

[P02 MV01–MV30](algorithms/P02-map-rendering-and-minimap.md#minimum-map-visual-feature-set)
and the [M2 acceptance matrix](testing/map-rendering.md) define the complete map
renderer. A small authored scene proves integration, not visual completeness.
Implement each capability against explicit owned, observer-filtered inputs; use
authored inputs for later gameplay producers without a second test-only renderer.
The [map performance contract](performance/map-rendering.md) separates unchanged
repaint, cached pan, continuous zoom, dirty world changes and resource replacement.
Native and browser backends must retain the same feature coverage. Actual platform
and hardware evidence remains separately scoped.

The comparison established feasibility, not production renderer provenance or
complete platform acceptance. Write the production renderer from permitted
asset contracts; the experiment's source-reuse exception remains isolated.

Build one menu/action model with stable action IDs, labels, shortcuts, checked,
enabled and hidden states. Derive availability from current game state; stale
fields left by an original menu refresh are not required. Menus return an action
identity through the same dispatch path. The command service revalidates authority
and target at acceptance. Resolve shortcuts centrally to prevent double dispatch.

| Platform | Main menu bar | Dialogs |
| --- | --- | --- |
| macOS | Avalonia `NativeMenu` using the system menu bar | Avalonia controls/custom drawing |
| Windows | In-window Avalonia menu | Avalonia controls/custom drawing |
| Linux | In-window Avalonia menu; optional desktop global-menu export | Avalonia controls/custom drawing |
| Browser WASM | In-window Avalonia menu | Avalonia controls/custom drawing |

Use native menus on macOS and Avalonia's own menus on Windows. Linux uses the
same in-window menu model, with optional global-menu integration where supported.
No Win32 menu adapter is required. Keep window ownership, shortcuts, focus and
menu availability consistent across hosts; test native menu tracking on macOS.
[NativeMenu documentation](https://docs.avaloniaui.net/controls/menus/nativemenu)
describes the framework's macOS and Linux integration.

Dialogs use normal Avalonia controls where possible. Custom city/map regions
need stable semantic identities, input/hit-test behavior, focus and accessibility
peers; pixels alone are not an interactive control. Use shared measurements and
asset caches without binding every map object as a UI control. Test keyboard-only
operation, IME, focus transfer, modal cancellation and target changes.

Desktop file selection uses a platform storage adapter; browser selection,
import/export and durable save completion use browser capabilities behind the
same semantic workflow. Never pretend the browser has arbitrary filesystem
paths. Keep audio/media behind separate services: choosing Avalonia does not
implement P10 playback or completion semantics. Native and browser hosts must
prove windowed/fullscreen behavior, DPI changes and responsive protocol deadlines
while dialogs and native menus are open.

The renderer reads observer-filtered models rather than unrestricted world state.
W03 and N04 govern fog, remembered knowledge, diplomacy privacy, and hotseat
concealment. Clear outgoing-player views, popups, and cached presentation data
before handoff. Rendering is read-only: required recalculations historically
performed by a report or dialog move into an explicit preparation/refresh
operation. P06/P07/V08 define the corrected semantic scope: no classic viewport,
font metric, page or repaint selects simulation work. Original score/Attitude
visible-prefix refreshes and Trade row-visibility effects are reference bugs,
not compatibility inputs. Local UI drafts can be discarded; accepted commands
retain their effects. Apply the
[randomness ownership rules](#randomness-ownership): presentation models carry
resolved report/dialog choices, and repainting or rebuilding a view of the same
interaction does not resample them. Cosmetic work uses only its own context.

## 7. Assets and content

Use [PeterJohnson/openciv2-assets](https://github.com/PeterJohnson/openciv2-assets),
available locally at `~/project/openciv2-assets`. Its inspected checkout was
`c4161c1843e8e06a5cf864e1633b6f6221abb797`. M0 also verified the published
2026.11.08.1 runtime archive and pinned its URL/hash in `dependencies.json`.
The [content inventory](content/README.md) separates source and release evidence;
the [format contract](content/asset-format.md) records the observed pack/layout
format, stock TrueType selection and runtime rasterization boundary.

The asset repository documents a runtime package containing `atlas-hd.pack`,
legacy bitmap fonts, layout data, generated layout constants, provenance, and
notices. Select the graphics/layout/notice data for the new engine and exclude
`fonts.bin`/`fonts_aa.bin` from runtime use and distribution. Consume graphics
through a newly written adapter. Document its format before writing the reader;
an undocumented field is a content-contract gap, not permission to consult the
old renderer. Prefer data contracts and
locally generated typed tables over incorporating another game's headers or
resolver code. A generated constants header is eligible only as reviewed
data-only asset output. See the asset repository's
[runtime package guide](https://github.com/PeterJohnson/openciv2-assets/blob/c4161c1843e8e06a5cf864e1633b6f6221abb797/docs/ASSET-DEPENDENCIES.md).

`tools/check_assets.py` verifies the pinned asset inventory. Runtime graphics-pack
loading and installation remain M2 work. The archive/hash pin is available for
explicit acquisition; an asset source checkout can use its standalone runtime
pack builder. Require no reconstruction checkout or original executable.
CI/release builds consume immutable runtime packages with checked inventory and
checksums. Pure game tests use small owned fixtures and do not build a graphics pack.

Use the DejaVu Serif, Sans and Sans Mono TrueType families identified in the
asset repository's `THIRD-PARTY.txt`, with regular/bold/italic/bold-italic faces.
The historical bitmap-generation font versions were not recorded; independently
pin DejaVu 2.37 and each TTF/license hash rather than claim an exact reconstruction.
Avalonia owns text measurement and rasterization. Current publications include
the dependencies and fonts they actually use, with verified notices; graphics-pack
font selection and integration remain M2 work.
CPU source bytes remain separate from frontend-owned glyph/GPU caches. Headless
builds do not fetch/link fonts or rasterizers. Custom drawn dialogs use
the same font measurements and atlases as widgets. Cache by face, size and DPI,
and retain source bytes until all consuming contexts are destroyed. Native menus
keep platform typography. New visual metrics must not change classic logical
admission inputs, game state, file data or protocol behavior. Text coverage and
complex-script shaping need their own tests before being claimed. See the
[font contract](content/asset-format.md#truetype-fonts-and-runtime-rasterization).

Expose semantic `AssetId`s and metadata for dimensions, anchors, sheets, palettes,
and versions; GPU texture handles belong only to the frontend. Preserve P03/F03
resource lookup and scenario override precedence. Decode supported classic
custom graphics independently, retaining sheet identity, marker/palette and
transparency meaning. The HD stock package must not silently replace authored
scenario graphics. S04 exports remain classic-compatible.

Graphics do not supply the complete game content set. Standalone play uses the
[versioned native JSON content formats](content/data-format.md), with explicitly
selected packs for rules, city names, production preferences, messages/help/facts,
menus, briefings/credits, conditional council content, encyclopedia and events. The
[per-file inventory](content/text-resources.md) identifies runtime, documentation,
companion-tool and unconfirmed legacy inputs; it is not a loader-completion claim.
A separately selected [built-in gameplay bundle](content/gameplay-format.md)
supplies the first six extracted constant families with shipped original defaults.
Its explicit custom mode is outside classic-equivalence claims. Typed loading
is implemented; complete gameplay/session integration retains its milestone gates. The content backend does not
implicitly choose different built-in values.
Original text files are optional inputs through compatibility adapters, not prerequisites for standalone play. Both decoders publish the same
logical models and owner-defined operations; mechanics, AI and map generation
never choose a different algorithm based on the content backend. Native files
describe game concepts and use semantic IDs/units. Optional compatibility
metadata carries classic slots, source addresses and import associations;
native consumers do not scan virtual text-file sections.

`OpenCiv2.Formats` owns these codecs
and receives explicit byte sources. Native pack
validation precedes installation; classic selected files retain F03/C01/P04/P08/
P09/S02 grammar, fallback, conversions and meaningful reload state. Classic
parsing/loading may also stage unpublished results under X01. Preserve accepted
game/peer effects and in-game keepNames/leader policies; native partial parser
memory is not a required publication. Resolve inheritance explicitly, keep scenario-authored
classic overrides, and record source kind/hashes. JSON does not replace F06
save formats or N02/N05 wire formats, add required original-peer negotiation,
or make original peers capable of reading JSON packs.

Inventory and provenance for complete native defaults, labels, Civilopedia text
and scenario fixtures are delivery dependencies. Use small authored fixtures for
initial tests; the [examples](content/examples/README.md) are deliberately not a
playable pack. Do not assume the specs contain a full distributable stock data
set or obtain one from reconstruction source. Keep user-supplied classic data
and approved distributable content clearly identified after conversion too.

The current asset dependency supplies graphics; stock sound can be added there
later. Reserve audio/media identities and manifest versioning now, with native/browser
audio adapters and a silent test backend. Select and pin the playback/decoder
dependencies when implementing those adapters. Missing stock cosmetics can
have explicit fallback; supported authored WAV/video requests, meaningful
notices, and completion/cancel semantics remain P10 obligations. Select
independent media decoders against that domain before claiming scenario support.
Do not let missing media, a browser audio permission, or a skipped animation
suppress a game effect or required reply.

## 8. Persistence and networking

Classic codecs read/write explicit field widths, little-endian values, strings,
and retained bytes. They never dump runtime objects or CLR layouts. Keep file bytes, N02 state
images/diffs, and transport frames as separate formats even where fields overlap.
Storage adapters provide byte I/O and failures; the portable operation owns
publication and partial effects. Validation and staged writes are useful only
where they preserve the admitted F03/F06 failure contract; blanket transactional
load/save behavior would be incorrect.

Implement N01–N03 as portable state machines and N05 as incremental framing,
routing, and discovery logic over replaceable stream/datagram services. Native
adapters use nonblocking TCP/UDP; fake adapters supply fragmentation, coalescing,
backpressure, loss, clocks, disconnects, and late replies. Preserve the original
per-operation authority model, section encodings/checksums, message order,
barriers, join/resume, and host transfer. A new host-authoritative or deterministic
lockstep protocol would not satisfy X01.

The session service provides a logical client/server API within each process;
it is not a claim that the network host performs all simulation. Each local
runtime executes only its assigned work and applies received state/results under
the classic contract. Map participant/controller identities to original wire
identities without adding mandatory fields. Handle role changes and host transfer
inside the mode/authority adapter while keeping client bindings and pending
interactions consistent with the new authority.

The browser host uses browser-supported networking through a platform adapter;
it cannot open arbitrary native TCP/UDP sockets. The planned path remains an
explicit WebSocket-to-native transport bridge. Changing the language does not
remove this platform boundary:

- The original peer speaks unchanged classic TCP/UDP; install nothing in it.
- The bridge carries transport bytes/events and discovery results, not gameplay
  decisions. Application encodings and authority remain N01–N05's.
- Hosting requires a reachable native listener at the bridge. UDP discovery
  sees the bridge's network, not the browser's LAN. Address/connection identity,
  reconnect, and host migration must be demonstrated in both directions.
- Scope bridge access to configured sessions/endpoints with authentication and
  bounded resources; it must not become an unrestricted public socket proxy.

Native mixed-peer support is a baseline release gate. Browser local play can
ship as an explicitly limited preview while the bridge is built. Full browser
multiplayer claims require bridge-backed interoperability evidence; do not
silently reinterpret WebSockets as native TCP/UDP or declare the gap optional.

For browser storage, stage imports and asset bytes through browser adapters and
persist saves/preferences in IndexedDB-backed storage with explicit completion
and export/download. A virtual filesystem is an adapter choice, not a core
requirement. Never report a durable save before persistence succeeds. Browser JS
interop stays outside Core and Formats. Desktop and browser resource lookup must
preserve classic name matching without depending on host case sensitivity.

## 9. Build, tests, and delivery

Use an SDK-style managed solution with pinned SDK/runtime/workloads and package
locks, nullable reference checking, analyzers and deterministic build identities.
The root `global.json`, shared MSBuild files and `Directory.Packages.props`
pin the SDK, runtime and NuGet versions; see the [build guide](BUILDING.md). Record all native dependency versions
and notices alongside NuGet inputs. Restore locked dependencies in CI; permit
cached/offline builds. Test upgrades as a set, not by silently selecting latest.

Keep Core, Formats, Query, Session and the base CLI independent of Avalonia, Skia,
Lua, SQLite, display servers and graphic assets. Optional features use separate
composition projects/build configurations. The console parser works without Lua;
SQLite remains native-only initially. No package restore for a base tools build
should pull GUI or optional native-runtime packages transitively.

Use explicit byte codecs and reviewed JSON readers/writers. Where serialization
is generated, enumerate supported shapes and converters so trimming/AOT cannot
silently remove required behavior. No runtime assembly scanning or arbitrary
reflection dispatch is needed for commands or Lua. Browser publication and smoke
tests run from the first managed foundations slice. Desktop builds remain managed
by default; `Oc2DesktopAot=true` opts into Native AOT publication with separate
locked inputs and the same application-input acceptance checks.

The [build guide](BUILDING.md) gives root-based .NET commands. C# formatting,
nullable analysis, warnings-as-errors and locked restores are checked in CI.
The optional Lua bridge uses native sanitizer/resource checks. Builds do not rewrite source files. GUI test instrumentation must remain
explicitly separated from player-facing behavior.

xUnit and shared native/browser cases verify the implemented contracts.
Keep the independent Python CLI/conformance/content drivers, changing
executable selection and provenance support deliberately. Native CI covers Linux,
macOS and Windows; browser tests execute the same portable contracts through a
WASM host rather than assuming a desktop test runner can run in the browser.
Publish self-contained native CLI packages and separately versioned desktop/web
application artifacts from the same locked source/content inputs.

Implement the existing X02 version 3 fixture format and its operation/state
registry, controlled-choice and implementation-replay modes, dependency-aware
trace comparison, provenance, and coverage ledger. Preserve the scope of the
current 57 fixtures and 15 registered operations, with deliberate versioning of
policy-dependent replay expectations. The bounded M1 domain remains explicit.
Missing scripted inputs fail; they
never fall back to random values or real clocks. Hash explicit logical state,
not native object memory. Separate gameplay, file/content, peer interoperability
and optional executable-reference claims. Internal API/provider regression tests
are implementation tests, not additional classic contracts. Capture meaningful
state closure and real event dependencies, not all original helper/global traces.

Test randomness isolation with the same initial gameplay and presentation-content
contexts, semantic operations, player responses and ordered external callbacks. Vary redraw count,
frame rate, animation skip, sound settings, cosmetic seeds and graphical versus
headless execution; gameplay state and future gameplay samples must agree.
Cosmetic variation must also leave semantic content choices unchanged. Verify
presentation initialization/teardown and pure visual openings cannot consume or
reseed gameplay randomness. Separately test report/council selection laws with
controlled content choices, pending-interaction restore, and gameplay callbacks
triggered by dialogs. Do not require unchanged game state when adding or removing
a semantic operation that the specifications say recalculates or mutates it.

Use managed view-model and Avalonia control tests for dialogs, custom hit regions,
focus, keyboard routing, accept/cancel and changing targets. Add browser interaction
checks over the real host. Native menu behavior also needs platform adapter tests
and real desktop smoke tests; headless control tests cannot validate native menus,
IME, accessibility or mixed-DPI/fullscreen behavior. Visual regression images of
our UI supplement semantic
checks, with original screenshots used only as optional comparisons.

Managed tests also cover stored versus supported loaded inspection,
malformed/unsupported saves,
lossless JSON, stable diffs, query non-mutation/privacy and descriptor validation.
Drive a supported operation through direct session calls, console input and Lua
and compare semantic outcomes, including stale targets and partial effects.
Test instruction/memory limits, bounded host calls, cancellation, task restore,
missing script metadata, event order and duplicate-command prevention on native
and WASM. Avalonia integration tests cover console focus, history, completion,
interaction
replies and hotseat concealment. Mixed-peer tests prove that automation uses the
ordinary protocol and preserves authority through reconnect/host transfer.

Test the session API with local and scripted controllers, with transport disabled
for local modes. Cover admission versus completion, stale bindings after handoff
or load, pending choices, revocation without losing required replies, and human
flags remaining independent of automation. Exercise classic AI's retained-state
effects and human advice callers. Fake-network tests use isolated process/session
contexts per peer and prove designated execution versus result application,
including role changes; they do not assume every peer runs the single-player loop.

For SQLite export, compare logical rows with the source query records, including
numeric boundaries, slot holes, retained counters and stored/loaded differences.
Test schema/provenance metadata, observer filtering, consistent snapshot capture,
failed/cancelled publication and unchanged source state/RNG/files. Check logical
contents rather than requiring byte-identical SQLite files. Exercise builds
with export enabled and disabled, including a headless tools build, and verify
the optional dependency never reaches core targets.

Package inventories include Avalonia/Skia, fonts and optional Lua/SQLite adapters;
test-only dependencies remain distinct from releases. Record SDK, runtime,
workload, native compiler and source identities for each build. Conformance
reports bind executed artifacts and fixture/specification inputs. Never relabel
older results as execution of changed sources.

Run portable arithmetic/replay vectors across native and WASM builds, sanitizers
on suitable native jobs, decoder/protocol fuzzing, browser smoke tests, and
display-backed UI tests. Original-game file/peer tests use separately provisioned
external runs with executable identity and capture provenance. Their absence
leaves an explicit conformance gap; passing cleanroom-only tests cannot close it.
