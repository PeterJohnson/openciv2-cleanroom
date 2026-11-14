# Map geometry and spatial state

**Required profile:** [X01 fresh-game isolation and bug repairs](X01-classic-compatibility-contract.md#fresh-game-isolation-and-supported-bug-repairs)
and [behaviorally significant ordering](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
override conflicting procedures and examples below. Retention applies within a
game or to explicit imported inputs, not across new games. Confirmed original-bug
stack/backing/stop cases are optional diagnostics; use documented safe repairs.
Only real effects and callbacks impose ordering; test adapters cannot invent them.
Unspecified safe continuations remain explicit gaps, not required arbitrary bytes.

**ID:** W01. **Target:** Civilization II Multiplayer Gold
Edition **5.4.0f, patch 3**, under [X01](X01-classic-compatibility-contract.md).

This specification owns tile coordinates, ordered neighborhoods, geometric
queries, primitive spatial reads and writes, region labeling and neighboring
water sets, and coordinate-derived resources, shields and huts. It uses
[F01](F01-determinism-and-arithmetic.md) for arithmetic and
[F02](F02-game-state-and-invariants.md#7-world-and-spatial-state) for the meanings
of shared map state. File decoding, visibility policy, path search and gameplay
consequences of terrain edits are separate operations; their boundaries are
listed below. Neighboring-hostile-unit and zone-of-control queries belong to
U02, irrigation-source eligibility to
[W04](W04-terrain-work-and-improvements.md#irrigation-source-predicate), and
coast-rendering masks to
[P02](P02-map-rendering-and-minimap.md#coast-quadrants).
This document supplies their coordinates and tile primitives.

Evidence is pinned to OpenCiv2
`a71e8aefffe04e6f719e0e213b027b128a747711` and the independent reconstruction at
`civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. Original encodings, source addresses
and verification limits appear in the appendices. Numeric IDs, scan order and
value reductions below are behavioral requirements; an implementation may use
its own storage and indexing structures.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

Pure geometry queries can use equivalent evaluation order. Region discovery,
label assignment, retained counts, first/last traversal results and callbacks
remain ordered wherever a later consumer can distinguish them.

## 1. Inputs and domain

Inputs are the allocated map, doubled width `W`, height `H`, current flat-world
rule, saved pattern seed `P`, tile and region state, and the operation's arguments.
Some operations also read directed treaties or multiplayer synchronization
controls. Outputs are the specified value, state mutations and ordered callbacks.
None of these primitives consumes a random draw or samples the clock. Pattern
seed creation is an F01 operation; reading that seed is not a random draw.

For nominal operation, `W` is positive and even, `H` is positive, there are
`A=(W/2)*H` tiles, and all referenced state is supplied. New-game dimension
acceptance is specified by F05; saved-file acceptance belongs to F06. Coherent
reference maps have an area representable as a positive signed value below
32768. Generation's nominal area limit is smaller. This is a backing/domain
condition, not a bounds check secretly performed by every helper.

Ordinary coordinates use the canonical parity defined below. Some original
callers deliberately pass a different parity or an out-of-range coordinate;
those cases have specified behavior. Keep the following distinctions:

- A rectangular miss through tile lookup reaches a supplied shared fallback
  tile. It does not cause an arbitrary memory access.
- Remembered-layer and coarse-grid direct indexing have no fallback or bounds
  check. Unsupported indices need original backing or an explicit rejection
  boundary; do not invent a zero result.
- Invalid dimensions, region references or civilization indices may exceed the
  original tables. Arithmetic results alone do not make the subsequent access
  defined. An implementation may reject these inputs outside its declared
  compatibility domain.

Out-of-bounds writes through direct indices are excluded bugs under X01;
supplied adjacent backing does not make their corruption required. Reject or
stop an unsupported write before it damages another object. This is distinct
from the deliberate shared fallback tile, whose specified mutations remain
in scope.

Within the nominal domain, ordinary mathematical arithmetic suffices except at
explicit reductions. For off-nominal scalar compatibility, use F01's `s32`,
`u32` and arithmetic right shift at the original operations, as detailed in
Appendix A. No operation turns a supplied negative coordinate into its absolute
value unless its formula explicitly says so.

## 2. Coordinates, lookup and lifecycle

Let `U=W/2`. Canonical coordinates satisfy `0<=x<W`, `0<=y<H`, and equal x/y
parity. All full-map scans in this specification visit rows ascending and then
x ascending in increments of two:

```text
for y = 0 .. H-1:
    for x = y mod 2; x < W; x += 2:
        visit(x,y)
```

`inBounds(x,y)` tests only the two rectangular intervals. Tile lookup does not
wrap or validate parity. For an in-bounds argument its row-order index is
`y*U+floor(x/2)`. Thus `(8,5)` and `(9,5)` identify the same stored tile even
though only `(9,5)` is canonical. This does not canonicalize the arguments for
other calculations: coordinate predicates still use the x/y actually supplied.

Any rectangular miss returns the **same mutable fallback tile**, whose entry
state is retained. A write through one miss is visible to another miss. Creating
or freeing the ordinary map does not reset this tile. Owner queries have an
explicit miss rule, while many terrain/feature queries simply read the fallback.
Caller-driven replacement of that state is an observable mutation, not a reason
to assume the fallback is always ocean or empty.

### Horizontal wrapping

```text
wrapX(x):
    if not flat:
        if x < 0: x += W
        else if x >= W: x -= W
    return x
```

There is no vertical wrap. This is one correction, not modulo: for `W=100`,
`wrapX(201)=101` and `wrapX(-201)=-101`. Apply it only at call sites that request
it. In particular, direct tile lookup and the resource/hut predicates do not
invoke it. Geometry uses the **current game rule**, not an independent copy of
the topology recorded in a file header.

The coarse grid has dimensions `CW=ceil(W/4)`, `CH=ceil(H/4)`.
`coarseWrapX` is the same single correction using `CW`. A direct coarse lookup
uses `y*CW+x`, without wrapping or checking either coordinate. Which grid is
selected and when its contents are rebuilt belong to the calling operation.

### Map allocation and release

On successful allocation, derive `A,CW,CH` from the supplied dimensions. In
canonical row order initialize every tile to plain Ocean, with all feature,
radius, region and revelation state zero, fertility zero and occupant owner
**civilization 0**. These newly allocated tiles are not initially unowned.
Create remembered-improvement layers for civilizations 1 through 7, all zero.
The civilization-0 layer is untouched. Allocate four coarse grids without
initializing their contents, then mark map storage allocated.

Allocation does not generate a pattern seed, clear region summaries, initialize
coarse connectivity, initialize the shared fallback, or free a preceding map.
The calling setup/load path supplies those separate operations. Do not read an
uninitialized coarse grid as if allocation guaranteed zeros.

Release does nothing if storage is not marked allocated. Otherwise release
coarse grids in order 3,2,1,0, then tiles, then remembered layers 1 through 7;
clear the allocated state last. Dimensions, seed, summaries and fallback remain.
Individual unlock/free calls depend on non-null storage/handles in the original;
their representation is not a requirement for a new allocator. Partial allocation
failure is not a successful empty map: preserve preceding writes and report the
failure at the failing allocation/lock, with the platform error outcome supplied
by the caller's failure contract. Original allocation details are in Appendix A.

A bulk tile-field fill covers exactly `A` records; reference evaluation visits
row order. A bulk field copy reads one source field and writes its destination
field for each record before advancing. Independent records may be processed
in another order when all accesses are backed and no admitted observer sees
intermediate updates. Preserve source/destination alias effects and any admitted
read/failure prefix; otherwise retain the reference traversal. The original
versions operate on bytes and can be used on scratch fields; their adapters
are not a general transaction or a request to recompute derived state.

## 3. Ordered neighborhoods

These are the nominal constant tables, not interchangeable sets. Wrap the
neighbor's x and then check rectangular bounds unless an operation explicitly
specifies different handling. The result retains its supplied coordinate;
lookup aliasing is applied only when accessing tile state.

| Direction `d` | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `DX[d]` | 1 | 2 | 1 | 0 | -1 | -2 | -1 | 0 | 0 |
| `DY[d]` | -1 | 0 | 1 | 2 | 1 | 0 | -1 | -2 | 0 |

Indices 0..7 are the eight directions; 8 is the center only when a caller
includes it. Cardinal steps use 1,3,5,7 in order. The diagonal table `Q` is
`[(1,-1),(1,1),(-1,1),(-1,-1),(0,0)]`; its first four entries are directions
0,2,4,6. Callers such as irrigation-source queries also include its center.
The coarse direction table is
`[(0,-1),(1,-1),(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1)]`.

The ordered radius table `B` contains 45 pairs:

```text
index:  0       1      2      3      4       5       6       7
B:     (1,-1) (2,0)  (1,1)  (0,2)  (-1,1) (-2,0)  (-1,-1) (0,-2)
index:  8       9      10      11      12      13      14     15
B:     (2,-2) (2,2)  (-2,2)  (-2,-2) (1,-3)  (3,-1)  (3,1) (1,3)
index:  16      17      18       19      20     21     22     23     24
B:     (-1,3)  (-3,1)  (-3,-1)  (-1,-3) (0,0)  (4,0) (-4,0) (0,4) (0,-4)
index:  25      26      27      28      29      30     31     32     33     34
B:     (1,-5)  (2,-4)  (3,-3)  (4,-2)  (5,-1)  (5,1)  (4,2)  (3,3)  (2,4)  (1,5)
index:  35      36      37      38      39      40       41       42       43       44
B:     (-1,5)  (-2,4)  (-3,3)  (-4,2)  (-5,1)  (-5,-1) (-4,-2) (-3,-3) (-2,-4) (-1,-5)
```

The usual city radius is B[0..20]; B[20] is the center. “20 neighbors” excludes
it. Extended revelation may include B[21..44]. Do not sort by distance or move
the center to the start. The original tables reside in writable data; nominal
inputs use the values above. A reference-memory fixture that supplies altered
tables must treat those values as inputs rather than silently restoring defaults.
[W05](W05-pollution-and-global-warming.md#notice-iteration-and-neighborhood) owns
warming's distinct 20-entry adjacent-storage read sequence; it is not B[0..19].

## 4. Distances, directions and geometric selection

For ordinary inputs define:

```text
wrappedDx(x1,x2):
    d = abs(x1-x2)
    if not flat and d > floor(W/2): d = W-d
    return d

stepDistance(x1,y1,x2,y2):
    return floor((wrappedDx(x1,x2) + abs(y1-y2))/2)

weightedFromDeltas(dx,dy):
    dx = abs(dx); dy = abs(dy)
    h = floor((dx+dy)/2)
    return max(dx,dy) - floor((h-min(dx,dy)+1)/2)

weightedDistance(x1,y1,x2,y2):
    return weightedFromDeltas(wrappedDx(x1,x2), abs(y1-y2))
```

These are two distinct distances. Neither computes a path or checks terrain,
movement cost, owner, parity or bounds. At exactly half the circumference the
horizontal distance is unchanged. Distant off-map coordinates can make
`wrappedDx` negative because it performs only `W-d`; `stepDistance` can then
be negative. `weightedFromDeltas` takes an absolute value again. Do not replace
these branches with a normalized shortest-path distance for arbitrary inputs.

To choose a direction from `(dx,dy)`, reduce each component to its sign, start
with result 8, and scan directions 0..7. Whenever both table-component signs
match, assign that direction. Return the last match. With the nominal table each
nonzero sign pair has one match; `(0,0)` returns 8. Magnitude is ignored; this
helper does not first compute the horizontal wrap of a displacement.

### Span and rectangle membership

To test x in `[left,left+width)`, work on a local copy of x. If cylindrical,
first add `W` when `x<left`, then independently subtract `W` when
`x>=left+width`. The second condition observes the first change. Finally require
`x>=left` and `(left+width)-x>=1`. Flat maps skip both corrections.
A rectangular test first uses that x test, then requires
`top<=y<top+height`. Neither checks the world's bounds or canonical parity;
a rectangle can intentionally straddle the seam or describe a region beyond
the map. Width/height are span lengths, not inclusive end coordinates.

### Two-position terrain selection

For a base `(x,y)` and requested ocean predicate, try `(wrapX(x),y)` and then
`(wrapX(x+1),y+1)`. Select the first in-bounds candidate whose ocean status
matches. Publish the selected coordinates only on success. One helper returns
success/failure; the coarse-region helper returns the selected tile's region or
Absent on failure. Failed calls leave coordinate outputs unchanged. This is
not a search of all tiles in a coarse cell and does not canonicalize parity.

## 5. Spatial queries

The base terrain, features, radius claimant, fertility and region queries read
the selected tile, including the shared fallback on a rectangular miss. Ocean
means base terrain exactly Ocean; preserved terrain flags do not change that
test. The land-only region query returns Absent for Ocean, otherwise the stored
region. It does not recalculate a connected component.

Occupant owner explicitly returns Absent for a rectangular miss or the retained
Unowned state. Other supplied owner values are returned unchanged. The gated
city, unit, airbase and city-or-unit owner queries use F02's feature conditions;
they do not look up records or verify ownership consistency. A tile with both
city and fortress components is an airbase, not a city.

`claimantOrFertility` first returns a nonzero radius claimant. Otherwise read
fertility: return 8 for values 1 through 8, and return the fertility itself for
0 or 9 through 15. Claimant and occupant owner are different inputs.

The peaceful foreign-radius query returns Absent unless all conditions hold:
coordinate in bounds; querying civilization greater than zero; tile not Ocean;
radius claimant greater than zero and different from the querying civilization;
no directed alliance from querying civilization to claimant; and a directed
peace treaty on that same relation. If all hold, return the claimant. An alliance
suppresses this result even if peace is also present. This is not a universal
movement or trespass permission rule.

Revelation membership returns true for a negative civilization selector.
Otherwise test that civilization's membership in the tile's revelation set.
The original result is a mask rather than normalized 1 (Appendix A). This query
does not update remembered improvements or establish unit visibility.
Remembered-improvement indexing uses the same row-order index in the selected
civilization layer, without the tile accessor's fallback or bounds checking;
[W03](W03-visibility-and-exploration.md#2-knowledge-primitives) defines memory
replace/merge, copying and revelation policy.

## 6. Primitive mutations and refresh boundaries

A primitive tile setter resolves the tile, captures the old value of the
field being changed, writes the result, and then applies synchronization below.
It does not independently check bounds: a miss can mutate the fallback. For
nominal operands:

| Operation | Effect; everything else is retained |
| --- | --- |
| Set/clear feature components | Add/remove the supplied components individually. Compound city/airbase and irrigation/farmland states can change as a consequence. |
| Set base terrain | Replace the base kind and retain river, suppression and other terrain flags. |
| Set/clear revelation | Add/remove the supplied civilization memberships. |
| Set fertility | Store the operand modulo 16; retain occupant owner. |
| Set occupant owner | Store civilization 0..7 or Unowned; retain fertility. |
| Set region | Store the operand modulo 256; do not alter summary counts or border sets. |
| Set radius claimant | Store the operand modulo 8; retain the other radius state. |

For exact off-nominal behavior, owner 8 is accepted and retained too; negative
owners or owners greater than 8 become Unowned. A terrain operand is reduced
modulo 256 and can **set** preserved flags as well as replacing the base kind;
it cannot clear a previously set preserved flag. Appendices give the adapter
that makes this behavior unambiguous. Feature/revelation operands retain only
their first eight component positions. A zero clear/set selector means clear;
any nonzero selector means set.

### Synchronization after mutation

A setter produces no synchronization if synchronization was not requested, the
complete stored field did not change, or the session is not a network mode. Otherwise:

1. On a client with tile batching active, append the corresponding tile operation
   to the batch, even if immediate tile synchronization is disabled.
2. Otherwise, if tile synchronization is enabled, a client sends a request to
   the host and a host broadcasts the update. Host-side batching does not take
   the client branch.
3. Otherwise retain only the local mutation.

The operation carries the supplied coordinates and operand, not a canonicalized
coordinate or the resulting whole tile. Owner normalization occurs before its
operand is sent. Stored reductions for fertility, region, terrain and claimant
do not generally reduce the transmitted operand. N02 owns message encoding,
authority and replay of these operations; Appendix A identifies the original
operation and field codes for adapters. This boundary is relevant even when a
new implementation models messages without the original packet layout.

### Derived-state discipline

These setters do not refresh region IDs/counts/borders, fertility, connectivity,
path-search caches, city outputs or remembered improvements. A terrain setter
can leave a former land region ID on an Ocean tile; queries then interpret that
ID in the water namespace. A feature setter can leave memories stale. Preserve
that state until the calling operation explicitly performs a refresh.

A region rebuild replaces IDs, land/water counts and neighboring-water sets,
while retaining fertile-tile counts and unrelated region fields. Rebuilding
borders alone replaces only neighboring-water sets. Neither refreshes coarse
connectivity or path state. Path search and its cache invalidation belong to
[U03](U03-pathfinding-and-goto.md); revelation and remembered-layer refresh to W03; complete terrain-work and
editor refresh sequences to [W04](W04-terrain-work-and-improvements.md#derived-state)/S03;
fertility calculation to W02. Reconcile these named operations at their callers,
not with a universal
“map changed” rebuild.

## 7. Region labeling

A conventional connected-components algorithm is not sufficient to reproduce
the numeric IDs and counts. In particular, cylindrical scanning carries the
current run across row boundaries, water IDs have special aggregation rules,
and the overflow bucket's count is not recomputed from assigned tiles.

Clear land and water counts for records 0..63 and set every tile's region ID to
zero. Initialize separate land and water overflow indicators to false. Process
ocean tiles first (`kind=Water`), then land (`kind=Land`), independently. Each
pass has a temporary label per tile, initially zero, and a count per provisional
ID, with absent counts treated as zero. Zero denotes an unlabeled tile.

### Provisional labels

Initialize `next=0`, `current=0`, `run=false`. Scan canonical coordinates in row
order. At the start of each row, set `run=false,current=0` **only if flat**.

If the tile does not belong to the pass's land/water kind, set `run=false,current=0`
and continue. Otherwise:

1. When `y!=0`, examine neighbor directions `6,7,0`, in that order. Wrap x and
   ignore out-of-bounds neighbors and zero temporary labels. These are
   `(-1,-1)`, `(0,-2)`, and `(1,-1)`, not all previously scanned neighbors.
2. For a nonzero neighbor label `other`, if `current` is nonzero and differs
   from `other`, merge the larger ID into the smaller. Add the larger count to
   the smaller, zero the larger count, and replace every occurrence of the
   larger label throughout rows 0 through y inclusive with the smaller ID.
   Include the whole current row in this replacement scan. Set `current` to
   the smaller. If no merge is needed, set `current=other`.
3. If `current==0`, and `run` is false, choose the smallest positive ID `next`
   with `count[next]==0`, and set `current=next`. The original temporary-label
   capacity cannot be exhausted within the valid map-size domain.
4. Assign the current tile's temporary label to `current`, increment
   `count[current]`, and set `run=true`.

Cleared counts permit provisional ID reuse. On a cylindrical world, a run at
the end of one row can flow directly into the first matching tile of the next
row. Preserve that behavior even when a generic flood fill would differ.

### Small oceans

On the water pass only, process IDs 0 through 62. Whenever `count[id]<9`, add
that count to `count[63]` and add the ID to a temporary set `small`. Then, if
`small` is nonempty, scan **all** temporary labels: any label in `small` becomes
63. Finally clear the counts of all IDs in `small`.

This includes ID zero, whose count is zero. Thus land tiles, which were unlabeled
in the water pass, temporarily receive water label 63 too. The later land pass
overwrites them. Only provisional water IDs 0..62 undergo this particular
less-than-nine aggregation; higher provisional IDs follow the next rule.

### Publishing labels and overflow

Start with an empty `remap` table. Scan temporary labels in row order. Ignore
zero labels. For a label `id>63`:

```text
if id in remap:
    id = remap[id]
else if count[id] == 0:
    id = 0
else:
    free = first positive index with count[free] == 0
    if free < 64 and (kind == Land or count[id] > 16):
        count[free] = count[id]
        remap[id] = free
        id = free
    else:
        overflow[kind] = true
        id = 63
```

Assign the resulting ID to the tile. On overflow, do not add the overflowed
component's count to slot 63 and do not record a remapping for that component.
Copy `count[0..63]` into the records' water counts for the first pass or land
counts for the second. Return both overflow indicators to the caller. Their
packed return encoding, and the original use of negative counts to hold
remappings, are validation details in the appendix.

### Neighboring water regions

After both passes, clear the neighboring-water set of every record. Scan land
tiles in canonical order. Walk neighbor directions with this exact skip behavior:

```text
d = 0
while d < 8:
    q = (wrap(x+DX[d]), y+DY[d])
    if inBounds(q) and ocean(tile(q)):
        add tile(q).region to record[tile(x,y).region].neighboringWater
        d += 2 - (d % 2)
    d += 1
```

Do not check all eight neighbors after finding water: the algorithm skips two
following directions after an even hit or one after an odd hit. This can omit
contacts that an exhaustive adjacency calculation would include. Only the land
record gains a neighboring-water entry.

### Region queries and callback order

Region assignment writes tile IDs directly; it does not call individual tile
setters or emit their synchronization operations.

`borders(landID,waterID)` tests membership in that land record's neighboring-water
set. `countBordering(waterID)` counts records **1 through 62** whose sets contain
the water ID; records 0 and 63 are excluded. Adding a border inserts one water
ID without making the relation symmetric or changing a count.

During a full rebuild, pump pending UI/network work after each row of provisional
labeling, after each tile of publication (including zero labels), and after each
row of border computation. On an uninterrupted coherent map this is
`2*A+3*H` pump calls. Small-ocean aggregation and summary-count publication do
not add pump calls. Border-only rebuilding pumps `H` times. No algorithm-owned
random draws occur. Fixtures must keep unrelated mutation/reentrancy outside
these calls or supply the invoked message processing and its intermediate state;
a pump is not permission to reorder completed tile operations.

Counts are cleared before temporary-storage allocation; tile labels are cleared
only after both temporary buffers are available. If either lock fails, report
the error, retain the preceding count clears, and do not enter either pass or
rebuild borders. If error reporting returns, release the label temporary and
then the count temporary (including the original null-handle cleanup where
applicable), and return the current overflow result. No successful assignment
is implied by a zero overflow result on this failed path.

## 8. Coordinate features

These predicates consume no random draws. They use the saved resource seed `P`,
independently of either gameplay sampling law.

### Grassland shield

For the supplied coordinate, the grassland-shield helper returns:

```text
mod(-floor((x+y)/2) - x, 4) < 2
```

The helper checks neither terrain nor bounds. Its callers decide whether to
use the result for grassland. Coordinate aliasing in tile lookup does not change
the x/y used in this expression.

### Special resource

Return zero if the coordinate is out of bounds, its resource is suppressed, or
base terrain is grassland. Otherwise:

```text
u = floor((x+y)/2)
v = x-u
cell = mod(u,4) + 4*mod(v,4)
pattern = mod(11*floor((x+y)/8) + 13*floor(v/4) + P, 16)
if cell != pattern: return 0
scale = 2 ^ mod(floor(P/16),4)          # mathematical exponentiation
return 2 if mod(floor(u/scale),2) == mod(floor(v/scale),2) else 1
```

`v` can be negative; use the stated floor and nonnegative-modulo operations.
Results 1 and 2 select the first and second special-resource variants in consumers,
although fertility treats both as selecting the first variant's score.

### Hut

Return false if out of bounds, ocean, or the tile is not unowned.
Otherwise use the same `u`, `v`, and `cell` and return:

```text
cell == mod(11*floor((x+y)/8) + 13*floor(v/4) + P + 8, 32)
```

The right side ranges from 0 to 31 whereas `cell` ranges from 0 to 15; values
16..31 do not match. Resource suppression does not suppress huts. The owner
state alone gates occupancy here: an unowned tile with a retained feature can
still qualify. A resource/hut query does not change the seed or consume a hut.
[W06](W06-huts-and-barbarians.md#hut-entry-and-consumption) owns hut-entry outcomes
and the caller sequence that retains hut presence before relocation changes
the occupant owner.

## 9. Worked examples and conformance cases

Unless stated otherwise, use coherent allocated state, nominal tables, no
callback mutations, local single-player mode and no pending messages. Save the implementation's
random contexts before each call; these operations leave them unchanged.
Unmentioned tile fields and summaries must remain unchanged.

| Input / operation | Expected output and effects |
| --- | --- |
| `W=100,H=80`; lookup `(8,5)` and `(9,5)` | Both select row-order index 254; a write through either is visible through both. |
| Cylindrical `W=100`; wrap `-1,100,201,-201` | `99,0,101,-101`, respectively. Flat wrapping returns the inputs. |
| Cylindrical `W=100`; from `(98,0)` to `(0,2)` | Wrapped dx 2; step distance 2; weighted distance 2. |
| Deltas `(6,2)` and `(2,6)` | Weighted distance 5 in either order. Step distance for the same unwrapped deltas is 4. |
| Direction of `(20,-1)`; direction of `(0,0)` | 0; 8. No angular or distance weighting. |
| Cylindrical span `left=95,width=10,W=100`; x `0,4,5,95` | True, true, false, true. Rectangle vertical endpoints remain half-open. |
| Fallback is Ocean, fertility 7, claimant 7, retained owner 3; query a rectangular miss | Ocean true, fertility 7, claimant 7, owner Absent. Mutating fallback fertility affects later misses, preserving its retained owner. |
| Tile radius claimant 0; fertility `0,1,8,9,15` | Claimant-or-fertility returns `0,8,8,9,15`. With claimant 3 it always returns 3. |
| Occupant owner 3; city+fortress components; no unit component | Airbase owner 3; city owner and unit owner Absent. Clearing fortress changes city owner to 3. |
| Unsuppressed Desert `(0,0)`, `P=16`, Unowned | Resource 2; no hut. |
| Desert `(0,0)`, `P=24`, Unowned | Hut true even when resources are suppressed; resource 0. Changing occupant to 0 suppresses the hut. |
| Shield query at `(0,0)`, `(2,0)`, `(1,1)` | True, true, false, regardless of supplied terrain. |
| Flat `W=8,H=4`, base `(0,0)` is Ocean, `(1,1)` is land region 7; select land | Second candidate selected and returned coordinates `(1,1)`; region variant returns 7. If both candidates miss, coordinate outputs retain their entry values. |
| `W=4,H=2`, all Ocean, rebuild regions | Every tile region 63; water count 63 is 4; all land counts 0; no borders or overflow; 14 pump calls. |
| Same dimensions, all land | Every tile land region 1; land count 1 is 4; water counts all zero; no overflow; 14 pumps. |
| `W=16,H=2`, all Ocean except land at `(14,0)` and `(1,1)` | Cylindrical: both land ID 1/count 2 through row carry. Flat: IDs 1 and 2/count 1 each. Water count 1 is 14; no overflow. |
| Flat `W=12,H=1`; tiles in row order alternate land/Ocean | Regions `[1,63,2,63,3,63]`; land counts 1..3 each 1; water count 63 is 3. |
| Land region 5 at `(4,4)` surrounded in direction order by distinct water IDs 1..8, with no other land | Border set for 5 is `{1,4,6,8}`, not all eight. Count-bordering(1) is 1. Adding the same border to records 0 and 63 does not increase that count. |
| Primitive terrain change from land to Ocean | Only terrain field changes (plus optional sync); existing region, borders, fertility, path state and memories remain. |
| Network client, batching enabled, immediate synchronization disabled; set an absent feature with sync requested | Mutate locally and append one batch operation. Repeating the identical set emits no additional operation. |

For region-capacity cases, use a flat `W=100,H=100` world and the 256 isolated
coordinates `(2+6*i,2+6*j)` for `i,j=0..15`, in canonical row order. With land
at these sites and Ocean everywhere else, land IDs 1..62 each cover one site;
all remaining sites have ID 63. Land count 63 remains **1**, not 194; return
land overflow. Reverse land and Ocean: all 256 water sites get ID 63, but water
count 63 is **63**, not 256; return water overflow.

To isolate the water publication threshold, use flat `W=100,H=60`, initially
all land. Place Ocean at the first 63 isolated coordinates from that same
row-order list. Add a final connected Ocean block: rows 40..43, each with four
canonical tiles starting at x=`y mod 2`. Its provisional ID is 64 and size 16:
it is assigned 63 with water overflow, and count 63 remains 63. Add `(0,44)` to
make the block size 17: it remaps to now-free ID 1, water count 1 becomes 17,
count 63 stays 63, and there is no overflow.

### Off-nominal compatibility examples

| Input / operation | Expected result within the stated backing domain |
| --- | --- |
| Cylindrical `W=100`; distances between `(0,0)` and `(250,0)` | Wrapped dx −150, step distance −75, weighted distance 112. No bounds check or repeated wrap. |
| `W=100,H=80`; lookup `(8,5)` versus `(9,5)`, then run coordinate patterns on each supplied pair | Lookup aliases; pattern calculations still use different original coordinates and need not agree. |
| Set owner 8, then 9, then −1 | Retain 8; then Unowned; then Unowned. No new civilization slot is allocated. |
| Set fertility −1, region −1, claimant −1 | Values 15, 255, 7 respectively. Region 255 is a defined stored result but does not establish valid backing for a later summary lookup. |
| Existing river flag and base kind 4; terrain operand 64 | Replace base kind with 0, retain river and set resource suppression. Terrain operand 0 afterwards cannot clear those preserved flags. |
| Revelation query for civilization 32 | Original shift reduction tests membership 0; a restrictive implementation may reject selector 32. |

## Appendix A. Original representation and arithmetic adapters

### Coordinates and arithmetic

Map header width, height, area and coarse dimensions are signed 16-bit fields;
seed and stored topology are unsigned 16-bit. Header size is 14 bytes. Runtime
wrapping tests game-rule bit `0x8000`, not the header's topology field. The area
calculation uses truncating division of width by 2 followed by multiplication
and signed-16 reduction; coarse dimensions reduce `(dimension+3)>>2` to signed
16. For malformed dimensions these derived values need not describe usable
storage; F06 owns file admission and the original failure boundary.

Tile addressing is `base + (W & ~1)*y*3 + (x & ~1)*3`, following a rectangular
bounds check. The fallback is at original address `006d1188`. Remembered-layer
indexing is `(x>>1)+(W>>1)*y`. Coarse indexing is `CW*y+x`. These formulas
explain aliasing without requiring a new implementation to use packed records.

For off-nominal scalar geometry, subtraction, addition and negation in the
distance formulas reduce with `s32` after each operation. `abs32(v)` returns
`s32(-v)` for `v<=0` and v otherwise, so the most-negative input stays negative.
`wrappedDx` first applies it to `s32(x1-x2)` and then performs its signed
half-width comparison and, when selected, `s32(W-d)`.
`stepDistance` returns `s32(dx+abs32(s32(y1-y2))) >> 1`.
In the weighted helper, apply `abs32` separately to both incoming deltas;
compute `h=s32(dx+dy)>>1`; choose the larger by signed comparison and return
`s32(larger - (s32(s32(h-smaller)+1)>>1))`. Shifts here are arithmetic.
The span helper also reduces its adds/subtracts before signed comparisons.
The shield expression reduces the sum, negation and subtraction at those same
boundaries. Within in-bounds nominal coordinate patterns, the mathematical
floor/mod expressions in section 8 equal the original signed shifts and masks.

### Tile and region mapping

| Tile byte offset | Meaning / original encoding |
| --- | --- |
| 0 | Terrain low nibble: Desert 0, Plains 1, Grassland 2, Forest 3, Hills 4, Mountains 5, Tundra 6, Glacier 7, Swamp 8, Jungle 9, Ocean 10. Resource suppression `0x40`, river `0x80`; other high bits retained. |
| 1 | Unit `0x01`, city component `0x02`, irrigation `0x04`, mine `0x08`, road `0x10`, railroad `0x20`, fortress `0x40`, pollution `0x80`. City test `(features & 0x42)==2`; airbase test `==0x42`. |
| 2 | Radius claimant in bits 5..7; other radius state in low five bits. |
| 3 | Unsigned region ID. |
| 4 | Revelation bits for civilizations 0..7. |
| 5 | Occupant owner high nibble, Unowned 15; fertility low nibble. |

Set-terrain writes `(old & 0xf0) | u8(operand)`; it does not mask the operand
to four bits. Fertility uses `(old & 0xf0) | (operand & 15)`. Owner uses the
normalized operand in the high nibble while retaining the low nibble. Region
reduces to `u8`; claimant reduces modulo 8. Feature/revelation updates perform
OR or AND-with-complement of the low eight operand bits. A revelation query for
nonnegative c returns `tile.visible & (1 << (c mod 32))`; negative c returns 1.
The low-nibble terrain reader returns values 11..15 too if supplied, and only
10 is Ocean; later terrain-table access may then exceed its defined domain.

There are 64 region records, each 16 bytes. Land, fertile-tile and water counts
are signed-16 values at offsets 0,2,4. Neighboring-water bitset bytes occupy
7..14; water ID b uses byte `b/8`, bit `1<<(b mod 8)`. Offsets 6 and 15 are
retained. Full labeling publishes signed-16-reduced counts; nominal coherent
maps cannot overflow them. Its return is bit 0 for land overflow and bit 1 for
water overflow. Bit access outside the defined 64-record/64-bit domain may touch
other backing. Out-of-bounds bit writes are excluded bugs, not logical neighbor
set operations. Original read effects require separate supplied evidence;
memory fixtures diagnosing a write do not make its corruption normative.

Temporary region counts have 32768 signed-32 slots. Provisional labels are
unsigned-16. Label allocation requests four bytes per tile, though logical
labels occupy two. Provisional free-ID search stops at 32767; nominal maps
cannot exhaust it. Small-ocean processing writes −1 in selected count slots,
then relabels and clears those slots. High-ID publication stores `−free` in the
old count slot as a remapping. The logical `small`/`remap` structures in section 7
are equivalent: retained positive counts exclude remapped high IDs from future
free-slot searches, and published IDs 0..63 have the same counts.

Tile allocation rounds `6*A` bytes up to a multiple of four; remembered layers
round `A` similarly; each coarse allocation rounds `CW*CH`. Allocation failure
reports −9 and lock failure −10 in category 5; tile allocation identifies W,H,
remembered-layer allocation identifies 234,civ, and coarse allocation CW,grid.
Region temporary lock failure reports −9 in category 8 with selector 1 for
counts or 2 for labels. These describe reference error observations, not a
requirement to use movable handles or duplicate the host allocator.

### Network adapter and constant locations

The tile-setter network gate uses `game.multiplayer > 2`; modes 0..2 do not
enter it. Tile batch operation numbers are features 0, terrain 1, revelation 2, fertility
3, owner 4, region 5 and claimant 6. Corresponding messages are
`NM_FEATURE_SET`, `NM_TERRAIN_SET`, `NM_SEEN_SET`, `NM_SITE_SET`, `NM_OWNER_SET`,
`NM_REGION_SET`, `NM_CITY_USING_SET`. Client destination is host 0 with request
marker 1; host destination is broadcast `0xff` with marker 0. Features and
revelation carry both operand and set/clear selector; the other operations carry
one value. Unused payload positions are zero. N02 supplies their numeric message
codes, wire layout, dispatch and batch framing.

The direction x/y tables start at `00628350/00628360`, radius x/y at
`00628370/006283a0`, diagonal x/y at `0062833c/00628344`, and coarse x/y at
`006283d0/006283e0`. The final zero entries and radius-table order are observable
where callers include them. Adjacent padding is not an additional valid table
entry unless an explicitly backed off-nominal caller requires it.

## Appendix B. Evidence and verification limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

All links below are external reference evidence at the pinned OpenCiv2 revision.

| Contract | Source and original entry |
| --- | --- |
| Bounds, bulk fields, shield | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp): `map_in_bounds` `004087c0`, fill `00408830`, copy `00408903`, shield `0040bcb0`. |
| Wrap and distances | Same file: `map_wrap_x` `005ae052`, horizontal delta `005ae10e`, step distance `005ae1b0`, weighted distance `005ae31d`; [support.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/support.cpp): coarse wrap `005ae0b0`, weighted deltas `005ae296`. |
| Geometric selection | `map.cpp`: span `0047c2f2`, box `0047c37f`, direction `004abea0`, two-position selection `004ad784`, coarse region selection `0055ac37`. |
| Regions | `map.cpp`: set border `004b3110`, rebuild borders `004b315c`, assign regions `004b32fe`, border query `005b9431`, count bordering `005b947f`. |
| Allocation / access | `map.cpp`: allocation `005b7fe0`, release `005b8416`, tile lookup `005b8931`, remembered lookup `005b898b`, coarse accessors `004aee90/004aeec0/004aeef0`. |
| Spatial queries / mutations | `map.cpp`: terrain/owner/region/radius/visibility helpers `005b89bb` through `005b8dec`; resource `005b8ee1`, hut `005b8ffa`; features `005b94d5`; tile setters `005b94fc` through `005b9c49`. |
| State / constants | [civ2types.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h), [map state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/map.h), [data image](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/data/image/data.bin) and [layout](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/data/image/layout.json). |

Independent reconstruction `src/map.cpp`, `src/map_updates.cpp` and the map
headers corroborate one-step wrap, arithmetic reductions, aliasing, radius-table
order, allocation effects and the complete region pass/cleanup sequence.
Strict map-dimension/file validators in reconstructions (including BF-094 and
later tile-field checks) are not proof of original input rejection. F06 must
separate the original reader from those repairs. No such validator has been
added to the W01 primitive contracts.

Validation extracted 44 OpenCiv2 functions into a temporary native harness.
It passed **58,357 assertions** covering wrap/spans, distances, sign directions,
coordinate features across nine seeds and a complete 32×32 coordinate period,
all feature-byte combinations for owner gates, setter reductions and sync gates,
fallback aliasing, two-position selection, small-region maps, cylindrical row
carry, border skipping, overflow count omissions, and the high-water-ID 16/17
threshold. Table values for nine direction positions and 45 radius positions
were compared with both the original image and independent reconstruction.

The harness adapted storage and allocation, widened two temporary-label pointer
casts for the native host, counted pump/synchronization callbacks, and did not
run their UI/network bodies. Compilation used wrapping arithmetic and disabled
strict alias optimizations. Those checks establish the selected recovered
operations, not original executable conformance, allocation-failure coverage,
all corrupt-memory aliases, or complete movement/visibility/gameplay behavior.
Historical reference cases in the [unit inventory](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/tests/unit/cases.tsv)
are additional finite evidence, not tests installed in this repository.

The eight shared bounds/wrap/lookup/coordinate-feature/region bodies extracted
from W02's earlier `1ff87e33fbec8b7be8c4bf98c77709e16aad4f31` pin match the newer
bodies after enum and state-name normalization. Moving their descriptions here
therefore does not silently change that specification's behavioral target.
Whole-game integration and independent executable fixtures remain X02 work;
the calling specifications own their refresh, failure and callback sequences.
