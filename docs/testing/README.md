# Tests and evidence

Run from the repository root after the [build prerequisites](../BUILDING.md).
Shared behavior lives in `tests/OpenCiv2.FoundationCases`, executed by native xUnit
and the browser host. Native I/O/API boundary tests are in `OpenCiv2.Base.Tests`.
Optional Lua/SQLite and the X02 adapter have separate test hosts. See
[implementation coverage](coverage.md) for admitted APIs and exclusions.

The unified local gameplay factory is exercised through actual movement contact,
recursive scenario creation/contact/Library grants, capture and automatic AI
production. Its content and presentation fixtures do not replace gameplay
children. These native cases qualify construction and continuation ownership;
desktop content/presentation binding and complete generated games remain open.

Player-notice cases verify observer privacy independently of command ownership,
same-seat snapshots, delivery before command failure, retained snapshots, bounded
history, revocation/reassignment and new-session isolation. Actual V03 acquisition
and government continuation use the desktop research presentation adapter. Native
input checks inspect its rendered completion notice and clear history on reload.

Emissary presentation cases run real V05 reception through Session with a
different command initiator and receiving player. They cover acceptance,
refusal, cancellation, retained prompts, per-observer focus, installed attitude
text, nuclear/vendetta disclosures, reset and notice failure after publication.
The default desktop research graph binds this adapter, but its authored world
has no foreign civilization; generated-game diplomacy UI acceptance remains open.

Diplomacy presentation tests traverse real retained gold gifts, map exchange and
treaty negotiation with distinct initiating/receiving participants. Actual attack
confirmation tests check permission without war mutation, installed government
and wonder names, and Core's observer gates for alliance cancellation. Separate
payload cases retain signed payments, custom technology/wonder names and third
party/city context without exposing private notices through command effects.

Lifecycle presentation tests use actual recursive scenario creation/contact,
spy escape and civilization destruction. Creation guides wait for retained
permission-event replies; spy relocation precedes notice and promotion follows
it. Destruction announcements precede history and spaceship reset. Required
notice failures retain those exact prefixes and delivered private histories.

Combat presentation cases run actual combat, capture and nuclear delivery through
Session. They verify promotion before retained casualty events, content-supplied
history colors/names, capture announcements after scenario decisions, local-viewer
focus and nuclear notices to distinct participants. Required promotion-notice
failure preserves its prefix; a failed nuclear notice is reported after the
actual interception or blast completes. General native combat UI acceptance and
the generated host's color/targeting bindings remain open.

Retained research-goal cases cover category rebuilding, captured Help operands,
reference/path acknowledgment, stable recommendations, cancellation/timeout,
privacy and disposal. Existing theft and creation/contact cases now traverse the
actual goal decisions. The native desktop driver also opens goal and reference
pages during upkeep and returns to research selection before continuing the turn.

U04 activation cases now join the next-unit search to real contact-capable W03
revelation. They check the combat-loss origin, order-sensitive timer clearing,
retained contact, live simultaneous-mode tail and private reply routing. Failures
and disposal preserve the published selection/revelation prefix. These cases do
not yet qualify general native focus/stack selection. The F04 human-phase family
now composes actual selection and active execution, with wait/city-window gates,
bound-change reselection, distinct session/phase exits and private Session
segments. General native human-phase admission remains open.

U04 Disband cases join actual cargo loss, city removal, production snapshots
and retained lifecycle decisions. They cover signed credit, live confirmation
inputs, private/stale replies, cancellation, failures and desktop save/replay.
Native input checks exercise both city shield credit and last-settler defeat.

U04 player-order cases cover Fortify versus applied fortification, air and coast
Sleep gates, explicit Home versus recovery orders, real Wait contact and simple
local-unit exhaustion. The desktop suite exercises Wait/Sleep, waking selection,
Home/Fortify and saved continuation through direct, console and native inputs.
Off-city Home execution and the general movement service graph remain open.

V03 retained-grant cases now use the production `SinglePlayerResearch` bundle.
Joined science/capture cases verify actual government decisions, stable RNG
across unanswered prompts and AI production reset after acquisition. Session
failure cases retain the acquired technology and first-discovery tail while
reporting failed cosmetic presentation, with no duplicate completion on polling.
Desktop science uses this composition; full foreign-actor presentation remains
outside the authored profile.

The single-player diplomacy composition cases join actual gold/technology gifts,
map exchange, treaty signing and AI advance exchange with private recursive
Library replies. The scenario cycle uses the same contact graph from unit
creation/revelation and waits through permission-event grants before later
money/text effects. Denial, failure, disposal, repeated polls and stale/private
replies preserve their prefixes. This qualifies the shared composition;
desktop movement admission and generated-game UI acceptance remain open.

Retained partisan tests use that diplomacy graph to pause U01 creation inside a
scenario contact. They check delayed fortification/veteran draws, retained count
and threshold, live later placement, allocation failure and presentation failure
handling. A complete C09 capture routes the contact to a separate participant
and delays its final city fields until that participant replies. These joins
remove the synchronous-creation blocker in the movement/capture dependency graph.

Retained C01 creation tests pause a first AI city's free defender in actual
scenario contact. They verify the initialized city and defender remain stable
while route clearing, environmental flags and commodities wait; later terrain
changes are read on completion. Disposal, required child failure and failed
unit allocation retain their specified effects. A private founding/session case
waits through contact before deleting its settler.

W06 retained-creation cases join mercenary, nomad, barbarian and tribe rewards
to actual U01/C01 and production diplomacy. They verify home clearing and
late-city choices wait for contact, unchanged identities/choice traces across
polls, live year/local-human decisions, private replies, cancellation/defaults,
disposal and required/cosmetic failures. U02 completion forwards the same contact
before its direction and cleanup tail. General native movement admission remains
open.

U05 paradrop retains W03 contact after landing, including scenario text and
recursive research decisions, before city notification and garrison cleanup.
Player, reactive combat and proactive unit-tactics cases use actual retained
revelation; private replies, cancellation/defaults, disposal and required failures
preserve the landing prefix. Child presentation failures reach the owning result.
General native paradrop and combat admission remain open.

The single-player lifecycle composition family uses the production research,
diplomacy and lifecycle bundles together. It covers scenario creation entering
contact and recursive grants, contextual recreation through elimination,
retirement, schism and terrain changes, private last-settler catch-up, accepted
espionage pretexts, required failure/disposal and actual settler placement. These
shared joins do not establish full desktop admission or complete-game acceptance.

The single-player combat composition family joins retained lifecycle, diplomacy
and research to capture, casualties, nuclear delivery and paradrop response.
Actual UnitKilled/CityTaken events can suspend through recursive Library grants;
private replies preserve the losing unit and delayed capture fields. Required
failures/disposal retain their prefixes, and casualty presentation errors reach
the final combat result. Full desktop combat/movement admission remains open.

The single-player mission composition joins authored city options to actual
research grants, capture events, nuclear delivery and lifecycle services.
Private visits, cooperative fallbacks, required failure/disposal and retained
presentation errors are checked alongside poison and unit bribery. These shared
cases do not yet qualify general desktop movement or mission admission.

The shared single-player movement composition exercises those services through
the actual dispatcher: private mission fallback scheduling, combat casualty
events followed by contact, capture grants/diagnostics, transport restarts and
boarding, hut creation, caravan donation and expulsion. Required failures and
disposal release movement ownership while preserving committed effects. General
native command admission and generated human/AI scheduling remain open.

Player movement command cases add actual scoped admission and execution-time
identity checks around this graph. They route private mission/focus contact,
retain cooperative fallback scheduling, preserve movement results through game
outcomes, and verify cancellation, disposal and required/cosmetic failures.
The default desktop content/presentation binding and native command UI acceptance
remain open.

F04 human-notice cases cover council scoring/schedules, tutorial/expansion gates,
retained last-city updates, private report choices and failure/disposal. The
stock council snapshot reads owned facts without city/AI recalculation. Desktop
turns execute notices after upkeep/autosave; direct/console replay and the native
turn-51 council scenario compare saved and resumed games. Original council
chapter files and generated-game scheduling remain separate work.

P04 substitution cases check token case/advancement, decimal wrap, safe HEX widths,
selector boundaries, culture-independent numbers and captured parameter ownership.
Actual parsed scenario text uses the adapter through private retained interactions;
preparation errors preserve earlier money changes without consuming the event.
Autoplay still prepares text. Process replacement is observed at the next action,
while already pending content remains unchanged.

Classic EVENTS grammar cases cover S02 triggers/actions through the F03 byte
reader, live name resolution, shared recognition flags, numeric gates, repeated
text/coordinates and partial failure. Parsed actions also run through the retained
turn trigger and accepted scenario-load workflow, including delayed one-shot
consumption and cleanup. These are authored specification cases; they do not
establish native scenario UI acceptance or original-game compatibility.

The [F04 original-game observation procedure](f04-original-observations.md)
prepares Windows save captures for the two undefined city-analysis expressions.
Its independent packaging tool uses stored F06 inspection; it neither runs our
gameplay formulas nor establishes compatibility from existing historical saves.

## Alder Valley local play

The [Linux M3 record](../validation/m3-linux-playable.md) gives the native
graphical scenario and headless reference commands. The suite compares direct
and console requests, and the X11 driver compares the saved and resumed state
with the reference and loaded CLI. Browser and other desktop acceptance remain
pending for this profile.

The [M4 desktop work record](../m4-desktop.md) tracks the next mechanics and AI
dependencies. Start search, generated-map joins, start scoring, research
recommendation/valuation and building/wonder queries run in the native foundation
suite. Shared city creation, spatial queries and full commodity refresh are also
covered; the local founding caller composes the real constructor. Native tests
join generated maps, all starting civilizations, real accelerated packages and
the retained uniform road path, including fresh replay and cancellation. Browser
execution and cross-platform qualification are deferred for this
workstream; leaf tests do not close complete human/AI-game desktop acceptance.
C05 support retirement now retains actual cargo destruction or bare deletion
through nested civilization elimination and private incident/research decisions.
Tests distinguish committed loss from the delayed redraw/removal/restart tail,
captured owner/government from live human membership, private reply routing,
cancel/timeout, disposal and terminal failure. Desktop upkeep awaits this child
before recalculation, mood and research. C05 famine likewise retains cargo loss,
city cleanup and captured-owner elimination/recreation before returning removal
or resetting the surviving city's food stock. Private routing, cancel/timeout,
bounded search and disposal/failure prefixes run through real lifecycle children.
The desktop caller awaits food processing before its first recalculation and
production; general city-turn composition remains open.
C06's retained unit branch now asks before abandoning a size-one city, then
waits through actual deletion, city removal and elimination before its second
creation and completion notices. Full production/session cases route the city
owner's choice and a nested foreign victim's decision independently; cancel,
timeout, disposal, failure and Darwin's research sequence retain their ordering.
Desktop production uses this adapter. Its retained trade branch now uses actual
C07 cargo selection: AI sampling, human confirmation/report loops and private
commodity lists are covered through the production/session parent. Dismissal of
confirmation/report returns to selection; dismissal of the selection itself
stops at the undefined negative-slot read, preserving the paid/created prefix.
The authored desktop profile still excludes settler and trade production.
C07 commodity search has direct/console and replay coverage through
`trade.commodities`, using an admitted save with Trade known. Shared cases cover
entry gating, live report rebuilding, private recipients and negative close.
The native advisor scenario scrolls the commodity list, visits populated and
empty reports, returns to search and compares complete saves before/after and
after reload. Foreign supply filtering remains covered by shared multi-civ cases.
V07 assembly now runs through the retained C06 production parent. Shared cases
cover full-part retries, live human membership, trusted signed replies, actual
AI launch, private choices and accepted failure/disposal prefixes. The player
prompt rejects cancellation and custom subtype values rather than treating a
negative reply as a universal cancel code. The authored desktop profile still
excludes spaceship production; these cases do not claim native part admission.
V06 incident cases compose actual war, stance and Anarchy transitions. They cover
directed treaty gates, live mission retention, human pretext without expulsion,
leniency and senate draw order, scenario/autoplay gates and retained failures.
The escape continuation destroys the unit before awaiting the victim and resumes
without repeating loss; cancel and timeout decline the response. Full mission
admission, desktop commands and saved pending incidents remain open.
V06 unit-mission cases cover Palace/Communism/signed-cost pricing, cached offers,
counter wrap, nearest-city home assignment, visibility and transfer effects.
Sabotage uses the actual HP query and resumable escape, including nested
last-settler elimination and victim decisions. Command-runtime cases verify
private menus/offers and affordable payment choices. Movement and general
desktop presentation callers remain open.
City sabotage/poison groups exercise real building and city-memory changes,
captured target names, cyclic selection, ordered capture chains, custom targets,
AI-versus-AI damage suppression, direct poison size/food rules and retained
failure effects. Session cases follow actor confirmation through committed
poison/unit loss to the human victim's decision. These are mission-child tests;
they do not claim full movement or desktop city-mission integration.
Captured-technology and theft groups cover live A08 valuation, retained best ties,
actual goal help, V03 grants and C06 AI production reset, V07 race selection and
U01 defender scans. They distinguish targeted versus ordinary flag timing through
pending incidents, preserve failure prefixes and exercise real session privacy.
Returned AI fallbacks remain requests for the unfinished city controller.
Retained grant groups exercise government acceptance after stored knowledge,
actual emissary reception/reset, recursive Great Library acquisition, private
recipient routing, cancel/timeout and terminal failure/disposal prefixes. Captured
technology, both theft paths and hut scrolls now suspend inside actual grants.
Integration cases preserve the distinct theft-flag/escape order, captured-city
presentation after grant completion, and U02 movement charge/relocation before a
private hut government choice, with final direction/cleanup afterward. The real
Anarchy setter recalculates owned cities. Research completion and accumulation now
retain grant/emissary and final government choices, including the two independently
ordered government proposals and the free-advance flag check. Darwin cases wait
through the first government question before the second completion and later
production reset. Private recipient, cancel/timeout and disposal/failure cases
exercise actual children. Civilization catch-up cases now pause inside recursive
Library grants before subsequent advances, counter clearing, alive restoration
and placement. Actual last-settler elimination, civil war and combat casualties
retain that decision; tests cover private command routing, decline/cancel/timeout,
terminal failures/disposal and synchronous-call rejection. Complete desktop
production composition is pending.
Embassy/investigation groups distinguish plain deletion, cargo destruction and
escape, captured Spy/type state, live investigation flags and one-point movement.
They exercise actual nested elimination before final notices. Session cases verify
private window routing, single adapter invocation, closure and failure handling;
the supplied window fixture is a P07/P06 boundary, not desktop report acceptance.
U07 groups cover third-party SDI, actual mixed-owner stack deletion and nested V01
incidents before the blast, direct-blast unit retention, signed city population,
wrapped aliases, primary feature draws, W05 pollution memory and hotseat attitude
gates. They verify terminal failure prefixes and private session routing; these
service cases do not claim movement/combat delivery or desktop nuclear UI acceptance.
V06 device groups join real capture checks to U07 and V04 identification. They
verify all capture-loss positions, relocation without a final escape, post-blast
government/veteran reads (including a destroyed actor), nested elimination from
either child, actor/victim privacy and terminal failure prefixes.
V01 civil-war groups use actual civilization creation, elementary city transfer,
map knowledge and stack reassignment. They cover region and farthest-city splits,
retained population targets, candidate aliases, multiple Palaces, failure prefixes
and bounded session continuation. NoSchism cases verify predicate/action routing
without claiming the still-required authored-action dispatcher. C09 query cases
exercise treasury rounding/faults and central-city ties.
S02 negotiation groups cover exact denial-value conversion and live event output,
first-match permission capture, stored-selector control classes, persistent
wildcards, local orientation and caller-owned loaded/alive gates. Action-dispatch
groups cover required child order, live enable flags, ignored ordinary results,
late one-shot consumption, reentry and committed failure prefixes. Actual money
actions verify single indirection, signed overflow/saturation and repeated denial
by consumed records. These synchronous service tests do not close the retained
authored-action dispatcher, parser or desktop audience integration.
C09 loss groups cover exceptional size-one admission, signed size/zero deletion,
captured-center regional input, filtered supported-unit loss and cargo/restart
behavior. Actual nested C01/U01/V01 incidents delay city route/wonder/spatial
cleanup; session privacy and failure/disposal checks retain the accepted prefix.
The existing C01 synchronous deletion groups remain regressions for the shared path.
C09 continuation groups join actual supported loss, C06 eligibility and A02
production. They cover first-defender termination, signed ratios, count wrap,
separate captured/live centers and required city-window closure/failure/privacy.
Partisan cases run actual U01 allocation and W03 revelation, including a real
meeting that changes the inputs after count/threshold capture. They verify
ordered terrain/feature scoring, exact owner queries, topology, military-limit
failure, last-result sentinels, veteran draws/flag preservation and single-unit
visibility. Required window-adapter fixtures do not claim P06 desktop rendering.
C09 parent groups compose the real war, civil-war, research, population,
production, elimination, partisan and wonder children. They cover ordering around
CityTaken, retained name/center/plunder, restoration/parity, Palace scoring and
fresh-gold payment, slot-zero/replacement quirks, observer-number retention,
incited units, theft after deletion, actual foreign last-settler incidents and
separate private Palace/technology/window decisions. CityTaken fixtures exercise
the required dispatcher boundary; they do not supply S02 matching/actions.
V06 revolt cases use actual classic text reading, escape/lifecycle, incidents,
government changes and C09 transfer. They cover signed price/discount order,
capital-coordinate and government distinctions, all option lines and closure,
resource failure, AI double-cost admission, custom payments, no-refund unit loss,
restoration versus ordinary transfer, real nested foreign incidents, senate
change before capture, private routing and a runtime-valid unaffordable dialog.
A02 groups now cover the complete ordered chooser with actual A03 tile
recommendation and U05 AI airlift children: survey captures, free irrigation,
redundant sales, donor termination, secondary interception, directed spatial
control, facility/wonder/unit selection, human preferences, cutoff and repaired
invalid reads. A direct/console desktop case imports automatic production flags,
completes real unit production and compares continued saves after reload. A01 outer movement, general advice/auto UI and generated-game turns remain open.
A01 assessment groups now run actual production reset, government transition,
unit support/loss and elimination/recreation children. They cover old census use,
ordered region policies, assignment/goal aging, paratrooper staging, power/history
and directed coordination, custom signed inputs and retained failure effects.
A08 government tests cover rate narrowing, strategic score inputs, availability,
lock/draw order and actual changes. Native direct/console turns verify old power
publication before refreshed human census and identical save/resume continuation.
The outer AI movement passes, complete U04 executor, remaining tactical policies
and full generated-game turn acceptance remain open.

W04 groups execute all seven jobs with actual stack progress, exhaustion, map
mutations, memory and near-city C03 calculation. U01/U04 fortification cases cover
signed home gates, on-tile foreign cities, fortress/airbase distinctions and refresh
order. Direct/console and native desktop cases start irrigation, save unfinished
progress, reload, finish through activation and compare complete classic saves.
Pillage diplomacy, general AI worker choice and full U04 execution remain open.

A03 city terrain goal cases join C02 suggestions, uniform road paths and A01
goal insertion. They cover pollution, directed treaties, bridge and rail
requirements, rejection of a route's first job, and retained job priority.
The desktop fixture imports a polluted city, advances through two city turns
with a reload between them, and compares civilization records with the direct
client, including terrain goals and government scores.

The authored game's automation binding can drain completed unit and improvement
events. The native cases check independent drains, command and city correlation,
and invalidation on load. An automation controller queries a valid next build
and submits the ordinary production command. Its resulting save matches the
manual command byte for byte.

The Linux native play runner's optional `--automation-desktop` argument adds
two checks for the separate Lua launcher. It drives the real task controls,
compares the production policy's save with the manual command, verifies that
reload stops the running script, and uses the normal turn dialog to cancel a
script-submitted command.

The desktop automation driver also records those sessions, removes the temporary
script files, and runs headless replay against the journals. It compares the
resulting classic saves byte for byte. Shared cases check invalid choices,
cancelled commands, incomplete traces, divergent receipts, damaged envelopes,
and journal capacity.

Player purchase/sale groups cover raw-row pricing, signed unit/unsigned building
costs, zero-stock doubling, caps/narrowing, affordability/disorder, cancellation,
terminal failures, Palace/scenario exceptions and once-per-turn sales. The direct
and console scenario confirms purchases and sales through real pending decisions,
compares complete saves and verifies later turn completion and sale-flag reset.
The native driver exercises the same controls, cancellation, quoted prices and
save/reload before completion.
Shared C02 worker/yield tests cover real stacks, facilities, knowledge, water
sources and pollution visits, including signed rules, retained assignments,
AI cadence, specialist wrap and fresh-world isolation. The local desktop
founding/turn/load caller now runs the full C03 calculation with C05 support,
C04 mood stages, C07 commodity refresh and U03 road/rail links. Tests include
all-city recalculation after actual generated/accelerated setup and fresh replay.
U01 cargo/loss groups exercise actual stacking, grouping and deletion, including
priority, off-map staging, repeated capacity queries and complete physical-stack
loss. C07 route groups exercise ordered mutation, valuation, refresh gates and
retained failure effects. C05 retirement now calls actual support and U01 loss,
including a forced-shortage desktop-profile save/resume test. C01 local removal
calls actual garrison queries, cargo loss, route removal and radius repair.
C05 food-turn tests cover starvation, growth/refill and accumulation using real
C01/U01/C07 children, plus direct/console desktop-profile save/resume. A further
case joins actual city/unit removal and V01 elimination to the pending defeat
reason. Local lifecycle cases exercise recursive settler checks, actual AI
restarts, interrupted spy escape, spaceship reset/loss, history and retained
human/alive/knowledge state. Controlled V06 choices verify escape odds, promotion,
nearest-city ties and actual relocation/exhaustion. General diplomacy/mission
callers and full generated-game outcomes remain open. Resumable elimination cases
join actual nested last-settler checks and interrupted spy incidents. They verify
loss before a pending pretext, separate nested replies, retained scan/password
prefixes, bounded creation search, disposal and terminal failures. A real command
session routes the decision to the victim and rejects other seats' replies;
cancellation declines the pretext before the lifecycle completes.
V02/C04 groups cover local government queries/transitions, actual all-city C03
recalculation, fixed production substitution, observer-dependent AI tax/notices,
disorder duration/restoration and real Democracy collapse, tax wrap and celebration
growth/facility limits. Direct/console tests exercise the desktop mood phase and
save/resume. Player government/rate choices, C06 production, remaining city-turn
consequences and generated-game turn acceptance remain open.
Shared C06 unit-production tests cover actual creation and capacity failure,
signed stock/cost behavior, disorder/barbarian accumulation, upgrade selection,
veteran facilities/wonders, transport counters, settler refusal/cancellation and
city-disband recreation through real C01/V01 services. AI diplomat and nuclear
contact cases preserve branch/draw behavior. Cargo and AI choice fixtures test
required continuation boundaries and retained failure effects; they are not full policy implementations. Actual AI purchasing
now follows unit completion, using retained human wonder ranges and real V07
race predicates. Separate cases cover spaceship capacities and raw group counts. Shared cases cover unsigned costs, ordered spending
overrides, negative remaining cost, refund failure prefixes, fixed nuclear identity
and repeated almost-wonder notices without a second completion pass. Direct/console
desktop tests cover overflow, excess stock, capacity failure and save/resume through this shared path.

V07 construction groups now join actual calendar, performance, group selection,
assembly, launch and bilateral treaty mutations. Cases cover loaded custom part
maxima/mass, the missing-kind supplement and separate estimates, Fusion and shared
scratch retention, year wrap, human retries, first-ship contacts, AI launch
thresholds/slot0 comparison, directed diplomacy and callback failure prefixes.
Player presentation and A02 reselection remain boundary fixtures; complete C06
nonunit turns and spaceship desktop reports/outcomes remain pending.

C08 maintenance and wonder-effect groups cover unsigned upkeep, disabled
first-prerequisite walks, ordered forced losses and captured-owner payments,
signed scalar wrap, Eiffel's real V04 attitude path and Leonardo's live upgrade
scan. V04 tests cover hotseat guards, independent attitude scratch, hostility,
reputation intervals, active-wonder suppression and human-only credit changes.
W03 Apollo cases cover all-seat exploration/city size memory, feature publication,
unchanged unit visibility and local failure boundaries. Direct/console turn tests
join real tax overflow to Palace loss, save/resume and global reputation decay.
Complete production/research wonder callers and general F04 turns remain open.

C06 order-setting groups exercise retained switch snapshots, full request
classification, unsigned-low16 penalties, signed/unsigned cost forecasts,
counter wrap, automatic chooser replies and failures, AI wonder start/switch/
abandon effects and live civilization reset scans. An integration case joins
actual C06 order setting to actual AI purchase spending with a controlled A02
reply. Desktop direct/console cases repeatedly change production while preserving
stock and per-type counts, then complete/save/resume. Chooser fixtures are not
complete A02 policy or V03 research-reset integration.

V01 restart cases now execute the real V03 grant service, including nested Great
Library acquisition, Leonardo upgrades and government/city changes. They check
the temporary inactive seat, post-grant counter resets, retained knowledge after
placement or restart denial and terminal failures after committed grant effects.
The actual elimination-to-recreation chain uses the same service. Full generated
desktop turns and general city/combat/mission caller integration remain open.

V03 cost/options/grant cases cover retained counters, human/AI/scenario scaling,
selection exemptions, duplicate and future grants, first-discoverer OR, source
narrowing, emissary draws and resets, Barracks credit/failure order, Philosophy,
raw-location wonder expiry, real Leonardo and recursive Great Library effects.
Government prompt cases use the actual V02 direct setter. Direct/console desktop
tests verify counter wrap and first-discoverer retention across completion,
save/reload and another turn. Research-flow groups cover selection resume and
single recommendation sampling, advice ordering, informational goal paths,
completion mode2, future labels, ability order, Philosophy/free-advance chaining,
progress overflow, actual production/government children and terminal failures.
The desktop regression suspends before forced building liquidation and verifies
that accepting research resumes maintenance before `turn.advanced`. General
presentation and generated-game caller integration remain open.

C06 production-parent cases join nonunit completion to real building storage,
research grants/selection, spaceship group choice/assembly and Eiffel/Leonardo/
Apollo operations. They cover human wonder priority using raw shield rows,
retained stock/item, duplicate and lost builds, notice/failure order, AI Palace
reselection, two sequential Darwin completions, AI part retargeting and common
automation tails. Desktop direct/console cases build Temple and Library, retain
manual orders, save/reload and reject duplicate completion without spending.
The native driver additionally builds a Temple through city controls and reloads
its save. General A02 policy, eligibility and generated-game integration are open.

## Complete M2 map renderer

M2 development is [closed with tracked deferrals](../m2-closeout.md). Continue
the renderer regressions below during M3 integration; failed/pending performance
and platform acceptance stays in M2-F01–F08 rather than being relabeled as passed.

Follow the [map-rendering acceptance matrix](map-rendering.md) for all P02
MV01–MV30 rows, [interactive workloads/budgets](../performance/map-rendering.md)
for performance, and the [visual ledger](../validation/map-visual-coverage.md)
for separate native, browser and live-provider status. The optional Linux
application CI step verifies the pinned archives, runs the full software raster
matrix, exercises the desktop gallery and scoped Riverbend controls, and checks
the first draw after texture recovery against a fresh frame. The 1× gallery run
captures every board. The 2× run repeats input and lifecycle checks. Full
performance trials and physical presentation measurements use the separate
performance protocol.

The optional `OpenCiv2.MapBrowser` host executes the same exhaustive matrix in
WASM and the production Avalonia gallery in WebGL2. Its driver captures every
board with stock, native and classic art at 1× and 2×, exercises actual input and
content/resource failures, and compares presented pixels with fresh WebGL frames.
The overview footprint is independently checked in each screenshot. See the
[browser commands](../BUILDING.md#browser) and [execution record](../validation/m2-browser-map.md).
This test host retains reflection metadata for evidence serialization; the
separate production browser publication remains trimmed. The
[browser performance diagnostics](../validation/m2-browser-performance.md) compare
Software2D/CPU, WebGL/CPU and WebGL/GPU with real input and the shared scoped
large-world fixture. They preserve correctness and timing failures separately
from the completed CPU-composition functional matrix.

`python3 tools/check_map_assets.py` validates binding metadata; supplying
`--pack /path/to/runtime/data/atlas-hd.pack` also checks the pinned bytes. Neither
command executes the renderer. Keep root headless tests independent of graphics
packages/displays; optional renderer jobs use explicitly pinned content and the
production host. Retain per-feature visible evidence as well as semantic tests.

## Independent CLI/helper drivers

After the root Release build, run the following. Append `.exe` to executable
paths on Windows; use `python` in place of `python3`. Set `DOTNET_ROOT` if apphosts
need a nonstandard SDK installation. SQLite commands require its optional build.

```sh
python3 tests/inspection_cli.py src/OpenCiv2.Cli/bin/Release/net11.0/oc2 tests/OpenCiv2.SaveRoundTrip/bin/Release/net11.0/OpenCiv2.SaveRoundTrip
python3 tests/content_cli.py src/OpenCiv2.Cli/bin/Release/net11.0/oc2
python3 tests/content_pack_cli.py src/OpenCiv2.Cli/bin/Release/net11.0/oc2
python3 tests/rules_cli.py src/OpenCiv2.Cli/bin/Release/net11.0/oc2
python3 tests/preference_cli.py tests/OpenCiv2.PreferenceRoundTrip/bin/Release/net11.0/OpenCiv2.PreferenceRoundTrip
python3 tests/roundtrip_helpers_cli.py tests/OpenCiv2.MapRoundTrip/bin/Release/net11.0/OpenCiv2.MapRoundTrip tests/OpenCiv2.WireRoundTrip/bin/Release/net11.0/OpenCiv2.WireRoundTrip
python3 tests/live_save_cli.py tests/OpenCiv2.LiveSave/bin/Release/net11.0/OpenCiv2.LiveSave
python3 tests/sqlite_cli.py src/OpenCiv2.Cli.Sqlite/bin/Release/net11.0/oc2-sqlite
```

These independently exercise complete JSON/status results, exact byte round trips,
publication, paths, malformed input and preserved sources. The bounded live-save
helper does not implement a full game load/play flow. See [helper contracts](helpers.md)
and [configuration coverage](configuration.md).

## X02 execution

```sh
python3 tools/dotnet/pcg_fixtures.py
python3 tools/dotnet/run_conformance.py --report build/test-results/conformance-native.json --adapter dotnet tests/OpenCiv2.ConformanceAdapter/bin/Release/net11.0/OpenCiv2.ConformanceAdapter.dll
python3 tools/dotnet/test_conformance.py --adapter dotnet tests/OpenCiv2.ConformanceAdapter/bin/Release/net11.0/OpenCiv2.ConformanceAdapter.dll
python3 tests/conformance_script_adapter.py dotnet tests/OpenCiv2.ConformanceAdapter/bin/Release/net11.0/OpenCiv2.ConformanceAdapter.dll
python3 tests/conformance_session_adapter.py dotnet tests/OpenCiv2.ConformanceAdapter/bin/Release/net11.0/OpenCiv2.ConformanceAdapter.dll
```

After installing the WASM workload and Node/Playwright as documented in BUILDING:

```sh
dotnet restore tests/OpenCiv2.ConformanceBrowser --locked-mode
dotnet publish tests/OpenCiv2.ConformanceBrowser -c Release --no-restore
python3 tools/dotnet/run_conformance.py --report build/test-results/conformance-browser.json --browser-bundle tests/OpenCiv2.ConformanceBrowser/bin/Release/net11.0-browser/publish/wwwroot
python3 tools/dotnet/test_conformance.py --browser-bundle tests/OpenCiv2.ConformanceBrowser/bin/Release/net11.0-browser/publish/wwwroot
python3 tools/dotnet/compare_conformance.py build/test-results/conformance-native.json build/test-results/conformance-browser.json
```

The selected 57 cases cover fifteen operations. The runner verifies compiled
source identities, fixture policy, invocation artifacts, result/trace comparisons
and consumed prefixes. Expected observations never enter the adapter. Follow
[conformance registration](../conformance/README.md) to extend the surface.

## Foundation reports and infrastructure

```sh
dotnet tests/OpenCiv2.FoundationProbe/bin/Release/net11.0/OpenCiv2.FoundationProbe.dll > build/test-results/native.json
python3 tools/dotnet/write_identity.py build/test-results/build-identity.json --report build/test-results/native.json --report build/test-results/browser.json
python3 tools/dotnet/test_identity.py
python3 tools/dotnet/test_coverage.py
python3 tools/dotnet/test_publish_notices.py
```

Create `build/test-results` before redirecting output. Generate the browser report
with the build guide's browser test command. Source changes invalidate prior
reports; rebuild/rerun rather than updating their hashes. Native dependency and
publication inventories also reject missing or modified inputs.

The [CI matrix](../../.github/workflows/ci.yml) runs the integrated commands.
[Platform acceptance](platforms.md), [packaging](../packaging.md) and
[performance](../performance/README.md) supply their own procedures. Keep authored
contract tests separate from external original-game observations and claims.


The optional renderer integration checks (`MapProbe --renderer-options`) exercise
MV29/MV30 fallback, retry, context replacement and bounded caches for 100 warmed
observer replacements. They require Linux hardware EGL. The CPU and experimental
GPU choices retain the existing native interaction and raster matrix suites;
[the integration report](../validation/m2-renderer-options.md) declares the GPU
pixel tolerance and distinguishes local evidence from deferred platform gates.

`MapProbe --gpu-optimization-checks ART_FIXTURES` adds navigation-history,
shared-mask alpha/opacity, sparse-delta and separated-damage regressions.
Experimental matrices record rejected history differences; require zero
`historyFailures` before treating a candidate as pixel-correct. Set
`OPENCIV2_REPLAY_ALL=1` for strict fresh-GPU comparison of every saved native
frame and warmup in `--replay-gpu-experiment`. The staged native, profiling,
pipeline and latency drivers and their evidence are described in the
[optimization report](../validation/m2-gpu-optimization.md).

The browser terrain-offload candidate remains experimental.
`MapProbe --gpu-sampling-checks ART_FIXTURES` and the browser driver's
`--renderer=gpu --gpu-sampling-only` enforce fractional nearest-sampling,
adjacent-chunk coverage and focused art/zoom CPU bounds. See the
[sampling investigation](../validation/m2-gpu-sampling.md).
`run_map_browser.cjs --renderer=gpu --gpu-history-only` runs the same 204
navigation cases through the host's live WebGL context, requiring exact retained
versus fresh GPU pixels and the existing CPU/GPU bounds, plus 100 warmed scene,
density and resource replacements. Add
`--gpu-features=Atlas,Direct,Textures,Terrain,Encoded,Commands` to exercise that
candidate in the test host. Replacement failures save actual/fresh PNGs, draw
errors and the comparison receipt before the driver rejects the run. Stable-layout
mask experiments retain their separate strict diagnostic results and are not
promoted with this profile.
The [browser default report](../validation/m2-browser-gpu-default.md) identifies
executed configurations. `run_application_browser.cjs` checks default CPU (or
`--renderer=gpu`), explicit CPU, software CPU and rejection of software/GPU in
the trimmed host.

The same application input drivers now exercise the M2 shell menus and custom
confirmation dialog: every admitted action, menu/shortcut/button dispatch,
checked-state synchronization, focus cycling, Escape, repeat suppression,
text-entry isolation and resized hit regions. The browser runs at 1× and 2×.
`ShellInteractionCases` separately checks stale targets and interaction identities,
observer handoff, cancellation, deadlines and peer requests without graphics
dependencies. See the [execution record](../validation/m2-menus-dialogs.md);
macOS native menu tracking and real assistive technology are separate gates.

The [storage/service drivers](../BUILDING.md#application-storage-and-service-checks)
exercise the actual native and WASM file library, selection cancellation,
unchanged import/export, persisted preferences, removal and restart. Browser
fault injection aborts a write after request success and rejects quota writes;
only transaction completion can publish success. `PlatformServiceCases` runs
portable envelope/workflow/clock/silent-media contracts in both foundation hosts.
`PlatformStorageTests` adds actual native atomic writes and delayed selection/
commit lifetime cases. See the [execution record](../validation/m2-storage-services.md)
for remaining platform and playable-session joins.

M2 audio checks and native dependency setup are documented in the
[authored audio profile](../content/media-profile.md). The root suite reports an
explicit skip for device playback unless `OC2_TEST_NATIVE_AUDIO=1`; use the
private audio helper for repeatable Linux execution without speaker output.


C06 eligibility/choice groups cover prerequisite chains, coastal/ocean access,
active/expired wonder effects, plant replacement, spaceship gates and explicit
scenario restrictions. Ordered option lists retain the opening stock and mark
expired but available wonders. Manual-change cases cover rejected penalties with
automation cleared, accepted counter changes, switching back to the opening
category, suppression/Chieftain gates and direct AI editing without wonder notices.
The desktop stock/save scenarios now import automation flags and verify that
manual selection clears them before the next city turn.


V02 rate cases exercise cap normalization and negative luxury, cyclic balancing
and retained preferences/holds, captured-cap decision order, real C03/C08 preview
effects/failure prefixes, explicit report preparation and accepted rate changes.
Direct/console cases verify invalid-choice rejection, preference bytes, output
failure with retained effects, save/reload and another turn. The Linux native
play driver checks `civilization.rates`, sidebar observations and persisted
`CIV2.DAT` bytes against the direct reference.

The rate-editor decision group covers staged previews, retained holds, cancellation,
invalid/stale replies, rejection of overlapping commands, failed preference output,
direct-command save equivalence, reload/turn continuation and journal replay.
The native driver repeats draft cancellation and acceptance through the Tax rates
button and named choices. Checkpoint cases round-trip named interaction options
and reject duplicate or unoffered option identities.

Government/diplomacy maintenance cases cover the four-turn Anarchy calendar,
Statue and live Revolution/Human gates, child acknowledgment, imported turn-bit
clearing, mandatory human diplomacy draws, signed request wrap, hostile-intent
draw domains, directed flags, aggression aging, stale contact and ordered policy
children. The AI child cases are declared-boundary tests, not complete A08 games.
Desktop/direct/console cases cover revolution refusal/cancellation, Anarchy
reload, government and rate decisions during upkeep, output failure after an
accepted change, continued play and journal replay. The native driver compares
complete saved bytes before and after a further turn/reload.

V02 financial turn cases cover treasury normalization before city upkeep and
population/funds notices after assessment. Native desktop population decisions
compare complete saved and resumed bytes with the direct client. The single-city
profile does not reach the funds warning; composed session tests join its actual
rate preview/acceptance and preference-output failures. These checks do not
complete generated human/AI-game acceptance.

V08 score groups exercise signed mood components, backed raw wonder locations,
actual spaceship preview/arrival points, conquest map scaling, objective threshold
precedence and perspective, wrapped ratings and one-time semantic report
preparation. The desktop Advisors score report joins the session command; native
checks compare complete saved and resumed state against the direct client.
Localized fame titles and pending end-game dispatch remain open.

Hall of Fame tests exercise all failed-read record/prefix positions, exact padding
retention, stopped writes, stable tied ratings, hidden wrapped scores, retained
clear/retry records and display reload after failed save. Native storage checks
actual replacement/case aliases and short files. Desktop viewing compares Hall
and game bytes before/after viewing and after reload; end-game dispatch and
localized fame-title resources remain open.

Explicit retirement cases join actual history, city score preparation, separate
rating and Hall persistence. They verify confirmation, live report gates, flags,
failure continuation, direct/console closure and reload. Native acceptance plots
a retained history, compares complete Hall bytes with a direct reference and
checks that Quit skips reports. Scheduled retirement and victory/defeat handling
remain open; numeric reports do not yet consume authored fame-title resources.

F04 arrival and V08 pending-outcome cases join real history/score/rating/Hall
children, live continuation gates, partial output failure and actual last-city
famine defeat. The authored desktop turn checks scheduled retirement before
treasury normalization and city upkeep. Its Linux acceptance driver imports
independent authored dates, compares warning/continued saves and Hall bytes with
the direct reference, then checks both Keep playing and End game. These are
implementation integration checks, not original-game compatibility evidence.

V04 war/alliance groups compose real city destinations, cargo lift and whole-stack
relocation, treaty cascades and active wonder queries. They distinguish alliance
cancellation from war, exercise asymmetric obligations, signed credits and repeated
penalties, and verify forced/joined-war gates and retained failure effects.
Border cases retain the first-occupied-tile exit, mixed-stack membership, warning
flags, topology and current summaries. These local-engine groups supply pending
V06/A08 dependencies; desktop diplomacy controls and complete generated games
remain open.

A08 stance/willingness tests use actual race, wonder, spatial, treaty and restricted
government children. They verify retained evaluation fields, ordered arithmetic,
third-party ties, actual captured cities, historical overrides and the V05 patience
helper. Alliance reasons survive later rejection, and neither evaluation nor
willingness silently changes a treaty. Full audience/incident/review callers and
generated desktop-game acceptance remain open.

V06 city-controller groups exercise actual mission/fallback chains, retained
context across yields, complete authored option reading, UN/Manhattan and
technology gates, ordered AI selection and session actor/victim/capture routing.
Custom hidden replies reach the real bodies; custom network Chat stops explicitly.
These are service/content/session checks, not generated desktop-game acceptance.

U02 movement-charge groups cover city-mediated control across land/sea, captured
versus live cost inputs, road/rail/river precedence, conditional lotteries and
ordered AI approach/reversal/crowding stops using actual stacks. The desktop
land-step caller uses the shared charge service; relocation, exhaustion and
choice failures are checked together. Full directional movement and complete
generated-game acceptance remain separate from this ordinary-step integration.

U05 transport and special-finish groups run actual gather/selection/stack/loss
services. They cover landfall bounds and answers, cargo retention and exhausted
ship eligibility, unload/port/exploration distinctions, carrier/submarine basing,
signed fuel rollover, Coast-bound odds and topology, and nested last-Settler
incidents for both aircraft and cargo-aware sinking. Session decisions and
failure/disposal prefixes are checked separately from the unfinished directional
and desktop UI integration. No complete-game acceptance follows from these groups.

Player airlift cases cover ordered domain/airport admission, used destinations,
captured actor/origin/owner across selection, directed fighter reach and veteran
risk, actual single-unit placement, bounded interception rolls and nested
last-Settler incidents. Direct transfer, live observer notices, cancel/timeout,
disposal and failures retain the specified prefixes. Desktop airlift controls
and generated complete-game acceptance remain pending.

V04 attack-permission cases check retained senate thresholds, directed sneak
attack, active UN ownership/obsolescence, protected treaty sets, warning priority,
reply-one semantics, live state after the warning, recipient routing and failure.
U05 paradrop cases join real confirmation, cargo lift/stack placement, capture
and revelation. They cover direct versus command admission, topology/draw order,
viewers and documented presentation repairs, mixed-domain cargo, human versus AI
capture exhaustion, captured tile ownership, P06 suspension, replacement moving
actors and committed failures. These are service/session integration cases;
desktop controls and complete-game acceptance remain open.

U06 preparation cases cover signed strength bonuses, retained defense cache,
fortress/walls/fighter precedence, health-weighted defender selection and later
ties, city-slot-zero defense exceptions, ordinary/special/barbarian preparation,
preview normalization and actual submarine standing-goal replacement. Division
and authority failures retain their preceding cache/notice effects. These cases
do not execute damage rounds, casualties or desktop combat.

U06 round/promotion cases exercise two separate primary rolls, asymmetric ties,
protected-defender rerolls, captured values, bounded yields and choice continuity,
byte wrapping, HP-query side effects, missile prelude and hit presentation order.
They cover signed sampling boundaries, retained failure prefixes, aggression
wrap, inclusive promotion chance, live Sun Tzu ownership/obsolescence and
veteran/missile/autoplay behavior. Actual preparation is joined to ordinary
rounds; casualty/event chains and the full combat/session/desktop caller remain
open. No browser execution or full-game acceptance is claimed.

U06 casualty groups join loss counters, world-owned history and real U01/V01/V06
deletion continuations. They cover saturation versus wrapping, the 300-slot ring's
299-entry count, live field reloads, event replacement of retained slots, captured
raw next links, nested last-settler decisions and session privacy. Required S02
and presentation/content callbacks are boundary fixtures; these cases do not
claim the complete event executor or full combat/city/desktop integration.

U06 resolution cases now compose actual rounds, result presentation, promotion,
cargo/stack casualties, city population loss, removal/elimination, knowledge and
AI production reset. They cover sound/fallback/viewer rules, origin-medium and
protection exceptions, byte population wrap, retained notices, deleted-city
retasking, suspended last-settler loss and presentation failure prefixes. A05
retasking cases include signed capacity, directed Peace, strict weighted distance,
damage-adjusted full movement, retained tasks/spent moves and flat/wrapped maps.
These close the post-round stage, not the full player/AI attack command.

U06 entry groups cover charge-before-confirmation, cancellation/alliance refusal
without refund, primary sneak/government sampling, actual A08/V02 transitions,
V04 directional war and ally effects, signed difficulty adjustment and retained
failure prefixes. Integration proceeds from entry through actual rounds,
promotion and casualty resolution. The viewer/start stage, nuclear delivery and
final attack continuation are still separate pending joins; the scenario
combat-visibility selector remains an explicit U06 specification gap.

U06 opening cases cover eligible local viewers, captured/live ownership and
seen-by knowledge, the explicit unresolved scenario-selector boundary, actual
single-unit disclosure and cargo restoration, nearest-city notices, customized
substitutions, autoplay and fixed sound-slot/domain precedence. Real rounds and
resolution produce the same casualty outcome when opening rendering is skipped
or fails. P10's semantic contract permits batched rendering and omitted cosmetic
delays; no original view count or callback trace is asserted. These are service
joins, not desktop UI or complete-game acceptance.

U03 local-path cases now exercise ordinary reverse costs, terrain and feature
precedence, actual stack/adjacency/treaty penalties, foreign occupancy, strict
limits, retained cache reuse, directional ties, sea gates and seam-window order.
Generation, acceleration and road queries regress through the same FIFO engine.
A04 response cases use actual local paths and paradrop rather than supplied
directions or success callbacks. They cover raw target ownership, allied AI
admission, direction-zero rejection, inherited path modes, first-drop precedence,
real pending decisions and failure/disposal. Coarse snapping/waypoint and Goto
cases cover actual local children, authored graphs, exact ties, failed snaps,
bounded search failure, cylindrical adjacency,
airbase refusal and conditional cancellation/exhaustion. General active-order
scheduling, desktop integration and complete-game acceptance remain open.


U06 continuation groups join actual U01/V01 deletion, U07 SDI/blast action and
A04 response with its U03/U05/C09 children. They cover current versus original
moving actors, air exhaustion, signed wrapped fuel counters, consumed-unit loss,
interception versus detonation bookkeeping and nested incidents before blast
population changes. Session cases verify victim privacy and a real palace-escape
prompt reached through AI response/paradrop. Entry through opening, damage rounds,
resolution and survivor handling shares one world and controlled random stream.
Cosmetic redraw failures retain gameplay effects and diagnostic exceptions.
Final rewards/notices, V05 help and full combat/desktop acceptance remain open;
browser and cross-platform execution stay deferred.


V05 emissary/ally-help groups use real stance, wonder queries, treaty changes,
war declarations and unit expulsion. They cover autoplay/prior responses,
forced nuclear reception, report repetition, semantic greeting variants, narrow
meeting reset, directed admission, regions 1 through 62, retained payment and
live treasury wrapping, refusal attitude and captured recipients. Session cases
verify private reception/report/payment decisions and cancel/timeout behavior.
Cosmetic failures do not prevent accepted gameplay; required notice failures
retain their committed prefix. These service/session cases do not claim full
audience controllers, U06 final notices or desktop acceptance.


U06 execution groups now run from actual preparation through the final returned
movement result. They cover charge/no-charge execution, bounded round yields,
ordinary win/loss, zero-firepower loss, cancellation/no-target returns, nuclear
early return, barbarian stack casualties and ransom. Aftermath cases cover local
notice priority, autoplay/viewers, unsigned reward/treasury wrapping, actual ally
activation/help and cosmetic versus required-publication failures. Runtime cases
route real last-settler incidents and ally-help/payment/war decisions privately
without repeating casualty history, movement charge or rewards. These tests use
the explicit S02 dispatcher/presentation boundaries; they do not claim desktop
combat input or complete-game acceptance.

U02 foreign-unit groups cover ordered amphibious/submarine/diplomat/zero-attack
and aircraft gates, signed-range aggregates, full civilization bits and private
fatigue decisions. Expulsion cases use actual nearest-city/Palace selection,
cargo lift/return, visibility clearing, attitude sampling and directed race treaty
changes. They cover strict ties, negative sizes, suppressed autoplay input,
retained city text, role changes across a decision, cosmetic failures and terminal
semantic/random failures. Combat joins verify actual losses and promotion,
charge-before-permission, no-execute result inversion and common-finish routing
to the origin. Runtime cases retain private expulsion/fatigue/attack decisions.
These tests cover the foreign-unit phase; full movement dispatch, common finish,
cleanup and desktop input acceptance remain open.

U02 foreign-city groups verify captured domain/live range, signed attack bounds,
execute-clear nuclear preview, live city owner and directed treaty admission.
Actual V04 permission retains senate refusal and does not declare war. Actual
U07 joins cover SDI, blast population and losses, aggression reset, cargo-aware
interception destruction and private last-settler incidents. Failure cases retain
required-notice prefixes while cosmetic origin redraw failures remain diagnostic.
The phase returns its continuation route; these cases do not complete outer
movement or desktop nuclear delivery acceptance.

C07 caravan groups join actual road connections, research cost, cached city
calculation, lifecycle/incident handling and route/commodity updates. Authored
vectors cover repeated demand premiums, map-size/region/facility/freight changes,
successive technology reductions, payment caps and treasury/science wrapping.
Food preserves incoming food rows; home decoding distinguishes signed entry
comparisons from unsigned city selection. Entry cases retain a donation choice
across changed production/cost/rules. Reciprocal cases verify three draws, last
ties, suppressed supplies, exact demand bonuses and missing-name failure after
payment, deletion and route effects. Runtime cases route entry menus and actual
last-settler incidents privately. These cases do not claim cargo selection,
general U02/C06 integration or desktop trade acceptance.

U02 land-entry groups compose actual ZOC/revelation, caravan donation/delivery,
embassy/investigation, unit bribery/sabotage and allied repair. They cover captured
domain and ZOC exceptions, selected lift/return topology despite animation failure,
owner-only reload after bribery, signed HP/Barracks repair before exhaustion,
original-unit restoration, private session cancellation and terminal failure prefixes.
The entry selector is an explicit input pending U02 specification resolution;
these cases do not claim complete movement or desktop/full-game acceptance.

U04 long-move and U02 cleanup groups cover signed timer boundaries, reversal and
negative direction, full-order versus Automated admission, private stop/cancel,
retained replies, changed unit bound versus dead occupied slots, captured AI owner,
live transport counter and conditional exhaustion. These do not claim the full
movement-finish caller or network synchronization.

W06 hut-entry groups exercise actual city/unit creation and advance grants, all
six outcomes, override and redirect gates, global technology versus owned knowledge,
signed gold/turns and year boundaries, barbarian direction/stride/type selection,
rank-index use and capacity failure. Cases include regional restriction-bit
round trips, explicit prerequisite backing, late-city failure before invalid
writes, a bounded repeating redirect cycle, and private city visits. Full movement
and desktop joins and periodic barbarian behavior remain pending.

U02 arrival/finish groups compose actual C09 capture and actor reload, W03
revelation, U05 port/exploration/boarding and aircraft/cargo loss, W06 hut outcomes,
U04 long move and local cleanup. They cover landing-count wrap, captured-barbarian
stack visibility, direction-write timing, bound changes after creation/deletion,
private capture-to-long-move and nested last-Settler incidents, and cosmetic versus
required-notice failure behavior. These establish the local finish composition,
not the outer movement dispatcher or desktop/full-game acceptance.

U02 relocation groups cover single-unit barbarian/city knowledge, directed
embassies, alive/human/autoplay viewer selection, destination-neighbor disclosure,
Shift/Goto/captured-Air animation decisions and saved autoplay membership.
Supported-repair cases give absent/zero observers no viewer flags, keep huts
independent and complete unpublished finish paths without a cosmetic pause.
Actual U01 cargo grouping, whole-stack visibility changes, hut capture before
placement and an actual completion join verify relocation effects. Landing tests
cover slot-zero suppression, real/absent-owner treaty gates, retained failures
and the early plus ordinary-arrival double count. An actual dispatcher landing
joins these effects without supplying original backing values. Desktop integration
remains pending.

U02 dispatcher groups join actual child services across ordinary/preview moves,
negative finish, signed capacity/boarding, passenger restarts, city missions,
conquest and full combat. They cover source-owner publication, movement guard
lifetime, AI cleanup and committed failure prefixes. Session cases verify private
capture-to-long-move, passenger and fatigue decisions. Existing fixtures can share
one already-built dependency graph without recreating its unit records. Desktop
command wiring and resolution of the optional entry-selector gap remain pending.

Cooperative command groups verify one-step advancement, FIFO fairness, private
interaction pauses, cancellation ordering, duplicate suppression, external waits,
failure prefixes and disposal. The actual movement session test now advances its
landfall restart through the automatically queued Proceed, using the same
AdvanceOne path as the desktop timer; it does not manually wake the command.

U04 order-execution cases compose actual fortification, all seven terrain jobs,
Goto selection and movement. They check handled results despite incomplete work
or refused movement, ordinary/AI arrival, path-owner reset asymmetry, live target
changes, AI exhaustion gates and transport counters. Pending LongMove, disposal
and required-publication failures retain prefixes. Private runtime cases cover
reply/cancel and a passenger restart through automatic cooperative continuation.
Outer AI scheduling and desktop/full-game acceptance remain open; browser and cross-platform execution remain deferred.

U04 recovery-search cases distinguish ordinary from weighted city selection,
first ties, raw Land regions on Ocean, unrestricted carrier candidates, submarine
missiles, foreign airbases, signed fuel/range and zero-multiplier failure. A04
cases cover context/colonist retention, signed HP damage classes, no-moves cargo,
city and peace priorities, remote air versus sea visibility, severe recovery and
transport companions. An actual Sea recovery Goto executes through U04/U03/U02.
These phases return their specified routes to the complete chooser below;
the desktop game loop remains incomplete.

A04 guard groups use actual defender selection and shared goals to check scenario
and ferry admission, foreign-fortress rules, Palace/neighbor quotas, temporary
fortification, stack-order ties and reserve counts, periodic order release,
retained settler state and live walls/garrison strength. Recovery into CityGuard
with absent captured home retains its prefix and stops at the required read.
Specialized/partisan dispatch, the full chooser and desktop acceptance remain open.

A04 retained-order groups cover military versus naval Goto, the one transport
retry, empty foreign-city Trade exception, defender reasons and standing-goal
clears. Direction/finish cases verify actual order production, captured/live
profile distinctions, air byte wrapping, early work without domain capture and
naval arrival with/without transport. Partisan fortress cases preserve counter
wrap, map features, raw-region filtering and owned city ties. Guard-to-Fortifying
and AI-Goto results execute through actual U04 children. Full planner/chooser and
desktop complete-game acceptance remain open.

W04 core pillage groups cover automatic/explicit removal, partisan priorities,
distant city slot-zero confirmation, live unit/city changes across a prompt,
absent-city memory behavior, self-war flags, actual goals, cosmetic versus
mechanical failures and private session cancellation. A04 cases join real pillage
to common Finish and partisan target choices to AI-Goto orders, preserving first
unit, visibility/region/treaty gates and last direction/city-break semantics.
The complete AI chooser and desktop command registration remain open.

W04 player pillage groups cover eligibility, automatic single-choice Farmland,
explicit multi-choice removal, airbase/railroad menu order, city-zero versus
positive-city declaration, AI wrapper confirmation, retained menu/coordinates,
second core confirmation and cancellation after an actual prior declaration.
Private session menu/warnings and invalid-choice/disposal/cosmetic failures are
covered. The restricted desktop profile does not yet register this command.

A04 movement-planner groups cover strict directional ties, retained orders/Hold,
role promotion, invisible and Air base scoring, captured IgnoreZOC, stack admission,
signed/cutoff aggregates, self-indexed claimant credit, empty-city replacement,
exploration and doubled settler food. Actual U06 preview is exercised through
attack/guard and zero-cost failure; distinct scaled/unscaled scores can force
Hold after a valid low-movement partisan attack. Peaceful diplomat scoring skips
the ordinary tail. Actual fallback/opportunistic V06 bribery, private session
choices, disposal and publication-failure prefixes preserve changes while rejecting
the candidate. Full tactical dispatch and complete-game acceptance remain open.

A04 ordinary-entry cases cover work orders before context reads, off-map Sleep,
directed trespass gates and draws, and adjacent empty-city forcing with signed
roles and treaty protections. They join real recovery to shared Hold/Finish,
including air movement, exhausted transports and a return-home Goto completed
through actual U04/U03/U02 execution. Civilization zero is explicitly excluded
from this entry API until W06 dispatch is composed.

A05 naval-target groups cover zero-score ocean candidates, occupied center versus
noncenter admission, later ties, exact claimant/fertility mapping, human treaty
gates, hostile city bonuses and occupied land rejection. Coastal Goto cases
distinguish raw regions and first matches, admit occupied/protected targets,
exclude city neighbors, retain unreachable fallback and wrap only on cylindrical
maps. One case executes the selected sea order through actual U04/U03/U02 and
checks arrival/exhaustion; the full naval/transport policy remains open.

A05 cargo cases use the actual A04 guard quota, including Palace floors and
CapacityPressure versus first-release behavior. They cover full-stack traversal,
foreign owners, exhausted members, carrier preservation, non-Land exclusion,
actual Settle versus effective colonist role, signed/custom role slots, away
defender reservation, fortified bypass and per-scan reset. Missing reached quota
reads retain earlier order changes; unconsumed quota is not invented. U04 role
wakeup checks validated order, first-match stopping and preservation of carrier,
destination and movement. The complete transport controller remains open.

A05 port groups join actual cargo scan, city connectivity, regional quotas and
goal publication. Cases cover strict ties, ferry counter matching, congestion,
shortage/trade admission, attack discounts, selected versus last-candidate state,
signed capacity, fallback draws, shared-distance replacement and retained policy
reads. They complete fallback Hold through A04 and execute a selected port Goto
through actual U04/U03/U02 movement. Port/unload/controller composition and full
game acceptance remain open.

A05 unloading/controller cases run actual entry, cargo scan, port selection,
landing scoring, exhaustion, goals and wakeup. They cover NoMoves before port
selection, first matching sleeper, reposition orders, trade-only peace gates,
last absent home and signed route counts, retained Goto-region matches, damage
overrides, absent-unit-owner hostility, raw midpoint goals, first actual settler
relocation, port Hold and reinforcement/retry/turn gates. F06 conversions preserve
the consumed upper relationship bits and TransportRequested; W06 low-seven
settlement checks remain unchanged. Missing relevant Goto-region state retains
prior transport marking. Full naval destination policy and game acceptance remain
open.

A05 destination/sea-prefix cases cover reinforcement thresholds and ties,
directed allied raids, stride scans, last-human ally selection and ordered
score reductions. Trade cases retain the last present passenger home, reached
civilization cursor and specified live city-memory read, with signed scoring
and committed failure prefixes. Exploration verifies y/x draw order, reused
targets, regional boundaries and all 22 rays with later destination replacement.
The shared sea prefix uses actual cargo/transport/goal children, distinguishes
NoMoves from immediate unloading, and executes a chosen order through actual
U04/U03/U02. Full A04/retry orchestration and desktop full-game acceptance remain
open; browser and cross-platform execution stay deferred.

A07 caravan/diplomat cases verify existing-order gates, captured-region landing
scores, strict ties, signed route counts, ordered attitude/nearest-city discounts
and actual-home fallback. Diplomat cases cover raw absent-home partition, Road
thresholds, unprotected foreign orders, fixed Spy identity, treaty/rank/government
gates, actual spaceship-race state and retained owned-target orders. Real
no-target destruction reaches nested elimination, spy incidents and recreation;
session replies and cancellation remain private and preserve prior removals.
These cases do not claim the paratrooper policy, full tactical parent or later
travel/trade/espionage joins. Desktop complete-game acceptance remains open.

A07 paratrooper groups cover captured return-home state, solitary fortresses,
movement/Goto/War/range gates, late owned-unit cancellation, retained scores
versus signed city sizes, descending landing ties, fortress bonuses and wrapped
range. Actual U05 refusal and capture preserve the direct-zero return, including
healed AI movement left after capture. Private capture replies/cancellation,
disposal, missing draws and late failures preserve earlier relocation and city
ownership. A03 city-selector production is an explicit phase input pending the
full tactical join; these groups do not claim complete-game desktop acceptance.

A06 nuclear-target cases cover strict unsigned military-pressure gates, fixed
enemy versus live actor type counts, signed city size/counts, per-tile penalties
and full hostile stack traversal. Reach comes from actual owned cities and full
damage-adjusted allowance, preserving first ties and earlier reachable targets.
Staging tests cover occupied neighbors, ocean/airbase admission, preserved orders
and movement, directed and bilateral diplomatic effects and signed contact wrap.
The selected direction runs through actual A04/U04/U03/U02/U07 for detonation and
nearby-SDI interception. Full tactical orchestration and desktop full-game
acceptance remain open.

A06 air groups cover signed fuel recovery, actual carrier return, expended
attack thresholds/modifiers, first staging and redistribution, blocked best
targets and live versus captured types. Superiority cases preserve visibility,
treaties, equality/veterancy, damage-independent rule strength, transport/ship
counts and adjusted ties. Strike/basing tests cover occupied return candidates,
reach, first bases, sequential Airlifted objectives, Vendetta and absent-home
zero/Hold. Raids retain raw regions, all friendly-neighbor bonuses, later ties,
HasOrders and historical-scenario bypass. Actual recovery Goto executes through
A04/U04/U03/U02. Full tactical/turn/desktop acceptance remains open.

C01 founding groups cover actual creation/deletion, surviving stack companions,
feature memory without exploration, supplied foreign/inactive city slots and
separate player eligibility. Joining preserves signed-size wrapping, unsigned
Aqueduct thresholds and existing economy values. Naming acceptance, empty/Unicode
names, cancellation resets, supported-unit destruction, final city visits and
real nested lifecycle incidents preserve committed effects. Cosmetic failures
remain diagnostic; required failures and disposal do not roll back creation.
Six additional groups cover private text replies, revoked controllers, invalid
choices/lengths, Unicode/empty values, deadlines, stale dialog identities, replay
versioning and bounded recording. Actual C01 session cases exercise accepted and
cancelled names, final city visits and private nested lifecycle decisions. The
shared Lua suite covers text metadata and replies with Unicode and colons. General
desktop founding registration and full-game acceptance remain open.

A03 site/fortress groups cover first-city and refused founding, nearest versus
owned-nearest regions, effective versus live actual roles, strict radius ties,
real W01 huts, signed timers, regional counts and the independent advanced-human
tail. Real C01 creation/deletion, A01 goals and A04 direction application are
composed. Fortress cases distinguish ordinary live-home distance, negative goal
priorities, adjacent exclusions, four-line markers, coordinate parity and wrap.
A real A03/A07 join preserves the selected city after a newer nearest city is
added. Worker/population phases and complete-game acceptance remain open.

A03 worker/population groups cover retained GotoCity and admission, actual stack
and adjacent-owner goals, pollution and human destinations, fortress ordering,
signed irrigation times, resources, road/rail authorization, capacity retention
and rotated movement. Role-21 cases retain home identities above 127, compare
signed thresholds independently of government/result/source, preserve tasks and
allow the specified ocean/occupied radius destinations. Actual selected cleanup
runs through A04/U04/W04. Population cases use real cargo-aware destruction,
retain the supported-home distinction, signed counts/size, cosmetic diagnostics
and failure prefixes. Missing custom work definitions retain earlier goals. Full
tactical/turn/desktop-game acceptance remains open.

W06 periodic spawning groups cover turn/activity/cadence and city admission,
actual raid creation/sleep/relocation and ship-failure stack deletion. Sea search
tests include exhaustion, the last permitted candidate and short maps. Uprisings
use real treasury and control queries, moving nearest-city centers, signed sizes,
surges, fixed type identities, direct rank-index government and groups of four.
Capacity cases distinguish final failure from last success; visibility, local
notice/autoplay and cosmetic failures preserve committed creation. The rules bit
round-trips through save/preferences and retains setup defaults. Full F04 and
generated desktop-game acceptance remain open; browser execution is deferred.

W06 ransom groups exercise actual classic CITY content, language/install fallback,
decimal counts, variant selection and explicit malformed-message failure. Demand
uses the retained nearest city's size before replacing the city. Payment uses
live treasury, keeps the retained amount and removes actual stacks in radius
order without the center. Imported mixed-owner links exercise the complete
stack child and nested last-settler/spy incidents. Session cases cover private
victim routing, refusal/cancel/timeout, stale replies and changing nested
recipients; cosmetic and required failures retain their committed prefixes.
The owning chooser is covered by the W06 order groups below; full-game desktop
integration remains open.

W06 order groups cover transport lifetime wrap, capacity-first dispatch, polar
cargo deletion, ordered adjacent attack/landing, cargo wake/visibility without
relocation, treasury ties and actual A05 coastal routing. Nontransport cases
cover garrison and leader holds, periodic removal, escort goto, actual U03
pursuit with the southward bias, Partisans, later neighbor ties and random draws
before rejection. Actual U04 executes a selected move. Actual W04 pillage covers
successful removal and refusal followed by exhaustion. Real ransom payment,
private prompts, cancel/disposal, required failures and cosmetic diagnostics
preserve committed state. The shared A04 chooser now invokes this service;
full A01/F04 scheduling and generated desktop acceptance remain open; browser/cross-platform execution is deferred.

A04 complete-chooser groups use the actual W06, recovery, guard, A03, A05, A06,
A07 and ordinary planner operations. They cover early work/off-map/barbarian
priority, real recovery, scenario/ferry gates, city guarding, site founding,
worker orders, population absorption, pillage, special-unit destruction/Goto,
air return, nuclear staging/reload, actual paradrop capture and U04 execution.
Transport cases exercise exhausted ships with Defend cargo, actual retained-Goto
retry, reached quota replacement and the documented A05 initialization repair.
Port fallback and landing-match retention also use real owning helpers. Private
session cases drive ransom and planner bribery through payment/cancellation,
recipient privacy and stale replies. Required failures and disposal retain
committed prefixes; cosmetic founding/population failures remain diagnostics.
Full A01/F04 scheduling, generated desktop composition and complete-game
acceptance remain open; browser/cross-platform execution is deferred.

A01 local unit-pass cases compose actual A04 and U04 operations. They cover
captured actor/Goto bypass, the naval-superiority gate, real work/movement and
direct founding, reverse admission, historical Air-only first pass, exhausted
and sleeping units, 11/21 action limits through actual U02 busy rejection,
founding removal and a real hut-created unit visited on pass two. Extra transport
activation covers adjacency, visibility retained after custom sea-worker cleanup
and barbarian exclusion. Private ransom refusal continues into actual conquest
and plunder; payment and LongMove replies retain recipients and stale-reply
protection. Required failures/disposal retain prefixes and cursor failures are
diagnostic. U04's unresolved hold producer is explicit; complete F04/generated
desktop and end-condition acceptance remain open. Browser/cross-platform and
network execution remain deferred.

U04 reset/recovery groups cover all/exhaust/owner selectors, presence/fallback
setters, old-movement retention, border cadence, zero-timer wrapping, safe mission
bookkeeping, actual spy survival/return/loss and private victim decisions. They
join reset to moved-air attrition and cover C01 origin-region semantics, ordinary
distance healing, facilities, retained unsupported-domain selectors, signed HP,
airbase owners, wake-up, observer visibility and failure prefixes. Direct and
console desktop turns use the shared normal reset/recovery path and reject
mission flags outside the authored profile. Full generated-game F04 composition
and acceptance remain open; browser/cross-platform execution remains deferred.

W05 warming groups cover signed AI-tally/human-count arithmetic, Solar Plant
admission, repeated warning equality, counter wrap and retained event severity.
Terrain cases cover repeated coastal visits, narrow-map aliases, flat borders,
all terrain identities, dry coordinate selection, feature/owner preservation,
selective whole-feature memory refresh and distinct redraw gates. Autoplay
suppresses notices without suppressing warming; cosmetic failures preserve
updates and required map failures retain committed prefixes. Full turn-loop and
desktop composition remain open; city pollution/meltdown is covered below.

W05 city pollution groups cover risk and Solar Plant laws, scenario suppression,
signed intensity multiplication, all twenty B candidates, rejection without
retry, wrapping, memory and notice routing. Reactor cases cover Fusion Power,
all stock difficulty laws, extra unsigned difficulty, actual fallout/population
changes with surviving garrisons/SDI and unchanged diplomacy, and actual C04
restoration before hazard admission. No-pollution/autoplay gates remain distinct.
Nuclear and pollution presentation failures are diagnostics; required RNG
failures retain prior pollution and blast mutations without removing the reactor.
Full F04/desktop phase composition and game acceptance remain open.

P09 throne groups cover threshold wrap and the nonterminating-stage safe stop,
scenario/mode/palace/turn admission, actual V08 scoring, one increment per call,
hidden progress, signed count limits and the piece-7 availability distinction.
Semantic piece/overlay choices validate eligibility and local actor, commit once,
and preserve cancellation/disposal behavior. Owned view reports do not calculate
score; accepted progress/levels round-trip through F06. Private Session cases
reject another participant's reply, invalid choices and stale replies. V01's
creation case verifies initial throne levels. Full turn-loop/desktop integration remains open.

F04 global-composition groups exercise actual old-turn spawning/warming/power/
throne children, the initial-turn skip, signed calendar/peace wrap and retained
private upgrades. Real mission loss and victim decisions pause reset before
reputation/history/arrival; subsequent units reset only after that decision.
History cases cover strict deadlines, autoplay draws, signed deadline wrap,
immutable wealth rankings, last-equal ties and undisclosed rank gaps. Arrival
cases admit extinct slots, process one ship, retain winner/reason and defer the
Won flag until landing acknowledgement. Required failures preserve accepted
prefixes; cosmetic child failures retain diagnostics. Session cases verify
recipient privacy, cancellation/timeout, invalid/stale replies and terminal
idempotence. Full civilization/city and generated desktop acceptance remain open.

Desktop global-turn cases drive actual `turn.end` through direct and console
clients. They pause in old-turn throne selection, preserve the award on cancel,
continue to history after advancement, and retain warming/deadline/upgrade state
through classic save/load. Simultaneously arrived ships verify that post-global
outcome presentation cannot process a second arrival in the same turn.

F04 human-city maintenance cases join W05 and C08 with the captured owner's
current human admission. Actual pollution precedes payment, and a melted reactor
is removed before upkeep can charge or sell it. Cosmetic failures preserve
pollution, building credits and the remaining maintenance scan; required RNG
and quote failures retain committed prefixes. The old synthetic InHock mutation
case is replaced by ordinary payment/credit observations under C08/X01. Desktop
direct/console cases run actual C03 population pollution and classic persistence.

F04 city-pass groups cover entry resets, empty completion, descending live
selection through actual C01 transfers/removal/creation, captured upper limits,
lower-slot reuse and the removed-city result. They check pass-owner alliances,
destroyed/global Apollo markers, observer exclusions, returned balance/size and
disorder inputs to actual A08 rates, signed score addition and terminal required
failures. Desktop turns retain this pass through their real city children. The
two open parent formulas remain outside these specification-derived cases.

A08 advance-exchange groups use actual valuation and V03 grants, covering
strict ties, disabled versus admitted prerequisites, directional gift gates,
difficulty/count boundaries, the human-leader override, scenario suppression,
retained government/emissary choices and required-failure prefixes. A V04 parent
case reaches an actual Great Library grant to the human during both exchange
paths, suspends before the second transfer and later relations, then completes
without replaying choices or grants. Other AI diplomacy children remain open.

A08 war-ally cases cover paid/unpaid acceptance, alliance-breaking requests,
admission and enemy cleanup, refusal contact timing, first-enemy return, local
intelligence gates and technology ties. Actual borrowed-technology grants pause
for government decisions before treaty/payment effects. V04/Session cases check
private recipient routing, cancellation at all three decision kinds, cosmetic
diagnostics after committed effects, and required failures/disposal. These do
not supply the remaining relation-review or audience policies.

A08 security cases cover directed human threats including dead/self slots, nuclear
shortcuts, region boundaries 1–62, truncated ratios, shared-city gating, leader
traits and independent third-party treaty terms. Military-aid cases retain both
bounds gates, including a city-marked fallback tile, and verify no transfers on
flat or cylindrical maps. V04 and war-ally Session cases now use the actual aid
scan instead of a gameplay callback. Full relation review and audience remain open.

A08 relation-review cases cover missing/supplied attitude deltas, final no-contact
resolution, border marker retention/cleanup, first warnings, recent peace/contact
gates, emissary refusal/acceptance, bilateral intrusion flags, actual withdrawal
and missing-delta failures after relocation, senate draws and Anarchy/war effects.
Arithmetic cases distinguish signed division from shifts and test wrapped scores,
wonder transforms, race/rank gates, nuclear pressure and AI-coordination behavior.
The V04/Session case uses actual review, military aid and war-ally policies and
checks private warning routing and cancellation. Full audiences and generated
human/AI acceptance remain open.

V05 technology-negotiation cases exercise later survey ties, blocking wonders,
sales and price thresholds, actual captured-technology choice after the donor
grant, repeated exchange, refusal/gift paths, alternate-offer rejection, required
failure/disposal and nested government decisions. Session cases prove private
routing and non-cancellable technology selection. The displayed wanted advance
does not replace V03's actual chooser. Full audience integration remains open.

V05 gift cases cover blockwise gold valuation, offered amount deduplication,
signed gift/request counters, alternative technology valuation and actual grants.
Military cases use real nearest-city and stack operations, check selective owner
counters and live destination coordinates, and exercise obsolescence and strength
gates. Breakthrough tests cover index-zero suppression and grants without donor
knowledge. Private Session, cancellation, nested government and required-failure
cases preserve committed effects. Full audience integration remains open.

V05 favor/W03 negotiated-map cases distinguish asymmetric feature memory and
city-size sharing, direct unit visibility, map acceptance gates, request wrapping
and publication failures. War cases cover sequential price rounding, signed
distrust, admission overrides, cached payment, nested two-technology grants,
mercenary/betrayal flags and failure prefixes. Actual exchange and Session cases
cover patience repetition, private prompts and cancellation. Full desktop audience
and generated-game acceptance remain open.

V04/V05 treaty cases exercise negotiated wrappers with actual stance evaluation,
hotseat attitude storage, request/contact updates and ceasefire cleanup after the
required notice. Bargaining cases cover peace reasons and wonder compulsion, gold
rounding, payment retention, live post-grant valuation, solidarity/mediated war,
refusal branches and private nested government choices. Cancellation and failure
cases preserve actual prefixes. Full audience integration remains open.

V05 demand/proposal cases exercise conditional government/avoid-war draws, immediate
versus later provocation failure timing, signed role/distance withdrawal gates,
actual relocation, tribute ages and signed amounts, aid rank/technology gaps,
wonder protection, captured request modes and retained grants. Proposal cases
cover options, treaty dispatch, ordered cancellation/expulsion, private government
choices and nested exchange/treaty cancellation. Full audience orchestration and
generated desktop acceptance remain open.

V05 conversation groups compose actual gift/map/proposal effects and retained
government choices, greetings, first-contact emissary reception, forced treaties,
later-tie denunciation and border/region notices. Cleanup cases cover fresh patience
across bounded yields, direction-specific hostility, ceasefire expiration,
ambassador intelligence, post-reset tutorial reads and required/cosmetic failures.
Session cases verify actor-to-recipient-to-actor privacy, cancellation, timeout and
single intelligence-window creation. The local audience now composes entry and
opening; production movement/turn and full desktop audience acceptance remain open.

V05 opening-treaty groups cover alliance gates and later target ties without alive
filtering, solidarity against slot zero, refused-solidarity outcomes, conditional
concession/senate/wonder draws, unsigned peace thresholds and signed cash arithmetic.
Actual government grants suspend before treaty publication; grovel grants scan live
knowledge after earlier decisions. Tests verify cached versus live payments,
required-notice failure after signed treaties, private grant routing and the distinct
zero-accepts cancellation defaults. The local audience now supplies entry and
parent composition; production movement/turn and full desktop integration remain open.

V05 audience groups now join actual scenario permission/actions, stance, emissary,
mode-one technology exchange, opening requests/threats, treaties and common cleanup.
They cover initiation and refusal ordering, live tribute/technology operands,
conditional sampling, historical targets, private grant routing and cancellation
across different reply conventions. Controlled state changes occur at retained
player decisions, not at formatting callbacks. The authored S02 action adapter
executes the real money action; the remaining action domains are still required
owning implementations. Native Core/Session coverage and the App build do not
claim production movement/turn contact integration or graphical audience acceptance.

V04 contact groups compose actual local audiences, security, advance exchange,
research/Library grants, mediated ally-war joins and ally-help requests. They cover
admission/cadence, post-audience contact publication, historical exclusions,
observer/autoplay gates, raw AI treaty transitions, player-civilization reputation,
NATO city ties, directed incidents and committed failure prefixes. Private Session
cases exercise local-mode audience refusal, nested Library government decisions,
cancel/timeout and presentation diagnostics. These service cases do not close
production movement/turn contact integration or graphical audience acceptance.

V04 turn/contact groups now compose actual contact, exchange, relation review and
ally requests. Private Library government and ally-help choices suspend stale-contact
cleanup and later turn work; disposal, required failures and cancellation retain
completed effects. Session tests reject replies from the wrong participant and
report presentation failures after cleanup. The unresolved review delta remains
an explicit failure when needed. Movement and desktop contact composition remain open.

W03/U02 contact integration cases use real audiences and Library grants during
revelation. They verify pre-meeting disclosure, deferred outer visibility, cached
versus live post-contact inputs, synchronous admission and failure/disposal effects.
Actual movement charges and relocates once before the private meeting; final
direction and cleanup wait for its reply. Session privacy, cancellation and timeout
cover direct completion and dispatcher routes, including presentation diagnostics.
These checks do not establish graphical audience or full-game acceptance.

U01/S02 creation groups retain actual contact before tutorials and caller-owned
veteran/home changes. Location cases cover sea occupancy, coastal and foreign-city
gates, first accepted coordinates, count backing and home-name/selector behavior.
MoveUnit cases preserve slot-based selection, descending counts, signed eligibility,
rectangle comparisons and destination narrowing without immediate movement. Private
Session replies, cancellation/timeout and failure/disposal preserve allocated units.
These cases do not close full event dispatch or desktop scenario integration.

V01 placement/contact groups retain real U01/W03/V04 children before capital
assignment and extra-unit admission. They cover live human exploration, disposal,
required failures, cancellation/timeout diagnostics, and private last-city restart
replies. Nested technology help preserves its full reply through Session, lifecycle,
creation and contact without granting an advance or continuing placement. These
native cases do not establish generated desktop or graphical audience acceptance.

V01 forced-removal cases distinguish human-control removal from destruction and
recreation. They cover low-byte flags, sticky stack relinking, stale carrier wakeup,
retained city fields, routes/wonders, dead-city and Ocean radius repair, and actual
foreign spy/recreation children. S02 cases cover one-level recipient resolution,
local defeat ordering, private nested government choices, stale replies and
cancellation/timeout diagnostics. These cases do not claim full event dispatch,
network execution or graphical desktop acceptance.

S02 terrain cases exercise actual city/unit deletion, post-deletion stack lookup,
two rectangle passes, full terrain encoding and remembered-feature updates.
Empty rectangles still run cityless resets without an Alive gate. Actual
last-settler/Library children retain private choices; session cases cover
cancellation, timeout and diagnostic completion. W01 rectangular parity aliases
are tested independently of the shared fallback tile. Full event dispatch and
graphical acceptance remain separate gates.

S02 civilization-action cases cover one-level selectors, Alive admission, actual
alliance expulsion, zero-result effects, source-zero/duplicate/future grants and
retained recipient/advance operands. Library acceptance recalculates the city
through the real government setter. Session cases check private recipients, stale
replies, cancellation/timeout and diagnostic completion after emissary closure.
Retained action dispatch and ordinary matching are covered below; remaining caller hooks and desktop commands remain open.

Retained S02 dispatcher groups join all eleven actions and actual nested gameplay
children. They cover live flags, final-only consumption, actual grant-notice
re-entry, real terrain/reset and destruction/Library choices, required failures
and cosmetic diagnostics after completed gameplay. A real event→creation→contact→
negotiation-event→Library chain verifies private replies and delayed money/text.
Negotiation captures the first permission before actions, while NoSchism runs all
matching events without a Loaded gate. Direct audience and civil-war callers
retain these choices before their normal work. Media cases cover wave fallback,
Sound-only gating, CD retries/close, sparse text lines and autoplay preparation.
These tests use required media/text ports; platform playback, full authored P04
substitutions, S01 suppression consumption, remaining caller hooks and graphical/desktop
acceptance remain separate gates.

Ordinary S02 trigger groups cover all seven kinds, captured turn and signed
interval inputs, controlled RandomTurn draws, live appended events, persistent
wildcard bindings and technology polling through action changes. Actual C09
capture and U06 casualty chains retain Library decisions before their later
ownership/deletion effects. Session cases cover private/stale replies, cancel,
timeout and diagnostics after committed gameplay. Required-failure, invalidated
event and disposal cases preserve completed prefixes. F04/V05/UI hooks and
general desktop/generated-game acceptance remain separate integration gates.

Retained F06 load groups use real save decoding/install and actual S02 dispatch.
Text decisions pause before music/title and invalid-unit cleanup while preserving
the world pin and client identity. Tests verify option/event gates, core input
failure, disposal, invalidation and cleanup resource-failure prefixes. Actual
cleanup retains last-settler restart/Library decisions and observes later live
scan bounds; separate cases check the local-mode and stack-budget boundaries.
Existing synchronous F06 groups also run. Host participant UI and full desktop
load acceptance remain open; these authored cases are not original-game evidence.

F04 turn-entry groups compose actual global advancement, retirement/outcome
reports and all four scenario passes. The resume matrix distinguishes runtime
and saved values, including signed nonzero reprocess flags. Retained choices
separate live turn reads and confirm that later passes do not repeat the entry
gate. Quit suppresses events; Library choices retain private recipients; disposal
and required failures preserve completed effects. Cosmetic global failures still
allow event execution and remain diagnostic failures at completion. Existing
direct/console desktop end-turn regressions run through this shared entry.
Full civilization/AI scheduling, general scenario desktop composition and graphical
acceptance remain open; browser/cross-platform execution remains deferred.

V05 city-transfer cases join real S02 matching/actions, cargo/lifecycle deletion,
C06 eligibility and C08 Leonardo upgrades. They cover descending live ID lookup,
duplicates and same-owner cession, wrapped counters, the mutable current city,
first eligible defender selection with signed cost/defense and divide failure,
event-before-unit ordering, captured turns, live post-event scan bounds and
exploration without feature-memory replacement. Private Library/text choices,
cancellation, timeout, disposal, invalid nearest-city results and required/cosmetic
failures preserve committed effects. Existing C09 tests exercise the shared
production repair. Enclosing offers and desktop negotiation UI remain open;
these are specification-derived native cases, not original-game observations.

F04 politics groups join the actual government prefix, selection/rate operations
and full V04/A08 diplomacy children. They verify imported flag clearing, private
government-to-rate handoff, diplomacy only after the rate child finishes, preference
failure propagation, calendar-driven AI announcements and recursive Library choices.
V02 cases cover captured eligibility, unrestricted identities, the human entry gate,
tutorial timing, explicit rate targets, persisted values and terminal idempotence.
Cancellation, timeout, disposal and invalid routing preserve completed effects;
existing desktop direct/console government and end-turn regressions also run.
Full civilization/AI scheduling and general desktop service composition remain open.

A01 retained assessment cases exercise actual V01 elimination, U01 obsolete-unit
retirement, nested last-settler checks and V06 human pretext decisions. They cover
pending-choice polling, ordered retirement redraw, bounded recreation, disposal,
required-provider failure, private/stale replies and completion after the active
turn flag is cleared. Existing census, assignment, government, production and
desktop direct/console regressions exercise the shared stage iterator. These are
specification-derived native cases; full generated turns and graphical acceptance
remain open.

F04 post-city cases join actual government/rates, diplomacy, assessment and
population/funds operations. Human cases verify census before financial gates,
private choices for a civilization distinct from the command actor, two real rate
children and deferred preference failures. AI cases retain human Library and
nested elimination decisions before census completion or finance, without creating
an AI participant. Explicit financial cases recheck live gates after population
acknowledgment and retain milestone/preview effects on cancellation or failure.
Private/stale replies and disposal at each stage preserve ordering. The lifecycle
case excludes A08's recorded missing incoming-delta path; that gap remains open.

F04 autosave cases cover each rotation residue, CP1252 byte-prefix names, local
civilization gating within directory scope, retained ordinary-writer effects and
output failures. The command case verifies that autosave can write at the trusted
upkeep boundary while manual save and world replacement remain unavailable.
Authored direct/console turns wait for government/rate decisions before saving;
resume skips autosave, and headless replay preserves the same writer effects.
Native cases exercise actual files, case aliases, backups and failed replacement.
The authored ruler name now survives ordinary save/load. These tests do not
establish full scheduling, general scenario-host composition or graphical acceptance.

V07 report and F04 ship-tail cases cover raw/capped quantities, custom capacities,
strict arrival display, current shared success at confirmation, accepted launch
without revalidation, private replies and retained failure/disposal effects. An
actual structural addition runs between upkeep capture and report; autosave waits
for the real launch decision. Direct/console advisor commands save and replay the
launched state. The isolated native driver additionally exercises the production
Spaceships menu, report, decline/confirm, exact save comparison and reload.

U04 focus cases retain actual activation/contact, distinguish initial focus from
later selection, and validate the live shared active unit after contact. Private
reply, failure and disposal cases preserve accepted prefixes. The authored
selection command uses this path; direct/console cases compare saves and replay
when the requested unit is exhausted, with and without another eligible unit.
General native chooser admission and human-phase scheduling remain open.

U04 stack-selection cases cover entry gates, complete stack validation with nine
captured rows, immediate raw wake followed by cancellation, and actual retained
focus/contact plus the originally selected slot's final revelation. Session cases
keep wake and activation separate, reject private/stale replies and retain wake
changes on cancel/timeout. The authored explicit selection command uses the same
accepted path; its direct/console save/replay cases remain part of the coverage.
General native stack-chooser admission and human-phase scheduling remain open.

The active-unit cycle family uses actual U04 work/movement and automated children.
Cases distinguish handled orders and child dialogs from player-input waits, retain
the execution actor across live selection changes, and check the twenty-first
automated pass through real zero-cost rail movement. An intervening input wait
does not reset the counter. Session cases release the command lane for actual
fortification, preserve private/stale reply rules, block world replacement while
held and release pins on completion/disposal. General native admission and the
explicit hold producer remain unresolved; these are not complete human-turn runs.
