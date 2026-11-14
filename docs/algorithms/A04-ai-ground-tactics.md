# A04 — AI ground tactics and shared unit-choice routing

**Required profile:** [X01 fresh-game isolation and bug repairs](X01-classic-compatibility-contract.md#fresh-game-isolation-and-supported-bug-repairs)
and [behaviorally significant ordering](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
override conflicting procedures and examples below. Retention applies within a
game or to explicit imported inputs, not across new games. Confirmed original-bug
stack/backing/stop cases are optional diagnostics; use documented safe repairs.
Only real effects and callbacks impose ordering; test adapters cannot invent them.
Unspecified safe continuations remain explicit gaps, not required arbitrary bytes.

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. This document owns the shared active-unit chooser, recovery
and guard decisions, ordinary eight-direction scoring, pillage/partisan choices,
retained-Goto decisions, order completion and immediate response to an attack.
The shared scorer also admits naval/air actors; its applicable branches are
specified here rather than replicated in each specialized AI document.

[A01](A01-ai-strategy-and-assessment.md) owns effective roles, shared goals,
regional assessment and the outer movement passes. [A03](A03-ai-settlement-and-terrain-work.md)
owns site/fortress classification and settler/role-21 work, including population
return. [A05](A05-ai-naval-and-transport.md) owns cargo/transport and naval goals,
[A06](A06-ai-air-and-nuclear.md) owns air/nuclear target selection, and
[A07](A07-ai-special-unit-missions.md) owns diplomat/caravan/paratrooper
destinations. Those children can return to specific stages here without restarting the context capture.
[W06](W06-huts-and-barbarians.md) owns barbarian dispatch.

[W01](W01-map-geometry-and-spatial-state.md) supplies coordinates, ordered D
neighbors, terrain/owner queries, distances and visibility membership.
[U01](U01-unit-lifecycle-and-stacks.md) owns stacks and aggregates;
[U02](U02-movement-and-zones-of-control.md) movement/ZOC;
[U03](U03-pathfinding-and-goto.md) route queries;
[U04](U04-unit-orders-and-activation.md) order setters, home travel, sleep and
execution; [U05](U05-transport-and-special-movement.md) paradrop execution.
[U06](U06-combat-and-veterancy.md) supplies best-defender selection and combat
preview; [V06](V06-espionage-and-bribery.md) owns actual bribery.
[W04](W04-terrain-work-and-improvements.md) owns pillage execution.
[C01](C01-city-lifecycle-and-ownership.md) owns nearest/coastal queries;
[C08](C08-buildings-and-wonders.md) facility queries;
[V04](V04-diplomacy-and-treaties.md) attitude/hostility helpers.
[F01](F01-determinism-and-arithmetic.md), [F02](F02-game-state-and-invariants.md)
and [F03](F03-rules-and-configuration.md) own arithmetic, state and rule inputs.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

AI scores, candidate sets, deterministic ties and stateful decisions are unchanged.
Pure candidate work may be reordered only with identical decisions and required
scratch/publication effects. Mutating surveys, retained cursors, live bounds,
child calls and sampled-value reuse prevent assuming an entire chooser is pure.

## 1. State, boundaries and arithmetic

Input is the live active-unit identity. Capture actor u and signed owner C.
Normal civilizations are 1…7, with owner 0 dispatched separately. Preserve live
reads after child calls; cached coordinates, type, distances, role and policy do
not automatically refresh after a callback or successful action.

The shared context consists of:

- Mutable working position p, initially u's coordinates; captured type T,
  original terrain t and raw region R. Later direction application changes p
  without directly relocating u. Air/nuclear children can also change p.
- Nearest city N and nearest owned city H, captured weighted distances dn/dh,
  and their land-only regions Rn/Rh. Both queries use unrestricted region and
  no alternate owner. Missing city gives Absent, distance 9999 and region -1.
- Captured effective role r from A01, then live type abilities and signed domain
  d. Regional policy P is captured from C's retained count/assessment state.
- Z, the original enemy-ZOC result, and Fortress, captured from a fortress with
  no City component. They can later be explicitly replaced as described.
- ForceAttack, StayHome, Hold, Guard, HasOrders, RetryTransport, NoMoves and
  GotoCity, initially false. HasOrders is the movement planner's retain-orders
  flag, not a freshly calculated `orders != None` predicate.
- SettlerKind and SettlerType, initially false and assigned by A03's site phase.
  A skipped phase leaves them false, even if the live rule role is Settle.

The original also shares city/unit cursors and score scratch between children.
Retain a value when a specified later stage consumes it. A05 owns its extra
cargo, selected-city and regional scratch; do not initialize those to sampled
stack constants or convenient zeros. An irrelevant copied attack-cost class
for a non-attack candidate is not a new random/unknown decision input: the
planner reads that class only when the selected candidate is an attack.

Unless noted otherwise, unit rule attack/defense/cost/role/HP, terrain defense/
movement/food and city size are read as signed8; stored damage, moves spent,
government, reputation and visibility are unsigned8. F01 defines
reference arithmetic results, arithmetic right shift and division truncated
toward zero; the numeric convention permits equivalent wider evaluation. Preserve division faults after preceding child effects;
zero-cost custom rules do not justify an invented attack score. Rule abilities,
orders and IDs use the F02/F03 domains and conversions. Original encodings belong
to Appendix A, not the required representation of a new implementation.

Every `draw(a,b)` below is F01's **secondary inclusive range helper**, with its
own reversed-range/overflow behavior. Draw only at the specified reached call.
A rejected candidate can already have consumed draws or executed bribery;
rejection does not undo those effects. Neither combat preview nor a movement
score is a promise that U02 will admit or execute that move.

Invalid writes are excluded bugs under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs).
Omit an invalid write only when its remaining valid continuation is established;
otherwise stop before it, retaining prior effects. Known read aliases below are
separate from writes. Unbacked city/region/rule/retained reads require supplied
backing or a bounded stop, without invented zero records. A stricter alternative
may declare/reject a narrower custom-input domain before entry.

## 2. Shared entry and recovery dispatch

1. If C=0, call W06's barbarian chooser and return its result directly.
2. If current order is one of the contiguous classic work orders Irrigate,
   BuildRoad, Mine, Transform, BuildFortress or CleanPollution, go straight to
   common Finish. No position/type/domain context has been captured yet.
3. Capture p and T, initialize direction Hold (8), and test bounds. If invalid,
   call U04 Sleep then Finish. Otherwise query Z, N/dn/Rn, H/dh/Rh, t, R and
   Fortress in that order.
4. If dh>8 or R≠Rh, and C is AI, clear AIColonist. Then capture effective r,
   live type abilities, signed domain d and P. Call the trespass response below.
5. Force an adjacent undefended-city opportunity when dn=1, H≠N, d=Land,
   live signed actual role<6, N's tile has no unit owner, and C→N.owner has
   none of Ceasefire/Peace/Alliance. Clear u's orders, set Z=1, replace the
   shared adjacent-foreign-civilization with N's live owner, and set ForceAttack.
6. Start damage class L at 1 if stored damage≠0, otherwise 0. If
   `signedHP[T]>>2 < unsignedDamage`, replace with 2. Independently, if
   `signedHP[T]>>1 < unsignedDamage`, replace with 3. The second test wins.
7. If the live type has cargo capacity and moves left=0, set NoMoves and enter
   the transport/work loop directly. Recovery, site and city-guard setup are
   bypassed; its later child reads must not pretend those stages ran.

For L≠0, start peaceful-territory flag A=false. Query p's radius claimant Q.
Set A when Q>0, C→Q Peace without Alliance, and
`unsigned Q.reputation - signed C.reputationCredit[Q] < 6`.
This test does not separately require Q≠C. If dn=0, set Hold and Guard, then
jump to CityGuard. If dn=1 and N=H, set ReturnNearbyCity Goto to N and Finish.
Otherwise, if dn=1 and ForceAttack, replace L with 0.

### Remote air recovery

Before the ordinary damage helper, test all of: L≠0, moves spent=0, A=false,
d=Air, live range=0, no adjacent nonallied unit, dh≥13, visibility=0, and
one of:

- L=2;
- AIRetreating is already set;
- L=3, dh≠0, dh≥24 and dn≥9.

If admitted, set AIRetreating, task Recover, and request Hold through the shared
direction route. Otherwise call the ordinary damage helper, including when L=0.

### Ordinary damage helper

Clear AIRetreating first. Consider resting when L≠0, moves spent=0, A=false, and
one of d=Land or (d=Sea, stack count<2, and either L=1 or
`unsignedDamage>>1 <= dh/2`). Additionally require d≠Sea or no human visibility,
and L=3 or live signed actual role≠P or d=Sea. Land rests only when Z=0; sea
rests only when the adjacent-nonallied query is false. If resting, set Recover
task and request Hold.

Otherwise, for L=2, d=Sea and either r=NavalSuperiority or (r=SeaTransport and
stack count<2), call U04 GoHome. Query moves left; zero goes to Finish. If
orders now Goto, set HasOrders and jump to PlanMove. Otherwise continue.

If L is now 3, run the severely damaged helper:

1. Set task SevereRecovery.
2. Request Hold when d=Land, signed r<2, (AIGarrison or signed terrain
   defense[t]>2), A=false, and any of r=Defend, maxMoves≤roadMultiplier, Z=0,
   or dh>8. These are OR alternatives; distant dh>8 admits holding here.
3. Otherwise, if d=Land, Fortress and A=false, request Hold.
4. Otherwise, if r≠SeaTransport or stack count<2, call GoHome. If orders are
   Goto, Finish without querying moves; else query moves and Finish if zero.
   If not finished, continue at ScenarioGuard. A transport with companions
   skips this GoHome call and also continues at ScenarioGuard.

For L other than 3, continue directly at ScenarioGuard.

### Trespass response

At its entry, require Z≠0, shared adjacent foreign civilization F≠0, H=N and
C→F Peace. F is the live shared value left by spatial queries, not necessarily
N's owner. Call adjustAttitude(C,F,+1). Then, using fresh F at each operation,
require F human, difficulty>Prince, attitude(C,F)>60 and rank[C]<rank[F]. Consume
`draw(0,3)`; only result 0 sets directed HostileIntent in C→the **then-current F**.
No war declaration or bilateral treaty update is added. Invalid relation writes
are excluded; a callback can change which valid relation the final store uses.

## 3. Specialized routing, guards and fortress holding

At ScenarioGuard, the historical scenario gate holds immediately when Scenario
and HistoricalScenario are active, C=1, C1→C3 lacks War, turn≤5 and live u.x≤79.
Request Hold; do not run later site/guard/movement choices.

Otherwise, if d=Land, dh=0, AIGarrison clear, H has AIFerryPort and P≠r, set
StayHome and skip specialized/site/paratrooper dispatch. Otherwise call:

1. For d=Air, A06 air orders. Its result can request PlanMove, Finish, an
   explicit direction, or continuation. It receives mutable p and HasOrders;
   preserve those changes without refreshing N/H/t/R.
2. If signed attack[T]>98, call A06 nuclear target selection, reload p from
   u's current coordinates and request its returned direction.
3. For r=Diplomacy, A07 diplomat choices: Continue, Finish, PlanMove or direct
   return 1. For r=Trade, A07 caravan choices: Continue, direction, Finish or
   direct return 1.
4. A03 city-site choices: Continue, direction or direct return 1.
5. If the live type has Paradrop, A07 paratrooper choices: Continue, Finish,
   PlanMove or direct return 0.

Next run the PartisanFortress phase in section 5. Use the resulting Fortress
flag to choose the following routing. Enter CityGuard if any is true:

- Fortress=false; p's radius claimant Q<1; or Q=C.
- C→Q Peace, `Q.reputation-C.reputationCredit[Q]<7`, and either C→Q Alliance
  or dh>2.

Otherwise, on this foreign fortress:

- If r=Defend, more than one Defend unit in stack plus current Goto enters
  CityGuard. Otherwise set task Fortify, set Hold and go to PlanMove.
- If r=Attack, set Hold for Z≠0. Query stack count; when exactly 1, live type's
  obsolete advance is nonnegative and that advance is known by anyone, also
  set Hold. A true Hold goes to PlanMove.
- Remaining cases enter CityGuard.

### CityGuard

Do nothing in the guard helper unless dh=0. Count actual Defend-role units.
When (r=Defend and count>1) or r=SeaTransport, calculate desired defenders:

1. Set divisor=4 when the same-domain nonallied-neighbor query is false,
   otherwise 3. Query Palace. If absent and P=Expand, increment divisor;
   if present, decrement it.
2. Set quota=`signed H.size/divisor+1`. Query Palace again. If now present,
   compute floor quota 1, replacing with 2 for size>3, then 3 for C.cityCount>7,
   then 4 for C.cityCount>11. Desired=max(floor quota, calculated quota).
   If Palace absent, desired=calculated quota.

For r=Defend: count<2 sets Guard. Otherwise query best defender of u's stack
with no attacker. If it is u, set Guard. For another best defender, count<desired
publishes current Defend goal priority 3. If count≤desired, set Guard. If
count>desired, save u's orders, temporarily assign Fortified and query the best
defender again. If it is now u and U01's reserve-defender aggregate≤desired,
restore orders and set Guard. Otherwise restore orders and, when
`(turn+u) mod 8=0` and restored order is Sleep/Fortifying/Fortified, clear orders.
Keep the temporary-order visibility to the defender and aggregate queries.

For r other than Defend: if stack count<2, publish current Defend priority 5.
Set Guard unless SettlerType and the same-domain nonallied-neighbor query is
false. The goal is published even in that settler exception.

After this helper, if Guard=false, StayHome sets Hold; when Hold=false enter
the transport/work loop. Otherwise go to PlanMove. If Guard=true instead,
capture live signed type defense v and query H's City Walls. Walls doubles v;
without walls, AIGarrison changes v to `3*v/2`. If live signed attack<v, set
Fortify task, assign H as home, clear StayHome and request Hold. Otherwise set
Hold and go to PlanMove. This is a direct home assignment, not the human Home
City command with its broader checks.

## 4. Transport/work loop and retained Goto

A05 owns the sea-specific prefix. In order: cargo handling; for NavalSuperiority
clear standing NavalSuperiority and SeaAttack goals at p; for SeaTransport call
transport selection, honoring direction/Finish results; if NoMoves, return 1;
then, when r=SeaTransport or Z=0, call war, trade and land ship-goal selection
in that order. A non-sea actor bypasses the sea prefix, including its NoMoves
return. The loop can be re-entered by retained-Goto handling once.

Continue in this order:

1. Pillage; Finish if chosen.
2. Partisan target/road choice; honor direction or break-to-PlanMove result.
3. A03 fortress-site classification, retaining fortress need.
4. For effective Settle with Goto, query destination city owner; present sets
   GotoCity. The flag is not cleared at loop re-entry.
5. A03 ordinary settler work when SettlerType, not ForceAttack, not GotoCity,
   p not Ocean and no city at p. Honor direction/Finish.
6. A03 role-21 work when r=21, orders not Goto, not ForceAttack, not GotoCity,
   dh≠0 and p not Ocean. Honor Finish.
7. Retained-Goto handling below; retry restarts this loop with retained context.
8. DefenderFortify below; honor direction or break-to-PlanMove.
9. A03 crowded-region population return; honor direct return 1 or Finish.
10. Otherwise leave the loop and go to PlanMove.

### Retained-Goto handling

Require current Goto, not ForceAttack, and either r≠NavalSuperiority or no
adjacent nonallied unit. Start keep=true. Query ordinary step distance to target.

- If distance>1, signed r<5 and an adjacent nonallied unit, set HasOrders=true
  and keep=false. Do **not** clear the current Goto at this branch.
- Otherwise if distance≤1, query combined destination city/unit owner Q.
  For Q present and Q≠C: a SeaTransport with stack count≥2 and RetryTransport
  false clears orders, sets RetryTransport and restarts the transport/work loop.
  This retry precedes treaty tests. In other cases, C→Q Ceasefire/Peace/Alliance
  clears orders and keep=false, unless r=Trade and a city owner exists there.

If keep remains true, Finish. Otherwise continue through DefenderFortify.
A unit outside the initial gate also continues without an invented clear.

### DefenderFortify

Require r=Defend, original t not Ocean, HasOrders=false and not ForceAttack.
Use four logical reasons, initially false:

- AloneNearHome: stack count<2 and dh<4.
- LastDefenderInDanger: Z≠0, exactly one Defend in stack, and either N absent,
  N=H, dn>4, C→N.owner War, or `N.owner.reputation-C.reputationCredit[N.owner]>6`.
- LastFortDefender: fortress need≠0 or Fortress, and exactly one Defend in stack.
- HoldFort: fortress need≠0 or Fortress, and any preceding reason or AIGarrison.

If signed attack[T]≥defense[T], clear AloneNearHome. If attack[T]>defense[T],
also clear LastDefenderInDanger and HoldFort, leaving LastFortDefender intact.
Query p's radius claimant Q. For Q>0, Q≠C, C→Q Peace and
`Q.reputation-C.reputationCredit[Q]<7`, clear every reason, even with Alliance.

If no reason remains, continue. Otherwise, when LastFortDefender or HoldFort,
clear standing Defend goals within weighted distance 1. Set Fortify task. If
attack[T]<defense[T], request Hold. Otherwise set Hold and break to PlanMove,
where an attack can still override holding.

## 5. Pillage and partisan decisions

### Pillage

Set the shared city selector to N. Query maxMoves. Require maxMoves≤roadMultiplier,
N present and human-owned, C→N.owner War, live domain Land and live attack≠0.
Capture current features. Select pillage when:

- Fortress component, Mine or Irrigation present, and dn≤3; or
- that first alternative fails, at least one of Fortress/Mine/Irrigation/Road
  present, and either (C's region city count=0 and dn<8) or captured T=Partisans.

These are raw feature-component tests; no extra airbase exclusion is added.
If selected and not ForceAttack, call W04 pillage(u,interactive=false), then
Finish regardless of its return. No AI work order or extra movement deduction
is invented before that child.

### Partisan targets

For captured T=Partisans, scan live units ascending. Choose the first live unit
v with signed actual role>4, owner Q≠C, no C→Q Ceasefire or Peace, step distance<4,
visible to captured actor-owner membership or owned by that owner, and raw
region R. Alliance alone is not a rejection here. Set HuntNonmilitary Goto to
v's coordinates and HasOrders=true, then stop scanning. Restore actor u as the
active local cursor. If HasOrders is true, break to PlanMove, including a flag
already true before the scan.

If still continuing, captured T=Partisans and the shared city selector (normally
N from Pillage) exists, is human-owned and lacks C→owner Ceasefire/Peace,
calculate weighted distance from p to that city. Scan D0…D7, retaining the last
eligible direction. Require bounds and absent unit-owner cache. A city owner
on such a tile immediately clears the selection and ends the scan. Otherwise,
weighted distance no greater than the original distance and any of Road/Mine/
Irrigation/Fortress components selects this direction. It does not reduce the
comparison distance when selecting; the last qualifying direction wins. If a
direction remains, set PartisanPillage task and request it. Otherwise continue.

### PartisanFortress

For captured T=Partisans with moves spent=0: if Z=0 and signed counter<15,
increment its modulo-256 retained counter. If Z≠0 and counter≠0, decrement it.
Signed counter>9 clears the local Fortress flag. It does not remove a map
improvement or clear a persisted unit flag.

If Fortress still true, scan cities ascending, skipping unused records and N,
and requiring raw region R. Start best distance=9999 and selected=Absent.
A strictly shorter weighted distance selects the city; an equal distance also
selects when that city's live owner=C. Thus an owned tie can replace an earlier
owned tie. If H≠N and the selected city is absent or foreign, clear Fortress.
This second-nearest-city test is distinct from merely inspecting N's owner.

## 6. Ordinary movement planner

Initialize best score B=-999, maximum unscaled score M=-999, best direction=Hold,
Explore=false and selectedAttack=false. For r=SeaTransport, dh≠0, stack count>1
and current Goto, set HasOrders. Query same-domain nonallied adjacency at p.
When false, set Explore to !HasOrders; when true, clear last direction instead.
Scenario plus SuppressExploration disables Explore. The Scenario test is distinct
from the Bloodlust rules bit; Appendix A resolves a reconstruction mismatch.

For r=Defend, compare captured T's live signed attack and defense. If attack>
defense and C's region lacks StrongerEnemy, replace r with Attack. Independently,
if attack≥defense and P=Attack, replace r with Attack. Retain the changed r in
this shared context; do not alter the rule's stored role.

Scan D0…D7 in order, performing the complete candidate procedure below before
advancing. B and M are separate accumulators. They can differ even when one
direction is best under both scoring variants.

After all eight candidates:

1. If selectedAttack, query moves left and require at least
   `roadMultiplier-selectedAttackClass`; otherwise replace direction with Hold.
2. If M≠B, replace direction with Hold. This is an equality test on the two
   scores, not a comparison of their winning direction identities.
3. Hold direction clears selectedAttack. If HasOrders and !selectedAttack,
   Finish without replacing task or direction/target fields.
4. If Hold and !selectedAttack, force Hold direction. Set ChooseMove task and
   pass the selected direction to the shared direction route.

### 6.1 Candidate admission

Form candidate q=p+D[k], wrapping x once; reject if out of bounds. Capture Q
from W01's **occupant owner** query, terrain v and whether v is Ocean. Reject
Ocean for cached d=Land. Query the first unit at q. Walk past leading units with
signed actual role>4 while a nonnegative successor exists; stop at the first
military role or the last remaining unit. Call this retained selected unit V.
The owner Q remains the tile cache, not a fresh read of V.owner.

Reject if any applies, in the following order:

1. Z≠0, d=Land, V absent, q has enemy ZOC, captured IgnoreZOC is false and q
   is not Ocean. The original contains another Land/Ocean enemy-occupant test;
   the earlier unconditional Land/Ocean rejection already excludes that case.
2. d=Sea and q is land, unless V exists, Q≠C, original t=Ocean and captured
   Submarine ability is clear. This allows a naval attack candidate, not general
   sea travel over land.
3. V exists and Q=C, stack count≥2, no city owner at q, and no fortress without
   City there. Two friendly units in an unfortified noncity tile block admission.

Next perform the base-score procedure. Its own rejection ends the candidate.
For d=Air, consume a fresh `draw(0,2)` and **replace** the base score; the earlier
base draw and child effects remain consumed.

If signed lastDirection≥0, compute h=abs(lastDirection−k), then if h>4 replace
h with 8−h. Subtract `2*h*h`. This is not modulo-8 normalization: an accepted
positive stored direction above 7 can produce a negative folded h, then square.
Start candidateAttack=false and withdrewFromCity=false.

### 6.2 Base score

Start S=0 and take the first applicable branch:

- SettlerType: if original t=Ocean and same-domain nonallied adjacency at q,
  reject before drawing. Otherwise S=draw(0,4).
- Otherwise visibility of u exactly zero and original t not Ocean: draw(0,2).
  For r=Attack subtract `2*signed terrainMoveCost[v]`; for other roles subtract
  signed terrainDefense[v]. This branch favors different terrain from the
  ordinary visible-unit branch below.
- Otherwise S=draw(0,4), followed by the applicable adjustments below.

In the last branch, if V absent or Q≠C: for signed r<6, add
`4*signed terrainDefense[v]`. Otherwise add
`6-(signed terrainMoveCost[v]-1)*roadMultiplier`. Then, only for these r≥6
roles, dn<4 and H≠N, query step distance q→N. At distance 1, add 8 when the
fresh signed move cost≤roadMultiplier, otherwise subtract 4. At distance 2,
fresh cost≤roadMultiplier plus Road adds 4.

With V present and Q=C, first test Unit feature present, City and Fortress
components absent, and adjacent nonallied unit at q. If true use the first table;
otherwise use the second. Each aggregate/role count is a fresh U01 query in
listed order. Roles not listed add zero.

| Role | Exposed friendly stack next to a nonallied unit |
| --- | --- |
| Attack | Let n=count Attack. If n<1 and count Defend≠0 add 8; otherwise subtract 8*n. |
| Defend | Let n=count Defend; add 8 for n<1, otherwise subtract 4*n. |
| NavalSuperiority | Add 8 if count SeaTransport≠0; then subtract 4*count NavalSuperiority. |
| SeaTransport | Add 8 if count NavalSuperiority≠0; then subtract 8*count SeaTransport. |
| Settle or role 21 | Subtract 4. |
| Diplomacy | Query count Attack then Defend; a nonzero sum adds 8. |

| Role | Other friendly stack |
| --- | --- |
| Attack | Query productionCost then count; add `(4*productionCost)/(count+1)`. |
| Defend | Query attack then productionCost; add `(2*attack)/(productionCost+1)`. |
| Settle | Capture signed terrainDefense[v], query productionCost, add `2*(defense+productionCost)`. |
| Diplomacy | Query count Attack then Defend; a nonzero sum adds 8. |

The Settle/role-21 table entries remain relevant when the parent skipped A03's
SettlerType assignment. Do not eliminate them as unreachable based only on a
unit's ordinary rule role.

### 6.3 Peaceful territory, owner penalty and empty targets

When V absent, q not Ocean and u's live signed actual role<5, apply both parts:

1. If dn<4, N.owner≠C, C→N.owner Ceasefire or Peace, no Alliance, and either
   Peace or attitude(C,N.owner)<50, measure weighted distance e from N to q.
   If e>dn and dn<3, add 5 and set withdrewFromCity. If e<dn, reject. If e=dn,
   subtract 5. These comparisons use cached dn, not a refreshed p→N distance.
2. Query q's radius claimant J. For J>0, J≠C, C→J Ceasefire/Peace without
   Alliance: use `age=J.reputation-C.reputationCredit[C]`. The credit is indexed
   by **C itself**, not by J. Peace plus age<7 rejects unless withdrewFromCity.
   Otherwise, Peace plus a freshly evaluated age<7, or attitude(C,J)<50,
   subtracts 5; then Irrigation/Mine/Road components at q subtract another 3.

Next apply the owner penalty for every admitted candidate, even without V:
for Q=0…7, C→Q Alliance subtracts 6. For Q=Absent, subtract 6 exactly when
`positiveModulo(C.subversionCount,16)>=8`. This unusual dependency is the
original's backed read through the relation index, not an Alliance with a
fictional civilization. For accepted Q=8, use
`positiveModulo(C.attitude[0],16)>=8`; other unsupported read backing must be
supplied or stopped. These reads perform no out-of-bounds writes. Appendix A
records the original field mapping and producers.

If V absent, query city owner at q. For a present city and Q≠C, reject when
C→Q has Ceasefire/Peace/Alliance and any of Q.reputation<7, no Vendetta, or
Alliance. Otherwise replace S with 999, clear HasOrders and clear Hold. This
can affect later candidates even if this direction does not ultimately win.
Then a hut at q adds 20. Do not require a military role for this empty-city/hut
score replacement.

If V exists and Q≠C, run section 7. If V exists and Q=C instead, subtract u's
live signed defense. Continue to the common scoring tail only if not rejected.

### 6.4 Exploration and final candidate selection

For dh≠0, d=Sea and either V absent or Q=C, query q's raw region then C01's
adjacent-sea selection for H. Equal regions add 10. Missing H is not replaced
with an invented coastal city.

Reject an Air candidate at a foreign airbase with no V. Then, when Explore:

1. Form lookahead l=p+4*D[k], wrapping x once. If valid and unrevealed to C,
   add 8 for land or 2 for Ocean.
2. Scan D0…D7 around l, wrapping each x and testing bounds. The center's own
   invalidity does not prevent in-bounds neighbors from being tested.
3. At each valid neighbor, unrevealed and (land or d=Sea) adds 2. A present
   unit-owner cache subtracts 2, regardless of owner.
4. For SettlerType, query terrain and add its signed base food. In nonzero game
   mode with double production, query terrain again and add that fresh signed
   food again. Neither resource bonuses, city ownership nor work legality is
   part of this food term.

At map row 0 or height−1, divide S by 3. Capture unscaled U=S. If candidateAttack,
moves left<roadMultiplier and no city owner at q, query u's stack production
cost then attack, clamp attack to 1…99, and replace
`S=(productionCost/clampedAttack)*S`. Divide before multiplying.

If S>B, set B=S, selected direction=k, selectedAttack=candidateAttack and retain
this candidate's attack class. Ties keep the earlier direction. Independently,
if U>M, set M=U. The special peaceful-diplomat case in section 7 instead writes
B/M directly and bypasses this tail, including exploration/polar/scaling.

## 7. Foreign-unit attack and bribery scoring

This phase starts candidateAttack=true and attack class=0. V is initially the
military/last occupant selected during admission, not yet the combat defender.
Reject immediately for SettlerType or live actual Trade role. Also reject:

- d=Sea with nonzero live cargo capacity, q on land and live signed attack<3;
- d=Sea with cargo capacity and stack count>1, when actor attack≤V.defense or
  V.attack≤actor defense, using live signed rule values;
- p currently Ocean, d=Land and live Amphibious ability absent.

### 7.1 Admission to combat preview

Enter ordinary attack scoring when C→Q lacks Ceasefire/Peace and either Q is AI,
or (r≠Diplomacy and `5*Q.reputation + attitude(C,Q) + draw(0,19) - 10 > 50`).
Reputation is captured before the attitude call and draw. Alliance alone does
not exclude this route. If the route fails, use section 7.3 instead.

Replace V with U06's best defender of its stack against u. Reject ranged Air V
unless actor has AttackAir, a city or airbase exists at q, or the defender stack
contains an aircraft carrier. Domain/range/ability and the spatial/stack queries
are made in that order, stopping when an admitting alternative succeeds.

If V's live role is Trade and H=N: require C.government<Q.government or C→Q
Vendetta. Then consume `draw(0,Q.government-C.government)` using fresh government
values and require nonzero. A negative/reversed range under a Vendetta remains
F01 behavior; do not swap it here or skip its draw.

### 7.2 Attack value

Call U06 combat preview `(u,k,execute=false)` and retain its returned value a.
Query V's stack production cost w, then actor's live signed rule cost c. Replace
`a=((w+1)*a)/c`. A zero divisor stops after preview and stack effects.
Then, in order:

1. City owner present at q multiplies a by 3.
2. r=NavalSuperiority and V.role=SeaTransport doubles a.
3. If Guard=false, apply the following. For r=Attack: P=Attack doubles a;
   capture actor attack and query actor stack attack, and when the latter>
   twice the former, double a and set class=1; any Attack-role unit in V's stack
   doubles a and sets class=1; a nonzero current Attack-goal priority at p
   doubles a. With NormalCombat, query max HP then remaining HP of V; remaining
   ≤max/3 doubles a and sets class=1. V's Submarine ability multiplies a by 3.
4. If Guard=true instead, halve a; if V.attack≤actor defense, replace a with 0.
   The nonguard multipliers above do not run.
5. Captured T=Partisans and V's signed role>4 sets class=2, replacing class 1.
6. Read V's role again. For Q=0 and Trade, halve a only when civilization 0 is
   AI. Otherwise for Q=0 and signed role>4, add 12. Otherwise Diplomacy doubles a.

Require a≥12 for Q=0, otherwise a≥6. If admitted, add `4*a` to S and continue
to section 6.4.

If below threshold, set S=-999. A fallback bribe is attempted only when P=Attack,
r=Attack, d=Land, V Veteran or Fortified, (signed terrainDefense[v]>3 or q is a
fortress without City), V stack count<2 and no city owner at q. Consume
`draw(0,3)`; result 0 invokes V06 bribe(V,C). Regardless of whether a bribe was
attempted or succeeded, reject this candidate after retaining its effects.

### 7.3 Noncombat route and opportunistic bribery

First test the peaceful-diplomat exception: C→Q Ceasefire/Peace, initial V's role
Diplomacy, its stack count exactly 1, actor attack nonzero, and C01's unrestricted
nearest city to q owned by C. The nearest-city result is directly read; a missing
city is a read boundary rather than false by default. If admitted, replace S
with 60. If B<60, assign B=M=60, selected direction=k, selectedAttack=true and
selected class=2. Equal B retains the earlier choice. Reject/finish this
candidate without passing through section 6.4.

Otherwise opportunistic bribery requires either:

- r=Diplomacy and (P=Attack, P=Expand or C→Q lacks Ceasefire/Peace); or
- P=Attack and r=Attack.

If neither holds, reject. If `(r=Diplomacy or H=N) and difficulty>Prince` is
false, additionally require V Fortified and a nonzero V04 hostile-query result
for at least one human civilization in slots 1…7. Scan all seven slots and keep
calling for later humans after the first success. This query is V04's specific
helper, not a replacement “at war” flag test.

Require d=Land. If Q is AI, scan humans 1…7 again, calling hostile(Q,human);
any zero result rejects the bribe route after completing that scan. For human
Q, skip this second scan. Then scan humans ascending and require no human has
a nonzero retained city count in R; this third scan stops at its first failure.
Require V stack count<2 and no city owner at q.

Consume `draw(0,7)`. Result 0 attempts bribe immediately. Otherwise, if N exists
and its live owner is human, also attempt bribe. The draw is still consumed
when the human-neighbor alternative would suffice. Call V06 bribe(V,C) when
admitted, then reject this direction regardless of the child's outcome.

## 8. Direction route and common finish

A requested Hold direction clears lastDirection and orders. Other admitted
directions update the parent's p by D, wrapping x once. If in bounds, assign
AI-Goto then retained target x and y in that order. If out of bounds, clear
orders and call U02 movement exhaustion. Then always run Finish; it may exhaust
again. Do not turn an invalid destination into immediate success or relocation.
Directions outside the caller's established domain require backed read evidence
or a stop; they are not reduced modulo 8 automatically.

Finish reads live orders:

1. For None, AIIdle, Fortifying or Fortified: AIIdle first sets Idle task.
   If live type domain Air, clear orders, add roadMultiplier to retained moves
   spent with modulo-256 wrap, and return 1 immediately. Otherwise exhaust moves.
   With cached domain Land and live actual role not Settle, assign Fortifying,
   replace with Fortified when AIGarrison, then clear orders when StayHome.
   Other cached domains or actual Settle clear orders.
2. Next, if current Goto target exactly equals u's live coordinates, set
   GoalReached and clear orders. If movement was already spent, exhaust it
   unless live actual role=NavalSuperiority and there is **no** SeaTransport
   in its stack. A warship accompanied by a transport does exhaust movement.
3. Return 0. Work, Sleep, AI-Goto and other orders otherwise remain as read;
   they are not automatically executed here.

The early work/sleep paths need no cached-domain read unless a child changes
orders to a branch that consumes it. Such changed-input cases need the original
retained domain supplied or a stop. Ordinary execution is owned by U04/U02/W04,
with its own return, randomness and failure boundaries.

## 9. Immediate response to an attack

`respondToAttack(C,target)` first scans live units ascending. At the first live
owned unit with Paradrop ability, a city owner at its current tile, either
moves spent=0 or AIGarrison, not Paradropped, and step distance≤paradropRange,
call U05 paradrop(unit,target) and return immediately regardless of its result.
It does not require that departure city's owner to be C, target bounds here,
or independent confirmation that the landing is legal. Later units and the
following response pass are skipped.

If no such paratrooper exists, capture target occupant owner Q and raw region
R. Scan units ascending again. Admit a live unit when:

- owner=C, or owner≠Q with directed C→owner Alliance and that owner AI;
- live domain Land, attack nonzero, moves left nonzero;
- actual role≠Defend or no city owner at its current tile;
- step distance to target<7 and raw region=R.

Set U03 path owner to the unit's live owner, type to its live type, then goal x/y
to the supplied target. Query moves left again and request next direction with
allowance twice that result. **Only a positive direction other than 8** assigns
ordinary Goto, target x/y, then RespondToAttack task. Direction 0 is ignored,
despite being a valid nominal direction; negative or Hold also leaves orders
unchanged. These direct assignments do not invoke the AI goto setter.

Continue the scan after successes. Do not reset road-planning mode or restore
other path service fields; this function does not establish their initial mode.
No direct random draw is made here. U03 and the paradrop child retain their own
side effects, bounds/permission checks and failure outcomes.

## 10. Conformance cases

Unless overridden, finite phase fixtures use C=1 at (20,20), Land Attack role,
attack=2, defense=1, cost=2, HP=10, visible to civilization 1, no damage or spent
moves, last direction Absent, orders None, original terrain Grassland and region
1. N=H=0 at (24,20), size 5, dn=dh=3 and Rn=Rh=1. Policy is Defend, all control
flags false, one city in the region, no facilities/advances/treaties. Map size is
100×100 cylindrical with W01's nominal D table. Unmentioned tiles are revealed
Grassland, base defense 2, move cost 1, no occupants/features/claimant/hut.
Road multiplier is 3, supplied movement allowance/remaining movement 6, turn 10,
difficulty Prince. Prescribed secondary draws return 0 at each reached call.
Child adapters record effects; controlled-choice cases do not invent internal
random context state after a call.

The ordinary enemy fixture places V=1 at D0=(21,19), owner 2, Defend role,
attack=2, defense=1, cost=3. Combat preview returns 3 and best-defender selection
returns V. Source checks below use those supplied child answers; they do not
execute combat, bribery or U03 search.

| Case and overrides | Expected result |
| --- | --- |
| SettlerType base score, no occupant | One draw(0,4), S=0; no visible-unit terrain bonus. |
| Actor visibility=0, Attack, candidate move cost 3 | One draw(0,2), S=−6. Ordinary visible Defend instead gets S=8. |
| Air candidate D0 | Consume the ordinary base draw and a second draw(0,2); replace score with 0. |
| Unowned D0 and C.subversionCount=8 | Base 8 becomes 2 through the backed owner penalty. |
| Full eight-direction planner, default fixture | Eight draws; B=M=8; strict tie chooses D0; task ChooseMove. |
| Scenario and SuppressExploration set | Explore=false. Bloodlust plus SuppressExploration without Scenario leaves Explore=true. |
| Settler lookahead, all neighbors food 2, nonzero mode and double production | Eight neighbors add 32 in total; each doubled term uses a second terrain query. |
| Ordinary enemy fixture | Attack value `(3+1)*3/2=6`; S=8+24=32, class 0, selectedAttack=true. |
| Preview 4, actor/defender stack-cost answers 4, attack aggregate 2, one move left | Scaling makes B differ from M; planner chooses Hold even when the scaled candidate has the highest score. |
| Human enemy, reputation 7, attitude 30 | Consume base draw then draw(0,19); pressure 55 admits attack preview. |
| Lone foreign diplomat under Peace, candidate's nearest city owned by C | Write B=M=60, selectedAttack=true, class 2; skip exploration, polar division and stack scaling. |
| Preview 0, Attack policy, Veteran defender, terrain defense 4 | After base draw, draw(0,3)=0 invokes bribe; candidate remains unselected. |
| Noncombat enemy under Peace, Attack policy, King, N=H, no human region cities | Base draw then draw(0,7)=0 invokes opportunistic bribe; direction remains unselected. |
| Land candidate Ocean; or Sea candidate empty land; or blocked Land→enemy-ZOC move | Reject before consuming any draw. |
| Candidate closer to a peaceful foreign N with dn=2 | Reject after consuming its base draw. |
| Empty foreign city, no protected treaty, HasOrders and Hold initially true | S becomes 999; clear both flags before final comparison. |
| Single Defend at owned city | Guard=true. A lone SettlerType instead publishes Defend priority 5 but does not guard when adjacency is clear. |
| SeaTransport at size-5 Palace city, civilization city count 12 | Desired defender quota at least 4; lone nondefender publishes Defend priority 5. |
| Defend alone, attack 1/defense 2, dh=3 | Fortify task and Hold direction. |
| Defend, attack 3/defense 1, dh=5, fortress need 1, only Defend in stack | LastFortDefender survives attack/defense filtering; clear standing Defend within radius 1, set Hold and proceed to planner. |
| Light damage, no spent movement, safe Land, AIRetreating initially set | Clear AIRetreating, set Recover and request Hold. |
| Damage class 2 warship, movement already spent, GoHome supplies Goto | Set HasOrders and enter PlanMove. |
| Severe Attack, terrain defense 3, Z≠0, dh=9 | The dh>8 alternative admits holding on defensive terrain. |
| Unmoved Partisan counter 9, Z=0 | Counter becomes 10; local Fortress clears. |
| Slow Land attacker near a human enemy city under War, current Irrigation | Invoke noninteractive pillage and Finish. |
| Trespass: foreign human, King, initial attitude 60, rank C lower | Adjust to 61, then draw(0,3)=0 sets directed HostileIntent. |
| NavalSuperiority Goto already at current tile, movement spent | With no SeaTransport, retain remaining movement; with a transport, exhaust it. |
| Trade Goto one step from a foreign peaceful unit without city | Clear Goto. An empty foreign city instead retains Goto; unit presence is not the exception. |
| Full chooser owner 0, W06 adapter returns 7 | Return 7 directly with no ordinary setup. |
| Full chooser current BuildFortress | Return 0 without spatial/scoring calls; existing work order remains. |
| Full chooser off-map actor | Invoke Sleep, retain Sleep and return 0. |
| Full chooser default ground fixture through actual selected A03 bodies | Eight draws, task ChooseMove, AI-Goto target (21,19), return 0. |
| Full chooser historical scenario, C=1, turn 5, x=20, no C1→C3 War | Hold before ordinary scoring; no draws, orders Fortifying after Finish. |
| Full chooser damaged range-0 Air unit, damage 3, invisible, dh=dn=13 | Remote recovery sets AIRetreating, adds 3 spent moves and returns 1; no draws. |
| Response-to-attack supplied direction 0, then a separate case direction 1 | Direction 0 leaves orders unchanged. Direction 1 writes Goto, target and RespondToAttack task, retaining path fields. |
| Eligible first paradrop unit standing at a foreign-owned city | Invoke paradrop and return even when its supplied result is failure; no ground-response path query. |

Accepted nonstandard cases also exercise positive lastDirection=127 (fold once,
then square; no automatic modulo), unsigned movesSpent=254 plus Air Finish
multiplier 3 (retains 1), and Partisan counter 128 under ZOC (decrements to 127).
A custom Land unit with cargo capacity and no moves enters the shared
transport/work loop; a qualifying pillage still occurs. These are scalar/custom
rule behaviors within valid storage. A narrower input policy may exclude its
declared nonstandard inputs; none requires an out-of-bounds overwrite.

## Appendix A. Original representation and evidence differences

Primary pin: `a71e8aefffe04e6f719e0e213b027b128a747711`, inspected at
`fc2155ccdb2123c1137babab6fa39e38152278c9`. Independent reconstruction:
`civ2` at `4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`.
Original executable SHA-256:
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.

### Exploration gate disagreement

The primary source uses `game.game_flags & GAME_SCENARIO`; the independent
reconstruction uses `gGameParameters.customRules & 0x80` in this one gate.
Fresh bounded original disassembly at `005415ae`…`005415d4` resolves it:
`005415b0` reads word `00655af0`, then tests low mask 0x80. Game state begins at
`00655ae8`, so this is its offset-8 game/map flags, not offset-0 custom rules.
Only after that test does `005415be` read scenario flags at `0064bc60` and test
0x08. Use Scenario+SuppressExploration. Bloodlust alone does not disable the
lookahead. This static instruction check is not a native gameplay replay.

### Shared-entry and Trade-Goto differences

The independent reconstruction hoists pillage into its normal non-sea route and
skips it on transport re-entry. The original shared transport entry at `0053be12`
checks cached domain, and `0053be19` sends non-sea actors to `0053f012`, the
pillage prefix. Naval-goal exits at `0053da75`, `0053ee87` and `0053eea0` also
join that prefix. It captures N then calls thunk `00401820` at `0053f025`;
the thunk targets max movement `005b2a39`. Retain that common entry, including
an exhausted custom Land carrier and normal returns from naval children.

For a Trade unit's nearby foreign Goto, the independent source queries the first
unit at the destination. Original `00540f75` instead calls `004017e9`, whose
jump target is city-owner query `005b8ca6`. A nonnegative result bypasses the
order clear at `00540f85`…`00540f98`. Section 4 therefore tests for a city, even
when it is empty. The two focused cases above distinguish the predicates.

These are fresh bounded instruction/target checks and source comparisons, not
changes to either reference implementation or saved-analysis annotations.

### Backed owner-index read

At `0054230c` the original tests byte
`0064c6c0 + C*0x594 + Q*4` against 0x08. The civilization base is `0064c6a0`;
its relation array begins at offset 0x20. Q=−1 therefore reads offset 0x1c,
the low byte of the retained signed16 subversion count. Q=8 reads offset 0x40,
attitude[0]. Both reconstructions expose the same original read. The independent
BF-288 owner validation narrows later inputs; it does not prove that the original
returned only slots 0…7. W01 owns the original owner-query domain.

V06's buy/bribe operations and F06's persisted civilization state are real
producers of subversion count; its current value is not arbitrary stack input.
For these known aliases, derive the predicate from that state rather than zero.
Other unbacked selector reads require supplied backing or a bounded stop. This
read contract authorizes no writes outside the intended relation array.

### Task and flag mappings

| Logical task/state | Reference encoding |
| --- | --- |
| Recover / ReturnNearbyCity | `'d'` = 100 |
| SevereRecovery | `'D'` = 68 |
| Fortify | `'F'` = 70 |
| HuntNonmilitary | `'P'` = 80 |
| PartisanPillage | `'p'` = 112 |
| ChooseMove / Idle | `'9'` = 57 / `'0'` = 48 |
| RespondToAttack | `'K'` = 75 |
| HistoricalScenario / SuppressExploration | First scenario flags word 0x8000 / 0x0008. |
| HostileIntent / War | Directed relation 0x20 / 0x2000. |

F02/U04 own unit order and state encodings, including AI-Goto=27, ordinary
Goto=11, None=−1, and AIIdle=16. The work fast path is orders 4…9 inclusive;
BuildAirbase=10 does not take it. W01 owns direction Hold=8 and feature
components. Tests for raw improvement groups correspond to 0x4c, 0x5c and
0x1c; the exposed-stack test is `(features&0x43)==1`. These are component
predicates in the algorithm, not mandatory bit-packed storage.

The primary function's FRAME_PRESETS are sampled original stack contents, not
universal initial state. B13/B19/B24 and independent BF-005/BF-008/BF-009/BF-090 transport
repairs are distinguished by [A05](A05-ai-naval-and-transport.md#appendix-a-original-representation-and-evidence-distinctions),
which specifies retained inputs and actual read aliases. No write corruption
is required by those sampled values. Only actual producers or supplied retained
reads may satisfy a later input; an unresolved branch must stop at its boundary.

## Appendix B. Evidence and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

| Contract | Primary source at the pinned revision |
| --- | --- |
| Shared chooser | [ai.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/ai.cpp): `ai_unit_choose_orders`, original `00538a29`. |
| Guard/recovery/order routing | Same source: `ai_orders_city_guard`, `ai_orders_trespassers`, `ai_orders_damaged`, `ai_orders_badly_damaged`, `ai_orders_keep_goto`, `ai_orders_defender_fortify`, `ai_orders_finish`; extracted children of that original entry. |
| Pillage/partisans | Same source: `ai_orders_pillage`, `ai_orders_partisan`, `ai_orders_partisan_fortress`. |
| Movement scoring | Same source: `ai_orders_plan_move`, `ai_orders_move_score_dir`, `ai_orders_move_base_score`, `ai_orders_move_attack_score`, `ai_orders_move_explore_bonus`. |
| Immediate response | Same source: `ai_respond_to_attack`, original `0057febc`. |
| Layout and domains | [civ2types.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h), [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h); U01 owns aggregate meanings, W01 map tables. |

Independent `src/ai_unit.cpp` and `src/combat_entry.cpp` corroborate the selected
entry/recovery/guard/partisan/scoring/finish/response branches, with the explicit
differences resolved in Appendix A. Source comments suggesting that a warship
with a transport keeps its moves contradict the implemented/original condition;
section 8 uses the actual test. Twelve record definitions, the enum header and
all seventeen A04 bodies matched the primary pin. Reference checkouts were clean
and the original executable hash matched.

A temporary Linux harness compiled **24 complete bodies**: all seventeen A04
bodies plus A03's seven owned procedures. The actual reference-member shared
context was retained, so the full chooser and its selected children share their
local writes. The suite passed **49 assertions** in optimized and
AddressSanitizer/UndefinedBehaviorSanitizer builds, using signed char, wrapping
arithmetic and disabled strict-alias optimization.

Adapters supplied map/knowledge/facility queries, secondary range draws, stack
aggregates, goal operations, nearest-city answers, combat preview/defender
selection, bribery, movement, path directions, home travel, lifecycle and
presentation boundaries. Known relation read aliases used the complete backed
civilization record instead of invoking a host C++ array overrun. Invalid
relation read backing stops explicitly. A05/A07 specialized test boundaries
throw if reached; the sampled full-parent cases do not enter them. Air/nuclear
and barbarian child answers are declared adapters; actual specialized algorithms
remain with their owners. The primary source's sampled FRAME_PRESETS are not
validated as universal values; sampled paths do not consume the unresolved A05
slots. Successful sanitizers on this finite domain do not qualify arbitrary
stale/corrupt input paths.

These are recovered-source checks, not original executable gameplay replays.
They do not validate U03 path search, U06 real combat preview/callback state,
V06 bribery, W04 actual pillage/work, complete A01 goal insertion/assignment,
all specialized A05–A07 branches, presentation/network callbacks or consecutive
human/AI turns. The ordinary ground-parent case joins selected A03 source
phases but is not a complete world-state producer/consumer test. X02 must add
those joins and independent full-state/trace comparisons, preserve ordered
random calls and supplied read boundaries, and exclude invalid writes.
