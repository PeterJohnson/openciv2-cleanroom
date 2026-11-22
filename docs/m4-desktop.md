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
| City, civilization and turn economy | C01 local removal/creation/radius/spatial queries, C05 food/growth/retirement, C07 directional route mutations and the full C03 recalculation are implemented. C01 creation, C03 and the C05 surviving-city food/retirement phases run in Alder Valley founding/turn/load; shared last-city famine now joins actual V01 elimination to the pending defeat reason; complete end-sequence handling remains open. The parent joins actual C02 workers, C05 support, C04 moods, C07 commodity refresh, U03 links and W05 intensity. City-turn consequences, remaining C01–C08, V02/V03 and F04 integration are pending. |
| Unit, world and civilization interactions | U01 creation and shared cargo/loss primitives are implemented. U01 creation, U02 allowance/HP/exhaustion, U04 eligibility/next selection and W03 knowledge primitives are integrated with the local game. W03 local surroundings uses real A01 goals and U04 contact orders. Local V01 last-city checks, V06 escape/capture and V07 reset/loss are joined and tested. Full diplomacy/incident children, restart catch-up grants, map sharing/Apollo, complete movement/orders, combat, capture, forced elimination/civil war, remaining espionage, transport/air/nuclear, pollution/warming, huts and barbarians remain pending. |
| Classic AI | A08 advance valuation is integrated with V03 recommendation. A01 orchestration and the remaining A02–A08 policies and turn scheduling remain pending. |
| Outcomes | V07 reset/loss and V01 destruction history are implemented. Spaceship construction/launch/arrival, scoring, victory/retirement, remaining history and Hall of Fame remain pending. |
| Automation | The existing optional Lua adapter remains. The M4 production policy, game caller and replay acceptance remain pending; browser execution is deferred. |
| Desktop acceptance | Complete human/AI games, full-turn snapshots and G01/G04–G12 cases with custom rules/failure variants remain pending. The new leaf tests do not satisfy this gate. |

The next mechanics integrations are the V06 diplomacy-incident and V03 restart
catch-up policies, C06 production selection, C04 disorder/tax/celebration and full
F04 turn processing needed by the generated desktop game.
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
Map exchange, Apollo and complete V04/V05 audiences remain unfinished.

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
cities. All nonzero-turn grant effects remain future work. `GameState.Paths`
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
