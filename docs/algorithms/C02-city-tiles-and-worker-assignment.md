# C02: City tiles and worker assignment

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in C02 |
| --- | --- |
| Gameplay | Tile eligibility/yields, worker/specialist assignment, deterministic allocation ties, overlap and accepted manual changes. |
| Original files/content | F03 yields/abilities and F06 worker/specialist/city fields. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Pick-table pixel hit testing, private bit packing, workspace addresses and control redraw sequence. |

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

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
semantic publications and failure effects. Only dependencies within the binding
scope above constrain evaluation; storage/API choices need no original-trace
equivalence proof. External file/wire definitions remain binding at adapters.

## Target, ownership and inputs

This specifies classic Civilization II Multiplayer Gold Edition **5.4.0f,
patch 3** city-square availability, individual tile yields, automatic worker
allocation, specialist storage/normalization and the city-window assignment
commands. Appendix B records the validation limits.

[F01](F01-determinism-and-arithmetic.md) supplies signed arithmetic, shifts and
retention; [F02](F02-game-state-and-invariants.md#the-shared-city-workspace)
owns shared workspace/cache dispatch and record identities. [F03](F03-rules-and-configuration.md)
owns interpreted terrain, government and cosmic rules. [W01](W01-map-geometry-and-spatial-state.md)
owns geometry, ordered B neighborhoods, resource/grassland-shield predicates
and tile setters. [U01](U01-unit-lifecycle-and-stacks.md) owns stack access.
[C01](C01-city-lifecycle-and-ownership.md) owns city lifetime and radius refresh;
[W05](W05-pollution-and-global-warming.md#ai-pollution-tally) owns the AI pollution
tally contributed during the availability pass.

[C03](C03-city-economy-and-corruption.md) owns the complete recalculation/economy
pipeline and recurring food/trade yields; C04 owns moods;
[C05](C05-city-growth-and-support.md) owns support and food requirements; C07 owns route creation/delivery; C08 owns active-wonder
and facility queries. A03 consumes terrain-work suggestions, V02/A08 government
scores, and [P06](P06-city-interface.md) owns city-window layout, hit surfaces and drawing. C03 also
owns the combined capital/overlap pass required to produce worker availability.

Nominal inputs are initialized map/rules, valid city/civilization/unit slots,
finite supported stacks, B[0..20] worked positions, yield kinds Food/Shields/
Trade, and valid window/provider state. City size is signed; defined scalar
wrap remains observable even with custom rules or editor input. F01 defines
reference expression results. Preserve signed terrain yield/bonus views,
nonnegative cosmic food-per-citizen values and the `s16` conversion on stock/
government-score publications. Government is a logical category; admitted raw
values retain the original unsigned view. The original widths are reference
annotations, not required types. No routine below draws random values
directly. Invoked children keep their actual side effects and draw contracts.

Out-of-bounds writes and the damage they cause are excluded bugs under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs).
Missing read backing is an explicit boundary, not a zero-valued rule or tile.
A stricter accepted-input domain may reject off-nominal state. Do not turn an
in-bounds scalar shift/wrap, deliberate shared workspace or retained slot into
an excluded overwrite merely because a reconstruction represented it awkwardly.

## Logical state and call order

The current city record holds a worked-position set, a specialist count modulo
64, sixteen explicit specialist kinds, signed size and food stock, plus flags
Disorder, Celebrating, OnEdge and SpecialistHeld. The center is B[20]. Positions
21..24 exist for surrounding calculations but are not ordinary worked squares.

The shared workspace contains:

| State | Meaning |
| --- | --- |
| Availability[0..24] | Sets of Unusable, HostileUnit, CityPresent, OtherCityWorked and HumanBlocker properties. Any nonempty set blocks an ordinary worker. |
| tileYield[Food,Shields,Trade] | Most recently accumulated tile result. |
| totals[Food,Shields,Trade] | Running city yields. The named total-shields/trade fields are these same values, not separate snapshots. |
| unassigned, specialistCount, workersOverflow | Assignment remainder, normalized UI/economy count, and overassignment result. |
| supportedSettlers, settlerFood, shieldSupport | Inputs produced by C05's support phase. |
| capitalDistance, capitalCity, capitalLink | Outputs of the combined pass below. |
| jobFound, jobPriority, jobX/Y | Shared terrain-work suggestion state, retained unless a stated caller resets it. |
| processing, reallocating, applyYield | Independent mode flags: city turn processing, AI specialist reconsideration, and application of the current tile yield. |

A full recalculation runs availability, combined capital/overlap, then support
before assigning workers. It then processes food routes, normalizes specialists,
and continues with industrial/pollution and trade/happiness calculations.
C03 owns this parent and later stages. Assignment's food-stock marker is computed
**before food-route adjustments**. The ordinary city-turn caller resets jobFound
and jobPriority and enables processing/reallocating before its later full
recalculation; these child routines do not add those resets themselves.

F02's non-full calculation returns cached happiness without rebuilding this
workspace or checking its city. The manual controllers below call that non-full
entry first; do not replace it by a fresh availability/assignment pass. Full
recalculation occurs in the subsequent window update only when its gates pass.

## Availability pass

`calculateTileFlags(city)` captures the city's signed owner c, then visits
B[0..24] ascending. For each index form the coordinate from the city's **live**
x/y, wrapping x once. Clear that workspace entry before testing it.

1. Test map bounds, even for outer entries. If outside, or index>20, mark
   Unusable and finish this entry. Otherwise require exploration by captured c;
   if absent, mark Unusable and finish this entry.
2. At this point perform [W05's AI pollution-tally contribution](W05-pollution-and-global-warming.md#ai-pollution-tally)
   for this visit, before occupation/city tests. It can accumulate on repeated
   calls, unworked squares, Ocean and overlapping radii.
3. Obtain the first unit at this coordinate. Traverse through U01 stack access,
   stopping the entire search immediately on an absent unit, an own unit,
   Ocean, or alliance with the encountered unit's live owner, in that order.
   Otherwise, if its type's attack is nonzero, mark HostileUnit and additionally
   HumanBlocker if its live owner is in the live human-player set; then stop.
   A foreign nonattacker advances to the next unit. Do not search past an own
   or allied unit for a hostile unit later in a mixed stack.
4. Independently query tile city owner. If nonnegative, mark CityPresent. This
   uses W01's City predicate: an Airbase alone does not pass.

No distance, visible-enemy flag, worked-assignment or city-owner equality test
is added. HostileUnit includes any nonzero attack value, including a signed
negative custom rule byte. Occupation on Ocean does not block work through
this pass. The center can receive flags but automatic allocation treats it
specially below.

`tileStatus(ignoredCity,index)` returns the unsigned value/set of the shared
availability entry. It does not look at that city or rebuild anything. Unknown
stored flag components also make the set nonempty; release and manual selection
do not mask them away. Unsupported indices require supplied read backing or a
stop before the read.

## Combined capital and shared-radius pass

[C03's combined pass](C03-city-economy-and-corruption.md#combined-capital-and-shared-radius-pass)
runs after availability and before support/assignment. It adds OtherCityWorked
to the existing flags while selecting a capital and computing distance/link
state. It owns the scan order, overlap offsets, capital ties and connection
queries; assignment consumes those outputs without an independent overlap pass.

## Tile yield evaluation

### Entry and base value

`tileYield(city,index,kind)` captures city's signed owner c. Derive B[index]
coordinate from current city x/y, wrapping x once. Out of bounds returns zero
before ownership, terrain, job or score effects. Valid indexing of B is a caller
requirement; no ordinary-radius limit is added by this routine.

If the **live applyYield** flag is nonzero, first set radius claimant to c with
synchronization. Then query city-or-unit occupant owner; if absent, set occupant
owner to c with synchronization. This tests City/Unit presence, not merely an
already stored owner. These operations occur once for **each yield-kind call**.

Read terrain and W01 special-resource class. Select the corresponding normal,
first-special or second-special terrain rule row, then load this kind's signed
base yield. If game mode is nonzero and double-production is enabled, double
**this base only** with signed32 retention. Retain the current tile feature
components for the remainder; later callbacks do not refresh that feature set.

### Terrain and feature modifiers

Apply exactly one row below, then continue with the common stages. “City
component” here means the component's presence, including the Airbase composite;
it is deliberately different from the tile-city-owner predicate.

| Branch | Ordered operations |
| --- | --- |
| Ocean, Food | Add1 if Harbor. Do not apply ordinary irrigation or Supermarket logic. |
| Ocean, Shields | Add1 if Offshore Platform. No Grassland suppression or center minimum. |
| Ocean, Trade | Add1 if the active Colossus location equals this city, even if base trade is zero. |
| Non-Ocean, Food | If neither City nor Irrigation component, call the irrigation-job helper. Otherwise add the base terrain's signed irrigation bonus; then query Supermarket. If present, call the irrigation-job helper when neither City nor Mine component is present; otherwise add `arithmeticShiftRight(value,1)` to value. Irrigation+Mine therefore supplies the farmland condition. |
| Non-Ocean, Shields | If Mine without Irrigation, or City component with base-terrain irrigation bonus zero, add signed mining bonus. Otherwise, only when neither Irrigation nor Mine is present, call the mining-job helper. Next, Grassland with no W01 shield pattern forces value0, even after a mining bonus. Finally center index20 changes exactly zero to1; a negative value is not changed by this minimum. |
| Non-Ocean, Trade | Add1 for the live raw River flag. When City or Road component is present, add1 if terrain is Desert/Plains/Grassland **or current value is nonzero**. With neither component, the same terrain/value condition instead calls the road-job helper. Next, if current value is nonzero and active Colossus is in this city, add1. |

Base-terrain irrigation/mining bonuses are used even when the base yield came
from a special-resource row. Active-wonder lookup is C08's query, not a test
that the global location is merely nonnegative. The order of facility, wonder,
advance and job-helper calls is part of the behavior.

### Common stages, in order

1. **Richard:** for Shields, if active King Richard's Crusade is in this city,
   add1, including Ocean and an otherwise zero-yield tile.
2. **Railroad:** for Shields, a Railroad component grants `value += value>>1`.
   With no Railroad, a City component also grants this bonus if captured c has
   Railroad. Otherwise, if neither City nor Railroad component is present,
   query Railroad; if known and terrain is non-Ocean, call the road-job helper.
   Preserve the short-circuit advance queries; do not apply a production bonus
   merely because the civilization has the advance on an ordinary tile.
3. **Despotism effect:** if value>2 and the city's live Celebrating flag is
   clear, decrement captured c's Despotism government-score entry when live
   processing mode is enabled. Then subtract1 from yield when c's live
   government is below Monarchy. The score decrement is not limited to those
   governments or to a worked tile.
4. **Government trade:** if value is nonzero and kind Trade, increment c's
   Republic and Democracy government-score entries in that order when processing
   is enabled. Add1 to yield when c's live government is at least Republic, or
   at least Monarchy if the city's live Celebrating flag is set. A negative
   intermediate still passes the nonzero test.
5. **Superhighways:** for Trade with retained City or Road component, query
   Superhighways and, if present, add `trunc(value/2)`. This is truncating division,
   unlike the arithmetic right shifts in farmland/railroad modifiers.
6. **Pollution:** with retained Pollution component, set
   `value=arithmeticShiftRight(s32(value+1),1)`. Increment jobPriority with
   signed32 retention, set jobFound=1, and replace jobX/Y by this coordinate.
   These writes happen even when processing/applyYield are zero, when a job
   was already found, and when yield becomes zero. Repeated previews and the
   three yield kinds can each contribute again.
7. If value<1 return0; otherwise return value. There is no upper yield clamp.

Government-score changes retain signed16. The direct function does not update
`tileYield[]` or running totals. It is not a pure query: both government scoring
and pollution suggestions can occur during unworked candidate evaluation.

### Terrain-work suggestions

`noteTerraformJob(ignoredCity,c,kind,x,y)` accepts irrigation or mining kind.
Require live applyYield, processing, and **no jobFound**, in that order. Query
current terrain. Require its corresponding conversion rule not Disallowed,
its signed AI-government threshold nonzero, and, for irrigation only,
[W04's irrigation-source predicate](W04-terrain-work-and-improvements.md#irrigation-source-predicate).
Normalize c's government below Monarchy to level1; other values stay unchanged.
If the signed threshold<=that level, increment jobPriority, set jobFound and
store x/y. A negative nonzero threshold passes this comparison. KeepTerrain
and actual conversion outcomes both pass the conversion-rule gate.

`noteRoadJob(ignoredCity,c,x,y)` requires the same mode/no-job gates. If the
live raw tile has River, additionally require Bridge Building. On success set
jobFound and x/y, **without incrementing jobPriority**. It does not check move
cost, current Road/Railroad, ownership or a settler's ability to execute the job.

These are suggestion producers. They do not improve the tile or order a unit.
Pollution's later unconditional suggestion can replace their coordinate.
[A03](A03-ai-settlement-and-terrain-work.md#3-city-terrain-job-publication)
owns consumption, and F04's outer city phase owns initial job-state resets.

### Three-kind accumulator

`accumulateTile(city,index,apply)` assigns the supplied value to applyYield.
For Food, Shields, Trade in that order:

1. Call the direct yield evaluator and store its result in this kind's shared
   tileYield entry.
2. If **live applyYield** remains nonzero after the child, add the live tileYield
   entry to this kind's live running total, with signed32 retention.

On normal return set applyYield=0, not its incoming value. Do not clear totals,
job state or government scores here. Child callbacks can change applyYield and
therefore later accumulation. A failure before the tail does not imply cleanup
or rollback that the original did not perform.

## Worked-set primitives and blocked release

`isWorked(city,index)` and `setWorked(city,index,enabled)` read/change the named
worked-position bit; nonzero enabled sets it, zero clears it. Other state is
retained. For nominal indices0..20 these act on the worked set. Off-nominal
indices use F01 shift-count reduction and may access reserved scalar components
or specialist-count components of the same 32-bit value; Appendix A defines
that mapping. This is a scalar operation, not an array overwrite.

`releaseBlocked(city)` scans positions0..19 ascending. Read each shared status,
remember whether any HumanBlocker component occurs, and clear that city's worked
selection whenever **any** status component is present. Return the collected
HumanBlocker property. It does not touch center20, stored specialist count,
other scalar components or the availability entries themselves.

## Automatic worker allocation

### Preparation and retained assignments

`assignWorkers(city)` starts newWorked empty, kept=false and workersOverflow=0.
Capture owner c. Set shared unassigned to `s32(signedSize+1)`; the extra one is
the center worker. Capture the stored specialist count as unsigned target S.

For captured c currently nonhuman:

- If not Disorder, reallocating is nonzero and S>0: decrement S when OnEdge is
  clear; otherwise set SpecialistHeld. This does not immediately change the
  stored count.
- If Disorder, clear the entire worked/count value; retain the previously
  captured S for the later comparisons.

If S==0, clear SpecialistHeld, regardless of controller. Then call blocked
release, retain its human-blocker result, and clear all three running totals.
Copy current status0..19 into a local taken array; set taken[20]=0 explicitly.
It is a separate center slot, not the contents of availability[20].

Attempt to keep existing worked positions if any of the following holds:

- Captured c belongs to the live human-player set.
- `(liveTurn + citySlot) modulo 4` is nonzero.
- The city's live Disorder flag is set.
- Blocked release returned HumanBlocker.

Otherwise an AI city reoptimizes on its staggered fourth turn. In the keep path:

1. Count selected positions0..20 by decrementing shared unassigned in ascending
   order. If the signed result<0, set workersOverflow=1 and reset unassigned
   to the city's **live** signed size+1. Retain no positions in this case.
2. Otherwise reset unassigned to live size+1 and visit **center20, then0..19**.
   For each position whose worked bit is currently set, accumulate it with
   apply=1, set its local taken entry1, include it in newWorked, then decrement
   live unassigned. Stop this scan when unassigned becomes exactly zero.
3. If current unassigned equals S or zero, set kept=true and skip all further
   placement. A missing center is not repaired after this shortcut.

Overassignment causes reconsideration rather than truncating the old set to
its first N entries. Live reads and accumulated child effects remain ordered;
old stored assignments remain observable until their stated clear or final
replacement.

### Center and food-first placement

If not kept and signed unassigned>0 and taken[20]==0, first include center20,
accumulate it with apply=1, set taken[20]=1, and decrement unassigned.

Set lastSelected=0 and capture divisor=max(unsigned foodPerCitizen,1). Repeatedly
run the food-first selection while all these conditions hold:

- `s32(totals.Food + trunc(unassigned/divisor)) < foodRequirement()` **or** the
  live signed city size is<3.
- The unsigned representation of unassigned differs from S and
  `s32(unassigned-S)>=0`.
- lastSelected is nonnegative.

`foodRequirement()` reads live signed size, unsigned foodPerCitizen and shared
supportedSettlers/settlerFood, returning their signed32 sum of products. The
captured divisor does not replace later live rule reads in this requirement.

For each round set lastSelected absent, bestFood=0, bestSecondary=0. Scan0..19
ascending with taken==0:

1. Call direct Food yield, then direct Shields yield; capture these two results.
2. If live unassigned==1, live totals.Shields==0 and `s32(shields*2)==0`, skip
   the candidate **without a Trade call**.
3. Otherwise call direct Trade yield and form secondary=`s32(shields*2+trade)`.
   Select when food>bestFood, or food==bestFood and secondary>bestSecondary.
   Replace both scores. Exact equal scores leave the earlier position selected.

If a position was selected, mark it taken, accumulate it anew with apply=1,
include it in newWorked, then decrement unassigned. Candidate results are not
reused for the selected accumulation; these repeated calls and their side
effects matter. A zero-food/zero-secondary candidate never beats the initial
scores. Failure to select ends this phase through lastSelected, not the whole
allocator.

### Remaining weighted placement

After the food phase, if captured c is now nonhuman, live Disorder is set and
reallocating is nonzero, set SpecialistHeld and increment local S. This branch
runs only when the kept shortcut was not taken and can raise S from63 to64.

While unsigned unassigned!=S and `s32(unassigned-S)>=0`, scan untaken0..19 with
bestScore=0 and bestPosition absent. For each candidate:

1. Accumulate it with apply=0, publishing its three tile yields and retaining
   any government/job effects.
2. Compute food denominator
   `clamp(s32(totals.Food-size*foodPerCitizen-supportedSettlers*settlerFood),1,99)`.
   Food score is `trunc(16/denominator)*tileYield.Food`.
3. Reread signed size. Shield denominator is
   `clamp(s32(totals.Shields-shieldSupport),1,99)`; shield score is
   `trunc(s32(size*3)/denominator)*tileYield.Shields`.
4. Reread signed size. Trade denominator is `clamp(totals.Trade,1,99)`; trade
   score is `trunc(s32(size*2)/denominator)*tileYield.Trade`.
5. Add the three scores with signed32 retention. Strictly greater than
   bestScore selects the candidate; equal scores retain the earlier position.

The divisions precede their multiplications. Zero/negative scores cannot win.
If nothing wins, end placement and leave remaining citizens as specialists.
Otherwise mark taken, accumulate the winner again with apply=1, include it in
newWorked, then decrement unassigned at the loop tail. Scores for the next
round use the changed totals. Do not sort once, compare floating ratios or
maximize a final whole-city utility independently of this greedy sequence.

### Publication and food-stock marker

Replace stored worked positions by newWorked and stored specialist count by
unassigned modulo64; clear the unused intervening scalar components. The shared
unassigned value itself stays signed32 and is **not** reduced to the stored
six-bit domain. It can therefore differ from the stored count for large or
negative off-nominal populations.

Compare live Food total with a freshly computed food requirement. If insufficient
and signed food stock is exactly zero, decrement stock to -1. If sufficient
and stock is negative, set it to zero. Otherwise leave stock unchanged. This is
an assignment/recalculation marker, not C05's per-turn food accumulation, growth
or starvation. No specialist-kind normalization occurs inside this allocator.

## Specialists

Kinds are Unused, Entertainer, TaxCollector and Scientist. The first sixteen
positions are explicit. A query at any index>=16 returns Entertainer without
reading that city's kind storage; a setter at such an index does nothing.
Counting a kind scans exactly0..15 and compares each result to the supplied
kind; it does not count implicit entertainers beyond16 or restrict the count
to currently active specialists.

For negative indices, the original scalar shift wraps to a stored position;
it does not reject them. The setter does not mask an off-nominal kind argument
before merging it, so it can set other kind components **within the same scalar**.
Appendix A defines the exact encoded operation. This defined scalar behavior
is distinct from an out-of-bounds destination write; an implementation may
instead declare such arguments outside its accepted domain.

`adjustSpecialistCount(city,delta)` changes only the stored count. For each
positive step, increment modulo64. For each negative step, decrement only when
count>0; once zero, further negative steps leave it zero. Worked/reserved
components and explicit kinds are retained. A positive step at63 wraps to0;
negative changes do not wrap from0 to63.

`fillSpecialists(city)` copies shared unassigned **without a modulo reduction**
to workspace specialistCount. For indices0..specialistCount-1, query kind and
replace Unused by Entertainer. Then for indices specialistCount..15, set kind
Unused. Kind queries/setters beyond15 retain their rules above. A negative
workspace count skips the first loop and starts the clear loop at that negative
index, using the scalar wrapping behavior. This is not a city-size clamp, an
automatic tax/science optimizer, or a recount of the stored specialist domain.
C03/C04 supply each kind's economic/mood consequences.

## Manual assignment commands

### Tile click

Permission uses the currently selected city's signed owner: local observer or
full-map revelation is required. Otherwise return. Call non-full recalculation
on the then-selected city; its cached behavior is defined above. Do not replace
repeated selected-city queries below by an entry-time slot snapshot.

Resolve the user action to a world-relative tile offset in the city's resource
area. Find the first B[0..20] whose offset matches; no match returns. P06 owns the
semantic target mapping. New rendering/hit geometry may differ; the original
pixel/quadrant adapter is in [Appendix C](#appendix-c-original-presentation-reference).
Keep permission and the preceding recalculation before applying the selection.

For a matched center20, clear the selected city's complete worked/count state.
For an ordinary matched position:

- If currently worked, clear it on the then-selected city, then increment stored
  specialist count on the then-selected city.
- If unworked and shared availability at that position is nonempty, return
  without an update. This tests the existing workspace, not a new map query.
- Otherwise if workspace specialistCount==0, clear the selected city's entire
  worked/count state. It does **not** force the clicked square to be selected.
- Otherwise set the worked position, then decrement stored specialist count,
  independently querying selected city for those two mutations.

Finally invoke window recalculate/redraw with flag0. Clicking the center or an
unworked tile without specialists requests fresh default placement through that
continuation; it is not a direct swap with a specifically chosen old worker.

### Citizen/specialist click

Permission and initial non-full calculation use the same policy. Narrow the
supplied citizen index to signed16. The controller makes its repeated live
selected-city/size reads, including a discarded size-minus-specialist-count
read, then compares the signed index against live `size-workspaceSpecialistCount`.
Only an index at or beyond that worker prefix continues.

Recompute that prefix through the newly selected city, subtract it from the
index, and retain the specialist position as signed16. Require position<live
workspaceSpecialistCount; there is no independent nonnegative-position test.
Read its kind through a newly selected city. Read size through selected city
again:

- Size<5 and Entertainer: show ELVISERR for the then-selected city and return
  without mutation/update, regardless of the dialog's result.
- Size<5 and another kind: choose Entertainer.
- Otherwise increment kind; after Scientist wrap to Entertainer. Unused advances
  to Entertainer as well.

Write that kind using the then-selected city and retained specialist position,
then invoke window recalculate/redraw with flag1. An implicit entertainer at
index>=16 can lead to a setter no-op followed by the ordinary update; it does
not gain a new explicit storage slot.

### Recalculate/redraw continuation

P06 owns the window operation. It proceeds only if that receiver is not closed,
redraw-suspended or redraw-deferred and its city index is nonnegative. It calls
**full** city recalculation on that index, then draws citizens `(1,flag)`,
resource map `(1)` and info panel `(1)` through the shared current city window,
in order. No new owner check is added. Rendering/resource queries can themselves
call yield evaluation, including its unconditional pollution side effects.
If the window gate fails, preceding assignment mutations still remain.

The original citizen hotspot layout can select the adjacent visible citizen in
crowded rows. P06 must preserve classic hit selection separately from these
controllers; OpenCiv2's B10 centering correction is not a C02 gameplay rule.

## Nominal examples and conformance obligations


> Compare the gameplay/interchange observations in the
> [production scope](#production-contract-scope). Original memory/helper traces,
> arbitrary native callbacks and unsafe backing-byte cases in historical lists
> are optional reference, not additional production acceptance criteria.

- An ordinary non-Ocean Food base2 with irrigation bonus1 reaches3 before
  Despotism, then2. With Supermarket farmland it reaches4 before that penalty,
  then3. Pollution afterward rounds those positive values upward when halving.
- Double-production affects base, then Harbor/Offshore/other bonuses are added
  once. A zero Ocean Trade base with Colossus can become positive, whereas
  a zero land Trade value needs its earlier river/road stages to become nonzero
  before Colossus applies.
- A foreign armed unit on land blocks a tile; the same unit on Ocean does not.
  An own/allied earlier stack member stops the search even with a hostile later
  member. Another city's worked square adds a separate overlap reason.
- Equal food and secondary scores choose the lowest radius index. Weighted
  placement also retains the first equal score. All-zero candidates can leave
  unassigned citizens rather than filling every available square.
- An already kept set that consumes exactly size+1 can finish without selecting
  a missing center. A center selected by default is allowed despite its status.
- At specialist count63, a positive adjustment gives0; at0, a negative adjustment
  stays0. A workspace remainder64 stores count0 but normalization still observes64
  and implicit Entertainers; it is not silently truncated to zero specialists.
- An unworked-tile click with no specialists clears assignments for recomputation;
  it does not reserve that clicked tile. A non-full cache call does not ensure
  the availability workspace belongs to the displayed city.

Complete tests must compare map/state/workspace, government scores, job outputs,
significant call dependencies and child effects, not just final yields. Include signed custom
bonuses, scalar wrap, shortage markers before food routes, turn cadence,
reallocation/disorder, overassignment, seam/raw overlap, partial failures,
callback mutations and the distinction between an unsupported read and an
excluded invalid write. Full city/AI/UI/network integration remains separate.

## Appendix A. Encodings and original temporary storage

These mappings support snapshots and F06 persistence; an implementation need
not use the same layout.

| Item | Original encoding |
| --- | --- |
| Availability | Unusable1, HostileUnit4, CityPresent8, OtherCityWorked16, HumanBlocker32. Getter returns unsigned byte. Producer writes25 entries; other workspace storage is not cleared here. |
| Assignment scalar | Unsigned32: worked bits0..20, retained components21..25, specialist count26..31. Final allocation stores `u32(unassigned)*2^26 + newWorked`; set/test use `1 << (u32(index) AND31)`. |
| Specialist kinds | One unsigned32, two bits per explicit slot:0 Unused,1 Entertainer,2 TaxCollector,3 Scientist. For index<16, shift=`(u32(index)*2) AND31`; getter=`(value>>shift) AND3`; setter=`(value AND NOT(3<<shift)) OR u32(type<<shift)`. Index>=16 bypasses storage. |
| Flags | Disorder1, Celebrating2, OnEdge `4000`, SpecialistHeld `8000` hex. |
| Yield selectors | Food0, Shields1, Trade2. Normal terrain rows0..10; resource class selects row=`terrain+11*class`; three signed byte yields begin at row offset10, irrigation/mining bonuses16/17, conversion14/15, AI threshold20/21. |
| Shared output aliases | Three tile yields at `006a65b8..65c0`; three running totals at `006a65c8..65d0`. The reconstructed one-element declarations followed by named members describe contiguous shared vectors. They do not license unrelated overwrite. |
| Worker temporary | Twenty taken entries plus a center-assigned local immediately following them in the original frame. The reconstructed 21-entry array models that evidenced shared center slot. It does not model arbitrary adjacent-frame corruption. |
| Pick-table backing | Nominal nonzero quadrants read original offsets from `0062833b+q` and `00628343+q`. Some annotated declarations have one element but share neighborhood-table backing. Unsupported reads require their actual bytes, not guessed zero offsets. |
| Rule/wonder/facility slots | Governments Anarchy0, Despotism1, Monarchy2, Communism3, Fundamentalism4, Republic5, Democracy6; Railroad67, BridgeBuilding7; Colossus2, KingRichard8; Palace1, Supermarket24, Superhighways25, Harbor30, Offshore31, Airport32. |

Oversized/invalid workspace or record writes are excluded bugs, even if a
particular source adapter can name the adjacent destination. The scalar kind
merge and reduced shift above stay inside one intentional scalar and therefore
remain defined off-nominal behavior. Distinguish these from index arithmetic
that actually leaves the destination record/workspace. A safe implementation
may reject unsupported input before such a write, preserving earlier accepted
effects when following the compatibility continuation policy.

## Appendix B. Sources, checks and remaining integration

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`, inspected at reference checkout
`fc2155ccdb2123c1137babab6fa39e38152278c9`. Relevant sources are unchanged between
those pins. Independent evidence is `civ2` revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. Neither reference tree was changed.

| Family | Primary source/entries |
| --- | --- |
| Kinds, flags, worked set, proximity | [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp): specialist set/get/count `004e7549/004e75a6/004e75ea`; flags `004e7641`; worked get/set `004e78ce/004e790c`; capital/overlap `004e7967`; status getter `004f3d30`. |
| Yields and allocation | Same city source: tile yield `004e868f`; terraform/road suggestions `004e8c8c/004e8db5`; accumulator `004e8e4d`; blocked release `004e8ecf`; workers `004e8f42`; count adjustment `004e9719`; specialist fill `004e97ae`. |
| Controllers | Same city source: window update `00501780`; citizen click `00501819`; tile click `005022c0`; later rendering and city-turn callers. |
| Full recalculation | [retvals.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/retvals.cpp): `city_recalc` `004eb4ed`. |
| Geometry/rules/layout | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp), [city workspace](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/city.h), [terrain types](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h), [table initialization](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/gamedata.cpp). |
| Classic UI distinction | [BUGS.md](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md): B10 citizen hotspots; B16 signed population wrap. |

Independent `src/city_yield.cpp`, `src/city_workers.cpp`, `src/city_tiles.cpp`,
`src/city_proximity.cpp`, `src/city_window.cpp` and `src/map_yield.cpp` corroborate
modifier order, workspace/vector aliases, center temporary, signed counters,
explicit read boundaries and live controller inputs.

A fresh temporary source harness extracted **20 additional routines**: specialist
and worked-set primitives, capital/overlap, yield/suggestion/accumulator stages,
blocked release, allocation and normalization, status lookup, both manual
controllers, their window-update continuation, and the two W01 coordinate
resource predicates. It reused **109 previously extracted routines**, including
actual availability production, U01 stack access and W01 tile mutation/batching.
The three tile yields and three running totals used explicit shared vectors,
matching the original named-field aliases; this is not adjacent-state damage.

Optimized and address/undefined-sanitized builds both passed **146 assertions**.
Finite cases exercised:

- Base doubling, irrigation/farmland, Ocean facilities, active-wonder locations,
  Grassland suppression/center minimum, railroad, government and highway order,
  special-resource rows, signed custom yields and Airbase's City component.
- Pollution changes during a nonprocessing preview, three-kind accumulation,
  occupant/radius updates, signed government-score wrap, terraform thresholds,
  water-source checks and river/Bridge-Building job gating.
- Foreign stack traversal with own/allied/Ocean early stops, City/outer flags,
  unknown availability components and center retention during blocked release.
- Other-owner radius conflicts, palace ties, airport links overwritten by route
  results, capital-city Railroad links and raw-offset behavior across a seam.
- Food-first/weighted lowest-index ties, retained assignments without a center,
  overassignment repair, staggered AI rebuilding and human-blocker retention,
  OnEdge/reallocation/disorder, shortage markers, no-positive-score candidates,
  workspace remainder64 and signed negative size with safe scalar normalization.
- Specialist-count wrap/saturation, reduced-shift worked/kind access, unmasked
  kind merging inside its scalar, explicit/implicit kind boundaries and counts.
- Actual tile/citizen controllers with cached inputs, closed-window retention,
  center reset, no-specialist default reset, unavailable-tile rejection,
  entertainer restrictions, kind cycling, implicit-slot no-op and draw order.

The map used a fixed 20-by-12 backing grid and original neighborhood tables;
map access/basic predicates were adapters. Facility, active-wonder, advance,
road-connection, full-recalculation, layout/pick-surface, drawing and network
transport behavior was supplied at declared boundaries. The full-recalculation
adapter does not establish C03's complete worker/support/economy/happiness join,
and raw wonder-location fixtures do not establish C08 obsolescence. Optimized
checks are not original-executable runs. Host-sized pointer carriers and scalar
fixtures do not establish 32-bit address aliasing, every callback mutation,
malformed-read backing, actual pixels, peer receipt or full saved-state equality.
No invalid-write corruption was asserted as a compatibility result.

Independent `tests/city_yield.py`, `city_workers.py`, `city_worker_flags.py`,
`city_tiles.py` and `city_proximity.py`, with their probes, define bounded
original/source comparisons and independent scalar/flag/center expectations.
Their supplied rule/map state, selected child boundaries and explicit original
commodity-counter controls remain part of those fixtures. Definitions were
inspected, not rerun for C02. The independent original-executable target is
SHA-256 `edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Those bounded observations do not establish an unmodified original stack,
production initialization or an entire city turn.

[C03](C03-city-economy-and-corruption.md#appendix-b-sources-checks-and-remaining-integration)
owns capital/overlap and joins the actual cached/full parent in finite source
checks, with support/commodity/rule-effect adapters.
[C05](C05-city-growth-and-support.md#appendix-b--validation-and-remaining-joins)
adds finite actual support, C04 mood and turn-parent joins; complete consecutive
turn and C07/C08 rule-effect integration remains required. A03/V02/A08 must join job and government
score consumers; P06 must join real layout, picking, redraw and citizen hotspots;
N02/N05 must exercise actual tile-message/receipt behavior. X02 must retain
independent full-state and external-trace fixtures, including partial effects,
custom tables/rules and safe invalid-write boundaries. No fresh original
executable run or annotation export/change was made. These checks do not establish
complete original-program or whole-game conformance.

## Appendix C. Original presentation reference

The following original input geometry and drawing procedures are optional under
X01's presentation policy. The main text retains semantic selections, calculations,
file/wire effects and required ordering. This appendix is for original-adapter
validation, not a required UI design.

### Original city worked-tile hit conversion

Use P06's layout, scaling and pick-surface services in this order:

1. Read map zoom for tile width `scaleMap(64,zoom)`, then reread zoom for tile
   height `scaleMap(32,zoom)`. Origin x is resource rectangle left plus
   `scaleLayout(5)`; origin y is rectangle top plus `scaleLayout(11)` plus
   arithmetic half tile height. Subtract origin from input mouse coordinate.
2. Require both relative coordinates nonnegative and below four times their
   corresponding tile dimension. Compute coarse dx=`2*trunc(rx/width)-3`,
   dy=`2*trunc(ry/height)-3` and remainders. With nonnegative remainders, read
   the pick-surface pixel and compute quadrant=`arithmeticShiftRight(pixel-10,4)`;
   otherwise quadrant0.
3. For a nonzero quadrant add its signed pick offset. Nominal offsets are
   quadrant1=(1,-1),2=(1,1),3=(-1,1),4=(-1,-1). Quadrant0 adds nothing. Other
   pixel/table states require the actual supplied read backing or a stop;
   do not clamp to a valid quadrant. P06 owns generating the pick surface.
4. Find the first B[0..20] whose offset exactly matches dx/dy. No match returns.
