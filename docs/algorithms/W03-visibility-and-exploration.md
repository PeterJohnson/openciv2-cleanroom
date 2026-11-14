# W03: Visibility and exploration

**Required profile:** [X01 fresh-game isolation and bug repairs](X01-classic-compatibility-contract.md#fresh-game-isolation-and-supported-bug-repairs)
and [behaviorally significant ordering](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
override conflicting procedures and examples below. Retention applies within a
game or to explicit imported inputs, not across new games. Confirmed original-bug
stack/backing/stop cases are optional diagnostics; use documented safe repairs.
Only real effects and callbacks impose ordering; test adapters cannot invent them.
Unspecified safe continuations remain explicit gaps, not required arbitrary bytes.

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**, under the
[X01 compatibility contract](X01-classic-compatibility-contract.md).

This document specifies the separate knowledge channels, tile and unit revelation,
nearby contact/order/AI-goal effects, two different map-sharing operations and
Apollo revelation. It defines when the revelation operation requests redraws and
synchronization. It also identifies the refresh boundaries used by setup,
movement, city changes and investigation. It does not turn these boundaries into
a continuous line-of-sight recalculation.

[W01](W01-map-geometry-and-spatial-state.md) supplies tile access, one-step
horizontal wrapping, bounds, the ordered `B`/direction tables, feature components
and primitive tile synchronization. [U01](U01-unit-lifecycle-and-stacks.md)
supplies tile/stack lookup, validation during traversal and elementary unit
seen-by operations. [F02](F02-game-state-and-invariants.md) defines identities
and retained state; [F03](F03-rules-and-configuration.md) defines the rule
properties used here. [F01](F01-determinism-and-arithmetic.md) supplies arithmetic
and the random-decision contract. No operation here draws a random value directly;
invoked diplomacy, AI and presentation operations keep their own effects/draws.

[V04](V04-diplomacy-and-treaties.md#10-contact-dispatch) owns contact admission
and treaties; V05 owns negotiation selection; A01 owns
inserting/updating requested AI goals; U04 owns the two shared order-changing
helpers specified below; V06/P06 own mission acceptance and the city window.
N02 owns message/state synchronization and batching, and P02 owns rendering.
These are explicit child contracts, not assumptions that those children are
effect-free.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

Revelation/contact hooks, overlapping map exchanges, retained knowledge and
in-place reads preserve their dependencies. A set of final explored tiles alone
does not establish equivalent contact, visibility or notification behavior.

## 1. State and nominal inputs

The nominal world has valid W01 dimensions, normal doubled coordinates and its
nominal ordered neighborhood tables. Civilization IDs are 0 through 7, with
0 the barbarians. Remembered-feature planes exist for civilizations 1 through
7. Units/cities have backed rule/owner entries, finite valid stacks and valid
live records at the slots supplied by ordinary callers. Network recipient
lookups have backed civilization-to-player mappings. Local viewer may differ
from the acting civilization; the human-controlled set is a separate input.

| Channel | Meaning and lifetime |
| --- | --- |
| Tile `exploredBy` | Retained set of civilizations that have been given that tile. Ordinary departure does not clear it. The reference calls this field visibility; it is not a current line-of-sight set. |
| `remembered[c,tile]` | Remembered **feature components**, separately for each normal civilization. Terrain, river and resource state are not copied into this plane. Memories can remain stale after features change. |
| City `knownBy` and `rememberedSize[c]` | Separate knowledge of the city and its last published size. A known city can have a stale or zero remembered size. Ownership is separately tested by some consumers. |
| Unit `seenBy` | Retained unit-specific set. It can be cleared by a movement caller, augmented by revelation or map sharing, and retained by plain relocation. It is independent of the tile and of city knowledge. |
| City `investigated` | Shared temporary mission flag; not a per-observer city map memory or a unit detector. |

There is no remembered copy of each tile's base terrain in these operations.
Changing live terrain therefore does not wait for a terrain-memory refresh.
Whether a renderer displays a tile or overlay is a consumer decision; no query
here silently fills another channel. An explored tile need not reveal its units,
and learning a city does not imply knowing its whole garrison.

Inputs also include unit type properties `domain` (Land/Air/Sea), `seesTwo`,
`submarine`, `spotsSubmarines`, attack and AI role; current orders; directed
Contact/Peace/Alliance relations; global turn, scenario/full-map flags; live
human set, local viewer, network mode/authority and autoplay. No alive-civ test
is added to knowledge publication merely because the recipient is absent from
the alive set.

Unless explicitly cached below, reads occur when the step is reached. A contact
callback can change treaties, units, rules or mode before the next step. Do not
freeze the whole game at entry, batch all contact until after revelation, or
deduplicate repeated coordinates on a small wrapping world.

## 2. Knowledge primitives

### Remember and reveal a city

`rememberTile(x,y,c)` does nothing when c=0. Otherwise it copies the current
tile's complete feature-component value into `remembered[c,x,y]`, replacing
the prior value and requesting synchronization. It neither checks exploration
nor adds c to `exploredBy`. The live tile uses W01's accessor; the remembered
plane has no dummy-tile fallback or bounds check.

`writeRemembered(x,y,value,c,merge,sync)` replaces the memory when merge is
zero; any nonzero merge unions the supplied individual feature components with
the retained components. It does not interpret a compound city/airbase or
farmland state before combining components. Synchronize only on a stored-value
change, using W01's client-batch / client-request / host-broadcast rule. The
transmitted value is reduced to the retained component positions. There is no
special c=0 skip in this lower-level writer.

`revealCity(city,c)` does nothing if c<0. Otherwise add c to `city.knownBy`,
then assign `city.rememberedSize[c] = city.size`. It does not check the city's
lifetime/owner or change tile memory, tile exploration, unit visibility or
investigation status. It emits no synchronization itself. Publishing size zero
is allowed; it does not clear the known flag.

U01's `markStackSeen(unit,c)` walks from the stack head and adds c to each
member whose owner differs from c; a negative c adds nothing. In contrast,
direct union into seen-by sets can retain/add the owner's own membership.
This distinction is used below.

### Reveal one tile

For `revealTile(x,y,c)`, return immediately when c<0. Otherwise:

1. Begin a tile-update batch.
2. Add c to the tile's explored set, with synchronization requested.
3. `rememberTile(x,y,c)`.
4. Look up a city at the coordinate using U01's tile-gated city lookup.
   If present, `revealCity(city,c)`. Otherwise find the first unit at that
   coordinate and `markStackSeen(first,c)`.
5. End/flush the tile-update batch.

The city branch suppresses the stack branch, even for a friendly city or a city
whose knowledge was already current. A submarine outside a city is not hidden
from this direct reveal. There is no contact, sentry wake-up, goto interruption,
neighborhood scan or map redraw here. The coordinate is neither wrapped nor
checked by this entry point before its children access it.

### Reveal occupied adjacent tiles of the same medium

`revealAdjacentUnits(x,y,c)` caches whether the origin is Ocean and begins a
batch. Scan directions 0 through 7. Wrap each neighbor's x once, then require
it to be in bounds, to have the same Ocean/non-Ocean classification as the
cached origin, and to report a nonnegative unit owner through the tile's
unit-present/occupant fields. For every qualifying tile:

1. Call `revealTile(neighbor,c)`; its own batch begin/end still occurs.
2. If the **current** local viewer equals c, request an area redraw centered
   at that neighbor with radius 0, viewer c and update flag 1, then emit a
   broadcast DrawArea request. This call is made even outside network mode.

End/flush the outer batch. No lifetime scan replaces the tile unit-owner gate.
A city with unit presence takes `revealTile`'s city branch. Negative c suppresses
that child's mutations but does not suppress this helper's outer batch or its
local-viewer redraw comparison.

## 3. Unit surroundings: entry and tile scan

`revealSurroundings(u,allowContact)` is called after U01 creation and by movement
callers. It has no host/client authority gate. Its second argument permits
contact callbacks; it is **not a redraw flag**. It does not check the unit lifetime, clear previous visibility, move the
unit or spend movement itself. Run the following three scans in order, followed
by the redraw tail.

Cache at entry:

```text
origin = unit[u].position
c = unit[u].owner
extended = type(unit[u]).seesTwo
hidden = type(unit[u]).submarine
domain = type(unit[u]).domain
originOcean = isOcean(origin)
local = (localViewer == c)
fullMap = globalRevealMap
suppressMapDifference = fullMap OR (scenarioGame AND scenarioMapAlreadyRevealed)
nearChange = farChange = nearOutline = farOutline = false
originRedrawn = false
```

The four redraw booleans are independent accumulators. Marking `nearOutline`
also marks `nearChange`; marking `farOutline` also marks `farChange`. They track
the reference's four redraw components, not four persistent tile fields.

If c=0, directly union the origin tile's explored set into **u's** seen-by set,
without changing the rest of its stack. Then begin a tile-update batch.

For i=0 through 24 in W01's B order, wrap `origin.x+B[i].x` once and form
`origin.y+B[i].y`. Require in bounds. Visit the tile if:

- i<8, or i=20 (the center); or
- `extended` and either cached domain is Air or the tile is Ocean exactly when
  cached domain is Sea.

For each visited tile, do this in order:

1. If c is nonzero, compare live features and exploration with its prior memory.
   If `local && !suppressMapDifference` and either the features differ or c
   has not explored the tile, mark nearOutline for i<8, otherwise farOutline.
   Replace the remembered features with the live features, requesting sync.
2. Add c to the tile's explored set, requesting sync.

The center is processed at index 20 and belongs to the **far** redraw class,
even without extended sight. Adjacent terrain is always visited; outer terrain
is filtered by the **target tile's** medium. Intervening hills, mountains,
forests, cities and water never block a ray: there is no ray test. Extended
Land sight reveals outer non-Ocean tiles even across intervening Ocean; extended
Sea sight reveals outer Ocean tiles even across intervening land. Air's outer
terrain has no medium filter. This phase does not reveal any city or unit.

## 4. Unit surroundings: adjacent interactions

Scan the eight directions 0 through 7, wrap x once and skip out-of-bounds
neighbors. Cache `city = cityAt(neighbor)` for that iteration. Follow all
substeps in this order; later substeps are not an `else` for the city substep.

### City substep

If city is present and its current owner d differs from cached c:

1. If `local && !suppressMapDifference`, mark nearOutline if c does not know
   the city; otherwise mark nearChange if its remembered size differs.
2. `revealCity(city,c)`.
3. If not cached hidden, `markStackSeen(u,d)`, even if u is itself in a city.
4. If cached domain is Land and the directed relation **d→c** lacks Peace,
   request AI goal `(d, neighbor, Attack, priority 4)`, then
   `(d, neighbor, Defend, priority 2)`.

These goals are requested for human owners as well as AI owners. Their insertion,
merging and capacity behavior belongs to A01.

Cache `neighborOcean = isOcean(neighbor)`. If cached hidden and neighbor is
non-Ocean, skip the rest of this neighbor. The city substep has already run.
Otherwise find and cache the first unit v there using U01's lookup.

### No-unit substep

When v is absent, reread whether the **origin** is Ocean. If it is not Ocean,
look up the neighbor's tile city owner d. Require d>=0, d≠c and no Contact on
the directed relation c→d. Then:

1. If originRedrawn is false, redraw the origin tile immediately. If the live
   mode is network, synchronize game state, then broadcast DrawSquare(origin).
   Set originRedrawn true after these calls.
2. If allowContact is nonzero, invoke `meet(c,d,neighbor)`.

This branch does not require cached domain Land, has no Partisans exclusion and
does not suppress its redraw for fullMap. It does not itself write Contact.
Continue to the next direction. A tile reporting an empty city may enter this
branch even if the city's record lookup failed.

### Foreign-unit substep

When v is present, cache d=v.owner and t=v.type. If d=c, do nothing more for
this neighbor. Otherwise execute all three groups below in order.

**A. The neighbor detects the acting unit.** Enter if not cached hidden, or
type t has SpotsSubmarines:

1. If the origin's live tile city owner is absent, `markStackSeen(u,d)`.
2. If directed c→d lacks Alliance, `wakeSentries(v)`.
3. If d is outside the current human set, interrupt v's goto when
   `neighborOcean || !originOcean`. If d is human, interrupt it only when
   `neighborOcean == originOcean`.

The human test uses the cached neighbor owner but the live human set. The
detector property is tested on the first unit's cached type only.

**B. The acting unit detects the neighbor.** Enter if type t is not a submarine,
or the stack count at v exceeds one, or u's **current** type spots submarines:

1. If the iteration's cached city is absent, mark nearChange when local,
   `!fullMap` and v was not already seen by c; then `markStackSeen(v,c)`.
   A present city suppresses these steps, not the following order/goal effects.
2. For an acting owner outside the **live** human set, interrupt u's goto when
   `originOcean || !neighborOcean || cached domain is Air`. For a human acting
   owner, interrupt it when the media match or cached domain is Air.
3. If neighborOcean, d≠0 and directed c→d lacks Peace, request
   `(c, neighbor, NavalSuperiority, priority 3 if t is submarine else 2)`.

Stack count uses U01's ordered aggregate, including its validation and cutoff
behavior. A submarine at the head of a multi-unit stack is detectable by this
adjacent rule even without a detector. Do not test every member individually.

**C. Land contact and goals.** Independently of detection group B, enter if
u's **current** type is Land, cached originOcean is false, neighborOcean is
false, and type t is not Air or the cached city is present:

1. If `!originRedrawn && !fullMap`, redraw the origin tile; in live network mode
   synchronize state and broadcast DrawSquare(origin); then set originRedrawn.
2. If allowContact is nonzero and u's **current type slot** is not Partisans,
   invoke `meet(c,d,neighbor)`. There is no Contact-bit test here.
3. Reread u's current type/role after the callback. If its signed role is less
   than 6, request `(d, neighbor, Defend, priority 1 if c=0 else 2)`.
4. If the live directed c→d relation lacks Peace, choose priority 1, raising
   it to 3 only if d≠0, type t's role is not Diplomacy, and v is not both
   Fortified and of Defend role. Request `(c, neighbor, Attack, priority)`.

The cached coordinates, owner, domain, origin medium, hidden/extended flags and
neighbor type survive contact callbacks; the expressly live fields do not.
If a callback invalidates a subsequently accessed record, use the backed-state
boundary in section 10, rather than silently abandoning or restarting the scan.

### Order helpers used here

Use [U04's shared order mutations](U04-unit-orders-and-activation.md#shared-order-mutations)
for `interruptGoto` and `wakeSentries`. Their calls remain at the exact revelation
steps above, using each member's live order/type/position. Neither helper adds a
visibility or diplomacy test, and their order changes do not clear cargo links.

## 5. Unit surroundings: outer interactions

Scan B indices 8 through 24, skipping index 20. Wrap x and require bounds.
In this order cache `v=firstUnit(neighbor)`, `d=tileCityOrUnitOwner(neighbor)`
and `city=cityAt(neighbor)`. The owner query prefers a city owner and uses tile
metadata; it need not equal v.owner in inconsistent state. Skip the iteration
when d=c; no general d>=0 guard is added. Execute three groups in order.

**A. Acting unit has extended sight onto this medium.** Enter when cached
extended and (cached domain is Air, or neighbor's current Ocean classification
equals whether cached domain is Sea):

1. If city is present, update its knowledge/remembered size for c. Before that
   update, local and unsuppressed unknown city marks farOutline; changed size
   of an already-known city marks farChange.
2. If v is present and its current type is **not** a submarine:
   - If the cached city is absent, mark farChange when local, `!fullMap` and
     **u's** seen-by set lacks c; then `markStackSeen(v,c)`.
   - If the neighbor is currently Ocean, d≠0 and directed c→d lacks Peace,
     request `(c, neighbor, NavalSuperiority, priority 2)`.

The far redraw test intentionally reads the acting unit's seen-by set, not v's.
Submarines at this distance are excluded even when u has detection or v has
other members in its stack. The cached city suppresses stack publication but
not the naval-goal test.

**B. An extended observer sees the acting unit.** Enter if v is present, cached
hidden is false, and v's current type has SeesTwo and either is Air or matches
the **origin's live** Ocean/non-Ocean classification (Sea iff Ocean):

1. `interruptGoto(v)`, then `wakeSentries(v)`.
2. If the origin's live tile city owner is absent, `markStackSeen(u,d)`.
3. If the observer's tile is currently **non-Ocean**, d≠0 and directed d→c
   lacks Peace, request `(d, origin, NavalSuperiority, priority 2)`.

The non-Ocean test in the last step is intentional despite the naval goal's
name. This group has no human/media matching goto gate and no Alliance gate
for waking sentries. It runs even when u lacks extended sight itself. A detector
does not override u's cached submarine exclusion in this far-observer rule.

**C. City awareness of an armed unit.** If city is present, the origin is
already explored by d, u's **current** type has nonzero attack, and cached
hidden is false, `markStackSeen(u,d)`. This rule needs neither u's extended
sight nor a unit in the observing city. Exploration is retained knowledge,
not a newly computed city vision radius. No contact callback occurs anywhere
in this outer scan.

## 6. Unit surroundings: redraw and synchronization tail

If the **live** turn is zero, end/flush the batch and return. All scans and their
callbacks already occurred; only this final redraw/sync tail is skipped.

Otherwise synchronize game state, then use the live human set and local viewer:

- If cached c is not human-controlled and the current local viewer differs from
  c, scan civilizations 1 through 7 ascending. For each currently human c2,
  require live full-map reveal, or membership in u.seenBy, or u's live owner=c2.
  If c2 is local, redraw the origin's neighbors. Otherwise, in network mode,
  send DrawArea(origin) to c2's current connection. After the scan, if
  originRedrawn is false, redraw the origin tile and, in network mode,
  broadcast DrawSquare(origin). Set membership and owner are read per recipient.
- Otherwise choose a redraw radius as follows. Start at 2. If cached extended
  is false or farChange is false, use radius 2 when nearOutline, else radius 1.
  Otherwise use radius 3 when farOutline, else radius 2. If all four redraw
  booleans are false, use radius 0; if originRedrawn is already true in this
  case, end/flush and return. If cached c is the **current** local viewer,
  redraw the area at the cached origin with that radius, viewer c and update
  flag 1. Otherwise, in network mode, send DrawMapRegion(origin,radius,c) to
  c's current connection.

Finally end/flush the batch. Radius is a parameter to the area renderer, not a
second revelation request. ScenarioMapAlreadyRevealed suppresses tile/city
difference flags; it does not suppress the unit-seen difference tests, which use
the entry fullMap value only. The AI-viewer branch uses the **live** global
reveal flag, unlike those cached tests. The early origin redraws are not deferred
until after contact; their ordering can expose the unit before the meeting UI.

## 7. Sharing maps

There are two distinct algorithms. Do not implement both as a symmetric union
of full tile snapshots. Their selection/acceptance belongs to [V05](V05-diplomatic-negotiation.md);
N02 owns message admission and receipt.
Neither exchange reruns unit detection, contact, sentry wake-up or goto rules.

Both tile scans start at (x,y)=(0,0) and process the live `map.area` count in
row order. After each tile, x+=2; if x>=width, increment y and set x=y mod 2.
The scan does not independently bound-check each coordinate or recompute area
from dimensions. The nominal dimensions make these the normal tile records.
Live slot bounds are reevaluated by the later unit/city scans.

### Accepted AI-negotiation exchange

Name A the granting party (second argument, `civ` in the reference) and B the
requester (first argument, `civ2`). First play leader reaction 2,
show MAPYES, and add 2 to A's diplomatic request counter modulo 256 (values
128..255 represent −128..−1 to later signed consumers). Compute their membership
selectors and begin a tile batch.

For each tile:

```text
if A has not explored it:
    if B has explored it:
        add A to exploredBy
        if A != 0 and B != 0:
            remembered[A] |= liveFeatures intersect remembered[B]
else:
    add B to exploredBy
    if A != 0 and B != 0:
        remembered[B] |= liveFeatures intersect remembered[A]
```

All tile/memory mutations request synchronization. Intersection and union act
on individual feature components. Prior recipient memory is retained, including
stale components no longer live. If both already explored the tile, **only B's
memory is augmented**. If only B knew it, A learns it without returning its
old memory to B in the same iteration. Neither-known tiles are unchanged.

End/flush the tile batch **before** these ascending scans:

1. Each live unit: if it is neither seen by A nor owned by A, add A to its
   seen-by set only if seen by B or owned by B. Otherwise add B. These are
   direct unions, without U01's owner exclusion or stack traversal.
2. Each live city: if it is neither known to A nor owned by A, add A to its
   known set only if known to B or owned by B. In this branch **do not update
   A's remembered size**. Otherwise `revealCity(city,B)`, publishing the
   **current** size even if A only had a stale remembered size.

Synchronize state, then redraw all views for the current local civilization
with update flag 1. Unit/city sharing is not filtered by tile exploration,
physical distance, stealth or whether the object is inside a city.

### Directional multiplayer map offer

`giveMaps(recipient R, donor D, kind)` takes kinds Full=0, Cities=1 or
Terrain=2. These are data selections, not separate wire authority gates: the
operation itself has no host/client early exit. Begin a tile batch and scan
row order. At each tile, resolve both remembered-plane addresses even if D
has not explored that tile. If D has explored it:

1. If R has not, replace R's remembered features with empty, **then** mark it
   explored by R. These writes happen for all kinds.
2. For Full only, reconcile the Pollution component. If both memories have
   Pollution, retain it even if live Pollution is absent. Otherwise retain it
   only if at least one memory has Pollution and the live tile has Pollution.
   Replace only this component in R's current memory, preserving other
   remembered components. Then union `liveFeatures intersect remembered[D]`
   into R's memory. All writes request synchronization.

Terrain/Cities offers therefore preserve features on already-explored recipient
tiles but leave newly explored recipient tiles with **empty** feature memory.
They do not copy a donor's remembered road or city marker there.

Retain the post-loop (x,y) cursor. For nominal area it is
`(height mod 2, height)`, immediately below the map. Define this helper:

```text
publishOfferNeighborhood():
    for i = 0..20 in B order:
        p = (wrapX(postLoopX+B[i].x), postLoopY+B[i].y)
        if inBounds(p):
            add R to p.exploredBy, requesting sync
            add D to p.exploredBy, requesting sync
```

For Full, scan live units ascending. If a unit is seen by D or owned by D,
directly add R to its seen-by set. Read exploration of its actual position;
if either R or D lacks that tile, call `publishOfferNeighborhood()`.

For Full or Cities, scan live cities ascending. If known to D or owned by D:

1. Assign its R remembered size: current city size if owned by D, otherwise
   D's remembered size. Then add R to the city's known set.
2. Read exploration of its actual position; if either R or D lacks it, call
   `publishOfferNeighborhood()`.

The helper is deliberately centered on the **post-tile-scan cursor**, not the
unit or city. It can reveal a strip at the lower map edge to both participants,
leave the triggering object's tile unexplored, and run repeatedly for multiple
objects. Do not repair this coordinate reuse in a classic-compatible path.
It refreshes no feature memory, even on those newly explored edge tiles.

End/flush the batch, synchronize state, then redraw all map views if R is the
current local viewer; otherwise send DrawMap to R's current connection. The
remote send here has no additional network-mode test. Any kind other than
0 or 1 takes the terrain-only behavior, including negative values. If R=D,
preserve the ordered in-place reads/writes; do not infer a no-op.

## 8. Apollo and city investigation

### Apollo revelation

The shared Apollo operation has an authority gate. In network mode on a client,
send DoApollo to host 0 and return, with no local mutation, wait or dialog.
Otherwise:

1. Begin a tile batch. Scan all tiles in the row order from section 7.
   Add every civilization 0..7 to each tile's explored set; then, for c=1..7
   ascending, replace remembered features with that tile's live features.
   All writes request synchronization; no alive/human/owner filter applies.
2. Scan live cities ascending. Set knownBy to all eight civilizations. For
   c=0..7 ascending, assign rememberedSize[c] from the city's current size.
   Add all eight to exploration of the city's position too, even though the
   ordinary map was already scanned.
3. End/flush the batch. If the live mode is network, enable tile synchronization,
   synchronize state and broadcast DrawMap. If autoplay is currently inactive,
   also broadcast the Astronauts text request.
4. Redraw all maps for the current local viewer. If autoplay is currently
   inactive, show ASTRONAUTS locally.

This does not reveal unit seen-by sets or enable the full-map cheat flag.
[C06](C06-city-production-and-purchasing.md) owns the completion trigger. C01
owns subsequent city creation: when
the Apollo wonder location is nonnegative, it initializes the new city's
known set to all eight and all remembered sizes to **1**, explores the center
for everyone and remembers its features for c=1..7. That initialization happens
before later size changes; it is not a promise that all later sizes remain known.
The destroyed/never-built wonder distinction remains the caller's test.

### Investigation versus map knowledge

`revealCity` only publishes map knowledge; it does not open an inspection window
or grant an espionage mission. Conversely, the accepted investigation branch
opens the city window and changes the mission flag/cost without calling
`revealCity`, `revealTile` or `revealSurroundings` itself.

V06 owns the full accepted-mission order: play the spy sound if actor or victim
is local; destroy an AI actor's unit; otherwise open the city window, dispatch
diplomat escape/consumption or charge a spy one spent-movement unit only if the
city was not already investigated, then set Investigated. Its victim
notification follows. P06 owns what the opened window reveals and whether its
own children mutate state. The flag is on the city, not one flag per observer.
[F04's city-turn phase](F04-turn-sequencing.md) clears it before processing
that city; the network turn loop also clears it across live cities after the
completed-player phase (N03 owns that gate). Do not synthesize
a remembered-map refresh or make the window's full contents a permanent map
snapshot because the mission was accepted.

## 9. Caller refresh boundaries

Callers specify the exact point at which these operations run. A feature or
owner setter does not imply a visibility refresh. The following reviewed
boundaries distinguish operations that might otherwise be conflated:

| Caller | Knowledge action and remaining owner |
| --- | --- |
| U01 creation | After ordered initialization and stack attachment, invoke `revealSurroundings(u,1)`, before creation notices and final network publication. Its entry need not be a movement. |
| F05 starting units | Creation performs the preceding reveal. The subsequent B[0..20] starting-radius loop only marks tiles explored; it does not call `revealTile` or contact. |
| F05 accelerated city development | Each newly improved tile is remembered by the city's current owner. The final B[0..44] same-land-region loop only marks qualifying tiles explored for its captured owner. |
| Ordinary movement relocation | The local/authoritative relocation child clears stack seen-by state before moving and, on a non-Ocean destination, marks it for the captured destination owner. Final movement calls `revealSurroundings(u,1)` after relocation/capture/reordering. Pre-move publication, viewer selection, relocation and common-finish order are specified in [U02](U02-movement-and-zones-of-control.md#movement-publication-and-viewers); U05 will extract its transport and air/sea continuations. |
| Plain U01 relocation | No exploration or remembered-map update; seen-by state is retained. |
| Multiplayer unit transfer | The transfer path calls surroundings with allowContact=0. Revelation and order/goal effects still run. [V05](V05-diplomatic-negotiation.md#8-executing-multiplayer-offers) owns transfer ordering; N02 owns dispatch/receipt. |
| City area refresh | The shared area refresh changes fertility/radius claims only; it does not refresh knowledge. C01 owns its calls. |
| City acquisition | Incited transfer explicitly explores and remembers the B[0..20] neighborhood for the new owner. Other capture/gift/civil-war paths have different explored/size/seen updates; C09/V01/V05 own those complete phases. No universal owner setter fills all memories. |
| Terrain-work completion | [W04's completion tail](W04-terrain-work-and-improvements.md#completed-work) remembers for the actor's live owner only, even for pollution cleanup; it does not generally reveal or update all owners. |
| Pillage | [W04's tail](W04-terrain-work-and-improvements.md#knowledge-diplomacy-and-movement-tail) remembers for the actor and the second nearest-city query's owner only when that city exists. No city means no explicit remember call. |
| Pollution placement | After adding new Pollution, remember the tile for c=1..7 irrespective of exploration, then increment the pollution count. [W05 owns the shared placement primitive](W05-pollution-and-global-warming.md#shared-pollution-placement). |
| Warming, nuclear damage, scenario terrain edits | These have explicit memory-refresh loops, sometimes for every normal civilization and sometimes only explored tiles. [W05 specifies warming's explored-and-remembered-feature gate](W05-pollution-and-global-warming.md#coastal-branch); S02 must retain its own conditions/order; [U07 specifies its ordered blast](U07-nuclear-attacks.md#noncity-land-branch). A terrain change alone is not one of those loops. |
| Civilization elimination | Its removal path clears that civ's tile exploration, separately from deleting its objects. V01 owns the phase and retained memories. |

The movement and city rows identify joins for their owning documents, not a
substitute for their complete algorithms. In particular, alliance does not
cause continuous global sharing here: it suppresses one adjacent sentry-wake
step, but the outer observer step still wakes allies. Diplomacy must explicitly
invoke the appropriate exchange or other publication when its contract says so.

## 10. Off-nominal inputs and failure boundaries

Nominal RULES inputs can change unit domains, roles and detection properties
without changing these tests. Fixed type-slot tests such as Partisans remain
slot tests, not searches by name. Unrecognized domains take the literal
comparisons: they fail the Air test and compare Ocean against false because
they are not Sea. Signed role/attack/order reads remain as indicated above.

Original set operations retain eight civilization positions; the selector
uses F01's x86 shift count modulo 32. Negative c suppresses `revealCity` and
`revealTile`, while W01's exploration query for negative c returns true.
These different sentinel contracts must not be collapsed into one helper.
For example c=8 adds no retained exploration bit; c=32 aliases bit 0. Neither
fact supplies a backed remembered plane or city-size entry for those selectors.

Arbitrary nonzero c in `rememberTile`, nonnegative c in `revealCity`, or the
plane lookups in `giveMaps` can access backing outside nominal arrays. A
coordinate miss first reaches the live tile's shared dummy, but remembered
addressing still uses the supplied coordinate and plane pointer. Preserve prior
effects, including a batch begin or dummy exploration write, before an
invalid access. Reads need actual supplied backing or a stop at that boundary;
do not invent a blank memory plane. Writes outside the remembered plane or
city-size array are excluded bugs under X01: prevent them and reject/stop at
the unsupported destination. Adjacent backing may diagnose the original bug,
but its corruption is not a required result. The intentional dummy-tile write
remains in scope because that accessor supplies a valid destination object.

Unit surroundings can be called on off-map units. Its neighborhood scans check
bounds, but entry terrain, barbarian union, final redraw and some origin tests
still use the cached origin/dummy. The entry does not validate a live unit ID.
An inconsistent tile occupant owner can differ from the city/unit record, and
far-scan owner=-1 can reach directed-relation indexing if an object exists but
tile metadata is missing. The nominal domain excludes unbacked such accesses;
it does not assert that the original rejected them cleanly.

Corrupt/cyclic stacks inherit U01's repair, retained-state and fault boundaries.
Invalid collection bounds, mismatched map area, altered neighborhood tables and
callback-invalidated records require their actual backing and live values.
All these operations mutate incrementally: there is no rollback of earlier
knowledge, orders, goals or network requests on failure. A stricter replacement
may reject inputs outside its declared domain before entry, but that is a
compatibility restriction, not the original's validation policy.

## 11. Worked conformance cases

Unless overridden, use a flat 24×16 doubled-coordinate map, nominal W01 tables,
Grassland tiles, valid empty memories/sets and finite stacks; acting unit u at
(10,8), owner 1, type with Land domain, attack 1 and Attack role; local viewer 1,
human set {1}, turn 1, non-network mode, no scenario/full-map suppression,
no treaties and no other objects. External AI-goal and contact children record
ordered calls without mutating state. No child consumes randomness in these
fixtures, so the implementation random contexts remain unchanged.

| Input/action | Expected observations |
| --- | --- |
| Ordinary `revealSurroundings(u,0)` | Explore and replace feature memory on eight neighbors, then center at B[20]; outer tiles untouched. No contact calls. New adjacent exploration sets nearOutline, so the final radius is 2. |
| Extended Air with all 25 positions in bounds | All B[0..24] tiles explored/remembered in order, including center at 20. New outer exploration produces final radius 3. |
| Extended Land, Ocean at B[8], land at B[9] with Ocean in between | B[8] is skipped by the outer tile scan; B[9] is revealed. No intervening-tile occlusion. |
| All memories/exploration current except center; ordinary sight | Center changes far-class flags but not nearOutline; final radius 1, not 2. |
| Foreign city and garrison at (12,8); direct `revealTile(12,8,1)` | Tile explored and features copied; city known and current size published. Garrison seen-by unchanged. No meeting/order/redraw calls. |
| Lone foreign submarine at (12,8) on Ocean; no detector | Adjacent group B cannot mark it seen. Ordinary u can still be seen by that stack and can wake its Sea sleeper through group A. No land contact. |
| Same submarine with a second stack member, or u with detection | Adjacent group B marks the whole foreign stack seen. At B[21]=(14,8), a submarine head remains excluded even with both conditions. |
| Submarine u, foreign city at adjacent non-Ocean (12,8) | City knowledge and its two Land-domain AI goal requests occur first. Remaining adjacent unit/contact work is skipped. City does not mark hidden u seen. |
| Empty foreign city at (12,8), no Contact; u uses Partisans type slot but Land domain | With allowContact=1, immediate origin redraw then meeting request. The Partisans exclusion applies to the foreign-unit branch, not this empty-city branch. |
| Foreign extended Land observer at (14,8), asleep; Alliance 1→2 | Far group B interrupts its goto if applicable, wakes its sleeper despite Alliance, marks u seen by 2 and requests the specified non-Ocean NavalSuperiority goal if Peace 2→1 is absent. |
| Foreign city at (14,8), origin explored by owner 2, ordinary armed u | Far group C marks u seen by 2, although u has no extended sight and does not learn the distant city. |
| Extended u and already-seen ordinary foreign v at (14,8); all map memories current; u lacks its own seen-by bit | Far group A still marks farChange by testing u's set; final radius 2. |
| Turn zero | Same tile/object/order/goal scans, but no final state-sync or area-redraw request; flush occurs. Contact child owns its own turn-zero guard. |
| Barbarian u; origin explored by {1,2} | Entry unions {1,2} into u only; terrain scan adds barbarian exploration. This is not a stack-wide own-owner-filtered mark. |
| Contact callback sets Peace 1→2 | Later adjacent group C reads the new relation and omits its Attack goal. Earlier goals and cached positions survive. |
| AI map exchange A=1,B=2; both explored tile, live Road, A remembers Road, B remembers Mine | B gains Road and retains stale Mine; A unchanged. This is not replacement or two-way memory union. |
| Same exchange; city owned by B, A unaware with remembered size 1, current size 5 | A gains knowledge but its remembered size stays 1. A-known/A-owned branch instead reveals current size to B. |
| Directional Cities offer; donor explored tile, recipient did not and has stale Road memory | Recipient becomes explored and its feature memory becomes empty. Eligible city knowledge/size transfers separately; units do not. |
| Full directional offer; both remember Pollution but live tile is clean | Recipient retains remembered Pollution. With only one memory polluted, the live Pollution component decides whether it survives. |
| Full offer R=1,D=2; donor-owned unit at (10,8), neither civ explored its tile | Unit gains R seen-by; B[0..20] about post-loop (0,16) is published to both where in bounds. Unit tile stays unexplored. No memory update accompanies that edge revelation. |
| Apollo locally authorized, arbitrary alive set | Every map tile explored by all eight; current feature memories for 1..7; live city sizes known by all eight; unit seen-by unchanged; redraw and Astronauts notice. |
| Apollo network client | Only host-directed DoApollo request; no local batch, knowledge mutation or dialog. |

Separate off-nominal checks: `revealTile(...,-1)` has no effects even with
otherwise unbacked coordinates; `writeRemembered` reduces a supplied feature
value before replace/union; selector 8 yields no retained tile bit but does not
make its remembered plane safe; unknown map-offer kinds take the terrain-only
path. Corrupt backing and callbacks that free accessed storage need separate
boundary fixtures; ordinary arithmetic examples do not validate them.

## Appendix A. Reference encoding and call mapping

These are adapters for comparing original snapshots and messages, not required
internal representations. F06 owns persistent encodings; W01/F02/U01 own the
shared record descriptions. Offsets and component masks in this table are
hexadecimal; message numbers are decimal.

| Logical value / operation | Original encoding |
| --- | --- |
| Explored set | Tile byte +4; selector `(1u << (c & 31))`, reduced to a byte on store. Membership query returns the masked value, not normalized 1. |
| Remembered features | Separate byte planes 1..7; address `plane[c] + (x>>1) + (width>>1)*y`. Full byte copied; merge is OR. Feature components include Pollution `80`, City `02`, Fortress `40`, Unit `01`, Road `10`, Railroad `20`, Irrigation `04`, Mine `08`. |
| City knowledge | `known_to` byte +0c; size byte +09; remembered size bytes +0d+c. Knowledge test in map exchange/scan sign-extends known_to before masking. |
| Unit visibility/order | Seen-by byte +09; order byte +0f. Sleep=3, Fortified=2, None=ff; Partisans type index=9. U04 owns the contact-interruption class and role exemption. |
| Unit type properties | SeesTwo `0001`, Submarine `0008`, SpotsSubmarines `4000`; Land/Air/Sea=0/1/2. Attack/Defend/NavalSuperiority goal roles=0/1/2; Diplomacy role=6. |
| Directed treaty tests | Contact `01`, Peace `04`, Alliance `08`. They are tested directionally at the sites specified above. |
| Redraw accumulators | nearChange=1, farChange=2, nearOutline=4, farOutline=8. Outline discoveries set `0x05` or `0x0a`; city-size-only changes set `0x01` or `0x02`. |
| Suppression/mode | Scenario game flag `80`; scenario flags[0] bit `08` suppresses map differences. `reveal_map` is tested separately. Network mode means multiplayer>=3. Investigation flag `400000`. |
| Diplomatic request count | Civilization byte +1f; accepted AI map exchange adds 2 modulo 256. |
| Memory writer | `known_set` `005b9d81`, tile batch operation 7; FeatureLocalSet message 151. Payload x,y,low-byte features,c,merge,forward. |
| Exploration writer | `tile_set_visible` `005b976d`, batch operation 2; SeenSet message 146. Payload x,y,selector,set,forward; selector is the original supplied operand, not its byte reduction. |

Redraw/operation messages used directly here are DoApollo=88,
DrawSquare=114, DrawMap=116, DrawArea=117, DrawMapRegion=118. Broadcast target
is 255; host target is 0. DrawSquare/DrawArea carry x,y then zeros;
DrawMapRegion carries x,y,radius,c then zeros; DoApollo/DrawMap have zero
argument payloads. State synchronization calls are `(255,2,0,0,0)`.
Apollo's Astronauts text request uses text ID 73 with no arguments. Network
player lookup follows civilization→slot→connection at each send; no cached
connection is implied. The setter forwarding flag is 1 for a client request
to host 0 and 0 for a host broadcast. N02 owns full packet framing and batching nesting.

## Appendix B. Evidence, validation and integration

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

The OpenCiv2 evidence pin is
`a71e8aefffe04e6f719e0e213b027b128a747711`. The inspected reference checkout
`fc2155ccdb2123c1137babab6fa39e38152278c9` has identical selected implementation
files; later history there changes documentation. Independent reconstruction
was inspected read-only at `civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. Source access is optional for
implementing the contracts above; evidence code/assets are not included here.

| Evidence | Contract checked |
| --- | --- |
| [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp), `004274a6` and its extracted tile/adjacent/far/redraw helpers | Entry caches, three distinct scans, opposite-direction visibility, contacts/goals and final redraw. `0042738c` and `004273e6` supply goto interruption and sentry waking. |
| [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp), `004272d0`, `0058fda9`, `005b8b1a`, `005b8b65`, `005b976d`, `005b9d81` | Direct tile reveal, occupied-neighbor reveal, memories, membership queries and setters. |
| [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp), `0043cc00`, `004f1220`, creation `0043f8b0` | City knowledge primitive, Apollo and new-city knowledge initialization. Area refresh `0043f7a7` has no knowledge update. |
| [diplomacy.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/diplomacy.cpp), accepted map-sharing helper within `0045dd7f` | Actor/other ordering, asymmetric feature and city transfer. |
| [network.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/network.cpp), `parley_give_maps` `004dd8ad` | Three offer kinds, Pollution reconciliation, post-grid-coordinate neighborhood and publication. Unit transfer invokes surroundings with contact=0. |
| [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp), placement within `004a7ce9`; [window.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/window.cpp), `004a93b3` | F05's extra radius publication only sets exploration; improvement memory has a separate call. |
| Unit movement `0059062c`, investigation within `004c6bf5`, pollution `005b90df`, incited transfer `004c66ba`, elimination `004e1763` | Reviewed refresh/child boundaries in sections 8–9, not complete specifications of those callers. |
| [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h) | Named flags, role/order values and message IDs. |

The independent reconstruction's `src/unit_exploration.cpp` (`reveal_tile_to`,
`unit_explore`) agrees on the split scans, cached/live reads, far self-visibility
test and directed goals. `src/multiplayer_request.cpp` confirms that `004ddb9a`
and `004dddac` use the post-grid cursor for offer neighborhoods. `src/diplomacy_commands.cpp` also confirms the exchange direction: the
granting party is the reference second argument, and only that party-known
branch publishes the current city size to the requester. `src/diplomacy_commands.cpp` also confirms the exchange direction: the
granting party is the reference second argument, and only that party-known
branch publishes the current city size to the requester. Its explicit
backing stops are reconstruction safety boundaries, not original admission
checks. No production source or saved analysis was changed for this document.

### Checks performed

A temporary native harness extracted **30 reference functions**, including the
complete surroundings helpers, direct tile and neighbor revelation, setters,
city reveal, both exchanges and Apollo. **46,507 assertions passed**. Cases
covered map-edge/topology/domain/extended footprints, cached versus live contact
effects, city-or-stack selection, submarine/multi-unit/detector distinctions,
far observers, scenario/full-map suppression and remote redraw recipients,
barbarian union, map-offer scopes and Pollution,
asymmetric sharing/size memory, the displaced neighborhood and Apollo's client
and local branches. Full feature/membership arrays and selected ordered effects
were checked, not only return values.

The harness uses signed reference records and wrapping compilation. Pointer
temporaries in two translated helpers were widened for native execution; dead
pointer high-byte construction was discarded where the writer stores only a
byte. Nominal finite stack traversal/counting and city/unit lookup were adapted
to their U01 contracts. AI goal insertion, contact, rendering, batch management
and network transport were trace adapters; one contact fixture deliberately
changed the treaty before the subsequent read. Optional goto extensions were
disabled. This establishes the documented source cases, not original-executable
equivalence, real diplomacy decisions or network convergence. No original game
binary was executed in this check, and no harness/source/assets were imported.

Historical documentation checks covered links, headings and specification IDs.
Original-runtime comparison,
corrupt backing/cycle cases, reentrant network/presentation callbacks and the
complete joined setup/movement/investigation paths have not been validated here.

### Integration obligations

- U01 creation now has a concrete surroundings child with allowContact=1;
  a joined creation fixture must exercise real revelation/contact rather than
  U01's earlier stub before claiming full lifecycle conformance.
- F05 radius-only publication must remain distinct from surroundings and
  `revealTile`; complete setup still needs its real city/research/path children.
- [U02](U02-movement-and-zones-of-control.md) specifies movement clearing,
  pre-move publication and common-finish revelation. Its dispatcher checks still
  adapt this child; real movement/contact joins remain required. U04 owns
  the shared goto/sentry helpers; joined activation/revelation cases must retain
  live-field reads after contact callbacks. U05 must retain its caller timing.
- C01/C09/C08 and V01/V06 must reconcile their distinct city knowledge,
  ownership, deletion, investigation and Apollo-trigger phases.
- A01/V04/V05 and N02 must join real goal/contact/batch/offer operations
  without reordering the callback boundaries or repairing the classic map
  offer's displaced-neighborhood behavior.
- P02/P06 must connect knowledge consumption and redraw-radius requests to
  rendering/inspection without converting persistent memory into continuous
  visibility. X02 should register source and original-executable vectors
  separately, including asymmetric city-size transfer and the far redraw test.
