# U03: Pathfinding and goto

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
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

Search queue priority, first/last selection, retained costs, shared path workspaces
and direction ties remain semantic state. Parallel cost calculations must not
change the frontier, cache publication, returned path or reached failure boundary.

## Target and scope

This specification describes classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3** path-cost search, retained search state, coarse connectivity,
waypoints, goto direction selection and the road/rail connection query. Appendix B
distinguishes source checks from original-program
and whole-game conformance.

Use [F01](F01-determinism-and-arithmetic.md) for signed reductions, shifts and
integer arithmetic; [F02](F02-game-state-and-invariants.md) for units, rules and
shared state; [W01](W01-map-geometry-and-spatial-state.md) for coordinates,
distances, direction tables, tile/region/ownership and peace-territory queries;
[U02](U02-movement-and-zones-of-control.md) for movement allowance and
zone-of-control queries. [U01](U01-unit-lifecycle-and-stacks.md)
owns stack-role counts. [U04](U04-unit-orders-and-activation.md) sets orders and
path owner around goto execution, consumes the returned direction, and performs
the actual move. A returned direction is an estimate, not permission to enter.

C07 owns the economic use of the road-connection result. W02 invokes the coarse
builder during map generation; F05 and
[A03](A03-ai-settlement-and-terrain-work.md#road-link-finder) invoke uniform-cost
directions when planning roads. These callers retain their own input setup, mutation and timing.
This document does not add a route object or a path invalidation service.

Nominal inputs supply a valid map, canonical coordinates, valid rule/region
records and the incoming shared workspace. Unit goto validates its destination,
not its actor position. Off-map actors and direct helpers outside the local
search window therefore need separately stated read domains; they are not made
valid by destination validation. [X01](X01-classic-compatibility-contract.md)
excludes out-of-bounds writes and their corruption. All defined scalar wrap,
in-bounds queue behavior and cache effects below remain part of the contract.

No search step draws random numbers. Coarse-build message pumping and optional
debug presentation can invoke external algorithms; retain their effects and
call order rather than assuming an inert callback.

## State and notation

Use [W01's ordered neighborhood tables](W01-map-geometry-and-spatial-state.md#3-ordered-neighborhoods).
Call its ordinary tile directions `D[0..7]`, with center D[8], and its coarse
graph directions `C[0..7]`. These tables differ: for example D[1]=(2,0), while
C[1]=(1,−1). Snap uses D in coarse coordinates; graph traversal uses C. W01
also owns `wrapX` and `wrapCoarseX`, the single horizontal corrections using
map width W and coarse width CW respectively; neither wraps y.
`stepDistance` and `weightedDistance` mean W01's two distinct distances.

| Shared state | Meaning and retention |
| --- | --- |
| Goal | Current requested tile goal, or temporary coarse coordinates during snapping/BFS. |
| Unit-rule selector | Rule slot consulted by local search. Distance queries change it to the fixed Warriors/Caravel slot and do not restore it. |
| Path owner | Nonnegative civilization enables occupancy restrictions during flood. Negative disables that gate; it does not suppress every later owner-dependent query. |
| Uniform-cost mode | Called road-planning in the reference. It overrides the ordinary base cost with one; flood penalties still apply as described below. |
| Roads-only mode | Rejects the ordinary no-road/no-rail/non-Alpine branch. Uniform-cost mode bypasses it. |
| Grid center | Set to the current goal at each local query, even on a cache hit. |
| Cached flood goal and costs | Retained across calls. Zero cost means unreached; the seed has cost one. No cache key for owner, rules, source, limit, flags or map version exists. |
| Result cost | Reset to zero at local-query entry; fresh flood sets it to the limit and lowers it when reaching the source. A reused grid leaves it zero. |
| Queue/workspace | Shared by local flood and coarse BFS, with different limits and termination rules. Queue contents and endpoints remain after return. |
| Coarse land/sea graphs | Sets of outgoing directions for each CW×CH cell. They change on explicit rebuild; changing terrain does not implicitly rebuild them here. |
| Coarse distances | A third CW×CH layer, cleared for each coarse BFS. Values retain modulo 256; zero also means unvisited. |
| Coarse start | Last successfully snapped source cell, retained by waypoint selection. |
| Debug flags | Requested and pending flags. Waypoint search can publish pending; unit goto clears both on normal return. |

Cost expressions and comparisons have F01 reference results. Preserve any
reduction that affects selection, returned cost or shared state. Road multiplier r is unsigned; terrain
movement, unit movement and role are signed rule values. Unit movement is already
scaled by [F03](F03-rules-and-configuration.md); do not multiply it by r again. A larger host integer
must preserve significant reduction effects; a proved redundant conversion
need not execute. Queued x coordinates are unwrapped; tile
queries wrap them once. Define `abs32(v)` as v when signed v>0, otherwise
`signed32(-v)`; thus the most negative signed32 value retains that same value.

The logical local grid has 48×48 positions around its center. For an unwrapped
point let `dx=x-centerX`, `dy=y-centerY`, `h=floor((dx+dy)/2)` using F01's
arithmetic shift. Then `column=24+h`, `row=24+dx-h`. For canonical parity these
are `(dx+dy)/2+24` and `(dx-dy)/2+24`. This finite logical domain and the search
window affect behavior; a new implementation need not use the original array.

Define the window test on the **supplied unwrapped** point:

```text
extent = signed32((abs32(x-goalX) + abs32(y-goalY)) with its low bit cleared)
insideWindow = extent < 48
```

For normal coordinates this is `2*floor((absDx+absDy)/2)<48`. For malformed extreme
values use the wrapped abs32 above and F01's signed comparison, then reject/stop
an invalid write as required by X01; arithmetic admission does not prove a valid
grid address.

For a grid lookup near a cylindrical seam, normalize x relative to center:
subtract centerX; add W if negative; then subtract W if at least `floor(W/2)`;
finally add centerX. These are ordered single corrections, not a general modulo.
The direction picker performs its window test **before** this normalization.

## Local next-direction query

`nextDirection(sourceX,sourceY,limit)` operates on the shared goal and selectors:

1. Capture pending-debug; set grid center to the live goal. Capture naval as
   selected rule domain equals Sea, and terrain-cost mode as
   `r < signed selected-rule movement`. This uses raw rule movement, not U02's
   allowance after damage, veteran, wonders or fuel effects.
2. Capture startsOnLand as naval and source not Ocean. Initialize result cost
   zero. Normalize source x relative to center as above.
3. If either goal coordinate differs from cached flood goal, or the source's
   grid cost is zero, run the fresh flood below. Short-circuit the goal tests
   before the grid read. Otherwise reuse the complete incoming grid.
4. Initialize chosen direction absent. Only if result cost is **strictly less
   than limit**, run direction selection below. A reached source exactly at the
   limit cannot yield a direction from a fresh search.
5. If no direction, assign shared goal from cached flood goal. If captured debug
   was nonzero, run the debug interaction after that assignment. Return the
   chosen direction, normally −1 or 0..7; do not clear debug flags here.

A per-call owner scratch value survives between flood and direction selection.
The original does not initialize it. Supply its incoming value when a later
comparison can use it; an occupancy lookup overwrites it. It is not a persistent
cache field. Both reconstructions' zero initializer is a preset/repair, not
proof that the original always began with zero. Uniform-cost selection does not
read it for penalties. See Appendix B for evidence and validation limits.

### Fresh reverse flood

Copy goal to cached flood goal, clear the cost grid, initialize queue with goal,
write its cost one, and set result cost to limit. The queue is FIFO with capacity
2304; head and tail each advance cyclically. Process at most the first 2304
removals. Do not replace this with a priority queue or an unlimited flood.

On each removal read the point's **current grid cost**, rather than a cost saved
when it was enqueued. If it exceeds current result cost, skip expansion. Otherwise,
if wrapped x and y equal the source, set result cost to this cost and do not
expand it. Otherwise capture at this point, in order:

- hasRoad: Road feature, or any tile city owner;
- terrain flags, including River;
- hasRail: Railroad feature, or any tile city owner.

For directions 0..7 form neighbor from unwrapped point, wrap x for map access,
and apply these tests in order:

1. Unwrapped neighbor passes the window test; wrapped tile is in bounds.
2. Neighbor Ocean status equals captured naval, **or** it is the source tile,
   **or** it equals cached flood goal. Endpoints can have the other terrain.
3. If captured startsOnLand, reject region 63 and regions whose W01 water count
   equals 1. This gate also applies to endpoints. An invalid region lookup is
   a separate read boundary, not a fabricated water count.
4. Start candidate cost at removed point's cost. With live path owner >=0,
   query city-or-unit owner into owner scratch. Reject an occupied foreign tile;
   alliance does not bypass this rejection. Then apply the two flood penalties
   below. With negative path owner, skip this entire owner-query/penalty block.
5. Add the base step cost below; a roads-only refusal skips the neighbor.
6. If live selected type has Coast-bound and the neighbor's resource-suppression
   marker is set, add `4*r`. This tests the stored suppression marker, not an
   actual distance-to-coast calculation, and applies even in uniform-cost mode.
7. Read the neighbor's grid cost using its unwrapped x. If zero or greater than
   the candidate cost, store the candidate and append the coordinates. Equal
   nonzero cost does not enqueue again. After advancing tail, if it equals head,
   return from the flood immediately; preserve the written cost and queue entry.

Otherwise stop when the queue empties or head wraps to zero after its 2304th
removal. The ordinary end-of-iteration result-cost update is already represented
by the source-hit assignment; enqueueing a higher cost does not raise the cutoff.
Costs above the cutoff can be stored and remain observable even though those
points are not expanded. Duplicate pending coordinates are possible.

The flood's owner penalties, before the base step, are:

- If path owner equals owner scratch, that owner is AI-controlled, neighbor has
  Unit-present but neither City nor Fortress component, and U02's adjacent
  nonallied-unit query is true: add `16 * U01.stackCountRole(firstUnit,live role)`.
  Human membership uses F01's masked shift and the full membership result.
- Query W01's peace-territory owner with live path owner. If present and signed
  selected rule role <5, add `4*r`. This is a cost penalty, not impassability.

### Base step cost

Use this branch order with the captured from-tile Road/Rail/River properties
and the neighbor's live properties. For local search this is **not restricted
to Land domain**:

| First matching condition | Base cost/action |
| --- | --- |
| Uniform-cost mode | 1 |
| From hasRail and neighbor has Railroad or any city owner | 1 |
| Selected rule has Alpine | 4 |
| From hasRoad and neighbor has Road or any city owner | 4 |
| Roads-only mode | Refuse this neighbor |
| Both tiles have River, wrapped absolute x difference is 1, absolute y difference is 1 | 4 |
| Captured terrain-cost mode | `signed neighbor terrain movement * r * 4` |
| Otherwise | `4*r` |

Railroads are deliberately positive here. Flood costs use the neighbor's terrain
while expanding **from the goal toward the source**; do not reinterpret them as
forward movement costs. No exploration/visibility check is added. City presence
supplies Road and Railroad connectivity regardless of whether those feature
bits are set.

### Choosing a descending neighbor

Initialize best score and best tie distance to 19999. Capture source hasRoad,
terrain flags, hasRail in that order, then source grid cost at normalized x.
Scan directions 0..7 from the **original source coordinate**:

1. Test the unwrapped neighbor against the window before normalizing; wrap x for
   bounds and terrain queries. Require matching Ocean/naval status, or source,
   or cached flood goal. Do **not** repeat startsOnLand's region/water-count gate.
2. Normalize neighbor x relative to center and read its grid cost. Only for
   nonzero cost and nonnegative live path owner, query city-or-unit owner into
   owner scratch and reject a present foreign owner. This lookup precedes the
   descending-cost test, so even a rejected descent can change owner scratch.
3. Require nonzero neighbor cost strictly smaller than captured source cost.
4. In uniform-cost mode use step cost one and skip all selection penalties.
   Otherwise use the base-cost table without its uniform branch, then add the
   Coast-bound/suppression penalty. Apply the same AI-stack predicate as above
   with **8**, not 16, times the role count. Its equality uses owner scratch
   even if path owner is negative and no owner lookup assigned it this call.
   Finally apply the peace-territory penalty with live path owner, also when
   negative. The queried child determines how that sentinel is handled.
5. Score is neighbor grid cost plus that step cost. If score <= best score,
   compute weightedDistance from live goal to the unwrapped neighbor. Replace
   for strictly smaller score, or equal score and strictly smaller distance.
   Exact ties retain the earlier direction. Initial values 19999 are real
   thresholds; do not replace them with mathematical infinity.

The cache can therefore be accepted yet yield no direction under current local
checks. It is not reflooded in response. A path computed for another unit, owner,
map state or cutoff remains usable whenever the goal/source-cost test passes.

## Bounded distance query

`pathLength(source,goal,bySea,limit)` computes W01 ordinary step distance first.
Zero distance returns zero without a search. Distance >=23 returns −1. Otherwise:

1. Select the rule in the Warriors slot for bySea=0, or Caravel slot otherwise.
   These are fixed slots, even when renamed or given customized domains.
2. Publish the goal, temporarily set path owner absent, and call nextDirection.
3. If its returned direction is nonnegative, return the **live shared result
   cost after the child**, otherwise return its negative result.
4. Restore the incoming path owner on normal return. Do not restore rule selector,
   goal, cost grid, cached goal, queue or result cost. No scope-wide reset occurs.

The owner is unchanged on the zero/distant early returns. Uniform/roads-only
flags are inherited, not set by this helper. On a fresh unpenalized uniform-cost
search, the result is one plus the number of moves. On a cache hit a successful
query returns **zero**, so it is not a general shortest-distance API.

## Coarse connectivity and waypoints

### Choosing a representative

Use [W01's two-position terrain selection](W01-map-geometry-and-spatial-state.md#two-position-terrain-selection)
for `representative(baseX,baseY,wantOcean)` and its raw-region-returning variant.
The base for a coarse cell is `(4*cx+1,4*cy+1)`. Keep the helpers' ordered probes
and unchanged output coordinates on failure; each caller below separately
specifies whether it checks success or retains an initialized base.

### Rebuilding the graphs

Set uniform-cost mode true. Build land then sea, clearing each graph before its
pass. Iterate coarse x as the outer loop, coarse y as inner; base is
`(4*cx+1,4*cy+1)`. Get a representative and its raw region ID; skip absent ones.
For each direction 0..3 of **C**:

1. Add four times C's offset to the base, wrap x once, and require tile bounds.
2. Find neighbor representative in the same two-position order. Require its
   raw region equals the first representative's region.
3. Call pathLength between the representatives with bySea for this pass and
   limit 5. Only a result **strictly between zero and five** creates an edge.
4. Add direction d to the current cell. Separately wrap adjacent coarse x and
   check both coarse coordinates; if valid add reverse direction `(d+4) mod 8`
   to that cell. This later check does not undo the first edge.

After each complete coarse x column, pump UI/network messages, including empty
columns. Callback changes remain visible to later columns/queries. Finally set
uniform-cost mode false unconditionally; do not restore its incoming value.
Leave the other workspace, including rule selector and cached flood, as produced.

With ordinary rules and a fresh uniform search, only one-, two- or three-move
routes qualify. A cached successful query returns zero and creates no edge.
Customizing the fixed Warriors/Caravel domains or Coast-bound flag changes the
calculation. The graph domain chooses representatives; the rule domain decides
search admission. This builder does not reset path owner globally: each distance
query temporarily suppresses and then restores it.

### Snapping a tile to a coarse cell

`snappedCell(x,y,bySea)` computes `(cx,cy)=(x>>2,y>>2)`. Read its chosen graph
cell directly before any rectangle check. Nonzero outgoing connections make it
an initial candidate, but it succeeds only if its representative exists and
`pathLength(representative,(x,y),bySea,18)>=0`. A zero cached result qualifies.

If that attempt fails, initialize best distance 99 and scan directions 0..7
using **D offsets in coarse coordinates**, not C:

1. Candidate is `(wrapCoarseX(cx+D[d].x),cy+D[d].y)`; require coarse bounds and
   nonzero outgoing connections in the chosen graph.
2. Require candidate base tile `(4*nx+1,4*ny+1)` in map bounds. Compute weighted
   distance from the original tile to that **unadjusted base**. Continue only
   if strictly smaller than best distance.
3. Find the two-position representative; require successful pathLength from
   that representative to the original tile, limit 18. On success retain this
   direction and the already-computed base distance. Equal distances keep the
   earlier direction.

Publish shared goal as the chosen coarse cell. The initial containing-cell
success uses D[8]; complete failure also publishes the containing cell via D[8],
but returns false. Success returns true. The initial direct cell read and the
published fallback do not prove every input tile has a valid coarse cell.

### Selecting a waypoint

`waypoint(source,target)` captures requested-debug, publishes target as goal and
captures bySea from the current selected rule domain. Snap source. On failure,
restore target as goal, set pending-debug if captured request was nonzero, and
return false. On success retain the snapped source and publish coarse-start.
Snap target next; **ignore its success Boolean**, using its published cell.

Clear coarse-distance layer. Initialize a FIFO ring of **256** entries with the
snapped target and distance 1. Only this ring's first 256 shared queue positions
are used; increment head/tail modulo 256. Repeatedly:

1. Remove a cell and advance head. If it equals snapped source, set found true
   and stop without expanding it or loading its distance.
2. Read its unsigned retained distance into `tieScore`, then its outgoing
   connection set from the graph selected by captured bySea.
3. For each connected C direction 0..7, compute/wrap neighbor and require
   coarse bounds and distance zero. Set its distance to `(tieScore+1) mod 256`,
   enqueue coordinates and advance tail.
4. After the entire neighbor loop, stop if head equals tail; otherwise repeat.

There is no full-queue check inside the neighbor loop and no general work cap.
Wrapping can overwrite **in-bounds pending queue entries**; this is a defined
queue-state quirk, distinct from an excluded out-of-bounds memory write.
Distance 255 advances to zero and can be treated as unvisited again. Do not
silently widen distances or make the queue unbounded. Outside the stated finite
cases, termination is not guaranteed by this specification.

If found, choose an outgoing neighbor of snapped source, scanning C[0..7], with
valid coarse coordinates and nonzero recorded distance. Begin best distance 99
and no direction. A smaller distance replaces the direction and best distance,
and writes tieScore as weightedDistance from original target to that neighbor's
**unadjusted base tile**. On equality with best distance, replace only if that
weighted distance is strictly smaller than tieScore, then update tieScore.

Before the first replacement, tieScore retains the last expanded BFS cell's
distance. Thus a first candidate of distance exactly 99 uses that value, not
an invented infinity. If the very first removed cell is already the source,
only the seed has a nonzero distance; a qualifying neighbor must first beat 99
and initialize tieScore. Exact completed ties retain the earlier direction.

For a chosen direction, compute its wrapped coarse neighbor's base tile,
initialize waypoint to that base, then attempt representative adjustment. Ignore
failure, leaving the initialized base. Publish waypoint as shared goal.
Without a chosen direction publish original target, even if found is true.
Finally set pending-debug when captured requested-debug was nonzero, and return
found. Return true therefore does not guarantee an intermediate waypoint.

## Unit goto direction

`gotoDirection(u)` begins with result −1. Capture target coordinates, actor
coordinates, actor domain and publish actor type as path unit-rule selector.
If target is out of map bounds, or target equals actor position, return −1
without clearing orders or exhausting. On every normal return clear pending and
requested path-debug flags; shared goals/caches otherwise remain as produced.

For other targets capture owner and full human-membership result. For an
adjacent target (`stepDistance<2`), derive direction from signs of dx and dy.
On a cylinder override dx to −1 when target is in the last two x columns and
source in the first two; override to +1 for the converse. Use W01's sign-direction helper, including its default 8 and last-match rule. This fast path
performs no route-cost, occupancy or terrain legality check.

For a farther target:

1. Air skips local and coarse searches, using target directly as shared goal.
2. For other live domains, if ordinary distance <23, publish target and call
   nextDirection with limit 999. A nonnegative result returns immediately.
3. Otherwise, or after that failure, call waypoint. If it returns true and
   ordinary distance to its published goal is <23, call nextDirection with
   limit **998**. If found but too distant, set result −1. A nonnegative result
   returns immediately. A false waypoint result proceeds directly to fallback.
4. Use the current shared goal for greedy fallback below. The original target
   remains captured separately for the own-city destination exception.

Snapping's pathLength calls can change the selected rule to Warriors/Caravel.
There is **no restoration to the actor's type before step 3's second local
search**. Greedy fallback instead reads the actor's actual unit type. Preserve
this distinction; treating all phases as one immutable unit profile changes
routes.

### Greedy fallback

Compute dx,dy from actor to shared goal. On a cylinder, when abs(dx)>W/2, shorten
its magnitude to `W-abs(dx)` and reverse its sign; equality does not wrap.
For nonnegative magnitudes a,b define the original scaled distance:

```text
h = arithmeticShiftRight(signed32(a+b),1)
G(a,b) = a>b ? signed32(2*a - (h-b+1))
             : signed32(2*b - (h-a+1))
```

This is the exact expression, not `2*weightedDistance`. In particular G(0,0)=−1.
All intermediate arithmetic retains signed32 behavior. If dx=dy=0, set orders
None, exhaust the unit, and return the retained negative result.

Otherwise capture source Road features, terrain flags, Railroad features in
separate queries and then source enemy-ZOC result. Initialize best score 9999
and direction 8. For d=0..7:

1. Form map neighbor with wrapped x, plus remaining magnitudes
   `a=abs32(dx-D[d].x)`, `b=abs32(dy-D[d].y)` and g=G(a,b).
2. A human actor requires g<=G(abs(dx),abs(dy)); AI may move farther away.
   Require map bounds. Live Air cannot choose a tile with any airbase owner
   unless it equals the **shared waypoint goal**.
3. Capture terrain and tile unit owner. Admission is either:
   - occupant absent/own, and either live Air, or matching Ocean status against
     the **captured actor domain**, with source not in enemy ZOC, destination
     not in enemy ZOC, or live Ignore-ZOC property; or
   - an own city exactly at the **original captured target**.
   The own-city exception can bypass domain, ZOC and unit-occupant rejection.
   It does not bypass the earlier human, map-bounds or Air-airbase gates.
4. Compute the greedy movement estimate below. Score is
   `signed32((g*4 + estimate + a+b)*4)`. If g!=0 and the captured unit owner is
   present, add one. Since G(0,0)=−1, that occupancy increment can apply at the
   destination too. Strictly smaller score replaces; ties keep earlier d.

For live Land, greedy estimate is 1 if **both Railroad feature bits** are set;
otherwise 4 if both Road bits; otherwise 4 for a diagonal River connection;
otherwise query U02 allowance. If allowance>r use signed destination terrain
movement×r×4, otherwise 4×r. For all other live domains use 4×r. Unlike local
search, this estimate does not synthesize road/rail from cities, use Alpine,
apply Coast-bound penalties, or consult uniform/roads-only modes. Its allowance
query can include current damage/modifiers that local search's raw movement
comparison omitted.

After scanning, a selected direction that reverses nonnegative signed last
move direction (`lastDirection XOR 4`), with non-Air live domain and nonzero
spent movement, is abandoned. Direction 8 (no candidate) is also abandoned.
Both cases set orders None and return −1. AI units exhaust. Human Automated
units clear Automated and exhaust; other humans retain spent movement. Air
bypasses the reversal refusal but not the no-candidate refusal. No direct
movement or goal-arrival flag change occurs in this selector.

## Road/rail connection query

`roadConnection(civ,source,target)` returns 0 absent, 1 road, or 2 all-rail.
Require equal land-only region IDs and ordinary distance <23; otherwise return
0 without changing path mode/workspace. Equal absent land-region IDs pass this
comparison; no extra land-domain validation is inserted.

Set uniform-cost true, path owner absent, goal target, unit-rule Warriors and
roads-only true. Initialize current position=source, blocked=true, allRail=true,
intermediate counter zero. Repeatedly call nextDirection(current,99):

1. Negative direction or direction 8 ends the loop. Otherwise advance current
   using D and one x wrap; out-of-bounds ends the loop.
2. Reaching target sets blocked=false and ends the loop. Otherwise query city
   owner: an own city also sets blocked=false and ends the loop, even before
   reaching target.
3. A foreign city without directed Alliance ends the loop. Then query
   city-or-unit owner; a positive foreign owner without directed Alliance also
   ends it. Barbarian owner 0 is excluded only from this second test.
4. Missing Road ends the loop. Missing Railroad clears allRail. Increment the
   counter; a value **above 50** ends the loop.

Finally clear uniform and roads-only unconditionally, leaving owner absent and
other workspace as produced. Return 0 if blocked, otherwise 2 if allRail, else 1.
Only intermediate tiles that passed step 2 contribute to infrastructure checks;
a direct step to target or an own city can return 2 without any Road/Railroad
bits. Uniform search bypasses roads-only, so this query checks the selected
route afterward rather than finding the best road-only alternative.

## Debug interaction and callback boundaries

When captured pending-debug is nonzero, local search has already selected its
return direction before displaying anything. Store source x/y in primary map
view center with signed16 retention. Repeatedly redraw that view with `(-1,1)`,
then scan grid rows 0..47 and columns 0..47. Draw each nonzero cost at tile
`(column-24+row-24+centerX, column-row+centerY)` using the debug-number style.
Show MOVEDEBUG and process its answer:

- 1 decrements primary zoom with signed16 retention; 2 increments it likewise.
- 3 invokes center-on-source; 4 invokes center-on-current-goal.
- Any other answer exits. Answers 1..4 repeat redraw/grid/dialog.

The original does not clamp zoom in this interaction. P02/P04 own rendering and
modal behavior; a stricter zoom policy must be labeled separately, and any later
out-of-bounds **writes** remain excluded bugs. Debug callbacks can mutate shared
goal/result cost and other state after direction selection. A wrapper such as
pathLength reads the live result cost afterward; it must not substitute the
value from before the dialog. Unit goto clears both debug flags after returning.

## Nominal and off-nominal conformance cases

Unless stated otherwise, use a flat 64×48 valid land map of one region, r=3,
raw movement 6, terrain cost 1, no features/occupants/penalties, valid human owner
1, inert callbacks and an empty incoming cache. Source=(20,20), goal=(24,20).
These fixtures require logical outcomes, not matching private addresses.

| Case | Required result |
| --- | --- |
| Ordinary local search, limit 999 | Direction 1; result cost 25. Two steps cost 12 each plus seed 1. |
| Repeat with same goal and nonzero source grid cost | Direction 1; result cost 0. No flood or automatic rule/map-key invalidation. |
| Fresh local search, limit 25 | No direction, result cost 25: the strict cutoff rejects equality. |
| Uniform pathLength, limit 5 | 3 on a fresh query; 0 on a successful repeated cached query. The coarse builder accepts the first result only. |
| All tiles Road / all tiles Railroad | Local result cost 9 / 3. Actual railroad movement remains a U02 cost of zero where applicable. |
| Terrain cost 2 / raw movement only 3 | Local result cost 49 / 25 respectively. Raw movement equal to r bypasses terrain scaling. |
| Roads-only, no infrastructure | No route ordinarily; Alpine gives cost 9; uniform mode gives cost 3. |
| Coast-bound, all candidate tiles resource-suppressed, uniform mode | Flood result cost 27; direction picker still uses step cost 1. |
| All entered tiles own AI Unit-present, no City/Fortress, adjacent enemy, one same-role stack member | Flood result cost 57 (two steps of 12+16, seed 1); selection uses an 8-point stack penalty. |
| All entered tiles yield a peace-territory owner, role 0 | Flood result cost 49; role 5 suppresses that penalty. |
| Sea source on land; surrounding water region count 1 | Flood refuses that region, even though the selected rule is naval. |
| Cylindrical W=64, source (0,20), goal (62,20), uniform limit 5 | Flood reaches source at cost 2, but direction selection returns −1: its pre-normalization window rejects the candidate neighbors. |
| Snapping (20,20), only coarse (7,5) connected, uniform mode | Snaps to (7,5) using D's +2 x offset. With only (6,5) connected it fails and publishes containing (5,5). |
| Adjacent valid goto target | Sign-derived direction; no flood or entry-legality check. Invalid target or already at target returns −1 without exhausting/clearing orders. |
| Human Land fallback would reverse after spending movement | Clear orders, return −1; retain spent if ordinary human, or clear Automated and exhaust if Automated. |
| Road query's intermediate (22,20) has Road / Road+Railroad | Return 1 / 2; reaching endpoint precedes feature tests. An own city at the intermediate point succeeds immediately. |
| No intermediate road and no early own city | Return 0; no alternate road-only search is attempted. |

Custom nominal rules may change costs, properties, domains and fixed slot
contents. Negative movement costs or costs wrapping to zero/negative must retain
the stated arithmetic and zero-unvisited semantics; the finite local queue
bounds still apply. The greedy score thresholds, local 19999 threshold, snap/
waypoint 99 thresholds and 255→0 coarse-distance wrap are observable limits.
They are not array-overrun bugs and must not be removed as an incidental cleanup.

Malformed bounds, grids, region IDs, active unit records or callback-invalidated
state need explicit read evidence or a bounded stop after valid prior effects.
Do not reproduce any write outside cost/queue/coarse storage. Unsupported
writes stop before damaging another object unless an owning operation specifies
a safe continuation. In-bounds cyclic queue overwrites above remain distinct.
Rejecting otherwise supported hidden/off-map actors is a declared input-policy
change; the original's missing actor check alone does not establish a successful
path result. No universal error/no-op value is invented for unsupported reads.

## Appendix A. Original encodings and validation mapping

These details interpret reference snapshots; they do not require the same
representation. Logical array limits and state reductions above remain binding.

| Item | Reference mapping |
| --- | --- |
| Path selectors | Unit type `0062d03c`, uniform-cost `0062d040`, owner `0062d044`, roads-only `0062d048`, pending-debug `0062d04c`, requested-debug `0062d050`; 32-bit values. |
| Goal/workspace | Goal x/y `00673fa0/00673fa4`; coarse start x/y `00673fa8/00673fac`; grid center `00673fb0/00673fb4`; cached goal y `00673fb8`, x `006787cc`; result cost `00673fc0`. |
| Grid | 2304 signed32 costs beginning `006ced60`, row-major 48×48. Offset for supplied point is `wrap32(4704 + dx*192 - ((signed32(dx+dy))>>1)*188)` bytes. Setter adds dy+dx before the same arithmetic shift. |
| Queue | Head `00673fc4`, tail `00673fbc`; x entries at `00673fc8`, y at `006763c8`, signed32 coordinates. Local search uses indices 0..2303; coarse BFS uses 0..255 in the same arrays. |
| Coarse graphs | Planes 0/1, row-major `cy*CW+cx`, byte direction mask `1<<d`; plane 2 holds unsigned byte distances. |
| Unit rules | Warriors slot 2, Caravel 33; domain Sea 2, Air 1, Land 0. Rule movement, terrain movement and role are signed bytes; multiplier is unsigned byte. |
| Feature tests | Road `10`, Railroad `20`, City component `02`, Fortress `40`, Unit-present `01`; AI stack predicate `(features & 43)==01` (hexadecimal). River is terrain flag `80`; resource suppression `40`. |
| Relevant type flags | Coast-bound `0020`, Ignore-ZOC `0002`, Alpine `0200`. F03 owns decoding and all other rule flags. |
| Goto abandonment | Full order None `ff`; Automated flag `8000`. Signed last-direction byte participates in reversal. U04 owns the broader order encoding. |
| Owner scratch | Original local at EBP−`7c` in `004abfe5`, read after skipped assignments. Supply incoming state where observable; zero in source is not an original initialization. |
| Debug | Primary center and zoom use signed16 fields; number style `29` hexadecimal. The exact map rendering contract belongs to P02. |

The original raw index helpers have no complete object-bound checks. A known
address outside an intended array is not an extended valid write domain; any
resulting overwrite is an excluded bug under X01. Conversely, wrapping the
coarse ring within its defined 256 entries stays inside the array and remains
specified even when it loses pending work.

## Appendix B. Evidence, verification and remaining joins

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary source is OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`, inspected in the unchanged reference
checkout at `fc2155ccdb2123c1137babab6fa39e38152278c9`. The relevant implementation
files have no differences from the evidence pin. Independent source was read
at `civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, principally `src/pathfinding.cpp`
and its BF-003, BF-193 and BF-283 notes. Neither source tree was modified.

| Family | Source and original entry |
| --- | --- |
| Local search/flood/pick/debug | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp), `map_path_next_dir`, `004abfe5`; named static children are source extractions of that body. |
| Grid and distance wrapper | [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp), grid get/set `004ad01e/004ad076`; map.cpp `map_path_length`, `004ad0d1`. |
| Coarse snap/waypoint | unit.cpp `path_coarse_snap_cell`, `004ad822`; `path_coarse_find_waypoint`, `004ad20f`. |
| Representatives/build | map.cpp `map_diag_tile_by_ocean`, `004ad784`; `map_coarse_cell_landmass`, `0055ac37`; `map_build_coarse_connectivity`, `0055a980`. |
| Goto/fallback | unit.cpp `unit_goto_next_dir`, `004adafc`, including extracted greedy/cost helpers; sign direction helper in map.cpp, `004abea0`. |
| Road connection | map.cpp `map_road_connection`, `00488a45`. Economic callers do not redefine its internal search. |
| Limits/known differences | [BUGS.md](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md), owner-scratch presets; [case inventory](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/tests/unit/cases.tsv) includes local flood, pick, distance, coarse snap/waypoint and goto cases. An inventory is not a new executed conformance run. |

BF-003 initializes owner scratch; this specification instead exposes the original
incoming-value dependency. BF-193 rejects off-map actors after the original
invalid-target tests; that additional validation is not silently inserted here.
BF-283 clamps debug zoom; the unclamped original mutation is described above,
subject to later X01 boundaries. Those differences are separate from the user's
explicit exclusion of out-of-bounds writes. The optional OpenCiv2 path/road
visualization does not define classic route behavior.

A temporary native harness extracted **26 routines** from the primary pin and
**22 routines** from W02's earlier pin
`1ff87e33fbec8b7be8c4bf98c77709e16aad4f31`; the latter retains monolithic search
and goto bodies. Each passed **4,559 assertions** in optimized and unoptimized
AddressSanitizer/UndefinedBehaviorSanitizer builds, with signed wrap enabled,
signed char semantics and strict-alias optimization disabled. Leak detection
was disabled for the ptrace environment. Both revisions produced identical
recorded state digests, `2264dbc1b4349c82`, for these finite cases.

Checks covered canonical rotated-grid positions, ordinary/road/rail/Alpine costs,
cutoff equality, cache reuse, uniform/roads-only interaction, AI-stack and peace
penalties, Coast-bound suppression, sea-from-land water-count rejection, the
horizontal seam failure, snap's unusual D offsets, goto adjacency/refusal/Air
fallback/reversal, complete coarse rebuild followed by snap/waypoint, 64 seeded
terrain probes and road-connection early-success/feature cases. The coarse
builder used the real path helpers and retained cache; this supports extracting
W02's duplicate contract without changing its exercised path behavior.

The harness supplied bounded logical tile/owner/peace/ZOC/stack queries, unit
allowance/exhaustion and UI/network adapters. It executed the extracted distance
primitives, local grid, queues, path routines and coarse builder. Original
32-bit coordinate-pointer stores were adapted to 32-bit values on the native
host; owner scratch was an explicit input set to zero for these cases. Debug
presentation was a recorded adapter, not the real dialog loop. These are source
execution checks, not an independent cleanroom implementation or a fresh run of
the original executable. No original disassembly or saved annotations were
changed during this slice.

Not established by these runs: all owner-scratch inputs, real diplomatic/ZOC/
stack children, queue saturation and coarse-distance wrap continuations,
malformed backing, reentrant debug/network callbacks, every customized rule
combination, complete map generation, or complete turns. Agreement across pins
on these cases is not W02's outstanding whole-document classic-target review.
Artifacts remain temporary research material outside this repository; X02 must
retain independently reproducible fixtures and their exact domains.

Remaining work is explicit:

- U02/U04/U05 and A03–A07 must join actual movement, orders, cargo and AI choices
  with the cache and rule-selector mutations, including failed moves/replanning.
- W02/F05 must join full generation/setup and callbacks; W04/S03/S04 must specify
  when their map edits explicitly rebuild or retain coarse/search state.
- [C07](C07-trade-and-commodities.md) consumes the connection result in distinct
  delivery/replacement formulas. Its finite cases still supply connection answers;
  join this actual search with those economic callers. The helper is not proof
  of a road-only route.
- N02/N03 and P02/P04/P05 must join message pumps, debug views and input changes
  without moving live reads across callbacks.
- X02 must retain independent fixtures for finite work limits, supplied incoming
  scratch, in-bounds queue overwrite/wrap, the excluded-write boundaries and
  complete caller traces. No full-game compatibility claim is made here.
