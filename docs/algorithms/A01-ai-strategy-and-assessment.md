# A01 — AI strategy and assessment

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. This specification owns shared goal storage/refresh, power
ranking and its coordination effects, civilization assessment and regional policy,
assignment of units to shared goals, and the outer AI movement passes. These
operations also run for human civilizations where specified; assessment is not a
pure query or a general-purpose state repair.

[F04](F04-turn-sequencing.md) owns session timing: power publication before global
turn advancement, assessment after city processing/diplomacy, and movement after
upkeep. [F01](F01-determinism-and-arithmetic.md) owns arithmetic/sampling laws;
[F02](F02-game-state-and-invariants.md) owns identity, counts and retained state.
[W01](W01-map-geometry-and-spatial-state.md) supplies map bounds, weighted/step
distances and land/raw-region queries. [U01](U01-unit-lifecycle-and-stacks.md)
owns unit relocation/deletion and stacks; [U04](U04-unit-orders-and-activation.md)
owns orders, movement eligibility, sleep/goto setters and active-order execution.
[C05](C05-city-growth-and-support.md) supplies support/shield factors;
[C06](C06-city-production-and-purchasing.md) owns production reset effects;
[C08](C08-buildings-and-wonders.md) owns active wonder queries.
[V01](V01-civilization-lifecycle-and-civil-war.md),
[V03](V03-research-and-technology.md), [V04](V04-diplomacy-and-treaties.md),
[V07](V07-spaceship.md) and [V08](V08-scoring-victory-and-retirement.md) supply
elimination, knowledge, hostile/attitude queries, ship-race queries and objective
value respectively. [A04](A04-ai-ground-tactics.md) owns shared unit-choice
routing and ground tactics. [A05](A05-ai-naval-and-transport.md) owns transport
and ship-goal choices; [A06](A06-ai-air-and-nuclear.md) owns air/nuclear selection.
A02/A03/A07/A08 own specialized production, settlement, mission and
government/diplomacy choices. N01–N05 own the real
network services invoked at the boundaries below.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

AI scores, candidate sets, deterministic ties and stateful decisions are unchanged.
Pure candidate work may be reordered only with identical decisions and required
scratch/publication effects. Mutating surveys, retained cursors, live bounds,
child calls and sampled-value reuse prevent assuming an entire chooser is pure.

## 1. State, inputs and arithmetic

Nominal actors are civilization slots 0…7; slot 0 is barbarians. Rules and
scenario inputs follow F03, and active records/regions follow F02/W01. Unit and
city scan bounds are retained live counts, not lists collected at entry. Forward
scans re-read their bound each iteration; reverse scans capture their initial
upper bound unless explicitly restarted. Skip unused records by their token.
Network polling can change later records and conditions. Unless a value is
explicitly captured below, observe it at the stated operation after prior calls.

Each civilization retains:

- Current goals: 48 ordered entries; standing goals: 16 entries. Each has x, y,
  role and priority. Free role, nonpositive priority and expired priority are
  different conditions. Do not compact, merge duplicates or sort automatically.
- Active and in-production unit counts by 62 rule slots; military, city and ship
  counts; total city size, attack and military-strength summaries; four groups
  of seven wonder counts; a shared total-unit count.
- For each of 64 regions: military count M, attack sum A, city count C, city-size
  sum, trade-unit count, presence flags and policy. Other civilizations' summaries
  are inputs from their most recent producers, including stale values.

Preserve F02's published value transitions: unit-type and region city/size/
trade counts wrap modulo 256; region M/A and military strength modulo 65536;
city/military/ship counts, city-size sum and total attack retain signed16 values.
Goal role/priority retain signed8 values, coordinates signed16. In this document
M and A are read as nonnegative modulo-65536 quantities unless explicitly stated;
unit rule attack/defense/move/cost/role and city size use signed8 interpretation.
F01 defines reference expression results, truncation toward zero and arithmetic
right shifts. Preserve multiplication's reduction when it affects the next
operation; equivalent sufficient-range evaluation is permitted.
No saturation is added except at explicit clamps.

There are four regional policies produced here: Attack (0), Defend (1),
CapacityPressure (4) and Expand (5). The first two names describe this classifier,
not a promise about every unit's subsequent behavior. Presence flags distinguish
foreign cities and military with or without Peace, plus StrongerEnemy. Alliance
suppresses those foreign-presence flags; Ceasefire alone does not mean Peace.

Nominal goal priorities are small positive values posted by their producers,
then negative values during refresh. Accepted custom values retain all signed8
behavior, including -128 and zero. A stricter implementation may declare a
narrower input domain. Invalid table writes and adjacent-field corruption are
excluded under [X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs).
Reads outside recovered inputs remain backing/stop boundaries; do not invent
zero values or infer that a mapped destination makes an invalid write legitimate.

## 2. Shared goals and small queries

### Current-goal insertion

`addGoal(C,x,y,role,p)` first converts the compared role/priority to their retained
signed8 interpretations, while x/y comparisons use the full incoming values.
Scan all 48 current entries ascending. If coordinates and role match and an
existing signed priority is at least p, return immediately. The free marker does
not independently exclude a matching entry from this scan.

If no such match exists, perform this immediate redirection **before** looking
for insertion capacity: when C is the current turn civilization, C is not human,
and incoming full role is NavalSuperiority or AirSuperiority, scan units ascending.
For each live owned unit, query awake-with-moves, then require its signed rule
role to equal the incoming full role. Query its raw region, then the target raw
region; if equal, obtain weighted distance from target to unit, then moves left.
When `2*distance <= movesLeft`, set ordinary Goto, then retained target x and y.
This direct store does not set an AI task or invoke the AI goto setter.

Scan current slots again for the first free role or stored priority strictly less
than p. If none, return; the redirections remain. Make a gap by following occupied
entries toward the first free slot or last slot, then copying whole entries
backward toward the insertion point. Stop at the first free entry; do not shift
the unrelated tail beyond that hole. A full list discards its last entry.
Store x, y, role, priority in that order, with their retained conversions.

A higher-priority matching goal is inserted as another entry: the older matching
goal is not removed or updated. Equal priorities preserve existing order. An
out-of-range incoming coordinate can fail equality with its own retained form,
then narrow when inserted; do not normalize before the duplicate test.

### Standing goals and refresh

`addStandingGoal` has the same full-coordinate duplicate suppression and signed
priority admission over 16 slots, but no unit-redirection pass. Its gap operation
always shifts slots 14 down through the insertion point one place right, even
across free slots. Store the new entry afterward; discard the old final slot.

`negateGoalPriority` sets the retained priority to `i8(-abs(i8(priority)))`.
Zero remains zero; -128 remains -128. `refreshGoals(C)`:

1. Scan all current goals ascending. If its incoming priority is negative, mark
   its role Free. Then negate its priority regardless of role or sign. Leave
   coordinates unchanged; do not clear a freed goal's priority.
2. Scan standing goals ascending. For every nonnegative signed role, call addGoal
   with its current coordinates/role/priority. Its normal redirection/insertion
   effects apply, including effects of earlier insertions.

Thus a fresh positive current goal survives one refresh as a negative-priority
entry, then becomes free at the next refresh unless another producer reissues it.
A zero-priority occupied goal does not expire through this sign test.
`resetStandingGoals` marks every standing role Free and priority zero, retaining
coordinates. `clearStandingNear(C,role,x,y,limit)` scans all standing goals of the
retained role, measures weighted distance from the input position, and clears
role/priority when distance<=limit. It does not require a positive priority.

`goalPriorityAt` scans all 48 matching coordinate/role entries. Start result=0;
for each, take full-width magnitude of its signed priority. If `i8(result)` is
at most that magnitude, replace result with the magnitude. Return full result.
This is not always a maximum: a magnitude 128 makes the next comparison use
-128, so a later matching magnitude 3 replaces it with 3.

### Role, defense quota and city-region contact

`unitRole(unit)` returns the signed rule role, except a Settle unit marked
AIColonist returns FoundCity. It does not inspect current orders or home city.

`defenseQuota(C,region)` takes the unsigned stored city count. Charge the first
five cities 3 each, the next five 2 each, the next five 1 each, then stop. Counts
beyond 15 add nothing. Results for 0/5/10/15/255 cities are 0/15/25/30/30.

`cityTouchesRegion(city,L)` captures city x/y, queries its raw region and returns
true if it equals L. Otherwise visit W01's directions 0…7 in order: wrap x, form
y, check bounds, then require Ocean. Query that water tile's raw region and test
W01 `borders(L,water)`. Return true on success. After an admitted ocean neighbor
that does not match, skip additional directions: the next loop direction is
`d + 3 - (d modulo 2)`. Non-ocean/invalid neighbors advance by one. This is a
specific sparse coastal test, not an exhaustive adjacency/path query.

## 3. Power ranking and coordination

### Values, history and ranks

`publishPower()` scans normal civilizations 1…7 ascending. For each:

1. If not alive, its local power is zero.
2. Otherwise, outside Scenario+objective scoring, compute
   `3*advanceCount + 8*totalCitySize + (gold arithmetic-shift-right 5)`.
   The original also scans all rule slots and computes a military subtotal that
   is **never added** to this value or otherwise consumed. Appendix A records
   that discarded calculation; do not improve the rating by adding army strength.
3. Otherwise, start power zero and scan live cities owned by this civilization,
   adding twice V08's objectiveValue for each. Do not recalculate city state.
4. Re-read Scenario/objective mode after these children. In ordinary mode, if
   turn<600, write `clampAB(power>>3,0,255)` to history sample `(turn>>2) rem 150`.
   In objective mode, if turn<75, write `clampAB(power,0,255)` at `(turn>>1) rem 150`.
   The column is this civilization. There is no lower-turn guard in the original;
   a resulting negative history index would write outside its table and is an
   excluded bug. A safe implementation can omit that history write or reject the
   invalid turn, while retaining declared preceding effects.

Repeat seven selections. Initialize best=-1 and selected=0; scan candidate slots
1…7 and choose the first power strictly above best. Publish rank 7 down through 1
to selected, then replace its local power with -1. Equal powers prefer lower
civilization IDs. Dead zero-power civilizations participate in this selection.
Negative custom powers can leave selected=0 and leave some normal ranks stale;
slot 0 itself is backed, so that store is not an out-of-bounds write.

Clear the global GangUp flag and all eight inverse-rank slots. For alive normal
civilizations ascending, set inverseRank[storedRank]=civilization. Retain overwrite
order if backed ranks repeat; reject/omit an out-of-range inverse write as an
excluded bug. Initialize bestHumanRank and worstHumanRank to zero. Scan inverse
ranks 7…0 for the first human membership, then 0…7 for the first, publishing the
rank numbers. Empty inverse entries are slot 0 and still participate in the
membership test; do not silently discard them or substitute civilization IDs.

### Choosing a coordination target

Set target=inverseRank[7], alert=false. Set alert when target is human, has more
than four stored cities, has no active Nuclear Missile slot units, turn>200 and
difficulty is above Chieftain. No fresh unit/city census is performed here.

At Emperor/Deity, additionally scan civilizations 1…7 ascending. For each human
member, call V07 Launched; every true result replaces target and sets alert.
There is no Alive gate; the last qualifying launched human wins. If alert remains
false, return. Otherwise set GangUp and count target's outgoing War flags toward
slots 1…7, without liveness filtering. Call that count warCount.

Scan civilizations A=1…7 ascending. Require alive, AI control and more than one
stored city. Admit its response if any of these short-circuit conditions holds:
`3*warCount+3 < target.reputation`, A→target Vendetta, or V07 raceActive().
Then inspect A→target:

- Alliance: query A's own Launched flag; if true and difficulty>King, set
  HostileIntent in A→target.
- Otherwise Ceasefire or Peace: consume one F01 primary draw and test
  `draw rem 32 <= difficulty`. Only if this fails, query A's own Launched flag.
  If either passes, set HostileIntent.
- Otherwise call V04 adjustAttitude(A,target,+1), then set Contact in A→target.

These are direct one-sided flag writes after the indicated child. Do not call
war declaration, clear peace, notify a player or make them bilateral. Preserve
the conditional draw even if A's launched status would also pass.

## 4. Civilization assessment: ordered passes

`assess(C)` initializes local settlersTotal=0, then queries knowledge of the fixed
Paratroopers rule slot's prerequisite and performs the staging prepass below.
It does so before clearing the old active-unit counts. Next execute sections
4.2 through 4.5, regional classification, reverse unit assignment, city ferry-flag
aging, then refreshGoals, in that order. There is no final recount after unit
upgrades, deletion or elimination; those effects can leave summaries stale.

### 4.1 Paratrooper staging

Require the prerequisite query true, old active count of the Paratroopers slot
nonzero, and C not human. Scan foreign live cities ascending whose owner has an
outgoing War flag from C. Capture target x/y; require no unit owner there.

For each target, scan live owned destination cities ascending within stepDistance
<= paradropRange of the target. Capture destination x/y. Scan its stack in stack
order: any unit with Paradrop ability ends the destination search for this target,
regardless of movement or type identity. Otherwise scan owned origin cities
ascending, polling network at each origin iteration. Require origin farther than
paradropRange from target but within that range of destination. Scan its stack;
the first Paradrop-capable unit with nonzero moves left is relocated to destination
through U01 with reveal argument 1, then its visibility is cleared and movement
exhausted. End origin and destination searches for this target, then continue to
the next target. No airport, transport, normal airlift or owner check is added to
the candidate stack entries. [A07](A07-ai-special-unit-missions.md#4-paratrooper-objectives)
owns later paratrooper objective selection; this
staging is the assessment prepass, not the actual drop attack.

### 4.2 Reset and goal census

Clear the four wonder-group counts. Scan 28 wonders ascending through C08's
active wonder query; increment group floor(wonder/7) for each true result.
For all 64 regions clear M, A, city-size sum, trade-unit count and presence flags;
zero local settlerCount and settleGoals arrays. **Retain region city counts and
policies at this point.** Clear shared unit total, military/city/ship counts,
city-size sum, military strength and attack total; clear all 62 active/in-production
unit counters. Do not clear lost-unit history or unrelated civilization state.

Scan current goals ascending. For each FoundCity role with positive signed
priority, call the land-region query at its coordinates. If nonnegative, increment
settleGoals[region]. There is no separate bounds or coordinate check before that
query. Invalid result writes outside the 64-element table are excluded bugs.

### 4.3 Initial unit/settler pass

Scan units ascending, polling network at every slot in mode>2. For each live
owned unit, clear GoalReached, AIGarrison and AIColonist before testing its role.
For a Settle role, mark AIColonist if C is currently human. If homeless, increment
settlersTotal. If its tile is not Ocean, mark AIColonist, increment local
settlerCount[rawRegion], and find the nearest city without an owner restriction.
If the child's shared nearest distance<4 and that city's owner is C, set its
AISettlerNear flag. Do not clear that flag for all cities here; its earlier
producer/clear boundary remains observable. A failed nearest lookup is not an
instruction to write a city before the array; backing/stop and excluded-write
rules apply at the reached accesses.

### 4.4 Main unit census and order normalization

Scan units ascending with the same per-slot polling. For each live owned unit,
capture its rule type and increment that type's active count. If signed attack
is at least 99, skip the remaining census work for this unit. Otherwise:

1. Mark AIGarrison if Fortifying/Fortified. Capture x/y. If out of map bounds,
   retain region=-1 and call sleep. Otherwise capture its land region; if C is
   currently AI, clear Sleep to None unless a land unit remains on Ocean, clear
   any signed order>15 to None, then change None to AIIdle when the adjacent
   same-domain nonallied-unit query succeeds. Finally, for any human/AI land unit
   on Ocean, call sleep. The last domain query uses the live type; earlier ones
   use the captured type.
2. Increment shared unit total. If captured signed role<Settle, increment military
   count and, when region>=0, M[region]. This includes admitted negative roles.
3. If the live role is Trade and home is present, set the unsigned home-slot
   city's HadCaravan flag; when region>=0, increment regional trade-unit count.
   No home-city owner/token check is added.
4. For captured Settle role with home, increment settlersTotal. If region<0,
   continue directly to the next unit, skipping all remaining strength/ship work.
   Otherwise increment settlerCount[region]. If the **old** stored regional city
   count is nonzero, choose divisor 4 for Anarchy/Despotism, 3 for governments
   below Republic, otherwise 2. Compute
   `quota=(floor(divisor/2)-leader.expand-1+oldCityCount)/divisor` with signed
   truncation. Add 1 for the fixed Engineers slot. Take min(quota,settleGoals).
   Mark AIColonist if local settlerCount<=quota; do not otherwise clear it here.
5. Still in that housed-settler branch, capture the unsigned home slot. If home
   already has AISettlerNear, this unit is AIColonist and C is currently AI,
   decrement settlerCount and clear AIColonist. If AIColonist remains and weighted
   distance from captured unit x/y to live home coordinates<5, set AISettlerNear.
6. Increment ship count if the live domain is Sea. Add captured-type signed
   attack to totalAttack, captured signed attack+defense to militaryStrength,
   and captured signed attack to regional A when region>=0.

For a foreign live unit instead, require NormalCombat and no C→owner Ceasefire
or Peace, then visibility to C (or the original low-owner equality case). Query
maximum HP, then remaining HP. If remaining<=maximum/2, post priority-1 Attack
on land or NavalSuperiority on Ocean at the unit's live coordinates. Alliance
alone is not checked by this branch. Goal insertion may immediately redirect
other units before the census continues.

### 4.5 City census and goal production

Only now clear all 64 regional city counts. Set local landCityCount=0 and scan
cities ascending, polling network at every slot in mode>2. For each live city,
capture x/y and require bounds. Owned cities:

1. Increment civilization city count and add signed city size to totalCitySize.
2. For a nonnegative production item, increment its unit-type production count.
   A selector beyond the 62 intended slots is an excluded table-overwrite bug.
3. Query land region. If nonnegative, add size to its region city-size sum and
   increment region city count; additionally increment landCityCount only for
   region 1…62. Region size/count arithmetic retains its cyclic domain.
4. If no unit owner occupies the tile, post Defend priority 5 there.

A foreign city is considered when difficulty*turn>200, its signed knowledge mask
contains C, its owner equals C's low unsigned identity, or its owner is not human.
There is no separate Alive test. Query V04 hostile(C,owner); otherwise admit
HostileIntent only without Alliance. If hostile and `(turn+cityIndex) rem 3 !=0`,
query City Walls, then post Attack priority 2 with walls or 4 without.
Independently, if the tile has no unit owner and no C→owner Ceasefire/Peace/Alliance,
post Defend priority 2, then Attack priority 3. The two goal producers can both
run. They use captured coordinates but read live ownership/treaties after children.

## 5. Regional presence and policy

After city counting, set policy of regions 0 and 63 to Expand. Clear AtWar on C,
then scan other normal civilizations 1…7; set it whenever C→other has War, without
an Alive gate. For each ordinary region 1…62 ascending, poll network in mode>2,
zero local attack/defend/threat counters and combined M/C sums, and capture old
policy. Run the following rival scan and classification.

### Rival scan

Visit civilization R=1…7 ascending, **including C**. Require R.M!=0 and either
`floor(C.A/4) < R.A` or R.C!=0. No liveness or global contact test is added.
For R!=C:

- Query V04 hostile(C,R); if true or C→R HostileIntent, count an attackable rival
  when `R.M < C.A` or `R.A < C.M` or C.C==0; otherwise count a stronger rival.
- Independently, if R.M!=0, R is human, C→R has Ceasefire/Peace, and
  `floor(C.A/2)+C.M < R.A`, increment threatCount.

For every admitted R, including C, add R.M and R.C to combinedMilitary and
combinedCities. For a foreign R with Alliance, add both quantities a second time.
For a foreign R without Alliance, set its city-presence flag if R.C!=0 and its
military-presence flag if R.M!=0, choosing WithPeace only when Peace is set.
The hostile/threat queries and these later flag checks observe current state.

The sums therefore can include the acting civilization and count allies twice.
These are intentional descriptions of the original computation, not an estimate
that may be replaced with a deduplicated count of enemies.

### Policy selection

If barbarian region city count is nonzero, increment attackCount. Start baseDensity=6.
Use 7 instead if inverseRank[7] is human, that civilization's stored city count>4,
and turn>150. Increment baseDensity for LargeMap.

Set crowded when `2*(C.M+combinedMilitary) > region.tiles`; equality is not enough.
If C's current presence flags are zero, density=baseDensity+leader.expand+1;
otherwise density=baseDensity. Set crowded also when
`(C.C+combinedCities)*density+2 > region.fertileTiles`. Region tile/land-quality
counts use their signed retained interpretation. Query Map Making; if absent,
force crowded=false even if one of the preceding tests passed.

Apply these writes in order:

1. Policy=CapacityPressure if crowded, otherwise Expand.
2. If attackCount!=0, policy=Attack.
3. If defendCount!=0 or threatCount!=0, policy=Defend.
4. If defendCount!=0, add StrongerEnemy to presence flags.
5. If C.M==0 and C.C==0, replace policy with Attack unless the military-presence
   subset is exactly WithPeace and has no WithoutPeace flag. Other presence bits
   do not affect that exception.
6. If the resulting policy differs from captured oldPolicy and C is currently
   AI, invoke C06's production reset for C and this region.

After all ordinary regions, if C has any cities in region 63 and landCityCount
is at most that region's unsigned city count, set region-63 policy=CapacityPressure.
This late write does not invoke the per-region production-reset child. Region 0
retains the earlier Expand assignment unless an intervening child changed it.

## 6. Assignment to shared goals

Skip this section for C=0; still run section 7 afterward. Otherwise capture the
human membership test once for this section. If AI, call V01 checkDestroyed(C,0)
when settlersTotal and city count are both zero. Continue after that child's
return without a fresh Alive gate. If `(liveTurn+C) modulo 8 ==0`, call
[A08's government chooser](A08-ai-research-government-and-diplomacy.md#choosing-a-government)
with restricted=false. The turn is read after elimination.

Set baseWeight=clampAB(signedMilitaryCount>>3,3,99). Initialize each of 48 local
goal weights to baseWeight, doubled for an Attack-role entry. Capture the special
scenario assignment gate: Scenario, its historical-script flag, C=3, and captured
human membership false. Capture the last unit index and visit units descending,
polling network per slot in mode>2. Require live token, current owner C, and either
captured AI control or Automated on the unit.

If signed live attack>=99, set the Nuclear AI task and continue. Otherwise skip
AIIdle. Orders greater than 15 or Goto bypass task reset; other orders first set
task Unassigned and must be below 4 to continue. Skip Sleep. Capture x/y, require
bounds, capture raw region and skip the preserved-special-task value. Then run
the scenario branch, retirement/upgrade and common goal selection below. An
explicit “continue” in a child means next unit; otherwise later children still run.

### Historical scenario assignment

This branch applies only under the captured special gate, to fixed Armor or
Alpine Troops rule slots, with captured y in 40…92. Capture all unit flags.
If turn==1, tile (43,53) is not owned by C, or this is Alpine Troops and that
tile's stack count is less than 3, set AI goto task Goal to (43,53), clear the two
scenario scratch flags, and continue to the next unit without restoring flags.

Otherwise, for Armor at turns 4…12:

1. If neither scratch flag is set, consume one primary draw. Remainder 0 modulo
   3 sets the second flag; either other remainder sets the first.
2. If the first flag is set and tile (76,34) is not owned by C, set AI goto Goal
   to (76,34).
3. Otherwise, when the second flag is set, query ownership at (62,72); if it is
   not C, or if it is C but (67,91) is not C, set AI goto Goal to (67,91).

Restore the originally captured flags on every non-early path, including the
new random bits. Continue into retirement/upgrade and ordinary goal assignment;
those can replace the scenario goto. The temporary random flags are not durable
route memory. These fixed slots/coordinates are classic scenario behavior, not
an inferred general strategy; S02 may invoke this documented branch through its
scenario flag rather than duplicating it.

### Retirement and replacement

Attempt only for captured AI control and `(liveTurn bitwise-and unitIndex
bitwise-and 15)==0`. This is not `(turn+unitIndex) modulo 16`.
A candidate either has a nonnegative obsolete-by advance that C knows, or is the
fixed Warriors slot under a government above Despotism in CapacityPressure policy.
Then require, in order: no adjacent same-domain nonallied unit, either no city at
captured x/y or stack count>1, not Ocean, and no regional StrongerEnemy flag.

Re-query obsolescence/knowledge. Replacement in place additionally requires that
advance, no visibility to the current local human, unit owner different from the
local human's unsigned identity, and either Fortress without City at the tile or
an existing city with stack count exactly 1. These later tests are distinct reads;
the earlier city/stack gate still applies. If admitted, scan rule slots 0…61 and
choose the first whose prerequisite equals this unit's live obsolete-by advance
and whose role equals its live role. Replace type and clear Veteran. Do not reset
damage/orders, pay treasury, check full build eligibility or repair census counts.
If no type matches, retain the unit. Either way, continue to common goal selection.

If replacement is not admitted, find the city at captured x/y. If present, invalidate
the shared calculation cache, call C05 support calculation, and add
`liveSignedUnitCost * currentShieldRows / 2` to its retained shield stock. Then
call U01 destroy/redraw(unit,1) and continue to the next unit. The actual deletion
child owns its count/stack effects. There is no unconditional recount afterward.

### Goal admission and distance score

Initialize bestScore=9999, bestGoal=Absent. Obtain unitRole once; it can change
locally during the scan as described below. Preserve an existing Trade-role Goto
to a valid foreign city: skip common selection when its destination has a
nonnegative city owner different from C. Also skip an Attack-rule unit with
signed attack zero.

Scan all 48 current goals ascending. Require role!=Free, signed priority p>0,
and bounds at its coordinates. An AntiSubmarine goal changes local unitRole from
NavalSuperiority to AntiSubmarine when the unit has SpotSubmarines. This change
persists for the rest of the scan even if that goal later fails region/range tests.

A goal is eligible if role equals this local role and its raw region equals the
captured unit region. Alternatively, an Air unit with signed attack at least that
of the fixed Bomber slot may use a matching-role or NavalSuperiority goal across
regions. Capture weighted distance d from the saved unit x/y to goal coordinates.
Compute its comparison score as follows:

| Unit case | Admission and score |
| --- | --- |
| Air with range exactly 1 | Let movement=`signedMove/roadMultiplier`. Require `d <= (movement-2)>>1`; score=d, without priority/allocation scaling. |
| Other Air | Require odd p and odd `((turn>>1)+goalIndex)`. Let movement be as above. Set multiplier=4 when d<=movement, otherwise 2; score=`multiplier*d/(p+1)`. The farther side uses 2, not 4. |
| Non-Air | allocation=`goalWeight/(2 if goal role is Attack else 1)`; score=`allocation*d/(p+1)`. |

Use the live fields at each indicated test; multiplying precedes truncating
division. For Fortifying/Fortified units, additionally require p>1,
`score<=p*baseWeight` or allocation==baseWeight, and no city at saved x/y.
Here allocation is the same role-based goal-weight quotient even for an Air unit.

For captured human control, if goalWeight==baseWeight and the goal tile has
Pollution, divide score by 8 with signed truncation. Do not compare the
role-normalized allocation here. Select a goal only when score<bestScore and
`score/baseWeight < p*4`. Both comparisons are strict, so the first equal score
wins. Keep the full goal index and score; do not convert this to a shortest-path
or nearest-city choice.

### Applying the selected goal

If none, return to the parent scan. Otherwise compare the goal's current x/y to
the unit's live coordinates. If already there, set AIIdle and GoalReached, then
continue to the next unit without incrementing its local goal weight.

For local unitRole Defend on a live Fortress without City, an even selected
priority means keep the unit and continue. For odd priority, post a standing
Defend goal of priority 2 at its live position, then proceed.

If the unit has Paradrop ability, require the selected goal's current city owner
to equal C before assigning the goto; otherwise retain the preceding effects and
continue. Set AI goto task Goal with the selected coordinates. If the unit's live
rule role is Settle and the goal now has any city owner, set that goal's priority
to zero. Increment local goalWeight for that slot. No movement is executed by this
assignment; U04 and the specialized AI algorithms execute the resulting order.

## 7. Assessment tail and outer movement passes

After assignment (also for C=0), scan owned live cities ascending. If FerryPortFresh
is clear, clear FerryPort; always clear FerryPortFresh afterward. Then refreshGoals(C).
New goal priorities can therefore become negative immediately after guiding the
current assignment, while standing goals can reappear positive for later consumers.

`processAIUnits()` captures the current turn civilization and the scenario first-
pass restriction: Scenario, historical-script flag, turn==1 and objective
civilization equal to the captured actor. The variable's source name suggests
naval priority, but the actual restriction admits **Air** units only on pass 0.

Run exactly two passes, 0 then 1. At each pass, capture the current unit bound
and scan downward. Before each slot in mode>2, return immediately if host handoff
is nonzero, then if playerHasQuit is nonzero. Require a live owned unit; set its
local actionCounter=0. Under the captured scenario restriction, skip non-Air
units on pass 0. For each admitted unit:

1. While U04 awake-with-moves(unit) returns nonzero, in mode>2 send state diff
   (all recipients, unit category), flush with the original 5000 ms limit, then
   process messages `(1,0)`. Ignore the flush return at this parent.
2. Capture the current unit bound, publish activeUnit=unit, and call U04 active-
   order continuation. If the unit bound changed, break this unit's loop before
   reading its order/domain. A same-bound replacement is not detected here.
3. If current order is not Goto and live domain is not Air, increment actionCounter;
   always increment it once more. If counter>20, exhaust movement and break.
   Thus an otherwise unchanged non-Goto ground unit gets 11 continuations, a
   Goto or Air unit 21. The counter is per unit per pass.
4. On pass 0 for non-barbarians, if live carrying capacity!=0, moves left==0,
   its live tile is Ocean, stack count>1, and either some human sees it or a
   nonallied adjacent civilization is present, call active-order continuation
   once more. This extra call receives no unit argument and uses the current
   shared active unit, which children may have changed. It does not increment
   actionCounter or perform another bound-change check here.
5. Re-test awake-with-moves at the loop head. After the loop, redraw cursor even
   if the admitted unit had no moves initially. Slots skipped before admission
   do not get that redraw.

The second pass can visit newly appended units because it captures a new bound;
an individual reverse scan does not extend when a child adds units. No random
choice belongs directly to this outer loop, but the actual U04/A02–A08 children
can sample using either F01 law. Human automation and multiplayer entry-point
selection remain with their calling session/activation algorithms.

## 8. Conformance targets

Observe retained counts/policies/goals, unit and city mutations, selected indices,
ordered child/network calls and random consumption. Do not compare only the final
move or selected production. The executed finite subset is listed in Appendix B.

| Input/transition | Required observation |
| --- | --- |
| Equal coordinate/role goal with equal priority | Return before redirection/insertion. |
| Existing matching priority 3, new priority 5 | Insert another entry; retain the old one after shifting. |
| Full goal list, low-priority naval goal in current AI turn | Nearby eligible naval units can receive Goto even when no goal slot admits insertion. |
| Current-list free hole / standing-list free hole | Current gap stops at the first hole; standing gap shifts the entire remaining tail. |
| Refresh priorities +5, -2, 0, -128 | Retain first role with -5; free second with -2; zero does not expire; -128 becomes free and remains -128. |
| Matching priorities -128 then +3 | Priority query returns 3 because its running 128 is compared as signed8. |
| Defense quota at 5/10/15/255 cities | 15/25/30/30. |
| Equal nonnegative power scores | Lower civilization ID receives the higher rank, including zero-valued dead candidates. |
| Same research/population/gold, vastly different armies | Equal ordinary power; the computed military subtotal is discarded. |
| Objective history at turns 74/75 | Write sample 37 at turn 74; no history write at 75. |
| Multiple launched humans at Emperor | Last ascending launched human replaces the coordination target. |
| Gang-up AI with Peace, draw remainder equal to difficulty | Set HostileIntent; the inclusive test passes. No war declaration occurs. |
| Old regional city count 8, rebuilt count 1 | Housed-settler quota uses 8; later region policy sees 1. |
| Attack 99 unit | Increment active type count, skip ordinary military/strength census, assign Nuclear task if admitted to reverse assignment. |
| Human civilization with unautomated ordinary unit | Census still clears shared unit flags; reverse assignment skips the unit. |
| Allied regional military/cities admitted to rival scan | Count twice in combined totals; suppress its foreign-presence flags. |
| Acting civilization admitted to rival scan | Count once in combined totals and again where the policy formula adds own totals. |
| No Map Making with crowded region | Force initial Expand, then still apply attack/defend/no-presence overrides. |
| Changed region policy, AI actor | C06 reset runs at that region's position in the scan, before later regions. |
| Fixed scenario Armor draw chooses second route | Destination can change, but temporary scratch bits are restored before common assignment. |
| Replacement rule found after census | Change type and clear Veteran without rebuilding active-type counts. |
| Air range not 1, target beyond movement | Use distance multiplier 2; nearer eligible target uses 4. Preserve turn/index parity admission. |
| First AntiSubmarine goal rejected later | Local role can still remain AntiSubmarine for subsequent goals. |
| Selected goal already at unit | Mark AIIdle/GoalReached without raising allocation weight. |
| Defender in Fortress, even goal priority | Retain position/order; no standing-goal post or goto. |
| Settler assigned to a city goal | Goto then zero that goal's priority; refresh does not free it merely for being zero. |
| New units appended during one movement pass | Break the affected inner loop on changed bound; the current descending bound stays captured, next pass captures anew. |
| Unchanged ground non-Goto / Goto or Air continuation | Exhaust at 11 / 21 ordinary child calls in the pass. |
| Historical scenario's restricted first pass | Air only, despite the source local's naval-related name. |
| Transport's extra continuation changes activeUnit | The extra child uses shared activeUnit; do not substitute the outer-loop slot. |
| Negative history index, invalid production selector, oversized region | Exclude the out-of-bounds write and resulting corruption; preserve the declared safe omission/rejection/stop policy. |

Full integration cases must join actual C06 production resets, C05 support,
V01 elimination/restart, A08 government, V04 hostile/attitude, V07 race,
V08 objectives, W01 regions, U01 stacks/deletion and U04 continuation. Consecutive
civilization turns are necessary to exercise stale foreign/regional summaries.
Human automation, callback-driven state changes and network authority require
separate joined traces, not just a single static-state census.

## Appendix A. Representation and reconstruction distinctions

- Current/standing goal records have 6 original bytes: signed16 x/y, signed8
  role/priority. Arrays have 48/16 entries. Free role is -1. Standard rule roles
  are Attack 0, Defend 1, NavalSuperiority 2, AirSuperiority 3, Transport 4,
  Settle 5, Diplomacy 6 and Trade 7; additional goal roles here are
  AntiSubmarine 18 and FoundCity 21. Capacity and order are behavioral; these
  offsets and packing are not required internal representation.
- Assessment AI task values are Goal `0x31`, preserved-special-task `0x37`,
  Unassigned `0x3f` and Nuclear `0x21`. Orders AIIdle/Goto/Sleep are
  `0x10`/`0x0b`/`0x03`; comparison of ordinary order bytes is signed.
  The historical scenario gate is first scenario flags word mask `0x8000`.
- Unit GoalReached/AIGarrison/AIColonist/Automated masks are
  `0x80`/`0x100`/`0x200`/`0x8000`. The two historical scenario scratch bits are
  `0x1` and `0x2` (the latter is also the spy mission bit elsewhere). The branch
  restores the entire captured flag field, not just those bits. City
  AISettlerNear/HadCaravan/FerryPort/FerryPortFresh are
  `0x40`/`0x1000`/`0x200`/`0x400`.
- Regional WithoutPeace/WithPeace city bits are 1/2; military bits 4/8;
  StrongerEnemy is 16. The no-own-presence exception tests `(flags & 12)==8`.
  AtWar and global GangUp are respectively civilization mask 2 and game mask 1.
  V04 owns the treaty encodings. Do not derive bit meanings from an independent
  source's descriptive names when its explicit tests differ.
- The nominal power table has eight local entries, with only slots 1…7 read as
  candidates; slot 0 is first written if no candidate beats -1. History is
  150 samples × 8 civilization columns. A negative original sample can address
  earlier game-state storage. Independent `power_ranking.cpp` models some such
  addresses, but those writes are excluded bugs under X01; no cleanroom
  implementation must recreate adjacent parameter corruption.
- The discarded military subtotal in ordinary power scans all 62 types. For
  signed role<5, take factor=(signedHP+1+signedFirepower)/2 on Land, otherwise 1;
  weight=(signedAttack*factor+signedDefense)>>1. Other roles use
  (signedCost+1)/2. Add unsigned active count*weight to a local accumulator.
  Independent instruction notes identify the accumulator store at `0048561e`
  and its lack of a subsequent consumer. All canonical rule/count reads are
  backed and constant divisors cannot fault; omitting this unused calculation
  preserves the specified power result in that domain.
- Independent goal/management sources expose wrapped owner/table accesses that
  can reach other named fields when malformed selectors are supplied. Original
  scalar conversion within a field remains compatible; writing beyond the
  intended goal/count/history table is excluded even inside a larger mapped
  record. Invalid unit-type counts, region counts, city-production counts and
  failed-city writes follow the same rule. A safe profile may reject such inputs
  before entry or omit the invalid write and continue where its required reads
  are established. If an earlier read lacks backing, stop there after preceding
  valid effects rather than inventing a result.
- The census uses the retained signed home byte's Absent test, then converts
  present home to the unsigned city slot. Thus slots 128…254 are not negative
  “no home” merely because of their signed view. Do not widen the sentinel test
  to home<0. Direct table accesses still require the relevant backed slot.
- The source's `naval_first_turn` name is misleading: its test is domain Air.
  The original also tests `(turn & unit & 15)==0` for retirement; neither behavior
  should be changed based on a more intuitive reading of a local name/comment.

## Appendix B. Evidence and validation

Primary OpenCiv2 pin: `a71e8aefffe04e6f719e0e213b027b128a747711`, inspected at
`fc2155ccdb2123c1137babab6fa39e38152278c9`. All **25 selected complete bodies**
match the pinned source. Independent revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752` in `civ2`
was compared for goal arithmetic, census order and regional values, scenario
continuation, power's discarded subtotal and movement-pass behavior.

Original target: Civilization II MGE **5.4.0f, patch 3**, executable SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
The executable hash and clean reference revisions were checked. No fresh
original-runtime replay, native Windows build, Ghidra investigation or annotation
change was performed. Historical gameplay evidence was not counted as fresh
validation of this specification.

| Evidence | Scope |
| --- | --- |
| [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp), `004853e7` | Power values, history, rank tie order and gang-up effects. |
| Same source, `00492b60`…`0049376f` | Nine shared goal operations, signed magnitude behavior, distinct gap shifts and refresh. |
| [ai.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/ai.cpp), `0053184d` and ten extracted private parts | Assessment, pre-census staging, city/unit counts, regional classification and goal assignment. |
| Same source, `00442885`, `00531287`, `00536bc9`, `00543cd6` | City-region contact, role conversion, defense quota and two movement passes. |
| [civ2types.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h), [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h) | Numeric domains, rule identities and original record/flag encodings. |
| Independent `src/ai_targets.cpp`, `src/ai_target_refresh.cpp` | Priority 128 comparison, list insertion, scalar retention and excluded cross-record write paths. |
| Independent `src/civ_unit_management.cpp` | Explicit original instruction boundaries, unsigned regional words, strict crowding, cached human/reverse bound and scenario restoration. |
| Independent `src/power_ranking.cpp`; `src/ai_unit.cpp` outer `00543cd6` | Discarded military subtotal, negative-history distinction, movement guard and active-unit continuation. |

A standalone source-derived C++ harness compiled all 25 bodies: 15 original-entry
bodies and ten extracted assessment parts. Both optimized and
AddressSanitizer/UndefinedBehaviorSanitizer builds passed **37 assertions**, with
leak detection disabled. The selected saved-state/rule structs were extracted
unchanged; civilization/unit/city sizes were asserted. Pointer-sized host types
allowed the original indexed field accesses without truncating host addresses.
The first whole-header attempt included unrelated native UI layouts and failed
on their host ABI assertions; extracting only the nine used record types resolved
that fixture issue without changing an algorithm body.

Executed cases cover goal duplicate/refresh behavior and signed minimum priority,
redirection before full-table rejection, defense quota, power/history/ties,
coordination target and draw threshold, old regional-city settler quotas, nuclear
census exclusion, human/automated assignment, regional defense, paratrooper
staging, scenario scratch restoration, post-census upgrade and outer movement
passes/guards. Calls between the selected goal, assessment, role and quota bodies
are real. Only the appropriate finite samples, not every branch of every compiled
body, were exercised.

Map queries and small distance calculations, lifecycle/stack/movement services,
knowledge/wonder/hostility/objective providers, production/support,
government, ship queries and network/display services were declared adapters.
The historical queued values checked original call count/order but did not validate generator
state updates. No out-of-bounds write was replayed. These checks establish finite
source behavior, not native UI/network or independent original-program conformance.
Markdown links/anchors, source pins, specification IDs and whitespace are checked
with the documentation update. X02 joined state/draw/callback traces remain the
basis for a future whole-game conformance claim.
