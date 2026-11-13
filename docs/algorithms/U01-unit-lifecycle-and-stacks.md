# U01: Unit lifecycle and stacks

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. Evidence: OpenCiv2
`a71e8aefffe04e6f719e0e213b027b128a747711` and independent reconstruction
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`.

This document specifies creation/deletion, ordered stacks and their repair,
relocation, cargo grouping, home-city mutation, elementary unit visibility and
lifecycle bookkeeping. It distinguishes relocation from legal movement and
basic deletion from combat, transport-loss and civilization-elimination policy.

Required definitions are [F01 arithmetic](F01-determinism-and-arithmetic.md),
[F02 identities and state](F02-game-state-and-invariants.md),
[F03 rule values](F03-rules-and-configuration.md), and
[W01 geometry and map state](W01-map-geometry-and-spatial-state.md).
[F06](F06-save-load-and-file-formats.md) owns serialized lifetimes and the
post-load stack/negative-position cleanup caller. U02–U07 own movement, orders,
transport and combat decisions; [W03](W03-visibility-and-exploration.md) owns
full revelation/contact; C01/C09 and
[V01](V01-civilization-lifecycle-and-civil-war.md) own city/civilization lifecycle. Their invoked boundaries are explicit below.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

Slot/token allocation, stack traversal/repair, cascading removal and child
publication retain their consequences and order. Independent record initialization
can be grouped only before any required observer or failure prefix exposes it.

## 1. State, inputs and nominal domain

Use F02's stable slot arrays, exclusive `unitEnd/cityEnd`, zero/free lifetime
markers, independent next-unit counter, exact coordinate pairs, ordered links,
home-city references and retained free-slot contents. A unit is not a newly
zeroed structure whenever its lifetime changes. Operations can observe previous
contents before overwriting them.

Additional inputs are unit rules (role, domain, movement, hit points, attack,
defense, cost, capacity, range and flags), civilization counters and regional AI
policy, human-player set, current turn/options, tutorial markers, game mode,
network authority, operation activity markers, callback outcomes and tick values.
The cached civilization total used here is separate from the military count and
the per-type active count. Do not silently recompute any of them at each call.

A nominal operation has backed unit/city/rule selectors, owners 0..7, coherent
scan bounds, a finite same-owner stack with reciprocal links and equal positions,
and valid map/rule state. Stack links and positions are signed; normal type/owner
selectors are within their owning domains. Unit roles 0..4 count as military,
role 1 is defense, 5 settlement, 7 trade. Domains are land, air and sea. Rule
changes can assign these meanings to different unit-type slots; the hard-coded
Partisans exception below still uses its designated type slot.

Ordinary on-map positions use W01 doubled coordinates. Lifecycle operations
also admit distinct off-map staging groups. They do not test movement legality,
terrain suitability, transport ownership, diplomatic permission or move cost
unless explicitly stated. Callers must not replace those tests with assumptions
about what a function named "relocate" or "delete safely" would usually do.

There are no direct random draws in these primitives. Revelation, notices,
elimination and network callbacks may invoke other algorithms with their own
specified effects. Preserve call order and re-read live fields where indicated.
Default conformance cases use callbacks that do not alter unrelated state.

## 2. Traversal, lookups and ordered stack queries

`Absent` is −1 for ordinary links. Accessors preserve a negative argument where
specified, rather than normalizing all negatives to −1. Every access to a
nonnegative stack member below requires its backing; a lifetime-zero record is
not automatically rejected. `validate` is the repairing operation in section 3,
not a pure predicate. Accessors ignore its returned 0/1 and continue.

| Operation | Ordered behavior |
| --- | --- |
| `next(s)` | For s<0, return s. Otherwise validate(s), then return its current successor. |
| `first(s)` | For s<0, return s. Otherwise validate(s), then follow predecessors while each is nonnegative and not equal to the current slot. Return the stopping slot. |
| `last(s)` | As first, following successors. |
| `firstForOwnerAt(owner,x,y)` | Scan slots ascending below the live bound; take the first nonzero-lifetime record with the exact owner and exact x,y; return first(found), or −1. |
| `firstAt(x,y)` | If unit-movement animation is active and W01's tile-unit-owner query is negative, return −1. Otherwise scan ascending for the first live record at the exact coordinates, irrespective of owner. If found, validate it, then call first(found), which validates again. No match returns −1. |
| `nth(s,n)` | Start at first(s), with position −1. Increment position for each member. On equality save the result, but still call next on that member as the loop's update before returning. No equality returns −1. |

The lookup comparisons do not wrap or canonicalize coordinates. W01 may alias
two coordinate pairs to a tile; the unit scans still distinguish those pairs.
A stored head is discovered from the lowest matching slot, not necessarily the
lowest slot in that chain. With animation inactive, `firstAt` scans records even
if the tile's unit marker is absent or coordinates are off-map. With animation
active, it first respects the marker/owner query. Its preliminary validation and
`first` validation are two real calls, even when they usually have the same result.

### Whole-stack aggregates

Except for `rawForwardCount`, begin with first(s). Initialize sum=0 and loop
while the slot is nonnegative **and sum<2048**. Add the selected contribution,
then advance with next(s). Rule cost/attack/defense/capacity/range/role use their
signed-byte views. The accumulator follows F01 signed-32 arithmetic.

| Query | Contribution per visited member |
| --- | --- |
| Production cost | Rule cost. |
| Defense | Rule defense. |
| Count | 1. |
| Attack | Rule attack. |
| Reserve defenders | 1 if predecessor is nonnegative and role is defense; otherwise 0. |
| Ships | 1 for sea domain. |
| Spare transport capacity | Sea unit's capacity; −1 for a land unit; 0 for air/other domains. |
| Long-range aircraft | 1 for air domain with signed range>1. |
| Expended attackers | 1 when destroyed-after-attack rule flag is set. |
| Aircraft carriers | 1 when carry-air rule flag is set. |
| Submarines | 1 when submarine rule flag is set. |
| Raw forward count | 1, start at the supplied slot and follow raw successor fields without validation. |

The limit tests the **sum**, not number of iterations. A contribution can carry
the result past 2048; negative or zero contributions do not bound an invalid
cyclic traversal. Reserve-defender count excludes any current head by its
predecessor field, not by a separate first-iteration test. Raw forward count
alone is guaranteed to stop after 2048 visits and is used for cycle detection.
An unrecognized aggregate selector adds nothing but still traverses with the
normal accessors. `countRole(s,r)` separately traverses the complete stack and
counts signed rule-role equality; it has no sum<2048 cutoff.

### Nearest-unit and required city queries

`nearestUnit(x,y,owner,excluded)` scans live unit slots ascending. Negative owner
means unrestricted; otherwise match the owner's low byte against the record's
signed owner. Exclude the specified slot. Initialize result −1 and the published
nearest-unit distance to 9999. Use W01's **weighted distance**, accepting a
candidate whenever distance<=best. Equal distance therefore chooses the
**higher slot**. Bounds, terrain, orders and position validity are not filters.
Retain distance 9999 when no candidate is accepted.

Creation and home reassignment use [C01’s weighted nearest-city query](C01-city-lifecycle-and-ownership.md#weighted-nearest-city)
with no region/port filter. Creation uses **no owner filter**, then checks
ownership of the selected city. Finding the nearest friendly city first would
give a different home when a foreign city is closer or wins a tie. Preserve
the query’s published distance.

City-at-position calls below use [C01’s exact lookup](C01-city-lifecycle-and-ownership.md#exact-city-lookup),
including its tile gate, exact-coordinate comparison and first-slot tie.

## 3. Ordered stack validation and repair

Nominal coherent stacks return 1 without mutation. Damaged but backed finite
fixtures use the actual sequence below. Diagnostics are observable callbacks;
if they return, continue with the described reads/writes. This operation does
not guarantee that every malformed stack becomes valid.

1. If entry slot is outside 0..2049, invoke the original assertion/report
   service. A returning report continues. Read the stack-check suppression
   marker **after** this callback; if nonzero, return 1 immediately.
2. Set result=1. Compute rawForwardCount(entry). If at least 2048, report the
   infinite-stack diagnostic, set result=0, and perform the repair below.
3. Run the four directional passes below in order, each starting again at entry.
   Each pass follows its **saved neighbor** after a cut. It does not stop just
   because it severed the current edge. On each detected problem, issue its
   diagnostic, set result=0, and then perform the writes.
4. Return result. A return of 1 is not proof of liveness, reciprocity, owner
   consistency or termination of an arbitrary future traversal.

Infinite-stack repair is:

```text
current = entry
savedNext = unit[current].next
repeat:
    if current < 0: break
    savedPosition = unit[current].position
    detach(current, sync=false)
    attachAtFront(current, savedPosition, sync=false)
    current = savedNext
    savedNext = unit[current].next       # original performs even for current<0
until current == entry
```

Attach uses ordinary lookup/accessors and can recursively validate another
chain. The routine does not set its own suppression marker around this work.
A self-loop can be repaired; a valid forward chain of exactly 2048 units also
enters this branch. The negative-terminal read is a real reference access even
though its value is discarded on the next iteration. Without supplied backing,
stop after the preceding effects rather than invent a value or claim an orderly
error. The independent reconstruction deliberately skips that access (BF-095).

For the four remaining passes, use these rules; neighbor is captured before the
diagnostic or cut, and reverse-link comparisons use the then-current fields:

| Pass | Continue while | Problem test | Cut |
| --- | --- | --- | --- |
| 1, backward dead link | current.previous>=0 | The **previous member's** lifetime is zero. | current.previous=−1; if previous.next equals current, previous.next=−1. |
| 2, forward dead link | current.next>=0 | The **current member's** lifetime is zero. | current.next=−1; if next.previous equals current, next.previous=−1. |
| 3, backward position | current.previous>=0 | Previous x differs, or otherwise previous y differs, from current. | Same backward cut. |
| 4, forward position | current.next>=0 | Next x differs, or otherwise next y differs, from current. | Same forward cut. |

Advance current to the saved previous/next after every iteration, even when the
edge was cut. In particular, pass 2 does not test the next member's lifetime.
A live unit linked to a dead terminal member can remain linked with result 1.
An otherwise coherent nonreciprocal edge is not repaired simply for lacking its
reverse. Owner mismatch is not a repair condition. Only a self-reference is
explicitly stopped by first/last after validation; other cycles may recurse,
loop or require backing beyond the declared domain.

## 4. Placement and ordering primitives

These descriptions give local/host mutation. Section 9 supplies each operation's
activity marker, synchronization and client-request boundary. No primitive here
implicitly reveals terrain, spends moves, changes home or clears orders.
Coordinates written to records reduce to signed 16-bit values. Destination
lookups and map tests use the original full arguments before/after those stores,
not a silently narrowed replacement argument.

### Detach, attach and relocate

`detach(s)` reads whether the unit has a predecessor; if so, assign that
predecessor's next from s.next. Then read whether s has a successor; if so,
assign that successor's previous from s.previous. Clear s.previous and s.next.
If neither neighbor existed and the old position is in bounds, clear the tile's
unit-present feature. Leave its occupant owner and other features unchanged.
Finally set both coordinates to the owner's detached staging position.

`attachAtFront(s,x,y)` requires a newly initialized or detached unit that is not
already in the destination chain. Find `head=firstForOwnerAt(s.owner,x,y)` before
changing s's coordinates. Set position=(x,y), previous=−1, next=head. If head
exists, set head.previous=s and do not change tile ownership/features. Otherwise,
if the full destination is in bounds, set unit-present and set occupant owner to
s.owner, retaining fertility. Original publication accepts owner values 0..8;
other signed owner values publish the absent-owner value. Nominal owners are 0..7.

`relocate(s,x,y)` calls detach(s,false), then attachAtFront(s,x,y,false), then
its own optional synchronization. `relocateInPlace` first captures the current
position and calls relocate. On a coherent stack this moves s to the front,
including when origin and destination coordinates are equal.

These operations do not protect against attaching an already placed member,
coincident mixed-owner chains, invalid links or mutations during child callbacks.
Preserve the ordered reads in such explicitly backed tests; a generic list
library's stronger invariants or automatic repair can change the outcome.

### Move to bottom

If the entry successor is negative, return without setting the operation marker
or synchronizing. Otherwise enter the authority boundary. On the local path,
recheck that the successor is nonnegative; when it is:

1. Capture position and find the stack head. If head is s, replace the anchor
   with next(s), leaving a member of the remaining chain.
2. Detach s without synchronization; find last(anchor).
3. Set that last member's next=s, s.previous=last, s.next=−1, and restore s's
   captured coordinates.

Perform the optional operation-level synchronization even if the second successor
test became false. The operation changes relative order, not the position or
order of other coherent members.

### Whole-stack relocation

Start at first(s). For each member, capture next(member) **before** relocating
it to the destination without synchronization; then continue at the captured
slot. Finally perform the requested whole-operation synchronization, even for
an empty/negative starting stack. Front insertion reverses the transferred
sequence ahead of any existing destination chain. Moving an entire stack to
its own position can also reverse it; it is not a no-op.

### Put ships first

For s<0 return. Capture s's position and traverse its original stack head to
tail, capturing next before each mutation. Relocate each sea-domain member to
its owner's ship-ordering staging position, remembering the last selected ship.
If any was selected, start at first(lastSelected) in that staging group and
relocate that entire group back to the captured position, again saving each
successor before relocation. Pass the caller's sync flag to each relocation.

On a coherent same-owner stack with an empty staging group, the two reversals
produce a stable partition: ships first in their original order, then other
members in their original order. Existing units at the same staging position
can join the restored group. A corrupted mixed-owner input can leave some ships
in other staging groups, since only the last selected ship's group is restored.
A wrapper performs this reorder then returns first(original s).

## 5. Creation

Creation returns the allocated slot, or −1 for an ordinary local capacity refusal.
A client returns the host's result as described in section 9. There is no initial
type/owner/bounds validator in the recovered original entry.

On the authoritative path:

1. Reserve the first free slot or extend `unitEnd` using
   [F02's exact slot-selection rules](F02-game-state-and-invariants.md#slot-selection).
   A refusal shows the too-many-units notice only for an effective human owner
   in a mode below network play. Clear the operation marker and return −1.
   No counters, ID or record fields are initialized on this return.
2. If the requested type's signed role is <5, increment the owner's military
   count with signed-16 reduction. Increment its active count for that type
   modulo 256 and its cached unit total modulo 2^32.
3. Store new type, owner and next lifetime, in that order; increment the next
   lifetime modulo 2^32 without skipping zero.
4. In local/nonsimultaneous play set movement spent to zero. In simultaneous
   network play compute the movement allowance for this newly typed/owned
   record **while its old damage is still present**, then store the allowance's
   low byte as movement spent. Use the allowance contract below.
5. Clear damage; set AI task to its neutral task value; clear flags and unit
   visibility; set orders to none and home to absent.
6. Query the nearest city with no owner restriction. If the requested owner is
   nonbarbarian and the selected city has that owner, assign its slot as home.
   Otherwise leave no home. Do not search again for a farther friendly city.
7. Clear work/commodity counter and auxiliary timer; set last direction absent;
   clear both links; set x,y and both destination fields to −1. Original
   reserved bytes remain unchanged.
8. Attach at the requested position without synchronization, then invoke
   `revealSurroundings(created, allowContact=1)`. [W03's surroundings
   operation](W03-visibility-and-exploration.md#3-unit-surroundings-entry-and-tile-scan)
   owns its exact visibility, contact, order/goal and redraw effects. Initialization
   has already happened if this child fails or mutates the unit.
9. Process creation tutorials below. Then, if the **live** mode is network,
   broadcast state differences and flush with argument 5000. Clear the activity
   marker and return the originally selected slot.

The returned slot need not remain alive if a callback destroys it. A zero
lifetime assignment after counter wrap still proceeds through placement and
revelation; it is not changed to a capacity error. Creation does not automatically
assign veterancy, commodity, home-support charges, a settler task or movement
legality. Its callers own any subsequent initialization.

### Movement allowance required at creation

Use [U02's allowance contract](U02-movement-and-zones-of-control.md#allowance-movement-left-and-exhaustion),
including its ordered technology/wonder queries and damaged-unit rounding. The
creation call remains at step 4, while retained slot damage is still present;
only its result is reduced modulo 256 for movement spent, before step 5 clears
damage. Moving that clear earlier changes the created unit.

For example, base movement 6, maximum HP 10, retained damage 5 and r=3 gives an
allowance of 3; after creation spent movement is 3 but damage is zero. Zeroing
the whole slot first gives 6. This timing example is part of creation; the
shared formula and off-nominal arithmetic are owned by U02.

### Creation notices

After revelation, use the original type/owner arguments but live rules, turn,
human mask and options. Only turn!=0, human owner, and mode exactly zero enter:

- Sea domain: if the ships marker is clear, optionally show the ships tutorial
  when tutorial mode is enabled; then set the marker even if no dialog was shown.
- Otherwise air domain: same procedure for the aircraft marker.
- Otherwise trade role: same procedure for the caravan marker.
- Otherwise, when tutorial mode is enabled and signed role<5, show first-unit
  notice if the live military count is 1, then independently show second-unit
  notice if its then-live value is 2. A first notice callback can make both fire.

Marker writes OR into the live marker word after the popup; do not restore an
entry snapshot. There is no single shared "first unit" marker for these paths.

## 6. Deletion and its bookkeeping

### Basic deletion

Negative slot or lifetime-zero record returns immediately, without activity
marker, UI or network request. No general positive-index check precedes the
lifetime read. On the authoritative path:

1. Capture old position, type, owner and decoded home (absent or unsigned city
   slot). If owner>=0 and slot<2048, decrement the owner's military count only
   when nonzero and the live type's signed role<5; decrement its active-type
   count only when nonzero; decrement the cached unit total only when nonzero.
   Reductions use each counter's normal width, not a new saturation policy.
2. Detach without synchronization. Set lifetime zero. If this slot is exactly
   `unitEnd-1`, decrement the bound once; do not trim earlier holes.
3. Scan all slots below the live resulting bound, including lifetime-zero
   records. Whenever orders are sleep and stored carrier slot equals the deleted
   slot, set orders to none. Preserve the stored carrier/destination value and
   aboard flag. This wakes references; it does not delete them.
4. If notification was requested, notify the city window of a removed unit at
   the captured position, then refresh the captured home city when present,
   including city slot 0.
5. Using the captured type/owner but live rule/civilization records, if the
   type has settlement role, owner has zero cities and the active count for
   **that exact type** is zero, call the civilization-elimination check with
   owner and absent killer. It runs even when notifications were suppressed.
   V01 owns its additional checks/cascades; no elimination is assumed here
   merely because this call occurred.
6. If live mode is network and notifications were requested, broadcast state
   differences, then a city-window unit-change notice for the captured position.
   Send a home-city refresh notice only when captured home is **strictly >0**;
   unlike the local refresh, this excludes city slot 0. Flush with 5000.
7. Clear the operation marker.

Do not increment the per-type lost-unit statistic here; combat and other callers
own their loss accounting. Orders/damage/destinations and reserved fields survive
unless another step explicitly changes them. Captured owner/type/home/position
stay captured across detach/UI callbacks; rule/counter values used later are
live. Activity markers clear only on normal completion, not via invented
exception unwinding.

### Delete a stack

Start at first(s). Repeatedly capture next(member), call basic deletion with
notifications false, and continue at that captured next. At the end perform the
requested network state sync and flush. It invokes per-unit elimination checks
but no per-unit city-window notifications. Those checks may themselves cascade.

The pool bound depends on deletion order: deleting head-to-tail `[0,1,2]` from
bound 3 leaves bound 2, even though all three slots are free. Only deleting slot
2 decremented the bound. Do not compact the array or substitute a bulk clear.

### Cargo-aware deletion

This entry has no negative/free-slot early guard. On the authoritative path,
capture the unit's position. For a sea-domain unit:

- In bounds, treat an ocean tile as at sea.
- Out of bounds, if x equals the owner's lifted staging x, delete the whole
  stack immediately and mark the operation already handled. **Y is not compared.**
- Otherwise, an out-of-bounds sea unit is also treated as at sea.

If at sea, run cargo selection in **mark mode**, then delete the **entire physical
stack**, and issue one city-window unit-change notice for the captured position.
The selected aboard flags do not filter deletion. Other ships and unselected
units in that chain are removed too. If not at sea and not already handled,
perform basic deletion of only the requested unit, notifications false. Finally
perform the requested network state sync/flush and clear the marker.

Thus a sea unit in a land city loses only itself; a ship in the lifted staging
group loses its staged stack without the at-sea city-window notice. Calling this
operation on a non-sea unit deletes only that unit, even if it shares a stack
with a ship. U05 chooses when transport loss invokes this operation;
[U06 casualty processing](U06-combat-and-veterancy.md#recording-one-casualty)
adds combat history/events before basic deletion and defines its own stack-loss
policy. It does not substitute cargo-aware removal for every combat death.

`destroyAndRedraw(s,sync)` captures position, calls cargo-aware deletion with
sync false, redraws that tile in all views if it is in bounds, then, when network
mode and sync are enabled, sends state differences followed by a draw-square
message for the captured position. This tail has **no explicit flush**. It
clears its marker after callbacks; a client follows its own request path.

## 7. Cargo selection and lifted groups

This section owns the shared grouping primitive used by movement and deletion.
[U05](U05-transport-and-special-movement.md#transport-operations) owns boarding,
unloading, landing, fuel and loss decisions. U02 owns the movement admission
checks. Those callers use this grouping operation at their stated positions;
its scan/priority rules are shared rather than a new cargo model per caller.

`gatherCargo(ship,lift,sync)` returns **remaining capacity**, not cargo count.
It reports an assertion for ship<-1 or ship>2049 before entering the activity/
authority boundary. A returning report continues. Although −1 passes that test,
the local path subsequently accesses its record; it is not a successful no-op.

### Preparation

1. If lift is true, put ships first without synchronization.
2. Set `start=first(ship)`. If start is ship, replace start with next(ship).
   This skips ship only when it was head; there is no separate universal
   self/owner exclusion in the candidate scan.
3. Capture ship x,y. Record whether they are in bounds and, only if in bounds,
   whether the tile is ocean. These values remain fixed during the selection.
4. For mark mode (lift=false), walk from start and clear each member's aboard
   flag. If ship was not head this can include ship itself. For lift mode,
   instead relocate ship to its owner's lifted staging position without sync.
5. Read signed capacity from the ship's then-current type. Determine carrier
   from carry-air. Determine missile-submarine only when capacity is exactly
   zero, carrier is false and submarine flag is set. Carrier or missile-submarine
   capacity becomes 20. Barbarian-owned ship capacity also becomes 20, without
   changing its ordinary/carrier selection category.

### Candidate passes

Carrier/missile-submarine uses one pass; ordinary transport uses two. Stop when
all passes finish or remaining capacity is **exactly zero**, not when it is
negative. Each pass starts at the current `start`. For every candidate, obtain
next(candidate) before deciding or mutating it.

Carrier/missile-submarine candidates must be air domain; ordinary candidates
must be land domain. A missile-submarine additionally requires the candidate's
destroyed-after-attack flag; failing this test skips the rest of that iteration.
The rule does not test the candidate's owner against the ship's owner.

| Pass/category | Eligibility after the domain test |
| --- | --- |
| Carrier or missile-submarine, pass 0 | All matching candidates. |
| Ordinary transport, pass 0 | Sleeping, with stored carrier slot equal to ship. |
| Ordinary, pass 1, nonhuman owner | Not fortifying/fortified, or captured location is ocean; then, on an in-bounds non-ocean tile, require that owner's regional AI policy is not 1, or signed unit role>4. Off-map/ocean needs no regional-policy test. |
| Ordinary, pass 1, human owner, sleeping | Stored carrier slot is negative. |
| Ordinary, pass 1, human owner, not sleeping | Captured location is ocean. |

Human/nonhuman classification uses each candidate owner's bit in the live human
set. The regional policy is indexed by the candidate's owner and the captured
ship tile's raw region. Do not replace it with the ship's owner or a nearest-city
policy. A human sleeper assigned to another ship does not become eligible merely
because the tile is ocean.

In mark mode, a candidate whose aboard flag is already set is ineligible after
these tests; this avoids charging a unit selected in the first pass again in the
second. Lift mode instead removes selected members from their original chain.
On selection:

1. If candidate==start, set start to its previously captured successor.
2. For carrier/missile-submarine, preserve sleep orders; any other order becomes
   none. Do not assign a carrier reference. For ordinary transport, perform
   `setSleep(candidate)`, then assign its carrier slot to ship.
3. In mark mode set aboard. In lift mode relocate the candidate to **its own
   owner's** lifted staging position without synchronization; do not set aboard.
4. Decrement remaining capacity and continue at the captured successor.

`setSleep(s)` clears the first destination/carrier field to absent only when
orders were not already sleep, then sets orders to sleep. It retains all other
fields. The explicit carrier assignment above follows this operation.

After passes, perform requested network state sync/flush, clear the activity
marker and return remaining capacity. There is no HP/movement cost, terrain
legality or capacity clamp added by this helper. A negative signed capacity
continues decreasing on selections; this is deterministic off-nominal behavior
for a finite candidate chain.

### Lifting and multiple ships

`liftUnit(s,sync)` is a no-op for negative s. Otherwise, on the authoritative
path, a sea unit invokes gatherCargo(s,true,false); a non-sea unit relocates
only itself to its owner's lifted staging position. Then perform the requested
network sync/flush and clear its activity marker. Captured ship/cargo order is
observable: selected cargo is inserted before the lifted ship in reverse
selection order. Existing same-owner staged units can join the group.

The stack-wide cargo query first puts ships first without sync, initializes
maximumRemaining=0, then walks the resulting stack. For each sea unit, run
mark-mode gatherCargo without sync and retain the greater returned capacity.
Read the next member **after** that call. Finally perform the requested network
sync/flush and return the maximum, not the sum. Each gather call performs its
own aboard-flag clearing and can change earlier assignments. There is no added
stack-wide reservation transaction. Its original entry is documented in the
independent reconstruction even though OpenCiv2's main auto file omits this body.

## 8. Home-city links and supported-unit removal

Home is an affiliation, not proof of ownership or a source of generic automatic
support updates. Writing a new home does not renew the unit's lifetime, move it,
adjust active-type totals or recalculate every affected city. Callers own the
specified refreshes and later support calculation.

### Display and explicit home change

For home absent, the home-name query returns the localized no-home label;
otherwise it selects that city slot's stored name. It does not validate the
city lifetime/owner before looking up the name. A byte with its high bit set
still denotes city 128..254; only the all-ones home value is absent.

The Home City command stops unit blinking and uses the current active unit.
If no city exists at its position, dispatch the return-home movement/order
operation (U04/U05), not a home-affiliation change. If there is a city and the
unit has trade role with an existing home, show the caravan-home refusal and
leave affiliation unchanged. Otherwise capture the old raw home value, assign
the current city, refresh the old home using that unsigned value (including
255 for absent), then refresh the new city. The command does not add an owner
check to city-at-position. P05 owns how a city-window refresh ignores a nonmatching
or absent displayed city.

### Rehoming during fortification

After the fortification operation has set orders to fortified, the AI-only
home adjustment runs when the unit is nonhuman and its stored home is in city
slots **0..127**. This is the original signed-byte nonnegative test; a valid
home in 128..254 does not enter. The later absent-home decoding inside this
branch does not broaden that entry gate.

- If a city exists at the unit's position and size>2, select it as new home.
- Otherwise, when no city exists and the tile is a fortress without the city
  component, select the nearest city of the unit's owner with no region/port
  filter; use it only when size>2.
- On selection, decode/capture old home, assign new home, refresh old home if
  present, then refresh new home. The on-tile city path does not filter owner.

[U04](U04-unit-orders-and-activation.md#apply-fortification) owns the fortification
trigger, blink change, final tile redraw and unit-change notification. This helper performs no additional population, support or production
calculation. W01's feature classification distinguishes the fortress test from
an airbase whose city component is also set.

### Units supported by a lost city

City deletion and city capture share the following per-candidate policy, with
explicitly different selection filters:

| Caller | Candidate |
| --- | --- |
| Delete city | Live unit whose decoded home equals the deleted city, irrespective of unit owner. |
| Capture city | Live unit with that home **and the captured old owner**. |

Scan slots descending, starting at the current `unitEnd-1`. For each candidate:

1. If its type is the designated Partisans slot, clear home and continue.
2. Otherwise, for a nonhuman unit owner, find its on-tile city. If it exists
   and differs from the lost city, test whether this unit has defense role and
   the stack has exactly one defense-role member. If both, assign that on-tile
   city as home and retain the unit. No city-owner check is added here.
3. If that other city exists but the defense condition fails, set its
   defender-lost flag before proceeding. There is no such flag write for a
   human-owned candidate or a candidate outside another city.
4. Destroy-and-redraw the candidate, with synchronization requested. Restart
   the descending scan from the **new live bound**, since cargo or civilization
   callbacks can have removed other units. Rehomed or Partisans candidates
   continue descending without restarting.

C01 owns decrementing the city's owner's city count before its supported-unit
scan and the subsequent city/map cleanup. C09 owns the capture caller's phase,
old-owner selection, new city's production/disorder and later aftermath. This
section owns the unit-selection/removal rule only. A city's disappearance does
not mean "give every supported unit the closest surviving home."

Direct ownership transfers from bribery, revolt/civil war, gifts or scenario
editing have different counter/order/home effects and remain V06, C09/V01 and
S02/S04 contracts. Do not introduce one automatic ownership setter that performs
all possible side effects for every caller. U01 creation/deletion account for
new/dead lifetimes; these transfers generally retain the existing lifetime.

## 9. Elementary visibility and network boundaries

### Unit visibility sets

These operations affect the unit's seen-by set, not tile exploration, remembered
terrain, current line of sight or city knowledge. [W03](W03-visibility-and-exploration.md)
owns when to invoke them.
They do not test unit lifetime or emit network synchronization themselves.

- `clearSeen(s)` does nothing for negative s; otherwise clear its seen-by set.
- `markSeen(s,c)` first requires c>=0, then reads whether the unit owner differs
  from c, then tests s>=0; if all pass, add c. Because owner is read before the
  slot guard, a nonnegative c with a negative s needs original backing or a stop.
- Whole-stack clear/mark starts at first(s) and advances with next after each
  mutation. Marking never adds the owner's own bit through `markSeen`.
- Whole-stack union ORs the supplied low-eight-bit set into every member, with
  no owner exclusion. Existing owner bits are retained by all OR operations.

Outside the nominal civilization range, the original single-civ bit uses an
x86-masked shift count then reduces to eight bits; for example c=8 adds no bit,
while c=32 can add bit 0 if the owner comparison passes. It does not validate
civ membership before this arithmetic. F01 owns the shift convention.

### Authority and operation activity

For the independently gated operations in the table below, mode below 3 is
local. In mode>=3, the host executes the
local body; a nonhost requests it remotely. A false sync/notify argument does
**not** authorize a client to perform the local mutation. The operation activity
marker is set at entry to the described boundary, before testing authority,
and cleared on normal local completion. It is a boolean assignment, not a
nesting counter or lock. The guards listed earlier occur before this boundary.

| Primitive | Extra local tail in network mode |
| --- | --- |
| Detach, attach, relocate, bottom | If sync: broadcast state diff, flush(5000). |
| Create | Always broadcast state diff, flush(5000) after creation/notices. |
| Basic delete | If notify: state diff, unit-change notice, home notice only for home>0, flush(5000). |
| Cargo gather, lift, cargo-aware delete | If sync: state diff, flush(5000). |
| Destroy-and-redraw | If sync: state diff, draw-square message; no explicit flush. |

Whole-stack relocate/delete and stack-wide cargo selection have no single
outer client-request/activity gate: they invoke their constituent operations,
then run their optional sync/flush tail. Ship-first ordering passes sync to
each constituent relocation. Consequently client calls can issue multiple
requests even when internal child sync arguments are false. Preserve these
boundaries instead of turning every composite operation into one new packet.

At a local tail, test the live mode and sync/notify gate after preceding callbacks.
Once a tail's condition was taken, run its specified calls in order, including
flush after a sync callback even if that callback changes mode. Clear the marker
only after those calls return. No rollback is implied by a failure/stop.

### Client request, wait and result

For each independently gated primitive, the nonhost branch does:

```text
clear its activity marker
set its distinct reply/result to Pending
send its operation request to host, with the arguments below
start = ticks()                         # even if sending resolved the reply
while reply == Pending:
    now = ticks()
    if signed32(now-start) >= 3600: break
    processMessages(1,1)
if reply == Pending:
    log timeout
    show server-connection-timeout notice
    set main turn loop inactive          # after notice returns
while either priority or normal queue has messages:
    processMessages(1,0)
return current reply for create/cargo, otherwise return void
```

Ticks and signed subtraction follow F01; 3600 is a tick interval, not 3600 ms.
A late queue-drain callback can resolve/change the result after the timeout
notice. Timeout does not undo already received state, add a new failure return,
or repeat the request. No final activity clear follows the client callbacks;
its earlier clear can itself have been changed by reentrant work.

| Operation request | Nonzero/meaningful payload fields in order |
| --- | --- |
| Pick up | unit |
| Put down | unit, x, y |
| Relocate | unit, x, y |
| Move to bottom | unit |
| Create | type, owner, x, y |
| Delete | unit |
| Gather cargo | ship, lift mode |
| Lift unit | unit |
| Delete with cargo | unit |
| Destroy and redraw | unit |

Other payload fields are zero. Client sync/notify arguments are not forwarded
as extra fields. N02/N03 own the wire encodings, response dispatch, state-diff
application, message queues and authority transitions. This table specifies the
calls/results the lifecycle algorithm requires; it does not assert that a
request completed merely because the send call returned. Session establishment
is N01, and hotseat mask/handoff behavior is N04.

## 10. Off-nominal and failure behavior

The intended inputs in section 1 do not imply original defensive validation.
Keep deterministic reductions and differences from stricter policies explicit:

| Case | Required classic behavior or boundary |
| --- | --- |
| Free interior slot when extension limits are exceeded | Reuse it; extension limits do not apply to holes. |
| Military count 1948, then 1949 | Extension allowed at 1948, refused above it; no fresh census before the test. |
| Next lifetime wraps to zero | Creation/renewal does not skip zero; later occupancy scans treat it as free. |
| Active-type count 255 on create | Wrap to 0. Delete decrements only when nonzero, so creation/deletion is not an unconditional inverse under wrap. |
| Negative recorded military/total count | Nonzero-delete guards still permit decrement; they do not clamp a corrupt negative count to zero. |
| Valid home slot 128..254 | Unsigned home lookup/deletion refresh works; the fortification gate nevertheless skips it. |
| Dead terminal next member | The forward-dead pass tests current, so the edge can remain and validation can return 1. |
| Nonreciprocal same-position live edge | No general reciprocity repair is performed. |
| Off-map stacks with equal staging position | They are ordinary coordinate groups for lookup/linking and can merge. |
| Sea unit with lifted-stage x but another off-map y | Cargo-aware deletion takes the already-lifted whole-stack branch. |
| Negative cargo capacity | Loop stops only at zero; eligible selections decrease it further. |
| Counter or rule-scalar overflow | Preserve F01's significant reductions and signed views at the specified boundary; wider or arbitrary-precision storage is permitted. |
| Out-of-range position supplied to attach | Store signed-16 coordinates but perform destination search/map test with full arguments; no implicit canonicalization. |
| Unit/rule/owner/link outside its logical domain | Reads need supplied original backing or a stop after established effects. Out-of-bounds writes are excluded bugs under X01; prevent the invalid write rather than reproducing corruption of neighboring records. |
| Interrupted callback/fatal report | Retain preceding record, tile, counter, activity and network effects. No automatic transactional rollback. |

Creation-input restrictions in independent BF-093 and native host array bounds
are not original unit-creation rejection rules. Stack-repair BF-095 removes an
otherwise unused negative-terminal read. Other host adapters can turn corrupt
backing accesses into exceptions. Neither behavior proves the original returned
a clean error for the same input. A new implementation may choose a restrictive
input policy or a safe compatibility stop, but should state the admitted domain.

The aggregate's sum cutoff is not a universal cycle guard. Repair itself can
re-enter traversal. Unbacked links, cycles that do not reach the expected repair
termination, invalid rule indices and counter aliases are not assigned invented
results here. Recorded diagnostics and all prior effects remain observable.

## 11. Worked conformance cases

Each case supplies ordinary rules/map/backing and inert callbacks unless a
specific callback is listed. Node names below are unit slots, not lifetimes.
Assert required dependencies, changed fields, scan bounds, callback sequence, message
sequence and no random decisions or context changes at the selected boundary.

| Initial state and operation | Expected effect |
| --- | --- |
| Stack `[4,2,7]`; relocate unit 2 to the same tile. | `[2,4,7]`; no move spent, home or visibility change by relocation itself. |
| Stack `[4,2,7]`; move 4 to bottom. | `[2,7,4]`; stored positions unchanged. Moving 7 when already last is an early no-op. |
| Stack `[4,2,7]` moved to a tile with `[8,9]`. | `[7,2,4,8,9]`; origin unit marker clears if empty, origin owner remains. |
| Stack `[a,b,c,d]`, b/d sea, empty ship staging group; ships-first. | `[b,d,a,c]`, preserving relative order within each category. |
| Singleton owned by 1, tile features unit+road, owner=1 with fertility 7; detach. | Unit staging position for owner 1, both links absent; tile road remains, unit feature clears, owner/fertility unchanged. |
| Unit 0 live points to unit 1 dead; unit 1 is terminal, same position, reciprocal link. Validate either. | Return 1 with the edge intact; this operation does not prove liveness. |
| Chain `[0,1,2]`, unit 1 dead; validate 0. | Cut 1→2 and reciprocal 2.previous; preserve 0→1. Return 0. |
| Same chain, unit 0 dead; validate 1. | Cut 1.previous and reciprocal 0.next. Return 0. |
| Chain `[0,1,2]`, only unit 1 has another position; validate 0. | Cut both edges around unit 1 while continuing through saved neighbors. Return 0. |
| Live same-position chain 0→1 with 1.previous absent. | Validation returns 1; no reciprocity repair. |
| One live unit with both links to itself, otherwise valid. | Raw count reaches 2048, repair detaches/reattaches once, both links become absent, return 0. |
| Empty/free slot with retained damage 5; network simultaneous creation, base movement 6, HP 10, r=3. | New spent movement 3, new damage 0. Reserved bytes retain their old values. |
| Closest own city slot 0 and foreign slot 1 at equal weighted distance from creation. | Slot 1 wins nearest query; new unit has no home. |
| Reuse slot 7 at bound 2048 with military count 1950. | Reuse succeeds, bound unchanged; extension-limit checks are bypassed. |
| Nonhuman owner, bound 2040, no holes. | Refuse extension and return −1. No local human limit popup. |
| Single player, nonzero turn, human creates first sea unit, tutorial disabled. | No tutorial dialog, but ships-seen marker is set. |
| First military-unit notice callback changes military count from 1 to 2. | The independent second test also shows the second-unit notice. |
| Delete carrier slot 2; free slot 1 still stores sleep and carrier 2. | Slot 1 orders become none; its stored carrier value remains 2. |
| Delete slots `[0,1,2]` head-to-tail, initial bound 3. | All lifetimes zero, final bound 2. No repeated tail-hole trimming. |
| Delete notifying unit with home city 0 in network host mode. | Local refresh includes city 0; network home-city refresh message is omitted. |
| Delete last type-A settler, zero cities, but type-B settlers remain. | Invoke elimination check because exact type-A active count is zero; V01 decides the civilization outcome. |
| Human transport capacity 2; stack `[ship,unassignedSleeper,boundToShip,boundToOther]` on land. | Bound-to-ship selected first, unassigned sleeper second; both aboard, remaining=0, other sleeper untouched. |
| Lift same-owner ship with two unassigned sleepers, capacity 2. | Staged chain `[secondSleeper,firstSleeper,ship]`; land sleepers bound to ship; lift mode does not set aboard. |
| Human sleeping candidate bound elsewhere on ocean. | Not selected in pass 1. An otherwise identical nonsleeping human land unit is eligible. |
| AI fortified land candidate on shore, regional policy not 1. | Ineligible while fortified; on ocean eligible. Unfortified shore candidate with policy 1 needs role>4. |
| Zero-capacity submarine without carry-air; one missile-flagged air unit and one ordinary air unit. | Capacity starts at 20; only missile selected; remaining=19. |
| Sea ship capacity 1 on ocean, stack has two other land units. Cargo-aware delete. | All three units deleted, even though at most one was selected aboard; one city-window unit-change notice follows. |
| Same physical stack on land. | Only ship deleted; other units remain, assigned sleepers are awakened by basic deletion. |
| Supported Partisans when home city is lost. | Home cleared; unit retained. A lone AI defense-role garrison in another city is rehomed there instead. |
| Mark unit seen by its owner, then union a set containing that owner. | Mark adds nothing; union adds the supplied owner bit. |
| Network client relocation, sync=false; host reply arrives on first pump. | Send relocation request, wait/pump, no local relocation in the requesting body. |
| Network client deletion times out, no received state. | Timeout log/notice, then main turn loop inactive; original unit stays live. |

The malformed cases deliberately exercise finite backed states. They are not a
claim that every arbitrary link graph terminates or that validation creates a
canonical stack independent of its starting member.

## Appendix A. Reference encodings and operation mapping

This appendix maps logical state to reference snapshots and messages. Internal
representation is unconstrained; preserve the observable reductions and grouping.
F02 owns the complete unit record and flag mapping; F06 owns its external file
encoding. The original has ordinary unit capacity 2048 and additional special
backing; a validator's 0..2049 assertion range is not an allocation limit.

| Logical value | Reference encoding |
| --- | --- |
| Detached position for owner c | Both coordinates `-100*(signedOwner+1)`. |
| Lifted ship/cargo position | Both coordinates `-200*(signedOwner+1)`. |
| Ship-ordering position | Both coordinates `-300*(signedOwner+1)`. |
| Position/link store | Signed-16 reduction. |
| Creation's neutral AI task | Byte `58` hexadecimal (`X`). |
| Absent last direction/home | Byte `ff`; home otherwise decoded unsigned. |
| Military count / active-type count / cached total | Signed 16 / unsigned 8 / signed-32 wrapping storage. |
| Cached civilization unit totals | Eight signed-32 values at `0064b9e8`. |
| Creation-retained bytes | Unit offset `11`, word `1e`; other old fields can be read before their initialization. |
| Partisans exception | Unit type 9, irrespective of that row's edited name/role. |
| Fortification home gate | Signed view of home byte >=0, so only 0..127. |
| Tutorial markers | Aircraft `0002`, ships `0004`, caravan `0010`. |
| Aggregate selectors | Cost 0, defense 1, count 2, attack 3, reserve defenders 4, ships 5, spare holds 6, long-range air 7, expended attackers 8, carrier 9, submarine 10, raw forward count 11. |
| Pending network result | Signed-32 −2. Timeout subtraction is signed-32 after wrap. |

The original operation activity fields and reply counters are separate per
primitive. Requests target host 0; synchronization/notifications target broadcast
`ff`. State-diff calls here use argument tuple `(ff,2,0,0,0)`. The numeric message
mapping is for validation, pending N02's complete wire contract:

| Request | Request code | Reply code |
| --- | ---: | ---: |
| Delete unit | 55 | 56 |
| Create unit | 61 | 62 |
| Pick up | 63 | 64 |
| Put down | 65 | 66 |
| Move to bottom | 67 | 68 |
| Relocate | 69 | 70 |
| Gather cargo | 73 | 74 |
| Lift unit | 75 | 76 |
| Delete with cargo | 77 | 78 |
| Destroy and redraw | 79 | 80 |

Additional broadcasts are draw square 114, city-window unit change 135 and
city-window city refresh 136. Numeric values are decimal in these tables.
Payload fields and their order are specified in section 9; unused fields are
zero. These numbers do not require a new implementation to organize its local
methods like the original message dispatcher.

## Appendix B. Evidence, checks and remaining integration

| Contract | External evidence and original entry |
| --- | --- |
| Stack validation/accessors | [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp): validation `005b2590`, next `005b2c82`, last `005b2cc3`, first `005b2d39`, owner lookup `005b2daf`, nth `005b2f92`. |
| Tile lookup and distances | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp): `005b2e69`, W01's distance functions. |
| Placement/order | `unit.cpp`: unlink `005b319e`, link `005b345f`, relocate `005b36df`, in place `005b3863`, bottom `005b389f`, stack relocation `005b3ae0`, ship ordering `005b3b78`. |
| Creation/deletion | `unit.cpp`: create `005b3d06`, delete `005b4391`, stack delete `005b47fa`, cargo delete `005b5d93`, visible delete `005b6042`. |
| Cargo | `unit.cpp`: sleep `005b2f50`, gather `005b542e` and extracted eligibility/client helpers, lift `005b5bab`. |
| Visibility/query | `unit.cpp`: clear `005b488a`, stack clear `005b48b1`, mark `005b490e`, stack mark `005b496e`, union `005b4ee2`, aggregates `005b50ad`, role count `005b53b6`, nearest `005b67af`, home name `005b6898`. |
| Allowance dependency | `unit.cpp`: max HP `005b29aa`, remaining HP `005b29d7`, max movement `005b2a39`. |
| Home mutation / supported units | `unit.cpp`: fortification `004c4ada`, Home City command; [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp): city lookup `0043cf76`, nearest `0043d07a`, delete `004413d1`, capture aftermath `0057b5df`. |
| State and encoded names | [civ2types.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h), [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h), [network state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/network.h). |

The pinned independent reconstruction corroborates these contracts in
`src/units.cpp`, `src/unit_creation.cpp`, `src/unit_deletion.cpp`,
`src/unit_stack_cargo.cpp`, `src/unit_reorder_top.cpp`, `src/city_capture.cpp`
and `include/civ2/units.hpp`. It also identifies wrapper `005b3cd4`
(reorder-and-return-head) and `005b620a` (stack-wide remaining cargo capacity),
which are not standalone bodies in the inspected OpenCiv2 auto source.
The reference repository and revision are identified in
[source provenance](README.md#source-provenance).

Independent BF-093 restricts creation inputs and BF-095 skips the original
negative-terminal read. Those are named deviations, not inferred original
validation. The independent capture source's nearby comment calling type 9 a
caravan is inconsistent with the tested slot and enum; the **type-9 test** is
the contract, irrespective of that comment. Neither reconstruction's exception
or array bound proves a safe original outcome for arbitrary corrupted records.

### Checks performed

A temporary native harness extracted **35 OpenCiv2 routines** and passed
**8,455 assertions**. It checked all stack permutations through five members,
every selected member's move-to-front/bottom, whole-stack reversal, every
sea/non-sea partition of those permutations, selected asymmetric repair cases
and a self-loop, plus creation limits/order/wrap, home tie selection, tutorial
callback effects, deletion bookkeeping/free-slot wakeups, cargo priorities,
AI/human/shore/ocean gates, staged grouping, full-stack ship loss, visibility,
nearest-unit ties, animation-gated lookup, and client reply/timeout behavior.

The harness used explicit map/rule/state adapters and stubbed revelation,
elimination, UI and network services, preserving/counting their call boundaries.
It used a separate sequence model for coherent stack-order expectations and
explicit expected states for repair/lifecycle/cargo cases. Compilation used
wrapping arithmetic and disabled strict-alias optimization. Native pointer and
failure-return adaptations retained the selected scalar contract; the harness
was not an original executable run. The actual tests and reference code remain
outside this cleanroom repository.

Not exercised: full 2048-member recursive repair with original negative backing,
arbitrary graph nontermination, all callback aliases, fortification and lost-city
rehoming execution, the two independent-only wrappers, real reveal/contact or
civilization-elimination children, complete network exchange, and full save/load
or combat integration. Source contracts and limits above remain explicit; the
harness is not evidence for broader whole-game conformance.

### Integration obligations

- W03 now supplies `revealSurroundings(unit,allowContact=1)` after creation,
  using these unit visibility primitives independently of exploration. The
  lifecycle harness above still stubs this child; joined creation/revelation
  validation remains required before claiming complete lifecycle conformance.
- U02 owns the shared allowance formula, with the retained-damage creation
  call linked above. Joined movement/cargo/revelation fixtures still need the
  real U01/W03 operations. U04 specifies the sleep/home/fortification
  invocation boundaries; its controlled home/revelation children are not full
  U01/W03 integration evidence.
- U05 specifies boarding, unloading, landfall and transport loss using these
  primitives; complete U02/U05 dispatcher and callback cases remain. U06 now
  specifies combat casualties/events and exercises basic/stack removal with
  these primitives. [U07](U07-nuclear-attacks.md#appendix-b-evidence-and-validation-limits)
  now exercises nuclear whole-stack removal with these primitives. Actual
  complete elimination/scenario/city callers and live session joins remain.
- C01/C09 must place supported-unit rehoming/removal at the correct city phase,
  and [V01](V01-civilization-lifecycle-and-civil-war.md) specifies elimination
  callbacks, cascades and restarts. Its finite actual lifecycle joins do not
  establish every enclosing movement/combat/capture caller. Forced retirement
  uses V01's separate inline removal, not this ordinary deletion.
- F05's created starting units and F06's loaded-unit cleanup need integrated
  cases with these real children. N01–N04 must validate requests, acknowledgments,
  state diffs, host/callback changes and hotseat masks; P05 owns city-window effects.
- X02 must add original-program fixtures and distinguish finite backed failure
  observations from a claim of exhaustive malformed-state compatibility.
