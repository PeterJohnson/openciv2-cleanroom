# Reading and extending the source

Open `OpenCiv2.slnx` at the repository root for the headless core, CLI and tests.
Optional solutions in `solutions/` add the desktop application, Lua, SQLite and
performance probes. SDK/runtime/package versions are pinned centrally.

Read the owning behavioral specification and [architecture](architecture.md)
before changing a gameplay operation. The [coverage inventory](testing/coverage.md)
links supported APIs and tests; [implementation milestones](implementation-plan.md)
identify what remains. Contracts describe ownership and partial failure as well
as successful results. Required file/wire layouts are explicit codecs, never
runtime object layouts.

## Project map

Projects live in `src/`; case libraries and executable hosts live in `tests/`.

| Project | Responsibility | Useful starting point |
| --- | --- | --- |
| `OpenCiv2.Base`, `OpenCiv2.IO` | Arithmetic, PCG32/choices, owned values and byte/storage contracts | `Arithmetic.cs`, `Randomness/RandomContext.cs`, `Storage.cs`, `Documents.cs`, `PlatformServices.cs` |
| `OpenCiv2.Contracts` | Immutable client observations, commands and setup values | `Clients.cs`, `Commands.cs` |
| `OpenCiv2.Core` | Live state, map geometry/storage, rules and implemented gameplay leaves | `State/GameState.cs`, `MapStorage.cs`, `Rules/RuleInstaller.cs` |
| `OpenCiv2.Formats` | Classic codecs, content decoding/conversion, portable graphics readers and versioned checkpoints | `ClassicFiles.cs`, `Content/ContentPack.cs`, `LocalCheckpointCodec.cs` |
| `OpenCiv2.Session` | Authority, clients, continuations, setup/load/save and checkpoint ownership | `SessionRuntime.cs`, `CommandRuntime.cs`, `Checkpoint.cs` |
| `OpenCiv2.Network` | Messages, framing, queues, compression and state images | `Stream.cs`, `SendQueue.cs`, `Sections.cs` |
| `OpenCiv2.Query` | Stored inspection/diffs and observer-filtered live entity/map values | `StoredInspection.cs`, `LiveProjection.cs` |
| `OpenCiv2.IO.Native`, `OpenCiv2.Platform.Native` | Native filesystem, classic text and audio output adapters | `NativeFiles.cs`, `ClassicStorage.cs`, `NativeByteStore.cs`, `ClassicTextFiles.cs`, `NativeAudio.cs` |
| `OpenCiv2.Cli`, `OpenCiv2.Export.Sqlite`, `OpenCiv2.Cli.Sqlite` | CLI composition and optional native export | `Program.cs`, `SqliteSnapshot.cs` |
| `OpenCiv2.Automation` | Optional managed Lua-CSharp interpreter, scoped player API and resource budgets | `LuaMachine.cs`, `ScriptTask.cs` |
| `OpenCiv2.ViewModels`, `OpenCiv2.Console` | UI-independent player-client model and command parser | `SessionPanel.cs`, `CommandText.cs` |
| `OpenCiv2.MapRendering` | Skia map drawing, scene indexing, camera, minimap geometry and presentation timelines | `MapPainter.cs`, `MapScene.cs`, `MapRendererSession.cs` |
| `OpenCiv2.App`, `OpenCiv2.Desktop`, `OpenCiv2.Browser` | Avalonia integration, authored map/gallery, controls and input | `SeamSession.cs`, `SeamView.cs`, `MapControl.cs`, `Program.cs` |

Shared cases are in `tests/OpenCiv2.FoundationCases`; native and browser
hosts execute the same portable cases. Native I/O and optional SQLite have their
own tests. `OpenCiv2.Conformance` and its two hosts exercise the selected X02
fixtures. Build commands and evidence checks are in the
[build guide](BUILDING.md). Public client APIs expose owned values;
mutable Core records and borrowed spans stay behind the session boundary.

`Core/MapGeneration*.cs` owns W02 terrain phases and finalization; `MapRegions`
owns W01 labeling and `GenerationPaths` supplies U03's bounded uniform-cost
connectivity domain. `Session/WorldGeneration` integrates the actual F05 world
entry child. See [generation inputs, retained state and evidence](map-generation.md).

`Core/StartingSites` and `Session/StartingPositionOperation` own F05 start search,
scoring and V01's late hint boundary. `Core/Research` composes V03 recommendation
with `Ai/AdvanceValuation` (A08), using `City/BuildingQueries` for C08 wonder
effects. These are the initial M4 dependency operations; the
[desktop work record](m4-desktop.md) distinguishes them from complete setup,
research, AI and desktop-game integration.
`Units/UnitMovement` and `UnitActivation` provide shared U02 movement budgets
and U04 selection rules used by the existing local commands.
`Units/UnitCreation` owns U01 initialization and composes the required revelation
and notice children. `UnitRevelation`, `MapKnowledge` and `UnitOrders` implement
local W03 surroundings/knowledge and U04 contact orders, using real A01 goal
insertion. The local creation caller uses this path; general V04 meeting services
and the generated desktop gameplay caller are still separate work.
`Core/CivilizationCreation` and `City/UnitAvailability` provide V01 local
reset/leader/admission/placement and C06 unit eligibility.
`Session/GeneratedGameInitialization` combines these with real U01/W03 and
F05 compensation, with cancellation and bounded search polls. `WorldEntry`
uses this implementation when its `Initialize` service is null. Accelerated
city packages are composed by `Core/AcceleratedStarts` and the cooperative
`Session/GeneratedGameAcceleration`. A null `WorldEntry.Accelerate` selects that
operation using explicit naming content. F05's Pikemen identity is fixed slot6,
verified from the user-identified original-data conversion and recorded in F05
Appendix D. The generated desktop gameplay caller remains open.

`City/CityCreation` owns local C01 allocation and initialization, composing
actual naming, C06 defender eligibility, C08 presence, U01 creation, W03
knowledge, `CityArea` claims and C07 `Commodities`. The local founding command
uses this constructor before the full C03 worker/output calculation.
`City/CityFounding` now composes C01 founding and player Build/Join City with
actual basic unit deletion, city removal and nested lifecycle decisions. Naming
pauses after creation; cancellation removes that city and then adjusts the live
name cursor. Accepted founding clears center improvements and remembers features;
joining only changes size and deletes the unit. Its final city visit retains the
calculation processing mode until a reply. `Session/CityFoundingOperation` routes
private naming, lifecycle and final city-window decisions through scoped clients.
The desktop dialog accepts text with the same identity/latch guards as numbered
choices. `SettlementTactics.Site` now invokes direct founding for A03 decisions.
General desktop registration and the full tactical parent remain open.
`CityQueries` also supplies region/port and coastal predicates. Commodity
refresh includes the specified zero road-accumulator repair. `TradeRoutes` supplies
C07 directional removal, slot writing, append/replacement and refresh, using the
actual U03 connection query. Caravan cargo selection remains separate; entry and
delivery now use `CaravanTrade`. `Units/UnitCargo` supplies U01 transport grouping, lifting and repeated
capacity queries; `UnitLoss` composes actual grouping and basic deletion for
whole-stack/cargo-aware loss. `StackOperations.ShipsFirst` uses the two real
staging passes. `CityRetirement` now joins real C05 charges with U01 cargo-aware
and basic loss, and the local desktop turn calls it. `CityRemoval` joins descending supported-unit
processing, real U01 role/stack queries, cargo loss, C07 route removal and C01
radius repair. It notifies the view owner after committed removal; the remote
request wrapper remains separate. `CityFood` runs C05 starvation/growth before
recalculation and food accumulation after it, using actual removal/loss and
routes. `CivilizationElimination` supplies V01's local last-city check with actual
U01 deletion, `SpyEscape`, `SpaceshipLifecycle` reset/loss and civilization
recreation. Famine-to-defeat, recursive last-settler checks, history, AI restart
and exploration clearing are tested together. `CivilizationElimination.Begin`
retains an explicit stack of child checks and actual V06 incidents across human
pretext replies and bounded creation search. U01 deletion/cargo loss and V06 escape
share their effect steps between synchronous and resumable callers. The synchronous
APIs still invoke their owning callbacks; the resumable operation runs actual
nested checks before completing each child's tail. `CivilizationEliminationOperation`
routes the current victim's prompt through the session's participant resolver.
General city/combat/mission caller joins and saved pending restoration remain open.
V01 restart
catch-up now calls actual V03 grants; forced retirement,
civil war and full outcome handling are separate work. Alder Valley's deletion
adapter now uses the real last-city check, although its admitted city-center
yields and absence of food routes/homed settlers cannot cause last-city famine.
C06 production selection remains a required food-turn continuation. Semantic
notifications leave tutorial hints, message preferences and view refresh to
presentation.

`City/CityWorking` owns C02 availability, signed tile yields, terrain suggestions,
ordered worker allocation and nominal specialist operations. Its world-owned
`CityCalculationWorkspace` retains totals, support inputs and job/mode state;
`TerrainSources` supplies the real W04 irrigation predicate, and availability
counts W05 AI pollution visits. `CityEconomy` supplies the full C03 calculation
parent, including capital/overlap, recurring food/trade, corruption/waste,
industrial production, W05 intensity and distribution. `CitySupport` provides
C05 box factors and military/support counts; `CityHappiness` provides C04's five
stages and normalization. The local founding/turn/load caller invokes this parent;
pure local display previews still use the authored profile's restricted yield
view. `CityMoodTurn` applies disorder duration/restoration, tax, celebration and
celebration growth using those live whole counters. Repeated Democracy disorder
calls actual `GovernmentChanges.Change`, including incoming Vendetta clearing,
all-city C03 recalculation and fixed Fanatics-to-Riflemen production substitution.
`GovernmentChanges` also owns availability, rate caps and local AI change notices;
player government selection and rate decisions remain explicit continuations.
The local desktop turn now uses the shared mood handler and complete local
production dispatch with actual A02 choice. Remaining V02 operations and general
F04 scheduling remain separate work.

`CityUnitProduction` handles C06's unit-at-entry branch with actual U01 creation,
veteran/transport initialization, first obsolete-type replacement, stock spending
and settler population/city-removal consequences. Its AI diplomat path uses real
nearest-unit queries and preserves the specified distance comparison. C07 cargo
selection remains an explicit owning continuation. A02 selection is provided by
`CityProductionChoice`; isolated production tests also retain controlled replies.
The AI common tail runs `AiProductionPurchase`
with retained F04 human wonder ranges, regional policy and real V07 race queries.
Alder Valley now calls this service instead of its previous completion shortcut and uses shared `UnitAvailability`.
`CityProduction` supplies the local unit/nonunit parent, using actual ordinary
building storage, wonder ownership/effects, spaceship retargeting/assembly and
resumable Darwin research. It retains cost/net/item across choices, applies the
human wonder-priority early return, and runs the common tail once. The desktop
city pass now uses it; Temple and Library choices extend the authored profile.
Shared unit/improvement/wonder eligibility is available; the scenario wonder-flag
binding, full production dialog and generated-game callers remain open.

`SpaceshipPerformance` owns V07 calculation and group selection, using selected
spaceship/calendar values and retained `GameState.Spaceship` scratch. The capacity
queries consume those values explicitly. `SpaceshipConstruction` performs actual
assembly and automatic/local launch, including first-ship contacts and directed
diplomacy effects. `DiplomaticRelations` supplies V04's bilateral flag cascades.
A02 city reselection uses `CityProductionChoice`; human choices/presentation remain required children;
C06 nonunit completion now composes these services; spaceship reports and F04
arrival outcomes remain open.

`BuildingMaintenance` owns C08's quote and ordered payment/forced-loss pass.
The desktop city turn calls it after research; no facilities or pollution sources
outside the authored profile are admitted. A forced Palace loss can be saved and
resumed. `WonderEffects` supplies Eiffel's one-time acquisition and Leonardo's
local upgrade pass, using `DiplomaticAttitudes` for V04's directed attitude guards
and retained evaluation scratch. That class also supplies global reputation
decay, now called after the desktop turn's unit reset. `ApolloRevelation` publishes
W03 exploration, feature memory and city sizes through actual world storage.
C06 production and V03 grant callers now compose these services. Other acquisition
callers remain pending; the effect services do not themselves acquire wonders or
complete research.

`ProductionOrders` owns C06's shared category/stock snapshot, switch forecast,
order assignment/counters, AI wonder start/switch/abandon continuation and
civilization reset scan. `GameState.Production` retains snapshot and notice state;
new worlds receive fresh state. Automatic requests require the A02 chooser as a
mutating child. The desktop production command starts its authored selection
transaction and saves its opening snapshot, then uses the direct manual-change
operation; the desktop city pass publishes the civilization-turn phase input.
The V03 reset caller is composed by
`ResearchFlow`; the desktop composes actual A02 policy. General P06 interaction
remains open.

`CityProductionChoice` owns A02's ordered survey and scoring with explicit
CITYPREF, optional unit/building recommendation destinations and actual child
services. Each call retains local captures across sales/airlift. The owner records
X01 repairs for invalid exhausted rule/treaty reads and rejects unbacked retained
city sizes. `TerrainImprovementChoice` supplies A03 §2; `SpatialControl` supplies
U02 adjacent nonallied and same-medium queries, shared with land movement.
`AiProductionAirlift` preserves orders, performs actual relocation/deletion and
uses semantic secondary draws for reachable human fighters. Worker priorities
are owned by `TerrainWorkerTactics`; generated-game orchestration remains open.

`CivilizationAssessment` owns A01 staging, ordered census, regional policy,
reverse goal assignment and goal aging. Its required children are the actual
production orders/policy, government transition, support, loss and elimination
services. `AssessmentQueries` owns the small role/quota/coastal queries;
`PowerRanking` publishes retained power/history and directed coordination using
`CityObjectives` for V08 objective values. `GovernmentPolicy` owns A08 post-city
rates/strategic scoring and government selection; F04 still supplies preceding
score accumulation and its complete maintenance caller. The desktop turn publishes
power before advancing and assesses its human civilization after city processing.
The A01 outer unit passes and full tactical composition stay open; U04 execution
is owned by `UnitOrderExecution`.

`TerrainWorkOrders` owns W04 player eligibility and all seven work executions.
It uses actual stacks, movement exhaustion, map mutations, remembered features
and C03 refresh for an eligible nearby city selection. It preserves per-unit
progress and first-donor transfer; it does not rebuild regions or fertility.
`UnitFortification` owns U04 fortification and U01's signed home-slot gate.
The desktop publishes scoped work options and resumes queued jobs while selecting
a manual unit. Its current authored Pioneer domain is before the first city;
A03 workers and U04 execution now have owning services; full tactical and
generated-game callers remain open.

`CityTerrainGoals` owns the A03 §3 pollution scan, retained job priority and road
link planning. It uses A01 goal insertion and the U03 uniform path workspace.
The desktop city turn enables C02 job collection during its second C03
calculation, publishes goals after research and upkeep, and runs A08 score
adjustments before assessment. The F04 specification records the remaining
city-analysis arithmetic.

`IPlayerClient.TakeCityProductionEvents` exposes settled C06 completions to
automation bindings in the authored game. The command runtime scopes each
publication to the actor's participant and verifies the city's current owner
and token. Each lease drains its own bounded queue in publication order.
The optional Lua adapter exposes completion events and owned production choices.
It keeps parsed requests and admission receipts for replay and supports pausing
between cooperative ticks. The next-production example exercises the ordinary
player command, with a matching manual and replay run in the automation suite.

`OpenCiv2.Desktop.Automation` is the opt-in launcher and owns the script file,
task and binding lifetime. The regular desktop host drives the optional task
on its existing game tick, and the view model follows the task's visible
commands so ordinary dialogs and replies still apply. The launcher retains
script results until the task finishes and revokes its auxiliary lease when
the task ends or the game reloads. `IScriptMachine` separates interpreter resumes
from owner-lane game dispatch. The desktop `IsolatedLuaMachine` owns one
heap-limited worker process and asynchronous, deadline-bounded pipe exchanges;
`LuaWorker` implements the private protocol. The worker receives no session
reference. The default `LuaMachine` remains portable and runs inline.

The optional launcher can record local inputs as a separate replay journal.
`LocalReplayRecorder` observes the existing command and file boundaries.
`LocalReplayCodec` owns the bounded, versioned representation: text replies use
version 2; journals containing only existing input types retain version 1.
`LocalReplay.Run` drives a fresh authored game through the recorded inputs and
checks receipts and save digests before returning the result.

`BuildingAvailability` owns C06 nonunit eligibility with actual knowledge,
stored facilities and active wonder effects. Scenario NoNewWonders/AllowManhattan
are explicit semantic inputs while their original-file binding remains a gap;
missing inputs stop scenario queries rather than defaulting to permissive rules.
`ProductionOrders.Options` lists units, facilities and wonders in contract order,
using retained stock/turn forecasts and an expiration marker.

`PlayerProduction` owns local C06 player purchase/sale/change quotes and confirmed
mutations. It uses actual C03 cached/full calculation and C06 snapshot refresh;
purchase fills shields without completing an item. Sales enforce the Palace and
once-per-turn rules with the reveal-map/scenario exception. Pending decisions
retain target tokens and quoted prices; terminal failures cannot repeat payment.
`LocalPlayCommands` exposes these transactions through correlated interactions, the desktop city
window and console. The city-turn entry clears the sale flag; classic save/load
retains it between turns. Ordinary production changes clear automation flags,
retain the opening snapshot across the penalty decision and update counters
directly without AI wonder notices. Acceptance and rejection both recalculate.
General production dialogs/advice/automatic/cheat actions and network busy/deferred
execution remain separate work.

`Research.Cost` owns the full V03 cost formula and `Research.Options` its offered
technology identities. `ResearchGrants` composes shared storage, actual C08
Leonardo/ownership and recursive Great Library grants, including first-discovery,
Barracks, announcement and emissary boundaries. `ResearchGovernmentPrompts` calls
actual V02 transitions for accepted research prompts. `ResearchFlow` composes
those children with C06 production reset and C03 mood queries for general
completion and accumulation, including free advances.
`ResearchGrants.Begin` retains acquisition state across actual V05 emissary and
V02 government decisions, including recursive Great Library grants.
`ResearchGrantOperation` routes those choices privately and declines on cancel or
timeout. The retained constructor requires both child services and rejects the
synchronous `Grant` entry. `Begin` also accepts explicitly supplied synchronous
adapters during migration. Captured technology, theft, huts and research completion
use the continuation. Civilization catch-up retains each grant before the next
advance test, alive-bit restoration, counter reset or placement. Creation is
disposable; elimination and civil war own and release their creation children.
`LifecycleDecision` carries actual espionage pretexts or research decisions through
loss, combat, city missions and movement; session adapters route the concrete
recipient and cancellation semantics. `CivilizationCreationOperation` exposes the
same private choices for an admitted standalone creation.
`ResearchOperation` is disposable and returns selection or acquisition decisions.
Completion retains its grant and final government prompt; accumulation waits for
completion before its free-advance check. Darwin retains both sequential completions
before its production tail. `ResearchFlowOperation` and the desktop turn route the
shared decisions and dispose their nested operations on exit.
`ResearchSelection` suspends for an accepted choice and retains its recommendation;
`ResearchGoals` owns informational goal/advice data. The desktop city pass waits
on this flow before maintenance. `LocalResearch` retains only authored-profile
eligibility/cost queries. Darwin now invokes two sequential completions from
`CityProduction`. `CivilizationCreationServices.CatchupResearch` supplies the
actual grant service for V01's nonzero-turn reset, using self source and completing
nested effects before the next advance. Turn-zero-only generated setup may omit
it after the real new-game reset. A01 outer movement and generated-game callers
remain open.

`GenerationConnectivity.UniformDirection` shares the U03 reverse flood used by
W02 connectivity and F05 accelerated roads. `GameState.Paths` retains this
workspace within a world. `TerritoryQueries.PeaceOwner` supplies the directed
peace/alliance radius gate. `RoadConnections` checks the chosen uniform route
for C03 road/rail links, including retained cache and owner/airport interactions.
The same engine now supports ordinary local queries through `LocalPathfinding`;
coarse waypoint selection, general movement and unit goto remain separate work.

`OpenCiv2.MapRendering` is an optional repository-local library over Base,
Contracts, Formats and SkiaSharp. It has no Avalonia or Session dependency;
Formats still brings Core transitively for its existing content/codec ownership.
App uses its public scene, camera, display, timeline and drawing APIs. Texture,
atlas, cache and GPU experiment types remain internal, with friend access for
MapProbe and MapBrowser acceptance. App retains inspection text, gallery fixtures,
settings/persistence and Avalonia controls. MapProbe remains a native integration
host over both projects. MapBrowser links the production browser entry point and
the shared matrix, with read-only result and fresh frame exports. Its performance-only exports
install the shared `ScopedMapPerformanceWorld` fixture; real gallery input then
drives production scoped projection, scene binding and drawing. BrowserContent
supplies asynchronous HTTP streams to Formats' pinned graphics/font readers.

`IO/Documents` owns asynchronous byte-storage/transfer contracts, the bounded
private document envelope and file-library lifecycle. `IO.Native/NativeByteStore`
implements atomic application-value replacement; it does not change
`ClassicStorage` partial-write semantics. `App/AvaloniaFileTransfer` owns native
picker handles and streams. Browser-only IndexedDB/file-input/download interop
is in `BrowserStorage` and `wwwroot/storage.js`. `App/FileLibraryView` and
`ShellPreferences` provide the real caller. Base owns explicit clock and silent
media contracts; the host composes them into the shell/session timer.

The host owns each painter's lifetime and lends a current Skia canvas/context to
`MapRendererSession.Draw` inside its drawing lease. Observer replacement, render
epochs, fallback/retry and cache disposal retain their existing ownership. The
library never creates an Avalonia context or loads a gameplay session.

The two desktop renderer options share `MapRendererOptions`; the native host
owns preference I/O in `RendererPreferences`. `MapRendererSession` owns per-window
fallback/retry and context replacement. `MapGpuComposition` owns the shared
context cache policy without owning Avalonia's context. The GPU implementation is in `OpenCiv2.MapRendering/MapGpuComposition.cs`.
`MapPainter` remains the common selector/drawing implementation. The
[prototype report](validation/m2-gpu-composition-prototype.md) records its
historical environment switches, matched benchmarks and correctness limits.
Desktop and browser retain CPU composition by default. GPU composition is opt-in;
browser terrain-offload promotion remains blocked by validation failures.
The [follow-up experiments](validation/m2-gpu-experiments.md) add sprite batching,
deferred GPU preparation, retained pictures, instrumented GL replay and native
input-to-window-image measurements. `GpuUploadChecks` verifies CPU raster updates
through actual GPU readback, covering the aliased-worker snapshot invalidation.
The [staged optimization report](validation/m2-gpu-optimization.md) covers
`MapForegroundCommands` (ordered per-frame commands), shared atlas masks,
`MapObservationDelta` (disclosed snapshot changes), and the experimental
`MapPainter.GpuRegions` / ground topology caches. Ordered commands are now
included in the opt-in GPU renderer after the
[browser measurements](validation/m2-browser-performance.md). The remaining
prototypes stay feature-gated; failed visual or performance experiments do not
change their supported defaults.

Ground atlas batching runs serially in WASM and on the existing workers natively.
Packing is independent of worker availability; the same bounded scratch batches
and unpacked fresh reference are retained. See the
[browser stall investigation](validation/m2-browser-stalls.md). The
[browser default validation](validation/m2-browser-gpu-default.md) records
shared native/WASM GPU history checks and the remaining browser failures.
`GpuPixelChecks` owns the common readback/comparison code; `GpuOptimizationChecks`
borrows a live host context and never owns or retains it.

`ViewModels/ShellActions` derives stable action identities, shortcuts, availability
and checked states from owned observations and local display preferences.
`App/ShellMenu` projects those actions to the in-window menu and macOS NativeMenu.
`SeamView.Actions` routes selections and shortcuts through the same command path
as the drawn order button. `InteractionDialogModel` owns the correlated reply
latch; `InteractionDialogView` supplies focus containment and custom painted
button regions with standard Avalonia automation peers. See the
[menus and dialogs record](validation/m2-menus-dialogs.md) for executed hosts and
the remaining real-platform checks.

`Session/LocalGame` composes the authored Alder Valley profile and owns staged
classic replacement. `LocalPlayCommands` admits movement, city and research
decisions and retains the F04 continuation through upkeep. `Core/Units/LandMovement`,
`Core/Units/LocalVisibility`, `Core/City/LocalCityEconomy` and the remaining
`Core/LocalResearch` queries own their bounded mechanics. The desktop passes owned
observations through
`SessionPanel` to the existing map renderer and dialog. The loaded CLI and
optional SQLite exporter consume a revision-stamped `LocalPlayerSnapshot`.

## Adding behavior

### Readable implementation and documentation

Production C# uses braces for control-flow bodies and puts each statement on its
own line. Keep short, pure forwarding members and simple computed properties as
expressions when they are easy to scan. Give mutations, validation sequences and
resource handling ordinary blocks. Use blank lines to separate steps with distinct
purposes rather than compressing several steps onto one line.

Aim for lines no longer than 120 characters. Wrap long parameter lists and calls
with one item per line; break conditions at logical operators and keep related
arithmetic together. Prefer a named intermediate value when it explains a concept,
but preserve evaluation order, checked/wrapping arithmetic and short-circuiting.
Long literal data, URLs and format definitions may justify an exception.
`.editorconfig` enforces braces and separate statements in `src/`; its line-length
setting is an editor guide, not a wrapping guarantee from `dotnet format`.

Use XML summaries on types and operations to explain their role and contract.
Document ownership, mutation, lifetime, failure behavior and meaningful return
values, especially at public boundaries. Add parameter/return details when the
names and types do not convey them. Private helpers benefit from documentation
when they encode a policy or invariant; avoid comments that just repeat a name.
Implementation comments explain why ordering, bounds checks, retained state or
apparently unusual arithmetic are required. Preserve specification references and
state the implemented scope without implying that a whole subsystem is complete.

### Implementation checklist

1. Choose the owning subsystem inside Core, Formats, Network or Session. Keep
   portable algorithms independent of filesystem, UI, database and Lua packages.
2. Put player-visible values and command descriptors in Contracts. Session owns
   authority, continuations and effects; ViewModels, Console and Automation receive
   scoped `IPlayerClient` bindings. Composition roots alone construct mutable worlds.
3. Add meaningful boundary/failure cases to FoundationCases when portable. Register
   them in `FoundationSuite`; native xUnit and browser hosts execute the same cases.
   Keep OS I/O tests in Base.Tests and optional interpreter/database tests separate.
4. Update `docs/testing/coverage.json` and the owning contract. For new X02 behavior,
   follow [conformance registration](conformance/README.md#updating-an-operation).
5. Run affected builds/tests and project-boundary checks from [the build guide](BUILDING.md).
   Rebuild after source/specification edits before accepting an identity-bound report.

Use explicit constructors, small service interfaces and C# XML documentation for
nonobvious ownership, mutation, failure and lifetime rules. Preserve explicit
integer wrapping and sampling laws rather than replacing them based on names.
No reflection-driven command dispatcher, global mutable service locator or generic
ECS is required. Avoid per-frame world copies and one UI control per map tile.

Game/runtime native interop is limited to the optional SQLite API.
Lua automation uses the managed Lua-CSharp package.
The independent Linux map GPU test probe additionally uses system EGL/OpenGL to
create its test context; production hosts continue to use Avalonia's context. Keep
protected native calls, safe-handle lifetimes and resource budgets intact. Tools
under `tools/dotnet/` build dependencies, execute hosts and verify evidence; root
Python tools validate specification/content data independently of the application.

Renderer integration and its Linux-only validation are recorded in
[the renderer-options report](validation/m2-renderer-options.md).

The P10 WAV service boundary uses `Base/PcmAudio`, `Formats/WaveAudio`,
`Platform.Native/NativeAudio` and `Browser/BrowserAudio` with `wwwroot/audio.js`.
`native/audio/oc2_audio.c` is the opaque miniaudio bridge;
`tools/dotnet/build_audio.py` owns its pinned build and receipt.


`CivilizationRates` owns V02 rate preparation, balancing, city previews and
accepted effects. `RateDraft` holds an opening cap/preferences and staged choices;
`Preview` and `Report` are explicit preparation operations, not draw callbacks.
The local `civilization.rates` command joins accepted rates to full C03 city
calculation and preference output. Desktop supplies `ClassicPreferenceWriter`;
headless sessions default to `BufferedPreferenceWriter` with owned encoded bytes.
`LocalPlayCommands.RateEditor` owns staged rate decisions and previews behind
`civilization.rates.edit`. Holds live with the local game and survive reload;
city-preview effects and accepted normalization survive cancellation. Owned,
labelled interaction options let the desktop present these choices without
duplicating their gameplay arithmetic. Checkpoint encoding retains optional
labels; the editor itself does not yet have a resumable checkpoint codec.
`PlayRates` is a pure, owned observation and the sidebar only renders it.

`GovernmentTurn` owns V02's resumable calendar/revolution prefix; its caller
acknowledges each completed government child before continuing. Opaque Reserved40
clearing stays in the F06 adapter. `DiplomacyTurn` then performs V04's entry draws,
request aging, directed relation scan and ordered AI policy calls. It retains
recent-peace values in `DiplomacyWorkspace`; `IDiplomacyTurnEffects` names the
unfinished A08/V04 children rather than supplying default behavior.
`LocalPlayCommands.Government` implements revolution and restricted selection,
joining actual government/city recalculation and the existing rate editor.
The local turn suspends for these decisions, then runs real human diplomacy
maintenance before assessment. The authored game supports Anarchy/Despotism
and preserves failed preference output without leaving the completed turn busy.

V02 treasury and population queries live in `CivilizationFinances`; its resumable
notice sequence keeps funds gates after the population acknowledgment.
`CivilizationFinanceOperation` supplies session decisions and delegates to
`CivilizationRateDecision`, also used by sidebar and government commands.
`RateDecisionPreferences` retains rate holds for that session. The local F04
adapter captures normalized start gold before upkeep and runs the notice sequence
once after civilization assessment, retaining accepted effects across decisions.

V08 arithmetic and report preparation live in `CivilizationScore`. `ScoreWorkspace`
is world-owned scratch; `ScoreReport` is an owned presentation snapshot. Direct
score queries use stored moods, while explicit ordinary report preparation calls
actual C03 for every owned city before actual V07 calculation and rating.
`CivilizationScoreOperation` owns the desktop report lifetime and prevents repaint
from triggering calculation. Outcome dispatch and fame-title resources remain
separate work.

F06 Hall of Fame bytes are owned by `ClassicHallOfFame`; numeric rows retain their
full name-field bytes. `HallOfFame` in Session owns V08 preparation, sparse I/O,
ranking and display decoding. `HallOfFameOperation` owns the insertion/clear modal
loop and its captured current entry. Ordinary desktop viewing passes no entry.
Desktop uses `ClassicStorage` beside CIV2.DAT; headless sessions use
`BufferedHallStorage` with the same record-transfer behavior. Outcome dispatch
must explicitly invoke insertion after real score/rating reports.

P07 power-history preparation lives in `PowerHistory`, with owned contract values
in `PowerHistoryReport`. `PowerHistoryView` renders those values and their exact
table. `EndGameReports` owns history/Scored/score/rating/Hall sequencing; the
end-game score child explicitly leaves rating to `CivilizationRatingOperation`.
`RetireGameOperation` implements the single-player explicit command, closes the
game even after a retained Hall output failure, and leaves outcome flags alone.
`LocalPlayObservation.GameActive` controls the stopped desktop view; loading a
valid saved game starts a fresh active session. Scheduled retirement and the
pending-outcome caller remain separate work.

`Core/SpaceshipArrival` owns F04's one-ship arrival publication.
`Session/OutcomeOperations` joins arrival notices, V08 scheduled retirement,
semantic ending pages and pending outcomes to the actual `EndGameReports`.
The local turn calls this boundary before upkeep and processes a pending defeat
after completed movement; the outcome child returns Quit and the caller closes
play. Generated-game orchestration and authored ending resources remain open.

`Core/DiplomaticWar` owns local V04 declaration, forced war, alliance obligations
and cancellation with real C01 destinations and U01 cargo/stack expulsion.
`BorderIntrusions` owns the mutating first-tile measurement and retained warning
flags; its current summaries live in `DiplomacyWorkspace`. Presentation effects
receive identities and names, not treaty-policy callbacks. V04 contact/audience,
A08 relation review, full V06 mission callers and network publication remain open.

`Core/Ai/DiplomaticStance` implements A08's ordered evaluation and alliance
willingness, plus V05's shared-attitude patience query. It composes V07 race, C08
wonders, U01 nearest units, V04 treaty primitives and actual A08/V02 government
choice. Evaluation fields belong to `DiplomacyWorkspace`; evaluating does not
select its attitude pair, declare a war, sign a treaty or reset an entire audience.

`Core/EspionageIncidents` owns V06's local treaty response and senate consequences,
with actual war, stance and government services. Its operation retains the human
victim's pretext across a decision; `GameState.Espionage.LastMission` is the live
city-mission domain. `SpyEscape.Resolve` returns the required incident after
actual unit loss. `CivilizationElimination.BeginEscape` adds the same resumable
nested last-settler/incident execution used by elimination; `Session/SpyEscapeOperation`
now uses that path. The owning mission must admit the action and supply victim
participant routing. Pending-incident restoration and network responses remain open.

`Core/UnitEspionage` owns V06 unit bribery and sabotage. It captures the price and
old owner, applies the actual counter/owner/home/visibility transfer, and consumes
the actual HP query and resumable escape for sabotage. `EspionageCosts` supplies
the capped Palace step-distance query shared with future city incitement.
`Session/UnitEspionageOperation` presents the mission menu, cached payment offer
and any nested incident. U02/A07 movement admission, city missions and general
desktop presentation remain open.

`Core/CityEspionageDamage` implements the V06 city sabotage and poison helpers
against their owning controller's captured `CityMissionContext`. It uses actual
building storage, city memory and resumable capture/escape. `BeginCaught` retains
the captured unit identity through nested destruction before reporting capture.
`Session/CityEspionageDamageOperation` routes mission questions to the actor and
any nested incident to its victim. The ordinary city menu/fallback controller,
other city missions and movement/desktop callers remain open.

`Core/CapturedTechnology` owns V03's resumable captured-advance choice, retaining
best valuation across actual research-goal help and composing A08 valuation,
ordinary grants and C06 production reset. `Core/CityTechnologyTheft` uses it for
targeted theft and the actual grant service for ordinary theft. It composes V07
race queries, U01 defender traversal, real capture/escape and victim incidents.
Both paths retain nested government/emissary choices before completing the grant,
resetting AI production or applying their ordered escape/theft-flag consequences.
Capture, revolt, paradrop, combat and movement adapters forward these decisions
and their intelligence-window adapter to the actual recipient.
`Session/CityTechnologyTheftOperation` routes actor choices and victim responses;
the owning city controller must execute any returned Embassy/Sabotage fallback.
General controller, movement and desktop joins remain open.

`Core/CityEspionageVisits` implements V06 embassy and investigation costs and
notices, retaining the controller's Spy/type captures. Its required visit request
belongs to the P07 intelligence or P06 city presentation adapter. The session
`CityEspionageVisitOperation` routes that window to the actor once and resumes
after closure; it does not provide a substitute report renderer. `BeginDelete`
and `BeginDestroy` run actual U01 plain or cargo-aware loss through the resumable
V01 lifecycle. The owning city controller still dispatches an existing embassy's
Investigate fallback and supplies full presentation/movement integration.

`Core/NuclearEffects` owns U07's local SDI/action and independent blast. Its action
uses actual U01 stacks and `CivilizationElimination.BeginEraseStack`, so nested
V01/espionage decisions finish before population/terrain damage. W05's shared
`PollutionPlacement` remembers features through W03 without exploring the tile.
`Session/NuclearActionOperation` routes nested incidents; admission and missile
consumption remain with movement/combat/device callers. P10 semantic presentation
is a required adapter, and network batching remains outside this local service.

`Core/PlantedNuclearDevice` owns V06's local device body after city admission.
It composes actual capture and U07 operations, preserving nested incidents before
post-blast identification and V04 bilateral War setters. The session
`PlantedNuclearDeviceOperation` routes the initial actor confirmation and each
nested victim separately. City-controller prerequisite/menu gates and full
desktop presentation remain with those owners.

`Core/CivilWar` owns V01's local schism selection, actual creation, region/fallback
partition, unit reassignment and capitals. `City/CityTransfer` supplies C01's
elementary transfer without capture policy; `City/CityCaptureQueries` owns C09's
shared treasury share and central-city queries. `ScenarioPermissions.AllowSchism`
runs the S02 predicate with a required authored-action dispatcher; it does not
implement those actions. `Session/CivilWarOperation` publishes bounded progress.
Movement/mission capture dispatch and full scenario/presentation callers remain open.

`City/CapturePopulationLoss` owns C09's signed population/exceptional preservation
helper and composes actual C01 deletion. `CityRemoval` shares supported-unit
traversal with C09's old-owner-filtered path, while its existing synchronous entry
retains its callback contract. `CivilizationElimination.BeginRemoveCity` and
`BeginSupportedLoss` run the nested checks through real resumable V01/espionage
operations. `Session/CapturePopulationOperation` routes those victims; the C09
parent now supplies its mode/restoration gate and takeover.

`City/CaptureProduction` continues actual supported-unit loss through disorder,
C06 eligibility/production and a required P06 city-window decision. Its session
adapter routes nested incidents independently and closes the window on reply,
cancel or timeout. `City/CapturePartisans` composes actual U01 creation/W03
revelation with C09 count, placement and veteran rules. `MapKnowledge.MarkUnitSeen`
changes only the returned unit's seen set. The ordered capture parent composes
these helpers; movement/mission admission and the desktop adapter remain separate.

`City/CityCapture` owns C09's local ordered parent and incited-transfer wrapper.
Its resumable sequence composes actual war, civil war, population/deletion,
research theft, supported-unit/production and elimination/partisan children.
The notice partial owns Palace selection/payment and semantic wonder/capture
announcements with retained numeric input. CityTaken matching/actions are a
required dispatcher when events are loaded; no presentation callback substitutes
for it. `Session/CityCaptureOperation` routes each Palace, technology, nested
incident and required P06 window to its own participant. The V06 revolt mission
now dispatches this operation through the local city controller; movement admission remains open.

`Core/CityIncitement` owns V06's revolt body after city admission: price/offer,
payment, actual resumable escape, explicit incident and actual C09 incited
transfer. The local city controller consumes its returned theft fallback. The
`Formats/Classic/ClassicRevoltOptions` adapter reads all four authored lines with
the existing classic reader; its caller supplies the filename/search context.
`Session/CityIncitementOperation` routes the offer and nested incidents, then
reuses capture presentation for Palace, technology and P06 decisions. An
unaffordable offer has only a presentation close action in the session. Full
content/dialog binding and desktop city-controller integration remain open.

`Core/CityMissions` owns the local V06 city entry, menu availability, ordered
AI/nonlocal/autoplay selection and repeated fallback dispatch. It captures the
mission context once, publishes `Espionage.LastMission` before each actual child,
and composes visits, theft, sabotage/poison, planted devices and incitement.
`Formats/Classic/ClassicCityMissionOptions` reads all seven ordinary authored
options; the caller supplies the Spy annotation and filename/search context.
`Session/CityMissionOperation` routes menu/body/incident/capture decisions and
requires actual P06/P07 presentation adapters. Movement admission, full desktop
content binding and network chat interception remain separate work.

`Units/MovementCharge` owns U02's ordinary charge and ordered AI gates after
entry/mission/combat checks. `UnitMovement.StepCost` distinguishes captured domain
and terrain from live features, abilities and costs. The city-aware
`SpatialControl.AdjacentNonalliedCivilization` query can bridge terrain media as
it scans cities. `LandMovement.Step` and the desktop player command now use this
charge service, preserving partial-movement failures and actual relocation.
The full directional dispatcher still owns admission, publication, arrival,
common finish and cleanup; these are not supplied by the charge helper.

`Units/TransportMovement` owns local U05 landfall selection, boarding, unload,
port arrival and exploration wakeup, using actual cargo/stack and U04 selection
children. Unload applies the known-selectable U04 initial-focus branch internally;
its adapter supplies cursor/status/visibility presentation. `MovementContinuation`
carries U02's unit/bound/answer/origin and shared finish values across those operations. `Units/SpecialMovementFinish` owns
basing/fuel and Coast-bound loss, preserving basic versus cargo-aware deletion.
`CivilizationElimination.BeginEraseCargo` exposes that latter U01 child with
resumable nested checks, without adding the destroy/redraw wrapper.
`Session/LandfallOperation` and `SpecialMovementOperation` route decisions;
the owning directional dispatcher still consumes restart/stop and shared state.
Full desktop movement binding remains open.

`Units/PlayerAirlift` owns U05 local player destination selection, retained threat
count and direct transfer/interception, composing actual single-unit placement
and resumable basic deletion. `Session/PlayerAirliftOperation` routes destination,
fighter warning and nested victim choices. Player and AI production airlifts
retain their distinct treaty directions, order handling and sampling laws. P05
desktop command binding and general generated-game movement remain pending.

`AttackConfirmation` owns V04 senate permission and protected-treaty warnings,
using retained diplomacy random and active wonders without declaring war.
`Units/Paradrop` composes that actual decision, cargo lift, stack visibility and
placement, C09 capture and W03 revelation. It reloads the moving actor after
capture; `Session/ParadropOperation` routes attack and capture decisions, requiring
the real city-window adapter. U05 documents safe presentation handling for
unwritten observer values and missing city/approach data. P01/P05 map-click and
desktop command binding and full AI/turn orchestration remain pending.

`Units/CombatPreparation` owns U06 attack/defense primitives, validated defender
selection and common preparation/preview. `CombatWorkspace` carries terrain/city
cache, defense factor and notice arrays; `PreparedCombat` retains captured
strengths/firepowers and operands for the later execution stages. Submarine
preview calls actual A01 standing-goal operations. Preparation with execute=true
scales strengths and sets city notices; it does not charge movement, declare war
or resolve combat. U06 execution and U02/A04/desktop callers remain pending.

`Units/CombatRounds` owns U06 primary sampling, finite batches of damage rounds,
byte-wrapped hits and promotion bookkeeping. The continuation retains exact
choice ordinals and damage across yields; a null yield is not a combat result.
`CombatWorkspace` also carries viewer/explosion state. Local presentation uses
`ICombatRoundEffects`; promotion emits an owner-addressed notice. The future
combat parent must retain its exclusive lane through yields, run result
presentation before promotion, then call real casualty/event/lifecycle chains.
These helpers do not delete a loser or provide a complete player attack command.

`State/CasualtyHistory` owns fresh-world casualty rings; `Units/CombatHistory`
(in `CombatCasualties.cs`) writes fields in U06 order and reloads owner/index after
content queries. Logical names follow F02/X01; classic byte widths belong to the
format boundary. `CombatCasualties` composes history, required S02 UnitKilled
dispatch and actual basic deletion through `CivilizationElimination`, preserving
captured raw stack links and nested decisions. `Session/CombatCasualtyOperation`
routes those decisions privately. Result presentation, loser-group selection,
city consequences and the full combat command remain separate pending joins.

`Units/CombatResolution` owns U06's post-round composition: result presentation,
promotion, cargo-aware loser grouping, actual casualties and defender-city
population/removal consequences. It uses C06 production reset with the real A02
chooser and A05 `Ai/ExposedCityTransports` for human exposed-city retasking,
including the retained city slot after deletion. Nested lifecycle decisions
propagate through its continuation. This stage returns captured winner/loser,
ransom eligibility and city information; final effects, surviving-attacker
handling and the full movement/AI/desktop combat parent remain pending.

`Units/CombatEntry` owns U06's charge/confirmation/treaty/government/difficulty
prefix. It returns a cancelled early result or adjusted prepared operands plus
the retained V04 force-war result; it does not run viewer/animation or damage
rounds. It composes real `AttackConfirmation`, `DiplomaticWar` and A08
`GovernmentPolicy`/V02 children. `CombatPreparation` initializes aggregate notice
and explosion inputs along with the existing per-civilization arrays. U06 records
the missing scenario combat-visibility selector; do not silently alias it to
another scenario flag when wiring the viewer producer.

`Units/CombatOpening` now owns local viewer filtering, actual single-unit
visibility publication, cargo lift and whole-group restoration. It prepares
immutable observer-addressed notices with classic substitutions and sound cues,
using C01's nearest-city query and live installed names. P10 permits a single
batched presentation: adapters own focus, repainting, delays and animation.
Renderer exceptions become diagnostic results and do not abort combat; restoration
also runs after content preparation failures. The caller must retain/report that
diagnostic, supply incoming argument-zero text, and resolve the scenario visibility
selector explicitly when ordinary knowledge does not establish visibility.
`CombatSound` covers opening and result resource IDs; nullable sound arguments
represent absence without a sentinel. Full combat/desktop orchestration remains open.

`LocalPathfinding` supplies U03's ordinary reverse flood, direction selection and
bounded distance query. `GenerationConnectivity.UniformDirection` now uses that
same engine with its declared uniform/absent-owner domain. `GameState.Paths`
retains costs across generation, roads and ordinary queries; owner/rule/map/mode
changes do not invalidate a matching goal's cache. Absent-owner queries use the
documented U03 safe scratch initialization.
`CoarsePathfinding` supplies graph snapping and bounded waypoint BFS, retaining
byte distances, queue behavior and deterministic ties. `Units/UnitGoto` joins
local/coarse queries to the actual greedy fallback, with adjacent/wrapped targets,
live rule estimates and conditional order cancellation/exhaustion. These selectors
do not execute movement themselves. `Units/UnitOrderExecution` composes them with
actual fortification, terrain work and retained movement, including pending
decisions and conditional arrival/exhaustion writes. `UnitOrderOperation` shares
private `MovementInteraction` routing with direct movement and advances cooperative
yields through the runtime queue. Full AI order selection, active-unit scheduling
and desktop wiring remain open.

`Units/UnitRecovery` implements U04 GoHome with actual city/carrier selection,
raw-region and fuel-reach rules, without changing home affiliation or moving.
`Ai/UnitTacticsContext` captures the on-map A04 state after parent admission,
including the AI colonist clear before effective-role selection. `GroundRecovery`
runs the damage, rest and return-home branches with actual stack/spatial queries
and returns the required next phase. It can install orders consumed by the real
U04 executor; it does not substitute for the unfinished complete AI chooser.

`Ai/GroundGuard` supplies the scenario/ferry gate, foreign-fortress routing and
city guard phases around specialized unit dispatch. It uses actual combat
defender selection, shared goals and the U01 reserve-defender aggregate, keeping
temporary Fortified orders visible to those queries. Its route results preserve
the parent's specialized/partisan boundaries. Missing captured home backing stops
at the specified read; no fallback city is supplied.

`Ai/GroundOrders` handles retained Goto, defender-fortification reasons and the
shared direction/finish path. It produces real orders for U04 execution, preserving
transport retries, goal clearing, air movement wrapping and naval arrival rules.
`PartisanFortress` changes only the retained fortress decision after actual
counter and second-city tests. `GroundPartisans` selects actual `Units/Pillage`
children and nonmilitary/road targets, retaining the city selector, first unit
and last direction rules. `Pillage` performs confirmation, synchronized feature
removal, memory, diplomatic flags, shared goals and movement exhaustion. The
session `PillageOperation` retains private attack warnings. `PillageCommand`
adds player eligibility, retained improvement choices, actual V04 declaration
and a fresh core child; `PillageCommandOperation` routes its private menu and
nested warnings. Desktop command registration still awaits the wider gameplay
composition. `GroundMovementPlanner` supplies the complete ordinary eight-direction
scan, actual U01 aggregates/U06 preview/V06 bribery, independent scaled/unscaled
scores and retained-order decision. Its session adapter routes nested private
mission decisions; the chooser still owns applying the result through
`GroundOrders`. `GroundTacticsEntry.EnterOrdinary` now runs work/off-map bypasses,
directed trespass response, adjacent empty-city forcing and real recovery with
early Hold/Finish completion. It retains the adjacent foreign civilization in
the context; civilization zero requires the separate W06 chooser.
Specialized/site dispatch and the full parent remain open. `Ai/NavalTargets` supplies
A05's landing direction and near-shore Goto helpers using actual W01 queries
and U04 order writes. Landing scoring retains later ties and directed hostile
city bonuses; coastal targeting retains first matches, raw regions and original
target fallback. Neither helper executes relocation or cargo unloading. `NavalCargo.Scan`
uses the desired-defender quota retained by `GroundGuard` to reserve or release
actual Land members across the complete stack, returning role slots and both
counts. `UnitOrders.WakeByRole` uses the same modulo-32 role set and changes only
full Sleep orders. A quota not produced by a reached guard phase remains
unresolved; only a cargo branch that consumes it requires it. `NavalTransportContext`
retains cargo and port/landing state across retries. `NavalPorts.Select` uses
actual connectivity, regional quota, stack counts, Goto and current-goal services
for strict port selection and fallback recruitment. Its Hold result belongs to
A04's shared completion path; fallback can replace the shared nearest distance
with the actual home settler count. `NavalTransport.Choose` composes real port
selection, first-settler relocation and `NavalUnload` around NoMoves/retry and
reinforcement gates. Unloading uses actual landing scores, goals, exhaustion
and role wakeup; it selects orders without relocating passengers. The format
adapter now carries TransportRequested and the upper relationship bits consumed
by A05's absent-unit-owner hostility query. Hut settlement still tests only
the regional low-seven components. `NavalGoals` supplies ordered war, trade,
other-port and exploration destinations with actual connectivity, retained
civilization state and F01 sampling. `NavalSeaTactics` composes cargo, standing
goal cleanup, transport and destination policies; its distinct ReturnOne route
bypasses common Finish for exhausted ships. Continue returns to A04 even after
a Goto publication. Full A04/retry and desktop composition remain open.

`Ai/SpecialUnitDestinations` supplies A07 caravan and diplomat/spy destination
policies. It keeps captured context, ordered scoring, retained orders and actual
V07 race queries; no-target destruction uses real U01/V01/V06 children.
`SpecialUnitDestinationOperation` routes their private lifecycle decisions and
bounded continuation. A destination does not execute trade or espionage. The
full A04 parent still awaits composition. `ParatrooperTactics` implements the
proactive A07 drop policy using that parent's retained city selector, actual
U05 execution and C09 capture. Landing scores survive cancelled searches; a
selected drop returns zero directly even on refusal. Its session adapter routes
actual capture decisions. `SettlementTactics.Site` now produces the A03 selector;
its actual A07 consumer is tested across a later nearest-city change. Full parent
wiring remains open.

`Ai/NuclearTargets` implements A06 nuclear selection with actual SDI, stack,
movement-allowance and diplomatic services. It stages the actor before returning
an attack direction; A04 must reload working position before applying it.
Staging itself does not consume movement or execute the blast. Tests join the
result through actual A04/U04/U03/U02/U07, including nearby SDI interception.
The full tactical/desktop composition remains open.

`Ai/AirTactics` supplies A06 fuel recovery, expended attacks, superiority,
strike/basing and city raids. It uses actual GoHome, stack aggregates, staging
relocation and Goto publication, returning a route to A04. Captured A04 context
and live rules remain distinct; pure nearest-city query scratch stays local
because no subsequent gameplay consumer uses its last candidate identity.
Recovery-order tests execute the selected Goto through U04/U03/U02. Full A04
and desktop turn composition remain open.

`Ai/AttackResponse` implements A04's immediate response using that local query
and real `Paradrop`. Its operation exposes the child's actual pending decision
and ends after the first eligible paratrooper even on refusal. Otherwise it
retasks eligible owned/allied AI ground units, preserving U03 modes and A04's
direction-zero exception. It assigns Goto but does not execute it. Full combat
and tactical-planner callers remain pending.


`Units/CombatContinuation` owns U06's surviving-attacker and nuclear-delivery
branches. Survive captures the current moving unit, exhausts eligible aircraft,
uses actual resumable basic deletion for consumed units, preserves signed wrapped
fuel comparisons and invokes real A04 response. Nuclear uses U07's actual SDI,
stack loss and blast operation; interception deletes the original attacker,
while detonation resets captured-owner aggression before any AI response.
Nested lifecycle and paradrop/capture decisions are exposed by the operation and
routed privately by `Session/CombatContinuationOperation`. Cosmetic redraw
failures remain available as diagnostics for the owning command. Final rewards,
notices, V05 help and full combat/desktop orchestration remain pending.


`Core/EmissaryReception` implements V05 reception, greeting and meeting reset.
It uses actual A08 stance and C08 wonders, retains shared response/asked state,
and exposes reception and intelligence-report decisions. Semantic notice data
contains live names, gender, attitude category, greeting variant and nuclear
information; localized wording and leader rendering belong to adapters.
`Core/AllyHelp` composes that reception with regional threat/payment, actual V04
mediated war and cancellation/expulsion. It captures the local recipient while
retaining offered payments across decisions. Session adapters route all choices
privately and require an intelligence-window provider. Camera, leader and status
failures are diagnostic results; callers must report them. Full audience,
exchange, research/contact and combat/desktop callers remain separate work.


`Units/CombatAftermath` owns U06 publication, multiple-casualty notices, barbarian
leader reward, real V04 ally activation, treaty/ally notices and real V05 help.
`Units/CombatExecution` composes preparation, entry, opening, nuclear delivery or
bounded rounds, resolution, aftermath and survivor handling. Its result retains
U06's movement meanings (999 no target, 1 cancellation/ordinary survival, 0 loss,
consumption/nuclear/AI continuation). U02 still supplies movement admission and
the incoming moving unit. Zero captured firepower on both sides skips rounds but
runs normal loss/promotion/aftermath. `Session/CombatOperation` routes all nested
choices privately through shared interaction helpers; repeated yields do not
replay accepted effects. Required S02 and presentation adapters, scenario visibility,
U02 movement dispatch and desktop command composition remain owning joins.

`Units/ForeignUnitMovement` owns U02's foreign-unit phase after land/boarding
admission. It evaluates the ordered gates, runs actual `UnitExpulsion`, retains
fatigue decisions and invokes `CombatExecution`. Its result distinguishes stop
from common finish and returns the captured destination required by that route.
The outer dispatcher still owns finish and cleanup; this phase does not charge
ordinary terrain movement. `UnitExpulsion` composes actual nearest-city/Palace,
cargo lift/return, placement, visibility, attitude and spaceship-race queries.
It requires explicit incoming scratch for suppressed autoplay choice and refuses
an undefined absent-city read after a target role change. `ForeignUnitOperation`
routes expulsion, fatigue and nested combat decisions through the private session
interaction boundary. Presentation adapters report cosmetic failures separately
from required semantic notice failures. Neither service admits remote authority.

`Units/ForeignCityMovement` handles the separate post-landfall city phase. Captured
domain and live range/attack select occupation denial, nuclear preview/delivery or
treaty admission. It uses actual V04 permission without declaring war and actual
U07 SDI/blast action; interception uses cargo-aware U01/V01 loss, unlike the U06
delivery continuation's basic deletion. Its result tells the outer dispatcher
whether to continue ordinary movement. `ForeignCityOperation` privately routes
attack warnings and nested elimination incidents. The dispatcher still owns
land checks, movement charge, arrival, common finish and cleanup.

`City/CaravanTrade` composes C07 local city entry, wonder donation and full
delivery. It retains the signed-home menu rules, actual U03 connection/V03 cost
queries, ordered payment arithmetic and three-draw reciprocal selection.
Payment/food and visible U01/V01 deletion precede actual route insertion; nested
incidents suspend before routes and do not replay payment. Food calls C03 cached
mode and consumes the existing shared food-row factor. Missing reciprocal names
require explicit backing after the earlier changes. `Session/CaravanOperation`
routes entry menus and deletion incidents privately. Cargo selection/report
search, C06 production and U02 land-admission callers remain owning work.

`Units/LandEntry` composes the captured-Land U02 checks with actual W03 adjacent
revelation, C07 caravan entry, V06 city/unit missions and C08 allied repair.
It returns the stop decision and retained destination owner; only a zero unit
mission reloads that owner. Entry selectors are explicit because their optional
gate is unresolved in U02, while selected lift/return can change stack order.
`Session/LandEntryOperation` shares private prompts and reply decoding with the
existing caravan and mission adapters, including cached visit/capture windows.
The outer movement dispatcher still owns boarding, ordinary arrival and cleanup.

`Units/LongMove` supplies U04's retained long-move warning and signed timer/reversal
arithmetic; `Session/LongMoveOperation` privately routes continue/stop and defaults
cancellation to continue. U02 must call it before storing last direction.
`Units/MovementCleanup` applies local bound-sensitive cleanup, captured-owner AI
orders/timer and live transport state, returning the full result. Dispatcher
lifetime and the deferred network authority path remain outside this helper.

`HutEntry` implements W06's local entry and six outcomes with real `CityCreation`,
`UnitCreation` and `ResearchGrants`. U02 supplies its already retained hut result
and mutable local-human context; the helper does not retest or consume the hut.
Redirects yield bounded work, including the permitted repeating scroll/nomad
cycle. `Session/HutOperation` routes created-city visits and scroll-grant choices
privately. U02 finish forwards the grant decision before its direction/cleanup tail;
the complete local dispatcher is tested through relocation, grant and cleanup.
The low-seven regional presence check includes `SettlementRestrictions`, whose
two independent values are translated by F06; the unrelated high bit remains
opaque. Out-of-catalog Armor prerequisites require explicit discovery backing.
`BarbarianSpawning` implements local periodic raids/uprisings using actual unit
creation, sleep, stack relocation/deletion, treasury and same-medium control
queries. Search retries retain the nearest-city replacement; final creation
failure suppresses the notice without suppressing human-city redraw. Its
operation routes real nested lifecycle decisions and records cosmetic failures.
`GameRules.ExpandedBarbarianTypes` carries the specified base-rules bit through
F06 and setup. `BarbarianRansom` retains the chooser's nearest-city size before
replacing its city, then composes actual stack deletion and nested lifecycle
decisions after payment. `Formats/ClassicRansomText` reads CITY/BARBARIANS with
the shared scenario/install reader; missing selected message content fails with
a P04 diagnostic. `Session/BarbarianRansomOperation` routes payment privately to
the victim, then any nested decision to its own recipient. The owning barbarian
chooser and full F04 invocation remain separate work.

`Units/MovementCompletion` composes U02 ordinary arrival, common finish and local
cleanup. It calls actual C09 capture, W03 revelation, U05 special/transport finish,
W06 huts and U04 long move in specification order, then applies bound-sensitive
cleanup. Capture reloads the shared moving actor; the resulting finishing unit is
retained through later children. `Session/MovementCompletionOperation` shares
capture/long-move prompt logic and privately routes nested loss incidents and hut
city visits. Cosmetic pause/refresh failures are diagnostics. U05 special finish
also collects cosmetic failures; boarding returns redraw diagnostics after required
state publication. Outer movement admission remains separate.

`Units/MovementRelocation` performs U02 local viewer/knowledge publication,
landing alert and early regional count, then actual U01 cargo lift, whole-group
visibility reset and relocation. Its result retains the hut presence from before
placement and the local viewer state for completion. Shift is a cosmetic input;
saved autoplay membership remains an explicit real input when consumed. U02's
supported repair gives absent/zero observers no viewer or animation state and
keeps hut presence independent. An unowned landing has no treaty partner; it
retains the early count and continues the ordinary notice/arrival gates. Required notices/publications retain
failure prefixes; cosmetic failures do not strand the lifted group. The outer
dispatcher and network authority paths remain separate.

`Units/MovementDispatcher` owns the local U02 movement guard and per-iteration
captures. It composes land checks, signed spare-holds admission, foreign-unit
combat, landfall restarts, foreign-city admission, charge, relocation and common
finish. Restarts retain the selected reply/options and reset only iteration state;
completion, failures and disposal release the guard without undoing prior effects.
`Session/MovementOperation` routes actual nested choices using shared interaction
helpers. Missing optional entry selectors fail at their consuming land branch;
a finish without relocation publication skips the optional enemy pause.
Desktop player-command composition and the entry-selector specification gap remain
open; this service does not silently broaden the existing `LandMovement` command.

`MovementOperation` signals `OperationStep.ReadyToContinue` when a bounded child
yields without a decision. `CommandRuntime` queues exactly one subsequent Proceed
behind admitted inputs, so the desktop's existing AdvanceOne timer can resume it.
Plain Pending remains an external wait. Interaction pauses, explicit input ordering,
effect publication and single disposal keep their existing command contracts.

`Ai/SettlementTactics` owns A03 §4 city-site selection and §5 fortress
classification. It retains effective/actual settler flags, nearest-city region,
strict radius ties, signed timer arithmetic and the independent advanced-human
founding tail. Actual C01 founding supplies the child result while A03 retains
its direct return-one contract. The four-line fortress scan includes both
coordinate parities and publishes actual A01 standing goals.
`TerrainWorkerTactics` supplies the following settler and role-21 work choices,
including retained GotoCity, separate recommendation/threshold policies, rotated
movement and actual standing/current goals. `SettlerPopulationReturn` composes
crowded-region return or city growth with actual cargo-aware destruction and
nested lifecycle checks. Full tactical/turn composition remains open.
