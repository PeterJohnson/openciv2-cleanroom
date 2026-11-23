# U04: Unit orders and activation

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in U04 |
| --- | --- |
| Gameplay | Activation/next-unit selection, order effects, movement continuation, recovery and automation decisions; keep gameplay activation distinct from visual focus. |
| Original files/content | F06 order/active-unit fields and F03 unit abilities. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Focus/centering order, native input cycles, redundant helper calls and private debug state. |

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

## Target and scope

This document specifies classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3** unit eligibility, selection, activation, order mutations,
order dispatch, global reset and civilization recovery. It includes the active
unit's input/automation cycle and command-level wait, finish, fortify, sleep,
disband, home and goto behavior. Appendix B records the source,
original-instruction and integration limits.

Use [F01](F01-determinism-and-arithmetic.md) for arithmetic,
[F02](F02-game-state-and-invariants.md) for state/identities,
[F03](F03-rules-and-configuration.md) for interpreted rules,
[W01](W01-map-geometry-and-spatial-state.md) for geometry/features,
[U01](U01-unit-lifecycle-and-stacks.md) for stack/home/lifetime primitives,
[U02](U02-movement-and-zones-of-control.md) for movement/HP/allowance and control
queries, and [W03](W03-visibility-and-exploration.md) for revelation.
[F04](F04-turn-sequencing.md) owns the surrounding turn and human-phase loops.

[U03](U03-pathfinding-and-goto.md) owns direction/path selection.
[W04](W04-terrain-work-and-improvements.md) owns terrain work and pillage;
[C01](C01-city-lifecycle-and-ownership.md) owns founding/joining a city.
[U05](U05-transport-and-special-movement.md) owns unload,
airlift and paradrop commands and their special movement.
[A04](A04-ai-ground-tactics.md) owns the shared automated chooser; A03/A05–A07
own its specialized work and mission choices. P01/P05 own input translation,
menu availability and presentation; the operations below do not inherit extra
validation merely because an ordinary menu usually makes it unnecessary.

Nominal calls supply valid active units where no explicit guard is stated,
live rule/owner/city selectors, finite stack chains and backed coordinates.
Cities/units are scanned by slot, not creation token or a sorted container.
Counts and state are live unless called **captured**. Callback-producing queries,
dialogs, synchronization, redraw and message pumping can change these values.
Do not reuse a captured observer, bound, active unit or rule where the procedure
requires a new read. Missing backing is handled under X01, after earlier effects,
not by a fabricated empty result.

Observe orders, targets, timers, flags, movement spent, damage, home identity,
city stocks, shared active/cursor/path state, unit and city lifecycle effects,
ordered notifications, input/network calls and return values. A handled order
is not necessarily a successful move. Most commands return no result; their
mutations, denials and child calls are the outcome.

## Eligibility and next-unit search

### Two distinct predicates

`selectable(u)` returns true only after these ordered checks succeed:

1. Slot is nonnegative, below the live unit upper bound and has a live identity.
2. W01 map bounds accept its coordinates.
3. Unit owner equals the current turn civilization.
4. Full order is neither Sleep nor Fortified.
5. U02 movement left is nonzero.
6. Spy-on-mission is absent.

Waiting, Automated, Fortifying, terrain work and Goto are not direct exclusions.
Movement-left is a read-only U02 query on valid stable state, with no event pump
or reentry. Its reference position before the spy flag test does not by itself
impose observable callback order; preserve eligibility and actual input failures.

`awakeWithMoves(u)` is less restrictive: require a nonnegative slot below the
live bound, live identity, x coordinate nonnegative, full order not Sleep and
nonzero U02 movement left. It does not check y, upper coordinate bounds, owner,
Fortified or spy mission. Do not substitute one predicate for the other.

### Search from a previous unit or cursor

`nextSelectable(previous,searchMode)` initializes best score 9999, result absent,
protected unit absent and origin equal to the current map cursor. If previous
is in the live unit range, live, and turn civilization is not barbarian 0,
capture previous's coordinates as origin and capture the **shared active unit**
as protected unit. That protected slot need not equal the previous argument.

Run at most three passes, numbered 0..2:

1. Scan unit slots ascending with a live upper bound. Require live identity,
   selectable, no Waiting flag, and, only if searchMode equals 1, owner equal
   to the live local observer. Other mode values do not add an owner filter.
2. Score each candidate as twice W01 weighted distance from captured origin,
   plus 1 for live Sea domain, using signed32 wrapping. Replace on strictly
   smaller score. On equal score replace only if this candidate is the **live
   shared active unit**. Thus absent that exception equal candidates retain
   the lower slot; Sea loses a same-distance tie to another domain.
3. If a candidate was selected, return it immediately. Otherwise scan all live
   unit slots ascending and clear Waiting. On pass 0 only, leave the captured
   protected unit's Waiting flag unchanged. Later failed passes clear it too.
   These writes affect units of every civilization, not just selectable units.
4. After pass 2's failure and Waiting-clear scan, return absent.

Best score/result are initialized once per call. A score of exactly 9999 can
select only through the active-unit tie rule; larger scores need not select
anything. Disabling optional OpenCiv2 rotation leaves the captured origin and
these tie/Waiting rules unchanged.

## Activation and focus

### Select and publish the next unit

`activateNext(searchMode)` passes the shared active slot to the search unless
active-unit-lost-in-combat is set, in which case it passes absent. After search
returns, clear that combat-loss flag, even if a callback set it during search.
Store the returned slot in shared active-unit with F02 slot retention.

For a returned nonnegative slot below the **live** bound with live identity:

1. If full orders are neither Fortifying nor Fortified, and are None or have
   signed order value above 10, clear the auxiliary timer; copy current unit
   coordinates to map cursor; redraw status with argument 1; ensure visibility
   using freshly read unit coordinates/owner; update menu states. No/Go-to/AI
   Go-to orders normally qualify; terrain jobs do not.
2. If live movement spent is zero, call W03 surroundings with allowContact=1.
3. If simultaneous-movement mode is disabled, flush queued input events. This
   test does not additionally require a network game.

Finally, if returned slot is absent or its identity is now absent, redraw all
maps with argument 0. That final identity read has no repeated upper-bound test.
Callbacks can change shared active-unit, but this procedure continues its cached
returned slot checks. Timer clearing at activation is separate from starting a
Goto: the latter does not generally reset it.

### Enter View mode

`enterView(force)` returns immediately if movement mode is already zero and
force is zero. Otherwise set movement mode to zero, blink phase to one,
skip-next-blink to one and blink-active to zero, in that order. Refresh main
status with argument 1, redraw the selected tile in each enabled map view (always
including primary), then update menu availability. Later callbacks see these
stores; this is not a synonym for a general full-map redraw.
[P05](P05-input-and-command-dispatch.md#8-map-keyboard-and-character-controllers)
owns the menu/keyboard routes into this operation and focus below.

### Focus an active unit

`focusActive(autoNext)` captures whether movement mode is not Move-units. If so,
clear blink phase, set skip-next-blink, redraw cursor, enter Move-units and clear
blink-active. Query selectable(shared active). If false call activateNext(autoNext).
The later `initialFocus` indicator is true only if mode was changed **and that
original selectable query was true**; it is not recomputed after activation.

Capture the now-shared active slot. Require valid range, identity, selectable,
and at least one of: nonnetwork mode, simultaneous movement, autoNext equals 0,
or network turn civilization equals live local observer. On success:

- If initialFocus, redraw status with 1.
- Copy unit coordinates to cursor.
- If initialFocus, ensure cursor visibility using live unit owner.
- Update menus and set unit-focus-done, then return.

On failure clear blink-active and redraw all maps with 0. Mode, cursor and earlier
selection changes are not rolled back.

### Explicit selection from a stack

Clear blink-active. In Move-units mode take shared active slot, requiring live
identity and owner equal to local observer; get U01 stack count and return
immediately if it is 1. Outside Move-units, locate first unit at cursor; require
present and owner equal to observer, and owner equal to turn civilization unless
full map revelation. Get stack count. If count is not 1, run the classic stack
chooser with cancel allowed; a negative result returns without final activation.

For the selected unit, set shared active slot, set full orders None, clear
Automated, set movement mode to the nonmoving mode, then focusActive(0). If that
**selected slot's** live spent is zero, reveal surroundings with allowContact=1.
The final selected slot need not equal shared active after focus callbacks.

The chooser traverses the complete validated stack but exposes only its first
nine members. Its row order is stack order, with owner adjective, optional
veteran label, type name and parenthesized home name; Trade adds commodity or
Freight label when its signed commodity counter is negative. A sprite click
on a nonnegative row immediately sets that unit's full orders None and clears
Automated; this callback returns zero. Double-click acceptance returns one.
Therefore canceling the dialog does not undo earlier clicks. P05/P03 own layout,
resources and modal selection decoding; no scrolling extension is part of classic.
The wake callback does not test lifetime, owner, remaining moves or bounds other
than its nonnegative argument before its writes.

## Shared order mutations

These operations have distinct preservation behavior:

| Operation | Ordered effects |
| --- | --- |
| Set ordinary Goto `(u,task,x,y)` | Set full order Goto, store task modulo 256, store destination coordinates with F02 signed-coordinate retention. Preserve auxiliary timer, flags and all other fields. |
| Interrupt Goto on contact | If order belongs to the Goto low-order class and current type role is not Trade, replace complete order by None. Preserve targets, task, timer and flags. No human/treaty test is added. |
| Wake sentries | Traverse validated stack head to tail. For full Sleep orders, set None if live domain is not Land, or the unit's own tile is not Ocean. Land sleepers on Ocean retain Sleep. Preserve cargo links and destinations. |
| Wake sleeping members by role | Traverse validated stack. For full Sleep and role membership in the supplied role set, set None. With stopAfterFirst nonzero stop immediately after this write; otherwise continue. Other orders are unchanged. |
| Set sleep | U01's shared set-sleep operation sets Sleep and clears its cargo-link/goto-x as specified there. This is not equivalent to writing None or Sleep directly. |
| Begin automation | On shared active unit, if current role is Settle, set Automated. Otherwise do nothing. Do not clear orders, timer, target or spent movement. |

W03 invokes interruption/sentry wakeup at its specified revelation positions.
The optional human-Goto-continues rule is excluded. Low-order class membership
includes additional backed off-nominal orders beyond the two order values
recognized by the execution switch; Appendix A maps that distinction.

## Order execution and Goto continuation

`executeOrders(u)` samples the complete order for its switch and returns 1 for
Fortifying, the seven terrain jobs, ordinary Goto and AI Goto. It returns 0 for
all other values, including None, Sleep and Fortified. Selected actions are:

- Fortifying: apply fortification below.
- Build Fortress, Build Road, Irrigate, Mine, Transform, Clean Pollution or
  Build Airbase: call [W04 work execution](W04-terrain-work-and-improvements.md#work-execution)
  with the unit and its then-read order.
- Goto or AI Goto: execute one goto step below.

Return 1 after a handled child even if that child failed, canceled or removed
the unit. This switch does not call selectable or exhaust on an unknown order.

### Apply fortification

Clear blink-active; set order Fortified. Run U01's **AI fortification home
adjustment** exactly once, retaining its signed home-selector gate, current-city
versus fortress alternatives, size threshold and refresh order. Then redraw the
unit's live tile and notify city-window unit-change `(u,no-old-position)`.
No movement is spent by this operation itself. The human Fortify command and
this later order-execution step are different operations.

### Execute one Goto step

1. Clear blink-active. Capture owner and full old order. Set shared pathfinding
   owner to captured owner, or absent for the AI-Goto variant's ignore-owner
   option. Invoke [U03 gotoDirection](U03-pathfinding-and-goto.md#unit-goto-direction) `(u)`.
2. For a negative result, clear full order; if live role is Sea-transport set
   counter to turn modulo 8. For direction 8, do neither of these writes.
   Both proceed to the final path-owner reset.
3. Otherwise call U02 move with `(u,direction,prompt+execute)`. A zero result
   returns **immediately without restoring pathfinding owner**.
4. For nonzero result, clear Goal-reached. If live coordinates equal the live
   goto target and captured old order lacks the ignore-owner option, set full
   order None and set Goal-reached.
5. On that arrival, if captured owner is outside the live human set, exhaust
   when live domain is Sea, or when signed auxiliary timer exceeds 9 and U02's
   adjacent-nonallied-unit query is false. After such exhaustion, if live role
   is Sea-transport set counter to turn modulo 8.
6. Set shared pathfinding owner to absent.

Do not restore a previously saved path owner; normal exit writes absent. Neither
nonzero movement return nor arrival implies displacement: U02's partial-step
failure can return 1 without moving. U03 owns what its direction 8 and negative
results mean and any order changes made during search.

### Continue automated active orders

Flush UI input, then capture shared active slot. Short-circuit this disjunction:

- Captured unit has nonzero spent, full Goto, no hold, and either role is not
  Naval-superiority or U02 adjacent-nonallied-civilization query is false.
- Otherwise call [A04 choose-active-orders](A04-ai-ground-tactics.md#2-shared-entry-and-recovery-dispatch)
  (no unit argument, using the live active slot); require its return zero, then
  nonzero movement left on the earlier captured unit.

If the disjunction succeeds, executeOrders on the captured unit; if execution
returns zero, exhaust that captured unit. Do not re-query the live active slot
for this execution. A nonzero AI-choice return bypasses it, and the first branch
bypasses the AI chooser entirely, including its callbacks/random consumption.

The automated continuation exposes `hold` as an explicit logical input. Its
producer and lifetime are not identified by this specification: it must not be
silently equated with Waiting, Garrison, a tactical context's Hold, a keyboard
modifier or an original address. Standalone callers must supply it explicitly;
full F04/desktop composition must resolve this producer gap before claiming
complete activation coverage. Tests cover both values without callback-driven
mutation of this input.

### Long-move handling

U02 calls `longMove(u,direction)` near the end of successful common finish,
before storing the new last direction. Only the shared local-human-move indicator
permits this helper. For full Goto or Automated, increment auxiliary timer modulo
256; if signed last direction XOR 4 equals requested direction, add 15 more
modulo 256. If signed timer is at least 48, show LONGMOVE; nonzero reply clears
orders and Automated. Reset timer to zero after either reply. For a local unit
neither Goto nor Automated, reset timer directly. Nonlocal units retain it.
No original Goto-start reset is implied by this helper; optional B9 changes that.

## Player commands

### Wait, finish this unit, finish this civilization

Wait sets Waiting on shared active unit, clears blink-active and blink-phase,
redraws cursor, then activates next with searchMode=0. Waiting does not consume
movement; the search can clear other units' Waiting flags as described above.

Finish/Skip this unit calls U02 move on shared active unit with negative
direction and prompt+execute. It therefore runs U02's common finish and cleanup,
not only exhaustion.

End Player Turn scans live unit slots ascending with live bound, exhausting
those whose owner equals the **live** local observer. It does not directly
advance the civilization/global turn or run U02 finish effects. Read observer
again for each candidate after child calls.

### Fortify and Sleep commands

Fortify clears blink-active and captures active unit/coordinates. Require the
tile not Ocean; for live Air also require any tile city owner or airbase owner.
On success set Fortifying, exhaust, redraw captured tile, then notify city-window
unit-change with no old position. Otherwise show CANTDO. No ownership test for
the city/airbase is added; other non-Air domains are accepted on land.

Sleep also clears blink-active and captures unit/coordinates. For live Air,
allow Ocean with a stack Carry-air member, or Ocean with a stack Submarine when
mover has Destroyed-after-attack. If neither permits it, require any tile city
owner; absence shows CANTDO and returns. **An airbase alone does not permit this
Sleep command**, unlike Fortify or U02 air basing.

Then, if the live type has Coast-bound, scan all nine W01 directions including
center for any in-bounds non-Ocean tile. Do not break the scan on the first find.
Without one show CANTDO and return. Lighthouse/technologies do not bypass this
command's coast requirement. If accepted, invoke U01 set-sleep. It does not
exhaust, redraw or notify the city window here beyond that child contract.

### Home command and return-to-recovery search

Home clears blink-active, captures active slot and locates its on-tile city.
If absent invoke `goHome(u)` below. If present, use U01's Home City command
mutation, including the Trade-with-existing-home refusal, retained home encoding
and old/new city-window refreshes. It does not additionally filter city owner.

`goHome(u)` captures actor slot, type, coordinates and owner. It chooses a
recovery destination rather than changing home affiliation:

1. Initialize carrier distance 999 and city result absent. For captured actor's
   live Land domain, capture the origin's **raw stored region ID**; otherwise
   use absent. Initialize shared nearest-city distance to 9999.
2. Scan live cities ascending with live bound, requiring captured owner. If
   a region was captured, require candidate tile's **raw region ID** equals it.
   Use W01 ordinary step distance and replace only on strictly smaller shared
   distance. Equal distances keep the lower city slot. This scan is not the
   weighted nearest-city helper used elsewhere.
3. If actor's live domain is Air, scan live unit slots ascending for captured
   owner and either Carry-air, or Submarine when the **captured actor type**
   has Destroyed-after-attack. Strictly smaller ordinary distance than current
   carrier distance selects/captures candidate coordinates; no hold/capacity,
   remaining movement or candidate-domain check is added.
4. Still in the Air branch, if origin has any airbase owner: human captured
   owner returns unchanged, AI exhausts actor and returns. This check occurs
   after the city/carrier scans and is not restricted to an own airbase.
5. Initialize destination to origin, best distance 999. If shared city distance
   is below 999 use the selected city's current coordinates. If captured carrier
   distance is **strictly smaller**, use its captured coordinates. A city wins
   an equal city/carrier distance. If final best is not below 999, return.
6. For human captured owner and live nonzero range, apply the remaining-reach
   test below against ordinary distance from captured origin to destination.
   If unreachable, return unchanged. This test is not explicitly gated by Air.
7. Destination equal to captured origin exhausts actor. Otherwise set ordinary
   Goto, then store destination x and y with coordinate retention. Preserve
   task, timer and flags; do not move immediately.

For both this command and the city-list filter below, remaining reach is:

```text
capture signed range and signed fuel counter
allowance = U02.allowance(u)
remaining = U02.left(u)
capture unsigned road multiplier r
reach = signed32((range - (counter + 1)) * allowance + remaining) / r
```

Use signed32 intermediate reduction and truncation toward zero. Capture each
input at its stated point; do not reuse the allowance calculation internally
performed by `left`. Compare reach with freshly computed ordinary distance.
Zero r is a fault boundary on this path, not infinite range.

### Goto City list

Construct the dialog before capturing active unit, owner, origin, raw region,
origin Ocean predicate and role. Begin with showAll=false. Each rebuild sets the
unit-type text argument, reloads GOTO and scans live cities ascending with a live
bound. Initially include only own cities; showAll permits foreign candidates,
subject to all the remaining filters:

1. Capture candidate raw region. Live Sea at Ocean origin requires candidate
   city adjacent to that captured origin water region. Sea on non-Ocean uses
   cityAt(captured origin), then tests the **candidate slot**, not the origin
   result, for nonnegativity before asking whether the two cities share ocean.
   Since ordinary candidate slots are nonnegative, an absent origin city is
   passed to that helper and requires backed-state handling.
2. Live Land requires candidate raw region equals captured origin raw region.
   Air with live nonzero range requires ordinary candidate distance no greater
   than the remaining reach above. Other domains/range-zero Air add no such
   filter.
3. Foreign candidates additionally require nonzero remembered size for captured
   owner or full map revelation. Persistent explored terrain alone is insufficient.

Add each eligible row with city slot as result value, in scan order. The row
starts with city name. For captured Settle role, count Pollution among in-bounds
B[0..19] radius positions (center 20 excluded); if nonzero append the parenthesized
count and pollution label. For a live own city with Airport append its facility
name in parentheses. No distance sort or route-search test is added.

If no rows, change false showAll to true and rebuild; if already true return.
With rows, show modal dialog. Negative returned selection cancels before testing
its extra/toggle result. A nonzero extra result toggles showAll and rebuilds.
Otherwise set actor's order Goto, then read selected city's live x and y into
its target and return. Do not validate the selected slot again or clear timer,
Automated, task or blink here. A missing selected-city read occurs after the
order write. Normal return releases dialog resources.

Use [C01’s coastal predicates](C01-city-lifecycle-and-ownership.md#coastal-predicates)
for `cityTouchesBody` and `citiesShareOcean`. These ordered queries do not
relabel W01 regions; preserve this list’s captured/live inputs above.

### Goto to a selected map coordinate

After [P01](P01-map-projection-and-interaction.md#accepted-coordinate-branches)
has accepted a map coordinate in Goto mode, capture shared active slot.
If nonnegative, store destination x, destination y, then full Goto and immediately
execute one goto step. Preserve auxiliary timer and other flags. This write order
is different from Goto City and setGoto. P01 owns cursor reset, hit testing,
input-busy flags and early returns around this accepted-command boundary.

### Disband

In Move-units mode capture shared active slot; otherwise capture cursor
coordinates and find first unit there. In network mode read that selected unit's
owner and require local ownership **before testing whether the slot is negative**.
That negative-slot owner read needs backing; it is not a harmless empty-tile
return. Nonnetwork mode bypasses this owner read.

With a nonnegative unit, require live local ownership or full map revelation.
Clear blink-active, set unit-type text argument and show DISBAND. Only reply 1
continues. Capture the unit's live coordinates after confirmation and find its
city. If present:

1. Credit signed32 `(signed unit cost * unsigned shield-box rows) / 2`, truncating
   toward zero, to the city's shield stock with signed-16 cyclic retention.
   Use the live type/rule values after the dialog; no owner filter is added.
2. Query the open city window. If it shows this city, call C06 save/publish build
   state. Query the window again after that callback; if it still shows this
   city, refresh it.

Then U01 delete-with-cargo `(u,sync=1)` and redraw area at the captured coordinates,
radius 0, with the live local observer and flag 1. Cargo does not add extra shield
credit through this command.

With a negative unit, only cheat-enabled **mode exactly zero** proceeds: use the
previously captured cursor coordinates to find a city. If present capture its
owner, set city-name text and show city DISBAND. Reply 1 calls C01 city deletion
with mode 0, V01 civilization-elimination check with captured owner/mode 0, then
redraws all views for live observer with flag 1. In Move-units mode those cursor
coordinates were never initialized; supply original incoming coordinates or stop
at that use. OpenCiv2's early return and independent BF-163 are deliberate fixes,
not original initialization. S04 owns the wider editor context for this branch.

## Global unit reset

`resetUnits(selector)` is distinct from recovery. F04 calls it before civilization
upkeep; setup/load/session callers can use different selectors. First rebuild
unit-presence flags:

1. Disable tile synchronization. Traverse the stored map area count in W01
   row order, beginning `(0,0)`, clearing Unit-present through the normal feature
   setter with synchronization requested. Increment x by 2; at width, increment
   y and use its parity as next x. Respect area rather than recomputing W×H/2.
2. Scan all live unit slots ascending and set Unit-present on their current tiles
   through the same setter. Do not filter owner or validate coordinates here
   beyond that setter's W01 behavior.
3. Enable tile synchronization unconditionally and send movement-style state
   sync `(all,2,0,0,1)`. Do not restore the previous enabled value.

Then, for selector Exhaust-all, scan live slots ascending and call U02 exhaust.
Continue to a second scan for **every selector**, again with a live upper bound.
For every live unit, first set Unit-present with synchronization requested; only
then filter reset eligibility. All-units and the raw All-units-alternate selector
admit every owner; a civilization selector admits exact owner equality.
Exhaust-all does not ordinarily match an owner, so its second pass marks presence
without clearing the movement just exhausted.

For each admitted unit without Spy-on-mission: set spent zero, timer zero, clear
Waiting; if Border-warned, no Border-intruder and `turn mod 4 == slot mod 4`, clear
Border-warned. Then clear Border-intruder. Preserve orders, targets, Moved,
Paradropped and unrelated flags. In particular, reset does not erase the old
Moved evidence needed by recovery.

For each admitted spy-on-mission unit: decrement timer modulo 256. Nonzero
returns to the scan without normal reset. If zero, clear Spy-on-mission, perform
the valid relationship update below, then invoke [V06 escape/loss resolution](V06-espionage-and-bribery.md#2-shared-escape-capture-and-treaty-confirmation)
`(u,mode=0,signed live counter)`. The counter is read again after bookkeeping.
Timer zero on entry becomes 255, not an immediate expiry. The invoked child can
remove/reuse slots or change the live upper bound.

### Mission expiry bookkeeping and excluded overwrite

For a civilization selector 0..7 and counter 0..7, clear Spy-chat in the directed
relationship `[selector][counter]`, then call V06. The original uses the reset
selector, **not the unit's owner**.

For All-units, All-units-alternate, or any selector/counter outside those valid
relationship indices, **omit the relationship write**. Preserve the preceding
timer/mission-flag changes and continue to V06 with the live signed counter;
V06 defines which signed counters skip the incident and where an invalid positive
counter requires a stop before its relation read. Do not modify naming
state or substitute the unit owner's relationship as an inferred repair.

The original indexes outside its relationship array and can corrupt unrelated
state even on the normal All-units path. This is an excluded out-of-bounds-write
bug under [X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
not a required logical alias. Appendix A records the original damage for
diagnosis. Omitting it here is the specified safe handling, not a claim that
the original performed bounds validation.

## Weighted nearest-city query

[C01 owns the weighted nearest-city query](C01-city-lifecycle-and-ownership.md#weighted-nearest-city),
including its owner/region/port filters, later-slot ties and published distance.
Recovery below converts the selected result to ordinary map distance itself;
that conversion is caller work and does not change C01’s shared contract.

## Civilization recovery

`recover(c)` scans slots ascending with live upper bound. Before inspecting each
slot, network mode processes messages `(1,0)`, even for a dead or foreign slot.
Then require live identity and exact owner `c`. Capture old flags and clear
Moved and Paradropped. Select the branch using **captured old Moved**.

### Unmoved, damaged units

Only nonzero live damage enters healing. Initialize healing factor 1 and invoke
the [weighted nearest-city query](#weighted-nearest-city-query) for owner c,
supplying the unit tile's raw region and no alternate owner. Its region test
uses the origin region, not candidate city regions. If a city was selected,
overwrite the shared distance with **ordinary step distance** from the unit's
live position to that city's captured coordinates. No city leaves the published
9999 unless a child explicitly mutated it.

Thus recovery can use an own city across a region boundary; it is different
from goHome's real per-candidate raw-region test.

Continue in this exact order, treating the shared distance as live at every use:

1. If unit tile has Fortress component without City component, set factor 2.
2. If shared distance <4 and live domain Land, query selected city's Barracks;
   add 1 to factor, or 2 if Barracks exists. This near-city increment is selected
   by the pre-query distance/type test, even if the query changes them.
3. Set fullRepair=false. If distance is zero, set a retained facility selector
   according to current domain: Barracks for Land, Airport for Air, Port Facility
   for Sea. Other domains leave its previous value unchanged. Query selected
   city's facility. If present and either live domain Land or shared distance
   still zero, set fullRepair and double factor.
4. If shared distance is now zero, double factor again.
5. Multiply factor by signed `maximumHP(u)/10` with signed32 reduction and
   truncation toward zero. If fullRepair and shared distance is zero, replace
   healing amount with current unsigned damage.
6. For live Air, if tile airbase owner is **strictly greater than zero**, replace
   amount with damage. A barbarian airbase owner 0 does not qualify, and no
   matching-owner check is added.
7. Take the signed minimum of amount and unsigned damage, then subtract from
   damage modulo 256. There is no separate lower clamp on healing amount.
8. If resulting damage is zero and full orders Sleep, set orders None if live
   domain is not Land or the unit's own tile is not Ocean. A unit already at
   zero damage on entry never reached this wake-up branch.
9. Redraw its live tile if local observer equals c, or seen-by contains the
   observer, or the unit's live owner equals the observer's retained owner
   representation. These tests use separate live observer reads.

The retained facility selector is per recovery call, not per unit. The original
never initializes it before the first unsupported-domain use; a prior supported
domain at distance zero can supply it for later units. Supply an incoming value
when needed. Both reconstructions' BF-028 zero default is not an original rule.
A facility query outside the valid facility IDs returns false before reading a
city; a valid facility with an absent/unbacked city requires that backing.

### Previously moved units

No ordinary healing or wake/redraw occurs. If live domain is Air, live range
is zero and tile lacks City component, query maximum HP twice as separate calls.
Set damage to F01 clamp of
`signed32(unsigned damage + firstMaximum/10)` in `[0,secondMaximum−1]`, then
retain modulo 256. This does not require Ocean or an enemy tile. A City component
(including airbase encoding) suppresses attrition; a carrier alone does not.
The clamp's reversed-bound behavior for malformed HP follows F01.

Recovery does not reset spent movement, timer, target, Waiting or cargo links.
Fuel/crash at movement completion remains U02/U05, not this turn-recovery branch.

## Active-unit input and automation cycle

F04's human phase invokes `runActiveUnit()`. Capture unit upper bound; initialize
waitedForInput=0, enteredLoop=false, resumedUnanimated=false and blink-active=true.
Capture shared active unit for tutorial checks. With tutorial mode, not Automated
and full order None:

- If live role Settle and selectable, invoke [P05’s settler-advice operation](P05-input-and-command-dispatch.md#settler-tutorial-advice) with
  the **newly read shared active unit**, not necessarily the checked slot.
- Otherwise, for nonzero live attack and unsigned damage above 4, query maximum
  HP. If freshly read damage is below maximum/3, mark the secondary damage-hint
  flag only if previously clear and request a hint. Otherwise, if the primary
  damage-hint flag was clear, set both flags and request it. A requested hint
  shows DAMAGE only when autoplay is disabled; the flags change even when the
  popup is suppressed. Appendix A maps the two flags without inferring severity
  from their source names.

After tutorial callbacks, capture shared active unit again as `actor`; initialize
automated-iteration count zero. Repeat while these short-circuit checks succeed:
selectable(**live shared active unit**); blink-active or unanimated-state=2;
player phase active; session active; live unit bound equals the entry capture;
and live local observer belongs to live human set.

Inside each entered iteration:

1. Network mode sends cleanup-style sync `(all,2,0,0,0)` and processes `(1,0)`.
   Do not re-run the loop gates immediately after these callbacks.
2. If unanimated-state=2, enable blink, clear unanimated-state and set
   resumedUnanimated=true. If that flag and **live shared active** full orders
   are not Goto, clear blink-active and the resumed flag. Continue this iteration.
3. Set enteredLoop=true. Synchronize music-option/playback state.
4. Inspect Automated on the **captured actor**. If absent, executeOrders(actor).
   Return zero sets waitedForInput=1 and pumps one window message. A handled
   order does not set waitedForInput, even if a child ran a dialog or moved.
5. If Automated, invoke continue-active-orders (which captures shared active
   after its input flush), increment the wrapping signed32 automated counter,
   and if above 20 exhaust the earlier captured actor. The counter is not reset
   by intervening nonautomated iterations.

On loop exit redraw cursors, clear blink-active, and if no iteration was entered
set shared active absent. Return waitedForInput. Guards can follow a different
live active unit from the cached actor whose orders are executed; this is an
observable callback contract, not a reason to replace the cache.

The blink timer independently processes network messages `(1,1)` first in network
mode. If skip-next-blink is clear, player phase and session active, then with
blink-active toggle phase and redraw cursor. Otherwise clear skip-next-blink.
When only blink-active is false inside the first branch, no phase/skip write is
performed. F04 owns the idle input wait and outer decisions about ending a turn;
this cycle's return is a Boolean “input wait occurred,” not a movement count.

## Nominal examples and conformance cases


> Compare the gameplay/interchange observations in the
> [production scope](#production-contract-scope). Original memory/helper traces,
> arbitrary native callbacks and unsafe backing-byte cases in historical lists
> are optional reference, not additional production acceptance criteria.

Unless overridden, use normal combat, r=3, type movement 6, HP 10, damage/spent
zero, alive human civilization 1 as turn/local civilization, no autoplay/network,
a flat valid map, coherent finite unit stacks and inert callbacks. Slots mentioned
are live; others below the stated bound are free. Default orders None, flags
empty, no cities/facilities and no extra rules. Each case records child calls
where a full subsystem fixture is not yet supplied.

| Fixture | Required result/effects |
| --- | --- |
| Own Fortified unit with moves | selectable=false, awakeWithMoves=true. Negative y with nonnegative x and live identity is another distinction: selectable=false; awakeWithMoves need not reject it. |
| Two equidistant candidates, lower slot Sea and higher slot Land | Land score is one lower and wins. Same-domain tie selects lower slot unless the higher is shared active. |
| All eligible units Waiting, protected active slot 0 and another slot 1 | First failed pass clears Waiting on 1 but preserves 0; next pass selects 1. Waiting on foreign live units is also cleared. |
| Sole eligible unit is protected and Waiting | Two failed passes release it; third selects it. The Wait command does not itself spend movement. |
| Activation of a Goto unit with timer 30 and spent 0 | Timer cleared, cursor/status/visibility/menu updates, surroundings allowContact=1, then input flush unless simultaneous mode. |
| Wake-sentry stack containing Land sleeper on Ocean, Air sleeper and Land sleeper on land | Preserve first Sleep; clear other two to None. Preserve all cargo links. Role-wake instead ignores terrain. |
| Dispatch Fortifying with remaining movement | Writes Fortified, U01 home-adjustment boundary, redraw and city-window update; returns 1 without its own exhaustion. |
| Fortify command for Air on land airbase | Fortifying, exhausted, redraw/update. Sleep command at the same airbase without city refuses with CANTDO. |
| SetGoto on timer 31, then accepted coordinate Goto | Both retain timer until their later children change it; coordinate command runs one goto step immediately. Goto City only installs the order. |
| Goto child direction negative / 8 / valid move returning zero | Negative clears orders and resets path owner; 8 retains orders and resets path owner; move return zero leaves captured path owner installed. |
| Automated active-order helper satisfies its first disjunct | AI chooser is not called; existing order executes even without a fresh left test at that branch. |
| Active-unit cycle with 21 automated iterations, inert chooser and still selectable actor | The 21st iteration exhausts captured actor. No input pump means return 0. A nonhandled normal order that pumps input sets return 1. |
| Return home: two own cities at equal ordinary distance | Lower city slot wins, unlike the higher-slot tie in the weighted nearest-city query. Equidistant carrier loses to selected city. |
| Land unit on Ocean raw region 7; own city on land region 2, with no own city whose raw region is 7 | goHome finds no eligible city and installs no Goto. Removing the raw-region gate changes this original behavior. |
| Goto City for Settle role with center polluted but no polluted B[0..19] cells | No pollution suffix. Foreign city without remembered size excluded unless full revelation, even if its tile is explored. |
| Disband cost 5, shield rows 10, city stock 9, reply 1 | Credit 25 to stock 34 before production-window callbacks and cargo-aware deletion. Cancel leaves stock and unit intact but blink remains cleared. |
| Global ordinary reset: spent 4, timer 7, Waiting/Moved/Paradropped set | Spent/timer zero; Waiting cleared; Moved/Paradropped retained until recovery. Orders unchanged. |
| Border warning on slot 5, turn 9, no intruder | Warning expires since both modulo 4 equal 1. Adding intruder preserves warning this reset, then clears intruder. |
| Spy timer 0 / 1, civilization selector 1, target counter 2 | Zero wraps to 255 with mission retained. One expires to zero, clears mission and relationship [1][2] Spy-chat, then calls escape `(u,0,2)`; no ordinary spent/Waiting reset. |
| Same expiry with All-units selector | Omit the invalid relationship write; all naming text and relationships remain unchanged by that step. Timer/mission-flag updates and escape `(u,0,2)` still occur. This case checks the specified bug exclusion. |
| Unmoved damaged Land, HP 10, no city, no fortress | Heal 1. Fortress alone heals 2. Near city within step distance 3 heals 2, or 3 with Barracks. |
| Unmoved damaged Land exactly in city without Barracks / with Barracks | Without Barracks heal 4 at HP 10; with Barracks heal all damage. Damage becomes zero and Sleep wakes except Land on Ocean. |
| Unmoved Air on airbase owner 2 / owner 0 | Owner 2 overrides to full healing without ownership matching. Owner 0 does not. |
| Already undamaged sleeper | Recovery does not run the damaged-unit wake branch. |
| Moved range-zero Air, HP 20, damage 18, no City component | Clear Moved/Paradropped; attrition clamps damage to 19, preserving orders and spent. With City component no attrition. |
| Recovery at distance zero, domain Land then unsupported domain, both damaged | First writes Barracks facility selector; second inherits it. Unsupported domain first requires supplied original incoming facility. |

Off-nominal fixtures must additionally cover signed negative HP/cost, movement
and timer reductions, target-coordinate retention, callback changes to bounds/
active slot, absent origin city in a Sea Goto list, missing disband coordinates,
unsupported rule/domain/owner backing and input cancellation after chooser clicks.
They are not all equivalent to invalid-file rejection.

## Off-nominal arithmetic and original temporary state

Spent, damage, timer, task and counters retain modulo 256; target/cursor/home
indices and city stocks use their F02 retention contracts. Signed timer 127→128
becomes −128 for threshold tests; damage subtraction with negative healing can
increase damage modulo 256. A maximum HP below 10 can produce zero healing outside
full-repair overrides. A negative cost can remove shields on disband; no positivity
validation is inserted by the command. Ordinary selection scores and range
numerators reduce in signed32 arithmetic before comparisons/division.

Retain original access order for unguarded active slots, invalid dialog result
slots and signed owner/region values. Backed incoming facility and disband
coordinates are explicit inputs, not default zero. Mission expiry omits the
excluded out-of-bounds write as specified above; Appendix A's corruption is
diagnostic evidence only. Other unsupported states can instead be
rejected under a declared [X01](X01-classic-compatibility-contract.md) input limit;
that stricter validation is not the original partial-effect/fault sequence.

This document introduces no direct random draw. Its AI, spy, work, movement,
revelation, city and presentation children keep their own random/event traces.
A dialog or input flush is not assumed to be an inert pause. The independent
reconstruction's extra checks, optional rotation/Goto continuation and B9/B27/
BF-028/BF-163 changes must not be silently adopted as classic behavior.

## Appendix A. Reference encodings and excluded bug evidence

These encodings identify validation snapshots; a new implementation can use
logical variants instead of the original layout. Original memory-write bugs
below are diagnostic evidence, not behavior to implement.

| Item | Encoding or reference operation |
| --- | --- |
| Orders | None −1 (`ff`), Fortifying 1, Fortified 2, Sleep 3, terrain jobs 4..10, Goto `0b`, AI Goto `1b`. Complete signed order selects dispatch. |
| Goto-class interrupt | `(order & 0x0f)==0x0b`; Trade role 7 bypasses. Captured old order's `0x10` suppresses goto arrival and makes path owner −1. |
| Role-wake membership | `1 << (signed role & 31)` against supplied role mask; full Sleep only. |
| Reset selectors | Civilization 0..7; All-units −1; All-units-alternate −2; Exhaust-all −3. Original −2 condition tests human membership at its own masked bit 30, which cannot be present in the ordinary eight-bit human set; it therefore admits all ordinary records. |
| Valid mission expiry | Clear unit flag `0002`; for valid selector/counter clear relationship bit `01000000`; signed counter supplies target. |
| Recovery facilities | Barracks 2, Airport 32, Port Facility 34. Valid facility query IDs are 1..34; other IDs return false before city access. |
| Recovery features | Fortress-only test `(features & 42)==40`; City component `02`; airbase uses City plus Fortress. Values here are hexadecimal. |
| Hint flags | Primary DAMAGE `0040`; secondary `0080` (named HEAVY_DAMAGE in OpenCiv2). Branch comparisons above, not names, define when each is set. |
| No-old-position city notification | `on_unit_change(u,-99,-99)`. Selection/focus status redraw uses 1; general fallback map redraw uses 0. |
| Stack chooser | Original `005b6aea`; first nine rows, sprite dimensions 66×48, dialog width 340, zoom −1. P03/P05 own pixel/layout contracts. |

### Excluded bug: mission-expiry relationship overwrite

The original clears bit 24 of the little-endian word at
`wrap32(0064c6c0 + selector*0594 + signedCounter*4)` without validating the
relationship indices. Its civilization base is `0064c6a0`; naming base is
`0064bcf8`, with eight records of `00f2`, seven 24-byte titles at offset `004a`.
For All-units selector −1, the word starts at naming offset `0434+4*counter`;
the changed byte is `0437+4*counter`, in naming slot 4:

| Signed counter | Corrupted government title | Zero-based byte position |
| --- | --- | --- |
| 0, 1, 2 | Despotism | 13, 17, 21 respectively |
| 3, 4, 5, 6, 7 | Monarchy | 1, 5, 9, 13, 17 respectively |

The corruption clears the low bit of that byte, including bytes after a string
terminator. Other invalid selectors/counters can reach different storage.
Mapped destination bytes do not make an out-of-range relationship write valid.
This calculation is retained solely to identify the original bug; a cleanroom
implementation must not reproduce the title mutation. The main algorithm
omits the invalid write and retains the established valid continuation.

## Appendix B. Evidence, checks and remaining integration

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

The View-mode entry added with P05 is `view_map_pieces` (original `004897fa`)
in independent `src/map_input.cpp` at revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`; primary `ui_redraw_all_maps` in
window.cpp corroborates the ordered stores and children. This addition was
source-reviewed; P05 records its event/callback integration limits.

Primary source is OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`, inspected in the unchanged reference
checkout at `fc2155ccdb2123c1137babab6fa39e38152278c9`. Independent source at
`civ2` was inspected read-only at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`.

| Family | Source and original entries |
| --- | --- |
| Eligibility/search/activation | [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp), selectable `005b633f`, awake `005b6458`, next `005b6512`, activation `00489859`. |
| Shared orders and dispatch | Same source: contact interruption `0042738c`, sentry wake `004273e6`, role wake `00531567`, setGoto `00531607`, dispatch `004c5408`, fortification `004c4ada`, goto step `004c4e6d`, goHome `004c54da`, automated continuation `00543b80`. |
| Commands | Same source: Wait `0058bdfd`, Disband `0058c295`, Home `0058cbe1`, Fortify `0058cce6`, Sleep `0058cde5`, Goto City `0058d6af`, Automate `0058df14`, chooser/wake `005b6aea` / `005b6a58`. |
| Focus, explicit selection and end commands | [window.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/window.cpp), focus `00489a0d`, stack selection `0058d442`, skip `0058bd60`, End Player Turn `0058bd84`. |
| Input loop and timer | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp), active unit `0048a004`; unit.cpp blink `004131c0`. |
| Reset/recovery | unit.cpp reset `0048710a`; [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp), recovery `00488cef`; [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp), presence rebuild `00487007` and accepted map-click Goto branch. |
| City queries | [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp), weighted nearest `0043d07a`, facility `0043d20a`, coast `0044263f`, shared ocean `004429af`. |
| Known differences | [BUGS.md](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md); independent BF-028 recovery facility and BF-163 disband-coordinate notes. |

Independent evidence includes `src/units.cpp`, `src/unit_orders.cpp`,
`src/map_input.cpp`, `src/human_turn.cpp`, `src/ai_unit.cpp`,
`src/ai_unit_air.cpp`, `src/civ_unit_recovery.cpp` and `src/city.cpp`.
These corroborate live read order, cached active-unit distinctions and the
original mission-expiry address calculation. They are independent source evidence,
not proof of every runtime continuation.

A narrow original-instruction inspection resolved a disagreement in goHome:
`004c5565` and `004c5611` call thunk `00401f50`, which jumps to raw-region query
`005b8a81`. The independent reconstruction instead calls its land-only-region
query there; this specification follows the original raw-region calls, agreeing
with OpenCiv2. The executable inspected in `civ2` was `Civ2/civ2.exe`,
SHA-256 `edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
It was disassembled read-only, not executed. No reference source or saved
annotation project was changed. A separate instruction check confirmed U02's
landfall reply store at `0059243b` into the same original choice location used
by expulsion; a restart can preserve that supplied choice. U02's expulsion flag
name is corrected to Broken-treaty (`40`), distinct from Vendetta (`10`).

A temporary native harness extracted **44 reference routines** and passed
**22,320 assertions** in both an optimized build and an unoptimized
AddressSanitizer/UndefinedBehaviorSanitizer build (leak detection disabled because
the execution environment uses ptrace). Signed arithmetic was compiled with
wrapping enabled. The recovery routine's reconstruction-only initial facility
was replaced by an explicit incoming-fixture parameter; ordinary cases use zero,
while an unsupported-domain case supplies Barracks deliberately.

Checks included all 256 full order values for eligibility/dispatch, all 256
contact-interrupt encodings for Trade and non-Trade, Waiting-pass mutations,
selection ties, activation/focus order, sentry/role wake differences, target
retention, goto path-owner early return, automation short-circuiting and cached
actor behavior, command Fortify/Sleep differences, raw-region goHome selection,
city/carrier ties and coastal predicates. Reset ran the real presence rebuild;
a positive civilization selector checked the actual treaty clear.

Separately, diagnostic cases ran the original mission-expiry write against
backing arranged at the original relative civilization/naming offsets. All
eight nominal spy counters reproduced Appendix A's corruption, comparing every
byte in that naming/prefix backing. Those assertions document the excluded bug;
they are not required cleanroom conformance results.

A separate guarded variant omitted writes unless both relationship indices were
0..7. It passed **22,386 assertions** in optimized and ASan/UBSan builds: the
same cases with naming bytes preserved, plus All-units-alternate and invalid
counter cases. Timer/flag/spent effects, the escape-call trace and unchanged
surrounding storage were checked. This validates the specified safe handling
with an adapted reference routine, not original equivalence at the excluded
write or V06's behavior for an invalid counter.

Recovery ran its actual nearest-city and facility queries across representative
HP/damage/location combinations, including a city across a region boundary,
full repair, owner-zero airbase exclusion, negative HP arithmetic, inherited and
supplied facility state, and network callbacks extending the live unit bound.
Actual active-unit cycles checked no eligible actor, input pumping, 21 automated
iterations, a changed active slot and suppressed tutorials; timer, Wait,
End Player Turn and long-move cases checked their independent effects.

Movement, path selection, AI, terrain work, fortification's U01 home child,
revelation, escape, music/presentation and network input were explicit adapters.
These cases validate the selected routines and their ordered continuation effects,
not those children's internal behavior. Full Goto City/stack chooser/Disband
modal flows and missing-backing faults were reviewed against source and are not
claimed executed by this harness. The overwrite diagnosis is a native reconstruction
case, not a runtime observation from the original executable. The instruction
inspection above is narrower than original-program conformance.

Artifacts remain in temporary research storage outside this repository. X02 must
retain independent fixtures and real child integrations before claiming whole-turn
coverage or reproducibility from this repository alone.

Remaining integration work:

- U03 now supplies direction/search contracts; full search/move cases must still
  reconcile path-owner lifetime, direction 8 and failed moves. U05 specifies
  basing, fuel, cargo callers and special movement; complete cases must join
  these command range checks, recovery and actual activation with that execution.
- W04 specifies work and pillage and exercises the actual order dispatcher
  with real work, U01 stacks and U02 exhaustion. Whole-turn activation, AI job
  choice and real UI/diplomatic/city/network integration remain.
- C01/C06, V06 and A04–A07 must join actual city, spy and AI children,
  including callbacks, deletion/reuse, omission of invalid writes and randomness.
- F04's complete global-reset/recovery/human-phase cases still need real upkeep,
  order and input continuations. N03/N04 must test simultaneous and hotseat
  observer/turn-owner transitions rather than assuming local single-player state.
- P01/P03/P05/P06/P10 must join semantic selection/cancellation, required notices
  and gameplay/network callbacks under X01. Native modal, redraw and music traces
  are optional presentation evidence. UI availability does not add checks to
  these callable mutation contracts.
- C01 owns shared city queries and lifecycle. Its finite source cases join
  actual U01 deletion, while full disband/recovery/input integration remains.
  X02 must retain reproducible original/source fixtures. Original incoming
  facility/disband values and unsupported backing remain distinct gaps.
