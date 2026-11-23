# Turn sequencing

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in F04 |
| --- | --- |
| Gameplay | Calendar, turn/civilization/city/unit phase dependencies, resume points, events, maintenance, autosave timing and end checks. |
| Original files/content | F06 supplies autosave/resume encodings; calendar tables define gameplay values, not runtime table layout. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Loop/function decomposition, menu/paint calls, UI caches and redundant timer reads between semantic phases. |

The algorithms below specify game values/decisions and their real dependencies,
plus explicitly identified external data definitions. They do not prescribe
function boundaries, object/global layout, allocation strategy or incidental
helper/store order. Preserve retained data where an admitted game/file/peer
consumer uses it; represent that data flow explicitly rather than emulating
original memory. Native or synthetic callbacks do not create new game boundaries.
Historical backing-byte/fault/trace cases are reference unless an actual external
format or supported semantic operation consumes the value. Missing gameplay or
format meaning is an owner gap, not permission to invent it or require arbitrary
original bytes. This scope controls stronger historical wording below.

**Required profile:** [X01 fresh-game isolation and bug repairs](X01-classic-compatibility-contract.md#fresh-game-isolation-and-supported-bug-repairs)
and [behaviorally significant ordering](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
override conflicting procedures and examples below. Retention applies within a
game or to explicit imported inputs, not across new games. Confirmed original-bug
stack/backing/stop cases are optional diagnostics; use documented safe repairs.
Only real effects and callbacks impose ordering; test adapters cannot invent them.
Unspecified safe continuations remain explicit gaps, not required arbitrary bytes.

**ID:** F04.
**Target:** Civilization II Multiplayer Gold Edition **5.4.0f, patch 3**, under
the [X01 contract](X01-classic-compatibility-contract.md).

This specification defines the single-player session loop, global-turn
advancement, civilization and city phase order, calendar calculation, resume
gates, autosave timing and end checks. It specifies when subordinate mechanics
run and which state they observe. Their production, research, combat, AI,
environment and presentation formulas belong to their owning documents.
Section 12 records those dependencies and the remaining integration review.

Original encodings and validation provenance are in the appendices. An
implementation can use different records and control structures while preserving
the specified behavior and ordering.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
semantic publications and failure effects. Only dependencies within the binding
scope above constrain evaluation; storage/API choices need no original-trace
equivalence proof. External file/wire definitions remain binding at adapters.

Phase order, city/unit scan consequences, event hooks, autosave/resume boundaries
and callback-visible mutations remain required. X01 permits equivalent work
within those boundaries, not a replacement turn schedule.

## Compatibility policy

X01's [presentation policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy)
allows different turn notices, dialogs, media and event-loop organization.
Preserve phase order, pending decisions, accepted/cancelled effects, autosaves,
resume state and all simulation work surrounding a presentation call. Cosmetic
waits need not last as long as the original; calls that recalculate game state
or admit a required operation retain their semantic ordering. The city/unit/AI
algorithms and end-condition decisions are unchanged.

## 1. Boundary and inputs

The main entry is an initialized or restored **single-player** world, after
setup/loading and session initialization. Its inputs include:

- Live civilization, city, unit and map state; loaded rules; difficulty;
  topology classification; scenario state and events.
- Turn/year, first spaceship-launch turn, history-report deadline, peace
  counter, victory/scoring state and any pending game-over reason.
- The alive and human civilization sets, local input/view civilization,
  current turn civilization and resume state.
- Movement mode, options, selected-unit/window state, casualty counts,
  tutorial markers, retained calculation state and presentation services.
- Controlled random choices or versioned implementation contexts, ordered user commands, message-pump callbacks and
  file-service outcomes. A save operation can itself mutate live state.

Identity and retained state follow [F02](F02-game-state-and-invariants.md).
Options come from [F03](F03-rules-and-configuration.md). Arithmetic follows
[F01](F01-determinism-and-arithmetic.md#1-numeric-model-and-notation): `s8`,
`s16` and `s32` below mean signed modular reduction to their respective domains,
and `u8` means reduction modulo 256. Turn, stored year, history deadline and
peace counter use the signed cyclic domain −32768…32767. Do not replace those
values with monotonically increasing counters in compatibility execution.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

The nominal session has valid slots/references, supported rule values,
readable assets, and an active loop. The resume civilization is either
**NoResume** or a living normal civilization 1…7. A normal game starts with
turn 0; setup may instead supply an accelerated or scenario starting turn.
Nominal state does not imply default rules, an empty event list or a new process.
Section 11 separates unusual inputs, defined arithmetic boundaries and
evidence-limited failures. Stricter acceptance policies follow X01.

The loop returns when the session is stopped or an end-game decision requests
exit. It is not a transaction and does not return a reconstructed initial
world on cancellation. A child operation's mutations and random draws remain
visible to later operations, even when that child destroys a city or changes
which civilizations are human.

Multiplayer authority, simultaneous turns and hotseat handoffs are not specified
by the single-player pseudocode. Section 11 identifies their dispatch boundaries.
Do not run this loop independently on every network participant.

## 2. Phase state and resume semantics

Use distinct logical values:

| State | Meaning |
| --- | --- |
| `sessionActive` | Whether the turn loop is still running. A callback can clear it. |
| `playerPhaseActive` | Whether the current human movement phase continues. |
| `turnCiv` | The civilization being processed now, including barbarian slot 0. |
| `localCiv` | The input/view civilization. AI processing does not ordinarily replace it. |
| `resumeCiv` | NoResume, or the first civilization slot eligible on resumed entry. |
| `processResumedCiv` | Reprocess the initial eligible civilization and turn events, as for a scenario start, despite having a resume slot. |
| Saved resume fields | Separate serialized continuation values. They are cleared with the runtime values after a civilization's movement phase. |
| `civProcessing` | True during civilization upkeep and its notices; false before the autosave call. |
| End-of-turn indicator | Presentation/movement state, cleared at global advancement and after each civilization. It is not the turn counter. |
| City-window-opened indicator | Affects whether human input may end automatically. It is not just the current window's visibility. |

NoResume is encoded as slot 0 in the original. Consequently it also means
“perform a global advance”; it cannot represent a distinct resume point inside
the barbarian phase.

| Entry state | Global advance | Four turn-event passes | First eligible civilization's upkeep |
| --- | --- | --- | --- |
| NoResume | Yes | If events are loaded | Yes |
| Resume 1…7, `processResumedCiv=false` | No | No | Skip |
| Resume 1…7, `processResumedCiv=true` | No | If events are loaded | Yes |

Both resumed paths still execute the first eligible civilization's movement
phase and human turn notices where applicable. After that phase, clear runtime
and saved resume fields. Later civilizations in the same pass receive upkeep
normally. This avoids duplicating upkeep on an ordinary mid-turn load.

The resume comparison is reevaluated for each civilization against the current
resume field. It is not a permanently cached starting index. Ordinary resumed
input names a living slot; if it does not, the first later living slot inherits
the skip/reprocess decision. See section 11.

## 3. Single-player session loop

At entry, close advisor and casualty picture windows and update menu states.
Then run this procedure. `alive(c)` and `human(c)` query current sets at the
point shown; do not snapshot them for the entire pass.

```text
repeat:
    humanMoved = false
    updateHumanWonderRanges()

    if resumeCiv is NoResume:
        advanceGlobalTurn(AllUnits)

    retired = checkRetirement()
    if retired or gameOverReason exists:
        if handleGameOver() requests exit:
            leave loop

    if eventsLoaded and (resumeCiv is NoResume or processResumedCiv):
        checkTurnEvents(current turn)
        checkIntervalEvents(current turn)
        checkRandomTurnEvents(current turn)
        checkReceivedTechnologyEvents()

    for c = 0 through 7, while sessionActive:
        if c is before resumeCiv, or not alive(c):
            continue
        turnCiv = c

        if human(c):
            prepareHumanView(c)
        drawCivilizationIndicator(c)

        if resumeCiv is NoResume or processResumedCiv:
            if resumeCiv is not NoResume:
                update map title; redraw local views; flush paint
            reportUnreportedLocalCasualties()
            processCivilization(c)

        if not human(c):
            executeAIUnitPhase(turnCiv)
        else:
            showHumanTurnNotices()
            runHumanMovementPhase()
            humanMoved = true

        clear saved and runtime resume fields
        clear end-of-turn indicator
        if not sessionActive:
            finish loop and return
        if gameOverReason exists and handleGameOver() requests exit:
            finish loop and return

    if not humanMoved:
        turnCiv = localCiv
        runHumanMovementPhase()
        if gameOverReason exists and handleGameOver() requests exit:
            leave loop

    humanSet = humanSet intersect aliveSet
    if not sessionActive:
        leave loop

finish loop and return
```

Finishing calls the turn-timer stop service with its non-finalizing argument;
dialog destruction follows. Session teardown, returning to the main menu and
freeing the map are separate caller operations.

Important ordering consequences:

- Barbarian slot 0 goes first if alive. Normal civilizations follow 1…7;
  neither human control nor power rank reorders them.
- Read human control again **after upkeep**. If upkeep eliminates a human
  civilization and clears its human bit, its remaining branch becomes AI
  movement. There is no intervening alive-set recheck there.
- The four event calls form one selected group. Once selected, all four are
  called in order; do not retest `eventsLoaded` or `sessionActive` between them.
  Each call receives the then-current turn, so earlier event actions can affect
  later event inputs. [S02](S02-event-language-and-execution.md#7-trigger-matching)
  owns matching/actions, sampled conditions and wildcard binding. Original
  samples used only to test an already consumed event may be omitted when no
  other specified effect depends on that test.
- Events run before the civilization scan, so they can create, destroy or
  change a civilization before its eligibility test. A later civilization
  observes earlier civilizations' changes during the same global turn.
- The fallback human phase is selected by `humanMoved`, without an additional
  alive or active check at that point. It supplies interaction even when no
  admitted civilization took the human branch. Its movement routine handles
  its own exit conditions.
- Clearing `sessionActive` is not an exception unwinding every intermediate
  call. Stop at the explicit gates above and within the called operation.

`updateHumanWonderRanges` runs before global advancement: for each of four
wonder groups, start maximum at 0 and minimum at 99, scan human slots 1…7,
and take their current counts' extrema. There is no alive-set filter. With
no humans the initial extrema remain. These are retained summaries, not a
request to recount every city's wonders.

`prepareHumanView(c)` sets the player civilization to `c`, captures the old
local civilization and changes `localCiv` to `c`. If that changed, or a resume
slot exists, first restore remaining seconds from any nonzero turn-time limit
(milliseconds divided by 1000, truncated toward zero) and invalidate the
displayed timer cache; then update the map title and redraw local views.
Draw status information and the
civilization color; the caller then draws the color again. Password, concealment
and handoff branches belong to the multiplayer variants.

Casualty reporting uses the **local** civilization, even before an AI's upkeep.
Compute total casualties minus already reported casualties. Unless autoplay
is active, a positive difference advances the reported count before showing
the singular/plural notice, flushing pending input and playing the notice
sound. The dialog's extra choice can open the casualty timeline. Thus later
civilizations in the same pass can expose newly accumulated local casualties.

## 4. Global advancement

`advanceGlobalTurn(AllUnits)` performs these steps in order:

1. If the **incoming turn is nonzero**, call [barbarian spawning](W06-huts-and-barbarians.md#periodic-barbarian-spawning),
   [global-warming processing](W05-pollution-and-global-warming.md#global-warming-pressure),
   [A01 power-rank publication](A01-ai-strategy-and-assessment.md#3-power-ranking-and-coordination), and the [P09 throne-room
   progress check](P09-council-and-throne-room.md#7-throne-room-progress-and-admission), in that order. These children observe the old turn/year.
   They are skipped on the initial advance from turn 0.
2. Set `turn = s16(turn + 1)`. Compute the year from that new signed turn using
   section 5, and store `year = s16(result)`.
3. If the new turn is greater than 199, set `peaceCounter = s16(peaceCounter+1)`.
   Combat can reset this counter elsewhere; this step does not test whether
   the whole world is currently peaceful.
4. Clear the end-of-turn indicator, then invoke the unit reset with AllUnits.
   Reset occurs before reputation maintenance, history and spaceship arrivals.
5. Perform the reputation maintenance below.
6. If `nextHistoryTurn < turn`, draw once from the primary sampling law and set
   `nextHistoryTurn = s16(turn + draw % 40 + 20)`. Only afterward, if autoplay
   is inactive, show the historian report. Equality does **not** trigger it.
   Autoplay suppresses the report, not the deadline update or its draw.
7. Scan normal civilizations 1…7 for a newly arrived spaceship and perform
   the arrival transition below. There is no alive-set filter.

The initial turn skips the four old-turn children, but still performs all
later steps. For example, a history deadline of zero triggers on turn 1.
Environmental processing consumes pollution accumulated before this advance;
pollution created during the upcoming city phases is not processed early.

### Reputation maintenance

Invoke [V04's global reputation maintenance](V04-diplomacy-and-treaties.md#global-reputation-maintenance)
at this phase, after unit reset and before history/arrival work. V04 owns the
captured period, inactive-slot scan, ordered Eiffel queries, reputation endpoint
and signed-credit normalization. This is distinct from the per-civilization
political maintenance later in the turn.

### Spaceship arrival boundary

Select the first slot whose ship is launched, whose arrival predicate is true,
and whose arrival has not already been processed. Set its arrived state. If
the game has not already been won, also mark that ship as winner, record the
winner civilization, and set the pending reason to local spaceship victory
or another civilization's spaceship victory according to `localCiv`.

If autoplay is inactive and either the arriving civilization is not local or
no game-over reason is pending, show the landing notice. Finally mark the game
won and return from advancement immediately. Later simultaneously arriving
ships wait for a subsequent invocation. Once the game is already won, a new
arrival still gets its arrived state and possible notice, but does not select
a new winner or overwrite the reason. Ship eligibility/arrival-time formulas
belong to [V07](V07-spaceship.md#8-race-arrival-reset-and-loss); game-over
presentation is dispatched after advancement. The arrival predicate uses launch
state and calendar year; it adds no success-percentage random draw.

## 5. Turn-to-calendar conversion

This operation accepts a signed turn argument and reads difficulty, small/large
map classification, first-launch turn, and scenario calendar parameters. It
does not itself mutate the turn or year or draw randomness. Negative returned
years use the game's calendar convention; display formatting is separate.

Choose calendar profile:

```text
level = difficulty
if largeMap: level -= 1
if smallMap: level += 1
profile = clamp(s32(level - 1), 0, 3)
```

Both adjustments occur if both flags are present. Difficulty is 0 chieftain,
1 warlord, 2 prince, 3 king, 4 emperor, 5 deity. Thus an ordinary-sized map
uses profiles 0, 0, 1, 2, 3, 3 respectively. The classification flags, rather
than recomputed map area, determine these adjustments.

If `firstLaunchTurn >= 0`, compute
`extra = max(s32(inputTurn - firstLaunchTurn), 0)` and replace the working
turn with `s32(inputTurn - extra)`. Otherwise `extra=0`. Add `extra` only after
the base calendar calculation. This changes progression after the first
spaceship launch to one calendar unit per turn.

For an active scenario with **both** nonzero calendar increment and nonzero
start year, use:

```text
baseYear = s32(startYear + s32(workingTurn * abs(increment)) - 1)
return s32(baseYear + extra)
```

The increment's sign does not change this magnitude calculation. A negative
increment selects the scenario's monthly display convention elsewhere. Do
not apply the ordinary zero-year substitution to this scenario result. If
either parameter is zero, use the ordinary profile even in a scenario.

Each ordinary profile contains these ordered triples
`(startYear, turnCount, yearStep)`:

| Period | Profile 0 | Profile 1 | Profile 2 | Profile 3 |
| --- | --- | --- | --- | --- |
| 0 | (−4000, 250, 20) | (−4000, 60, 50) | (−4000, 60, 50) | (−4000, 60, 50) |
| 1 | (1000, 50, 10) | (−1000, 40, 25) | (−1000, 40, 25) | (−1000, 40, 25) |
| 2 | (1500, 50, 5) | (0, 150, 10) | (0, 50, 20) | (0, 75, 20) |
| 3 | (1750, 50, 2) | (1500, 50, 5) | (1000, 50, 10) | (1500, 25, 10) |
| 4 | (1850, 50, 1) | (1750, 50, 2) | (1500, 50, 5) | (1750, 50, 2) |
| 5 | (1900, 100, 1) | (1850, 50, 1) | (1750, 50, 2) | (1850, 50, 1) |

```text
t = workingTurn
if t != 0: t = s32(t - 1)
p = 0
while p < 6 and t > period[p].turnCount:
    t = s32(t - period[p].turnCount)
    p += 1
if p < 6:
    baseYear = s32(period[p].startYear + s32(period[p].yearStep * t))
else:
    baseYear = s32(period[5].startYear
                   + s32(period[5].yearStep * period[5].turnCount) + t)
if baseYear == 0: baseYear = 1
return s32(baseYear + extra)
```

The period test is strictly greater, not greater-or-equal. Turns 0 and 1 both
map to −4000 in the ordinary calendar. After the last period, the remaining
turns add one each regardless of the last period's step. Zero substitution
occurs before adding the post-launch supplement. Appendix A records an extra
reference-table read that does not change these returned values.

## 6. Civilization upkeep and city order

For a selected civilization `c`, upkeep does not test human/alive eligibility
again before entry. Execute:

1. Set `civProcessing=true`. Initialize a spaceship-part difference to zero.
   If `c` is currently human and has a ship, sum its six part quantities into
   that difference. Cache the ship's presentation part counts in all cases;
   close the advisor picture window.
2. Apply [V02 treasury normalization](V02-governments-and-tax-rates.md#treasury-and-settlement-boundary)
   and capture its resulting treasury as `startGold` for the later funds notice.
3. Run unit recovery/healing for `c` (U04/U05).
4. Run the city pass below, including its civilization-level AI rate and
   government-score postprocessing.
5. Run [V02's government prefix](V02-governments-and-tax-rates.md#6-government-portion-of-turn-maintenance),
   then [V04's remaining diplomacy maintenance](V04-diplomacy-and-treaties.md#11-civilization-diplomacy-maintenance)
   with A08 choices.
6. Run [A01 civilization assessment/census](A01-ai-strategy-and-assessment.md#4-civilization-assessment-ordered-passes). Invoke it for human
   civilizations too; it maintains shared summaries and is not just an AI move.
7. Run [V02 population/funds checking](V02-governments-and-tax-rates.md#combined-population-and-funds-check)
   with `startGold` (P07 presentation).
8. Recheck whether `c` is human and has a ship. If so, subtract its six current
   quantities from the captured difference. Independently of that gate, if
   the difference is nonzero, request its spaceship window; if that request
   returns zero, show the ship status dialog with the current human-control
   indicator. Losing human control or the ship does not erase the captured
   difference. Do not substitute a comparison of each part type: the original
   compares the sum difference.
9. Set `civProcessing=false`. If autosave is enabled, change to the scenario
   directory, call `autosave(c)`, then return to the home directory.

These calls are sequential and their intermediate state is observable.
In particular, healing precedes support losses, city research precedes the
diplomacy pass, and the census follows city production and diplomacy. There
is no general early exit between them merely because a child cleared the
session-active flag or eliminated `c`.

### City pass

Reset `c`'s best-shield-surplus summary and seven government scores. Clear its
28 wonder-notice entries and the shared attitude-dirty indicator. Initialize
two local summary predicates, `largeBalancedCity=false` and `disorder=false`.
Begin the city window's notice batch.

Capture `cityEnd` once, then scan **cityEnd−1 down through 0**. At each slot,
read the current record and process it only if live and currently owned by `c`.
Clear that city's airlift-used state before calling its city-turn operation.
[U05](U05-transport-and-special-movement.md#ai-city-production-airlift) owns
airlift execution and uses this per-city reset boundary.

If the operation returns the city-removed sentinel, skip all remaining work
for that slot. Otherwise:

1. If its returned happiness balance is zero and its current size exceeds 5,
   set `largeBalancedCity=true`.
2. If its current disorder flag is set, set `disorder=true`.
3. For civilizations 1…7, update their remembered size of this city to the
   current size when `c` has an alliance with that civilization or the global
   Apollo wonder location is not Absent. This tests the global location marker.
4. If this is the city currently displayed and no zoom was requested, refresh
   its window.

End the notice batch after the scan. Add twice the despotism score to the
anarchy score. For a currently nonhuman civilization, run
[A08's AI rate adjustment](A08-ai-research-government-and-diplomacy.md#rates-after-the-city-pass)
using the two accumulated predicates and current state. Then run
[A08's strategic government-score adjustments](A08-ai-research-government-and-diplomacy.md#strategic-adjustments-to-government-scores)
for all civilizations, and clear the 28 wonder-notice entries again. These run
**after** the completed city scan and before civilization diplomacy; C04/C05
own the city-level score contributions.

The scan captures its upper bound, not the records. A city created in a higher
slot during a callback is not appended to this pass. A newly occupied lower
slot can be processed when reached. Deletion, ownership change or slot reuse
affects the later live test; there is no snapshot of initial city identities.

### Inside one city turn

The following stage contract belongs to F04. The named calculations and
mutations inside each stage remain with the mechanics owners.

| Order | Stage and gates |
| --- | --- |
| 1 | Lock city-window refresh; capture owner. Apply the capture-age founder update. Enable city notices only for the local human owner outside autoplay. Clear the zoom request, improvement-sold, AI-settler-near and investigated states. Capture whether this city is displayed, clear the window-dirty indicator and capture its incoming food box. |
| 2 | Resolve food/growth/starvation from incoming state ([C05](C05-city-growth-and-support.md#food-turn-starvation-or-ordinary-growth)). If the child returns nonzero, return the city-removed sentinel immediately; bypass the remaining stages and their trailing cleanup. |
| 3 | Recalculate the city fully (C02–C05), refresh a displayed dirty window, and issue any worker-overflow notice. Compute and add this turn's food surplus; test the low-food warning using the captured incoming box ([C05](C05-city-growth-and-support.md#this-turns-food-accumulation-and-warning)). |
| 4 | Process [C06 production](C06-city-production-and-purchasing.md#production-during-a-city-turn), then run [C05 support-shortage/retirement handling](C05-city-growth-and-support.md#unsupported-unit-and-disorder-retirement-pass). Newly produced units therefore exist before this support-disband pass. |
| 5 | Enable worker reallocation and city-processing mode, clear the current terrain-job result/priority, recalculate fully again, then disable worker reallocation. Accumulate support and unhappiness effects into government scores (C04/C05/A08/V02). |
| 6 | Process [disorder, tax collection and celebration](C04-city-happiness-and-disorder.md#city-turn-disorder-tax-and-celebration-operation), then add research (V03). If the captured owner's civilization is currently human, process [pollution and meltdown](W05-pollution-and-global-warming.md#ordinary-city-pollution), then [improvement upkeep](C08-buildings-and-wonders.md#city-maintenance-payment-and-forced-loss). The human test is made once for this pair; it is not retested between them. |
| 7 | Add [AI terrain-work goals](A03-ai-settlement-and-terrain-work.md#3-city-terrain-job-publication) (A03) and update the civilization's best-shield summary from this city's calculation. |
| 8 | Unlock refresh and invalidate the cached calculated-city identity. If it was displayed, save its build state and refresh; if zoom was requested, open it. Clear city-processing mode and return the current happy-minus-unhappy count. |

The founder update in stage 1 occurs when
`(turnsCaptured−1) modulo 64 == turn modulo 64`; it assigns the captured owner
as founder. The city-removed path does not execute stage 8. Do not add a
universal cleanup block and assume its workspace effects match the original.

### Open city-analysis arithmetic

The stage-5 reference to additional support/unhappiness government-score terms
does not yet define those terms. C02/C03's explicit yield, corruption and
distribution contributions and C05's retirement sentinel are specified; they
do not establish the missing parent expressions. Stage 7 likewise names the
best-shield summary without defining its producer's scale, comparison and
publication arithmetic. A02 consumes that retained value alongside doubled
shield surplus and also shifts it; those consumers do not uniquely determine
the producer. Keep these two parent operations open pending a precise behavioral
contract or independent original-game observations. Do not infer missing formulas
from historical source records or claim a complete F04 city analysis from the
already specified contributions.

## 7. Unit reset, recovery and movement boundaries

Global reset and per-civilization recovery are separate passes:

| Boundary | Required timing and traversal |
| --- | --- |
| Global reset, AllUnits | Rebuild map unit flags, then scan live units in ascending slot order, with a live upper bound. Mark unit presence on their tiles. For ordinary units, reset spent movement and the auxiliary timer, clear waiting, expire an old border warning on its four-turn slot cadence when not still an intruder, then clear intruder state. |
| Spy mission timer | At that same reset, a unit on a spy mission instead decrements its auxiliary timer modulo 256. If it reaches zero, clear the mission flag, perform valid mission-expiry bookkeeping and call the escape operation. [U04](U04-unit-orders-and-activation.md#mission-expiry-bookkeeping-and-excluded-overwrite) specifies the selector-dependent update and omission of the original out-of-bounds write; V06 owns the escape child. |
| Civilization recovery | Scan that civilization's live units ascending. Capture old flags, then clear moved/paradropped before evaluating recovery from the **old moved flag**. Healing, facility effects, wake-up and turn air attrition are defined in [U04](U04-unit-orders-and-activation.md#civilization-recovery); U05 owns special-movement effects. Global reset did not clear that old-moved evidence. |
| AI movement | After all upkeep, run [A01's unit coordinator](A01-ai-strategy-and-assessment.md#7-assessment-tail-and-outer-movement-passes) for `turnCiv`. A01 owns the two descending passes, captured bounds and action limits; U04 owns active-order execution and A04–A07 tactical choices. |
| Human movement | After upkeep and turn notices, enter the interactive phase below. U04/P01/P02 own selection, commands and unit-order execution. |

The border-warning cadence compares `turn modulo 4` with `unitSlot modulo 4`.
It does not expire every warning at a single universal four-turn boundary.
An auxiliary timer of zero on entry to the spy branch wraps to 255; expiry
tests the decremented value, not the incoming value.

### Human movement phase

At entry, clear the active unit, request input focus, set `playerPhaseActive`,
clear the per-turn tutorial-hint marker, set `waited=false`, and initialize
the previous unit scan bound to −1. Then repeatedly:

1. In move-units mode, validate/select the active unit. Reselect if absent,
   outside the live bound, free, unselectable, or the unit bound changed.
   Otherwise update focus/cursor presentation when needed. Selection and
   tie breaks belong to U04, not this loop.
2. Capture the current unit bound and clear the unanimated-move and focus-done
   indicators. Initially select the input branch for this iteration.
3. If no active unit exists, set end-of-turn, play its sound in move-units
   mode, and redraw. End the player phase automatically only when `waited`
   is true, Always Wait is off, and the city-window-opened indicator is clear.
   Otherwise retain the input branch.
4. On the input branch, enable unit blinking. With no unit, clear the
   city-window-opened indicator and call the idle input wait. With a unit,
   clear both transient indicators and execute its active-unit cycle; OR
   its returned “waited for input” value into `waited`.
5. Still on that input branch, if no city window is selected, clear the
   city-window-opened indicator.
   If the session stopped, return immediately at this point. Otherwise, if
   that indicator remains set, reset `waited=false`. Update the blinking
   cursor if needed and disable unit blinking.
6. If the session stopped, the player phase ended, or `localCiv` is no longer
   human, close an open city window owned by another civilization, flush
   pending input, clear `playerPhaseActive` and return. Otherwise repeat.

An automatic end at step 3 skips steps 4–5 and proceeds directly to step 6.

The idle wait captures the unit bound, enables blinking and pumps music/input
while blinking, player-phase and session states remain active, movement mode
is the nonmoving mode, and the unit bound has not changed. It disables blinking
on return. Message callbacks are ordered inputs, not empty delays.

[U04's active-unit cycle](U04-unit-orders-and-activation.md#active-unit-input-and-automation-cycle)
executes orders or pumps input; its return indicates
that it waited for input, not the number of units moved. Thus exhausting all
selectable units does not by itself prove the human phase can end. Opening
a city window can force another wait. The End Player Turn command exhausts
the local civilization's units; it does not directly increment the global
turn or call the next civilization.

## 8. Human turn notices and random ordering

Human turn notices run before movement even when resumed upkeep was skipped.
Their presentation may invoke further callbacks and randomness; this section
owns the schedule and selection at their entry boundary.

When `turn > 1`, `(turn−1) modulo 50 == 0`, and High Council is enabled:

1. Scan live cities owned by `localCiv` in ascending slot order with a live
   upper bound. Start selected city Absent and best score −1.
2. For each, take its signed city size and double it if it has a palace. If
   this value is at most 1, its score is zero without a draw. Otherwise draw
   once from the primary stream and take the remainder modulo that value.
3. Replace the selected city only for a **strictly greater** score. Equal
   scores retain the first slot. Center the map on the selected city; if
   there is none, use the absent-city label. Show the council invitation
   with civilization name and year. A zero dialog result invokes [P09 council](P09-council-and-throne-room.md#3-council-construction-and-initialization).

This occurs at turns 51, 101, 151, …, not at turns 50, 100, 150. It consumes
draws even if the player declines the invitation. No autoplay gate surrounds
this schedule in the notice routine.

If Tutorial is enabled afterward, perform these checks in order:

- Turn 1: show First Move.
- Turn 20 or 60: show the first help notice.
- Turn 40 or 80: show the second help notice.
- If turn is 35, the local civilization has exactly one city with total city
  size greater than 2, and it has neither an active nor an in-production
  Settler type, find its nearest city to the map midpoint. If found, show
  the one-city notice and set its last-city turn to `s16(turn−25)`.
- Otherwise, if more than 24 turns have passed since its last-city turn,
  it has fewer than six cities, and no active Settler type, show the expansion
  notice and set last-city turn to the current turn. This alternative does
  **not** test Settlers in production. It also does not run when the preceding
  one-city condition was selected but found no city.

The surrounding loop ignores the notice routine's incidental integer return.
Do not derive control flow from a presentation function's residual register.

The sequencer directly samples the history deadline and council city scores.
Spawning, warming, ranking, reports, events, city mechanics, diplomacy, AI and
presentation children have their own decision sites; a complete turn has no
constant sample count. Preserve state-dependent call order and child effects.
Decide using the live
inputs at each specified site; precomputing outcomes must not capture stale
state. Assigning samples from the original shared stream is not required.

## 9. Autosave boundary

Autosave is requested at the **end of civilization upkeep**, after treasury,
cities, diplomacy, census and notices, with `civProcessing` already false.
It precedes human turn notices and movement. There is no autosave at this
boundary when resumed upkeep is skipped.

The helper returns without changing filename or file state unless its argument
equals `localCiv`. With Autosave enabled, the caller still changes directory
to the scenario directory and then home even for another civilization.

For a local save:

1. Select the ordinary-game save extension: `SAV` in single player.
2. Take at most the first **two bytes** of the current leader name, then trim
   leading/trailing spaces and tabs. Append `_` and the localized autosave
   label. This is the stem, for example `Ca_Auto` when those inputs match.
3. The current filename is `stem.SAV`; the backup is `stem2.SAV`.
4. If `turn modulo 8` is in 0…3, delete the backup, then rename current to
   backup. Ignore both return values. In residues 4…7, skip both operations.
5. Invoke the ordinary save writer for the current filename, ignoring its
   return value at this caller. Return to the home directory after it returns.

Saving occurs on every eligible invocation, not only every fourth turn. The
rotation condition groups four turns with rotation and four without. A failed
rename does not prevent the write. [F06](F06-save-load-and-file-formats.md#5-ordered-save-operation)
owns writer format, partial failures and its mutations, including saved-current-civilization state. Do not assume
the save is observationally pure or roll back a completed upkeep on failure.

## 10. Retirement and game-over dispatch

### Scheduled retirement

After global advancement (or its resume skip), call
[V08's scheduled retirement check](V08-scoring-victory-and-retirement.md#5-outcome-producers-and-scheduled-retirement)
before turn events. V08 owns the equality tests, warning/end notices, Won and
pending-reason mutations and network sends. The caller does not suppress a
repeated warning or replace equality with a reached-year test.

### Handling a pending outcome

The session loop checks after retirement/global advancement and again after
each completed civilization movement phase. At an admitted pending outcome,
call [V08's handler](V08-scoring-victory-and-retirement.md#6-pending-outcome-handling-and-continuation)
and use its return to choose exit or continuation. V08 owns preliminary
presentation, graph/score/rating/Hall of Fame order, live reason/Scored decisions,
Keep Playing admission and reason clearing. In particular, scenario retirement
outside year 2020 can return Continue without marking Scored.

Immediate elimination, conquest and combat/event triggers belong to V01 and
the invoking mechanic; this loop does not perform an additional generic victory
scan after every action. Arrival production remains in this document's
[advancement boundary](#spaceship-arrival-boundary). Children can change the
pending reason or Scored flag; preserve their live observations and the stated
session gates. Continuing does not reset the calendar or world.

## 11. Variants, unusual state and failure limits

### Session variants

The application dispatches modes as follows:

| Mode | Sequencing owner |
| --- | --- |
| Single player | The loop specified here. |
| Hotseat | The same local-loop entry has additional player-mask, concealment, password, cursor and timer transitions; [N04](N04-hotseat-and-player-handoff.md) owns them. |
| Sequential network game | Separate host/client loops with phase messages and possible client-to-host handoff; [N03](N03-multiplayer-turn-sequencing.md). |
| Simultaneous network game | Separate host/client loops, production barriers and movement synchronization; [N03](N03-multiplayer-turn-sequencing.md). |
| PBEM branch | Not an established playable MGE mode under X01. Its existence in the local function does not extend this specification's supported modes. |

The global advance helper also accepts a particular civilization or a special
exhaust-all selection for network scheduling. This specification uses AllUnits
only. Shared child ordering does not establish equivalent network scheduling,
and a sequential network resume may force upkeep by a separate host parameter.

### Nominal and off-nominal state

- A nominal resume names a living normal civilization. A dead resume slot can
  cause the next living slot to skip upkeep. A resume value above 7 admits no
  civilization, leaves resume uncleared, and can repeat the fallback human
  phase without advancing the global turn. A stricter loader can reject it;
  that is a declared input policy, not an extra check inside this loop.
- Turn and year wrap are deterministic. At turn 32767, advancement wraps to
  −32768 before calendar, peace-counter and deadline tests. Retaining a larger
  turn internally without applying the signed reduction changes behavior.
- Treasury normalization has an intentional discontinuity: −16385 becomes
  30000, while −16384 becomes zero. Do not replace it with a single clamp.
- A zero spy timer wraps on decrement. The original AllUnits mission-expiry
  bookkeeping can write outside its relationship array; this is an excluded
  bug even though an ordinary caller reaches it. U04 omits the invalid write
  while preserving timer/flag changes and the escape call. Complete-turn
  fixtures must check that continuation without requiring unrelated corruption.
- Invalid record indexes, unsupported unit domains and missing scratch state
  do not acquire a universal safe fallback here. Apply X01's bounded failure
  rule at the first unrecovered access, preserving earlier effects.

Nominal file validation alone cannot remove arithmetic boundaries produced
by later play. Alternative implementations may restrict accepted saves/rules
but must state the domain and rejection effects. The classic observations for
accepted executions, including callbacks, remain the compatibility target.

## 12. Delegated contracts and remaining review

This document specifies a sequencer that can be implemented and tested with
explicit child operations. It does not by itself implement every child. Their
input is current shared state at the listed boundary, and their mutations,
draws and callback effects become input to the next phase.

| Child contract | Required integration consequence | Owner |
| --- | --- | --- |
| Barbarian spawning, warming, power/throne updates | Run in order with the old turn/year; preserve child draws and mutations before increment. | W05/W06, A01, [P09](P09-council-and-throne-room.md#7-throne-room-progress-and-admission) |
| Unit reset and recovery | Global movement/timer refresh precedes per-civilization healing; recovery reads old moved state. Join U04's safe mission-expiry continuation with V06 before claiming that complete case. | U04/U05/V06 |
| Food, recalculation, production, support, disorder, research, pollution, upkeep | Honor the two recalculations and city-removed return; retain ascending/descending scan distinctions. | C02–C06/C08, V03, W05 |
| AI rates, government scores, diplomacy and census | City summaries feed postprocessing, then diplomacy, then census. AI movement observes their completed state. | A01/A08, V02/V04 |
| Movement and commands | Human wait/stop values and AI movement mutations are live; do not treat interaction as a pure function. | U02–U05, A04–A07, P01/P02 |
| Scenario events | Four ordered passes, plus reactive hooks within child mechanics. Actions can change turn, control sets and later event inputs. | S02 |
| Autosave/write/load | Resume producer and file writer must match the pre-movement snapshot and their own partial side effects. | F06 |
| Outcome predicates, scoring and media | Arrival predicates precede retirement; other outcomes are posted by children and handled at explicit gates. Reports/media can consume randomness. | V07/V08, P07/P10 |

Integration cases must reconcile these contracts, especially city deletion
during notices, scenario actions during a pass, save/reload around upkeep,
spy timer expiry and callback-driven exit. N03 specifies the network phase
traces; N04 specifies the hotseat transitions and original saved-actor boundary.
The source contracts and supplied-child fixtures do not establish independently
verified whole-game turns.

## 13. Worked conformance cases


> Compare the gameplay/interchange observations in the
> [production scope](#production-contract-scope). Original memory/helper traces,
> arbitrary native callbacks and unsafe backing-byte cases in historical lists
> are optional reference, not additional production acceptance criteria.

For sequencing cases, child stubs return normally, consume no draws and mutate
nothing except the explicitly stated changes. Presentation callbacks likewise
do nothing unless specified. This isolates F04; it does not claim real children
are pure. Trace every selected child, record initial/final shared state and
the declared random inputs. Unmentioned state is retained.

### Nominal operation

| Input | Expected observation |
| --- | --- |
| No resume; turn 0; alive slots 0, 1, 3; local/human slot 1; history deadline 100; no events or outcome. Stop during the human movement phase. | Advance to turn 1/year −4000 without old-turn environment/rank/throne calls. Reset units, then upkeep/movement for 0, then upkeep/notices/movement for 1. No processing of 3 after the explicit stop gate. |
| Same alive set; incoming turn 20, resume 1, reprocess false. Stop after human movement. | No global advance, no turn events or slot 0 processing, no upkeep/autosave for 1. Human notices and movement still run, then all resume fields clear. |
| Resume 1, reprocess true, events loaded. Event callbacks are inert. | No global advance; call exact-turn, interval, random-turn and received-tech passes in order, then upkeep for 1. |
| City scan bound 6; live owned cities 1, 3, 5. City 5's child deletes 3 and creates owned city 7, increasing the bound. | Process 5 then 1. Skip deleted 3 and do not visit 7 during this captured-bound pass. |
| Ordinary-sized prince game; no launch/scenario override. Calendar inputs 0, 1, 61, 62, 101, 102. | Years −4000, −4000, −1000, −975, 1, 10. |
| Same calendar, first launch at turn 101, input 110. | Base calendar uses turn 101 and returns 1; add 9, producing 10. |
| Active scenario; start 100, increment −12, no launch; input turn 2. | Numeric calendar result 123; display uses the monthly convention separately. |
| After increment, turn 50 and history deadline 50. Then repeat at turn 51 with the deadline still 50 and primary draw 39. | First invocation has no schedule draw. Second consumes one and sets the deadline to 110 before showing the report. With autoplay active, the same draw/deadline occurs without the report. |
| High Council enabled, turn 51, local cities in slots 2 and 5; sizes 1 and 2, no palaces; next primary draw 0. | City 2 scores 0 without a draw; city 5 scores 0 using one draw. Select city 2 on the tie. Declining the invitation does not undo the draw. |
| Autosave enabled; local leader Caesar; autosave label Auto; turn 2, then turn 6. | Both invoke `Ca_Auto.SAV` writing after upkeep. Turn 2 first deletes/renames to `Ca_Auto2.SAV`; turn 6 does not rotate. |

### Arithmetic, resume and failure boundaries

| Input | Expected observation |
| --- | --- |
| Civilization treasury −16385, −16384, −1, 30001 in separate upkeep calls. | Captured startGold values 30000, 0, 0, 30000. |
| Resume slot 2 dead, slot 3 alive, reprocess false. | Slot 3 receives the initial upkeep skip, then movement; completing it clears resume. This is not a promise that such a save is nominal. |
| Two newly arrived ships in slots 2 and 4. | Mark only slot 2 on this advance; slot 4 waits for another invocation. |
| Unwon, no pending reason, no applicable scenario limit; year 2021. | No retirement warning or retirement outcome. Equality with 2020 was missed. |
| Pending scenario Retirement at year 123, not previously scored. | Show power graph, score, rating, hall of fame; do not offer Keep Playing or set scored through the year-2020 branch; clear reason and return Continue. |
| All eligible units exhausted; no earlier input wait in this player phase. | Lack of units alone does not select the automatic end branch. |
| Autosave backup deletion/rename fails. | Still call the save writer. The caller does not roll back upkeep or propagate that helper's write result. |

## Appendix A. Original encodings and snapshot mapping

The logical values above need not use these addresses or field layouts.

| Logical value/operation | Original mapping |
| --- | --- |
| Active loop, resume slot | `turn_loop.active` at `00628044`; resume byte at `00628048`, read unsigned for scan comparisons. NoResume = 0. |
| Reprocess resumed upkeep/events | Runtime integer at `0062c488`; saved resume civilization and reprocess word at `00654fa4`/`00654fa6`. Both saved and runtime forms are cleared after a civilization phase. |
| Civilization processing | `0062c5b8`; spaceship winner `0062c5b4`; tutorial-per-turn marker `0062c5c0`. |
| Calendar | Four tables of six 12-byte triples at `0062c490`; each triple is signed 32-bit start, count, step. The period loop reads the next count before testing period < 6, including one adjacent value after the last period. That extra read does not affect the result within the mapped backing. |
| Turn/year/first launch | Signed 16-bit game fields at offsets `10`, `12`, `14` hexadecimal. Peace counter at `2c`; history deadline at `136`. |
| Map/scenario/outcome state | Game flags: large `0004`, small `0008`, scenario `0080`, won `0002`, scored `0020`. |
| Transient state | End-of-turn `0001`, city-window-opened `0002`, attitude-dirty `0004`. |
| Unit reset selection | AllUnits = −1; alternative all-unit reset selector = −2; particular civilization = 0…7; network exhaust-all = −3. Do not replace a negative selector with the owner before mission-expiry bookkeeping. |
| City-removed return | −999. Remaining successful return is current happy minus unhappy. |
| Pending outcome | 0 none; 1 local spaceship, 2 other spaceship, 3 conquest, 4 defeat, 5 retirement/scenario end. |
| Autosave rotation | `(turn & 4) == 0`; current leader text truncated by writing null at character offset 2 before trimming; label `005c`; original backup-name local capacity 80 bytes. |

The arithmetic domain, scan order and returned sentinels remain behavioral
requirements when translating snapshots to a different object model. Extra
calendar reads and filename capacity matter for original-memory/failure tests;
they are not a requirement to use unsafe indexing or fixed-size strings.

## Appendix B. Evidence and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Evidence is pinned to OpenCiv2
`554851eb19faedc57220e679597d7678bf409a08` and the independent reconstruction in
`civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`.

### Source traceability

The external OpenCiv2 links below use published revision
`a71e8aefffe04e6f719e0e213b027b128a747711`. Every linked file or directory was
verified to have the same Git object as at this document's pinned evidence
revision. See [source provenance](README.md#source-provenance) for the
evidence-link conventions.

| Contract | Source and original entries |
| --- | --- |
| Application/session dispatch | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): `game_main` `0041f8d9`, `game_session_begin` `0041f69f`, `game_session_end` `0041f878`. |
| Local loop and retirement/outcomes | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): `game_play_loop` `0048b340`, `game_check_retirement` `0048aedc`, `game_handle_game_over` `0048b165`. Helpers split out of the local loop retain that original boundary. |
| Global advance, calendar, autosave | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): `game_advance_turn` `00487371`, `game_turn_to_year` `00484fec`, `game_autosave` `00488937`, save extension `00473d5e`. |
| Unit timing and input loop | [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp): `units_reset_for_turn` `0048710a`; [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): active unit `0048a004`, wait `0048a374`, human movement `0048a416`; [window.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/window.cpp): end-player-turn command `0058bd84`. |
| Civilization/city coordination | [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp): `civ_process_turn` `00489553`, `civ_process_cities_turn` `00487a41`, recovery `00488cef`, population/funds `00489292`; [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp): `city_process_turn` `004f0a9c`. |
| Human summaries and notices | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): wonder extrema `0048a92d`; [retvals.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/retvals.cpp): `game_turn_start_popups` `0048aa24`, load/resume production. |
| Event and AI boundaries | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): event passes `004fba0c`, `004fba9c`, `004fbb2f`, `004fbbdd`; [ai.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/ai.cpp): census `0053184d`, unit coordinator `00543cd6`. |
| Tables and state | [civ2types.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h), [game state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/game.h), [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h), [original data image](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/data/image/data.bin) and its [layout](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/data/image/layout.json). |

Independent evidence at the pinned `civ2` revision includes
`src/local_turn.cpp`, `src/window_startup.cpp`, `src/civilization.cpp`,
`src/civ_turn.cpp`, `src/city_turn.cpp`, `src/units.cpp`, `src/human_turn.cpp`
and `src/civ_autosave.cpp`. It corroborates phase ordering, calendar tables,
live reads, treasury normalization and autosave rotation. Its explicit stops
for missing backing state are evidence boundaries, not original error returns.

### Differences and bounded child behavior

- OpenCiv2's extra gold-limit and retirement options change treasury limits
  and replace equality retirement checks with configurable thresholds. Those
  extra-rule branches are excluded here.
- B34 changes hotseat restoration of the local player after a civilization
  loses human status during upkeep. Even OpenCiv2 classic mode supplies a
  safer result when no prior AI phase initialized the saved player. This is
  a hotseat state/backing obligation specified in
  [N04](N04-hotseat-and-player-handoff.md#original-actor-width-and-private-saved-state),
  not a single-player rule. N04 separately excludes the sentinel casualty
  reported-count overwrite while specifying its backed reads.
- BF-046 expands the autosave backup filename buffer. Ordinary names/labels
  fitting the original capacity retain the behavior described here; overflow
  writes beyond that buffer are excluded bugs. Safe storage or rejection must
  prevent the overwrite; reproducing its damage is not required.
- BF-028 supplies an initial healing-facility value for unsupported unit domains.
  Do not use that port default as proof of original recovery behavior.
- Mission-timer expiry indexes its relationship-like bookkeeping using the
  **reset selector**, not necessarily the unit owner. With AllUnits, the original
  write corrupts preceding naming state. U04's appendix records that bug and
  its main procedure omits the invalid write. Joined whole-turn/escape fixtures
  must preserve the remaining valid effects; an owner-based relationship update
  would be an additional inferred repair, not part of this contract.
- BF-281 adds load-time validation for a saved resume civilization above 7.
  That producer check does not exist in the local-loop body. File acceptance
  and the loop's behavior on supplied continuation state are separate domains.

See the [bug ledger](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md) for relevant fixes. Classic-mode selection alone
does not prove all memory and callback boundaries match the original.

### Validation scope

Source review covers the single-player loop, phase coordinators, calendar,
resume gates, autosave and notice/retirement schedules above. Calendar constants
were compared between OpenCiv2's original data image and the independent
reconstruction. The [recorded unit-case inventory](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/tests/unit/cases.tsv)
contains calendar, autosave, city-pass, turn-notice and end-player-turn cases;
the [turn replay script](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/harness/scripts/turns.txt) is an integration input,
not proof of every branch described here.

A temporary extraction harness exercised eight complete source functions:
calendar conversion, global advancement, autosave, retirement, game-over
dispatch, the local session loop, human wonder extrema and civilization upkeep.
It also exercised the city-pass prefix through the end of its notice batch,
excluding subsequent rate/scoring formulas. All **165 assertions passed**,
covering calendar boundaries, phase/resume gates, a live city mutation during
the descending scan, treasury normalization, autosave order and failure
continuation, spaceship-part notices after control or ship changes,
reputation/history/arrival transitions, signed counter wrap and end-game
decisions. The 72 calendar constants matched both the original data
image and the independent reconstruction.

The harness used the actual game-state type, signed-character and wrapping
arithmetic settings, and adapters for other state, UI, files, child operations
and scripted random returns. Its unit-reset adapter interpreted the AllUnits
argument in the original signed 32-bit domain. Human notices and movement were
stubbed at their boundaries; their internal control flow, dialog cleanup and
full child mechanics were not validated by these assertions. These are focused
source-execution checks with expected traces, not an independent whole-game
implementation or execution of the original binary.

No new full original-program replay or complete turn with all child mechanics
is claimed. In particular, callback-rich deletion/elimination, unusual resume
state, safe mission-expiry continuation, network/hotseat variants and child
integrations require the scoped follow-up in section 12. These checks do not
establish full independent conformance.

[V02](V02-governments-and-tax-rates.md#appendix-b--validation-and-remaining-integration)
owns the shared treasury normalization, government prefix and population/funds
children, with finite source checks and explicit query/UI/network adapters. Their
ordered positions above remain F04's contract; full native/dialog/AI/effect joins
and consecutive civilization turns still require validation.
