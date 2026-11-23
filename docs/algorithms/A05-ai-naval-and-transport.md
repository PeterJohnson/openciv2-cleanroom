# A05 — AI naval and transport choices

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in A05 |
| --- | --- |
| Gameplay | Sea/cargo recruitment, port/landing/war/trade destinations, capacity decisions and stateful retries. |
| Original files/content | F03 naval/cargo rules and F06 cargo/orders/home links. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Uninitialized parent-local cases, receiver pointers and storage of intermediate cargo summaries. |

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

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. This document specifies ship cargo classification, port
recruitment, unloading decisions, war/trade/settlement destinations, nearby
landing selection and the immediate retasking of transports after combat.
These decisions publish orders and wake cargo; they do not themselves perform
boarding, a sea move, a landing, combat or a trade delivery.

[A04](A04-ai-ground-tactics.md) owns the shared chooser, captured context, guards,
recovery, movement scoring and direction/Finish continuations. [A01](A01-ai-strategy-and-assessment.md)
owns roles, goals, regional summaries/policies and defense quotas.
[U01](U01-unit-lifecycle-and-stacks.md) owns stack traversal, aggregates, cargo
binding and in-place relocation. [U04](U04-unit-orders-and-activation.md) owns
Goto and waking sleeping units by role;
[U02](U02-movement-and-zones-of-control.md) owns movement allowance, remaining
movement and exhaustion;
[U05](U05-transport-and-special-movement.md) owns boarding/unloading/landfall execution.
[W01](W01-map-geometry-and-spatial-state.md) supplies ordered D neighbors, city
radius offsets, bounds, wrapping, region queries and weighted distance.
[C01](C01-city-lifecycle-and-ownership.md) supplies nearest-city and shared-ocean
queries. [V04](V04-diplomacy-and-treaties.md) supplies directed treaties and
hostility. A07 owns the carried caravan's own destination choices.
[F01](F01-determinism-and-arithmetic.md), [F02](F02-game-state-and-invariants.md)
and [F03](F03-rules-and-configuration.md) supply arithmetic, state and rules.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
semantic publications and failure effects. Only dependencies within the binding
scope above constrain evaluation; storage/API choices need no original-trace
equivalence proof. External file/wire definitions remain binding at adapters.

AI scores, candidate sets, deterministic ties and stateful decisions are unchanged.
Pure candidate work may be reordered only with identical decisions and required
scratch/publication effects. Mutating surveys, retained cursors, live bounds,
child calls and sampled-value reuse prevent assuming an entire chooser is pure.

## 1. State, entry and compatibility boundaries

Use A04's actor u, civilization C, working position p, captured domain/role,
raw region R, nearest city N/distance dn/land-only region Rn, nearest owned city
H/distance dh, captured regional policy P, enemy-ZOC Z, damage class L,
RetryTransport and NoMoves. A04 can enter here without executing city-guard
setup, particularly when a type with nonzero cargo capacity has no moves left.
A retry keeps the original context and the retained values below.

Additional logical state and original producer relationships follow. The
production initialization repair below supplies otherwise unwritten values.

| Name | Meaning and initialization |
| --- | --- |
| K, F, D | Cargo role set, released-member count, reserved-defender count. Reset by each cargo scan. |
| Q | Desired defenders, written only by a reached A04 city-guard quota calculation; otherwise an incoming unresolved value. |
| G | A retained “Goto matches a landing region” value. A reached match sets it true; neither a new neighbor nor a retry clears it. No unconditional original initializer is established. |
| E | Shared regional cargo-needed value. Port candidates and landing candidates reset/write it; fallback recruitment can set it true without clearing it first. Retained across retries; initially unresolved. |
| S, PS | Selected port identity and its policy. Updated on strict port-score improvements; retained across retries. PS is otherwise unresolved. |
| B | Shared civilization cursor. Its reached writers are specified below; the trade discount reads its retained value. |
| Room | The special “settlers can take more cargo” condition, not general free capacity. |
| ExpandNeighbors | Control flag governing whether to consider unloading. A port search overwrites it even if no port is selected. |

K contains role **slots**: the nonnegative remainder of each actual rule role
modulo 32. This is a logical set, not a required bitmask representation. Nominal
roles are A01's named roles; custom roles separated by 32 share membership.
Effective role 21 is distinct from a unit's actual Settle role when testing cargo.
Regional policy is the retained A01 value, normally Attack, Defend,
CapacityPressure or Expand. Policy-to-role membership uses the same modulo-32
slot conversion, including accepted nonstandard policies.

Reads of hold capacity, actual role and domain use signed8 interpretation;
city size and route count likewise use signed8. Regional military/attack sums
are unsigned modulo-65536 values unless stated otherwise; tile/fertility totals
are signed16. Distances, scores and local counts follow F01 reference arithmetic,
with significant reductions, arithmetic right shift and division toward zero. Counter assignments retain modulo 256; counter comparisons explicitly use
signed8 or the stated remainder. Stored coordinates retain signed16. Live
queries and child calls occur in the listed order; later reads can observe their
changes. A captured distance does not refresh just because a helper runs.
All draw(a,b) calls use F01's secondary inclusive range helper. Consume only
reached calls, with its accepted reversed-range/overflow behavior; no primary
random draw is introduced by these bodies.

Normal inputs have valid actor/city identities, consistent stacks and region
summaries, positive capacities and supported role values. Accepted custom data
can produce negative signed capacities/counts or unusual policies. Preserve
defined comparisons and arithmetic; do not silently clamp them. A narrower
implementation may declare and reject those inputs before entry.

### Production initialization repair

The complete A04 chooser initializes otherwise unwritten Q/G/E/PS once per
activation: Q=0, G=false, E=false and PS=0. A reached city-guard quota replaces Q;
port and landing producers replace the other values as specified. Transport
retry retains all four values rather than reinitializing them.

This is an independent deterministic repair under X01, not an equivalence claim
for native stack contents or historical supplied-byte tests. Q=0 reserves no
defenders before a quota has been calculated; G=false records no established
landing-region match; E=false records no established cargo demand. PS=0 gives
the ordinary priority 2 until a selected port supplies its policy, rather than
inventing capacity pressure and priority 4. The missing-producer cases can
therefore release cargo, leave the landing counter unchanged, decline fallback
recruitment, or publish ordinary-priority goals where arbitrary original locals
would produce different results. Valid reached producers and their subsequent
retention are unchanged.

The isolated helpers may still accept explicit retained state for diagnostics;
an unresolved diagnostic input is not the production chooser's entry contract.
Tests must cover exhausted ships bypassing city guarding, Defend cargo away
from home, fallback demand/priority, landing match retention and transport retry.
Other missing identities or unsupported semantic inputs retain their own bounded
failure contracts; this initializer does not manufacture a port or city.

Out-of-bounds writes are excluded bugs under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs).
Stop before an invalid store unless safe omission and its remaining continuation
are established, retaining prior valid effects. Do not emulate corruption as
writes to adjacent logical fields. Reads are separate: known aliases use their
actual live producers; unresolved backing or retained state must be supplied
or cause a bounded stop at the first relevant read. Never infer a universal
initial Q/G/E/PS from a sampled stack value or a reconstruction's zero repair.

## 2. Shared sea-unit routing and cargo scan

A04 enters this prefix only when its captured domain is Sea. In order:

1. Perform the cargo scan below.
2. If captured effective role is NavalSuperiority, clear standing goals of
   NavalSuperiority and role 18 at p, radius zero, in that order.
3. If captured role is SeaTransport, run the transport controller (§3).
   Honor an ApplyDirection or Finish result immediately.
4. If NoMoves, return 1 directly to A04's caller, without its common Finish.
5. If role is SeaTransport or captured Z=0, run war, trade and land goals
   (§§6–8), in that order. Each phase has its own live order gate.
6. Continue at A04's shared pillage/partisan/work phase, including after goals
   have set Goto. This is also the return path after transport retry.

Cargo scan resets K to empty, F=0 and D=0. Traverse the complete stack in U01
order, preserving the original actor identity across the traversal. There is
no extra owner, capacity, assigned-carrier or movement-remaining filter.
Only live actual Land-domain members participate:

- If order is Fortified: only when dh=0, increment D. If D>signed Q and
  (P=CapacityPressure or F=0), clear that member's orders, add its actual role
  slot to K, and increment F. The earlier increment of D is not undone.
- Otherwise, if actual role is Defend and D<signed Q, increment D and set
  Fortifying. This test is allowed away from a city and consumes Q there.
- Otherwise clear orders, add the actual role slot to K and increment F.

Thus F is a count of released members, not a count of all passengers or free
holds. A Land member with Sleep is ordinarily cleared here; later role-wakeup
only clears members that are **still** sleeping when that separate call runs.
Fortified cargo at sea is skipped without consulting Q.

## 3. Transport controller

This section runs only for captured SeaTransport role after the cargo scan.

1. If dn=0 and the signed counter is negative, replace it with turn modulo 8.
2. Set Room iff removing Trade from K leaves exactly {Settle} and
   F<signed live hold capacity. Set OnlySettlersWithoutGoto iff K={Settle}
   and live order is not Goto.
3. Set ExpandNeighbors to !NoMoves. If !NoMoves, call port selection (§4);
   that call can replace ExpandNeighbors. Honor its Hold result immediately.
4. If dh=0, traverse the live stack and call U01 relocate-in-place(mode=1) on
   its first actual Settle member, then stop that scan. No domain check is
   added. This is the original reordering/relocation operation, not an invented
   detach-all or landing operation.
5. If ExpandNeighbors is true, task is ReinforcePort, order is Goto, dn≠0,
   exact destination has a city, that city is Coastal and adjacent to R,
   and turn is not divisible by 16, clear ExpandNeighbors. City ownership is
   not additionally checked.
6. If ExpandNeighbors or RetryTransport or NoMoves, run unloading (§5).
   Honor its ApplyDirection or Finish return. Otherwise continue §2.

Port selection is skipped when NoMoves, but the local settler relocation and
forced unloading pass are not. A still-exhausted actor can finish unloading
before the direct return-1 gate in §2.

## 4. Selecting and recruiting at a port

Set minimumCargo to min(signed hold,3) when dn=0 or OnlySettlersWithoutGoto;
otherwise use 1. A negative accepted hold remains negative. If F≥minimumCargo
and Room=false, leave ExpandNeighbors unchanged and return Continue.
Otherwise set ExpandNeighbors=Room, best=999 and selectedTradeOnly=false.

Scan city slots ascending, considering live owned Coastal cities. A city already
marked FerryPort is eligible only if u's signed counter is negative and equals
`-1-(citySlot modulo 64)`. For each admitted city J:

1. Get its raw region RJ. Connectivity is citiesShareOcean(H,J) when dh=0;
   otherwise cityAdjacentToBody(J,R). Reject if false.
2. Set E=false, then read regional military M and A01 defense quota. If
   M<quota and policy[RJ]≠CapacityPressure, reject if regional Trade count=0;
   otherwise set E=true.
3. Start s=weightedDistance(p,J). Query its first unit. If present and its
   stack's SeaTransport count exceeds 1 when s=0, or exceeds 0 otherwise,
   add 16. The threshold uses the original distance before this addition.
4. If policy[RJ]=CapacityPressure or all presence flags[RJ] are zero,
   subtract `unsigned regional attack[RJ]>>2`; otherwise add 16.
5. If s<best, replace best, S=J, selectedTradeOnly=E and PS=live policy[RJ],
   then set RecruitPort Goto to J. Continue scanning after this publication;
   a tie retains the first candidate. A nonwinning candidate can still change E.

After the scan, if dn=0, `counter modulo 4 != (turn-1) modulo 4`, and any of
Settle∉K, K={Settle}, or F<signed live hold, replace best with 1000. This does
not undo a Goto already published. Define priority a=4 if retained PS is
CapacityPressure, otherwise 2. If PS was never established, its comparison is
an incoming read; Appendix A explains when only a later publication needs it.

### A sufficiently attractive selected port

When best<3*a:

1. Set counter to `-1-(S modulo 64)`, retaining modulo 256, and set S's
   FerryPort and FerryPortFresh flags.
2. Unless selectedTradeOnly, query Settle-role count at the actor's **live Goto
   coordinates**. If zero and PS≠Expand, post Settle priority a then role 21
   priority a-1 at those coordinates. Next post Defend priority a unless
   PS=Defend; Attack priority a unless PS=Attack; and Diplomacy priority a
   when PS is CapacityPressure or Expand.
3. Always post Trade priority 2 at live Goto coordinates. Return Continue.

Reload the coordinates for each publication as U04/A01 children may affect
live state. Selecting a port does not itself request Hold or exhaust movement.

### Fallback recruitment at the current owned city

Otherwise, require dh=0 and fewer than two SeaTransport-role units in the
actor's stack. Continue only if best=1000 or draw(0,3)≠0, and captured R's
presence lacks StrongerEnemy. The original sampled even for best=1000; that
unused sample is optional. Preserve the captured-R check and later live queries.

Read M[R], call its defense quota, and select H as the working city. If M≥quota
or captured P=CapacityPressure, set E=true; otherwise **retain E** from the
last candidate/retry/incoming state. If regional Trade count≠0, set
selectedTradeOnly=true; otherwise retain the selected-port value.
If neither E nor selectedTradeOnly, return Continue.

Set H's FerryPort and FerryPortFresh flags. If E:

1. Query Settle count at H and store that result in shared **dn**, replacing
   its original nearest-city distance for subsequent controller/unload tests.
2. If zero and P≠Expand, post Settle priority a, then role 21 priority a-1 at H.
3. Post Defend priority a unless P=Defend, and Attack priority a unless P=Attack.
4. For P equal to CapacityPressure or Expand, post Diplomacy priority a at the
   actor's **live Goto coordinates**, not H's coordinates.

Always post Trade priority 2 at H, then return ApplyDirection(Hold=8) to A04.
The priorities still depend on retained **selected-port PS**, not captured P.
No chosen port, no available cargo, or failed random gate is a normal result;
no fallback dummy city or extra random draw is added.

## 5. Considering adjacent land and unloading

Capture OnlyTrade=(K={Trade}). Scan directions D0…D7 around captured p, wrapping
x. Skip out-of-bounds and ocean tiles. Set shared B to the candidate's unit
owner O, including Absent. Reject if O is present and differs from C. For O=C,
query the first unit and stack count; skip if count>1.

Define PeaceNear iff N is present and foreign, C→N.owner has **Peace**, and dn<4.
This name intentionally does not mean Alliance. Query candidate raw region RJ,
set working city=N, E=false, tradeDrop=false and exploreOne=false.

### Trade willingness

If Trade∉K and PeaceNear, skip the candidate after the preceding resets.
If Trade∈K and RJ=Rn, calculate tradeDrop:

1. Traverse the live actor stack. Each actual Trade member replaces home B0:
   Absent home resets it to Absent; otherwise use its unsigned home slot.
   The last Trade member wins, including a last Absent home.
2. If Absent, find nearest owned city to the **candidate tile**, unrestricted
   region. Clamp results less than 1 to slot 0. Slot 0 is retained city backing,
   not a newly created or necessarily live city.
3. Start tradeDrop=(task≠RecruitPort). If B0's raw region=Rn and
   distance(N,B0)<16, clear it. If N is owned, also clear it when its signed
   size<8 or distance(N,B0)<16, even if regions differ.
4. For route indices 0…signed routeCount[B0]-1, clear tradeDrop if any partner
   equals N. Keep scanning; there is no early success or inferred route count.

Missing N or invalid home/route backing is a read boundary, not permission to
create a dummy city. Nominal routes have at most three entries. Accepted larger
counts read the ordered original backing described by C07/F06 or stop explicitly.

### Cargo demand and exploration, in order

Apply these steps to E, allowing later steps to override earlier ones:

1. Set E=true if unsigned M[RJ]<quota(C,RJ) and policy[RJ]≠CapacityPressure.
2. Set E=true if owned regional city count=0 and unsigned M[RJ]<8.
3. Set E=true if policy[RJ]'s role slot belongs to both K and {Attack,Defend}.
4. Set E=true when Attack∈K, N present, H≠N, dh>10, dn<4, RJ=Rn,
   N.owner human and C→N.owner War.
5. If actor has Goto, destination's raw region=RJ and task≠RecruitPort,
   set E=true and G=true. G is not cleared on a later nonmatching neighbor.
6. If Settle∉K and all presence flags[RJ] are zero: when signed M[RJ]=0
   and signed region tile count>49, set TransportRequested in those flags
   and exploreOne=true; otherwise set E=false. Setting the marker does not
   itself set E, and persists even if this candidate is later rejected.
7. If Room and RJ has ForeignCityWithoutPeace or ForeignMilitaryWithoutPeace,
   clear E. Peace-only presence does not satisfy this test.
8. If captured damage class L>1, set E=true.
9. If !Room, candidate has a city, O≠C and hostility(C,O), set E=true. Pass
   the captured **unit owner O**, even when Absent; do not substitute city owner.
   With O=Absent, the relation tests use C's live subversion counter; if the
   attitude branch is reached, it uses the retained highest octet of C's
   relation toward civilization 7. Appendix A specifies this read mapping.
   Other unsupported owner values need established backing or a bounded stop.
10. If OnlyTrade, clear E.
11. If G is nonzero and (OnlyTrade ? !tradeDrop : !E), replace actor counter
    with turn modulo 8. This updates the **ship**, not every passenger.

### Commit a landing decision

When E, signed region fertile tile count>4 and `1<y<height-2`:

1. Query actor moves left. If at least the road multiplier and
   (**dn≠0** or captured P≠Defend), call bestLandingDirection (§9). If its
   result is present and not Hold, set task RepositionLanding and return that
   direction through A04. No exhaustion or wake has occurred yet.
2. Otherwise, when policy[RJ]=Attack, post NavalSuperiority priority 5 at p.
3. Set task UnloadCargo, exhaust actor movement, then clear actor orders.
   If !tradeDrop, remove Trade from K. Call U04 wake-by-role(actor,K,exploreOne),
   then return Finish to A04. exploreOne limits the wake to the first match.

If this path did not commit, but tradeDrop and `1<y<height-2`, post the same
NavalSuperiority goal for Attack policy, set task UnloadTrade, exhaust movement,
replace K with {Trade} if PeaceNear or {Trade,Defend} otherwise, wake by role
with exploreOne, then **clear actor orders after the wake**, and return Finish.
This trade-only path does not require five fertile tiles or repositioning.

Otherwise, when turn is divisible by 4, clear actor orders and continue to the
next candidate. This clear occurs only after all earlier admission gates; it
can affect the next candidate's Goto test.

After all eight candidates without a committed choice, query moves left.
If moves≤roadMultiplier and live order remains Goto, compute
`mx=(gotoX+p.x)>>1`, `my=(gotoY+p.y)>>1`, with signed32 retained sums.
Make mx even by subtracting 1 if odd, then add 1 to mx if my is odd.
Post NavalSuperiority priority 3 at (mx,my). This is the raw arithmetic midpoint,
not the shortest path across a wrapping seam; do not wrap it here. Return Continue.

## 6. War destinations

The three searches below run in order. Test live order before each search;
an existing Goto bypasses that search. Within a search, continue the scan after
each improving Goto publication. Weighted distances and all described modifiers
are applied sequentially with integer truncation; a strict comparison keeps the
first tie.

### Reinforcement

Require no Goto, K intersects {Attack,Defend}, and live hold≠0. Set best=24.
Scan live owned cities J ascending with policy[rawRegion(J)]=Defend.
Use citiesShareOcean(H,J) if dh=0, otherwise cityAdjacentToBody(J,R).
For connected J, s=distance(p,J). If no unit owner at J, divide s by 2.
Otherwise, if its current Defend goal priority is nonzero, subtract s/3.
On s<best, replace best and set ReinforcePort Goto to J.

### Undefended cities at war

Require no Goto and K intersects {Attack,Defend}; no hold test is added.
Set best=999 and scan all live cities ascending. Assign B=J.owner before
rejecting owned cities; require C→B War and no unit owner at J. Query J's raw
region even though its value is unused by this search. Require the same city
connectivity test as Reinforcement, including for foreign cities.

Start s=distance(p,J). Scan civilization slots 1…7 ascending; if a human slot
is allied with C and has directed War toward B, halve s once and break.
Double s if J has CoastalFortUsed. On strict improvement, store best, set actor
counter=J.slot modulo 8, then set RaidEmptyCity Goto to J.

### General overseas attack

Require no Goto. Choose an ally by scanning civilization slots 0…7: replace
selectedAlly when C has Alliance toward that slot and (it is human or no ally
has yet been chosen). Thus the first nonhuman qualifies only provisionally;
the last qualifying human wins. The shared B loop cursor exits as **8**.

Proceed only if Attack∈K or Settle∉K. Set best=999 and capture the land-only
region at p. Scan cities starting at counter modulo 8, then every eighth slot.
For every live city set B=J.owner before the remaining tests. Require:

- B≠C and no C→B Alliance;
- no C→B Peace, or C→B HostileIntent;
- the strongest civilization is not human, or that strongest civilization is C;
- rawRegion(J) differs from the captured land-only region at p.

For connectivity use A01 cityTouchesLandmass(H,rawRegion(J)) if dh=0,
otherwise W01 landmassBordersBody(rawRegion(J),R). If connected, start
s=distance(p,J). In order: halve if selectedAlly>0 and ally→B War; halve if
C→B Aggressor; halve if C→B War and J has no unit owner; double if CoastalFortUsed.
On s<best, replace best and set OverseasAttack Goto to J. No counter write is
added here. Even rejected live cities update B; if none is reached, B remains 8.

## 7. Overseas trade destinations

Require no Goto, Trade∈K and live hold≠0. Set best=-999 and capture the
land-only region at p. Traverse the actor stack, retaining the home of each
actual Trade member **only when its home is present**. A later Absent home does
not erase a previous home here, unlike §5. If no home was retained, find nearest
owned city to p; clamp a negative result to slot 0.

Scan all live cities J ascending whose raw region differs from p's captured
land-only region. This scan does **not** assign B=J.owner. Connectivity:

| Location and ownership | Required query |
| --- | --- |
| dh=0, J owned | citiesShareOcean(H,J) |
| dh=0, J foreign | cityTouchesLandmass(H,rawRegion(J)) |
| dh≠0, J owned | cityAdjacentToBody(J,R) |
| dh≠0, J foreign | landmassBordersBody(rawRegion(J),R) |

For each connected J, calculate s in this order:

1. s=distance(home,J); halve if J is owned.
2. If C→J.owner War, subtract s/3. If Alliance, add the then-current s/3.
3. If J.owner is human with power rank 7, read the city-count value selected
   by retained B. If that signed value>4, halve s. **B is not J.owner.**
4. Multiply by J's signed baseTrade, retaining signed32 product.
5. Divide by `(distance(p,J)>>3)+1`. Retain original signed division/fault
   behavior for off-nominal coordinates; do not replace a fault with zero.
6. If s>best, replace best and set OverseasTrade Goto to J.

For B=0…7 the city-count read is that civilization's live signed city count.
For **B=8**, the established original alias reads city slot 1's remembered sizes
for civilizations 3 and 4: interpret
`signed16(unsigned rememberedSize[3] + 256*unsigned rememberedSize[4])`.
Use the current retained values, including in an unused city record.
[W03](W03-visibility-and-exploration.md) and
[F06](F06-save-load-and-file-formats.md) own their revelation/load producers.
This is read compatibility, not an allowed overwrite. Other invalid B values
need established backing or an explicit stop.

## 8. Other ports and exploration destinations

### An owned city on another landmass

When no Goto, set best=999 and capture land-only region at p. Scan all live
cities ascending, setting B=owner before the ownership test. Require owned J,
distance(p,J)>7, rawRegion(J) different from that captured region, and
policy[J.region]≠CapacityPressure. Use the city connectivity test from §6
Reinforcement. Double distance if K lacks the policy's role slot.
On strict improvement below best, set AnotherLandmassPort Goto and keep scanning.
This phase does not require nonzero hold.

### A large unsettled region

When still no Goto and live hold≠0: if task is ExploreRegion, reuse stored Goto
coordinates without draws. Otherwise draw y in [0,height-1], then x in
[0,width-1]; make x even, then add 1 if y is odd. Query land-only region RT at
that target, then at p. Require 0<RT<63, signed fertileTiles[RT]>16,
RT different from p's land-only region and C's regional city count=0.
Require cityTouchesLandmass(H,RT) when dh=0, otherwise
landmassBordersBody(RT,R). On success call nearShoreGoto (§9) with ExploreRegion.
A reused target does not get parity correction.

### Random rays to unowned land

If still no Goto, loop radius k=2…23 inclusive. **Every iteration** draws a
D direction in [0,7], then forms q=(wrapX(p.x+k*D.x),p.y+k*D.y).
Require q in bounds, 1<q.y≤height-3, non-ocean, and no owned cities on its raw
region. Use the preceding region connectivity test. On success call
nearShoreGoto with ExploreNearby. Do not break or retest the actor's order
inside the loop: later successes replace earlier destinations and all 22 draws
are consumed, even when radius 2 already found land. No hold requirement is
added to this final search.

## 9. Landing and coastal-target helpers

### Best landing direction

Capture actor coordinates and owner. Set bestScore=-1 and answer=Absent.
Scan D0…D8 (D8 is the center/Hold). A candidate must be in bounds and ocean.
D0…D7 must have no unit owner; D8 does not apply that occupancy test.
Start candidate score at zero and scan its D0…D7 neighbors:

- Require in-bounds non-ocean land without a unit owner.
- Query W01 owner-or-fertility value V. Admit if V>7, or V is not a human
  civilization, or actor→V has neither Ceasefire nor Peace. Use that exact
  query, not radius ownership or city ownership; Alliance alone does not veto.
- Query city owner J. If J is present and foreign, repeat the no-unit-owner
  test and call hostility(C,J). When hostile and the **candidate is D8**, add
  12. The hostility query is reached before the D8 test.
- Add 1 for the admitted land neighbor.

If candidate score≥bestScore, replace the best and answer. Equality favors
**later** directions, so D8 wins a complete tie. A valid ocean candidate with
score zero is still eligible. Return Absent only if no candidate was eligible.

### Near-shore Goto

Inputs actor, task and target q. Capture actor's raw region and target's raw
region. Scan W01's first 20 ordered city-radius offsets around q, wrapping x.
A candidate must be in bounds, ocean, and in actor's captured raw region.
Scan all eight adjacent D positions, with no early break: a match is in-bounds
non-ocean land in q's captured raw region with no city owner. Unit occupancy
and diplomatic permission are not tested.
Choose the **first** candidate with any match, then stop the outer scan.
If none, retain original q, not the last tested offset. Call U04 SetGoto once
with the supplied task and chosen/original coordinates. The fallback can be
land, occupied, or unreachable; actual movement remains a later decision.

## 10. Retasking transports after a city becomes exposed

[U06](U06-combat-and-veterancy.md) invokes this helper at its specified aftermath
join. Capture the supplied city's coordinates and signed owner once. Scan live
unit slots ascending. For each unit, require:

1. Owner nonzero, AI-controlled, and different from captured city owner.
2. No directed Peace toward that city owner. Neither War nor a Sea domain or
   SeaTransport role is required; Alliance alone is not a separate rejection.
3. Live signed hold capacity nonzero, stack count>1, and its tile is ocean.
4. Weighted distance from captured city coordinates to live unit coordinates
   is **strictly less than maxMoves(unit)**. Use movement allowance directly,
   not remaining moves or allowance divided by the road multiplier.

Write ordinary Goto order and the captured destination coordinates directly.
Retain its AI task, counter and moves spent. Do not call SetGoto with an invented
raid task, and do not recheck whether the city is empty inside this helper.
It consumes no direct random draws.

## 11. Conformance cases and validation limits


> Compare the gameplay/interchange observations in the
> [production scope](#production-contract-scope). Original memory/helper traces,
> arbitrary native callbacks and unsafe backing-byte cases in historical lists
> are optional reference, not additional production acceptance criteria.

These cases distinguish the specified decisions; they are not a claim that
all original-game sessions or callback interleavings have been reproduced.

| Input or event | Required result |
| --- | --- |
| Fortified Land cargo away from an owned city | Skipped without consuming desired-defender Q. |
| Unfortified Defend cargo at sea, supplied Q=2 | Reserve first two as Fortifying; later members are released. |
| Own-city fortified cargo exceeds Q, F initially zero | Release the first surplus member; later fortified surplus stays unless P=CapacityPressure. |
| Equal port scores | First city remains selected, but later candidates still replace E. |
| Port scan publishes Goto, then turn/counter gate sets best=1000 | Published Goto remains; fallback random call is still consumed if its earlier gates pass. |
| Fallback P=CapacityPressure, retained PS=Defend | Recruitment uses priority 2, not 4, and Diplomacy uses live Goto coordinates. |
| Exhausted transport | Skip port search; still perform own-city settler relocation and forced unloading pass. |
| Landing RJ matches Goto then a later neighbor does not | G remains true; later counter update can occur. |
| Ordinary unloading with tradeDrop false | Clear ship orders before wake and exclude Trade from the role set. |
| Trade unloading with PeaceNear | Wake only Trade; clear ship orders after wake. |
| Reposition gate with dn=0, dh>0, P=Defend | Do not call landing-direction selection; the test uses dn. |
| Midpoint across a wrapping seam | Use arithmetic average, then parity correction, with no seam optimization. |
| Nine landing candidates tie at zero | Choose D8; occupied noncenter ocean candidates are excluded. |
| Near-shore radius finds nothing | Publish Goto to the original target. |
| General attack scan reaches no live stride candidate | Retained B=8; strongest-human trade discount reads city-1 remembered-size alias. |
| Last Trade passenger has Absent home | Unload willingness resets home; overseas trade search retains the previous present home. |
| First exploration ray succeeds | Continue through radius 23; later successes can replace its Goto. |
| Empty-city retask at distance=maxMoves | Leave orders unchanged; strictly smaller distance publishes Goto without changing task. |

Finite source checks and their exact scope are recorded in Appendix B. Complete
native execution, real movement/boarding/wake integration across consecutive
turns, custom callbacks, all read-backings and network/presentation effects
remain outside that finite validation. These checks do not
establish original-program conformance beyond their stated limits.

## Appendix A. Original representation and evidence distinctions

The main algorithms use logical sets, identities and task names. These original
encodings are for saved-state comparison and instruction validation:

| Logical value | Original encoding |
| --- | --- |
| RecruitPort / OverseasAttack / AnotherLandmassPort / ExploreNearby | task `33` / `34` / `35` / `36` hex |
| ReinforcePort / RaidEmptyCity / OverseasTrade | task `41` / `49` / `54` hex |
| UnloadCargo / ExploreRegion / RepositionLanding / UnloadTrade | task `55` / `56` / `62` / `75` hex |
| Cargo roles Attack, Defend, Settle, Trade | mask bits `1`, `2`, `20`, `80` hex |
| FerryPort / FerryPortFresh / Coastal / CoastalFortUsed | city masks `200`, `400`, `80`, `08000000` hex |
| ForeignCityWithoutPeace / ForeignMilitaryWithoutPeace / StrongerEnemy / TransportRequested | regional masks `1`, `4`, `10`, `80` hex |
| City record / civilization record | 88 / 1428 bytes; no equivalent internal packing is required. |

The original shared frame's Q/G/E/PS are respectively EBP-118, EBP-168,
EBP-d8 and EBP-28. Port selection's S is EBP-15c; the shared civilization cursor
B is EBP-74. These locations are evidence identifiers, not fields a new
implementation must allocate.

The primary reconstruction supplies sampled entry values Q=`64a8c19c`, G=4,
E=`21f238`, PS=`21f2dc` hex. Its B13/B19/B24 fixes substitute zero for G/Q/E.
The independent reconstruction supplies BF-005/BF-008/BF-009/BF-090 initial
repairs for G/Q/PS/E. None establishes the original initial state in every call.
Use actual preceding producers, supplied retained inputs or a bounded stop.
When G cannot affect the counter because the later local condition is false,
its value is immaterial to that result; it is not permission to assume G=false
for a later candidate. Similarly, best≥12 takes the port fallback for either
PS comparison outcome, but a subsequent priority publication can still need PS.

The primary B13 prose says every cargo unit's counter changes; the complete
routine writes the retained **actor** identity's counter. It also does not
clear G each neighbor. The B24 description's decompiler local name is not the
original frame offset; the reached E writers and retry retention govern behavior.

### Landing gate: nearest city versus nearest owned city

The independent reconstruction uses own-city distance in its reposition gate.
Original `0053d774` compares EBP-174; `0053d781` compares captured policy against 1.
The unrestricted nearest-city call `00538be9` supplies EBP-174 at `00538bfc`.
The owned-city query at `00538c64` instead supplies EBP-38 at `00538c74`.
Thus §5's **dn** agrees with the primary source and original instructions.
The distinction matters with a foreign nearest city or changed shared dn.

### Retained civilization cursor and the city-memory read

The original ally scan leaves B=8 unless the subsequent live-city stride scan
replaces it. At `0053e915` the trade discount loads EBP-74, scales it by 1428,
and at `0053e926` reads signed16 at `0064c708+B*1428`.
Civilizations begin at `0064c6a0`, city count is offset `68`; city records begin
at `0064f340`. B=8 therefore reads `0064f3a8`, city slot 1 offset `10`, its
remembered-size bytes for civilizations 3 and 4. This is established readable
city backing with W03/F06 producers, not an unknown default, a city-8 record,
or a required out-of-bounds write. A new representation can calculate the
same signed value from those two logical remembered sizes.

### Hostility toward an absent unit owner

Step 9 of §5 can call hostility(C,-1). Original `00467b0d` reads the War test
from the second byte of C's relation indexed by -1; `00467b3c` and `00467b68`
read its Alliance and Contact/Peace tests. These addresses lie within C's
subversion counter at original offset `1c`, not an absent civilization record.
Let s be that counter's nonnegative remainder modulo 65536. In order:

1. If `floor(s/8192) modulo 2 = 1`, return true (the War test).
2. If `floor(s/8) modulo 2 = 1`, return false (Alliance).
3. If s is odd and `floor(s/4) modulo 2 = 0`, return whether the highest
   retained octet of C's directed relation toward slot 7 exceeds 49.
4. Otherwise return false.

The last read is original attitude(C,-1): thunk `00401b36` reaches `00467904`,
whose unsigned load at `00467920` uses offset `40-1`, the last octet of
relation[7]. The full relation read also spans the reputation/request-count
bytes, but these tests do not depend on them. V06/F06 own subversion's
mutation/loading; V04/F06 own directed relation state. Nominal high treaty
bits usually make the attitude branch false; accepted retained data can differ.
These are reads of established state. Do not fabricate an absent-owner record
or recreate an overwrite to obtain the values.

Accepted excessive route counts in §5 similarly require source-backed reads
of following fields, not overflow writes or invented padding. Unbacked map,
city, role, region and relation reads are explicit stops unless supplied.
No out-of-bounds write or resulting corruption is required by this document.

## Appendix B. Source traceability and finite checks

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is the classic path of
[OpenCiv2 ai.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/ai.cpp)
at revision `a71e8aefffe04e6f719e0e213b027b128a747711`. Selected bodies and record
definitions are checked unchanged in OpenCiv2 comparison revision
`fc2155ccdb2123c1137babab6fa39e38152278c9`.
The independent read-only reconstruction is `civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`: `src/ai_unit.cpp`,
`src/ai_transport.cpp` and `src/combat_entry.cpp`. Its repairs and the landing
gate disagreement are distinguished above. Research does not resume its
accepted reconstruction campaign or alter source/assets/annotations.

Original instruction checks use MGE 5.4.0f patch 3 `Civ2/civ2.exe`, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Bounded disassembly verifies the nearest-distance producer/consumer and trade
cursor read/alias and absent-owner hostility backing. It is static evidence; no fresh native original execution or
saved Ghidra annotation cycle is claimed.

| Behavior | Primary navigation and original entry |
| --- | --- |
| Landing choice / near-shore target | `ai_ship_best_landing_dir`, `005312e4`; `ai_ship_goto_near_shore`, `00531653` |
| Postcombat retask | `ai_transports_raid_empty_city`, `005369f3` |
| Shared regional quota, owned by A01 | `ai_continent_defense_quota`, `00536bc9` |
| Cargo/controller/port/unload | `ai_orders_transport_cargo`, `ai_orders_transport`, `ai_orders_transport_port`, `ai_orders_transport_unload`, parts of `ai_unit_choose_orders`, `00538a29` |
| War/trade/land goals | `ai_orders_ship_goal_war`, `ai_orders_ship_goal_trade`, `ai_orders_ship_goal_land`, same parent |
| Role-wake and Goto, owned by U04 | [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp), `unit_stack_wake_by_role`, `00531567`; `unit_set_goto`, `00531607` |
| Sampled temporary-value repairs | [BUGS.md](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md), B13, B19, B24 and uninitialized-local notes |

The historical bounded Linux harness compiled **13 complete
source bodies**: ten A05 bodies, A01's defense quota, U04's role-wake and V04's
hostility helper. It uses the original reference-member shared order context,
with explicit supplied Q/G/E/PS for selected cases. It does not substitute a
sampled entry value as original initialization. Forty-five assertions pass
with an optimized build and an AddressSanitizer/UndefinedBehaviorSanitizer
build (leak detection disabled because this sandbox runs under tracing).
They sample the cases above, retained city-memory reads and the
absent-owner subversion/attitude mapping, including signed alias values.

The fixture supplies map/geometry, first/next stack traversal and aggregates,
nearest/coastal/connectivity queries, actual draw answers, Goto/exhaustion,
in-place relocation and goal-publication adapters. Full logical city/civilization
records back the explicit read mappings; adapters stop outside their declared
backing. V04's real hostility decisions use a supplied byte-access attitude
adapter. The source's raw regional-attack read uses the verified original record
layout. These adapters are declared boundaries, not evidence that full W01/U01,
A01, U02/U04 or movement execution ran. The complete A04 parent is not executed
by this fixture; the selected A05 controller calls its actual port/unload
children, and unloading calls actual U04 role-wake.

Source checks verify every extracted body, twelve record definitions and the
enumeration header against the pinned revision, reference checkout cleanliness
and executable hash. Link/anchor, index and whitespace checks accompany
the documentation commit. Temporary fixture sources and original binary inputs
are not part of the cleanroom repository. No overwrite fixture is counted as a
required conformance case.
