# U02: Movement and zones of control

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Target and scope

This specification describes classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3** movement allowances, the cost of one commanded step,
zone-of-control queries and the complete directional-move dispatcher. Its
ordered phases include attempts that end without relocation, special entry
commands, movement publication, authority delegation and cleanup. Appendix B
records the finite evidence and validation limits.

Use [F01](F01-determinism-and-arithmetic.md) for arithmetic and the primary
sampling law, [F02](F02-game-state-and-invariants.md) for state and identities,
[F03](F03-rules-and-configuration.md) for interpreted rules, and
[W01](W01-map-geometry-and-spatial-state.md) for geometry, tile components and
ordered directions. Stack, cargo-group and placement operations are those of
[U01](U01-unit-lifecycle-and-stacks.md); knowledge publication is defined in
[W03](W03-visibility-and-exploration.md).

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

Path search and its estimated costs belong to [U03](U03-pathfinding-and-goto.md). An estimated path cost is
not this step cost: in particular, actual railroad movement can cost zero.
[U04](U04-unit-orders-and-activation.md) owns command selection and order execution.
[U05](U05-transport-and-special-movement.md) owns transport and special air/sea
continuations; this document retains their entry gates and call order.
[U06](U06-combat-and-veterancy.md) owns combat;
[U07](U07-nuclear-attacks.md) owns nuclear effects, [C07](C07-trade-and-commodities.md) caravan actions, C09
capture, V06 espionage and [W06 hut outcomes](W06-huts-and-barbarians.md). The calls
and their ordering are part of this contract; their internal mechanics are not
replaced by an assumed success result.

## Inputs, observations and read timing

`move(u,direction,options)` takes a unit slot, a signed direction selector and
independent `prompt` and `execute` options. `prompt` enables selected messages
and confirmations; `execute` permits the ordinary relocation phase. **Clearing
execute does not make this a pure query.** Earlier phases can change orders,
reveal a unit, heal, expel, invoke missions, or call combat with execution enabled.
A nonzero result is not proof of displacement, and a zero result is not proof
that nothing changed.

Nominal inputs have a live unit with backed records, valid civilization/type
selectors, finite coherent stacks, consistent tile ownership and the F03 nominal
rules. Movement is measured in road units: the road multiplier `r` ordinarily
lies in 1..10; one ordinary terrain movement point costs `r` road units. Rule
movement is already scaled by F03, including its option changes. Do not apply
that scaling a second time here. Standard adjacent directions are W01's 0..7;
any negative direction finishes the unit's movement at its current position.
The backed center direction, 8, is also consumed without a special denial.

Inputs also include live game/session mode, local observer, autoplay, human and
alive sets, technology/wonder predicates, treaties, type flags and terrain costs;
UI choices, Shift-key samples, timer samples and callback/network events; the
implementation random context or controlled choices; and, on the branches identified below, incoming temporary
values not initialized by the original routine. A fixture must supply these
values or stop at their first observable use after preserving earlier effects.

Observe the full return integer, units/stacks and tile state, orders and counters,
knowledge, regional military counts, shared scratch values, random draws,
messages, animations, waits, locks and child calls. Networking means game mode
at least 3. Other modes take the local movement path, although individual message
construction calls below can still occur outside networking.

Unless explicitly called **captured**, a state or rule read is live at its stated
step. Calls can run callbacks, change records, change the observer or process
network input. Do not turn repeated reads into an entry-time snapshot. Cached
slot numbers remain slot numbers after a callback; this routine does not apply
a new identity-token check before each subsequent access. Missing or deleted
record backing is a compatibility boundary, not permission to invent a replacement.

Shared movement options and the local-human indicator are written at entry but
read live thereafter. `human(c)` means membership in the live human set. Treaty
tests are directed, from the named civilization to the other civilization.
`cityAt` identifies a city record; a tile's **City component** is the W01 feature
component and also participates in airbase encoding. These are different tests.

These updates have cyclic results, not saturation: movement spent, fuel,
last direction and the auxiliary timer retain values modulo 256; regional
military increments retain values modulo 65,536. Signed counter/type comparisons
interpret their retained values in the signed range −128..127 where stated.
Intermediate results follow F01 reference evaluation; redundant reductions
need not be executed by an equivalent implementation. Exact
reference encodings belong to Appendix A.

## Allowance, movement left and exhaustion

`allowance(u)` performs these steps in order:

1. Read the type's signed movement value into `moves`. If zero, return zero
   immediately, before technology, wonder, damage or hit-point queries.
2. If the live domain is Sea, capture the unit owner. Query its Nuclear Power
   possession; if true add the live `r`. Query effective Magellan's Expedition;
   if true add twice the live `r`. Query effective Lighthouse; if true and the
   then-current type lacks Coast-bound, add the live `r`. These are V03/C08
   possession queries; later type/rule reads are not frozen by the owner capture.
3. If live damage is nonzero, normal combat is enabled and the live domain is
   not Air, read signed maximum hit points and replace a value below 2 by 1
   for the divisor. Separately call `remainingHP(u)` below. Compute
   `scaled = signed32(remainingHP * moves) / divisor`, truncating toward zero.
   If `scaled % r` is nonzero, add `r - (scaled % r)`, using signed remainder.
   Let `minimum` be `2*r` for the then-current Sea domain, otherwise `r`.
   Return the signed maximum of `minimum` and `scaled`.
4. Otherwise return `moves` unchanged.

`maximumHP(u)` returns the type's signed hit-point value. `remainingHP(u)` first
clears damage if normal combat is disabled, then subtracts unsigned damage from
that signed maximum, returning zero if the difference is less than 1. Thus the
standalone helper has a mutation even though allowance normally gates its call
with normal combat enabled.

`left(u)` calls allowance, subtracts unsigned movement spent using signed32
arithmetic and returns zero if the result is less than 1. `exhaust(u)` calls
allowance and stores that result modulo 256 as spent movement. Exhaustion is
not an increment and need not preserve a previous larger spent value. Neither
allowance nor exhaustion is necessarily a pure query when their callees mutate
state. U01 creation deliberately evaluates allowance using retained slot damage
before clearing that damage.

## Spatial control queries

All four queries below reset shared `adjacentForeign` to absent. Scan W01
adjacent directions 0..7 in order, wrapping x and skipping out-of-bounds cells.
Stop at the first qualifying neighbor and publish its civilization in
`adjacentForeign`; otherwise leave absent. Return whether one was found.

- **Adjacent nonallied unit:** a neighbor has a tile unit owner, that owner
  differs from the supplied civilization, and the supplied civilization has no
  Alliance with it. There is no domain, terrain, unit-role, attack, peace,
  detection or barbarian exclusion.
- **Adjacent ZOC unit:** capture whether the origin is Ocean; apply the previous
  test but require the neighbor's Ocean predicate to equal that captured value.
  The existence of an appropriate tile owner is sufficient; do not inspect the
  unit's own domain or attack strength.
- **In enemy ZOC:** after resetting the scratch owner, return false if the
  origin has any tile city owner. Otherwise run adjacent ZOC unit. A friendly,
  neutral or enemy city all bypass this query; an airbase alone does not.
- **Adjacent nonallied civilization:** capture origin Ocean status as `medium`
  and origin city owner. For each neighbor, use its actual Ocean status as
  `neighborMedium` if the origin has no city owner, otherwise use current
  `medium`. Query neighbor city owner; if absent, use its unit owner. If a
  neighbor city owner exists, assign `medium = neighborMedium` **before** the
  match. Qualify if owner is present, differs from the supplied civilization,
  `neighborMedium == medium`, and there is no directed Alliance.

The last query lets a city bridge the medium boundary and can change the medium
used for subsequent neighbors, even when that city is friendly or allied. Do not
replace it with either unit-only query. The two ends of a ZOC-blocked move need
not be controlled by the same foreign civilization.

## Cost of an ordinary step

Use the domain captured for this movement iteration. For any domain other than
Land, cost is the live `r`. For Land, read the source and destination tiles and
select the first applicable row:

| Condition | Cost in road units |
| --- | --- |
| Both tiles contain at least one of City component or Railroad | 0 |
| Live unit type has Alpine | 1 |
| Both tiles contain at least one of City component or Road | 1 |
| Both have river, wrapped absolute x difference is 1 and absolute y difference is 1 | 1 |
| Otherwise | Signed destination-terrain movement cost multiplied by unsigned `r`, reduced to 32 bits |

Mixed city/railroad or city/road endpoints qualify. A City component does not
require a city record, so an airbase can supply that component. There is no
technology, ownership or diplomatic permission test for a road/rail discount.
River movement on the horizontal or vertical doubled-coordinate directions
costs ordinary terrain movement. Terrain identity was captured at movement
entry; the cost property read from that terrain rule is live.

## Dispatcher state and phase order

For the procedure below, `stop` means go to **Cleanup** with the current result
(initially zero), not immediate return. The initial invalid-entry gate is the
exception. `finish` means enter **Common finish** and skip ordinary arrival.

Per-call state starts with `attackDone = false`, `unlockPending = false`, lock
argument 1, and shared local Boolean `workFlag = false`. The visibility arrays
and expulsion choice have no original general initialization; see **Temporary
input boundaries**. Each restart initializes the remaining captured state as
specified below. A restart does not repeat the outer UI-message flush.

1. If movement is already in progress, the unit identity is absent, or either
   unit coordinate is negative, return 0 immediately. The original does not
   check an upper unit bound or upper coordinate bounds at this gate.
2. Set movement-in-progress. Store the options' low-order value in shared movement
   options. Flush pending window messages.
3. Begin/restart an iteration: capture `originalUnit = u`; clear unanimated-state,
   boarding and `hasHut`; set result 0. If prompt is enabled clear the active-unit
   blink flag. Capture the unit-slot upper bound `unitBound`. Mark transient
   attitude state dirty. Capture source coordinates `from`; initialize `to=from`.
   For nonnegative direction, apply its direction-table offset and x wrapping.
   Capture type domain, owner value `c`, and its owner-set selector.
4. If no city record exists at `from`, set the tile occupant owner to `c` using
   W01's synchronized owner setter. Set the shared local-human indicator to
   `autoplay disabled AND local observer == c`.
5. If `to` is out of bounds, show NOTONMAP only when prompt and local-human are
   both true; restore `u=originalUnit`, exhaust if Automated, then stop.
6. If networking, simultaneous movement, human `c`, and nonnegative direction,
   request a map lock for `(c,from,to,1)`. Failure exhausts an Automated unit and
   goes to cleanup **without** unlocking. Success sets `unlockPending=true`.
7. Capture source/destination terrain identities and Ocean predicates. Capture
   destination owner `d` from the tile city-or-unit query, then overwrite it with
   the first destination unit's owner when a unit exists, then overwrite it with
   the destination city record's owner when a city exists. Capture that first
   unit `v` and city `city` independently.
8. Negative direction: exhaust, retain this unit as the finishing unit, and
   finish. It does not set Moved, charge a step or run ordinary arrival.
9. Set Moved on the unit. If captured domain is Land, execute **Land checks**.
   A stop there bypasses subsequent terrain/attack checks.
10. For captured Land entering captured Ocean, restore the original unit and
    require `d == c` and positive available cargo capacity in the destination stack
    (U01 spare-holds aggregate). Otherwise stop; if accepted set boarding=true.
11. If `v` exists and its live owner differs from `c`, execute **Foreign unit**.
    That phase either stops or enters common finish; it does not fall into
    ordinary relocation after combat.
12. If captured Sea enters non-Ocean with no captured city, execute **Landfall**.
    It stops or selects a different unit and restarts at step 3, retaining
    direction, shared options, attack/work flags and lock state. Otherwise leave
    this restart loop.
13. If the captured city exists and its live owner differs from `c`, execute
    **Foreign city**. If it does not stop, proceed to **Charge and AI gates**.
14. Publish movement visibility, issue the pre-relocation state sync, execute
    the appropriate authority path, then **Ordinary arrival** and **Common
    finish**, in that order. Finally perform **Cleanup**.

### Land checks

Evaluate these in order; only the captured Land domain reaches this phase:

1. If destination first unit is absent, origin is in enemy ZOC, destination is
   in enemy ZOC, source is not Ocean, and the live type lacks Ignore ZOC, stop.
   The queries occur in that order, before the Ocean/type exceptions. Before
   stopping, if local-human and prompt, play NEG1 with mode 1; recheck local-human
   and show the ZOC tutorial if tutorial mode is enabled; then call W03 adjacent
   unit revelation at `(to,c)`. Restore original unit and exhaust if Automated.
   A destination unit of any ownership bypasses this particular ZOC denial.
2. If the live role is Trade and a city is captured, optionally show entry into
   a foreign human owner's city for 10 ticks, then call [C07 caravan entry](C07-trade-and-commodities.md#entering-a-city-with-a-caravan)
   `(u,city)`. Restore original unit; nonzero return stops.
3. If live role is Diplomacy and `d` is present and foreign: with a city,
   optionally show entry for 20 ticks when its captured owner is human, call
   V06 city mission `(u,city)`, restore original unit and always stop. Without
   a city call V06 unit mission `(u,v,c)`, restore original unit and stop on
   nonzero. On zero reload `d` from the tile city-or-unit query only; do not
   recapture `v`, city or the other fields.
4. If a city and foreign `d` exist and `c` has Alliance with `d`, compute
   `heal = signed maximumHP(u) / 10`, doubled for city Barracks. Clamp to
   `[0,unsigned damage]`, subtract from damage modulo 256, then exhaust using
   the now-healed unit and clear Moved. If autoplay is disabled, `c` is human
   and local observer equals `c`, play POS1 mode 1, set text arguments for the
   foreign civilization and unit type, and show ALLIEDREPAIR. Use the diplomatic
   portrait if Communism is known by any civilization, otherwise the allied
   signal image. Restore original unit, exhaust again if Automated, then stop.

Showing entry means: when networking broadcast PossiblyCenter `(from,c)`;
ensure that location is visible; wait the supplied ticks; U01 lift `(u,sync=1)`;
animate `(u,from,direction,-1,-1)`; U01 relocate back to `(from,sync=1)`; when
networking broadcast DrawArea at `to`; redraw `to`. This can alter stack order
although the unit returns to its original coordinates. It precedes the mission.
The original's other-domain allied-repair branches are unreachable through this
captured-Land gate and are not extra Sea/Air permissions.

### Foreign unit

Reset `d` to `v`'s live owner, then take the first stopping branch:

1. Captured Land attacks from Ocean without live Amphibious: restore original
   unit, optionally show AMPHIB when prompt and local-human, then stop.
2. Captured destination is not Ocean and mover has Submarine: restore original
   unit and stop.
3. Target has Submarine, is alone (stack count 1), is unseen by captured owner,
   has a different owner and captured owner is human: restore original unit;
   if prompt, clear its orders, mark target seen by `c`, redraw `to`, and
   broadcast DrawSquare at `to` without a mode gate. Stop even without prompt.
4. Mover's live role is Diplomacy: clear orders; set result to the human-set
   intersection with its captured owner's single member bit; restore original
   unit and stop. Preserve that full value: for civilization 3 it can be 8,
   rather than normalized true.
5. Live attack is zero: if prompt, local-human and human owner play NEG1 mode 0
   and show NONCOMBAT; restore original unit, exhaust if Automated and stop.
6. No city, no tile airbase owner, destination has an Air unit with signed range above
   one (U01 long-range-air aggregate), mover lacks Attack-air and destination has no Carry-air
   unit (U01 carrier aggregate): optionally show FIGHTER when prompt and local-human; restore
   original unit, exhaust if Automated and stop.
7. Execute **Expulsion**. Its successful expulsion or cancel stops movement.
8. If `left(u) < r`, prompt/local-human shows FATIGUE with freshly queried
   movement left and live `r`; nonzero answer restores original unit and stops.
   Otherwise, for a nonhuman owner, query left again and stop if below `r-1`.
   A human without that prompt does not receive the AI fatigue denial.
9. Store the unit as shared moving-unit and clear AI-garrison. Call
   [U06 combat execution](U06-combat-and-veterancy.md#execution-before-combat-rounds)
   `(u,direction,execute=1)` regardless of the movement execute option.
   With movement execute clear, set result to 1 if combat returned zero, else 0;
   restore original unit and stop. With movement execute set, a zero combat
   result stops. A nonzero result sets attackDone, selects the shared moving-unit
   as the finishing unit, changes captured `to` back to `from`, then finishes.

The combat result is consumed exactly as above; it is not a promise that the
original unit still exists or occupies the captured destination. U06 owns combat
movement, war handling and losses. No ordinary terrain cost is additionally
charged by this dispatcher on the combat branch.

### Expulsion

Qualify only if the live target role is Diplomacy, its stack count is 1, and the
directed `c→d` treaty has Ceasefire or Peace. Find the weighted nearest city to
`to`, over all owners, using the U01 nearest-city selection contract. If absent
or not owned by `c`, autoplay-disabled human `c` sees NOEXPEL: set text argument
1 to the target type name, replacing it with the generic diplomat label if the
subsequent live role read is no longer Diplomacy. Movement then continues to
fatigue/combat. NOEXPEL is not a denial of attack.

Otherwise fill text arguments 0..2 with target civilization adjective, target
unit name and mover civilization plural name. If no Alliance and `c` is human,
ask EXPEL only when autoplay is disabled, assigning its result to `choice`.
When autoplay is enabled this branch consumes the original incoming choice.
For the other branches set choice=0. Restore original unit. Choice 2 stops;
choice 0 performs the following expulsion and stops; any other value continues
to combat.

If either civilization is human, perform the entry animation at the mover's
origin with 10 ticks and synchronized lift/return. Here the network DrawArea
uses the origin and carries `c`, then `to` is redrawn. Next select the expelled
unit's city by ascending live city slots belonging to `d`:

- Initialize best score to −1 for live Diplomacy role, otherwise 9999.
- Re-read role per candidate. Diplomacy score is signed city size plus 50 when
  the city has a **Palace**. Replace only on strictly greater score. Thus equal
  scores keep the lower city slot; a negative score need not select any city.
- For a now-nondiplomatic target, score is weighted distance from `from` to the
  **captured destination city**, not to the candidate. Replace only on strictly
  smaller score. If that captured city is absent, this read requires original
  backing; a nominal ordinary diplomat avoids it.
- If selected, relocate target to the selected city's current coordinates with
  sync=1, clear only its seen-by state, and set text argument 3 to city name.
  No selected city means no relocation or visibility clear.

Redraw the original destination and broadcast DrawSquare there regardless of
mode or city selection. Then run exactly one notification branch in the table.
A local dialog listed here is preceded by FEEDBK03 mode 0. Human/autoplay/viewer
conditions are live at their individual uses.

| First matching branch | Local/network notifications, in order |
| --- | --- |
| Autoplay disabled and victim `d` human | Nonnetwork: UPMINE. Network with `d` local: UPMINE, then send UPYOURS to `c` if `c` human. Network with `d` remote: send UPMINE to `d`, then UPYOURS locally if `c` local. |
| Otherwise autoplay disabled, mover `c` human and `c→d` Alliance | Nonnetwork: UPYOURSTOO. Network with `c` local: UPYOURSTOO, then send UPYOURSTOO to `d` if human. Network with `c` remote: send UPYOURSTOO to `c`, then show it locally if `d` human and local. |
| Otherwise mover `c` human | While autoplay disabled: nonnetwork UPYOURS; network with `c` local UPYOURS then send it to `d` if human; network with `c` remote send it to `c`, then show it if `d` human and local. Then perform the attitude operation below, including when autoplay suppressed these messages. |
| Otherwise | No notification or attitude operation. |

Only the third branch queries A08's spaceship-race-behind predicate for `d`.
If false, consume **one primary-law sample**, take remainder by 4 and call attitude
adjustment `(d,c,remainder)`. If true and `d→c` lacks Hostile-intent, set it.
Otherwise clear Ceasefire, Peace and Hostile-intent on `d→c`, then set Broken-treaty.
There is no direct movement exhaustion here; cleanup can exhaust Automated units.

### Landfall

Invoke [U05 landfall selection](U05-transport-and-special-movement.md#landfall-selection)
with original ship, mutable current actor, captured origin, mutable captured
unit bound and shared answer. Stop on its refusal/failure result. On acceptance,
restart this dispatcher's iteration at step 3 with its selected passenger,
recapturing domain, owner, bound and destination state. Do not flush window
messages again or reset retained options, lock state, workFlag or answer.
The helper's bound-zero mutation persists even on failure; cleanup uses it.

### Foreign city

Reset `d` to the captured city's live owner. If captured domain is not Land and
is not Air with live range zero:

- Signed live attack below 99: restore original unit; when prompt, local-human
  and human owner, play NEG1 mode 0 and show OCCUPY.
- Otherwise with execute clear: set result 1 and restore original unit.
- Otherwise call [U07 nuclear action](U07-nuclear-attacks.md#nuclear-action)
  `(c,to,checkSDI=1)`. On zero, delete mover with cargo and sync=1, redraw origin
  and unconditionally broadcast DrawSquare
  there. On nonzero, clear aggression counter `[c][d]`. Restore original unit.
- Stop in all three cases; do not run ordinary cost, arrival or finish.

For Land or range-zero Air, if `c→d` has Ceasefire, Peace or Alliance, AI clears
orders and stops. Human movement calls V04 confirm-attack for those relations
only when prompt and local-human; a nonzero reply stops. Without that call or
with reply zero, continue. The movement layer does not independently declare war.

## Charge and AI gates

Query `left(u)`, restore original unit and stop if zero. Compute step cost, then
query left again. The partial-movement lottery applies only when all hold:

- left is below signed step cost;
- captured domain is Land and unsigned spent movement is nonzero;
- either prompt and shared local-human, prompt and local observer equals `c`,
  or execute and captured owner is AI.

Use roll zero without a draw if cost equals 1 or `signed32(cost−1) < 0`.
Otherwise take one primary-law sample and signed remainder by cost. Re-query left;
if `left <= roll`, exhaust and set result 1, then stop. This includes a spent
turn with no relocation. With nominal positive cost, success requires the sampled remainder to be below
left. The finite CRT output range makes remainder classes unequal when cost
does not divide 32,768; do not substitute an independently sampled floating
probability. The first step of a fresh unit
bypasses this lottery even when its terrain cost exceeds the whole allowance.

If execute is clear, set result 1 and stop after the possible lottery. Otherwise
capture old unsigned spent, add cost to spent modulo 256 and, for AI owners,
apply these gates in order. A failure retains the charge:

1. Live role is Diplomacy, or Attack with `2*signed defense < signed attack`;
   orders are Goto; old spent is nonzero; freshly queried left is at most 0
   for Diplomacy or 1 for Attack; signed cost is below freshly queried allowance;
   and either normal W01 distance from `to` to the goto target is 1, or
   `(adjacentNonalliedCiv(to,c) OR seen-by intersects humans)` and
   `NOT adjacentNonalliedCiv(from,c)`. Stop without an extra exhaustion here.
   Evaluate left-to-right with short-circuiting, preserving scratch-owner effects.
2. `signed lastDirection XOR 4` equals the direction selector, old spent is
   nonzero, live domain is not Air and own stack count is below 3: exhaust, stop.
3. Destination first unit exists, `d==c`, no city, captured domain Land and live
   role is not Diplomacy: query left, then count destination stack. If count is
   at least 2 when left is zero, otherwise at least 4, exhaust and stop.

## Movement publication and viewers

Before building viewer arrays, perform these two knowledge publications:

1. For captured barbarian owner 0, non-Ocean destination, origin explored by the
   live observer, mover not already seen by that observer, and mover's live
   owner different from the observer, find the nearest city to the origin. If
   present and owned by the observer, union the origin's explored set into this
   **single unit's** seen-by state.
2. For a captured foreign city at a destination explored by the observer, if
   observer has Embassy with `c`, Embassy with `d`, or is `d`, mark mover seen
   by the observer using U01's elementary operation.

Process civilizations 1..7 ascending. For each set `seen[k]=animate[k]=0`.
Continue only if `k` is alive, networking or `k` is local observer, and either
live human membership or autoplay with saved-human membership contains `k`.
Set seen when any of these holds: mover seen-by contains `k`; live mover owner
is `k`; captured `c==k`; or full map revelation. Otherwise scan the eight
neighbors of `to` and set seen on the first in-bounds tile with city-or-unit
owner `k`. This scan uses destination neighbors, not origin neighbors.

If this is the local observer and seen:

- Only if observer is `c`, show-enemy-moves is enabled or a city is captured,
  set animate=1 and sample Shift's high-order key-state portion. Any nonzero
  sample clears animate.
- Within that same gate, if cost is zero, Shift is down, or observer is `c`
  with captured Air domain, and additionally observer differs from `c` or live
  orders are Goto: clear animate and set shared unanimated-state=1. When
  observer is `c`, use state 2 and set the map cursor to `to` with coordinate
  retention as in F02.

For a seen nonlocal civilization, set animate=1, then clear it when
`(cost==0 OR (c==k AND live domain Air)) AND (c!=k OR live orders Goto)`.
No Shift query occurs for this remote decision. Index zero is not initialized
by this pass; neither is any array entry if movement jumped to common finish
before publication.

### Landing alert and early count

If local `seen` is nonzero, first ensure origin visibility, even if the following
landing conditions fail. Then require captured Ocean origin, non-Ocean
destination, observer different from `c`, `c!=0`, and **live** Land domain. Query
destination region; if military count `[c][region]` is zero, increment it before
checking the treaty. Only if `c→d` lacks Ceasefire and Peace, nearest city to
origin has slot **strictly greater than zero**, that city belongs to observer,
and autoplay is disabled, show LANDING with mover civilization and city name.

The count increment is not conditional on showing LANDING. Ordinary arrival
increments it again, so a first such landing can add two. A city in slot zero
suppresses the dialog but not the count. An absent destination owner on the
treaty-read path requires explicit backing as described below.

## Authority and relocation

Issue state synchronization `(all,2,0,0,1)` before choosing authority. These
arguments distinguish this movement sync from cleanup sync; N02 owns the message
format and receipt. Use the local/authoritative path if mode is below 3, this
instance is host, or captured owner is AI. Otherwise use the client-human path.
Neither path substitutes for the later common finish.

### Local, host or AI path

1. U01 lift the unit with sync=0. When networking, issue movement sync again;
   scan civilizations 1..7 ascending, reading live observer and connection:
   send SlidePiece `(u,from,direction,-1,1)` to nonlocal animated viewers, or
   DrawSquare at origin to nonlocal seen viewers. Flush with limit 5000.
2. If local animate is zero and local seen is nonzero, redraw origin. If local
   animate is nonzero, play MOVPIECE mode 1 and animate the move with arguments
   `(u,from,direction,-1,-1)`.
3. Clear stack seen-by state. If captured destination is not Ocean, mark the
   stack seen by captured `d` using U01; its absent-owner semantics apply.
4. Query and retain destination hut presence. U01 relocate the whole movement
   group/stack to `to` with sync=0; U01 determines cargo grouping and ordering.
   Issue movement sync again, even outside networking. Continue to arrival.

### Client-human path

1. Capture destination hut presence. Set shared stacker acknowledgement to −2.
   Send RealtimeStacker to host with `(c,d,u,destinationOcean,to)`; set
   `workFlag=true` after that send returns; flush with limit 5000.
2. If not locally animated, redraw origin only when locally seen. Otherwise
   play MOVPIECE mode 1 and prepare the origin tile for animation:
   count its stack. With count 1 clear its Unit-present component directly.
   With count above 1 capture mover coordinates into animation origin scratch.
   If mover's stack has no Sea units, select its next link if present, otherwise
   previous link, otherwise clear Unit-present. With one Sea unit clear it.
   With at least two, scan from stack head along raw next links to the first
   Sea unit other than mover, storing it as animation origin-stack unit; clear
   Unit-present if the scan ends absent. Failed selection leaves other scratch
   values as written or retained, rather than synthesizing a unit.
3. Set movement-animation-active, animate `(u,from,direction,-1,-1)`, then clear
   animation-active on normal return. Recount the origin stack. If nonempty,
   restore Unit-present directly on a client, or use the synchronized W01
   setter if the live host flag has become true. The direct path uses the
   previously selected tile backing.
4. For civilizations 1..7, send SlidePiece to nonlocal animated entries;
   otherwise send DrawSquare at origin to **every nonlocal entry**, including
   unseen entries. Recipient connections are read live per send.
5. With the nonzero lock/wait argument, capture start ticks. Repeatedly read
   ticks and compare signed32 elapsed against 3600, then check acknowledgement
   equals 1; while elapsed is below 3600 and acknowledgement differs, process
   network messages with `(1,1)`. The time read occurs even if already acknowledged.
   If final acknowledgement differs from 1, log the timeout, show
   SERVERCONNECTTIME, then clear turn-loop-active after the dialog returns.
6. While either priority or normal receive queue is nonempty, process `(1,0)`.
   Only after draining both queues clear `unlockPending`. Continue to arrival
   even following timeout; there is no local rollback or success revalidation.

The receiver and synchronization can mutate or delete units during these calls.
N02 must implement actual authoritative movement and replies. A fixture supplying
an acknowledgement without those state changes validates only this continuation.

## Ordinary arrival

After the authority path, in this order:

1. Captured Land from Ocean to non-Ocean increments regional military count
   `[c][destination region]` modulo 65,536, independently of the landing alert.
2. For a captured foreign city, clear mover damage, set shared moving-unit to
   mover, call C09 capture aftermath `(city,c,mode=0)`, then reload mover from
   shared moving-unit. Retain it as the finishing unit.
3. For captured barbarian `c=0`, non-Ocean destination explored by observer,
   mover seen-by lacks the **captured owner** membership, and mover's live owner
   differs from **captured owner**, find nearest city to destination. If present
   and owned by observer, union destination explored membership into the whole
   stack. This unusual test ordinarily fails because mover still belongs to 0;
   do not replace the captured-owner comparisons with observer comparisons.
4. Invoke [U05 port-arrival wakeup](U05-transport-and-special-movement.md#port-arrival-wakeup)
   with captured origin Ocean state, city and owner, plus the live finishing unit.

## Common finish

Restore the finishing unit. If nonnetwork, host, or `workFlag` is false, and the
unit has a next stack link and zero live attack, move it to stack bottom with
sync=0. This order precedes revelation. Call W03 `revealSurroundings(u,allowContact=1)`,
then P06 city-window unit-change `(u,captured from)`.

If live observer differs from captured owner, local seen is nonzero,
no-pause-enemy is disabled and a new Shift high-order sample is zero, pause
10 ticks if show-enemy-moves is disabled or shared unanimated-state is nonzero,
otherwise 15 ticks. The seen read can consume incoming temporary state when
this path was reached without viewer publication.

Then perform, in order:

1. If live domain is Air, invoke [U05 aircraft continuation](U05-transport-and-special-movement.md#aircraft-continuation)
   with retained unit, captured destination, attackDone, mutable workFlag and
   captured origin; crash stops with the prior result.
2. Invoke [U05 coast-bound continuation](U05-transport-and-special-movement.md#coast-bound-continuation)
   with retained unit, captured owner/destination and mutable workFlag/origin;
   sinking stops with the prior result.
3. If retained `hasHut` and live domain Land, call [W06 enter-hut](W06-huts-and-barbarians.md#hut-entry-and-consumption) `(u)`.
4. Invoke [U05 exploration wakeup](U05-transport-and-special-movement.md#transport-exploration-wakeup)
   with retained unit, captured owner/domain/destination and mutable workFlag.
5. If captured boarding, invoke [U05 boarding completion](U05-transport-and-special-movement.md#boarding-completion)
   on the retained finishing unit and captured destination.
6. Long-move handling for the retained finishing unit.
7. Store direction modulo 256 as its last direction; set result 1, restore that
   unit as the cleanup unit and clean up.

There is no generic liveness guard inserted between these children. The called
contracts must state their changes and shared-unit continuations. Negative
direction reaches this tail with hasHut=0, so it does not collect a hut merely
by finishing on its tile.

### Long-move handling

Invoke [U04 long-move handling](U04-unit-orders-and-activation.md#long-move-handling)
on the retained finishing unit at common-finish step 6, before the last-direction
write. Its timer, reversal and dialog rules belong to U04; movement preserves
this call position and the shared local-human indicator it consumes.

## Cleanup

1. If unlockPending, unlock map `(c,1)`.
2. When networking, issue state sync `(all,2,0,0,0)`, broadcast DrawArea at the
   captured origin (possibly rewritten by crash/sinking), then flush with 5000.
3. If the current unit-slot upper bound differs from captured `unitBound`, clear
   movement-in-progress and return **0 immediately**, skipping the remaining
   cleanup regardless of actual relocation, combat result or prior result value.
4. If result is zero: clear cleanup unit orders; exhaust if Automated; if live
   role is Sea-transport, set its counter to `turn modulo 8`. If captured owner
   is AI, set Fortified orders when AI-garrison is set, increment auxiliary timer
   modulo 256, and if its signed value is at least 20, reset it and exhaust.
   A zero-result unit can therefore finish with Fortified rather than None.
5. Otherwise clear AI-garrison on the cleanup unit.
6. When networking issue cleanup-style state sync again. Clear movement-in-progress
   and return the full result.

The bound comparison is not a live-unit count check and not a token check. A
child can change a slot without changing the upper bound; this routine continues
using the retained slot. Conversely a changed upper bound forces return zero
even after successful physical movement. No part of cleanup undoes a prior
movement charge, visibility publication, mission, repair, capture or network send.

## Random and external-event contract

U02 directly samples from the primary law at two sites: third-branch
expulsion attitude (always one draw when the race predicate is false) and the
partial-movement lottery (one draw except its stated zero-roll branch). U05's
coast-bound continuation consumes its own specified primary draw at common-finish
step 2. These movement operations introduce no secondary-law decisions.
Invoked mechanics, presentation, technology and
wonder queries retain their own contracts; include their semantic choices and
callbacks in the trace with their required dependencies.

Record each dialog result, each Shift sample, each timer result, message-pump
mutation and the live state at resumed reads. Window-message flushing happens
before movement snapshots. Network processing can occur inside sends, flushes,
locks, synchronization, animation and explicit wait loops. UI effects above are
ordered calls, not authorization to assume callbacks are inert. P03/P04/P06/P10
must reconcile their actual dispatch and media contracts with these call sites.

## Nominal examples and conformance cases

For these fixtures, unless a row overrides them: r=3, normal combat, an alive
human civilization 1 as local observer, autoplay off, networking off, no tech or
wonder bonuses, finite stacks, no treaties/features/cities/huts or other units,
unit HP 10, damage 0, base movement 6, terrain cost 1, all callbacks inert and
no uninitialized temporary value read. Direction east maps `(4,4)` to `(6,4)`
on a flat 16×12 map. Movement fixtures use prompt+execute and start with spent 0,
orders None and no special flags. Neighbor directions are W01's directions.

| Fixture or change to that baseline | Expected result and discriminating observations |
| --- | --- |
| Allowance for Land with damage 5 | 3. With damage 1: 6. With damage 10: minimum 3. |
| Sea, base 6, Nuclear Power, Magellan and Lighthouse, not Coast-bound | 18; reference query order is technology, Magellan, Lighthouse. With damage 5: 9. Coast-bound excludes only the Lighthouse increment. |
| Base movement zero, even with bonuses/damage | Allowance 0 with no bonus queries. |
| Exhaust base 6 after spent 9 | Spent becomes 6, not 9. Left becomes 0. |
| City component at origin and Railroad at destination | Cost 0. Changing destination to Road makes cost 1. A destination airbase's City component also qualifies. |
| River at both ends | East `(4,4)→(6,4)` costs 3; diagonal `(4,4)→(5,5)` costs 1. Alpine costs 1 in either case unless railroad/city pair gave 0. |
| Foreign tile-unit owner 2 on a land neighbor | Land origin is in ZOC even if the represented unit has zero attack or Air domain. Ocean origin is not controlled by this land neighbor. Alliance 1→2 removes it; Peace alone does not. |
| City-owned origin, regardless of city owner | In-enemy-ZOC returns false and leaves adjacentForeign absent. |
| Nonallied-civ query: land origin, direction 0 friendly Ocean city, direction 1 foreign Ocean unit | Friendly city changes medium; direction 1 qualifies and publishes its owner. |
| Empty destination, origin and destination each in foreign ZOC; prompted local move | Moved set, NEG1 then optional tutorial then adjacent-unit reveal; no relocation or charge; result 0, orders cleared. Adding any destination unit bypasses this ZOC denial. |
| Ordinary valid step | Spent 3, displacement east, stack visibility clear/republication then W03 surroundings, last direction east, result 1; no direct random draw. |
| Destination terrain cost 3, fresh unit | Cost 9 exceeds allowance 6 but move occurs with spent 9 and no partial-step draw. |
| Same cost 9, spent 3, primary state 1 | Primary next result 41 gives remainder 5; left 3 is at most 5, so exhaust to spent 6, stay at origin, return 1, no finish/revelation, clear AI-garrison. Next primary state is 2,745,024. |
| Same state but execute clear | The same prompted lottery still draws and can exhaust. If no lottery applies, returns 1 without charge but retains earlier Moved/owner changes. |
| Unseen enemy Submarine alone at destination | Prompt reveals it and clears mover orders, returns 0, no combat or cost. Without prompt still blocks but does not publish target. |
| Allied city, mover damage 5, Barracks | Heal 2, damage 3; allowance rounds back to 6, spent becomes 6, Moved cleared, no relocation, result 0. |
| Diplomat expulsion with two equal-score victim cities | Lower city slot wins. A Palace adds 50; Courthouse alone does not. Third notification branch, race predicate false, state 1: one draw 41 adjusts attitude by 1. |
| AI charged move rejected for immediate reverse, old spent 1, own stack count 1 | Charge occurs, then exhaustion; no displacement, result 0; ordinary AI failure timer increments afterward. |
| First visible foreign landing with region military count 0 and valid treaty backing | Count becomes 1 during alert even if no dialog, then 2 during ordinary arrival. Nearest city slot 0 suppresses LANDING only. |
| Client with acknowledgement timeout then drained queues | Timeout dialog precedes turn-loop-active clear; unlockPending cleared after drain; arrival still runs; no rollback. |
| Any child changes upper unit bound before cleanup | Return 0 after network cleanup, regardless of earlier result; no failure-order/timer cleanup or final second sync. |
| Negative direction, supplied incoming seen values all zero | Exhaust, common revelation/city-window continuation and finish effects; no ordinary charge, Moved write or hut entry. Last direction retains the negative selector modulo 256. |

These are local contract fixtures. Rows using child adapters require the named
response/state trace as an explicit fixture input. Joined cases must exercise
the owning algorithms before claiming coverage of those integrations.

## Off-nominal behavior and temporary input boundaries

Deterministic retained-value cases must be distinguished from missing backing:

- Signed negative movement/rule hit points and movement spent wrap can produce
  zero remaining movement, a minimum allowance after damage, or an exhaustion
  store much larger than the signed allowance. For example base −1, no damage
  and Land gives allowance −1, exhaustion 255 and left 0; with damage 1 and
  maximum HP 10, signed scaling and minimum yield allowance 3.
- Negative terrain costs can reduce spent modulo 256. With spent 2, cost −3
  stores 255 after charging; allowance 6 then gives left 0. Do not clamp cost
  to a positive value as an undocumented original rule.
- Zero r faults on the damaged allowance remainder path. An undamaged unit can
  return a base allowance with r=0. An invalid terrain/rule selector, invalid
  owner/treaty index, out-of-range direction table selector or incoherent stack
  needs supplied backing/fault behavior. This dispatcher does not validate all
  of those before its earlier side effects.
- Fuel or auxiliary timer increment 127→128 yields signed −128 and avoids a
  positive threshold test. Timer 255→0 can restart accumulation. Regional count
  65,535→0 changes later zero-count landing tests.
- Partial-step roll uses the exact signed cost condition above; division/remainder
  faults and signed32 overflow follow F01. No generic random-range helper may
  add or remove a draw.

### Temporary input boundaries

The original does not initialize all per-call viewer and choice values:

1. Viewer publication writes only seen/animate entries 1..7. Entry 0 is never
   initialized locally, yet a local observer of 0 can select it. A path reaching
   common finish before publication can also consume incoming seen entries 1..7.
   Supply those exact values when used; nonzero magnitude need not be 1.
2. For observer −1, local seen selects the live `hasHut` scalar, initially zero
   each iteration; local animate selects seen[7]. Preserve these logical aliases
   and read timing without requiring a contiguous machine stack in a new design.
   Other out-of-range observers require separate supplied backing or a stop.
3. When a human's nonallied expulsion prompt is suppressed by autoplay, choice
   is not assigned. It may retain a prior landfall choice on a restarted call,
   or otherwise require incoming state. Choice 0 expels, 2 cancels, other values
   continue to combat. Assigning zero is a deliberate deviation.
4. Landing-alert treaty lookup uses captured destination owner even when absent.
   If that path is reached, the nominal civilization table does not define the
   read. Supply its backing or stop after the regional-count increment and prior
   visibility/UI effects. Similarly the non-Diplomacy expulsion fallback can
   index an absent captured destination city after callbacks change the role.

OpenCiv2 B14's classic preset for skipped-publication seen entries is one sampled
stack configuration, not a universal original default. B22's entry-zero clearing,
B25's autoplay choice fix and independent reconstruction BF-004/BF-105/BF-238
are also not normative definitions of missing original state. The selected
reference can contain such fixes even when classic mode is enabled. Tests using
those values must say that the inputs were supplied or the behavior was changed.

A stricter implementation may reject malformed rules, unsupported indices and
states needing unrecovered temporary values. Declare those input limits under
[X01](X01-classic-compatibility-contract.md). Rejection at an earlier validation
boundary is not the original algorithm's partial-effect/fault sequence, and zero
initialization is not recovery of a value the original never wrote.

## Appendix A. Reference encodings and operation mapping

The following encodings aid validation; they do not prescribe internal storage.
F02/F03 and U01 own the complete record/type/order mappings.

| Logical item | Reference representation |
| --- | --- |
| Prompt / execute | Low movement-option bits `0x01` / `0x02`; shared options retain a byte. |
| Domain | Land 0, Air 1, Sea 2. Terrain Ocean is terrain index 10; a coincident unit-enum name in generated source is not a different test. |
| Cost components | Tile feature City `0x02`, Road `0x10`, Railroad `0x20`; River is terrain component `0x80`. Cost tests use any City/Railroad `0x22` or City/Road `0x12`. |
| Treaties used here | Ceasefire `0x02`, Peace `0x04`, Alliance `0x08`, Embassy `0x80`, Hostile-intent `0x20`, Broken-treaty `0x40`. Expulsion escalation clears `0x26` then sets `0x40`. |
| Membership result | Human-mask AND `1 << (owner & 31)`, full integer retained. Owner records/indices still need backing separately. |
| Spent, damage, counter, timer, last direction | Retained bytes; unsigned spent/damage, signed counter/timer/range/last-direction comparisons as specified. Regional military count retains 16 bits. |
| Normal/weighted distance | W01 `map_distance` / `map_distance_diag`; nearest-city helper uses weighted selection. |
| Stacker request / reply | Messages `0x5a` / `0x5b`; reply state starts `0xfffffffe`. N02 owns receipt and wire layout. |
| SlidePiece / DrawSquare | `0x70` / `0x72`. DrawArea/PossiblyCenter use named N02 message contracts. |
| Expulsion text messages | UPMINE `0x5f`; UPYOURS `0x60`; UPYOURSTOO `0x61`; text arguments mode 4, unit payload, other values zero. |
| Fuel/sinking presentation | U05 Appendix A owns the large-screen dialog mapping used by its continuations. |
| Missing local-view inputs | Original seen/animate arrays have eight integer elements; seen[−1] aliases hasHut, animate[−1] aliases seen[7]. Expulsion choice is original frame location EBP−`0xe0`. |

Network sync in this document denotes `net_send_state_diff(255,2,0,0,last)`;
last=1 for movement sync and 0 for cleanup sync. Broadcast recipients use 255;
client stacker target uses 0. Per-civilization sends resolve the live session
slot to a connection. Packet serialization, unsupported recipients and receiver
semantics are N02/N05 obligations, not inferred from a successful local send.

## Appendix B. Evidence, validation and remaining integration

Primary evidence is OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`, targeting the classic build above.
The inspected movement bodies are unchanged in the reference checkout at
`fc2155ccdb2123c1137babab6fa39e38152278c9`. Independent reconstruction was
inspected read-only at `civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. No original executable was run for
this document and no original temporary-state defaults are inferred from either
reconstruction's initialized locals.

| Function family | Evidence and original entry |
| --- | --- |
| Directional move, entry, gates, publications, execution and cleanup | [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp), `unit_move_dir`, `0059062c`, and its extracted `unit_move_dir_*` helpers. Helpers are parts of that original entry, not new independently callable original functions. |
| Maximum/remaining HP; allowance/left | Same source: `005b29aa`, `005b29d7`, `005b2a39`, `005b2c3d`. |
| Exhaustion | Same source: `unit_exhaust_moves`, `005b6787`. |
| Neighbor owner and ZOC queries | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp), `005b49cf`, `005b4b66`, `005b4c63`, `005b4d8c`. |
| Primary stream identity | [crt.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/native/crt.cpp), `_rand` and `crt_rand_at` use the same primary generator. |
| Original quirks and selectable fixes | [BUGS.md](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md), especially B9, B14, B22 and B25. |

Independent evidence includes `src/move_unit.cpp` and the `move_entry`, `move_zoc`,
`move_land_actions`, `move_attack_gate`, `move_expulsion`,
`move_expulsion_effects`, `move_combat_gate`, `move_landfall`, `move_city_entry`,
`move_charge`, `move_visibility`, `move_notifications`, `move_execution`,
`move_arrival`, `move_city_window`, `move_air`, `move_sea` and `move_finish`
translation units and their declared original instruction boundaries. Their
BF-004/BF-105/BF-238 notes distinguish production fixes from original temporary
inputs. In particular, both reconstructions agree that both movement combat
branches pass execute=1 and that client redraw sends are not gated by seen state.

A temporary native harness extracted **27 reference routines**, including the
complete directional dispatcher, allowance/HP/exhaustion, step cost, all four
control queries, viewer selection, AI refusal, expulsion city selection and
long-move/failure cleanup. It passed **134,459 assertions** in an optimized build
and again in an unoptimized AddressSanitizer/UndefinedBehaviorSanitizer build
with leak detection disabled because the execution environment uses ptrace.
The source was compiled with signed-char behavior and wrapping signed arithmetic;
one pointer-valued temporary in the dispatcher was widened for the native host.

The finite checks covered all 65,536 endpoint feature combinations for Land
step cost, nominal damage/allowance combinations for r=1..10, maximum HP=1..40
and 1..8 base movement points, ordered technology/wonder queries with live-rule
mutation, direction/medium/treaty control selection, signed negative costs and
allowances, timer wrapping, AI charge retention, Palace/tie expulsion selection,
viewer publication and explicitly supplied observer −1 aliases. Actual dispatcher
cases covered initial refusal, out-of-bounds cleanup, ordinary relocation,
first-step overspending, the exact state-1 partial-step draw, execute-clear side
effects, negative direction, changed unit bound, lock refusal and host/client
continuation/unlock selection.

Stack aggregate/placement, technology/wonder possession, city lookup, spatial
scores, presentation and networking were explicit adapters. In the dispatcher,
land/foreign-unit/foreign-city gates, relocation, landing alerts and air/sea
children were controlled boundaries; their full internal effects were reviewed
against source, not exercised by these joined dispatcher cases. Revelation was
an ordered callback with a unit-bound mutation case, not real W03 revelation.
The harness's incoming arrays follow the selected reference's B14/B22 presets
where publication was skipped; those presets are not evidence of original
initialization. No test of that path claims a universal original pause result.

These checks establish the selected source formulas and continuation behavior,
not complete original-program movement conformance. They do not cover original
stack-value production, actual combat/mission/capture/hut integration, real
host/client state exchange, UI reentrancy, missing-backing faults or every
custom-rule state. The fixture artifacts remain research tools outside this
specification repository; X02 must turn selected cases into retained independent
conformance fixtures before claiming their reproducibility from this repository.

Remaining joins are explicit:

- U03 specifies different estimated costs, search state and goto directions;
  complete route/execution cases still need the real dispatcher. U04 owns long-move
  handling and order start/activation; real command/move cases must reconcile
  its path-owner lifetime and this dispatcher's failure/finish effects.
- U05 owns transport, fuel and coast-bound continuations with their captured/
  live inputs and shared scratch. Its finite checks join actual U01 cargo and
  deletion; complete dispatcher/landing/survival/cleanup cases remain to run.
- U06 now supplies the combat parent, including its preview/execution distinction,
  charge-before-confirmation and shared moving-unit aftermath. Complete joined
  attack/cleanup cases still need real war/event/city/AI children.
- [U07](U07-nuclear-attacks.md#appendix-b-evidence-and-validation-limits) now
  exercises the foreign-city nuclear child with the real nuclear action and
  U01 deletion on interception and completion. Complete dispatcher/cleanup and
  live session integration remain open. C07/C09, V04/V06, A08 and W06 must add
  expulsion, caravan, capture, diplomacy and hut traces with real children.
- U01/W03 joins need real stack repair/cargo grouping, creation allowance,
  contact/goal mutations and observer changes, not inert callbacks alone.
- N02/N03/N05 must join host stacker receipt, acknowledgement, message pumping,
  locks and simultaneous movement; P03/P04/P06/P10 must join notifications,
  animation, city-window refresh and callbacks. No network timeout here implies
  successful authoritative relocation.
- X02 must retain finite fixtures, supplied original backing and complete traces,
  including original observer −1/0, autoplay expulsion and off-nominal faults.
  These checks do not close those whole-game validation obligations.
