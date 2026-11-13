# V07 — Spaceship

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch3**. This owns spaceship part counts/capacities, performance,
assembly, group selection, launch, arrival predicates, shared reset/loss and race
queries. It includes the state decisions at report/window boundaries; P07/P10
own rendering, input translation, animation, timers and media playback.

[C06](C06-city-production-and-purchasing.md) owns production eligibility, costs,
shield spending and the calls to group selection/addition. [F03](F03-rules-and-configuration.md)
supplies the mass/thrust rule and part prerequisites/costs. [V03](V03-research-and-technology.md)
owns knowledge queries. [V04](V04-diplomacy-and-treaties.md) owns treaty mutations.
[F04](F04-turn-sequencing.md) owns calendar conversion and arrival processing;
[V08](V08-scoring-victory-and-retirement.md) owns scoring, victory presentation and continuation. [V01](V01-civilization-lifecycle-and-civil-war.md)
and [C09](C09-city-capture-and-revolt.md) decide when civilization/capital loss
invokes this document's ship loss/reset. A02/A08 use the group/race decisions
here within their wider production and strategy policies.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

Independent group-query terms can be evaluated as described below. Assembly,
production, launch, arrival/reset/loss and callbacks preserve their mutations,
required publications and live-versus-captured inputs.

## 1. State, arithmetic and accepted inputs

Each civilization stores six signed part counts, independent Exists/Launched/
Arrived/Won/Fusion flags, a population measure, launch year and arrival year.
Shared calculated statistics contain mass, support/energy/fuel percentages,
success percentage, flight time and a projected arrival year. This scratch is
shared across civilizations; a launched ship calculation leaves projected arrival
unchanged. Do not silently replace it with a fresh per-ship value.

Parts and groups are fixed gameplay categories:

| Part p | Name | Group | Hard maximum | Mass units per part |
| --- | --- | --- | --- | --- |
| 0 | Structural | Structural0 | 39 | 1 |
| 1 | Propulsion | Component1 | 8 | 4 |
| 2 | Fuel | Component1 | 8 | 4 |
| 3 | Habitation | Module2 | 4 | 16 |
| 4 | Life support | Module2 | 4 | 16 |
| 5 | Solar energy | Module2 | 4 | 16 |

One mass unit represents100 tons; one habitation unit represents10,000 people.
Flight values below are tenths of a year. These numeric scales affect rounding;
a different internal representation must preserve the same results.

Use F01's behavioral arithmetic, truncating division, arithmetic right shifts
and ordered `clampAB(x,lo,hi)`. Nominal part counts are integers 0…39 for
Structural, 0…8 for each Component, and 0…4 for each Module; supported capacities
can be lower than stored counts. These counts require sufficient range, not
signed16 variables. Mass/thrust paradigm is nominally 25…200, stock 75; F03 owns
its ingestion. Preserve tenths-of-year calculations and their rounding.

Population and saved-year publications retain the specified `s16` results.
For admitted extra count values, specified count writes also apply `s16` and
F01 governs any significant intermediate wrap. The original blanket storage
annotations are in [Appendix C](#appendix-c-original-numeric-annotations). A nominal bound is not permission to clamp an
accepted over-limit stored count or skip its contribution to mass.

Civilization indices0…7 are backed; ordinary race scans use1…7 without an
implicit alive filter. Human membership, local human and autoplay are distinct.
Read fields again at the operations below unless explicitly cached. UI/network,
production, knowledge and calendar children can affect later reads; no general
transaction rollback is implied.

For accepted off-nominal inputs retain in-bounds negative counts, arithmetic wrap,
reversed clamps and custom reply effects. A stricter implementation may declare
narrower inputs. Invalid collection writes and resulting corruption are excluded
bugs under [X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
even where adjacent named storage exists. Stop/reject before them rather than
recreating that storage alias. Unknown or out-of-bounds reads require evidenced
backing or a bounded stop; they do not acquire an invented zero value. Division
by zero or signed quotient overflow has F01's explicit fault boundary.

## 2. Part capacities and group queries

Let S be the live stored Structural count, max[p] the table above and C the
owner. `partLimit(C,p)` first reads max[p], then:

- p=0: return that maximum.
- p=1 or2: return `clampAB(max[p],0,(S+1-(2*p-2))/4)`.
- Other backed parts: compute `limit=clampAB(max[p],0,(S+1-(4*p-12))/8)`.
  If C is AI, replace limit by `clampAB(limit,0,1)`. Finally replace a negative
  result by0 and return it.

The divisions truncate toward zero. Supported first parts require S=3 for
Propulsion,5 for Fuel,7 for Habitation,11 for Life support and15 for Solar.
AI owners can use at most one of each module, even when they have built more.
For accepted negative S, the component branch can return a negative capacity
because it lacks the module branch's final zero floor.

`partCount(C,p)` captures the stored signed count **before** calling partLimit,
then returns `clampAB(captured,0,limit)`. This is the effective/supported count,
not a destructive clamp of the stored part record.

Group0 uses part0. Group1 sums parts1 and2; group2 sums parts3,4,5. Group helpers
with an else-module branch also treat other group values as modules; operations
which index `improvement[group+35]` still need that read's own valid domain.

| Query | Exact operation |
| --- | --- |
| groupMax | Sum hard maxima:39,16,12. |
| groupCount | Sum stored signed counts, without clamping. |
| groupLimit | Sum the group's partLimit values. |
| groupCountClamped | Sum the group's partCount values. |
| groupFull | `groupMax<=groupCount`. |
| groupAtLimit | `groupLimit<=groupCount`. |
| groupCanBuild | Query groupAtLimit. Continue if false, or if a fresh groupCount is exactly0. Then require !groupFull, then V03 knowledge of that group's improvement prerequisite. Short-circuit in that order. |

These pure group queries can evaluate their terms in any equivalent order on
stable, fully backed state. Preserve each part's clamp/division, raw-versus-effective
count distinction and significant sum conversions. The reference query sequence
in [Appendix D](#appendix-d-original-query-sequencing) applies if admitted
read/failure behavior makes order observable.
Do not extend this freedom across production, knowledge-grant, calendar, UI or
network operations that can change subsequent inputs.

Consequently an empty unsupported group can build its first part. A nonempty
group at its structural limit cannot continue through groupCanBuild. This query
alone does not enforce Apollo, date, Bloodlust, Launched or production costs;
C06 owns those caller gates.

`partMaxed(C,improvement)` is a different production predicate: Structural tests
S>=39; Component requires both stored Propulsion>=8 **and** Fuel>=8; every other
improvement argument uses all three module counts>=4. For off-nominal uneven
counts, this differs from groupFull's sum test. Do not merge the predicates.

`thrust(n)` starts0 and loops i=0 while i<n: add1; use an increment of2 instead
when i>3; add another1 when i>5. Thus nominal n=0…8 yields
`[0,1,2,3,4,6,8,11,14]`; ordinary integer addition suffices on that domain.
Negative n performs no iterations. Larger admitted counts retain F01's reference
accumulation results where overflow affects the result.

## 3. Performance calculation

`calculate(C,includeSupport)` mutates shared scratch and, for an unlaunched ship,
its saved preview fields. It draws no random value.

1. Query Launched. Only if false query knowledge of Fusion Power; if known, set
   Fusion. Do not clear an existing Fusion flag when knowledge is absent. Capture
   the ship flags after this step for the engine modifier below.
2. Zero shared mass. Scan p=0…5 and add `stored[p]*mass[p]`. Capture fullMass as
   that total, then scan p=0…5 again and add one mass[p] for each **exactly zero**
   stored count. Negative counts do not receive this missing-kind supplement.
   Unsupported/over-limit parts still contribute to mass.
3. Set success=100. Query effective Life support then Habitation:
   `support=100*life/clampAB(hab,1,99)`. When includeSupport!=0, replace success
   by `clampAB(support,0,100)*success/100`. Otherwise leave success unchanged.
4. Query effective Solar, Habitation, then Life support:
   `energy=200*solar/clampAB(hab+life,1,99)`. Always multiply success by
   `clampAB(energy,0,100)/100`, with the product divided after multiplication.
5. Query effective Fuel then Propulsion:
   `fuel=100*fuelCount/clampAB(propulsion,1,99)`. The reported fuel percentage
   is not another direct success multiplier; thrust below constrains flight.
6. Read the mass/thrust paradigm P. With captured Fusion, set P=(P*3)>>2.
   Start scale=1; while P>100, double scale then arithmetic-shift P right by1.
   With the nominal unsigned rule byte, these are nonnegative values.
7. Compute three flight estimates, repeating their component queries each time:
   - A uses shared mass and effective components; store it as shared flightTime.
   - B uses captured fullMass and effective components; retain locally.
   - D uses captured fullMass and raw stored components; retain locally.
   Each uses `numerator=s32(massForEstimate*P)`, and
   `denominator=clampAB(10*thrust(propulsion),0,10*thrust(fuel))+1`.
   Read Fuel and compute its thrust before reading Propulsion. Divide numerator
   by denominator with truncation. Do not reuse A's denominator for B.
8. If scale>1, multiply each of A/B/D by scale **after** its division. A direct
   full-paradigm division is not equivalent for odd paradigms or rounding.
9. If shared flightTime>150, subtract `(flightTime-150)/10` from success.
   Then clamp success to0…100. The penalty begins affecting success at160,
   while156 still subtracts0. It uses A, not either arrival estimate.
10. Query Launched again. If still false, store effective Habitation as population;
    call F04 calendar(current turn) and store launch year; call calendar again
    and store `year+B/10` as arrival year; call calendar a third time and set shared
    projected arrival=`year+D/10`. The two saved years retain signed16; the shared
    projected year retains signed32. Each call reads the live turn.
    If now launched, skip all four publications, leaving their previous values.

Displayed mass and flight split the integer into quotient/remainder by10; this
presentation does not change calculation precision. The calculated arrival year
uses B's missing-kind mass, whereas displayed flight uses A's actual mass. The
shared projected year uses D's raw engines and can be earlier than the saved
arrival for an under-supported ship.

A nominal full human ship `[39,8,8,4,4,4]` has mass295, all ratios100, flight156
and success100 at stock P75. With Fusion, P becomes56, flight117. A human ship
`[15,8,8,1,1,1]` has mass127 but effective engines4/3: flight307, saved arrival
current calendar+30, projected arrival current calendar+6. These are deliberately
separate estimates.

Success is a calculated percentage used by UI, AI and V08 scoring. Neither this
calculation nor the launch/arrival predicates performs a success/failure lottery.
P07/P10 presentation can consume F01 randomness separately.

## 4. Selecting a production group

`chooseGroup(C,g)` begins calculate(C,0), even when it will reject the request.
If Launched is then true return-1.

Scan human civilizations1…7 for the smallest **positive** stored arrival year,
starting best=9999, using strict improvement. No Launched, Exists or alive test
is made for this preview scan. Define raceClose when C's saved arrival is nonzero,
`C.arrival-game.year<=14`, and best>C.arrival. A negative wrapped arrival can
satisfy those tests; do not add a future-year requirement.

Set preferOther=false. If incoming group is Structural, set preferOther when
Module's effective group count is below its current limit. If that is false and
!raceClose, also set it when Component's effective count is below its limit.

Keep the current group only if all hold in order: !preferOther; group is not
Component or !raceClose; its effective count<limit; groupCanBuild succeeds.
Otherwise search groups descending from Module2 to Component1 when the incoming
group was Structural, or to Structural0 for any other incoming group. At each,
query groupCanBuild first, then reject Component when raceClose, then require
effective count<limit. The first match replaces g and ends the search. With no
match retain the incoming g.

If the resulting g is Structural, scan human civilizations1…7. For each launched
human whose arrival<=shared projected arrival, require !groupFull(C,Component),
then knowledge of Component's prerequisite. Query raw Propulsion and its limit;
if raw<=limit return Component immediately. Otherwise query raw Fuel and its
limit; raw<=limit likewise returns Component. Equality is allowed, unlike the
ordinary strict capacity test. This early return does not call groupCanBuild
again. Finally, for all other continuations, return g if groupCanBuild(C,g),
else-2. These two negative results have different causes; C06 handles both.

## 5. Adding a part and automatic launch

`addPart(C,g)` starts selected=-1, calls calculate(C,0), then tests Launched.
If true return selected. Otherwise enter the selection loop below. This is the
assembly algorithm; it does not itself spend shields, validate all C06 production
gates or discard parts exceeding current structural support.

### Selection loop

- Structural: if raw S>=39 return the current selected value; otherwise selected=0.
- Component: if raw Propulsion>=8 and raw Fuel>=8, return current selected.
  AI chooses Propulsion when its raw count is strictly smaller, otherwise Fuel.
  A human receives Component with both counts and selected=`reply+1`.
- Every other group takes the Module branch: if all three raw counts>=4 return
  current selected. AI initializes a local minimum999, scans p=3…5 and replaces
  selected only for a strictly smaller count. Ties therefore favor Habitation,
  then Life support, then Solar. A human receives Module with all three counts
  and selected=`reply+3`.

Re-read human membership. If AI, exit the selection loop without rechecking the
chosen subtype's maximum. If human, compare that selected raw count with its hard
maximum. When below, exit; otherwise publish its label, show NoFurther and repeat
from the group branch. Do not reset selected=-1 on a repeat. A callback making
the entire group full can therefore return the retained nonnegative selection
without adding a part.

Stock Component replies0/1 select1/2; Module replies0/1/2 select3/4/5. There is no
generic signed-negative cancellation gate here. For example Component reply-1
selects backed part0 and can add a Structural. Other custom results can select a
different backed part, cause an unsupported read, or reach an excluded invalid
count write. Keep these domains separate; do not require overwriting population
or adjacent state for a selected index outside0…5. A strict UI may reject custom
results at ingress, explicitly documenting that policy.

### First ship and increment

If Exists is false, set it. Then scan human civilizations H=1…7; admit H=C or
H with no existing ship. For each, set lastContact[H][1…7]=0 ascending. The current
ship's new Exists flag is already visible to these tests. This does not touch
column0, AI rows or other humans who already have ships, and has no alive filter.

Increment stored[selected] with signed16 retention, then calculate(C,0) again.
If C is human now, return selected. For AI, reuse the selected variable as a loop
counter0…2 to test whether each of Structural/Propulsion/Fuel is at its hard
maximum; the ordinary return value from the rest of this path is therefore **3**,
not the added subtype. This matters even when the AI added Fuel.

### Automatic launch decision

After that full-engine test, require not Launched and shared success>=40.
If the three engine/structural kinds are full, launch immediately. Otherwise:

1. Scan human civilizations1…7 for the earliest launched ship. Retain the first
   on equal arrival years. No alive/Exists filter is added.
2. If none and difficulty>King, scan other AI civilizations1…7 with launched
   ships. If any arrival is less than **civilization0's stored arrival year**,
   return3 without launch. The original comparison uses slot0 while the missing
   earliest-human selector is0; this is a valid record read, not an out-of-bounds
   write or a reason to substitute C's arrival.
3. If a human ship was found, launch only when C's arrival is strictly earlier.
4. If none, launch when success>75. Otherwise scan humans ascending: launch if
   C's power-rank number is smaller than that human's, or if that human has an
   existing ship and gold>999. Stop on the first qualifying human.

No autoplay gate, additional habitation check, support-inclusive recalculation
or final random success draw is inserted. The current shared success came from
calculate(C,0), which omitted the life-support multiplier.

## 6. Launch and diplomatic consequences

`launch(C)` is a mutation entry, not a universal eligibility validator. It first
sets Launched. If firstLaunchTurn<0, store the current turn there. It does not
recalculate statistics, set Exists or update arrival/launch years. Callers must
supply the preview calculation when their path requires it.

Publish C's plural name and saved arrival. In modes>2 broadcast Launched text.
If C is AI at this point, show Launched and run the spaceship window with a
**negative** civilization argument to request the launch presentation.
For the backed exceptional owner0, negation still gives0, so the window request
takes its ordinary nonnegative mode.
Re-read human membership after those callbacks:

- If still AI, scan live city slots ascending to the live bound; for each nonzero
  token owned by C request C06 production selection99. Other production fields
  are not directly overwritten here.
- If human, scan D=1…7 excluding C, excluding humans, requiring D Launched and
  C.arrival<D.arrival. No alive filter. Apply the following to each qualifying D.

For difficulty below Emperor: below Prince, or C.reputation==0, call the shared
V04 treaty setter for Incident(C,D). Otherwise directly add D→C HostileIntent.
For Emperor/Deity: if **C→D Alliance** is absent, first clear D→C Ceasefire,
Peace and HostileIntent, then in a separate ordered mutation add BrokenTreaty,
Aggressor and AggressorLong. If C→D Alliance is present, only add D→C
HostileIntent. Do not replace these direct reverse-direction writes with full
war declaration or a bilateral peace cancellation.

The first-launch marker changes F04's future calendar rate and is not cleared
when a ship is destroyed. The launch routine does not require it to belong to C.

## 7. Reports and graphical launch boundary

### Text status report

`statusDialog(C,allowLaunch)` calculates with includeSupport=1. If the raw
Habitation count is exactly0, clear allowLaunch. Publish adjective, leader and
plural names; load Spaceship text. Scan all six raw counts against their limits
to determine over-limit, then append the six part rows in order, annotating the
Structural row when any part exceeds its limit. Append blank row, raw Habitation
population, support, energy, mass, fuel/Fusion annotation, flight and success.

Query Launched. If false, append a launch button only when C is human,
allowLaunch!=0 and current success!=0. If true, append arrival status: it says
arrived only when saved arrival<game.year; equality still displays the in-flight
arrival year here, although the shared arrival predicate already returns true.
Run the modal. If its extra-result field is nonzero, publish the **current shared**
success, show Launch confirmation and call launch(C) for any nonzero reply.
There is no second eligibility check after modal callbacks.

The view selector lists all Exists ships in civilization order1…7, without
alive/human/contact filters. With none show NoSpaceships. Otherwise a nonnegative
selected civilization snapshots its six raw counts, calls the graphical window,
and falls back to statusDialog(C,humanMembership(C)) only when that request returns0.
Negative selection cancels. A custom nonnegative result still needs backed
civilization state; the absence of a row is not a validation guarantee.

### Graphical path and service contract

The general window request returns1 without opening if another spaceship window
or the council is active. Otherwise construct/init for the absolute civilization
argument; initialization failure returns0 after cleanup. A negative argument
runs launch presentation; a nonnegative one runs the interactive modal. The
local-only presentation request returns0 for a nonlocal civilization; otherwise
it has the same already-open/council success and initialization-failure results.
A return1 therefore need not mean that a screen was shown or launch occurred.

Initialization resolves the ship resource library before calculate(C,1) and stats
text. Readiness is raw Habitation!=0 and success!=0; Launched overrides it to the
launched display state. The launch hotspot/button belongs only to the local
civilization's ready ship. Ordinary launch input requires the ready state and
completed typing; it changes display state to LaunchRequested and exits the
modal. [P07](P07-advisors-reports-and-history.md#19-spaceship-presentation) owns the actual region, press/release and typing state machine.

Both interactive-modal and local construction-presentation continuations, when
LaunchRequested, finish their capture/timer phase, play launch video, set display
state Launched, invoke launch(C), then re-enter the ship modal. The negative-
argument launch sequence instead plays video and shows the launched display
without invoking launch(C) again. The gameplay mutation already preceded that
AI presentation. P07/P10 must preserve this distinction and real callback effects.

Network window idle polls messages(1,0) first. End the modal when host handoff is
nonzero or signed elapsed ticks>=1201; otherwise return. The clock/start field and
active window are read after polling. Presentation typing/starfield consumes
randomness and pumps messages; replacing the entire window with an inert return
is not whole-game conformance. P07/P10 own its rendering tables and those draw
sequences, including overlong-text safe handling in Appendix A.

## 8. Race, arrival, reset and loss

`raceActive()` is false when Bloodlust is enabled. Otherwise it is true when
Apollo's **raw** wonder location differs from Unbuilt, or any human membership
bit intersects global Space Flight knowledge. The original raw location test
also accepts the Destroyed sentinel; do not substitute C08's active-wonder query.
It does not require a built ship or any launched ship.

`raceBehind(C)` is the shared AI threat query. Require raceActive, !Launched(C),
!gameWon, difficulty!=Chieftain and C AI, in that order. Scan humans1…7; any
Launched immediately returns1. Otherwise compute C's raw progress as the sum
of stored[p]*unsigned improvementCost[group(p)], with group mapping0,1,1,2,2,2.
Scan humans1…7 again: query Space Flight knowledge, then exclude C. Compute the
same progress; C.progress<=human.progress returns1, so a tie counts as behind.
If none qualifies return0. There is no cost multiplication by shield-box rows,
capacity clamp or alive filter in this comparison.

`shipLaunched` and `shipExists` test their independent flags. `shipArrived(C)`
queries Launched, then returns true when game.year>=saved arrival; it does not
require Exists, success, positive population or unprocessed Arrived. F04's
[arrival boundary](F04-turn-sequencing.md#spaceship-arrival-boundary) owns the
ascending scan, one-arrival-per-invocation transition, winner and outcome flags.
[V08](V08-scoring-victory-and-retirement.md#6-pending-outcome-handling-and-continuation) owns their scoring/endgame consumers. There is no journey-progress counter
or probabilistic destruction step here.

`resetShip(C)` clears all ship flags, population, launch year, arrival year, then
all six counts ascending. It leaves firstLaunchTurn and shared calculated scratch
unchanged. It does not recalculate or show a notice.

`shipLostNotify(C)` first publishes C's adjective. Query Launched; if true,
broadcast SpaceReturns in network modes then show it. Otherwise query Exists;
if true, broadcast SpaceDestroyed in network modes then show it. These notices
have no human/local/autoplay gate in this child. Finally resetShip(C), even if
neither notice applied. A launched flag without Exists still takes Returns.
V01 owns removal, C09 owns capital-loss admission and when to invoke these effects;
this routine does not independently decide whether a civilization has a capital.

## 9. Conformance cases

These cases check behavior, independent of record layout. Compare stored counts,
flags, preview/scratch values, ordered child calls and the absence of invented
random draws. They are targets for cleanroom validation; the executed subset is
identified separately below.

| Input or transition | Expected behavior |
| --- | --- |
| S=2/3/5 | No supported engine at2; first Propulsion at3; first Fuel at5. |
| S=7/11/15 | First supported Habitation/Life support/Solar respectively. |
| Full human and AI ships | Raw Module count12 for both; effective Module count12 for human,3 for AI. |
| S=-10, stored Propulsion1 | Component capacity/count can be-2 through reversed clamp. Module capacity is floored0. A restrictive importer may reject the negative count. |
| Thrust at counts4/6/8 | Values4/8/14; these are not linear thrust-per-component. |
| Full human ship, P75, no Fusion | Mass295, flight156, support/energy/fuel100, success100, preview arrival calendar+15. |
| Same ship with Fusion | P56, flight117. Removing knowledge later does not clear the existing Fusion flag. |
| P151 without Fusion | Reduce to75 and scale2; flight312 for the full ship. Scaling before division would change rounding. |
| `[15,8,8,1,1,1]`, P75 | Raw mass127, supported engines4/3; flight307, preview arrival calendar+30, projected arrival calendar+6. |
| No Life support but sufficient energy, includeSupport0/1 | Support remains reported0 in both; omission can retain nonzero success, inclusion makes it0. |
| Preview calendar32767 plus15 years | Stored arrival=-32754; shared projection32782. Three calendar calls remain separate. |
| Already launched ship later learns Fusion | Recalculate scratch, but no new Fusion, population/year updates or projected-arrival overwrite. |
| Empty Component group with no structures, prerequisite known | groupCanBuild true for its first part; after a single unsupported engine, false. |
| Stored engines16/0 | groupFull true by sum; partMaxed false because Fuel is below its own maximum. |
| Structural group request with supported empty modules | Group selection prioritizes Module. Launched request returns-1 only after calculation. |
| AI equal engine counts / equal module counts | Choose Fuel / Habitation. Successful AI add normally returns3, irrespective of selected subtype. |
| Human chosen subtype at hard maximum | NoFurther and repeat. A callback making the whole group full returns retained selection without an increment. |
| Component raw reply-1 | Maps to backed Structural0; it is not automatically a cancel. A reply mapping outside all six parts requires separate read/write handling. |
| First ship owned by human1, human2 already has a ship | Reset human1 contact columns1…7, preserve column0 and human2 row. |
| AI full engines with success>=40 after adding Solar | Launch without a support-inclusive second calculation or separate human-style confirmation. |
| No human launch, Emperor+, other AI earlier than slot0 arrival | Suppress automatic launch using slot0's backed value. Do not compare against C by inferred intent. |
| AI launch callback changes its owner slot to human control | The negative window request still follows the initial AI decision; the second live membership test selects human diplomacy rather than city production resets. |
| Human launch ahead of AI, Emperor+, no forward Alliance | Clear reverse Ceasefire/Peace/HostileIntent; separately add reverse BrokenTreaty/Aggressor/AggressorLong. No full war call. |
| Destroyed Apollo raw location, no human Space Flight | raceActive true unless Bloodlust. A zero-progress tie with a human knowing Space Flight makes eligible AI raceBehind true. |
| Arrival exactly equals current year | Shared predicate true; text status report still prints its in-flight arrival-year variant. F04 handles the actual transition. |
| Launched without Exists, then loss | Returns notice, then reset. First-launch calendar marker and shared scratch survive reset. |
| Current ship/council window already active | General request returns1 without opening, so its caller does not take the text-report fallback. |

Also join actual C06 spending/reselection, F04 calendar and arrival ordering,
V04 cascades, V01/C09 loss, V08 score/endgame, graphical launch/video callbacks
and network receipt. Vary government-independent human/AI control, difficulty,
custom rules, admitted signed counts and callback changes. A supplied callback
result does not establish its production implementation.

## Appendix A. Reference encodings and excluded writes

These aid validation/import; they do not prescribe packed arrays, fixed strings
or native class layouts for a new implementation.

- Original ship flags are Exists1, Launched2, Arrived4, Fusion8, Won16. Counts,
  population and saved years are signed16. Ship flags are a byte; calculated
  scratch is signed32. FirstLaunchTurn and global turn/year are signed16.
- Part records at original `00634f60` are six triples of signed32 label identity,
  maximum and mass. Label identities198…203 correspond to the six part rows.
  The maxima/masses in section1 were checked directly against the executable.
  The mass/thrust rule is an unsigned byte; Fusion Power is advance32 and
  Space Flight76. Structural/Component/Module are improvement identities35/36/37;
  Apollo is wonder25, with raw Unbuilt=-1 and Destroyed=-2.
- The production reselection request is99. The group chooser uses signed-1 for
  already launched and-2 for the final unavailable group; addPart's initial-1
  has its own meaning. Stock dialog indices are described in section5.
- Launch's low-difficulty shared treaty mutation is Incident `0x10000`.
  Reverse HostileIntent is `0x20`; its high-difficulty clear mask is `0xffffffd9`,
  followed by OR `0x80840`. These correspond to the named flags in section6.
  F06/N02, not this algorithm, own serialized encodings and authority.
- The graphical display states are Building0, Ready1, LaunchRequested2 and
  Launched3. Its separate field named `launched` in the primary source also
  indicates typing completion; it is not the civilization's Launched bit.
  P07 must preserve this distinction when routing input.
- The original graphical formatter appends to a512-byte statistics region and
  a128-byte success region. Validly parsed custom labels can overflow them and
  corrupt later window fields. These writes are excluded bugs even though
  ordinary custom-resource input can reach them. Safe owned text or declared
  ingress rejection is permitted; reproducing the corrupted window is not.
- Independent BF-190 rejects oversized combined label expansions at resource
  ingress; the primary reconstruction composes separate temporaries and truncates
  to destination bounds. These are different repairs. Neither demonstrates that
  the original bounded its output. P07/P10 own final safe presentation policy.
- Original part-index arithmetic can address adjacent ship metadata after a
  nonstandard reply. A mapped out-of-range part write is still excluded; it is not
  a legitimate population/year mutation. Preserve any prior defined calculations
  and first-ship bookkeeping, then stop before an invalid write if the path has
  reached it. Invalid earlier reads need their own backing/stop boundary. A
  stricter accepted-reply domain may prevent the entire continuation instead.

## Appendix B. Evidence and validation

Primary source pin: `a71e8aefffe04e6f719e0e213b027b128a747711` in OpenCiv2,
inspected at checkout `fc2155ccdb2123c1137babab6fa39e38152278c9`. **37 complete
bodies** were compared with that pin:24 core/query/report bodies and13 selected
window-boundary bodies. The independently reconstructed source at
`civ2`, revision `4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`,
was consulted for numeric query order, cached inputs, subtype/result reuse,
slot0 comparison, launch membership reloads and text bounds. No source code or
original assets were imported into this repository.

Original executable: Civilization II MGE **5.4.0f, patch3**, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
The hash, clean reference trees and original part-table bytes were checked.
No fresh original-program replay, native Windows build, Ghidra instruction
investigation or annotation update was performed. Archived native comparisons
were not re-read or claimed as newly executed tests.

| Evidence | Scope |
| --- | --- |
| [spaceship.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/spaceship.cpp), `00596b00`…`00596eec` | Capacity, effective/raw group queries, thrust and ordered calculation. |
| Same file, launch `005973fd`, report `0059772c`, chooser `00597d6f`, addition `00598197` | State mutations, return reuse, acceptance, forecasts and automatic launch decisions. |
| Same file, selector `00598b4e`, race `00598ceb`, predicates `005998b0`/`00599910`/`005999c0` | Report list, raw race gate and distinct group predicates. |
| Same file, idle `004d0160`, snapshot `004d01ae`, wrappers `004d0208`/`004d0339`, init `004d0b58`, modal/present/sequence and input/hotspots | Request outcomes, readiness, launch mutation timing and presentation boundary. |
| [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp), reset `004a74bc`, launched `004a7577`, exists `004a75a6`, arrived `004a75d5`, loss `004a762d`, maxed `00598a05` | Shared lifecycle queries and production maxima. |
| [ai.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/ai.cpp), race-behind `00598d45` | AI admission, weighted raw progress and human comparison order. |
| [gamedata.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/gamedata.cpp), original `00634f60`; [civ2types.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h) | Part constants and retained fields; constants independently checked in the PE image. |
| [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp), arrival tail; civ score consumer | F04's arrival transition and V08 boundary; no added random arrival check. |
| Independent `src/civ_spaceship.cpp`, `src/civ_spaceship_addition.cpp`, `analysis/reference/spaceship-presentation.md`, bugfix log BF-190 | Retained count/query/math order, full addition/launch continuation and explicit unsafe-text repair. |

### Checks performed

A standalone source-derived C++ harness compiled **22 complete bodies**: all
core capacity/group/thrust/calculation/launch/choice/addition/race functions,
the six civilization query/reset/loss/maxed bodies and AI race-behind. The text
status and view-selector bodies and the13 window bodies were source-reviewed,
not executed by this harness. Both optimized and AddressSanitizer/
UndefinedBehaviorSanitizer builds passed **53 assertions**, with leak detection
disabled. Original packed relation/part/count-table arithmetic was adapted to
named host fields; the original signed-1 chooser sentinel was retained on the
64-bit host. Arithmetic used explicit signed-wrap compilation.

The tests exercise capacity thresholds, reversed clamps, AI effective-module
limits, thrust, stock/Fusion/scaled flight calculations, missing support,
unsupported mass and distinct estimates, signed year wrap, launched metadata
retention, build predicates, chooser priorities, part replies/ties/return values,
first-ship contact reset, automatic launch, launch diplomacy/membership callbacks,
race ties and loss/reset. No excluded overwrite was replayed as a required result.

V03 knowledge, F04 calendar, V04 treaty mutation, C06 production selection and
UI/network services were explicit adapters. These checks establish finite source
continuations, not actual calendar-era changes, treaty cascades, city spending,
full native dialog/video cleanup, packet receipt or original-runtime conformance.
Source-pin, Markdown link/anchor, specification IDs and whitespace checks
accompany the document. Complete joins and independently reproducible X02 traces
remain required for implementation validation.

## Appendix C. Original numeric annotations

These original annotations retain storage widths and reference-evaluation context.
They do not prescribe internal types or redundant casts. The main contract and
F01 determine logical ranges and behaviorally significant conversions; external
file/wire encodings remain normative in their owning mappings. Where an excerpt
includes an off-nominal conversion, it applies to that explicitly admitted profile,
not as a new requirement to accept unsupported inputs.

### Reference annotation 1

Use [F01](F01-determinism-and-arithmetic.md)'s signed32 retention, truncating
signed division, arithmetic right shifts and `clampAB(x,lo,hi)` (lower bound,
then upper bound; do not reorder reversed bounds). Every intermediate product,
sum, subtraction and shift retains its stated domain. Stored counts/population/
years retain signed16. Nominal mass/thrust paradigm is25…200, stock75; F03 owns
its ingestion. Nominal counts are nonnegative and no greater than their hard
maxima, although supported capacities can be lower than stored counts.

### Reference annotation 2

`[0,1,2,3,4,6,8,11,14]`. Negative n performs no iterations. Retain signed32
accumulation for any larger admitted count.

## Appendix D. Original query sequencing

The following sequence records original reference evaluation. X01's ordering
convention governs cleanroom conformance: retain the dependencies and failure
effects stated in the main contract; unobserved query/store order alone is not
an additional requirement. Historical source traces retain their evidence scope.

| Query | Original evaluation |
| --- | --- |
| groupLimit | Sum partLimit values. Query order for Component is2 then1; for Module3,4,5. |
| groupCountClamped | Sum partCount values in the same query order. |
| groupFull | `groupMax<=groupCount`; max query precedes raw-count query. |
| groupAtLimit | `groupLimit<=groupCount`; raw-count query precedes limit query. |
