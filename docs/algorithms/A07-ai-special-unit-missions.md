# A07 — AI special-unit missions

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. This document owns caravan destinations, diplomat/spy city
targets and proactive paratrooper choices inside unit-order selection.
[A04](A04-ai-ground-tactics.md) owns the parent, captured context, generic move
planning, direction application and common Finish. [A03](A03-ai-settlement-and-terrain-work.md)
owns the intervening city-site phase. [A01](A01-ai-strategy-and-assessment.md)
owns effective roles, regional summaries and paratrooper assessment.

[C07](C07-trade-and-commodities.md) owns caravan entry, donation, delivery,
commodities and routes. [V06](V06-espionage-and-bribery.md) owns the city-mission
controller, including AI mission selection and its repeated fallbacks. Selecting
a destination here does not preselect a delivery or espionage mission.
[U05](U05-transport-and-special-movement.md) owns actual paradrop; A04 owns the
separate reactive paradrop after an attack. [W06](W06-huts-and-barbarians.md)
owns barbarian orders.

[W01](W01-map-geometry-and-spatial-state.md) supplies coordinates, bounds,
wrapping, distances and ordered directions. [C01](C01-city-lifecycle-and-ownership.md)
supplies exact/nearest city queries; [U01](U01-unit-lifecycle-and-stacks.md)
supplies stack queries and deletion; [U04](U04-unit-orders-and-activation.md)
supplies SetGoto. [V04](V04-diplomacy-and-treaties.md) owns directed treaty and
attitude queries, and [V07](V07-spaceship.md#8-race-arrival-reset-and-loss) owns
race-behind queries. [F01](F01-determinism-and-arithmetic.md),
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

## 1. Entry context and results

A04 reaches these phases after its guards and air/nuclear decisions. It calls
diplomat choice for effective role Diplomacy, then caravan choice for effective
role Trade. If neither redirects control, A03's city-site phase runs. Only after
that phase continues does the parent test the **live type's Paradrop ability**
and call paratrooper choice. An earlier redirect prevents later phases from
running. Effective role is distinct from the live type's rule role or fixed
identity; a custom rule can combine abilities differently from default units.

The shared inputs used here are:

| Name | Meaning |
| --- | --- |
| u, C, p | Actor, captured acting civilization and working coordinates. |
| R | Captured raw region at p, including ocean regions. |
| H, dh, Rh | Captured nearest owned city, its weighted distance and raw region. |
| N, dn, Rn | Captured unrestricted nearest city, its weighted distance and raw region. |
| HasOrders | Retained planning intent; it is not synonymous with a stored Goto. |
| Hold | Retained stay-in-place request for A04's planner. |
| OnFortress | A04's captured fortress state. |
| cityCursor | Shared city cursor; its A03 producer matters in §4. |

A04 defines how those values are produced and retained across its joins. Do not
recompute nearest cities at these entries. City/unit/rule values read within a
phase are live unless captured below. City scans visit allocated slots in
ascending order, skip id=0 and use the current upper bound; unused slots are
not candidates. Explicit fallback record reads can still use retained payload. H, N, home and
candidate identities denote city slots, not their saved unique id numbers.

Return outcomes are:

- Continue: resume the parent's next specialized phase.
- PlanMove: enter A04's shared scorer with the changed or retained intent.
- ApplyDirection: enter A04's direction application with the selected direction.
- Finish: enter A04's common completion without another local target scan.
- DirectReturn(0 or 1): return that value from the parent, bypassing Finish.

Task names below describe logical values: TradeLanding, CityTarget,
DiplomatTarget and FortressGuard. Original encodings belong to Appendix A.
SetGoto writes task, order and destination using U04; it does not execute a
move. These choices do not clear cargo, establish a route, transfer a city,
spend diplomatic funds or commit a paradrop merely by selecting coordinates.

### Arithmetic and input domains

Caravan and diplomat scoring use W01 **weighted distance**. Paradrop uses W01
**ordinary distance**, including both its city and landing-square range checks.
F01 defines the reference results of local additions, subtractions and
multiplications; preserve reductions that influence the next operation. Division truncates toward zero, with F01's
reached faults rather than a fabricated score. Comparisons are signed unless
specified otherwise. Preserve the stated intermediate divisions; combining them
into one fraction can change the result.

Use signed8 views for city size, route count and terrain defense, signed16 for
city base trade and partner identities, and unsigned views for regional
city-size totals, civilization advance counts, power ranks, military/attack
summaries and the paradrop-range rule. Attitude is the unsigned retained value
returned by V04. Caravan choice clamps it; diplomat choice does not. Home-city
identity is Absent for the sentinel and otherwise unsigned. Diplomat partitioning
instead uses the raw unsigned home value, **including 255 for Absent**.

Nominal inputs have a live actor, valid civilization/type/terrain identities,
consistent cities and stacks, ordinary map coordinates, and route counts 0…3.
Accepted unusual inputs retain signed counts, negative size/defense, scalar
wrap, actual retained home-slot payload and the backed reads in Appendix A.
A stricter implementation may declare/reject a narrower input domain. Missing
read backing requires supplied state or a bounded stop at that read; do not
invent zero or infer an absent city from a high unsigned identity.

Out-of-bounds writes and memory corruption are **excluded bugs** under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
even when deterministic. Stop before such a store unless safe omission and
continuation are established, retaining earlier valid effects. A backed read
does not authorize a write into the same neighboring field. No direct RNG draw
occurs in these three choice bodies. Their execution children keep their own
RNG contracts; in particular V06's later mission choice can draw randomness.

## 2. Caravan destinations

### Existing orders and landing from ocean

If u already has Goto, return Finish immediately. Otherwise query whether p is
ocean. If so, initialize best=0 and selectedDirection=Absent, then visit D0…D7
in ascending order:

1. Add the direction to p, wrap x once, and test bounds.
2. Require non-ocean terrain and a nonzero C presence flag in the candidate's
   raw region. Presence is A01's summary, not an on-demand ownership scan.
3. Initialize score=0. For civilization index k=1…7, in order, read **C's
   regional city-size total at captured R**, S. If k=C add `(S+1)>>1`;
   otherwise add `2*S`.
4. Replace the selected direction only for score>best.

Both score branches read C and R; neither reads civilization k's total nor the
candidate region's total. With stable summaries and C in 1…7, score is
`12*S + ceil(S/2)`, the same for every eligible direction. S=0 produces no
selection; S>0 retains the first eligible direction. Keep the ordered reads if
inputs can change across queries.

If a direction was selected, set u.task=TradeLanding and return ApplyDirection
with that direction. Otherwise continue to destination scoring, even while on
ocean. There is no additional transport lookup in this phase.

### Choosing a city

Initialize best=0 and selectedCity=Absent. Set home to the actor's actual home
identity, or H if actual home is Absent. If home is still Absent, call U01
DestroyRedraw(u, synchronization enabled), then DirectReturn(1).

For each live city J ascending, require rawRegion(J)=R. There is no owner,
treaty, visibility, road-connection or commodity-demand admission test here.
Perform the following operations in order:

1. Start newRoute=true. Visit home route indices from zero while index is
   below the **live signed route count**. If a retained partner identity equals
   J, set newRoute=false. Do not stop the scan on a match. Accepted counts above
   three use Appendix A's read contract; they do not create extra route slots.
2. Calculate weighted distance from J to home. Set
   `score = ((home.baseTrade + J.baseTrade) * distance) / 24`.
3. If J.owner=C, divide score by 2. Otherwise get attitude(C,J.owner), clamp
   it to 0…100, then set `score = ((200-attitude)*score)/100`.
4. If newRoute=false, divide score by 2.
5. Set `score = ((5-signedRouteCount(J))*score)/5`.
6. If N is present, H≠N and R=Rn, and J.owner differs from N.owner, divide
   score by 3. Then divide it by 2 again if **N.owner is human**.
7. Replace selectedCity only if score>best. Zero and negative scores never
   replace the initial zero; equal positive scores retain the first city.

The final discount is tied to the captured nearest city and its live owner,
not a general foreign-city penalty. It can affect owned or foreign candidates.
The formula is a destination heuristic; do not substitute C07's payment or
recurring-route calculation for it.

If a city was selected, SetGoto(u, CityTarget, selectedCity coordinates) and
return Finish. Otherwise reread the **actual** home identity. If it is present
and its raw region equals R, SetGoto to that home with CityTarget and Finish.
This last fallback does not use H as a substitute home. If it does not apply,
return Continue. The fallback does not add a live-id or same-owner check.

### Joining trade execution

A04/U03/U02 subsequently handle travel. Reaching a city invokes C07's existing
caravan-entry rules, including donation, delivery, payments, route changes,
reciprocal cargo and unit deletion. Cargo selection and commodity refresh remain
C07 operations. A chosen city does not guarantee that any of those operations
succeeds; earlier Goto publication remains governed by the normal order path.

## 3. Diplomat and spy city targets

### Retaining a foreign Goto or proceeding from ocean

First, if u has Goto, query the exact destination city. If one exists, it is
foreign, and C→its owner has **none** of Ceasefire, Peace or Alliance, set
HasOrders=1 and return PlanMove. War is not required. Do not publish a new Goto
or run a spaceship/target scan in this case.

Otherwise query ocean at p. If ocean, return PlanMove with HasOrders unchanged.
If land, capture `raceBehind = V07.raceBehind(C)` once, initialize best=-999
and selectedCity=Absent, then run the following scan.

### City admission and distance

For each live J ascending:

1. Capture its raw region. Requery ocean at p. While p is land require that
   region=R. If the fresh query instead says ocean, visit D0…D7 ascending,
   wrapping and bounds-checking, and require at least one non-ocean neighbor
   whose raw region is J's region. Stop that neighbor scan at its first match.
   This retained live-query branch is distinct from the outer entry gate.
2. Capture owner E and weighted distance d from p to J.
3. Compare `unsignedRawHome(u) % 3` to `J.slot % 3`. If different, query
   Road at p. If d>3+hasRoad, add 8 to d. Here hasRoad is 0 or 1. No home-city
   record is read for this partitioning.

### Owned and foreign scores

For E=C, score=99-d. For a foreign E, begin score=100 and apply these steps:

1. If C→E has Ceasefire, Peace or Alliance, reject J if E is not human or
   J is TechStolen. Otherwise make a **fresh** V07 raceBehind(C) query. If it
   returns false, reject J if any of the following conditions is true:

   - E's power rank is less than C's power rank.
   - The actor's **actual type is not fixed Spy**, E.advanceCount minus
     C.advanceCount is less than 10, and at least one of: C.government is above
     Fundamentalism; E.powerRank<7; or C→E has Alliance and E.advanceCount is
     less than C.advanceCount.
   - C→E lacks Alliance, E.government is above Fundamentalism,
     C.regionalMilitary[R]<E.regionalAttack[R], and C.regionalCityCount[R]≠0.

   A fresh true race result bypasses those three strategic rejections, but not
   the earlier human/TechStolen gates. For a surviving treaty-protected city,
   divide score by 2.
2. If C→E has War add 100; otherwise add raw attitude(C,E), without clamping.
3. Divide score by d+1.
4. If J is TechStolen, divide score by 2. If C.advanceCount+6<E.advanceCount,
   divide it by 2 again. If the **entry-captured** raceBehind is true, replace
   this contribution with 1. This is not a third race query.
5. Add 100.

After either owned or foreign calculation, if score>best retain J and score.
Equal scores retain the first city. Preserve numeric government ordering and
the fixed Spy identity; do not replace them with a generalized espionage
capability or a strategic interpretation of the intended target.

### Publishing or retaining orders

If no candidate replaced -999:

- With no existing Goto, DestroyRedraw(u, synchronization enabled) and
  DirectReturn(1).
- With an existing Goto, return Finish with it retained.

Otherwise set HasOrders=1. Publish SetGoto(u, DiplomatTarget, selectedCity)
only if the actor lacks Goto **or the selected city is foreign**. If an owned
city wins while Goto already exists, retain the previous destination and task.
Return PlanMove in either case.

### Joining espionage execution

A04/U03/U02 handle travel and city entry. V06's city-mission controller then
owns AI/default selection, direct mission effects, target changes and fallback
repetition. Follow that controller's captured identities and live reads; do
not infer “always steal” from a DiplomatTarget task or rerun this city scan when
a mission falls back. Unit bribery and the reactive opportunities reached from
A04 remain V06/A04 operations, separate from this city-destination heuristic.

## 4. Paratrooper objectives

This phase is reached only after A03's city-site phase continues. That phase
assigns **cityCursor=N** before its ordinary search, with a later optional
assignment of the same value. The away-from-home branch below therefore has
an established city identity; it does not need a guessed stack value or an
extra nearest-city query.

### Away from the captured own city

When dh≠0:

1. If H=N, R=Rh and p has no adjacent nonallied unit, SetGoto(u, CityTarget,
   cityCursor coordinates) and return Finish. On this producer path the
   cursor is N. Missing record backing stops at the coordinate read; matching
   absent identities do not create a valid city.
2. Otherwise, if OnFortress and stackCount(u)=1, set task=FortressGuard and
   bestDirection=Hold (D8), then return Finish. This does not directly set the
   Hold planning flag.
3. Otherwise return Continue.

### At the captured own city

When dh=0, attempt a drop only if u.movementSpent=0 and order is not Goto.
Initialize retainedBest=0; retainedTarget coordinates have no value yet.

Visit live foreign cities J ascending. Require C→J.owner War and ordinary
distance(p,J)≤the unsigned paradrop range. For each admitted city:

1. Query unit owner on J. If it is Absent and
   `retainedBest < signedSize(J)+999`, set retainedBest to that sum and capture
   J's coordinates as retainedTarget.
2. If retainedBest>0, run the landing search below around retainedTarget.
   Reset retainedBest=0 and chosenDirection=Absent **at the start of that
   search**. The search updates retainedBest and can cancel chosenDirection.
3. If a direction survives, recompute its wrapped destination from
   retainedTarget, call U05 Paradrop(u,destination), then DirectReturn(0),
   **regardless of the child's success or return value**.
4. Otherwise continue the city scan, retaining the last score and coordinates.

The initial positive score is always paired with coordinate assignment. A
failed landing search can leave a positive score behind, so a later occupied
city can trigger another search around the **earlier** target. Do not reset
retainedBest or retainedTarget on each outer iteration. Conversely, the first
successful local choice immediately calls the child; this is not a global
search for the largest enemy city.

After the city scan, or when movement/order prevented it, reread u.order. If
not Goto, set Hold=1 and return PlanMove. Otherwise return Continue. No other
flag or task is assigned by this fallback.

### Landing search and cancellation

Visit these **ten** candidates in order:

`extra center, D8 center, D7, D6, D5, D4, D3, D2, D1, D0`.

The extra center is an established zero offset, distinct from the ordinary
center for scoring. Appendix A records its original read. For each candidate:

1. Add its offset to retainedTarget, wrap x once, test bounds, then require
   ordinary distance(p,candidate)≤the live unsigned paradrop range.
2. Query unit owner. If C, set chosenDirection=Absent and **break the whole
   landing search**, retaining the current score. This can cancel an earlier,
   better empty square. If any other owner is present, skip this candidate.
3. For an unoccupied square initialize score=500 for D8 center, otherwise zero.
   Add 50 if it has Fortress **without City**. Read terrain and add
   `3*signedTerrainDefense`, then add ordinary distance from p.
4. If score>retainedBest, retain score and this direction. With equal scores,
   the earlier element in this descending search wins. Zero/negative scores
   cannot replace the initialized zero.

Do not add ocean, city-existence, visibility, target-strength, movement-remaining
or transport filters here. U05 supplies execution legality. The extra center
has no 500 bonus, but it still performs queries and can cancel the search if an
owned unit appears there. A later query may observe different state. Reuse of
D8's coordinates does not justify deduplicating these two candidates.

## 5. Conformance cases

These cases specify expected distinctions, not independent proof of every
accepted state. Use the owning children for actual execution integration.

| Case | Required result |
| --- | --- |
| Caravan already has Goto | Finish before ocean, home or city queries. |
| Ocean caravan, captured city-size total 1, two eligible land directions | Each stable score is 13; first direction wins. A large total in a different region is irrelevant. |
| Ocean caravan, captured total 0 | No landing selection; continue to home/city logic. |
| Home/target base trade 48 each, weighted distance 6, foreign attitude 100, no routes | Destination score 24. Attitude 255 still gives 24 after clamping. |
| Same trade case, an existing home route to target | Score 12; all remaining partner reads still occur. |
| Same base case, target route count retained as 255 | Signed count is -1; score becomes 28. This does not authorize writing a fourth route. |
| Positive equal caravan or diplomat city scores | Lowest visited city slot wins. |
| No actual caravan home, H exists, all scores≤0 | H supplies the scoring origin but not the final actual-home fallback; Continue. |
| Foreign diplomat Goto with no Ceasefire/Peace/Alliance | Set HasOrders and PlanMove even without War; retain task/destination. |
| Diplomat on ocean after that gate fails | PlanMove with HasOrders unchanged and no target scan. |
| Diplomat partition differs, weighted distance 4 | Add 8 without Road; Road raises the threshold to 4 and avoids that addition. |
| Foreign diplomat target, distance 6, same partition, attitude 255, no treaty protection | Score is `355/7+100 = 150`; attitude is not clamped. |
| Treaty city, human owner, entry race true but fresh race false | Apply fresh strategic rejections. The captured result does not bypass them. |
| TechStolen foreign target, no treaty protection, entry race true | Final score 101 regardless of the preceding contribution reductions. |
| Owned diplomat winner with a preexisting Goto elsewhere | HasOrders becomes 1; retain old destination and task. |
| No diplomat candidate | Delete and DirectReturn(1) without Goto; Finish without deletion with Goto. |
| A03 continuing path followed by away paratrooper home choice | Use N written by the actual preceding phase, not initial cursor contents. |
| Unspent paratrooper at own city, first viable enemy city versus a larger later one | Attempt the first city's selected landing immediately. |
| Empty target center at distance 2, terrain defense 0 | D8 score 502; the preceding extra center scores 2. Both center queries occur. |
| Own unit found later in the landing scan | Cancel the chosen landing, retain best score, and continue the outer city scan. |
| Canceled first target, later admitted occupied city | Positive retained score can cause a new search around the earlier coordinates. |
| Paradrop child reports failure | Parent still DirectReturn(0); do not retry another city in this call. |
| Own-city fallback with no Goto | Set Hold and PlanMove, including a unit that already spent movement. |

## Appendix A. Representation, read backing and bugs

Original effective roles Diplomacy and Trade are 6 and 7. Fixed Spy is type 47;
Paradrop is type flag `00000100` hex. Task TradeLanding is `74` hex (`t`),
CityTarget is `63` (`c`), DiplomatTarget is `53` (`S`), and FortressGuard is `46`
(`F`). Goto order is 11 and Absent home is `ff`. Road is feature `10` hex;
Fortress-without-City tests `(features & 42)==40` hex. These are validation
encodings, not required internal representations.

### Partner reads beyond the nominal three routes

The original loop reads its route count as signed8 at `0053a568` and partners
as signed16 at `0053a589`, without stopping after a match. Home-city record
stride is 88 bytes; the first partner begins at offset 68. Thus partner k is
the signed little-endian word at `88*home + 68 + 2*k` in the canonical city
record array. The fourth, fifth and sixth reads are home.science, home.tax and
home.trade; later ones can reach other retained fields and following records.

These values are not an independent array of arbitrary “extra routes.” Use the
actual producer state from [C03](C03-city-economy-and-corruption.md) and the
[F06 city-record layout](F06-save-load-and-file-formats.md), including retained
inactive-slot payload. If that canonical read is not backed, supply its original
input or stop at the read after earlier effects. A new engine can expose a safe
read adapter or reject counts outside 0…3; it need not perform a native array
overrun. No branch here writes an extra partner; an analogous out-of-bounds
route write is an excluded bug, not required behavior.

### Established paratrooper inputs

The original site phase loads nearest city from EBP-84 at `0053aac7` and writes
the shared city cursor at EBP-130 at `0053aacd`. The later optional assignment
writes the same nearest identity. The away-home path reads that cursor at
`0053b3fc`, then the city's y/x at `0053b40a`/`0053b421`, and publishes task
CityTarget at `0053b433`. The primary reconstruction exposes the shared cursor;
the independent reconstruction uses its established nearest-city value. Those
forms agree on this real continuing path. Initializing the cursor to zero is
neither necessary nor a compatible general substitution.

The original neighbor loop starts at index 9 (`0053b193`) and descends through
zero. Offset loads are signed at `0053b1af` and `0053b1ce`. Original bytes at
`00628359` and `00628369` are both zero, producing the extra center; ordinary
D8 is also zero. Only index 8 gets the 500 bonus. Terrain defense is signed8
at `0053b2ca`. This read of established direction padding is represented by an
explicit extra candidate in §4, with no out-of-bounds write or guessed input.

Invalid actor, city, rule, region or treaty identities retain X01's distinction:
an unavailable read needs input or a bounded stop, whereas an out-of-bounds
write is excluded. Valid scalar narrowing remains required. A safe rejection
policy must say which accepted original inputs it declines and must not silently
substitute a destination, destroy a different unit or corrupt neighboring state.

## Appendix B. Evidence and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is
[OpenCiv2 ai.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/ai.cpp)
at `a71e8aefffe04e6f719e0e213b027b128a747711`. The selected bodies are unchanged
in OpenCiv2 comparison revision `fc2155ccdb2123c1137babab6fa39e38152278c9`.

| Behavior | Primary body and original context |
| --- | --- |
| Caravan choice | `ai_orders_caravan`, part of `ai_unit_choose_orders` at `00538a29`; city-scoring/partner reads within `0053a4de`…`0053a8e1`. |
| Diplomat/spy city choice | `ai_orders_diplomat`, part of the same parent; role dispatch precedes caravan/site/paratrooper. |
| Paratrooper choice | `ai_orders_paratrooper`, original admission/choices within `0053af66`…`0053b445` and the following fortress branch. |
| Retained nearest-city producer | `ai_orders_city_site`, A03-owned complete body; shared cursor assignment `0053aac7`…`0053aacd`. |
| Type and direction values | [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h), [ai state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/ai.h); W01 owns ordinary neighborhoods. |

The independent read-only checkout `civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, corroborates these branches in
`src/ai_unit.cpp`, including the retained city producer, city-record read aliases
and extra center. Original MGE executable SHA-256 is
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Bounded original disassembly/data checks confirm the cursor producer/consumer,
signed route reads, descending neighbor loop, zero padding and signed defense.
No new original-runtime execution or saved Ghidra annotation cycle is claimed;
the independent reconstruction campaign, source, assets and annotations remain
unchanged.

The historical bounded Linux harness compiled all **three complete
choice bodies** plus A03's complete city-site body, preserving the shared
reference context. **38 assertions pass** in optimized and AddressSanitizer/
UndefinedBehaviorSanitizer builds, with leak detection disabled under tracing.
Cases cover trade arithmetic/ties/landing/home/route aliasing, diplomat intent,
partition/attitude/race/Spy gates, deletion/fallback, the actual A03 cursor
producer, paradrop ordering/cancellation/retained target, signed defense and
failed-child return. Source checks compare all four bodies, twelve record
definitions and enums against the pin, verify clean reference checkouts and
verify the executable hash.

The harness uses explicit map/geometry, stack, attitude, race-behind, SetGoto,
delete/found-city and Paradrop adapters. Race results and child failure are
supplied boundary outcomes. The route adapter safely reads actual contiguous
city-record backing; direction storage explicitly includes the recovered extra
zero offset. No source decision branch is replaced, but the native layout of
pointer-sized local references is not a claim of original Win32 execution.

These finite checks do not execute the full A04 parent, real U01/U04/U05/V07
children, C07 delivery, V06 mission dispatch, pathfinding, native presentation or
network callbacks, arbitrary invalid inputs or consecutive turns. Integration
must join selected destinations to those actual children, compare ordered
state/RNG effects including failures, and retain the bounds policy above.
Link/anchor, index and whitespace checks accompany this document;
These checks do not establish complete original-program conformance.
