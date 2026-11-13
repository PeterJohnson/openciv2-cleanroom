# W04: Terrain work and improvements

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Target, ownership and inputs

This specification describes classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3** player terrain-work commands, work accumulation, completed
improvements/conversions, irrigation sources and pillaging.
Appendix B records the finite validation and outstanding integrations.

[F01](F01-determinism-and-arithmetic.md) owns arithmetic/random primitives;
[F02](F02-game-state-and-invariants.md) owns units, identities and shared state;
[F03](F03-rules-and-configuration.md#terrain-results) owns parsed terrain outcomes
and work parameters. [W01](W01-map-geometry-and-spatial-state.md) owns geometry,
feature predicates and primitive tile mutations. [W03](W03-visibility-and-exploration.md)
owns remembered features. [U01](U01-unit-lifecycle-and-stacks.md) owns stack
traversal/repair; [U02](U02-movement-and-zones-of-control.md) owns movement
allowance and exhaustion. [U04](U04-unit-orders-and-activation.md) owns activation
and order execution. [C01](C01-city-lifecycle-and-ownership.md#weighted-nearest-city)
owns the shared weighted nearest-city query used by pillaging.

[A03](A03-ai-settlement-and-terrain-work.md) chooses AI/automated work;
[A04](A04-ai-ground-tactics.md#pillage) chooses tactical pillage. P05 owns menu/key
availability and dispatch, P04 owns dialog/text behavior, P02/P06 own redraw and
city-window callbacks, and V04 owns confirmation/treaty/war children.
[W05](W05-pollution-and-global-warming.md) owns pollution generation and warming, and C02/C03 own yields and city recalculation.
W04 specifies when those children run; a boundary is not assumed success.

Nominal worker commands take a valid active live unit at a canonical land
coordinate, valid rule records, and one of the seven named work orders. The
unit must have the Settle role. Transform additionally requires the fixed
Engineers type. Queued execution and direct work calls have **fewer gates** than
the player command, as specified below. Valid stock-rule work thresholds are
positive and reachable by the progress counter; accepted custom files need not
satisfy those downstream assumptions.

Nominal pillage commands use a live unit with nonzero attack and Land domain
on a noncity tile with a selectable improvement. Direct pillage calls instead
supply a unit and either automatic selection or a specified feature set. Their
caller supplies tactical eligibility. No generic adjacency or ownership gate
is inserted into either core operation.

[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
excludes out-of-bounds writes and their resulting corruption. A cleanroom may
reject such inputs or stop before the invalid write; preserve prior valid
effects unless a safe continuation is explicitly established. Do not replace
an overwrite with a logical write to a guessed neighboring object. In-bounds
counter wrap, signed rules, retained slots and live callbacks remain required.
Unsupported reads require declared backing or a bounded stop at the first such
operation. F03 parser acceptance alone does not prove a safe terrain-work path.

No operation owned here directly draws a random number. Include any child
algorithm's draws at its actual call position in a complete trace.

## Irrigation-source predicate

Use W01's ordered diagonal table **Q[0..4]**, including its center last. For each
entry add the offset to the supplied coordinate with F01 signed32 retention,
wrap x once, and test rectangular bounds. Skip misses. Return true at the first
in-bounds candidate that, in this order, is Ocean, has a river, or has Irrigation.
Return false after all five fail.

This is a local source check, not a recursive search to water. It ignores owner,
exploration, city radius and whether an irrigated source remains connected to
water. The center's own Irrigation suffices for Farmland. The four other D
neighbors are not searched. Repeat wrapped aliases rather than deduplicating.
There is no separate center-validity or parity rejection before the loop.

## Player work command

Capture active unit slot `u`, its coordinates `p`, owner `c`, and the tile's
terrain kind `t`, in that order. Clear blink-active. Test the unit's live type
role. If it is not Settle, show ONLYSETTLERS using the no-improvement sprite,
presentation mode 8, and return. Earlier coordinate/terrain reads still happened.

Apply only the requested job's following checks, in the stated order. Each
rejection returns immediately without invoking work. Eligibility helpers and
shared state are queried where used; do not snapshot all conditions at entry.

### Airbase

1. Require Radio for captured owner `c`; otherwise show RADIO with that advance.
2. Reject a city, then a Fortress without the City component, then Ocean, using
   CANTIMPROVE. These tests short-circuit in that order.
3. If the Airbase-owner query returns an owner, show ALREADYAIR and return.

The Fortress test deliberately excludes an Airbase. Existing Fortress cannot
be upgraded to Airbase through this command.

### Fortress

1. Require Construction; otherwise show CONSTRUCTION with that advance.
2. Reject a city, then Ocean, then an existing Fortress without the City
   component, using CANTIMPROVE.
3. Query features again; if now a Fortress without the City component, show
   ALREADYFORT and return.

With unchanged state the second Fortress test is redundant: an existing one
already returned CANTIMPROVE. Preserve the repeated query and notice distinction.
An Airbase passes these tests and can be converted to Fortress on completion.

### Road or railroad

The single Build Road order chooses what to construct from tile state:

1. Reject a city or Ocean with CANTIMPROVE.
2. If the retained coordinate's tile has a river, require Bridge Building;
   otherwise show BRIDGEBUILDING with that advance. This precedes the next tests.
3. If Railroad is present, show ALREADYROAD.
4. If Road is present, require Railroad technology; otherwise show RAILROADS
   with that advance. An unroaded tile requires no Railroad technology.

There is no separate railroad work order. Road/rail state can change between
starting and finishing the job, and completion checks it afresh.

### Irrigate or mine

Read the requested outcome from the rule for captured terrain `t`. If it is
nonnegative (a terrain conversion), skip **all** checks in this subsection.
In particular, conversion does not require water and is not rejected merely
because this coordinate is a city.

For a negative outcome, reject NoResult with CANTIMPROVE. Then reject any city
with CANTIMPROVE. Other negative outcomes take the improve-in-place path:

- Mine: if Mine is present without Irrigation, show ALREADYMINING with the mine
  sprite, mode 0. Farmland is not an already-mined rejection.
- Irrigate: require the irrigation-source predicate above, otherwise NOWATER.
  Then reject Farmland with ALREADYFARMLAND. Then, if Irrigation is already
  present, require Refrigeration; otherwise show FARMLAND with that advance.

No Ocean test is added here. The outcome rule selects the conversion versus
improvement checks. In-place Irrigation replaces a Mine; in-place Mine replaces
Irrigation or Farmland, as the completion section specifies.

### Transform and pollution cleanup

Transform first rejects Ocean with CANTIMPROVE. It then requires the live type
to be the fixed Engineers slot, otherwise shows ENGINEERS with that unit sprite,
using mode 0 on a small screen or 8 on a large screen. It does **not** validate
the terrain's transformation result, require a further advance, or reject a
city. An Engineers slot whose rules no longer give it Settle role failed the
initial role check before reaching this point.

Clean Pollution rejects a tile without Pollution, showing NOPOLLUTION with the
pollution sprite, mode 8. It has no additional terrain or city gate.

### Accepted command

Advance-requirement notices use presentation mode 8. CANTIMPROVE, ALREADYAIR,
ALREADYFORT, ALREADYROAD, NOWATER and ALREADYFARMLAND use the unit-dialog entry.
The exact resource keys/mappings are in Appendix A; P04 owns their text.

After all applicable checks, redraw the captured coordinate in all views, then
call `doWork(u, requestedOrder)`. The redraw may change state. The retained unit
slot and full order argument survive it, but work rereads the unit's position,
owner and terrain. No counter reset, movement charge or order assignment is
performed by the command before that call. Existing progress can carry into a
different requested job. There is no command-local selectability, remaining-move,
identity-token or diplomacy recheck; P05/U04 own their invocation conditions.

## Work execution

`doWork(u, order)` can be reached immediately by a successful command or later
by U04's order executor. U04 dispatches its seven terrain-work orders, passes
the then-read full order and returns handled=1 even when work is incomplete.
AI/automation sets its chosen orders through A03 and reaches the same executor.
The core does not repeat player-command role, technology, terrain, water,
existing-improvement or city checks.

### Capture and required work

Capture actor slot, owner `c`, coordinates `p`, and terrain kind `t`. Read the
old full order and write the requested order with stored-order retention.
Calculate the required work `R` from captured terrain and live rule fields:

| Order | Required work |
| --- | --- |
| Build Fortress | `truncTowardZero(signedMoveCost(t) / 2) + 3` |
| Build Road | `signedMoveCost(t) * (4 if Road is present, else 2)`, then add 2 if the live tile has a river |
| Irrigate | Signed irrigation-work parameter of `t` |
| Mine | Signed mining-work parameter of `t` |
| Transform | Signed movement cost of `t` × unsigned cosmic transformation-time parameter |
| Clean Pollution | 4 |
| Build Airbase | 4 |

Retain signed32 arithmetic at each step. The Road presence query precedes the
separate river read; a city does not implicitly count as Road for the threshold.
No difficulty, government, unit damage, remaining-move fraction, Veteran status
or number-of-workers divisor modifies `R`.

Clear blink-active and exhaust the actor's movement using U02's operation.
This happens on both incomplete and completed work, before looking for a donor.
Exhaustion queries live unit/rule state; it is not replaced by subtracting a
fixed movement point. Retain the earlier `c`, `p`, `t`, old order and `R` across
that child and all subsequent callbacks.

### Transfer progress and advance the job

Starting from U01's stack head for the actor, traverse in physical stack order.
Select the **first** other member whose live type role is Settle and whose full
order equals the requested order. Do not require the same owner, same type or
positive progress. A zero-progress match ends the search just like any other.
Do not accumulate all workers in one scan.

If found, add that donor's stored counter to the actor's counter, retaining the
result modulo 256, then set the donor's counter to zero. Leave the donor's order,
moves, flags and other fields unchanged. The optional reconstruction rule that
cancels the donor's order is not classic behavior.

Increment the actor counter by 1 with modulo-256 retention. If the actor's live
type is the fixed Engineers slot, increment it again with the same retention.
This fixed-slot bonus is independent of the actor's role. Interpret the result
as signed8 and compare it to retained `R`.

Thus progress is stored on units and transferred between successive actors.
It is not a tile-owned pool, an average of the stack, or all matching counters
summed at once. Changing an order does not itself discard progress here. A
worker can receive a donor's credit even if that donor has already spent its
movement. Exhausting the actor does not exhaust the other workers.

### Incomplete work

If signed progress is below `R`, redraw an area of radius 0 at captured `p`
in all views, using the live local observer and refresh argument 1. Then, if
the actor's live full order differs from the captured old order, notify the
city window of that unit change with the no-old-position sentinel. Return.
There is no feature or known-map update, status redraw, or near-city refresh.
The redraw callback can affect whether the unit-change notification occurs.

### Completed work

Otherwise traverse again from the actor's current stack head. For every member
with live Settle role and full order equal to the requested order, clear its
counter and set its full order to None. Include the actor if it satisfies those
conditions, and do not require matching ownership. Each next-link traversal
occurs after the member's writes; do not replace it with a saved membership set.
A direct caller using a non-Settle actor can complete effects while retaining
that actor's order/progress because it does not qualify for this clearing loop.

Next apply the appropriate completion effects below, using retained `p`, `t`
and captured owner `c`. Then perform this tail even if the completion branch
made no feature change:

1. Remember the tile for the actor's **live owner**, using W03's overwrite with
   synchronization requested. Civilization 0 is skipped by that primitive.
2. Redraw radius 1 at captured `p` in all views, using the live local observer
   and refresh argument 1.
3. If the actor's live owner now equals the live local observer, redraw status
   with argument 1.
4. If its live order differs from captured old order, notify the city window
   of the unit change with no old position.
5. Call the city window's refresh-if-near operation with captured `p`.

Refresh-if-near requests recalculation/redraw with argument 0 only when the
window is open, neither redraw-suspended nor redraw-deferred, its selected city
is nonnegative, and that city's W01 weighted distance from `p` is less than 3.
P06/C03 own the actual recalculation and its effects. Do not substitute a redraw
without that child, or recalculate every city unconditionally.

All setters below request synchronization. W01/N02 own immediate versus batched
host/client handling. Work does not begin its own map batch. Feature writes,
known-map writes and presentation/network children can change later live reads.

## Completion effects

### Fortress and Airbase

For Fortress, if the live city-owner query is absent, set Fortress, then clear
the City component. These are two ordered mutations, converting an Airbase to
Fortress. If a city is present, perform neither mutation.

For Airbase, if the live city-owner query is absent, set both Fortress and City
components as one Airbase mutation. Otherwise do nothing. Other features and
terrain are retained. The core does not recheck Construction or Radio.

### Road and railroad

Query live features. If Road is absent and the subsequent city-owner query is
absent, set Road. Otherwise, if captured owner `c` has Railroad technology,
set both Road and Railroad. Without that advance do nothing. A city can reach
this latter branch through direct or queued work even though the player command
would reject it. Do not freeze “road versus railroad” when the job begins.

### Irrigation and mining

Read the requested result from the rule for retained source terrain `t`.
For **any negative result**, perform the in-place branch, even NoResult when
reached by a direct/queued call that bypassed the command:

- Irrigate: if Irrigation is absent, clear Mine then set Irrigation. Otherwise
  require Refrigeration for captured owner `c` before setting both Irrigation
  and Mine, which represents Farmland. Without that advance do nothing.
- Mine: clear Irrigation, then set Mine. This turns Farmland into a Mine.

For a nonnegative result, set terrain to that result using W01's terrain setter.
After the setter and its network callbacks return, **reread the source rule's
result** and retain it as destination index `d`. Do not derive `d` from the tile's
now-current terrain. Run the destination feature cleanup below.

### Transformation and destination cleanup

Transform sets terrain using the transformation result in the rule for retained
source terrain `t`. Reread that same rule's transformation result after the setter
returns and retain it as destination index `d`. Then run the same ordered cleanup
as irrigation/mining terrain conversion:

1. If destination rule `d` does not have ImproveInPlace as its irrigation result,
   and live tile features contain Irrigation, clear both Irrigation and Mine.
2. If destination rule `d` does not have ImproveInPlace as its mining result,
   and live tile features contain Mine **without** Irrigation, clear Mine.

The destination rule fields and features are read at their uses. A callback
between the two tests can change them. If Farmland survives the first test,
it does not qualify as a lone Mine in the second; do not clear its Mine component
merely because the destination cannot support a standalone Mine.

The initial terrain mutation and the cleanup can use **different** destination
values if a callback changes the source rule between its two reads. A tile
conversion does not add irrigation or mining automatically. Road, Railroad,
Fortress/Airbase, Pollution and other features are not cleared by this cleanup.

The transformation entry has no negative-result rejection. F03 accepts NoResult
and ImproveInPlace tokens for this field too, but they are not an implemented
“do nothing” or in-place Transform operation. A negative destination reaches the
terrain setter before a subsequent negative-index rule read. Preserve the
setter's valid effects according to W01 and then require explicitly supplied
read backing or stop before the unsupported read. A stricter input policy may
reject this job earlier; describe that policy as distinct from permissive classic
input handling. Never implement a resulting invalid write as state corruption.

### Pollution cleanup

If Pollution is currently present, decrement the global polluted-tile count,
retaining signed16 wrap. Then clear Pollution unconditionally. Do not clamp or
recount the map. For example, cleanup at count -32768 stores 32767; a repeated
cleanup of the now-clean tile does not decrement again.

Only the actor's live owner is explicitly remembered by the common completion
tail. Cleanup does not publish the new features to all seven normal civilizations
as [pollution placement](W05-pollution-and-global-warming.md#shared-pollution-placement)
does. W05 owns other pollution-count updates; W03 owns
known-feature overwrite semantics.

### Derived state

There is no direct region/border rebuild, fertility recalculation, path-cache
invalidation, ownership reassignment, general exploration or all-civilization
knowledge refresh in these completion branches. Use W01's primitive mutations
and the exact tail above. The city-window child may recalculate its selected
nearby city; that does not authorize a universal “terrain changed” repair pass.

## Pillage operation

The core `pillage(u, selection)` captures the unit's coordinates `p` and owner `c`.
Use C01's [weighted nearest-city query](C01-city-lifecycle-and-ownership.md#weighted-nearest-city)
with no owner, region or alternate-owner restriction. It can select a distant
city; there is no maximum distance or tile-claimant test. Equal distance selects
the higher slot according to the shared query.

If a city was found (including slot zero), its owner differs from captured `c`,
and captured `c` is human according to the live human mask, call V04 attack
confirmation for `c` versus that city owner with the Ceasefire/Peace/Alliance
relation set. Nonzero result cancels the core immediately, before tile changes,
redraw or movement exhaustion. It does not undo a caller's prior war declaration.

### Feature selection and removal

After confirmation, acquire the tile at captured `p` and retain its feature
reference. A specified positive feature selection clears the requested features
as one synchronized W01 mutation. Automatic selection (the original selector
is zero or negative) uses this priority, with exactly one clear operation:

1. If the unit's live type is Partisans and Road is present, clear Railroad if
   present, otherwise Road. This bypasses all priorities below.
2. If either Irrigation or Mine is present, clear Mine when present, otherwise
   Irrigation. Farmland therefore downgrades to Irrigation.
3. If the Fortress component is present, clear both Fortress and City components,
   removing Fortress or Airbase.
4. If Railroad is present, clear Railroad.
5. Otherwise clear Road, even when Road was already absent.

Do not clear Road together with Railroad automatically. The core does not check
attack strength, Land domain, city presence or whether a useful feature remains.
Specified feature sets bypass this automatic priority, including the Partisans
special case. W01 owns stored feature retention for off-nominal operands.

### Knowledge, diplomacy and movement tail

Query nearest city again, using the same captured `p` and unrestricted filters.
If a city is found:

1. Remember the tile for the unit's live owner.
2. Remember it for the selected city's live owner.
3. Call V04's treaty-flag operation for live unit owner toward live city owner,
   requesting War. This call also occurs when the owners are equal.
4. If those owners now differ, add an AI goal for the unit's live owner at `p`,
   role Attack and priority 4. [A01](A01-ai-strategy-and-assessment.md#current-goal-insertion) owns goal insertion
   and its downstream effects.

The two owner fields are reread after preceding children. The selected city slot
is retained through this tail; it is not a lifetime-guarded identity snapshot.
If no city exists, there is **no explicit remembered-feature update**, even for
the pillaging owner, and none of these diplomacy/goal calls.

Finally redraw radius 1 at `p` in all views, using live local observer and refresh
argument 1, then exhaust the unit's movement through U02. These run even if the
clear changed nothing. No unit-order or work-counter reset, blink clear, direct
pollution-counter update or status redraw is added. A custom specified selection
that clears Pollution does not thereby perform the Clean Pollution job.

## Player pillage command

Create the pillage dialog before reading the active unit. Initialize selected
feature operand to automatic. Capture unit, owner and coordinates. A zero attack
value or non-Land domain returns silently. Any nonzero attack value passes this
local test, including off-nominal negative signed values. If a city-owner query
succeeds, show CANTDO and return. Otherwise capture the tile's feature set. If
none of Irrigation, Mine, Road or Fortress components is present, show CANTDO
and return. A lone Railroad bit does not pass this command gate.

Load PILLAGEWHAT, then open PILLAGEMENU. If opening succeeds, consume its text
lines and construct choices from the **captured** feature set in this order:

| Text/choice | Include when | Explicit removal |
| --- | --- | --- |
| Farmland | Irrigation and Mine | Both Irrigation and Mine |
| Irrigation | Irrigation without Mine | Irrigation |
| Mine | Mine without Irrigation | Mine |
| Airbase | Fortress and City components | Both components |
| Fortress | Fortress without City component | Fortress |
| Railroad | Railroad present | Railroad |
| Road | Railroad absent and Road present | Road |

Read the first six text lines regardless of whether each row is added. Read the
seventh (Road) line only when Railroad is absent. If more than one choice exists,
show the modal dialog with argument 0; a negative result cancels. Otherwise use
its returned operand without rebuilding eligibility from changed state. For
zero or one choice, **do not show the modal dialog or substitute that option's
operand**: leave automatic selection. Failure to open the text section likewise
continues with automatic selection. The shared text reader is not explicitly
closed here; P04 owns resource/parser effects.

Consequently a tile with only Farmland downgrades to Irrigation, while explicitly
choosing Farmland when multiple choices exist removes both components. Modal
callbacks can change the unit, feature state or active-unit selection. Captured
unit slot/owner/coordinates and the chosen operand are retained by this wrapper.

After the dialog, query nearest city using the wrapper's captured coordinates.
Only when its slot is **strictly greater than zero**, its owner is nonnegative,
and that owner differs from captured unit owner:

1. Set movement options to prompt-only, preserving unrelated retained flags
   as mapped in Appendix A.
2. Confirm attack with the same Ceasefire/Peace/Alliance relation set. On nonzero
   result return without entering the core.
3. If the live relation from captured unit owner toward the selected opponent
   lacks War, call V04 declare-war with argument -1.

There is no human-player gate in this wrapper condition. City slot zero skips
it, but still qualifies in the core's separate confirmation. With a positive
foreign city slot and a human actor, two confirmation calls can occur. The core
performs a fresh city search and captures the unit's then-live coordinates/owner,
so a preceding dialog may make it pillage a different tile than the one used for
the wrapper's choices and diplomatic precheck.

Call `pillage(retainedUnit, selectedOperand)`. Destroy the dialog on normal
completion and every normal early return; P04/P05 own resource/UI and exception
lifecycle boundaries. Canceling after an earlier child does not roll back that
child's valid state changes.

## Off-nominal execution and conformance cases

Work parameters and progress comparisons use signed interpretations even when
their stored bytes have the high bit set. Ordinary byte wrap is required:
progress 127 plus 1 becomes -128 and can delay completion. Transfer also wraps
before the actor's increment(s). A positive threshold above 127 cannot be met
by this signed counter without a change to rules/state; do not widen the counter
or invent completion after that many turns. Zero/negative thresholds can
complete immediately depending on the signed accumulated value. Transform's
cosmic multiplier is unsigned while its movement-cost factor is signed.

Neither work nor pillage core performs an initial bounds check. W01 owns
off-map fallback-tile reads and mutations; W03 owns remembered-layer addressing.
A valid fallback object is not permission to write outside a known-map array.
Retain established fallback effects and stop before any unsupported read or
excluded invalid write, without clipping to an invented replacement coordinate.

Outside the seven work orders, the core starts with threshold 1, still writes
the retained requested order, exhausts movement, transfers/increments counters
and performs its incomplete/complete tail. No completion feature case runs.
Full order comparisons use the full argument against signed stored orders, so
narrowing a large argument does not make its stack comparisons match. This is a
direct-call compatibility boundary, not another intended player job.

Test independent expected mutations and ordered child calls, including:

- Q neighbors versus excluded D neighbors, the center itself and wrapped edges.
- Each command gate and notice order; conversion bypassing water/city gates;
  Fortress/Airbase asymmetry; command redraw relocating or changing the actor.
- First donor versus sum-of-stack, a zero donor, mixed owners, retained progress
  after changing jobs, Engineers fixed-slot bonus and all signed progress values.
- Every work threshold, signed division toward zero, river addition, completion
  clearing before tile mutation, non-Settle direct actor, live next links.
- Captured source terrain/owner versus live rule results and owner knowledge;
  destination cleanup with Mine/Irrigation/Farmland; callback retargeting between
  setter and cleanup; count wrap; unsupported negative-destination reads after
  prior valid mutations. Invalid-write corruption is bug evidence only.
- Every automatic pillage priority, specified selection, no-op removal, no city,
  equal-distance cities, city zero, two confirmations, callback changes and
  no movement exhaustion on core cancellation.
- One-option automatic choice versus modal explicit Farmland removal, missing
  text section, dialog cancellation/destruction and remembered-feature recipients.

Keep whole-turn worker activation, automated job choice, multiplayer receipt,
real war/senate decisions, city recalculation and UI traces as separate joins.
A passing arithmetic/adapter case does not prove those paths.

## Appendix A. Original representation and source mapping

Logical order names and features above do not require the same private storage.
The following supports comparison with original snapshots and callbacks.

| Item | Mapping |
| --- | --- |
| Entry addresses | Work `004c42a0`, pillage core `004c50d0`, order dispatcher `004c5408`, irrigation source `0058c56c`, work command `0058c65e`, pillage command `0058cfcd`. |
| Work orders | Fortress 4, Road 5, Irrigate 6, Mine 7, Transform 8, Clean Pollution 9, Airbase 10. None is stored `ff` and read as -1. |
| Type/role | Engineers slot 1, Partisans slot 9, Settle role 5; other properties come from live rules. X17 worker-handover cancellation is excluded. |
| Arithmetic | Work progress stored byte, signed8 comparison; signed movement/work-rule bytes; unsigned cosmic transform-time byte; pollution count signed16. Required-work intermediates signed32. |
| Terrain results | NoResult -1, ImproveInPlace -2; irrigation/mining fields at offsets 14/15 and transform at 13 in a 24-byte terrain rule. Negative conversion-result reads can reach preceding advance-rule storage; only explicitly supplied read backing can interpret it. |
| Features | Irrigation `04`, Mine `08`, Farmland `0c`, Road `10`, Railroad `20`, Fortress `40`, Airbase `42`, Pollution `80` hex. City component `02`. Primitive terrain setter behavior remains in W01. |
| Dialog options | Farmland `0c`, Irrigation `04`, Mine `08`, Airbase `42`, Fortress `40`, Railroad `20`, Road `10`; positive operands clear their retained low-byte components. Nonpositive operands select automatic behavior. |
| Required advances | Bridge Building 7, Construction 18, Radio 66, Railroad 67, Refrigeration 70. |
| Diplomacy | Confirmation relation set `0e` = Ceasefire/Peace/Alliance. Requested War `2000` hex; goal role Attack=0, priority=4; declare-war third argument -1. |
| Window/movement | No-old-position `(-99,-99)`; pillage-dialog construct flags `4000` hex; prompt-only low movement-flags byte 1. Refresh-if-near `0050c6ef`. |
| Notices | ONLYSETTLERS, RADIO, CONSTRUCTION, BRIDGEBUILDING, RAILROADS, CANTIMPROVE, ALREADYAIR, ALREADYFORT, ALREADYROAD, ALREADYMINING, NOWATER, ALREADYFARMLAND, FARMLAND, ENGINEERS, NOPOLLUTION, CANTDO, PILLAGEWHAT, PILLAGEMENU. Repeated CANTIMPROVE/CANTDO sites share resource names. |

Original pillage choice count is a full integer in the independent recovered
source, while the primary reconstruction uses a byte. At most three choices
can coexist for a captured feature byte, so this representation difference does
not change the branch or loop results. Do not infer a new unbounded menu from it.

## Appendix B. Evidence, validation and remaining joins

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`, inspected in reference checkout
`fc2155ccdb2123c1137babab6fa39e38152278c9`. The relevant unit/map/city/AI/menu
sources are unchanged between these revisions. Independent source is
`civ2` at `4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`.
Neither reference tree was modified.

| Family | Primary evidence |
| --- | --- |
| Work, pillage and commands | [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp): `unit_do_terrain_work` and its threshold/completion children, `unit_pillage`, `unit_cmd_terraform`, `unit_cmd_pillage`, `unit_execute_orders`. |
| Water predicate, tile setter and keyboard callers | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp): `tile_has_irrigation_source`, `tile_set_terrain` `005b9646`, keyboard dispatch. W01/W03 own the shared mutation/memory primitives. |
| Nearest city and city-window tail | [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp): `city_find_nearest`, `CityWindow::refresh_if_near`. C01 owns the shared nearest-city contract. |
| AI and menu callers | [ai.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/ai.cpp), [window.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/window.cpp): direct automatic pillage and work-order selection; `menu_cmd_terraform`. |
| Definitions | [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h), [civilization/cosmic state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/civ.h). |

Independent `src/unit_orders.cpp`, `src/map_input.cpp` and `src/map_yield.cpp`
corroborate worker transfer, signed threshold/counter arithmetic, live destination
rereads, conversion cleanup, pillage priority, one-option automatic selection,
city-zero distinction and dialog cleanup. `tests/saved_postload_fixture.inc`
contains focused work-dispatch cases, including signed move cost, counter wrap,
first donor, Engineers, negative destination backing and pollution decrement.
Those independent native fixtures were read, not rerun here.

A temporary native harness extracts **10 additional routines**: work threshold,
completion, work core, pillage core, both command handlers, U04's order dispatcher,
irrigation-source query, terrain setter and weighted nearest-city query. It reuses
**75 extracted unit/combat/map routines** from earlier slices. Optimized and
unoptimized AddressSanitizer/UndefinedBehaviorSanitizer builds pass **7,891
assertions**, with signed-char/wrap semantics and strict-alias optimization
disabled. Leak detection is disabled for the ptrace environment. Extraction
counts do not claim coverage of every inherited function or branch.

Checks exercise all 256 movement-cost bytes with Road/river combinations,
Fortress division and Transform multiplier, all irrigation/mining-work bytes,
and all 256 stored progress values for both ordinary workers and Engineers.
They join the real order dispatcher, work implementation, U02 exhaustion and
U01 stack operations. Additional cases cover first zero donor, mixed owners,
complete stack resets, retained credit after changing jobs, non-Settle direct
execution, each completion kind, destination cleanup combinations, pollution
count wrap, live destination-rule change after the terrain setter, Q versus D
water sources, command rejection and command redraw relocating/changing owner.

All 256 feature bytes exercise automatic pillage for ordinary and Partisans
types. Command cases cover sole-option versus selected Farmland, cancellation,
text-section failure, lone Railroad rejection, city-zero versus positive-city
confirmation counts, and relocation during modal input. Explicitly off-nominal
feature combinations are included as backed fixture state, not asserted to be
reachable from stock play.

The harness executes actual tile/remembered-feature setters, terrain mutation,
nearest-city selection and unit work code. Map access/predicates, advance/wonder
queries, dialogs/text, city-window/status/redraw, diplomacy, goal insertion and
network transport are declared adapters. The wrapper's war check and core's
confirmation are exercised against supplied replies; V04's senate/war and A01's
goal algorithms are not implemented by these adapters. City-window callbacks
are recorded without actual city recalculation. The unrelated Fortify/Goto
branches of the extracted order dispatcher are supplied, not tested here.
Selected callbacks mutate unit position/owner or conversion rules; they do not
prove arbitrary backing/lifetime reuse or UI reentrancy. Negative-destination
read paths were inspected in both sources and independent fixture definitions,
not executed as unsafe accesses by this harness.

No original executable run, fresh instruction export or saved annotation change
was performed for W04. X02 must retain independent state/input/trace fixtures and
join whole-turn U04 activation, A03 automated work, full command dispatch, real
V04/A01 diplomacy/goals, P06/C03 recalculation, and N02/N05 synchronization/receipt.
P04/P05 must exercise authored content, accepted/cancelled choices and required
gameplay/network effects; native resource/dialog lifecycles are optional reference
presentation under X01. The
negative-destination read boundary needs original/supplied backing cases distinct
from excluded invalid-write bugs. The finite checks leave these obligations open.
