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
`CityCreation.Begin` retains a first AI city's free defender through actual
contact decisions before clearing routes, surveying environmental flags and
refreshing commodities. `Create` remains the synchronous compatibility entry.
Founding awaits this child and forwards its contacts through `LifecycleContact`
before deleting the settler or applying the founding tail.
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
and basic loss. Its synchronous and retained entry points share the C05 scan;
`CityRetirement.Operation` waits through actual V01 elimination/recreation and
V06/research decisions before removal presentation and restart. The session's
`CityRetirementOperation` routes those choices privately, and desktop upkeep waits
for it before the next calculation/mood/research stage. `CityRemoval` joins descending supported-unit
processing, real U01 role/stack queries, cargo loss, C07 route removal and C01
radius repair. It notifies the view owner after committed removal; the remote
request wrapper remains separate. `CityFood` runs C05 starvation/growth before
recalculation and food accumulation after it, using actual removal/loss and
routes. Its retained operation waits through cargo loss, city cleanup, the captured
owner's elimination and recreation. `CityFoodOperation` routes private decisions;
the desktop caller retains incoming food/size across this child and bypasses
later city stages when removal completes. `CivilizationElimination` supplies V01's local last-city check with actual
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

`Session/SinglePlayerResearch` composes real V02 government policy, A08 stance,
V05 emissaries and retained V03 government/grant operations for one world and
RNG. Its science and captured-technology factories share those children, with
the caller's actual C06 production service and presentation adapters. Desktop
city science now uses this bundle. General movement/diplomacy composition and
generated-game presentation remain open; the authored profile still excludes
foreign research recipients. The grant and flow Session adapters report cosmetic
failures after finishing committed gameplay, retaining their successful result
values and preventing repeated grants on later polls.

`Session/SinglePlayerDiplomacy` composes the actual V04 war/contact and V05
audience/conversation graph with A08 stance, ordered advance exchange, V03 grants
and retained S02 permission actions. Technology, gifts, favors, demands, treaties,
map exchange, senate checks and ally help share those services. The explicit
presentation record supplies semantic notices and view effects; decisions remain
retained operations. A scenario dispatcher factory closes the unit-creation,
revelation and contact cycle before execution. Movement and generated-game host
composition remain open.

`Session/SinglePlayerLifecycle` joins that research/diplomacy graph to retained
unit creation/loss, espionage incidents, elimination/recreation, city removal,
retirement, civil war and scenario terrain/actions/triggers. The composition
owner supplies the same basic deletion service used by production, then binds
the scenario permission factory to this graph's dispatcher before execution.
Contextual recreation factories receive the owning operation's call path;
legacy one-argument constructors remain available. Operations that can prompt
use the retained lifecycle entries, including the incident child of spy escape.
Full desktop host admission and synchronous callback binding remain caller work.

`Session/SinglePlayerCombat` builds capture, partisans, paradrop/response,
nuclear effects, casualties and combat execution over those same lifecycle,
research, diplomacy and production services. One workspace carries combat
preparation, rounds, result and survivor state. CityTaken/UnitKilled use the
shared retained scenario triggers; child presentation diagnostics reach combat
completion. Semantic content, view and publication ports are explicit. Full
movement/mission/turn admission and the generated desktop host remain open.

`Session/SinglePlayerMissions` composes city visits, damage, theft, planted
devices, incitement and unit espionage over the same retained services. Required
readers supply authored mission and revolt options. Capture events, technology
grants and lifecycle escape decisions stay with their actual children. Mission
and land-entry parents retain child presentation errors for the final session
result; cooperative fallbacks mark themselves ready for another local step.
General native movement/mission admission remains open.

`Session/SinglePlayerMovement` joins those bundles to the full local U02
dispatcher. It constructs actual caravan delivery, retained C01 city creation
and W06 huts, expulsion, transport, foreign-unit/city entry, charge, relocation,
special movement loss and completion. Names, food/commodity backing and semantic
presentation ports are explicit. Viewer/entry inputs and admission remain with
the caller; generated desktop turns do not yet own this graph.

`Session/PlayerMovementCommands` admits an owned live unit and direction through
the player lane, rechecks both at execution, and retains actual U02 movement,
pending outcomes and U04 focus. `PlayerMovementBinding` supplies the gameplay
graph, content/view inputs and private interaction adapters. Its factory borrows
the current step through an accessor that is unavailable outside `Resume`.
The default authored desktop command still uses its bounded handler until the
general content/presentation binding is installed.

`Session/SinglePlayerGameplay` constructs those services from one world, installed
rules, RNG, content and semantic presentation. It owns actual stacks, cargo,
deletion, economy, A02 production, research, diplomacy, lifecycle, combat, missions,
movement and focus. The deletion and scenario-permission cycles close over the
actual lifecycle before any operation runs. Player government/rate decisions use
retained Session operations; unsupported synchronous callbacks fail explicitly.
The owner must rebuild the graph when replacing world/rules and supply a unique
call scope. Native content/presentation binding remains the next integration step.

`City/CapturePartisans.Begin` retains actual U01 creation through W03/V04 contact.
Fortification, veteran sampling and visibility follow the completed child; later
placement attempts read the live map while keeping the initial count/threshold.
`CityCapture` wraps those contact decisions in `LifecycleContact`, so existing
capture, mission and movement Session adapters route the correct participant
without adding a separate decision channel. The capture founder/turn tail waits
for all partisans. The explicit synchronous helper remains available to callers
with synchronous creation services.

`CityUnitProduction` handles C06's unit-at-entry branch with actual U01 creation,
veteran/transport initialization, first obsolete-type replacement, stock spending
and settler population/city-removal consequences. Its retained entry shares the
unit sequence, asks for settler abandonment after creation/stock spending and
waits through actual city removal/elimination before the second creation and
completion notices. Its AI diplomat path uses real
nearest-unit queries and preserves the specified distance comparison. Retained
production uses C07 `CargoSelection` for AI cargo and human slot choices,
confirmation and commodity reports; the synchronous entry keeps its supplied
cargo callback. `CommodityReport` filters exact signed supply/demand membership
for the viewer without recalculation. A02 selection is provided by
`CityProductionChoice`; isolated production tests also retain controlled replies.
`CommoditySearch` applies the Trade prerequisite once and retains the report/list
loop; `CommoditySearchOperation` routes it privately. `trade.commodities` and the
desktop Advisors menu expose the search for admitted Trade-known saves. Reporting
does not change supply/demand, refresh the economy or automatically open a city.
The AI common tail runs `AiProductionPurchase`
with retained F04 human wonder ranges, regional policy and real V07 race queries.
Alder Valley now calls this service instead of its previous completion shortcut and uses shared `UnitAvailability`.
`CityProduction` supplies the local unit/nonunit parent, using actual ordinary
building storage, wonder ownership/effects, spaceship retargeting/assembly and
resumable Darwin research. The lifecycle-service overload also retains the unit
child; `CityProductionOperation` routes its cargo/abandonment/lifecycle choices and
research decisions privately and reports deferred child presentation failures.
The lifecycle-service overload also exposes retained spaceship `PartDecision`:
`SpaceshipConstruction.BeginAddPart` shares assembly with the synchronous entry,
and the session adapter waits for nominal component/module replies before the
production tail. Trusted core replies retain V07's signed behavior; the player
prompt admits only listed choices and has no cancellation sentinel.
It retains cost/net/item across choices, applies the
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

`CityPass` owns F04 §6's entry reset, captured upper scan limit and descending
live city selection. The desktop retains it across actual production/research
decisions, then reports each city's balance or removal. Completion updates sizes
from the pass owner's alliances or the global Apollo marker and runs actual A08
rates/strategic scores after doubling Despotism into Anarchy. It does not define
the still-open support/unhappiness terms or best-shield producer.

`BuildingMaintenance` owns C08's quote and ordered payment/forced-loss pass.
The desktop city turn calls it through `CityHumanMaintenance` after research and
actual pollution/meltdown. The wrapper tests the captured owner's current human
control once for both children. Cosmetic upkeep failures retain diagnostics
without losing credits or later payments. Authored-profile admission remains
separate from the general mechanics. A forced Palace loss can be saved and
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
services. Its `Begin` continuation retains actual elimination and retirement
children through nested incidents and bounded recreation. The synchronous entry
shares the same ordered stages. `CivilizationAssessmentOperation` routes private
lifecycle choices and holds later desktop financial notices until completion;
disposal/failure preserves the completed prefix. `AssessmentQueries` owns the
small role/quota/coastal queries;
`PowerRanking` publishes retained power/history and directed coordination using
`CityObjectives` for V08 objective values. `GovernmentPolicy` owns A08 post-city
rates/strategic scoring and government selection; F04 still supplies preceding
score accumulation and its complete maintenance caller. The desktop turn publishes
power before advancing and assesses its human civilization after city processing.
`AiUnitPasses` now owns the local two-pass reverse unit scan, captured scenario
restriction, action limits and conditional extra transport activation.
`Units/AutomatedOrders` composes the real `UnitTactics` and `UnitOrderExecution`
children, retaining the captured actor and full-Goto bypass. Session
`AiUnitPassOperation` routes private child decisions and cooperative yields;
interaction windows are retained per pending decision. The unspecified U04 hold
producer remains an explicit input/gap. F04 and generated desktop composition
remain open.

`Units/ActiveUnitCycle` drives actual order execution and automated continuation
with a captured execution actor and live selection/phase/bound/human gates. It
retains real child decisions, distinguishes those dialogs from a player-input
wait, and exhausts the captured actor after more than twenty automated passes.
Its explicit input boundary lets a host process ordered commands; native blink,
music and tutorial presentation remain host policy. `Session/ActiveUnitCycleHost`
pins the world and lends one command segment at a time. A segment finishes at an
input boundary, allowing an actual player command before the next segment; it
routes nested decisions privately and releases ownership at completion or disposal.
Cancellation/timeout finishes the child's default reply/tail before stopping.
`HumanMovementPhase` composes selection/focus and this active cycle under F04's
human-phase rules. It tracks actual input waiting separately from idle scheduling,
applies Always Wait and city-window resets, reselects after bound changes, and
preserves the session-stop early return before common phase cleanup.
`Session/HumanMovementPhaseHost` pins the world across private child decisions and
input segments, allowing real commands between segments. The host supplies current
cursor/selected-city facts; blink/music/tutorial procedures remain presentation
policy. General native admission, full civilization/global scheduling and U04's
unknown hold producer remain open.

`HumanTurnNotices` owns F04's council invitation schedule/selection and ordered
tutorial reminders, including retained last-city updates. `CouncilSummary` is a
pure owned snapshot for P09-permitted stock advice; it does not implement the
original opinion-keyed chapter parser. `HumanTurnNoticesOperation` routes private
invitation/report/tutorial interactions. The authored desktop turn invokes it
after upkeep/autosave and before unit activation. General generated scheduling
and original council content admission remain open.

`Units/UnitDisband` joins accepted shield credit, the actual C06 build snapshot,
U01 cargo-aware deletion and retained V01/V06 decisions. Its local cheat branch
joins C01 city removal and the captured owner's elimination check.
`UnitDisbandOperation` routes private confirmations and nested decisions. The
desktop `unit.disband` caller uses these services and handles last-settler defeat
through the existing outcome reports before closing gameplay. General editor
and network admission remain separate.

`Units/UnitPlayerOrders` owns U04 Fortify/Sleep/Home/Wait, starting settlement
automation and exhausting local units at End Player Turn. It uses actual stack,
U01 sleep, U04 recovery and retained selection services. Cosmetic failures do not
undo accepted orders or prevent later refreshes. Desktop Wait/Sleep and on-city
Home use the service; Fortify and the existing end-phase caller share it too.
The authored save profile now admits sleeping land units. Off-city Home/Goto,
Skip's full movement finish and general automated native execution remain open.

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

`IPlayerClient.ObserveNotices` returns an owned snapshot of the observer's recent
informational notices. Trusted operations publish through `OperationContext.Notify`
to an explicit civilization, independently of command ownership. Each civilization
has a 64-entry history and an omitted count; same-seat controllers do not consume
each other's view. Session replacement clears it. `SessionPanel` discards notices
when rebinding or losing access, and the desktop shows a recent-notices panel.
`SinglePlayerResearchPresentation` binds actual V03 announcements, advice,
reference facts, tutorials and government notices to this feed. Local-viewer
announcements remain distinct from explicitly owner-addressed notices; retained
operations still own all required decisions. Desktop science uses this adapter.
`SinglePlayerEmissaryPresentation` supplies V05 reception notices through the same
feed, using installed attitude names and captured ruler/nuclear/vendetta inputs.
It updates only the receiver's map center and publishes textual leader context;
there is no separate portrait window. Desktop research binds it, while generated
desktop gameplay composition and P07 intelligence-report appraisal remain open.
`SinglePlayerDiplomacyPresentation.Effects` binds the complete V04/V05 diplomacy
graph's informational ports. Explicit recipients take precedence over command
ownership; Core-gated observer announcements use the host's current viewer.
Attack permission remains distinct from war declaration. Names, signed amounts,
technology/wonder identities and supplied city/third-party context remain visible.
The authored turn's war effects also publish scoped notices and retain legacy
command effects only when their recipient is the initiating civilization.
`SinglePlayerLifecyclePresentation` binds creation guides, spy outcomes,
destruction, spaceship loss, civil war and espionage incidents. Spy outcomes and
incidents use their owner/actor; observer announcements use the host viewer.
Repaint hooks publish existing state. Legacy revelation/contact and network
synchronization entry points reject missing owning services rather than silently
substituting presentation. `Effects` and `Deletion` supply the general gameplay
factory; the authored turn also uses scoped lifecycle notices.

`SinglePlayerCombatPresentation` supplies the U05/U06/U07/C09 informational and
publication ports. Explicit recipients keep combat, promotion and nuclear notices
private; locally gated capture/paradrop focus moves the viewer's map, not the
actor's. History uses live civilization names and a required content color query.
The host must also supply actual paradrop targeting; this adapter does not answer
retained combat/capture decisions. Cosmetic audio and animation are omitted.
The generated desktop host still needs to assemble these ports and content.

`SinglePlayerMissionPresentation` binds V06 mission information to scoped notices.
Visit notices address the victim; theft/damage/refusal notices address the actor.
The unit-bribery refusal callback carries its actor explicitly. Desertion uses
the local former-owner viewer, not the unit's already-transferred owner. Required
city/intelligence visits and mission choices stay in their retained operations;
the generated host must still provide the actual P06/P07 windows. Repaints publish
existing state, and cosmetic sound/animation/delays are omitted.

`SinglePlayerMovementPresentation` supplies the movement graph's information and
publication ports together with U04 selection focus. Core retains activation,
relocation, visibility, rewards and decisions. Focus changes the host viewer's
map; notices use their supplied recipients and captured names. Cosmetic effects
are omitted, and input-flush callbacks do not discard admitted session commands.
The generated desktop caller still needs to bind the general movement inputs.

`SinglePlayerGameplayPresentation` assembles these adapters into the complete
`SinglePlayerGameplayEffects` record. It also supplies V02 government, C08 upgrade,
C06 wonder-production and U05 AI-airlift notices. Government information retains
the old ruler title; Leonardo addresses the owner, while the other announcements
use the host viewer under Core's gates. The host must supply content history colors,
paradrop targeting and the scenario wonder-presentation request. Its `Research`
adapter is also available to retained research choices/completion. This bundle
does not supply the generated host's scheduler, command admission or report windows.

`ClassicGameplayContent` binds selected classic CITY/CITYPREF and GAME option
resources to `SinglePlayerGameplayContent`. Naming is an owned scenario/home
snapshot; preferences update the supplied per-game state with F03 retention.
Mission/revolt options are read only when their retained operation requests them,
using the current selected directories and language. Each read closes its stream.
The caller supplies resolved labels, media, scenario text expansion, starts,
gameplay values and the existing map compatibility object; the binding does not
replace imported map metadata or guess label indices. Rebind after replacing
world/rules. Native JSON text binding and the generated host remain separate work.

`SinglePlayerUnitExecution` composes U03 paths/Goto, W04 work/pillage, C01 founding,
W06 barbarian orders/ransom, the A03–A07 unit-tactics graph and U04 order/active
execution around one `SinglePlayerGameplay`. Combat preparation/strength, missions,
movement, contact and lifecycle services come from that shared graph. It exposes
the F04 human movement phase and A01 two-pass AI unit scan. The host still supplies
authored ransom text and semantic presentation, then admits phase inputs including
the unresolved U04 Hold value. It does not construct the full civilization turn
or infer missing movement-input producers.

`SinglePlayerUnitExecutionPresentation` supplies that graph's informational and
view effects. Work rejections use installed rule names and address the unit owner;
founding and landing notices retain their explicit recipients. Founding/landing
focus follows the local viewer, while ransom focus follows the human victim.
The host supplies its current visible city and actual focus/close callbacks.
Required naming, ransom and city visits remain retained operations. The founding
Session wrapper reports accumulated cosmetic failures after the final city visit,
without undoing the city or settler deletion.

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
`LifecycleDecision` carries actual espionage pretexts, research decisions and
placement contact through loss, combat, city missions and movement. Creation
retains U01 revelation before capital assignment, extra-unit admission and final
exploration. Lifecycle parents use full `ResearchReply` values so nested technology
help does not become a selection; scalar entry points remain compatibility wrappers.
Session adapters route the concrete recipient and cancellation semantics.
`CivilizationCreationOperation` exposes the same private choices for an admitted standalone creation.
`ResearchOperation` is disposable and returns selection or acquisition decisions.
Completion retains its grant and final government prompt; accumulation waits for
completion before its free-advance check. Darwin retains both sequential completions
before its production tail. `ResearchFlowOperation` and the desktop turn route the
shared decisions and dispose their nested operations on exit.
`ResearchSelection` suspends for an accepted choice and retains its recommendation;
`ResearchGoals` owns informational goal/advice data. `Begin` retains goal lists,
Help operands, rule-reference requests and path acknowledgments;
`ResearchSelection.ResumeDecision` carries them through science without rerolling
the recommendation. Captured technology carries the same decisions through
capture, diplomacy, theft and their enclosing lifecycle/movement operations.
Session's `ResearchGoalInteraction` routes private replies and displays reference
facts from installed rules through `ResearchReference`. Full authored Civilopedia
prose remains a content-host concern. Synchronous compatibility entries drive
the same state machines with their supplied presentation callbacks.
The desktop city pass waits
on this flow before maintenance. `LocalResearch` retains only authored-profile
eligibility/cost queries. Darwin now invokes two sequential completions from
`CityProduction`. `CivilizationCreationServices.CatchupResearch` supplies the
actual grant service for V01's nonzero-turn reset, using self source and completing
nested effects before the next advance. Turn-zero-only generated setup may omit
it after the real new-game reset. Full F04 and generated-game callers
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
`AiAdvanceExchange` supplies A08 §6's actual candidate valuation, primary draws,
ordered V03 grants and bilateral tribute. The retained `DiplomacyTurn.Begin`
composition uses it for both periodic and stale-contact exchanges, forwarding
real research decisions and diagnostics before proceeding with later relations.
Its synchronous adapter remains for callers without retained exchanges. Other
AI policy/audience children still require their owning implementations.
`WarAllyRequests` supplies A08 §9's paid/unpaid requests with real V05 reception,
V03 grants and V04 treaties/war. The retained diplomacy turn invokes it after
relation review and military aid. `DiplomacyTurnDecision` distinguishes research
and war-ally children; `DiplomacyTurnOperation` routes their private choices,
including nested research, and reports cosmetic diagnostics after completion.
`GovernmentSelectionOperation` supplies restricted or unrestricted V02 selection
for an explicit civilization and private recipient, joining actual government/city
recalculation and rate editing. It captures offered identities once and retains
accepted government effects when the rate draft is cancelled. The legacy player
command keeps its phase admission and delegates to this shared selector.
`CivilizationPoliticsOperation` joins the actual government prefix to retained
V04/A08 diplomacy, clearing imported Reserved40 metadata and waiting for all
selection/rate children before diplomacy starts. The authored desktop end-turn
path uses it before assessment. AI announcements and recursive human Library
decisions use the same operation; full generated desktop service composition
remains open. Preference-output failures preserve gameplay and are reported after
the diplomacy continuation finishes.

V02 treasury and population queries live in `CivilizationFinances`; its resumable
notice sequence keeps funds gates after the population acknowledgment.
`CivilizationFinanceOperation` supplies session decisions and delegates to
`CivilizationRateDecision`, also used by sidebar and government commands.
An explicit civilization/recipient pair lets trusted parent workflows edit rates
without substituting the command actor. The legacy constructor keeps player-phase
admission. Completion and failures are terminal; a cached result cannot save twice.
`RateDecisionPreferences` retains rate holds for that session. The local F04
adapter captures normalized start gold before upkeep and runs the notice sequence
once after civilization assessment, retaining accepted effects across decisions.
The finance operation's explicit civilization/recipient-resolver pair supports
trusted upkeep for other civilizations, including silent AI milestone updates. It rechecks the
actual funds gates after population acknowledgment without requiring the command
actor to be the affected human; the legacy entry retains phase admission.
`CivilizationPostCityOperation` joins actual government/diplomacy, retained
assessment and financial notices in F04 order. It carries the first output failure
through later gameplay, routes each child privately and owns their disposal.
The authored desktop turn uses it after the city pass. This is steps 5–7 of
civilization upkeep; spaceship presentation and full scheduling remain separate
integration work.

`Session/ClassicAutosave` owns F04 §9 naming, rotation and scenario/home scope.
It calls the ordinary F06 writer on the trusted command lane after upkeep clears
civilization processing and before movement. Manual saves still require settled
commands and pin the world. `IO.Native/ClassicStorage.Rename` preserves an existing
destination when rotation fails. The authored desktop host saves in its configured
storage directory; headless play/replay uses in-memory files with the same writer
mutations. `LocalGame` initializes the human ruler's saved name during setup, as
ordinary F06 load restores that name from the civilization record. Shared hosts
supply the localized label and scenario directory; the authored desktop profile
uses English `Auto` and its own storage directory. This does not complete the
general civilization scheduler.

`Core/SpaceshipUpkeep` captures all six presentation counts before treasury/healing
and checks the F04 sum difference after finances. The local turn waits for
`SpaceshipPresentationOperation` before clearing civilization processing, saving
and activating movement. The retained window-result interface supports an active
window or an unavailable graphical presentation; the latter runs the real text
status with human membership read after the window request.
`SpaceshipReport` prepares V07 support-inclusive performance and raw report values.
`SpaceshipStatusOperation` retains its report and launch confirmation, samples live
shared success at confirmation, and invokes the actual V07 launch exactly once
without a second eligibility check. `SpaceshipSelectionOperation` lists all Exists
ships in civilization order. The desktop `spaceship.view` advisor uses this text
path and real launch logic; graphical artwork/media and general construction
selection remain separate work.

V08 arithmetic and report preparation live in `CivilizationScore`. `ScoreWorkspace`
is world-owned scratch; `ScoreReport` is an owned presentation snapshot. Direct
score queries use stored moods, while explicit ordinary report preparation calls
actual C03 for every owned city before actual V07 calculation and rating.
`CivilizationScoreOperation` owns the desktop report lifetime and prevents repaint
from triggering calculation. Outcome dispatch and fame-title resources remain
separate work.

P09 `ThroneProgress` uses actual V08 score for old-turn progress, retaining signed
threshold/count arithmetic and one award per check. `ThroneUpgrade` captures the
piece/overlay mode, revalidates the local actor and current eligibility, and
commits one semantic selection. Its owned view report performs no score or game
updates. `ThroneProgressOperation` privately routes the choice and retains the
progress award across cancellation. General F04/desktop dispatch and authored
throne presentation remain separate integration work.

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
Its A08 security query combines directed human threats, nuclear strength, third-party
relations and regional strength without changing the workspace. `Core/Ai/MilitaryAid`
preserves A08’s contradictory coordinate gates and performs no transfers on a stable
map. `DiplomacyTurn` calls this implementation directly after relation review.

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
Nuclear notice/animation/redraw failures are retained as diagnostics while the
action/blast completes. Required gameplay failures still preserve their prefix.
Combat delivery, foreign-city movement and planted devices retain those child
diagnostics after completing their own continuation.

`Core/City/CityPollution` consumes the current C03 pollution workspace and runs
W05's one-candidate risk process followed by reactor checks. It uses actual
`PollutionPlacement`, Fusion Power/facility queries and direct `NuclearEffects.Blast`.
Meltdowns preserve garrisons and diplomacy, remove the reactor after the blast,
and retain presentation diagnostics. F04 owns prior mood/research processing and
human-city admission; general desktop composition remains open.

`Core/GlobalWarming` owns W05 local pressure updates and deterministic terrain
transformation. It counts actual Solar Plants, retains signed counters and old
event severity, and uses the twenty specified ocean visits rather than a city
radius. Coastal memory refresh and dry redraw have distinct observer gates.
Notices/redraw failures are diagnostics. F04 still owns its nonzero incoming-turn
admission and general desktop integration; network execution is not admitted.

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
and local `AllowNegotiation` run S02 predicates with a required authored-action
dispatcher. Negotiation captures the first matching record's permission before
actions, using stored-selector control classes without binding Anybody.
`ScenarioActions.Execute` supplies synchronous live-flag action ordering and late
one-shot consumption; every enabled child remains required. `ChangeMoney` supplies
the actual S02 treasury mutation. The other authored actions and a retained
dispatcher for participant decisions remain open. Formats converts only the exact
stored negotiation value4096 to semantic `DenyNegotiation`, exporting4096 or0.
`Session/CivilWarOperation` publishes bounded progress.
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
announcements with retained numeric input. The retained constructor uses actual S02 CityTaken matching/actions when
events are loaded; the synchronous constructor retains its required dispatcher. `Session/CityCaptureOperation` routes each Palace, technology, nested
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
yields through the runtime queue. Local A01 scheduling uses the same executor; full
F04 and desktop wiring remain open.

`Units/UnitTurnReset` owns local presence rebuilding and the live global reset
scan, including actual V06 escape/lifecycle/incident handling at mission expiry.
Its ordinary reset retains Moved/Paradropped for `CivilizationRecovery`, which
uses actual C01/C08 queries for healing, wake-up and moved-air attrition. The
unsupported-domain facility selector is explicit when no preceding supported
unit supplied it. `UnitTurnResetOperation` routes private victim decisions.
The authored desktop uses the shared ordinary reset and recovery; its restricted
save profile rejects mission flags rather than erasing them. General F04/desktop
composition must use the full retained reset operation.

`Units/UnitRecovery` implements U04 GoHome with actual city/carrier selection,
raw-region and fuel-reach rules, without changing home affiliation or moving.
`Ai/UnitTacticsContext` captures the on-map A04 state after parent admission,
including the AI colonist clear before effective-role selection. `GroundRecovery`
runs the damage, rest and return-home branches with actual stack/spatial queries
and returns the required next phase. It can install orders consumed by the real
U04 executor; the complete chooser composes these routes below.

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
`Ai/UnitTactics` now composes barbarian dispatch, ordinary entry/recovery,
specialized/site/paratrooper choices, fortress/city guards, the retryable
transport/work loop and the ordinary planner. All dependencies are concrete
owning services. It retains one context and transport state, honors direct
returns, reloads position after nuclear staging and applies direction/finish
through `GroundOrders`. `Session/UnitTacticsOperation` routes real child
choices privately and retains bounded continuation. Selected orders are tested
through actual U04 execution; A01/F04 scheduling and desktop composition remain
open. `Ai/NavalTargets` supplies
A05's landing direction and near-shore Goto helpers using actual W01 queries
and U04 order writes. Landing scoring retains later ties and directed hostile
city bonuses; coastal targeting retains first matches, raw regions and original
target fallback. Neither helper executes relocation or cargo unloading. `NavalCargo.Scan`
uses the desired-defender quota retained by `GroundGuard` to reserve or release
actual Land members across the complete stack, returning role slots and both
counts. `UnitOrders.WakeByRole` uses the same modulo-32 role set and changes only
full Sleep orders. `NavalTransportContext.ForChooser` implements A05's X01
production initializer: no defender reservation, no landing match or cargo
demand and ordinary port priority until actual producers replace them. The
context retains cargo and port/landing state across retries. Explicit unresolved
inputs remain available only to isolated helper diagnostics. `NavalPorts.Select` uses
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
a Goto publication. The complete chooser now owns the actual retry; A01/F04
and desktop composition remain open.

`Ai/SpecialUnitDestinations` supplies A07 caravan and diplomat/spy destination
policies. It keeps captured context, ordered scoring, retained orders and actual
V07 race queries; no-target destruction uses real U01/V01/V06 children.
`SpecialUnitDestinationOperation` routes their private lifecycle decisions and
bounded continuation. A destination does not execute trade or espionage. The
shared chooser now composes these destination policies. `ParatrooperTactics` implements the
proactive A07 drop policy using that parent's retained city selector, actual
U05 execution and C09 capture. Landing scores survive cancelled searches; a
selected drop returns zero directly even on refusal. Its session adapter routes
actual capture decisions. `SettlementTactics.Site` now produces the A03 selector;
its actual A07 consumer is tested across a later nearest-city change and through
the complete chooser.

`Ai/NuclearTargets` implements A06 nuclear selection with actual SDI, stack,
movement-allowance and diplomatic services. It stages the actor before returning
an attack direction; A04 must reload working position before applying it.
Staging itself does not consume movement or execute the blast. Tests join the
result through actual A04/U04/U03/U02/U07, including nearby SDI interception.
The shared chooser now reloads the staged position before direction application;
A01/F04 and desktop composition remain open.

`Ai/AirTactics` supplies A06 fuel recovery, expended attacks, superiority,
strike/basing and city raids. It uses actual GoHome, stack aggregates, staging
relocation and Goto publication, returning a route to A04. Captured A04 context
and live rules remain distinct; pure nearest-city query scratch stays local
because no subsequent gameplay consumer uses its last candidate identity.
Recovery-order tests execute the selected Goto through U04/U03/U02. The shared
chooser now owns air routing; outer AI and desktop turn composition remain open.

`Ai/AttackResponse` implements A04's immediate response using that local query
and real `Paradrop`. Its operation exposes the child's actual pending decision
and ends after the first eligible paratrooper even on refusal. Otherwise it
retasks eligible owned/allied AI ground units, preserving U03 modes and A04's
direction-zero exception. It assigns Goto but does not execute it. Full combat
and tactical-planner callers remain pending.


`Units/UnitSelection` composes U04 next-unit search/publication with actual W03
revelation and retained V04 contact. It consumes the shared combat-loss flag after
search, clears timers only for qualifying orders and preserves the captured
selected slot through diplomacy. Contact completion precedes the live simultaneous
input-flush gate and missing-selection presentation. `Session/UnitSelectionOperation`
routes nested decisions privately and reports cosmetic failures after completion.
`Units/UnitFocus` captures original selectability after entering Move-units,
retains activation/contact, then validates the current shared active unit.
`Session/UnitFocusOperation` routes the nested private decisions. The authored
`unit.select` command clears the requested unit's orders, runs focus and reveals
the originally selected slot when unmoved. Its reactive map supplies presentation;
foreign contact remains outside that profile. `Units/UnitStackSelection` owns the
complete local stack-entry gates, validated nine-row chooser, immediate wake
mutations and final original-slot revelation after focus. `BeginSelected` supplies
the same accepted-selection path to the authored command. Its Session adapter
exposes separate wake and activate choices while preserving cancellation effects
and private nested contact. General desktop chooser admission, human-phase
scheduling and general desktop contact admission remain caller work.

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
Mercenary, nomad, barbarian and city rewards await retained U01/C01 creation.
`HutEntry.Operation.ResumeDecision` carries full contact replies; `HutOperation`
and U02 finish forward the resulting private decisions before home clearing,
late-city awards, redraw and movement cleanup. Cosmetic failures are reported
after committed gameplay completes, without duplicating rewards on polling.
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
the victim, then any nested decision to its own recipient. `BarbarianOrders` owns
W06's capacity-first transport/landing branch and land/leader neighbor scoring.
It composes actual deletion, pathfinding, coastal routing, ransom and pillage;
selected AiGoto orders remain for U04 execution. Landing wakes cargo and consumes
the ship's movement without relocating units. `Session/BarbarianOrderOperation`
shares private ransom/lifecycle routing and forwards pillage confirmation.
The shared chooser now invokes this operation; full A01/F04 scheduling remains
separate work.

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
nested lifecycle checks. The shared chooser now owns this work loop; A01/F04
and desktop composition remain open.

`GlobalTurn` owns F04 §4 local global advancement. Concrete W06 spawning, W05
warming, A01 power and P09 throne operations observe the incoming nonzero turn;
calendar/peace advancement precedes the actual U04 reset, V04 reputation, history
deadline and V07 arrival. The retained operation forwards lifecycle and throne
decisions, historian acknowledgement and landing acknowledgement without replay.
`GlobalTurnOperation` routes private recipients using shared lifecycle/throne
interaction helpers. Its completion does not dispatch game-over presentation or
another arrival scan. `HistorianReport` prepares an immutable wealth ranking with
P07 ties and disclosure, without exposing treasury amounts or adding game RNG.
Full civilization/city sequencing and generated desktop command composition remain
open. The authored local profile now composes the shared global operation before
its restricted civilization/city path. `TurnOpeningOperation` presents retirement
and pending outcomes after global advancement; it never scans arrivals again.

`SinglePlayerTurnEntryOperation` composes F04 §3's human-wonder snapshot, optional
global advancement, retirement/outcome handling and four ordered S02 passes.
The event gate reads the current runtime resume values after outcomes, then
admits all four passes; each turn argument is read when its pass starts. Nested
choices keep their actual recipient. Quit skips events, required failures stop
the entry, and presentation diagnostics are returned after semantic completion.
Runtime `Multiplayer.ResumeEvent` remains separate from the F06 saved word.
The authored desktop end-turn command uses this entry and clears both resume
pairs after movement; its existing profile still excludes loaded scenario events.
The caller owns civilization/AI scheduling and subsequent resume clearing.

`Core/Ai/RelationReview` implements A08 §7 with actual border measurement, retained
emissary/warning/violation decisions, city knowledge, U01 relocation, V02 Anarchy
and V04 war/attitude effects. Its nullable incoming delta remains unknown unless
the border phase assigns it or the final no-contact rule resolves it. Unknown
contacted results fail before attitude publication while retaining earlier effects.
The full retained `DiplomacyTurn` composition invokes this child before actual
military aid and war-ally requests. Session routes review choices to the intruder.
Full V05 audiences and the original missing-delta producer remain open.

`Core/DiplomaticTechnology` implements V05 §3's survey, cached-price sale and
repeated exchange controller. Shared best/second/wanted choices live in
`DiplomacyWorkspace`; survey counts and scores belong to each iteration. Actual
A08 valuation, V05 reception, V03 grants and captured-technology selection supply
the gameplay children. `Session/DiplomaticTechnologyOperation` routes offers,
research/government and intelligence choices privately. Full audience/menu callers
remain open.

`Core/DiplomaticGifts` owns V05 §4's gift menu and gold attitude valuation. It
composes the technology survey, retained grants, nearest-city query and stack
transfer. The meeting gift counter belongs to `DiplomacyWorkspace`; the future
audience entry owns its reset. `Session/DiplomaticGiftOperation` routes gift and
nested research choices privately. Full audience callers remain open.

`Core/DiplomaticFavors` owns V05 §4's favor controller and war price/distrust
queries. It composes actual technology negotiation, A08 patience, retained grants,
W03 `Units/NegotiatedMaps` and V04 alliance cancellation. Mercenary treaty changes
remain distinct from generic war declaration. `Session/DiplomaticFavorOperation`
shares technology interaction handling with direct exchanges. Full audience and
treaty-negotiation callers remain open.

`Core/NegotiatedTreaties` owns V04 §5's negotiated alliance, peace and ceasefire
wrappers, including actual stance evaluation. `Core/TreatyNegotiation` owns V05
§5's willingness, payment and solidarity stages and composes real grants, war and
treaty effects. `Session/TreatyNegotiationOperation` routes actor choices and nested
recipient research decisions. The local audience composes these services; production
movement/turn and desktop callers remain open.

`Core/DiplomaticDemands` owns V05 §5 aid, tribute, withdrawal and provocation;
`DiplomaticProposals` supplies proposal options and response dispatch through real
treaty/demand children. Its cancellation branch runs actual stance and expulsion.
`DiplomacyWorkspace.HadCeasefire` is the retained meeting input whose capture belongs
to `DiplomaticAudience` entry. `Session/DiplomaticProposalOperation` routes private
nested choices; treaty interactions are shared with direct negotiation. The
audience entry/opening is composed by `DiplomaticAudience`.

`Core/DiplomaticConversation` owns V05 §6 main conversation and common cleanup,
consuming explicit opening outcomes. It joins actual emissary reception, senate
queries, border measurement, treaty/demand responses, favors and gifts. Forced
treaties use bare V04 primitives; cleanup retains bounded request increments,
post-reset intelligence/tutorial reads and the separate AI-meeting-active flag.
`Session/DiplomaticConversationOperation` privately routes actor menus, nested
recipient choices and intelligence. Direct and conversation callers share the
gift, favor, proposal and conversation interaction adapters. The full local
audience and local contact compose this continuation; production movement/turn
and desktop composition remain open.

`Core/OpeningTreaties` owns V05 §6's opening alliance, ceasefire and peace offers,
separately from player-initiated bargaining. Explicit opening inputs carry paid,
declared-war, no-contact and hostile outcomes. The operation joins actual emissary,
senate, war, research-grant and negotiated-treaty services; its result carries
treaty-made, concession, senate-blocked and audience-termination outcomes to the
enclosing controller. Concessions retain live technology and grovel inputs versus
cached cash amounts. `Session/OpeningTreatyOperation` supplies private choices with
the specified zero-accepts treaty replies and decline-on-cancel behavior. Audience
entry and earlier demands/threats are composed by `DiplomaticAudience`.

`Core/DiplomaticAudience` owns V05 §6 local audience entry, S02 negotiation
permission, A08 opening evaluation, opening war/tribute/technology requests and
threats. It composes the real mode-one exchange, grants, war/alliance changes,
opening treaties and conversation/cleanup. Scenario action execution is a required
owning adapter, not an assumed-success default. Replies retain live demand and
technology reads; required failures preserve committed effects and cosmetic
failures remain diagnostics. `Session/DiplomaticAudienceOperation` forwards private
emissary, exchange, grant, treaty and conversation choices, sharing their existing
interaction adapters. V04 local contact dispatch now composes this audience;
production movement/turn/desktop callers remain open.

`Core/DiplomaticContact` owns V04 §10 local contact dispatch, using actual S02
permission checks and V05 audiences before common Contact/RecentContact writes.
Its AI continuation composes A08 security and ordered advance exchange, V03 grants,
V04 ally-war obligations and V05 ally help. AI peace/alliance/war paths retain their
own notice, observer, directed flag and reputation behavior. NATO city information
uses the actual C01 nearest query; absent city information is semantic absence.
`Session/DiplomaticContactOperation` privately forwards audience, research and ally
help decisions through shared interaction adapters. `DiplomacyTurn` can compose
the actual contact child before stale-contact cleanup and later relation work;
`DiplomacyTurnOperation` forwards full replies and private child choices. Production
desktop contact callers remain open; movement/revelation now composes the contact
continuation below. Network forwarding and
two-human network parley belong to the network session.

`UnitRevelation.Begin` retains the ordered W03 scan through actual diplomatic
contact. Cached entry and neighbor values survive private choices; later goals
and outer visibility read their specified live inputs. The synchronous entry
rejects a contact-capable graph when contact is allowed, before mutations.
`MovementCompletion` composes this continuation after arrival and before special
finish/cleanup. Direct and parent movement Session adapters route its private
choices and report cosmetic failures after completed gameplay. The desktop
composition root still needs the full service graph and audience presentation.

`UnitCreation.Begin` retains actual revelation/contact between initialization and
creation tutorials, returning the originally allocated slot. `ScenarioUnitActions`
uses it for S02 CreateUnit, then applies live veteran and home operands. It also
owns MoveUnit order assignment, including the classic unit-slot selector and signed
checks. `ScenarioUnitCreationOperation` forwards private contact choices and reports
cosmetic failures after the action completes. The retained dispatcher uses this
path; other creation callers still need it before general desktop composition.


`CivilizationRemoval` implements V01 forced removal separately from last-city
checks. Inline unit and city changes preserve the specified stack links, dead
records and radius-claim behavior; actual lifecycle spy escapes and recreation
can retain private decisions. `ScenarioDestruction` resolves the S02 recipient,
publishes local defeat when required, and waits for this removal before returning.
The matching Session adapters retain full replies and completion diagnostics.
The remaining caller hooks and desktop composition remain open.

`ScenarioTerrain` owns the S02 two-pass terrain action and cityless-civilization
reset loop. It retains actual C01/U01 deletion and V01 creation decisions;
`ScenarioTerrainOperation` routes them through the private Session interaction
path. `ScenarioTerrainEncoding` in Formats replaces the whole terrain encoding,
including retained file flags, through allocation-bound `MapCompatibility`.
W01 rectangular lookup aliases noncanonical parity; it does not select fallback
for parity alone. The remaining caller hooks and desktop composition remain open.

`ScenarioCivilizationActions` owns S02 MakeAggression and GiveTechnology
admission. Forced war invokes the actual synchronous V04 child; technology uses
the retained V03 grant with source zero. `ScenarioTechnologyOperation` routes
Library and emissary decisions privately and preserves diagnostic completion.
The retained dispatcher now composes both actions.

`ScenarioDispatcher` composes all eleven S02 action types with actual gameplay
children, live flags and final-only one-shot consumption. It retains complete
research replies through creation/contact and lifecycle chains. `ScenarioEventOperation`
uses the shared private decision routing, including local scenario text acknowledgements.
`ScenarioPermissionActions` performs first-match negotiation and all-match NoSchism
execution through this dispatcher. Retained constructors on `DiplomaticContact`,
`DiplomaticAudience` and `CivilWar` use it before their later effects; explicit
synchronous constructors remain for bounded authored callers. The typed dispatcher
factory closes the event/contact/creation dependency cycle.

`ScenarioPresentationActions` owns S02 wave-search and CD-selection policy, collects
all present text lines and applies the autoplay gate after required preparation.
Its media port owns platform playback; its text preparation delegate accepts
`ClassicScenarioText.Prepare`, which captures the current process's number/string
parameters once and applies P04's adopted substitution grammar. Its suppression
delegate publishes the S01 request. Native media adapters, the setup caller's
suppression consumption and broader presentation integration remain required. Remaining caller hooks,
general desktop composition and pending-operation persistence remain open.

`DiplomaticCities` owns V05 §8's offered-city list and local cession. It resolves
live IDs descending for every item, including duplicates, then composes actual
S02 CityTaken, U01 cargo deletion/V01 lifecycle checks, C06 production eligibility
and C08 Leonardo upgrades. Its current city can change after a garrison transfer;
later support, production and capture fields use that replacement. A missing
nearest result stops at the next required city read, preserving completed effects.
`DiplomaticCitiesOperation` routes nested choices privately. Negotiation admission,
the enclosing offer's final destruction checks and desktop UI remain caller work.
`City/TransferredCityProduction` shares C09/V05's first-eligible-defender repair;
it preserves signed cost/defense arithmetic and the zero-cost failure boundary.

`Formats/Classic/ClassicTextExpansion` preserves classic token spelling, selector
meaning and fixed cursor advancement, including STRING/NUMBER suffix behavior.
It uses owned inputs, does not rescan inserted text and preserves Unicode semantic
parameter values. Missing selectors, truncated tokens and unsafe HEX padding widths
receive explicit diagnostics. `ClassicScenarioText` captures fresh parameters for
each action and follows process parameter-object replacement; retained prompts keep
their already prepared content. General P04 dialog-section parsing remains open.

`Formats/Classic/ClassicEventParser` reads classic EVENTS through the shared F03
reader into the same S02 event records. It preserves ordered append, shared
recognition flags, numeric gates/wrap, repeated action payloads and partial helper
failures. Name lookup uses live civilization/rule names and an explicit comparison
profile. Direct `Read` retains earlier records/fields on a grammar failure;
`ReadCatalog` supplies `LoadDialogServices.LoadEvents` and rejects a failed catalog
before installation. The parser does not execute actions. Actual retained trigger
and accepted-load cases execute parsed actions through the existing dispatcher.
Bare THEN without IF is explicitly rejected; original DEBUG traces are not
reproduced. Native scenario admission/error notices and media adapters remain open.

`ScenarioTriggers` owns ordinary S02 matching for Turn, TurnInterval, RandomTurn,
ScenarioLoaded, UnitKilled, CityTaken and ReceivedTechnology. It preserves live
traversal, persistent wildcard binding and polling, and invokes the real retained
dispatcher. `ScenarioTriggerOperation` routes its private choices. Capture and
casualty constructors use these triggers at their specified ownership/deletion
boundaries; their Session adapters report retained child presentation failures
after semantic completion. Broader enclosing diagnostic propagation remains open.
F06 post-load uses the retained workflow below. The F04 turn passes, V05 cession
and foreign-minister caller hooks still need integration; direct trigger calls do not add those callers' gates.

`Session/ClassicLoadDialog.Begin` retains the accepted-file workflow around the
actual core loader. Its typed child factory runs after content/world installation
so map-bound services refer to loaded storage. ScenarioLoaded decisions precede
music/title and cleanup. `ClassicLoadedUnitCleanup` repairs the live unit scan and
uses actual retained lifecycle deletion for invalid coordinates, including restart
research choices. The load operation preserves its world pin until completion,
failure or disposal and publishes at yields. `Load` keeps the synchronous
compatibility path. A trusted host must own the cooperative mutation lane and
route pending decisions to their participants; desktop UI composition is pending.
