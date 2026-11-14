# C03: City economy and corruption

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in C03 |
| --- | --- |
| Gameplay | City economy/corruption/support/trade/distribution/mood calculations and their true data dependencies, including values consumed by later city/AI operations. |
| Original files/content | F03 economic constants and F06 published city/civilization fields. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Shared-global placement, aliases used only by a harness, per-store publication and presentation-triggered refresh schedules. |

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

City recalculation mutates shared workspaces, analysis scores and child state.
Pure facility queries may be simplified on stable inputs; neither memoizing the
whole calculation nor reordering city passes follows from that local purity.

## Target, ownership and inputs

This specifies classic Civilization II Multiplayer Gold Edition **5.4.0f,
patch 3** capital/overlap calculation, recurring food and trade contributions,
industrial production, corruption and waste, tax/science/luxury distribution,
capitalization recalculation, publication and the complete city-stat parent.
Appendix B records the validation and integration limits.

[F01](F01-determinism-and-arithmetic.md) owns retained arithmetic and clamps;
[F02](F02-game-state-and-invariants.md#the-shared-city-workspace) owns record
identities, the shared workspace and cached/full dispatch. [F03](F03-rules-and-configuration.md)
owns interpreted rules. [W01](W01-map-geometry-and-spatial-state.md) supplies
geometry and ordered B neighborhoods; [U03](U03-pathfinding-and-goto.md#roadrail-connection-query)
owns road/rail connectivity. [C02](C02-city-tiles-and-worker-assignment.md) owns
availability, tile yields, assignments and specialists. [W05](W05-pollution-and-global-warming.md#city-pollution-intensity)
owns pollution intensity between industrial production and shield waste.

[C04](C04-city-happiness-and-disorder.md) owns citizen mood formulas, tax collection
and later disorder/celebration transitions; [C05](C05-city-growth-and-support.md) owns support/food requirements
and ordinary growth; [C06](C06-city-production-and-purchasing.md) owns production
accumulation and purchases. [C07](C07-trade-and-commodities.md) owns commodity
refresh, route creation/removal and delivery
bonuses; **C03 owns recurring route yields**. [C08](C08-buildings-and-wonders.md)
and [V03](V03-research-and-technology.md#2-shared-knowledge-and-availability) own
facility, active-wonder and advance queries. V02/A08 consume government-analysis
scores. F04 owns turn
ordering, and P06/P07 own city/report display and rate-preview callers.
These child contracts are required. Integration cases must execute their
operations or declare supplied child responses and the resulting coverage limits.

Nominal inputs supply initialized rules/map/workspace, a valid city and owner,
ordinary government and rates, valid route partners and at most three routes,
valid specialist kinds, finite stacks and the state required by children.
Tax/science rates are tenths, normally nonnegative with sum at most ten.
A saved partner base-trade value need not equal a fresh recalculation. The
original deliberately consumes stored snapshots and shared intermediate state.

Off-nominal inputs include unusual accepted rules, signed negative or wrapped
fields and stale but backed records. A replacement may declare a narrower
accepted domain. Within its accepted executions, preserve defined scalar wrap,
significant conversions, shifts, partial updates and query dependencies. Out-of-bounds writes
and their resulting damage are excluded bugs under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
including emulated damage to adjacent logical fields. Missing read backing or
an unknown required input is a bounded stop, not a zero-valued replacement.

## Logical state and arithmetic

The city record supplies location, owner, size, flags, facilities, production
selection, specialist kinds and routes. A route has a signed commodity selector
and partner slot; a negative commodity denotes food. Stored economy results
are base trade, net trade, science, tax, food, shields, happy and unhappy counts.
There is no persistent city luxury-total field written by these routines.

The shared workspace contains:

| State | Meaning |
| --- | --- |
| Food, Shields, Trade | Running totals, shared with C02's three-kind accumulator. They are not distinct copies of the named total fields. |
| capitalDistance, capitalCity, capitalLink | Outputs of the combined capital/overlap pass. |
| shieldSupport, supportedSettlers, settlerFood | C05 inputs used by assignment, waste and capitalization. |
| pollutionDivisor, populationFactor, pollution | C03/W05 coupled production and pollution state. |
| shieldWaste, tradeCorruption | Losses from their most recent stages. |
| routeTrade[0..2] | Last evaluated route yields; inactive entries are not automatically cleared. |
| foodOut, foodIn, foodPartner | Counts of negative-commodity routes and the last outgoing partner. |
| luxury, science, tax, buildingContent | Distribution results and its separate building-content subtotal. |
| happy, unhappy, angry; mood stages | C04's shared mood state. |
| processing, reallocating, workspaceCity | Turn/evaluation flags and remembered city identity; entry does not reset them. |

Use F01 reference arithmetic, arithmetic right shift (`asr`),
and signed division truncating toward zero (`div`). Products wrap **before**
shift or division. `clamp` below means F01's `clampAB`, which does not normalize
reversed bounds. City size/owner and route count/commodity are interpreted as
signed8; rates, government, difficulty and cosmic rules here as unsigned8.
City economy and government-analysis score publications apply `s16`.
These numerical semantics affect behavior; a replacement can use any storage
that produces the same results. Addresses and physical layouts are Appendix A.

Unless stated captured, later stages read the then-current shared fields.
Facility and advance queries use the reference short-circuit evaluation; pure,
stable and fully backed queries may be simplified under X01. The routines
specified here draw no random values directly; child contracts remain in force.
Do not hoist a child, infer facility prerequisites, refresh a partner city, or
turn the parent into a pure memoized function.

## Full city recalculation

F02 owns entry dispatch: absent city returns zero; a non-full call returns the
**full signed32** shared happy-minus-unhappy values, without checking the
remembered city. For a full call, set workspaceCity first, then execute:

| Order | Operation | Required boundary |
| --- | --- | --- |
| 1 | C02 availability pass | Refresh all 25 status entries, including its W05 AI pollution-tally side effects. |
| 2 | Combined capital/overlap pass below | Add overlap flags to those entries and publish capital state. |
| 3 | [C05 unit-support calculation](C05-city-growth-and-support.md#unit-support-calculation) | Produce shield/settler support and food requirements, military-unhappiness inputs and other support/box state before worker allocation. |
| 4 | C02 automatic assignment | Rebuild tile totals, worked selections, unassigned count and the food-stock marker; retain its job, score and map effects. |
| 5 | Recurring food routes below | Adjust Food after assignment's food-stock marker. |
| 6 | C02 specialist normalization/fill | Publish the specialist state consumed by distribution. |
| 7 | Industrial production → W05 intensity → shield waste | Pollution sees production before waste; resulting Shields still include shield support. |
| 8 | Trade/commodity/routes → distribution → C04 moods → publication | Return the signed32 difference of whole happy/unhappy counters after publication. |

The separate final-stat helper consists of steps 4..8 only. It assumes earlier
availability, capital and support state already exist; it does not set the
remembered city. Its original scaffold is declared void, but the full-entry
caller preserves the last mood result rather than inventing zero.

A full call does not clear government-analysis scores, job suggestions,
processing/reallocating flags or every workspace field. The ordinary city-turn
caller owns its resets. Repeated evaluation can accumulate analysis and AI
pollution side effects. Recalculation is distinct from paying support, advancing
food/shield stocks, collecting revenue, completing production or changing
Disorder/Celebrating flags; those are later caller/child operations.

## Combined capital and shared-radius pass

`calculateCapitalAndOverlap(city)` captures entry x/y. Set capitalDistance to
0 if the city currently has Palace, otherwise 32; set capitalCity absent.
Do **not** clear availability: this pass adds OtherCityWorked to prior flags.

Scan live city slots ascending with a live upper bound, excluding the input
slot. For each candidate:

1. Compute W01 weighted distance using both cities' then-live coordinates.
2. If candidate owner equals the input city's live owner and candidate has
   Palace, select it when distance<live capitalDistance **or capitalCity is
   absent**. Independently lower capitalDistance only for a strictly smaller
   distance. Thus the first palace beyond distance32 can be selected while
   the distance stays32; later equally distant palaces do not replace it.
3. If the retained candidate-to-city distance<6, visit candidate B[0..20]. Test its worked bit,
   but include center20 regardless. Form candidate tile x/y by raw addition
   **without wrapping**, using its live coordinates. If weighted distance from
   the input city's **entry** x/y to this tile is<3, compare raw differences
   against every input B[0..19] offset. Mark OtherCityWorked on each exact match.
   Do not stop at the first matching offset if custom tables contain duplicates.

Overlap uses other cities’ worked selections, not the tile radius-claimant
field. It is independent of civilization ownership and exploration. Center20 is
never marked by this overlap step. Wrapped physical equality alone is not
sufficient: the exact raw-offset test can leave seam-adjacent conflicts unmarked.
The proximity distance test uses W01 wrapping while the offset match does not.

After the scan set capitalLink=1 if capitalDistance==0, otherwise0. If nonzero
and the live city owner has Railroad, increment it. When a nonnegative selected
capital differs from the input city:

1. If input city and the **live selected capital** both have Airport, set link1.
2. Read raw region at entry x/y; capture selected capital's live x/y and read
   its raw region. On equality, replace link with [U03's road-connection result](U03-pathfinding-and-goto.md#roadrail-connection-query),
   using live city owner, entry x/y and captured capital coordinate. This can
   overwrite an airport link with zero; there is no Ocean exclusion here.

A selected capital is not implicitly revalidated after children. Economy and
corruption consume these same outputs; do not replace this pass by a generic
nearest-capital query or independently computed exclusive-radius ownership.

## Recurring food routes

Set foodOut=0, foodIn=0 and foodPartner=Absent. Visit the input city's routes in
ascending index order, testing the **live signed route count** each time. For
each negative commodity, increment foodOut and replace foodPartner with that
route's signed partner slot. Thus the last outgoing food route wins, regardless
of partner owner, existence or reciprocal entries. Subtract foodOut from Food.

Next scan live city slots ascending, with the live city-slot upper bound.
Skip records whose identity is absent. For every route, in ascending order
under its live signed count, first compare its partner with the input city;
only on equality test whether its commodity is negative. If both match,
increment Food and foodIn. No owner restriction or deduplication is applied.

Each negative entry contributes one unit. Two entries from one city count twice;
a self-route can count as both outgoing and incoming. A dead record contributes
no incoming food, even if its route payload remains. A negative route count
iterates nothing. These adjustments neither validate outgoing partner liveness
nor clear other route fields, and do not rerun C02's earlier food-stock marker.
They modify food yield, not accumulated food stock.

## Corruption helper and analysis scores

`loss(city, amount, capDistance, updateScores)` immediately returns zero when
amount<1, before reading city/owner or making queries. Otherwise capture owner
c and its current government g. Capture `communist = (g is Communism)` **before**
adding one to g if the city currently Celebrates.

The divisor helper ignores its city argument and uses the numeric government
ordering from Anarchy through Democracy:

| Effective government number | Divisor |
| --- | --- |
| ≤0 | 4 |
| 1 | 5 |
| 2 | 6 |
| 3 or 4 | 7 |
| ≥5 | 8 |

When `(processing and updateScores) or not communist`:

1. Capture d=capitalDistance. If capDistance and d>16, set d=16. There is no
   lower clamp here.
2. Let a=d. If effective g<2, d is nonzero and captured c is human in the live
   player set, replace a with `clamp(signed32(d + difficulty), 0, 32)`.
3. Compute `distanceLoss = signed32(a * amount * 3) div (divisor(g) * 20)`.

When `(processing and updateScores) or communist`, compute:

```text
communistLoss = signed32(communismPalaceDistance * amount * 3)
               div (divisor(g) * 20)
```

Select communistLoss if the **captured** communist identity is true; otherwise
select distanceLoss. Clamp the selected value to 0..amount. Query Courthouse,
and only if absent query Palace. If either exists, arithmetic-shift the clamped
loss right by one. Do not apply both reductions.

If processing and updateScores, visit candidate governments 1..5 ascending:

- Communism: subtract the previously computed **raw communistLoss** from its
  government score. This raw loss used the actual effective government divisor,
  not a newly computed Communism divisor.
- Fundamentalism: leave its score alone here.
- Each other candidate: start with that candidate number and add one if the
  city currently Celebrates. Reuse retained d, apply the low-government human
  difficulty adjustment above using the then-live difficulty/player set, and
  subtract the resulting raw distance formula from that candidate's score.

Each score subtraction narrows modulo 65536. Scores use losses **before** the
0..amount clamp and Court/Palace reduction. Anarchy and Democracy score entries
are untouched by this helper. Return the selected, clamped, facility-reduced
loss. There is no Fundamentalism, Democracy or barbarian immunity here; callers
apply their own suppression afterward, without undoing analysis side effects.

Actual scalar/clamp/facility children do not change processing state during
this calculation. An injected provider that enables score updating after a
branch skipped its required local values is outside that established domain;
comparison would need the missing original local inputs or a stop. Do not
initialize them to zero and claim equivalence.

## Industrial production, pollution and waste

Capture owner c at entry to the combined shields/pollution operation. Retain
that owner through W05's population-advance queries and the waste-government
read. Its explicit Hoover query below instead uses the city's then-live owner.

### Industrial production prefix

Initialize factory factor `B=0`, power factor `E=0`, and shared divisor `D=1`.
Apply the following queries and updates in order:

1. If Factory is present, set `B=2`.
2. If Manufacturing Plant is present, add 2 to `B`.
3. If Power Plant is present, set `E=2`.
4. Query Hydro Plant; only if absent query Nuclear Plant; only if that is also
   absent query Hoover Dam for the city's **live owner**. If any qualifies,
   set `E=2` and `D=2`.
5. If Recycling Center is present, set `D=3`.
6. If Solar Plant is present, set `E=2` and `D=3`.
7. Limit `E` to at most `B`.

Replace shared shields by:

```text
S0 = live shared shields after these queries
S = signed32(S0 + (signed32(S0 * B) >> 2)
                + (signed32(S0 * E) >> 2))
```

Each bonus uses the same pre-bonus `S0` and rounds separately. Do not multiply
by one combined fraction or apply power to factory-boosted production. Multiple
power sources do not stack. A plant can still set the pollution divisor when
no Factory/Manufacturing Plant exists; its production power bonus is then zero.
Facility queries test their actual records/effects, not inferred prerequisites.

### Pollution boundary

Invoke [W05's population factor and final intensity](W05-pollution-and-global-warming.md#population-factor-and-final-intensity)
using the captured owner c, the shared gross Shields and the divisor just
produced. W05 initializes its population factor, performs the ordered
Transit/advance/Solar queries and publishes pollution intensity. Do not recapture
the owner at this boundary, subtract support/waste first, or recompute the
intensity after the following loss. This is one coupled operation even if a
replacement factors it into separate modules.

### Shield waste

After pollution, compute:

```text
raw = loss(city, signed32(Shields - shieldSupport), capDistance=true,
           updateScores=true)
w = signed32((3 - capitalLink) * raw) div 3
w = w div ((liveGovernmentOfCapturedOwner >> 1) + 1)
shieldWaste = signed32(Shields - (shieldSupport + 1))
if w <= shieldWaste: shieldWaste = w
if shieldWaste < 1: shieldWaste = 0
```

Then force waste to zero if any of these holds: captured owner is barbarian;
its current government is Democracy or Fundamentalism; or the city's current
production is Palace and capitalCity is absent. The Palace-building exemption
uses the selected-capital sentinel, not merely capitalDistance.

Finally subtract shieldWaste from live Shields. Do **not** subtract shieldSupport
again here. The ordinary cap on waste leaves support plus one shield when
production permits it, but it does not create shields when gross production is
already smaller. A zero/negative helper amount returns zero before score work;
an immunity tested after a positive helper call does not suppress its scores.
The two divisions occur sequentially and truncate separately. Off-nominal
capitalLink is not clamped to 0..2 by this operation.

## Recurring trade, refresh and second corruption

Capture city owner c at entry to the trade/happiness operation.

1. Compute tradeCorruption=`loss(city, Trade, false, false)`.
2. If captured c's then-current government is Fundamentalism or Democracy,
   force that workspace corruption to zero.
3. Store baseTrade as signed16 of `Trade - tradeCorruption`.
4. Invoke [C07 commodity refresh](C07-trade-and-commodities.md#commodity-refresh-gate-and-terrain-survey) **now**, before iterating routes.
   Its state changes and original unknown-local boundary remain C07's contract;
   a cleanroom implementation must not silently skip it.

Visit routes ascending under the live signed route count. For each:

1. Capture the signed partner slot p. Compute
   `v = asr(signed32(signedCityBaseTrade + signedPartnerBaseTrade + 4), 3)`.
   Read both stored base-trade fields after commodity refresh. Do not recursively
   recalculate the partner or use its net trade, gross tile trade or routes.
2. Capture the signed commodity. Query U03 road/rail connection using the
   **live input city owner and both live coordinates**.
3. Query the input Airport; if present, query partner Airport. If both are
   present and connection<2, set connection=1. An existing rail result2 stays2.
4. Query the input Superhighways; if present, increment connection. Partner
   Superhighways are not tested.
5. If connection is nonzero, add `asr(signed32(connection * v), 1)` to v.
6. If the captured commodity is negative, set v=0. All preceding connection
   and facility queries still occurred for a food route.
7. If the partner's current owner equals the **captured entry owner c**, set
   `v=asr(v,1)`. This follows connection bonuses and food suppression.
8. Store v in routeTrade[index], then add v to Trade, both as signed32 values.

No partner identity, reciprocal-route, mutual-exploration or commodity-demand
eligibility check is added here. Route insertion/delivery has its own rules.
Unused routeTrade entries are left unchanged. Nonpositive route counts do no
iterations. Values are not clamped nonnegative by the recurring-route formula.

After all routes, compute tradeCorruption=`loss(city, Trade, false, true)`.
Again suppress it for captured c's live Fundamentalism/Democracy government.
Capture `netTrade = signed32(Trade - tradeCorruption)` and invoke distribution
below in mode0, with extraTax=0. Shared Trade remains **gross including routes**;
the city record's net-trade field receives the captured difference. BaseTrade
remains the earlier value before route additions. The second corruption call,
not the first, is eligible to update government-analysis scores.

### Route bounds and excluded overwrite

The supported route workspace has three entries. A larger positive signed count
can make the original read later record fields as route data and eventually
write beyond routeTrade. Reads outside declared route data require explicitly
supplied backing or a bounded stop. Extra backing does **not** extend the valid
three-entry output array. Stop before the first invalid output write, retaining
prior valid effects; rejecting such state earlier is a declared input restriction.
Do not emulate changes to nearby mood, notice, production or food-route fields.
Appendix B distinguishes that bug evidence from required conformance.

## Tax, science and luxury distribution

[V02](V02-governments-and-tax-rates.md) owns player rate limits and balancing.
Its preview calls this distribution operation with mode1, then restores the
civilization's rates while retaining the city's published fields. Full government
and rate-commit recalculation use the complete parent defined here.

`distribute(city, inputTrade, mode, extraTax)` has two inputs that are especially
important for previews: supplied inputTrade and the city's previously stored
trade/tax. At entry, when mode1 and production is Capitalization, save inputTrade
and replace the working trade argument by the signed stored city trade.
Otherwise use inputTrade unchanged. Then capture owner c.

### Rates, specialists and Fundamentalism

Let rS and rT be unsigned science and tax rates. Effective science rate eS=rS,
except Fundamentalism caps it at the cosmic maximum-science rate. Compute:

```text
luxury = clamp(signed32(trade * (10 - (rS + rT)) + 4) div 10, 0, trade)
science = clamp(signed32(trade * eS + 4) div 10, 0, signed32(trade - luxury))
tax = signed32(trade - (science + luxury))
```

Luxury uses **raw rS**, even when Fundamentalism reduced eS. The rounding bias
is +4, not +5. Tax receives the remainder; it is not independently rounded.

If processing, add signed16 of `luxury div 2` to Fundamentalism's analysis
score. Then subtract signed16 of
`(signed32((signedLeaderCivilize + 3) * science) div 4)` from that same score.
The leader comes from captured c's current tribe. This is truncation toward
zero even for negative products, not a plain arithmetic shift.

If captured c is currently AI and its live government is Fundamentalism, add
luxury to science and set luxury=0. Next, call C02's specialist counter for
Entertainer, Tax Collector and Scientist, **in that order**, adding respectively
2×count to luxury, 3×count to tax and 3×count to science. Each call scans all
sixteen explicit kinds regardless of the displayed/normalized specialist count.

If captured c's live government is Fundamentalism, subtract
`signed32(fundamentalismScienceLoss * science) div 100` from science. This is
a subtraction of the truncated loss, not multiplication by a retained percent;
specialist science and any AI luxury conversion are included before this loss.

### Building-content subtotal and tithes

Set buildingContent=0, then perform these queries in order:

1. Temple: if present, query Mysticism and then Ceremonial Burial, adding one
   for each known advance. Then query Oracle; if present, double the subtotal.
2. Colosseum: if present, add three; then query Electronics and add one if known.
3. Query Monotheism. Only when known, query Cathedral and, only if absent,
   Michelangelo. If either qualifies, query Communism and then Theology; add
   `(Communism absent ? 1 : 0) + (Theology present ? 3 : 2)`.
4. Query Bach; if present add two.
5. If captured c's current government is Fundamentalism, add buildingContent
   to tax as tithes.

This subtotal is calculated for every government. It supplies tithes and the
analysis adjustment below. It is **not** a cached substitute for C04's later
building happiness pass, whose query order and live inputs are separate.

### Economic and science bonuses

Set economic level b to one if Marketplace exists, otherwise zero; then query
Bank and Stock Exchange, adding one for each present. Independently replace:

```text
luxury = signed32(luxury + asr(signed32(luxury * b), 1))
tax    = signed32(tax    + asr(signed32(tax    * b), 1))
```

If processing, add signed16 of
`asr(signed32((b + 2) * buildingContent), 1)` to Fundamentalism's analysis score,
with signed16 retention of that score. This occurs even outside Fundamentalism.

Set science level b to Library presence, add one for University, then query
Research Lab and, only if absent, SETI. If either qualifies, add one. Let
bonus=`signed32(science * b)`. Query the active city of Newton; unless it equals
the input city, replace bonus with `asr(bonus,1)`. Add bonus to science. Query
the active city of Copernicus; if it equals the input city, double science.
Newton changes only the building bonus, not the base science. Science facility
presence is queried independently; do not infer a Library from University or
count Lab and SETI twice. Apply no additional cap.

### Capitalization and publication modes

Test the city's **current** production after those stages. Only Capitalization
adds these effects:

| Mode | Extra action |
| --- | --- |
| 0: ordinary calculation | If shieldSupport<Shields, add `Shields - shieldSupport` to tax. This uses production after waste. |
| 1: stored-total preview | Reinvoke distribution with saved inputTrade, mode2, and `signedStoredCityTax - liveTax` as extraTax. Return immediately; the outer call performs no final record stores. |
| 2: recursive adjustment | Add extraTax to tax. |
| Any other value | No capitalization addition. |

When no recursion returns early, store city science and tax from the current
workspace with signed16 retention, then store the working trade argument as
signed16 city trade. Luxury remains workspace-only. No treasury/research
account or food/shield stock is changed by these stores.

Mode1 first computes tax from the previous city trade, then carries the old
stored-tax difference into a second calculation with supplied trade. Both passes
perform all queries and processing-score updates; recursion captures owner
again. Do not collapse them into a single pass. The tax-rate/report callers
normally supply the stored trade itself, but the helper also accepts a distinct
inputTrade. A non-Capitalization mode1 call simply performs one ordinary split
without capitalization tax.

The actual default children do not change production during distribution, so
the saved-input local is initialized whenever the recursive path uses it.
A provider that introduces Capitalization only after the entry test would need
the original missing local input or a bounded stop; no default value is defined.

## Mood continuation and final publication

After mode0 distribution, the trade/happiness parent continues into C04 with
its captured owner c, the current city, support/military counters and the live
luxury total. Its [ordered stages and normalization](C04-city-happiness-and-disorder.md#ordered-calculation-stages)
own the complete mood sequence and stages0..4; do not substitute a guessed
net-happiness formula or assumed zero.

This calculation establishes moods; C04's later turn operation owns disorder
and celebration changes. No reallocation is added between distribution and
publication. The separate distribution buildingContent subtotal cannot replace
the later facility/advance/wonder queries.

After the final mood clamp, reference evaluation publishes the following values.
Independent assignments may be grouped under X01; preserve each retained-versus-live
source, conversion and any admitted alias, observation or failure dependency:

1. City science from live workspace science, signed16.
2. City tax from live workspace tax, signed16.
3. City trade from **retained netTrade** captured before distribution, signed16.
4. City food from the low eight bits of live Food.
5. City shields from the low eight bits of live Shields.
6. City happy, then unhappy, from their respective low eight bits.
7. Return signed32(workspaceHappy − workspaceUnhappy), using the whole counters.

The first three stores repeat the ordinary distribution publication, but they
occur after mood children. City trade uses the retained netTrade even if a child
changes shared Trade; science/tax and yield stores read current workspace.
Food/shield totals and mood snapshots retain their modulo-256 values; the returned
mood balance is calculated from the full workspace counters, not those snapshots.

[C04's original-instruction evidence](C04-city-happiness-and-disorder.md#appendix-b-sources-corrected-evidence-and-checks)
shows that mood calculations replace/update whole signed32 counters. The byte
casts in OpenCiv2's decompilation are a reconstruction error; they do not establish
retained upper bits or a separate signed-byte full-return contract. F02's cached
entry also subtracts the whole counters, but can read another city's stale state.

## Examples and conformance obligations


> Compare the gameplay/interchange observations in the
> [production scope](#production-contract-scope). Original memory/helper traces,
> arbitrary native callbacks and unsafe backing-byte cases in historical lists
> are optional reference, not additional production acceptance criteria.

- At distance32, amount100, difficulty3, human Despotism yields96 loss; the
  distance-cap option yields57. Courthouse or Palace halves57 to28. The score
  variants still subtract their raw losses, without that halving.
- The Communism identity is captured before celebration changes its effective
  level. With palace-distance rule7 and amount100, Communist loss is15 both
  before and after celebration because divisor levels3/4 are both7.
- Three shields with Factory and Power Plant become5, not6: each half bonus
  truncates separately. With base41, Factory+Manufacturing+Power produce102;
  no industrial advances gives pollution82. Monarchy, support4, distance cap16
  and no connection then waste19, leaving83 shields without recomputing pollution.
- Trade11 split at science50%, tax50% yields science5 and tax6. At science60%,
  tax30%, it yields luxury1, science7 and tax3.
- Base-trade snapshots20 and28 yield a route base6. Rail plus input
  Superhighways gives connection3 and yield15; same-owner halving then gives7.
  A negative commodity yields zero, after the same connection queries.
- Mode1 Capitalization with supplied trade20, prior trade10, prior tax17 and
  50/50 rates computes first tax5, retains adjustment12 and finally publishes
  science10, tax22, trade20. Processing analysis runs twice.
- A tax workspace value32768 is published as signed city tax -32768. Food258
  publishes2. These are defined scalar narrowing, not memory-overwrite bugs.

Conformance must cover ties/no capital, raw seam offsets and Airport overwrite;
food-route multiplicity and dead/self records; both corruption calls; scores
under celebration/immunity; separate bonus rounding; negative and wrapped
arithmetic; all distribution modes; stale partner totals; inactive route outputs;
specialist-kind versus count independence; full/non-full identity and whole-counter
returns; partial effects; and the three-route invalid-write stop. Compare child
order and shared state as well as final city fields.

## Appendix A. Original encoding and storage for validation

These identify original observations, not required replacement layouts.
F02/F06 own complete record definitions and save encoding.

| Observation | Original representation |
| --- | --- |
| City | 88-byte slot; owner+8, size+9; specialist kinds+22; production+57, base trade+30, route count+58, goods+65 and signed partner words+68; science/tax/trade+74/+76/+78; food/shields+80/+81, happy/unhappy+82/+83. |
| Government | Anarchy0, Despotism1, Monarchy2, Communism3, Fundamentalism4, Republic5, Democracy6. Capitalization production is -38, Palace -1; negative signed commodity bytes designate food routes. |
| Analysis scores | Signed16 civilization entries indexed by government, original offset `0x3d4 + 2*government`. |
| Shared route values | Three signed32 entries beginning `006a6590`. Index3 starts overwriting other scratch state; it is not a fourth supported route. |
| Shared mood counters | Happy `006a6550`, unhappy `006a65a8`, angry `006a659c`; whole signed32 counters; stage and final-record snapshots copy only low bytes. |
| Distribution workspace | Tax `006a6554`, science `006a6578`, luxury `006a65fc`, building content `006a6618`. |
| Totals and support | Food `006a65c8`, Shields `006a65cc`, Trade `006a65d0`; shield support `006a6568`. |
| Parent mode/cache | Processing `0062edf8`, reallocation `0062edfc`, cached city `0062ee08`. |

The physical route-write spill is excluded even if an emulator maps its later
locations to named objects. Preserve only prior valid effects and the specified
safe boundary. In contrast, word score wrap and narrowing at specified
stage/city-record publication remain valid behavior. Original mood-history writes outside
stages0..4 are likewise excluded writes; the normal parent uses exactly0..4.

## Appendix B. Sources, checks and remaining integration

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`. The relevant files were checked equal
to OpenCiv2 comparison revision `fc2155ccdb2123c1137babab6fa39e38152278c9` when researched.
The specification repository imports no implementation code or executable assets.

| Evidence | Coverage |
| --- | --- |
| [City routines](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp) | Capital/overlap `004e7967`; divisor/loss `004e9849/004e989a`; shields/pollution `004e9c14`; mood clamp `004ea031`; distribution `004ea1f6`; trade/moods `004ea8e4`; food routes `004eb327`; final-stat helper `004eb4a1`. |
| [Return-preserving parent](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/retvals.cpp) | `city_recalc` `004eb4ed`, including whole-counter cached and full returns; the original-width correction is documented in C04. |
| [Shared city state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/city.h), [record types](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h) | Workspace identity, partial stores, record types and array bounds. |
| [Clamp implementation](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/support.cpp), [F01](F01-determinism-and-arithmetic.md#shared-clamp-and-shift-helpers) | `005adfa0` ordered bounds and F01 retained arithmetic. |
| [Rate-preview caller](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/reports.cpp), city source above | Mode1 callers include trade advisor and tax-rate dialog. Full UI behavior remains P06/P07. |
| [Bug ledger](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md) | C07 commodity unknown-local issue and B31 waste-bar drawing are distinct from the scalar economy formulas. B31 is a presentation repair, not a changed waste amount. |

A temporary external source harness reused C02's 129 extracted routines and
added eight: divisor, actual corruption, mood clamp, distribution, trade/moods,
food routes, final-stat helper and the return-preserving recalculation entry.
The earlier corruption and recalculation adapters were replaced by their actual
bodies. C04 subsequently corrected the two mood bodies to original signed32
counter accesses and reran the economy cases. The fixture preserves packed
city/civilization records, the real terrain record and C02's shared tile/total aliases. It uses F01's ordered clamp, not a
library clamp with a reversed-bound precondition.

**140 finite assertions passed** in optimized and Address/UndefinedBehavior
Sanitizer builds, including the rerun with original mood widths and corrected
expectations documented in C04. Cases checked fixed corruption vectors across
governments, celebration and distance capping; score wrap and raw-score versus reduced-loss
behavior; industrial rounding/pollution/waste; immunity and Palace-building
conditions; raw luxury rates, specialist counting, Fundamentalism, tithes,
Newton/Copernicus and economic bonuses; capitalization recursion; negative and
wrapped inputs and narrowed outputs; food-route multiplicity; route bonuses,
food suppression and stale inactive entries; both corruption stages; retained
net trade versus live final fields; whole mood counters and cached return;
and an actual full parent joining C02 assignment, capital, food, specialists,
W05 intensity and trade/distribution/moods. No invalid output-array write was run
as a required case. The code and fixture are external validation material, not
cleanroom implementation source.

Declared adapters remain for C05 unit-support production, C07 commodity demand,
facility/advance/active-wonder providers, U03 connection answers and inherited
map/UI/network environment. Full-parent assertions use explicit support outputs;
they do not validate C05's formula. Commodity refresh checks its position after
base-trade publication but does not resolve C07's unknown local. Mood bodies
use the original-width correction established in C04; this finite economy
check is not an exhaustive mood test. Queries use controlled state, not the full
C08/V03 obsolescence/technology graph. C02's earlier capital/overlap checks and W05's
separate intensity checks remain scoped as recorded in those documents; they
are not newly run original-executable comparisons.

Independent corroboration was read from `civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`: `src/city_distribution.cpp`,
`src/city_pollution.cpp`, `src/city_trade.cpp` and the
`tests/fixtures/city_distribution` README/protocol. The distribution fixture
records 1,224 native cases, including 74 recursive capitalization calls; those
archived results were inspected, **not rerun** here. Its target executable is
SHA-256 `edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Actual default callees support the saved-input/processing-local initialization
claims; this does not establish arbitrary injected mutation between pure queries.

The independent trade reconstruction explicitly maps route indices beyond2
onto nearby mood/turn/food fields. That is evidence of an original overwrite,
**excluded by X01**, not a requirement to reproduce it. Its unsupported-read
stops and commodity input boundary are separate from the forbidden writes.
No fresh original executable run or saved annotation change was made. C04 adds
read-only original disassembly resolving mood-counter widths; the primary
reference source is retained unchanged.

C04 specifies the mood continuation and later turn transitions.
[C05](C05-city-growth-and-support.md#appendix-b--validation-and-remaining-joins)
adds finite actual support and turn-parent joins to the calculation bodies.
[C06](C06-city-production-and-purchasing.md#appendix-b--sources-and-validation)
adds the production and player-command join with explicit downstream adapters.
[C07](C07-trade-and-commodities.md#appendix-b--evidence-and-validation) adds actual
commodity refresh with explicit original-input/stop handling.
[C08](C08-buildings-and-wonders.md#appendix-b--validation-and-remaining-joins)
adds focused actual effect-query and upkeep checks; their city-calculation join
and V03 technology behavior remain. F04 must exercise
complete consecutive turns; V02/A08 must validate accumulated
analysis consumers; P06/P07 must join actual rate previews, city updates and
reports. X02 must retain independently reproducible full-state and call-trace
cases, including errors/partial effects, scalar wrap and excluded-write stops.
These finite checks do not establish complete original-program or whole-game
conformance.
