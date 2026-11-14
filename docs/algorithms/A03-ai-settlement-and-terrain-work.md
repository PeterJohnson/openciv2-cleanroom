# A03 — AI settlement and terrain work

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in A03 |
| --- | --- |
| Gameplay | Settlement/terrain-work scores, tasks, city-site ties, worker roles and retained decisions across retries. |
| Original files/content | F03 terrain/work rules and F06 unit task/Goto state. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Parent local-variable layout, job-buffer representation and temporary cursor addresses. |

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

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. This specification owns local city-site choices, city
terrain-job publication, road-link suggestions, fortress-site classification,
settler work priorities, role-21 worker behavior and the crowded-region return
to a city. It also describes the human-automation differences in those paths.

[A01](A01-ai-strategy-and-assessment.md) owns assessment, effective role
conversion and shared current/standing goals. [C02](C02-city-tiles-and-worker-assignment.md)
produces tile-job suggestions during city calculation; [F04](F04-turn-sequencing.md)
owns the city-turn scheduling and resets around their consumption.
[W04](W04-terrain-work-and-improvements.md) owns actual work execution and its
irrigation-source predicate. [C01](C01-city-lifecycle-and-ownership.md) owns
founding and nearest/coastal queries. [U01](U01-unit-lifecycle-and-stacks.md)
owns deletion and stacks; [U04](U04-unit-orders-and-activation.md) owns order/goto
setters and active execution. [U03](U03-pathfinding-and-goto.md) owns path search.
[W01](W01-map-geometry-and-spatial-state.md) owns geometry, neighborhoods and
claimant/fertility queries; [W02](W02-map-generation.md#fertility) owns
the stored fertility calculation.
[F01](F01-determinism-and-arithmetic.md) and [F02](F02-game-state-and-invariants.md)
own arithmetic, identities and retained state. [A04](A04-ai-ground-tactics.md)
owns the shared unit chooser's damage/guard/trespass gates, ordinary movement scoring and common order finish;
A05–A07 own the transport, air and special-mission branches around these phases.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
semantic publications and failure effects. Only dependencies within the binding
scope above constrain evaluation; storage/API choices need no original-trace
equivalence proof. External file/wire definitions remain binding at adapters.

AI scores, candidate sets, deterministic ties and stateful decisions are unchanged.
Pure candidate work may be reordered only with identical decisions and required
scratch/publication effects. Mutating surveys, retained cursors, live bounds,
child calls and sampled-value reuse prevent assuming an entire chooser is pure.

## 1. Inputs, state and return boundaries

Nominal unit/city/rule identities and terrain/region values follow F02/F03/W01.
Custom rules are inputs, including irrigation/mining results, work times, AI
government thresholds, resource yields and leader expansion. Use F01 reference arithmetic,
truncation toward zero for division and arithmetic shifts. Local variables
need sufficient range; preserve any significant reduction before its consumer. City size and terrain work-time/threshold/shield
reads identified below use signed8 interpretation; regional city counts use
unsigned8, region fertile-tile counts signed16. A unit's auxiliary timer wraps
modulo 256 and is compared as signed8. No saturation is added except explicitly.

Keep actual unit type role separate from its captured effective AI role. A01
maps a Settle unit with AIColonist set to effective role 21, while its actual
role remains Settle. Define `settlerKind` as effective role=5 and `settlerType`
as the unit's rule role=Settle. These are initialized false at parent entry,
then assigned by the city-site phase. A path that skips that phase must retain
the parent-provided values; it must not recompute them conveniently later.

Unit-choice procedures use this context from the shared parent:

- Active unit u and captured civilization C; coordinates p=(x,y); raw region R;
  original terrain t; effective role r, domain and regional policy P.
- Nearest city N and owned nearest city H, their captured W01 weighted distances
  dn and dh, and their land-only regions Rn and Rh. Both searches use unrestricted
  region -1 and no alternate owner; N has no owner filter, H filters C. A missing
  city gives index -1 and distance 9999. Land-region capture is -1 when absent.
- Enemy-ZOC flag Z, forced-attack flag, fortress status, hold/guard state and
  goto-city flag. Parent captures fortress status from Fortress present with
  City absent. It clears AIColonist for AI units when dh>8 or R≠Rh, before
  calculating the effective role. The captured context can remain stale after
  a child changes the unit or world.

Fields not explicitly captured are read live at their stated operation.
Forward scans re-read the live city/unit bound and skip unused identities.
Use W01's eight-direction table D and ordered city-radius table B; B[0…19]
excludes the center. Every indicated coordinate wraps x once before the bounds
test. Rectangular bounds tests do not introduce a parity test for the wider
fortress samples.

Each unit phase can continue to the next phase, request a direction, finish
with current orders, or return 1 directly after a founding/absorption call.
The direction route applies D[direction] to the **parent's current p**, checks
bounds, and sets AI-Goto targeting the resulting coordinate; the out-of-bounds
route clears orders and exhausts moves. Direction 8 clears last direction and
orders. A04 owns that shared route and common finish; work-order assignment
here does not instantly execute W04's work or consume an invented random draw.
The selected A03 procedures themselves draw no randomness. Shared movement,
founding, path search and execution children retain their own contracts.

Invalid writes and memory corruption are excluded under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs).
Missing-city, invalid-region or missing retained-read backing means a supplied
read or a bounded stop after prior valid effects, not a fabricated city/zero.
A stricter implementation may reject a declared custom-input domain before
entry. Ordinary in-bounds scalar wrap remains observable in accepted cases.
For an excluded write, omit the invalid operation when its remaining valid
continuation is established, otherwise stop before it and retain prior valid
effects; do not emulate its corruption through a separate logical
field. Supplied read backing does not authorize an out-of-bounds write.

## 2. Tile-improvement recommendation

`improvementChoice(C,x,y)` returns 0 for no recommendation, 1 for irrigation,
2 for mining. It is used by this document and A02's free-irrigation survey.

1. Query city owner; a present city returns 0. Capture tile improvements.
   Mine present returns 0. Irrigation present requires Refrigeration, else 0.
2. Capture terrain. Consider irrigation first. Query W04's irrigation-source
   predicate; if false, **return 0 immediately**, even when mining would qualify.
3. If irrigation's result is not Disallowed (-1), its signed AI-government
   threshold is nonzero and that threshold≤C's live unsigned government, return 1.
4. Before considering mining, return 0 if the captured tile had Irrigation.
   Otherwise, if mining's result is not Disallowed, its signed threshold is
   nonzero and threshold≤live government, return 2. If not, return 0.

The threshold comparison uses the actual government, without C02's
below-Monarchy normalization. Negative nonzero thresholds are accepted and
compare normally. KeepTerrain (-2) and actual conversion results pass the
result gate. This function does not itself check enemy units, terrain work time,
unit abilities, food need, city radius or all W04 command eligibility.

## 3. City terrain-job publication

The city-turn parent clears jobFound and jobPriority before its second city
recalculation. C02's yield pass may then set/increment/replace the shared job
state. This procedure consumes it later; it does not recreate the suggestions
or clear the state at entry. Capture the city's owner C.

Scan B[0…19] around the city's live coordinates. For every valid polluted tile,
insert A01 current goal role 21, priority 6, then set the city's AITerraformGoal
flag. Finish the full scan, including multiple pollutants and repeated wrapped
coordinates. If any pollution was found, return without the nonpollution steps.
Goal capacity rejection does not suppress setting the city flag.

Otherwise:

1. Query Railroad. If known and (C is AI or jobFound=0), set jobPriority to 3
   when below 4, retaining greater values. Call the road-link finder below.
   If jobFound remains zero, return **without clearing AITerraformGoal**.
2. Add 2 to live jobPriority. If jobFound=0, replace priority with 2 and call
   the road-link finder.
3. If still no job, clear AITerraformGoal and return. Otherwise, when priority>2,
   increment it for size>4, then independently for Palace. Size<4 decrements it
   regardless of the previous priority gate.
4. Insert A01 current goal role 21 using live job coordinates and priority, then
   set AITerraformGoal. A01 owns priority narrowing, insertion ties and failure.

An AI with Railroad can replace a previously suggested tile with a road job.
A human with an existing suggestion skips that initial road search. A road
search returning 0 does not clear an existing jobFound, so the return value is
not interchangeable with the shared flag.

### Road-link finder

Capture the city's land-only region and owner C. Return 0 unless C's retained
city count in that region exceeds 1. Scan live cities ascending in two passes.
Pass 0 admits owned cities other than the source. Pass 1 admits owned cities
again, plus cities with directed Peace or Alliance C→owner. Ceasefire alone
does not qualify. There is no nearest-city sorting.

For each admitted city, capture target coordinates, reload source coordinates
and set steps=0. Require the target's land-only region to match and ordinary
step distance<23. Set U03 uniform/road-planning mode on, owner filter -1,
unit-type identity Warriors (2), and target coordinates. Starting at the source:

1. Ask U03 for next direction with allowance 99. Negative or 8 ends this path.
2. Apply D to the working coordinates. If now exactly at the target, end before
   inspecting its work needs.
3. At a non-city tile, missing Road is a job when there is no River or C knows
   Bridge Building. If Road exists, missing Railroad is a job when C knows
   Railroad. A river tile without Bridge Building does not stop this path;
   continue looking beyond it.
4. Increment steps, including the first job tile. Stop when steps>50 or a job
   has been found; otherwise repeat.

After an ordinary path exit, set road-planning mode to zero. Do not restore the
other shared path fields or earlier queue state. A bounded child failure before
this cleanup retains its preceding effects and the mode still set.

If a job was found, require combined city/unit occupant absent or C, then require
W01's peaceful foreign-radius owner absent or C. If admitted, set jobFound=1,
then jobX and jobY, and return 1. If rejected, abandon this candidate path;
do not resume beyond its first job to find a second usable tile. Continue the
city/pass scans. Exhaustion returns 0 while retaining prior job state and the
path service's effects. No job priority is written by the finder.

## 4. Local city-site choice

This phase assigns settlerKind and settlerType as above. Start best at W01's
claimant-or-fertility value of p. If dn<4, R=Rn and best>7, replace best with 8.
Ocean replaces it with zero. This is the **nearest city's** region test; Appendix
A distinguishes an independent reconstruction mismatch.

For settlerKind, first clear standing Settle goals at p with radius 0. If C has
no cities, turn=1, p exactly equals C's tribe-indexed starting coordinates and
best>7, call C01 founding on u with automatic name/input, then return 1 directly,
regardless of that child's result. Do not execute the shared order finish.

Set the shared city selector to N. If best≠0, (signed r<2 or settlerType), and
not forced attack, set selected direction absent and scan B indexes 0…19 for
settlerKind, otherwise 0…7. For each valid non-Ocean candidate in raw R:

- Query claimant-or-fertility; a **strictly greater** value replaces best and
  direction. Ties keep the earlier choice, including the current tile.
- Query hut. For signed r<2 and index<8, the first hut immediately sets task
  Hut and requests that direction, even if another tile had higher fertility.

After the scan, require best>7 and Z=0. Proceed if `15-dn<=best`; otherwise
proceed if settlerKind, no selected direction and C's regional city count=0.
Only if both preceding alternatives failed, for settlerKind with no selected
direction increment the retained auxiliary timer and proceed when its signed8
value>4. The timer is not incremented on the successful earlier alternatives.

If proceeding with settlerKind and no direction, found at p and return 1.
With a selected direction, set task FoundSite; index<8 immediately requests that
direction. An outer-radius index instead sets ordinary Goto with FoundSite task to the
selected coordinate, then **continues** this phase's tail. For a non-settlerKind
with selected index 0…7, add a standing Settle goal priority 2 at that tile;
there is no immediate movement request from this branch.

Finally, independently of whether that scan ran, settlerKind can found beside
a more advanced human: require difficulty≠Chieftain, N present and human-owned,
Z=0, dn>1, and C has fewer advances than N's live owner. Query **raw fertility**
at p, not claimant-or-fertility. Fertility>8 and `14-dn<=fertility` found at p
and return 1. This may supersede the outer-radius Goto just assigned. Otherwise
continue to the parent's following phase with all changes retained. The shared
city selector remains N on every continuing path; the optional later assignment
also uses N. [A07's away paratrooper choice](A07-ai-special-unit-missions.md#away-from-the-captured-own-city)
consumes this produced value for its home destination.

## 5. Fortress-site classification

Set fortressNeed=0. Continue only for settlerType or effective Defend, and C
knows Construction. Start ordinary home distance at dh; if H exists, replace it
with W01 step distance from p to H's live coordinates. Require dh≠0, H=N,
captured fortress status false, no airbase at p and non-Ocean p.

Initialize oceanCount and four directional markers north/south/west/east to
zero. Scan D[0…7] neighbors. For each valid neighbor:

1. Fortress-without-City, or a city-owner query reporting present, immediately
   returns with fortressNeed=0.
2. If Ocean, increment oceanCount; the fourth Ocean immediately returns. Set
   north/south to 1 according to the sign of that direction's dy, and west/east
   to 1 according to dx. A diagonal can set two markers.
3. A nonzero A01 current Defend-goal priority at the neighbor returns immediately.
   This includes negative priorities; no positive-only filter is added.

If p has Road and 1<dh<4 and H=N, set fortressNeed=1. Then scan offsets -2…2
ascending across the north line y-2 and south line y+2, north before south at
each offset. If valid and that direction's marker is still zero, Ocean or
Fortress-without-City changes it to 2. Next scan offsets -2…2 down the west
line x-2 and east line x+2, west before east. Wrap each x as indicated by W01.
Markers already 1 are not upgraded to 2 by these outer scans.

Set fortressNeed=2 if north+south>1 and (west=0 or east=0). Independently set
it to 2 if west+east>1 and (north=0 or south=0). This is the recovered four-line
test, not a generic choke-point/path-connectivity calculation.

When need is nonzero, a settlerType posts a standing Defend goal priority 2 at p.
A non-settlerType posts standing TerrainWork role 21 with priority
`fortressNeed + (ordinaryHomeDistance<4 ? 1 : 0)`. Classification itself does
not build a fortress or replace the actor's orders. Keep the calculated need
for subsequent worker and defender decisions.

## 6. Settler work choices

The parent invokes this phase only when settlerType, not forced attack, no
goto-city flag, non-Ocean p and no city at p. Before it, if settlerKind has Goto
and its destination city-owner query is present, the parent sets goto-city.
That query has no independent destination-bounds check here. A role-21 worker
can enter this phase too because its actual role is still Settle.

Count the actor's stack. If at most one unit, scan D[0…7] for a valid neighbor
with any non-absent unit-owner cache. The first such neighbor ends the scan;
ownership and hostility are not checked. If no companion or neighbor exists,
post current Defend goal priority 2 at p. Clear standing TerrainWork goals at p
with radius 0. These effects happen before deciding which work to perform.

Pollution at p assigns CleanPollution, then the local-cleanup task, and finishes
current orders immediately. Otherwise a human actor with valid Goto destination
containing Pollution preserves that Goto by skipping all remaining choices in
this phase. It continues to the parent's later phases; do not return a fabricated
successful work action.

For the remaining path:

1. If fortressNeed=2 and C is AI, and either no adjacent nonallied unit or stack
   count>1, assign BuildFortress then the fortress task and finish.
2. Start `goalWork=false`. Set it when settlerKind and GoalReached; otherwise
   query A01 current TerrainWork priority at p and set it if nonzero.
3. If dh≤2, H's live AISettlerNear flag is set, settlerKind and current order is
   Goto, skip the remaining choices in this phase. The H flag read precedes
   the settlerKind/order tests; unbacked H is not silently ignored.
4. Start `localWork=false`. Require 0<dh<3 and either
   `3*(signedIrrigationTurns[t]/5)-3 <= H.size` or a resource at p. Then call
   improvementChoice. A nonzero result or P=CapacityPressure sets localWork.
   These time/size tests use irrigation turns even when the desired job is mining.
5. If localWork, call improvementChoice again using fresh inputs. Result 1
   assigns Irrigate then the local-irrigation task and finishes; result 2 assigns
   Mine then the local-mining task and finishes. The repeated query is not
   replaced with the preceding answer.

If either goalWork or localWork was true and no irrigation/mining was selected,
capture current raw features:

- With Road and no Railroad, require Railroad advance. Discard localWork and
  set it anew if a resource exists, Mine is present, or the original terrain's
  signed shield yield>1, in that order. If goalWork or this new localWork,
  assign BuildRoad then the railroad task and finish.
- Otherwise, only if Road is absent, require no River or Bridge Building.
  Discard localWork and set it anew when Irrigation or Mine is present and
  (River or original terrain identity<3). If goalWork or this new localWork,
  assign BuildRoad then the road task and finish.

After that work block, if fortressNeed≠0 and C is AI and (no adjacent nonallied
unit or stack>1), assign BuildFortress then fortress task and finish. This second
fortress opportunity is below irrigation/road priorities; need=2 had an earlier
opportunity above them.

Set the shared city selector to H. If order is Goto and AIColonist is clear,
compute `capacity=(expansionTrait+1)*regionFertileTiles` with signed32 wrap.
When `16*regionalCities < capacity`, finish while retaining Goto. The original
tests nonzero difference plus a signed comparison; equality does not retain it
through this branch.

If dh<2, scan eight directions rotated by the current turn:
`direction=(turn+index)&7`, index 0…7. For each valid non-city neighbor, require
no unit or a first unit owned by C whose stack contains no Settle-role unit.
Require improvementChoice nonzero, then query its fresh terrain. Require
`3*(signedIrrigationTurns/5)-3 <= H.size` or resource present. Query W01 weighted
distance from p to this neighbor and require <3. The first admitted neighbor
sets the move-to-work task and requests its rotated direction. It does not rank
all candidates, check that neighbor's raw region, or set the job's work order.

Last fallback: missing Road, (no River or Bridge Building), dh<3 and original
terrain identity<3 sets the road task **before** assigning BuildRoad, then
finishes. Otherwise continue. The original terrain test refers to fixed Desert,
Plains and Grassland slots even when those rule rows have customized names.

## 7. Role-21 worker choices

After the preceding settler phase, the parent invokes this phase when effective
role=21, current order is not Goto, no forced attack, no goto-city flag, dh≠0
and p is non-Ocean. Thus these workers first get the earlier settler-work
opportunities. The phase is not selected merely because a unit can transform
terrain; there is no generic Transform action added here.

Choose reference city from retained home, or H when home is Absent. A present
home is the unsigned city index, including 128…254. If its live raw region
differs from R, use H instead. If the resulting city is present and weighted
distance>4, set ordinary Goto with ReturnHome task to its live coordinates and
finish.

Next, missing Road at p and (no River or Bridge Building) sets road task then
BuildRoad and finishes. This road decision precedes retreat from enemy ZOC.
If H exists and Z≠0, set ReturnHome Goto to H and finish.

When dh<3, capture raw features at p:

1. If none of City, Irrigation and Mine is present, read signed terrain AI
   thresholds. Nonzero mining threshold strictly below irrigation threshold
   assigns Mine and finishes. Otherwise nonzero irrigation threshold assigns
   Irrigate and finishes. Neither branch changes the current task field.
2. Pollution assigns CleanPollution and finishes, also retaining task.
3. Missing Railroad and known Railroad sets road task then BuildRoad and finishes.
   There is no fresh Road requirement at this step.
4. Set the shared city selector to H and scan all B[0…19] around H's live
   coordinates. At each valid tile, query terrain then raw features. With no
   City/Irrigation/Mine, the same threshold comparison selects the first Mine
   destination, otherwise first Irrigate destination, using ordinary Goto and
   their distinct destination tasks. If neither chosen, require no River or
   Bridge Building, then missing Road or (missing Railroad and known Railroad).
   The first such tile gets road-destination Goto. Finish immediately on any
   choice. No ownership, unit-presence, Ocean, irrigation-source or size test
   is added to this radius scan.

When dh≥3 instead, missing Railroad and known Railroad sets road task then
BuildRoad at p and finishes. Otherwise continue to the parent.

The threshold-only mining/irrigation branches compare the two rule thresholds
to **each other**, not to government. They do not call improvementChoice, check
conversion-result permission or require an irrigation source. W04 later owns
the actual work outcome. Automatically applying the ordinary helper here would
change which job/order the AI chooses.

## 8. Parent continuation, travel and population return

The city-site phase runs after admitted air/nuclear/diplomat/caravan decisions
and before paratrooper/partisan-fortress and guard decisions. It can be skipped
by damage, scenario, ferry-port or forced parent routing. Fortress classification
occurs later in the no-hold/no-guard transport/work loop after pillage/partisan
choices. Then come the goto-city test, settler work and role-21 work in that
order. A phase's Continue result preserves orders and its shared scratch fields;
it does not restart the original context capture.

Next, [A04's retained-Goto decision](A04-ai-ground-tactics.md#retained-goto-handling)
can finish the orders, cancel a forbidden foreign destination, or retry the transport/work loop. In particular,
an ordinary Settle/TerrainWork Goto at distance>1 is not canceled merely because
an enemy is adjacent: that local cancel test is for signed effective role<5.
At distance≤1, a foreign destination under Ceasefire, Peace or Alliance can
clear Goto. The Trade exception and transport retry remain owned by A04/A05.
Do not implement a separate settler pathfinder or consume a new draw here.

After that continuation and A04's defender-fortify decision, the parent checks
population return for settlerKind. Require
`(regionFertileTiles>>3) < regionalCities` and a non-Absent home. If dh=0 and
H's live size<10, increment that city's retained size, call U01 destroy/redraw
for u with synchronization requested, refresh the city window if displaying H,
redraw the map area at captured p, and return 1. There is no human join-city
confirmation or general C01 join eligibility check on this branch.

Otherwise, if H exists, R=Rh and its size<10, set ReturnForPopulation Goto to
H's live coordinates and finish. Do not require H to equal the home-city byte.
If neither path applies, the parent proceeds to shared ordinary movement scoring
and order finish in A04. That scoring includes its own settler exploration
terms and random consumption; those are not duplicated here.

Assigning a work order does not reset work progress, replace home, or clear
GoalReached unless the invoked setter/executor explicitly does so. The common
finish normally leaves work orders intact. A Goto already at the actor's current
coordinates sets GoalReached, clears orders and, when movement was already
spent, exhausts a non-NavalSuperiority unit. U04 performs the eventual work/goto
execution at its own boundary.

## 9. Conformance cases


> Compare the gameplay/interchange observations in the
> [production scope](#production-contract-scope). Original memory/helper traces,
> arbitrary native callbacks and unsafe backing-byte cases in historical lists
> are optional reference, not additional production acceptance criteria.

These finite cases exercise the selected source procedures with controlled
child answers. Unless overridden: actor C=1 at (20,20), effective Settle and
actual Settle; nearest/owned city H=N=0 at (24,20), owner 1, size 5; captured
dn=dh=3, R=Rn=Rh=1; no force/ZOC/fortress; one city in region 1. Map geometry
and B/D order follow W01 on a 100×100 cylindrical map. Unmentioned tiles are
Grassland in region 1, without features, occupants, huts or fertility. Advances,
goals and job state start empty; government is Monarchy, difficulty Prince,
turn 10, unit order None, home Absent. These are supplied phase contexts, not
claims that every captured distance was freshly recomputed from this fixture.

Irrigation-source answers are explicitly supplied. Rule results default to
KeepTerrain with both recommendation thresholds zero. A “viable” tile gives
both thresholds 1 and a true source answer. Founding records its arguments and
returns 0 without changing the map, making the parent's constant return visible.
Path cases supply their direction sequence; they do not exercise U03's search.

| Case and overrides | Expected behavior |
| --- | --- |
| Tile recommendation: viable but irrigation-source false | Return 0, even though mining's result/threshold would qualify. |
| Irrigation threshold 5, mining threshold 1, source true | Monarchy fails the irrigation comparison; return 2 for mining. |
| Existing Irrigation, viable | Return 0 without Refrigeration; with it, return 1. Existing Mine returns 0. |
| City at (20,20); pollution at B0 and B1; retained job priority 7 | Publish both TerrainWork goals in that order at priority 6; priority remains 7 and city flag is set. |
| Railroad known, no job and regional city count 1; city flag already set | Priority becomes 3; no goal; flag remains set on the early return. |
| Retained job priority 3, size 5 and Palace | Publish priority 7: add 2, then size and Palace. A size-3 job starting at priority 0 instead publishes 1. |
| Road source at (20,20), owned target (26,20), regional count 2; directions 1,1,1 | First job is (22,20); stop after one direction and clear road mode, retaining the other path fields. |
| Same road case, first job occupied by foreign C=2 | Reject that path; revisit the owned target in pass 1 and reject the same tile. Do not continue to (24,20). |
| Same road case, first tile River without Bridge Building | Continue beyond it and choose (24,20). A target at (22,20) itself is never chosen as the job. |
| No candidate job; retained jobFound and coordinates already supplied | Return 0 without clearing them. A child path stop instead leaves road mode on at its failure boundary. |
| Current fertility 12, force=true, dn=3 | Rn=1 caps the starting best at 8 even with Rh=2. Rn=2 leaves best 12 even with Rh=1. |
| Current fertility 12, B0=B1=13 | Select B0 strictly before the tie; return a direction request with FoundSite task. |
| Actual/effective Attack, current fertility 12, hut at B0 | Immediately request B0 with Hut task. |
| Current fertility 8, regional city count 1, timer 4 | Increment to 5, call founding and return 1 despite its supplied return 0. |
| No cities, turn 1, current tile matches tribe start, fertility 9 | Clear standing Settle goals before founding; return 1 directly. |
| dn=4, current fertility 11, B8 fertility 15; N is a more advanced human city | Assign outer-radius FoundSite Goto, then call founding at the current tile and return 1. The later human-neighbor rule is independent. |
| Construction, dh=2, current Road, no coastal/goal obstacles | Classify need 1 and post standing Defend priority 2. A nonzero negative neighbor Defend priority suppresses it. |
| Construction; outer tile (18,18) Ocean, otherwise default terrain | Four-line scan classifies need 2; publish standing Defend priority 2. |
| Current Pollution, no companions/neighbors | Publish current Defend, clear standing TerrainWork, then assign cleanup/task and finish. |
| Human actor, dh=2, viable current tile, Goto polluted destination | Keep Goto and return Continue from settler work without choosing local irrigation. |
| AI actor, dh=2, viable current tile | Need 2 selects Fortress before irrigation; need 1 selects Irrigate before the weaker fortress opportunity. Irrigation queries the helper twice. |
| Current GoalReached, Road, Railroad known, no resource/yield benefit | Goal authorization alone selects railroad work. |
| dh=1, turn=2, Mine on current tile, viable neighbors | Rotated scan first chooses direction 2 with MoveToWork. |
| Role-21 phase, valid home city 200 at (40,20), same region | Use unsigned city identity 200 and ReturnHome Goto, before other work. |
| Role-21 phase, Z≠0, reference city nearby, no Road | Select current Road work before enemy-ZOC retreat. |
| Role-21 phase, dh=2, Road; mining/irrigation thresholds 5/6, mining Disallowed, no irrigation source | Select Mine while retaining task. This branch does not apply the ordinary helper's government/result/source gates. |
| Role-21 phase, dh=2, current Irrigation+Road; first radius tile is Ocean with irrigation threshold 1 | Select that tile's IrrigationDestination Goto. No extra Ocean filter is introduced. |
| Settle Goto at distance>1 beside an enemy | Common continuation retains Goto; a distance≤1 foreign destination under Peace instead cancels it. |
| Goto target equals current position and moves were spent | Common finish sets GoalReached, clears orders and exhausts moves. |
| Crowded region, fertile count 0, home=7, dh=0, H size 9 | H becomes size 10; destroy actor, refresh H then redraw, returning 1. Home identity need not equal H. With dh=3 instead, use ReturnForPopulation Goto to H. |

Accepted off-nominal cases remain separate: a stored threshold of 255 reads as
−1 and can recommend irrigation under government 0; founding timer 127 becomes
128, reads as −128 and does not found. A stricter rules/state loader may
exclude those inputs. Valid home identities 128…254 in the nominal case above
remain ordinary city identities despite the original signed storage type. They involve valid
field/table storage, not an out-of-bounds overwrite.

## Appendix A. Evidence mappings and compatibility differences

Primary source pin is `a71e8aefffe04e6f719e0e213b027b128a747711`, inspected at
`fc2155ccdb2123c1137babab6fa39e38152278c9`. Independent reference is
`civ2` revision `4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`.
The target executable SHA-256 is
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.

### Nearest-region disagreement

The independent reconstruction compares the city-site value against the owned
nearest region. The original uses the unrestricted nearest city's land region,
as in the primary source:

- `00538c45` stores the land-region query for unrestricted nearest city at
  EBP-0x100. The owned-city land region is separately stored at EBP-0x54 by
  `00538cb1`; the actor's raw region goes to EBP-0xfc at `00538ce7`.
- `0053a986` compares nearest distance at EBP-0x174 to 3.
- `0053a993/999` compares EBP-0x100 with EBP-0xfc before capping the score at 8.

Fresh bounded executable disassembly establishes that dataflow; it does not
establish a full original gameplay replay. Cases where N and H are in different
regions must retain the distinction rather than following the independent name.

### Task and state encodings

The following map logical tasks to the retained reference AI-task byte. They
are not required character-based storage in a new implementation.

| Logical task | Reference value |
| --- | --- |
| Hut | `'U'` = 85 |
| FoundSite | `'2'` = 50 |
| LocalCleanup | `'p'` = 112 |
| FortressWork | `'X'` = 88 |
| LocalIrrigation / LocalMine | `'I'` = 73 / `'M'` = 77 |
| RoadWork / RailroadWork | `'r'` = 114 / `'R'` = 82 |
| MoveToWork | `'s'` = 115 |
| MineDestination / IrrigationDestination / RoadDestination | `'m'` = 109 / `'i'` = 105 / `'r'` = 114 |
| ReturnHome / ReturnForPopulation | 99 / `'B'` = 66 |

TerrainWork effective role is 21; Settle is 5, Defend 1 and Attack 0. GoalReached
is unit flag 0x80, AIColonist 0x200; city AISettlerNear is 0x40 and
AITerraformGoal 0x80000. The auxiliary founding timer is unit offset 0x0e,
distinct from the other unit counter at 0x0d. City/Irrigation/Mine exclusion
tests feature mask 0x0e. Fortress without City tests `(features&0x42)==0x40`;
an Airbase is not treated as that fortress state.

The original wider fortress-line scans include rectangular coordinates of both
parities. Do not silently omit an odd-parity sample; W01 defines its map access.
Missing H can still reach a direct city read on retained/custom contexts. Supply
backing for the read or stop after prior valid goal/flag mutations. No fallback
city or zero-size record is established by the source. Invalid goal, path-table
or city writes are excluded bugs; deliberate in-bounds timer/priority/size wrap
is a separate compatibility obligation.

## Appendix B. Evidence and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

| Contract | Primary source at the pinned revision |
| --- | --- |
| Road-link finder and recommendation | [ai.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/ai.cpp): `ai_find_road_link_tile`, original `004f03b7`; `ai_tile_improve_choice`, `005b68f6`. |
| City job publication | [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp): `city_add_terraform_goals`, original `004f080d`. |
| Local AI phases | Same AI source: `ai_orders_city_site`, `ai_orders_fortress_site`, `ai_orders_settler_work`, `ai_orders_terraformer`, extracted from `ai_unit_choose_orders` at original `00538a29`. Parent city-return block is at `0054137e`…`005414d7`; shared retained-Goto and finish remain A04's responsibility. |
| Captured state and types | [civ2types.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h), [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h); W01 owns the ordered map tables. |

Independent `src/map_updates.cpp`, `src/city_turn.cpp` and selected branches of
`src/ai_unit.cpp` corroborate the recommendation, road search, publication,
work/site decisions and population return. The nearest-region expression is
resolved by the bounded original instruction check in Appendix A. Reference
revisions were clean, the executable hash matched, and all nine selected bodies,
twelve record definitions and the enum header matched the primary source pin.

A temporary Linux harness compiled **nine complete bodies**: seven A03-owned
procedures, plus A04's retained-Goto and finish helpers. It also compiled the
parent's selected population-return block with the two exits mapped to its
explicit phase-result enum. The other parent gates and direction route were
reviewed, not executed by that fragment. The harness passed **42 assertions**
in an optimized build and under AddressSanitizer/UndefinedBehaviorSanitizer,
using signed char, wrapping arithmetic and disabled strict-alias optimization.

Adapters supplied map/query state, irrigation sources, city facilities,
advances, goal publication/lookup, stack answers, path directions, goto/founding,
delete/redraw and city-window callbacks. A value-field context replaced the
source's reference-field context without changing the selected body text.
Neighborhood tables and distances used W01's nominal definitions. Goal inserts
were recorded rather than running A01 capacity/priority resolution. Founding
and deletion did not run C01/U01's complete continuations. No default answer
was supplied to an unbacked reference read in the selected cases. The bounded
path failure verifies partial effects, not arbitrary pathfinder failures.

These checks do not establish full original-executable replay, actual C02→F04→
A03 job production, A01 goal assignment, U03 route search, W04 execution, the
complete A04 parent or consecutive human/AI turns. No RNG-consuming child body
was executed; “no A03 draw” is the selected source contract, not a claim that
founding, route selection or eventual orders consume no randomness. X02 must
join those real children, retain full state/trace comparisons, and stop before
excluded writes. Unknown read backing remains a separate input/failure boundary.
