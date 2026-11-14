# C05 — City growth and support

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in C05 |
| --- | --- |
| Gameplay | Support, growth/starvation, food/production factors, facility eligibility and unit retirement, including tie rules and accepted prior effects. |
| Original files/content | F03 support/growth rules and F06 city/unit/civilization state. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Famine redraw locals, notice resource sequencing and implementation-local box-factor storage. |

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

Population/stock conversions, growth or starvation, support scans and unit/city
removal retain their dependencies. Independent arithmetic must not move these
publications across recalculation, notices or later turn phases.

## Target and scope

The target is **Civilization II Multiplayer Gold Edition 5.4.0f, patch 3**.
This document specifies food and shield box factors, settler food requirements,
unit support and military-unhappiness inputs, ordinary growth and starvation,
food accumulation, and support-shortage or disorder-driven unit removal. The evidence
and integration limits are stated below.

Use [F01](F01-determinism-and-arithmetic.md) for arithmetic and
[F02](F02-game-state-and-invariants.md) for records, live slots and shared
calculation state. [F03](F03-rules-and-configuration.md) supplies parsed rules.
[F04's city phase](F04-turn-sequencing.md#inside-one-city-turn) owns enclosing
turn order; [C03](C03-city-economy-and-corruption.md) owns full recalculation,
including the call to this document's support scan before C02 worker assignment.
[C04](C04-city-happiness-and-disorder.md) consumes military counts and owns
disorder, tax collection and **celebration** growth. C05 owns the shared
growth-facility query, used by both ordinary and celebration growth.

[W01](W01-map-geometry-and-spatial-state.md) supplies tile features, city-owner
queries and the two distance functions; [U01](U01-unit-lifecycle-and-stacks.md)
owns unit deletion, cargo and redraw continuations. [C01](C01-city-lifecycle-and-ownership.md)
owns exact city lookup and city deletion; [C07](C07-trade-and-commodities.md#ordered-route-removal)
owns shared route removal. [C06](C06-city-production-and-purchasing.md) owns production and consumes
shieldRows; [C08](C08-buildings-and-wonders.md) and V03 provide facility, wonder
and technology effects. C05 does not define their algorithms by replacing their
results with constants.

## Accepted state and observation boundaries

Nominal calls identify a live city and valid owner, unit types, routes, map
coordinates and rule tables; collection bounds fit their logical collections.
Governments and difficulty use the F03 domains. Food stocks can legitimately
be negative between turns. Food-route counts and partner are **incoming shared
workspace**, not necessarily values calculated for the current city: the food
turn routine runs before the first full recalculation.

For accepted off-nominal values, preserve the signed comparisons, ordered
narrowing and scalar wrap below. A restrictive implementation may reject
unsupported records or rule values and declare that narrower domain. Do not
silently clamp accepted population, food stock, difficulty, government or role.
Unavailable record/table reads require their actual supplied input or a bounded
stop; they do not produce invented zero or None values.

[X01's exclusion](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
applies to all children and output writes: overwriting an unrelated record,
array or workspace is an excluded bug, including a logical recreation of the
damage. Safe rejection or a specified bounded stop is acceptable. This does
not exclude a valid city's population or stock wrapping within its own field.
The original's undefined famine-redraw input is optional presentation evidence
under [X01](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy),
with its exact-reference boundary recorded below. It does not require arbitrary
memory access or stopping required growth/support processing.

The algorithms below make **no direct random draws**. Their actual children
retain their own random and external effects. Full city turns can draw in other
phases, including W05 pollution; a test must not infer a draw-free entire turn.

## Logical state and arithmetic

| State | Meaning and arithmetic contract |
| --- | --- |
| size | Signed population value. Each specified population update applies `s8`; a positive size can reach 127→−128. Internal storage may be wider. |
| foodStock, shieldStock | Signed stock values. Each specified publication applies `s16`, including this turn's surplus; later reads observe that result. |
| Food, Shields | Current C03 calculation outputs, signed32. Shields is already the current production result; shortage handling does not rederive it. |
| foodRows, shieldRows | Shared signed32 growth/production factors. They are outputs, not persistent per-city caches. |
| settlerFood | Shared food consumed by each supported Settler-role unit; selected from an unsigned rule. |
| supportedUnits, shieldSupport, supportedSettlers | Shared signed32 counts/cost. The support charge primitive updates the first two. |
| aggressiveUnits, homeAttackUnits | Shared signed32 military counts consumed by C04. They need not be disjoint. |
| foodSurplus | Signed32 amount computed once in the turn's accumulation phase; later recalculation does not recompute this turn-local result. |
| foodRoutesOut, foodRoutePartner | C03's incoming food-route workspace. A nonnegative partner can select famine text even when no route is removed. |
| windowShown, windowDirty | F04's shared turn/UI state. Child calls can change it; reread at the specified gates. |
| unit home | For these scans, its raw unsigned identifier is compared to the full city argument. The usual None interpretation is not applied. |
| unit role, type cost | Role comparisons here are signed8. Retirement shield credit uses signed8 type cost. Attack is tested only for zero/nonzero. |
| government, cosmic rules | Government and food/support/box/size-threshold rules are unsigned values. Government is captured for each support scan; other reads stay live. |

Use `s8`, `s16`, `s32`, truncating signed division and F01's wrap rules.
Arithmetic counters/products below use signed32 wrap unless a destination's
explicit narrowing applies. These are observable numeric contracts, not a
requirement to use the original record layout or host integer types.

`human(c)` tests the live human-control mask at selector `c`, with F01's low-five
shift semantics. Nominal civilization selectors are 0..7. Off-nominal selectors
8..31 do not match the control mask; higher selectors can alias after masking.
City owner is read with its signed semantics before indexing civilization state.

## Box factors and settler food

### Food-factor entry

`calculateFoodFactor(unusedCity, owner)` does not read its city argument:

1. Set settlerFood to the low-government rule when owner's live government is
   Anarchy, Despotism or Monarchy; use the high-government rule when its unsigned
   value is greater than Monarchy. No separate validation of higher values occurs.
2. Set foodRows to the raw unsigned food-box rule.
3. If owner is AI, replace foodRows with `aiBoxFactor(food, foodBoxRule)` below.

The support scan calls this first, then independently produces shieldRows from
the raw shield-box rule or `aiBoxFactor(shields, shieldBoxRule)`. The food turn
routine also calls it before examining foodStock. Human factors remain raw,
including odd or zero custom rules; they do not receive AI rounding.

### Shared AI factor algorithm

Start with `rows = 13-difficulty`; add one if difficulty<King, then one if
difficulty=Chieftain. For the six nominal difficulties, Chieftain through Deity,
this gives **15, 13, 12, 10, 9, 8**. Continue:

1. Apply the late-game adjustment only when all of these hold:
   - Food factor: turn>200. Shield factor: turn>=200.
   - difficulty>Warlord.
   - The incoming selector at rank entry7 identifies a human civilization.
     Use that supplied entry; do not recompute rankings or substitute bestHumanRank.
2. Within that gate, initialize `p=2`.
   - If Manhattan's **raw stored location** is not Unbuilt and
     `nonhumanKnowledgeTest(Rocketry)` below is true, set p=1; then set p=0
     if no human knows Rocketry **or** no human knows Nuclear Fission.
   - Set `a=p`.
   - If Apollo's raw stored location is not Unbuilt and
     `nonhumanKnowledgeTest(Space Flight)` is true, set a=p-1; then set a=p-2
     if no human knows Space Flight.
   - Subtract `clamp(a,0,2)` from rows.
3. If the raw box rule differs from10, set
   `rows = trunc(s32(boxRule*rows)/10)`; if rows is odd, increment it once.
   Return rows. There is no minimum-factor clamp.

Destroyed wonder locations pass the raw-location test: only Unbuilt is excluded.
Do not replace these tests with “owner has the active wonder.” Knowledge masks
are incoming game state; no technology traversal occurs here.

The original's `nonhumanKnowledgeTest(advance)` is true if **either** a nonhuman
civilization knows that advance **or civilization7 knows it**, even when
civilization7 is human. This second clause comes from a signed expansion of the
knowledge mask before comparison with the complemented human mask. Ordinary
human-knowledge tests remain “any human knows it.” Preserve the exceptional
civilization7 result; ordinary set difference would change the factor.

Example: King AI, turn200, highest rank entry selecting a human, no qualifying
wonder/technology condition, raw rules10: foodRows=10 but shieldRows=8. At
turn201 both are8. With Manhattan present, only human civilization7 knowing
Rocketry and Nuclear Fission, the signed-mask clause instead makes both9.

## Unit support calculation

### Charge one eligible unit

`chargeSupport(city, unit, capturedGovernment)` first increments supportedUnits.
Then decide whether to charge:

| Government argument | Charge condition after the increment |
| --- | --- |
| Anarchy or Despotism | supportedUnits > current signed city size. |
| Monarchy | supportedUnits > monarchy free-support rule. |
| Communism | supportedUnits > communism free-support rule. |
| Fundamentalism | Unit type lacks FundamentalismFree **and** supportedUnits > fundamentalism free-support rule. |
| Any other value, including Republic and Democracy | Always. |

If charging, increment shieldSupport and return its **accumulated value**;
otherwise return0. FundamentalismFree units still increment supportedUnits and
consume positions in the allowance. There is no preliminary city/unit liveness
test here: callers select eligible records. Only Anarchy/Despotism require the
size read; only Fundamentalism requires the unit's type flag.

The return is not a Boolean. If an accepted incoming shieldSupport of -1 is
charged, the new value and return are0. A caller testing return!=0 must observe
that result even though a charge occurred.

### Full support and military-count scan

Capture city owner and that owner's unsigned government at entry. Retain both
for the scan. Calculate food/settler factors, then shieldRows. Reset
shieldSupport, supportedUnits, supportedSettlers, aggressiveUnits and
homeAttackUnits to0. Scan unit slots in ascending order, testing the live bound
on each iteration:

1. Require a live unit, signed unit owner equal to captured owner, and raw
   unsigned home identifier equal to the full city argument. Other units retain
   their flags unchanged.
2. Clear this unit's Aggressive and CostsShield flags **before** the role test.
   If its signed role>=6, continue to the next unit.
3. Call chargeSupport with captured government. Set CostsShield if its return
   is nonzero. Retain the current aggressiveUnits as `before`.
4. If the type's attack is nonzero:
   - Air domain: increment aggressiveUnits unless role is Air Superiority.
     Independently increment homeAttackUnits if unit.x=city.x **or**
     unit.y=city.y. A bomber at home can count in both totals; a fighter can
     count at home without aggression. The OR test also admits distant units
     sharing only a row or column.
   - Other domain, both coordinates equal to current city coordinates:
     increment homeAttackUnits.
   - Other domain, elsewhere: query tile city owner. If it returns any
     civilization, do nothing further, regardless of that civilization's
     relationship to this city. Otherwise increment aggressiveUnits and query
     tile features. If it has the Fortress component without the City component,
     scan live cities in ascending order for one owned by captured owner with
     **ordinary W01 distance<4** from the unit. On the first match, decrement
     aggressiveUnits and stop that city scan. Airbase encoding does not qualify
     as this fortress case.
5. If aggressiveUnits differs from before, set the unit's Aggressive flag.
6. If role is Settler, increment supportedSettlers.

The scan does not assign workers, change stocks, refresh visibility or draw
random values. It clears the two flags for qualifying home/owner units even
when their roles later exclude them. Negative signed roles pass the `<6` test.
Raw home255 can match city argument255 in supported off-nominal backing; an
extra “None home” rejection would change this algorithm.

## Shared growth-facility query

`growthFacilityNeeded(city)` examines live signed size and unsigned thresholds:

1. If aqueductThreshold<=size, query Aqueduct; if absent, return Aqueduct.
2. Reread size. If sewerThreshold<=size, query Sewer System; if absent, return it.
3. Return None.

Aqueduct precedes Sewer even with reversed custom thresholds. The query has no
government, owner, food-stock or population-cap test and no direct mutation or
notice. C04 celebration growth requires None before incrementing size. Ordinary
growth below instead retains the result, attempts growth/refill, then rolls
size back if a facility was missing.

## Food turn: starvation or ordinary growth

Capture owner at entry and calculateFoodFactor(city, capturedOwner). Read live
foodStock **after** that call. A negative stock takes the famine branch; any
nonnegative stock takes the growth branch. Return1 only on the famine city-removal
continuation; ordinary completion returns0. F04 uses the nonzero return to
bypass the remaining city phases and their trailing cleanup.

### Famine selection and message

1. Scan live unit slots ascending for the first live unit whose type role is
   Settler and whose raw unsigned home equals city. **Do not filter by owner.**
   Retain its slot or None; no later search replaces this selection.
2. Begin with message Famine0. If no unit was selected, choose Famine1.
   Otherwise supply text argument1 from the selected type's name.
3. If the live shared foodRoutePartner is nonnegative, choose Famine2 and
   replace text argument1 with that partner city's name. This test does not
   depend on foodRoutesOut and does not validate a live route to that partner.
4. If autoplay is off, call the ordinary city notice with force=false and no
   sprite. The notice can be suppressed by its own gates. Selection is retained
   through the callback; route workspace, visibility and population remain live.

### Famine consequences and redraw input

Read foodRoutesOut after the notice:

- If foodRoutesOut<1 and no unit was selected, set size to `s8(size-1)`
  and capture the city's current x/y. If the resulting signed size<1,
  call C01 city deletion with local origin, then the captured owner's
  civilization-elimination check, and return1 immediately. Do not reset foodStock,
  execute the final famine redraw or set windowDirty on this return.
- If foodRoutesOut<1 and a unit was selected, call U01 delete-with-cargo on that
  retained slot, with refresh enabled, only if captured owner is currently human
  **or any human knows the city**. An AI city unknown to all humans retains its
  selected settler; no population decrease substitutes for the skipped deletion.
- If foodRoutesOut>=1, read the city's signed route count once. Visit its indices
  descending from count-1 to0. Remove each route whose signed goods is negative
  and whose partner equals the **live shared foodRoutePartner**, using C07 removal. After a removal,
  continue with the preceding index, without restarting or reloading count.
  All matching routes can be removed; unrelated destinations remain. This branch
  does not delete the selected settler or shrink the city, even if no route matches.

For every nonremoved-city continuation, set foodStock=0. The final redraw uses
radius0, live local-human observer and refresh=true. Its coordinates are:

- Population-shrink branch: the x/y captured immediately after decrementing size.
- Selected-unit or outgoing-route branch: **incoming original temporary x/y**,
  not defined by this routine. An optional exact-original presentation case
  requires supplied coordinates; without them that reference case stops after
  foodStock=0, before the final redraw and following windowDirty assignment.
  Earlier deletion/route effects are not undone. Required semantic UI may instead
  safely redraw the affected city/view and continue, preserving required external
  event delivery and the following live windowDirty update.

After the final redraw, if live windowShown is nonzero, set windowDirty=1.
Return0. Replacing the undefined UI input with this city's coordinates is now
permitted by X01's presentation policy; it is not evidence of the original value.
The two reconstructions choose different repaired read times; neither supplies
the original input. This permission changes no support, food or population rule.
No support, route-count or mood recalculation is performed by this routine.

### Ordinary growth and stock reset

Compute `limit=s32((signed size+1)*foodRows)`. If foodStock<limit, skip growth
and proceed directly to the final map redraw. Otherwise perform **one** attempt:

1. If windowShown is nonzero, draw the food box with presentation enabled.
2. Call growthFacilityNeeded and retain the result.
3. Set current size to `s8(size+1)`, even when a facility was
   missing. There is no classic cap at127.
4. Query Granary. Only if absent, query captured owner's Pyramids effect.
   If both are absent, set foodStock=0. Otherwise set
   `foodStock=s16((current signed size+1)*floor(rawFoodBoxRule/2))`.
   Use the raw unsigned rule, **not** AI-adjusted foodRows. Read size/rule after
   the relevant child query. Excess accumulated food is discarded; there is no
   subtraction of limit and no repeated growth loop.
5. If the retained facility is not None, set current size to `s8(size-1)`. **Keep the stock reset/refill from step4.** Supply the facility name
   as text argument1 and execute the growth-halt notice rules below.
6. If captured owner is currently AI and the city's live signed size equals2,
   invoke production selection with its existing request99 semantics. Do not
   treat99 as a literal production item. This test also follows a blocked attempt.
7. If live windowShown is nonzero, draw the food box, draw citizens with
   presentation enabled and secondary flag0, then set windowDirty=1. Once this
   gate passes, do not recheck it between the two child draws.

After either below-threshold or completed-attempt handling, redraw the city's
**live** coordinates, radius0, live local-human observer, refresh=true, and
return0. This final redraw alone does not set windowDirty in the growth branch.

For a size8 city with raw rule10, a full90 food, Granary and missing Aqueduct,
the attempted size9 refill is50; size then returns to8 and food remains50.
For a size3 Deity AI with raw rule10, 32 food is enough to grow to4, but a
Granary still refills to25, not20. With raw rule9 it refills to20.

Size127 can become -128. With Granary and raw rule10, that attempt writes
foodStock=-635. These are in-bounds scalar results. A following turn can take
the famine branch; its decrement of -128 wraps to127 and does not satisfy the
signed size<1 deletion test. Do not require an adjacent overwrite to produce
these values, or import the optional cap from a repaired implementation.

### Growth-halt notice rules

After supplying the missing facility name:

1. If captured owner is currently human, Tutorial is enabled, the Aqueduct hint
   has not been shown, and the missing facility is Aqueduct, supply the unsigned
   aqueduct threshold as number argument0 and call the Aqueduct sprite notice.
   Then OR the hint-shown flag into its **live** flags after the callback.
2. Otherwise, call FurtherGrowth with the missing facility's sprite only if
   the live production item is not that facility and GrowthHalted messages are
   enabled. This is a sprite notice with force=false.

The tutorial case is not gated by the GrowthHalted preference or current build.
These calls have the ordinary notice/zoom boundary described in
[C04](C04-city-happiness-and-disorder.md#notification-and-callback-boundaries);
calling a helper does not guarantee a visible dialog. Production selection,
city elimination and UI children retain their own external/state effects.

## This turn's food accumulation and warning

F04 captures `previousFood=foodStock` **before** calling the food turn routine.
If the city survives, it performs C03 full recalculation, the displayed-window
refresh if needed, and any worker-overflow notice. Then C05's accumulation is:

```text
foodSurplus = s32(Food - s32(size*foodPerCitizen)
                      - s32(supportedSettlers*settlerFood))
foodStock = s16(foodStock + s16(foodSurplus))
```

Use live signed size, unsigned foodPerCitizen and current shared calculation
outputs at this point, not the size or food used at entry. If all of these hold,
call the ordinary FoodShortage notice with force=false and no sprite:

1. Captured previousFood>0.
2. Live foodStock<previousFood.
3. `s32(foodStock+s32(foodSurplus*3))<0`.
4. FoodLow messages are enabled and autoplay is off.

The notice's own gates still apply. A city with zero incoming stock does not
get this warning even when the new stock is negative. Negative stock does not
cause a second famine pass in this turn. Likewise, filling the stock does not
cause immediate ordinary growth; those decisions were already made.

F04 then calls production, unsupported-unit removal below, and a second full
recalculation with worker reallocation enabled. New units from production can
be removed by the shortage pass in that same turn. That pass and recalculation
do not undo food already consumed by settlers or recalculate foodSurplus.
C04's later celebration growth has its own current-consumption check.

## Unsupported-unit and disorder retirement pass

Capture city owner and unsigned government at entry; retain them through all
restarts. This pass uses live Shields and updates support counters, but does
not itself rerun yields, worker assignment or military-unhappiness calculation.

### Outer scan and trigger

At each restart, reset supportedUnits=0 and shieldSupport=0. Scan ascending
live unit slots. A trigger candidate must be live, owned by captured owner,
raw-home-matched to city, and have signed role<6. Charge it using captured
government, ignoring the charge helper's return value. Enter selection if:

- Shields<shieldSupport; **or**
- city currently has Disorder, `(turn+unitSlot) mod8=0`, captured owner is
  currently AI, and captured government>Fundamentalism.

The latter permits strategic retirement while the city can pay its support.
It uses the existing Disorder flag before this turn's C04 transition. Save the
trigger slot as the continuation index. Capture
`withinSupport=(shieldSupport<=Shields)` for candidate filtering.

### Farthest candidate selection

Initialize bestDistance=-1 and selected=None. Scan live units ascending,
requiring liveness, raw-home=city and signed role<6. **There is no owner test.**
Do not restrict candidates to charged units or require nonzero attack.

When captured withinSupport is true, additionally exclude:

1. Signed role>4, including Settler role5.
2. Any tile with a nonnegative W01 city-owner answer.
3. A Fortress component without the City component, if an ascending scan of
   live cities finds one owned by the input city's **live owner** at ordinary
   W01 distance<4. Airbase is not this fortress encoding.

For each remaining candidate calculate W01 **weighted distance** from its live
coordinates to the input city's live coordinates. Replace selected and
bestDistance only when distance>bestDistance. First equal-distance candidate
wins. Keep the distinction between this metric and the ordinary fortress-radius
metric. A supported strange input returning distance<=-1 never wins.

### Shortage action, restart and no-selection continuation

After selection, test **live** Shields<shieldSupport again; do not use captured
withinSupport as the action branch. Child queries may have changed shared state.
If the shortage remains:

1. If bestDistance<0, take no action and resume the outer scan at triggerSlot+1.
2. Otherwise, if captured owner is currently human, supply the selected type's
   name as text argument1 and call the Support sprite notice for that live
   type, force=false. Retain the selected slot through the callback.
3. Call U01 destroy-and-redraw(selected, refresh=true), whose continuation
   includes cargo-aware deletion. Do not replace it with bare unit deletion.
4. If windowShown is nonzero after deletion, draw supported units and then the
   information panel, both with presentation enabled. Do not regate the second
   draw after the first returns.
5. Restart the outer scan from0, resetting the two support counters.

The selected unit may belong to another civilization or be FundamentalismFree.
Deleting it can leave support unchanged, requiring another restart and deletion.
No direct change to the city's disorder flags is made by this shortage branch.

### Affordable strategic retirement

If no shortage remains after selection:

1. If AI Gang Up is enabled and the unsigned advance count in the civilization
   selected by **bestHumanRank** exceeds captured owner's count, set captured
   owner's Democracy and Republic government scores to -999. Resume at
   triggerSlot+1 without deleting anything. The selector is used directly as a
   civilization index; it is not rank entry7 from the box-factor algorithm.
2. Otherwise require bestDistance>0 and the selected type's live domain=Land.
   If either fails, resume at triggerSlot+1. Do not retry the next-best land unit
   when an air or sea unit won selection.
3. Call C01 exact city lookup at the selected unit's live coordinates. If a
   destination city is found, add
   `s16(trunc(s32(signed typeCost*shieldRows)/2))` to its shieldStock, storing
   signed16. The lookup normally finds no city after the earlier city-tile
   filter, but the original retains this continuation after child effects;
   do not omit it or credit the home city instead.
4. Call U01 **bare unit deletion** on selected with refresh=true, not the
   cargo-aware wrapper used by shortage handling.
5. Clear Disorder, SecondTurnDisorder and LongDisorder in the input city's live
   flags **after** deletion. Do not clear OnEdge or Celebrating here.
6. Restart from0 with the captured original owner/government.

No new search occurs between the chosen candidate and its action. An action
that resumes instead of restarting uses the retained triggerSlot+1, not the
candidate loop's final index. Every outer iteration rereads the live unit bound.
On normal exit, support counters describe the final scan. CostsShield,
Aggressive, supportedSettlers and the military mood counts may still reflect an
earlier calculation; F04's following full recalculation refreshes them.

## Discriminating conformance cases


> Compare the gameplay/interchange observations in the
> [production scope](#production-contract-scope). Original memory/helper traces,
> arbitrary native callbacks and unsafe backing-byte cases in historical lists
> are optional reference, not additional production acceptance criteria.

Unless varied, use a valid flat map, city0 at (8,6), owner1, raw box rules10,
foodPerCitizen2, low/high settler cost1/2, no relevant wonders, no callback
mutation and no excluded writes. Other live units/routes are absent. Record
full child order, live/captured inputs, final stocks/flags/counters and random
state as well as the selected result.

| Case | Required result |
| --- | --- |
| AI factors, nominal difficulty0..5, turn0 | Both factors15/13/12/10/9/8. Human owner uses raw10 at every difficulty. |
| AI Warlord, raw rule7 | `trunc(7*13/10)=9`, then round odd result to10. Human raw7 stays7. |
| Late King adjustment, no qualifying knowledge, human rank entry7 | Food10/shields8 at turn200; both8 at201. No adjustment for Warlord or a nonhuman selected rank. |
| Civilization7-only human Rocketry/Fission, Manhattan not Unbuilt | At turn201, King AI factors9. A generic nonhuman set-difference implementation would incorrectly yield8. Destroyed Manhattan still qualifies. |
| Fundamentalism free allowance1, free unit first, ordinary unit second | supportedUnits2, shieldSupport1; first has no CostsShield, second does. Swapping order changes the flags/cost. |
| Qualifying unit role6 with both flags set | Both flags clear, no support/military/settler count. A foreign-owner unit retains both flags. Signed role-1 instead participates. |
| Air bomber sharing only x with city | Aggressive and home-attack counts each increment. Air Superiority sharing only y increments only home-attack. |
| Nonair away unit, Fortress at ordinary distance3 versus4 | Exempt from aggression only at3. Airbase encoding does not obtain this exemption. A city-owner answer exempts it without a distance test. |
| Size3, food39 versus40 | At39 remain size3/food39 and redraw. At40 grow once to4 and reset food0, or25 with Granary. No carryover excess. |
| Size8, food90, Granary, missing Aqueduct | End size8/food50 and eligible halt notice. Aqueduct tutorial wins even if that facility is being built and general growth-halt messages are disabled. |
| Size127, food1280, growth facilities and Granary | End size-128/food-635. This is scalar wrap, not required corrupting memory. |
| Negative food, first matching Settler foreign-owned | Select that unit before any later owner1 settler. Owner1 human permits deletion; city size unchanged. Supplied famine coordinates determine the final redraw. |
| Negative food, AI city unknown to all humans, matching Settler | Retain unit and population, reset food0. Safely redraw the affected city/view; no original scratch-coordinate input is required. |
| Negative food, outgoing routes to selected partner at indices0 and2, other partner at1 | Remove2 then0, preserve the other route, no size/unit loss. Incoming food-route workspace is not recomputed. |
| Negative food, no Settler/routes, size1 | Delete city and check captured owner for elimination; return1 before stock reset and final famine redraw. Full F04 parent returns its removed-city sentinel. |
| Republic, owner1 units at (10,6)/(14,6), foreign homed unit at (18,6), Shields1 | Remove foreign farthest first, then owner1 farthest after restart; keep the nearest owner1 unit. Final support1. |
| Republic support shortage, equal maximum weighted distances | First slot at maximum wins. Notice/deletion callbacks do not replace the retained choice with a new search. |
| Affordable AI Republic, Disorder, phase0 trigger, distant land unit | Bare deletion, clear three disorder flags, keep OnEdge/Celebrating, restart. A farther air winner prevents falling back to the land unit. |
| Same affordable trigger, AI Gang Up and comparison civilization ahead in advances | Keep units; set Democracy/Republic scores-999. |
| First recalculation gives Food0, size3, no settlers; previousFood1 | Accumulate surplus-6 and food-5, invoke FoodShortage before production; no famine yet. PreviousFood0 suppresses the warning. |

Additional cases should vary notice/query mutations, the live unit bound,
captured owner/government, support counter wrap, food-stock narrowing, zero and
odd rules, reversed growth thresholds, stale route workspace, no-selection
continuations, gang-up equality and the retirement shield-credit boundary.
For unavailable read backing, assert the last valid mutation and stop, not a
fabricated result. Invalid output writes are excluded-bug tests, never required
corruption outcomes.

The following original representation/provenance sections retain diagnostic
supplied-byte and stop cases. X01's repaired profile controls implementation and
required tests, including fresh-game isolation and the named C07/A02/C05 repairs.

## Appendix A — Original representation and evidence

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

These mappings support source review and validation. They do not prescribe a
cleanroom data structure:

| Original detail | Behavioral interpretation |
| --- | --- |
| Size BYTE, stocks WORD, calculation counters DWORD | Apply only the signed8/signed16/signed32 arithmetic contracts stated above; no adjacent field damage is admitted. |
| Unit home BYTE compared to incoming city DWORD | Raw-home equality, including255 when that city input has supported backing. |
| CostsShield `0x0800`, Aggressive `0x0400` | Independent semantic flags, cleared together before role eligibility. |
| FundamentalismFree type flag `0x0800` | Charge exemption after counting the unit's ordinal position. |
| Fortress/City feature test `(features & 0x42)==0x40` | Fortress component without City component. `0x42` is Airbase; W01 owns the composite decoding. |
| Discovery mask `MOVSX BYTE`, human mask zero-extended then complemented | Civilization7 exception in nonhumanKnowledgeTest; no arbitrary-width mask is needed in a logical implementation. |
| Rank selector at `00655c31` | Incoming rank entry7, distinct from bestHumanRank used for retirement. |
| Granary3, Aqueduct9, Sewer23; Pyramids0, Manhattan23, Apollo25 | F03 identity mappings; use named effects in the algorithm. |
| Famine text scratch at original `EBP-18` | Eight contiguous bytes hold `FAMINE0` and terminator. The split local declarations in decompiler output do not require a logical buffer overflow. |
| Famine x/y at `EBP-1c` / `EBP-20` | Assigned only after population decrement, otherwise unresolved incoming temporary inputs. |
| Ordinary notice and sprite wrapper | Notice invocation order is required; P04/P06/P10 will own concrete text, zoom, rendering and sound integration. |

Primary source evidence is OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`:

| Pinned source | Use |
| --- | --- |
| [City routines](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp) | Support charge `004e7d7f`; food factor `004e7eb1`; support scan `004e80b1`; growth-facility query `00441a79`; food turn `004ebbde`; shortage/retirement `004eef23`; city-turn parent `004f0a9c`. |
| [Unit routines](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp) | Distinct deletion/cargo/visible continuations; U01 is the behavioral owner. |
| [Enums](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h) | Named governments, roles, rules, flags and identities. |
| [Bug ledger](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md) | B16 optional population cap; B30 always-on famine redraw repair. Neither repair defines this document's original behavior. |

Independent reconstruction was read from `civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, particularly
`src/city_production.cpp`, `src/city_food_turn.cpp` and `src/city_turn.cpp`.
It corroborates signed mask/role/cost reads, retained versus live inputs,
foreign candidate selection, the two deletion continuations and B30's exact
checkpoint. Its repaired famine continuation uses current city coordinates
after removal callbacks; primary OpenCiv2 presets them before the notice.
C05 uses neither repair as proof of the original undefined values.

Read-only disassembly of the original `Civ2/civ2.exe`, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`,
was inspected for these distinctions:

| Instructions | Observation |
| --- | --- |
| `004e7f6b/004e7f70` versus `004e8168/004e816d` | Food's turn200 comparison skips on <=; shield's skips on <. |
| `004e7fba`, `004e8017` and matching shield reads | Discovery bytes are sign-extended for the complemented-human-mask predicates. |
| `004e8378`, `004e844e`–`004e8476` | Signed role filter and air-coordinate OR continuation. |
| `004ebe6f/004ebe85`, `004ebee8/004ebeec` | Famine coordinate assignments exist only in population shrink; final redraw reads them in other continuations. |
| `004ebf7d`, `004ebfdd`, `004ec014` | Growth increments size BYTE, writes refill WORD and later rolls back size BYTE when blocked. |
| `004ef0dc`–`004ef11f`, `004ef481`, `004ef537` | Candidate scan omits owner, affordable retirement calls bare deletion, shortage calls visible deletion. |

No fresh original executable execution or saved analysis/annotation change was
made. Static instructions settle these local questions; they do not establish
complete runtime conformance.

## Appendix B — Validation and remaining joins

Finite source checks add the actual support charge, food factor, support scan,
food turn, shortage/retirement and city-turn parent to the previous140 actual
routine bodies from C04. They retain C04's original-instruction correction to
whole mood-counter arithmetic. The host fixture uses packed reference records,
signed-char and wrapping arithmetic compilation; pointer carriers and explicit
query/UI/network adapters are not a native original-process model.

For B30 only, the fixture removes primary OpenCiv2's unconditional coordinate
initialization, tracks the actual population-shrink assignment, and requires
supplied incoming x/y or a bounded-stop exception after stock reset. This is an
explicit observation adapter, not a claim that the primary source preserves
the original's uninitialized read. No invalid overwrite is executed as a
required compatibility case.

The cases check factor gates and rounding, civilization7 knowledge, support
allowances/flag order, raw homes and signed roles, air and fortress predicates,
growth/refill/halt/tutorial ordering, scalar population wrap, famine selection
and visibility, route compaction, B30 partial effects, foreign victims, first
distance ties, restart capture, affordable retirement and gang-up markers.
The actual turn parent joins C02/C03 calculation, C05 food/support and C04
disorder, including negative-food warning timing and the city-removal return.
The previous C04 cases are also rerun against the extended fixture.

**143 C05 assertions** and **142 C04 regression assertions** passed in optimized
builds and with AddressSanitizer/UndefinedBehaviorSanitizer. These checks are
external research material, not cleanroom implementation source.
Production, research accumulation, improvement upkeep, terraform goals and build
snapshot are declared parent adapters; production selection/government changes,
facility/advance/wonder queries, geometry/presentation/network also retain their
stated fixture boundaries. Actual W05 pollution accounts for its own random
draw in the parent case. This does not validate complete consecutive turns,
native UI, arbitrary callback injections or every accepted off-nominal input.

[C06](C06-city-production-and-purchasing.md#appendix-b--sources-and-validation)
adds actual production, setter and build-snapshot bodies to finite city-turn
cases, retaining explicit choice/effect/space/UI/network adapters.
[C07](C07-trade-and-commodities.md#appendix-b--evidence-and-validation) adds actual
commodity refresh with supplied original-input/stop handling.
[C08](C08-buildings-and-wonders.md#appendix-b--validation-and-remaining-joins)
specifies effects and upkeep with focused checks; the full turn-parent join
and V03 research remain. V01 owns civilization elimination; A03/V02/A08 must join
terrain goals and government-score consumers. F04 must exercise complete
human/AI consecutive turns; P04/P06/P10/N02/N05 must join actual notice, window,
rendering and receipt behavior. X02 must retain reproducible full-state and
call-trace cases, supplied-read boundaries, signed scalar wrap and safe handling
of excluded writes. These finite checks do not establish complete original-program or whole-game
conformance.

## Appendix C. Original numeric annotations

These original annotations retain storage widths and reference-evaluation context.
They do not prescribe internal types or redundant casts. The main contract and
F01 determine logical ranges and behaviorally significant conversions; external
file/wire encodings remain normative in their owning mappings. Where an excerpt
includes an off-nominal conversion, it applies to that explicitly admitted profile,
not as a new requirement to accept unsupported inputs.

### Reference annotation 1

| size | City population count, read and stored with signed8 semantics. Nominal positive sizes may still reach a wrapping increment. |

### Reference annotation 2

| foodStock, shieldStock | Signed16 city stocks. Narrow after each specified write, including adding this turn's surplus. |
