# C07 — Trade and commodities

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

Ranking stability, suppression/collision handling, route mutations, reciprocal
cargo and payment/research effects retain their dependencies. An explicit tie
key may replace incidental sorting steps only with equivalent retained outputs.

## Target and ownership

The target is **Civilization II Multiplayer Gold Edition 5.4.0f, patch 3**.
This document owns commodity refresh, supply/demand ranking and suppression,
human/AI cargo selection, trade-route mutation, caravan entry choices, delivery
payments and food, reciprocal cargo selection, and commodity search reports.
The evidence and integration limits are stated below.

[C03](C03-city-economy-and-corruption.md#recurring-trade-refresh-and-second-corruption) owns
**recurring** route trade and incoming/outgoing food contributions. The payment
made on delivery and the score used to replace a route are different formulas,
specified here. [C06](C06-city-production-and-purchasing.md) calls cargo selection
after producing a trade unit; [C01](C01-city-lifecycle-and-ownership.md) and
[C05](C05-city-growth-and-support.md) call route removal. [U02](U02-movement-and-zones-of-control.md)
owns movement into a city, and [U01](U01-unit-lifecycle-and-stacks.md) owns unit
creation and visible deletion. [U03](U03-pathfinding-and-goto.md) owns
the road/rail connection query. [W01](W01-map-geometry-and-spatial-state.md) owns
coordinates, radius order, resources, terrain, regions and distance.
[A07](A07-ai-special-unit-missions.md#2-caravan-destinations) owns caravan
destination scoring; it uses a separate heuristic from these delivery formulas.

[F01](F01-determinism-and-arithmetic.md) owns arithmetic, clamps, shifts and the
primary sampling law. C08/V03 supply effective facilities, wonders and
advances; [V03 supplies research cost](V03-research-and-technology.md#7-research-cost).
[V04 owns the shared attitude operation](V04-diplomacy-and-treaties.md#2-shared-attitude-and-hostility).
P04/P06/P07/P10 and
N02/N05 own real dialogs, report layout, sound, network delivery and callbacks.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

## State, domains and arithmetic

There are sixteen commodity identities in a fixed order, listed in Appendix A.
Each city has three supply and three demand entries, up to three outgoing
routes, a CommodityDirty flag, cached base trade, food stock, size and owner.
A route contains a partner city and cargo. Negative route cargo means food.
Negative supply/demand entries suppress the corresponding absolute commodity
identity. **Identity zero cannot be suppressed by negation:** its negative is
still zero. A unit's counter holds its cargo; negative signed values mean food
to delivery, while normal selectors use the Food sentinel.

Nominal inputs have live city/unit identities, backed valid owners and partners,
standard terrain identities, initialized rules and region records, route counts
0..3 and supplied dialog choices. Supply/demand entries may legitimately be
negative after suppression. Production and delivery use the fixed Trade role;
the freight bonus instead tests the **Freight type identity**, even with custom
unit rules. No general “faster caravan” predicate replaces it.

F01 defines reference results for additions, subtractions, products and left
shifts; preserve significant conversions under the numeric convention. `/` means signed division truncated toward zero;
`>>` means arithmetic right shift, which rounds negative values downward.
`abs32` is wrapped absolute value. F01's `clampAB` and `shiftSigned` apply.
City size/owner, route count, supply/demand/cargo and rule cost are signed8
where used as signed quantities below; coordinates, base trade, food/shield
stock and research accumulation retain signed16. Treasury retains signed32.
Counts stored as bytes retain their low eight bits. The advance count and
cosmic box rules are unsigned8; region tile count is signed16.

Values are live unless explicitly captured. A city getter, query, redraw or
modal child is not permission to cache later reads across its effects. Earlier
mutations survive cancellation and bounded stops. Accepted off-nominal input
retains these numeric results; a stricter implementation may declare its input
limits. Missing read backing requires explicit input or a stop at the first
unavailable read. Out-of-bounds writes are excluded under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
including corrupting ranking metadata with invalid terrain indices and writing
route records outside their three slots. Extra physical backing does not expand
those logical destinations.

### Original inputs not initialized locally

The commodity routine does **not** initialize its road counter. Name its incoming
signed32 value `roadBase`, separately from the number of road tiles encountered.
It can affect ordinary, otherwise nominal games. A supplied-input implementation
must use the actual input, not assume it is a count or normalize it to zero.
Without that input, stop at the first increment of the counter; if no road tile
was visited, stop at the later Copper-demand read. Keep earlier dirty-flag,
score and query effects. A declared zero-initialization repair is a different
policy, used by both reconstructions; it is not recovered original input.

Route replacement has another missing-local boundary when no existing route
scores below 9999. Its exact read point and safe handling are given below. An
unknown slot is not automatically an out-of-bounds write: an explicitly supplied
slot could be valid. Neither boundary permits inventing original stack contents.

## Commodity refresh gate and terrain survey

`refreshCommodities(city)` returns without changes unless CommodityDirty is set,
reveal-map/editor mode is active, or `(signedTurn + city) modulo16 = 0`.
It does not draw random numbers. On entry to the active path:

1. Clear CommodityDirty. Capture city x, y and owner, then query its region.
2. Capture `p=(live signed size+2)/5` and `a=unsigned advanceCount[live city owner]`.
   The captured owner used by subsequent advance queries is not recaptured here.
3. Set `dx=x-(mapWidth>>1)`, `dy=y-(mapHeight>>1)`, `east=max(dx,0)`,
   `south=max(dy,0)`, `X=abs32(dx)`, `Y=abs32(dy)`. Unused west/north values
   do not enter the scores. Later formulas read live map dimensions again.
4. Clear river count and all eleven terrain counts. For each commodity index
   ascending, clear its demand score, copy that zero to its supply score, and
   initialize both ranking identities to that index. The two score arrays are
   shared state; `roadBase` is not among the initialized counters.
5. Visit W01 radius A indices 0..20 in order, using captured center coordinates.
   Wrap x once, then test bounds. For an in-bounds tile, read its terrain and
   increment that terrain's count. Query its resource; any nonzero resource
   adds **three more** to the already retained terrain count. Then query the
   live River feature and increment river count if set; query live Road and
   increment the road counter if set. A resource does not multiply river/road
   counts. Do not filter tiles by ownership, workers, occupancy or yield.
6. Add Glacier count to Tundra count; set Glacier count to zero. All subsequent
   terrain formulas see this merger, including formulas that still name Glacier.

Let `T(name)` be these weighted terrain counts, `R` the river count, `Q` the
wrapped `roadBase + encounteredRoadTiles`, and `L` the captured region. `N` below
means a fresh signed tile-count read from region L. Let `s` mean a **live** city
size read, not a second name for captured p. `S[g]` and `D[g]` are signed32
supply/demand scores. Evaluate the following operations in their listed order.
Do not collapse repeated technology/facility queries or rounding steps.

## Supply scores

The order is Hides through Uranium. Each row starts the named score, then applies
its modifiers sequentially. `v+=v>>1` and `v=(3*v)/2` are intentionally different
for negative or overflowing inputs.

| Commodity | Initial score and ordered modifiers |
| --- | --- |
| Hides | `3*T(Jungle)+6*T(Tundra)+4*T(Forest)+3*R`. Double if a<16; double if a<24. If a<49, double if s<3; otherwise, for a>=49, divide by2. Finally divide by2 if s>7. |
| Wool | `(R/2+2*T(Hills)+T(Grassland)) * (T(Tundra)+k)`, where k=3 if `mapHeight/3<Y`, otherwise2. |
| Beads | `8*T(Ocean)-Y`; shift right1 if s>9, then right1 if a>32. |
| Cloth | Capture `b=3*T(Plains)+T(Desert)-R`; set `b*clampAB(a/10,1,2)`. If Industrialization, replace by `(3*v)>>1`. Shift right1 if a<8, then right1 if a<16. |
| Salt | `3*T(Ocean)+4*T(Desert)+2*T(Swamp)-a/6`. Without Pottery divide by3. With Aqueduct add v>>1. If L is odd and L<6, add v>>1 again. |
| Coal | `(T(Jungle)+T(Plains)+T(Forest)+T(Swamp)+1)*T(Hills)*5`; apply `shiftSigned(v,min(p/2,2)-1)`. If L is odd and L>1, add v>>1. If a<20, shift right1. |
| Copper | `5*T(Mountains)+5*T(Hills)`; double if L is nonzero and even. |
| Dye | Capture `b=5*T(Grassland)-T(Plains)+R`; set 2*b, or 4*b if L is nonzero and divisible by4. |
| Wine | Capture `b=clampAB(5*R-T(Grassland),0,4*T(Plains))`, then `c=clampAB(4*T(Plains),0,b)`. Set `c+((mapWidth>>1)-X)/2+b`. If south!=0, shift right1. Apply `shiftSigned(v,clampAB(p-1,-2,1))`. If `(L-2) modulo4=0`, add v>>1. Double for the French tribe identity. If s>10, shift right1. |
| Silk | `(2*T(Forest)+T(Jungle)+1)*(T(Hills)+1)`. Only if this is nonzero, add 2*east and, if L is nonzero and divisible by5, double it. Double for Chinese. **After the Silver operation below**, double Silk again for Aztecs; that later tribe test does not modify Silver. |
| Silver | `8*T(Mountains)`. Only if nonzero: add `T(Hills)+X`; without Iron Working shift right1; if L>8 add v>>1; if s<5 shift right1. Then perform the Aztec Silk modifier above. |
| Spice | `(3*T(Jungle)+2*T(Swamp)+2*T(Desert))*((T(Ocean)+R)/2)`. Only if nonzero: double if Y<10; subtract Y; add v>>1 if N<26; shift right1 if a fresh N>300; shift right1 if L=1. |
| Gems | `(T(Mountains)+1)*(T(Swamp)+1)*(T(Desert)+1)+T(Plains)`. Only if nonzero: replace by `clampAB(p,1,4)*v/2`; if L=7 add v>>1. |
| Gold | `(T(Mountains)+T(Hills)/2+1)*(R+2)`; double if T(Mountains)>2; double if s>4; double if a fresh s>9. |
| Oil | `8*T(Tundra)+10*T(Desert)+6*T(Swamp)+12*T(Glacier)`. If the global civilization-known mask for Combustion is zero, shift right3. If the result is zero, set -1 and stop this row. Otherwise triple if L=17; else add v>>1 if L>1 and `(L-1) modulo8=0`. Multiply by `clampAB(p/2-2,1,2)`. |
| Uranium | `(T(Tundra)+T(Desert)+1)*(T(Hills)+R+1)*(T(Mountains)+1)`. Without Nuclear Fission set0. If zero, set -1; otherwise add v>>1 if nonzero L is divisible by10, then replace by `v*min(p,6)/6`. |

The global Combustion test is not the city's owner's knowledge test. The Wine
clamps are two ordered clamps, not a conventional min/max simplification.
Read region size only on the branches that request it.

## Demand scores

Compute the initial Hides and Cloth values before modifying Hides. Cloth's
demand is retained even when later Hides modifiers change its source value.

| Commodity/order | Initial score and ordered modifiers |
| --- | --- |
| Hides, then Cloth seed | Set Hides=`5*(T(Tundra)+T(Glacier))+(3*Y)/2+2*T(Mountains)+T(Forest)`. Set Cloth=`4*T(Hills)+4*T(Forest)+((a/10)*Hides)/8`, retaining signed32 intermediate multiplication. |
| Hides modifiers | Double if s<3; divide by3 with Industrialization; replace by1 with Mass Production; double if a<10; double if a<20; divide by2 if a>47. |
| Wool | `2*abs32((mapHeight>>2)-Y)+2*T(Plains)+T(Forest)`; double with Industrialization. |
| Beads | `Y+((21-T(Ocean))*3)/2`; add v>>1 if s<4; divide by2 if a fresh s>11; divide by2 if a>47. |
| Salt | Start from its cleared zero. Set remaining=live s and weight=8. While weight!=0 and remaining>0: chunk=`clampAB(remaining,0,5)`; add chunk*weight; weight=weight/2; remaining-=chunk. Then subtract a/2. At most weights8,4,2,1 contribute. |
| Coal | `(Y+10)*p+a`; set0 if s<5; shift right1 if a fresh s<8; double with Industrialization, then Electricity, then Power Plant. If Hydro Plant, or only if absent Nuclear Plant, or only if both absent Solar Plant, shift right3 once. |
| Copper | `(R+Q+1)*p`. If this is <=S[Copper], shift right1. With Marketplace add v>>1; with Bank add v>>1; with Electricity add v>>1. With Computers divide by4 using truncation. If s<5, shift right1. This is the first read of Q when the survey encountered no Road. |
| Dye | `S[Cloth]+Q`; divide by2 with Chemistry, then divide by2 with Mass Production. |
| Wine | `4*p+4+abs32(live city x-live city y)`. |
| Silk | `2*((mapHeight>>1)-Y)-X+(mapWidth>>1)+4*T(Jungle)+T(Plains)/2+2*T(Swamp)+4*T(Desert)`. Apply `shiftSigned(v,clampAB(p-1,-1,1))`; if L=1 and city identifier's value modulo4 is2 or3, add v>>1; if s<7, shift right1. |
| Silver/Gems/Gold seed | Set Silver=8*s, Gold=0, Gems=0. Select Silver initially. Compute `(live y+live x)%3`. If1, select Gems and copy Silver into Gems; if2, select Gold and copy Silver into Gold. Silver is not cleared when another metal is selected. |
| Selected metal modifiers | Cathedral or, only if absent, Michelangelo effect: `(3*v)>>1`. Bank: `(3*v)>>1`. Economics: shift right1. Computers: shift right1. Spanish tribe: double. Only the selected metal receives these modifiers. |
| Spice | `N/10-max(a-12,0)`; divide by2 if s<4. If a fresh N>400: double if s>7, then set0 if `(-live x-live y) modulo4=0`. Finally shift right1 with Refrigeration. |
| Oil | Without Industrialization set -1. Otherwise `(a/3)*(p+2)`; if <=S[Oil], shift right1. Query Factory. If absent, shift right1 for each successive true size test s<5, s<10, s<20. If present, replace by `(3*v)/2`. With Automobile triple; with Superhighways double; with Mass Transit shift right1; with Recycling Center shift right1. |
| Uranium | Without Nuclear Fission set -1. Otherwise set a*a, then `shiftSigned(v,clampAB(p-3,-3,0))`. With Nuclear Plant, or only if absent SDI Defense, double. |

## Ranking, exceptional slots and suppression

### Ranking

For every commodity ascending, if `S<D` and `S>0`, set S=0. Otherwise, if `S>=D`
and `D>0`, set D=0. Negative scores remain negative; zero is eligible.

Sort the supply score/identity pairs ascending by signed score, then the demand
pairs. The original repeatedly scans adjacent pairs from index0, swaps the first
strict inversion, and restarts from0. No equal-score swap occurs. Thus it is
stable ascending order; selecting from its high end favors **higher original
commodity identities on ties**. Both shared score arrays are reordered in place,
so after this step their positions are ranks, not commodity IDs.

For supply, start cursor=15. For each output slot0..2, walk downward over negative
scores and publish the first nonnegative score's identity; resume below it for
the next slot. Then do the same for demand, starting a fresh cursor=15. Preserve
each output publication as it occurs. If the cursor leaves its sixteen entries,
require explicit read backing or stop; do not invent an extra commodity.

### Exceptional middle entries

Use current city coordinates and captured advance count:

```text
if a < 32:
    extraDemand = (5*y + 3*x) % 14
    extraSupply = (7*y + 13*x) % 14
else:
    extraDemand = (5*y + 3*x) % 9 + 5
    extraSupply = (7*y + 13*x) % 9 + 5
if extraSupply == extraDemand: extraSupply = -2
```

Remainders are signed. Starting with no advanced-presence flags, scan the three
current pairs ascending. Mark advanced demand/supply separately when their signed
entry>13. If either entry equals extraDemand, set extraDemand=-2. Then, using
the now-live extraSupply, if either equals extraSupply, set extraSupply=-2.

If extraDemand<0 and there was no advanced demand:

1. Query Industrialization. If known and extraDemand is odd, choose Oil.
2. Otherwise query Nuclear Fission; if known and extraDemand is even, choose
   Uranium. Otherwise retain the negative extraDemand.

If resulting extraDemand>=0, overwrite **demand slot1** with its retained byte.
Do not reorder or perform a second collision check.

If extraSupply<0 and there was no advanced supply, first set it to
`14+(extraSupply modulo2 using its low bit)`. If this is Oil without
Industrialization, set -1. If it is Uranium without Nuclear Fission, set -1.
If the result is nonnegative, overwrite **supply slot1**. These extra choices
can introduce Oil even when its normal supply score was -1, and can introduce
Uranium demand through the collision sentinel. They are not random choices.

### Blocking goods already used

Scan outgoing routes ascending through the live signed route count. For each
nonnegative cargo g:

1. For supply slots0..2, negate every entry exactly equal to g.
2. Retain the route partner. Scan that partner's routes ascending; whenever its
   partner equals this city, replace a retained reciprocalCargo with that cargo.
   Start reciprocalCargo at Food/None(-1); **the last matching route wins**.
3. If retained reciprocalCargo>=0, negate every matching demand entry.

A food route skips the entire three-step block, including reciprocal suppression.
No partner owner, liveness or reciprocal-cargo equivalence test is added here.

Then scan units ascending through the live unit bound. For each live Trade-role
unit whose **unsigned home byte equals the full city identifier** and whose
signed counter is nonnegative, negate each matching supply entry. Do not add
an owner match or a separate no-home check. At city identifier255, the original
unsigned comparison can match a no-home byte; ordinary supported city domains
must be stated. Negating zero never blocks Hides. No re-sort follows suppression.

## Cargo selection

### AI trade unit

Set the unit counter to Food. Consume one primary-law sample; scan the three
supply slots starting at `draw%3` and wrapping cyclically. Copy the first
nonnegative entry into the counter and stop. If the live signed counter remains
negative, consume one more draw, select `draw%3`, and store the absolute value
of that live signed supply entry with byte retention. Set the city's HadCaravan
flag. This does not negate the selected supply or force CommodityDirty.
Normally there is one draw, or two when all supplies are suppressed. A -128
supply can still yield a negative retained cargo byte on the fallback.

### Human trade unit

This is the shared operation called by C06, after unit creation. Construct the
dialog and ensure the city is visible. Repeat:

1. Supply live city name, load CaravanBuilt, append nonnegative supply entries
   in slot order, then Food. A supply option's identity is its **slot**, not its
   commodity. Retain the modal's selected option and extra action.
2. For ordinary choice action, supply the live selected commodity name, or Food
   name, and show CaraConfirm. Result0 accepts; a nonzero result sets the extra
   action to1 and repeats.
3. For another action, a supply slot invokes the commodity-city report below;
   Food invokes NoFoodReport. Repeat while the dialog's live extra result is
   nonzero; the report's returned city does not become a cargo selection.

After acceptance, HelpPressed sets shared zoomRequested=1. For choice<3, store
the absolute value of the **live signed supply entry** in the unit counter,
then replace that live supply with its negative absolute value, both retaining
bytes. Any returned choice>=3 follows the Food branch, setting the counter to
its sentinel without changing supplies; 3 is the nominal Food option. HadCaravan
is not set here. A negative choice follows the original choice<3 supply path,
so its first unavailable read requires backing or a bounded stop, and a later
invalid supply write is excluded. Do not reinterpret it as default Food. The
dialog scope closes on normal return.

## Trade-route primitives

Routes are directional. Mutation does not itself create a reciprocal route.
The delivery operation chooses and adds that separately.

### Ordered route removal

For supplied city and index k, while `k<signed8(liveRouteCount)-1`, copy
partner[k+1] to partner[k], then cargo[k+1] to cargo[k], and increment k.
Decrement the live count with unsigned8 wrap and set CommodityDirty. Leave the
now-unused trailing payload unchanged. Do not recalculate or remove a reciprocal
route. C01 deletion scans other cities' references in descending index order;
C05 uses this primitive for canceled outgoing food support.

There is no original guard. With count0 and index0 the loop does nothing, the
count becomes255, and the dirty flag is set; this is defined scalar wrap.
A negative index or excessive count can require unsupported reads or excluded
writes instead. Stop at that boundary without emulating adjacent-field damage.

### Writing one slot

For a valid slot, store partner with signed16 retention, then
`clampAB(requestedCargo,-1,15)` with byte retention, then set CommodityDirty.
The partner is not validated or recaptured between writes. No route-count
change, reciprocal update or recalculation occurs in this primitive.

### Insertion and replacement

`addRoute(city, destination, requestedCargo)` first scans existing routes
ascending through the live signed count. Return immediately for an exact match
of signed partner to the full destination argument and signed cargo to the
**unclamped full** requestedCargo. Cargo clamping occurs later, so a request16
does not match an existing Uranium15 route before being stored as15.

If the live signed route count<3, capture that count, increment the stored count
with byte retention, and call the slot writer using the captured signed index.
Then refresh commodities for city and destination, in that order. The second
refresh still obeys its own gate; only the first city's slot writer set Dirty.
An invalid append index stops before the invalid write, retaining the earlier
count change.

Otherwise initialize minimum=9999, with **no replacement slot assigned**. Scan
existing routes ascending:

1. If this route's signed cargo<0 and requestedCargo<0, return immediately. This
   prevents a second food route only in the full-list branch; spare slots can
   admit multiple food routes to different partners.
2. Capture partner and that partner's signed base trade as value. Query U03
   connection from the city to partner using the city's live owner.
3. If city has Airport, partner has Airport, and connection<2, replace connection
   with1. Keep short-circuit query order. If connection!=0, add
   `(connection*value)>>1` to value.
4. If the two live owners are equal, shift value right1. If value<minimum,
   replace minimum and the selected slot. Strict comparison preserves the
   **first** minimum on ties.

After the scan, compare minimum to destination's live **unadjusted** base trade.
If minimum exceeds it, return. Otherwise write the selected slot, then refresh
city and destination in order. Equality permits replacement. No Superhighways
bonus, source-city base trade, commodity demand, special food score or new-route
connection adjustment is included in this replacement comparison. C03's recurring
route formula is not substituted.

If no slot was assigned and the final threshold passes, the original reads an
uninitialized replacement index. Require an explicitly supplied index or stop
there, after the destination read. A supplied valid index permits the ordinary
write/refresh continuation; an out-of-range one must not corrupt other fields.
Both reconstructions instead return without replacement in this case (BF-026).
That is a declared repair, not evidence of the original's missing value.

## Entering a city with a caravan

Construct the dialog scope. Autoplay returns0 immediately. Otherwise capture
unit owner c and start action=Deliver. If the unit's **signed home byte** equals
the full city identifier and c is AI, set action=Continue. When the city's live
owner equals c, also set Continue if c is AI and the city has AiFerryPort, or if
the unit has GoTo orders whose target coordinates differ from this city's live
coordinates. The GoTo check applies to human owners too. It applies only to an
owned destination. A signed home comparison here differs from the delivery
operation's unsigned home decoding.

If c is currently human, is the local human, owns the destination, and action
is not Continue, build the CaravanMenu:

1. Supply its live cargo name or Food, load the menu and add the live unit sprite.
2. For nonnegative cargo, append a line naming the destination and stating whether
   **any exact demand entry equals the unit counter**. No absolute-value match
   or commodity refresh occurs here.
3. Add Continue. Add Deliver only if the signed home byte differs from the city.
   Add HelpBuildWonder only if the city's live production is a wonder.
4. Start action=Continue. Show the modal only if Deliver or HelpBuildWonder was
   offered; use its returned action without an additional eligibility check.

Other actors keep the preselected action and do not show this menu. Execute:

| Action | Ordered effects and return |
| --- | --- |
| Deliver | Run the complete delivery operation below. If the live mode is network, broadcast a state diff afterward. Return1. |
| HelpBuildWonder | Play Freight's diesel sound or the other caravan sound. Add `signed live unitTypeCost * unsigned rawShieldBoxRule` to the city's shield stock, retaining signed16. Supply city name and a fresh cost-times-rule value to the notice. Compute `remaining=max(unsigned improvementCost[abs32(live production)]*rawShieldBoxRule-live stock,0)` and supply it. Show AddToWonder. If the live unit has a home other than the None byte, mark that unsigned home city's CommodityDirty. Visibly delete the unit with refresh, then broadcast a diff if network. Return1. |
| Continue, cancel or any other action | Return0; no delivery or donation. |

Donation uses the raw shield rule, not C05's AI-adjusted shieldRows. It does not
finish the wonder, charge gold, recalculate, or refresh C06's build snapshot.
Food caravans can donate too. Its menu eligibility is not retested after the
modal: a changed production item still supplies the later absolute improvement
lookup, subject to actual backing. No rollback restores a unit after a completed
donation/deletion. Close the dialog scope on normal return.

## Delivery: preparation and payment formula

`deliver(unit,destination)` captures unit owner c and destination owner d, each
with signed8 interpretation; retain their selector bytes for later human-mask
tests. If home byte is not None, decode it as unsigned home h. Otherwise query
C01's nearest city at the unit's live coordinates with owner c, no region filter
and no extra owner. If it returns None, use city slot0. This fallback does not
prove slot0 is live; a supported call must supply the required record or stop.

No preliminary full recalculation is made. Use cached base trade and the
following ordered operations, retaining each rounding and wrapping step:

1. Obtain W01 weighted/diagonal distance from destination to h. For LargeMap,
   replace distance with `(4*distance)/5`. Then, independently, for SmallMap,
   replace it with `(5*distance)/4`. Both flags can act if both are accepted.
2. Set `value=((baseTrade[h]+baseTrade[destination])*(distance+10))/24`.
3. Query destination region, then home region, retaining both. If unequal,
   double value. If captured c=d, shift value right1; this does not compare
   the home city's current owner with the destination's current owner.
4. Capture signed cargo from the unit counter and isFreight from its live type
   identity. If Freight, add value>>1. A custom Trade-role type receives this
   bonus only when it occupies the Freight identity.
5. Query U03 connection from destination to h using captured c. If destination
   has Airport and then h has Airport, add1 to connection for equal retained
   regions, otherwise add2. Add1 if destination has Superhighways; add1 if h
   has Superhighways. Add `(connection*value)>>1` to value. These Airport rules
   differ from route replacement and C03 recurring trade.
6. Capture one commodity premium from the current value:

| Cargo | Premium |
| --- | --- |
| Cloth, Coal, Wine, Silver | value/2 |
| Silk, Spice, Gems, Gold | value |
| Oil | (3*value)/2 |
| Uranium | 2*value |
| Hides, Wool, Beads, Salt, Copper, Dye, Food/other | 0 |

7. Scan all three destination demand entries ascending. For **each** exact
   signed match to retained cargo, if the unit's live owner equals captured c,
   set `value=2*value+premium`; otherwise set `value=2*(value+premium)`.
   Do not break on the first match or recalculate premium. A normal unchanged
   owner uses the first formula. Suppressed negative entries do not match a
   positive cargo, though off-nominal negative cargo can match negative demand.
8. If live turn<200, query c's Invention; only if absent query Navigation.
   If both absent, double value. Query Railroad; if known subtract value/3.
   Query Flight; if known subtract the then-current value/3. These are two
   successive reductions, not multiplication by4/9 with one rounding.
9. Consume one primary-law sample, set `floorCap=200+draw%10`, then query c's
   research cost. Set `cap=clampAB((2*researchCost)/3,floorCap,30000)` and
   `payment=clampAB(value,0,cap)`. Retain payment, cargo and isFreight.

This random draw and the complete formula also run for Food, although Food
does not credit money or research. The cap supplies a lower bound to the **cap**,
not a minimum delivery payment. Zero value can pay zero. No external exchange
rate, demand-price table or commodity rarity multiplier is read.

## Delivery: ordered state changes and notices

If captured c is currently human, issue the initial notice operation below.
Then, regardless of owner type or Food:

1. Call the shared attitude adjustment `(d,c,-10)`, then `(c,d,-10)`. Preserve
   two calls even if c=d; the adjustment child owns its government/clamp behavior.
2. For retained Food/negative cargo, request cached-mode C03 calculation for
   destination. Add `((live signed size+1)*live foodBoxRows)/2` to its food stock,
   retaining signed16. This uses C05's **shared incoming** box factor: cached-mode
   calculation does not recompute it for the destination. It can still describe
   another city's prior calculation. No immediate growth occurs. For nonnegative cargo, add retained payment to captured
   c's live treasury with signed32 wrap and to its research progress with
   signed16 retention. If c equals the live local human, update main status.
   No research-completion call occurs here.
3. Capture the unit's current type for later notices. Call U01 visible deletion
   with refresh. Subsequent route work occurs after deletion and its callbacks.
4. Call `addRoute(h,destination,retainedCargo)`, even if the home record's current
   owner differs from c. Route rejection does **not** refund the payment, food
   or unit, nor reverse attitudes.
5. For retained nonnegative cargo, execute reciprocal selection and addition
   below. For Food, finish without that selection or its three draws.

The parent has no unconditional autoplay check. U02's entry command suppresses
delivery in autoplay, but a separately invoked delivery follows this contract.

### Initial delivery notices

Supply cargo/Food name as text argument0, home name as1, destination name as2,
and retained payment as number0. Play the retained Freight/caravan sound before
the dialog. In nonnetwork modes, show Caravan or FoodCaravan with the unit.

In network modes, first handle captured c: if it is the local human, show that
dialog; otherwise, if its captured selector is currently human, send its text
notification to c. If captured c!=d, then handle d the same way, using d's
captured selector and destination recipient. Do not independently notify d
when the entry human-c gate was false. Each notification carries three strings,
one number, the unit identifier and retained isFreight. N02/N05 own receipt and
the recipient mapping; Appendix A records wire identifiers.

### Reciprocal commodity and route

Start bestScore=0 and bestCargo=None(-1). For destination supply slots0..2:

1. Consume one primary-law sample and set score=draw%8.
2. Read its live signed supply. If negative, candidate=its absolute value;
   otherwise candidate=supply and add10 to score.
3. If candidate equals retained delivered cargo, skip it after consuming the
   draw. Otherwise, if score>9, scan home demand slots0..2 and add10 for **each**
   exact signed match to candidate. Suppressed supplies get no demand bonuses.
4. If bestScore<=score, retain score and candidate. **The last tie wins.**

Call `addRoute(destination,h,bestCargo)`. Exactly three draws occur, including
slots that carry the delivered commodity. If every candidate was skipped,
bestCargo remains -1 and the slot writer can create a food route. Do not invent
a different commodity or omit the route attempt. Nonnegative delivered cargo
also remains the gate for the notices below, even if return cargo is -1.

### Reciprocal notices

Proceed only if captured d is currently human and either c!=d or mode is
network. Play the retained caravan/Freight sound again. Supply c's adjective,
delivered cargo name, home name, destination name and return cargo name as text
arguments0..4. A missing return name, including bestCargo=-1 without supplied
name backing, stops here after prior route/payment/deletion effects; it is not
an authorization to invent a Food label for this specific lookup.

If local human=d and c!=d, show CaravanOther using the retained unit type.
Otherwise, if network and local human!=d, notify d. Then, independently, if
network and c!=d: show CaravanOther when local human=c; otherwise notify c when
its captured selector is human and local human!=c. Use five strings, no numbers,
the retained type and the current large-screen presentation flag. There is no
new sprite lookup through the deleted unit. Same-owner nonnetwork delivery
skips this entire operation; same-owner network delivery can prepare text/sound
without showing a local reciprocal dialog.

## Commodity-city report and search

These operations do not recalculate commodities or normalize suppressed values.
An exact three-slot signed membership query returns true at the first equal
entry, false otherwise; it does not compare absolute values.

For `showCommodityCities(viewer,commodity)`, construct the dialog, supply the
commodity name, and load SupplyShow. Perform two passes, each scanning live
cities ascending through the live city bound:

1. **Demand pass.** Admit owned cities; otherwise admit a city with nonzero
   viewer-revealed size, or Palace, or reveal-map mode. Preserve that short-circuit
   order. Include it once if any demand entry exactly equals commodity.
2. **Supply pass.** Admit owned cities or reveal-map mode only; include once for
   any exact supply entry. A Palace or previously revealed size alone does not
   expose a foreign supply list in this pass.

For an included city append its live name, parentheses and size. For a foreign
city append its owner's adjective and comma before size. If reveal-map is off,
the city lacks Palace, and its viewer-revealed size is nonzero, show that revealed
signed size; otherwise show current signed size. An owned city always shows its
current size. Append the pass's Demands/Supplies wording and commodity name;
add an option with the city identifier. After each nonempty pass add a blank
option with None identity. Count real city entries separately from blank rows.

If no real entries were added, invoke SupplyNone with its popup flag; otherwise
show the list modal. Return that child result without automatically opening or
zooming to a chosen city. Destroy the scope on return.

`searchCommodities(viewer)` first requires Trade technology. If absent, return.
Otherwise repeatedly load SupplySearch, append all sixteen commodity names in
identity order and show the modal. A negative selection returns. A nonnegative
selection invokes showCommodityCities; repeat the commodity list while that
report returns nonnegative, return when it is negative. Unknown selections need
actual backing, not a hidden clamp to the commodity domain.

## Discriminating conformance cases

Unless varied, use backed city/unit/rule/region records, stable query results,
King difficulty, no relevant technologies or facilities, raw box rules10,
nonnetwork mode and the declared primary-law samples. Observe intermediate state,
ordered children, both score arrays, city records and controlled choices (plus
context in implementation replay), not only the
displayed payment or selected goods.

### Refresh and selection

For a complete score vector, use map20 by12, city0 at (8,6), size3, owner1,
tribe1, advance count0, region1 with100 tiles, roadBase0, and a radius containing
21 ordinary Plains tiles without resources, River or Road. Thus p=1, X=2, Y=0.
Before competition/sorting, the score vectors in commodity identity order are:

```text
Supply: 0, 0, 0, 15, 0, 0, 0, -42, 4, 1, 0, 0, 11, 2, -1, -1
Demand: 0, 48, 46, 0, 24, 0, 0, 15, 10, 15, 24, 5, 0, 24, -1, -1
```

With no routes or trade units, the final supplies are Cloth/Copper/Gold
and demands Wool/Beads/Gold. The coordinate-derived Copper overwrites the
middle supply. With roadBase1000 and otherwise identical inputs, final demands
become Dye/Copper/Wool. This is sensitivity to a supplied original input, not
proof that either initial word occurs in every original invocation.

| Case | Required result |
| --- | --- |
| Dirty clear, reveal off, turn1/city0, unknown roadBase | Return unchanged; do not demand an input that this path never reads. |
| Active refresh, roadBase5, two Road tiles and one Irrigation-only tile | Q=7. Irrigation alone is not counted. River/resource weights are independent. |
| Active refresh, unknown roadBase, first Road encountered | Stop before that counter increment, retaining prior dirty clear, score initialization and terrain/resource/river operations for that tile. |
| Active refresh, no Road, unknown roadBase | Reach supply scoring and the earlier demand rows; stop before Copper's first Q read. Do not publish new ranked lists. |
| Terrain identity outside the eleven counts | Exclude the invalid count write. Do not let it corrupt the initialized supply-order array. |
| Equal score keys with identity order0,1,2 | Stable sort preserves ascending identity order; reverse selection prefers2, then1, then0. |
| Route or live Trade unit carrying Hides0 | Negation leaves the matching supply entry0. Do not substitute a separate blocked bit that changes later eligibility. |
| Partner has two routes back, cargo14 then13 | The last route suppresses demand13; demand14 is not suppressed by that partner scan. A negative local route skips the reciprocal scan. |
| AI supplies[-1,4,6], first draw0 | Choose cargo4 after one draw; supplies unchanged; HadCaravan set. |
| AI supplies[-1,-4,-6], draws1 then2 | Scan all suppressed slots, then choose absolute cargo6 with the second draw. |
| Human choice selects third available supply slot containing3 | Store cargo3 and supply-3; no selection draw and no HadCaravan publication. Food stores its sentinel without negating a supply. |

### Routes and delivery

For the payment examples, use home city0 at (8,6), destination1 at (12,6),
base trade24 each, owner1 delivering to owner2, same region, no connection,
turn200, research cost600 and a non-Freight Cloth caravan. The W01 distance is3,
initial value26, and the destination demands Cloth exactly once. Use cap draw0.

| Case | Required result |
| --- | --- |
| One Cloth demand | Premium13, payment65, one cap draw. |
| Railroad and Flight known | Starting65, subtract21 then14; payment30. |
| Freight identity instead | Value39 before demand, premium19, payment97. A custom fast Trade type at another identity receives no Freight bonus. |
| Three identical Cloth demand entries | Apply 2*v+13 three times:65,143,299. Premium remains13. |
| Cloth demand entry suppressed to a negative value | No matching positive cargo; payment26. |
| Turn199 with neither Invention nor Navigation | Double65 to130. Knowing Invention skips both doubling and the Navigation query. |
| High uncapped value, research cost1, cap draw9 | Cap209, not a minimum payment209 for smaller values. |
| Initial treasury100, research progress32760, payment65 | Credit treasury165 and signed16 progress-32711 before unit deletion and route attempts. No immediate research completion. |
| Food, destination size3/stock1, shared foodRows7 | Formula and one cap draw still run; add14 food for stock15. Cached-mode calculation leaves foodRows7 intact; no gold/research credit and no reciprocal-selection draws. |
| Ordinary nonfood delivery | One cap draw, then three reciprocal draws after deletion and the forward route attempt. Child callbacks may have their own separately specified draws. |
| Return candidates tie | Last qualifying supply slot wins. Every slot consumes its draw, including a candidate equal to delivered cargo. |
| All return candidates equal delivered cargo | Retain return cargo-1 and attempt that route. A later missing commodity-name read stops after earlier effects; do not silently change its label or route. |
| Exact existing partner/cargo pair | addRoute returns without a write or refresh. Request16 differs from stored15 before clamping, and can add a duplicate stored15 route. |
| Full routes with equal minimal values | First minimum is replaced when new raw base trade equals that minimum. The new destination receives no connection adjustment for this comparison. |
| Full routes, an existing food route, new food request | Return from that scan; spare-capacity behavior differs. Delivery rewards and unit deletion already performed are retained. |
| Every existing route score>=9999, new raw trade<9999 | Return without reading the unset replacement slot. |
| Same existing scores, new raw trade>=9999 | Need supplied replacement index or stop at that read. A valid supplied slot continues; an invalid write is excluded. Retaining all routes is the separately declared BF-026 repair. |
| Remove index0 from route count0 | Count becomes255, Dirty is set, route payload unchanged. No array write occurred. |
| Append with signed route count-1 | Stored count wraps to0 before the attempted slot-1 write. Exclude that write and stop; preserve count0. |
| Human HelpBuildWonder, type cost5/raw shield rule10, stock10 | Add50 for stock60; consume unit, mark its live home Dirty and synchronize if network. Do not finish the wonder or refresh C06's snapshot. |
| Autoplay caravan entry | Return0 before menu, payment and deletion. A standalone call to delivery has no such entry gate. |
| Owned city before a GoTo unit's actual target | Continue without the caravan menu or delivery. Foreign cities do not receive this own-city suppression. |
| Foreign city with revealed size and matching supply/demand | Appear in the demand pass, not supply pass, unless reveal-map mode allows the latter. No commodity refresh occurs. |

Additional conformance work must exercise every technology/tribe modifier,
resource/polar/wrapped radius, custom signed-size and score overflow, supplied
negative road inputs, middle-entry collision variants, home-byte boundaries,
route-query mutations, modal cargo changes, negative/name backing stops, donation
retargeting, foreign human notifications, real network receipt and consecutive
city/civilization turns. The unknown original locals remain inputs, not constants
inferred from one passing run.

## Appendix A — Representation and excluded bugs

These encodings support original-program validation and persistence; they do not
prescribe cleanroom record layout.

| Detail | Mapping or evidence use |
| --- | --- |
| Commodity identities | Hides0, Wool1, Beads2, Cloth3, Salt4, Coal5, Copper6, Dye7, Wine8, Silk9, Silver10, Spice11, Gems12, Gold13, Oil14, Uranium15. Names may be localized/customized; formulas attach to identities. |
| Tribe identities | Wine bonus French9; Silk bonuses Chinese11 and, later, Aztecs10; selected-metal demand Spanish17. Custom tribe names do not redirect these tests. |
| Advance identities used here | Automobile5, Chemistry10, Combustion14, Computers16, Economics22, Electricity23, Flight30, Industrialization37, Invention38, Iron Working39, Mass Production48, Navigation57, Nuclear Fission58, Pottery65, Railroad67, Refrigeration70, Trade84. |
| Facility/wonder identities used here | Palace1, Marketplace5, Aqueduct9, Bank10, Cathedral11, Mass Transit13, Factory15, SDI Defense17, Recycling Center18, Power Plant19, Hydro Plant20, Nuclear Plant21, Superhighways25, Solar Plant29, Airport32; Michelangelo wonder10. [C08](C08-buildings-and-wonders.md) owns effect/presence queries. |
| Terrain identities | Desert0, Plains1, Grassland2, Forest3, Hills4, Mountains5, Tundra6, Glacier7, Swamp8, Jungle9, Ocean10. After the survey Glacier is merged into Tundra. |
| Unit identities | Trade role7; Freight type49. Food counter255 reads as signed -1. Home255 is None for delivery decoding; entry comparisons and suppression scans have the distinct signed/unsigned behavior stated above. |
| City route representation | Count BYTE at58, supplies59..61, demands62..64, route cargo65..67, three signed-WORD partners68..73. CommodityDirty is original flag `0x20000`; HadCaravan `0x1000`; AiFerryPort `0x200`. HadCaravan is not a dirty-list trigger. |
| Shared score arrays | Sixteen signed DWORDs each: demand `0063f540`, supply `0063f668`. Sorting mutates scores and identity pairs; score positions afterward are ranks. |
| Road counter | Unwritten incoming DWORD at EBP-128h in `0043d400`; original `0043d780` tests feature `0x10` and `0043d788` increments it. W01 identifies this feature as Road. Independent source/notes call it irrigation, but their actual feature test agrees. Primary B1's historical local/probe names are not an alternative field contract. |
| Route replacement local | EBP-1ch in `00440453`, assigned at `004406e7` only for a score strictly below current minimum; consumed at `0044071e` after the final destination test. Both reconstructions' BF-026 guard is a deliberate repair. |
| Terrain count overflow | Original index11..15 writes beyond eleven counters into ranking metadata. Excluded bug; do not reproduce logical metadata corruption. |
| Invalid route-slot writes | A missing or malformed slot can address adjacent supply/demand or city fields. Exclude the write even if bytes exist. Preserve scalar count changes and valid earlier effects. |
| Menu option identities | Entry Continue0, Deliver1, HelpBuildWonder2. Human cargo supply slots0..2, Food3. Caravan/FoodCaravan label64 and production-Food label192 are distinct lookups. Supply/ demand report wording labels86/87. |
| Sound/presentation | Freight diesel22; other delivery caravan42; non-Freight wonder donation44. Reciprocal notices use flag0 for small screen,8 for large. Presentation policy belongs to P10/P06. |
| Map-size flags | LargeMap `0x04`, SmallMap `0x08`; tested independently in that order for distance scaling. |
| Delivery text messages | Network notification15 Caravan,16 FoodCaravan: three strings, one number, unit ID and isFreight. Reciprocal17 CaravanOther: five strings, no numbers, retained type and presentation flags. N02/N05 own encoding/receipt. |

A zero road-base repair avoids dependence on uninitialized stack state but can
change demand, reciprocal cargo and payments. A no-candidate route repair avoids
an unresolved index and possible overwrite but can also skip a valid supplied
slot's original continuation. Neither is silently enabled by the global exclusion
of out-of-bounds writes. That exclusion applies when the actual destination is
outside its intended object; it does not erase defined in-bounds scalar results.

## Appendix B — Evidence and validation

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`. Relevant bodies were verified unchanged
in the read-only reference checkout used for this document.

| Pinned source | Entries and scope |
| --- | --- |
| [City routines](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp) | Commodity report/search `0042b824/0042bc47`; commodity refresh `0043d400` and four extracted continuations; route removal/write/add `00440325/004403ec/00440453`; C03 commodity call and C06 human-cargo continuation. |
| [Unit routines](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp) | Delivery `00440750` and three extracted continuations; AI cargo selection `004ec1c6`; caravan entry `0058fedb`; movement caller and U01 visible deletion. |
| [Numeric helpers](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/support.cpp) | Stable pair sorting `00414f02`; F01 signed shift `005ae3ec`. |
| [Calculation wrapper](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/retvals.cpp) | C03 recalculation `004eb4ed`; cached-mode entry does not recompute foodRows. |
| [Encodings](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h), [strings](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/gamedata.cpp), [bug ledger](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md) | Identity/feature mappings, shared text section names, and the always-enabled B1 reconstruction repair. |

Independent reconstruction was read at `civ2` revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`: `src/city_commodities.cpp`,
`city_deletion.cpp`, `city_trade.cpp`, `city_trade_membership.cpp`,
`city_commodity_report.cpp`, `city_production_turn.cpp` and `trade_delivery.cpp`.
Its BF-001/BF-026 ledger and current commodity investigation distinguish supplied
inputs, original stack history and intentional repairs. Referenced archived
exports were not opened; historical native comparisons in those notes were not
rerun or counted as this document's validation. Its terrain/routing alias
adapters are evidence of original damage, not cleanroom requirements.

Read-only instruction ranges were inspected in the original `Civ2/civ2.exe`,
SHA-256 `edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
The `0043d400` prologue/survey establishes the missing road-counter initializer
and Road feature test. The `00440453` replacement scan establishes minimum9999,
strict candidate assignment and the later unresolved slot read. Additional
delivery/entry ranges were retained for traceability. No original executable
was run, and no reference source, assets or saved analysis were modified.
The stock tribe names were checked against the local `RULES.TXT` leader order;
the fixed identity tests, not those editable display names, control the formulas.

The external host fixture adds **17 source bodies**—ten original entries and
seven extracted private continuations—to the previous168 bodies. It replaces
commodity refresh, AI cargo choice and commodity-report adapters with actual
scoped source, retaining the actual C01 removal and C06 human cargo operation.
Identity arrays and shared scores use explicit signed32 cells; wide host pointer
carriers are not an original process model. The original missing road and route
inputs are explicit supplied values or bounded stops. Invalid terrain/route
writes are stopped, and unsupported ranking reads are stopped. The primary's
zero-counter and no-candidate repairs are not misrepresented as original behavior.

**135 C07 assertions passed** in optimized and AddressSanitizer/UndefinedBehaviorSanitizer
builds. They cover complete score vectors and ranking sensitivity, unknown-input
prefixes, excluded terrain/slot writes, suppression and draw counts, route
selection, payment modifiers, research wrap, actual deletion and route creation,
food's retained shared factor, donation, entry cancellation, report visibility
and a complete city-turn parent. **156 C06 regression assertions also passed**
in optimized and sanitizer builds with the new actual commodity bodies.
These finite cases do not establish every custom input or real UI/network path.

The fixture's road-connection answer, advance/facility/wonder/research-cost providers, selected
UI choices, text, sound, redraw and network transport remain controlled children.
Actual source includes the resource predicate, U01 deletion, C01 route removal
and C02–C06 calculation/turn bodies. Map access/distance use bounded backed
fixtures. This does not establish complete C01 creation/transfer, original
native UI callbacks or a complete network game. Tests and reference
source remain external research material, not implementation code imported here.

Remaining work: C08/V03 must join actual effects and research-cost/advance rules;
U03 must join the actual connection search; A05/A07 must use these cargo and
delivery rules through real AI choices. P04/P06/P07/P10/N02/N05 need populated
reports, modal transitions, messages and receipt. F04 needs consecutive turns
with declared original road-base history or an explicit initialization repair.
X02 must preserve full-state/trace vectors, unresolved-read prefixes, retained
scalar wraps and safe behavior at excluded writes. These finite checks do not establish complete original-program or whole-game
conformance.
