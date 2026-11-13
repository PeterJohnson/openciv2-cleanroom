# C09 — City capture and revolt

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

Capture, building/population loss, supported-unit changes, events and diplomacy
retain their partial publications and caller order. Matching the final owner
alone is insufficient, including when a callback deletes or replaces a slot.

## Target, scope and dependencies

The target is **Civilization II Multiplayer Gold Edition 5.4.0f, patch 3**.
The validation limits are stated below. This document
owns conquest/capture aftermath, the incited-transfer wrapper, treasury-share
calculation, central-city selection for Palace replacement, population/building
losses, supported-unit consequences, production repair and partisan creation.

[U02](U02-movement-and-zones-of-control.md) owns entering a foreign city;
[U06](U06-combat-and-veterancy.md) owns combat and casualty consequences.
[V06](V06-espionage-and-bribery.md#8-inciting-a-revolt) owns espionage eligibility,
price/payment, escape and the decision to incite.
It passes its accepted choice to the wrapper here. [C01](C01-city-lifecycle-and-ownership.md)
owns deletion and elementary civil-war ownership transfer; this capture sequence
is **not** that elementary transfer. [V01](V01-civilization-lifecycle-and-civil-war.md)
owns civil war, civilization-elimination checks and their own mutations. V03 owns capture technology theft. The exact
positions of those calls below are part of C09's contract.

[C06](C06-city-production-and-purchasing.md) supplies build eligibility and
production selection; [C08](C08-buildings-and-wonders.md) supplies building and
active-wonder queries, setters, Eiffel acquisition and Leonardo upgrades.
[U01](U01-unit-lifecycle-and-stacks.md) owns unit creation/deletion, stack queries
and visibility. [W01](W01-map-geometry-and-spatial-state.md) supplies geometry,
B[0…20] city radius, D[0…7] neighbors, regions and tile mutation;
[W03](W03-visibility-and-exploration.md#remember-and-reveal-a-city) owns city
knowledge publication. [S02](S02-event-language-and-execution.md#11-caller-hooks-and-persistence)
owns city-taken events, and P04/P06/P10/N02/N05 own
real presentation, network receipt and transport. These child operations are
required contracts, not assumed-success no-ops.

## Inputs, arithmetic and observation boundaries

Write `K` for the selected city slot, `N` for the incoming new owner, `O` for
its captured old owner, `P=(x,y)` for its captured coordinates and `m` for mode.
Nominal owners are 0…7, with 0 Barbarian. Live unit types are 0…61, ordinary
buildings use C08's identities, and initialized cities have a terminated name,
positive size and a supported C06 production item. Rules, maps, ranks, treaty
state, network mappings and presentation arguments are explicit inputs.

Modes used by the callers are:

| Mode | Entry and specific differences |
| --- | --- |
| 0 | Conquest, including U02 city entry and related U06 consequences. Force war; nonfounder acquisition applies alternating building loss and invokes the population-loss helper even at size one. |
| 1 | Ordinary incitement. The wrapper first transfers nearby old-owner units. Force war, but no alternating building-loss draw. |
| 2 | Paid subversion without the ordinary incident. V06 sets the city's founder to N before the wrapper, so the parent normally treats this as restoration. No force-war call or alternating loss. |

The parent itself only tests `m<2`, `m==0` and `m!=0`; it does not validate this
enumeration. Do not add mode-specific gates to plunder, technology theft,
Palace handling or population loss beyond those specified below.

[F01](F01-determinism-and-arithmetic.md) defines full-width operations, division,
clamps, masked civilization shifts and the two sampling laws. F01 defines
reference expression results unless a more specific conversion is stated.
City owner, founder, size and production are signed 8-bit values. Size changes
wrap modulo 256 before the signed next read; capture age retains the low byte
of the turn. Unit home is compared as an **unsigned** identity; assigning a city
retains its low byte. Per-type counts wrap modulo 256. Civilization population
and city totals used here are signed 16-bit inputs. Treasury is signed 32-bit.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

All direct random calls in C09 use F01's **primary** sampling law, whose results
are 0…32767. The alternating-building draw, exceptional size-one draw and each
successful partisan's Veteran draw are distinct draws. None is a secondary draw.
Invoked children, notably technology theft, production selection, unit creation
or presentation, have their own documented random consumption; do not count a
C09 direct-draw total as the whole enclosing turn's consumption.

Captured variables below remain captured across calls; other fields are read
live at their specified step. Do not add final eligibility checks after a dialog
or event. Earlier effects remain if a later child stops or fails. An unavailable
read requires supplied original input/backing or a bounded stop. Out-of-bounds
writes to names, per-type counters, visibility slots or unit/city records are
**excluded bugs** under [X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
even when adjacent storage is mapped. Safe rejection/containment is separate
from in-bounds scalar wrap and from read-boundary behavior.

## Shared treasury share

This query is also used by [W06](W06-huts-and-barbarians.md) for uprising probability
and naval target scoring. It does not debit gold or clamp against affordability.
Capture signed owner `O`. First compute `threshold = trunc(32000 / signedSize)`;
this division occurs **before** reading treasury. Then compare live gold with
threshold and evaluate the selected branch:

```text
if gold < threshold:
    share = trunc(signed32(signedSize * gold) / signed32(totalCitySize + 1))
else:
    share = signed32(trunc(gold / signed32(totalCitySize + 1)) * signedSize)
if share < 0: share = 32000
return share
```

The selected branch reads its operands afresh; there are no child callbacks or
random draws. The first branch reads size, gold, then totalCitySize; the second
reads totalCitySize, gold, then size. The branch changes rounding order. Positive
results are not capped at 32000. Size zero, a zero denominator or signed division
overflow uses F01's fault/bounded-stop contract, not a fabricated zero share.
For size 3, totalCitySize 10 and gold 100, share is 27. At gold 10667 the other
branch returns 2907. A negative computed share becomes 32000.

## Shared central-city selection

`centralCity(owner)` begins with bestTotal=32767 and result=None. Scan live
cities in ascending slot order through the **live** upper bound. For each city
whose owner equals the full argument, sum W01 weighted diagonal distance from
that city to every live city of the same owner, including itself, in ascending
slot order. Add with signed32 wrap. Retain a candidate only when total is
**strictly less** than bestTotal, so ties retain the earlier slot. Return None
if no candidate beats 32767. There is no size, Palace, region or human filter.
V01 also uses this query; the capture caller temporarily excludes its current
city by changing its owner before invoking it.

## Incited-transfer wrapper

`inciteTransfer(K,N,reason)` first captures the city's old owner byte, its signed
interpretation `O`, and x/y. A negative N returns after those reads, with no map
or unit changes. Otherwise:

1. Begin a tile-update batch. Scan B[0…20] around captured P, wrapping x once.
   At each in-bounds position, add N to exploration with synchronization and
   update N's remembered tile. Finish the batch.
2. Outside autoplay, show CivilWar if any of these **short-circuit** tests succeeds,
   in this order: N currently human; captured old-owner byte currently human;
   local civilization has Embassy toward N; local owns United Nations; local
   owns Marco Polo; local has Embassy toward O; local owns United Nations again;
   local owns Marco Polo again; full map reveal. Do not deduplicate the repeated
   wonder queries.
3. In that notification branch, if full reveal, current local-civ knowledge of
   K, or current K owner matching the local byte permits it, ensure K's **live**
   coordinates visible for its live owner. Set text args 0=O plural name,
   1=live city name, 2=N adjective; show CivilWar. After the popup, if the captured
   old owner is currently human and mode is network, send its remote CivilWar
   text request. This send has no local-recipient exclusion.
4. Capture the current unit upper bound and scan downward. For each live O unit,
   compute weighted diagonal distance from its live coordinates to captured P.
   Admit distance<2 only if distance=0 or its current tile has no city owner.
   An adjacent unit inside any city does not transfer.
5. For each admitted unit: decrement O's live-type active count; increment N's
   live-type active count; assign owner=N, home=K's low byte and movement spent=0.
   Preserve Fortifying/Fortified; replace any other order by None. Set its live
   tile owner to N with synchronization. If the retained distance was nonzero,
   redraw its live coordinates. No generic U01 owner-transfer operation or role/
   region-count repair is inserted.
6. Call the capture parent with the same K, N and reason. The parent captures
   owner/coordinates again after this wrapper's callbacks.

A successful V06 incitement has already charged gold and performed its spy
escape/incident steps. The wrapper neither recharges nor rechecks affordability.
Its transferred units can avoid the parent's old-owner supported-unit deletion
because their owner has already changed.

## Ordered capture parent

Construct the capture dialog context, then capture O and P. If m<2, invoke V04
force-war `(N,O)` and wait for it to return. Capture `restored = (live founder == N)`
after that call, then copy the current city name for later notices. The name is
retained even if later callbacks rename the city. Do not require the original
80-byte local's overflow; use safe text storage or a declared stop.

Execute these phases in order:

### Diplomatic flags and civil-war attempt

1. If N or O is currently human, test `(!shipLaunched(O) && !shipLaunched(N)) ||
   difficulty<Prince`, in that order. A launched old-owner ship skips the
   new-owner ship query; the difficulty check follows the ship expression.
   When true, invoke the shared treaty setter for Incident `(N,O)`.
2. In that branch, set **O→N CityCaptured** if N is currently human, Democratic,
   and powerRank[N]=7. Otherwise, if N is currently human and rank[O]<rank[N],
   count other live cities whose founder is O and owner N, in ascending order.
   Set O→N CityCaptured when that count is greater than one and even. The current
   selected city is excluded. No rank flag is cleared when the condition fails.
3. Invoke the shared treaty clearer for Aggressor `(N,O)` unconditionally.
4. If O is currently AI, K's live signed size>14 and restored=false, set
   **O→N Vendetta** directly.
5. Set replacementCity=None. If O is AI, K has Palace and O's stored city count>4,
   calculate `weaker`: for a human N, rank[N]<rank[O]; otherwise
   rank[weakestHumanSelector]<rank[O]. The selector is used as a civilization
   index exactly as stored; do not replace it by a freshly calculated minimum.
6. If weaker, call V01 civil war for O. Only when its result is zero, temporarily
   assign K owner=None, call `centralCity(O)`, retain its result, then restore
   K owner=O. A stopped child does not imply early restoration/rollback.

The shared V04 setter/clearer can affect both treaty directions; the direct
CityCaptured/Vendetta writes above affect only the stated direction. The V01
child may change cities or civilizations before the later capture phases.

### Palace, wonder notices and plunder

1. Compute and retain `plunder = treasuryShare(K)` using the state at this point.
2. If K currently has Palace, run the Palace operation below with retained
   replacementCity. It can spend O's gold before plunder is finally clamped.
3. If K's live production is a wonder and autoplay is off, run the abandoned-
   wonder notice below. This branch itself does not reset production.
4. If either participant is currently human and autoplay is off, run the built-
   wonder notice pass below. Eiffel acquisition occurs **inside this gate**, so
   AI-only capture or autoplay can skip it. The pass still precedes the city
   owner assignment; do not move Eiffel to an unconditional post-transfer hook.
5. Clamp retained plunder with F01 `clamp(plunder,0,live O.gold)`. Debit O's live
   treasury; if N is not Barbarian, credit N's then-live treasury. Both stores
   wrap signed32. Preserve debit-before-credit even if off-nominal owners alias.
   The clamp's bound order, including a negative upper bound, is F01's contract.
6. If the local human is N or O, redraw status.

### City reset, building loss and population

1. Set K's shield stock to zero. Clear Disorder, Celebrating, TechStolen,
   Autobuild and DefenderLost; preserve the other flags, including SoldThisTurn.
2. If K's **live** production is a unit identity, decrement O's corresponding
   production count modulo 256. An invalid counter destination is an excluded
   overwrite, not a requirement to corrupt neighboring civilization state.
3. If restored=false, clear Palace, Temple, Cathedral and Courthouse in that
   order through C08's elementary setter.
4. If m=0 and restored=false, consume one primary draw. An even result keeps
   only installed ordinary buildings with odd identity; an odd result keeps
   only those with even identity. Apply this same choice across the whole city,
   after the four removals. This is one shared parity decision, not independent
   per-building random survival. Appendix A maps the serialized presence bytes.
5. Invoke the population helper if `(m=0 && !restored) || live size>1`.
   That helper can delete the city and replace local K by None.
6. If K remains nonnegative, perform tile takeover and the city-taken event.
7. Always set the **captured center P** tile owner to N with synchronization,
   even when the city was deleted. If local human is N or O, play Barracks sound.

Food stock, name, ordinary route records and other fields are not cleared by
this reset; child deletion or subsequent window/recalculation can change them
under their own contracts. Capture does not call C01's elementary transfer to
adjust city/population totals; preserve the actual calls and later refresh
boundaries rather than adding a second bookkeeping transfer.

### Announcements, units and finalization

1. Set text arg 0 to the cached city name. Set args 3/4 to the restoration or
   capture wording selected by retained restored. Update remembered center P
   for N, then O.
2. In live network mode, send state difference, broadcast InfoStatus, then flush
   with 5000 timeout. Do not retest mode within this entered three-call group.
3. Run the capture-announcement operation below, passing K (possibly None), N,
   O, P and retained/clamped plunder.
4. Unless both scenario mode and the scenario NoCaptureTechnology flag are set,
   call V03 capture-advance theft `(N,O)`. This occurs even if K was deleted.
5. If K remains nonnegative, run the supported-unit and production continuation.
6. Call V01 civilization-destroyed check `(O,N)`. Only when it returns zero,
   K remains nonnegative and restored=false, consider partisans. Admit that
   operation if O's current government is Communism or Democracy, or global
   Guerrilla Warfare first-discoverer is nonzero, or O knows Communism. Preserve
   that short-circuit order; the partisan operation has additional gates.
7. If K remains nonnegative, assign founder=O, then capture age=low byte of the
   **live** turn. This replaces founder even after restoration. Query active
   Leonardo location; if it equals K, invoke C08's upgrade for N.
8. In live network mode repeat state difference, InfoStatus and flush, then
   destroy the dialog context. No rollback of previous state changes is added.

K is a retained slot/sentinel, not a token that is revalidated after every child.
A city-taken event or dialog can mutate a record before later operations.
A cleanroom implementation may reject unsupported callback states, but must not
claim the original added liveness checks. No invalid record write is required.

## Palace escape or loss

This operation runs only after the parent's Palace-presence query succeeds.
Start bestScore=−999, bestCity=None. Scan live old-owner cities ascending,
excluding K. Admit a candidate if its signed size≥trunc(K's live size/2) and>7.
Capture candidate size, then compute:

```text
score = signed32(3*capturedSize - weightedDistance(candidate,P))
score += 4 * U01 stackCount(topUnitAt(candidate))
if candidate has SAM Battery: score = trunc(signed32(score*3)/2)
if candidate has City Walls or O owns Great Wall: score *= 2
if candidate has SDI Defense: score *= 3
if candidate region != region(P): score = trunc(score/2)
```

Apply signed32 wrap at each operation. Query Walls before Great Wall and skip
the wonder query if Walls exists. Stack count is U01's whole-stack count, not
defender count. Compare regions after facility queries. Strict improvement wins;
equal scores keep the earlier candidate.

Initialize escapeDecision=0. Offer/consider payment only if live O.gold≥1000,
**bestCity>0** and replacementCity<0. A winning slot 0 is deliberately excluded;
do not substitute a second-best nonzero city. Supply text args 1=bestCity name,
2=O adjective, numeric0=1000, then choose one branch:

- If O is human, local human=O and autoplay off, invoke CanEscape and retain its
  exact returned decision.
- Otherwise, if O is human, network mode and autoplay off, set shared reply=−1,
  send CanEscape to O, capture ticks, and poll messages while signed32 elapsed
  ticks<3600 **and** reply remains −1, testing time before reply each iteration.
  A received reply merely bypasses fallback: **its value is never assigned to
  escapeDecision**. When reply is still −1 after the loop, choose 1 if O has a
  spaceship, city count>11, or gold>1999, in that order. This timeout fallback
  has no difficulty test. Unknown clock/recipient input is a boundary, not a
  fabricated timeout or acceptance.
- Otherwise choose 1 only if difficulty>Prince and the same spaceship/count/gold
  expression succeeds. This includes autoplay and appropriate AI/local modes.

Only decision **exactly 1** purchases escape. Debit 1000 from O's fresh gold,
without an affordability recheck, then set Palace at the retained candidate.
If O is AI, mode is nonnetwork, or local human=O, show Escape with that city;
otherwise send the remote Escape notice. This result notice is not suppressed
by autoplay. If O is currently AI, request C06 production choice 99 for bestCity.
The operation does not itself publish a capital-coordinate hint.

For any other decision, invoke [V07 spaceship-lost notification/reset](V07-spaceship.md#8-race-arrival-reset-and-loss) for O.
If replacementCity is nonnegative, directly set that city's production to Palace;
do not substitute C06's setter or repair counts at this step. Original gold,
slot-0 and received-reply quirks are distinct from excluded memory bugs.

## Wonder notices during capture

### Abandoned construction

Capture the magnitude of K's production as the improvement identity. Count
other live O cities with production equal to K's **live** production in an
ascending scan. Set text1=O plural name, text2=improvement name, numeric0=count.

If count=0, load AbandonWonder and, in network mode, broadcast its request.
Otherwise load StillWonder1 or StillWonder2 using `clamp(count,1,2)`. For the
clamped value 1, send the single-remaining notice: the original emits one text
kind when nonnetwork and a different kind when network. For value 2 it sends
neither. Append the improvement sprite, set zoom 8 and show the modal in all
these branches. The parent already gated autoplay; there is no ownership or
local-human filter here. Appendix A records text kinds and argument counts.

### Already built wonders

Scan wonder identities in C08 order. For each whose **active** location equals K,
set text1=its improvement name, clear the temporary text, optionally append the
scope wording and space, then publish that text as arg2. Scope wording is used
for Pyramids through Great Wall inclusive, Statue of Liberty, Eiffel, and
Manhattan through Cure for Cancer inclusive; others use empty text.

If O is human, network mode and local human≠O, send LostWonder to O. Then if
local human=N and autoplay off, show CaptureWonder; otherwise if local human=O
and autoplay off show LostWonder. Each local notice uses its improvement sprite
and zoom 8. For Eiffel, invoke C08's acquisition operation **after** these
boundaries, regardless of whether a local dialog was shown. Only active wonders
are visited: expired wonders are absent from these notices and Eiffel hook.

## Population loss and possible deletion

Start humanOnRegion=false. Only for Barbarian N, capture region(P), then scan
civilizations 1…7 ascending. For each currently human civilization, read its
stored city-count byte for that region; any nonzero value sets true. Finish the
scan; do not filter alive state or stop after the first match.

The exceptional size-one preservation branch requires **all** of:

1. K's signed size<2;
2. either both N and O are AI, or N=Barbarian and O human;
3. N is non-Barbarian or humanOnRegion=true;
4. no bit of current humanPlayers intersects K's known-to set;
5. powerRank[N]≠7.

If any condition fails, decrement size modulo 256. If all hold, decrement only
when rank[weakestHumanSelector]<rank[N] **and** one primary draw is odd. If that
rank comparison fails, consume no draw and keep size. If the rank comparison
passes and the draw is even, keep size.

After either branch, if resulting stored size is exactly zero, call C01 deletion
with flag 0, then replace the parent's local K by None. Do not test `size<=0`
instead. Off-nominal negative sizes can survive or wrap; size one is normally
preserved for non-conquest/restoration callers because the parent may skip this
helper entirely.

## Tile takeover and event

For a surviving K, set N.lastCityTurn from the current turn, then clear N's
standing Settler goals within distance 4 of captured P. Begin a tile batch.
For each in-bounds B[0…20] around P, set radius claimant=N with synchronization.
For Barbarian N also add Barbarian exploration. Finish the batch.

Assign K owner=N, then W03-reveal K to **O**, updating O's known membership and
remembered size from the then-live city. If scenario events are loaded, invoke
CityTaken with K's live name, N and O. This is the event boundary; the later
center tile-owner write and announcements still use captured P. Do not reveal
the whole map or all other civilizations as part of this step.

## Capture announcement

The parent's cached name and wording arguments are already installed. This
operation does not generally reset all text/numeric arguments.

If either participant is human and autoplay is off, set text1=N plural name and
numeric0=plunder. If local human is N or O, ensure P visible for N, redraw P and
show CityCapture with K, city picture, zoom and N. Then separately test remote
human O and remote human N in that order:

- Remote O receives CityWindowDeleted(K), CityWindowRedraw, PossiblyCenter(P,N),
  DrawSquare(P), then CityCapture text.
- Remote N's branch sends the **first two window messages to O's mapped player**,
  then PossiblyCenter, DrawSquare and text to N's player. Preserve that routing
  quirk. Each branch's outer guard is human membership, network mode and local
  human differing from its participant; no regating is inserted between sends.

Otherwise, autoplay suppresses the remainder. Outside autoplay with both
participants AI, visit civilizations 1…7 ascending, selecting alive humans.
For each viewer, redraw P if local; otherwise send DrawSquare to that viewer
only in network mode. Then choose the first applicable notice:

1. Winner-ally if viewer→N Alliance, or `(N→viewer Mercenary && N→O Aggressor)`.
2. Loser-ally if viewer→O Alliance, or `(O→viewer Mercenary && O→N Aggressor)`.
3. General capture if viewer has Embassy toward N, owns United Nations, owns
   Marco Polo, has Embassy toward O, owns United Nations again, or owns Marco
   Polo again, evaluated in that short-circuit order.
4. Otherwise no text notice for that viewer.

Winner-ally and general capture set text1=N plural name and text2=O adjective;
loser-ally uses O plural name for text2. Show the respective local city-picture
popup **or**, only for a nonlocal viewer in network mode, send the remote text.
Do not send to the local viewer after its popup. The general/ally path does not
assign numeric0=plunder: prior numeric argument contents, including those set by
Palace/wonder dialogs, remain inputs to its requested text argument bundle.

K may be None after deletion. Passing that sentinel to the picture/network child
is the original caller contract; unavailable downstream record reads require an
explicit boundary, not an unconditional invalid record access.

## Supported units and production continuation

For surviving K, capture `unitUpperBound−1` and scan downward. Select live units
whose signed owner=O and **unsigned home identity equals full K**.

- Fixed Partisans type: assign home=None and keep it.
- Other types: if O is AI, query the exact city at the unit's live coordinates.
  If it is nonnegative and different from K, then a Defend-role unit with exactly
  one Defend-role unit in its U01 stack is rehomed there and retained. There is
  no additional same-owner check on that destination city.
- If that other-city test succeeded but the unit was not retained, mark the
  other city DefenderLost. Destroy the selected unit with U01 visible deletion,
  flag 1. After return, reload the current unit upper bound and restart downward
  from its last slot. Deletion/cargo side effects can change this bound.

Next set K Disorder if N is human, either participant's current government is
Republic or Democracy, and difficulty is not Chieftain. This uses the original
numeric government>Fundamentalism comparison; C09 does not recompute happiness.

### AI new owner

Assign production=Settlers. For Barbarian N, assign Legion instead, then replace
that with the top unit's live type at captured P if any exists. Increment N's
corresponding production count modulo 256, then request C06 production choice
99. Invalid per-type-counter writes are excluded bugs. No human city-window
continuation is added to this branch.

### Human new owner

Redraw neighbors at K's live coordinates. If its current production is a unit,
query C06 unit eligibility. If that succeeds (or the initial item was negative)
and its **then-live** production is negative, query C06 improvement eligibility
for the live magnitude. An initial failed unit query goes directly to fallback;
do not reroute it based on a callback changing production afterward.

If the retained eligibility result is false, set production=Settlers and
bestScore=0. Scan candidate types **62 down to 0**, querying role first. For the
first Defend candidate passing C06 unit eligibility, calculate:

```text
score = trunc(signed32(signedDefense * 8) / signedCost)
if candidate has double-versus-horse: score += 1
if score > bestScore: assign production=candidate
break
```

The loop exits after the **first eligible defender**, even when its score is
nonpositive. It does not find the globally best defense/cost ratio. Zero cost
and signed division overflow retain F01's division boundary. Nominal rules have
62 types; candidate 62 reads an adjacent original role value, described below.
This direct production assignment does not perform C06 stock/count adjustments.

Begin a tile batch. Scan D[0…7] around captured P; if in bounds and N has not
explored that tile, add its exploration and redraw. Finish the batch. Set shared
CityProcessing=true, begin the city-window notice scope, open K, end the scope,
then set CityProcessing=false. It remains true if a child stops before that last
store. Opening the real window is a separate P06 integration boundary.

### Candidate 62 read

The extra candidate is an original **read**, not a 63rd nominal unit rule.
Evidence from the independent reconstruction's producer trace establishes that
its role byte aliases an initialized scenario-event dialog prefix and is 0x19
in the normal application, so it fails Defend without calling build eligibility.
An implementer can represent this nominal extra probe as that declared input;
there is no need to reproduce the memory adjacency. An extended input domain
that permits the byte to differ must supply its actual value and any reached
rule/eligibility reads, or stop. Do not silently assume a usable extra unit or
allow an out-of-range per-type write. Appendix A records the evidence and limits.

## Partisans

This operation runs only after the parent's survival/technology/government gate.
Capture the absolute government difference `G` and power-rank difference `R`
between O and N. These use nonnegative logical values; admitted extra codes
use the `u8` view before taking the differences.
Using the city's **current post-loss signed size**, calculate with signed32 wrap:

```text
count = trunc(signed32(trunc((size+5)/8) * (G+R+1)) / 2)
if mode != 0: count = trunc(count/2)
if O lacks Conscription: count -= 1
if O lacks Guerrilla Warfare:
    if O lacks Communism: count = 0
    if O lacks Gunpowder: count = 0
else:
    if N lacks Guerrilla Warfare: count *= 2
    else: count += 1
veteranThreshold = max(0, N.reputation - 4 + (O→N Vendetta ? 2 : 0))
```

Both Communism and Gunpowder queries occur in the no-Guerrilla branch, even if
the first already sets count=0. Do not clamp negative count to positive. Retain
the computed count and Veteran threshold throughout the attempt loop.

For each attempt index from zero while index<count:

1. Start bestScore=0. Scan B[0…19] around captured P in order, wrapping x.
   Admit only in-bounds non-Ocean tiles whose city-or-unit owner query is
   negative. This is the owner query's exact result, not a separate feature
   or stack-empty test.
2. Start score=2×signed terrain defense. Query tile features separately: Road
   adds one; Railroad adds one; then the Fortress component **without the City
   component** doubles score. Airbase's combination therefore does not double
   it. Each query is live; preserve their order.
3. Retain coordinates only for score>bestScore. Ties keep the earlier tile.
   A candidate scoring zero or negative is never selected.
4. If bestScore=0, this attempt creates nothing and consumes no direct draw.
   Otherwise call U01 create fixed Partisans for O at retained coordinates.
   A negative result creates nothing and does not roll Veteran. Continue later
   attempts, including retries of the same site if it remains admissible.
5. On a nonnegative result, if this is the first successful creation, play
   Infantry sound when local human is N or O; then, if live mode is network,
   O is human and not local, send O the same sound request. Increment made.
6. Set the returned unit's order Fortified. If retained threshold>0, consume one
   primary draw and set Veteran when `draw % (threshold+1) != 0`; threshold=0
   consumes no draw. Preserve other flags. No invalid returned-slot write is
   required; stop/contain it as an excluded bug after earlier effects.
7. Mark that unit seen by N, redraw retained coordinates, and delay one tick if
   local human is N or O. Continue the attempt scan against the now-live map.

After all attempts, if made>0, either participant is human and autoplay off,
set text0=O adjective and text1=cached original city name. If local is N or O,
show Partisans with the **retained most recent creation result**, not necessarily
the last success. Send the corresponding notice to remote human O in network
mode. A later failed creation can leave that unit argument negative; no-site
attempts leave it unchanged. The presentation child must honor its declared
sentinel/input boundary rather than dereference an invalid record.

## Conformance cases

Unless specified otherwise, callbacks return normally without mutating unrelated
state, map/rules are initialized, nominal N=1/O=2, only N is human, size=4,
founder=O, gold[N]=gold[O]=100, totalCitySize[O]=9, production=an eligible unit,
no Palace/wonder/partisan gate, and mode is nonnetwork. Child random draws must
be accounted for separately from the C09 direct draws listed here.

| Setup | Required result |
| --- | --- |
| Mode0, primary draw even, buildings Barracks/Granary/Library/Temple/Palace/Cathedral/Courthouse. | Treasury share40; O60/N140; stock0; size3; only Granary survives among those buildings. One direct draw. |
| Same capture, founder=N. | No alternating draw or four mandatory building removals; size still decreases to3. Final founder becomes O. |
| Mode1 ordinary incitement, Barracks and Temple. | Force war, no alternating draw; Temple removed and Barracks retained; size3. |
| Mode2 with founder=N, size1. | No force war, no population-helper invocation, no direct draw, city survives size1. |
| Mode0 nonfounder size1, ordinary human capture. | Building parity draw, delete at size0, still publish center owner and invoke technology theft and elimination check; no surviving-city window/Leonardo step. |
| AI N takes unknown size1 from AI O; N rank3, weakest-human-selector rank1; exceptional draw even/odd. | Even keeps size1; odd decrements to0 and deletes. If selector rank≥3, no draw and size stays1. |
| Palace candidate slot1 size10, gold2000, difficulty King, AI O. | Escape1000, Palace installed at1, chooser invoked; plunder is later clamped against remaining gold. |
| Best Palace candidate slot0. | No purchase regardless of affordability; spaceship-loss child invoked. |
| Remote human O replies1 to CanEscape. | Reply ends polling but does not set decision; no Palace purchase. A timeout with gold2000 purchases even at Chieftain. |
| Local CanEscape returns2 versus1. | Only1 purchases. Debit reads current gold after the prompt. |
| Active Eiffel at K, human participant and autoplay off. | C08 acquisition invoked during wonder notices while K still has O as owner; AI-only/autoplay capture skips that whole hook. |
| AI O's sole Defend unit at another city, supported by K. | Rehome to that city; no owner check on destination. Other supported non-Partisan units are deleted with scan restart; Partisans become homeless. |
| Human unavailable production; type61 is eligible Defend with positive score; type60 has better score. | Select61 and stop. If61 has nonpositive score, retain Settlers and still stop. |
| Third-party local ally viewer in network mode. | Show local ally popup, skip that viewer's text-send branch. Nonlocal viewer receives the remote request. |
| Size12, O Democracy/N Despotism, ranks2/7, both know Guerrilla and O Conscription. | Base11 then +1 yields12 attempts; reputation5 and no Vendetta gives threshold1, one primary Veteran draw per successful creation. |
| No positive-scoring free partisan site. | No creation/draw, but attempt loop still runs its count. |
| Incited transfer: an O unit at P and another at distance1 outside a city. | Both transfer owner/home, reset movement, preserve Fortified but clear Goto; old/new active counts change before parent. |
| Invalid production99 reaches old-owner counter decrement. | Exclude the overwrite after earlier plunder/status/stock/flag effects; do not corrupt adjacent counts. |

## Appendix A — Source and representation evidence

Primary source pin: OpenCiv2
`a71e8aefffe04e6f719e0e213b027b128a747711`, selected bodies compared with checkout
`fc2155ccdb2123c1137babab6fa39e38152278c9`.
Sources: [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp),
[civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp),
[unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp),
[enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h).

| Entry | Original address / ownership |
| --- | --- |
| `city_incite_transfer` | `004c66ba`, C09 wrapper; V06 caller owns mission price/acceptance. |
| `city_treasury_share` | `00579dbb`, C09; extracted from W06's shared contract. |
| `civ_find_central_city` | `0057a685`, C09 shared query, also consumed by V01. |
| `city_capture_aftermath` | `0057b5df`, C09 parent and its eight source-local fragments. Those fragments are not eight distinct original entries. |
| `city_reveal_to_civ` | `0043cc00`, W03 child, not a second C09 definition. |
| `city_transfer_for_civil_war` | `0057a7e9`, C01 elementary transfer; not called in the capture parent. |

Independent source: `civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, selected bodies in
`src/city_capture.cpp` and the BF-127 producer notes in
`analysis/reference/bugfix-log.md`. Adjacent-record adapters there are evidence
for read dependencies, not authorization to reproduce excluded writes.
BF-127 ties role62 to EventPool initialization: a static −999 DWORD contributes
byte0x19 to the aliased role, and the recorded application writer inventory
finds no later writer before these role tests. This document uses that nominal
input; it does not claim a fresh exhaustive writer audit of arbitrary mods or
callback corruption. The independent comment calling type9 a caravan is not
used: the mapped fixed identity is Partisans.

Original executable is MGE 5.4.0f patch3 with SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Read-only instruction checks establish two discriminating branches:

- At `0057bed2`, a received Palace reply jumps past fallback without assigning
  the retained decision. Timeout enters the explicit spaceship/count/gold tests.
- After local ally/general popups, jumps at `0057d17a`, `0057d295`, and
  `0057d3ef` skip their respective remote-send branches. The primary source
  matches this. The independent reconstruction at the recorded revision uses
  separate `if(network)` sends and would send after a local popup; that difference
  is **not** the normative C09 behavior.

These are static original-instruction observations, not original runtime replays.

| Logical state/operation | Snapshot or protocol mapping |
| --- | --- |
| City reset | AND full city flags with `0xffffffc4`, not `0x000000c4`. Clears bits1/2/8/16/32; keeps Sold4 and higher bits. |
| Building parity | One primary draw; apply byte mask `0xaa >> (draw % 2)` to all five stored improvement bytes, including unused bits when validating serialized snapshots. Main algorithm uses ordinary identities. |
| Permanent removals | Palace1, Temple4, Cathedral11, Courthouse7. |
| Treasury and counters | Gold signed DWORD; total city size signed WORD. Production and active-unit counts are byte entries indexed by type0…61. Overflowing these arrays is excluded, unlike byte counter wrap. |
| Capture history | founder signed BYTE set to O, capture-age BYTE set from turn low byte. Deleted local K is −1. Captured name local was80 bytes. |
| Direct treaties | Incident `0x10000` via shared setter; Aggressor `0x800` via shared clearer; O→N CityCaptured `0x400000`, Vendetta `0x10` directly. |
| Scenario flag | First scenario flags word `0x20` prevents capture-technology child only with GameScenario set. |
| Partisan terrain | Test `(features & 0x42)==0x40`, separating Fortress from Airbase. Partisans type9; Fortified order2; Veteran `0x2000`. |
| Defaults and factors | Settlers0, Legion5; Defend role1; Settler standing-goal kind5; Republic5, Democracy6, Fundamentalism4; Gunpowder35, Conscription17, Communism15, Guerrilla34. |
| Common network group | State difference `(255,2,0,0,0)`, InfoStatus `0x78`, flush5000. DrawSquare `0x72`. Group admission is one live network-mode test. |

### Text/presentation payload mapping

`Text(kind,recipient,strings,numbers,a,b)` uses the current argument bundle;
counts do not imply that all those arguments were just initialized by C09.
Ordinary remote recipients come from the then-live civ-to-slot/player-ID lookup.
Invalid mappings require supplied read input or a bounded stop. C08's BF-194
repair for Leonardo is not a general repair silently applied to these calls.

| Notice | Payload |
| --- | --- |
| Incited CivilWar | kind`0x5e`, O, 3 strings/0 numbers, `(0,0)`; can target local O too. |
| CanEscape request | `0x17`, O, 3/1, `(currentLocalHuman,0)`; shared reply starts−1. |
| Escape success | `0x18`, O, 3/0, `(candidateCity,0)`. |
| AbandonWonder | `0x14`, broadcast255, 3/0, `(improvement,8)` in network mode. |
| StillWonder, exactly1 other | Nonnetwork`0x4c` or network`0x4b`, broadcast255, 3/0, `(improvement,0)`; no send for2+. |
| LostWonder | `0x1a`, O, 3/0, `(improvement,0)`. |
| Participant CityCapture | `0x1b`, participant, 4/1, `(N,K)`. First two winner-branch window messages retain O recipient. |
| Winner ally / loser ally / general viewer | `0x1c` 5/1, `0x1d` 4/1, `0x1e` 4/1, viewer recipient, `(N,K)`. Local popup skips send. |
| Partisans | `0x1f`, O, 2/0, `(mostRecentCreationResult,0)`. |
| Captured/restored wording | Text args3/4 use labels174/176 for ordinary capture,175/177 for restored. Built-wonder scope prefix is label243 plus space. |
| Local picture/sounds | Capture picture70, zoom1, N; wonder zoom8. Barracks sound5 mode0; partisan Infantry34 mode1. |

## Appendix B — Validation and remaining joins

The 13 selected primary bodies are unchanged from the source pin: C09's four
original entries plus eight capture fragments, and the W03 city-reveal child.
A focused external harness includes these actual bodies and the previous scoped
source harness's unit lifecycle/stack, city deletion, map mutation, eligibility,
production and calculation bodies. **65 assertions passed** in optimized and
AddressSanitizer/UndefinedBehaviorSanitizer builds. Leak detection is disabled
because the traced sandbox cannot run LeakSanitizer; address and undefined-
behavior checks remain enabled.

Cases cover treasury rounding/negative replacement/division stops; central-city
strict ties and temporary exclusion; the full capture parent under modes0/1/2;
restoration, parity loss, count wrap, size-one deletion/preservation and primary
draw counts; city-taken event and theft/elimination order; Palace slot0, exact
local choice, remote reply and timeout; abandoned/built-wonder boundaries;
actual U01 supported-unit deletion/rehome and Partisan homelessness; first-
eligible-defender selection and nonpositive score; local-versus-remote observer
notices; successful and no-site partisan attempts; actual incited unit ownership,
home/order/movement/count changes; and an excluded production-count overwrite
stop after the ordered preceding effects.

The fixture adapts pointer carriers and packed member address expressions,
uses a defined signed-wrap build, supplies nominal role62=0x19, and replaces
host division faults with explicit observations. It refuses excluded counter/name
writes. Primary returns are scripted for distinguishing branches while the
recorded generator state advances; F01 owns generator conformance. Geometry,
advance/effect answers, diplomacy, civil war/elimination, theft, AI choice,
clock/network transport/receipt, events and presentation are declared adapters.
The actual C08 upgrade/Eiffel children and native UI/peer receipt were not run
inside this harness. Arbitrary callback state, invalid names/recipients/extended
rule inputs and division faults are not exhaustively covered.

These are Linux-adapted source checks, not native Windows or original-program
conformance and not an independent cleanroom implementation test. The inspected
independent reconstruction and original instructions do not substitute for a
fresh original runtime replay. Reference source and temporary harnesses remain
external research material; they are not imported implementation code.

[V01](V01-civilization-lifecycle-and-civil-war.md#appendix-b--validation-and-remaining-integration)
specifies civil war/elimination and adds finite joins with the actual shared
central-city query and C01 transfer. Its checks do not exercise this complete
capture parent with all real children. Remaining integration: V03 real capture
theft; V04 real treaty operations;
V06 full mission/payment; S02 event dispatch. C08 effects, A02 production choice,
P06 window behavior, real network waiting/receipt and P04/P10 notices/sounds must
be joined with their actual consumers. U02/U06 require full movement/combat-to-
capture traces, including post-capture moving-unit changes. F04 must exercise
consecutive turns and statistic refresh after capture. X02 must retain full-state
and ordered call/RNG traces, original-versus-reconstruction divergences, supplied
read boundaries, scalar wrap and excluded-write diagnostics separately.
