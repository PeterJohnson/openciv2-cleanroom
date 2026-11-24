# M4 desktop work record

M4 is in progress. The delivery target is the complete single-player mechanics,
classic AI and initial Lua automation specified in the
[implementation plan](implementation-plan.md#m4--complete-single-player-mechanics-and-ai),
integrated into the desktop game. Browser work, WASM execution and cross-platform
qualification are deferred for this workstream. Linux is the available execution
host; no new Windows/macOS acceptance is claimed. M2's existing deferrals remain
in the [M2 closeout](m2-closeout.md).

## Dependency groups and remaining work

| Group | Current state and remaining scope |
| --- | --- |
| F05 generated setup and starts | W02/setup join real V01 reset/leader/admission/placement, U01/W03 units/revelation, compensation and accelerated packages with real V03/A08, C01/C07 and U03 children. Cooperative operations and WorldEntry defaults are tested. Pikemen is now fixed at slot6 from verified repository original-data mapping; the generated desktop gameplay caller remains open. |
| City, civilization and turn economy | C01 local removal/creation/radius/spatial queries, C05 food/growth/retirement, C07 directional route mutations and the full C03 recalculation are implemented. C01 creation, C03 and the C05 surviving-city food/retirement phases run in Alder Valley founding/turn/load; last-city famine now joins actual V01 elimination, ordered end reports and game closure. The parent joins actual C02 workers, C05 support, C04 moods, C07 commodity refresh, U03 links and W05 intensity. C04 disorder/tax/celebration, C06 unit/nonunit production, V03 grants and resumable research completion, C08 maintenance and V04 global reputation now run in Alder Valley using real local children. C06 joins actual wonder, Darwin and spaceship effects. V02 rate operations, desktop revolution/government selection, the government maintenance prefix and V04 human diplomacy turns are now integrated. Remaining C01–C08 operations, general research/government callers and full F04 integration are pending. V02 treasury normalization and post-assessment population/funds continuations are integrated; the single-city desktop profile reaches population notices, with funds/rate composition tested through the shared session service. |
| Unit, world and civilization interactions | U01 creation and shared cargo/loss primitives are implemented. U01 creation, U02 allowance/HP/exhaustion and ordinary charge/AI gates, U04 eligibility/next selection and W03 knowledge primitives are integrated with the local game. W03 local surroundings uses real A01 goals and U04 contact orders. Local V01 last-city checks, V06 escape/capture and V07 reset/loss are joined and tested. W04 work execution and desktop queued jobs are now implemented. V04 local war/alliance transitions, cargo-aware expulsion and mutating border measurement now compose real children. V06 local incidents now compose actual war, stance and government children; failed escape can suspend for the human victim after actual unit loss. V01 catch-up now invokes full V03 grants with actual Library, Leonardo and government children; elimination/restart joins are tested. V01 elimination now has a resumable operation with actual nested last-settler and foreign-spy incidents, bounded restart search and victim routing. V06 unit bribery and sabotage now compose real transfer, HP and resumable escape/lifecycle effects. City sabotage and poison now use actual building, city-memory and resumable capture/escape children, with actor/victim decision routing. Technology theft now joins actual race selection, physical defender detection, captured-technology selection and research grants; fallback requests now run through the local city controller. Embassy/investigation now retain their distinct human/AI unit effects through actual nested deletion/destruction, with required intelligence/city presentation adapters. U07 now provides local SDI, actual nested stack loss, independent blast damage and W05 pollution placement. The V06 planted-device body calls that real action after capture checks and applies live identification consequences. V01 civil war now joins actual creation, C01 city transfer, knowledge and unit reassignment, with the NoSchism action boundary explicit. C09 population and supported-unit loss now compose actual resumable C01 deletion and V01 checks. The ordered C09 capture/incitement parent now composes actual war/civil-war, Palace/wonder consequences, loss/technology, production and elimination/partisan children with required CityTaken and P06 boundaries. V06 revolt now joins retained pricing/payment, actual classic option text, escape/incident/government changes and the complete incited transfer. The local city controller now composes these actual mission bodies, retained-context fallbacks and session decisions. U05 local landfall, boarding, unload, wakeups and aircraft/coast continuations now use actual cargo, selection and resumable loss services. Player airlift now composes destination and fighter choices, retained risk, actual single-unit relocation and resumable interception loss. Local paradrop now composes senate/attack permission, cargo lift/stack placement, capture and revelation, with session decisions and documented presentation repairs. U06 strength, defender selection and common combat preparation/preview now use actual stacks, movement, building effects and AI goals. Damage rounds now preserve primary sampling, bounded yields and hit effects; promotion joins actual C08 wonder checks. Casualty accounting now joins world-owned history, the required S02 dispatch boundary and actual nested U01/V01/V06 deletion with session decisions. Post-round resolution now composes presentation/promotion, loser grouping, city population/removal/elimination, knowledge, real AI production reset and A05 exposed-city transport retasking. Combat entry now charges movement before actual attack confirmation and joins treaty/force-war, A08/V02 government and execution-difficulty changes. The local opening publishes filtered visibility and joins real cargo lift/restoration with semantic notices and sound identities; the scenario visibility selector remains explicit. Survivor and nuclear-delivery continuations now compose actual U01/V01 loss, U07 SDI/blast effects and A04 response, including private nested paradrop/capture decisions. V05 emissary reception and ally help now join real stance, intelligence gates, payment, war and cancellation/expulsion. U06 final effects and the local execution parent now compose the actual stages with private nested decisions and returned movement results. U02 foreign-unit admission now joins real expulsion, fatigue and complete combat with private decisions and explicit finish routing. U02 foreign-city admission also joins actual attack permission, nuclear action and cargo-aware interception loss. U02 land checks now compose ZOC/revelation, caravan entry, city/unit missions and allied repair; the optional entry selector remains an explicit specification gap. U04 long-move warnings and U02 local cleanup now preserve retained choices, signed timers and bound-sensitive results. W06 hut entry now composes all six outcomes with actual city/unit creation and research grants, including private city visits and retained regional restrictions. U02 local ordinary arrival and common finish now compose actual capture, revelation, special loss, hut, transport, long-move and cleanup services with private nested decisions. U02 local movement now publishes viewer knowledge, applies landing alerts/counts and performs actual cargo-group visibility and relocation. The local U02 dispatcher now composes those actual stages with retained landfall restarts, a world movement guard and private session decisions. Movement now opts into explicit cooperative command readiness, allowing the existing desktop AdvanceOne timer to resume bounded work. Production desktop movement service/command composition, the entry-selector gap and full audiences remain pending. Full diplomacy/movement/desktop mission callers, map sharing and remaining acquisition callers, complete movement/orders, desktop combat dispatch, movement/mission capture dispatch, forced elimination, remaining espionage, remaining transport/air movement and full nuclear command delivery, remaining pollution/warming, huts and barbarians remain pending. |
| Classic AI | A08 advance valuation is integrated with V03 recommendation. C06 AI spending now consumes real V07 race queries and retained F04 human wonder extrema. A02 city production now has its ordered survey/scoring policy, actual A03 recommendation and U05 AI airlift children, and the desktop automatic-production join. A01 census/strategy, power and goal assignment now compose actual children; desktop turns publish power and run human assessment. A08 government, diplomatic stance and alliance-willingness policies are implemented, with actual restricted government changes and V05 patience. A04 immediate attack response now composes ordinary U03 local search and the actual resumable U05 paradrop child. U03 ordinary and uniform queries share a bounded FIFO engine and retained cache. Outer AI movement, the remaining A03–A08 policies, coarse waypoints/general goto and full turn scheduling remain pending. |
| Outcomes | V07 configurable capacities, performance/group choice, assembly/local launch, race/arrival predicates, reset/loss and V01 destruction history are implemented. C06 now integrates local spaceship construction; V08 score/objective/rating queries and the explicit desktop score report are implemented. Hall of Fame record preparation, classic persistence, insertion/clear continuation and desktop viewing are implemented. Explicit desktop Retire joins retained power history, score, separate rating and Hall insertion; Quit skips reports. F04 ship arrival, V08 scheduled retirement and pending outcome dispatch now join real reports and continuation. Semantic ending pages use actual ship/history data; authored ending/fame-title resources, remaining history and generated-game callers remain pending. |
| Automation | The optional Linux desktop launcher hosts Lua scripts on the player seat and can record and replay the authored game's inputs, including interaction replies and cancellations. Desktop tasks now use separate heap-limited interpreter processes with exchange deadlines. Live task restoration remains M5; browser isolation is deferred. |
| Desktop acceptance | Complete human/AI games, full-turn snapshots and G01/G04–G12 cases with custom rules/failure variants remain pending. Implemented services and authored-profile checks do not satisfy this gate. |

The next mechanics integrations are the general V06 mission and city/combat
callers, A01 outer movement/U04 order execution, general
government/research callers, and full F04 turn processing
needed by the generated desktop game. C04's shared
disorder/tax/celebration handler now joins actual V02 local government transitions
and runs in the authored desktop turn; this does not complete general turn processing.
`GeneratedGameInitialization` now supplies actual starting creation/balancing;
`WorldEntry` selects it when its initialization service is absent. Its supplied
acceleration delegate is optional when acceleration naming content is available;
presentation remains a supplied child. Do not replace city, production
or turn children with counters or generic policies to obtain a successful launch.

## Implemented start-selection and research operations

`Core/StartingSiteSearch` implements F05 §7, including pre-draw insets,
incremented-attempt relaxation, the turn-zero live-unit distance scan, signed
regional fertility, claimant versus occupant queries, and the AI attempt-2001
return. A bounded `Advance` yields without inventing a human retry limit.
`Session/StartingPositionOperation` pins the world, copies captured regional
counts and hints, applies V01's hint only after search completion, and releases
the pin on completion, cancellation or failure. The caller still owns exclusive
setup progression; a storage pin is not a gameplay scheduler.

`StartingSites.Assess` captures the F05 §8 nearest unit for each normal seat and
then all scores before compensation. It retains later nearest-distance ties,
no alive/human filter, signed land counts and river-before-grassland scoring.
The shared U01 nearest-unit query and C01 nearest-city query expose owned
slot/distance values. The city query currently supports the unfiltered-region
domain needed by setup; C01's region/port and coastal queries are now also implemented.

`Core/Research` supplies V03 availability, ancestry, science-report prerequisite
counts and recommendation. Reached ancestry cycles fail explicitly; a later
matching sibling is not used as a fabricated successful result. Completed pure
subtrees may be reused within one ancestry query. The report walk remains a
separate repeated-edge count with its entry-only bound.

`Ai/AdvanceValuation` implements A08 §2, including working civilization traits,
the specified own-region expansion test, fixed-identity/custom-prerequisite
bonuses, all tribe adjustments, active-wonder queries and known direct children.
Human and AI recommendations retain their different candidate filters, sampling
and first-tie selection. `Research.CompleteStarting` performs actual V03
completion/grant effects for local turn zero before the recipient owns cities, with the
ordinary completion mode (clear progress). It does not substitute for nonzero
turn grants, network forwarding, UI research selection or city production reset.
At turn zero, ordinary advance counts and first-discoverer fields are unchanged.

`City/BuildingQueries` supplies C08 ordinary facility storage and distinct expiry,
active location, active owner and civilization-effect queries. Dead civilization
knowledge can obsolete a wonder; the recorded location and dead city payload are
not repaired. Only the Eiffel effect query applies the scenario suppression.
Maintenance and acquisition effects remain separate unfinished operations.

`Units/UnitMovement` implements U02 allowance, signed maximum/remaining HP,
movement left and exhaustion, including sea knowledge/wonder bonuses,
Coast-bound, Air damage exemption and the sea damage minimum. The standalone
remaining-HP operation retains its damage clear under simplified combat;
allowance remains pure. `UnitActivation` implements U04's distinct selectable
and awake predicates and its three-pass next-unit search, including sea penalties,
active-unit ties, the protected slot and Waiting clears across all civilizations.
The existing local movement and selection callers now use these operations.
Full order dispatch and movement arrival/contact remain open.

## Implemented creation and local revelation

`Units/UnitCreation` implements U01's authoritative initialization, first-hole
allocation, wrapping counters/tokens, movement allowance before retained damage
is cleared, unrestricted-nearest home selection, attachment and creation notices.
Revelation and notice/synchronization services are required children; failure
retains preceding changes. A wrapped zero token or a revelation child deleting
the unit does not change the allocated return slot. Alder Valley's creation
caller now uses this operation and then applies its producing-city home.

`Units/MapKnowledge` separates feature memory, exploration, city size knowledge
and stack visibility. Exact city lookup uses its tile gate and first matching
record; a city suppresses direct stack publication. The existing local ZOC
revelation caller now uses these primitives and validated stack traversal.

`Units/UnitRevelation` implements W03's three local scans: terrain first, adjacent
city/unit/contact effects second, and outer observer effects last. It preserves
cached versus live fields, medium/submarine detection differences, city garrison
concealment, the deliberate outer non-Ocean naval-goal test, and the independent
barbarian visibility union. It composes real A01 goal insertion/redirection and
U04 sentry/goto changes. Required meeting and pre-meeting disclosure services
remain explicit boundaries; no no-op diplomacy implementation is supplied.
The session owns final observer publication and repaint policy. Local revelation
rejects network mode before mutation; peer tile synchronization is not claimed.
Map exchange, Apollo completion callers and complete V04/V05 audiences remain unfinished; the local Apollo publication service is now implemented below.

`RevelationCases.cs` and `UnitCreationCases.cs` add twelve shared contract groups,
including real creation-to-revelation integration. Child-boundary tests isolate
retained-damage network creation and live meeting/notice continuations; they are
not full network or diplomacy integration evidence.

## Implemented civilization creation and starting initialization

`Core/CivilizationCreation` implements V01 §§2–5 as a resumable local operation.
Its partial reset retains the specified research, future-tech, spaceship and
regional fields, refreshes real A01 goals before clearing standing goals, and
captures regional counts before later search. It implements catch-up qualification
and sequencing around a required V03 grant child. Leader selection preserves
inactive-seat draws, late overrides, reuse, usage wrap and gender changes.
Admission preserves search-before-hint behavior, AI rates before rejection,
polar/reuse/deadline rules and old-tribe-only restoration. Rejected conquest posts
V08's pending reason separately from saved Won/Scored flags; fresh world
replacement clears that transient reason.

Placement composes actual U01 deletion/creation and W03 revelation, with the
captured descending barbarian scan, late capital-x, conditional revival units
and exploration-only final radius. `City/UnitAvailability` supplies C06's ordered
technology, obsolescence, fixed-identity, coast and AI-dominance gates. The
general nonzero-turn grant, diplomacy and elimination services are not yet
implemented, so these child-boundary tests do not claim complete revival.

`Session/GeneratedGameInitialization` owns the world pin and cancellation for
F05's reset, all eight creation calls, actual bonus Settlers/research, captured
starting census and final human rates/gold. Poll size changes no gameplay choice;
cancellation/failure retains committed effects and releases the pin. It admits
single-player setup after the fresh-world/rules/player/map boundaries. Turn-zero
contact uses V04's actual early return, before audience/treaty effects.

`WorldEntry` now uses these real starting operations when `Initialize` is null,
while retaining the existing supplied-child overload. The synchronous entry
is suitable for its existing owning workflow; cooperative desktop setup can poll
`GeneratedGameInitialization` directly. W02-to-V01/F05 tests cover preset and
custom maps, odd height, both topologies, Deity compensation, fresh replacement
and cancellation. The complete rule-loader/wizard/world-entry test also reaches
introduction without substituted creation or balancing. A playable generated
human/AI desktop game and accelerated starts remain pending.

## Implemented local city creation and commodities

`CityCreation` implements C01 allocation/initialization with first-hole reuse,
the 255-city extension limit, token/count wrap, retained inactive route partners,
raw Apollo knowledge, real default naming and C06 defender selection. The first
AI city has the specified turn-40 irrigation and later population/facility/unit
bonuses. Environmental flags retain sequential region-63 visits. `CityArea`
suppresses fertility across all 45 offsets and replaces the inner 21 claims.
Creation invokes actual C07 refresh before clearing output fields; it does not
assign workers or recalculate population/output. It rejects network mode before
mutations. The local founding command now calls this constructor, then applies
its accepted player name, founder deletion and bounded economy calculation.

`Commodities.Refresh` implements the gate, weighted terrain survey, all sixteen
supply/demand formulas, stable ties, exceptional middle slots and route/unit
suppression. Roads start at zero on every call under X01's required repair.
Tests include C07's published full score vector, distinct resource/road/river
weights, negative rounding, facility/tribe/technology effects, last reciprocal
route selection and safe failure prefixes. Scores are temporary calculation
values rather than original global-memory emulation. C07's erroneous reference
to radius A was corrected to the city-radius B defined by W01; no new geometry
or external source was introduced.

`CityQueries.Nearest` now admits the original origin-region filter and Port
Facility mode. Coastal queries include shared bodies and the deliberately
skipping sea-summary traversal, retaining last region and final traversal index.
General founding/joining/deletion, city economy and remaining C07 cargo/routes/
deliveries remain open. These additions do not close the full human/AI-game exit gate.

## Implemented accelerated city packages

`AcceleratedStarts` implements F05 §§9–10 with real C01 city creation, U01 unit
creation, V03 turn-zero grants/recommendation, C08 facilities, W03 memory/
exploration, and U03 uniform-cost road directions. It captures the last live
unit per civilization, preserves starters, sets the one stack home, applies
era-specific units/fortification/facilities, and retains the original last-unit
distance selection and first score ties. Failed first cities skip the remaining
package; failed required defenders or second cities stop after earlier effects.
Final turn narrows as specified and year remains unchanged.

The documented F05/X01 repair initializes the previously undefined last-unit
distance to five once per operation. Actual later distances persist across
candidates and civilizations. This changes stack-dependent original outcomes;
it is not independently validated original-game behavior.
F05-Pikemen was subsequently resolved from the user-identified original-data
conversion: Pikemen is fixed slot6. The required mapping and pinned data evidence
are now in F05 Appendix D; callers supply naming content only. The regression
renames the unit while preserving the fixed-identity branch. No reconstruction
sources were consulted.

`Research.GrantStarting` implements the turn-zero grant exit including duplicate
sources and future-tech counters. `CompleteStarting` rejects only the recipient's
owned production-reset targets, allowing earlier civilizations' accelerated
cities. The nonzero-turn grant service is implemented in the later research-grants
checkpoint; these start operations retain their turn-zero boundary. `GameState.Paths`
now retains W02's real flood into later F05 queries; fresh worlds get new
workspaces. `TerritoryQueries.PeaceOwner` distinguishes claims, occupants,
directed peace and alliance. Road improvements can skip blocked tiles while
the path continues; the goal itself is not changed.

`GeneratedGameAcceleration` pins the world, advances one civilization per poll,
retains capacity notices, and releases its pin on success, cancellation or
failure. WorldEntry uses it when `Accelerate` is null and explicit acceleration
options are supplied. Generated-map and whole-entry tests cover both eras,
topologies, fresh replay and cancellation/failure. General U03 goto/occupancy
paths and the generated desktop gameplay/turn caller remain open.

## Evidence and reproduction

The independently authored specification cases are in
`StartingSiteCases.cs`, `ResearchPolicyCases.cs` and `UnitMovementCases.cs`, registered in the shared
foundation suite and [coverage inventory](testing/coverage.json). They are
contract tests, not original-game captures. W02's original-target reconciliation
gap and X02's independent integration evidence requirements remain open.

Run the root Release build and native test suite from [BUILDING](BUILDING.md).
In a sandbox that prevents compiler-server IPC, the equivalent build is:

```sh
dotnet build OpenCiv2.slnx -c Release --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
dotnet test --project tests/OpenCiv2.Base.Tests -c Release --no-build --no-restore
python3 tools/dotnet/check_coverage.py
python3 tools/dotnet/check_graph.py
```

The .NET test runner itself requires local named-pipe IPC. Browser execution is
not part of this desktop pass. Run desktop acceptance when these operations are
connected to the generated playable caller; a headless pass is not desktop UX
evidence.

## Initial dependency execution on Linux, 2026-11-22

Root and desktop Release builds passed with zero warnings/errors. Root and App
formatting verification passed. The native suite ran 525 tests: 523 passed,
with the existing Windows file-sharing and opt-in native-audio skips. The
foundation report passed all 491 registered groups and verified 342 compiled
inputs. Coverage/project-boundary checks, conformance metadata checks, eight
identity tests and three coverage-inventory tests passed.

The existing Alder Valley desktop regression passed all ten checks with CPU
composition, pinned map art and fonts on an isolated 1440×1000 Xvfb display.
It saved on turn 7 and continued on turn 8, comparing direct-client, console,
native controls and loaded CLI results. It also covered failed partial-step
selection, required research, native picker import, malformed-save rejection,
replacement and shutdown. SQLite was not part of this run. This validates the
changed local caller, not a complete generated human/AI game.

Xvfb was absent on this host. The first alternate virtual Xwayland attempt
timed out on initial console input; it is not recorded as passed. The successful
run used Ubuntu's `xvfb`/`xserver-common` 2:21.1.22-1ubuntu1.3 extracted under
`/tmp/oc2-m4-xvfb`, without system installation. Reproduce the M3 native driver
command with `run_xvfb.py --xvfb /tmp/oc2-m4-xvfb/root/usr/bin/Xvfb`, omit the
optional SQLite argument, and select the M4 receipt path below.

Build identities now include the newly consumed A08/C08/U02/U04/V01
specifications. The independent verifier uses the same set; a regression test
rejects divergence between its specification inventory and the build manifest.
An initial receipt after expanding the build manifest was rejected by the old
verifier; the final receipt below comes from a complete rerun after correction.

| Receipt under `build/test-results` | SHA-256 |
| --- | --- |
| `m4-desktop-dependencies-play.json` | `4fdc5b7572d942ffd00425859eb59594106af8d30f107461906f6e8695ae13de` |
| `m4-desktop-dependencies-headless.xml` | `ea7814801cd047797f37af44030883bf2429bd221cbdfb8a06220fdc0acc6153` |
| `m4-desktop-dependencies-native.json` | `cb845f5982df1c08f0537d2ba0cca2858fce7fe9a435ce7f17b8091a1ff5dd3b` |
| `m4-desktop-dependencies-identity.json` | `393f6be17eacbc2d8001ddad3ab64814b11f396a3e9589cf00fdce30638fbc5a` |

## Creation and revelation regression on Linux, 2026-11-22

After connecting shared creation and knowledge primitives to the local game,
root and App Release builds again passed with zero warnings/errors, and both
solutions passed formatting verification. The native suite ran 537 tests:
535 passed and the same two platform/opt-in cases were skipped. The foundation
report passed all 503 groups and verified 348 compiled inputs. Coverage now
accounts for 62 API/test families. Project boundaries, conformance metadata,
eight identity tests and three coverage tests passed.

The isolated Xvfb desktop driver again passed all ten checks, including native
production, turn-7 save/turn-8 continuation, direct/native/CLI agreement,
scoped console observations and native import failure/replacement. This run
used CPU composition and the same pinned assets/fonts, without SQLite or
browser execution. It is still an Alder Valley regression, not the M4 exit gate.

| Receipt under `build/test-results` | SHA-256 |
| --- | --- |
| `m4-desktop-creation-play.json` | `799f286f901bc620b9ac76acf2d06ebba46f51dc196ed175aeb94150206d968a` |
| `m4-desktop-creation-headless.xml` | `539d65c2ed2e3a203148f8743119969e4715a90be4e0017c157982a5102985e6` |
| `m4-desktop-creation-native.json` | `83378d9b03264ed19734790d5ae57927dc3b69dc0a76d85210dfa41d2c330fcb` |
| `m4-desktop-creation-identity.json` | `fe93234c057ee973a8abf9512f64dc97a8c52b5a9056062fca418965209714f5` |

## Civilization starting integration on Linux, 2026-11-22

Root/App Release builds passed with zero warnings/errors, and both formatting
verifiers passed. The native suite ran 549 tests: 547 passed, with the same two
platform/opt-in skips. All 515 shared foundation groups passed, including the
real generated-map/civilization initialization and world-entry joins. The
foundation identity verifier checked 360 compiled inputs. Coverage accounts
for 63 families; project boundaries, conformance metadata, eight identity tests
and three coverage tests passed.

The desktop regression again passed all ten Alder Valley checks on isolated
Xvfb with CPU composition, saving on turn 7 and resuming on turn 8. This checks
existing desktop behavior after the shared setup/state changes; the new
generated starting path is currently validated through the engine/session tests.
No browser or additional operating-system execution was performed.

Build identities additionally include C06 and the normative specifications
consumed for its fixed identities, V04's turn-zero contact exit and V08's
pending-reason definitions. Inclusion does not claim those complete families.

| Receipt under `build/test-results` | SHA-256 |
| --- | --- |
| `m4-desktop-starts-play.json` | `292b3abf8036dc6103594a3b059a1262e95d7c1819ad2bcdca6019010484ce23` |
| `m4-desktop-starts-headless.xml` | `35e9fac61330f1c7ef04583b655d4d066b7ed3542ef514053eb346c6ac1d5e74` |
| `m4-desktop-starts-native.json` | `578744d51d1f8e1f424127d634e530aaa9e20335255e3dd4011ddca00168b8a8` |
| `m4-desktop-starts-identity.json` | `dbd0e3d94cf4a6efe481eab40bdc6879c5abf3e1aadfe150400f0114f8da104e` |

## City creation and commodity integration on Linux, 2026-11-22

Root/App Release builds passed with zero warnings/errors; both formatting
verifiers passed. The native suite ran 562 tests: 560 passed and the same two
platform/opt-in cases were skipped. All 528 shared foundation groups passed,
including thirteen new city/commodity groups. Identity verification checked
365 compiled inputs. Coverage accounts for 64 families. Project boundaries,
conformance metadata, eight identity tests and three coverage tests passed.
The C07 registry hash was updated after reviewing the radius-label correction;
its formal X02 operation remains unregistered, rather than implying complete
trade conformance from these implementation tests.

The isolated Xvfb desktop driver passed all ten Alder Valley checks using
the shared constructor, including native founding/worker/production controls,
turn-7 save and turn-8 resume, and direct/native/CLI state agreement. CPU
composition and pinned assets/fonts were used. No SQLite, browser or additional
operating-system execution was added. Full generated human/AI-game acceptance
remains open.

| Receipt under `build/test-results` | SHA-256 |
| --- | --- |
| `m4-desktop-cities-play.json` | `427cf675c34b34766788ef052e30a3dea77cb8f931c66fe2d11b1c344a265e12` |
| `m4-desktop-cities-headless.xml` | `3d35d6e0fe743d22c60295374e500c0ada67af5f4b755a0f7be0b0c21ec8f019` |
| `m4-desktop-cities-native.json` | `9ca1f486b0e75e8bed7b89b9db3ec50795f3c0fd28ed13603638ccfe74fdb3f9` |
| `m4-desktop-cities-identity.json` | `079f5babdf28af6df89290693aa02779a0ed628a8211eef32fc9f7777dd91903` |

## Accelerated starting integration on Linux, 2026-11-22

Root/App Release builds passed without warnings/errors, and both formatting
verifiers passed. The native suite ran 573 tests: 571 passed with the same two
platform/opt-in skips. All 539 foundation groups passed, including eleven new
accelerated-start groups. Identity verification checked 369 compiled inputs;
coverage accounts for 65 families. Project boundaries, conformance metadata,
eight identity tests and three coverage tests passed.

The isolated Xvfb desktop driver passed all ten Alder Valley regression checks,
including turn-7 save/turn-8 continuation and direct/native/CLI agreement, with
CPU composition and pinned assets/fonts. No browser or additional operating
system was exercised. Generated acceleration is tested through real engine and
session joins; full generated desktop play remains open. The then-open Pikemen
identity gap was subsequently resolved from original-data conversion in F05
Appendix D. The deterministic distance initializer is documented in
F05 and does not claim original-stack equivalence.

| Receipt under `build/test-results` | SHA-256 |
| --- | --- |
| `m4-desktop-acceleration-play.json` | `195b9e7a3adc673241f7c9bac2efb2c2e532cd1250a83c1da929f6deed4765db` |
| `m4-desktop-acceleration-headless.xml` | `14673ad123c778332c2d6ea40dd67317a8e2fe65c930ddfd34091c5300da63e1` |
| `m4-desktop-acceleration-native.json` | `01e432d48aa96dc7e3083eb4110941554649fb9d6011436821251bb0a7121725` |
| `m4-desktop-acceleration-identity.json` | `fcc0ccfd21c6793328f7f9eeb2c503d2446f12217380a65ca4ac80e7827c6bfa` |

## Shared city working operations

`CityWorking` now supplies C02 availability, tile yields, the three-kind
accumulator, blocked release, worker allocation and specialist normalization.
It calls actual U01 stack access, W01 resources/ownership, C08 facility/active
wonder queries, V03 knowledge and W04's diagonal irrigation-source predicate.
Availability also accumulates W05 AI pollution visits. The world-owned
`CityCalculationWorkspace` preserves processing/reallocation modes, suggestions,
support inputs and yield totals between calls, and resets with a fresh world.

Tests cover signed base/special yields and bonuses, city-component versus city
predicates, government-score wrap, facility/wonder order, repeated pollution
suggestions, mixed-owner stack stops, unknown availability components, worker
ties, retained sets without a center, overassignment, AI fourth-turn cadence,
disorder/specialist retention and the full remainder versus six-bit stored
specialist count. Negative specialist-kind indices and non-kind arguments are
outside this implementation's declared API domain; negative-population allocation
retains its signed remainder and normalization stops at that unsupported index.
Missing terrain work rules stop at the required read with earlier effects intact.

The Alder Valley caller uses shared availability/allocation/normalization around
its existing bounded support, distribution and mood calculations. Its one-capital
domain has no overlap or surviving Settlers; this is not a substituted general
C03/C05 parent. C03 overlap/capital, full support, economic/route/mood calculations,
manual city-window continuation and generated desktop turns remain pending.
Transport synchronization is outside this local service; browser and additional
platform work remain deferred. New tests derive expectations from repository
contracts and are not independent original-game captures.


## City working integration on Linux, 2026-11-22

Root/App Release builds passed with zero warnings/errors; both formatting
verifiers passed. The native suite ran 582 tests: 580 passed and the same two
platform/opt-in cases were skipped. All 548 foundation groups passed, including
nine new C02 groups and the extended fresh-game reset checks. Identity
verification checked 378 compiled inputs. Coverage accounts for 66 families;
project boundaries, conformance metadata, eight identity tests and three coverage
tests passed. Build identities now include C02, C05, W04 and W05.

The isolated Xvfb desktop driver passed all ten checks through the shared worker
allocator: native founding/worker/production controls, turn-7 save and turn-8
continuation, direct/native/CLI agreement, rejected import and replacement.
CPU composition and pinned assets/fonts were used. No SQLite, browser or
additional operating-system execution was added. This remains the Alder Valley
regression; the complete generated human/AI game and M4 exit gate remain open.

| Receipt under `build/test-results` | SHA-256 |
| --- | --- |
| `m4-desktop-workers-play.json` | `d2d7ad7169eef4ae82f93dfd3b09b3456ce5f197d87d49878ea68067db075cf5` |
| `m4-desktop-workers-headless.xml` | `b28640c45de37cba3df9926521d04b4693f9805b7c3976656407d2ce81e6bfdb` |
| `m4-desktop-workers-native.json` | `3ce948c88094ce7e95ed14ef8d25033a75e1ec0cb524f8be4abd6dc537471c80` |
| `m4-desktop-workers-identity.json` | `ec88c7e19b5b56af18ece3c8134d07066082685bb955ae920754c4e6c2f7197d` |

## Full city recalculation

`CityEconomy.Calculate` now executes C03's complete local parent: availability,
capital/overlap, support, ordered workers, recurring food, specialist normalization,
production/pollution/waste, commodity refresh, recurring trade, distribution,
five mood stages and publication. Cached calls retain F02's previous-city result;
the separate final-stat entry does not select the cached city. Calculations do
not advance stocks, collect revenue or change disorder/celebration flags.

`CitySupport` supplies C05 food/shield box factors, including turn-200 asymmetry,
rank-selector wrapping, civilization7 knowledge behavior and raw destroyed-wonder
tests. Support counts and flags use the actual unit scan, raw home255, signed
roles, aircraft row/column tests and fortress exceptions. The ordered growth
facility query is implemented; food-turn and retirement consequences remain open.

`RoadConnections` uses the retained U03 uniform flood, then checks the selected
route for roads, rail and directed alliances. C03 capital selection preserves
first/tie/distance behavior, raw seam overlap and airport-link overwrite. Recurring
trade consumes stored partner base trade without recursively recalculating it.
W05 pollution sees gross production before waste; corruption and distribution
retain signed arithmetic, score side effects, capitalization preview recursion
and narrowed record publication. C04 retains whole mood counters and all five
stage snapshots, including its distinct worker-bound clamps and military rules.

The local desktop founding/turn/load path now invokes this real parent, replacing
its bounded support/distribution/happiness calculation. Its outer turn loop,
research, production and pure display preview remain restricted to Alder Valley.
New tests include repository examples, custom signed rules, failure prefixes,
and actual W02→V01/F05→accelerated setup→all-city calculation on both topologies.
Fresh runtime replay reproduces results with no recalculation RNG draws. These
are specification-derived and implementation integration tests, not original-game
capture evidence or the complete M4 human/AI-game exit gate.

## City recalculation integration on Linux, 2026-11-22

Root/App Release builds passed with zero warnings/errors and both formatting
verifiers passed. The native suite ran 600 tests: 598 passed and the same two
platform/opt-in cases were skipped. All 566 foundation groups passed, including
18 new recalculation/support/mood/connection groups. Identity verification checked
388 compiled inputs. Coverage accounts for 67 families; project boundaries,
conformance metadata, eight identity tests and three coverage tests passed.
Build identities now also include C03 and C04.

The isolated Xvfb desktop driver passed all ten checks with the full C03 parent:
founding, worker and production controls, turn-7 save and turn-8 continuation,
direct/native/CLI agreement, rejected import and replacement. CPU composition and
pinned assets/fonts were used. This remains the Alder Valley regression, not the
complete generated human/AI-game exit gate. Browser and additional platform work
remain deferred.

| Receipt under `build/test-results` | SHA-256 |
| --- | --- |
| `m4-desktop-economy-play.json` | `e0c504ff61c757b912d30cc22ea94c588daaa4488f56e089fbc66391686d2946` |
| `m4-desktop-economy-headless.xml` | `c64163d453c5db7694f790e00c7b120089b05925dc3331138c4fbeb65d26f5d9` |
| `m4-desktop-economy-native.json` | `1be4463ed814b828ab66fd02785335b5a5a2e75068f9e159a107f05bb6b68415` |
| `m4-desktop-economy-identity.json` | `6a1aafaccf4ce8fd585ab317a138fcd98d10d68582332db820ac0819b574fbe6` |


## Cargo loss and directional route mutations

`UnitCargo` now supplies U01 mark/lift selection, aircraft and missile-submarine
categories, human/AI boarding priorities, signed capacity and repeated stack
capacity queries. `StackOperations.ShipsFirst` performs the actual two staging
passes, retaining existing staging occupants and mixed-owner placement behavior.
`UnitLoss` composes those operations with basic `UnitDeletion`: sea loss deletes
the entire physical stack, land loss deletes only the requested unit, and the
lifted-stage branch compares x alone. Local redraw and network synchronization
remain explicit effects; client request orchestration is not supplied by these
local operations. New active markers reset with the fresh world.

`TradeRoutes` supplies C07 ordered removal, slot writing, append and replacement.
It preserves trailing payload, signed route counts, full pre-clamp duplicate
comparisons, spare-slot food routes, first minimum ties and raw destination
valuation. Replacement uses actual U03 links and C08 airport queries; publication
calls actual commodity refresh with its separate gates. The specified X01/C07
repair retains existing routes when no candidate beats the initial minimum.

Eleven new groups cover specification examples, repeated selection, signed
capacity/counts, narrowing, spatial staging and retained failure effects. They
exercise actual children; they are not original-game capture evidence. C05
starvation/support retirement, C01 city removal, V01 elimination, U05 transport
movement and caravan delivery remain parent integrations to complete.


## Cargo and route dependencies on Linux, 2026-11-22

Root/App Release builds passed with zero warnings/errors; both formatting
verifiers passed. The native suite ran 611 tests: 609 passed and the same two
platform/opt-in cases were skipped. All 577 foundation groups passed. Identity
verification checked 393 compiled inputs. Coverage accounts for 69 families;
project boundaries, conformance metadata, eight identity tests and three coverage
tests passed. The extended fresh-game test resets every new activity marker.

This batch adds shared operations without changing the desktop caller. The ten
native desktop checks immediately above validate the preceding full city-economy
integration; cargo loss and route mutations await their city-turn callers and do
not claim a new complete-game acceptance result. Browser and additional platform
work remain deferred.

| Receipt under `build/test-results` | SHA-256 |
| --- | --- |
| `m4-desktop-loss-headless.xml` | `5746f0d7c68e430f44c7c5e334ca3aeea5d0b76de13e70c24c2fc6a54568853c` |
| `m4-desktop-loss-native.json` | `5ae407c9c0539aaac0da5d5cbdbedb0a2f2d672377b499bd771069e3f9828fc5` |
| `m4-desktop-loss-identity.json` | `25d6dc77e9cf5327e70455ed057d16967c201135a898411de54490c679dbe814` |


## City support retirement and local removal

`CityRetirement` now executes C05's shortage and affordable-disorder pass with
actual support charging, weighted-distance selection and U01 loss. Selection can
remove a foreign or support-free unit before restarting. Affordable retirement
retains role/city/fortress protections, the farther-air no-fallback rule,
coordination government-score updates and bare deletion. The later C03 calculation remains
the caller's responsibility. Alder Valley's turn now calls this shared operation;
its presentation adapter publishes the committed loss and refreshes map/city
projections. A loaded twelve-unit fixture forces the shortage, checks slot-order
ties, saves/resumes and advances another turn.

`CityRemoval` implements C01's local supported-unit scan, fixed Partisans,
AI garrison rehoming, one-slot tail decrement, ordered route cancellation,
destroyed wonders and spatial repair. It uses actual U01 role queries/cargo loss,
C07 removal and C01 area refresh, including nearby visibility union and later-slot
radius claims. Presentation receives the removed lifetime after these changes;
original control locks and drawing calls are not simulation state. Civilization
elimination and the network deletion wrapper are separate pending operations.
Tests cover signed counts, raw home255, retained payload and failure effects.

The user-identified original-data branch also resolved F05-Pikemen. Its pinned
rules/compatibility hashes match its provenance receipt, and the explicit mapping
establishes slot6. F05 Appendix D records the data evidence. Acceleration no longer
accepts a caller-supplied replacement identity; the renamed-unit regression and
generated initialization/acceleration joins use the fixed constant. No branch
implementation or reconstruction source was imported.


## Retirement integration on Linux, 2026-11-22

Root/App Release builds passed with zero warnings/errors and both formatting
verifiers passed. The native suite ran 622 tests: 620 passed and the same two
platform/opt-in cases were skipped. All 588 foundation groups passed, including
ten new retirement/removal groups, the forced-shortage local-game continuation,
and updated fixed-identity acceleration cases. Identity verification checked
397 compiled inputs. Coverage accounts for 71 families; project boundaries,
conformance metadata, eight identity tests and three coverage tests passed.

The isolated Xvfb desktop driver passed all ten checks: founding/worker/production
controls, turn-7 save and turn-8 continuation, direct/native/CLI agreement,
rejected import, replacement and normal shutdown. CPU composition and pinned
assets/fonts were used. These are the Alder Valley regression and shared
mechanics cases; complete generated human/AI-game acceptance remains open.
Browser and additional platform work remain deferred.

| Receipt under `build/test-results` | SHA-256 |
| --- | --- |
| `m4-desktop-retirement-play.json` | `fc0c4cd5a749fd85d660043dace5a0c1ec9e4a643df770a0231ef29a0d526263` |
| `m4-desktop-retirement-headless.xml` | `e1bf3b7fa7d77263a0f3c7dc3e838c74711e9b6e8c73622edb125fc7f7835c90` |
| `m4-desktop-retirement-native.json` | `ce93ecd111005718c4a7b0d6175ff48f5c052c90a52657287b08bb7561f64103` |
| `m4-desktop-retirement-identity.json` | `527c88688cd792052c6d12b402b01eb2085c1cd8403c83b1e350566993e58dd2` |


## Food-turn processing and desktop caller

`CityFood` implements C05 ordinary growth, starvation and the separate food
accumulation after C03. It retains the incoming food-route workspace, first
raw-home settler without owner filtering, human-knowledge deletion gate and
reverse route removal. Real `UnitLoss`, `CityRemoval` and `TradeRoutes` execute
the consequences. Granary/Pyramids queries are actual C08 operations; the raw
food rule controls refill even for AI, including blocked attempts and signed
population/stock wrap. Accumulation uses live full-width calculation outputs,
settler consumption and the specified shortage warning, without a second
famine/growth pass.

Alder Valley's turn calls the shared operation around its actual C03 calculation.
Direct and console tests load growth, shrinkage and facility-blocked cities,
advance, save/resume and advance again. The authored profile has no homed
settlers or food routes. Its admitted city-center yields feed size1, so last-city
famine is unreachable in this profile; defeat/V01 elimination remain pending for
general games. The
load test retains a real size2 deficit through C03, and separately verifies that
C02 repairs imported negative stock when sufficient food is available.

The core's removal path invokes a required elimination continuation after actual
city removal; AI size-two growth invokes a distinct required C06 request99
continuation. Tests observe those boundaries and do not claim to implement their
owning policies. Semantic notices preserve famine details and missing-facility
information. X01 permits presentation-owned preferences/tutorial hints and safe
city redraw without original native hint bits, widget flags or undefined
coordinates. Full F04, AI production and generated-game acceptance remain open.


## Food-turn integration on Linux, 2026-11-22

Root/App Release builds passed with zero warnings/errors, and both formatting
verifiers passed. The native suite ran 632 tests: 630 passed and the same two
platform/opt-in cases were skipped. All 598 foundation groups passed, including
nine new C05 groups and the direct/console food-turn save/resume case. Identity
verification checked 399 compiled inputs. Coverage accounts for 72 families;
project boundaries, conformance metadata, eight identity tests and three coverage
tests passed. Expectations are specification-derived and implementation
regressions, not new independent original-game captures.

The isolated Xvfb driver passed all ten desktop checks with CPU composition and
pinned assets/fonts: city controls, turn-7 save and turn-8 continuation,
direct/native/CLI agreement, rejected imports, replacement and normal shutdown.
This remains the Alder Valley regression; complete generated human/AI gameplay,
V01 elimination, C06 AI selection and general F04 turn acceptance remain open.
Browser and additional platform work remain deferred.

| Receipt under `build/test-results` | SHA-256 |
| --- | --- |
| `m4-desktop-food-play.json` | `871d4ec6fe9849a65a4b6ea820809d5ede6f31f9646ce30285dcd91724466d37` |
| `m4-desktop-food-headless.xml` | `92d5560f5716e94cdd57f666d9ef304f6744105b511ef5fec2afb33082a95d74` |
| `m4-desktop-food-native.json` | `d0b2e8e6179d1c85c174fe62e57225c7a56fd2e8cdc84c90a05b1c9b1ab136a1` |
| `m4-desktop-food-identity.json` | `a50e946819549135d3816c6d2cb5b3f55e5d79e0e896f7657a73e835a5043a80` |


## Local civilization elimination and escape

`CivilizationElimination.Check` implements V01's local last-city check using the
actual live-city scan, U01 basic deletion, V06 spy escape, V07 loss/reset and V01
recreation. It preserves captured descending unit bounds and recursive
last-settler checks. The last human receives the pending defeat reason and has
its password cleared while human/alive bits and exploration remain. Other
continuations clear exploration through W01 rectangular addressing, retain
remembered features and foreign visibility, and call the actual creation
operation, including restart rejection and its conquest consequences.

Destruction history retains capacity, signed turn, portrait and names; absent
killers reset the ship without history or notices. `SpaceshipLifecycle` preserves
first-launch state while resetting all ship fields. Launched takes precedence
over Exists for loss notices, with no invented autoplay or human gate.
`SpyEscape` uses the fixed Spy47 identity, conditional primary remainder draws,
actual nearest-city tie rules, individual relocation and movement exhaustion.
It retains no-city survival, mode-dependent promotion/notices, real cargo-aware
loss and the required diplomacy-incident continuation after loss. Interrupted
missions clear the specified self-directed SpyChat relation.

Nine new groups include actual C05 famine→C01 removal→V01 defeat, actual AI
restart/rejection, local/Hotseat retention and exact controlled escape choices.
Incident and nonzero-turn catch-up policies remain required integration services;
these fixtures do not replace them with claimed gameplay implementations. The
Alder Valley deletion adapter now invokes the real local last-city check; its
admitted rules still cannot naturally reach last-city famine. F04 end-sequence
presentation, forced retirement, civil war, full espionage and generated-game
acceptance remain pending. Original busy flags, native map batches and password
scratch encoding are not added to logical simulation state.


## Elimination integration and checkpoint on Linux, 2026-11-22

Root/App Release builds passed with zero warnings/errors and both formatting
verifiers passed. The native suite ran 641 tests: 639 passed and the same two
platform/opt-in cases were skipped. All 607 foundation groups passed, including
nine new elimination/escape groups and the actual famine-to-defeat join.
Restart rejection also preserves the creation child's conquest outcome. Identity
verification checked 405 compiled inputs, now including V06/V07 specifications.
Coverage accounts for 73 families; project boundaries, conformance metadata,
eight identity tests and three coverage tests passed.

The isolated Xvfb desktop driver passed all ten checks with CPU composition and
pinned assets/fonts, including turn-7 save/turn-8 continuation, direct/native/CLI
agreement, rejected import, replacement and normal shutdown. This is the bounded
Alder Valley regression, not complete generated-game or endgame acceptance.
Browser and additional platform work remain deferred.

At the user's request, the accumulated M4 foundation/lifecycle work is checkpointed
on `codex/m4-desktop`; subsequent coherent implementation steps receive separate
commits. The unrelated `.vscode/settings.json` remains outside this checkpoint.
M4 remains active with the outstanding dependencies recorded above.

| Receipt under `build/test-results` | SHA-256 |
| --- | --- |
| `m4-desktop-elimination-play.json` | `23e24e8f44a23d8a2df79aa9a1fbd0c1d7ef041ff39c3aa3a999977aec2e6579` |
| `m4-desktop-elimination-headless.xml` | `7f57fde63b57a25886b378872d4e21a0c1bb56a9102bef397a23a4d7f4a2dc5d` |
| `m4-desktop-elimination-native.json` | `7e699353e9c525bae1f9faf5d297d85b7d059874f83148506413d973d9f50acb` |
| `m4-desktop-elimination-identity.json` | `20e13bc6280794b1cf8efbf68d83dac71a2250f76d010317f06fa688dbf04e41` |

## Government transitions and city mood turns

`GovernmentChanges` implements V02 §§2–4 for local games: fixed advance/wonder
availability, unsigned-government rate caps, direct setters and announced changes.
A changed human government runs full C03 recalculation for every live owned city
in ascending slot order, then directly substitutes Riflemen for fixed Fanatics
production outside Fundamentalism. Production stock and census counters remain
unchanged. Incoming Vendetta clearing retains outgoing and other treaty flags.
AI announcement eligibility uses actual embassy/wonder/reveal state, and only an
eligible observer causes the associated tax formula. Human nonzero changes
require the government-selector continuation; direct positive setters require the
rate-decision continuation even for an unchanged government. These choices are
not implemented by the test boundary observers or the authored profile.

`CityMoodTurn` implements C04 disorder duration/restoration, wrapped treasury
collection, scenario-gated repeated Democracy collapse, celebration transitions
and strict food/facility-limited celebration growth. It consumes live whole mood
and yield counters, with no synthetic recalculation. Collapse calls the actual
V02 change and retains its all-city recalculation effects. First celebration
sets the flag without growth; continuing celebration leaves food stock and
workspace identity unchanged even when size wraps. Semantic notices carry the
owner and government for presentation; native buffers and popup choreography are
not simulated. C06 AI reconsideration remains a required continuation.

Alder Valley now calls this handler after its second C03 calculation, replacing
its previous mood-flag/tax shortcut. Shared tests cover the wider mechanical
branches; direct/console tests cover its admitted imported celebration, retained
duration flags, treasury and save/resume. Its Despotism-only rules and high
contentment still exclude natural disorder/Democracy collapse. General government
selection, rates, maintenance, production and generated-game turn integration
remain open, as does full desktop presentation/acceptance.


### Government and mood validation on Linux, 2026-11-22

Root and App Release builds passed with zero warnings/errors. Both solutions
passed formatting verification. Native tests reported **649 passed and two
existing skips** (native audio opt-in and Windows delete-sharing); all **617
shared groups** passed. Source identity verified **408 compiled inputs**.
Coverage accounted for 74 API/test families; graph, conformance metadata,
identity (8 cases) and coverage (3 cases) checks passed.

The isolated Xvfb CPU desktop driver passed all ten checks, including direct
client/native/CLI state agreement, native picker import, rejected-load retention,
save at turn7, resumed turn8 and shutdown. This remains the authored Alder Valley
regression, not generated-game, AI or endgame acceptance. Browser execution and
cross-platform qualification remain deferred. No Lua automation was changed.

| Local receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-desktop-mood-play.json` | `e88ebd02a2670b8dd9bc44f152bc464e99e553f1c1eeb7946295b482b6655313` |
| `m4-desktop-mood-headless.xml` | `4101d48ae1d5f729aeb14decb0d91ea7d5ce37758413f77462f2e69e55f8da9a` |
| `m4-desktop-mood-native.json` | `5414d4b9fa1a86c4aa9d246d5e551d39e6e03877ef95c71c48ef0d0042f99911` |
| `m4-desktop-mood-identity.json` | `aeebd17381e5cb38fa8c683afc6f33b6838f1846e689e255acd0e7f3824a4b27` |


## Unit production and desktop completion

`CityUnitProduction` implements C06's local unit-at-entry path and common tail.
It uses the caller's actual C05 shield factor and whole C03 shield/support values,
including disorder suppression, signed16 accumulation and the barbarian regional
military/occupant gate. Obsolete units select the first matching prerequisite and
role without availability checks or an upgrade chain. Signed unit costs are spent
before actual U01 creation; a capacity failure retains the remainder, whereas
ordinary human success clears excess stock.

Creation uses real revelation, home assignment, counters and lifetime tokens.
Veteran tests preserve the Communist diplomat exception, classic-combat branch,
facilities, active wonders, disposable units and transport city-counter encoding.
Settler cases distinguish the Chieftain refusal, last-city early return, declined
or autoplay city abandonment, ordinary size decrement and signed-size wrap.
Accepted size-one abandonment runs actual C01 removal and V01 checking, then
performs a separate U01 creation after the first unit has been removed. Tests
verify both lifetime tokens and the surviving unit's new home.

AI diplomat placement keeps C06's unusual distance comparison and does not invent
a placement-chance draw in normal state. Failed creation omits the invalid veteran
write while preserving the remaining continuation. The first unit of a type with signed attack>=99
resets incoming contact turns for normal seats. A02 production reconsideration,
C07 cargo selection and C06 AI purchasing remain required continuations. Tests
observe those boundaries and their failure prefixes; they do not implement the
missing policies. Nonunit completion, wonder acquisition/effects, the shared
completed-improvement result, switches, buying/selling and the full C06/F04 parent
remain open.

Alder Valley uses this shared production service for its supported military
orders, and its availability check now delegates to existing `UnitAvailability`.
Direct/console tests cover overflow to negative stock, successful excess-stock
removal, capacity failure with retained spent-cost remainder, and save/resume.
The authored profile still has no settler, trade, nonunit or automatic-production
policy. General generated-game integration and full desktop acceptance remain
pending; this batch does not close M4.


### Unit-production validation on Linux, 2026-11-22

Root and App Release builds passed with zero warnings/errors, and both solutions
passed formatting verification. Native tests reported **658 passed and two
existing skips** (native audio opt-in and Windows delete-sharing). All **626
shared groups** passed; source identity verified **410 compiled inputs**.
Coverage accounts for 75 API/test families. Graph, conformance metadata,
identity (8 cases) and coverage (3 cases) validators passed.

The capacity regression initially reused the founder's free slot, correctly
bypassing U01's extension limit. The final fixture removes that hole before
using the retained high military count and now verifies actual refusal without
changing allocator semantics. Both direct and console paths retain the spent
cost and resume from the saved remainder.

The isolated Xvfb CPU desktop driver passed all ten checks, including production
controls, native/direct/CLI agreement, native picker import, invalid-load retention,
save at turn7, resumed turn8 and normal shutdown. This remains authored-profile
regression evidence; generated full-game/AI acceptance is pending. Browser and
cross-platform qualification remain deferred. No Lua automation was changed.

| Local receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-desktop-unit-production-play.json` | `9dbcd37a3779e5b26745ec886c8550b3a04550195a2c12f0ee946b660ed41a5c` |
| `m4-desktop-unit-production-headless.xml` | `8cded63ee8d3f21b392e8764ba12836c0bae2ad95be60b97c6198d9f1625b6fb` |
| `m4-desktop-unit-production-native.json` | `37a168924e24c064feee134d7f54fef6ecd6010cc10ad6970dde38ec98e71da6` |
| `m4-desktop-unit-production-identity.json` | `f962dfd8f57433a1819a02d6a91f1e1c38c35c4c8900891af146aae0904f560e` |


## AI production spending and spaceship predicates

`AiProductionPurchase` replaces the unit-production purchase continuation with
C06's actual local spending phase. It retains unsigned rule costs, regional
policy inputs, the ordered wonder/ship/disorder/defense/Palace/nuclear overrides
and the fixed Nuclear Missile slot45 identity. Human wonder-group extrema are
retained in the world and refreshed before the local global advancement; the
purchase phase does not refresh their source counts or the regional census.

The reversed-bound clamp permits negative spending for excess stocks. Stock is
written before the two refund divisions, so a zero divisor leaves that write and
prevents the treasury charge and announcement. Best/worst selectors are direct
civilization indices. Almost-wonder notices use live whole shield/support values
even during disorder, have no one-time flag and require the raw Unbuilt location.
The parent clears DefenderLost only after successful return, and purchasing does
not run a second completion pass.

`SpaceshipQueries` supplies V07 capacity, raw/effective group counts, buildability,
part maxima, thrust, race and arrival predicates. It preserves negative component
capacities, the AI module cap, empty unsupported-group eligibility, uneven raw
count distinctions, destroyed Apollo, dead human seats and raw weighted progress.
Arrival tests only launched status and the saved year. These queries do not
implement performance calculation, group choice, assembly, launch or arrival
outcomes. A02 production reselection remains a required policy child; nonunit
completion and general generated-game turns remain pending.


### AI-purchase validation on Linux, 2026-11-22

Root and App Release builds passed with zero warnings/errors, and both solutions
passed formatting verification. Native tests reported **669 passed and two
existing skips** (native audio opt-in and Windows delete-sharing). All **637
shared groups** passed, including eleven new groups. Source identity verified
**414 compiled inputs**; coverage accounts for 76 API/test families. Graph,
conformance metadata, identity (8 cases) and coverage (3 cases) checks passed.

The isolated Xvfb CPU desktop driver passed all ten checks, including production
controls, native/direct/CLI agreement, native picker import, invalid-load retention,
save at turn7, resumed turn8 and normal shutdown. These desktop checks remain the
authored Alder Valley regression; they do not establish complete generated-game,
AI or endgame acceptance. Browser and cross-platform qualification remain deferred.
No Lua automation was changed.

| Local receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-desktop-purchase-play.json` | `c8b1d674a407e9850bacc1af5ba1812eefda338dbb39e2f0d2940330bd6017d9` |
| `m4-desktop-purchase-headless.xml` | `35312dd1434656ed70c78b1b8aaf2a4f662542947b5e076d17059799230f2ca2` |
| `m4-desktop-purchase-native.json` | `bf4b2641a007229d4d5788b89266f4804ae7ad6893cd265feba26f6bb921e006` |
| `m4-desktop-purchase-identity.json` | `e50d1532c99f1814f2327394c6ad649b7e403c05e5b239fd687218e4cdf1e1fc` |


## Spaceship performance, construction and launch

`SpaceshipPerformance` implements V07's three distinct flight estimates using
actual F04 calendar calls and shared within-world scratch. It preserves Fusion
retention, raw count mass, the supplement for exactly-zero kinds, support omission,
post-division paradigm scaling, signed preview years and retained launched metadata.
The specification's explanatory mass sentence was inverted relative to its numbered
procedure; it now matches the unchanged procedure, with a discriminating missing-Life-
support example. No external implementation or historical harness was used.

The capacity APIs now require selected `SpaceshipValues`. The previous checkpoint's
fixed maxima did not cover the declared custom gameplay format. Loaded custom values
now govern capacity, group maxima, per-kind maxima, assembly limits and raw/missing
mass consistently; structural-support formulas and AI module caps remain fixed.

Group choice preserves preview-only human arrival comparisons, descending group
search and the emergency component equality exception. Assembly implements human
retry/custom backed replies, AI selection ties, first-ship contact resets, signed
count writes and the AI return value3. Automatic launch preserves success thresholds,
the earliest human race, the slot0 comparison and power/gold alternatives. Local
launch mutates its first-launch marker before callbacks and uses actual V04 bilateral
Incident or directed reverse relation changes. `DiplomaticRelations` also implements
the ordered Alliance/Peace/War/Contact cascades, including contradictory flag sets.

Required A02/C06 city reselection and player choice/presentation boundaries remain
explicit. Tests observe those boundaries; they do not implement a production policy
or spaceship window. C06 nonunit completion, graphical/text spaceship reports,
launch media, F04 arrival processing, V08 scoring/outcomes and full generated desktop
acceptance remain open. This checkpoint advances their dependencies without closing M4.


### Spaceship-construction validation on Linux, 2026-11-22

Root and App Release builds passed with zero warnings/errors. Both solutions
passed formatting verification. Native tests reported **682 passed and two
existing skips** (native audio opt-in and Windows delete-sharing); all **650
shared groups** passed, including thirteen new groups. Source identity verified
**418 compiled inputs**, and coverage accounts for 77 API/test families. Graph,
conformance metadata, identity (8 cases) and coverage (3 cases) validators passed.
The gameplay-data validator passed using the existing pinned schema environment;
the classic semantic fingerprint is unchanged. V07's reviewed documentation hash
was refreshed in both the conformance registry and gameplay provenance ledger.

The isolated Xvfb CPU desktop driver passed all ten authored-profile checks:
production controls, native/direct/CLI agreement, picker import, invalid-load
retention, save at turn7, resumed turn8 and shutdown. This does not establish a
playable spaceship UI or complete generated human/AI games. Browser execution
and cross-platform qualification remain deferred. No Lua automation was changed.

| Local receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-desktop-spaceship-play.json` | `6bcb0c38ca99f0078c21142df11b060dfd70d30d0dea26588b47b28f46192aee` |
| `m4-desktop-spaceship-headless.xml` | `94d09ab0062ef453bbea02fd81df48006e34b61d353c9faa09c69c833e99f869` |
| `m4-desktop-spaceship-native.json` | `421846c374640442ade4798543ca9a20d8225825ef7dcbba028227433ba69060` |
| `m4-desktop-spaceship-identity.json` | `b6ccf733d5b1db16fd42759c61e2aff4ec9a709be5e8d5db0209f51aaa519fdb` |


## Building maintenance, wonder effects and reputation

`BuildingMaintenance` implements the C08 quote and payment pass, including
full-width Barracks costs, the disabled first-prerequisite walk, Adam Smith's
exact-one exemption and Fundamentalist religious upkeep. Disabled cycles report
a bounded stop; they do not invent a cost. The payer preserves ordered forced
loss, the captured owner, fresh post-notice credit inputs, scalar wrap and prior
effects when a child fails. It does not invoke manual sale, repair capital hints
or refund production stock.

Alder Valley calls this payer after its research phase. Its authored population,
industry and admitted facilities have no pollution-producing path. A wrapped tax
treasury can remove even its zero-upkeep Palace; the resume domain now accepts a
city with no facilities as well as Palace only. Direct and console tests run
this loss, save/reload, and another turn; other facilities remain rejected.

`WonderEffects` supplies local Eiffel acquisition and Leonardo upgrades. Eiffel
uses actual `DiplomaticAttitudes` guards and selected-pair scratch before clearing
incoming Vendetta. Leonardo scans the live unit bound, selects the last qualifying
replacement, publishes once per old type per invocation, and preserves damage,
movement, orders and counters. W03 `ApolloRevelation` updates exploration, normal
civilization feature memory and live city knowledge for all eight seats without
revealing units. These are actual child operations, with C06/V03/C09/N02 callers
still pending; no substitute research completion is introduced for Darwin.

V04 attitude storage/categories/hostility and global reputation maintenance are
also implemented. The desktop global phase now invokes reputation maintenance
after unit reset. Tests cover inactive seats, Eiffel expiry/scenario suppression,
ordinary endpoint1, Eiffel endpoint0, unsigned difficulty and human-only credit
normalization. Local effect services reject network execution before mutation;
network publication, browser execution and cross-platform qualification remain
deferred. General F04 scheduling and complete generated human/AI games remain
open, so this batch does not complete M4.


### Building-effects validation on Linux, 2026-11-22

Root and App Release builds passed with zero warnings/errors, and both solutions
passed formatting verification. Native tests reported **695 passed and two
existing skips** (native audio opt-in and Windows delete-sharing). All **663
shared groups** passed, including thirteen new groups. Source identity verified
**423 compiled inputs**. Coverage accounts for 78 API/test families. Graph,
conformance metadata, identity (8 cases), coverage (3 cases) and gameplay-data
validators passed; the classic semantic fingerprint remains unchanged.

The isolated Xvfb CPU desktop driver passed all ten authored-profile checks,
including native/direct/CLI agreement, save at turn7, resumed turn8, picker
import, invalid-load retention, session replacement and shutdown. This proves the
bounded desktop regression; full generated games and the remaining wonder callers
are not covered by that driver. Browser execution and cross-platform qualification
remain deferred. No Lua automation changed.

| Local receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-desktop-wonder-play.json` | `0249325f4dfb1a73212010b81ab68651fdd8009bf7e69ac71f24f355915c6c35` |
| `m4-desktop-wonder-headless.xml` | `d1e213d581f073c6f34c84c3eddef237448a598e44bf6bb636b81f105328a4f3` |
| `m4-desktop-wonder-native.json` | `1927a236ca59aaa7621a2bdbfe4de4cd6c832918826c8fa0f082f7e85c53f8cf` |
| `m4-desktop-wonder-identity.json` | `9ca94cd8a1090615c2d778fb437f1c98121815f2fa3cf03a25f4948b31a58a60` |


## Production snapshots, setting orders and AI wonder changes

`ProductionOrders` implements C06 categories, the shared saved-category/stock
snapshot, switch penalties and build-turn forecasts. Classification uses the
full requested item before the signed production store. Penalty multiplication
uses unsigned low16 stock and preserves the full saved stock in the subtraction;
previews and assignments do not refresh the snapshot. Unit forecasts use signed
costs, while improvement forecasts use unsigned costs and C05's shared shield
factor.

The setter owns per-type counter changes around the mutating A02 chooser,
including automatic sentinels, captured-owner behavior and retained failure
prefixes. Its AI continuation implements first-wonder stock halving, retained
builder counts, direct redirects from an already claimed wonder, Started and
Abandoned state, real C08 active ownership and the zero-stock group bonus.
Civilization reset honors hotseat suppression, low-byte owner matching, regional
filtering and a live city upper bound. The state belongs to the world and resets
with a new world. Network execution is explicitly unsupported here.

Alder Valley's production command now starts its atomic selection transaction,
saves current build state and uses the shared setter. Its two admitted unit
choices at Chieftain preserve stock across repeated switches. The desktop city
pass publishes the civilization-turn phase input. Direct/console regression
cases inspect stock and counts through repeated commands, then completion and
save/resume. A shared integration case runs actual C06 order changes and AI
purchasing around a controlled chooser reply. This does not implement A02's
choice policy or the complete P06 transaction; those and the V03 production
reset callers remain pending alongside nonunit completion and general turns.

A separate eligibility binding gap remains: C06 names scenario inputs
`NoNewWonders` and `AllowManhattan`, but does not map them to S01/F06's declared
scenario option fields. S01 mentions wonder and Manhattan consumers under other
option names without explicitly equating those inputs. Do not guess their stored
bits or aliases. Resolve this mapping from the behavioral contract or independent
original-game observations before claiming scenario wonder availability. This
batch does not implement or depend on that predicate.


### Production-order validation on Linux, 2026-11-22

Root and App Release builds passed with zero warnings/errors; both solutions
passed formatting verification. Native tests reported **707 passed and two
existing skips** (native audio opt-in and Windows delete-sharing). All **675
shared groups** passed, including twelve new groups. Source identity verified
**425 compiled inputs**, and coverage accounts for 79 API/test families. Graph,
conformance metadata, identity (8 cases) and coverage (3 cases) validators passed.
No gameplay data or specification bindings changed.

The isolated Xvfb CPU desktop driver passed all ten authored-profile checks,
including production controls, native/direct/CLI agreement, save at turn7,
resumed turn8, picker import, invalid-load retention, session replacement and
shutdown. This covers the admitted production commands, not complete A02 policy,
nonunit completion or generated-game acceptance. Browser execution and
cross-platform qualification remain deferred. No Lua automation changed.

| Local receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-desktop-orders-play.json` | `0b65ed39499036fa8e7660d12890d1ad6d6935ad457b0f68cef435e9162a995b` |
| `m4-desktop-orders-headless.xml` | `930b64dc68f6a8158f992eab2e55b014badfc936db381dc3d2c944c91d97cc63` |
| `m4-desktop-orders-native.json` | `2fc695322ca111981717e1b04539e395b591b3869888fcbff6bfe315fcf63c97` |
| `m4-desktop-orders-identity.json` | `db032c656484138978f4ba20549fa08e551f54c72f513ac1095ad8f33e7e3c85` |

## Research costs, choice options and local advance grants

`Research.Cost` now implements V03's full human/AI/scenario formula. It retains
the ordinary and research-cost counters, rank-as-slot leader comparison, scenario
calendar bypass, signed rule count/paradigm, separate map scaling steps and human
floor. `LocalResearch.Cost` delegates to it. `Research.Options` supplies the
ordered first-option/modulo filters with forced/scenario exemptions; the authored
desktop research-choice list now uses it while excluding unsupported Future
Technology from that profile.

`ResearchGrants` implements the local advance-grant sequence with actual C08
storage, active ownership and Leonardo operations. Turn-zero storage is shared
with the start caller. Nonzero grants preserve duplicate/future counter wrapping,
source replacement, first-discoverer bitwise OR, acquisition/emissary gates and
secondary draw timing, Barracks liquidation using raw cosmic rows, Philosophy's
free-advance flag and weighted last-tie Golden Age city, raw-location wonder
expiry notices and recursive Great Library grants. The Library counts inactive
knowledge holders. Disabled prerequisite cycles stop explicitly after the valid
grant prefix rather than inventing a completed walk.

`ResearchGovernmentPrompts` owns V03's government/tutorial decisions and calls
actual V02 direct Anarchy transitions on reply1. It preserves the Republic
tutorial return, government-specific early exits and the caller-owned human/
local/autoplay gates. Presentation and accepted player decisions remain explicit
interfaces; the emissary reset is still supplied by its diplomacy owner.

Alder Valley's ordinary science threshold now invokes this shared grant service
instead of directly changing the technology bits/counts. Direct/console cases
load a retained counter255 and first-discoverer4, complete research and verify
counter0 and first-discoverer5, then save/reload and continue. The profile keeps
its existing ordinary completion and session selection boundary. Its declared
advances0..2 do not reach grant dialogs, government decisions or Barracks sales;
the adapter rejects an unexpected presentation path.

The complete research-selection/advice/goal interface, general completion and
free-advance accumulation, nonzero V01 catch-up caller, Darwin's two completions,
full A02 production reset and general F04/generated-game acceptance remain open.
These shared grants do not by themselves close those parent operations or M4.
Network authority/publication, browser execution and cross-platform qualification
remain deferred.

Validation on Linux: the root and App Release builds succeeded with no warnings
or errors; root/App formatting and formatting verification passed. The native
suite passed **722 tests**, with the existing audio-output and Windows
handle-sharing skips (724 total). All **690 shared groups** passed, with **428
compiled inputs** verified. Graph, conformance, 80-family coverage, eight identity
tests, three coverage-tool tests and gameplay-schema checks passed.

The Xvfb CPU desktop driver passed all ten checks, including pending research,
GUI/console replies, multi-turn play, save/resume, direct/CLI/native agreement,
file-picker import, session replacement and shutdown. These are implementation
regressions for the admitted authored profile, not full-game compatibility
acceptance. No Lua automation changed; browser and cross-platform execution
remain deferred.

Receipts under `build/test-results/` (generated evidence, not committed):

| Receipt | SHA-256 |
| --- | --- |
| `m4-desktop-research-play.json` | `c60c88f933005a5cf2acda4092d442f2194a82e932b215d84b52e79a6bab13b1` |
| `m4-desktop-research-headless.xml` | `e673af3166a6d5d3c91478d02891e040d741dac12e36cc77ba8f3c67a933d80b` |
| `m4-desktop-research-native.json` | `8f580fd83a4e57bcf55ac803d7506996c94263c41817e98b393a479640c20862` |
| `m4-desktop-research-identity.json` | `5fcb95207eb3b51071bcdedff2c32cbcbf560511e250ab8a004778a21fdf7e63` |


## Resumable research completion and free advances

`ResearchSelection` implements V03's single retained recommendation, human/AI/
forced/autoplay gates and accepted choice, including Help and goal rebuilds
without another recommendation draw. It yields an owned request and resumes with
an admitted reply. `ResearchGoals` provides advice with the five-item ordered
limit, category lists/help and research/theft paths, preserving the first-candidate
exemption and full exclude-current arithmetic. It changes no research goal or
knowledge. These are semantic presentation interfaces; native dialog timing,
layout and localized list formatting stay with presentation.

`ResearchFlow` now implements completion and positive science accumulation using
actual shared grants, C06 production reset, C03 cached mood queries and V02
research prompts. It preserves mode2 progress, captured completion identity,
post-grant Future Technology labels, ability notices, and the final research
choice before government prompts. Philosophy can trigger a second completion in
the same accumulation. The free flag is cleared before that completion, even
below the ordinary threshold; an initially absent target only requests a choice.
Signed progress overflow and nonpositive-point early exits remain unchanged.
Failed operations cannot resume and replay their committed grant prefix.

The desktop city pass now suspends inside this flow. Accepted research resumes
before C08 maintenance and `turn.advanced`; the old partial accumulation and
end-of-upkeep choice are removed. Deferred callbacks borrow only the current
resume context. The production phase is retained while awaiting research and
released on completion/disposal. Direct research commands admit the actual
option list. A direct/console test combines counter wrap with a treasury overflow:
the research request sees the negative treasury, and accepting it precedes the
forced Palace sale and turn completion. Save/reload and another turn still run.

The authored desktop profile continues to admit its three ordinary advances.
Shared tests exercise the broader completion/choice/goal mechanics; the desktop
adapter does not yet present general encyclopedia, goal, emissary, government or
wonder dialogs. A02's full production policy, V01 catch-up, Darwin's acquisition
caller, full F04 and generated-game acceptance remain open. This checkpoint does
not close M4. Browser and cross-platform execution remain deferred; no Lua
changes are included.

Validation on Linux: root and App formatting/verification and Release builds
passed, with zero build warnings/errors. The native suite passed **731 tests**
with the two existing audio-output/Windows handle-sharing skips (733 total).
All **699 shared groups** passed; identity verification covered **431 compiled
inputs**. Graph, conformance, **81-family** coverage, eight identity-tool tests,
three coverage-tool tests and gameplay-schema validation passed.

All ten Xvfb CPU desktop checks passed with the new mid-upkeep request boundary,
including GUI/console replies, saved turn7/resumed turn8, direct/CLI/native
agreement, file-picker import, replacement and normal shutdown. This verifies
the admitted desktop profile; it is not evidence of a complete generated game.

Receipts under `build/test-results/` (generated evidence, not committed):

| Receipt | SHA-256 |
| --- | --- |
| `m4-desktop-flow-play.json` | `4d85bc154605d2305034c3a60c17dee423ef6f2544e97d4275166dabf1153750` |
| `m4-desktop-flow-headless.xml` | `08be0d5181761eae61aa84b9995aa69c89bbf76c32c411e42947e00fa9f4fe05` |
| `m4-desktop-flow-native.json` | `38fb8bcdb16c1c95d15a6ec3b75da97dd05510d74d2a7681ff554e475775daf9` |
| `m4-desktop-flow-identity.json` | `111e5c5011269beacacafa9746be0188f8749ea542378457dbe60810991c7655` |


## Building and wonder production parent

`CityProduction` now dispatches the actual unit child or the C06 nonunit branch.
It retains owner/net/item/cost, resets the completed-improvement publication,
handles Capitalization without stock accumulation, warns on invalid builds
without changing orders, and preserves signed stock arithmetic. Human spaceship
launch/max gates precede readiness; AI spaceship choice uses actual V07 group
selection, recomputes the new group's cost and never builds the old group as a
fallback. Normal exits execute the common automation/AI purchase tail once.

Wonder races use raw locations and the human threshold's raw cosmic shield rows.
The AI early return keeps accumulated stock and DefenderLost. Local acquisition
publishes location/WonderBuilt before completion effects. Ordinary AI facilities
retain surplus; manual human completions discard surplus but keep their order.
Palace relocation clears other owned Palaces and publishes only capital-x.
Completion notices preserve preference/autoplay/local gates, sound-before-notice,
wording identities and movie order. Failed operations cannot replay their prefix.

The parent invokes actual spaceship assembly, Eiffel diplomacy, Leonardo upgrades
and Apollo publication. Darwin invokes two actual `ResearchFlow.Complete` calls,
sequentially, allowing research choices between them. The second uses the first
call's selected target. AI cities building the completed wonder are reconsidered
only after these children. Presentation and A02 chooser inputs remain explicit;
tests supplying a production choice do not implement that policy.

The desktop city pass now suspends production as well as science accumulation.
Its ordinary profile offers Temple after Land Surveys and Library after Clay
Casting, with authored costs/upkeep and real C03/C04/C08 consequences. Production
projection handles signed building items; load admission preserves Palace/Temple/
Library combinations and their retained manual orders. Direct/console cases
complete both facilities, save/reload, and verify duplicate-build warnings without
another completion or cost deduction. Marketplace remains outside this profile.

Darwin's fixed mapping is now explicit in C06 Appendix A: wonder18/improvement57.
It was checked only against the original-game data conversion in the authorized
local `codex/classic-menus-civilopedia` branch,
`data/gameplay/classic-mge-5.4.0f/rules.json` (SHA-256
`163cae26da033a8418342bac81e33c9d586edcafedc4549d0935ed02d4e209be`).
The ordered C08 catalog agrees. No external implementation or historical harness
was consulted. The current C06 conformance pin was updated for this definition.

General player buying/selling/selection, full build eligibility (including the
recorded scenario-option binding gap), full A02 policy, V01 catch-up, all-city/
civilization F04 integration and generated-game acceptance remain open. This
local parent does not implement network wonder acquisition. General desktop
wonder/spaceship presentation remains open. Browser execution and cross-platform
qualification remain deferred; no Lua automation changed. M4 remains in progress.

Validation for this batch: root and App formatting plus verification passed;
Release builds completed with zero warnings/errors. Native tests passed
**740 of 742**, with only the two existing platform/opt-in skips. The shared
native probe passed **708 groups**, and identity verification covered **433
compiled inputs**. Graph, conformance, **82-family** coverage, eight identity-tool
tests, three coverage-tool tests and gameplay-schema validation passed.

All **11 Xvfb CPU desktop checks** passed, including building a Temple through
city controls, save/reload, retained production and duplicate-build handling.
The existing saved turn7/resumed turn8, direct/CLI/native agreement, file-picker
import, replacement and normal-shutdown checks also passed. This validates the
admitted desktop profile, not the full generated-game M4 exit gate.

Receipts under `build/test-results/` (generated evidence, not committed):

| Receipt | SHA-256 |
| --- | --- |
| `m4-desktop-nonunit-play.json` | `cb8c01e8f39197c968edc64e6bfcfffcf735ff61f2af232da04a9fcfee48f8b1` |
| `m4-desktop-nonunit-headless.xml` | `963f536089baa8115e92e771e3a9d249afa1c7439f6d0b3cbb892e2f0042679f` |
| `m4-desktop-nonunit-native.json` | `64abea758da793adec9f66167abc0d0c75ba2d301676154d10b3f2ea76f0b116` |
| `m4-desktop-nonunit-identity.json` | `ae4c35269bfcf95da326fc3c38419c35e866a8734f7e36a442ec457871ba52aa` |


## Player purchases and facility sales

`PlayerProduction` implements C06 local purchases and sales with actual C03
calculation and C06 snapshots. The purchase quote retains owner, item, signed
unit/unsigned improvement cost, price and affordability across the confirmation.
It uses raw shield-box rows, the required price families, zero-stock doubling and
remaining-stock clamp. Disorder blocks unit buying but permits nonunit buying;
Capitalization has no purchase. Confirmation pays once, fills signed shield stock,
refreshes the saved category/stock, then fully recalculates. It creates no unit or
building until the normal production pass.

Sales use semantic owned-facility identities. Palace and repeat sales are denied
except with both reveal-map and scenario mode. Confirmation clears the building,
credits its retained price, sets ImprovementSold and refreshes actual city values.
Signed treasury wrap remains defined. Both operations retain city tokens rather
than emulating incidental UI selection changes, and become terminal before effects
so a failed notification cannot duplicate payment.

The desktop exposes `city.buy` and `city.sell` with correlated decisions shared by
GUI and console. The city window lists owned facilities and enables available
sales. Cancel/decline preserves stock, facilities and treasury; an unaffordable
quote offers no affirmative choice. F04 city entry clears ImprovementSold, while
classic save/load retains it within the turn. The direct/console integration case
compares complete saves after purchase, sale, reload, turn completion and another
sale. The native driver adds the actual controls and save-before-completion path.

General production-change/advice dialogs, full eligibility, A02 policy and the
remaining generated-game/civilization/turn integration are still open. Local
transactions do not implement N02 busy/deferred execution. Browser execution and
cross-platform qualification remain deferred. No Lua automation changed; M4
remains in progress.

Validation: root/App formatting and verification passed; both Release builds
finished with zero warnings/errors. Native tests passed **745 of 747**, with the
two existing platform/opt-in skips. The shared probe passed **713 groups**;
identity verification covered **435 compiled inputs**. Graph, conformance,
**83-family** coverage, identity/coverage tool tests, content schemas and all six
gameplay component schemas passed.

All **12 Xvfb CPU desktop checks** passed, including native sale/purchase
confirmations, decline/cancel, exact quoted prices, unchanged unit count at
purchase, save/reload and actual production during the next city turn. Existing
turn7/turn8 direct/CLI/native agreement, imports and normal shutdown also passed.
These checks cover the admitted desktop profile; the full M4 generated-game
exit gate remains open.

Receipts under `build/test-results/` (generated evidence, not committed):

| Receipt | SHA-256 |
| --- | --- |
| `m4-desktop-transactions-play.json` | `6b866358c3bb9c180a995174e18529a7a19c9ed7410a19a19c07a71e13540280` |
| `m4-desktop-transactions-headless.xml` | `d06ca8263c1dcd79b87e1a578b639c29319a35dc07d730362701469903107cbb` |
| `m4-desktop-transactions-native.json` | `01dfa106fa504369ca96d46526bf828666ea697fca599049d5d18a7796b6a390` |
| `m4-desktop-transactions-identity.json` | `f9f94ae4a25607c954a41718d2b01e428e308526a21004796f095f941fe0268f` |


## Shared eligibility and ordinary production changes

`BuildingAvailability` completes C06's nonunit predicates using actual C08
facility/effect queries and civilization knowledge. It covers prerequisite
chains and their prerequisite-knowledge checks, water access, wonder substitutes,
plant replacement, Hydro geography and spaceship gates. City-less queries skip
all city-specific gates after the initial prerequisite. Destroyed Apollo permits
ship choices, destroyed wonders cannot be rebuilt, and expiration alone does not
remove an unbuilt wonder. Ship capacity is deliberately not an eligibility gate.

The NoNewWonders/AllowManhattan semantic inputs are explicit for scenario queries.
The repository does not define their original-file bit mapping; an unrevealed
scenario query without those inputs stops with that specification gap. Non-scenario
and reveal/editor queries require no invented flags. This does not close S01/F06
binding work. Coastal Fortress identity28 follows C08's declared catalog order.

`ProductionOrders.Options` supplies ascending unit, improvement and wonder choices
with the shared saved-stock penalty, turn forecast and wonder-expiration marker.
`PlayerProduction.BeginChange` implements the ordinary Choose continuation:
clear all automation flags, retain the proposed stock, request a penalty decision
when applicable, update unit counters and production directly on acceptance,
then recalculate on either acceptance or rejection. It does not refresh the
opening snapshot or invoke AI wonder-change behavior. Repeated choices can return
to the original snapshot category/stock. Terminal operations cannot repeat counts.

The authored desktop command now calls this direct change operation and the real
building predicate. Each atomic Chieftain command starts its own selection
snapshot; the general persistent city dialog remains open work. Shared tests
exercise penalty decisions at other difficulties and negative retained stocks.
The direct/console stock/save scenarios import automation flags and confirm that
manual choices clear them before real turn completion.

Full A02 policy, optional advice, automatic/encyclopedia/cheat dialog actions,
general desktop production-list presentation, network busy/deferred work and
full generated-game F04 callers remain open. Browser/cross-platform qualification
is deferred. M4 remains in progress.

Validation: root/App formatting and verification passed, and both Release builds
completed with zero warnings/errors. Native tests passed **752 of 754**, with the
two existing platform/opt-in skips. The native shared probe passed **720 groups**
and identity verification covered **437 compiled inputs**. Graph, conformance,
**84-family** coverage, identity/coverage tool tests and gameplay schemas passed.
All **12 Xvfb CPU desktop checks** passed through the changed manual-production
path, including direct/console/native/CLI agreement, building completion,
purchase/sale confirmation, save/reload and next-turn completion. Full M4
human/AI generated-game acceptance remains open.

Receipts under `build/test-results/` (generated evidence, not committed):

| Receipt | SHA-256 |
| --- | --- |
| `m4-desktop-choices-play.json` | `14127939a9af47cc9e150ac25dde2204335903d87f06141eed297f8003cb2715` |
| `m4-desktop-choices-headless.xml` | `a9f5bca3e2f587fc075573225d324c2bc744fd024eaff9b79a8d657731a73ebe` |
| `m4-desktop-choices-native.json` | `6f82ff1a3277c875847a790d3a07db048337d67e80ef9bb96f5b2ff1992dba85` |
| `m4-desktop-choices-identity.json` | `aa38d7237dd9e728f6f13b95be20cb0b9aa96cb04722e07e177e3e9478074606` |


## Ordered city production policy and desktop automatic turns

`CityProductionChoice` implements A02's ordered survey, facility/wonder scoring,
special and ordinary unit scoring, human CITYPREF selection and final automation
policy. Optional recommendations retain their own IDs/scores before final Palace,
Temple or current-wonder substitutions. Raw wonder preference can change both IDs
while preserving a previous weighted minimum; tests distinguish that from sorting
weighted scores. Census, leader traits, regional policies and human wonder ranges
remain the live/retained inputs owned by A01/F04.

The policy calls actual C03 cached calculation, C06 eligibility, C08 facilities,
V07 part-group/race queries, A03 tile-improvement recommendation and U05 AI airlift.
The latter preserves orders, moves/exhausts the unit, checks directed human fighter
war and consumes one semantic secondary result per reachable fighter, then uses
actual basic deletion on interception. U02 spatial-control queries are shared with
ordinary land movement. Free irrigation, sales and donor transfer precede scoring;
the chooser preserves those effects when a later arithmetic/backing read fails.

A02's owner now documents narrow X01 repairs: skip the nonexistent rule62 role
block and treaty8 adjustment, and use zero rivalry outside the wonder table.
Backed inactive city sizes remain retained inputs; an unbacked cursor stops at its
read. These repairs are declared contract behavior, not recovered original bytes.
Additional fixed unit/advance identities use only the user-authorized original
`data/gameplay/classic-mge-5.4.0f/rules.json` data on local branch
`codex/classic-menus-civilopedia`, SHA-256
`163cae26da033a8418342bac81e33c9d586edcafedc4549d0935ed02d4e209be`.

Desktop production, research reset and city-mood callbacks now compose actual A02.
An imported unit-only Autobuild city completes units and continues after save/load;
direct and console requests yield identical saves. The 20 added shared groups
cover recommendation/return differences, cutoff500, negative scores, signed roles,
naval borders, diplomat targets, redundant sales, free-irrigation visibility,
first-donor termination, intercepted transfers, invalid survey writes, retained
size reads and failure prefixes. These are authored specification tests, not
original-game capture evidence.

Full A01 strategy/census, A03 worker tasks, other U05 movement, general desktop
advice/automation controls and generated-game F04 callers remain open. Browser and
cross-platform execution are deferred. M4's complete human/AI-game gate is still
open; this batch does not close the milestone.

Validation: root/App formatting and verification passed; both Release builds
completed with zero warnings/errors. Native tests passed **772 of 774**, with the
two existing opt-in/platform skips. The native probe passed **740 shared groups**;
source identity verification covered **448 compiled inputs**, now including the
A03/U05 owner documents. Graph, conformance, **85-family** coverage,
identity/coverage tool tests, content schemas and gameplay schemas passed.

All **12 Xvfb CPU desktop checks** passed, including manual production, purchases,
sales, building completion, save/reload, turn7/turn8 direct/native/CLI agreement,
imports and normal shutdown. Automatic production's new execution evidence is
the direct/console save/resume group; the graphical driver does not yet exercise
an automation control. Full generated-game desktop acceptance remains open.

Receipts under `build/test-results/` (generated evidence, not committed):

| Receipt | SHA-256 |
| --- | --- |
| `m4-desktop-policy-play.json` | `f3971cf694235bd25e04405cb2f462f97f43ee3dd39a533e2a552cbd26afd6ff` |
| `m4-desktop-policy-headless.xml` | `afeae214b443765b77340c977ed1106b8c2785aead51979fc5d4f9caad1c8081` |
| `m4-desktop-policy-native.json` | `c68369210952e767be103523b1628da0642c7719aec38bb05e62c99675f28aad` |
| `m4-desktop-policy-identity.json` | `98aea84c9c95617643bfd25211904c4234af39230d6a4fb284f00d68eebb31ae` |

## Civilization assessment, power and government policy

`CivilizationAssessment` now runs A01's ordered staging, census, regional policy,
reverse unit assignment and goal aging. It retains old regional city counts until
the city pass, uses retained rival summaries, double-counts allied presence as
specified, and invokes actual C06/A02 production reset when an AI region changes.
Assignment composes actual A08/V02 government change, C05 support and U01/V01
loss/elimination/recreation. In-place upgrades leave the prior census intact;
unit deletion retains its own count updates. Historical routes, air goal ranges,
anti-submarine role changes, fortress rules and ferry flags keep their specified
ordering. The two outer AI movement passes and U04 execution remain pending.

`PowerRanking` publishes old census power/history, inverse ranks and directed
coordination. `CityObjectives` supplies V08's raw objective/wonder value.
`GovernmentPolicy` implements A08 post-city rates/strategic scores and government
selection, including rank-as-civilization input, unsigned rate narrowing, later
ties and conditional draws. Complete F04 city-score accumulation, government and
diplomacy maintenance still need their general turn caller.

The desktop turn now publishes power before advancing the global turn and runs
human assessment after city processing. A direct/console case imports retained
census values, verifies old power then refreshed counts, saves/reloads and compares
continued bytes. The 18 new shared groups also cover actual policy reselection,
elimination followed by government change, paratrooper relocation without terrain
reveal, regional threat, failed quota/range reads and retained mutation prefixes.
These are specification-derived cases, not original-game comparison evidence.

A01 now resolves the staging argument against U01's synchronization contract and
records narrow X01 repairs: omit negative history, invalid inverse-rank and census
writes, while stopping at subsequently required unbacked reads. Laser41 and Armor22
use the same user-authorized original rules-data mapping recorded above.

The complete generated human/AI game, remaining A03–A08 tactical/diplomacy policies,
full turn scheduling and M4 exit gate remain open. Browser and cross-platform
execution remain deferred; no Lua automation changed.

Validation: root/App formatting and verification passed; both Release builds
completed with zero warnings/errors. Native tests passed **790 of 792**, with two
existing opt-in/platform skips. The probe passed **758 shared groups**, and source
identity verified **456 compiled inputs**. Graph, conformance, **86-family** coverage,
identity/coverage tool tests, gameplay and content-schema checks all passed.
All **12 isolated Xvfb CPU desktop checks** passed, including real controls,
research continuations, production/purchases/sales and save/reload comparisons.

Receipts under `build/test-results/` (generated evidence, not committed):

| Receipt | SHA-256 |
| --- | --- |
| `m4-desktop-assessment-play.json` | `4def926274cfa29023197b156299e57de1503956fa5500e1e5086bea6e8f4b7e` |
| `m4-desktop-assessment-headless.xml` | `a91f00c39c9982d24b3d7f32ecb0eb9ffc6c0a5db75b1eae7f8064c17dcac6c0` |
| `m4-desktop-assessment-native.json` | `fc99e26d95bf9269637d9cdbd030d516f8d61c9552bdf484a07612820bc82ea8` |
| `m4-desktop-assessment-identity.json` | `8e5c7c12773d7695aabc3469899b7f54b16a1f6142415411daa77c6877663159` |


## Terrain work execution and desktop queued orders

`TerrainWorkOrders` implements all seven W04 commands and executions. Command
checks remain distinct from direct work: conversion bypasses city/water checks,
queued work does not repeat player admission, and changing orders retains progress.
The executor exhausts the actor, takes only the first matching donor (including
zero progress and retained mixed ownership), wraps counters and compares signed
progress, then clears matching workers on completion. Actual W01/W03 mutations
and memory precede the display tail; an eligible near-city selection runs actual
C03 calculation. It performs no invented region, fertility or global-memory repair.

Fortress/airbase conversion, road-to-rail choice, irrigation/farmland/mining,
destination cleanup and pollution counts retain their declared order and widths.
A negative Transform result preserves W01's terrain-kind/preserved-flag effects,
then fails at the unsupported destination-rule read. Fixed-cost cleanup does not
read an unrelated terrain rule. Radio uses fixed advance66 from W04's required
identity mapping and the authorized original rules data.

`UnitFortification` now implements U04's real operation and U01's AI home update,
including home slots0–127, foreign on-tile cities, fortress-only nearest-city
fallback, size threshold and old/new refresh order. The desktop's former global
reset shortcut has been removed: fortification executes when activation reaches
the unit, so a nearer manual unit may be active first.

The desktop publishes player-scoped work options, accepts `unit.work`, displays
a terrain-work selector/menu actions, and resumes jobs through activation. Its
Pioneer remains in the authored pre-founding domain. Work features/progress survive
classic load; unsupported queued Transform remains rejected by this profile.
The direct/console scenario saves unfinished irrigation, reloads, finishes over
successive turns and compares full bytes. The native driver exercises the selector,
file import, queued progress and completed bytes against the same direct reference.
It starts a fresh desktop process with the reference's load history, making the
regenerated entity tokens and normalized load fields comparable without masking
any save bytes. Work menu actions have stable identities from startup, with
availability controlling visibility as the selected unit changes.

Eleven new shared groups cover work gates, thresholds, signed/wrapped/custom rules,
donor order, non-Settle direct execution, conversion failures, actual C03 refresh,
fortification home gates and desktop continuation. These are specification-derived
checks, not original-game comparison evidence. A03 worker policy, W04 pillaging,
remaining U04 movement/recovery/AI execution and generated-game turns remain open.
Browser and cross-platform work stay deferred; no Lua automation changed. M4 is
still in progress.

Validation: root/App formatting and verification passed; both Release builds
completed with zero warnings/errors. Native tests passed **801 of 803**, with two
existing opt-in/platform skips. The probe passed **769 shared groups**, and source
identity verified **460 compiled inputs**. Graph, conformance, **87-family** coverage,
identity/coverage tool tests, gameplay and content-schema checks all passed.
All **13 isolated Xvfb CPU desktop checks** passed, including queued irrigation,
unfinished/completed save bytes and normal shutdown of both desktop processes.
After final formatting, foundation and desktop receipts still matched the current
compiled sources. These authored-profile checks do not close the full M4 gate.

Receipts under `build/test-results/` (generated evidence, not committed):

| Receipt | SHA-256 |
| --- | --- |
| `m4-desktop-work-play.json` | `2ca929150325a1d45d10c2092c4eb49a8563fc818be9ae2166c9d6fb97ac5156` |
| `m4-desktop-work-headless.xml` | `d7d3019c84191aaeac2ee1154c6227ac54b551dd265a59df0c66c1ab9832b017` |
| `m4-desktop-work-native.json` | `97b258c31c5d62e430ea0645207c5662d9119c66b5cbbf3038fef6506df72fe9` |
| `m4-desktop-work-identity.json` | `ac988d70a355b34289998ee7d46e17418e6daa0f5a9a70c42ec3c696fa289f2b` |

## City terrain goals and analysis during upkeep

`CityTerrainGoals` publishes pollution jobs and retained C02 suggestions using
A01 goal insertion. Its road planner follows the U03 uniform route, checks
bridge and railroad knowledge, and tries owned cities before cities admitted by
directed peace or alliance. A rejected first job ends that route. The retained
suggestion and path workspace follow the individual search outcomes.

Alder Valley now enables job collection during the second city calculation and
publishes terrain goals after research and upkeep. The city pass clears the
defined government scores, applies the anarchy contribution and A08 strategic
adjustments, then runs A01 assessment. A03 tests cover the calculation join,
directed treaty and job gates, priority retention, and polluted city
continuation through direct and console commands.

The F04 specification records two open arithmetic questions for the support
and unhappiness score contributions and the best-shield summary. General
worker movement, complete city and civilization upkeep, and generated human/AI
play remain open. Browser and cross-platform work remain deferred.

Root and App formatting verification and Release builds passed. The native
suite passed **809 of 811 tests**, with two existing skips. The probe passed
**777 shared cases** and verified **462 compiled inputs**. Repository graph,
conformance, coverage, identity, gameplay data and schema checks passed.
All **14 Linux desktop checks** passed, including civilization record equality
with the direct client after the polluted city turn and its save/reload continuation.

Receipts under `build/test-results/` (generated evidence):

| Receipt | SHA-256 |
| --- | --- |
| `m4-desktop-cityjobs-play.json` | `2b8273595e9e6f44ba5ead69b15a8ef33fc937a8a584b4c4bd6b04983e23c143` |
| `m4-desktop-cityjobs-headless.xml` | `38ced4c391776d1c5030654638004038c4b261b6bee0bef8f66c2d41c9843a58` |
| `m4-desktop-cityjobs-native.json` | `beacac2381e8641e59cc5e35808e48dc78b29655d0922e00d429341c13f18c78` |
| `m4-desktop-cityjobs-identity.json` | `b48daa693963e2827a9cdd848bc30a3d25737169a5c92d8ae5646cb6889ac2c9` |

## Production completion feed for automation

The authored local game publishes a unit or improvement completion after its
production operation settles. The notification includes the city token, signed
production item, observation revision and turn command correlation. It shares
the participant scope of the command and checks live city ownership before
delivery. New and replacement bindings begin with an empty queue. Each
automation binding retains up to 32 events and reports how many older events
were dropped. Reading one queue leaves other bindings' queues intact.

The integration case reads the event, queries the current city's choices, and
submits the next build with the ordinary production command. The same choices
through the frontend and automation bindings produce identical saves. Lua
consumption, the desktop task lifecycle, and replay of script commands are
separate remaining work.

Root and App formatting checks passed. The root, App and optional automation
Release builds passed. The native suite passed **810 of 812 tests**, with two
existing skips, and the optional Lua suite passed all **13 existing tests**.
The probe passed **778 shared cases** and verified **463 compiled inputs**.
Repository checks and all **14 Linux desktop regression checks** passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-desktop-productionevents-play.json` | `bd2bcbb4c95fe8ee017adcdf7bd4b229bdbdc0d0c07993c1b58f38ea211d19e3` |
| `m4-desktop-productionevents-headless.xml` | `12ed7499c8bb9054938c1dc7917791bd254a0c6835e458c5b2faada15a998089` |
| `m4-desktop-productionevents-native.json` | `09fffee67ac04f9d657825fa5378cfe5facacbe53d0125430d2bca3bb6079abc` |
| `m4-desktop-productionevents-identity.json` | `79d728ed7603411d72260af840dfb06d2bb8e72cd1d8de92f9ae2165ab20d34a` |


## Lua production policy and request replay

The authored game's Lua binding exposes own-city completion events and current
production choices by city token. The example waits for one completion, chooses
a different available item, submits the ordinary production command and waits
for its result. Paused tasks keep their tick budget and can resume or cancel.
The task retains parsed requests and admission receipts in memory. The
integration case compares the script, manual command and recorded request
replay on equivalent loaded games. All three produce identical save bytes.

Root and optional automation formatting and Release builds passed. Native
headless tests passed 810 of 812 with two existing skips. All 14 shared
automation groups passed on native and Chromium, and the evidence comparison
passed. The local browser test bundle needed notice generation with the pinned
runtime archive from the NuGet cache because the installed SDK did not contain
it at the notice tool's lookup path. This run verifies the shared Lua tests.
Desktop task attachment, task memory accounting, and replay of interaction
inputs remain open.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-desktop-lua-production-native.json` | `bd476eef46351cff65f7d4de2185eba4826b179064b8756281d81eb90c1f1c2e` |
| `m4-desktop-lua-production-browser.json` | `4d2ddb69f5354148bcf802f650c79ae5f0a3e4c4777df21a930429fd64b3769b` |
| `m4-desktop-lua-production-headless.xml` | `4664235b9223ed4e435ed7193d452d4994e31715b4939e100e292105c48a0bee` |
| `m4-desktop-lua-production-root.xml` | `95d83f92abae1d5abc78cb6023afe2d4628960caeafe7f696e1cb839f39e5dad` |

## Optional desktop Lua launcher

The optional managed launcher opens Alder Valley with an explicitly selected Lua
script. Start, Pause, Resume and Cancel drive the task on the existing game tick.
Script commands enter through an auxiliary binding on the human seat and their
pending decisions use the regular player dialog. Terminating a task releases its
command results and binding after pending work settles. A successful save reload
ends the previous task and requires an explicit start in the new session.

The normal desktop launcher, shared app and default CLI remain independent of
the interpreter. The optional native scenario uses the production example,
compares the saved game with equivalent manual play, and exercises the dialog
for a script-submitted end turn. Desktop persistence and replay of recorded input
sequences, task memory accounting, and the generated human/AI game gate remain
open.

Root, App and optional launcher formatting and Release builds passed. The
native suite passed **810 of 812 tests**, with two existing skips. The probe
passed **778 shared cases** and verified **463 compiled inputs**. All **14 Lua
groups** passed on native and Chromium, with matching results. All **16 Linux
desktop checks** passed. The optional launcher also published as a managed
application with verified notices for 31 locked packages.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-desktop-automation-play.json` | `d0fd29b4f9c3565b9993f60b2c0ed9116b926942368157c274a455606882434f` |
| `m4-desktop-automation-headless.xml` | `2d6437d32e07b1822aee6f178690c896f5e2d4bcf1153bd48af7a344559d367e` |
| `m4-desktop-automation-native.json` | `c53deeb489fe8fc2330cef0ac4c6c7305f46a96818bba5e50a232128f8621a3c` |
| `m4-desktop-automation-identity.json` | `e73e9036cbff3ae7405f90f44543c3c2f2d966839ce178926e6a89eafe7b60d6` |
| `m4-desktop-automation-lua.xml` | `ea486212e8e60697b9200ef9f29c6a7b51097ea0417dd7d2aa9c73dbfdc34462` |
| `m4-desktop-automation-lua-native.json` | `326b74fdd9c2c0d8d041a79ca250dd780a39ebcb3f45f79eaafa4ba788bf23b5` |
| `m4-desktop-automation-lua-browser.json` | `cc059149d24902bceaff82e2cf6d92de86b9a59484738a65cab16bd80e9df113` |

## Desktop automation input journals

The optional launcher records the authored local game from startup when given
an explicit output path. The versioned journal includes the script digest,
typed commands and interaction inputs from all local controllers, continuation
receipts, imports, and hashes of explicit saves. It has a bounded recording
budget and reports if an export cannot be written.

Headless replay recreates the local game from the journal through the same
session runtime. Diverging receipts or save hashes stop the replay. The output
is a new classic save, written after verification. The desktop cases compare
production and cancellation replays byte for byte after removing the script
files. Live task restoration, task memory accounting and the broader
generated-game exit gate remain open.

Root, App and optional launcher formatting and Release builds passed. The
native suite passed **812 of 814 tests**, with two existing skips. The probe
passed **780 shared cases** and verified **468 compiled inputs**. The **14 Lua
groups** passed on native and Chromium with matching results. All **18 Linux
desktop checks** passed. Command-line checks verified output collision handling
and rejection of a malformed journal before writing a save.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-desktop-replay-play.json` | `bb632b67cb31fea1934342b49a015c82aff5623f0318fcbb122a986bb4eaa56f` |
| `m4-desktop-replay-headless.xml` | `8f668ca015c5c01c0c35e8585605cf598c8304a3a672dbfc6dc8a6e9f373878f` |
| `m4-desktop-replay-native.json` | `5f22d913d1c7847552b9d93fbdd8834b1d9f2a4c5385f38300a1f95be73dc10c` |
| `m4-desktop-replay-identity.json` | `4da17699fe4d6892ac5eb86c66ec6255cd36e4a50d1feaec5e327def09a49065` |
| `m4-desktop-replay-lua.xml` | `7424af0b84df9ccaf8325bbf2af1c4ab5e27a85324bfed4b99110a61236f63b1` |
| `m4-desktop-replay-lua-native.json` | `c1fdb7ed8cf88c649a3bf5a407891b6b4c5bacca041eab87401d7582d78f9675` |
| `m4-desktop-replay-lua-browser.json` | `553e6398f64ecc961ff519d484cbb9579871e299aca9470b32a1354bdee0a5ae` |


## Desktop Lua resource isolation

Desktop tasks now execute Lua in separate interpreter processes. Each worker
verifies a 128 MiB managed heap cap before loading source. Private, bounded
pipe exchanges carry only source and scoped reply values; all game requests
still dispatch on the owning session lane. A five-second exchange deadline
ends a stalled worker without blocking presentation or command continuations.
The cap covers the managed interpreter heap, not total process RSS or OS access.
The portable inline adapter remains available without a per-task heap cap.

The Linux checks exhaust a worker's heap after a completed end-turn command,
then complete another turn. A second run suspends the worker at an interaction,
accepts the player's reply, observes watchdog failure and continues play.
Existing pause/cancel/reload and journal replay checks also pass with the
process boundary. Shared task tests cover delayed replies, paused polling and
authority revocation before dispatch. The independent worker driver covers
UTF-8/exact integer replies, malformed frames, instruction and allocation limits,
and refusal to execute without an admitted heap cap.

Root, desktop/automation and optional automation formatting verification and
Release builds passed. The native suite passed **812 of 814 tests**, with the
two existing platform/audio skips; the probe passed **780 shared groups** and
verified **468 compiled inputs**. All **14 Lua groups** passed natively and in
Chromium with matching results, as required for shared adapter changes. All
**20 Linux desktop checks** passed. Coverage/project-boundary checks, eight
identity tests, three coverage-inventory tests and six Lua receipt tests passed.
The native driver used the documented isolated X11/GTK environment. The test-only
WASM publish used the repository's complete pinned toolchain because the system
installation lacked its runtime archive for notice verification.

Browser resource isolation and Windows/macOS execution remain deferred.
Explicit task checkpoint restoration remains M5 work. This closes the desktop
allocation/stall containment gap, not the remaining M4 mechanics/AI or complete
generated human/AI game gate.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-lua-isolation-play.json` | `984a0646f84ab701ca7bd28b0838b4b0fb3d6ce5350a27aab396953b52b2d933` |
| `m4-lua-isolation-native.json` | `37648a9c053b946f074ed9d6603d5f038fdec58f8701362afba2583af4285695` |
| `m4-lua-isolation-identity.json` | `2ec78e12b2a6487bed91ab702ce2d2496c9514fb68ca995676aa025462064c35` |
| `m4-lua-isolation-lua-native.json` | `3d7cb68cb8c9f0d9c6d50082e78dfaf0fedd67099f15db55911b64967d929a01` |
| `m4-lua-isolation-lua-browser.json` | `956be5f66f63e0ce57009eda200918c6e8bc0ed7b2a55b0c69bce2a868105243` |


## Civilization rate operations and desktop choices

`CivilizationRates` implements V02 §§7–10 local cap enforcement, explicit live
normalization, staged balancing/holds, actual C03 distribution and C08 upkeep
previews, report preparation and acceptance. Preview preserves its city-field
changes while restoring civilization rates on normal completion; a real child
failure preserves the reached prefix. Drawing does not normalize rates, run
previews or apply the research-report science floor.

The `civilization.rates tax=N science=N` command accepts ordinary rates in
tenths, validates each component against the current government cap, and applies
full city recalculation. The desktop sidebar displays current tax/science/luxury.
Early Despotism/Anarchy choices update preferred startup rates and write actual
classic `CIV2.DAT` bytes in the configured storage directory, preserving existing
loaded preferences and imported fields across game reloads. Headless sessions
retain the same encoded output in a `BufferedPreferenceWriter`; owners can
supply another writer. A failed write reports `preferences_output_failed` and
retains the accepted rates and their committed effect. Reload retains session
preferences, and saves with any valid Despotism rates can continue in Alder Valley.
The authored new-game profile still supplies its fixed starting rates; general
F05 preference-driven desktop startup remains open.

Shared cases cover normalization including negative derived luxury, all three
cyclic adjustment orders, fixed preferred values, own/other and multiple holds,
live decisions with a captured cap, actual city previews, disordered/foreign
cities, upkeep failure, report science floor/full cost, persistence failure and
full-width accepted values. Direct and console paths compare saves through
reload and another turn. The native driver compares the accepted and continued
saves plus preference bytes against the independent direct-client sequence.

Government/revolution decisions, their rate-dialog continuations, the graphical
rate editor, population/funds notices, full F04 scheduling and generated-game
acceptance remain open. Browser and cross-platform execution are deferred.

Root and App formatting verification and root/desktop automation Release builds
passed. The native suite passed **817 of 819 tests**, with the two existing
platform/audio skips; the probe passed **785 shared groups** and verified
**471 compiled inputs**. All **22 Linux desktop checks** passed, including exact
rate/preference bytes, invalid-choice rejection, reload/turn continuation and
preference-output failure with committed rates. The driver scrolls sidebar
controls into view through ordinary mouse input and matches the direct client's
creation/reload history so entity lifetime tokens remain part of the full-save
comparison. Project boundaries, coverage inventory, eight identity tests and
three coverage-inventory tests also passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-rates-play.json` | `e6c16c2165e05f4f58f7b38a63c96be31ad9c976278253089d4d9de4c4813a9c` |
| `m4-rates-native.json` | `160704b4c04d5e52ad796805b1a653792058df9670cabfb68b34d78fd7024080` |
| `m4-rates-identity.json` | `1a3556da3de827f30e99bb3a5282c67e8ff72f4885795e262260d44188543ce9` |


## Desktop rate decisions

The Tax rates sidebar button and Orders action now submit
`civilization.rates.edit`. Its session continuation owns the V02 draft, initial
cap/preferred values, retained holds, explicit C03/C08 previews and Apply/Cancel
decisions. Adjusting a rate uses the shared cyclic balancing policy; painting
only displays the published choices and report. Apply uses the same validation,
full city calculation and preference writer as `civilization.rates`. Cancel
discards the draft while preserving accepted normalization and preview effects.
Holds persist across openings and classic reloads in the same `LocalGame`.

Named interaction options supply meaningful labels without UI-specific game
logic. The checkpoint codec retains these optional labels, validates option
identities and still reads older payloads without options. The editor itself
has no pending-operation checkpoint codec; that remains outside this checkpoint.
The owner checks current phase, government and accepted rates on continuation;
the local session rejects overlapping commands while a decision is open.

Shared cases compare editor and direct-command saves, reload and next-turn
results, plus journal replay of holds, cancellation and rejected/stale inputs.
They also exercise failed preference output. Desktop checks use actual pointer
input for hold selection, balanced drafts, cancellation and Apply, then compare
the complete save and preference bytes against the direct-client reference.
General government/revolution callers, population/funds notices, full F04
processing and generated human/AI game acceptance remain open. Browser and
cross-platform execution remain deferred.

Root/App formatting verification and root/desktop automation Release builds
passed. The native suite passed **818 of 820 tests**, with two existing skips.
The probe passed **786 shared groups** and verified **473 compiled inputs**.
All **23 Linux desktop checks** passed, including the new editor decisions and
the existing automation isolation/replay cases. Project-boundary, coverage,
eight identity and three coverage-inventory tests also passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-rate-editor-play.json` | `cf56894aebd8b4d04b9a2d06a0287d8fe12ddab70bfa12d9c7b1d160b3d8f1a0` |
| `m4-rate-editor-native.json` | `23250778a90e13fa924183361a9e212b8e5a4c0c873275d2aac32e62366506cd` |
| `m4-rate-editor-identity.json` | `fff3bf5ddfabb7e642e023a29e3a0ae2a6d917d10f311c0e9185e4b84e451ec0` |


## Revolution and civilization maintenance

`GovernmentTurn` implements V02 §6 as a resumable prefix. It captures the entry
government, clears GovernmentChanged and asks the F06 adapter to clear imported
Reserved40, then applies the calendar/Statue/Revolution branches. A requested
change must complete its actual government and rate children before acknowledgment;
the subsequent live AI Revolution test remains separate. Opaque imported bits
stay in `RecordCompatibility`.

`DiplomacyTurn` implements the following local V04 §11 continuation: two entry
draws for every nonzero civilization, senate toggling, signed request-count aging,
seven directed snapshot/clear visits, AI hostility/aging/stale-contact branches
and the ordered review/aid/ally scans. Recent peace is retained in the diplomacy
workspace. Stale contact invokes actual bilateral Contact clearing, preserving
Alliance where specified. AI Gang Up uses the existing AiCoordination game flag
and its specified encoding. A08 exchange/review/aid/seeking and V04 meeting/notice
services remain required children; their boundary tests do not claim complete AI
turns or generated games. Network routing remains outside this local service.

The desktop Revolution command confirms with the V02 result-zero convention,
uses actual government/city recalculation and does not invent a Revolution flag.
Restricted selection publishes named available governments; its setter completes
the real rate editor before the established-government notice. The local turn
suspends for these decisions after its city pass, then runs actual human
diplomacy maintenance before civilization assessment. Rejected/closed government
choices retain prior flags; closing the rate draft retains the selected government.
A failed preference write reports the accepted change and lets a completed
upkeep return to the player phase.

Alder Valley now admits Anarchy and Despotism saves. It has no additional known
government advances, Statue or AI civilization, so broader government choices,
research-triggered government dialogs, complete A08 policy children, population/
funds notices and general F04 scheduling remain unfinished. New native checks
compare revolution, Anarchy reload, turn-four government/rate selection and another
turn/reload against complete direct-client save bytes. Shared cases additionally
cover cancellation before execution, cancel/decline, failed output and replay.
Browser and cross-platform work remain deferred.

Root/App formatting verification and root/desktop automation Release builds
passed. The native suite passed **824 of 826 tests**, with the two existing
platform/audio skips. The probe passed **792 shared groups** and verified
**478 compiled inputs**. All **24 Linux desktop checks** passed. The Cancel
button case now waits for measured layout before clicking. Project-boundary,
coverage, eight identity and three coverage-inventory tests also passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-government-play.json` | `9c07695d9270a322582e0014c305fae5dc799e08d442c217c2c3d8c9b031c191` |
| `m4-government-native.json` | `0854ae603315645ec08409bb6b873691b8c59de08567505e557dada0f0d5ae8c` |
| `m4-government-identity.json` | `ffa9d6e2c9b94315e139cd1a3963559132c5e5259285ad29b19a20a84d801bbd` |

## Treasury and population/funds turn decisions

`CivilizationFinances` implements V02 §11's ordered treasury normalization before
start-gold capture: values below −16384 become 30000; the remaining negatives
become 0. City upkeep retains its own subsequent effects and there is no closing
treasury clamp. Population queries use signed city size and owner, live city
occupancy, triangular population units and the specified civilization cap.
Milestones preserve rounded-down thresholds, silent AI/autoplay updates and
malformed negative values. Funds checks use the retained city-count summary and
wrapped ten-turn forecast, with live presentation gates reread after the
population acknowledgment. Any nonzero funds response requests the rate child.

The desktop turn now captures normalized gold before recovery/cities, then runs
the resumable financial notice stage after government/diplomacy and assessment.
Closing a population notice does not repeat those stages or publish an early
`turn.advanced`. `CivilizationFinanceOperation` composes actual previews, rate
acceptance and preference writes through the now-reusable
`CivilizationRateDecision`. Closing a rate draft completes the financial notice;
a failed preference write retains accepted rates and its failure code.

Shared cases cover signed treasury boundaries, token-independent city queries,
signed owner comparisons, population caps/thresholds, post-popup gate changes,
nonboolean funds responses, wrapped forecasts, real rate acceptance/cancellation
and failed preference output. Direct and console population turns compare complete
save bytes with a headless reference, including reload and continued turns. The
native scenario imports a size-four city and maximum integer treasury, reaches
the population notice through actual upkeep, and compares saved/resumed bytes.
The authored one-city profile cannot reach the multi-city funds warning; that
branch is tested through the shared session service without widening the profile.

Earlier upkeep/research fixtures used a maximum integer treasury to force tax
overflow before building loss. Required start-of-turn normalization makes that
setup invalid. Those tests now retain the ordering check using an unaffordable
Temple and its actual refund; standalone C04 wrapping coverage remains in place.
Full generated human/AI games and remaining F04/AI/outcome dependencies are still
open. Browser and cross-platform work remain deferred.

Root/App formatting and verification, root/desktop automation Release builds and
project-boundary checks passed. The native suite passed **828 of 830 tests**,
with the two existing platform/audio skips. The probe passed **796 shared groups**
and verified **481 compiled inputs**. All **25 Linux desktop checks** passed;
coverage maps all groups across 94 families, and the eight identity/three coverage
validator tests passed. The first native attempt timed out before opening the
population scenario's end-turn confirmation; the full rerun passed. The driver
now persists the current window's observation on timeout to diagnose that input
timing failure if it recurs; no gameplay failure was established by that attempt.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-finance-play.json` | `3bd74935c271182d20b4ef86f84c37077a6c6b64a7bdedf50f081501ec84daf5` |
| `m4-finance-native.json` | `a58cfca45411f5eacdd5c7f9037f131bb1d0ddab0433310e2fa1bd65aac55883` |
| `m4-finance-identity.json` | `e9c86efdb6e4ebd819e63b24709b5871d7c792709d157043fdafd04a1648cd16` |


## Civilization scoring and desktop reports

`CivilizationScore` implements V08 §§2–3's direct score query, objective-value
query, scenario score and rating preparation. World-owned scratch keeps ordinary
total separate from conquest bonus, pending ship points separate from arrived
points, and configured-civilization objective outcome separate from the reporting
civilization's points. It uses actual V07 performance calculations, retaining
preview year/population and Fusion effects even for unlaunched ships. Raw wonder
scoring accepts backed dead/beyond-live slots; report icon eligibility remains
stricter. Arithmetic preserves signed city mood bytes, wrapping products,
pollution differences, peace thresholds, sequential map factors and unsorted
objective thresholds. The original zero floor belongs only to the ordinary sum.

Report preparation is a semantic operation: ordinary reports recalculate every
live owned city through actual C03 once, objective reports skip that recalculation,
and Scored games show the existing-score notice without changing score or city
state. `ScoreReport` owns its values, thresholds and visible-wonder identities;
painting and closing the report do not rerun the operation. Rating consumes the
prepared shared score, retaining full-width percentage and title-index selection.
Localized fame-title resources and their display remain a separate unfinished
consumer, along with Hall of Fame and outcome dispatch.

The desktop Advisors menu and `civilization.score` command share the session
continuation. Reports show the component breakdown, pending ship points, the
selected total/conquest score and rating, with scenario/cheated annotations.
Direct/console tests cover accepted/Scored reports and input replay. Native
acceptance opens the report, verifies one preparation across subsequent display
updates, and compares complete save/reload continuation against the direct client.
Full generated human/AI-game acceptance and the remaining mechanics/AI/outcome
work are not closed by these reports. Browser and cross-platform work remain deferred.

Root/App formatting verification, root/desktop automation Release builds and
project-boundary checks passed. The native suite passed **834 of 836 tests**
with the two existing platform/audio skips. The probe passed **802 shared groups**
and verified **485 compiled inputs**. All **26 Linux desktop checks** passed.
Coverage maps all groups across 95 families; eight identity and three coverage
validator tests passed. No browser or cross-platform execution was added.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-score-play.json` | `214e57bba09bf3a7d046fefc7761c7dab204acf640f50d913c26fd8610c94fcc` |
| `m4-score-native.json` | `f52877a5645c60efdb3fc3c19225b250bbb19fea844e192308fc569fee18949c` |
| `m4-score-identity.json` | `a2628ff67b75a8484bc8f9045e54a96b99cbe2642b6261056df13f23338d9d33` |


## Hall of Fame records and desktop viewing

`ClassicHallOfFame` implements the F06 headerless six-record format with exact
numeric widths and retained 24-byte name fields. `HallOfFame` implements V08 §4
record preparation from the existing shared score/rating and V02 population
query, without recalculating either score or rating. It resolves default/custom
leader and plural names from installed rule/name state. Scores and ratings narrow
to signed 16-bit values, scenario/objective/calendar fields remain distinct, and cheated
status adds its stored marker. V08 uses the actual Scenario bit (`0x80`), not
the separate saved-as-scenario/export bit; score queries/reports and Hall records
now explicitly preserve that distinction, with a regression for export-only state.

Loading sparsely resets only score/rating, reads records in order, copies accepted
partial prefixes and sparsely resets all rows after the first failed record.
Writing uses the actual classic truncate/write/close service and stops on the first
failed item. It does not delete or roll back partial output. Display reloads the
file, so failed output or external changes can affect the visible ranking.
Insertion compares retained ratings strictly, shifts whole records and preserves
previous rank when nothing qualifies. `HallOfFameOperation` retains one prepared
entry across the Clear/insert/save/show loop; any nonzero modal result retries
that loop, including nonboolean values from a trusted caller. Output failure is reported after
closing while preserving file effects; no automatic retry is added.

Safe new-name policy rejects embedded NUL, unrepresentable CP1252 text and names
longer than 23 bytes before touching the file. Fresh name padding starts at zero;
loaded padding stays byte-exact through storage, shifting and sparse resets.
Unterminated/unrepresentable stored names and invalid difficulty values receive
safe display diagnostics. Numeric rows preserve signed score hiding, ordinary or
saved scenario-month dates, objective/population selection and scenario/cheat
markers. Localized fame titles remain omitted pending the owning resource join.

The desktop Advisors Hall of Fame action uses `HALLFAME.DAT` in the same native
storage directory as gameplay preferences. The ordinary viewer offers Close only;
it cannot insert a current game or clear rankings. End-game insertion is a trusted
session continuation with real file services, exercised through shared tests, but
the full V08 outcome caller remains unfinished. Native UI acceptance reads ranked
records across game reload, verifies unchanged Hall/game bytes, then verifies a
short Hall file displays an empty ranking without rewriting the partial input.
No browser or cross-platform acceptance is claimed.

Root/App formatting verification, root/desktop automation Release builds and
project-boundary checks passed. The native suite passed **840 of 842 tests**
with the two existing platform/audio skips. The probe passed **807 shared groups**
and verified **489 compiled inputs**. All **28 Linux desktop checks** passed.
Coverage maps all groups across 96 families; eight identity and three coverage
validator tests passed. The final suite includes trusted nonboolean Hall replies
both with and without an insertion rank.

An earlier desktop attempt missed the End Turn button after imported text changed
its layout. Its failure snapshot showed no command submission. The input driver
now waits for measured button bounds to stay fixed across service frames and
records the last click bounds in failure diagnostics. The final acceptance run
passed with that stabilization; it does not automatically repeat a submitted command.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-hall-play.json` | `1add1b700b6ca9cd534298570d3906e47102abbb45a8c439f13fd5429295e01f` |
| `m4-hall-native.json` | `5dd54ea899b14d69ef5ef0a47bf90bc7b0207638c2ee4e9f1dc5e925e4e75e17` |
| `m4-hall-identity.json` | `579e71e3b1cfe48bda84bc14e176c0ba833b6ddf633045419e4fa137e909bdbd` |


## Explicit retirement and power-history reports

P07 history preparation now captures retained A01 samples, F04 calendar dates and
currently alive or starting civilizations. It bounds sample count to 150, keeps
objective cadence separate from ordinary cadence, and includes undisplayed
civilizations when finding the vertical reference. It never rebuilds history
from current strength. The desktop plot and expandable exact-value table consume
one owned report; resizing and repainting do not run gameplay.

The File menu and player command registry now expose explicit Retire and Quit.
Retire first confirms, then opens history, checks live Scored, and runs score,
rating and Hall insertion in order. Each child reads the current local human.
The end-game score report does not prepare rating early; the separate rating
child consumes the then-current shared score. Ordinary Advisors score still
prepares its combined score/rating report. Already-scored retirement skips the
score/rating/Hall tail. Quit confirms and skips all reports.

Confirmed completion disables the game/map and exposes reload through the File
menu. It does not set Won or Scored, post Retirement, or clear an existing pending
reason. Hall output failure preserves file effects and is reported after the
confirmed game closure. This profile has no active CD playback to stop. Numeric
rating is presented; authored fame-title resources and ending media remain open.
Scheduled retirement and pending outcome handling are subsequent integrations,
and full generated human/AI games remain an unclosed M4 acceptance gate.

Shared tests cover sample boundaries, hidden maxima, extinct identities, stable
report values, report order, live Scored/local-human reads, failure continuation,
state flags and direct/console cancellation, retirement, quit and reload. Pending
history checkpoint encoding explicitly rejects unsupported restoration instead
of dropping the chart; saved live task restoration remains M5. Native acceptance
uses an authored imported history, compares retired Hall bytes with the direct
reference, checks report order and reopens the saved game before Quit.

An initial native run exposed a reference-lifecycle mismatch in the byte comparison:
the desktop had read a complete Hall before reading a short file, while the direct
reference started with only the short file. V08 sparse reset retains metadata from
the earlier read. The reference now executes the same complete/short viewer sequence
through the real service before retirement, preserving that retained metadata.
Byte comparison failures now save both images for inspection.

Root/App formatting verification, root/desktop automation Release builds and
43-project boundary checks passed. The native suite passed **844 of 846 tests**
with the two existing platform/audio skips. The probe passed **811 shared groups**
and verified **495 compiled inputs**. All **30 Linux desktop checks** passed.
Coverage maps all groups across 97 families; eight identity and three coverage
validator tests passed. Browser and cross-platform execution remain deferred.

The history layout was inspected in the initial desktop capture and a separate
focused capture from the owned application window. Both showed the plotted series,
date labels, legend and controls. The final full-run root-screen capture was black,
so it is not used as visual evidence; the focused capture below is retained.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-retire-play.json` | `53af59c7dc980ab514704dd8693a92cc065cfb977f6075a2e69260c69b4c0f51` |
| `m4-retire-native.json` | `59e0ae6f7e7435aa8a037a3bc76d961c6c2b6ce5bde4eb13b2e75b62c66e4c03` |
| `m4-retire-identity.json` | `4311bafb1287d2b16a310238e0f5e8afe8afb9c55a36a11e64e917a3ab588815` |
| `m4-retire-history-focused.png` | `a63d8bc73700893a9a34898676ced40eccc6c2c67e40dd98fad33b08dc60c42e` |

## Scheduled retirement and pending turn outcomes

F04's arrival boundary now processes one launched, newly arrived spaceship in
ascending normal-civilization order, including inactive slots. It retains the
first winner and pending reason after Won, uses the real V07 arrival predicate
without a chance draw, and sets Won after any landing notice. V08 retirement
checks exact classic years 2000/2020 or a scenario's limit minus five/limit;
Won and pending outcomes suppress it, autoplay does not. The end notice completes
before Won and pending Retirement are posted.

Pending outcomes present actual spaceship, conquest-history or defeat information,
then run retained history, score, rating and Hall through the existing children.
The final continuation reads the current mode, reason and year. Defeat closes
without asking; other eligible outcomes offer End game/Keep playing and then set
Scored. Scenario retirement outside 2020 continues without setting Scored. The
handler clears the pending reason on normal return and preserves Won. Child
failures remain visible after the required continuation and closure effects.
The current Hall service remains single-player only; a mode change during its
report is reported as unsupported while the outcome's final mode gate still runs.

The authored desktop turn now checks opening outcomes before treasury normalization
and city upkeep. Closing at retirement therefore leaves those phases unprocessed;
continuing executes them once. Pending movement outcomes run before the next global
advance. Last-city famine with no surviving units completes movement immediately
and now reaches the real defeat page, reports, Hall and game closure. No generic
victory scan or supplied gameplay child was added.

Ending pages show real launch/arrival dates, population, local/rival identity,
recorded conquests or an actually owned advance. Missing artwork and narrative
text are diagnosed explicitly under P10's no-video presentation contract. Authored
ending sections and fame-title resources remain unfinished, as do the generated
human/AI game and remaining F04 orchestration. Browser and cross-platform work
remain deferred.

Shared cases cover simultaneous/inactive arrivals, first/local winner gates,
notice-before-Won ordering, equality misses and repeated warnings, scenario/export
flags, live continuation reads, report failure and nonboolean trusted replies.
Direct/console retirement cases compare phase/flags, preserve pre-upkeep state,
save/reload and continue without repeating retirement. The Linux driver uses a
fresh Hall service for scheduled retirement, compares complete saves and Hall bytes
with a direct reference, and exercises both final choices. These comparisons are
implementation integration evidence, not independent original-game observations.

Root/App formatting and verification, root/desktop automation Release builds and
43-project boundary checks passed. The native suite passed **850 of 852 tests**
with the two existing platform/audio skips. The probe passed **817 shared groups**
and verified **498 compiled inputs**. All **32 Linux desktop checks** passed,
including the existing optional Lua/replay/resource-failure checks. Coverage maps
all groups across 98 families; eight identity and three coverage validator tests
passed. No browser or cross-platform acceptance was run.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-outcome-play.json` | `54a3bc4fc658449a14843eec72b7497a57371791b93a0ac8847ec638d5be3ea6` |
| `m4-outcome-native.json` | `0c4b0d34447728b48f2ce5ff7696547ad3747b68e0c954278995d0a7a93950c8` |
| `m4-outcome-identity.json` | `3d18db80a190a1edf78ccd15d25a665bf7064b0df12eb6dc8329a100a240ae34` |

## War, alliance obligations and border measurement

V04 declaration now applies actual bilateral cascades, directed Vendetta/aggressor
flags, last-contact updates, reputation and mediator credit/attitude effects.
Contradictory Peace+Ceasefire runs both penalty blocks; stored rank/city summaries
and active Eiffel queries decide each contribution. Unsigned reputation and signed
credits wrap. Declaring against an ally cancels the alliance and expels units,
retaining Peace and leaving War unset. Forced war composes that declaration when
its reverse-relation/human gate admits it, then applies the separate contact,
attitude and new-sneak-attack result rules.

Alliance cancellation uses real weighted city queries and coastal-body admission,
then actual U01 lift/cargo grouping and whole-stack relocation. Land consumes the
stored regional city census and skips Trade; ocean bypasses those gates. Land
nearest-city ties prefer the later city, ocean port ties the first. Missing
destinations preserve position and order. The relocation can include cargo and
retained lifted members; it is not replaced by a coordinate store. Maps redraw
after both directions, and an active moving unit produces a cursor update.

Ally activation preserves directed contact/alliance checks, ascending slots without
an alive filter, asymmetric human branches, local notices and contact effects.
Joined war uses the specified last-contact-minus-turn test and controlled primary
modulo draw. It does not substitute ordinary declaration penalties or activate
another layer of allies. Network modes are rejected before mutation; their
publication and remote notice contracts remain open.

Border intrusion measurement retains warning/intruder flags across calls, resetting
only current summaries. It uses the first qualifying occupied neighborhood tile
for each city, even when no unit in that physical stack qualifies. It counts signed
roles below five without another owner filter, weights captured tile components,
and applies the extra repeat offense for more than three local units. Tests cover
mixed physical stacks, overlap, earlier empty-result stacks, wrapping, ocean/city
skips, directed alliance gating, classic unit conversion and fresh summary state.

The new operations compose actual C01, U01, C08 and existing V04 children. Effects
interfaces expose semantic notices/redraw/cursor updates only; stock portrait
choice remains presentation-owned. V06 incidents, A08 relation review and the
remaining V04/V05 contact/negotiation callers, desktop diplomacy UI and generated
human/AI-game acceptance remain unfinished. No new desktop UI or browser/cross-
platform acceptance is claimed by this checkpoint.

Root/App formatting and verification, root/App Release builds and the 43-project
boundary check passed. The native suite passed **860 of 862 tests**, with the
two existing platform/audio skips. The probe passed **827 shared groups** and
verified **502 compiled inputs**. Coverage maps all groups across 99 families;
eight identity and three coverage validator tests passed. This checkpoint adds
no UI controls, changes no Lua interpreter/task code and runs no browser or
cross-platform qualification.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-war-native.json` | `bdc0a8739540d714fcd039d1d9fd2056a385824130c457e4c076593199cddcfa` |
| `m4-war-identity.json` | `066dfd973241eb769378fd97335decbd68c7d993a1966e1fffd5818c5befb1f6` |

## Diplomatic stance, willingness and patience

A08 diplomatic evaluation now uses actual V07 race queries, C08 active wonders,
regional military/city summaries, nuclear counts, reputation and directed treaties.
It preserves unsigned fear contributions and signed demand arithmetic, nearby-unit
pressure through U01 spatial queries, third-party hostility, first strength ties
and the last eligible war target. Real captured-city records feed the declaration
threshold. Scenario overrides run after war intent and preserve earlier demand,
mode and initial-aggression results.

Evaluation updates only its declared fields. The selected attitude pair survives;
last contact is captured only for the first active opening evaluation, and war
intent can remain from an earlier evaluation. Later peace and scenario stages can
change aggression without rewriting its earlier snapshot or request mode. Final
attitude caps read the actor-to-counterpart direction. War intent alone changes
no treaty. Invalid reached arithmetic retains the valid evaluation prefix and
fails explicitly instead of fabricating a neutral score.

The opening government branch captures the signed request count, computes real
V05 patience and invokes actual restricted A08 choice/V02 transition when admitted.
Tests verify the government, announcement, flags and controlled primary draw, plus
strength and scenario suppression. Patience consumes the shared signed attitude,
active Eiffel and reverse treaty additions, with War overriding the result.

Alliance willingness consumes that evaluation and retains its ordered overrides.
Dangerous-enemy admission replaces earlier reasons; common-target search uses
first ties and the existing-target override without reapplying candidate gates.
Later vendetta/distrust rejection can be overridden by an already allied target;
attitude/request rejection retains the accumulated reasons. The result does not
sign an alliance or stand in for V05's price and accepted response.

Shared cases cover regional boundaries, nuclear balance, reputation, wonders,
nearby garrisons, third-party ties, captured cities, signed attitude, race/strength
gates, scenario/export distinctions, retained/fresh state, government changes,
patience boundaries, common targets and reasons surviving rejection. The next
joins remain V06 incidents, A08 security/relation review and V04/V05 negotiations.
Live audience checkpoint restoration and complete generated desktop human/AI games
remain unfinished; browser and cross-platform work stay deferred.

Root/App formatting and verification, root/App Release builds and the 43-project
boundary check passed. The native suite passed **867 of 869 tests**, with the two
existing platform/audio skips. The probe passed **834 shared groups** and verified
**504 compiled inputs**. Coverage maps all groups across 100 families; eight
identity and three coverage validator tests passed. No UI controls or Lua task
code changed, and no new UI/browser/cross-platform acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-stance-native.json` | `1eeabff06a04002b29b5f720ed1bf4f7515a6b3da2f5d2fa1b65fc3c9b6ded77` |
| `m4-stance-identity.json` | `cb2ab4c6dcb7343a09df80ca972bc03e8589015cfcbd96c581c0e84910aac95d` |

## Espionage incidents and resumable escape

V06's local incident service now composes actual V04 war/alliance transitions,
A08 stance and V02 government changes. Reverse treaties admit the incident;
the human actor's response independently reads the forward alliance. A human
victim can declare war or cancel an alliance through a retained pretext decision.
That alliance reply only clears flags and updates contact; it does not expel
units. The human-to-AI alliance response invokes actual cancellation and expulsion
without inventing a simultaneous war.

The retained city-mission value belongs to the current game and is read live.
Theft leniency runs stance, captures the attitude category before the random
adjustment, then returns before the senate. Democracy reaches the senate without
a draw; other governments consume their specified primary draw before the live
Republic and scenario gates. Autoplay suppresses notices without suppressing an
admitted Anarchy transition. Actual government changes recalculate cities and
clear vendettas; earlier war effects survive a later presentation failure.

`SpyEscape.Resolve` exposes the completed escape/loss prefix and its required
incident. `SpyEscapeOperation` performs actual loss before yielding the victim's
pretext, retaining the fate and incident through choice, cancel and timeout.
Resuming neither repeats removal nor consumes another escape draw. The existing
synchronous `Try` caller still invokes its required incident child. Full city/unit
missions, desktop mission commands, general elimination incident composition and
saved pending incidents remain open; this checkpoint adds no mission-admission
bypass or new desktop control. Network response, browser execution and
cross-platform qualification remain deferred.

Shared cases cover asymmetric treaty directions, AI/human combinations, no-treaty
and already-war returns, human alliance response versus real AI expulsion, live
mission/fresh state, exact leniency category and draw, senate/scenario/autoplay
ordering and actual city production changes. Failed spies and non-spies exercise
loss before pending reply; invalid victims and later output failures preserve
committed prefixes. M4 remains active until full generated human/AI desktop games
and the remaining mechanics/callers pass their acceptance gates.

Root/App formatting and verification, root/App Release builds and the 43-project
boundary check passed. The native suite passed **874 of 876 tests**, with the two
existing platform/audio skips. The probe passed **841 shared groups** and verified
**507 compiled inputs**. Coverage maps all groups across 101 families; eight
identity and three coverage validator tests passed. No UI controls or Lua task
code changed, and no new UI/browser/cross-platform acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-incident-native.json` | `74f830df18bfa0daeaca7ef846fcb4a65cddf0258074ef714ed4b7909df813be` |
| `m4-incident-identity.json` | `3b6438d38e7b4418b69122c9cd05bed71e42a35e1dd7325537aa9d3be245e616` |

## Civilization restart research integration

V01 creation now takes the actual V03 research-grant service instead of an
arbitrary catch-up callback. Every selected catch-up advance uses the restarting
seat as source, permits the specified encyclopedia path and completes the entire
grant before the next candidate. The generated initializer explicitly omits this
service only in its turn-zero domain, established by the real new-game reset.
A reached nonzero-turn grant without the service fails after its valid prefix.

The temporary inactive seat suppresses ordinary observed acquisition notices,
but its new knowledge can trigger a recursive Great Library grant for another
civilization. Integration cases run those actual grants, Leonardo upgrades and
V03/V02 government prompts, including the resulting city recalculation and
Fanatics production substitution. They verify source bytes, global known-by
sets, unchanged first discoverers, research-target clearing and the retained
state visible during nested notices. The parent's later type-counter clear and
placement-time advance-count assignment do not undo granted knowledge or effects
on other civilizations.

The actual last-city elimination/recreation chain now runs catch-up grants in its
regression cases. Both accepted restarts and later NoRestart rejection retain
knowledge; only successful placement overwrites advance count with one. Nested
Library notice failures and invalid disabled-prerequisite walks terminate the
creation after committed research effects, leaving the inactive seat and the
not-yet-cleared type counters intact. Existing catch-up tests now exercise real
grants instead of test-supplied gameplay effects.

General elimination still needs its resumable foreign-spy incident join, and the
generated desktop game's full turns and human/AI acceptance remain open. No new
desktop controls or Lua task code are introduced. Browser and cross-platform
qualification remain deferred.

Root/App formatting and verification, root/App Release builds and the 43-project
boundary check passed. The native suite passed **877 of 879 tests**, with the two
existing platform/audio skips. The probe passed **844 shared groups** and verified
**508 compiled inputs**. Coverage maps all groups across 102 families; eight
identity and three coverage validator tests passed. No new UI/browser/cross-platform
acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-catchup-native.json` | `0d870d6d1ca5261d87742a9f266e966e24bd3e40037c65ff1aaa303a2ca87bc6` |
| `m4-catchup-identity.json` | `2eefb6e5d792f2e89c96114ea9baef721272fa55517802fa3e9ed839d484ccf5` |

## Resumable last-city elimination

`CivilizationElimination.Begin` now retains the actual V01 destruction flow across
V06 human pretext decisions. An explicit stack runs nested last-settler checks
before completing each deletion/loss tail. Shared internal U01 deletion, cargo
loss and spy-escape steps preserve the existing synchronous APIs and their owning
callbacks; the resumable path runs actual lifecycle and incident children.

The captured descending unit bound and current index survive a decision. A lost
spy and earlier history/ship effects are committed before the pretext, while
later units, password handling, exploration and recreation wait for the incident.
A reply belongs only to the currently pending victim; it cannot accept a later
incident reached by the same resume. Creation search yields after its candidate
budget without restarting or changing AI admission. Disposal retains committed
prefixes and releases the pending frames; failed operations cannot resume.

Tests include a custom Settler-role Spy whose deletion triggers another
civilization's last-city check and interrupted foreign mission. Both land and
at-sea cargo paths complete the inner victim's decision before finishing loss
and offering the outer victim's decision. Actual recursive checks, cargo removal,
password/human returns, subsequent recreation and the final map redraws retain
their order. Other cases cover pending lower units, one-time history, self-treaty
SpyChat clearing, bounded search/disposal, senate-notice failure and authority
rejection before mutation.

`Session/CivilizationEliminationOperation` routes each decision to the current
victim participant. A real command-runtime test verifies that the initiating seat
cannot see or answer another victim's prompt, unrelated seats receive no result,
and accepted replies or cancellation-as-decline finish the actual lifecycle.
This is a trusted child continuation, not a player-accessible removal command.
General city/combat/mission parents, saved pending-operation restoration and full
generated desktop human/AI games remain open. No desktop controls or Lua task
code changed; browser and cross-platform work remain deferred.

Root/App formatting and verification, root/App Release builds and the 43-project
boundary check passed. The native suite passed **882 of 884 tests**, with the two
existing platform/audio skips. The probe passed **849 shared groups** and verified
**510 compiled inputs**. Coverage maps all groups across 102 families; eight
identity and three coverage validator tests passed. No new native UI, browser or
cross-platform acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-elimination-resume-native.json` | `bc8a7662948fe12076c11179c7cb04b0ede418dc9f990ddc82dd25d7d196d5ef` |
| `m4-elimination-resume-identity.json` | `0030b35f85c3d2a4cc5f49e080cf92404c0c7e98e4f0df8f667d73a02d956a84` |

## Unit bribery and sabotage

`UnitEspionage` implements the V06 §9 local unit-mission dispatcher and direct
bribery helper. Pricing uses actual Palace step distances, the actor's Communism
cap, signed unit cost and ordered integer arithmetic. Human offers retain the
price and original owner across the reply. Transfer changes the actual owner,
type counts, subversion, movement, orders, home, visibility and tile ownership;
the nearest city of any owner determines whether a home can be assigned.
The specified helper does not consume the bribing actor or repair unrelated
unit-total/military counters. AI prerequisite, Trade-role and half-treasury gates
remain distinct from human acceptance.

The human Spy menu retains its autoplay behavior. Sabotage applies the actual
remaining-HP query and half-damage operation, then runs real escape and nested
civilization elimination before returning the movement stop flag. The shared
`CivilizationElimination.BeginEscape` path also replaces the earlier standalone
session escape composition, allowing nested last-settler incidents to suspend
and route each decision to its victim.

Seven new groups exercise cached-price acceptance, signed and wrapping counters,
foreign-nearest home rejection, local/nonlocal/autoplay presentation, stack and
Democracy refusal, AI admission, surviving/lost spies, simplified-combat HP,
nested elimination, failure prefixes and local authority. Session-runtime tests
verify actor-private menu/payment choices and ordinary affordability admission;
the core retains the specified forced/custom reply semantics. U02/A07 movement
admission, city missions, general desktop presentation and complete generated
human/AI games remain open. No desktop controls or Lua task code changed;
browser and cross-platform work remain deferred.

Root/App formatting and verification, root/App Release builds and the 43-project
boundary check passed for this checkpoint. The native suite passed **889 of 891
tests**, with the two existing platform/audio skips. The probe passed **856 shared
groups** and verified **513 compiled inputs**. Coverage maps all groups across
103 families; eight identity and three coverage validator tests passed. No new
native UI, browser or cross-platform acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-unit-mission-native.json` | `30d95a564fccf09ad63746fc9688708f8266037fd043c66b97ed38ac47431adb` |
| `m4-unit-mission-identity.json` | `37dc07465bd7340bcbda35fdb6f5f95349fe3c3a87ed3ae18fc674ee9e50dca0` |

## City sabotage and poisoning

`CityEspionageDamage` implements V06 §6 against the city controller's retained
actor/victim/Spy context. Human treaty confirmation precedes damage and sampling,
including during autoplay. Sabotage retains cyclic building selection, safe owned
production names, the targeted building/production menu, SDI refusal and ordered
Palace/City Walls capture checks. The ordinary production-diversion roll uses the
captured Spy identity and live Veteran flag. AI-versus-AI sabotage still escapes
but leaves production and buildings unchanged.

Damage uses actual C08 storage. Production sabotage clears shields; building
sabotage removes the selected facility. Custom targets35–38 retain their supported
names and ignored building writes; larger unsupported name reads stop explicitly.
Poison directly changes size or food and remembers the resulting city size through
W03, without substituting a city turn or population-loss helper. Both missions run
the actual escape/incident child after their committed effects.

`CivilizationElimination.BeginCaught` now shares the actual negative-mode escape
and retains capture identity until all nested last-settler checks finish. A lost
custom Settler-role Spy can therefore suspend for another victim's incident before
its capture notice and parent mission continuation. The synchronous `Caught` API
uses the same steps. `CityEspionageDamageOperation` routes actor questions and
nested victim decisions separately, including cancellation and terminal failure.

Seven shared groups cover real city/building/knowledge outcomes, captured names,
signed stocks/sizes, exact conditional draws, capture loss at each stage, relocation,
promotion, custom target handling, failure prefixes and disposal. A real session
case follows confirmation through poison and unit loss to the victim's private
decision, then verifies accepted war or cancellation without repeated damage.
These helpers preserve the owning controller boundary; city-menu admission and
fallbacks, remaining missions, U02/A07 movement callers, complete desktop controls
and generated human/AI-game acceptance remain open. Browser and cross-platform
work remain deferred; no Lua task or desktop UI code changed.

Root/App formatting and verification, root/App Release builds and the 43-project
boundary check passed. The native suite passed **896 of 898 tests**, with the two
existing platform/audio skips. The probe passed **863 shared groups** and verified
**516 compiled inputs**. Coverage maps all groups across 104 families; eight
identity and three coverage validator tests passed. No new native UI, browser or
cross-platform acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-city-damage-native.json` | `551acf14f240d65b24ea6614216ecdfdc7655ee5d2de9c738beaef57d9de1200` |
| `m4-city-damage-identity.json` | `620cd5db2b4b6af54aa25c2cb0fc933d5c85dab7a715bceb296f04ad29ac9bdb` |

## Captured technology and city theft

`CapturedTechnology` implements V03 §12 with actual A08 valuation, research-goal
help, ordinary research grants and C06 production reset. It retains the best
identity/value across help rebuilds and uses later equal valuations. Eligibility
is independent of ordinary enabled/research prerequisites; the scenario government
lock excludes its five specified identities. Accepted backed human replies are
not checked again against the option list. Unsupported indices stop before name
reads and grant writes. Local victim notices and the final live AI production
reset retain their own gates.

`CityTechnologyTheft` implements V06 §5 with real V07 spaceship-race selection,
U01 physical defender traversal and resumable capture/escape. An active race can
retain the last ordinary cyclic qualifier or select the first qualifying part
prerequisite/Fusion Power. Detection scans diplomatic-role units without an owner
filter, applies the three thresholds in stack order and exhausts the actor without
granting knowledge or invoking escape. No-candidate AI outcomes request Embassy
or Sabotage from the owning controller; human failure charges the specified moves.

Targeted theft completes actual capture and V03 selection before escape, then sets
the city's stolen flag after the incident finishes. Ordinary theft grants the
retained candidate and sets that flag before escape. Both paths retain the target
city after a capture check relocates the Spy. `CityTechnologyTheftOperation`
routes actor choices and mandatory captured selection separately from the victim's
incident. Its completion can carry a fallback; the parent must dispatch it.

Eight shared groups cover chooser eligibility/ties/help, actual AI production
reset, cyclic race selection, human/AI cancellation and empty paths, defender
thresholds/order, targeted/hard capture, private session choices and failure
prefixes. Composed tests exercise actual Barracks sale after Gunpowder and a failed
government choice after a committed research grant. Session help rebuilds neither
repeat capture nor grant early. Complete city-menu/fallback dispatch, other city
missions, C09/V05 callers, U02/A07 movement admission, general desktop presentation
and generated human/AI-game acceptance remain open. Browser and cross-platform
work remain deferred; no desktop UI or Lua task code changed.

Root/App formatting and verification, root/App Release builds and the 43-project
boundary check passed. The native suite passed **904 of 906 tests**, with the two
existing platform/audio skips. The probe passed **871 shared groups** and verified
**520 compiled inputs**. Coverage maps all groups across 106 families; eight
identity and three coverage validator tests passed. No new native UI, browser or
cross-platform acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-theft-native.json` | `fe34c7e42506516e5cb1c76f225871b257fbbf2841c1fcfc45b6562eec05e71b` |
| `m4-theft-identity.json` | `2736e5db4c041223716eca84b9a6f766a8dd17a623bdfe790cd4becd20c83a26` |

## Embassy and city investigation

`CityEspionageVisits` implements V06 §4. Embassy creation sets only the actor's
directed relation; an existing embassy returns Investigate to its owning
controller without consuming the unit. A human visits intelligence before the
captured Spy classification selects a one-point movement charge or plain U01
deletion. An AI destroys its unit with redraw and cargo handling, including an
AI Spy. These paths do not substitute escape for embassy consumption.

Investigation requests the city window for a human. A captured non-Spy then runs
the actual escape child with victim−1; a Spy pays one movement point only when
the city's live Investigated flag is absent. The human branch sets that flag
after the window/loss child; the AI destruction branch leaves it unchanged.
Neither operation invents city/map revelation or a generic mission incident.
Victim notices follow the actor's effects and retain the original type/name inputs.

`CivilizationElimination.BeginDelete` and `BeginDestroy` provide actual plain and
cargo-aware U01 operations through the same resumable V01 check stack. Last-settler
loss and interrupted foreign missions therefore finish their pending incidents
before the visit's final flag/notice. No synchronous callback replaces those checks.

The session adapter requires a P07/P06 window adapter, routes it to the actor and
invokes it once per visit, including when polled without a reply. Closure,
cancellation and timeout continue the already-admitted mission. Window construction
failure is terminal and retains the acquired embassy without consuming the unit.
This boundary does not claim an intelligence/city report renderer or complete
desktop presentation. Full city-menu/fallback dispatch, movement callers and
generated human/AI-game acceptance remain open.

Seven groups verify movement wrap, retained Spy/type classification, plain deletion
versus cargo loss, actual relocation/escape, AI and autoplay gates, nested defeat,
pending notices, failures, disposal and session privacy. Browser and cross-platform
work remain deferred; no desktop UI or Lua task code changed.

Root/App formatting and verification, root/App Release builds and the 43-project
boundary check passed. The native suite passed **911 of 913 tests**, with the two
existing platform/audio skips. The probe passed **878 shared groups** and verified
**523 compiled inputs**. Coverage maps all groups across 107 families; eight
identity and three coverage validator tests passed. No new native UI, browser or
cross-platform acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-visits-native.json` | `3d84938f0a8c61b2010eca549e58225823e8b3aea8759208b1080529de15be70` |
| `m4-visits-identity.json` | `3fdc83cd77164ec1be73513f9887081edd6c6c85b0eca22eef78d3fdf63f3db3` |

## Nuclear action and pollution placement

The local U07 action now announces the target, selects the first qualifying SDI
city (including third parties), completes the semantic animation, and removes
stacks before beginning the separate blast pass. Foreign first-stack owners
receive the directed nuclear flags and actual V04 attitude adjustment; friendly
stacks are removed without that penalty. Delivery admission and a distant
missile's fate remain with U02/U06/V06 callers. No combat casualty event or
ordinary victory aftermath is invented.

`BeginEraseStack` carries actual U01 retained-next deletion through the existing
resumable V01 stack. A last-settler loss can interrupt a foreign espionage mission
and wait for its human incident response before population or terrain damage.
The session child routes that decision privately and preserves accepted effects
on failure or disposal.

The independent blast halves signed city sizes with the specified rounding,
retains units and aggregate population, and visits wrapped aliases separately.
Noncity land uses the required primary draws and farmland branch, remembers
damage without exploring, and calls actual W05 pollution placement. That shared
primitive supports city/ocean coordinates, skips existing pollution and retains
the signed16 counter. Local play needs no network batching.

Seven groups cover the service joins and failures, including intercepted attacks,
stack ownership, real nested elimination, private decisions, byte/signed arithmetic,
feature memory and P10 semantic completion before live target lookup. Full
movement/combat/device dispatch, meltdown, desktop presentation and generated-game
acceptance remain open. Browser and cross-platform work remain deferred.

Root/App formatting and verification, both Release builds and the 43-project
boundary check passed. Native tests passed **918 of 920**, with the two existing
platform/audio skips. The probe passed **885 shared groups** across 108 coverage
families; source identity verified **528 compiled inputs**, now including U07's
owning specification. Eight identity and three coverage validator tests passed.
No new desktop UI, browser or cross-platform acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-nuclear-native.json` | `127b06f8d1a5939f6d7295e5849299d40fe6f4ab151c2296dfa1e56960e1e1de` |
| `m4-nuclear-identity.json` | `bdc74ccc2335592e30f6629419876fd8051d563ec19b60802f3a9227dd3070f1` |

## Planted nuclear-device mission

V06's device helper now runs the actual capture checks and U07 nuclear action
with SDI disabled, then applies identification through V04 attitude and bilateral
War setters. Autoplay skips the entire body. Every other actor receives the
confirmation, and only reply1 proceeds. Palace adds a fourth capture check; loss
at any check ends without an explosion. Surviving capture checks can relocate
and exhaust the Spy while retaining the target city.

Identification reads the live actor government and unit Veteran flag after the
explosion, including a backed unit record whose token U07 cleared. It scans
civilizations1…7 except the actor without alive/contact gates. Fundamentalism
skips that draw. The final local notice is unconditional on the admitted branch;
there is no final escape, movement increment, promotion or generic espionage
incident. Network notifications remain outside this local operation.

Both the capture and nuclear children retain actual nested V01 elimination and
foreign-mission incidents. Their decisions complete before the parent's later
blast or identification/notice. The session continuation routes confirmation to
the actor and each incident to its own victim, including a third party caught
in the blast. Cancellation of confirmation leaves the mission unexecuted;
failures after admitted effects retain their prefix and cannot restart.

Six groups cover these actual joins, SDI bypass, every capture failure position,
relocation, retained movement, hotseat attitude gating, inactive actor backing,
private session decisions and terminal failure/disposal. General city admission
and fallback dispatch, movement/combat joins, full desktop presentation and
generated-game acceptance remain open. Browser and cross-platform work remain
deferred.

Root/App formatting and verification, both Release builds and the 43-project
boundary check passed. Native tests passed **924 of 926**, with the two existing
platform/audio skips. The probe passed **891 shared groups** across 109 coverage
families; source identity verified **531 compiled inputs**. Eight identity and
three coverage validator tests passed. No new desktop UI or platform acceptance
is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-device-native.json` | `348f9c07ce4b640b19f8d391f6534ae5bf8ebb8761b19a509a0ccfa46d2e91a6` |
| `m4-device-identity.json` | `ed57f2ef24cf2fa1f0f682ce3fecd850589ea2a90f9826a5ed10699a016d4b31` |

## Civil war and elementary city transfer

V01's local civil war now uses the actual civilization-creation service after
selecting the highest zero-city/zero-military slot and clearing its alive bit.
NoSchism is checked even when events are not marked loaded; every matching
record reaches the required action dispatcher and vetoes regardless of its
consumed state. The predicate preserves wildcard operands. Full authored action
execution remains a required scenario integration, not a supplied success value.

Civil war copies only the specified treasury, military-strength and research
fields, preserves global knowledge-summary distinctions, and copies exploration
without remembered features. Region partition uses captured signed population
sums; fallback selection retains the capital position and original population
target, reveals both viewers each scan and transfers the first farthest city.
Each fallback transfer yields bounded progress without restarting the split.

C01's elementary transfer updates explicit old/new city and population summaries,
city owner and occupant owner. It leaves capture policy, flags, units and knowledge
to the parent. The civil-war unit pass uses actual physical stacks, can transfer
mixed-owner members, repairs homes only as specified and does not invent active
count repairs. C09's shared central-city query installs replacement Palaces while
retaining the original Palace; its treasury query preserves both rounding orders
and division boundaries for the later capture and barbarian callers.

Six groups exercise the actual joins, region/fallback partitions, multi-Palace
selection, candidate-parent aliasing, human creation refusal, signed16 transfer
wrap, explicit mismatched old owners, failure/disposal and session continuation.
C09 capture/incitement, full scenario actions and desktop presentation/generated
games remain open. Browser and cross-platform work remain deferred.

Root/App formatting and verification, both Release builds and the 43-project
boundary check passed. Native tests passed **930 of 932**, with the two existing
platform/audio skips. The probe passed **897 shared groups** across 110 coverage
families; source identity verified **538 compiled inputs**, including C09's
owning specification. Eight identity and three coverage validator tests passed.
These are service-integration results, not desktop UI or complete-game acceptance.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-civil-war-native.json` | `dd65eae1f1d593167d49d1182172102e01171653cdb37d1c67fd3c8d85883180` |
| `m4-civil-war-identity.json` | `d0453df61c52372025ca063652cb19a40398537f29eba6655f582878979222c7` |

## Capture population and supported-unit loss

C09's population helper now implements exceptional size-one preservation and
signed-byte decrement, deleting only exact zero through the actual C01 service.
The barbarian regional check uses the parent's captured center; membership,
known-to and power-rank inputs remain distinct from alive state. The stored
weakest-human selector is used as a civilization index, without recomputing it.
The owning capture parent still decides whether mode/restoration requires this
helper; no such gate is invented inside it.

C01 deletion now exposes its real unit-loss/check sequence to the resumable
V01 lifecycle. Nested last-settler loss and interrupted foreign missions complete
before the selected city's token, routes, wonders, spatial cleanup and final
removal publication. The synchronous C01 entry still runs the same sequence
through its existing callback contract.

The shared supported-unit traversal has C01's all-owner path and C09's explicit
old-owner filter. It preserves fixed Partisans, rehomes a lone AI defender even
inside another owner's city, performs actual cargo-aware loss and restarts its
descending scan against the changed unit bound. Capture's city, production and
other-owner supported units are retained by that filtered operation.

Six groups cover the gates, signed sizes, zero deletion, cargo and defender
behavior, actual nested incidents, private session decisions and failure/disposal
prefixes. Full capture admission, plunder, takeover, production, partisans and
desktop/generated-game integration remain open. Browser and cross-platform work
remain deferred.

Root/App formatting and verification, both Release builds and the 43-project
boundary check passed. Native tests passed **936 of 938**, with the two existing
platform/audio skips. The probe passed **903 shared groups** across 111 coverage
families; source identity verified **541 compiled inputs**. Eight identity and
three coverage validator tests passed. The existing C01 deletion groups also pass
through the shared traversal. No desktop UI or complete-game acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-capture-loss-native.json` | `054a69e5aaac6a5d1f6fe4be25a36b92a28307cc615b9d6af702123e43003052` |
| `m4-capture-loss-identity.json` | `6a8f0b0e813449c1d81ed297e7f69ede06a3c24a3acfcf43b019745da168d077` |

## Capture production and partisans

C09's surviving-city continuation now runs actual supported-unit loss before
setting disorder and repairing production. AI owners reset and count production,
then invoke actual C06/A02; humans use C06 eligibility and stop at the first
eligible defender in the descending fallback scan, including nonpositive scores.
The declared nominal candidate62 role is skipped without inventing a 63rd rule.
Human direct assignments preserve stock/counts. Neighbor exploration uses the
captured center and leaves remembered features unchanged.

The human path requires a P06 city-window decision with CityProcessing held true.
The session adapter routes nested victim incidents and the owner's window
separately; reply, cancel and timeout close the window, while failure/disposal
retain the committed flag. The parent still supplies the actual desktop window.

The partisan helper retains post-loss count and veteran threshold, scans the
ordered radius with the exact city-or-unit owner query and signed terrain/feature
score, and creates fixed Partisans through actual U01/W03. It preserves flags,
marks only the returned unit seen, and keeps the latest allocation result for
the notice, including null after a later failure. Cosmetic delay is not game state.

Seven groups cover actual children, nested incidents, the first-defender rule,
count wrap, window routing/failures, partisan placement/topology/ties, technology
and notice gates, no-draw failures, retained inputs across a real W03 meeting,
and nullable last-result notices. Full capture/incitement admission, plunder,
takeover, event ordering and desktop/generated-game integration remain open.
Browser and cross-platform work remain deferred.

Root/App formatting and verification, both Release builds and the 43-project
boundary check passed. Native tests passed **943 of 945**, with the two existing
platform/audio skips. The probe passed **910 shared groups** across 112 coverage
families; source identity verified **545 compiled inputs**. Eight identity and
three coverage validator tests passed. These service tests do not establish P06
desktop rendering or complete-game acceptance.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-capture-continuation-native.json` | `1349dd8549675dd19597cb44f455b306ed78fecbf45fa1912ecd545d21476bec` |
| `m4-capture-continuation-identity.json` | `3c18ade2d8d32889aaf946ce3b6dcece9ec5c7a28202368a6b9529e90be32b72` |

## Ordered local capture and incited transfer

The C09 parent now preserves the specified order across actual V04 force-war,
V01 civil war and replacement selection, Palace escape/payment or ship loss,
wonder notices/Eiffel, retained plunder, city reset/building parity, population
loss/deletion, radius takeover and the required CityTaken dispatcher. The city
owner is assigned directly as specified; no extra C01 transfer/census adjustment
is inserted. Captured center and name remain separate from live event/window
coordinates and names.

The tail remembers the center, makes local capture/observer announcements,
performs actual V03 technology capture even after city deletion, then joins
supported-unit loss/C06 production, the required P06 city window, actual V01
elimination, partisans and founder/age/Leonardo finalization. Palace decisions,
technology selection, nested victim incidents and window closure each consume
only their own replies. Palace escape scores whole stacks and ordered facilities,
retains the slot-zero exclusion, debits fresh gold without rechecking affordability
and clamps the earlier plunder against the post-payment treasury.

The incited wrapper reveals/remembers the captured radius, transfers the admitted
nearby old-owner units with exact byte-count/home/order changes, then invokes the
parent with freshly captured city inputs. It does not recharge the V06 mission.
Semantic announcements retain the supplied numeric argument for ally/general
messages and preserve the Palace/wonder changes to that argument.

Nine groups exercise real children, restoration/parity, mutation boundaries,
Palace scoring and live-payment quirks, observer/wonder gates, deletion followed
by theft, real nested foreign loss, partisan and Leonardo tails, incited-unit
admission, failure/disposal and private session decisions. Required CityTaken
matching/actions and P06 desktop rendering remain explicit integration boundaries.
Movement/V06 admission and generated complete-game acceptance are still open;
browser and cross-platform work remain deferred.

Root/App formatting and verification, both Release builds and the 43-project
boundary check passed. Native tests passed **952 of 954**, with the two existing
platform/audio skips. The probe passed **919 shared groups** across 113 coverage
families; source identity verified **549 compiled inputs**. Eight identity and
three coverage validator tests passed. These results verify local service
composition, not desktop UI or complete-game acceptance.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-city-capture-native.json` | `59b4a9dc3aad23a6160aa7e4f3ed14ab6c7f78b736eb472137718228dd18660c` |
| `m4-city-capture-identity.json` | `35c7a2555f1d0b27de72608c53f020ef0425e25b6a2bdd454c4b11e29ea41f47` |

## Revolt mission and paid subversion

V06's revolt body now uses actual capital distance from the actor's live position,
victim-government/Courthouse distance changes, signed price and ordered discounts.
The captured Spy classification and live veteran flag remain distinct. Democracy,
near-capital and AI double-cost/treaty admission retain their separate end/fallback
behavior; the returned theft fallback still requires the city controller.

Human offers read all four DissidentOptions lines through the existing classic
reader, close it before the decision and preserve resource-open failure as an
end. Truncated backing stops explicitly. Unaffordable offers do not read options
and remain closable; the session supplies only a close action for that dialog.
The content adapter supplies the real filename/search context, without inventing
a game-file location inside the core service.

Every positive reply pays the cached cost times that reply from live gold and
increments subversion before actual escape. The escape uses victim0 and retains
nested lifecycle incidents. Its loss result does not cancel or refund payment.
Choice1 then runs the real explicit espionage incident, including senate and
government effects; choice2 sets reverse Vendetta and founder before C09; other
positive replies keep their distinct path. The complete C09 incited transfer
runs afterward with its own Palace, technology and city-window continuations.

Seven groups cover prices and the 15000 double-option limit, authored option
reading/closure, missing/truncated resources, AI/autoplay paths, custom/live-funds
payment, no-refund death, nested foreign loss, Republic-to-Anarchy before capture,
and actor/victim/window routing. Required city-controller dispatch, full content
and dialog binding, desktop/generated-game acceptance remain open. Browser and
cross-platform work remain deferred.

Root/App formatting and verification, both Release builds and the 43-project
boundary check passed. Native tests passed **959 of 961**, with the two existing
platform/audio skips. The probe passed **926 shared groups** across 114 coverage
families; source identity verified **553 compiled inputs**. Eight identity and
three coverage validator tests passed. Existing capture session cases also pass
through the shared presentation helper. No desktop UI or complete-game acceptance
is claimed from these service/content/session checks.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-incitement-native.json` | `a0d5f8274d81a50baeefe858dd0417ff7287e8b100abfd173a4e5bdd4e50e0b1` |
| `m4-incitement-identity.json` | `0c1b88680567bbb3635af47320d3c22eb5c805b5e90eba4ea53ee5e4ec626f4b` |

## Local city mission controller

`CityMissions` now composes the V06 local entry and all seven ordinary mission
bodies. It establishes bilateral Contact, reads actual classic SpyOptions text,
filters the ordinary human menu and applies the ordered AI/nonlocal/autoplay
selector. Menu gating is distinct from accepted custom replies. Every dispatch
publishes LastMission before its real child; fallbacks preserve the original
actor, victim, type and Spy classification and yield without rebuilding the menu.
The session adapter routes actor choices, victim incidents, captured technology
and required P06/P07 visits, including incitement through the actual C09 parent.

Six groups cover resources and menu gates, ordered fallback selection, retained
context, real mission effects, multi-stage AI fallbacks/failure prefixes and
actor/victim/capture privacy through CommandRuntime. Network Chat is not offered
by the local entry; a custom Chat reply stops explicitly after LastMission.
Movement admission, full desktop content/dialog/window binding, generated-game
integration and complete-game acceptance remain open. Browser/cross-platform
work remains deferred.

Root/App formatting and verification passed; both Release builds passed without warnings or
errors. Native tests passed **965 of 967**, with the two existing platform/audio
skips. The probe passed **932 shared groups** across 115 coverage families;
source identity verified **557 compiled inputs**. The 43-project boundary check,
eight identity validator tests and three coverage validator tests passed.
These results do not claim desktop UI or complete-game acceptance.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-city-missions-native.json` | `ac59b7d8d3958127b0ba56492d5c3039b45117655002a75ea3e5724ab06a45da` |
| `m4-city-missions-identity.json` | `c2c4a5db024e91e1893e42dc8680c9d392c8afd2b6c782720bef1fdf815d869c` |

## Ordinary movement charge and AI gates

The desktop land-step command now uses `MovementCharge` and the shared actual
step cost. This replaces its inline partial-movement calculation and composes
U02's complete charge phase: conditional lottery/exhaustion, execute-clear
continuation, byte charge, and ordered AI approach, reversal and destination
crowding stops. The latter use actual movement allowance, geometry, visibility,
city-aware control and validated stacks. A stop retains its charge and does not
pretend that relocation occurred or that dispatcher cleanup has run.

`SpatialControl.AdjacentNonalliedCivilization` supplies the ordered city-mediated
land/sea query. `UnitMovement.StepCost` retains captured domain and destination
terrain while reading current map features, Alpine ability and rule costs.
Six groups cover medium changes, directed alliances, signed/captured cost inputs,
wrapped rivers, lottery mode combinations, AI thresholds and precedence, real
stack populations and the joined local land-step outcomes. Mission/combat and
transport admission, full movement publication/arrival/finish/cleanup and
generated desktop games remain open. Browser/cross-platform work is deferred.

Root/App formatting and verification passed. Both Release builds passed without
warnings or errors. Native tests passed
**971 of 973**, with the two existing platform/audio skips. The shared probe
passed **938 groups** across 116 coverage families; source identity verified
**559 compiled inputs**. The 43-project boundary check, eight identity tests and
three coverage validator tests passed. The independent X11 application input
driver was not run because Xvfb is unavailable on this host's PATH. These
service/session results do not claim new UI or complete-game acceptance.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-movement-charge-native.json` | `4746f4343e3b2c5a3b21f9ec7b6be2122c10ba4bace08430b075f1a58875619c` |
| `m4-movement-charge-identity.json` | `f602918883f5470868661c040ef6bb886cd8bae0a74cd5437d9ec23049a11f4b` |

## Transport and special movement continuations

U05's local transport and finish operations now compose actual U01 cargo and
stack services, U02 allowance and U04 selectability. Landfall retains the shared
unit bound and answer, chooses the first eligible passenger and requests a
movement restart after confirmation. Boarding performs real gather passes,
unloading uses the last selectable awakened land unit, and port/exploration
wakeups retain their distinct gates and cargo links.

Aircraft basing uses the captured destination and actual physical stack, then
applies the range-one attack exception, fuel reset/exhaustion or signed byte
counter rollover. Crashes run basic deletion. Coast-bound processing preserves
conditional Lighthouse/technology checks, random odds before the coast scan and
shared work state; sinking runs actual cargo-aware deletion. The lifecycle now
exposes that child without the separate destruction/redraw wrapper. Both paths
can suspend on a nested last-Settler/foreign-spy incident before final notices.
Session adapters return restart/stop and route the actual decision recipient.

Twelve groups cover selection/refusal, live cargo effects, wakeup gates, basing,
fuel and odds boundaries, topology, nested loss, cancellation and failure/disposal.
U02's full sequence, desktop movement controls/presentation, player airlift and
paradrop, combat and generated complete-game acceptance remain open. Browser and
cross-platform work remains deferred.

Root/App formatting and verification passed, as did both Release builds without
warnings or errors. Native tests passed **983 of 985**, with the two existing
platform/audio skips. The shared probe passed **950 groups** across 117 coverage
families; source identity verified **565 compiled inputs**. The 43-project
boundary check, eight identity validator tests and three coverage validator tests
passed. No new desktop UI or complete-game acceptance is claimed here.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-transport-native.json` | `f645c845ee128389de959023b9effc97dfe7a4b3e751af2bff19a81d4e999074` |
| `m4-transport-identity.json` | `a72b9ac9520b0d2fd415871f2d81e9c1b09746eecb2c157c00499a69c917331b` |


## Player airlift checkpoint

U05 player airlift now has a local command continuation and a direct transfer
executor. Destination selection retains the actor, origin and owner, includes
used destination airports and checks their used state after selection. The
threat scan uses actor-to-fighter War, endpoint reach and veteran risk without
extra domain, visibility, fuel or movement gates. Acceptance consumes one primary
remainder test per risk point, stopping at the first interception. AI production
airlift retains its separate policy and secondary draws.

Execution marks both airports, exhausts and clears orders, invokes actual U01
single-unit relocation, and notifies the city window before interception.
Actual basic deletion can suspend for V01/V06 decisions after a last-Settler
loss; the session adapter routes that victim independently of the airlift actor.
Notices use live observer/autoplay gates and retained last-fighter identity.
Failures and cancellation preserve the specified committed effects.

Six shared groups cover command admission, retained selection, fighter direction
and reach, interception and bounded continuation, actual nested loss/session
routing, and failure/disposal. The direct-transfer elimination case uses foreign
airports, allowing the real V01 city scan to proceed to the last-Settler check.

P05 desktop command/menu binding, paradrop, full directional movement and the
remaining mechanics/AI/complete-game acceptance remain open. Browser and
cross-platform qualification remain deferred. This checkpoint establishes no
new desktop UI or complete-game acceptance.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **989 of 991**, with the two existing
platform/audio skips; the shared probe passed **956 groups** across 117 coverage
families. Identity verification checked **568 compiled inputs**. The 43-project
graph check, eight identity validator tests and three coverage validator tests
passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-airlift-native.json` | `6a202e16dd5686b8eb5f6dacc24b8f4f509012f295892fa255ac2438cd6c2f9f` |
| `m4-airlift-identity.json` | `a9592c4c0acd52fdabbbeb8abbd7d2dafb54e1be2521f2414de2a798d7a6583e` |


## Paradrop and attack-permission checkpoint

V04 senate and attack confirmation now consume the retained turn random,
directed sneak-attack/reputation state and actual active UN queries. Protected
relations, warning priority, exact reply-one acceptance and live post-warning
senate checks are preserved. Permission does not declare war or change treaties.

U05 local paradrop now distinguishes command entry from direct target execution,
then joins actual V04 confirmation, U01 cargo lift/stack visibility/placement,
C09 capture and W03 revelation. It preserves approach samples and ties, captured
tile ownership, human movement, AI capture exhaustion and the continuing actor
reloaded after capture. Session decisions route the attack warning and real
capture choices/window. No gameplay child is replaced with a successful default.

U05 records X01 repairs for unwritten presentation values, absent nearest-city
names/approach coordinates and missing active/continuing actors. Viewers outside
1..7 have no local announcement/animation; absent city names remain absent and
missing approaches suppress animation. Ordinary movement/draw/capture behavior
is preserved, without requiring arbitrary original stack values.

Ten shared groups cover senate/permission, direct versus command admission,
treaty cancellation, viewer/approach behavior and repairs, flat/wrapped sampling,
actual cargo and revelation, capture/session suspension and replacement actors,
human versus AI capture (including damaged exhaustion before healing), and
failures/disposal. P01/P05 desktop controls, the
full U02 dispatcher, remaining mechanics/AI/turn joins and complete-game
acceptance remain open. Browser and cross-platform work remains deferred.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **999 of 1001**, with the two existing
platform/audio skips. The shared probe passed **966 groups** across 117 coverage
families, and source identity verified **574 compiled inputs**. The 43-project
boundary check, eight identity validator tests and three coverage validator tests
passed. No new desktop UI or complete-game acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-paradrop-native.json` | `74a623b75145a39a51752de30051c76f7b3971cdbef4b4d852d37f3bad1846af` |
| `m4-paradrop-identity.json` | `46e9241e7b73fc476369c6ddf42a903b5c347ae0c4d6a110b6f60b8f9c6e1e87` |


## Combat preparation checkpoint

U06 now provides signed attack/defense strengths, validated defender selection,
common preview/execution preparation and preview scoring. Cached terrain/city
and defense factor remain explicit; selection uses health weighting, capability
preferences, later ties and the supplied-unit fallback. Actual U01 stack, U02
movement/HP, C08 protection and A01 standing-goal services supply the children.

Preparation preserves city-slot-zero exceptions, ordered coastal/SDI/SAM and
fighter effects, signed firepower and special-unit modifiers, barbarian policy,
and the distinction between preview and movement-scaled execution inputs.
Preview consumes no combat draws, but retains shared notice/cache changes and
submarine goal replacement. Seven groups cover these contracts and failure
prefixes. U06 is now included in the compiled source-identity inputs.

This does not execute a fight: movement charge and diplomacy, damage rounds,
promotion, casualty/event/lifecycle chains and aftermath remain open, as do the
U02/A04 callers, desktop combat and complete-game acceptance. Browser and
cross-platform qualification remain deferred.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1006 of 1008**, with the two existing
platform/audio skips. The shared probe passed **973 groups** across 118 coverage
families, and source identity verified **577 compiled inputs**. The 43-project
boundary check, eight identity validator tests and three coverage validator tests
passed. No new desktop UI or complete-game acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-combat-preparation-native.json` | `6c0e52f58d1360745518fa932a9b4543535a5443053a42bf592f940275967381` |
| `m4-combat-preparation-identity.json` | `8ab57e5a454d3ae2998c96398fbb01e6a16f1cb5619d5d0a398d979d69b21793` |


## Combat rounds and promotion checkpoint

U06 damage rounds now consume captured preparation values, preserving separate
attack/defense primary rolls, first-pair defender ties, conditional defensive
rerolls and second-pair attacker ties. Hits retain signed firepower and byte
wrapping. HP checks, missile prelude, interval-triggered publication/explosion
and post-explosion HP/redraw ordering use the actual unit records.

Round operations yield after finite batches and retain damage and semantic
choice ordinals. The U06 contract documents this scheduling boundary so custom
zero/negative firepower cannot monopolize a call. A yield does not invent an
outcome, and disposal/failure preserves applied hits. Promotion updates captured
owners' aggression counters, samples even for veteran/missile winners and uses
the winner's live owner for actual C08 Sun Tzu protection and owner-addressed
human notices.

Seven additional shared groups cover sampling extremes, asymmetric ties,
rerolls, captured/live values, finite yields, normal and single-round HP effects,
negative/wrapped damage, presentation failures, promotion and wonder eligibility.
Actual preparation is joined to ordinary rounds. The parent still needs movement
charge/diplomacy, result presentation before promotion, real casualty/event and
city consequences, aftermath, and U02/A04/session/desktop integration. Complete
human/AI games remain open; browser/cross-platform work is deferred.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1013 of 1015**, with the two existing
platform/audio skips. The shared probe passed **980 groups** across 118 coverage
families, and source identity verified **579 compiled inputs**. The 43-project
boundary check, eight identity validator tests and three coverage validator tests
passed. No new desktop UI or complete-game acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-combat-rounds-native.json` | `7f51910a894055261e1381f15141c26e7354aaf9d70c96fed1bb219ad4c4770f` |
| `m4-combat-rounds-identity.json` | `daadf8c35b5a7ecc2a4c5245d53c64cffbfc9649e5887e7f3bef980dbbacf4bb` |


## Combat casualty checkpoint

U06 single and whole-stack casualties now saturate per-type loss totals, write
world-owned history, increment the wrapped combat kill count, dispatch loaded
UnitKilled before deletion and invoke actual U01 basic deletion through V01's
resumable lifecycle. Whole-stack processing retains each raw next link before
callbacks/deletion and yields between casualties. Events may replace a retained
slot; deletion and subsequent traversal preserve that specified behavior.

History uses 300 allocated entries with at most 299 counted entries in ordinary
operation. Owner and next-index reads are repeated after color/name queries;
field writes and failures retain their prefixes. Names follow the repository's
managed-text contract; classic width/termination remains an adapter boundary.
Fresh worlds receive empty independent rings. The session continuation privately
routes real nested last-settler/espionage decisions without repeating accounting.

Six shared groups cover ring wrap, signed fields, fresh isolation, live queries,
loss saturation, kill-count wrap, event replacement, captured-next traversal,
actual nested lifecycle decisions, session privacy and failure/disposal prefixes.
S02 action execution and content/presentation queries remain required owning
boundaries. Result presentation, loser grouping, defender-city consequences,
aftermath and the full movement/AI/desktop combat caller still require work;
complete human/AI-game acceptance remains open. Browser/cross-platform work is
deferred.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1019 of 1021**, with the two existing
platform/audio skips. The shared probe passed **986 groups** across 118 coverage
families, and source identity verified **583 compiled inputs**. The 43-project
boundary check, eight identity validator tests and three coverage validator tests
passed. No new desktop UI or complete-game acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-combat-casualties-native.json` | `9003ce5f43d32af7c859321dcccbf80c225dbe162c6789446fd4383ecc57fe39` |
| `m4-combat-casualties-identity.json` | `51e95fd4473812ec71440f15f0fa46ab487dddc78b458dd4da32e749b9adc218` |


## Post-round combat resolution checkpoint

U06 now joins result presentation before promotion/deletion, actual U01 cargo
grouping and casualty continuations, protected defender stacks, city population
loss and real C01 removal/V01 elimination. It preserves the captured origin's
medium, walls/Great Wall/Chieftain exceptions, byte population wrap, knowledge
updates and retained treaty/ally notice behavior. Surviving exposed AI cities
run actual C06/A02 production reset for the target's raw region.

A05 exposed-city retasking now scans live AI capacity units, preserves directed
Peace and strict weighted-distance gates, and uses full damage-adjusted movement
allowance. It preserves tasks, counters and spent moves, including the retained
city-slot call after deletion. A05 is included in compiled source identity.

Ten new shared groups cover presentation order/sounds/viewers, actual rounds
through casualties, cargo and protected stacks, population/deletion policy,
actual AI defense production, stale-city retasking, movement/topology gates,
nested last-settler decisions and committed failure/disposal effects.

This stage returns captured winner/loser and ransom/city information. It does
not grant the ransom reward or finish an attack: pre-round diplomacy/viewer
setup, final notices/rewards and surviving-attacker handling, full U02/A04/session/
desktop callers and complete human/AI-game acceptance remain open. Browser and
cross-platform qualification remain deferred.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1029 of 1031**, with the two existing
platform/audio skips. The shared probe passed **996 groups** across 118 coverage
families, and source identity verified **587 compiled inputs**. The 43-project
boundary check, eight identity validator tests and three coverage validator tests
passed. No new desktop UI or complete-game acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-combat-resolution-native.json` | `b79c18daed621400b76e3e32ef04ab09b9726c65ac6e0eb9a79e1760e0dc92ad` |
| `m4-combat-resolution-identity.json` | `533d7297063cf7e1b9f852f98c3e756dd0322cd7eae95fbe14d9096146a82a1e` |


## Combat charge and diplomacy checkpoint

U06 entry now charges the live road multiplier before real V04 confirmation,
retains that charge on cancellation or alliance refusal, and performs captured-
owner treaty handling and V04 force-war. Sneak-attack policy uses primary
sampling, effective United Nations checks and actual restricted A08/V02
government changes. Notices are addressed to the human defender. Execution
strength changes preserve their order and signed arithmetic. Preparation now
clears aggregate treaty/ally and explosion inputs when beginning a new preview
or execution context.

Eight new shared groups cover charge wrap/resumption, confirmation and alliance
changes, sneak/ceasefire branches, government thresholds/locks/wonders, retained
force-war results, directed ally warnings, invalid observer and failure prefixes,
and composition from entry through actual rounds and post-round resolution.

U06's viewer section names a scenario combat-visibility option without mapping
its selector/mode gate. That gap is recorded explicitly; no alias to
WholeMapReveal is inferred. Viewer/start presentation, nuclear delivery, final
notices/rewards and surviving-attacker handling, full movement/AI/session/desktop
callers and complete human/AI-game acceptance remain open. Browser and
cross-platform qualification remain deferred.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1037 of 1039**, with the two existing
platform/audio skips. The shared probe passed **1004 groups** across 118 coverage
families, and source identity verified **589 compiled inputs**. The 43-project
boundary check, eight identity validator tests and three coverage validator tests
passed. No new desktop UI or complete-game acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-combat-entry-native.json` | `14ee158826e6298b34c561c6c8a7ec8e399218c3e1c788567718a45d322f1891` |
| `m4-combat-entry-identity.json` | `c45be1bbe92825655615d4b8ad4ed8e8d1f12da874c6fc6a4f8fd1ab9680e2a7` |

## Combat opening checkpoint

U06 now builds local viewer eligibility from alive/human membership, captured
participants and live visibility/ownership. Opening publishes actual U01
single-unit knowledge, lifts the attacker's cargo group and restores it to the
captured origin. Semantic notices use C01 nearest-city lookup, custom installed
names, explicit retained text, facility/SDI branches and autoplay policy.
Classic sound identities preserve fixed-slot priority and deferred explosion
selection. Sound arguments can now be absent.

P10 permits a batched observer presentation rather than original per-view draws
and delays. Skipped rendering retains the same gameplay; renderer exceptions
are returned for diagnostics and do not cancel combat. Whole-group restoration
also runs after content preparation failures. Eight shared groups cover viewer
and authority boundaries, real cargo/knowledge effects, notice and sound vectors,
and opening through actual rounds/resolution with skipped or failed rendering.

The scenario combat-visibility selector remains unresolved: a caller must supply
its explicit resolved value if ordinary knowledge does not establish visibility
in a scenario. No alias to WholeMapReveal or silent fallback is introduced.
Nuclear delivery, final combat effects/surviving-attacker handling, general
movement/AI/session/desktop orchestration and complete-game acceptance remain
open. Browser and cross-platform work remain deferred.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1045 of 1047**, with the two existing
platform/audio skips. The shared probe passed **1012 groups** across 118 coverage
families, and source identity verified **591 compiled inputs**. The 43-project
boundary check, eight identity validator tests and three coverage validator tests
passed. No new desktop UI or complete-game acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-combat-opening-native.json` | `c0be25ec532ce1bac660b844cbd6d1b6422e1665a2a7da185f5564613e0350f7` |
| `m4-combat-opening-identity.json` | `e26a5bcb6289d47ef835f4419ef7300b7d6a8788cba6a946e5fe9058318e41c4` |

## Local path search and attack response checkpoint

U03 ordinary local search now shares the bounded FIFO engine used by generation,
acceleration and road queries. It preserves the goal-only cache, reverse terrain
costs, occupancy rejection, actual stack/adjacency and peace penalties, coast and
naval-region gates, strict limits and deterministic direction ties. Selection
retains its different stack multiplier and pre-normalization seam window.
Bounded distance queries retain selected rules/cache and restore owner on normal
return. Unassigned owner scratch for negative-owner nonuniform selection remains
an explicit required input; no convenient original-stack value is assumed.

A04 immediate response now invokes real U03 directions and the actual resumable
U05 paradrop. The first eligible paratrooper ends the response even after refusal.
Otherwise raw target ownership/region and ordered admission select owned/allied
AI ground units; positive non-Hold directions assign Goto and the response task.
Direction zero leaves orders unchanged. Path modes, counters and spent movement
are retained. A04 is now included in compiled source identity.

Nine shared groups cover path costs/cache/bounds, actual penalty dependencies,
response admission/modes/directions and successful/refused/pending/failing drops.
Full tactical planning, coarse waypoints/general goto, combat/session/desktop
callers and complete human/AI-game acceptance remain pending. Browser and
cross-platform work stay deferred.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1054 of 1056**, with the two existing
platform/audio skips. The shared probe passed **1021 groups** across 120 coverage
families, and source identity verified **596 compiled inputs**. This includes a
negative terrain-cost corridor whose bounded FIFO result is derived from its
three-removal cycle. The 43-project boundary check, eight identity validator tests
and three coverage validator tests passed. No new desktop UI or complete-game
acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-response-native.json` | `11c09244dd3cb0dd36090643b51b90109284cf8edaf09e7b99ea32eae65f62a0` |
| `m4-response-identity.json` | `794a53285bde31c18b557f65e58e79c107e42857b7dd8c94ac0421a6ad2e625c` |


## Combat survivor and nuclear-delivery checkpoint

U06 survivor handling now captures the current shared moving actor, exhausts
eligible aircraft and deletes consumed units through actual resumable U01/V01.
The AI-city branch retains signed fuel-counter wrapping and calls real A04
response; reaching the range threshold returns without inventing an immediate
crash. The response may run actual U03 search or U05 paradrop/C09 capture and
replace the moving unit.

Nuclear delivery uses the live signed attack gate and actual U07 with SDI checks.
Interception deletes the original attacker and redraws its origin. Detonation
resets the captured attacker's aggression toward the defender and may invoke AI
response. Both return zero without ordinary rounds, promotion or casualty history.
Nested last-settler/spy decisions suspend before later blast population changes.
The session adapter routes those and actual paradrop/capture questions privately;
cosmetic redraw failures remain diagnostics without aborting gameplay.

Eight shared groups cover replacement actors, signed counter/range boundaries,
real SDI/blast/lifecycle consequences, pending decisions, failure/disposal and
private routing. Integration joins entry, opening, rounds and resolution to the
survivor stage; another case reaches a real palace-escape prompt through A04.
Final rewards/notices, V05 ally help, full combat/desktop commands and complete
human/AI-game acceptance remain open. Browser and cross-platform work stay deferred.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1062 of 1064**, with the two existing
platform/audio skips. The shared probe passed **1029 groups** across 120 coverage
families, and source identity verified **599 compiled inputs**. The 43-project
boundary check, eight identity validator tests and three coverage validator tests
passed. No new desktop UI or complete-game acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-combat-continuation-native.json` | `383a81a52f5b255907995855b90a243ef1249c4b9e1f5551aca97928e34d6876` |
| `m4-combat-continuation-identity.json` | `6dd6054667ea1025e4ecca228d727b75b34ba9568355cdce9c45f0dc6adaeee5` |


## Emissary reception and ally-help checkpoint

V05 reception now preserves autoplay/prior-response gates and shared meeting
state, invokes real A08 evaluation, and distinguishes voluntary from nuclear-forced
reception. Intelligence availability uses actual embassy/UN/Marco Polo queries;
the private report can close and return to a freshly evaluated reception choice.
Greeting data carries installed names, title, gender, attitude category, the
secondary random variant and nuclear information. Reset clears only the owned
meeting fields, preserving other evaluation state.

Ally help now captures the local human, applies directed admission, and runs that
actual reception. Declining the initial demand calculates regional threat over
regions 1 through 62 and may offer payment in units of 50 gold. The price remains
fixed while the player decides; acceptance updates live treasuries and calls real
V04 mediated declaration. Refusal uses live evaluation attitude and can cancel
the alliance with actual cargo/unit expulsion. Admitted requests reset the meeting
on normal completion; rejected admission does not.

Eleven shared groups cover those branches, report repetition, nuclear flags,
negative treasury and wrapping, regional boundaries, retained recipients, real
war/expulsion, session privacy and cancellation. Cosmetic leader/camera/status
failures are reported as diagnostics without preventing accepted gameplay.
Required semantic notice failures remain terminal with prior effects retained.
The V05 specification is now included in compiled source identity. Full audience,
exchange and proposal controllers, research/contact and U06 final-notice joins,
saved pending negotiations and desktop complete-game acceptance remain open.
Browser and cross-platform work stay deferred.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1073 of 1075**, with the two existing
platform/audio skips. The shared probe passed **1040 groups** across 121 coverage
families, and source identity verified **605 compiled inputs**. The 43-project
boundary check, eight identity validator tests and three coverage validator tests
passed. No new desktop UI or complete-game acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-ally-help-native.json` | `d6a7553de6ec052a60909308c2ef38c07e14a15a9452d97673b4e0621357bf20` |
| `m4-ally-help-identity.json` | `62b0c11febc8467036213510f0bf7604dc3b941679180eccb14cf06a62e32aed` |


## Local combat execution checkpoint

U06 final effects now publish completed loss, show local multiple-casualty notices,
apply the barbarian leader reward, activate actual V04 allies and run treaty/ally
notices. Ally-under-attack invokes the actual V05 reception/help/payment/war
continuation before advisor refresh. Cosmetic redraw/info/advisor failures are
retained as diagnostics; required semantic publication failures remain terminal.

The local execution parent now connects actual preparation, precharged attack
permission, opening visibility/cargo presentation, nuclear early return or bounded
damage rounds, promotion/casualties/city consequences, final effects and the
current-moving-unit survivor continuation. No target returns 999, cancellation
retains the precharge and returns 1, and the both-zero-firepower path skips rounds
but still executes normal loss consequences. The parent retains each child across
suspension and forwards actual incident, help and capture decisions. Session
routing shares the existing private interaction adapters.

Nine shared groups cover the final effects and complete parent joins. They include
ordinary victory/defeat, finite round budgets, zero firepower, intercepted and
detonated nuclear delivery, actual barbarian stack losses/ransom, private nested
last-settler incidents and real ally-help payment/war after combat. Repeated calls
do not repeat charge, history or rewards. U02 movement admission/dispatch,
concrete S02 and presentation adapters, unresolved scenario visibility, desktop
input integration and complete human/AI-game acceptance remain open. Browser and
cross-platform work stay deferred.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1082 of 1084**, with the two existing
platform/audio skips. The shared probe passed **1049 groups** across 121 coverage
families, and source identity verified **609 compiled inputs**. The 43-project
boundary check, eight identity validator tests and three coverage validator tests
passed. No new desktop UI or complete-game acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-combat-execution-native.json` | `504f1c3b266bd9ed7f810218d97df26f33741b666d3c534b81b2831989bf6a48` |
| `m4-combat-execution-identity.json` | `e76963eb8485b4e8b02a6492637a4ca6b0cb15d07fb4d50618f93d49fbe54b5d` |

## Foreign-unit movement and expulsion checkpoint

U02 foreign-unit handling now evaluates ordered amphibious, submarine, diplomat,
zero-attack and long-range-aircraft gates, then runs actual expulsion, fatigue
admission and full U06 combat. Diplomat admission retains the complete human
civilization bit. Hidden-submarine discovery restores the original mover before
clearing orders and publishing visibility. Combat still executes when the outer
movement execute option is clear; the returned value is inverted as specified.
An executed nonzero combat result retains the shared finishing unit and routes
common finish back to the captured origin, without an ordinary terrain charge.

Expulsion composes actual U01/C08 queries, cargo lift/return and relocation.
Weighted nearest-city ties, strict size/Palace scoring, single-unit visibility,
notification precedence and actual V07 race/attitude consequences are retained.
Autoplay's unassigned choice is an explicit input; a changed-role path with no
captured destination city reports the specification boundary. Cosmetic entry
and redraw failures retain diagnostics and allow gameplay to finish; required
notices and missing random choices fail with their prior state changes retained.

Nine shared groups cover those branches, real combat loss and finish routing,
and private session expulsion/fatigue/attack decisions. The outer land/city
admission, movement dispatcher, common finish/cleanup, concrete desktop effects
and full generated human/AI games remain open. Browser and cross-platform work
stay deferred. This checkpoint does not claim desktop UI or complete-game
acceptance.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1091 of 1093**, with the two existing
platform/audio skips. The shared probe passed **1058 groups** across 122 coverage
families, and source identity verified **613 compiled inputs**. The 43-project
boundary check, eight identity validator tests and three coverage validator tests
passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-foreign-unit-native.json` | `36fce1c494bd923c0738614f8153b2aa0a499c8ac2a4c8ea911c12890e6d382a` |
| `m4-foreign-unit-identity.json` | `10c06006a66b91d4cab2b946c2d322ea98bc6a7c1a629b6589295863b88b6063` |

## Foreign-city movement checkpoint

U02's post-landfall foreign-city phase now resets the victim from the captured
city's live owner and uses captured domain plus live range/attack for admission.
Ineligible ships/aircraft stop with the specified notice gates. Nuclear preview
returns one without acting; execution invokes actual U07 with SDI. Interception
uses cargo-aware U01/V01 deletion, retains nested V06 decisions and redraws the
origin after loss. Successful detonation resets the captured pair's aggression.
Land and range-zero Air use the specified AI treaty denial and human prompt/local
gates, with actual V04 senate/attack permission and no independent war declaration.

Five shared groups cover signed boundaries, domain/range capture, prompt gates,
live city ownership, treaty/permission results, actual nuclear blast/SDI/cargo
loss, private last-settler incidents and retained failure effects. Session routing
preserves private decisions and repeated calls do not repeat completed loss.
Outer land checks, dispatcher, common finish/cleanup, desktop effects and full
generated human/AI games remain open. Browser and cross-platform work stay
deferred; no new desktop UI or complete-game acceptance is claimed.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1096 of 1098**, with the two existing
platform/audio skips. The shared probe passed **1063 groups** across 123 coverage
families, and source identity verified **616 compiled inputs**. The 43-project
boundary check, eight identity validator tests and three coverage validator tests
passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-foreign-city-native.json` | `ef192f654df792a056b93fd9dca98609701d9cefc440c95b75cea54b43605168` |
| `m4-foreign-city-identity.json` | `44139e4f51b5817f1c0b3f85ec109a3e729c024d2f05a6c5736f5551efe5d83f` |

## Caravan city entry and delivery checkpoint

C07 local city entry now applies autoplay, signed-home, AI ferry-port and owned
goto gates, retains the human menu choice and executes actual delivery or wonder
donation. Donation uses the live signed unit cost and raw shield-box rule, retains
short stock arithmetic, marks the decoded home dirty and invokes visible deletion.
It does not retest wonder eligibility after the retained choice.

Delivery composes actual U03 connection, V03 research cost, C03 cached mode,
diplomatic attitude, U01/V01 deletion and C07 route/commodity services. Ordered
distance, map flags, region, freight, facilities, repeated demand premiums and
advance reductions precede the sampled cap. Payment/food and unit loss precede
route attempts; rejection does not refund them. Food uses the incoming shared
food-row factor. Reciprocal selection consumes exactly three primary-law samples
and retains the last score tie. Missing reciprocal-name backing stops after the
completed route/payment/loss prefix. Cosmetic sound/status failures are diagnostic;
required notice failures remain terminal.

Seven shared groups cover arithmetic vectors, cap and wrapping, signed/unsigned
home behavior, retained food state, live donation inputs, reciprocal ties/names,
AI/autoplay, real route rejection and private nested last-settler incidents.
The session adapter retains entry and incident decisions without repeating money
or deletion. Cargo selection/report search, C06 production joins, U02 land checks,
desktop presentation and full generated human/AI-game acceptance remain open.
Browser and cross-platform work stay deferred.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1103 of 1105**, with the two existing
platform/audio skips. The shared probe passed **1070 groups** across 124 coverage
families, and source identity verified **619 compiled inputs**. The 43-project
boundary check, eight identity validator tests and three coverage validator tests
passed. No new desktop UI or complete-game acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-caravan-native.json` | `72f103bbc951b5724196744c5bfc1681f1ffc54d8c7b9ec945a484091fc2d94e` |
| `m4-caravan-identity.json` | `e8b8371c3fee298e0024da638b5e979a5570032809f04e8bdf54940c575639e0` |


## Land-entry composition checkpoint

The local U02 captured-Land phase now calls actual W03 ZOC/revelation, C07
caravan entry/delivery/donation, V06 city and unit missions, and C08 allied repair.
It retains captured destination facts, reloads only owner after a zero-result
unit mission, and restores the original mover at the specified boundaries.
Allied repair uses signed maximum HP and Barracks, clamps damage, exhausts the
healed unit and retains the separate original-unit automated exhaustion.

The existing private caravan and mission interactions now serve the land-entry
adapter too, preserving cancellation, technology replies, private recipients
and cached report windows. Selected entry performs real lift/single-unit return
and stack-order changes even when animation fails. The optional selector itself
is not fully specified; U02 now records that gap and isolated calls require
explicit selectors. Production movement must resolve it before claiming this
phase complete. Required notice failures retain prior effects and remain terminal.

Seven shared groups cover actual revelation, ZOC exemptions, entry topology,
caravan loss/payment/donation, embassy/investigation, bribe owner reload, sabotage,
repair arithmetic and restoration, private cancellation and failure prefixes.
Full U02 dispatch/arrival/finish/cleanup, desktop effects, remaining mechanics/AI
and generated human/AI-game acceptance are still open. Browser and cross-platform
work remain deferred.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1110 of 1112**, with the two existing
platform/audio skips. The shared probe passed **1077 groups** across 125 coverage
families; source identity verified **622 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed. No desktop UI or complete-game
acceptance is claimed by this checkpoint.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-land-entry-native.json` | `0bf753e7e3eea9de2828433cf597e3b0ffb447145176181800d58a0d30695116` |
| `m4-land-entry-identity.json` | `56aa16530295fcb084adadc241633517158ac58b041a1b754ed45c4121fd711f` |


## Long-move and local cleanup checkpoint

U04 long-move handling now increments and wraps its byte timer, adds the reversal
penalty using signed last direction, and retains its private stop/continue choice.
It distinguishes full Goto from low-nibble variants, honors Automated separately,
resets other local orders and leaves nonlocal state untouched. A retained nonzero
reply clears live orders and Automated; either reply resets the timer. The caller
still owns the later last-direction write.

U02 local cleanup now compares the actual unit-slot upper bound before any unit
mutation. A changed bound returns zero without undoing earlier effects. Otherwise
zero results clear orders, exhaust Automated units, update live Sea-transport
counters, and apply captured-owner AI garrison/timer behavior. Nonzero results
clear Garrison and preserve the full result. Occupancy is not substituted for
that bound test. The dispatcher owns movement lifetime; network synchronization
remains outside the local helper.

Five shared groups cover signed/wrapped timer boundaries, negative directions,
retained replies, private cancellation, unsupported-mode failure prefixes, bound
versus occupancy, captured-owner AI rules and transport exhaustion. Full common
finish still needs actual hut and arrival composition; these helpers do not
claim outer movement, desktop UI or complete human/AI-game acceptance.

Root/App formatting and verification passed, and both Release builds completed
with zero warnings/errors. Native tests passed **1115 of 1117**, with the two
existing platform/audio skips. The shared probe passed **1082 groups** across
126 coverage families; source identity verified **626 compiled inputs**. The
43-project graph, eight identity tests and three coverage tests passed. Browser
and cross-platform work remain deferred.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-movement-tail-native.json` | `dbe8bf60bf88287b432d750edc321bb3e7c175961407771dbf203a851ca5a9d4` |
| `m4-movement-tail-identity.json` | `e669160a382aeb1b6c2040a1802a1ab9cb91b583220a959a383f9d7e2b74f18b` |


## Hut-entry checkpoint

W06 local hut entry now implements all six outcomes using actual C01 city
creation, U01 unit creation/W03 revelation and V03 research grants. It preserves
the initial human override draw, recorded regional counts, fixed global-technology
unit choices, distinct year-1000 thresholds, gold wrapping, owner-specific versus
global unsupported-settler counts, rank-index behavior and barbarian stride.
The helper consumes U02's retained admission and does not retest hut presence;
direct repeated calls remain separate awards with separate random-call identities.

Scrolls perform up to 999 actual eligibility queries, then redirect as specified.
The potentially repeating scroll/nomad cycle yields bounded work instead of
inventing a fallback reward. Signed out-of-catalog Armor prerequisites require
explicit discovery backing. Failed late city creation consumes the size choice
then stops before the excluded invalid write, preserving prior effects.

The initial override reads regional presence flags at F06's declared field,
not the separate policy array. The two additional low-seven restriction bits
now have retained gameplay state and explicit conversion, so imported games
participate in this branch correctly. The unrelated high bit stays opaque.
The W06 prose now uses the field name consistent with F06's declared offset.

Eight shared groups cover real rewards and creation, global technology sequences,
private created-city visits, year/difficulty boundaries, repeated entry, early
barbarian gates, rank and stride, capacity failures, semantic/cosmetic failure
prefixes and regional flag round trips. U02 common finish and desktop callers,
periodic barbarian spawning/orders/ransom, remaining mechanics/AI and full generated
human/AI-game acceptance remain open. Browser and cross-platform work remain deferred.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1123 of 1125**, with the two existing
platform/audio skips. The shared probe passed **1090 groups** across 127 coverage
families; source identity verified **629 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed. No new desktop UI or
complete-game acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-huts-native.json` | `2ff9c192565abe9321f345bd4c25a40e2c84c32c9c3c28cf02ef899779ed536c` |
| `m4-huts-identity.json` | `6f80409de1a6e607490d735d81e70773f82a86a497093c2bae41997b507d683a` |


## Ordinary arrival and common finish checkpoint

U02 local arrival now increments captured-Land landing counts, clears damage
before actual C09 conquest, reloads the shared moving actor after capture and
retains it for finish. The captured-barbarian visibility condition applies to
the whole stack, and actual U05 port arrival wakes eligible sleeping units.

Common finish moves a zero-attack unit to stack bottom before actual W03
revelation and city-window notification. It then composes actual aircraft/coast
continuations, hut outcomes, exploration wakeup, boarding and retained long-move
choices before writing last direction and applying local cleanup. Crash/sinking
keeps the incoming result until cleanup; a changed unit-slot bound overrides it
with zero. Creation/deletion and earlier state are not rolled back. A negative
finish route retains the supplied no-hut admission and writes the narrowed direction.

The session adapter shares existing capture reply/prompt and long-move prompt
logic, routes each nested recipient privately, and retains capture/hut windows.
Seven shared groups cover actual conquest and actor reload, regional-count wrap,
barbarian visibility, port/exploration wakeup, direction ordering, hut-created
bound changes, aircraft and cargo loss, boarding, private capture-to-long-move
choices and nested last-Settler incidents. Required notice failures remain terminal.

Joining the services also corrected two cosmetic-failure paths: aircraft/coast
sound and redraw failures now retain diagnostics and continue required loss work;
boarding redraw failure still permits required state publication and later finish.
The shared U05 failure tests now assert that behavior. Optional enemy pauses and
city-window refreshes likewise cannot cancel gameplay. Shift handling remains
inside the cosmetic pause adapter.

Outer movement admission, viewing, authority/relocation and desktop command
binding remain pending, including the previously recorded entry-selector gap.
This checkpoint does not claim full movement or generated human/AI-game acceptance.
Browser and cross-platform work remain deferred.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1130 of 1132**, with the two existing
platform/audio skips. The shared probe passed **1097 groups** across 128 coverage
families; source identity verified **632 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-completion-native.json` | `3db123a5ee700fb21cf17dc1eea93c478e91fc4ff996b998d1ea3cbed3755b1e` |
| `m4-completion-identity.json` | `c5b92617d26cca133fa2062d33cc2b6a2dfd484cfc664ab056bde45d13870ce6` |


## Local movement visibility and relocation checkpoint

U02 now publishes barbarian origin knowledge and foreign-city embassy visibility
on the single mover before selecting local viewers. Local selection preserves
alive/human/autoplay gates and destination-neighbor disclosure. Animation choices
use captured Air domain, Goto, cost and Shift, with retained unanimated state and
cursor updates. Autoplay saved membership and unwritten observer-zero values are
explicit inputs; observer minus one follows the documented hut/seen-seven aliases.

The landing phase ensures origin presentation, increments a zero regional count
before treaty lookup, and retains the nearest-city slot-zero dialog exclusion.
An absent destination owner needs supplied treaty backing; otherwise it stops
after the count and prior presentation. Required landing notices and publications
retain failure prefixes. Cosmetic failures are diagnostic and permit gameplay.

Local relocation invokes actual cargo lift, clears visibility on the resulting
whole group, marks the captured destination owner on non-Ocean arrival, captures
hut presence and relocates the group. Excess cargo remains at the origin. Six
shared groups cover those effects, directed embassies, viewer/animation gates,
exceptional inputs, hut consumption and failure ordering. A join to actual
ordinary arrival verifies that early and arrival landing counts both apply.

The outer dispatcher, optional entry-selector resolution and desktop command
integration remain pending. This checkpoint does not close M4 or claim complete
generated human/AI-game acceptance. Browser/cross-platform work remains deferred.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1136 of 1138**, with the two existing
platform/audio skips. The shared probe passed **1103 groups** across 129 coverage
families; source identity verified **634 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-relocation-native.json` | `ff802e1d1e8b6cb2d054c4aa56910b3086385fe2db7f1c4386c4424fc1763234` |
| `m4-relocation-identity.json` | `32b51ef99362159eab5fe3e8ac006b7b85dc0d9c5f56e99e030b3e7bd6b33060` |


## Local movement dispatcher checkpoint

U02 now has a local retained dispatcher composing actual land checks, spare-holds
boarding admission, foreign-unit expulsion/combat, landfall, foreign-city admission,
charge, relocation and common finish. It publishes origin ownership and attitude
state at the specified stage, captures destination owner in tile/unit/city order,
and preserves preview, negative-direction and bound-sensitive cleanup results.

The world movement guard rejects a concurrent movement and remains held across
private decisions and cooperative yields. Completion, disposal and terminal errors
release it without rolling back committed changes. Landfall restarts retain direction,
options and the chosen reply while recapturing the new actor, domain and unit bound.
The added U01 spare-holds query uses signed capacities, Land penalties and the
specified sum cutoff; it is not the cargo-marking maximum-capacity operation.

The session adapter shares existing mission, capture, combat, expulsion, fatigue,
landfall and long-move interactions. Eight shared groups cover ordinary/preview
moves, source ownership, wrapping, guard lifetime, failures, negative finish, AI
cleanup, actual boarding/disembarkation, city missions, conquest and full combat.
Session cases exercise private capture-to-warning, passenger and fatigue sequences,
including the runtime's explicit ContinueCommand scheduling after a bounded yield.
Fixtures reuse one actual dependency graph without overwriting attached unit slots.

Missing optional caravan/city-entry selectors now fail only at the consuming
branch. Common finish likewise consumes an unpublished seen input only when its
live observer condition requires it, preserving earlier revelation/publication.
Those inputs remain explicit; this checkpoint does not resolve the recorded
entry-selector specification gap or claim complete desktop movement delivery.
The desktop player command still uses the earlier immediate LandMovement path;
production service composition, cooperative command scheduling and broader AI/order
callers remain next integration work. Full M4 acceptance remains open. Browser
and cross-platform work remain deferred.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1144 of 1146**, with the two existing
platform/audio skips. The shared probe passed **1111 groups** across 130 coverage
families; source identity verified **637 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-dispatch-native.json` | `51185be8fb6b2af8da40bf43160c26b8369cbdd030346c1b2a179cfa089bdcce` |
| `m4-dispatch-identity.json` | `6fb313d7a36af81e3447c75163ae66b4ac6593142c07b66ffdf91cae33ecff0c` |


## Cooperative movement scheduling checkpoint

Review of the desktop call path found that its timer already advances one queued
runtime input per tick, but a bare Pending result supplies no next input. The
movement adapter's bounded landfall/combat/hut yields therefore needed an explicit
readiness signal before production command integration could work.

OperationStep now offers ReadyToContinue for a local Pending step without an
interaction. CommandRuntime queues one Proceed after already admitted work unless
an input for that command is already queued. Each AdvanceOne still executes at
most one step. External waits retain their explicit ContinueCommand/event contract;
private choices are not polled. Cancellation retains FIFO order, and a queued
cancellation prevents another automatic Proceed from being added ahead of it.
Invalid readiness combinations fail without rolling back committed effects.

MovementOperation opts into this contract for its decision-free bounded yields.
The actual movement session test now completes a passenger restart through the
same automatically queued work consumed by the desktop timer, without manual
ContinueCommand calls. Three additional shared groups cover two-command fairness,
private decision pauses, cancellation ordering and duplicate suppression, invalid
readiness states, external waits, terminal failures and disposal. The existing
explicit-continuation tests remain unchanged.

This supplies the required scheduling mechanism; the desktop player command still
uses the earlier LandMovement path. Full movement service composition, command
replacement, meaningful notices/windows and desktop acceptance remain pending.
No generated full-game, browser or cross-platform acceptance is claimed.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1147 of 1149**, with the two existing
platform/audio skips. The shared probe passed **1114 groups** across 130 coverage
families; source identity verified **638 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-cooperative-native.json` | `2fd1f6209a6cbbe7aaf32ab2f4d327a66aa300d9343326bdd29a11879f8df8ee` |
| `m4-cooperative-identity.json` | `af1d592ba763fb56b123126b7476449408479b779ebe6a4cc8f7080766e7578a` |

## Retained research-grant checkpoint

Production movement composition exposed synchronous government and emissary
questions in V03 grants reachable from hut rewards and captured technology.
ResearchGrants now supplies a retained operation with actual V05 reception/reset
and V02 government children. Stored knowledge and advance counts precede the
question; first-discovery, Leonardo and recursive Great Library effects resume
in their specified order. Nested grants forward their decisions without repeating
earlier mutations. Required notice failures retain their committed prefix;
cosmetic leader-window failures remain diagnostics.

ResearchGovernmentPrompts now supplies a retained operation while preserving its
synchronous compatibility entry. ResearchGrantOperation routes government and
emissary choices to the private recipient; cancel/timeout decline, while an
intelligence-window close follows the existing reception contract. The retained
grant constructor requires actual child services and rejects synchronous Grant
calls before mutation. Existing synchronous callers remain operational but need
explicit migration: captured technology, theft, huts, civilization catch-up and
research completion. This checkpoint does not yet wire those choices into the
production desktop movement command or complete M4 acceptance.

Five new shared groups cover accepted/declined government changes, tutorial and
turn-zero gates, recursive Library acquisition, actual emissary greeting/reset,
private recipient routing, cancel/timeout, disposal and failure prefixes. Existing
grant and government cases continue to exercise the shared algorithm through
the synchronous adapters. Browser and cross-platform work remain deferred.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1152 of 1154**, with the two existing
platform/audio skips. The shared probe passed **1119 groups** across 130 coverage
families; source identity verified **640 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-grant-retained-native.json` | `fb9e2a1c672ef9cab9a8635ea5c3ccf255d8708ff0e5130b25697fffff8e9d03` |
| `m4-grant-retained-identity.json` | `ac621ba654f2b4b2ba08383c53bd6f5913231ecc386ca426af628a77ebeea767` |

## Acquisition callers and movement grants checkpoint

CapturedTechnology now owns and disposes a retained ResearchGrants operation.
Its selection/help choice stays distinct from the subsequent government or
emissary decision; the result and live AI production reset occur only after the
grant finishes. C09 capture forwards those decisions before its production/city
visit. CityTechnologyTheft retains both direct and targeted grants, preserving
the ordinary TechnologyStolen write before escape and the targeted write after
escape/incident completion. Disposal and required failures leave committed
knowledge intact and prevent continuation.

Hut scrolls now retain their actual grant, including AI emissary reception and
human government prompts. U02 finish forwards those choices before its direction
and cleanup tail. The actual MovementDispatcher session test reaches a coordinate
hut after charge and relocation, suspends privately with the movement guard held,
accepts the government change and completes cleanup without repeating relocation,
grant storage or the hut notice. A captured-city session also exercises the real
Anarchy setter and owned-city recalculation before the city visit opens.

Capture, revolt, paradrop, combat, city mission, land entry and movement adapters
now forward nested grant prompts and the supplied intelligence-window adapter.
Government and emissary cancel/timeout replies retain their distinct decline
values. HutOperation also requests cooperative continuation for bounded redirects.
ResearchGrants.Begin supports existing explicitly supplied synchronous adapters
during migration; a graph built with retained children still rejects synchronous
Grant calls. Civilization catch-up and research completion remain to migrate.
Production desktop command/service composition, general research-goal presentation
and full M4 human/AI-game acceptance remain open. Browser/cross-platform work stays
deferred.

Six new shared groups cover actual acquisition parents, private recipients,
ordered escape/flags/city visits, cancellation, disposal/failure prefixes and
the complete movement-to-scroll-to-government continuation. Root/App formatting
and verification passed. Both Release builds completed with zero warnings/errors.
Native tests passed **1158 of 1160**, with the two existing platform/audio skips.
The shared probe passed **1125 groups** across 130 coverage families; source
identity verified **641 compiled inputs**. The 43-project graph, eight identity
tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-acquisition-native.json` | `f445d1283cfe5fa79f850153609e5e54e26dfc8cb65ad49c8af637281fc25c15` |
| `m4-acquisition-identity.json` | `63119e9417a673d41f983feb7a399304640c43923c3df653d720a41a84577daa` |

## Retained research completion and Darwin checkpoint

ResearchFlow now retains its actual grant before querying live human/turn state,
resetting AI production or presenting completion notices. Its final government
prompt follows the next-research selection and uses the completed identity.
Declining a source-nonzero grant-time government proposal can therefore lead to
the independently ordered final proposal; neither stage repeats storage, counts
or completion notices. Mode2 retains progress across the pause.

Accumulation waits for the threshold completion before testing and clearing the
one free-advance flag. Darwin likewise waits for the first complete research
operation, including its government decision, before the second completion and
the later production effects. ResearchOperation and CityProduction.Operation now
dispose retained children on cancellation/failure without completing their tails.
Actual AI emissary reception precedes the production reset; cosmetic reception
errors propagate through research operations as diagnostics.

ResearchFlowOperation routes private selection, government and emissary decisions.
The existing desktop turn accepts the same decision union, publishes the pending
state and disposes completed/abandoned research and production children. Its
authored profile still admits only its three research identities; this change does
not claim an executed full desktop government/research game. Informational goal
presentation, civilization catch-up migration, production movement composition
and complete M4 acceptance remain open. Browser/cross-platform work is deferred.

Five new shared groups cover both government stages, progress retention,
free-advance ordering, actual emissary/AI-reset timing, private session decisions,
cancel/timeout, terminal failures/disposal and Darwin's retained production tail.
Existing direct/console authored turns and save/resume cases still pass.
Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1163 of 1165**, with the two existing
platform/audio skips. The shared probe passed **1130 groups** across 130 coverage
families; source identity verified **643 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-research-flow-native.json` | `9f89a638910c366ed68afa9fe469d8d7281d82db7c58aad8bb4b660380ed570e` |
| `m4-research-flow-identity.json` | `95b247cb567b2c3199a44025bd98acd0b7d870ef1a0954d15674ad135bb2f3be` |

## Retained civilization catch-up checkpoint

Civilization creation now retains each actual research grant, including recursive
Great Library government decisions, before testing the next advance, restoring
its captured alive state, clearing unit counters or selecting/placing a leader.
Creation owns and disposes its retained grant. Failed or abandoned work preserves
the committed reset/acquisition prefix and does not execute the remaining tail.
Turn-zero generated setup disposes completed and abandoned creation operations.

Civilization elimination and civil war retain their creation child. Civil war
waits before its notice, treasury split and transfer work; actual last-settler
loss waits before the deletion tail. LifecycleDecision carries the concrete
espionage or research choice through city loss, capture, missions, nuclear loss,
combat and movement. Session adapters route the actual recipient and cancellation
semantics; CivilizationCreationOperation provides the admitted standalone path.
The synchronous elimination adapter explicitly rejects a reached retained choice
instead of choosing an answer or endlessly polling it.

Six new shared groups cover the later-grant boundary, actual Anarchy city
recalculation, placement/count overwrite, failure/disposal prefixes, last-settler
restart, civil-war treasury ordering and combat casualty continuation. Private
command-runtime cases reject answers from the command submitter and unrelated
players, and cancel/timeout decline the government proposal. Existing incident
cases still cover their original victim routing and independent replies.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1169 of 1171**, with the two existing
platform/audio skips. The shared probe passed **1136 groups** across 130 coverage
families; source identity verified **647 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-lifecycle-grants-native.json` | `ec5fa143fc22a9fbf3651180010969f475bb2332c9c4511b4fe20d0b81790797` |
| `m4-lifecycle-grants-identity.json` | `e787db3541c490234001a976a389f5518ff042db0ddf6ac18a264435b72285e9` |

Full desktop movement/service composition, general research presentation,
complete turn/AI scheduling and human/AI-game acceptance remain open. This
checkpoint does not add a generated desktop playthrough. Browser and
cross-platform work remain deferred.

## Movement viewer and unowned-landing repair checkpoint

Reviewing production movement composition found historical undefined-memory
requirements in U02's inputs. The owning specification now records narrow X01
repairs: absent/zero observers have no local viewer or animation flags, hut
presence is independent, and a common finish without relocation publication
skips its optional enemy pause. No original viewer-array or adjacent-frame value
is accepted as a production input. Real saved-human membership for autoplay
remains explicit.

A visible foreign landing on an unowned tile now has no treaty partner. It keeps
the early regional count, applies the remaining notice gates and continues actual
relocation and ordinary arrival. Existing-owner Peace/Ceasefire suppression and
required-notice failure prefixes remain intact. The dispatcher regression reaches
this path through actual admission, charge, relocation and completion and verifies
the two independent count updates, notice and movement cleanup. Viewer cases
cover absent/zero observers with and without huts; unpublished negative-direction
finish completes without requiring arbitrary seen data.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1170 of 1172**, with the two existing
platform/audio skips. The shared probe passed **1137 groups** across 130 coverage
families; source identity verified **647 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-movement-repairs-native.json` | `d4c210fa49c895742f426b4ffc52fc577bbdbc1e0f323eaaff58090623b16f2e` |
| `m4-movement-repairs-identity.json` | `c8973d7edae05d6734f21e1c731a80bf95515ba505bb60f1928424c3b018c36c` |

These are specification-derived supported repairs, not claims of equality to
undefined original memory. Optional caravan/city-entry and autoplay expulsion
choices retain their separate semantic gaps. Full desktop movement/service
composition, U04/A01 execution, complete turn/AI scheduling and human/AI-game
acceptance remain open. Browser and cross-platform work remain deferred.

## U03 coarse waypoints and unit Goto

`CoarsePathfinding` now snaps actual map representatives and traverses retained
land/sea graphs. It preserves D offsets for snapping, C offsets for BFS,
modulo-256 distances and the 256-entry ring, failed-target snap seeding, the
retained tie score and ordered waypoint selection. An explicit finite removal
budget rejects unfinished searches while retaining their prefix. U03 documents
this bound, coarse-coordinate rejection and absent-owner scratch initialization
as supported safety repairs; no original undefined backing value is required.

`UnitGoto` joins actual local/coarse searches and the greedy fallback. It retains
adjacent cylinder rules, inherited modes/cache, fixed-rule changes made by
snapping, actual-unit fallback estimates, airbase refusal, reversal and
human/AI/Automated cancellation/exhaustion. The authored coarse join customizes
Caravel to Land while the actor remains Sea, demonstrating the specified second
local-search rule retention without performing movement. Other cases cover
coarse graph paths, snap failure, exact ties, budget failure prefixes and local
selection. These selectors do not authorize or execute a movement command.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1177 of 1179**, with the two existing
platform/audio skips. The shared probe passed **1144 groups** across 130 coverage
families; source identity verified **650 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-goto-native.json` | `a7336f8ffc51fbf21ae3a16b0fb9ba3feca9fd83efc039f78fffb67dfb2779f7` |
| `m4-goto-identity.json` | `07955cb2cee361cdc8db9a42574fd56f4983eb3d6ea0abf82e01dec6c141751b` |

U04 order execution, general AI orders, desktop service composition, complete
turn scheduling and generated human/AI-game acceptance remain open. Browser
execution and cross-platform qualification remain deferred.

## U04 retained order execution

`UnitOrderExecution` now runs the exact full-order switch through actual
fortification, all seven W04 terrain jobs and U03 Goto/U02 movement. It returns
handled after a child refuses or leaves work unfinished. The Goto parent retains
owner and old order, preserves the zero-movement-return path-owner asymmetry,
uses live arrival coordinates/target and human membership, and performs the
specified GoalReached, AI exhaustion and transport-counter writes. Pending
movement, cancellation, failure and disposal retain their committed prefixes.

`UnitOrderOperation` uses the same extracted `MovementInteraction` routing as
direct movement, including grant, capture, lifecycle and other nested decisions.
The native runtime cases verify private LongMove handling and a passenger
landfall restart through the automatically queued cooperative continuation.
Cancellation still completes a handled order. Actual AI arrival cases distinguish
timer 9/10, adjacent enemies and Sea transport; no generic AI chooser is supplied.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1183 of 1185**, with the two existing
platform/audio skips. The shared probe passed **1150 groups** across 131 coverage
families; source identity verified **654 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-orders-native.json` | `531c4d80ea3ff3aaa2f7762d2a359fb8be9ee7105db3374cd9f79df923700019` |
| `m4-orders-identity.json` | `846979ecda32d6cf42a4cfd723443f272ce107ee1da3f490c56e62ceac2f4c79` |

General A04/A01 order choice, the U04 active-unit input/scheduling cycle, desktop
service composition, full turn scheduling and generated human/AI-game acceptance
remain open. These service/session joins do not establish complete M4. Browser
and cross-platform execution remain deferred.

## U04 return-home search and A04 damage recovery

`UnitRecovery.GoHome` now selects actual owned cities and eligible aircraft
carriers/submarines using U04's ordinary distance, first ties and raw Land-region
filter. It retains the city/carrier tie, foreign-airbase behavior, human signed
fuel-reach gate and zero-multiplier failure. It changes recovery orders or spent
movement without changing home affiliation, task or executing a step itself.

`UnitTacticsContext` captures A04's on-map city/terrain/domain/effective-role state,
including the AI colonist clear before role selection. `GroundRecovery` evaluates
actual damage classes and stack/spatial conditions, preserves the no-moves cargo
bypass, nearby-city and peaceful-territory priorities, forced-attack damage reset,
remote-air recovery, sea human-visibility gate and severe-recovery routes. Its
GoHome calls are real; an integration case follows the resulting Sea Goto through
U04 order execution, U03 selection and U02 displacement. It returns explicit next
phases for the unfinished parent rather than claiming a complete AI chooser.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1191 of 1193**, with the two existing
platform/audio skips. The shared probe passed **1158 groups** across 132 coverage
families; source identity verified **658 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-recovery-native.json` | `744298409d2de753ae35cd5e3d5e462fe03577ccf8058274d9d559cba6410928` |
| `m4-recovery-identity.json` | `cefa1ff6672863f3220223abe96ea8e50f8c3b118f108590719d4d777a461081` |

A04 barbarian/work/off-map entry, trespass/forced-city admission, specialized
routing, guards and movement planning remain open, as do the complete AI caller,
desktop service composition, full turns and generated human/AI-game acceptance.
Browser and cross-platform execution remain deferred.

## A04 city and fortress guard phases

`GroundGuard` now supplies the scenario/ferry gate, foreign-fortress routing and
city guard around the separate specialized/partisan phases. Actual U06 defender
selection sees temporary Fortified orders; normal return restores them before
periodic order release. The actual U01 reserve-defender aggregate excludes the
current head by its predecessor field. Shared goals, Palace/neighbor quotas,
signed city sizes, retained settler-phase flags and live walls/garrison strength
produce the specified guard and movement-planner routes.

Authored cases cover Scenario versus export-only status, live actor x with
captured home context, ferry-to-guard transitions, foreign-fortress treaty and
obsolescence gates, exact stack-order ties, reserve thresholds and negative
quotas. Recovery at a foreign city with no captured owned city reaches the
specified missing-home read boundary: it stops explicitly, retaining Hold/Guard
and prior unit state. No arbitrary city/backing value or unestablished fallback
is supplied. That continuation remains a scoped limitation for the complete AI
caller; successful ordinary cases do not resolve it.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1198 of 1200**, with the two existing
platform/audio skips. The shared probe passed **1165 groups** across 133 coverage
families; source identity verified **660 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-guard-native.json` | `3a5f1b99aee06dff1a277352a817d64694bf40f0fba693428ee97d8481df3759` |
| `m4-guard-identity.json` | `a44d1b3f62075f67acfbbecad99652e866028752dcb1daef459e7d1ac3a9464a` |

A04 entry/trespass/forced-city logic, specialized/site/partisan phases, transport
and movement planning, full AI/turn scheduling, desktop composition and complete
human/AI-game acceptance remain open. Browser and cross-platform execution remain
deferred.

## A04 retained orders and partisan fortress decisions

`GroundOrders` now implements retained Goto, defender-fortification reasons and
the direction/common finish path. Transport retries precede treaty filtering;
standing goals clear through A01, and naval arrival consumes movement only under
the specified transport condition. Hold and direction results produce real
Fortifying/AI-Goto orders. Integration cases execute them through U04, including
actual U03/U02 movement. `PartisanFortress` retains signed counter wrapping and
the distinct second-nearest-city test without removing map improvements.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1205 of 1207**, with the two existing
platform/audio skips. The shared probe passed **1172 groups** across 134 coverage
families; source identity verified **663 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-ground-orders-native.json` | `7d564afaf2646e4173e5f94d9a300ded7fa8e702a70e7aafc9cfe3763a22364a` |
| `m4-ground-orders-identity.json` | `889d1776f5211ecde1161e708d174ead279dc4398ff7d34f4d1e947f7c909f40` |

Pillage/partisan targeting, entry/trespass/forced-city logic, specialized/site/
transport/movement planning, full AI/turn scheduling, desktop composition and
complete human/AI-game acceptance remain open. Browser and cross-platform
execution remain deferred.

## W04 core pillage and A04 partisan choices

`Pillage` executes actual confirmation, W01 feature removal, fresh nearest-city
selection, W03 remembered features, V04 war flags, A01 goals and U02 exhaustion.
It retains the original tile across an attack warning while consuming live type
and owner afterward. Automatic and explicit feature selection remain distinct;
no-city pillage has no invented memory update, and same-owner city selection
still invokes the treaty operation. Cosmetic redraw failure is reported while
movement completes; a mechanical failure retains prior valid effects. The
session adapter presents the actual private attack warning and cancellation.

`GroundPartisans` now selects that actual W04 child, retains the shared city
selector, and implements first eligible nonmilitary targeting and last eligible
road-direction selection. Cases join pillage to common Finish and partisan
directions to real AI-Goto production, including raw airbase components,
visibility/region/treaty gates, city interruption and existing HasOrders.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1212 of 1214**, with the two existing
platform/audio skips. The shared probe passed **1179 groups** across 135 coverage
families; source identity verified **667 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-pillage-native.json` | `1d5555b0d3c871ccf8aec4f90962d1ad73871f832d066b0b95ff014ea9876573` |
| `m4-pillage-identity.json` | `0696f307c9fb601659bea481616c2218b065693bc0492e54e894ada08874948e` |

The W04 player command menu/declaration wrapper, A04 entry/trespass/forced-city
logic, specialized/site/transport/movement planning, full AI/turn scheduling,
desktop composition and complete human/AI-game acceptance remain open. Browser
and cross-platform execution remain deferred.

## W04 player pillage command and private choices

`PillageCommand` now supplies actual player eligibility, captured improvement
choices, V04 confirmation/declaration and the core W04 child. A single Farmland
choice remains automatic and removes Mine only; explicitly choosing Farmland
from a multi-choice menu removes both components. Airbase and Railroad choices
retain their component and priority rules. The wrapper's positive-city check
remains distinct from core city-zero confirmation, with the specified resulting
reputation and treaty effects.

The session adapter routes the menu and each actual nested warning privately.
Tests move the actor during its menu: the wrapper retains its original diplomacy
origin, while the core recaptures the live tile. Canceling the second confirmation
preserves the earlier war declaration. Explicit choices remain selected after
features change, and cosmetic failure does not skip movement exhaustion. No
native dialog/text-buffer or unconsumed movement-option backing is introduced.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1217 of 1219**, with the two existing
platform/audio skips. The shared probe passed **1184 groups** across 136 coverage
families; source identity verified **670 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-pillage-command-native.json` | `3913f8aa0feac88977db8eec47089602609bdc7dc49a43b17be50a16aa09f308` |
| `m4-pillage-command-identity.json` | `84c97720f8af3a5cfb6114589160c9c15dd40d7f59d8c64fae1cc298a6263d36` |

The restricted desktop profile does not yet register the player command. Wider
desktop composition, A04 entry/trespass/forced-city and movement planning,
specialized/site/transport policies, full AI/turn scheduling and complete
human/AI-game acceptance remain open. Browser and cross-platform execution remain
deferred.

## A04 ordinary movement, attack and bribery planner

`GroundMovementPlanner` now implements the eight-direction procedure with real
stack traversal/aggregates, spatial queries, U06 best-defender/preview and V06
bribery. It retains captured abilities and the mutable effective role, consumed
draws on later rejection, peaceful-territory/owner effects, empty-city changes
to Hold/HasOrders, exploration/food and polar scoring. Independent scaled and
unscaled maxima govern the final Hold/attack/order-retention result. The caller
applies that result through `GroundOrders`; planning does not relocate the actor.

Foreign-unit scoring includes the ordinary combat route, peaceful diplomat
exception and fallback/opportunistic bribery. Actual transfer can commit even
when the candidate is rejected, and a required publication failure preserves
that transfer while making the operation terminal. The session adapter retains
private mission choices and cooperative child yields. A04 now explicitly names
Q→human for its first hostility scan, matching the directed pair already stated
for the repeated scan immediately below; cases distinguish it from C→human and
distinguish the any-human and all-human conditions.

Authored groups also cover strict ties, holding/existing orders, role promotion,
Air draw replacement, signed aggregates/cutoffs, captured IgnoreZOC, zero-cost
failure after actual preview, low-movement partisan scaling, city direction/order
production, C01's last visited sea rather than largest sea, empty foreign airbases
and polar candidates. Expectations derive from repository contracts and actual
child composition, not supplied gameplay-success callbacks or external harnesses.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1232 of 1234**, with the two existing
platform/audio skips. The shared probe passed **1199 groups** across 137 coverage
families; source identity verified **675 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-ground-planner-native.json` | `ba307954febbafb095b9c1360f0b0d8a5373b72495311d751b105c33d0ed3fad` |
| `m4-ground-planner-identity.json` | `7c6b259621ae73bdb9f71b05ff6d0dc7f1e9561ca23f7b3aaefcd2905dac18e8` |

A04 entry/trespass/forced-city and specialized/site/transport dispatch, the full
AI chooser/turn schedule, wider desktop composition and complete human/AI-game
acceptance remain open. Reached missing nearest/home-city reads remain explicit
boundaries rather than invented backing values. Browser and cross-platform
execution remain deferred.

## A04 ordinary entry and recovery completion

`GroundTacticsEntry.EnterOrdinary` composes work-order and off-map bypasses,
context capture, directed trespass response, adjacent empty-city forcing and
actual recovery. Hold and Finish routes complete through `GroundOrders`; other
routes retain the context and recovery result for the owning chooser. The
adjacent foreign civilization is retained separately from the nearest city.
Civilization zero is explicitly excluded here and requires W06 dispatch.

Authored cases cover all six bypassed work orders, invalid position/type without
context reads, Sleep field preservation, trespass thresholds and directed flags,
absent nearest/home equality, signed roles and treaty protections, air Hold,
exhausted transport bypass and actual U04/U03/U02 return-home movement.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1238 of 1240**, with the two existing
platform/audio skips. The shared probe passed **1205 groups** across 138 coverage
families; source identity verified **677 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-ground-entry-native.json` | `0f02289b63a9bc92889d91482c856f0f773e9e9c2ceede5ab27e05efe66455cc` |
| `m4-ground-entry-identity.json` | `2868f893aafdd5008b0eece6d584450fb9c3d4aefdfc2ce4bee0c1ae7c6e37ce` |

Specialized/site/transport dispatch, W06 barbarian choice, the full AI chooser
and turn schedule, wider desktop composition and complete human/AI-game
acceptance remain open. Browser and cross-platform execution remain deferred.

## A05 landing direction and coastal targets

`NavalTargets` implements A05 section 9 with actual W01 spatial/ownership
queries, directed hostility and U04 SetGoto. Landing selection keeps zero-score
ocean candidates, the occupied-center exception, exact claimant-or-fertility
admission, hostile-city bonuses and later ties. Near-shore targeting retains
raw-region comparisons, first matching city-radius candidate, no unit/treaty
filter and the original target when no candidate qualifies.

Authored cases cover human treaty gates, fertility mapping, city/land occupancy,
flat and cylindrical topology, preserved movement/counter state and task byte
retention. An integration case runs the selected coastal order through real
U04/U03/U02 sea movement, reaching the target and applying arrival exhaustion.
The helpers themselves do not execute movement or unload cargo.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1244 of 1246**, with the two existing
platform/audio skips. The shared probe passed **1211 groups** across 139 coverage
families; source identity verified **679 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-naval-targets-native.json` | `8120c8887a72d6552923bc9a0f61a5c8d2035657991efbd1d22262bcfc8bbfe9` |
| `m4-naval-targets-identity.json` | `f4dc07e5fcdb7c6d4a728e62ee54fc3eddd82e1a5246495f535c19022aa2e0cd` |

The full naval/transport controller and barbarian callers, specialized/site
dispatch, full AI/turn scheduling, wider desktop composition and complete
human/AI-game acceptance remain open. Browser and cross-platform execution
remain deferred.

## A05 cargo scanning and U04 role wakeup

`GroundGuard` now retains its reached desired-defender calculation in the A04
context. `NavalCargo.Scan` consumes that value only on the specified branches,
reserving or releasing actual Land members across the complete validated stack.
Each scan resets its role set, released count and reserved count. Released
fortified members still count as reserved; CapacityPressure and the first
release gate remain distinct. Fortified members away from home skip the quota.

`UnitRoleSet` represents actual role slots modulo 32. `UnitOrders.WakeByRole`
changes only full Sleep orders, with the specified first-match option, retaining
carrier, destination and movement. Cases use actual guard quota production,
Palace floors, negative quotas, foreign/exhausted members, zero ship capacity,
non-Land exclusion, actual Settle versus effective colonist roles and signed
custom-role aliases. Missing consumed quota retains preceding order changes;
no quota is fabricated for a phase that did not run.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1249 of 1251**, with the two existing
platform/audio skips. The shared probe passed **1216 groups** across 140 coverage
families; source identity verified **682 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-naval-cargo-native.json` | `f23b5c00f353eb68320637f90cc482952611c45b69eb8740814a57d73268c6f2` |
| `m4-naval-cargo-identity.json` | `890c05bbd1f889b235b721cf40a83e29d60e34dd65590bc45a4cae049d285fcd` |

Port selection/recruitment, unloading, naval destination policy and complete
transport dispatch remain open, alongside other specialized/site/barbarian
choices, full AI/turn scheduling, wider desktop composition and complete
human/AI-game acceptance. Browser and cross-platform execution remain deferred.

## A05 port selection and recruitment

`NavalPorts.Select` now uses actual cargo output, city/ocean connectivity,
regional defense quotas, validated stack counts and current goals. It retains
strict score ties, ferry counter matching, zero-distance congestion thresholds,
signed capacity, attack discounts and selected trade/policy state independently
of the last candidate's cargo demand. Attractive ports receive actual Goto,
FerryPort/FreshPort flags and recruitment goals.

Fallback preserves previously published Goto, uses selected-port priority and
the specified home versus live-destination coordinates, and replaces the shared
nearest-city distance with the actual home settler count. Irrelevant missing
policy does not stop a fallback rejection; a later consumed missing policy
retains preceding guaranteed effects. `NavalTransportContext` carries these
values across future controller retries.

Authored groups join fallback Hold to actual A04 completion and a selected
port order to U04/U03/U02 movement. The first probe exposed a mistaken test
expectation: weighted distance minus the attack discount produced a tie, which
correctly retained the first port. The corrected case uses a strict improvement.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1256 of 1258**, with the two existing
platform/audio skips. The shared probe passed **1223 groups** across 141 coverage
families; source identity verified **685 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-naval-ports-native.json` | `972ee5d64ee7c14decc2dc265188725c17cb29929a8db8ca94842f050ac35fa6` |
| `m4-naval-ports-identity.json` | `c472b2a6b94748e92b13fb14b8fdb1ee7ee9660f576ec10a77fad4f52e0c296c` |

Unloading, naval destination policy and complete transport dispatch remain open,
alongside other specialized/site/barbarian choices, full AI/turn scheduling,
wider desktop composition and complete human/AI-game acceptance. Browser and
cross-platform execution remain deferred.

## A05 unloading and transport control

`NavalTransport.Choose` now composes actual port selection, first actual Settle
relocation and unloading around the negative-counter, NoMoves, reinforcement
and retry gates. Port Hold returns before stack reordering; an exhausted ship
can complete unloading before its parent's direct no-moves return.

`NavalUnload` retains trade-home/route decisions, cargo-demand overrides,
Goto-region matches, exploration markers, damage and directed treaty behavior.
It uses actual landing scores for repositioning, current goals, movement
exhaustion and role wakeup for cargo/trade completion, and raw arithmetic
midpoints for the final naval goal. Passengers are woken without relocation.
Missing consumed Goto-region state remains an explicit boundary after earlier
valid effects, including a transport-request marker.

The live model and F06 conversion now retain TransportRequested and the upper
relationship bits used by A05's absent-unit-owner hostility read. Tests exercise
the subversion/relationship calculation, round trips, clearing or changing live
values through opaque-preserving export, and unchanged W06 low-seven settlement
checks. Controller groups cover NoMoves before port selection, first matching
sleeper, actual direction completion, trade Peace, last absent home, signed
route counts, Room/damage overrides, first actual settler relocation, port Hold
and reinforcement/retry/turn gates. A nearby-city fixture initially favored
repositioning correctly; moving that destination farther away isolates the
intended immediate-unload case.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1264 of 1266**, with the two existing
platform/audio skips. The shared probe passed **1231 groups** across 142 coverage
families; source identity verified **688 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-naval-unload-native.json` | `6041e4e5b3aedc0a685a9ca709046f7cb9ff6a34593e0d14f42ac28891fe8792` |
| `m4-naval-unload-identity.json` | `733706b20541f344abd1f5302f874a14c784db7b2292a11899df51dc89ce258d` |

Naval war/trade/exploration destinations and the complete sea prefix remain
open, alongside other specialized/site/barbarian choices, full A04/A01 and turn
scheduling, wider desktop composition and complete human/AI-game acceptance.
Browser and cross-platform execution remain deferred.

## A05 destinations and shared sea dispatch

`NavalGoals` now selects reinforcement, undefended enemy cities, general overseas
attacks, trade destinations, owned ports and exploration targets in the specified
order. Strict score comparisons retain first ties; successive modifiers retain
integer truncation. Trade uses the actual retained passenger home and reached
civilization cursor, including the specified city-memory read. Exploration
preserves y-before-x sampling, stored-target reuse and all 22 random rays, whose
later successes can replace earlier destinations.

`NavalSeaTactics` composes actual cargo scanning, standing-goal clearing,
transport control and these destination policies. It preserves immediate
transport direction/Finish, the distinct direct NoMoves return and continuation
to A04 after Goto publication. Authored cases exercise those routes and execute
a selected reinforcement order through actual U04/U03/U02. Two initial test
inputs landed just below their intended secondary-sampling boundaries; moving
the supplied codes inside those intervals corrected the fixtures without
changing the F01 law or destination implementation.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1277 of 1279**, with the two existing
platform/audio skips. The shared probe passed **1244 groups** across 143 coverage
families; source identity verified **693 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-naval-goals-native.json` | `35a47dd980edb45217671d73f9ea15e3da63dcc9a0658e4ca3db860b8af3daf7` |
| `m4-naval-goals-identity.json` | `56e28113bc6aace5f330c676c703c5bd8e3f8a929d8b3b9fea207fdd30cf536f` |

A03 settlement/work, A06 air/nuclear, A07 special-unit and W06 barbarian policies,
full A04/A01/turn orchestration, generated desktop composition and complete
human/AI-game acceptance remain open. Browser and cross-platform execution
remain deferred.

## A07 caravan and diplomat destinations

`SpecialUnitDestinations` now supplies caravan and diplomat/spy policies using
captured A04 context, actual W01/C01 queries, V04 attitudes and V07 spaceship
race state. Caravan choice retains existing Goto, uses the origin region's
city-size total for landing, preserves ordered trade-score reductions and uses
only actual home for its final fallback. Diplomat choice retains unprotected
foreign orders, the absent-home partition, Road thresholds, fixed Spy identity,
treaty/rank/government gates and existing orders when an owned city wins.

No-home/no-target destruction composes actual cargo-aware U01 removal, V01
elimination/recreation and V06 incidents. `SpecialUnitDestinationOperation`
routes private lifecycle replies and cancellation without replaying removals.
Tests verify first ties, signed route counts, retained inactive home, nearest
human discounts, unclamped diplomat versus clamped caravan attitudes, spaceship
race gates and committed deletion before a private incident. The initial
trade fixture accidentally triggered the specified later nearest-city tie;
strictly closer home geometry isolates the intended trade-score tie. The
lifecycle fixture now supplies the actual creation child required after loss.

Caravan route reads beyond the three supported partners stop explicitly at
the reached unsupported read; they do not fabricate additional route slots.
Selection assigns orders without delivering cargo or executing espionage.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1285 of 1287**, with the two existing
platform/audio skips. The shared probe passed **1252 groups** across 144 coverage
families; source identity verified **698 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-special-destinations-native.json` | `2264e865f7aaa9d67b78f817f589d0c75c155a36b1b879bddd8f9a08cb784430` |
| `m4-special-destinations-identity.json` | `8e1826cd5d0f2df6e97c255be9bffb1cb8b67b0515d6f4644b8243fe5126f0a3` |

A07 paratroopers, A03 settlement/work and full founding, A06 air/nuclear, W06
barbarian policies, full A04/A01/turn orchestration, generated desktop composition
and complete human/AI-game acceptance remain open. Browser and cross-platform
execution remain deferred.

## A07 proactive paratrooper policy

`ParatrooperTactics` now implements captured return-home and solitary-fortress
routes, directed-war city admission, retained landing scores/targets and the
ten-candidate landing search. It preserves the extra center, ordinary center,
descending directions, strict ties, signed terrain/size values, fortress bonus
and complete search cancellation by an owned unit. A cancelled high score can
keep an earlier target when a later city's signed-size seed is lower.

Selected landings invoke actual U05 and C09, returning zero directly whether
the child lands or refuses. Tests cover ocean/protected-city refusal, human
capture windows, AI city removal and movement left after damage is cleared.
The parent does not run a second common Finish or exhaust the healed actor.
`ParatrooperTacticsOperation` routes private capture replies/cancellation; late
child failures retain the actual landed status and committed position. The
session result schema explicitly declares the Boolean landed result.

The away branch consumes A03's specified city selector. Tests supply that phase
input explicitly; this does not close A03's city-site implementation or the full
A04 join. No synthetic query callbacks or original private-memory inputs were
introduced to exercise retained-score behavior.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1293 of 1295**, with the two existing
platform/audio skips. The shared probe passed **1260 groups** across 145 coverage
families; source identity verified **701 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-paratrooper-policy-native.json` | `10a478bb5cbe6d3a8ae03fc3daa797ce7c03f4fb5a2123401e7f9cd7de90e25f` |
| `m4-paratrooper-policy-identity.json` | `38f3917ae158b25291bcd55d3702b497a3d03749b67d893b50b195105f943796` |

A03 settlement/work and full founding, A06 air/nuclear, W06 barbarian policies,
full A04/A01/turn orchestration, generated desktop composition and complete
human/AI-game acceptance remain open. Browser and cross-platform execution
remain deferred.
## A06 nuclear targeting and delivery join

`NuclearTargets` now implements city admission, per-tile scoring, reach from
owned cities, staging relocation and directed diplomatic consequences. It uses
actual SDI queries, validated stack traversal, damage-adjusted movement allowance
and bilateral Incident publication. First reachable ties remain stable; a higher
unreachable score preserves the previous target. Fixed enemy missile counts and
the actor's live actual-type count retain their separate admission roles.

Tests cover strict unsigned strength thresholds, signed size/count boundaries,
large hostile stacks, friendly/protected penalties, wrapped reach and staging on
ocean/airbase tiles. Staging preserves existing orders, task and spent movement,
clears only actor visibility and retains contact-turn wrapping. Missing selected
targets and zero road divisors stop at their reached boundaries. Integration
reloads A04's working position after staging and executes the produced order
through actual U04/U03/U02/U07, exercising detonation and nearby SDI interception.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1300 of 1302**, with the two existing
platform/audio skips. The shared probe passed **1267 groups** across 146 coverage
families; source identity verified **703 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-nuclear-targets-native.json` | `192e72c75ba2136eba40c60b105f0642a2f0c2e6ddc405faa1249823620ca1c9` |
| `m4-nuclear-targets-identity.json` | `2129c17a8f17430348983e7f6d8040cca374c87af61e7d26c6f670e46fb900e9` |

A06's general air chooser, A03 settlement/work and full founding, W06 barbarians,
full A04/A01/turn orchestration, generated desktop composition and complete
human/AI-game acceptance remain open. Browser and cross-platform execution
remain deferred.

## A06 general air tactics

`AirTactics` now composes fuel/range recovery, destroyed-after-attack selection
and redistribution, AirSuperiority targeting, return feasibility and basing,
and Attack-role city raids. It uses actual U04 GoHome and Goto setters, U01
stack aggregates/relocation and U02 movement allowance. Working position is
updated after staging; ordinary travel and attack execution remain with A04/U04.
Shared query scratch without a subsequent gameplay consumer stays local rather
than becoming original-frame state in the production model.

Fourteen groups cover signed fuel/counter and cached/live type distinctions,
actual carrier recovery, expended cost thresholds and later ties, absent versus
owner-zero airbases, ocean/fortress/hold modifiers, first redistribution and
blocked-best-target refusal. Superiority checks preserve visibility/treaties,
equality/veterancy, damage-independent rule strength, aircraft bypass, transport
passengers and adjusted human/range/stack ties. Strike tests cover occupied
return candidates, unreachable-home attacks beyond reach, first reachable bases,
sequential human Airlifted objectives, Vendetta and missing-home zero/Hold.
Raids preserve raw ocean regions, all friendly-neighbor bonuses, later ties,
HasOrders, remaining movement and the historical-scenario exception.

The first adjusted-tie fixture blocked the chosen target's sole nearer return
tile and correctly produced staging. Separate cases now isolate selection ties
and that return-occupancy gate. Actual recovery orders execute through common
A04 completion and U04/U03/U02, arriving at the owned city with movement charged.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1314 of 1316**, with the two existing
platform/audio skips. The shared probe passed **1281 groups** across 147 coverage
families; source identity verified **705 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-air-tactics-native.json` | `ca75f5ae9e01ce0d4b7345fbaebeae3fdaac2675d559f041bd5c1f000198b709` |
| `m4-air-tactics-identity.json` | `95b4994928f9b3dd6d8ee93bca9b256a9a5da9e3d00bab0cd6dac0a69698dc9b` |

A03 settlement/work and full C01 founding, W06 barbarians, full A04/A01/turn
orchestration, generated desktop composition and complete human/AI-game
acceptance remain open. Browser and cross-platform execution remain deferred.

## C01 founding and player Build/Join City mechanics

`CityFounding` now supplies the direct founding helper and resumable player
Build/Join City procedure. Creation uses actual C01 naming/allocation; founding
uses basic U01 deletion with real nested V01/V06 checks, clears the specified
center improvements and remembers features for civilizations 1 through 7 without
adding exploration. Supplied city slots preserve the helper's lack of location,
owner or occupancy checks. Oversized direct names stop safely after preceding
creation effects; fifteen Unicode scalar characters are supported.

Player admission retains actual Settle role, Ocean and exact adjacent-city
checks. Joining uses signed city size against the unsigned Aqueduct threshold,
without an owner/facility gate or new economy recalculation. Naming pauses after
city creation. Cancellation performs actual city removal, including supported
unit loss, before decrementing the live leader name cursor; it does not restore
creation tokens, capital summaries or earlier naming resets. Acceptance runs
founding, actual calendar year, required notices/tutorial and a final city visit
whose reply explicitly clears calculation processing mode.

Twelve groups cover surviving stack companions, feature memory, direct/custom
inputs, empty/Unicode names, signed population wrapping, cancellation prefixes,
supported-unit destruction, a real private lifecycle decision, cosmetic failures,
required publication failures and disposal. Fixture corrections removed access
to nonexistent barbarian memory, restored the intended turn after incident
fixture setup and distinguished no-killer loss from conquest history.

The core exposes typed name and city-window decisions. The public session API
currently only accepts numbered choices; text replies, the founding session
adapter, desktop registration and generated-game composition remain required.
The existing restricted Alder Valley command has not been replaced by this step.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1326 of 1328**, with the two existing
platform/audio skips. The shared probe passed **1293 groups** across 148 coverage
families; source identity verified **708 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-city-founding-native.json` | `f3fcbc9a89bf06f9f4fac171852bf37cd8f84de74dfe72effcb01086e445c1a5` |
| `m4-city-founding-identity.json` | `930eb64f9cd4fcfe2a853d0fecc4446013f38ae3b462b5649a69f6d168d595fa` |

A03 settlement/work and its founding join, W06 barbarians, full A04/A01/turn
orchestration, generated desktop composition and complete human/AI-game
acceptance remain open. Browser and cross-platform execution remain deferred.

## Private text replies and C01 naming presentation

`InteractionTextInput` adds a labelled initial value, Unicode scalar limit and
one submit choice to the existing private interaction contract. Text replies use
the same current participant/controller checks and queued-input boundary as
numbered choices. Missing text, disallowed choices and excessive lengths do not
consume the decision; empty values are accepted. Deadlines selecting the text
choice use its initial value. Duplicate and stale replies remain rejected.

`CityFoundingOperation` composes actual C01 naming, cancellation, nested lifecycle
and final city-window decisions. The desktop dialog preserves drafts across
refreshes, focuses the editor, supports Enter or the offered button and disables
accepted input while awaiting completion. Escape follows existing cancellation
semantics. Lua exposes the optional field metadata and a third `game.reply`
argument, preserving colons and Unicode. Local replay version 2 records bounded
text replies, including rejected attempts; journals without text replies retain
version 1. The replay profile remains the restricted authored local game.

Six shared foundation groups cover authority/revocation, invalid metadata,
Unicode/empty values, deadlines, stale identities, cancellation, replay versioning
and recording exhaustion. Actual founding cases preserve created-city and
unit-loss prefixes and route real private lifecycle decisions. A fifteenth Lua
group checks metadata and both rejected and accepted text replies; existing
native task/resource tests also pass.

A standalone independently authored native widget harness uses the actual
Avalonia dialog, SessionPanel and SessionRuntime with a test-only operation.
It checks editor bounds/focus, draft retention, visible length errors, Unicode
Enter, empty button replies, stale handlers and Escape cancellation. Its source,
project, log, screenshot and artifact hashes are retained under
`build/test-results/m4-text-replies-widget.*`. The screenshot was visually checked
with the application's light theme. Initial harness attempts lacked Xvfb on PATH
and selected an older native graphics dependency; the passing run used the
existing extracted Xvfb and explicitly matched the repository's Linux graphics
package versions. This is widget evidence, not a complete desktop founding flow.

Root/App/Automation formatting and verification passed. All three Release builds
completed with zero warnings/errors. Native tests passed **1332 of 1334**, with
the two existing platform/audio skips. The shared probe passed **1299 groups**
across 149 coverage families; source identity verified **710 compiled inputs**.
All 15 native Lua groups and six Lua receipt-validator tests passed. The
43-project graph, eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-text-replies-native.json` | `885a0b3ecebfc3b77fa2ea945532ce15acd86baee3dd7b583357fb736b989f5c` |
| `m4-text-replies-identity.json` | `c6a0f171474f1165ccedbb8dd3755c95dd6b8397d05896e7e3ba5e48878d3b73` |
| `m4-text-replies-lua-native.json` | `5c8f749918b602b2fb3af58f1fa988e3b1d5c82166ae9745245276d143bc620e` |
| `m4-text-replies-widget.json` | `3a08f9d0f2abac12814e6534d8efc4de2f11d3e43cd708b78a8e02b7423cbc3b` |

General desktop founding command registration, A03 settlement/work and its
founding join, W06 barbarians, full A04/A01/turn orchestration, generated desktop
composition and complete human/AI-game acceptance remain open. Browser and
cross-platform execution remain deferred.

## A03 city-site selection and fortress classification

`SettlementTactics.Site` implements A03 §4 using actual W01 fertility/hut queries,
A01 goals and C01 founding. It distinguishes retained effective Settle from the
live actual rule role, caps the current fertility using the nearest city's region,
preserves strict radius ties and chooses the first admitted hut. Founding can be
triggered by first-city placement, local scores, the signed wrapping auxiliary
timer or the independent advanced-human neighbor rule. An outer-radius FoundSite
Goto continues into that last rule. Actual C01 success or capacity refusal both
produce A03's direct return-one route without shared order completion.

The continuing phase produces the retained city selector consumed by A07. A real
site/paratrooper test adds a closer city after selection and verifies that the
paratrooper returns to the selected city. Direction choices also pass through
actual A04 order application. These are concrete child joins, not a full tactical
parent or generated-game loop.

`ClassifyFortress` implements A03 §5's four-line test. It uses ordinary distance
to the live captured home, preserves adjacent fortress/city/goal/four-ocean exits,
does not upgrade adjacent marker 1 to outer marker 2, and includes both coordinate
parities and wrapped samples. Need 1 and need 2 publish actual standing Defend or
TerrainWork goals while retaining the actor's task and orders.

Eleven groups cover first-city goal removal, refused founding, nearest versus
owned-nearest regions, role distinctions, signed role gates, radius ties, real
huts, timer wrap, regional counts, independent founding after Goto, unsupported
mode/failure/disposal, live-home distance, negative goal priorities, four-line
barriers, parity, wrapping and the A07 consumer. The first failure fixture tried
to capture context after entering an unsupported network mode; it now captures
before testing the operation's own guard. The first wrap fixture used adjacent
water, which correctly set marker 1; the final fixture isolates an outer sample.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1343 of 1345**, with the two existing
platform/audio skips. The shared probe passed **1310 groups** across 150 coverage
families; source identity verified **713 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-settlement-sites-native.json` | `4628817a722642d15e4c92031048ce75b28f57128ea5cb72c438f718c3e43233` |
| `m4-settlement-sites-identity.json` | `d88d06ad1e80ea737a0ec50bb27d5ae37498e8f7ad7da0d74696e07993bbcdd9` |

A03 settler/role-21 work and population return, W06 barbarians, full A04/A01/turn
orchestration, generated desktop composition and complete human/AI-game
acceptance remain open. Browser and cross-platform execution remain deferred.

## A03 settler work, role-21 work and population return

`TerrainWorkerTactics` implements A03 §§6–7 in the specified order, including
retained GotoCity, company/neighbor-owner goals, standing TerrainWork removal,
current pollution, human polluted destinations and both fortress opportunities.
Local improvement choice uses signed irrigation time and actual resource/source
queries; road and railroad priorities retain their distinct goal and terrain
conditions. Capacity comparison preserves equality and signed fertile counts.
The rotated scan chooses the first admitted neighbor using actual first-unit and
Settle-stack queries, without adding a raw-region restriction.

Role-21 work retains unsigned home identities, raw-region fallback and travel
before local work. It keeps road-before-retreat ordering and the separate signed
threshold comparison, without adding ordinary government, result-permission or
irrigation-source checks. Threshold work and cleanup retain the task; radius
choices keep their distinct task identities and do not add ownership, occupant
or Ocean exclusions. Work selection preserves progress, home and spent movement;
actual A04/U04/W04 executes a selected cleanup order in the integration suite.

`SettlerPopulationReturn` implements the crowded-region return/absorption tail.
The retained supported home need not equal the captured nearest owned city.
Absorption increments signed city size before actual U01 cargo-aware destruction
and nested lifecycle checks, then requests cosmetic city/map refresh. It does not
recalculate city output or apply human join-city admission. Cosmetic failures are
diagnostic; required failures retain the size/deletion prefix and cannot resume
as a fresh operation. Returning elsewhere retains movement while assigning the
specified Goto task.

Nineteen groups cover phase admission, retained flags, goal prefixes, priorities,
custom signed work values, resources, stack exclusions, rotated movement, old
home identities above 127, permissive radius choices, actual work execution,
signed population/counts, destination selection, cosmetic failures and disposal.
Missing custom work definitions stop at their reached read after earlier goal
changes; an earlier travel decision does not read unused definitions. The first
compile corrected the unit's semantic home member name; the initial executable
probe passed all new behavior cases.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1362 of 1364**, with the two existing
platform/audio skips. The shared probe passed **1329 groups** across 151 coverage
families; source identity verified **718 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-terrain-workers-native.json` | `a081fe5699ba50c04607cd449ced0b3a2aef10686355977192eb456db0a3ce83` |
| `m4-terrain-workers-identity.json` | `ca1c661e5f8216644a04fbe187518666ba33121041a15e9acc9fc15e041c496a` |

W06 barbarian spawning/orders/ransom, full A04/A01/turn orchestration, generated
desktop composition and complete human/AI-game acceptance remain open. Browser
and cross-platform execution remain deferred.

## W06 periodic sea raids and uprisings

`BarbarianSpawning` supplies the periodic producer with actual U01 creation,
U04 sleep, synchronized in-place relocation and whole-stack deletion. Entry
preserves activity/difficulty thresholds, cadence and signed Armor-prerequisite
queries. Sea search admits at most 1,000 candidates; exhaustion returns from the
whole operation. Ship/escort types retain global versus human discovery and the
base-rules setting. Failed ship creation deletes the actual remaining stack and
still permits the subsequent city attempt.

Uprisings call the shared C09 treasury query and replace their search center with
each unrestricted nearest-city result. Bounds, same-medium control, region and
occupancy gates retain their ordering and final-attempt exclusion. Size/surge
arithmetic, direct strongest-rank indexing, fixed unit identities, groups of four,
Fanatics veterancy and exploration visibility are preserved. The final creation
result controls notification; a human city's redraw still occurs after capacity
failure or autoplay. Local presentation failures are diagnostic and required
failures retain earlier creation. The operation routes actual nested lifecycle
decisions without introducing cosmetic reentry points.

`GameRules.ExpandedBarbarianTypes` exposes the specified base-rules bit, including
save/preference conversion and fresh setup defaults. The first probe found that
advanced setup also needed to initialize this newly semantic bit. Test assertions
were corrected for actual U01 visibility, silent stack deletion/pool shrinking
and the controlled-choice exception type. Seventeen groups cover the producer,
its real children, bounds, failure prefixes and the format setting.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1379 of 1381**, with the two existing
platform/audio skips. The shared probe passed **1346 groups** across 152 coverage
families; source identity verified **721 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-barbarian-spawning-native.json` | `64b5f4ceaf43d4133a0c5ebce03e78e2687cb881d78e054496e55893404245ba` |
| `m4-barbarian-spawning-identity.json` | `a68e6f5b9117edad258f4beb4d12b78ed160b850e5e740bcf1999a1fd7ca154b` |

W06 barbarian orders/ransom, full A04/A01/F04 orchestration, generated desktop
composition and complete human/AI-game acceptance remain open. Browser and
cross-platform execution remain deferred.

## W06 city ransom with classic content and private replies

`BarbarianRansom` supplies the empty foreign-city neighbor child. Demand uses
the retained nearest city's signed size before replacing that city with the
actual target. Strongest-civilization pricing, human/affordability gates and
missing-content versus nonpositive-count routes remain distinct. Autoplay still
loads the selected content and requests visibility, then resumes neighbor logic.
The child returns Continue, Finish or Paid so the owning chooser retains its
specified finish behavior.

`ClassicRansomText` reads CITY/BARBARIANS through actual F03 scenario/install
lookup and language selection. Decimal counts use the shared numeric parser's
wrapping arithmetic without accepting radix prefixes. Selection preserves the
signed coordinate remainder. A missing count follows the reader's cleared-line
semantics; missing selected message content fails explicitly under P04's safe
malformed-content policy. Streams close on every exit and directory context is
preserved rather than changing the process working directory.

Acceptance debits the retained amount from current treasury, without another
affordability gate, then uses actual first-unit queries and whole-stack deletion
in B[0..19] order. The center is excluded. Required deletion failures retain the
payment; cosmetic status/map failures are diagnostic. Imported mixed-owner links
exercise nested last-settler elimination, real spy incidents and the actual
creation operation's refusal to restart a nonzero-turn human civilization.
`BarbarianRansomOperation` routes the offer privately to the payer and subsequent
lifecycle choices to their own recipients. Cancellation and timeout decline;
accepted payment is not replayed while a nested decision waits.

Eleven groups cover these paths, including retained target replacement, strongest
pricing, signed/wrapped values, actor survival outside the radius, stale replies
and disposal. Initial checks corrected an invalid default-without-deadline prompt
and fixtures that assumed normal placement created mixed-owner links. The nested
fixture now supplies both the real incident choice and actual creation child.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1390 of 1392**, with the two existing
platform/audio skips. The shared probe passed **1357 groups** across 153 coverage
families; source identity verified **725 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-barbarian-ransom-native.json` | `901c785b6d8c548e137d01e63bb9d6e051456980285887c1604a6e78a8eabb27` |
| `m4-barbarian-ransom-identity.json` | `942300144933c4b0e54801d33cd18ddbc02fcfe7e16b4310e1cb8b1f76ed8da6` |

The owning W06 barbarian chooser, full A04/A01/F04 orchestration, generated
desktop composition and complete human/AI-game acceptance remain open. Browser
and cross-platform execution remain deferred.

## W06 barbarian order chooser — 2026-11-23

`BarbarianOrders` implements the local chooser before ordinary A04 entry. Hold
exhausts movement and preserves the specified role/fortress order behavior;
selecting a direction only assigns AiGoto for later U04 execution. Finish reads
the retained slot even on the transport deletion path that returns zero. Polar
removal uses actual cargo deletion and other removals use actual synchronized
loss/lifecycle operations.

The capacity-first transport branch retains signed-byte age, ordered adjacent
landing/attack, first treasury-score ties and the actual A05 coastal helper.
Landing publishes the specified visibility, wakes cargo and consumes ship
movement without relocating units. Nontransport behavior covers garrison and
periodic removal, actual U03 pursuit with the specified southward bias, leader
escort/hold/removal, Partisans and candidate-specific secondary draws. Neighbor
scoring retains later ties and post-draw rejection. Real W04 pillage still leads
to exhaustion after refusal; actual ransom payment can remove the actor and
returns without the ordinary finish tail.

`BarbarianOrderOperation` routes private ransom, lifecycle and pillage decisions.
The existing ransom adapter shares its prompt/reply mapping with the chooser.
Sixteen new groups cover these branches, including actual U04 movement after
selection, deletion return distinctions, coastal fallback, cosmetic failures,
required random-input failure, cancellation/disposal and private/stale replies.
The tests use repository-authored world/rules/content and actual gameplay
children. Fixture corrections supplied required pathfinding state and fixed
near-distance and role assumptions; no production behavior was changed to
satisfy an incorrect expected result.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1406 of 1408**, with the two existing
platform/audio skips. The shared probe passed **1373 groups** across 154 coverage
families; source identity verified **730 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-barbarian-orders-native.json` | `2834059bd59a1141f487d5e933a6cf6c472175702ab3083fdc87d759885c17c5` |
| `m4-barbarian-orders-identity.json` | `0e9496d1e9a28edde561572d36555065db8b4cd5e03e81c3e2d3c0e080e853d1` |

Full A04/A01/F04 orchestration, generated desktop composition and complete
human/AI-game acceptance remain open. This delivers the chooser and its actual
children, not complete M4. Browser and cross-platform execution remain deferred.

## A04 shared unit chooser — 2026-11-23

`UnitTactics` composes the actual barbarian, ordinary entry/recovery, scenario,
air/nuclear, special destination, settlement/paratrooper, fortress/city guard,
transport/work and movement-planning operations. One captured context survives
child decisions and transport retry. Direct returns preserve their distinct
completion behavior; nuclear staging reloads position before direction
application. Selected orders remain for actual U04 execution.

`UnitTacticsOperation` routes actual private ransom, lifecycle, founding,
paradrop/capture, mission and pillage decisions through shared interaction
helpers. Bounded yields resume through the runtime. Founding diagnostics survive
the settlement child's disposal. Required failures and disposal preserve already
committed gameplay effects.

A05 now documents the narrow X01 production initialization repair for otherwise
unwritten defender quota, landing match, cargo demand and selected-port policy.
The chooser initializes these once; reached producers replace the values and
transport retry retains them. Explicit unresolved helper inputs remain diagnostic
cases. No port/city identity or arbitrary original stack state is manufactured.

Nineteen new groups exercise real founding, population absorption, paradrop
capture, pillage, nuclear staging and U04 movement, plus retained orders, guards,
work assignment, private ransom/bribery and failure prefixes. Naval cases cover
exhausted Defend cargo, actual transport retry, quota replacement, fallback
priority/demand and landing-match retention. Test corrections preserved the
specified damage-dependent movement allowance, city-guard early completion and
war admission; no gameplay policy was changed to fit an invalid expectation.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1425 of 1427**, with the two existing
platform/audio skips. The shared probe passed **1392 groups** across 155 coverage
families; source identity verified **735 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-unit-tactics-native.json` | `712c3599cf1b328e686cdcc42618e1b018ff59174986846b9d1489eb0d7e398c` |
| `m4-unit-tactics-identity.json` | `184bcf1fe327a926d90459779689d09a2b58b6c87f8090ece219885f35ea26cb` |

Full A01/F04 orchestration, generated desktop composition and complete human/AI
end-condition acceptance remain open. Browser and cross-platform execution
remain deferred.

## A01 local unit passes and U04 automated continuation — 2026-11-23

`AutomatedOrders` composes actual shared tactical selection and order execution.
The spent/full-Goto bypass retains its naval-superiority adjacency gate and has
no added movement-left check. The alternate path honors the chooser's direct
return before reading the captured actor's remaining movement. Execution keeps
that captured actor and exhausts it only on an unhandled order.

`AiUnitPasses` captures owner and historical first-pass restriction, then performs
two descending scans with separately captured bounds. It preserves live
order/domain action increments, the 11/21 continuation limits, bound-change
breaks and the conditional extra transport continuation using shared active
state. Admitted exhausted/sleeping units still receive cursor redraw; skipped
slots do not. Cooperative yields keep the local session responsive without
turning presentation callbacks into gameplay interleavings.

`AiUnitPassOperation` forwards actual private tactical/movement decisions through
shared helpers. Windows are retained only for the same pending decision, so a
later unit cannot inherit an earlier unit's city window. Required failures and
disposal preserve committed effects; cosmetic cursor/child failures remain
diagnostics.

Fourteen groups cover real movement, work and founding, Goto bypass/hold/naval
gates, reverse admission and historical Air priority, no-moves/sleep, action
limits through the actual U02 busy guard, founding removal and real hut-created
units admitted on pass two. Transport cases cover adjacent enemies, visibility
retained after a custom sea worker completes cleanup, and barbarian exclusion.
Private ransom refusal resumes actual conquest and treasury plunder; payment,
LongMove, cancellation and stale replies follow the real children. Initial
fixtures incorrectly supplied a human-only hut draw to an AI, assumed sea
relocation retained visibility, and assumed ransom refusal prevented later
conquest; expectations were corrected without changing gameplay policy.

U04's `hold` producer/lifetime is not defined in the current specifications.
The helper requires an explicit input and the owner records the gap. Full
F04/desktop composition must resolve it; it is not silently mapped to a unit
flag or keyboard modifier. Existing open F04 city-analysis arithmetic also
remains outside this milestone.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1439 of 1441**, with the two existing
platform/audio skips. The shared probe passed **1406 groups** across 156 coverage
families; source identity verified **740 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-ai-passes-native.json` | `615aa1114ae1b4bba295535ed0138e87b5c665501160c38b20df27d3eee520f1` |
| `m4-ai-passes-identity.json` | `db447d7c31641a0744a7c7219dea66616a7921625a8079ea930b06a5ded47f0e` |

Full F04 orchestration, generated desktop composition and complete human/AI
end-condition acceptance remain open. Browser/cross-platform qualification and
network execution remain deferred.

## U04 global reset and civilization recovery — 2026-11-23

`UnitTurnReset` rebuilds presence through W01 setters and performs the live
ascending reset scan. Ordinary reset preserves Moved/Paradropped for recovery.
Mission expiry uses actual V06 escape and lifecycle/incident children, including
private human decisions through `UnitTurnResetOperation`. Invalid relationship
indices omit only the excluded write; committed timer, mission and child effects
remain. Exhaust/all/owner selectors retain their distinct behavior.

`CivilizationRecovery` uses actual C01 nearest-city and C08 facility queries for
healing, wake-up and moved-air attrition. It preserves signed HP arithmetic,
airbase owner eligibility, observer filtering and the per-invocation retained
facility selector. An unsupported domain with no prior selector requires an
explicit input. Presentation failures are diagnostics, not gameplay rollback.

The authored desktop now uses shared ordinary reset and recovery. Its restricted
save profile rejects mission flags instead of silently erasing mission state;
general F04 composition must use the retained full reset operation. Thirteen
new groups cover the shared operations, actual spy survival/return/loss, private
incident decisions, failure/disposal prefixes, and direct/console desktop turns.
An initial Ocean-origin test expectation was corrected against C01's explicit
disabled region gate; gameplay policy was not changed to fit the fixture.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1452 of 1454**, with the two existing
platform/audio skips. The shared probe passed **1419 groups** across 157 coverage
families; source identity verified **744 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

The unchanged native desktop driver passed **26 checks** through production
controls, including save/reload, worker/rate/government/population continuation,
retirement reports, Hall persistence and shutdown. Earlier attempts encountered
an Xwayland image-capture error and native picker failures. The successful run
used Xvfb with an isolated session bus/XDG directories and
`GSETTINGS_BACKEND=memory`; no product or acceptance-check changes masked those
harness failures. This remains authored-profile acceptance, not full M4.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-unit-turn-native.json` | `8682039cf4141ad12ccca839f9d545320bb70d7e6db605149a4831b3d62b5e63` |
| `m4-unit-turn-identity.json` | `ea713728542d43afcdf21466d6fbd21badccf233a7cc9bddaaa83c5eb8eb58f3` |
| `m4-unit-turn-play.json` | `8420a6bc7ddf0d7dfff2f6bd67612014a8abbd86c5ccfb0b8b95ab561e202fd0` |

Full F04 orchestration, generated desktop composition and complete human/AI
end-condition acceptance remain open. Browser/cross-platform and network
execution remain deferred.

## W05 global warming pressure and terrain — 2026-11-23

`GlobalWarming` implements local pressure updates and deterministic terrain
transformation using actual C08 Solar Plant queries, W01 tile setters and W03
remembered features. It retains signed AI-tally arithmetic, human-count rounding
(including barbarian membership), one-step pressure narrowing/clamping, warning
equality and old event severity. Event-count wrap is preserved; a successful
update clears the AI tally without recounting polluted tiles.

The terrain scan uses the specified twenty ocean visits, including repeated
neighbors and wrapped aliases, followed by coastal conversion or the dry
coordinate pattern. Coastal conversion removes irrigation/mine and selectively
refreshes whole remembered features. Dry conversion preserves those features
and memory. Both retain unrelated terrain flags and map state. Notices/redraw
are cosmetic diagnostics; a required map failure retains preceding counter
effects. Autoplay suppresses notices without suppressing transformation.

Twelve new groups cover arithmetic, solar admission, warning repetition,
counter wrap, severity, all terrain identities, repeated visits, borders and
narrow-map aliases, ownership/feature retention, observer gates, autoplay and
failure prefixes. They invoke actual shared children; synthetic presentation
callbacks do not supply gameplay or mutate query state.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1464 of 1466**, with two existing
platform/audio skips. The shared probe passed **1431 groups** across 158 coverage
families; source identity verified **746 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-warming-native.json` | `1cd43ab1a2ab6129ec1230731799689520c5688d05dafa3b3594f79c85dbe50f` |
| `m4-warming-identity.json` | `39cc3f24439a43040dbafd2bb9b34ad6e381014643ea96cb7ca5fdb17794baa8` |

F04's nonzero incoming-turn admission and general desktop composition remain
open, along with ordinary city pollution/meltdown and full human/AI acceptance.
This checkpoint does not claim a new desktop playthrough. Browser/cross-platform
qualification and network execution remain deferred.

## W05 city pollution and reactor meltdown — 2026-11-23

`CityPollution` consumes the actual C03 workspace, performs W05's risk sampling
and single B-neighbor selection, and then runs the independent reactor checks.
It uses actual W05 placement, C08 facilities, V03 Fusion Power and direct U07
blast. Solar Plant reduction, no-draw thresholds, signed intensity multiplication,
scenario suppression, wrapping and rejected-candidate behavior are retained.
Presentation respects exploration and pollution-message suppression; ordinary
pollution has no added producer-level autoplay filter.

Meltdowns preserve the distinction from nuclear attacks: SDI, garrison deletion
and nuclear diplomacy are not invoked. The real blast changes population,
features and fallout before reactor removal. Disorder/Fusion Power and difficulty
laws govern admission. No-pollution scenarios and autoplay do not suppress the
hazard; actual C04 order restoration before this producer does.

The U07 dependency previously stopped gameplay on cosmetic notice, animation or
redraw exceptions. Those failures now remain diagnostics while the action/blast
completes, following X01's production boundary. Combat delivery, foreign-city
movement and planted devices retain the child diagnostics. Required RNG or
other gameplay failures still stop with their committed prefix. A nuclear test
now supplies its entities before execution rather than creating them from an
animation callback.

Twelve new W05 groups cover risk/solar laws, all twenty candidates, suppression,
rejection/no-retry, memory, count wrap, all stock meltdown difficulties and extra
unsigned difficulty, actual fallout with surviving garrisons, real mood recovery,
cosmetic failures and required RNG failure prefixes. The first suite run exposed
two older caller tests expecting cosmetic nuclear failures to abort. Their
expectations were corrected against X01, parent diagnostics were retained, and
a distinct missing-RNG case preserves required-failure coverage.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1476 of 1478**, with two existing
platform/audio skips. The shared probe passed **1443 groups** across 159 coverage
families; source identity verified **748 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-city-pollution-native.json` | `fd0502483d79d68a788604ded5f3ebda1017db0b21ca03eda1618c796a4dd1ce` |
| `m4-city-pollution-identity.json` | `b7356549138d09056fe88aca7b77e969e0f0a2dc3b9e2f3a3a04dd741687eaf3` |

Full F04 city/civilization/global composition, generated desktop integration and
complete human/AI end-condition acceptance remain open. This is shared mechanics
and caller validation, not a new desktop playthrough. Browser/cross-platform
qualification and network execution remain deferred.

## P09 throne progress and semantic upgrades — 2026-11-23

`ThroneProgress` uses actual V08 score and C08 Palace queries for old-turn
progress. It preserves scenario/exact-mode exclusion, signed owner admission,
the strict accelerated-start turn boundary, one increment per check, signed
progress wrap and awards while the visual preference is disabled. Threshold
evaluation preserves the modulo-32-bit sum before signed division; the query
has no admitted mutation callbacks. The explicitly nonterminating `INT32_MAX`
stage takes P09's permitted safe stop.

`ThroneUpgrade` captures piece/overlay mode and exposes explicit semantic
choices. It preserves the specified availability wrapper's omission of piece 7,
revalidates current actor and eligibility, and commits a selection exactly once.
Cancellation/disposal before acceptance leaves the room unchanged; the earlier
progress award remains. Existing overlay bits and unused classic storage remain
intact. Pixel hit fallback and uninitialized/rejected temporary selections are
not gameplay actions under P09's adopted presentation contract.

`ThroneProgressOperation` routes the decision privately through Session. Its
owned report exposes piece levels and overlay ownership without recalculating
score or mutating state. Ten new groups cover threshold boundaries/wrap, actual
score and admission, hidden awards/count limits, selection/cancellation, owned
reports, F06 round trips and private/wrong-recipient/invalid/stale replies. The
existing V01 creation case now verifies initial levels and zero progress.
P09 is included in both build-time and validator source-identity inputs.

The initial build caught a missing namespace import in the new Session test;
it was corrected before the successful runs. Root/App formatting and verification
passed. Both final Release builds completed with zero warnings/errors. Native
tests passed **1486 of 1488**, with two existing platform/audio skips. The shared
probe passed **1453 groups** across 160 coverage families; source identity
verified **752 compiled inputs**. The 43-project graph, eight identity tests and
three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-throne-native.json` | `0a3142194790960faab933f5321f95ba112a90cc1bf5f53e11d506888bfb1d39` |
| `m4-throne-identity.json` | `02a770c40c4725415a77f830a17a31c41a60eeded32f99472bdef65e9d80340e` |

Full F04 orchestration, generated desktop integration, authored throne display
and complete human/AI end-condition acceptance remain open. This checkpoint
does not claim a new desktop playthrough. Browser/cross-platform qualification
and network execution remain deferred.

## F04 global advancement composition — 2026-11-23

`GlobalTurn` now joins actual W06 spawning, W05 warming, A01 power publication
and P09 throne progress on the incoming nonzero turn. Only after these children
finish does it advance the signed turn/year and peace counter, clear EndOfTurn,
run the actual U04 reset and V04 reputation maintenance, schedule history and
process the first newly arrived V07 ship. The initial turn skips only the four
old-turn children. Private lifecycle and throne decisions retain their operation
and accepted effects; cancellation of an upgrade preserves the progress award
and continues advancement. Landing acknowledgement precedes the final Won flag.

`GlobalTurnOperation` routes private decisions through shared Session interaction
helpers and reports cosmetic child failures after gameplay completes. Required
failures retain their accepted prefix and prevent replay. `HistorianReport` uses
P07's adopted wealth ranking, last-equal ties and disclosure predicates, with
immutable identified ranks and no disclosure of treasury amounts. The permitted
stock topic choice consumes no extra gameplay RNG; F04 still draws its history
deadline during autoplay. P07 is now part of both source-identity input lists.

Twelve new shared groups cover actual spawning/warming/scoring composition,
old-turn history sampling, private upgrades, reset/incident/reputation/history
ordering, strict and wrapped deadlines, extinct-slot arrivals, one arrival per
advance, cancellation/disposal, failure prefixes and Session privacy/idempotence.
The first builds caught a report-constructor ambiguity and a missing test content
namespace. The first shared run caught a fixture that had omitted the increased
score penalty after warming clears the AI tally; the corrected case explicitly
asserts that ordering. All were corrected before final validation.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1498 of 1500**, with two existing
platform/audio skips. The shared probe passed **1465 groups** across 161 coverage
families; source identity verified **758 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-global-native.json` | `dcdec1f0cdd41630657cc32b2d628f95dc1684d60bec1284e7a23dd4ae92d988` |
| `m4-global-identity.json` | `af71f5acf349dcf48b68af7d3120260ffd34dc06bb228304754e35255abbc8bf` |

Full F04 civilization/city sequencing, generated desktop integration and complete
human/AI end-condition acceptance remain open. The authored desktop profile still
uses its limited global path; this checkpoint does not claim a new desktop
playthrough. Browser/cross-platform qualification and network execution remain
deferred.

## Desktop global-turn integration — 2026-11-23

The authored desktop `turn.end` command now retains `GlobalTurnOperation`
between movement completion and civilization upkeep. It constructs the actual
spawning, warming, power, throne, lifecycle/reset and diplomacy children, and
forwards private decisions without repeating movement exhaustion or global
advancement. The previous inline calendar/reset/reputation sequence is removed.
`TurnOpeningOperation` now owns only post-global retirement and pending outcome
presentation; it cannot scan and process a second spaceship arrival.

Two additional shared groups exercise the real desktop command through both
direct and console clients. They verify old-turn throne selection/cancellation,
preserved progress, warming before score, history after advancement, stale reply
rejection and classic save/load persistence. A simultaneous-arrival case keeps
playing after the local ship wins and proves that the extinct competitor's ship
is not processed in the same turn. Existing profile admission restrictions and
the remaining civilization/city and generated-game work remain explicit.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1500 of 1502**, with two existing
platform/audio skips. The shared probe passed **1467 groups** across 161 coverage
families; source identity verified **759 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

The unchanged native input driver also passed all **26 desktop checks** under
isolated Xvfb/session-bus and in-memory settings. It exercised actual application
input, city controls, native file dialogs, save/load and direct-client comparison,
finance/government decisions, score/Hall reports and both scheduled retirement
paths. The new throne/history and simultaneous-arrival cases are Session command
checks; the native run covers the existing authored playthrough.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-global-desktop-native.json` | `11a888124df2064e501797356f8972ecda9cc542bf168547d08938cd3bb2a4f3` |
| `m4-global-desktop-identity.json` | `1329f8a09acb78f367125a793905d0a739dc6a6e80fa450d5783de367ebe63f3` |
| `m4-global-desktop-play.json` | `00c442da718a881b8789c033986278fbb762a5162fafe688f07a30bec0692057` |

Full F04 civilization/city sequencing, generated desktop play and complete
human/AI end-condition acceptance remain open. Browser/cross-platform
qualification and network execution remain deferred.

## F04 human-city pollution and upkeep — 2026-11-23

`CityHumanMaintenance` now owns stage 6's single current-human test for the
captured city owner, followed by actual W05 pollution/meltdown and C08 upkeep.
There is no alive filter or second human-control check between the children.
The desktop turn captures the owner before food/production/research and invokes
this shared stage after research finishes, before terrain-goal publication and
city summary processing. Its nuclear dependency uses the same actual loss,
lifecycle and incident services constructed for the turn.

`BuildingMaintenance.Run` now returns cosmetic diagnostics while preserving
forced-loss credits and subsequent payments. Required quote failures still stop
with their committed prefix. The old test that changed government, ownership,
rules and treasury inside an InHock callback has been replaced by ordinary
payment observations: C08/X01 explicitly excludes those synthetic interleavings.
The desktop retains presentation failures while finishing the admitted turn.

Six new groups cover captured-owner admission with a different current city
owner, extinct slots, pollution before payment, a reactor melting before it can
be charged/sold, multiple cosmetic failures, and required sampling/quote failures.
A direct/console desktop case imports industrial knowledge and a populated city,
uses actual C03 economy to produce pollution, verifies that this new pollution
has not been processed early by warming, and preserves it through classic
save/load. Reactor behavior is covered by the composed core stage; the authored
desktop profile still does not admit nuclear plants.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1506 of 1508**, with two existing
platform/audio skips. The shared probe passed **1473 groups** across 162 coverage
families; source identity verified **761 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

The first isolated native UI run timed out importing the malformed-save fixture;
its snapshot showed the file library still waiting for a native file selection.
The diagnostic was retained. An unchanged retry under isolated Xvfb/session bus
and in-memory settings passed all **26 desktop checks**, including file dialogs,
save/reload comparison, finance/government, score/Hall and retirement paths.
No driver or application workaround was added for that timeout.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-city-maintenance-native.json` | `f684ecf5ba74727b08c9da608649ef063e25f4b5d264871f908d0d8353f247bd` |
| `m4-city-maintenance-identity.json` | `db1c122c7b8482429cf7006e88e576a691e2b5de8ac6cda31d93880b9d77e0b2` |
| `m4-city-maintenance-play.json` | `d98389b86883ff95cbd5003952258fe1193bfa344752769f4454d3dc0b121a41` |
| `m4-city-maintenance-play.first-failure.json` | `635d29cfda24a48799d7813abe4d24903d604789cccd83185f7f1852491db15a` |

F04's unresolved government-score and best-shield producer definitions, full
city/civilization sequencing, generated desktop play and complete human/AI
end-condition acceptance remain open. Browser/cross-platform qualification
and network execution remain deferred.

## F04 shared city-pass boundaries — 2026-11-23

`CityPass` now owns the defined city-pass entry, traversal and completion
contract. The desktop retains the operation through its actual city production,
research and maintenance children. Entry resets best-shield surplus as explicitly
specified, along with government scores, wonder notices and attitude-dirty state.
The unresolved best-shield producer does not justify retaining a previous pass's
summary. City memory now uses the pass civilization's alliances, including when
an actual child transfers the selected city to another owner.

The pass captures its upper scan limit once, selects current live/owned cities
in descending order, clears airlift use before the child and skips summaries for
an actual removed-city result. It accumulates balance/size and disorder, then
adds twice Despotism into Anarchy with signed wrapping and runs actual A08
rates/strategic scores, including their final wonder-notice clear. Completion
is idempotent; required policy failure retains its committed prefix and prevents
a second application. The two unspecified city-analysis formulas remain open.

Six specification-derived groups cover empty passes and all entry resets,
actual C01 transfer/removal/creation and lower-slot reuse, the captured upper
limit, pass-owner alliances, global/destroyed Apollo markers, normal-observer
limits without alive/knowledge filters, removed results, AI rate predicates,
signed score completion, admission and required failures. These are pass-boundary
tests, not a substitute for the still-incomplete general city-turn parent.

Root/App formatting and verification passed, and both Release builds completed
with zero warnings/errors. Native tests passed **1512 of 1514**, with two existing
platform/audio skips. The shared probe passed **1479 groups** across 163 coverage
families; source identity verified **763 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed. The unchanged isolated
native input driver passed all **26 desktop checks**, including save/load,
finance/government, score/Hall and retirement paths, on its first run.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-city-pass-native.json` | `6831e09d9602ca3963e86afae8de9e0b98763ef0289a7222e04ee5bf5ba54d42` |
| `m4-city-pass-identity.json` | `1271813c8010731a3b4723b9dd2610424e2df202f9bf16fdc0a70819a8d0f8b4` |
| `m4-city-pass-play.json` | `135baff5c3fdd100235721192fa0945d569bcd92ddc36d3699dd2ddece3e5faf` |

The user supplied an original MGE installation at
`/home/peter/project/civ2/Civ2`. Its `civ2.exe` is a PE32 Intel i386 Windows GUI
executable with SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Neither `wine` nor `wine64` was on this Linux host's PATH. Only file identity was
checked; the executable was not run or analyzed, and no original-game behavior
or version compatibility is established by this check. Best-shield surplus and
all seven government scores have defined classic-save fields, so controlled
original-game before/after saves can provide independent evidence for the two
F04 formula gaps on a suitable host. The user offered a Windows handoff if
needed; defined desktop work can still progress here and the goal remains active.

Full city/civilization sequencing, generated desktop play and complete human/AI
end-condition acceptance remain open. Browser/cross-platform qualification and
network execution remain deferred.

## A08 technology exchange in V04 turns — 2026-11-23

`AiAdvanceExchange` now scans admitted advances in order using actual A08
valuation and primary candidate draws. It retains first/strict-better selections
before starting either ordinary V03 grant, supports directed one-sided alliance,
tribute and advance-count gates, and captures the specified leading-human
exception. The scenario option uses its existing S01/F06 state: the same stored
option excludes conquest technology and AI exchange. No extra live/alive or
AI-only admission filter is added to this child.

Both grants use retained `ResearchGrants` operations, preserving government and
emissary decisions, Great Library propagation, ordinary acquisition effects and
cosmetic diagnostics. Bilateral tribute is committed before a gift grant. A
required failure preserves preceding tribute, knowledge and completed grants;
resuming a failed operation cannot repeat the effects. Disposing a pending
exchange preserves its first grant and does not begin the second.

`DiplomacyTurn.Begin` now composes this child at both its periodic-alliance and
stale-contact exchange sites. It retains entry draws and relation progress while
an actual research child waits. Its existing synchronous entry remains for
callers without retained exchanges, including the authored human desktop turn.
Other audience, review, military-aid and war-ally policy boundaries remain
explicit; this does not claim a complete AI diplomacy turn or generated game.

Eight new specification-derived groups cover candidate ties and prerequisites,
actual knowledge/source/research changes, both gift directions and threshold
boundaries, each leading-human gate without an alive filter, scenario/local
admission, retained government and emissary choices, disposal and failure
prefixes. The V04 parent case exercises both exchange sites with a real Great
Library grant to the human player, pauses for the resulting government choice,
and completes the second transfer and later relations without replaying earlier
work. The emissary case retains a failed window-close diagnostic while both
grants complete. No synthetic presentation callback mutates gameplay inputs.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1520 of 1522**, with two existing
platform/audio skips. The shared probe passed **1487 groups** across 164 coverage
families; source identity verified **765 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed. The unchanged isolated
native input driver passed all **26 desktop checks** on its first run. That run
regresses the existing authored desktop playthrough; the new AI exchange and
Great Library continuation evidence comes from the composed shared core cases.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-exchange-native.json` | `23240629bda478def7d249b368403c3028d94957090ea535353573e964f87e01` |
| `m4-exchange-identity.json` | `27903c2e5dfaf4165fd4f68390b90b68f7a4926a49ba786e9ed54235edcf3f65` |
| `m4-exchange-play.json` | `5431788f945872ca4685e4422f2b8500b8845106220fedeec804cd086a3651c7` |

F04's open city-analysis arithmetic, full city/civilization sequencing, remaining
AI diplomacy policies, generated desktop play and complete human/AI end-condition
acceptance remain open. Browser/cross-platform qualification and network
execution remain deferred.

## A08 war-ally requests and private turn decisions — 2026-11-23

`WarAllyRequests` now implements A08 §9's paid and unpaid request paths using
actual V05 emissary reception, A08 advance valuation, V03 retained grants and
V04 alliance/war operations. It checks the sender's turn slot and distrust,
scans live distinct enemies in order, clears Fighting on non-request paths,
and returns after the first request without that enemy's cleanup. Refusal
retains the specified signed contact-date updates.

Paid offers capture their amount before technology selection and retain it
through the decision. Technology may come from the enemy when the local-human
intelligence gates permit it; actual acquisition by the sender precedes the
ordinary grant to the partner. Both grants finish before treaties, war, contact
dates and payment. Unpaid requests retain their separate contact/write order
and perform no payment or status redraw. The minimum paid offer can make the
sender's treasury negative; no affordability recheck was introduced.

`DiplomacyTurn` now invokes the real request after relation review and military
aid when this composition is supplied. `DiplomacyTurnDecision` distinguishes
research and war-ally children. `DiplomacyTurnOperation` routes private emissary,
intelligence, offer and nested research choices through the existing Session
interaction helpers. A cancellation declines its current choice: declining a
later revolution still completes a previously accepted war offer. Cosmetic
status/close failures are reported after committed gameplay; required child
failures and disposal retain their actual prefix without completing later effects.

Nine specification-derived groups cover paid/unpaid acceptance, requests against
an existing ally, admission and cleanup, first-enemy/refusal behavior, borrowed
technology and government choices, local-versus-partner intelligence gates,
strict technology ties, payment limits, cosmetic and required failures, disposal,
private recipient routing and cancellation/timeout at all three decision kinds.
The V04/Session integration also checks that legacy war-ally callbacks are not
invoked and that cosmetic diagnostics propagate through the parent to the final
command result. Remaining audience, security/review and military-aid boundaries
are not supplied by these tests.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1529 of 1531**, with two existing
platform/audio skips. The shared probe passed **1496 groups** across 165 coverage
families; source identity verified **768 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed. The unchanged isolated
native input driver passed all **26 desktop checks** on its first run. That
native run covers the existing authored playthrough; the new war-ally policy and
private decisions are exercised through actual Core/Session compositions.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-war-ally-native.json` | `015484e4dbbe73856b97ee9a4805834a6a2be5a2e5a8909791aeb89df0a90ae1` |
| `m4-war-ally-identity.json` | `ce49d30a29a29b61fdd8a30c32bd8625317c77075d25b684c5c42c2d9fe18e3b` |
| `m4-war-ally-play.json` | `088c3e271622a87621149ef6332f4a83ae2a3aab1135d6cbe78d0a3af9c528dd` |

F04 city-analysis formula gaps, complete city/civilization orchestration,
remaining diplomacy policies, generated desktop play and complete human/AI
end-condition acceptance remain open. Browser/cross-platform qualification and
network execution remain deferred.

## A08 security and actual military-aid scan — 2026-11-23

`DiplomaticStance.FeelsSecure` now implements A08 §6's security query, including
forward Aggressor/Vendetta shortcuts, both directed human-threat checks without
alive/self exclusions, active nuclear strength, independent third-party relation
terms and the live leader attack trait. Regional accumulation visits 1–62,
preserves the local city-defense veto, signed division/truncation and final
shared-city gate, and changes neither treaties nor evaluation workspace.

`MilitaryAid.Run` implements A08 §8's ordered ally/enemy/unit admission and real
map queries. Its first bounds test requires an outside coordinate, and the
following city-owner query cannot return an owner there. This preserves the
specified no-transfer behavior, including a city-marked fallback tile; no dormant
transfer path or inverted bounds test was introduced. `DiplomacyTurn` now calls
this implementation directly after relation review. The old required military-aid
callback and the desktop adapter's unsupported placeholder are removed.

Four new shared groups cover human direction/dead/self slots, nuclear shortcuts,
region boundaries, local defense equality, truncated aggregate ratios, shared-city
gating, alliance/hostility thresholds, leader traits, independently overlapping
third-party terms, local-only admission, and flat/cylindrical no-transfer cases.
Existing V04 exchange and war-ally Core/Session compositions now execute actual
military aid. The first validation run exposed one stale callback-count assertion
in the exchange case; it now verifies arrival at the subsequent war-request step.
The initial failed probe remains `m4-security-initial-failed.json` for diagnosis.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1533 of 1535**, with two existing
platform/audio skips. The shared probe passed all **1500 groups** across 166
coverage families; source identity verified **770 compiled inputs**. The 43-project
graph, eight identity tests and three coverage tests passed. No graphical behavior
changed, so this checkpoint uses the native composed turn/Session tests and
App build; it does not claim a new desktop input-driver run.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-security-native.json` | `c4d8eb69091bd8f5ca19cf16cb55c799c9969bdfc4802ed9f10c3940320f7540` |
| `m4-security-identity.json` | `5a4f54218ec8b86d6f411ae3c8cc602772f617c8ca2a7bea2262cfa2040e3f38` |

Periodic relation review still has an undefined incoming attitude value on paths
without a producer; the explicit owning-specification missing-input policy remains
required. Full audience integration, F04 city-analysis formula gaps and complete
city/civilization orchestration, generated desktop play and complete human/AI
end-condition acceptance remain open. Browser/cross-platform qualification and
network execution remain deferred.

## A08 retained relation review and border decisions — 2026-11-23

`RelationReview` now implements A08 §7's local review using actual V04 border
measurement, A08 stance, retained V05 reception, W03 city knowledge/exploration,
C01 nearest destinations, U01 relocation, V02 Anarchy and V04 war/attitude effects.
No-peace complaints retain their random draws and first emissary request; A08's
Intrusion relation maps to V04's HostileIntent through the specified shared 0x20
encoding. Peace paths retain recent-peace/contact gates, first warnings and
repeated withdraw-or-fight decisions. Withdrawal clears each reached marker before
its owner check, moves only admitted units with a real destination and preserves
range-dependent counters. Democracy skips the senate draw; other governments draw
before the Republic test. Scenario prohibition suppresses the actual Anarchy step.

The operation retains an optional supplied attitude delta as an explicit input.
Border assignment replaces it when specified. Otherwise nullable signed arithmetic
propagates the unknown through all independent queries; the final no-contact
rule can establish zero. An unresolved contacted result fails before AdjustAttitude
while preserving border effects, including completed withdrawal or war and marker
cleanup. It never supplies the historical replay constant or an invented zero.
Nuclear reductions are independent, rank halving uses signed division, and explicit
arithmetic shifts and wrapped additions retain their separate meanings.

The full retained `DiplomacyTurn` composition runs this service before actual
military aid and war-ally requests. Session routes reception, warnings and
withdraw-or-fight choices privately to the intruding participant. Canceling a
warning acknowledges it; canceling a repeated demand chooses withdrawal. Required
notice failures and disposal preserve their actual prefix. Cosmetic tile/leader
failures are reported separately from committed effects.

Seven new groups cover unknown/supplied deltas and final zero publication, border
warning/cleanup and recent contact boundaries, actual emissary refusal/acceptance,
withdrawal and failures after relocation, senate/scenario/war effects, arithmetic
and wonder/race/rank thresholds, and private V04/Session decisions. The first probe
caught a fixture that inherited Settler role for its intended military intruder;
the role is now explicit. That initial result remains `m4-review-initial.json` for
diagnosis. Final private cases exercise both completed warnings and withdrawal
retained before a missing-delta failure, through actual Core/Session composition.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1540 of 1542**, with two existing
platform/audio skips. The shared probe passed all **1507 groups** across 167
coverage families; source identity verified **773 compiled inputs**. The 43-project
graph, eight identity tests and three coverage tests passed. This checkpoint uses
composed native turn/Session evidence and the App build; it does not claim a new
run of the graphical input driver or a general desktop AI game.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-review-native.json` | `ff682b2207f5e9febac03a5ef4ae58052a49e312632c8b695991bf44f34b806e` |
| `m4-review-identity.json` | `e0ed1ae9acd6b4f67ce03962ebd3e0ef7bcd152a5780358bdf7f5e46800756eb` |

The original incoming attitude-delta producer, full V05 audience/controller,
F04 city-analysis formulas and complete city/civilization orchestration, generated
desktop play and full human/AI end-condition acceptance remain open. Browser,
cross-platform qualification and network execution remain deferred.

## V05 retained technology exchange and sales — 2026-11-23

`DiplomaticTechnology` implements V05 §3's survey, sale and repeated exchange
controller with actual A08 valuation, V05 reception, V03 grants and captured
technology selection. Surveys preserve later ties, blocking-wonder scans and the
specified prerequisite admission. Sales retain the offered price, use live gold
and technology on acceptance, preserve signed arithmetic and do not credit the
seller. Exchange retains the captured alliance across iterations, applies refusal,
leader and gift gates, and grants the donor technology before asking the recipient
to choose through the actual captured-technology operation.

The Session adapter routes reception, exchange, purchase and nested research or
government choices privately. Cancel and timeout decline offers; the captured
technology chooser remains non-cancellable. Required failures preserve committed
payment or knowledge; cosmetic failures are reported separately. Nine new groups
cover these paths, pricing boundaries, alternate offers, repeated exchanges,
leader exceptions, gift counters and private Session routing.

The supplied original MGE `RULES.TXT` established Howitzer's fixed unit identity
and verified the other five prerequisite unit identities. V05 Appendix A records
the data hash and evidence scope. No executable code was inspected, and this
data check does not establish original-game behavioral conformance.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1549 of 1551**, with two existing
platform/audio skips. The shared probe passed all **1516 groups** across 168
coverage families; source identity verified **776 compiled inputs**. The 43-project
graph, eight identity tests and three coverage tests passed. This checkpoint uses
native Core/Session composition and the App build; it does not claim a new
graphical input-driver run or full desktop audience integration.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-tech-native.json` | `a48fd87f68b7c29b48a8599a2ccf47322c5af0a3e872d256fd882d24e7052602` |
| `m4-tech-identity.json` | `00cea78c3a214407732458a0ded07b5d2682b1cdc60ed9d17fe26ec0eba4e02c` |

Full V05 audiences, gifts/favors and treaty negotiation remain open, together with
the previously recorded F04 formulas, relation-review input gap, complete turn
orchestration and generated human/AI end-condition acceptance. Browser,
cross-platform qualification and network execution remain deferred.

## V05 retained gifts and military transfers — 2026-11-23

`DiplomaticGifts` implements V05 §4's gift menu with the actual technology survey,
retained V03 grants, C01 nearest-city query and U01 detach/attach operations.
Technology gifts value the best choice even when giving the alternative. Gold
offers use the specified quantized, deduplicated amounts and blockwise attitude
value; accepted off-nominal negative amounts reverse the transfer. Gift counters
retain signed32 and their request adjustment reads signed8, independently of the
signed8 request counter. The future audience entry owns the meeting-counter reset.

Military gifts retain the selected destination identity and use its live position
after unit selection. They update only the specified owner/type counters,
visibility and home, leaving movement, orders and military totals unchanged.
Obsolescence and strength gates execute before transfer. Breakthrough uses actual
V03 ancestry, suppresses index zero and can grant an advance the donor does not
know. Actual grant decisions retain transfer effects and delay the gift tail.

The Session adapter routes menu, city, unit, gold, technology and nested grant
choices privately. Cancel/timeout maps to the specified decline for each prompt.
Required notices stop at their actual mutation prefix; status/city presentation
failures are collected without undoing gameplay. Eight groups cover arithmetic,
rejection gates, transfer/stack effects, breakthroughs, government continuations,
private routing, cancellation and failures/disposal.

Initial probes caught missing/misencoded controlled emissary draws in the
breakthrough fixture and missing terrain-work rules in the government fixture.
The corrected fixtures now exercise the actual grant and city-recalculation
children. Earlier failing receipts are retained for diagnosis; final evidence is
listed below.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1557 of 1559**, with two existing
platform/audio skips. The shared probe passed all **1524 groups** across 169
coverage families; source identity verified **779 compiled inputs**. The 43-project
graph, eight identity tests and three coverage tests passed. This checkpoint uses
native Core/Session evidence and the App build; no new graphical input-driver run
or full desktop audience acceptance is claimed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-gift-final-probe.json` | `01cfa24262cb22f36d6c1ba7410c51e70b85695545b402e75e5cb9f4931b1986` |
| `m4-gift-identity.json` | `885ff0445fa8e1e2595b7f173fa7e4c39ebd77a426556b34ef79dc7f35509270` |

Full V05 audiences, favor/map-exchange and treaty negotiation remain open,
together with the previously recorded F04 formulas, relation-review input gap,
complete turn orchestration and generated human/AI end-condition acceptance.
Browser, cross-platform qualification and network execution remain deferred.

## V05 favors and W03 negotiated maps — 2026-11-23

`DiplomaticFavors` implements V05 §4's local favor controller using actual
technology exchange, A08 patience, retained research grants and V04 alliance
cancellation. Exchange success can reopen the menu; exhausted patience and failed
exchange publish state before returning. War requests retain their price and
money-versus-technology offer across decisions, recheck live affordability and
grant the live first and second technologies in order. Mercenary execution applies
its own treaty flags and publication order, with separate alliance/peace betrayal
branches; it does not inherit generic declaration reputation effects.

`NegotiatedMaps` implements W03 §7's accepted local AI exchange. Exploration and
feature memory follow the specified direction, preserving stale recipient memory
and intersecting donor memory with live features. Unit visibility uses direct
unions, including owners. City sharing preserves remembered size in the reverse
knowledge-only branch and publishes current size in the forward branch. The
acceptance notice precedes request adjustment; final publication precedes redraw.

Session favors privately route target/payment choices and nested grants or
technology exchanges. Direct and favor exchanges now share the same interaction
helper, preserving the non-cancellable captured-technology choice and grant
defaults. Required publication/notice failures retain their actual prefix;
cosmetic status/map failures are reported after committed gameplay.

Ten new groups cover directional map memory and entity knowledge, request wrap,
barbarian memory exclusion, publication failures, sequential war-price rounding,
signed distrust, admission overrides, cached money payment, live second-technology
selection after a government decision, betrayal branches and failure timing.
Private Session tests exercise actual exchange repetition and cancel/timeout
defaults. The initial build caught missing namespace imports in the extracted
interaction helper; both initial and final shared probes passed their cases.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1567 of 1569**, with two existing
platform/audio skips. The shared probe passed all **1534 groups** across 170
coverage families; source identity verified **784 compiled inputs**. The 43-project
graph, eight identity tests and three coverage tests passed. This checkpoint uses
native Core/Session composition and the App build; it does not claim a new
graphical input-driver run or full desktop audience acceptance.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-favor-native.json` | `70b8cb6e147f15ee3913986243ab90d6496170756dee02635c053719b7247c45` |
| `m4-favor-identity.json` | `969a93512bb091a6e315e94078462b33f2c0a5667bb477130d3012eeadd60671` |

Full V05 audiences and treaty negotiation remain open, together with the
previously recorded F04 formulas, relation-review input gap, complete turn
orchestration and generated human/AI end-condition acceptance. Directional
multiplayer map offers are separate work. Browser, cross-platform qualification
and network execution remain deferred.

## V04 negotiated treaties and V05 bargaining — 2026-11-23

`NegotiatedTreaties` implements V04 §5's alliance, peace and ceasefire wrappers
with actual A08 stance evaluation. Alliance adjusts attitude, establishes its
Peace cascade, updates both HasAllied flags and clears the counterpart's request
counter. Peace and ceasefire preserve the shared hotseat attitude-store gate.
Ceasefire clears Tribute before evaluation, retains the request count, and clears
incoming Aggressor flags only after the required notice; AggressorLong survives.

`TreatyNegotiation` implements V05 §5's alliance/peace willingness, technology and
gold payments, solidarity and refusal branches. Peace reasons retain their
distinct meanings; wonder compulsion survives payment refusal while other reasons
are cleared. Technology grants precede valuation of the live best choice. Gold
prices use signed products and the specified repeated decrement; accepted prices
remain cached without another affordability check. Later solidarity rejection
retains payment and distinguishes whether the audience should end. Acceptance
uses actual mediated war, new-common-target flags and the negotiated wrappers.

The Session adapter privately routes actor payment/solidarity decisions and
nested recipient grants. Ten new groups cover treaty wrapper ordering, hotseat
storage, peace/wonder reasons, pricing, live post-grant valuation, paid/unpaid
solidarity refusals, mediated war, refusal messages, request wrap, required
failures, disposal and cancel/timeout. A command-runtime case transfers prompt
ownership from the actor to the technology recipient's government decision.
The initial build caught an unused failure-fixture flag; adding its intended
required-notice test completed that case. Initial and final shared probes passed.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1577 of 1579**, with two existing
platform/audio skips. The shared probe passed all **1544 groups** across 171
coverage families; source identity verified **788 compiled inputs**. The 43-project
graph, eight identity tests and three coverage tests passed. This checkpoint uses
native Core/Session composition and the App build; it does not claim a new
graphical input-driver run or full desktop audience acceptance.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-treaty-native.json` | `f34cc62e61e40797eb66c45157fcd7ef559ff4d983477d6f70052c3f2d358253` |
| `m4-treaty-identity.json` | `85c81fa186f904df1bb07fb00a1ac56be8b82660499b9386f33d9c7adca55d6c` |

Full V05 proposal dispatch, demands and audience orchestration remain open,
together with the previously recorded F04 formulas, relation-review input gap,
complete turn orchestration and generated human/AI end-condition acceptance.
Browser, cross-platform qualification and network execution remain deferred.

## V05 demands, withdrawal and proposal responses — 2026-11-23

`DiplomaticDemands` implements V05 §5's aid, tribute, withdrawal and provocation
using actual A08 patience, C01 nearest cities, U01 relocation, retained V03 grants,
V05 allied technology exchange and V04 alliance cancellation. It captures the
incoming request counter before replacing it, preserving allied mode selection.
Tribute retains contact-age reductions, signed amounts, wonder and technology-gap
gates. Withdrawal preserves signed role checks, the weighted-distance boundary,
visibility/order changes and the original conditional random draws.

Immediate provocation writes the actor's contact turn before the required notice;
later provocation writes it afterward. Both use the specified treaty primitive
rather than generic declaration reputation effects. Payments retain their cached
amount and live gold/technology inputs. A granted technology remains pending
through its real government decision before wanted-choice clearing and ceasefire
publication. The retained HadCeasefire meeting input controls Tribute; its capture
belongs to the still-unfinished audience entry.

`DiplomaticProposals` supplies the specified menu options and response dispatcher.
Treaty responses use the completed bargaining controller; demand responses use the
new service. Cancellation clears Alliance, adjusts/evaluates attitude, presents its
notice, increments reputation and then invokes actual V04 cancellation/expulsion.
Favor and gift execution remain owned by the audience. The Session response adapter
shares treaty interaction handling and privately routes all nested choices.

Eleven new groups cover option/dispatch gates, both provocation failure orders,
withdrawal eligibility and conditional draws, tribute age/ceasefire and negative
amounts, aid and wonder gates, captured request modes, real grants, actual alliance
expulsion, private Session decisions, cancellation and disposal. Initial, expanded
and final probes passed; the expanded cases added aid/wonder and expulsion checks.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1588 of 1590**, with two existing
platform/audio skips. The shared probe passed all **1555 groups** across 172
coverage families; source identity verified **793 compiled inputs**. The 43-project
graph, eight identity tests and three coverage tests passed. This checkpoint uses
native Core/Session composition and the App build; it does not claim a new
graphical input-driver run or full desktop audience acceptance.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-demand-native.json` | `07f7a9ffe3852ba73cef841b7825484804a0b45f28269e9fd80c171fcc034b44` |
| `m4-demand-identity.json` | `64d2d5f71087ecc0ffd1de4886d21c0fb18bbbdf57407c2efebbfb7ef2f07191` |

Full V05 audience entry, opening negotiation and conversation/cleanup orchestration
remain open, together with the previously recorded F04 formulas, relation-review
input gap, complete turn orchestration and generated human/AI end-condition
acceptance. Browser, cross-platform qualification and network execution remain
deferred.

## S02 local negotiation permission and action ordering — 2026-11-23

The next V05 audience dependency exposed an event conversion defect: negotiation
permission had been decoded as an arbitrary nonzero Boolean and exported as1.
S02 §7 and Appendix A require exact equality to4096 for Deny. The semantic field
is now `DenyNegotiation`; live conversion/export uses4096 or0, while the stored
codec continues to preserve arbitrary input values. Cases exercise both the
stored codec and actual live event writer across denial and non-denial values.

`ScenarioPermissions.AllowNegotiation` implements the local first-match gate,
capturing permission before required actions. It classifies the stored selectors,
including Anybody's absent membership bit, does not bind wildcards, and does not
reverse local human participants. Loaded-event, turn and civilization-admission
gates remain with V04/V05 callers. Network calls are explicitly outside this local
entry point.

`ScenarioActions.Execute` supplies synchronous S02 §8 action ordering with required
owning children. It reads each enabled flag live, ignores ordinary child results,
publishes one-shot consumption only after children return, and permits specified
reentry before consumption. `ChangeMoney` implements actual single-indirection
recipient resolution, alive admission, signed32 wrapping and amount-sign
saturation. Six new groups include consumed records that continue denying
negotiation without repeating payment, action mutations and committed failure
prefixes. Other authored actions and a retained dispatcher for participant choices
remain open; no assumed-success event handler was installed in the audience.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1594 of 1596**, with two existing
platform/audio skips. The shared probe passed all **1561 groups** across 173
coverage families; source identity verified **795 compiled inputs**. The 43-project
graph, eight identity tests and three coverage tests passed. No new graphical
input-driver run was performed for these Core/Formats services.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-scenario-native.json` | `4d094ad1be73ca95c278c0c66cf21a1a59f35fb6745ca8b635801519438b1253` |
| `m4-scenario-identity.json` | `2091e90309b6bf46d6c235cc756ff1348573156dccc0bd59aa5587269652fa36` |

Full V05 audience orchestration, the recorded F04 formulas and relation-review
input gap, complete turn orchestration and generated human/AI end-condition
acceptance remain open. This checkpoint needed no original-game observations;
the Windows installation remains available for the unresolved behavioral gaps.
Browser, cross-platform qualification and network execution remain deferred.

## V05 main conversation and common cleanup — 2026-11-23

`DiplomaticConversation` implements V05 §6's main conversation and common cleanup
from explicit retained opening outcomes. It joins actual emissary reception,
senate queries, border measurement, proposal responses, favors and gifts. Actor
menus can suspend through a recipient's real research/government decision and
then return to the actor without repeating accepted effects. Forced treaties use
bare V04 setters, preserving their distinction from negotiated treaty wrappers.

Greeting selection retains initiation, relations, paid-demand, request and address
rules. Denunciation scans the specified directed relations, includes dead third
parties and chooses later credit ties. Border messages use selected city index;
ally region checks cover1…62. Proposal termination and ordinary menu exit keep
their distinct request-counter effects. Patience reads are fresh after children.

Cleanup clears incidents and updates directed hostility, advances signed request
counts with bounded continuation, resets emissary state, handles ceasefire expiry
and retains intelligence after ambassador/Embassy publication. Tutorial conditions
read the actual post-reset asked/response fields. The separate AI-meeting-active
flag remains set until cleanup finishes. Required failures preserve their prefix;
cosmetic close/status failures remain diagnostics. Entry and opening still own
the capture of `HadCeasefire`, opening outcomes and initial active-state setup.

Direct and conversation Session callers now share gift/favor/proposal interaction
adapters. Ten groups cover actual gold gifts, maps, treaties and provocation,
emissary acceptance/refusal, greeting/admission, denunciation, forced treaties,
borders/regions, bounded cleanup, intelligence/tutorial ordering, failures,
private actor/recipient/actor routing, cancellation and timeout. The initial
probe exposed an existing-city fixture collision and an incorrect expectation
that a non-owner retains command results after its private prompt closes. Both
fixtures were corrected; the final probe passes.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1604 of 1606**, with two existing
platform/audio skips. The shared probe passed all **1571 groups** across 174
coverage families; source identity verified **801 compiled inputs**. The 43-project
graph, eight identity tests and three coverage tests passed. These are native
Core/Session integration cases and an App build, not a new graphical input-driver
run or full desktop audience acceptance.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-conversation-native.json` | `77bd1c9dcb2b5412da0d1a490ca47bda8d5d684a8b2e321a0d91e05cbfa19707` |
| `m4-conversation-identity.json` | `4b8d2a6d21df0fe982154b23b25dc7362dc6642d4f2f0351ca6fe257c5562189` |

V05 audience entry/opening and complete desktop composition remain open, along
with the recorded F04 formulas, relation-review input gap, full turn orchestration
and generated human/AI end-condition acceptance. Browser, cross-platform
qualification and network execution remain deferred.

## V05 opening treaty offers and concessions — 2026-11-23

`OpeningTreaties` implements V05 §6 opening alliance, ceasefire and peace offers
using actual emissary, senate, declaration, research-grant and negotiated-treaty
services. Explicit inputs carry the earlier opening's declared-war, paid,
no-contact and hostile outcomes; results carry treaty-made, concession,
senate-blocked and audience-termination outcomes for the enclosing controller.
The phase does not reset the meeting or perform common audience cleanup.

Alliance admission preserves directed relations, later target ties without alive
filtering, target zero, and actual mediated war. Refused solidarity retains
`treaty-made` after an accepted alliance proposal even when no alliance forms.
Ceasefire/peace preserve zero-valued acceptance replies, the conditional senate
and wonder draws, concession-roll gates and their distinct termination outcomes.
Unsigned strength thresholds and signed cash arithmetic follow the specification.

Technology concessions grant the live wanted advance and retain it afterward.
Cash offers retain their amount while transferring live treasuries. Grovel takes
all live treasury, then scans live technology knowledge in ascending order through
actual government decisions, excluding advances with both prerequisites disabled.
Required-notice failures preserve already signed treaties; cosmetic status
failures remain diagnostics. The private Session adapter declines cancelled or
timed-out treaty offers despite their zero-accepts convention and routes actual
recipient research decisions.

Ten new groups cover alliance gates/targets/solidarity, ceasefire refusal and
compulsion, concession arithmetic and grants, grovel input retention, senate/wonder
sampling, peace admission, private decisions, cancellation and failure/disposal.
The initial and expanded/final native probes passed all groups. Root/App formatting
and verification passed, and both Release builds completed with zero warnings/errors.
Native tests passed **1614 of 1616**, with two existing platform/audio skips. The
shared probe passed all **1581 groups** across 175 coverage families; source
identity verified **804 compiled inputs**. The 43-project graph, eight identity
tests and three coverage tests passed. This is Core/Session integration plus an
App build; no new graphical input-driver run was performed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-opening-native.json` | `87e051559c73176d361dec43f91caea753c1161928db87dec789f232824f4359` |
| `m4-opening-identity.json` | `9802f0f4f7b81085e6ddac5aaca5300d61dd1b6e7578cadc13faafa2a7afe07c` |

Audience entry, earlier requests/demands/threats and complete parent/desktop
composition remain open, together with the recorded F04 formulas, relation-review
input gap, complete turn orchestration and generated human/AI end-condition
acceptance. Browser, cross-platform qualification and network execution remain
deferred.


## V05 complete local audience controller — 2026-11-23

`DiplomaticAudience` now joins V05 §6 entry and opening requests/threats to the
actual mode-one technology exchange, opening treaties and conversation/cleanup.
Entry runs S02 negotiation permission with a required action executor, performs
actual A08 evaluation, captures prior ceasefire/contact state and preserves the
different player/AI initiation rules. Scenario denial exits without ordinary
cleanup; later audience termination reaches the common cleanup service.

War requests execute actual mediated declarations. Tribute payments read the live
demand; technology demands retain actual V03 grants and fresh post-grant valuation,
including a second demanded advance. Allied demand easing, fixed classic technology
identities, historical target overrides, captured-city suppression, conditional
reconsideration and ordered threats follow the repository specification. Required
failures preserve the accepted prefix; cosmetic failures remain diagnostics.

The Session adapter routes nested emissary, exchange, grant, treaty and conversation
choices privately. The conversation interaction adapter is shared with its direct
caller. Cancellation keeps each child's acceptance convention, including opening
treaties where zero accepts. Tests compose real gameplay children; controlled live
changes occur at retained player decisions. The S02 fixture executes real money
actions; other authored-action domains still require their owning implementations.

Twelve new groups cover scenario entry/failure, player rejection, refused reception,
technology identities, live tribute/technology demands, war and dismissal, threat
failure prefixes, reconsideration/protection, repeated exchanges, historical/dead
targets, private grants, disposal and cancellation. Initial runs exposed fixture
assumptions about intervening treaty offers, a replaced captured-city flag, and an
omitted command result schema; corrected fixtures pass.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1626 of 1628**, with two existing
platform/audio skips. The shared probe passed all **1593 groups** across 176
coverage families; identity verified **807 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed. This is Core/Session
integration and an App build; no new graphical input-driver run was performed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-audience-native.json` | `ad1306d837d7558864a4f07a7512a1b626d91c1c114b4e1cf7a0a50897a03a94` |
| `m4-audience-identity.json` | `b9b59918bbfc114f99a730cc41b33ced7431a556e795755d401c57747995fd3c` |

General V04 contact dispatch and production desktop audience composition remain
open, together with the recorded F04 formulas, relation-review input gap, complete
turn orchestration and generated human/AI end-condition acceptance. There is still
specified implementation work available on Linux. Original MGE observations on
Windows remain useful for resolving the evidence gaps. Browser, cross-platform
qualification and network execution remain deferred; M4 is not complete.


## V04 local contact dispatch and AI treaty transitions — 2026-11-23

`DiplomaticContact` implements V04 §10 local dispatch through the actual V05
parent, then publishes common contact flags from the live post-audience state.
It preserves the initial barbarian/turn-zero gates, S02 admission and the audience's
separate permission check, ordered local participant tests, first-contact capture,
AI cadence and historical pair exclusions. The local-mode Session adapter routes
private audience, grant and ally-help decisions through shared child adapters.

AI peace, alliance and war transitions use their specified primitives and notices.
They compose actual A08 security and advance exchange, V03 grants, V04 ally-war
joins and V05 retained ally help. Alliance review retains third-party scan order,
dead-slot eligibility, leader gaps, game-player reputation, observer/autoplay
qualification, both ally joins and the repeated first-side War test for incidents.
NATO notices use actual C01 nearest-city selection. Absent city information remains
semantic absence, without retaining native formatter state. Raw alliance rejection
preserves Peace and does not invoke expulsion. AI war retains its own directional
Vendetta/Aggressor effects without adding declaration penalties.

Thirteen new groups cover admission, cadence/history, forcing and observer notices,
actual exchange and failure prefixes, alliance targets/reputation/nearest-city
selection, incident direction, raw cancellation, war flags, actual ally help,
post-audience contact publication, local-mode private refusal, cancel/timeout and
cosmetic failure diagnostics. A real Great Library acquisition suspends an AI
exchange for the human owner's private government choice before its second grant
and peace publication. The initial and expanded shared probes pass. Production
movement/revelation, diplomacy-turn and desktop caller integration remain open.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1639 of 1641**, with two existing
platform/audio skips. The shared probe passed all **1606 groups** across 177
coverage families; source identity verified **810 compiled inputs**. The 43-project
graph, eight identity tests and three coverage tests passed. This is native
Core/Session integration and an App build, not a new graphical input-driver run.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-contact-native.json` | `a4f441fd9182188c7d79b44405d1d52d51b9ab1bafcd73950080f56a83bbe1c3` |
| `m4-contact-identity.json` | `93593791037bbaa7ac4334bf49b254559101b22ea61694033ed25fc159b905d7` |

The remaining F04 formulas, A08 relation-review input, complete turn orchestration
and generated human/AI end-condition acceptance remain open. S02 full action
domains still need owning implementations. Network contact forwarding and
two-human network parley, browser execution and cross-platform qualification
remain deferred. M4 remains in progress.

## V04 retained contact within the diplomacy turn — 2026-11-23

`DiplomacyTurn` now accepts the actual `DiplomaticContact` service for its stale
AI contact branch. Nested private audiences, Library grants and ally-help choices
complete before the specified Contact/RecentContact cleanup and later slots.
The Session adapter carries full research replies and resets cached windows when
the pending decision changes. Required failures preserve completed effects;
cosmetic child failures remain diagnostics after turn completion.

Six new groups join actual contact, exchange, relation review and ally requests.
They cover alliance/war effects followed by stale-contact cleanup, suspended Library
grants, disposal, required notice failures, retained ally help, recipient privacy,
cancellation and timeout. A case reaches the unresolved A08 review input after
contact completes and verifies that it fails without inventing an attitude delta.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1645 of 1647**, with two existing
platform/audio skips. All **1612 shared groups** passed across 177 coverage families;
identity verified **811 compiled inputs**. The 43-project graph, eight identity
tests and three coverage tests passed. These are Core/Session integration checks
and an App build, with no new graphical input-driver run.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-turn-contact-native.json` | `448c9dd8282eefab8af60ea9494dfdf9da78c76d9996e5dafa1a2a02a8efeeb2` |
| `m4-turn-contact-identity.json` | `6755f0ec95726828e4deb916bfecb3585209f73af548151882e4da95d3391dd5` |

Production movement/revelation and desktop contact composition remain open. The
recorded F04 formulas and A08 input need independent evidence; complete turn and
generated human/AI end-condition acceptance remain outstanding. Browser,
cross-platform qualification and network execution remain deferred. M4 is ongoing.

## W03/U02 retained contact during movement revelation — 2026-11-23

`UnitRevelation.Begin` now composes actual V04 contact at both empty-city and
foreign-unit meeting points. Adjacent disclosure precedes the private meeting;
post-contact goals and outer observers wait until it completes. The continuation
retains the specified entry/neighbor values and rereads the later live operands.
Its synchronous entry rejects contact-capable execution before mutations when
contact is allowed. Contact-free creation and existing synchronous adapters keep
their bounded behavior.

`MovementCompletion` joins revelation after arrival and before special finish,
final direction and cleanup. Actual dispatcher cases charge and relocate before
the meeting, retain that committed prefix during disposal, and release movement
ownership without replaying the move. Both Session movement adapters route private
contact choices and preserve full replies. They now report collected presentation
failures after completed gameplay. This exposed an older animation fixture that
checked unit zero while a passenger moved; it now checks the supplied moving unit,
and the private dispatcher cases assert that no presentation failure is hidden.

Seven new groups cover city/unit meetings, pre-meeting disclosure, deferred outer
visibility, cached/live reads, synchronous admission, disposal and required failure,
actual Library grants, real movement retention, private replies and cancellation/
timeout diagnostics through direct completion and dispatcher adapters.

Root/App formatting and verification passed, and both Release builds completed
with zero warnings/errors. Native tests passed **1652 of 1654**, with two existing
platform/audio skips. All **1619 shared groups** passed across 178 coverage families;
identity verified **812 compiled inputs**. The 43-project graph, eight identity
tests and three coverage tests passed. No new graphical input-driver run was
performed; the App build does not establish graphical audience acceptance.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-reveal-contact-native.json` | `2eb63946e33b97fee64d8a0f237bfd2fee019fdfc154b5fd78b111fbe481816b` |
| `m4-reveal-contact-identity.json` | `8b5966f95179ae4c5cbb707db357468def33554eb96db9c9037861142bf5e187` |

The production desktop service graph, audience presentation, complete turn
orchestration and generated human/AI end conditions remain open. F04 formulas,
the A08 incoming review delta and the other recorded independent-observation gaps
are unchanged. Browser, cross-platform qualification and network execution remain
deferred. M4 is not complete.

## U01 retained creation and S02 unit actions — 2026-11-23

The general desktop composition audit reached S02's action dependency: a scenario
CreateUnit must finish actual U01 revelation/contact before applying its own
veteran and home fields. `UnitCreation.Begin` now retains that boundary. Allocation,
counters, lifetime, fields and attachment precede the meeting; tutorials read the
requested type/owner and live rules afterward. The result remains the originally
allocated slot. Synchronous callers supplying retained revelation are rejected
before allocation, and disposal/failure retains the created unit and prior effects.
Existing synchronous adapters remain available for their explicitly bounded graphs.

`ScenarioUnitActions` implements the real CreateUnit and MoveUnit bodies. Creation
keeps ordered location admission, sea-on-ocean occupancy behavior, one-level owner
resolution, live post-creation veteran/home values, the first home-name match even
when foreign, and byte-domain home absence. Movement assigns orders by classic
unit slot rather than type, with captured descending bounds, signed eligibility,
all eight rectangle comparisons, narrowed destination coordinates and the distinct
All/zero/negative quantity rules. It performs no immediate movement.

Eight new groups cover these action branches and actual creation/contact, including
live tutorial rules, initialized-unit failure/disposal effects and private Session
reply routing. Cancellation and timeout finish contact with its existing defaults;
presentation failure is reported after the scenario's post-creation assignments.
The shared probe passed all 1627 groups. Full retained S02 dispatch and other action
domains remain required before the general desktop service graph is complete.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1660 of 1662**, with two existing
platform/audio skips. All **1627 shared groups** passed across 179 coverage families;
identity verified **815 compiled inputs**. The 43-project graph, eight identity
tests and three coverage tests passed. These checks cover native Core/Session
composition and the App build; no new graphical input-driver run was performed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-scenario-units-native.json` | `102d626078f44b23185a08452ca377a387d08b79a10adbb9cd2cd51df79e6c69` |
| `m4-scenario-units-identity.json` | `97d3c674db55cece77a19d4e231b813487b5447a547f2529bd8a2c6bfb0416bc` |

The next S02 dependencies include V01 forced removal (distinct from last-city
checks), terrain changes and the retained dispatcher/matching hooks. Other U01
creation callers also need to forward contact decisions. Desktop service graph,
audience presentation, full turn orchestration and generated-game acceptance
remain open. Recorded original-observation gaps are unchanged. Browser,
cross-platform qualification and network execution remain deferred; M4 is ongoing.


## V01 retained placement contact and lifecycle replies — 2026-11-23

Civilization creation now retains actual U01/W03/V04 placement contact. The first
settler is allocated before its meeting; capital assignment waits for completion.
The live turn and human-exploration gates then control additional settlers and the
fixed Phalanx, followed by the explicit exploration pass. Disposal and required
failures preserve the allocated unit without running the remaining placement work.

`LifecycleContact` carries these choices through restart, civil war, capture,
unit loss, missions, movement and global reset. Full research replies now pass
through those parents and the corresponding private Session interactions; a
research-help request cannot silently become a technology selection. Existing
integer entry points wrap scalar choices for bounded callers. Creation, restart
and civil-war adapters expose their collected child presentation diagnostics after
committed completion, and bounded lifecycle searches remain schedulable.

Four new groups exercise actual placement/contact ordering, live exploration,
disposal and required failures, private last-city restart replies, cancellation and
timeout, and nested technology help during restart. The help case verifies that
no advance is granted and capital assignment remains pending until the actual
technology choice and audience complete. These are specification-derived native
Core/Session cases, not original-game comparison or graphical acceptance.

Root/App formatting and verification passed, and both Release builds completed
with zero warnings/errors. Native tests passed **1664 of 1666**, with the two
existing platform/audio skips. All **1631 shared groups** passed across 179 coverage
families. Identity verified **816 compiled inputs**; the 43-project graph, eight
identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-creation-placement-native.json` | `82a5c4b8d3c8c2502cf34ce4c41dc766f291cb7a82b98dd838af962ae6164449` |
| `m4-creation-placement-identity.json` | `bc98b1a095a1d490bcf86903f62a76478bc90ac1394d47d9e1877eed935a88e7` |

The original MGE installation remains available for independent observations;
this slice required no executable inspection or Windows execution. F04's missing
formulas and the recorded A08 relation delta remain unresolved. Other U01 creation
callers, full S02 dispatch, generated desktop composition and graphical/end-game
acceptance remain open. Browser, cross-platform and network execution remain
deferred; M4 is ongoing.


## V01 forced removal and S02 destruction — 2026-11-23

`CivilizationRemoval` implements the separate V01 §8 operation. Human control,
kill/recreate low bytes and retirement policy retain their distinct effects. The
inline ascending unit pass preserves sticky relinking, retained unit payload,
carrier wakeup without occupancy filtering and one-slot bound shortening. The
city pass preserves route removal, raw wonder destruction, retained records and
visibility; its final radius repair includes dead and foreign cities and assigns
the eliminated owner, including on Ocean tiles. Ship/alive/exploration and password
effects precede the live remaining-human/recreation tail.

Actual foreign spy escapes and last-settler checks retain their own research
choices before later unit removals. Recreation uses the actual V01 operation;
the non-destructive path retains existing assets while creation may add units and
pause in diplomatic contact. Required failures and disposal retain committed
prefixes. Presentation diagnostics follow completed gameplay.

`ScenarioDestruction` supplies S02's one-level recipient resolution, alive/normal
player admission and local defeat reason before actual removal. Its Session
adapter routes private child decisions and publishes the action result only after
the child completes. Eight new groups cover these branches, invalid stack writes,
real nested Library decisions, private replies, stale replies and cancellation/
timeout. These are specification-derived Core/Session tests, not original-game
observations or graphical acceptance.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1672 of 1674**, with the two existing
platform/audio skips. All **1639 shared groups** passed across 180 coverage families;
identity verified **821 compiled inputs**. The 43-project graph, eight identity
tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-forced-removal-native.json` | `ff3c224ab3afeff455784c628b1f2a99c10869ce73fe9531b8378e75c2863f75` |
| `m4-forced-removal-identity.json` | `8d6afb18c3297a74a1c3777ae8b356ed152ebe879326bbeb2ee2ae00998e2f2d` |

The retained S02 dispatcher and remaining terrain/action dependencies still need
integration, as do other U01 creation callers and the general desktop graph.
Generated-game, full turn and graphical/end-game acceptance remain open. The
recorded F04/A08/original-observation gaps are unchanged. Browser, cross-platform
and network execution remain deferred; M4 is ongoing.

## S02 retained terrain action

Implemented `ScenarioTerrain` with the actual C01 city-removal, U01 deletion and
V01 creation/reset children. The two captured rectangle passes preserve X/Y
order, signed wrapping coordinates, post-deletion next-link reads and live terrain
operands. Whole terrain-byte replacement lives in Formats and updates retained
file flags; improvements, claims, regions and remembered features follow the
specified order. Live city radii are then reapplied. Cityless civilizations are
processed even for empty rectangles and absent Alive membership, with descending
unit deletion, local defeat and actual retained reset.

The operation retains nested last-settler and Library decisions. Its Session
adapter routes private replies, cancel and timeout, and reports cosmetic failures
after committed completion. Periodic yields keep wrapping rectangle scans
cancellable, including an empty inner range, without clamping their coordinates.
Seven shared groups cover these paths, retained fields, parity aliases, stale
map-allocation failure prefixes, network rejection and disposal.

Resolved S02's contradictory parity/fallback phrase through the owning W01 §2
contract: rectangular in-bounds coordinates alias stored tiles regardless of
parity; only rectangular misses reach fallback. The S02 wording and tests now
state that distinction. This is a contract clarification, not original-game
execution evidence.

Validation: root and App formatting and verification passed. Both Release builds
completed with zero warnings/errors. Native tests passed **1679/1681**, with the
two existing audio/platform skips. All **1646 shared groups** passed across
**181 coverage families**; identity verified **825 compiled inputs**. The
43-project graph, eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-scenario-terrain-native.json` | `3e55bf8b91fbd6c7ab5e559e0255a9e6b68e272a8329ee215728c2c6237a5a33` |
| `m4-scenario-terrain-identity.json` | `30591f2f90922793a1bb87a88c1e3bc6a548128aaac1396697130e4e4091d233` |

Full retained S02 dispatch, its remaining action joins, other U01 creation callers
and the general desktop graph remain open. This slice does not establish generated
complete-game or graphical acceptance. F04/A08/original-observation gaps remain;
Windows may support those observations, but specified implementation work remains
available on Linux. Browser, cross-platform and network execution stay deferred.
M4 remains in progress.

## S02 forced war and retained technology grants

Implemented `ScenarioCivilizationActions` for MakeAggression and GiveTechnology.
The parent resolves selectors once, applies the specified Alive/range checks and
invokes actual V04 force-war or V03 research. Forced war preserves child return
values, alliance expulsion and zero-result partial effects. Technology captures
its recipient and advance, passes source zero, and adds no duplicate/technology
filter or immediate ReceivedTechnology dispatch.

The grant operation retains actual recursive Library government and emissary
choices. Its Session adapter routes each choice privately, preserves the captured
operands across replies, and reports presentation diagnostics after semantic
completion. Seven groups cover direct selectors and rejection, same-civilization
child behavior, real alliance expulsion, ordinary dispatch after a zero result,
turn-zero storage and future technologies, duplicate counts, source-zero prompt
gates, Library acceptance through actual city recalculation, failure/disposal,
private/stale replies and cancellation/timeout.

Validation: root and App formatting and verification passed. Both Release builds
completed with zero warnings/errors. Native tests passed **1686/1688**, with the
two existing platform/audio skips. All **1653 shared groups** passed across
**182 coverage families**, and identity verified **828 compiled inputs**. The
43-project graph, eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-scenario-civilizations-native.json` | `1c799c0fb410669d0db2b5dab019b620db69c71db3e3c33e5a29015c1d79c22b` |
| `m4-scenario-civilizations-identity.json` | `4a0101d62b872318a6d4935c745828968e15a09c11df1bc9348c30c6d003a5de` |

Full retained event dispatch, S02 presentation actions and the negotiation/schism
callers that still use synchronous event callbacks remain integration work. Other
U01 creation callers, the general desktop graph, generated complete-game and
graphical acceptance, and the recorded F04/A08/original-observation gaps remain
open. Browser, cross-platform and network execution stay deferred. M4 is ongoing.

## S02 retained dispatch and permission callers

Implemented `ScenarioDispatcher` with all eleven action types, actual retained
gameplay children, live action flags and final-only JustOnce consumption.
`ScenarioPermissionActions` captures the first matching negotiation permission
before actions and executes every matching NoSchism event. Actual contact,
audience and civil-war callers retain their nested decisions before subsequent
gameplay. City capture passes its operation identifier into civil war so separate
event executions retain distinct replay keys.

`ScenarioPresentationActions` implements wave search, CD retry/selection and
text/autoplay policy through required presentation ports. `ScenarioEventOperation`
routes private nested choices and local text acknowledgements, preserving
completion diagnostics, cancellation defaults and stale-reply isolation. Thirteen
shared groups exercise all-action order, live flags, actual grant-notice re-entry,
real terrain/reset and destruction/Library children, creation/contact/permission
nesting, audience entry, schism vetoes, replay identifiers and failure prefixes.

Validation: root and App formatting and verification passed. Both Release builds
completed with zero warnings/errors. Native tests passed **1699/1701**, with the
two existing platform/audio skips. All **1666 shared groups** passed across
**183 coverage families**; identity verified **833 compiled inputs**. The
43-project graph, eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-scenario-dispatch-native.json` | `2ef09b15090651fa621cf4cb61eb22ed841879be7eaf00f43334b923f86dce18` |
| `m4-scenario-dispatch-identity.json` | `ac0d789f6776e737cd4859c8bd16d2eed7abc26f4d748cb1ced06ea681534fdd` |

Desktop media playback, P04 authored text substitutions and S01 suppression
consumption still require composition. Other S02 trigger families and caller
hooks, remaining U01 creation callers and the general desktop graph remain open.
These contract tests do not establish original-game behavior or generated-game,
full-turn and graphical acceptance. The F04/A08/original-observation gaps remain;
specified implementation can continue on Linux. Browser, cross-platform and
network execution remain deferred. M4 is ongoing.

## S02 ordinary triggers with capture and casualty callers

Implemented `ScenarioTriggers` for Turn, TurnInterval, RandomTurn,
ScenarioLoaded, UnitKilled, CityTaken and ReceivedTechnology. The retained scans
preserve captured turn inputs, signed remainder faults, primary random rolls,
live traversal and city names, persistent wildcard bindings and technology
polling across action changes. Matching includes consumed records; the action
child still owns late one-shot consumption. Callers retain their Loaded, Music
and phase gates.

Actual C09 capture invokes the retained trigger after radius/owner/reveal changes
and before center ownership and capture announcements. Actual U06 casualties
invoke it after loss/history/kill-count updates and before deletion, retaining
captured stack links. Their private Session adapters preserve nested event choices,
text acknowledgements and cancellation defaults; retained presentation failures
are reported after committed completion. Capture also forwards civil-war
presentation diagnostics and schedules bounded continuation work.

Nine shared groups cover every ordinary trigger kind, live appended records,
consumed wildcard binding, captured versus live polling inputs, invalid selectors,
required failure/invalidation/disposal prefixes, actual Library decisions during
capture and casualty handling, private/stale replies, cancel/timeout and emissary
closure failure after completed deletion.

Validation: root and App formatting and verification passed. Both Release builds
completed with zero warnings/errors. Native tests passed **1708/1710**, with the
two existing platform/audio skips. All **1675 shared groups** passed across
**184 coverage families**; identity verified **836 compiled inputs**. The
43-project graph, eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-scenario-triggers-native.json` | `a3f6b877788997c67208b19898e660c359b33091a82c6dee392d946bc85580ab` |
| `m4-scenario-triggers-identity.json` | `8a9131bc107e66969570eabb5020576967f081088168368d351cc77e81df0d2b` |

F04 turn-pass orchestration, F06 post-load, V05 cession and foreign-minister event
hooks remain open, as do parser/desktop presentation composition and broader
enclosing diagnostic propagation. General desktop/generated-game, full-turn and
graphical acceptance still require integration. These are specification-derived
checks, not independent original-game observations. The recorded formula/evidence
gaps remain; browser, cross-platform and network execution remain deferred.
M4 is ongoing.

## F06 retained post-load events and unit cleanup

`ClassicLoadDialog.Begin` now retains the accepted-file workflow around the actual
core loader. Its typed child factory runs after content/world installation, so
map-bound services can use loaded storage. ScenarioLoaded runs behind the live
Music/Loaded gate after option copying and sound configuration; pending event
choices finish before music synchronization, title presentation and cleanup.
The standalone workflow keeps the load's world pin through pending choices and
bounded work, publishes at yields, and releases ownership on completion, failure
or disposal. The original `Load` API shares the implementation while keeping its
synchronous compatibility services.

`ClassicLoadedUnitCleanup` performs the real local repair/deletion scan through
retained U01/V01 children. It preserves host publication, dead-record link clearing,
stack validation, negative-coordinate deletion and the live end bound. Actual
last-settler restart can retain a recursive Library government decision. Completed
effects remain visible on failure or disposal; child presentation diagnostics are
available to the owner after completion.

Six shared groups exercise real byte loading, delayed cleanup and path publication,
unchanged client identity, pin release, option/event gates, input failure, disposal
before/after input, invalidation, cleanup resource failure, live scan changes and
actual restart/Library failure prefixes. Existing synchronous load tests also pass.

Validation: root and App formatting and verification passed. Both Release builds
completed with zero warnings/errors. Native tests passed **1714/1716**, with the
two existing platform/audio skips. All **1681 shared groups** passed across
**185 coverage families**; identity verified **838 compiled inputs**. The
43-project graph, eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-retained-load-native.json` | `2c9f87ada1e64a9bf6c85041bc8494eb42e999421bf9fbc9219a013addccc2a4` |
| `m4-retained-load-identity.json` | `5ab40e431cd22adedcbceda07df85fbefcea6bd1c9cea541e982d342aeaaa1ec` |

The trusted host still must own the cooperative mutation lane and route pending
choices to their participants. Desktop host UI, parser/media composition and full
load/generated-game acceptance remain open. F04 turn passes, V05 cession and
foreign-minister scenario hooks also remain integration work. These authored
checks do not supply original-game evidence or resolve the recorded formula gaps.
Browser, cross-platform and network execution remain deferred. M4 is ongoing.

## F04 retained single-player turn entry

`SinglePlayerTurnEntryOperation` joins the human-wonder snapshot, actual global
advancement when there is no runtime resume slot, scheduled retirement/pending
outcome handling, and the four ordered S02 turn passes. A quit outcome finishes
before events. Event admission reads current Loaded/resume/reprocess values after
outcomes; admitted passes each read their own live turn argument without repeating
that gate. Actual global, outcome and scenario choices remain suspended in their
children, with private recipient routing and no replay of accepted effects.

The runtime reprocess flag is separate from F06's signed saved word. F06 installation
retains the incoming runtime value, and the authored desktop movement boundary
clears both runtime/saved resume pairs. The desktop end-turn command now uses the
shared entry before its existing restricted city/civilization path. Its profile
continues to exclude loaded scenario events; the general event service factory is
exercised through the real Session workflow, not claimed as desktop scenario UI.

Seven shared groups cover the resume/admission matrix, saved/runtime retention,
fresh pass arguments, one-time admission, real throne and outcome ordering,
quit-before-events, private recursive Library choices, stale replies, terminal
idempotence, disposal, required event failures and deferred global presentation
diagnostics. Existing direct/console desktop turn and outcome regressions pass.

Validation: root and App formatting and verification passed. Both Release builds
completed with zero warnings/errors. Native tests passed **1721/1723**, with the
two existing platform/audio skips. All **1688 shared groups** passed across
**186 coverage families**; identity verified **840 compiled inputs**. The
43-project graph, eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-turn-entry-native.json` | `088c349da397da940a81d611b086d579b182531fca030e63a2295632f0314f02` |
| `m4-turn-entry-identity.json` | `3cdd41eed2bc74f3da2542d7d7bb95e17f4964b1211f20c84532841a0c967c72` |

Full civilization/AI scheduling, general desktop scenario composition and
generated-game/graphical acceptance remain open. The unresolved gameplay formulas
still require specification clarification or independent original-game evidence.
This checkpoint uses specification-derived expectations and adds no original-game
observations. Browser, cross-platform and network execution remain deferred.
M4 is ongoing.

## V05 retained negotiated city transfers

`DiplomaticCities` executes offered-city lists against descending live ID lookup,
including duplicate IDs and repeated same-owner cession. Its local cession joins
real S02 CityTaken actions, U01 cargo deletion with V01 lifecycle checks, C06
production eligibility and C08 Leonardo upgrades. The current city changes through
the actual nearest-city query after a garrison transfer; later unit tests,
production and founder/captured-turn writes use that current city. Exploration
still uses the original center. A missing nearest city stops at the next required
city read, preserving prior ownership/counter changes without an invalid write.

`DiplomaticCitiesOperation` retains nested Library and text choices through the
private Session path. City ownership/radius changes precede the scenario choice;
center ownership and the captured unit scan follow its completion. View failures
are reported after gameplay, while required failures preserve their completed
prefix and stop the operation. `TransferredCityProduction` shares C09/V05's
production repair, retaining the first eligible defender rule, signed arithmetic,
anti-horse bonus and zero-cost divide failure. Existing capture callers use it.

Eight shared groups exercise real child operations, live offer lookup, duplicates,
wrapped counters, current-city replacement, supported cargo deletion, Leonardo,
fresh post-event unit bounds, retained captured turns, exploration without feature
memory replacement, private/stale replies, cancellation/timeout, disposal and
required/cosmetic failures. C09 capture regressions also pass.

Validation: root and App formatting and verification passed. Both Release builds
completed with zero warnings/errors. Native tests passed **1729/1731**, with the
two existing platform/audio skips. All **1696 shared groups** passed across
**187 coverage families**; identity verified **844 compiled inputs**. The
43-project graph, eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-city-cession-native.json` | `82a3a51898c8a48d65197d969a211b93d31f5df7f4b37f357ac30c0c6211f6cd` |
| `m4-city-cession-identity.json` | `ca48829f38e524d2bcf908ca48872a8796129a13f92247349e2799790dfa8481` |

Enclosing negotiation offer execution/final destruction checks and desktop UI
remain open. Full civilization/AI scheduling, generated games, graphical acceptance
and the recorded original-game evidence/formula gaps also remain. These are
specification-derived native checks, not original-game observations. Browser,
cross-platform and network execution remain deferred. M4 is ongoing.

## F04 retained government and diplomacy composition

`CivilizationPoliticsOperation` joins the actual V02 government prefix to the
retained V04/A08 diplomacy turn. Government selection and rate children finish
before diplomatic random draws and relation maintenance. Imported Reserved40
metadata is cleared through its format adapter. AI calendar transitions and
recursive human Library decisions use actual gameplay children. Preference-output
failures retain committed government/rate effects and are reported after diplomacy
finishes. The authored desktop end-turn path now uses this shared operation.

`GovernmentSelectionOperation` captures restricted or unrestricted offered
identities for an explicit civilization and private recipient. It preserves the
human entry gate, accepts an offered identity without a second availability test,
and completes notices/tutorial state after actual rate editing. Explicit rate
civilization and recipient arguments must be supplied together; they preserve the
admitted command actor. Cached terminal results prevent duplicate rate persistence.
Existing player commands retain their own admission checks and use these children.

Six shared groups cover private government-to-rate handoff, imported flag clearing,
calendar-dependent maintenance and preference persistence, captured choices,
unrestricted selection, tutorial timing, actual AI contact/Library continuations,
cancellation, timeout, disposal, stale replies and deferred output failure.
Existing direct/console desktop government and end-turn regressions also pass.

Validation: root and App formatting and verification passed. Both Release builds
completed with zero warnings/errors. Native tests passed **1735/1737**, with the
two existing platform/audio skips. All **1702 shared groups** passed across
**188 coverage families**; identity verified **847 compiled inputs**. The
43-project graph, eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-politics-native.json` | `5939b9e53ba515ed49435400b8643a2ddee1495e942e73d4798450fbd5a370ab` |
| `m4-politics-identity.json` | `b803f415d46bcf4f7d7214815cdac75de6e2157e3bdebeeef1be6a2b008457ca` |

Full F04 city/civilization scheduling, generated desktop service composition,
complete games and graphical acceptance remain open. The recorded F04/A08 formula
and original-game evidence gaps remain unresolved. These checks are specification
derived; they add no original-game observations. V05 multiplayer offer controllers
are outside M4. Browser, cross-platform and network execution remain deferred.
M4 is ongoing.

## A01 retained assessment lifecycle children

`CivilizationAssessment.Begin` now retains the actual V01 elimination and U01
obsolete-unit retirement children. Its ordered census, captured human flag,
assignment scan and goal weights survive nested lifecycle decisions and bounded
civilization recreation. Later government/assignment work, port cleanup and goal
aging follow the completed child. The synchronous entry shares those same stages;
it retains its existing immediate-child domain.

`CivilizationAssessmentOperation` routes lifecycle prompts privately, publishes
bounded pending work and reports child presentation diagnostics after completion.
Disposal and required failures stop later stages while preserving completed
effects. The authored desktop end-turn path now uses this operation and waits
before financial notices. Its existing human profile and admission remain scoped;
this does not establish generated desktop AI play.

Four new groups join actual elimination, foreign mission interruption, nested
last-settler loss, human pretext decisions and recreation. They check stable
pending prompts, no synchronous lifecycle fallback, deferred retirement redraw,
continued processing after the active flag is cleared, bounded search, disposal,
required-provider failure, private/stale replies and terminal idempotence.
Existing census, production, government, assignment and desktop direct/console
turn regressions pass through the shared implementation.

Validation: root and App formatting and verification passed. Both Release builds
completed with zero warnings/errors. Native tests passed **1739/1741**, with the
two existing platform/audio skips. All **1706 shared groups** passed across
**188 coverage families**; identity verified **850 compiled inputs**. The
43-project graph, eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-assessment-native.json` | `3629bcb0302e9533d2cbf31af9c14556ca4a33ea6d6f14105c4095d8d812be15` |
| `m4-assessment-identity.json` | `0fce2b4b23c64a670b72e1496ed707ce74c9209a0b322658db2dd7aadd9d05a0` |

Full F04 scheduling, general production service composition, generated human/AI
games, full-turn snapshots and graphical acceptance remain open. The recorded
formula and independent original-game evidence gaps remain unresolved. These
specification-derived native cases add no original-game observations. Browser,
cross-platform and network execution remain deferred. M4 is ongoing.

## F04 post-city upkeep composition

`CivilizationPostCityOperation` joins actual government/diplomacy, retained A01
assessment and population/funds handling for an explicitly captured civilization.
Each child finishes before the next begins; accepted replies are consumed only by
their pending child. A preference-output failure is retained while later gameplay
continues, then reported at completion. Required exceptions and disposal stop later
stages without undoing completed effects. The authored desktop end-turn path now
uses this shared sequence after its city pass.

`CivilizationFinanceOperation` accepts an explicit civilization and recipient
resolver together for trusted upkeep. It routes population, funds and actual rate
decisions privately without changing the command actor. AI milestones update
silently without resolving a participant. Funds gates remain live after population
acknowledgment; inactive turns and changed human/autoplay state are handled by those
specific gates, not a blanket player-phase rejection. The legacy constructor keeps
its existing admission. Terminal results and disposal own the rate child's lifetime.

Six new groups cover the complete human government/rates/diplomacy/census/finance
sequence, actual human Library choices during AI diplomacy, actual nested
elimination during assessment, private and stale replies, explicit-target rate
persistence, post-population gates, cancellation/timeout, disposal at each stage,
required resolver failure and deferred preference failure. Existing desktop
direct/console government, assessment, population and turn regressions pass.

Validation: root and App formatting and verification passed. Both Release builds
completed with zero warnings/errors. Native tests passed **1745/1747**, with the
two existing platform/audio skips. All **1712 shared groups** passed across
**189 coverage families**; identity verified **852 compiled inputs**. The
43-project graph, eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-post-city-native.json` | `3fe1e34a5d0fc2bd1082cfb73672a6c785e41b45a94385d7bcf2cfe2f5f6c007` |
| `m4-post-city-identity.json` | `2901797c3ada8e5cf39bd2e97f4f48af8259ef106ba772d02345487859cd2d16` |

This is civilization-upkeep steps 5–7. Full F04 scheduling, general city/production
composition, spaceship presentation/autosave, generated human/AI games and graphical
acceptance remain open. A08's incoming attitude delta and the other recorded
formula/evidence gaps remain unresolved; the lifecycle case excludes that missing
path. These specification-derived cases add no original-game observations. Browser,
cross-platform and network execution remain deferred. M4 is ongoing.

## F04 desktop upkeep autosave

The 2026-11-24 checkpoint adds F04 §9 autosave after post-city finance and before
unit activation. The caller clears civilization processing first; enabled calls
enter scenario-directory scope and return home even for another civilization.
The local helper selects the ordinary extension, builds the name from the first
two CP1252 bytes of the live ruler name, trims spaces/tabs and appends the supplied
autosave label. Residues 0–3 modulo eight delete the backup and rename current to
backup; every eligible invocation then calls the actual ordinary F06 writer.
Delete/rename/output status failures do not roll back upkeep or suppress that write.

`ClassicGameFiles` shares its existing encoding and output implementation between
settled manual saves and the trusted upkeep command lane. Manual save admission
and its world pin remain intact. The autosave entry requires completed
single-player upkeep before movement; an admitted command prevents replacement
while it writes. Preparation mutations and partial output retain F06 semantics.
The native storage adapter adds non-overwriting rename with existing case-alias
resolution. Desktop uses its configured storage directory; the authored English
profile supplies `Auto`. General localized/scenario-host composition is separate.

Headless play and replay use in-memory files through the same writer, preserving
save mutations and publication. The actual authored end-turn waits for government,
rate and financial decisions before autosave. Ordinary load/resume does not save
again. A test exposed an authored setup omission: the displayed ruler was Cedar,
but its persistent human name record was empty. Setup now initializes that record
and its custom-name reference, preserving the name across ordinary F06 reload.
No generic load-name or writer behavior was changed to accommodate the profile.

Six shared groups cover rotation across two complete cycles, byte-prefix naming,
live default/custom names, local-owner gates, directory scope, output and encoding
failures, admitted-command saving, decisions, disabled saves, reload and replay.
Two native tests cover real file rotation, case aliases, failed rename and an
actual turn whose current-file replacement fails without undoing upkeep.

Validation: root and App formatting and verification passed. Both Release builds
completed with zero warnings/errors. Native tests passed **1753/1755**, with the
two existing platform/audio skips. All **1718 shared groups** passed across
**190 coverage families**; identity verified **854 compiled inputs**. The
43-project graph, eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-autosave-native.json` | `0270bb799f3946c4f0caef44395522e34f7d1d5297b0f0c4a0139baeb20e8952` |
| `m4-autosave-identity.json` | `c45914536eca3ef381132c7a9addd51b1efbfa7aa7de9554fcd6fb20fe96cdb5` |

This closes the autosave boundary for the authored desktop caller, not all F04
upkeep or civilization scheduling. Spaceship presentation, general city/production
composition, generated human/AI games and graphical acceptance remain open. The
recorded formula/evidence gaps are unresolved. These specification-derived tests
add no original-game observations. Browser, cross-platform and network execution
remain deferred. M4 is ongoing; Windows migration is not required for this checkpoint.

## V07 spaceship reports and F04 upkeep tail

The 2026-11-24 checkpoint adds the desktop Spaceships advisor and V07 §7 text
status/launch flow. `SpaceshipReport` runs the actual support-inclusive calculation
and owns raw part counts, capacities, over-capacity annotation, population,
performance and arrival display. Arrival equality still displays in-flight here,
while the shared arrival predicate already returns true. Offered launch choices
are captured with the report. An accepted extra result builds confirmation from
current shared success; acceptance invokes the actual V07 launch without a second
eligibility check. Cancellation retains preview effects, and completion/failure
cannot repeat launch.

The selector lists Exists ships in civilization order without contact/alive/human
filters. It snapshots parts before its retained window request. An already-active
window result suppresses fallback; an unavailable graphical presentation runs the
actual text status using human membership read after the request. The authored
desktop host provides the semantic text path. Specialized ship artwork, launch
video and graphical region/timer behavior are not supplied by this checkpoint.

`SpaceshipUpkeep` captures six counts before treasury normalization and healing,
and compares the eligible sum with the live sum after finances. Loss of the ship
or human control does not erase an earlier captured sum; equal sums with changed
subtypes do not request a window. The desktop caller now sets civilization
processing at upkeep entry and waits for any ship presentation before clearing
that flag, autosaving and activating movement. General advisor-picture closing,
construction choices and full civilization scheduling remain separate work.

Seven shared groups cover report arithmetic/gates, custom capacities, strict
arrival display, live confirmation success, accepted launch after eligibility
changes, cancellation/disposal, required callback failure, window results and
selector ordering. Actual structural construction, a private human launch during
another participant's command, and the ordinary autosave writer run sequentially
in a session case. Direct and console advisor commands save and replay the launch.

The extended native input driver passed **27 desktop checks**. It opens the new
menu, shows actual statistics, declines and then confirms launch exactly once,
compares the complete save bytes with a direct-client reference, reloads and
verifies launched status. Existing Hall, retirement and continuation checks also
passed. The independent ship scenario starts in a fresh process after the Hall
sequence so both unit-token and retained Hall-record histories match their inputs;
no file fields or comparisons were excluded.

Earlier native attempts and byte diagnostics remain under
`build/test-results/m4-spaceship-report-play-attempt*.log` and sibling save/Hall
images. Two attempts could not find the native picker while host portals
activated. The passing environment used isolated Xvfb/XDG directories and a
private session bus without host portal autoactivation, exercising the native GTK
fallback. One earlier process exited with signal 11 during a government-save
import; its cause is not established. Later complete execution passed that path.
The save/Hall mismatches during driver development were resolved by correcting
scenario process ordering, not by changing gameplay or weakening expectations.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1760/1762**, with the two existing
platform/audio skips. All **1725 shared groups** passed across **191 coverage
families**; identity verified **860 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-spaceship-report-native.json` | `93c8461c594b362828e6135013c7a403b4a7e1c42b7c296f7b25af38e44b6f0f` |
| `m4-spaceship-report-identity.json` | `a340b7e61148d247f7a2d9792143fb7942f7c99d0f8aaf5e211ab08b93137ceb` |
| `m4-spaceship-report-play.json` | `bd731e8315f5b640559d8e21e287842e61152a8ae4a07bfbf4ae89e98fa2ff72` |

This is authored desktop acceptance plus specification-derived shared cases, not
a complete generated human/AI game or independent original-game evidence. Full
city/production/civilization orchestration and the recorded formula/evidence gaps
remain open. Browser, cross-platform and network execution remain deferred. M4
is ongoing.

## Retained city support retirement (desktop continuation)

C05's retirement scan now shares one sequence between its synchronous entry and
`CityRetirement.Begin`. The retained operation keeps the captured owner/government,
selected unit, shortage branch and scan restart across actual U01/V01 lifecycle
children. Shortage uses cargo-aware destroy/redraw; affordable disorder retirement
uses bare deletion. Removal presentation, disorder clearing where applicable and
support-counter restart occur only after that child finishes. A pending decision
or bounded recreation slice cannot advance the city into its next calculation.

`CityRetirementOperation` routes actual lifecycle choices to their private
recipient, retains a pending intelligence window, reports deferred child
presentation failures and prevents completed work from replaying. Desktop upkeep
now waits for this operation after C06 production and before the second city
calculation, mood and research. Its existing authored-domain limits remain;
foreign espionage and AI recreation are exercised through the shared service and
session fixtures, not claimed as admitted Alder Valley gameplay.

Four added shared groups compose a foreign homed last settler's destruction with
actual recursive civilization elimination, espionage pretext, recreation and C05
restart. They distinguish committed loss from delayed redraw/removal, capture
owner/government while rereading human membership, preserve the accepted prefix
through disposal and notice failure, and check private session reply admission,
cancel/timeout and completion replay. Affordable retirement also proves that the
bare-deletion notifications run without the destruction redraw wrapper. Existing
foreign/free/cargo/tie/protection and desktop shortage/save/reload cases continue
to execute the shared scan.

Root/App formatting and verification passed. Both Release builds completed with
zero warnings/errors. Native tests passed **1764/1766**, with the two existing
platform/audio skips. All **1729 shared groups** passed across **191 coverage
families**; identity verified **863 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

The unchanged native input driver passed **27 desktop checks**, including ordinary
turns, government/rate choices, complete save-byte comparisons, reload, outcomes,
Hall records and spaceship launch/reload. An earlier run reached the final ship
import and timed out with the native file picker still open. Its log and observed
application state remain in `m4-retained-city-play-attempt1.log` and
`m4-retained-city-play-attempt1.failure.json`; no gameplay comparison had failed.
A fresh isolated Xvfb/private-bus run passed without source or driver changes.
The cause of the earlier picker timeout is not established.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-retained-city-native.json` | `1f6c45c6266e87b1c227d39f65d163cfa0e17b876498c0ef5a908555ef9f2290` |
| `m4-retained-city-identity.json` | `81f0d174b04e10fb11ad763886deeba63cc1687424f7b6fa58d07da947e780b2` |
| `m4-retained-city-play.json` | `4094a887b7f6d6500412d930ea8928c1c79c8af2bca8a1d07baa3b6d22f392f7` |

These are specification-derived shared tests and authored desktop regressions.
They do not establish independent original-game compatibility or complete the
human/AI city/civilization scheduler. Retained famine/removal and maintenance
parents, F04's missing government-score and best-shield formulas, generated-game
acceptance and the other recorded M4 gates remain open. Browser, cross-platform
and network execution remain deferred; the M4 goal continues on this host.

## Retained food and famine before city production

`CityFood.Begin` now shares the existing C05 growth/famine sequence with the
synchronous entry and retains actual lifecycle children. A selected settler uses
U01 cargo deletion without the destroy/redraw wrapper. Food reset and the final
city redraw wait until nested V01 checks and private V06/research decisions finish.
Population famine instead waits for C01 removal, including supported cargo loss
and city cleanup, then checks the captured owner before returning the removed-city
sentinel. This branch keeps the negative incoming food stock and skips the final
famine redraw. Bounded recreation is pending work, not completed removal.

The session's `CityFoodOperation` routes private decisions and reports deferred
child presentation failures. Desktop upkeep captures incoming food, size and city
identity before entering it. Only a surviving city proceeds to recalculation,
accumulation and C06 production; a removed city bypasses those stages. The same
required incident services now serve food and support retirement. Existing
Alder Valley admission limits remain: its center yields and available content do
not admit last-city famine, foreign espionage or AI recreation. Shared lifecycle
cases cover those branches without claiming that they are reachable in this
restricted desktop profile.

Five new groups use actual U01/C01/V01/V06 children. They cover a foreign homed
settler's loss, a supported ship whose unhomed last-settler cargo interrupts city
removal, captured-owner elimination after cleanup, private replies, cancel/timeout,
disposal, required-provider failure, bounded recreation and completion replay.
The removal case includes two human creation gates and one actual AI recreation;
controlled choices are consumed only by the latter. Existing growth, route,
visibility, scalar-wrap, accumulation and direct/console desktop cases still run
the shared food sequence.

Root/App formatting and verification passed; both Release builds completed with
zero warnings/errors. Native tests passed **1769/1771**, with the two existing
platform/audio skips. All **1734 shared groups** passed across **191 coverage
families**; identity verified **866 compiled inputs**. The 43-project graph,
eight identity tests and three coverage tests passed.

The native input driver passed **27 desktop checks**, including complete save-byte
comparisons and reload/continuation paths. An earlier run timed out during the
scheduled-outcome scenario's import, with the application still waiting for the
native picker. Its log and application observation remain in
`m4-retained-food-play-attempt1.log` and
`m4-retained-food-play-attempt1.failure.json`. The cause remains unestablished.
The driver now captures an isolated-display screenshot on an ordinary wait timeout
so a future failure can expose the native picker as well as the application state.
Python compilation passed. This diagnostics-only change alters neither input
sequencing nor acceptance assertions. A fresh isolated Xvfb/private-bus run passed.

| Receipt under `build/test-results/` | SHA-256 |
| --- | --- |
| `m4-retained-food-native.json` | `cdb2bb4ed8b92af334a7bc1dfd0034ce6553575d8fbdba1b5f767572e1e757b7` |
| `m4-retained-food-identity.json` | `fd02b5e1913d04eb9392dff4565243d5ce61ba88974f31e5e0a4cea9fb107170` |
| `m4-retained-food-play.json` | `78ebed7c77861663c3c76a122c7c072a376a14d4d111294881505b16ffa2bd6a` |

This closes the retained food/famine child and its authored desktop caller,
not the full F04 city/civilization scheduler or complete human/AI games.
Maintenance and other general parent integration, the undefined government-score
and best-shield formulas, generated-game acceptance and independent original-game
evidence remain open. Browser, cross-platform and network execution are deferred;
M4 remains active.
