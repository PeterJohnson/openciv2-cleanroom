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
| City, civilization and turn economy | C01 local removal/creation/radius/spatial queries, C05 food/growth/retirement, C07 directional route mutations and the full C03 recalculation are implemented. C01 creation, C03 and the C05 surviving-city food/retirement phases run in Alder Valley founding/turn/load; shared last-city famine now joins actual V01 elimination to the pending defeat reason; complete end-sequence handling remains open. The parent joins actual C02 workers, C05 support, C04 moods, C07 commodity refresh, U03 links and W05 intensity. C04 disorder/tax/celebration, C06 unit/nonunit production, V03 grants and resumable research completion, C08 maintenance and V04 global reputation now run in Alder Valley using real local children. C06 joins actual wonder, Darwin and spaceship effects. Remaining C01–C08 operations, V02 choices/rates, general research presentation/capture/catch-up and full F04 integration are pending. |
| Unit, world and civilization interactions | U01 creation and shared cargo/loss primitives are implemented. U01 creation, U02 allowance/HP/exhaustion, U04 eligibility/next selection and W03 knowledge primitives are integrated with the local game. W03 local surroundings uses real A01 goals and U04 contact orders. Local V01 last-city checks, V06 escape/capture and V07 reset/loss are joined and tested. Full diplomacy/incident children, restart catch-up grants, map sharing and remaining acquisition callers, complete movement/orders, combat, capture, forced elimination/civil war, remaining espionage, transport/air/nuclear, pollution/warming, huts and barbarians remain pending. |
| Classic AI | A08 advance valuation is integrated with V03 recommendation. C06 AI spending now consumes real V07 race queries and retained F04 human wonder extrema. A02 city production now has its ordered survey/scoring policy, actual A03 recommendation and U05 AI airlift children, and the desktop automatic-production join. A01 orchestration, the remaining A03–A08 policies and full turn scheduling remain pending. |
| Outcomes | V07 configurable capacities, performance/group choice, assembly/local launch, race/arrival predicates, reset/loss and V01 destruction history are implemented. C06 now integrates local spaceship construction; spaceship reports/arrival outcomes, scoring, victory/retirement, remaining history and Hall of Fame remain pending. |
| Automation | The existing optional Lua adapter remains. The M4 production policy, game caller and replay acceptance remain pending; browser execution is deferred. |
| Desktop acceptance | Complete human/AI games, full-turn snapshots and G01/G04–G12 cases with custom rules/failure variants remain pending. Implemented services and authored-profile checks do not satisfy this gate. |

The next mechanics integrations are the V06 diplomacy-incident and V03 restart
catch-up policies, A01 assessment/orchestration, remaining V02 government/rate choices
and full F04 turn processing needed by the generated desktop game. C04's shared
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
