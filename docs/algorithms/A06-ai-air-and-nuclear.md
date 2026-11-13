# A06 — AI air and nuclear target selection

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. This document owns the specialized air chooser and nuclear
city targeting, including relocation before an attack. [A04](A04-ai-ground-tactics.md)
owns entry routing, captured context, generic movement scoring, direction
application and common Finish. [A01](A01-ai-strategy-and-assessment.md) owns
roles, regional/civilization summaries and active-unit orchestration.

[U01](U01-unit-lifecycle-and-stacks.md) supplies relocation, stack traversal and
aggregates. [U02](U02-movement-and-zones-of-control.md) supplies movement allowance,
remaining movement and directional dispatch. [U04](U04-unit-orders-and-activation.md)
owns SetGoto and GoHome. [U05](U05-transport-and-special-movement.md) owns actual
fuel/basing and exceptional movement; [U06](U06-combat-and-veterancy.md) combat;
[U07](U07-nuclear-attacks.md) nuclear delivery, interception and blast.
[W01](W01-map-geometry-and-spatial-state.md) supplies map queries, bounds,
wrapping, ordinary distance and D0…D8. [C08](C08-buildings-and-wonders.md) supplies
facility queries; [V04](V04-diplomacy-and-treaties.md) directed treaty state and
bilateral mutations. [F01](F01-determinism-and-arithmetic.md),
[F02](F02-game-state-and-invariants.md) and [F03](F03-rules-and-configuration.md)
own shared arithmetic, state and rule interpretation.

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

## 1. Inputs, outputs and arithmetic

The air chooser takes actor u, A04's captured type T, mutable working coordinates
p, effective role r, mutable HasOrders, captured own-city distance dh, raw region R
and captured enemy-ZOC Z. It captures original u as actor and signed owner C.
Target scans use separate cursors; they do not change which unit receives orders.
Live type/rule reads and cached-T reads below are deliberately distinct.

The result is one of:

- ApplyDirection(D0…D7 or Hold), with any explicitly changed working p;
- Finish, which enters A04's common completion;
- PlanMove, which enters A04's shared scorer with HasOrders retained/set;
- Continue, which lets A04 continue its specialized dispatch.

A04 calls the air chooser for captured Air domain. After a Continue result,
its next nuclear gate tests **signed attack of T >98**, independently of domain
or a named Nuclear Missile type. Nuclear selection returns a direction/Hold;
A04 then reloads working p from the actor's actual coordinates and applies that
result. A successful selection is not proof that U02/U06/U07 executes an attack.
A relocation or treaty effect made here remains if the later move fails.

Distances calculated in this document use W01 **ordinary distance**. Incoming
dh retains A04's weighted nearest-city distance and is only tested against zero
here. D0…D7 are W01's ordered neighbors and D8 is the center. Wrap x once;
bound-check where explicitly stated.

Use signed8 views for rule attack/defense/HP/firepower/cost/hold/range, city size
and unit counter in fuel comparisons; the counter's zero test is simply zero.
Civilization military strength and per-type active counts are unsigned retained
quantities. Civilization city count and last-contact turns are signed16. F01
defines reference results for local addition, subtraction, multiplication and
left shift; equivalent evaluation need not perform redundant conversions. Right shifts are arithmetic; division truncates toward zero.
Where §7 uses unsigned strength comparisons, retain those stated unsigned views.
A zero road multiplier faults at its reached division after earlier effects;
do not silently supply reach zero. Local coordinates are full signed32 values;
U01/U04 apply the specified stored-coordinate retention on mutation.

None of these six decision bodies consumes a direct RNG draw. Their children
retain their own contracts; do not add random tie breaking to a deterministic
scan. Live values are re-read at the described operations. In particular, query
side effects and relocation can change later coordinates, owners or summaries.

Nominal input has live units/cities, valid rule/owner identities, consistent
stacks and a positive road multiplier. Accepted unusual data retains the exact
signed range/counter arithmetic, negative scores, zero counts and wrapped scalar
stores. A stricter implementation may declare/reject a narrower input domain.
Unbacked reads require supplied original state or a bounded stop. Invalid writes
are **excluded bugs** under [X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs):
stop before the store unless its safe omission and continuation are established.
Keep earlier valid effects; do not reproduce corruption through adjacent fields.

## 2. Air range, recovery and dispatch

Calculate remainingReach before any specialized choice:

1. If the live type's range=0, obtain maxMoves(u) and double it.
2. Otherwise capture signed range f and signed counter k; obtain maxMoves(u),
   then movesLeft(u). Calculate `(f-(k+1))*maxMoves + movesLeft`, retaining each
   intermediate signed32 result.
3. Divide by the unsigned road multiplier to obtain reach.
4. Set homeDistance=9999. Scan live owned cities ascending; calculate
   distance(p,city) and retain a strict smaller value. There is no carrier or
   airbase substitution in this scan, and a missing owned city leaves 9999.

Recovery uses fresh live range reads:

- If range=1 and actor order is not Goto: query maxMoves, then movesLeft.
  If movesLeft≤(maxMoves>>1), reach<homeDistance+1 and homeDistance≤reach,
  set task AirReturn, call U04 GoHome, and return Finish.
- If range is nonzero and not 1: when signed range/2≤signed counter and
  homeDistance≤reach, set AirReturn, call GoHome and return Finish.
  This branch applies even with an existing Goto.

Then run destroyed-after-attack selection (§3). If it returns a result, return
it immediately. Otherwise, if r=AirSuperiority, run target selection (§4), then
strike/basing (§5), honoring its result. If no target result and dh≠0, call
GoHome and return Finish. Next run the Attack-role city raid phase (§6),
returning its result if any. Otherwise return Continue to A04.

The effective-role input governs r, not a fresh rule role. GoHome is an actual
child with its own carrier/airbase/city behavior; this chooser does not replace
it with a nearest-city Goto or require that it successfully publishes an order.

## 3. Destroyed-after-attack units

Enter when the actor's live type has DestroyedAfterAttack and signed cached-T
attack<99. This phase precedes the ordinary AirSuperiority and city-raid phases.
Initialize best=`signed live actor cost*4` and target=Absent.

### Choosing a target

Scan live units V ascending. Require foreign owner, C→V.owner War, actual
non-Air domain and in-bounds coordinates. Obtain maxMoves(u), capture the road
multiplier, then distance(p,V); require distance≤maxMoves/multiplier.
No visibility test, remaining-moves test or attacker-versus-defender strength
comparison is added here.

Set score to signed live cost of V. Define specialTile as:

- the tile has Fortress but no City component; or, if that is false,
- W01 airbaseOwner(tile) is **not zero**, including Absent (-1).

This nonzero test is intentional compatibility behavior. A normal tile without
an airbase normally satisfies the second alternative. Do not replace it with
“airbase exists.” Apply the following branches and modifiers in order:

1. If specialTile=false and there is no city owner, replace score with the
   stack's total production cost.
2. Else, only if specialTile=false: double score when the stack count=1;
   then scan D0…D7 around V, doubling once more at the first in-bounds neighbor
   whose unit owner is C, then break this neighbor scan.
3. Double score if V's tile is ocean.
4. Re-read features; double score if Fortress without City component.
5. Add V's signed live hold capacity.
6. If score≥best, replace best and target. Later ties win, and equality with
   the actor-cost threshold is sufficient.

This is a heuristic using rule cost/hold, not a combat preview. The separately
queried airbase-owner, city-owner and improvement values keep their W01 semantics.

### Relocation for an attack

If a target was selected, capture its current coordinates. Scan its D0…D7
neighbors, wrapping x. At the first in-bounds tile with no city-or-unit owner:

1. Call U01 relocate(u,tile,mode=1).
2. Clear actor visibility.
3. Write working p.x, then p.y to that neighbor's full local coordinates.
4. Return the opposite D direction (four positions around the eight-direction
   cycle) through ApplyDirection.

There is no ocean, airbase, route, range-to-staging-tile or diplomacy test on the
staging tile. This is an actual relocation before A04's attack dispatch. If all
neighbors fail, continue to the fallback below; do not try the next-best target.

### Redistribution when no staging tile was selected

Query actor stack's ExpendedAttackers aggregate, meaning count of members with
DestroyedAfterAttack. If it exceeds 1, scan live owned cities ascending:

- Query the city's first unit and its ExpendedAttackers aggregate.
- If that count is smaller than the captured actor count, calculate distance
  from working p to the city; obtain maxMoves(u); compare distance with
  maxMoves/roadMultiplier.
- For the first reachable city, relocate u there with mode=1, clear visibility,
  then set working p.x and p.y from the city's **live coordinates after the
  relocation**, and return ApplyDirection(Hold).

If no such city, return Hold without a relocation or task change. The city need
not be closest, have an Airport, or have room under a separate capacity rule.

## 4. AirSuperiority target selection

Set target=Absent, bestAdjusted=9999 and selectedDistance=9999.
Scan live foreign units V ascending with neither C→V.owner Ceasefire nor Peace.
Require V visible to the captured owner, or V.owner equal to the captured
owner's unsigned-byte identity. That second original alternative is normally
redundant after the foreign-owner test; retain it for accepted owner domains.
Alliance or War is not an additional admission test.

For a non-Air target:

1. If hold=0, require stack Count≤1. Otherwise require stack Ships≤1, which
   permits a transport with land passengers but rejects multiple ships.
2. Capture actor flags and cached-T attack, HP and firepower. Define veteran
   factor as 3 for Veteran, otherwise 2. Form attackProduct as actor factor
   times those three signed rule values, with signed32 retention at each step.
3. Form defenseProduct as V's signed HP*defense*firepower*its veteran factor.
   If V's hold≠0 and a fresh Count=1, double defenseProduct.
4. Query V's terrain. Compute
   `defenseComparison = ((defenseProduct*signed terrainDefense)>>1)*3`.
   Reject when defenseComparison>attackProduct*2. Equality qualifies.

Air-domain targets bypass this entire stack/strength test. Damage is not
subtracted from either rule HP in this heuristic, and U06 combat preview is not
called.

For each surviving V, calculate d=distance(p,V), adjusted=2*d. If V.owner is
human, arithmetic-shift adjusted right by one. If its live range≠0, subtract 1.
Query Count; subtract another 1 if count=1. If adjusted≤bestAdjusted, retain V,
adjusted and the original d. Later adjusted ties win even if raw d differs.
No reach restriction is applied during selection; §5 handles the chosen target.

## 5. Strike feasibility and basing for the chosen target

If target is Absent, return no result to §2. Otherwise let d be its selected
raw distance, h the captured homeDistance and q the captured reach.

### Publishing a strike

When d≤q or q<h:

1. If (h≤q-d and d=1) or q<h, set Strike Goto to the target's live coordinates
   and return Finish. The q<h alternative admits a target beyond q; it does
   not recheck target reach merely because home is unreachable.
2. Otherwise set bestReturn=9999 and scan D0…D7 around the target, reading
   live target coordinates for each candidate. Require in bounds, no city
   owner, unit owner Absent or C, and distance(p,candidate)≤d-1.
3. For each admitted candidate set the shared nearest-city distance to 9999
   and nearest identity=Absent. Scan live owned cities ascending; retain
   strict smaller ordinary distance from that candidate in the shared value.
   If no city was selected, explicitly replace the shared distance with **0**.
   Update bestReturn to the smallest resulting shared value.
4. If d+bestReturn≤q, set Strike Goto to the target's live coordinates and
   return Finish. The shared nearest-city distance is left from the **last**
   admitted neighbor, not necessarily the neighbor that gave bestReturn.

These queries do not move u to the tested neighbor or reserve a landing space.
If no neighbor is admitted, bestReturn stays 9999.

### Adjusting the objective and selecting a base

If no strike was published, capture objective coordinates from the target and
retain comparison d from target selection. Scan live human-owned cities
ascending that have the Airlifted flag:

1. Calculate v=distance(current objective,city).
2. Unless C→city.owner Vendetta: double v if War is absent, then reject if
   Peace is present. Vendetta bypasses both that doubling and Peace rejection.
3. If v<d, replace d=v and objective coordinates with the city's coordinates.
   Later cities measure from this **changed objective**, not the original unit.

Next set bestBase=999 and selectedCity=Absent. Scan live owned cities ascending.
Require distance(working p,city)≤q. Calculate distance(objective,city); retain
strict smaller values below bestBase. First ties win. If selected, set
StageAirStrike Goto to its live coordinates and return Finish.

If none and dh≠0, call GoHome and return Finish. Otherwise set task AirHold and
return ApplyDirection(Hold). The home-distance parameter dh is a captured
boolean gate here; the separately computed h and q govern reach tests.

## 6. Attack-role city raids

Require r=Attack, nonzero live range and signed cached-T attack<99.
For an existing Goto: only when counter=0 and Z≠0, set HasOrders=true and
return PlanMove. Otherwise this phase gives no result; do not automatically
retain the Goto through a new Finish result.

With no Goto, first test the historical scenario exception: Scenario active,
HistoricalScenario flag active, turn=1, scenario objective civilization=C, and
unitOwner(45,53) present. If all pass, set CityRaid Goto to (45,53) and use the
publication return rule below. It requires neither a city nor a foreign owner
at that coordinate and bypasses the ordinary target scan.

Otherwise initialize selected=Absent and best=-9999. Scan live foreign cities
J ascending with neither Ceasefire nor Peace from C. For each:

1. Calculate distance(p,J), then query movesLeft(u). Require distance≤
   movesLeft/roadMultiplier. This uses remaining movement, not the multi-turn
   reach from §2.
2. Query the first unit at J and require a present result. Capture signed
   city size, then query Count; score=`size*10-count*25`.
3. Query J's **raw region**, adding 40 if it equals captured R.
4. Scan all eight D neighbors, adding 60 for each in-bounds tile whose unit
   owner is C. Multiple friendly neighbors each contribute; do not stop at one.
5. On score≥best replace best and selected. Later ties win, including equality
   with the initial -9999 threshold.

For selected J, set CityRaid Goto to its live coordinates. For either this or
the scenario publication, if Z≠0 set HasOrders=true; if Z=0 retain the incoming
HasOrders. Return PlanMove when the resulting HasOrders is nonzero, otherwise
Finish. No selected city returns no result, eventually Continue from §2.

## 7. Nuclear city targeting

Input is the live actor u. Capture signed owner C, set best=-1 and target=Absent.
Scan city slots ascending. For each live foreign city J owned by E:

1. Reject if C08 says J has SDI Defense; then reject signed size≤4.
2. Admit military pressure if any of: `2*strength[E] > 3*strength[C]`;
   C→E Vendetta; or (E's active count of the fixed Nuclear Missile rule slot
   is zero and `2*strength[E] > strength[C]`). Read strengths as unsigned
   modulo-65536 quantities. These products fit unsigned32; the comparisons
   are strictly greater, not greater-or-equal.
3. Require C→E NuclearThreat present and Peace absent. War is not required.
4. Start score=0 and scan D0…D8 centered at J, re-reading its coordinates for
   each neighbor and wrapping x. For each in-bounds tile, query unit owner O.
   If O≤0, do nothing, including barbarian units. If O=C, subtract 2 once for
   that tile. Otherwise, if C→O has NuclearThreat without Peace, traverse
   from the tile's first unit using U01 next and add 1 per member. Otherwise
   subtract 99 once for that tile. Do not replace this with a clamped stack
   aggregate or a per-unit friendly penalty.
5. Add signed size[J]>>1. Only if score>best, test reach: scan live owned
   cities ascending; for each, calculate distance(city,J), then query
   maxMoves(u). If distance≤maxMoves/roadMultiplier, mark reachable and break.
   This tests reach from **any owned city**, not the missile's position,
   home-city link or remaining movement. If reachable, replace target/best.
   A rejected higher score does not discard the previous reachable target.

Ties keep the first reachable city. A candidate's own SDI test does not replace
U07's later nearby-SDI interception; no adjacent-defense search is added here.

### Threshold, relocation and diplomacy

After all cities, set shortage=max(5-signed C.cityCount,0).
Proceed only if best≥10-shortage, then C's unsigned active count for u's **live
actual type** exceeds 1. This second type query is distinct from the fixed
Nuclear Missile slot used during candidate admission.

Scan D0…D7 around the selected city's live coordinates. At the first tile that
is in bounds and has no city owner and then no unit owner:

1. Relocate u there with mode=1, then clear actor visibility. Terrain,
   route legality, airbase ownership and staging-tile reach are not checked.
2. Re-read selected city owner. If human-controlled, inspect C's directed
   Nuked flag toward that live owner. If absent: set that directed Nuked flag,
   clear directed Vendetta, then call V04's **bilateral** setRelation with
   Incident. Reload city owner for each reached operation.
3. If Nuked was already present instead, subtract 2 from the selected owner's
   lastContactTurn[C], retaining signed16 wrap. Reload owner for the read and
   store as specified by the live state; do not clamp negative contact turns.
4. Return the direction opposite the chosen staging neighbor to A04.

If the threshold/count gate fails, or no staging neighbor is available, return
Hold. No move exhaustion, order/task change, nuclear blast, visibility publication
or war declaration is added by this selector. Relocation itself retains U01's
side effects before the explicit visibility clear and diplomacy.

An accepted negative C.cityCount can lower the threshold enough that an absent
target reaches its coordinate read. This is an explicit read boundary; do not
invent city -1 or silently select city 0. An invalid subsequent treaty/record
write is excluded separately, after preserving preceding valid effects.

## 8. Conformance cases

| Input or event | Required result |
| --- | --- |
| Range=0, maxMoves=6, multiplier=3 | Reach=4 regardless of current moves left. |
| Range=3, counter=1, maxMoves=6, movesLeft=3, multiplier=3 | Reach=3; range/2≤counter can send the unit home if an owned city is reachable. |
| Range=1, existing Goto | Skip the special one-turn recovery test. |
| DestroyedAfterAttack target score equals four times actor cost | Admit; later equal target wins. |
| No airbase on missile target tile | Absent=-1 satisfies the original nonzero test; do not use the unprotected stack-cost branch. |
| Selected missile target surrounded by occupied tiles | Do not try runner-up; attempt redistribution, then Hold. |
| Missile fallback has several reachable less-crowded owned cities | First ascending slot wins; no closest-city search. |
| AirSuperiority candidate is Air domain | Bypass non-Air stack and strength gates. |
| Carrier target with land passengers but only one ship | Ships test can admit it even though total Count>1. |
| Equal adjusted target scores | Later unit wins; retain its own raw distance for reach decisions. |
| Home unreachable, selected target beyond reach | Publish Strike Goto under q<h branch. |
| Successive flagged human cities improve the objective | Each distance uses the previously changed objective. |
| City raid has several friendly adjacent tiles | Add 60 per tile. |
| Equal city-raid scores | Later city wins. |
| Raid publication with Z=0 and incoming HasOrders=true | Keep true and return PlanMove. |
| Historical scenario target occupied by an owned unit without a city | Fixed-coordinate exception can still publish CityRaid Goto. |
| Two equally scored reachable nuclear cities | First wins. |
| Stronger nuclear score is not in reach of any owned city | Retain prior reachable target. |
| Own units stacked near a nuclear candidate | Subtract 2 for that tile, not 2 for every member. |
| Nuclear city at threshold but only one actor-type unit active | Hold. |
| Nuclear staging succeeds against a human never marked Nuked | Relocate and clear visibility before directed Nuked/Vendetta and bilateral Incident effects. |
| Repeated nuclear staging, lastContactTurn=-32768 | Retained result after subtracting 2 is 32766. |
| City count makes absent-target threshold pass | Stop at unresolved coordinate backing; no invented target or invalid write. |

## Appendix A. Representation and original instruction checks

Logical statuses, tasks and features above do not require the original layout.
For validation, the air helper returns -99 for Continue, -1 for Finish, -2 for
PlanMove, or direction 0…8. Its internal child sentinel is `7fffffff` hex.
Tasks AirReturn, Strike, StageAirStrike, AirHold and CityRaid are respectively
`48`, `41`, `61`, `68`, `42` hex. A04's working coordinates and HasOrders are
32-bit locals passed by address; a native pointer width does not change their
value widths.

DestroyedAfterAttack and Veteran are respectively type mask `1000` and unit
mask `2000`. Fortress-without-City tests `(features & 42)==40` hex; Airlifted is
city mask `10000`. Fixed Nuclear Missile is rule slot 45. NuclearThreat,
Nuked, Vendetta and Incident are V04's `100`, `20000`, `10`, `10000` masks.
These masks describe original bytes, not mandatory bit storage in a new engine.

### Raw region in the city-raid score

The independent reconstruction uses a land-only region query in the city-raid
bonus. Original `00538878` calls thunk `00401f50`, which reaches raw-region
query `005b8a81`; the result is compared to the incoming region at `00538880`.
The primary source and §6 use the raw region. This distinction matters for
accepted city/terrain combinations involving ocean, even when nominal cities
produce the same result.

### Nonzero airbase-owner test

Original `005377ba` calls `004026f8`, reaching airbase-owner query `005b8d15`.
`005377c2` tests its full result; the branch treats every nonzero value,
including -1, as specialTile. The earlier feature comparison independently
admits Fortress without City. Both reconstructions retain this behavior, though
the primary source's local name and prose can suggest a more general “protected
city/stack” score than its actual branches admit. Preserve the test, not that
inferred strategic intention. It is not an out-of-bounds write.

### Write widths and excluded failures

The missile relocation writes working x and y as original 32-bit values at
`00537a11` and `00537a19`. The primary portable source spells one x store through
`iptr*`; a validation harness with 64-bit pointer integers must adapt that store
to signed32, not recreate an eight-byte overwrite. This is a host representation
issue, not original overflow behavior. Invalid unit/city/treaty stores are
excluded bugs; unbacked coordinate/rule reads retain supplied-input/stop semantics.
Original last-contact subtraction stores the retained low 16 bits and remains
required scalar wrap behavior.

## Appendix B. Source traceability and validation limits

Primary evidence is
[OpenCiv2 ai.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/ai.cpp)
at `a71e8aefffe04e6f719e0e213b027b128a747711`, with the selected complete bodies
checked unchanged in OpenCiv2 comparison revision `fc2155ccdb2123c1137babab6fa39e38152278c9`.
The independent read-only checkout `civ2` is pinned to
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`; `src/ai_unit_air.cpp` and
`src/ai_unit_special_attack.cpp` corroborate ordered operations and widths,
subject to the raw-region correction above. Its accepted reconstruction campaign
and source/assets/annotations are not changed by this research.

| Behavior | Primary body and original entry |
| --- | --- |
| Nuclear city selection and staging | `ai_nuke_pick_target`, `00536c4c` |
| Complete air parent | `ai_air_unit_orders`, `00537331` |
| Missile targeting/redistribution | `ai_air_jump_attack`, part of that parent |
| AirSuperiority selection/strike/basing | `ai_air_pick_target`, `ai_air_strike_target`, parts of that parent |
| City raids | `ai_air_raid_city`, part of that parent |
| Raw-region and airbase-owner queries | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp), `005b8a81`, `005b8d15`; W01 owns their semantics. |

Original instruction evidence uses MGE 5.4.0f patch 3 `Civ2/civ2.exe`, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Bounded disassembly checks the complete air-helper interval for the specific
region/nonzero/coordinate-width claims above. No fresh original runtime or
saved Ghidra annotation cycle is claimed.

The historical bounded Linux harness compiled all **six complete
bodies**, including the full air parent and its four actual children, plus the
nuclear selector. Thirty-nine assertions pass in optimized and AddressSanitizer/
UndefinedBehaviorSanitizer builds (leak detection disabled under the sandbox's
tracing). Cases sample recovery gates, signed counters, missile thresholds and
ties, absent-airbase handling, staging/redistribution, air-target strength and
stack distinctions, basing, raid ties/scenario/retained intent, raw ocean-city
region scoring, nuclear reach/thresholds/SDI, treaty-effect order and contact
wrap. The guarded-coordinate case verifies the original signed32 write after
the declared native-pointer adaptation.

The fixture supplies map/geometry, stack/aggregate, facility, movement allowance,
GoHome, SetGoto, relocation and bilateral treaty adapters. Goal/region context
and the out-parameter values are explicit inputs. It checks effects at those
boundaries; it does not claim that real U01/U02/U04/C08/V04 children, the A04
parent or U06/U07 combat/blast ran. No RNG adapter is reached by the sampled
choices. Absent backing, invalid writes, arbitrary callback interleavings and
all accepted custom-rule states are not exhaustively executed.

Source checks verify the six extracted bodies, twelve record definitions and
the enumeration header against the pinned revision, clean reference checkouts
and original executable hash. Link/anchor, index and whitespace checks
accompany the documentation commit. No source code, temporary harness or
original binary input is copied into the cleanroom repository. Full native
chooser execution, real GoHome/relocation/attack/SDI/blast integration,
consecutive-turn fuel and network/presentation joins remain separate obligations.
These checks do not imply complete independent conformance.
