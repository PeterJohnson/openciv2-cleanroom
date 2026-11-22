# A02 — AI city production

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in A02 |
| --- | --- |
| Gameplay | Production eligibility/scores/ties, survey side effects, sales/airlift and recommendations; non-wonder rivalry uses the documented safe zero contribution. |
| Original files/content | F03 cost/prerequisite/leader inputs and F06 selected production/AI fields. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Adjacent-local rivalry bytes, temporary score layout, unused candidate traces and supplied stack backing. |

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
**5.4.0f, patch 3**. This specification owns the production chooser used by AI
cities, human advice and human automatic production. It describes the ordered
survey, free irrigation, redundant sales, airlift selection, candidate scores,
preference override and final recommendation. Calling it for advice can change
state; it is not a pure ranking query.

[C06](C06-city-production-and-purchasing.md) owns eligibility, changing production,
completion and AI purchasing. The chooser returns a production identity; it does
not itself apply that identity through C06. [C01](C01-city-lifecycle-and-ownership.md)
supplies nearest-city and coastal queries. [C03](C03-city-economy-and-corruption.md)
owns the opening city recalculation; [C05](C05-city-growth-and-support.md) supplies
its support and shield factors. [C08](C08-buildings-and-wonders.md) supplies
facility and active-wonder queries. [A01](A01-ai-strategy-and-assessment.md) owns
retained regional policy, counts, power ranks and goals; these are inputs here,
not recomputed by the chooser. [V07](V07-spaceship.md) owns ship queries and part
group choice. [A03](A03-ai-settlement-and-terrain-work.md#2-tile-improvement-recommendation)
supplies the tile-improvement recommendation used below.
[U05](U05-transport-and-special-movement.md#ai-city-production-airlift) owns the
actual AI airlift and interception continuation. [F03](F03-rules-and-configuration.md#human-city-production-preferences)
owns CITYPREF loading. [F01](F01-determinism-and-arithmetic.md),
[F02](F02-game-state-and-invariants.md), [W01](W01-map-geometry-and-spatial-state.md)
and [U01](U01-unit-lifecycle-and-stacks.md) supply arithmetic, retained identities,
geometry and stack operations.

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

For the required profile, a rivalry lookup outside the 28 wonder entries returns
zero. The original adjacent-local mappings and supplied-byte cases are diagnostic
evidence only; they do not define AI inputs. Other valid scoring/ties are unchanged.

### Production safety repairs

X01 overrides the diagnostic backing requests in Appendix A. The production
chooser uses the following narrow repairs; no original stack or adjacent record
bytes are inputs:

- The exhausted unit-rule index 62 has no rule backing. Skip that conditional
  role block: Flagged and Homeless stay zero, while every owned unit still
  contributes to Ut and its valid regional N entry. Settler scores can therefore
  differ from executions whose unrelated byte happened to denote Settle.
- The exhausted diplomacy treaty index 8 has no treaty backing. Skip its
  Ceasefire/Peace adjustment, preserving the actual last qualifying target.
  Attitude[0] does not act as a ninth treaty. This may change diplomat scores.
- A retained city cursor still reads the size of a backed inactive slot. An
  unbacked cursor stops at that read after preceding effects; it does not use
  the current city's size or invent a zero-sized city.
- Non-wonder rivalry is zero, as already required by X01. Consequently the
  uninitialized quota/ship-cost/role frame bytes are never read through rivalry.

These are contract-derived repairs, not original-game equivalence observations.
Tests cover their branch outcomes and retained preceding mutations. Valid scalar
wrap, ties, donor cursors and inactive backed city sizes remain unchanged.

## 1. Boundary, domains and notation

Input: an existing city K; optional unit and building recommendation outputs;
live world/rules/civilization state; city preferences; and the declared retained
read inputs in Appendix A. Nominal rule identities are units 0…61, improvements
1…38 and wonders 0…27. A wonder's improvement identity is 39+w. The production
encoding used in the formulas is nonnegative unit identity or negative
improvement identity; 99 is the automatic-choice request/result, while 999 is
the chooser's internal no-candidate sentinel. Those two sentinels are distinct.

Civilizations are slots 0…7, with barbarian slot 0. `human(C)` tests the current
human-player membership. `active(t)` and `queued(t)` mean C's retained unsigned
unit counts from A01, not the local survey counts. Regional counts likewise
remain as last published. `H=bestHumanRank` and `L=worstHumanRank` are used
**directly as civilization slots** where stated below. Do not replace them with
the civilization at that rank. Only an explicit `civByRank` lookup does that.

Use F01 reference expression results, signed comparisons, truncation toward
zero for `/`, and arithmetic right shift for `>>`.
Thus `x>>1` and `x/2` differ for negative x. Multiplication wraps before division;
do not replace successive multiply/divide steps with a rational expression.
Rule attack, defense, movement, cost, hit points, firepower and role are read
with signed8 interpretation; improvement costs, cosmic factors and type counts
are unsigned8. City size is signed8, stock and civilization city/military totals
signed16; ship part counts signed16. Regional military/attack and civilization
military strength are unsigned16; regional city/size/trade counts are unsigned8.
Gold is signed32. F02 owns other retained conversions. `clamp` is F01's
`clampAB`, including inverted bounds.

Leader traits `a`, `e`, `z` mean aggression, expansion and civilization at the
point read. Nominal traits are -1, 0, 1. Custom accepted values use the same
arithmetic; a zero divisor or an unbacked read is a bounded failure after prior
effects. A stricter implementation may declare a narrower accepted domain.
Neither accepted scalar wrap nor a stale read authorizes an invalid write.
[X01's overwrite exclusion](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
applies to every count, rivalry and output destination in this document.

The following values are captured by the opening survey:

| Name | Meaning |
| --- | --- |
| C, R, P | City's owner, raw region and that owner's policy for R. Policies are Attack 0, Defend 1, CapacityPressure 4, Expand 5. |
| T | Threat preference, initially 0 or 1; it is not a count of hostile units. |
| F, S, Q | Food surplus, doubled shield surplus and net trade. |
| E, O, Fm | Nearby foreign stack count, weighted ocean tiles and farmland tiles. |
| U | Unrest state 0, 1 or 2. |
| Wip, Wh, Wo | Established wonder production, active wonders here and active wonders owned. |
| B, Ap, Pf | Other regional barracks, airports and port facilities; B also includes queued barracks and the Sun Tzu offset. |
| Same, Ahead, ForeignSame | Other owned cities with same production, those with greater recorded shield output, and foreign cities with same production. |
| Wb, Sb, Hsb | Other owned regional wonder builders, other owned global spaceship builders, and foreign human spaceship builders. |
| N[t], J[t] | Owned unit counts in R and other owned cities in R producing t, accumulated during this call. |
| Ut, Flagged, Homeless | Owned unit total and the special counts from the exhausted-rule read below. |
| Expand, G, Offshore | Expansion bonus, regional standing Defend goals and offshore-enemy flag. |
| Need, Urgent, Naval | Defense shortage, urgency and local naval count. |

These captures remain unchanged after sales, free irrigation except its explicit
F increment, airlift or subsequent calls. Other fields are live reads at the
stated step. Forward city/unit scans use live bounds and skip unused identities.
The city scan cursor is retained between phases; it is not always K. Preserve
its final value for the explicitly identified size reads. Services/callbacks
must not be reordered across reads. The chooser has no direct random draw;
U05 interception and child operations retain their own draws and side effects.

## 2. Opening survey and early returns

1. Set Expand=0. Write 999 to the unit output if supplied, then 999 to the
   building output if supplied. Call `recalculateCity(K,0)`.
2. Capture C from the owner (the original sign-extends its retained owner byte),
   R from the city's raw region, and P from C's regional policy. Set T=1 for
   P=0 or 1, otherwise 0; GangUp also makes T=1 when P=4.
3. Call V07's behind-in-race query, then race-active query; capture their sum
   as Race. Scan cities ascending for live cities founded by C, now owned by
   another human, in R. If their count exceeds `a+1`, set T=1.
4. Capture `F=foodYield-size*foodPerCitizen-settlersSupported*settlerFoodCost`,
   `S=2*totalShields-shieldSupport`, `Q=totalTrade-corruption`. Set O=0.
   Wip is true exactly when current production is a wonder and stock>39.
5. Start Wh=Wo=0. For each wonder ascending, clear its rivalry entry, query
   its active city and increment Wh if K, then query C's active ownership and
   increment Wo if true. Clearing is interleaved with the queries.
6. U=2 if Disorder is set, or both OnEdge and AISpecialistHeld are set.
   Otherwise U=1 if calculated happy and unhappy counts are equal, else U=0.
7. Call C01 nearest-city search at K with owner C, requested-region selector -2
   and alternate owner -1. This requires an owned **Port Facility** and disables
   the region gate; it does not exclude K. Retain its published distance D
   (9999 if no match). Run the tile survey below.
8. For each type 0…61, clear J[t] then N[t]. Run city survey, unit survey and expansion
   bonus in that order. Count all 16 standing goals with role Defend and raw
   region R, regardless of their priority. Set G to this count; if T is nonzero,
   add `G>>1`.
9. If Ut=0, C is AI, government<Monarchy, and **both** recommendation outputs
   are absent, scan type 61 down to 0. Return the first C06-buildable Defend
   type without Paradrop. This return precedes the barbarian branch.
10. If C=0, return the barbarian selection below. Early returns leave any
    supplied recommendation outputs at their opening 999 values.
11. Set Offshore=0. Only for P=4, scan civilizations 1…7: directed War C→other
    plus no other cities in R sets Offshore=1. No alive/human filter is added.
12. Determine defense needs. For AI C only, sell redundant buildings and try
    the donor airlift, in that order. Continue with candidate selection.

### Tile survey and free irrigation

Scan W01's city-radius B indexes 0…19, excluding center. For each index, compute
coordinates from the city's live position, wrap x once, and skip out-of-bounds
coordinates. Ocean contributes 1 to O, plus 3 if that indexed tile is worked.
For a foreign non-absent tile unit-owner cache, fetch the first unit; if present
and its owner also differs from C, add stack-count mode 2 to E. Alliance, peace
and actual hostility do not suppress this count. Count a tile as farmland when
both irrigation and mining feature bits are present; use W01's farmland meaning.

Start E=Fm=0. Seek free irrigation only if F=0, C is AI, AISettlerNear is clear
and settlersSupported=0. While seeking, call A03's tile-improvement choice.
Recommendation 1, no irrigation, and a negative irrigation-result terrain code
make this the latest selected index. If it is Plains with a nonzero resource,
stop replacing the selection, but finish all other tile counting.

After the scan, recompute the selected index's coordinates from the live city.
If no human has visibility of that tile, set Irrigation and, if no River, Road,
through W01's feature setter with set=true and synchronization requested.
Increment F by one; the setter adds no derived-state or remembered-map refresh.
Do not recalculate the city or derive the increment from actual food yield.
If a human sees the selected tile, do nothing; do not fall back to another tile.

### City survey and rivalry

Initialize all listed city-survey counts to zero; B starts at 4 if C has active
Sun Tzu, otherwise zero. Scan live cities ascending, retaining the scan cursor.

For each owned city, count a palace if Palace exists **or** Palace is being
built; keep the largest size among these cities, starting at zero. For owned
cities other than K, count Same when production matches K, and Ahead if their
recorded total shields exceed K's recorded total shields. Count Sb for production
Structural, Component or Module regardless of region. Then, only in R:

- Query Barracks, Airport and Port Facility, incrementing B, Ap and Pf. Queued
  Barracks increments B again, independently of existing Barracks.
- A wonder production increments Wb. Set that wonder's C-rival marker if the
  other city's recorded total shields exceed K's. Set its completed-stock marker
  if `unsignedImprovementCost*liveShieldRows <= otherStock`.
- Nonnegative production increments J at that type.

For a foreign city, count ForeignSame when its production matches K. A wonder
sets its owner's rivalry marker regardless of region, shield output or stock.
A human foreign city producing any of the three ship groups increments Hsb.
No extra alive or treaty filter applies. Rivalry entries retain multiple owners;
the completed-stock marker shares the original barbarian-owner position.

An invalid nonnegative production such as 99 must not overwrite beyond J. Omit
that invalid increment, preserving other established survey effects, or reject
the input under a declared stricter profile. Invalid wonder writes likewise do
not corrupt other state; subsequent cost reads still require valid backing or
a stop. Extra mapped memory does not extend these logical tables.

### Unit survey and expansion

Set Flagged=Homeless=0. For every live owned unit ascending, increment Ut.
The original tests the role at the **exhausted rule index 62**, left by the
array-clear loop, rather than the unit's own role. Supply that read as Appendix
A specifies. If it equals Settle, increment Flagged for AIColonist; if the home
byte is the Absent sentinel, increment Homeless and skip this unit's regional
count. Otherwise query its raw region and increment N[live type] if R.
Invalid N indexes are excluded writes, not reasons to corrupt adjacent counts.

Expand stays zero unless AITerraformGoal is set, AISettlerNear is clear, E=0,
settlersSupported=0, P≠Defend and size>2. If admitted, scan all other live owned
cities for the least W01 step distance, starting at 9999. Distance>8 sets
Expand=1. Independently in this admitted block, size>6, government>Fundamentalism,
F>0 and P≠Attack increments Expand. Retain this scan's cursor if it ran.

### Barbarian choice

If current production<1, return Dragoons if any civilization knows Explosives,
otherwise Legion. Otherwise keep the current type unless it has a nonnegative
obsolete advance, difficulty is above Chieftain, and at least two bits are set
in that advance's known-by mask. Also keep it below Emperor if no human knows
that advance. When admitted, scan types 0…61 and return the first with the same
role as the current type and prerequisite equal to its obsolete advance.
If none, keep the current type. This path does not call full unit eligibility.

## 3. Defense needs, sales and donor airlift

Set Need=Urgent=0. Query the first city unit and stack counts for Defend,
SeaTransport and NavalSuperiority, in that order. Capture
`Naval=(transportCount>>1)+navalCount`. With no defenders, set Need=Urgent=2 and
leave the original desired-defender local unwritten; Appendix A distinguishes
that retained read input from an initialized zero.

Otherwise start size-per-defender at 4 if no adjacent ZOC unit, else 3. A Palace
decrements it; without Palace, P=Expand increments it. Start desired at
`(T!=0 or regionalFlags!=0 ? 1 : 0) + size/sizePerDefender`. Query Palace again:
if present, raise desired to at least 1; its minimum becomes 2 for size>3,
3 for total cities>7, and 4 for total cities>11, in that order. If defenders are
below desired, Need=1; if defenders+1 are also below desired, Urgent=1.
The original extra local threat flag is initialized zero and never raised here.

For AI C, process these active-wonder/facility pairs in order: Pyramids/Granary,
SETI/Research Lab, Women's Suffrage/Police Station, Michelangelo/Cathedral.
Query active ownership then facility possession. If both true, clear the facility
and add `unsignedCost*liveShieldRows` to live gold with signed32 retention.
These automatic sales have no player one-sale-per-turn gate or sale notice.

Try an airlift only if K has Airport, C is the current turn civilization, K's
AirliftUsed flag is clear and P is neither 4 nor 5. Scan other owned live cities
ascending. Require Airport, no AirliftUsed flag and no adjacent nonallied unit.
For a donor in R require Need>1; for another region require its current policy
to differ from captured P. Require a first unit; for P=Defend require at least
two Defend units. Require total stack count at least 3, or at least 2 when the
donor's live policy is 4.

The first donor meeting those gates ends the donor search. Walk its stack to the
first unit whose signed rule role equals P; if found, execute U05's
[AI city-production airlift](U05-transport-and-special-movement.md#ai-city-production-airlift).
**Break the donor scan even if no matching-role unit exists.** Keep the donor
cursor for later stale-size reads. Do not refresh Need, Urgent, N, Naval or the
captured city calculations after transfer or interception.

## 4. Candidate order and common weighting

Initialize choice, bestBuild, bestUnit and their three scores to 999. Lower
signed scores are preferred. Scan improvements 1…38 ascending with C06
eligibility. For each eligible improvement calculate section 5's base score b
and defensive preference d. Unless stated otherwise b=999 and d=0.

For improvements and special-role units, common weighting is:

```text
if b < 400:
    multiplier = 10 if T == d else 20
    b = (multiplier * b * 3) / (z + 3)
else:
    b = 16383
```

For an improvement, if K has Barracks, add `b/((B>>1)+3)`. If the active
Shakespeare city is K, add `b/3`. Compare independently against choiceScore
and bestBuildScore; a strict reduction replaces that score and the corresponding
identity with the negative improvement. Equal scores retain the earlier choice.

After the improvement loop, a human owner with a nonempty CITYPREF list scans
it in order. Query possession, then eligibility for each missing facility.
The first eligible missing entry sets both choice and bestBuild to its negative
identity, **without changing either score**. It skips wonders and special-role
units; the military/settler loop still runs. This preference applies to human
advice as well as Autobuild, because there is no Autobuild gate here.

Without a selected preference, scan wonders ascending when Wip, or when all of
these hold: no regional StrongerEnemy, size>2, not both OnEdge/AISpecialistHeld,
and either regional cities≥`4-z`, or regional flags=0 and total cities≥`4-z`.
Then, if military count<512, scan special-role units 0…61.

Finally, for any preference path, if military count<512 scan ordinary-role units
0…61. Otherwise, if choice is still 999, set choice to Capitalization (-38),
leaving its score and recommendation identities alone. Publish bestBuild first,
then bestUnit to their supplied outputs. Apply section 9's final policy and
return choice. Recommendation outputs therefore precede final substitution and
can differ from the return value.

## 5. Improvement scores

Here h=`F>>1`, n is the live size of K, and `oldSize` is the live size at the
retained city-scan cursor, with Appendix A's read boundary. Do not substitute n
for oldSize. `shields`, `waste`, `corruption`, `happy`, `unhappy`, `support` and
`aggressive` are the live shared city-calculation values.

| Improvement | Base score and changes before common weighting |
| --- | --- |
| 1 Palace | Admit only when (no owned palace-count or C is AI), and (no palace-count or exactly one with `2*largestPalaceSize<oldSize`), and (P=4, 5 or 0, or waste>3). Set b=2; oldSize>9 makes b=1, oldSize>14 decrements it. If waste>3, d=T. |
| 2 Barracks | `b=z+B+4-a`. If `(a<0 or z>0)` and `z+a!=0`, add B. Set d=1. For a human on King use `(3*b)/2`; above King double b. |
| 3 Granary | Only without active Pyramids: b=8 for shields<3, else 4; add 2 if happy≤unhappy. For humans add `difficulty+4-oldSize` only when positive. |
| 4 Temple | U=1 gives 9; U=2 gives -5. |
| 5 Marketplace | `clamp(10-(Q>>1),1,10)`; U=1 decrements, U=2 replaces with -4. |
| 6 Library | `clamp(10-Q/3,1,10)`. |
| 7 Courthouse | `14-(2*waste+corruption)`. In Democracy replace with -1 if U=2. Outside Democracy for AI C subtract `civs[H].subversionCount/2`. |
| 8 City Walls | Use the defense-facility procedure below. |
| 9 Aqueduct | If `aqueductSize-h<=n`, use `clamp(aqueductSize+4-n-h,1,20)`. |
| 10 Bank | `clamp(10-Q/3,1,10)`; U=1 decrements, U=2 replaces with 0. |
| 11 Cathedral | Only without active Michelangelo: U=1 gives 8, U=2 gives -3. |
| 12 University | With nonzero science rate and (Race=0 or C lacks Space Flight), use `clamp(10-(Q>>2),2,10)`. |
| 13 Mass Transit | Keep 999. |
| 14 Colosseum | U=2 gives -2. |
| 15 Factory; 20 Hydro Plant | `clamp(14-shields,1,14)`. |
| 16 Manufacturing Plant; 18 Recycling Center; 21 Nuclear Plant | `clamp(12-shields/4,2,12)`. |
| 17 SDI Defense | If U<2, start `(clamp(15-n,1,15)+1)/2`; double if Manhattan's raw city marker is NeverBuilt (-1), double if `civs[H].active[NuclearMissile]=0`, decrement if n>9, then replace with 0 if Palace. Set d=1 even when U=2. |
| 19 Power Plant | `clamp(12-shields/5,2,14)`. |
| 22 Stock Exchange | Only if U≠0 or Race<2: `clamp(11-(Q>>2),2,11)`; U=1 decrements, U=2 replaces with 0. |
| 23 Sewer System | If Race<2 and `sewerSize-h<=n`, use `clamp(sewerSize+4-n-h,1,20)`. |
| 24 Supermarket | `clamp(14-(n>>1)-2*Fm+2*Race+h,2,14)`. |
| 25 Superhighways | `clamp(15-(n>>1)+6*Race,2,15)`. |
| 26 Research Lab | If (Race=0 or C lacks Space Flight) and science rate≠0, use `clamp(11-(Q>>2),2,10)`. The advance query precedes the rate test here; University tests rate first. |
| 27 SAM Battery; 28 Coastal Fortress | Use the procedures below. |
| 29 Solar Plant | Keep 999. |
| 30 Harbor | If U<2, `clamp(16-O,2,16)`; halve when F<1. |
| 31 Offshore Platform | If U<2, `clamp(16-O,2,16)`. If initially positive, halve for Factory, then independently halve for Manufacturing Plant. Subtract 2 if `(shields-support<=shields/3 or shields<oldSize/2)` and `oldSize/2<=O/4`. |
| 32 Airport | `b=4*Ap+4-a`; halve when the **live** regional policy is 4; double without Advanced Flight. If regional cities=1, or StrongerEnemy and Ap=0, lower b to regional city count when that count<b. If Palace and b≥0, replace with 0. Set d=T. |
| 33 Police Station | Only without active Suffrage: q=aggressive, doubled above Republic. If government>Fundamentalism, q≠0 and happy-unhappy<2, add 2 to q. Set b=10-q. |
| 34 Port Facility | Only if C01's adjacent-body query with selector 1 succeeds: `clamp(11-(min(D,40)>>2)-5*Naval+2*Pf-a+z,2,15)`. If a<0 add Pf **after** clamping. |
| 35 Structural | Use the spaceship procedure below. |
| 36 Component; 37 Module; 38 Capitalization | Keep 999; they have no individual score case. C06 can later choose the actual group from Structural production. |

### Walls, SAM and coastal fortress

Walls keeps 999/d=0 with active Great Wall. Otherwise start `10-(n>>1)`.
For every human civilization 1…7, subtract 2 if directed C→other has Vendetta
or War, then subtract 1 if that civilization has any cities in R. Add 2 for
each globally known Robotics, Advanced Flight and Rocketry, in that order.
Clamp to 1…10, then subtract 4 if Palace. Set d=1.

For AI SAM, threat is twice
`2*H.StealthBomber + 2*H.Bomber + H.CruiseMissile + H.Helicopter`.
For human C instead sum, over other civilizations 1…7, their counts of Cruise
Missile, Bomber, Helicopter and Stealth Bomber. Halve each other civilization's
subtotal if neither War C→other nor Vendetta other→C is set. Start SAM score
at `clamp(12-threat,1,12)`; subtract 3 if SAMUnused is set, with no second clamp.
No ordinary writer of that flag has been established; accepted retained flags
still affect this formula.

Coastal Fortress first requires adjacent-body selector 1. For AI C its threat is
`3*H.Cruiser+3*H.AegisCruiser+5*H.Battleship+H.Ironclad+H.Destroyer`.
For human C sum other civilizations' `Ironclad/2+Destroyer/2+2*Battleship+Cruiser+
AegisCruiser`, halving each subtotal under the same directed treaty test as SAM.
Use `clamp(12-threat,1,12)`.

For both admitted SAM and Coastal Fortress, start d=1. Scan human civilizations
1…7; the first with reputation>4 or directed War C→other replaces d with
`T!=0` and ends this scan. No alive or other≠C test is added to this last scan.

### Spaceship production score

Structural keeps 999 unless C has not launched, GameWon is clear, V07's
`choosePartGroup(C,0)` returns nonnegative, and either C is AI or its Structural
count<38. Preserve the group chooser's side effects even though only the sign
of its result is used.

Set b=4. The first human civilization 1…7 with an existing ship changes it to 2.
If P≠Attack and no StrongerEnemy, set d=T. Set humanLaunched=0; if GameWon is
still clear, scan humans for a launched ship and set it to 1 on the first match.
If Race>1 and difficulty≠Chieftain, subtract 1 from b, or 2 if humanLaunched;
when Urgent=0 also set d=T.

Initialize ownCost and leadingCost to zero. For each of the six ship parts
ascending, group=`clamp((part+1)/2,0,2)`; accumulate that civilization's signed
part count times the unsigned cost of improvement 35+group, for C then H.
Keep ownCost as the original frame value described in Appendix A.

If `(Sb=0 or (Sb<=Hsb and ownCost<=leadingCost) or humanLaunched)` and K is not
in Disorder and difficulty≠Chieftain, then `(bestShieldSurplus>>1)<=S` or S>19
sets **choiceScore=999**. Do not clear choice, bestBuild, bestBuildScore or any
other score. This side effect precedes Structural's common weighting/comparison.

## 6. Wonder candidates

For each wonder w ascending, let i=39+w and first require C06 improvement
eligibility. Set d=0, or d=T if P=4. Let group=`w/7` and
`base=Wo+Wh+8-gold/200`; if base<2 replace it with 1. Add C's group count;
add 2 if that count exceeds the retained minimum human group count; subtract
1 if C's count is below `civs[L]`'s group count. Call the result b.

If Wip and current production differs from -i, reject when the current wonder's
completed-stock marker is set. Otherwise allow the switch only if at least one
of these holds: no human rivalry marker for the current wonder; its raw location
is not NeverBuilt; or stock is below half of its unsigned cost times live shield
rows. These are tests of the **current** wonder, not the candidate.

At Warlord or lower, a nonnegative candidate prerequisite must be known by a
human. For candidates other than Manhattan, at difficulty below Prince scan all
live human cities; any producing -i sets b=999. Finish the scan, retaining its
exhausted city cursor, then reject if b=999. Also reject non-Manhattan if C knows
Rocketry and no human knows Rocketry. Manhattan uses its own rule below.

### Individual wonder adjustments

Apply the following to b, in the listed order inside each row. Facility/wonder/
advance queries use C08/V03. Civilization scans run 1…7 without an added alive
filter. `cities` in formulas means C's signed total city count. Active unit
counts are C's unless another civilization is explicitly named.

| w / Wonder | Adjustment |
| --- | --- |
| 0 Pyramids | Subtract `3*z+2`. |
| 1 Hanging Gardens | Subtract `cities/3+1`. |
| 2 Colossus | Subtract 1 for Palace, then 1 for Marketplace, then `size/3`. |
| 3 Lighthouse | Subtract `(Caravel+2+Trireme)>>2`. |
| 4 Great Library | For each civilization with fewer advances than C, add 1 only if human. For each with at least C's advances, subtract 1, or subtract 3 instead if human and more than C+5. This includes C's own slot. |
| 5 Oracle | Subtract `cities/4-turn/25`. |
| 6 Great Wall | Subtract `2*z`. |
| 7 Sun Tzu | Subtract `3*a+e-2*z+1`. |
| 8 King Richard's | Subtract `clamp((2*shields)/3,0,3)`. |
| 9 Marco Polo | For each human, subtract 1 for directed Alliance C→other and separately subtract 1 for Ceasefire or Peace C→other. |
| 10 Michelangelo | Subtract `cities/4`. |
| 11 Copernicus | Subtract `Q/8+1`, then 1 for Palace. |
| 12 Magellan | Sum active counts of all 62 types whose domain is Sea; subtract `(sum+3)/4`. |
| 13 Shakespeare | Subtract `size/5`. |
| 14 Leonardo | Set d=T; subtract 4. |
| 15 Bach | Subtract `regionalCitySize>>4`, then 1 for Palace. |
| 16 Newton | Subtract `size/6`, then 1 for Palace. |
| 17 Adam Smith | Subtract `cities/4`. |
| 18 Darwin | Subtract 3. |
| 19 Statue of Liberty | Subtract 2 without Communism, else 1. |
| 20 Eiffel Tower | Only if `civByRank[7]` is human: subtract 2 if its reputation>3, else 1. If that civilization has >7 cities, subtract another 2 when C's rank=6, else another 1. |
| 21 Women's Suffrage | Subtract 2 if all three traits≥0, else 1. Subtract 1 if the Republic government score>0, 1 if current government>Fundamentalism, then 1 if >Republic. |
| 22 Hoover Dam | Subtract `regionalCitySize/20`. |
| 23 Manhattan | Reject unless C knows Rocketry and there is no human with Rocketry whose military strength is lower than C's. If `civByRank[7]` is human, subtract 1 and subtract another 1 if it lacks Rocketry. Then add `2*z-a`. |
| 24 United Nations | Subtract `z-2*a`, then 1 if C's rank=7. |
| 25 Apollo | Subtract `3*z-2*a`. |
| 26 SETI | Subtract `2*z+2`. |
| 27 Cure for Cancer | Subtract `a+2`. |

### Rivalry, weighting and selection

1. Query expiry once and retain it. If expired, replace b with
   `3*b+50*difficulty`. Appendix A resolves a disagreement in the reconstructions
   using original instructions. Add `5*Wb`. If `(Wb=0 or Wip)` and C's group
   count=0, subtract 2. Add E+1 for P=Defend, otherwise E/2.
2. If current production=-i, halve b if ForeignSame≠0, then set
   `b=(Ahead+1)*b+Ahead`. Otherwise read the rivalry value indexed by the
   **current production**, even when that production is not a wonder. If its
   C-owner marker is set, replace b with `2*b+1`. Use Appendix A's original
   retained-read contract; both reconstructions' non-wonder guards are repairs.
3. Capture raw=b. Set cost=`unsignedCost[i]*liveShieldRows+1`.
4. If Wip, Urgent=0 and not expired: with Wb=0, halve b when T=d, else set d=T.
   Stock≥`(3*cost)/4` sets d=T. If T still differs from d, stock≥cost/2 and
   Wb<3 also sets d=T.
5. Without Wip, double b when `(bestShieldSurplus>>1)>=S`. Otherwise double
   only for a human when bestShieldSurplus≠S and S<6. If bestShieldSurplus=S,
   subtract b/3, whether or not Wip.
6. Apply `(multiplier*b*3)/(z+3)` with multiplier 10 when T=d, else 20.
   Wonders do **not** have the improvement/special-unit 400 cutoff.
7. If Barracks, add `b/((B>>1)+3)`. If Palace, subtract b/3, then halve when
   Wh=0 or C's group count=0.
8. If WonderBuilt is set, triple b. Otherwise, when Wip and not expired, use
   `(((cost-stock)*((3*b)/4))/cost + b/4)/3`, retaining each intermediate
   multiplication and division in that order. This is candidate cost with live
   stock, even if current production is another wonder.
9. If choice already identifies a wonder, replace it only when raw is strictly
   below bestRaw. Set bestRaw=raw and **both** choice and bestBuild=-i. Lower
   choiceScore and bestBuildScore individually only if b is smaller; thus the
   chosen wonder can retain an older wonder's smaller weighted score.
   Otherwise independently compare b to choiceScore and bestBuildScore. A
   strictly better choice also sets bestRaw=raw. Equal values retain prior IDs.

bestRaw is initialized by the first winning wonder, not at entry. The ordered
candidate loop establishes it before the later-wonder comparison. Do not turn
the procedure into a single sort of final weighted scores.

## 7. Diplomacy and trade units

The special loop admits C06-buildable types with signed role>5. Start b=999,d=0.
Role Diplomacy (6) uses the procedure below; Trade (7) uses the trade procedure.
Other accepted role values retain 999. Apply section 4 common weighting, then
add b/4 if Barracks. Strictly improve choice and bestBuild independently with
the **unit identity**. Special units compete for bestBuild, not bestUnit.

### Diplomacy role

Call V07 behind-in-race again, capturing q. If nonzero, clear it when C knows
Space Flight, the live Component prerequisite, the live Module prerequisite and
Fusion Power, in that short-circuit order. Still within the initially nonzero
block, Urgent≠0 or Disorder clears q; below Prince also clears q. Human Autobuild
skips this candidate outright, after those query effects.

Only when `J[t]<=q` and `N[t]<regionalCities` calculate further; otherwise retain
999 and d=0 and proceed to common weighting. Start b=11 and target absent.
Scan civilizations 1…7, excluding C. A civilization qualifies if it has cities
in R and either no Ceasefire/Peace C→other, or **all** these conditions hold:

- It is human and (its rank≥C's rank or q≠0).
- t is the fixed Spy identity, or its advance count exceeds C's by >7, or q≠0,
  or its rank=7.
- Alliance C→other, or q≠0, or its government<Republic, or its regional attack
  total≤C's regional military count, or C's regional city count=0.

Retain the **last** qualifying civilization. No alive test or nearest-city
selection is implied. If a target exists, set b=`clamp(10-(targetAdvances-
ownAdvances),2,10)`. The next Ceasefire/Peace test uses the **exhausted scan
index 8**, not target. Its backed interpretation is in Appendix A. If that test
is true, increment b; additionally N[t]≠0 and q=0 replaces b with 999.
If target's military strength≤C's, set d=T.

With no target, set target=H. Only for P=4 and difficulty≠Chieftain, set d=T,b=8.
If `active(t)+queued(t)<(totalCities+3)/4`, set b=7 when H's gold<C's; decrement
b when twice H's gold<C's. If H has more advances, subtract 3 when N[t]+J[t]=0,
then subtract the advance difference. Clamp to `active(t)+1…10` using F01's
ordered clamp. After this policy block, halve b if q≠0; N[t]≠0 replaces b with
999 regardless of policy. The target remains H for subsequent prerequisite tests.

For either target path, Communism decrements b. If q≠0 and b<20, scan ship
improvements 35…37. Query C's knowledge of that improvement's live prerequisite;
if absent, query target's knowledge using a freshly read prerequisite. If target
knows it, set d=T and lower b to `min(b,2*J[t]+1)`. When J[t]=0 and active(t)=0,
decrement b; if choice is currently a ship group (-37…-35), replace **choice
only** with t. Continue all three tests, then apply common weighting/comparisons.

### Trade role

Skip if HadCaravan is set. Start b=10; if the signed trade-route count r<3,
use `10-((5-e)*totalTrade)/10`. Set b=b+2*r; if b<3 replace it with 2. Add C's
retained regional trade-unit count. Skip human Autobuild after these calculations.
Otherwise perform the common special-unit weighting. No route destination,
commodity or active trade-unit recount occurs here; those belong to C07/A07.

## 8. Ordinary-role units

### Eligibility and initial score

For type t ascending, read signed role r. Require r<6, C06 unit eligibility,
and t≠the fixed Carrier identity. The signed comparison admits negative custom
roles; it is not an implicit 0…5 validation. Additional gates are:

- Settle requires size>1 and either settlerFoodCost≤F, or all of: no supported
  settlers, `active(Settlers)+active(Engineers)=Flagged`, and
  `J[Settlers]+J[Engineers]<2`.
- Warriors requires government<Monarchy. A signed attack≥99 requires active(t)<4
  and queued(t)<2.
- AirSuperiority requires `active(Fighter)+queued(Fighter)+active(StealthFighter)+
  queued(StealthFighter) < H.Bomber+H.Helicopter+H.StealthBomber+2`, and either
  no Peace C→H or active(t)+queued(t)=0.
- For human C, reject Warriors when the Phalanx prerequisite is known, Archers
  when the Legion prerequisite is known, and Horsemen when the Chariot or
  Elephant prerequisite is known, querying in that order.
- For all C, reject Chariot when the Knights or Crusaders prerequisite is known;
  reject Elephant when the Knights prerequisite is known. These tests use
  fixed rule identities and their current prerequisites, not a general upgrade
  graph. C06's prerequisite query semantics apply to negative markers.

For Land, start b=`2*J[t]+N[t]`. For a human, halve it and add 2 for Marines,
Alpine Troops or Partisans. Archers adds `2*J[Legion]+N[Legion]`. When a<0,
Knights adds `2*J[Crusaders]+N[Crusaders]`; otherwise Crusaders adds
`2*J[Dragoons]+N[Dragoons]`.

A Land Attack type with zero attack is treated as an explorer: reject for P=0
or 1. Its cap is 1 for P=4, otherwise `(regionTiles+199)/200`. Require the
unadjusted `2*J[t]+N[t]<=cap` and active(t)<2. For every Land type at P=4,
add `1-e`, doubled unless original role is Settle, then halved if Offshore.
Additionally with Offshore and original Attack role, subtract a+1, doubled if
C knows the Transport prerequisite, then clamp b to 0…99.

For non-Land, require regional military≥4 when regional cities<2, else ≥8.
Set `b=((active(t)+2*queued(t))*regionalMilitary)/(Ut+1)`. With SpotSubmarines,
subtract H's Submarine count; if the result<1 replace with 0.

### Defense override and role gates

After the initial score, Paradrop changes the effective role to Attack.
When Need≠0 and (C is AI or NoDefend is clear), Defend gets b=2/Need, replaced
with -1 when Need=2 and U<2; every other role is rejected when Need=2.

For roles other than Defend and Settle, double b when P equals the effective
role, otherwise quadruple it. Partisans is allowed only with Need=0 and
StrongerEnemy. Every Sea type requires a coastal-city flag and adjacent-sea
query result neither 0 nor 63. Require either more than one bordering land
region, or a non-SeaTransport role with water-body size>47 and water ID<63.
The border query precedes the size/id alternative; no domain-only shortcut.

For SeaTransport at P=4 use `(3*b)/2`. Otherwise reject when P=Defend or there
is any active unit of this type. If still admitted and t≠Transport, replace b
with `(2*b)<<T` using F01's masked shift count. Settle uses the following
procedure; every other role multiplies b by 16.

### Settler score

Query Granary then, only if absent, Pyramids. Set base=4 when either exists,
otherwise 6. Choose trait z when `N[t]<(government+1)/2`, otherwise e.
Set `b=(base-trait-Expand)*b`. If supported settlers≠0, for each index j from
0 up to the live supported-settler bound add `(j+1)*penalty`, with penalty 16
for size<4, else 8. Size<2 doubles b. If N[t]=0 and settlerFoodCost=F, subtract
b/2. This retained branch can be reached by direct helper/custom-state inputs
even when ordinary parent eligibility has already excluded size<2.

Only when Need<2:

1. If J[t]=0, government between Monarchy and Fundamentalism inclusive halves
   b when `N[t]<clamp(regionalCities>>1,1,3)`, then halves again when
   `N[t]<regionalCities/5`. For government≥Republic use instead
   `N[t]<clamp(regionalCities-1,1,6)`, then `N[t]<(regionalCities>>2)`.
   Anarchy and Despotism apply neither pair.
2. If N[t]=J[t]=0, halve b regardless of policy. Unless P=4, compute threshold
   e+3, or e+2 for P=Defend; increment it for P=Expand. If regional cities are
   below threshold, replace b with zero.

### Combat, support and final unit comparison

1. Multiply b by signed rule cost+2. If both attack and defense are zero,
   replace b with zero. Otherwise hpFactor=2 with NormalCombat disabled;
   with it enabled use `clamp(hitPoints/10+firepower,2,4)`. Let
   `combat=(attack+defense)*hpFactor`; add 1 for X2VsHorse, then 1 for Partisans.
   Divide b by `((move/roadMultiplier+1)*combat)/2`. Do not round that divisor
   up or substitute a positive minimum. Invalid custom divisors are explicit
   failure/domain boundaries.
2. For NavalSuperiority subtract b/4 on LargeMap; subtract b/4 again if P=4
   and b>0. Double b when T=0, otherwise quadruple. If Port Facility, subtract
   b/4. Independently, domain Air doubles b and Airport then subtracts b/4.
3. Defend subtracts `(G+1)>>1`. Ut>119 quadruples b.
4. If Need=0 and effective signed role<6, start supportPenalty=0. If both
   support and totalShields are nonzero, use
   `((3-T)*(support+1)*b)/totalShields`.
   For Settle, when `active(t)-Homeless+J[t]=0` and `size>3-e`, force penalty=0;
   if `totalShields <= (e>=0 ? 1 : 0)+2*support`, subtract b/3. Other Settle
   cases halve the penalty. For non-Settle, double b if `(T=0 and
   totalShields<=supportUnitCount)` **or** C lacks AtWar. Add the previously
   calculated penalty after these adjustments.
5. Human Defend with NoDefend sets b=999. Replace choice when b<choiceScore,
   **or** choice is a building/wonder, K is not in Disorder, and either Need>1
   or (Urgent≠0, U<2 and StrongerEnemy). This latter override does not compare
   scores and is not separately restricted to Defend. Set choiceScore=b and
   choice=t. Independently replace bestUnit only for b<bestUnitScore.

## 9. Final choice and recommendation differences

After publishing the two optional recommendation IDs, apply one of these paths.
For AI C, or a human city without Autobuild:

1. AI currently building Palace keeps Palace, regardless of the provisional
   choice. Otherwise, a wonder choice without Temple becomes Temple.
2. Only when neither substitution above occurred, current production is a
   wonder, Same=0, provisional choice is not a wonder, and DisorderSecondTurn
   is clear: derive current wonder from the live production. Require Need<2,
   unexpired, raw wonder location NeverBuilt, and either Urgent=0 or
   DefenderLost clear. Query the current city stack's Defend count; when >1,
   keep current wonder production.

For a human Autobuild city, UnitsOnly takes precedence over ImprovementsOnly.
UnitsOnly selects bestUnit unless its ID>98 or bestUnitScore>500, which returns
99. ImprovementsOnly similarly uses bestBuild; this can be a diplomacy/trade
unit because of section 7. With neither flag retain choice unless choiceScore>500,
which returns 99. Comparison is strictly greater than 500; equality is accepted.
No generic final C06 eligibility recheck is added.

## 10. Conformance cases and failure policy


> Compare the gameplay/interchange observations in the
> [production scope](#production-contract-scope). Original memory/helper traces,
> arbitrary native callbacks and unsafe backing-byte cases in historical lists
> are optional reference, not additional production acceptance criteria.

Each case declares prior state, child results and retained read inputs. Compare
the return, both recommendation outputs, candidate scores where exposed by a
test boundary, ordered calls, persistent effects and random choices. Do not use
only the final production label as an oracle.

| Case | Required observation |
| --- | --- |
| Unitless AI in Despotism, two buildable non-paradrop Defend types, no outputs | Highest type index wins before ordinary scoring. Supplying either output suppresses this shortcut. |
| Barbarian current production is an improvement | Legion, or Dragoons with globally known Explosives; supplied outputs retain 999. |
| Unseen selected free-irrigation tile without River | Irrigation and Road are set; F increments once without recalculation. A visible selected tile blocks the change without fallback. |
| First admissible airlift donor lacks a role equal to P; later donor has one | No transfer; the first donor terminates the scan and remains the retained cursor. |
| Temple at U=2, z=0 and T=d=0, no later modifiers | Base -5 becomes -50; a generic nonnegative clamp is incorrect. |
| Equal Marketplace/Library scores encountered in that order | Marketplace remains the choice. |
| Palace scoring after a survey, retained slot size 15 and current city size 5 | Threshold tests use 15, if that slot is backed; they do not substitute current size. |
| SAM threat clamps score to 1 and SAMUnused is set | Base score is -2 after the post-clamp subtraction. |
| Structural race condition resets choiceScore | Existing choice and bestBuildScore survive until their individual comparison steps. |
| Expired Pyramids with base 6 before expiry, difficulty 2, Wb=0, group count=0 and E=0 | Raw after expiry/group adjustments is 116, from `3*6+50*2-2`. |
| Current production is non-wonder; supplied original rivalry byte has C's bit | The affected wonder's raw score changes to `2*b+1`. No supplied backing means a stop at that read, not a zero rival mask. |
| Existing chosen wonder raw 4/weighted 30; later wonder raw 3/weighted 40 | Later wonder IDs replace both choice and bestBuild, while both weighted minima remain 30. |
| A trade unit wins the special loop | bestBuild may be a nonnegative unit; bestUnit remains unchanged. |
| Diplomacy scan selects civilization 2, but C's attitude toward barbarian slot has the Peace-position bit | The exhausted-index treaty test observes that retained byte, not C→2's treaty. |
| Human CITYPREF selects Granary after another improvement established a lower score | Both IDs change to Granary; scores retain the prior values. The ordinary-role loop can still replace choice. |
| Human Autobuild has both UnitsOnly and ImprovementsOnly | UnitsOnly wins; score 500 is accepted, 501 returns 99. Published recommendation IDs precede this cutoff. |
| Other city has nonnegative production 99 | No J[99] overwrite. Preserve prior valid survey effects and use declared safe omission/rejection. |

Nominal rule files and ordinary city state can reach the exhausted-rule and
non-wonder rivalry reads. They are not automatically malformed-input cases.
Full equivalence for a case requires the specified backing; an implementation
may choose a declared read repair or stop, but cannot call a neutral default
recovered original behavior. For custom arithmetic, preserve wrap before later
comparisons, even if a subsequent invalid index is safely rejected. A zero
divisor, missing input, invalid source read or rejected write retains prior
valid sales, irrigation, transfers, output initialization and child effects.
No transactional rollback is implied.

The following original representation/provenance sections retain diagnostic
supplied-byte and stop cases. X01's repaired profile controls implementation and
required tests, including fresh-game isolation and the named C07/A02/C05 repairs.

## Appendix A. Original representation, read boundaries and excluded bugs

The logical descriptions above do not require packed records, pointer-sized
integers, stack layout or bitsets in a new implementation. These details identify
reference inputs, exceptional read results and validation observations.

### Known retained reads

| Read | Original mapping and behavioral input |
| --- | --- |
| Exhausted unit rule | `00499d07` reads role offset 0x12 at rule index 62, following the 62-entry clear loop. Each rule is 0x14 bytes. The independent reconstruction maps this byte to `gEventPoolBacking+0x12`, directly after the rule table. Supply its signed8 value at each reached read, or stop after incrementing Ut. Do not copy the current unit's role or synthesize a 63rd valid rule. |
| Retained city size | Palace, human Granary and Offshore Platform use the city scan cursor left by city survey, admitted expansion scan or admitted donor scan. A completed scan often leaves `numCities`; an early donor break leaves that donor. A backed inactive slot retains its size. Index 256 or another unbacked slot requires supplied read backing or a stop. No identity check or zero initialization is implied. |
| Exhausted diplomacy treaty | At `0049e428` the scan cursor is 8. Only the low byte of treaty slot 8 is tested for Ceasefire/Peace. Treaties begin at Civ+0x20 with eight four-byte entries; this byte is Civ+0x40, **attitude[0]**. Supply that current retained attitude byte. This is a read, not a write to an extra treaty record. |
| Non-wonder rivalry | At `0049db04` the live signed production byte is loaded; `0049db10` reads byte `[EBP-production-0xef]`. The produced 28-byte rivalry table starts at EBP-0xc8, corresponding to production -39…-66. Outside that range the original reads other frame state; use the mappings below or supplied original backing. |

Rivalry uses one bit per civilization; bit 0 also acts as the owned
completed-stock marker. The test masks the shift count to five bits, then tests
against the zero-extended byte. The subsequent signed score operation is
`i32(2*b+1)`. For the following mapped production ranges, successive productions
select bytes 3, 2, 1, 0 of the named little-endian signed32 frame quantity:

| Current production | Frame quantity | Initialization and required input |
| --- | --- | --- |
| -14…-11 | EBP-0xe4, later cached unit role | No earlier writer in this admitted wonder path; later ordinary-unit role/zero stores do not initialize it retroactively. Supply incoming bytes or stop. Carrying the preceding call's role is not established initialization. |
| 10…13 | EBP-0xfc, Expand | Initialized zero at `00498e97`, conditionally set to 1 at `00499f22`, conditionally incremented at `00499f88`. Use the actual computed Expand value, not unknown or zero backing. |
| 18…21 | EBP-0x104, desired defender quota | Written at `0049a51e` and possibly `0049a5de` when at least one defender exists. Use the calculated quota, including Palace minimum. With no defenders this local is unwritten: supply its incoming bytes or stop. Need=2 does not initialize it. |
| 22…25 | EBP-0x108, own spaceship cost | Initialized at `0049c5c5` and accumulated at `0049c643` only after the spaceship-score gates. Use that computed value if reached, otherwise supplied incoming bytes or stop. |

Other non-wonder productions require their selected original backing byte at
that read boundary; this specification does not invent the rest of the original
frame. A fixture may supply only the needed byte with its provenance, rather
than recreate a native stack. Known producer mappings above take precedence over
arbitrary supplied values. Reading the supplied byte has no corruption effect.
The reader must still distinguish a backed value from unavailable backing.

OpenCiv2 B21 returns zero for non-wonders in every mode. Independent BF-202
likewise restricts the rivalry table to -66…-39. Older BF-006/BF-007/BF-025
initialized the role, ship cost and quota to zero; these were deliberate repairs,
not recovered initializers. They can change selection and the Autobuild cutoff.
The primary B21 ledger describes the array origin as EBP-0xcc; direct instruction
arithmetic and the independent evidence instead establish EBP-0xc8. For example,
production -39 reads EBP+39-0xef = EBP-0xc8.

### Original arithmetic disagreement

The independent reconstruction represents expired-wonder adjustment
as `6*score+25*difficulty` at its inspected revision. Primary source uses
`3*score+50*difficulty`. A fresh bounded disassembly of the pinned executable,
`0049d9f2`…`0049da35`, establishes the latter:

- `0049da13` loads unsigned difficulty; the two LEAs at `0049da18/1b` multiply
  it by 5 then 5, giving 25*difficulty.
- `0049da1e/21` loads score and triples it.
- `0049da24` forms `3*score+2*(25*difficulty)`, stored at `0049da27`.

This resolves the formula without adopting the independent expression or
claiming an original runtime replay. Retain wrap at these operations.

### Destination bugs and validation encodings

Survey J/N have 62 elements; rivalry has 28; CITYPREF has 32. Invalid indexes
must not spill into another local, goal, city, rule or civilization field.
Recreating an original spill with a named logical assignment is also excluded.
For a simple invalid increment, safe omission preserves the established remaining
survey. Where subsequent reads or continuation are unknown, reject/stop and
record the partial effects. Output destinations must be valid objects for their
specified result; arbitrary pointer corruption is not a required observation.

Selected flags used here are AIColonist=0x0200 on units; on cities, Disorder=1,
Autobuild=0x10, WonderBuilt=0x100 and SAMUnused=0x40000. Other flag meanings and
field conversions are owned by F02 and their producing algorithms. Farmland
tests the tile feature pair 0x0c; free irrigation sets 0x04 and adds Road 0x10
when terrain byte's River 0x80 is clear. These encodings are evidence mappings,
not a required internal representation. Production is signed8, while the
original optional recommendation stores are signed32. The host port's final
pointer-sized store does not enlarge the classic output value's domain.

## Appendix B. Evidence and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary OpenCiv2 pin: `a71e8aefffe04e6f719e0e213b027b128a747711`, inspected at
`fc2155ccdb2123c1137babab6fa39e38152278c9`. All **26 selected complete bodies**
match the pin: the chooser parent, its 24 private parts and the CITYPREF loader.
The loader's algorithm is owned by F03. Independent revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752` in `civ2`
was reviewed for selected parent/scoring branches, exhausted indexes, retained
frame inputs, safe boundaries and deliberate rivalry repairs.

Original executable: MGE **5.4.0f, patch 3**, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
The hash and clean reference revisions were checked. Fresh bounded original
instruction reads covered the expired-wonder formula and rivalry address/test
at `0049db04`…`0049db37`. No native Windows build, original-runtime replay or
Ghidra annotation/database change was performed. Historical receipts and native
stack samples cited by the independent notes were not rerun or counted as fresh
conformance validation.

| Evidence | Scope |
| --- | --- |
| [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp), `00498e8b`, `city_choose_production` and its private parts | Complete chooser, survey, candidate order, scores and continuations. |
| Same source, `00498d40`, `city_load_citypref` | Ordered preference loading, retained NoDefend, name matching and 32-entry limit; owned by F03. |
| [civ2types.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h), [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h) | Retained numeric domains, fixed identities, roles, flags and record mappings. |
| [BUGS.md](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md), B21 | Always-on non-wonder rivalry guard; its original array-origin description is corrected above. |
| Independent `src/city_production_choice.cpp`, `src/window_startup.cpp` | Explicit read boundaries and original instruction locations, chooser and preference reconstruction; expiry expression and repairs remain distinguished. |
| Independent `analysis/reference/bugfix-log.md`, BF-006/BF-007/BF-025/BF-202; `analysis/city/capture/production-chooser/expansion-alias/README.md` | Incoming role/quota/cost versus initialized expansion, later repairs, original read consumers and historical validation limits. |

A standalone C++ harness compiled all 26 bodies and passed **38 assertions** in
optimized and AddressSanitizer/UndefinedBehaviorSanitizer builds, with leak
detection disabled. It uses the selected original rule/game record definitions,
asserts civilization/unit/city sizes and compiles with signed wrap and disabled
strict aliasing. Pointer-sized host adapters avoid truncating host addresses.
Two explicit read adapters replace the unbacked exhausted-rule access and the
primary non-wonder guard with supplied original read values or bounded stops.
The exhausted treaty read uses the backed civilization-object byte at the
original offset. These adaptations do not claim the original initialized the
supplied bytes, or validate a complete unknown native frame.

The executed sample covers preference retention/termination/cap, irrigation and
human visibility, city rivalry/cursor retention, exhausted-rule counts/stops,
defense quota, sale order/gold wrap, donor early break/transfer, stale size and
negative scores, strict improvement ties, SAM flag, spaceship score reset,
original expiry arithmetic, raw-versus-weighted wonder selection, supplied-rival
effects/stops, trade recommendation class, exhausted diplomacy treaty read,
defense unit score, Carrier exclusion, non-Land quota, settler expansion, final
Temple/Palace substitutions, Autobuild cutoff, reverse early return, barbarian
outputs, CITYPREF override and army-cap fallback.

Adapters supply city recalculation, C06 eligibility, C08 ownership/facility
queries, knowledge/space queries, map/radius/stack services, unit lifecycle,
secondary draw results, text parsing and notices. The preference cases exercise
the actual loader with supplied lines/tokens/names; F03's parser validation is
separate. The airlift sample covers the actual donor body and no-interceptor
continuation with lifecycle adapters; U05 owns interception's separate checks.
The fixture's initial compile used a wrong unit field name, corrected to the
actual retained field. An initial defense case accidentally chose Warriors
under Monarchy, where eligibility correctly rejects it; changing the fixture
to Phalanx resolved that case without changing an algorithm body.

Remaining joins include actual C03 recalculation/C06 eligibility and spending,
C08 effects, A01-produced counts, A03 terrain advice, V07 group selection,
U01/U05 transfer/interception, P06 advice/automation and consecutive F04 city
turns. Direct accepted-custom-input wrap, all 28 wonder branches, original
incoming-frame histories, live callback mutations and complete native UI/network
sessions are not exhaustively exercised by these 38 checks. X02 should add
independent original-state traces, including read-backed cases and safe
excluded-write outcomes. These finite checks do not establish complete original-program or whole-game
conformance.
