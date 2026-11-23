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
| Unit, world and civilization interactions | U01 creation and shared cargo/loss primitives are implemented. U01 creation, U02 allowance/HP/exhaustion and ordinary charge/AI gates, U04 eligibility/next selection and W03 knowledge primitives are integrated with the local game. W03 local surroundings uses real A01 goals and U04 contact orders. Local V01 last-city checks, V06 escape/capture and V07 reset/loss are joined and tested. W04 work execution and desktop queued jobs are now implemented. V04 local war/alliance transitions, cargo-aware expulsion and mutating border measurement now compose real children. V06 local incidents now compose actual war, stance and government children; failed escape can suspend for the human victim after actual unit loss. V01 catch-up now invokes full V03 grants with actual Library, Leonardo and government children; elimination/restart joins are tested. V01 elimination now has a resumable operation with actual nested last-settler and foreign-spy incidents, bounded restart search and victim routing. V06 unit bribery and sabotage now compose real transfer, HP and resumable escape/lifecycle effects. City sabotage and poison now use actual building, city-memory and resumable capture/escape children, with actor/victim decision routing. Technology theft now joins actual race selection, physical defender detection, captured-technology selection and research grants; fallback requests now run through the local city controller. Embassy/investigation now retain their distinct human/AI unit effects through actual nested deletion/destruction, with required intelligence/city presentation adapters. U07 now provides local SDI, actual nested stack loss, independent blast damage and W05 pollution placement. The V06 planted-device body calls that real action after capture checks and applies live identification consequences. V01 civil war now joins actual creation, C01 city transfer, knowledge and unit reassignment, with the NoSchism action boundary explicit. C09 population and supported-unit loss now compose actual resumable C01 deletion and V01 checks. The ordered C09 capture/incitement parent now composes actual war/civil-war, Palace/wonder consequences, loss/technology, production and elimination/partisan children with required CityTaken and P06 boundaries. V06 revolt now joins retained pricing/payment, actual classic option text, escape/incident/government changes and the complete incited transfer. The local city controller now composes these actual mission bodies, retained-context fallbacks and session decisions. U05 local landfall, boarding, unload, wakeups and aircraft/coast continuations now use actual cargo, selection and resumable loss services. Player airlift now composes destination and fighter choices, retained risk, actual single-unit relocation and resumable interception loss. Local paradrop now composes senate/attack permission, cargo lift/stack placement, capture and revelation, with session decisions and documented presentation repairs. U06 strength, defender selection and common combat preparation/preview now use actual stacks, movement, building effects and AI goals. Damage rounds now preserve primary sampling, bounded yields and hit effects; promotion joins actual C08 wonder checks. Casualty accounting now joins world-owned history, the required S02 dispatch boundary and actual nested U01/V01/V06 deletion with session decisions. Full diplomacy/movement/desktop mission callers, map sharing and remaining acquisition callers, complete movement/orders, combat, movement/mission capture dispatch, forced elimination, remaining espionage, transport/air/nuclear delivery, remaining pollution/warming, huts and barbarians remain pending. |
| Classic AI | A08 advance valuation is integrated with V03 recommendation. C06 AI spending now consumes real V07 race queries and retained F04 human wonder extrema. A02 city production now has its ordered survey/scoring policy, actual A03 recommendation and U05 AI airlift children, and the desktop automatic-production join. A01 census/strategy, power and goal assignment now compose actual children; desktop turns publish power and run human assessment. A08 government, diplomatic stance and alliance-willingness policies are implemented, with actual restricted government changes and V05 patience. Outer AI movement, the remaining A03–A08 policies and full turn scheduling remain pending. |
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
