# U07: Nuclear attacks

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

Blast passes, unit deletion, stack changes, city loss and pollution publication
retain their dependencies. Per-target calculations are independent only when
all earlier mutations, callbacks and failure prefixes are accounted for.

## Target, ownership and inputs

This specification describes classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3** nuclear interception, unit removal, population loss, damaged
improvements, pollution and immediate diplomatic effects.
Appendix B distinguishes source checks from complete original-program conformance.

[F01](F01-determinism-and-arithmetic.md) owns arithmetic and sampling laws;
[F02](F02-game-state-and-invariants.md) owns object identities and shared state;
[F03](F03-rules-and-configuration.md) supplies interpreted rules.
[W01](W01-map-geometry-and-spatial-state.md) owns coordinates, ordinary distance,
wrapping, tile predicates and direction table D. [U01](U01-unit-lifecycle-and-stacks.md)
owns stack lookup/repair and deletion. [W03](W03-visibility-and-exploration.md)
owns remembered features, exploration and redraw/revelation distinctions.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

There are two different entries:

| Entry | Inputs and result | Effects owned here |
| --- | --- | --- |
| Nuclear action | Attacking civilization `c`, target coordinate `p`, whether to check SDI; returns 0 or 1 | Announcements, optional interception, animation call, diplomacy and whole-stack removal, then blast. |
| Blast | Center coordinate `p`; no result | Population and land-feature damage, remembered features, pollution, redraw and map batching. It does not remove units or apply nuclear diplomacy. |

The action has no missile-unit argument. A caller supplies delivery eligibility
and handles the delivering unit afterward. [U02's foreign-city branch](U02-movement-and-zones-of-control.md#foreign-city)
and [U06's nuclear delegation](U06-combat-and-veterancy.md#nuclear-delegation)
own the two movement/combat routes. [V06](V06-espionage-and-bribery.md#7-planted-nuclear-device) owns planting a device;
[W05](W05-pollution-and-global-warming.md) owns pollution placement and the
reactor-meltdown trigger; [A06](A06-ai-air-and-nuclear.md#7-nuclear-city-targeting)
owns AI target selection and staging. P10 owns animation and
N02/N05 own message transport, receipt and map synchronization. These children
are ordered operations, not assumed-success or inert callbacks.

Nominal inputs provide civilization 0..7, canonical in-bounds target coordinates,
valid rule/state collections and live well-formed stack/city identities. Stock
positive city sizes follow the ordinary rounding rule below. Direct blast calls
need no attacking civilization. Normal delivery requires prerequisites in the
calling algorithm; the core does not recheck technology, Manhattan Project,
missile type, available moves, war status or target ownership.

Off-nominal signed sizes, counter wrap, repeated wrapped coordinates, valid
retained slots and callback changes remain observable as stated below.
[X01's write exclusion](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
applies: an out-of-bounds write and its corruption are bugs, not compatibility
requirements. Reject or stop before an invalid write unless a safe continuation
is separately established. Do not emulate corruption through another logical
field. Invalid or stale reads require declared retained/backing inputs or a
bounded stop at that operation, preserving prior valid effects. Additional
allocated research storage does not enlarge an object's valid write domain.

## State and operation ordering

Use F01's **primary sampling law** for each direct random decision. A separate
explosion context is permitted; do not substitute the secondary range law.
Primary results are nonnegative, so `odd(draw)` and `draw remainder 3` have their
ordinary meanings. Child algorithms can consume their own draws; include them
at the call position in a complete replay.

Both passes visit `D[0]` through `D[8]` in W01's order, with the center **last**.
For each entry, add the offset to the retained center using signed32 arithmetic,
apply W01's one-step horizontal wrapping, and test rectangular bounds. Skip
out-of-bounds neighbors. Do not add a parity test, clip the center first, sort
coordinates or deduplicate wrapped aliases. Nominal ordinary-sized maps visit
up to eight neighbors and the center once each.

The action completes **all unit removals before beginning any population or
terrain damage**. Do not fuse the two passes tile by tile. Each pass computes
its coordinates and queries live state as it reaches an entry. Children can
change dimensions, wrap rules, occupancy, records, participants or observer.
The center and attacking civilization are retained parameters; live fields are
not globally snapshotted at entry.

## Nuclear action

### Initial target notice

Capture `targetOwner = cityOrUnitOwner(p)` using W01's combined owner query.
Set shared string argument 0 to the attacking civilization's plural name.
Then announce USEWEAPONS if the captured owner is human:

- In network mode (mode at least 3), if that owner differs from the local
  observer, send the target-owner text notification with one string argument,
  no numeric arguments and zero auxiliary payloads.
- Otherwise show the local USEWEAPONS improvement dialog with Manhattan Project
  and presentation mode 8. Outside network mode this human check is not limited
  to the currently observed civilization.

Human membership uses the live human-player mask and F01's masked shift of the
owner's retained low byte. An absent owner normally matches no human bit.
There is no autoplay suppression and no friendly-owner exemption. The owner
query and notice precede SDI interception. The target owner is retained through
notices and animation routing; do not query a replacement owner after a callback.

### SDI interception

When `checkSDI` is nonzero, scan city slots from 0 upward, comparing against the
live city bound each time. The first city satisfying all of the following
intercepts:

1. It has a live identity.
2. Its owner differs from attacking civilization `c`.
3. It has SDI Defense.
4. W01's ordinary map distance from its position to the target is **less than 4**.

City slot zero qualifies. There is no nearest-city selection, target-owner
restriction, diplomacy/alliance check or requirement that the target be a city.
A third party's SDI can protect another civilization, including an ally's target;
SDI belonging to the attacker is ignored. The first matching slot wins even
when a later SDI city is closer. The facility is not consumed.

On interception, set shared string argument 0 to that city's name. Notify its
owner with SDI and the SDI Defense improvement, then notify the attacker with
the corresponding SDI notice. Each notice independently uses the same live
human/network/local-observer rules as above. Nonhuman recipients receive no
notice. Network notifications have one string, no numbers and zero payloads.
Do not rebuild the shared string argument between these two notices: a child
can alter the shared text state. The city owner is read at its uses, not retained
as the initial target owner was.

Return **0** immediately. No local nuclear animation, unit-removal pass, blast,
or direct random draw follows. The earlier USEWEAPONS notice still occurred.
The caller, not this core, disposes of an intercepted missile.

### Animation boundary

When not intercepted, if the **captured target owner** is currently human in
network mode and differs from the live local observer, send that recipient
ShowNuke with the retained target coordinate. Then call the local nuclear
animation unconditionally. Other collateral owners are not separately routed
ShowNuke by this entry. This entire boundary precedes the first stack lookup.

[P10's nuclear effect](P10-audio-video-and-animation.md#nuclear-effect) owns
visibility and semantic completion, with original sound/frame timing, geometry
and backgrounds retained as optional reference presentation under X01. An
invisible target still takes damage. Input/network callbacks can change state
before destruction starts: preserve their required delivery and state ordering
at this semantic boundary. Cosmetic drawing/waits may change or be skipped;
do not skip damage, required remote ShowNuke handling or completion effects.

### First pass: units and diplomatic consequences

Visit the neighborhood in the ordered manner above. For each in-bounds location,
call U01's first-unit-at-tile lookup, including its stack validation/repair and
animation-state gate. If it returns no unit, continue without diplomatic work.
Otherwise use the returned stack member:

1. If its live owner differs from `c`, union Vendetta and Nuclear-threat into
   **victim owner's relation toward attacker**.
2. Union Nuked into **attacker's relation toward victim owner**.
3. Adjust the victim owner's attitude toward the attacker by **+100**, through
   the shared attitude operation.
4. Whether foreign or friendly, call
   [U01 whole-stack deletion](U01-unit-lifecycle-and-stacks.md#delete-a-stack)
   with synchronization requested.

Steps 1–3 are skipped for an attacker-owned first member. Their relation changes
are direct unions, not a call to ordinary war declaration. No treaty condition
blocks them. The owner is reread where used; do not replace this with a per-member
relation update. An off-nominal mixed-owner physical stack is all deleted, but
only the first member's owner receives these direct nuclear relation effects.
There is no extra penalty merely for population loss in an ungarrisoned city.

Use [V04's shared attitude adjustment](V04-diplomacy-and-treaties.md#2-shared-attitude-and-hostility)
with delta+100 at the stated point. Its stored-attitude gate/clamp and separate
active-evaluation update apply; do not replace that call with a direct attitude
assignment.

Whole-stack deletion uses U01's retained-next traversal, per-unit deletion,
bookkeeping/elimination checks and final synchronization. Own units, allied
units, cargo and an adjacent launching missile have no special immunity.
Do not substitute U06 combat casualty deletion: this entry does not directly
record combat casualty history, increment combat loss counters, promote a unit
or invoke UnitKilled scenario events. U01 still updates living-unit counts and
can cause civilization elimination with its own downstream effects.

After the entire unit pass, call **blast(p)**, then return **1**. This means the
action reached its completed path, not that it killed a particular unit or won
ordinary combat. It does not directly consume a distant delivering unit. An
invalid center can still have in-bounds neighbors deleted in this pass before
the blast rejects that center; the core has no initial center-validity return.

## Blast: city and terrain pass

First test the retained center for map bounds. If invalid, return with no batch,
neighbor queries or direct random draws. Otherwise begin a map-update batch,
visit D[0..8] independently of the preceding unit pass, and flush/end the batch
after the final entry. A direct blast need not have had a unit pass at all.

### City branch

At each in-bounds coordinate, query the live city-at-location. If a city exists:

```text
s = signed interpretation of stored population
new population = retainByte(s - arithmeticRightShift(s, 1))
redraw area in all views at this coordinate, radius 0,
    using the live local observer and refresh argument 1
```

For nominal positive populations this is `ceil(s/2)`: 5 becomes 3, 2 becomes 1,
and 1 stays 1. There is no random draw, pollution attempt or land-improvement
damage for that coordinate. This branch precedes the Ocean test. It does not
delete the city, change its owner/facilities, recompute city output, repair worker
assignment, adjust aggregate population totals or explicitly refresh city
knowledge. Redraw is its stated child; C01/C02/P02 own their own operations.

For off-nominal values the signed-byte interpretation matters: stored 0 stays 0,
128 denotes -128 and becomes stored 192, and 255 denotes -1 and becomes 0. There
is no minimum-one repair. Those scalar transformations are in-bounds behavior,
not excluded memory-write bugs.

### Noncity land branch

If no city exists and the tile is Ocean, skip it with no draw, feature mutation,
memory refresh or redraw. Otherwise perform these operations **in order**:

1. Retain the tile record/reference before querying its current features.
2. If the Fortress component is present and the City component is absent,
   clear Fortress with synchronization requested. An Airbase, which combines
   these components, is not cleared by this condition.
3. Draw once from the primary stream. If odd, clear Railroad with synchronization
   requested. Consume this draw even when Railroad is absent.
4. Read Irrigation and Mine from the **retained tile record** after the earlier
   feature operations. If both are present (Farmland), draw once and clear Mine
   if odd; Irrigation is retained. Otherwise draw once and clear Mine if odd,
   then draw once and clear Irrigation if odd. Consume each specified draw even
   when the corresponding feature is absent.
5. In civilization order 1..7, remember the tile's current features using W03's
   ordinary overwrite operation, with synchronization requested. Do this
   regardless of exploration or ownership; do not mark it explored.
6. Draw once. If the result's remainder modulo 3 is nonzero, call
   [W05 pollution placement](W05-pollution-and-global-warming.md#shared-pollution-placement). The draw precedes that helper's check for
   existing pollution, so an already polluted tile still consumes it.
7. Redraw this tile in all views.

The feature setters operate on the queried coordinate; the Farmland test uses
the retained record from step 1. A callback that replaces map backing can make
those differ. Supply the retained backing contract or stop at an unsupported
read; do not silently acquire a new tile record for the test.

Road, terrain kind, river, resources and other unmentioned features are retained.
No automatic terrain downgrade or reconstruction of missing Road occurs when
Railroad is cleared. Feature removals are independent except for the explicit
Farmland branch. Pollution is a two-out-of-three result-class test, not a floating
probability or a secondary generator.

With no intervening child's draws, each noncity non-Ocean visit consumes:

| Live branch at step 4 | Ordered primary draws | Count |
| --- | --- | --- |
| Farmland | Railroad, Mine, pollution | 3 |
| Other features | Railroad, Mine, Irrigation, pollution | 4 |
| City, Ocean or skipped neighbor | None | 0 |

Do not precompute this count from the pre-explosion map. An earlier redraw,
message, deletion or feature callback can change a later branch. Repeated
wrapped coordinates are repeated operations: on a width-2 cylindrical fixture,
a center city of size 8 can be visited three times and become 4, then 2, then 1.
Previously removed units need not reappear, but feature draws and city halving
are not suppressed because the location was already processed.

### Pollution placement boundary

Call [W05's shared placement primitive](W05-pollution-and-global-warming.md#shared-pollution-placement)
at step 6 after the blast's first remember loop and its pollution draw. New
pollution causes the helper's additional seven-civilization remember loop;
existing pollution does not. Keep this call before the tile redraw and preserve
the shared primitive's effects and draw-free behavior.

### Batch boundary

Single-player begin/end operations are inert. In network mode, begin disables
immediate tile synchronization, activates batching and resets pending map
records. These are shared flags/state, not a nested rollback transaction.
Changed feature/remember setters still mutate local state. Clients enqueue the
corresponding changes; the host defers their broadcast to the final state diff.

End restores immediate synchronization and clears batching. A client sends and
flushes pending map records only when nonempty, then resets the pending records.
The host sends a state diff to all peers and flushes even if no land changed.
The requested send-buffer flush timeout is 5000 milliseconds. N02 owns record
capacity, message assembly and reentrant changes; N05 owns transport results.
Neither failure nor a changed session mode implies rollback of prior valid
mutations. Mode and host checks use live state at each child entry.

## Delivery and other callers

| Caller | Join and ownership |
| --- | --- |
| U06 nuclear combat branch | Calls action with SDI checking after its own precombat work. An intercepted result uses basic deletion of the attacker; successful action clears caller-owned aggression state. U06 defines viewer/AI continuation and its overall return. |
| U02 foreign-city movement branch | Calls action with SDI checking only on its nuclear execution branch. An intercepted result uses cargo-aware deletion; success clears caller-owned aggression. Its eligibility, preview and early-stop result remain in U02. |
| [V06 Plant Nuclear Device](V06-espionage-and-bribery.md#7-planted-nuclear-device) | Calls action with SDI checking disabled after confirmation and three capture checks, plus a fourth for Palace. It ignores the core return, then performs identification and wider diplomatic penalties. There is no final escape/promotion step. The preceding checks can already relocate/exhaust the Spy. |
| [W05 reactor meltdown](W05-pollution-and-global-warming.md#nuclear-power-plant-meltdown) | Calls nuclear animation, then blast directly. It does not call the nuclear action's SDI, diplomacy or stack-removal pass. Removing Nuclear Plant and the meltdown notice belong to the caller. |
| [A06 AI attacks](A06-ai-air-and-nuclear.md#7-nuclear-city-targeting) | City selection, staging relocation and pre-attack treaty effects belong to AI. The core does not independently authorize or choose a target. |

Do not interpret the action's completed result as ordinary combat victory or
execute ordinary combat aftermath on it. Do not force every blast to
kill units: meltdown's direct blast retains them unless some separate child
operation changes them. Full delivery prerequisites and outcome handling must
be joined with each owning document, rather than copied into this core.

## Conformance cases

Record incoming full state, controlled primary-law decisions, retained coordinates,
all direct/child draws, notices, callbacks, unit lifetimes, ordered mutations,
remembered features and final output. Useful independent checks include:

- Distance 3 versus 4; city slot zero; own, allied and third-party SDI; two
  qualifying cities where the earlier slot is farther; checkSDI disabled.
- Initial target-owner notice before interception and before animation; remote
  owner routing; callback changes without retargeting the captured owner.
- Friendly and foreign stacks at multiple directions; U01 retained-next
  deletion and elimination; animation-created units; all removals before the
  first terrain draw; no substituted U06 casualty/history/events.
- Positive odd/even populations and signed off-nominal values; size-zero cities
  retained; Ocean skipping; Airbase versus Fortress; Farmland versus independent
  Mine/Irrigation; absent-feature draws; preexisting pollution.
- All seven remembered-feature writes without exploration; new pollution's
  second refresh; counter wrap; repeated wrapped coordinates; early redraw
  changing a later city/land branch; invalid center's two-entry distinction.
- U02/U06 intercepted and completed calls with their different deletion children;
  V06 and meltdown's distinct entry points; host/client batches and real receipt.

Reproducing an out-of-bounds overwrite is never a passing conformance case.
Original bug evidence may describe the attempted address and observed corruption
separately. Safe rejection or a documented bounded stop retains any preceding
valid effects; it does not fabricate a replacement target or successful result.

## Appendix A. Original encodings and comparison mapping

These details support snapshots and validation, not a required private layout.

| Item | Original mapping |
| --- | --- |
| Core entries | Nuclear action `0057f9e3`; blast `005b9179`; pollution placement `005b90df`; nuclear animation `0057f657`. |
| Features | Unit `01`, City `02`, Irrigation `04`, Mine `08`, Road `10`, Railroad `20`, Fortress `40`, Pollution `80` hex. Airbase uses `42`; Farmland uses `0c`. W01 owns predicates/encoding. |
| Relations | Victim-to-attacker union `00000110` (Vendetta + Nuclear-threat); attacker-to-victim union `00020000` (Nuked). These are not War `2000`. |
| Facilities | SDI Defense slot 17; Nuclear Plant slot 21; Manhattan Project improvement slot 62. |
| Population/count | City size is signed byte for halving; polluted-tile count is a signed16 increment. Main-text signed arithmetic and wrapping remain required even with a different representation. |
| Text | USEWEAPONS kind `29`, SDI kind `2a` hex; one string, zero numbers, two zero payloads. Local improvement mode 8. |
| Messages | ShowNuke `7d`; FeatureSet `90`; remembered FeatureLocalSet `97`; MapPackets `59` hex. Validate N02's schema rather than inferring it from these names. |
| Map records | Feature change kind 0; known-feature change kind 7. Original pending buffer is 256 words, begins with one used word; batch begin clears 1024 bytes. Capacity/receipt belong to N02. |
| Animation | NUKEXPLO sound; nominal source frame dimensions 91×72 before primary-view zoom, 11 frames, sound wait 5500 ms, per-frame wait 100 ms; original surface/sprite layouts belong to P03/P10. |

Logical civilization and object domains remain bounded even when an original
byte/shift calculation could reach unrelated storage. The original upper bits
of the temporary argument used to remember a tile do not affect its retained
feature byte or the synchronized feature value. Do not make incidental pointer
bits into new game state.

## Appendix B. Evidence and validation limits

Primary evidence is OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`, inspected in reference checkout
`fc2155ccdb2123c1137babab6fa39e38152278c9`. The relevant unit/map/civilization/
network sources and pollution caller have no difference between those pins.
Independent evidence is `civ2` revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. Neither reference tree was modified.

| Family | Source and recovered entry |
| --- | --- |
| Action, blast, movement/combat and spy joins | [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp): `unit_nuclear_attack`, `nuclear_blast`; `unit_move_dir_enemy_city` within `0059062c`; `unit_resolve_attack_nuclear` within `00580341`; `spy_city_mission_plant_nuke` within `004c6bf5`. |
| Pollution, features, memory, animation | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp): `tile_add_pollution`, `tile_set_improvements` `005b94fc`, `tile_update_known` `005b8b1a`, `known_set` `005b9d81`, `map_animate_nuke`. |
| Attitude operation | [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp): adjust/get/set at `00456f20` / `00467904` / `00467933`. |
| Batch boundaries | [network.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/network.cpp): begin `005b9ec6`, flush `005b9f1c`; record packing follows at `005b9fde`. |
| Meltdown caller | [retvals.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/retvals.cpp): `city_process_pollution`. |
| Shared encodings | [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h), [game records](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/game.h). |

Independent `src/move_city_entry.cpp` and `src/map_updates.cpp` corroborate
captured target owner, first SDI slot, cached tile reference, signed population,
draw ordering and pollution-count wrap. `src/diplomat_action.cpp`,
`src/city_turn.cpp` and `src/combat_entry.cpp` corroborate the distinct callers.
Their explicit runtime boundaries are evidence of required children, not
permission to substitute generic success.

A temporary native harness extracts **12 additional routines** for action,
blast, the U02 foreign-city child, pollution/features/memory, attitude and batch
begin/end, alongside **63 inherited unit/combat routines**. Optimized and
unoptimized AddressSanitizer/UndefinedBehaviorSanitizer builds pass **8,037
assertions**, using signed-char/wrap semantics and disabled strict-alias
optimization. Leak detection is disabled for the ptrace environment. Routine
counts do not imply that every inherited path or combination was exercised.

Cases cover all 256 stored population values, and all 256 feature bytes under
all-even and all-odd supplied draw sequences, including explicitly off-nominal
feature combinations without a corresponding city record. Additional cases
exercise SDI order/distance/ownership, actual U01 stack deletion, mixed-owner
first-member effects, live animation/redraw callbacks, hotseat attitude scratch,
pollution idempotence/count wrap, width-2 repeated visits, invalid-center behavior,
network notification order and actual batch begin/end. Real U06 nuclear and U02
foreign-city **children** call the real core on interception and completion;
these are not complete movement-dispatcher or full-combat-parent runs.

The bounded map/query, animation, redraw, UI, elimination, wire transport and
map-record enqueue services are declared adapters. Feature and remembered-feature
setters, attitude updates, batch begin/flush and U01 deletion execute extracted
code; packing/receipt, actual elimination and frame timing do not. The batch
adapter counts records rather than validating buffer capacity or wire bytes.
The retained-tile pointer's irrelevant upper temporary bits are omitted. Scripted
primary draws are supplied external fixture results; unscripted ones use F01's
CRT recurrence. These checks do not establish runtime graphics, allocation
failure, every callback/backing case or whole-turn conformance.

The independent historical report
`analysis/reconstruction/single-player-positive-nuclear-acceptance-2026-10-23/README.md`
records matched Debug/Release/original action and reload exits for Plant Nuclear
Device. Its declared fixture supplied four prerequisite bytes, then ordinary
capture checks and blast execution produced city size 5→3, deleted two foreign
garrison units and an adjacent own unit, placed six pollution tiles and allowed
the spy to escape. It also reports direct action/fallout isolation comparisons.
Those archived runs were **not rerun here**. They do not cover all SDI, AI,
network, unidentified-government or alternate-delivery cases.

No fresh original-executable run, instruction export or annotation change was
performed for U07. X02 must retain independently reproducible state/draw/trace
fixtures, join V06/W05 delivery and real U02/U06 parents, and exercise original
runtime dialogs, animation, elimination, synchronization and receipt. W05 now
owns shared pollution placement and exercises the actual meltdown-to-blast
join with declared external adapters; full-turn/runtime integration remains.
V04 owns the shared attitude primitive; the nuclear caller retains its
original call order. The finite checks leave the remaining integration obligations open.
