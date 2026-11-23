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
callers, resumable elimination and full generated-game outcomes remain open.
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
desktop turns and resumable foreign-spy elimination remain open.

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
