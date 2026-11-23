# Tests and evidence

Run from the repository root after the [build prerequisites](../BUILDING.md).
Shared behavior lives in `tests/OpenCiv2.FoundationCases`, executed by native xUnit
and the browser host. Native I/O/API boundary tests are in `OpenCiv2.Base.Tests`.
Optional Lua/SQLite and the X02 adapter have separate test hosts. See
[implementation coverage](coverage.md) for admitted APIs and exclusions.

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
first-prerequisite walks, ordered forced losses and notice-time mutations,
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
General AI orders, active-unit scheduling and desktop/full-game acceptance remain
open; browser and cross-platform execution remain deferred.

U04 recovery-search cases distinguish ordinary from weighted city selection,
first ties, raw Land regions on Ocean, unrestricted carrier candidates, submarine
missiles, foreign airbases, signed fuel/range and zero-multiplier failure. A04
cases cover context/colonist retention, signed HP damage classes, no-moves cargo,
city and peace priorities, remote air versus sea visibility, severe recovery and
transport companions. An actual Sea recovery Goto executes through U04/U03/U02.
These phases return their specified parent routes; the full A04 chooser and
desktop game loop remain incomplete.

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
