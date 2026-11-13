# W05: Pollution and global warming

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Target, ownership and inputs

This specification describes classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3** city pollution intensity, ordinary placement, reactor
meltdowns, pollution bookkeeping, warming pressure and terrain changes.
Appendix B records the source and validation limits.

[F01](F01-determinism-and-arithmetic.md) owns arithmetic and sampling laws;
[F02](F02-game-state-and-invariants.md) owns shared state and identities;
[F03](F03-rules-and-configuration.md) supplies parsed rules/options.
[F04](F04-turn-sequencing.md) owns turn/city phase order.
[W01](W01-map-geometry-and-spatial-state.md) owns coordinate iteration, neighborhoods,
feature predicates and tile setters. [W03](W03-visibility-and-exploration.md)
owns exploration and remembered features. [W04](W04-terrain-work-and-improvements.md#pollution-cleanup)
owns worker cleanup. [U07](U07-nuclear-attacks.md#blast-city-and-terrain-pass)
owns blast damage, which both nuclear actions and meltdowns invoke.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

[C02](C02-city-tiles-and-worker-assignment.md) owns city tile scans and yields;
[C03](C03-city-economy-and-corruption.md#industrial-production-pollution-and-waste)
owns the coupled industrial-production prefix and waste. This document consumes
its gross shields, divisor and captured owner without recomputing production.
C04 owns disorder. C08 owns facility/wonder queries and removal; V03 owns advance
queries. P01/P04/P06/P10 own centering, notices, city windows and nuclear animation;
N02/N05 own synchronization and transport. Their call boundaries remain part of
this algorithm rather than assumed-success results.

Nominal state supplies valid city/civilization/rule records and a valid map with
canonical staggered coordinates, initialized neighborhood tables, city calculation
workspace, pollution counters and external callback state. Stock difficulty is
0..5. Warming normally starts with pressure and event count zero. No requirement
is added that stored counters equal a fresh recount: they are maintained fields,
and original callers can leave them different from current map populations.

Off-nominal signed fields, narrowing, repeated visits, accepted custom rules,
callback changes and supplied read backing are specified separately where they
matter. [X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
excludes out-of-bounds writes and resulting corruption. Reject or stop before an
invalid write unless a safe continuation is established; do not emulate adjacent
object damage with logical assignments. Unsupported reads require supplied
backing or a stop after prior valid effects. Valid scalar wrap and deliberate
sentinel/shared storage remain behavior, not excluded overwrite bugs.

## State, arithmetic and timing

| State | Meaning |
| --- | --- |
| `L` | Maintained global polluted-tile count, with signed16 retention. Placement increments it and worker cleanup decrements it. |
| `A` | Signed16 tally of polluted-tile visits during qualifying AI city scans, accumulated until the warming update resets it. It is not a distinct-tile set. |
| `C` | Warming pressure, normally an integer 0…99. Updates clamp to that range and an event resets it. Accepted extra inputs use the conversion before clamping described below. |
| `G` | Warming-event count; updates apply `s8`. The old signed value supplies the next event's severity, so the cyclic conversion remains significant. |
| `S`, `D`, `F`, `Phi` | Shared city workspace: shields, industrial pollution divisor, population pollution factor and resulting pollution intensity. These are not separate immutable per-city snapshots. |

Use F01 reference results for the arithmetic below, arithmetic right shifts,
and signed division truncating toward zero. Specified byte/word stores narrow
before later reads and clamps. Every direct random draw here is from F01's
**primary sampling law**. Warming update and terrain transformation themselves draw
nothing. Children can consume draws and mutate shared state at their call sites.

F04 calls the warming update during global advancement only when the incoming
turn is nonzero, before increasing turn/year. It therefore consumes earlier
pollution/tally state. In the city phase, ordinary pollution/meltdown processing
follows disorder and research, after city recalculation. The parent calls it
only when its captured owner's civilization passes the current human-player
test; it does not retest that membership before the following upkeep child.
The producer itself has no human-owner guard. Direct callers or callback-altered
state can therefore reach it for other owners.

A facility just built, a newly restored disorder state, or a different calculation
workspace can affect the city operation. Do not use serialized disorder alone as
a prediction of meltdown after the earlier city phases. The city caller ignores
the producer's incidental return; it is not an action-success flag.

## Shared pollution placement

`placePollution(p)` is the shared placement primitive. If `p` is
out of bounds or its live features already contain Pollution, return without
mutation. Otherwise, in order:

1. Set Pollution with synchronization requested.
2. For civilizations 1..7 in ascending order, remember the tile's current features
   through W03's overwrite operation, with synchronization requested, regardless
   of exploration or ownership.
3. Increment `L` with signed16 retention.

There is no draw, terrain/city restriction or redraw inside this helper. Its
caller supplies any eligibility and presentation. An independent caller can
therefore place Pollution on Ocean or a city coordinate. Existing Pollution
neither repairs a stale count nor refreshes knowledge. A new placement at
`L=32767` stores -32768. Repeating it with Pollution still present does nothing.
The counter increment follows all seven remembered-feature calls and uses the
then-live counter, so callbacks can affect its input.

The primitive does not mark exploration and never writes civilization 0's known
layer. W03 only synchronizes changed remembered values, but all seven calls
still occur. U07 first remembers blast damage, then attempts this helper; adding
new pollution causes its additional remember pass at that exact point.

## City pollution intensity

This is a stage of city recalculation, not something the turn producer reruns
for itself. [C03's combined operation](C03-city-economy-and-corruption.md#industrial-production-pollution-and-waste)
captures owner c before industrial facility queries, applies production bonuses
and supplies shared gross shields S and divisor D. Retain that same captured
owner for the population-advance queries below; do not recapture it here.

### Industrial production prefix

The [prefix is specified in C03](C03-city-economy-and-corruption.md#industrial-production-prefix).
It precedes this intensity calculation, which in turn precedes shield waste.
Facility queries use the live city slot; the population-advance queries below
use the owner's earlier captured identity. Shared fields remain live across
children rather than becoming immutable per-city snapshots.

### Population factor and final intensity

Set shared population factor `F=0`. Query Mass Transit. If present, skip the
following advance/factor work entirely. Otherwise:

1. In order, query Industrialization, Automobile, Mass Production and Plastics
   for captured owner `c`; add 1 for each held advance.
2. If current `F` is nonzero, query Sanitation; if absent add 1.
3. If current `F` is nonzero, query Environmentalism; if present subtract 1.
4. If current `F` is nonzero, query Solar Plant for the city; if present subtract 1.

These gates use updated `F`. A zero after Environmentalism skips the final
Solar query. Without any of the four industrial advances, missing Sanitation
does not create population pollution on its own.

Set `Phi = truncTowardZero(S / D) - 20`. Query Solar Plant again; if present,
replace that industrial part by zero. Then add:

```text
signed32(signedCitySize * F) arithmetic-right-shifted by 2
```

Read the city's live size with signed8 interpretation. There is no minimum-zero
clamp on `Phi`; small industrial production can make it negative. Mass Transit
removes the population contribution, not industrial pollution. Solar removes
the industrial contribution and can reduce the population factor, but does not
necessarily make the final value zero.

This formula uses shields **before shield waste**. The containing recalculation
then invokes [C03's waste stage](C03-city-economy-and-corruption.md#shield-waste)
and reduces shared shields; it does not recalculate `Phi` from those reduced
shields. Later city/window children can replace the shared workspace; the turn
producer reads the then-live `Phi`.

## Ordinary city pollution

`processCityPollution(city)` captures owner `c` and first computes:

```text
K = (unsignedAdvanceCount(c) * unsignedDifficulty) >> 1
```

The result is signed32. If the game is a scenario **and** the scenario's
No-pollution flag is set, assign `K=0` and shared `Phi=0`. Neither condition
alone suppresses it. Then query Solar Plant. If present, repeatedly divide `K`
by 2, truncating toward zero, while it exceeds 255. This reduction does not
force 255 below the no-draw threshold.

Select the risk result `r`:

- If `K>=255`, use zero without drawing.
- Otherwise draw once and use `draw remainder (256-K)`.

For the supported unsigned advance/difficulty fields, `K` is nonnegative and
the latter denominator is positive. Compare `r` to **signed32(2 × live Phi)**.
If `r` is not less, skip placement. A zero/negative ordinary `Phi` normally
cannot pass, but the risk draw was still consumed unless `K>=255`. Off-nominal
large workspace values retain multiplication wrap before comparison.

If it passes, draw exactly once more. Let `i = draw remainder 20`; choose
W01's **B[i]**, the 20 city-radius neighbors excluding B[20]'s center. Read the
city's current x, add the selected x offset and wrap once; then read its current
y and add the selected y offset. Test this single candidate, in order:

1. It must be in bounds.
2. Its live feature components must contain neither City nor Pollution.
3. It must not be Ocean.

The City-component test also excludes an Airbase, even though W01's city-owner
predicate would not classify an Airbase as a city. No owner, worker assignment,
exploration, resource or unit-occupancy restriction is added. On failure, abandon
placement; do not draw another candidate or search for an eligible tile.

On success, call `placePollution(candidate)`. Then query exploration for the
live local observer. If explored, center the **primary** map view on the candidate.
After centering returns, if the current Pollution-message suppression option
is clear, call the city POLLUTION notice with the original city slot, zero extra
argument and no notice sprite. Suppression prevents the notice, not centering
or pollution. There is no direct autoplay gate here; the notice child owns its
normal routing/suppression. If unexplored, neither center nor notice runs.

The candidate is retained across placement and presentation. The producer then
continues to the meltdown checks regardless of risk result, rejected candidate,
existing pollution or scenario No-pollution. There is no unconditional redraw
or global recount added after the ordinary placement path.

## Nuclear Power Plant meltdown

After ordinary pollution/presentation, query the city's live Disorder flag.
Only if set, query Nuclear Plant. Only if present, query Fusion Power for the
**owner captured at producer entry**. Fusion Power prevents meltdown. These
conditions short-circuit, so a preceding callback that restores order can skip
the remaining checks.

For an eligible city, read the current unsigned difficulty. At difficulty at
least 5 use meltdown result zero without a draw. Below 5, draw once and take its
remainder modulo `6 - live difficulty`, reloading difficulty after the draw.
A nonzero remainder leaves the plant intact. Zero triggers meltdown. Thus stock
levels 0..4 use denominators 6,5,4,3,2; Deity (5) guarantees it without this draw.
This occurs after the ordinary risk draw, which can still be consumed even
when the city has no ordinary pollution risk.

The meltdown sequence is:

1. Test exploration of the city's current coordinate by the live local observer.
   If explored, ensure the city's then-current coordinate is visible, passing
   its then-live owner to the map-window operation.
2. Invoke nuclear animation at the city's live coordinate. U07 defines the
   gameplay-relevant animation boundary; P10 owns its implementation/timing.
3. Read the city's coordinate again and call **U07 blast directly**.
4. Remove Nuclear Plant from the original city slot through the facility setter.
5. If autoplay is now zero, call the CHERNOBYL city notice with its nuclear
   catastrophe sprite. Ordinary Pollution-message suppression is not checked.

The coordinate is deliberately reread between children. A callback can move a
city or change records; do not freeze one coordinate for the entire sequence or
pretend the slot has been revalidated by identity. Unsupported lifetime/backing
changes use the explicit stop contract, not invented defaults.

This calls neither U07's nuclear action nor SDI interception, its unit-removal
pass or nuclear diplomatic penalties. The U07 blast halves city populations and
changes nearby land/features/pollution, but does not by itself kill garrisons.
There is no technology/wonder prerequisite beyond the checks above, no direct
war declaration, and no extra full-city recalculation after plant removal here.
Child UI/recalculation behavior remains with its owner.

No-pollution scenarios do not suppress this meltdown sequence or its fallout.
Autoplay suppresses the final catastrophe notice, not the hazard or the blast.
The animation has its own exploration gate; an invisible city can still melt
down. A saved Disorder flag alone is insufficient: F04/C04 can restore order
before this producer runs.

## AI pollution tally

The shared `A` tally is contributed by [C02's availability pass](C02-city-tiles-and-worker-assignment.md#availability-pass),
not by ordinary pollution placement. The relevant producer contract is:

- Capture the city's owner when the scan begins. Traverse W01 B[0..24] in order,
  using the city's live coordinate and wrapped x at each visit.
- Only in-bounds entries B[0..20] explored by that captured owner reach this
  contribution. B[21..24] are outside the usable city radius and do not count.
- If the captured owner is not in the **live** human-player mask, city-processing
  mode is currently enabled, and the tile currently has Pollution, increment
  `A` with signed16 retention.

This happens before the scan's later occupation/city-flag tests. It does not
require the tile to be worked, inside that city's exclusive claims, non-Ocean
or free of a City component. There is no distinct-coordinate set: overlapping
city radii, repeated calls and wrapped aliases can each count a tile again.
A processing-mode scan of two polluted visible cells at `A=32767` stores -32767.

C02 owns the rest of tile-flag production and C03 owns which recalculations
execute it. F04 sets/clears city-processing mode at its stated phases. Do not
replace the tally by a once-per-turn count of AI-owned polluted tiles; that
would change both overcounting and callback/phase behavior.

## Global warming pressure

The global update captures `H`, the number of set bits in the human-player mask,
including bit 0 if supplied. It does not count alive civilizations or active
connections. Compute signed32 effective pollution:

```text
P = signed32(L - A + truncTowardZero(A / 2))
```

Scan live city slots ascending, using the live upper bound. Count one `Z` for
each live city with Solar Plant. There is no human-owner, owner-alive, power,
prerequisite or region filter. Despite some recovered local names, this counts
**Solar Plants**, not Mass Transit or Recycling Centers.

If `H>1`, replace `P` by `truncTowardZero(signed32(P + H - 1) / H)`. For ordinary
nonnegative `P` this rounds upward; for negative off-nominal values use the exact
expression, not mathematical ceiling. Zero or one human leaves `P` unchanged.

Compute `T = signed32(2*P - 4*signed8(G) - Z)`. Adjust the stored pressure by at
most one toward `T`: increment if `T>C`, decrement if `T<C`, otherwise leave it.
For pressure initially in 0…99, the one-step result lies in −1…100, so ordinary
integer addition/subtraction followed by the clamp gives the same result. For
an admitted extra initial value, apply `s8` **before** clamping its reread value
to 0…99. The reference example is in [Appendix C](#appendix-c-original-numeric-annotations). This does not remove the
separate warming-event count conversion.

If the resulting `C` equals 12, `P>6`, and autoplay is zero, issue FEARWARMING:
first send text notification kind 4 to all peers when currently in network mode,
then show the local notice with the information sprite, mode 0. This is an
**equality test**, not a crossing test; pressure remaining at 12 can warn again
on the next update. Once inside the notice branch, the local notice follows the
network send even if its callbacks change autoplay or session state.

After that boundary, capture the then-live signed `G` as `oldG`. If the live `C`
is greater than 16:

1. Increment stored `G` with signed8 retention.
2. Call warming transformation with retained severity `oldG`.
3. On normal return set `C=0`.

Finally, whether or not an event occurred, set `A=0`. Do not reset `L` or
recount polluted map cells. Warning or transformation callbacks can affect
later reads and counters; there is no rollback or final restoration of `G`.
The event uses the old count, so the first ordinary event has severity 0.

## Warming terrain transformation

### Notice, iteration and neighborhood

If autoplay is zero on entry, send GLOBALWARMING text notification kind 3 to all
peers when in network mode, then show the local information-sprite notice,
mode 0. The local notice remains inside this already-entered branch even if
network callbacks change autoplay. Begin map-update batching only **after**
the notices. N02 owns the batch/transport contract; this is not a transaction
that rolls back earlier effects.

Start coordinates `(0,0)` and tile index 0. While index is less than the live
map area, process the coordinate, then add 2 to x. If x has reached or exceeded
the live width, increment y and set x to its parity. Advance the index and
repeat. Use F01's retained arithmetic and W01's map-access behavior. Do not
substitute a snapshot-sized rectangular iterator if callbacks change the map.
Flush/end batching after the scan.

For each coordinate capture its terrain kind `t`. Only Desert, Plains,
Grassland and Forest (`t<4`) are eligible. For these, count in-bounds Ocean
**visits** over the following 20 ordered offsets, wrapping x once at each visit:

| Entries | Nominal offsets |
| --- | --- |
| 0..8 | W01 D[0..8], including its center |
| 9..15 | Seven further `(0,0)` entries |
| 16..19 | `(-1,1)`, `(0,2)`, `(1,1)`, `(2,0)` |

This is **not** B[0..19] or a 20-cell city radius. The final four repeat D[4],
D[3], D[2] and D[1]. Count repeated visits separately. With ordinary unchanged
state the eight center visits do not count because the eligible tile is land.
Four Ocean neighbors at D[1..4] nevertheless count as eight, enough to pass the
first event's threshold 7. Appendix A maps the original adjacent read storage.
Nominal offsets are given above; supplied altered table/backing values remain
live inputs at their actual reads and must not be silently restored.

Let `N` be that Ocean-visit count and `Q = signed32(7 - severity)`. Use the
captured terrain `t` in the following branches, even if a child later changes
the tile's terrain. There is no city, unit, ownership or resource exclusion.

### Coastal branch

If `N>=Q`:

1. Query stored tile occupant owner. If Unowned, set it to civilization 0 with
   synchronization requested. This does not create a barbarian unit or city.
2. Set terrain to Jungle for captured Forest, otherwise to Swamp, with
   synchronization requested.
3. Clear both Irrigation and Mine components, also removing Farmland, with
   synchronization requested.
4. Visit civilizations 1..7 in ascending order. Only if the coordinate is
   explored by that civilization **and its remembered features contain either
   Irrigation or Mine**, remember the current whole feature set. After that
   call, if the live local observer equals this civilization, redraw the tile
   in all views.

The memory condition examines the remembered layer, not just the actual tile.
An explored observer with neither remembered component gets no direct redraw
from this branch, even though the actual terrain changed. Remembering can update
other feature components too, because it is a whole-feature overwrite.
Unexplored civilizations retain memory unchanged; civilization 0 is skipped.

### Dry branch

If `N<Q`, compute `h = (3*x - 3*y) modulo 8` using retained32 arithmetic. Only
when `h` equals the full unsigned32 interpretation of severity:

1. If the stored occupant owner is Unowned, set it to civilization 0.
2. Set captured Desert or Plains to Desert; set captured Grassland or Forest
   to Plains. Request synchronization.
3. If explored by the live local observer, redraw the tile in all views.

Do not remove Irrigation/Mine or refresh remembered features on this branch.
An already-Desert tile can still have its owner changed and be redrawn. The
selection is a coordinate pattern, not a random chance. Canonical x/y parity
makes `h` even, so nominal severities 1,3,5 do not select dry tiles. At severity
7 or higher the ordinary threshold is nonpositive and all eligible tiles take
the coastal branch instead.

Other terrain kinds are skipped, including off-nominal low-nibble values 11..15.
Both branches preserve other features, including Pollution, Road and Railroad,
and use W01's terrain setter to retain unrelated terrain flags. There is no
direct pollution-count change, city-population loss, unit deletion, city-output
recalculation, region/border relabeling, fertility recalculation or path-cache
repair. Later named callers own any such refresh. Stored owner changes can
alter W01's coordinate-feature predicates without a separate “remove hut” call.

## Nominal inputs, off-nominal cases and checks

No-pollution scenario rules affect the ordinary city's risk workspace and
factor only; they do not skip the risk call, the meltdown hazard or the global
warming update. Autoplay and notification options gate the stated presentation,
not a general environmental immunity. Solar's three roles—production/divisor,
city risk-factor reduction and global plant count—must remain distinct.

Counter retention is observable even with a wider cleanroom representation.
`A` can exceed `L` from repeated city scans. Negative counts and signed sizes
are not clamped before formulas. Signed `G` wrap changes the pressure term and
the next severity. A callback changing difficulty after a meltdown draw can
produce a zero/negative denominator; apply F01's division contract or stop at
an unsupported division, preserving prior effects rather than inventing a roll.

The 20-entry warming read sequence is specified as a logical input sequence;
a cleanroom need not place arrays next to one another. It is a read-contract
quirk, not authorization to recreate an out-of-bounds write or its corruption.
Malformed map dimensions, city bounds, owners or backing must be rejected or
stopped at their actual unsupported operation. A safe stricter input policy
should be distinguished from the permissive original's partial effects.

Useful conformance traces include:

- Facility/power combinations and separately rounded shield bonuses; all four
  industrial advances, Sanitation/Environmentalism gates, Transit and repeated
  Solar queries; negative intensity and the pre-waste versus post-waste value.
- `K=254/255/256`, Solar halving, scenario gates, no-risk draw consumption, all
  20 candidate offsets, Airbase/City/Pollution/Ocean/off-map rejection without
  rerolls, and centering before the notice-suppression check.
- All stock meltdown difficulties, Fusion Power, live Disorder changes, invisible
  and autoplay paths, animation changing the later blast coordinate, real U07
  blast and plant removal, with no substituted nuclear-action unit deletion.
- AI tally with unused tiles, radius center/exclusions, repeated calls, overlapping
  cities, human-mask/processing changes and signed16 wrap.
- Human counts 0/1/multiple, Solar in any live city, tally reset, pressure movement
  before clamp, repeated warning at 12, event above 16, old-severity argument,
  signed event-count wrap and callback changes during notices.
- The repeated warming neighbors versus B, protected terrain kinds, dry phase
  parity, coastal remembered-feature gates, Unowned-to-0 assignment, batch
  ordering and no global recount/derived-state repair.

## Appendix A. Original encodings and evidence mapping

These assist snapshot/instruction comparison; private layout is not required.

| Item | Original mapping |
| --- | --- |
| City/global entries | Shield/pollution stage `004e9c14`; city producer `004efd44`; AI tile-flag scan `004e7641`; warming update `00486c2e`; warming terrain scan `004868fb`; placement `005b90df`. |
| Global fields | `C` offset `026` signed8, `G` `027` signed8, `A` `028` signed16, `L` `02a` signed16 in game state. |
| City workspace | Pollution `006a6584`, population factor `006a65c4`, divisor `006a65f8`; city size signed byte. C03 owns the rest of city scratch and waste. |
| Facilities/wonder | Mass Transit 13, Factory 15, Manufacturing Plant 16, Recycling Center 18, Power Plant 19, Hydro Plant 20, Nuclear Plant 21, Solar Plant 29; Hoover Dam wonder 22. |
| Advances | Automobile 5, Environmentalism 26, Fusion Power 32, Industrialization 37, Mass Production 48, Plastics 62, Sanitation 74. |
| Flags | Scenario game flag `0080`; scenario No-pollution `0040`; Disorder `00000001`; Pollution-notice suppression `0100`; feature City `02`, Irrigation `04`, Mine `08`, Pollution `80` hex. |
| Notices | POLLUTION and CHERNOBYL city resources; FEARWARMING network text kind 4, GLOBALWARMING kind 3, broadcast recipient 255, no strings/numbers/payloads. Catastrophe sprite corresponds to improvement 62. |
| Warming read storage | X reads start `00628350`, Y reads `00628360`, each for 20 signed bytes. Entries 0..8 are D; 9..15 are seven adjacent padding bytes; X[16..19] read D's Y[0..3], while Y[16..19] read B's X[0..3] at `00628370`. |
| Nominal padding | The adjacent seven bytes are zero in the initialized source/original data. Preserve supplied nonzero backing when validating altered storage; do not infer a new radius table. |

The primary source's incidental integer return from city processing is ignored
by the known game caller; the independent reconstruction exposes a void entry.
No success/failure, hazard probability or placement result is inferred from that
residual register value. Whole-entry ABI probes should record their own exact
scope separately from gameplay-state conformance.

## Appendix B. Sources, validation and remaining integration

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`, inspected in reference checkout
`fc2155ccdb2123c1137babab6fa39e38152278c9`. Relevant unit/map/game/city sources,
handwritten return-value producer and initialized direction data have no changes
between those pins. Independent evidence is `civ2` revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. Neither reference tree was modified.

| Family | Primary source |
| --- | --- |
| City intensity and AI tally | [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp): `city_calc_shields_pollution`, `city_calc_tile_flags`, city-phase caller. |
| City risk and meltdown | [retvals.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/retvals.cpp): `city_process_pollution`; [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp): U07's `nuclear_blast`. |
| Global pressure and phase | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): `game_update_global_warming`, `game_advance_turn`. |
| Placement, warming, tile mutations | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp): `tile_add_pollution`, `map_apply_global_warming`, owner/terrain/features/known setters. |
| Adjacent read data | [gamedata.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/gamedata.cpp), [AI table declarations](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/ai.h). |
| Shared encodings | [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h), [game types](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h). |

Independent `src/city_pollution.cpp`, `src/city_turn.cpp`, `src/environment_turn.cpp`
and `src/map_updates.cpp` corroborate signed arithmetic, risk/meltdown gates,
live coordinates, AI tally consumers, warming neighbors, memory gating and
placement wrap. `tests/city_pollution_placement.py` defines six original/source
returns with actual CRT/radius/placement/remembering on a supplied 8×8 map;
its stated scope excludes UI/meltdown/network gameplay. Those native comparison
fixtures were read, not rerun for this document.

A temporary native harness extracts **seven additional routines**: the full
shield/pollution stage, city producer, city tile-flag scan, warming update,
warming map pass, owner query and owner setter. It joins **85 inherited
unit/combat/map routines**, including actual U07 blast, shared pollution
placement, terrain/features/known setters and batch begin/end. Optimized and
unoptimized AddressSanitizer/UndefinedBehaviorSanitizer builds pass **21,485
assertions**, with signed-char/wrap semantics and strict-alias optimization
disabled. Leak detection is disabled for the ptrace environment. Extraction
counts do not imply coverage of every inherited routine or path.

Checks cover all 256 combinations of the selected production/pollution facilities
at two shield inputs, all 64 combinations of the six population-pollution advances
with Solar/Transit variants, and a Hoover case. They exercise all 20 candidate
offsets, City/Airbase/Pollution/Ocean/boundary rejection, no-draw risk threshold,
Solar reduction, scenario workspace clear, centering/notice suppression and
actual meltdown-to-blast joins at all six stock difficulties. Additional cases
cover Fusion, a failed meltdown roll, No-pollution plus autoplay, animation
moving the city before blast, and AI tally repetition, unused cells, radius
limits and signed16 wrap.

The pressure matrix covers all 256 stored pressure values at three signed event
counts and six pollution counts, with a two-human/tally fixture. It verifies
narrow-before-clamp, event/reset ordering and zero direct draws. Other cases
check Solar counting across owners, repeated equality warnings, notice callbacks,
all low-nibble terrain kinds at severities 0..8, repeated-neighbor coastal versus
dry selection, exact memory/redraw gates, owner mutation and a host batch flush.
Backed off-nominal values and facility combinations are explicit fixture inputs,
not a claim that stock gameplay reaches every combination.

Bounded map access/predicates, advance/facility/wonder queries, corruption,
facility removal, UI/centering/notices/animation and network transport/enqueue
are declared adapters. The shield-stage tail executes with a supplied corruption
result; it does not establish C03's actual waste calculation. The full city
producer calls real U07 blast; animation and facility removal are supplied
boundaries, so no runtime rendering or C08 consequence claim follows. The actual
owner query handles Unowned; it is not an assumed numeric-owner adapter.

The warming pass uses explicit 20-entry signed-offset arrays matching the
recovered adjacent read sequence, avoiding host out-of-bounds pointer access.
The tested offsets are the nominal data; arbitrary mutated padding/aliasing
is not validated. Scripted primary results select finite boundaries; unscripted
results use F01's CRT recurrence. These are source/harness checks, not new
original-program, complete city-turn, multiplayer-receipt or presentation runs.
Temporary harnesses remain outside this documentation repository; X02 must
retain independent fixtures before claiming reproducibility from this repository.

The historical independent report
`analysis/reconstruction/single-player-positive-meltdown-acceptance-2026-10-23/README.md`
records one visible local-human difficulty-1 meltdown during a complete turn,
with a declared two-byte prerequisite edit adding Factory and Nuclear Plant.
The ordinary random result 5335 satisfies remainder 0 modulo 5; New York stays
in disorder, changes size 12→6 and loses the plant. Five fallout tiles plus one
independent ordinary pollution tile produce global count 6. Debug/Release files
match each other; the original matches the asserted operation but retains 18
unrelated city-record differences. Reload preserves the non-identifier action
state. These archived runs were **not rerun here**, and do not prove all hazard,
visibility, AI, network, callback or whole-turn cases.

No fresh original-executable run, instruction export or saved annotation change
was made for W05. C02 now joins the actual tile scan, and C03 extracts industrial
production and joins the real corruption/waste and recalculation parent in
finite source checks with declared support/commodity/query adapters.
[C03 records those limits](C03-city-economy-and-corruption.md#appendix-b-sources-checks-and-remaining-integration).
F04 must join full phases and all child draws; C04/C08 and V03 must supply
real disorder/facility/advance transitions; P01/P04/P06/P10 and N02/N05 must join
real notices, animation, city windows, batch capacity, transport and receipt.
X02 must retain independently reproducible full-state/random/external-trace
fixtures and the altered-table read boundaries. The finite checks leave those
integration and whole-game conformance obligations open.

## Appendix C. Original numeric annotations

These original annotations retain storage widths and reference-evaluation context.
They do not prescribe internal types or redundant casts. The main contract and
F01 determine logical ranges and behaviorally significant conversions; external
file/wire encodings remain normative in their owning mappings. Where an excerpt
includes an off-nominal conversion, it applies to that explicitly admitted profile,
not as a new requirement to accept unsupported inputs.

### Reference annotation 1

| `C` | Warming pressure, stored with signed8 interpretation. Normal update clamps it to 0..99, then an event resets it. |

### Reference annotation 2

| `G` | Warming-event count, stored with signed8 interpretation and wrap. The old value supplies the next event's severity. |

### Reference annotation 3

Retain the signed8 store **before** clamping its reread value to 0..99. This
ordering is material off nominal: incrementing stored 127 produces -128 and
therefore clamps to 0, rather than to 99.
