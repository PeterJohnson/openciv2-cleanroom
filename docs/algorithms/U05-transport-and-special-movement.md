# U05: Transport and special movement

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Target and scope

This specification describes classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3** boarding, unloading, landfall, transport wakeup, aircraft
basing/fuel, coast-bound loss, airlift and paradrop.
Appendix B states the finite source checks and remaining integration work.

Use [F01](F01-determinism-and-arithmetic.md) for arithmetic and both sampling
laws, [F02](F02-game-state-and-invariants.md) for identities and shared state,
[F03](F03-rules-and-configuration.md) for rule interpretation, and
[W01](W01-map-geometry-and-spatial-state.md) for coordinates, tile predicates,
ordinary distance, wrapping and ordered direction table D. Directions 0..7 are
the neighbors; D[8] is the center. Wrap x once and test bounds separately;
there is no vertical wrapping. Stored movement values already include the
road multiplier; do not multiply them again.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

[U01](U01-unit-lifecycle-and-stacks.md) owns validated stack traversal, cargo
selection/grouping, lift/relocation, basic versus cargo-aware deletion and
set-sleep. [U02](U02-movement-and-zones-of-control.md) owns allowance, movement
left, exhaustion and the directional dispatcher. [U04](U04-unit-orders-and-activation.md)
owns selectability, activation, other orders and turn recovery. W03 supplies the
[revelation operation](W03-visibility-and-exploration.md), which can contact
other civilizations and invoke callbacks. Those child operations retain their
specified side effects; they are not replaced by simple field assignments here.

C09 owns city capture; [A02](A02-ai-city-production.md) owns selection of an AI
city-production airlift's origin and actor.
[A05](A05-ai-naval-and-transport.md) owns ship cargo/port/landing decisions that
lead into these movement operations; [A06](A06-ai-air-and-nuclear.md) owns
fuel-derived AI reach, recovery and air/nuclear target choices.
[A07](A07-ai-special-unit-missions.md#4-paratrooper-objectives) owns proactive
paratrooper objectives and landing cancellation; A04 owns reactive attack response.
[U06](U06-combat-and-veterancy.md) owns combat;
[U07](U07-nuclear-attacks.md) owns nuclear effects. V06 owns mission
expulsion/escape, and P01/P05 own command availability and accepted map clicks.
This document specifies the special movement operations reached by those callers,
not new global permissions for every unit of a particular name or domain.

## Inputs, retention and safe boundaries

Nominal inputs have live valid actor/city/rule slots, an initialized map, valid
civilization/region records and supplied callback outcomes. The movement
multiplier `r` is an unsigned rule value; nominal division requires `r>0`.
A nonnegative command selection normally names one of the offered city rows.
The routines below do not universally revalidate those conditions on entry or
following dialogs. Keep cached actor, owner, city, origin and destination values
where stated, and reload live fields at the specified later reads.

`allowance(u)`, `left(u)` and `exhaust(u)` are U02's distinct operations. Stack
scans start at U01's validated head and follow its next-link operation. A live
slot bound is reread at each ascending collection-loop condition. Clearing an
order means setting the **whole order** to None; it does not clear cargo links,
movement, counters or unrelated flags unless a specified child does so.
`localHumanMove` is the shared movement indicator, distinct from the human-player
mask and the current local observer. `network` means the multiplayer state is
at least 3. Human tests use the current mask at the stated point.

In the directional continuations, `u` is the retained finishing unit, `c` and
`domain` are captured owner/domain, and `from` and `to` are captured coordinates.
`workFlag` is U02's shared Boolean scratch, reused by authority/combat and these
continuations. It is not initialized separately for each helper. Crash/sinking
rewrite `from` to the actual unit coordinates; later cleanup consumes that
changed origin. There is no new liveness guard between children.

Scalar counters retain their documented modular values even when a subsequent
comparison uses a signed interpretation. That behavior remains required for
accepted inputs. Under [X01's write exclusion](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
an invalid destination write is a **bug**, including writes through malformed
actor/city/region slots or an overflowing text buffer. Do not emulate its damage
through adjacent logical fields. Reject or stop before such a write unless a
safe continuation is separately established; keep completed valid mutations.
Supplying research backing does not make the invalid write legitimate.

Stale and out-of-bounds **reads** are a separate boundary. Supply the needed
original incoming values/backing, or stop at the first unsupported read without
inventing an original result. Examples include changed modal city selections,
zero divisors, a missing nearest city for paradrop text, and the original
uninitialized local-view inputs discussed below. A restrictive implementation
can declare a smaller accepted input domain; that is separate from categorizing
out-of-bounds writes as excluded bugs.

## Directional movement integration

U02 keeps its own entry gates, charge, authority/relocation path, ordinary
arrival, common finish and cleanup. A Land unit entering Ocean is admitted only
by U02's target-stack gate using U01's spare-capacity aggregate. That aggregate
is not a reservation or a cargo-selection pass. U01 owns which units a ship
actually gathers and how carrier links and stack order change.

For a captured Sea actor entering non-Ocean without a captured destination
city, U02 invokes **Landfall selection** below. Accepted selection restarts its
iteration with the passenger; it does not move the ship onto the land tile.
Ordinary arrival invokes **Port-arrival wakeup** at its stated position.

After ordinary arrival, stack-bottom handling, W03 revelation, city-window
notification and the enemy-movement pause, common finish invokes these operations:

1. If the retained unit's **live** domain is Air, run aircraft continuation;
   a crash stops and proceeds to U02 cleanup with its prior result.
2. Run coast-bound continuation; sinking stops in the same way.
3. U02 runs W06's hut entry when its captured hut state and live Land test allow.
4. Run transport exploration wakeup.
5. If captured boarding is true, run boarding completion on the retained unit.
6. U02 runs U04 long-move handling, stores last direction, sets success and cleans up.

A negative-direction end-move attempt also reaches this finish, with destination
captured as origin and hut state zero. Fuel and loss processing therefore need
not follow a physical step. Preserve U02's captured/live distinctions and its
shared answer/unit-bound mutations across landfall restarts.

## Transport operations

### Landfall selection

Inputs include the original ship, mutable current actor, captured origin,
mutable captured unit bound and shared dialog answer. Execute in order:

1. Restore current actor to the original ship. If `localHumanMove` is false or
   the origin tile has any city owner, stop the attempt without a passenger.
2. If the live unit bound equals the captured bound, clear ship orders. Then
   set the captured bound to zero. This mutation persists on refusal/failure.
3. Scan the ship's validated stack from head to tail. A candidate has live
   Land domain and satisfies at least one of: full order is not Sleep; stored
   cargo-link/goto-x is negative; or that link equals the original ship slot.
   Record whether any eligible candidate exists. Select the **first** with
   `left(candidate) >= unsigned r`; stop scanning immediately.
4. If none is selected, restore current actor to ship. If autoplay is disabled,
   an eligible candidate existed and the movement prompt option is enabled,
   show NOLANDFALL using the ship. Stop; the dialog result is not stored in the
   shared answer on this path.
5. If a passenger was selected and autoplay is disabled with prompt enabled,
   show LANDFALL using the original ship and store its return in the shared
   answer. Zero restores actor to ship and stops. With no prompt retain the
   incoming answer.
6. Set current actor to the passenger, clear its orders and request a dispatcher
   iteration restart. U02 recaptures its domain, owner, bounds and tile state;
   it does not flush window messages a second time.

There is no extra ownership, cargo-capacity or intrinsic unit-type test here.
A sleeping passenger explicitly assigned to a different ship is skipped; an
awake passenger with the same old link can qualify. Cleanup often sees bound
zero after refusal and consequently takes U02's bound-changed return.

### Boarding completion

Apply U01 set-sleep to the boarded unit, then exhaust it. Set-sleep's treatment
of an already sleeping unit preserves the existing link as specified in U01.
Scan its validated stack in order:

1. For each live Sea unit, capture its unsigned movement spent, then call
   `allowance(ship)`. Continue unless captured spent is **at most** that value.
   Equality qualifies even though an ordinary selection test might call the
   ship exhausted.
2. Call U01 gather-cargo with lift disabled and synchronization disabled.
3. If the boarded unit's resulting cargo link equals this ship's slot, clear
   ship orders, set the shared active unit to that ship and end the scan.

Finally redraw the captured destination, send state difference `(all,2,0,0,0)`,
then broadcast DrawSquare for that destination. These calls have no additional
network-mode gate here. A ship that does not receive the boarded unit can still
have changed other cargo through its gather operation.

### Unload command

If active unit is negative, return. Capture the active slot; if its live domain
is Sea, set its Waiting flag. Start traversal at the stack of the **then-live**
shared active unit. For every live Land unit with full Sleep order, clear that
order and call U04's selectability test. Remember the **last** selectable unit
in traversal order. Do not clear its cargo link or charge/exhaust movement.
There is no explicit ownership filter in this scan.

If a selectable unit was found, set it active, set moving-units mode to zero,
and call active-unit focus with argument zero. Otherwise retain the current
active unit and mode. The optional stack-sentry fallback is an OpenCiv2 addition
and is excluded from this classic command.

### Port-arrival wakeup

At U02 ordinary-arrival step 4, require captured Ocean origin, a captured city,
a human captured owner and nonzero **live** mover hold. Scan the mover's validated
stack and clear every full Sleep order. This includes all domains. Preserve
cargo links and other fields; this is not a series of U01 set-sleep calls.

### Transport exploration wakeup

Require captured Sea domain, nonzero live hold, human captured owner,
`left(u)==0`, and nonzero U01 stack count of **Attack role**. If any gate fails,
leave `workFlag` unchanged. Otherwise set it false and perform these scans:

1. Visit D[0..7] around captured destination in order. For each wrapped,
   in-bounds non-Ocean tile, read its raw region. Continue only if captured
   owner's policy for that region is not 4.
2. Visit D[0..7] around that tile. The first wrapped, in-bounds tile unexplored
   by captured owner sets `workFlag=true` and ends both neighborhood scans.
3. If true, scan mover's validated stack. Clear orders on each unit whose live
   role is Attack, live rule attack is zero and movement spent is zero.

The final scan has no Sleep requirement. It does not select Settle role despite
the reference helper's name. No cargo links are cleared. All neighborhood tests
use exploration state, not current unit sight or an alliance shortcut.

## Aircraft continuation

Use captured destination even if U02 reset it to origin after combat. First
set `workFlag` true if the tile has any city owner, otherwise any airbase owner;
set it false when neither exists. If false, scan the mover's validated stack.
The first member with Carry-air qualifies; a Submarine member also qualifies
when the mover has Destroyed-after-attack. Set `workFlag` true on the first match.
These tests require neither matching owners, available capacity nor a prior
cargo assignment.

If `workFlag` is true and the mover's live range equals 1, call `left(u)`; if
that is nonzero and U02's `attackDone` is true, clear `workFlag`. Evaluate left
before testing attackDone. This exception lets a range-one attacker with moves
remaining avoid immediate basing even on a qualifying destination.

When `workFlag` remains true:

1. Reset fuel counter to zero and exhaust the unit.
2. Move it to stack bottom through U01 with synchronization enabled.
3. Redraw captured destination and unconditionally broadcast DrawSquare there.
4. Return without a crash.

Otherwise, if live range is nonzero, query left. If left is zero, increment
fuel counter modulo 256. Compare the retained counter and the live range as
signed values in −128..127. A counter **at least** the range crashes; a lower
counter survives. Range zero does not increment or crash here. A transition
127→128 compares as −128, and 255→0 compares as zero; do not saturate either.

For a crash, execute in order:

1. If shared `localHumanMove` is true, play engine-sputter for unit type slots
   below 30, jet-sputter otherwise, with sound mode zero.
2. Replace captured origin with the unit's actual live coordinates and capture
   its type. Invoke U01 **basic** deletion with synchronization enabled.
3. Redraw area `(origin,radius=0,live observer,1)`, then broadcast DrawArea there.
4. Recheck `localHumanMove`; if true, show FUEL using the captured type and the
   large-screen dialog presentation mode. Return crash/stop to U02.

Fuel processing itself consumes no random draws. It does not perform a turn
reset or repair. U04 owns recovery and the range-aware return-to-base commands;
those command reach checks do not replace this finish-time basing test.

## Coast-bound continuation

Require live Coast-bound property, shared `localHumanMove`, `left(u)==0`, and
no effective Lighthouse for the **captured** owner. Evaluate in that order.
There is no additional Sea-domain or Ocean-center test. If a gate fails, return
without changing `workFlag` or drawing.

Initialize `odds` from the unsigned trireme-loss cosmic parameter. Query Seafaring;
if present, double odds, then replace a result below 3 with 2. Query Navigation;
if present double odds again. If `signed32(odds-1)<1`, set roll zero without a
draw. Otherwise take **one primary-law sample** and set `roll=draw % odds`.
The draw precedes the coast test, including when the center tile is land.

If roll is nonzero, return, retaining the incoming `workFlag`. For roll zero,
set `workFlag=false` and scan D[0..8] around captured destination, wrapping x
and checking bounds. The first non-Ocean tile, including center, sets it true
and ends the scan. If true, return without loss. Otherwise:

1. If shared `localHumanMove` remains true, play boat-sinking with sound mode 1.
2. Replace captured origin with actual live unit coordinates, capture its type,
   and invoke U01 **cargo-aware** deletion with synchronization enabled.
3. Redraw area `(origin,radius=0,live observer,1)` and broadcast DrawArea there.
4. Recheck `localHumanMove`; if true show TRIREME using captured type and the
   large-screen presentation mode. Return sinking/stop to U02.

Zero or one initial odds can therefore force a coast scan without a draw;
Seafaring changes zero to two. The nominal doubled parameter is at most 1020.
Cargo-aware deletion uses U01's actual cargo preparation and cascading policy;
it does not simply delete every unit sharing the tile.

## Player airlift

### Command entry and destination selection

Construct the selection dialog before capturing the active unit; release it on
normal return. A negative active unit returns. Otherwise capture actor `u` and
its owner `c`, and apply the following ordered checks:

1. Live Sea domain shows LIFTSHIP and returns. Otherwise live Air shows LIFTPLANE
   and returns. Other domain values pass this command's domain checks.
2. Find the city at the actor's live coordinates. If absent, show NOAIRPORT with
   the information sprite. If present but without Airport, show NOAIRPORT with
   that city. Both return.
3. If origin city is Airlift-used, set its name as text argument zero and show
   ALREADYAIRLIFT with that city; return.
4. Load AIRLIFTSELECT and scan live cities in ascending slot order with a live
   bound. Include live own cities other than origin which have Airport. Add
   their names as labels and slots as option values. **Include already used
   destination airports**; they are checked after selection.
5. With no rows, show NOAIRPORT2 using Airport's improvement presentation and
   return. Otherwise show the modal chooser. Negative selection cancels.
6. If selected destination is now Airlift-used, set its name as text argument
   zero but show ALREADYAIRLIFT with the **cached origin city**; return.

The actor, origin and owner remain cached across the dialog. There is no new
validation of selected city's lifetime, owner or Airport. Command entry does
not require moves remaining, no prior paradrop, or origin-city ownership.
Availability imposed by the menu/caller is a separate P05 contract.

### Threat count and acceptance

Set signed threat count to zero and retain the cached actor. Scan live units
ascending with a live bound. A candidate must have a live identity, owner
different from captured `c`, Air-superiority role, and directed War relation
**c→fighter owner**. There is no additional Air-domain, fuel, movement-spent,
visibility or city test.

For each candidate, call allowance, capture unsigned `r`, then calculate ordinary
distance from origin city's live coordinates to the fighter's live coordinates.
If `allowance/r >= distance`, it qualifies. Otherwise repeat allowance, reload
`r`, and calculate distance from destination city's live coordinates. It
qualifies if this second reach covers the second distance. Division truncates
toward zero; zero divisor stops at the arithmetic boundary.

For a qualifying fighter, overwrite retained enemy owner with its live owner.
Increase threat count by 2 if Veteran, otherwise 1, retaining signed32 arithmetic.
There are **no random draws in this scan**. A later loss notice uses the last
qualifying owner's name, not a randomly selected fighter or the owner responsible
for a particular roll.

Zero count proceeds without a warning. A nonzero count shows ENEMYFIGHTERS with
the fixed Fighter sprite and large-screen presentation mode. Zero dialog return
cancels; nonzero accepts. Invoke execution below with cached actor, origin,
destination, count and retained enemy owner. When count is zero the original
passes an unwritten enemy-owner temporary, but execution does not consult it.
Do not create a fighter or consume a draw merely to initialize that argument.

### Execution

The direct executor takes actor, origin, destination, signed risk count and
enemy owner. It does not repeat the command's eligibility checks:

1. Mark origin Airlift-used, then destination Airlift-used.
2. Exhaust actor, clear its full orders, and invoke U01 single-unit relocation
   to the destination city's live coordinates with synchronization enabled.
3. Notify the city window of unit change `(u,-1,-1)`.
4. For increasing `i` starting at zero while `i < signed risk count`, consume
   one **primary-law sample**. If `draw % 6 != 0`, continue. At the first zero
   remainder, if live actor owner equals live observer and autoplay is disabled,
   set enemy plural-name text argument zero and show SHOTDOWN using the fixed
   Fighter sprite and large-screen presentation mode. Then invoke U01 basic
   deletion with synchronization enabled and return immediately.
5. If all rolls survive, and live actor owner equals live observer with autoplay
   disabled, set origin and destination names as arguments zero and one and
   show AIRLIFT using the actor.

Both cities remain used after loss. Zero or negative risk count consumes no
rolls; negative is an off-nominal direct input, not a second cancellation code.
Unit relocation and city-window effects precede the first interception roll.
There is no extra revelation, Moved flag, fuel reset, damage reset, cargo-group
relocation or home reassignment in this executor. U01's actual children retain
their side effects, and callbacks can change later live owner/observer tests.

## AI city-production airlift

[A02](A02-ai-city-production.md#3-defense-needs-sales-and-donor-airlift) owns
airport/donor eligibility, policy comparisons, defense need and actor selection
in city production. Once that caller has selected a donor origin,
destination and actor for civilization `c`, its transfer continuation is:

1. Mark **destination** Airlift-used, then origin Airlift-used. Exhaust actor.
2. U01 single-unit relocation to destination with synchronization enabled.
   Preserve full orders; there is no explicit player-executor city-window call.
3. Retain transferred actor and set watcher count zero. Scan live unit slots
   ascending with a live bound. Require a live identity, Air-superiority role,
   human fighter owner, and directed War **fighter owner→c**. There is no
   separate foreign-owner check in this predicate.
4. Calculate destination-to-fighter ordinary distance **before** allowance;
   call allowance and divide by freshly read unsigned `r`. If out of reach,
   repeat with origin distance first and a fresh allowance/divisor. Skip a
   fighter out of reach of both endpoints.
5. Each qualifying fighter consumes one **secondary inclusive range** result:
   `secondaryRange(0,3)` for Veteran, `secondaryRange(0,5)` otherwise. If zero,
   basic-delete the transferred actor with synchronization enabled, mark its
   retained slot absent and end the scan immediately. Otherwise increment
   signed32 watcher count and continue.
6. If actor was deleted, set destination-name argument zero and show WESHOT.
   Otherwise, if watcher count is nonzero, set the same text and show WESAW.
   No extra observer or autoplay gate surrounds these two notices.

This continuation uses no primary draws itself. The veteran case is one result
from 0..3, **not** the player command's two primary remainder tests. Treaty
direction, endpoint order, call evaluation order and order retention also differ.
Do not implement the AI caller by forwarding to the player executor. A02 retains
its enclosing donor-loop termination behavior.

[F04's descending city scan](F04-turn-sequencing.md#city-pass) clears each
city's Airlift-used state immediately before that city's turn. Neither airlift
operation resets it on completion; do not replace the caller's per-city reset
with an assumed simultaneous global reset.

## Paradrop

### Command entry

Capture active actor without an intrinsic nonnegative/lifetime guard. If movement
spent is nonzero or Paradropped is set, show PARADROPRULES1. Otherwise inspect
origin features: without the City component show PARADROPRULES2; with it enter
paradrop cursor mode. The City component also occurs in an airbase. No intrinsic
Paradrop type-property, Land domain or owner check is added here.

The accepted map-click caller invokes the target operation below with its
selected coordinates;
[P01](P01-map-projection-and-interaction.md#7-selection-and-the-map-click-controller)
owns hit testing, cursor reset and input-busy state.
AI can invoke the target operation directly. It does not repeat these command
checks, so spent movement or Paradropped alone does not reject a direct call.

### Target checks

Capture actor owner `c` and original coordinates, and set numeric text argument
zero to the unsigned cosmic paradrop range. Apply these checks in order:

1. Ocean target rejects with PARADROPTARGET for a human captured owner.
2. A present target unit owner different from `c` rejects with PARADROPTARGET2
   for a human captured owner.
3. Calculate ordinary distance from actor's **live** coordinates to supplied
   target. Greater than unsigned paradrop range rejects with PARADROPTARGET1
   for a human captured owner.

Each rejection returns before approach draws or movement; AI rejects quietly.
The human tests do not add an autoplay suppression. The routine does not perform
an intrinsic target-bounds check; tile access follows W01's boundary contract.

Capture target tile owner `d` and city-at-target result. Use the tile owner,
not a replacement city-record owner. If a city is present, `d!=c`, and directed
relation c→d contains Ceasefire, Peace or Alliance, then:

- AI captured owner exhausts actor and returns.
- Human captured owner invokes diplomatic attack confirmation `(c,d,these three
  relations)`. Nonzero return cancels without exhaustion; zero continues.

Then publish viewers, announce, choose approach and perform movement in the
following order. Preserve captured city and owner across those callbacks.

### Viewer publication

Use logical `seen[0..7]` and `animate[0..7]` incoming temporary values; skipped
entries are not initialized implicitly. Scan civilizations `v=1..7` ascending.
Process a civilization only if `(network OR live observer==v) AND alive(v) AND
human(v)`. For each processed entry:

1. Set seen and animate to zero.
2. Set seen if captured actor owner equals v or full map revelation is enabled.
3. Also set seen if a city was captured at target, its captured owner differs
   from actor owner, target is explored by v, and v has an Embassy toward actor
   owner or target owner.
4. Scan all D[0..8] around target; wrap and check bounds. If tile city-or-unit
   owner equals v, set seen. Do not stop on the first match.
5. If seen and `(v==c OR show-enemy-moves OR captured city present)`, set animate.

An alliance alone does not substitute for any of these conditions. Entries for
newly eligible observers can still contain incoming values if callbacks changed
eligibility after this producer. Those are real read inputs, not permission to
write outside the arrays.

### Announcement and local-view inputs

Set text arguments in order: actor civilization adjective, **live** unit type
name, and nearest city's name. The nearest-city call searches all owners and
regions with no alternate owner, using the shared weighted nearest-city contract
defined in [C01](C01-city-lifecycle-and-ownership.md#weighted-nearest-city).
Text preparation runs even with no local observer or a human actor. If no city
exists, the original still reads the name through the negative result; supply
that read backing or stop. Do not synthesize an empty name as original behavior.

If the local `seen` value is nonzero, ensure target visible with captured actor
owner as the third argument. This visibility call happens even for a human
actor or autoplay. After it returns, if captured actor owner is AI and autoplay
is disabled, show PARADROP using the actor.

When networked, scan v=1..7. For each live alive human civilization different
from live observer, with nonzero seen entry, AI captured actor owner and autoplay
disabled, send the paradrop text-argument notification to its current connection,
carrying actor and the three prepared substitutions.

The original local consumers index the live observer twice independently: once
for seen before announcement, later for animate after lift/redraw. For ordinary
produced entries 1..7 use the array value. Observer 0 consumes two distinct
incoming unwritten entries. Observer −1 consumes an incoming approach-direction
temporary at the **first** consumer and the current `seen[7]` at the **second**;
they are not one shared default. Appendix A maps original storage. Other
unsupported indices need supplied read backing. The reference's always-on
no-local-visual repair for observers ≤0 is identified in Appendix B; it is not
silently substituted for original read behavior or required by the write exclusion.

### Approach selection and random consumption

Initialize best score zero and direction absent. Visit D[0..7] around target:

1. Wrap candidate x, compute y, and skip if out of bounds.
2. Consume exactly one **primary-law sample** and start score at `draw % 6`.
3. If both components of the direction are nonzero, add 3. The components are
   read again after the draw.
4. If candidate has no city-or-unit owner, add 200.
5. Only a score **strictly greater** than best replaces it. Retain candidate
   coordinates as animation origin and `direction = index XOR 4` as approach.

A tie keeps the earlier direction. Draws occur for valid neighbors even when
nobody will watch the animation. If no positive score wins, clear animate
entries 1..7 and retain absent direction; entry 0 is untouched. No winning
candidate means no produced animation-origin coordinates. Any later off-nominal
local animation requiring those coordinates needs supplied incoming values or
a bounded stop, rather than inventing target as the approach origin.

### Movement and capture continuation

After approach selection:

1. Clear actor orders. Invoke U01 lift-off-map with synchronization disabled.
   Its domain-dependent cargo grouping still applies to customized direct
   callers; this operation does not add a Land-only restriction.
2. Redraw captured origin if live observer equals captured actor owner, or
   full revelation is enabled, or the unit's live seen-by contains the observer,
   or its live owner equals the observer interpreted in the owner field's
   unsigned domain. Evaluate these tests against current state after lift.
3. Independently read local animate as above. If nonzero, animate the actor
   from retained approach coordinates and direction with both ordinary
   animation optional arguments absent.
4. Clear whole lifted-stack visibility through U01. If shared active unit
   equals actor and live observer equals captured owner, store target x then y
   into cursor coordinates with their coordinate retention.
5. U01 relocate the entire lifted group to target with synchronization disabled.
6. When networked, send state difference `(all,2,0,0,0)`, then run remote relay
   below. No capture or Paradropped assignment has happened yet.
7. Set Paradropped on the original actor.
8. If a city was captured at target and captured `d!=c`, exhaust actor only if
   captured owner is currently AI. Clear damage, publish actor as shared
   moving-unit, invoke C09 capture `(city,c,mode=0)`, then **reload the continuing
   actor from shared moving-unit**. Capture can replace that actor.
9. W03 reveal-surroundings for the continuing actor with contact allowed, then
   notify city window `(actor,captured origin)`. Clear AI-garrison on that
   continuing actor and redraw status with argument 1.

Remote relay scans v=1..7 ascending, requiring v different from live observer,
live alive and live human. Send DrawSquare at target when full revelation,
live actor seen-by for v, or live actor owner equals v. Independently, when
animate[v] is nonzero, send SlidePiece for actor at target with approach
direction, absent ordinary optional destination and final mode zero. DrawSquare
precedes SlidePiece for a recipient qualifying for both. Connection IDs are read
at each send. The transport/wire ownership of these messages remains N02/N05.

An ordinary human paradrop does not itself charge/exhaust movement or set Moved.
Paradropped is its separate persistent flag, cleared by U04 recovery. There is
no air-fuel/coast-loss continuation here. Capturing an empty foreign city clears
damage, and AI capture exhausts movement, as explicitly ordered above. Invoked
children can add their own effects; do not collapse them into a single relocation.

## Random and external-event contract

| Operation | Direct random consumption |
| --- | --- |
| Landfall, unload, boarding, port/exploration wake, fuel/basing | None. |
| Coast-bound loss | No draw for failed gate or odds 0/1 after adjustments; otherwise one primary-law sample before coast scan. |
| Player airlift threat scan | None; qualifying veterans add two later tests. |
| Player airlift execution | One primary draw per risk test, stopping immediately at first remainder-zero loss. |
| AI production airlift | One secondary inclusive-range call per qualifying fighter, stopping at first zero. Veteran upper bound 3; otherwise 5. |
| Paradrop | No direct draws on target/peace rejection; one primary draw per valid approach neighbor on acceptance, regardless of viewer visibility. |

Preserve child decision rules and state-dependent order. F01 permits separate
random contexts; do not replace the risk loops with a single aggregate
probability, since they stop at the first loss and have per-candidate behavior.
Record dialog choices, observer/human-mask changes, live collection bounds and
callback mutations. Callbacks include allowance/wonder queries, presentation,
synchronization, city-window notification, capture and W03 contact. Original stale
slot reads after a callback are not made valid by the initial actor.

## Worked cases and conformance fixtures

Unless varied, use an initialized flat 20×12 map with canonical parity, empty
non-Ocean tiles, r=3, valid rules, no callback mutations, human/local civilization
1, live actors with full health and allowance 6, no extra rules, no networking,
and autoplay disabled. Define stacks in head-to-tail order. Cases requiring a
city also supply valid names for nearest-city text; all local-view consumers use
produced observer entry 1. Scripted random results below are boundary fixtures;
F01 separately specifies generator-state vectors.

| Initial state and invocation | Required observation |
| --- | --- |
| Ocean stack `[land,ship]`; ship hold 1, spent 6; boarding land, also allowance 6. | Land sleeps and exhausts; cargo selection can link it to ship. Equality spent=allowance qualifies, ship orders clear and ship becomes active. |
| Unload `[ship,landA,landB]`, both land units sleeping/selectable with links to ship. | Ship Waiting set; both orders clear; links remain; last selectable landB becomes active, moving mode zero. |
| Landfall `[ship,A,B]`; A sleeping and linked to another ship; B sleeping and linked to ship with left 6. Accept dialog. | Skip A, select B, captured bound becomes zero, B order clears, restart dispatcher with B. |
| Same eligible passenger with left 0 and prompt enabled. | No passenger selected, NOLANDFALL shown, ship restored and bound remains zero. |
| Human Sea transport left zero beside eligible land with an unexplored neighbor; stack has zero-attack Attack-role A and Settle-role B, both spent zero and sleeping. | A order clears, B remains sleeping. With region policy 4 everywhere, neither wakes. Links remain. |
| Air range 2, counter 1, spent 6, no city/base/carrier. | Counter becomes 2; crash, basic deletion, no random draw. |
| Air range 127, counter 127, spent 6, no base. | Counter retains 128, interpreted −128; survives. This is scalar wrap, not a write bug. |
| Air range 1, counter 1, moves remain, attackDone true, city at captured destination. | Basing exception clears workFlag; counter and spent remain unchanged here. |
| Air range 2 on a city owned by another civilization, or sharing stack with a foreign Carry-air member. | Basing qualifies; counter zero, exhausted, moved to bottom. No added ownership/capacity check. |
| Coast-bound left zero, base odds 3, no technology/Lighthouse, nearby land and primary draw 0. | Draw consumed before coast scan; survives when land found, workFlag true. |
| Same ship with ocean at all nine tested positions and assigned cargo. | Sink and invoke actual cargo-aware deletion; DrawArea and TRIREME follow deletion. |
| Coast-bound base odds 0, Seafaring present. | Adjusted odds 2; one primary draw. With neither technology, no draw and roll zero. |
| Player airlift with one veteran qualifying fighter and primary results `[1,0,5]`. | Two tests: relocate and mark both cities before result 1; result 0 deletes; 5 unconsumed. |
| Player chooser includes used own Airport destination; select it. | Row exists, ALREADYAIRLIFT uses its name but cached origin city; no transfer or roll. |
| AI airlift with one qualifying veteran human fighter and secondary result 1. | One call over 0..3, no primary draw, actor orders preserved, WESAW. Result 0 instead deletes and shows WESHOT. |
| Paradrop from (4,4) to (8,4), no foreign unit/city, all eight approach neighbors valid. | Eight primary draws, Paradropped set, Moved unchanged, human spent unchanged, AI-garrison cleared after revelation/window notification. |
| Same approach, all results zero, empty neighbors. | First diagonal D[0] wins score 203: animate from (9,3) with direction 4. At corner (0,0), only three neighbors draw; first valid diagonal is (1,1), direction 6. |
| Foreign city target protected by Peace, AI actor. | Exhaust at origin, no approach draws or relocation. Human confirmation cancel instead leaves movement unchanged. |
| Capture callback replaces shared moving-unit with another valid unit. | Original actor has Paradropped; W03/window notification and AI-garrison clear follow replacement actor. |

Off-nominal fixtures must state signed counter/range inputs, division domains,
incoming observer temporaries and callback-mutated slots. Negative direct risk
count is a no-roll execution, whereas zero r at a fighter reach division is an
arithmetic boundary. For invalid writes, test safe rejection/stop with preceding
valid effects retained; any observed adjacent-memory damage is diagnostic bug
evidence and is never an expected cleanroom conformance result.

## Appendix A. Original representation and validation mapping

The main algorithms use logical properties. The following private encodings are
only for comparing reference snapshots; they do not prescribe new storage.

| Reference item | Mapping |
| --- | --- |
| Domains and roles | Land 0, Air 1, Sea 2; Attack role 0, Air-superiority 3, Settle 5. |
| Unit fields | Spent/counter/type/range and owner occupy bytes; counter/range comparisons in fuel use signed char. Coordinates and active/moving-unit slots retain signed16. |
| Unit flags | Paradropped `0010`, Waiting `4000`, Veteran `2000`, AI-garrison `0100`, Moved `0040` (hex). Use F02/U04 for their broader lifecycle. |
| Type properties | Coast-bound `0020`, Carry-air `0080`, Submarine `0008`, Destroyed-after-attack `1000`. |
| Feature predicates | City component `02`; actual City versus Airbase queries distinguish `(features & 42)==02` and `==42`. |
| Airlift | City Airlift-used `00010000`; Airport improvement slot 32; fixed Fighter sprite/type 27. |
| Treaty bits | Ceasefire/Peace/Alliance `02/04/08`; Embassy `80`; War `2000`. Player airlift's raw byte test reads the second treaty byte, not the low-byte `20` property. |
| Dialog presentation | Large-screen fuel/sinking/fighter flag is 8 for any nonzero large-screen indicator, otherwise 0. Ordinary unit notices have their separate wrappers. |
| Paradrop text | Number setter consumes unsigned low byte of cosmic range; upper concatenated scratch is not a second range input. Remote text notification is `15` hex with three substitutions. |
| Original seen/animate | At `004ca39e`, seen starts EBP−`5c`, animate EBP−`3c`, eight signed32 entries each. Producer writes eligible indices 1..7 only. |
| Observer −1 reads | Seen consumer `004ca81f` reads EBP−`60`, before first approach-direction store at `004ca956`; animate consumer `004cab00` aliases seen[7]. Supply values at their different consumption times. |
| Other observer reads | Observer 0 uses the two unwritten entry-zero values. Seen indices 8..15 alias animate[0..7] in the original frame; further reads require explicit backing. These are read mappings, not enlarged writable arrays. |
| No nearest city | Original `cities[-1].name` begins at `0064f308`, in the final 56 civilization bytes before city storage; string reading can continue until a terminator. Insufficient read backing stops; text-buffer overwrite is excluded. |

Original seen/animate arrays and automatic locals do not need to exist in a new
implementation. A compatibility fixture can provide their logical incoming
values and dependencies. It must not silently zero all of them or promote an
out-of-bounds read mapping into permission to overwrite unrelated state.

## Appendix B. Evidence and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`, inspected in checkout
`fc2155ccdb2123c1137babab6fa39e38152278c9`; the relevant implementation files
are unchanged from the pin. Independent evidence was read at
`civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, principally `src/unit_orders.cpp`,
`src/map_input.cpp`, `src/combat_entry.cpp` and `src/city_production_choice.cpp`,
plus its BF-074/BF-237 observer notes. Neither source tree was modified.

| Behavior | Source and original entry |
| --- | --- |
| Player airlift executor | [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp), `unit_airlift_perform`, `004ca1cd`. |
| Commands | Same file: `unit_cmd_paradrop` `0058d60a`, `unit_cmd_unload` `0058ddce`, `unit_cmd_airlift` `0058df7b`. |
| Paradrop and viewer/approach/relay children | Same file: `unit_paradrop`, `004ca39e`; named static children are source extractions of this body. |
| Movement continuations | Same file: landfall, air/fuel, trireme loss, exploration wake and boarding children within `unit_move_dir`, `0059062c`. |
| AI airlift | [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp), `city_choose_production_airlift_defender` within `city_choose_production`, `00498e8b`. |
| City-used reset | [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp), descending city-turn scan, specified in F04. |
| Reconstruction differences | [BUGS.md](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md) B22; [EXTRAS.md](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/EXTRAS.md), stack-sentry addition. |

B22's local-view helper returns zero for observers ≤0 even in classic mode.
Independent BF-074/BF-237 make the corresponding −1/0 presentation repairs.
Those are explicit reconstruction changes to original read behavior, distinct
from this collection's out-of-bounds-write exclusion. The independent observer
notes retain instruction evidence for the two original inputs; no fresh original
execution, disassembly export or saved annotation change was made for U05.

A temporary native harness combined **54 extracted routines**: 35 U01 lifecycle,
stack and cargo primitives, plus 19 special-movement, allowance/selection and
AI-airlift routines. It passed **9,126 assertions** in both optimized and
unoptimized AddressSanitizer/UndefinedBehaviorSanitizer builds with signed wrap,
signed char semantics and strict-alias optimization disabled. Leak detection
was disabled for the ptrace environment. Not every extracted primitive or
branch was independently covered.

The finite cases exercised all 256 fuel-counter values against ranges
0, 1, 2, 127, 128 and 255; city/foreign-carrier basing and the range-one attack
exception; representative odds/technology branches, coast-before-loss behavior
and actual cargo deletion; boarding at equality, unload's last selection and
link retention, landfall choice/failure, Attack-versus-Settle wakeup and region
policy suppression. Airlift cases covered used destination rows, cancellation,
ordered primary loss, live-owner callback changes, and AI secondary veteran/
nonveteran survival and loss. Paradrop cases covered rejection, treaty handling,
approach ties/edge draw counts, actual stack relocation, capture-actor replacement,
skipped viewer entries and the recorded network draw/slide order.

The harness used a bounded 20×12 logical map and real extracted U01 stack/cargo/
placement/deletion operations plus actual allowance, exhaustion and selectability.
Tile/city/technology/wonder/nearest-city queries and UI/network/text/capture/W03
operations were declared adapters. Secondary-range outputs and selected primary
results were scripted to exercise branches; other primary draws advanced the
F01 CRT recurrence. This is not a new proof of either generator, a full AI donor
policy test, real city capture, a network session or real visibility/contact.
The dialog adapter omitted teardown; no modal resource-lifetime claim follows.

Original unproduced seen/animate values were explicit harness inputs; executed
local consumers used observer 1. The no-threat enemy owner was supplied but was
unused by execution. The numeric text adapter consumed only the original low
byte. These cases do **not** validate original observer −1/0 reads, all malformed
backing or invalid-write handling, reentrant dialog lifetime, complete U02
movement dispatch, complete turns, or every custom-rule combination. Original
slot backing was not inferred from a successful sanitized native run.
Artifacts remain temporary research material outside this repository; X02 must
retain independently reproducible fixtures before claiming that coverage.


Remaining joins are explicit:

- U02 must run complete movement attempts with these real continuations and
  U01 cargo operations, including failed landfall, skip/end-move, capture,
  callback actor replacement and cleanup's bound/result decisions.
- U04 must join turn recovery, return-to-base and actual activation; F04/A02
  must exercise airlift use/reset and AI donor selection across a complete turn.
- W03/C09/V04 must supply real contact, capture and diplomacy children for
  paradrop, including city removal, owner changes and continuing-actor effects.
- N02/N03/N04/N05 must join authority, transport, observers and remote redraw/
  animation completion; P01/P04/P05/P06/P10 must join real command availability,
  information, selection/cancellation and required gameplay/network callbacks.
  Native modal/focus/sound/frame traces are optional under X01.
- X02 must retain independent fixtures, full initial/callback state and bounded
  original temporary reads, separating write-bug diagnostics from required
  behavior. Source execution is not complete original-program conformance.
