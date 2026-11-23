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
`CityQueries` also supplies region/port and coastal predicates. Commodity
refresh includes the specified zero road-accumulator repair. `TradeRoutes` supplies
C07 directional removal, slot writing, append/replacement and refresh, using the
actual U03 connection query. Caravan cargo selection and delivery remain separate
work. `Units/UnitCargo` supplies U01 transport grouping, lifting and repeated
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
and exploration clearing are tested together. V06 incident decisions and V03
restart catch-up grants remain required owning policies; forced retirement,
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
uses semantic secondary draws for reachable human fighters. These do not implement
A03 worker tasks, player airlift or generated-game turns.

`CivilizationAssessment` owns A01 staging, ordered census, regional policy,
reverse goal assignment and goal aging. Its required children are the actual
production orders/policy, government transition, support, loss and elimination
services. `AssessmentQueries` owns the small role/quota/coastal queries;
`PowerRanking` publishes retained power/history and directed coordination using
`CityObjectives` for V08 objective values. `GovernmentPolicy` owns A08 post-city
rates/strategic scoring and government selection; F04 still supplies preceding
score accumulation and its complete maintenance caller. The desktop turn publishes
power before advancing and assesses its human civilization after city processing.
The A01 outer unit passes, U04 executor and remaining tactical policies stay open.

`TerrainWorkOrders` owns W04 player eligibility and all seven work executions.
It uses actual stacks, movement exhaustion, map mutations, remembered features
and C03 refresh for an eligible nearby city selection. It preserves per-unit
progress and first-donor transfer; it does not rebuild regions or fertility.
`UnitFortification` owns U04 fortification and U01's signed home-slot gate.
The desktop publishes scoped work options and resumes queued jobs while selecting
a manual unit. Its current authored Pioneer domain is before the first city;
A03 workers, general U04 execution, pillaging and generated-game callers remain open.

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
`ResearchSelection` suspends for an accepted choice and retains its recommendation;
`ResearchGoals` owns informational goal/advice data. The desktop city pass waits
on this flow before maintenance. `LocalResearch` retains only authored-profile
eligibility/cost queries. Darwin now invokes two sequential completions from
`CityProduction`; V01 catch-up, A01 outer movement and generated-game callers remain
open.

`GenerationConnectivity.UniformDirection` shares the U03 reverse flood used by
W02 connectivity and F05 accelerated roads. `GameState.Paths` retains this
workspace within a world. `TerritoryQueries.PeaceOwner` supplies the directed
peace/alliance radius gate. `RoadConnections` checks the chosen uniform route
for C03 road/rail links, including retained cache and owner/airport interactions.
General movement and goto remain separate pathfinding work.

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
