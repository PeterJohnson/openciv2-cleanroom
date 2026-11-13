# P02: Map rendering and minimap

This specification describes classic Civilization II Multiplayer Gold Edition
5.4.0f, patch 3 map composition: terrain connections and overlays, cities, unit
stacks and shields, labels, redraw scheduling, and overview colors/rectangles.
It includes the shared city/unit sprite renderers used by other interfaces.
The required output conveys the specified map information and visibility.
Ordered raster/text operations describe original reference rendering; different
art and composition are permitted without changing fog, knowledge or game rules.

[P01](P01-map-projection-and-interaction.md) owns zoom arithmetic, view geometry,
projection, membership, clipping rectangles and overview transforms.
[W01](W01-map-geometry-and-spatial-state.md) owns topology, neighborhoods,
feature predicates, resource patterns and distance; [W03](W03-visibility-and-exploration.md)
owns visibility and remembered information. [U01](U01-unit-lifecycle-and-stacks.md)
owns stack traversal, [U04](U04-unit-orders-and-activation.md) activation/blink
state and unit eligibility, and [C08](C08-buildings-and-wonders.md) building and
active-wonder queries. [P03](P03-graphics-and-asset-processing.md) owns resource
loading, sprite extraction/markers,
palettes, scaling and pixel primitives; [P04](P04-text-and-dialogs.md) owns fonts, measured/styled text
and window chrome. P05/N02 own native input and network polling. P10 owns
animation timelines that supply temporary rendering selections here.

[F01](F01-determinism-and-arithmetic.md) applies to arithmetic and ordering.
These rendering algorithms do not directly consume gameplay random choices.
Message pumps and child callbacks can execute other algorithms, so drawing is
not generally a pure snapshot of game state.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Required contract and reading guide

Terrain/resource/route/city/unit information and identities, stack/selection
semantics, actual versus remembered knowledge, fog/privacy and any simulation
changes made by queries. Scenario overrides remain supported.

The numbered main sections specify required behavior under
[X01](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy).
Original rendering, geometry, widgets, cosmetic media and native traces are in
[Appendix D](#appendix-d-original-presentation-reference); they are optional reference material.
Main-section numbers are retained for stable citations. Required calculations
that happen inside a presentation operation remain in the main text, including
state mutations, visibility/choice rules and file/protocol effects. Native event
traces do not replace semantic operation boundaries. Harmless UI failures and
rejected off-nominal inputs follow X01's declared safe policies.

## 1. Inputs and observable state

A nominal operation supplies a loaded world, observer, logical view and valid
city/unit/rule identities. Terrain queries accept a negative observer for
unrestricted terrain and a city-radius index or negative ordinary-map selector.
Valid city/unit preview records need not occupy playable coordinates.

Required outputs are the permitted terrain/city/unit facts, representative
identity, report information, and gameplay/network/input-discard effects. Native
draw order, graphics targets, scale resets, colors and glyph positions are
reference behavior in Appendix D.

Use these state distinctions:

| State | Meaning |
| --- | --- |
| Supplied terrain observer | Controls terrain knowledge and remembered improvements. It need not equal the local observer used for city/unit overlays. |
| Unrestricted display | The shared score-locked/unrestricted mode; several callers substitute observer −1 or override visibility. |
| Scenario reveal | Both scenario mode and whole-map reveal. It overrides some city display checks, not every unit/label/overview check. |
| Bare-terrain display | Suppresses selected terrain features and city/unit/label overlays. It does not uniformly remove all resources or pollution. |
| Blink-active, blink-phase | Shared low-byte states; their raw values can matter, particularly overview equality. |
| Active unit and shared map selection | U04/P01 state, independent of temporary rendering overrides. |
| Selected override | Temporary selected unit plus x/y, used by combat/animation. |
| Moving-stack override | Another temporary unit plus x/y, used while movement exposes a different stack member. |
| View/input flags | Session active, map-redraw-busy, input-discard suppression, discard mode, network game mode and overview suppression. |

Both temporary override unit indices and their coordinates are −1 in the
original initial image. Rendering consumes their supplied live values; callers
may retain/change them, and an adapter’s zero initialization is not evidence
for a different original initial value.
Do not infer an override from the active unit. P10/U02/U06 own their publication
and retirement around animation/movement/combat.

F01 defines reference calculation results before comparisons or truncating
division, unless another logical range is stated. Equivalent wider evaluation
is permitted; display geometry alone does not constrain internal types. Arithmetic shifts differ
from division on negative values. Captured owner, size, zoom, pixel coordinates
and loop bounds remain captured only where specified; reads after callbacks
are live. Nominal scalars do not overflow, but accepted off-nominal scalar wrap
is part of compatibility. A restrictive input profile may reject such values
before starting and declare that limitation.

Memory corruption is excluded under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs).
Invalid reads require known backing, supplied retained input or an explicit
stop after preceding valid effects. A negative unit result is not universally
an early-return sentinel in these callers; the precise boundaries are below.

## 2. Terrain tile composition

Present terrain, river, resources and improvements for the supplied observer.
Use the following knowledge/actual-versus-remembered rules before drawing. Source
asset identities and overrides follow P03; decorative coast joins and raster
layers may change without changing the represented terrain or its visibility.

A nonnegative radiusIndex greater than 20 makes the tile invalid without a
world-bounds query. Otherwise test W01 rectangular validity. If observer is
nonnegative or the tile is invalid, require a valid tile and, for a valid tile,
W03 visibility to that observer. On failure, disclose only unexplored terrain.

Recheck world validity after those calls. If now invalid, capture Tundra as
terrain, no improvements, no river and invalid=true. Otherwise capture actual
terrain and actual river state; capture actual improvement components when
observer is less than 1, and that observer's remembered improvement components
otherwise. Set invalid=false. This second check matters when callbacks changed
the map after the first check. A negative observer bypasses knowledge, not the
initial world-bounds failure.

Required feature information uses captured center improvements unless stated
otherwise. Irrigation without Mine is irrigation; both mean farmland; Mine
without Irrigation is mine. Fortress without City is fortress; both mean airbase.
W01 supplies actual resource/hut/Grassland-shield predicates. Road/railway display
includes actual city presence and neighbors' actual features for observer<1,
remembered features otherwise. Neighbor City components count as connections.
Bare-terrain suppresses roads, mines, huts, fortresses, airbases and city/unit/name
overlays; special resources and pollution remain available. An airbase's occupancy
indication scans the actual stack for an air-domain unit without an additional
observer visibility/owner test. Decorative adjacency joins, fog-edge shapes and
zoom-dependent omission are in Appendix D; a new renderer must preserve the
knowledge sources above rather than replace remembered facts with live ones.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 3. Civilization colors and unit selection

Civilization identity must remain distinguishable. New display colors may differ;
scenario color metadata remains an input. Preserve the representative-unit
selection below.

### Preferred unit in a stack

If the supplied unit is negative, return −1 before record or map reads.
Capture that unit's signed x/y. If valid, query whether it is Ocean; retain
that query and any failure/effects, although its Boolean is not used afterward.
Initialize selected=−1 and bestPriority=−1. Traverse U01's top-of-stack and
forward links:

1. Assign priority 1 for Land, 2 for every other domain.
2. If this slot is the live active unit and its signed owner equals the local
   observer's low unsigned byte, assign priority 4.
3. Reread type/domain. If Air and an airbase-owner query succeeds at the
   **captured incoming unit coordinates**, override priority with −1.
4. Replace selected/bestPriority only for a strictly larger priority.

Thus the first tied unit wins. Air units on an airbase lose even active-unit
priority, and a stack containing only such units can produce −1. Do not select
by attack/defense strength or movement allowance; those are different algorithms.
An invalid/cyclic stack follows U01's backing/traversal contract, not an invented
render-only repair.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 4. Direct unit renderer

For a requested unit, optionally select the representative through §3 before
reading/displaying its record; a negative representative produces no unit.
Show unit identity/type, ownership, stack presence, damage/health, veteran status
and orders subject to §6 visibility. Do not change U01 stack order or combat
values to suit icon geometry. Previews use the supplied record, not an implicit
live unit selection. Health uses the rule's signed hit points
(zero substitutes10) and unsigned damage; remaining=hitPoints−damage under F01.
Simplified combat or a request without damage reports undamaged health without
changing stored damage. The original bar's zoom cutoff, pixel length and color
thresholds are optional; new health presentation must not clamp imported values
silently or change combat state.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 5. Direct city renderer

Display city identity, ownership, wall/garrison/disorder indicators and permitted
population information. City Walls or active Great Wall supplies the wall flag.
Garrison presence uses actual occupancy as specified, without adding a unit-
visibility test. Decorative city style/size classes do not change stored city size.
The population disclosure rule is:

Capture size
as a signed byte: actual size when owner equals local observer, unrestricted
display is on, scenario reveal is on, or caller requests actual size; otherwise
that city's remembered size for the local observer.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 6. Tile overlays and visibility

### City overlay

Look up the city before the coordinate-validity check; return if absent or
invalid. Query W03 visibility to the local observer. If signed owner differs
from the observer's low unsigned byte, require the tile visibility and either
city KnownTo bit or the reread owner matching that low byte; then require
remembered size for that observer to be nonzero. A local owner retains the
tile-visibility result rather than bypassing it.

Show the city if that result is true, unrestricted display is enabled, or
scenario reveal is enabled, using §5 ordinary disclosure. The caller's terrain
observer is not passed to this city operation.

### Unit overlay at a tile

Return on invalid world coordinate; otherwise query top unit, including an empty
result. Pixel offsets are reference behavior.

1. If selected override is nonnegative and its live x/y match the tile,
   follow stack links until reaching the live selected index or a negative
   result. Invoke the one-unit chooser below even when traversal ended at −1;
   return afterward. This override bypasses the normal visibility path.
2. If moving-stack override is nonnegative and its x/y match, invoke the
   chooser for that live override and return.
3. If actual city owner is present, return unless mode is exactly Move-units
   and blink-active is nonzero. Traverse to the live active unit; return if
   absent, unable to move, Fortifying or Fortified. The latter are full signed
   order-byte equality tests.
4. Query W03 tile visibility to the local observer, then read this unit's
   signed owner. That read occurs even if top/selection returned −1. For a
   foreign owner, require tile visibility and either the unit's visibility bit
   or a subsequent owner read matching the observer's low unsigned byte.
   A local owner keeps the tile-visibility result.
5. Draw through the chooser if visible or unrestricted. Scenario reveal alone
   is not an override here.

Negative record reads need the backing described in Appendix B or a bounded
stop. Do not replace all empty-stack paths by an unconditional no-op and claim
the same off-nominal behavior; equally, do not reproduce corrupting writes.

### One-unit chooser and blink wrappers

For a supplied slot and pixel position:

- If selected override is nonnegative and equals the slot, call the blink
  wrapper and return.
- If moving-stack override is nonnegative and equals it, call direct renderer
  with ShowStack+ShowDamage, without representative selection, and return.
- Otherwise read active-unit x, then supplied-unit x. If equal, reread active
  slot for y, then supplied y. If equal, reread active slot for unsigned owner;
  if that owner is human and the live active unit can move, call the blink
  wrapper for the **fresh active slot after eligibility callbacks** and return.
- Otherwise call direct renderer with all three flags. Its representative
  selection may choose a different stack member.

The blink wrapper omits the unit exactly when blink-active and blink-phase
are both nonzero and mode is exactly Move-units. Otherwise it calls direct
renderer with ShowStack+ShowDamage and fortress suppression zero. The wrapper
that draws a unit at its own location has this same early gate before any
unit reads, then loads y before x, projects with P01, subtracts live stepY and
invokes the direct renderer with those flags.

### Complete map tile

Present terrain for the supplied world coordinate/observer and ordinary radius
selector−1. Service relevant external events even when it is unexplored/invalid.
If now-live unsigned game mode is at least3, poll network with `(1,0)`.
Recheck world validity after polling; return on failure.

Test bare-terrain once: when off, draw city overlay, then unit overlay, without
retesting that flag between the two calls. Their own visibility/state reads
remain live. A callback changing bare-terrain during city drawing does not
cancel the already-selected unit call.

If ShowCityRadius is now enabled, scan cities from slot 0 to the **live signed
city bound**. Require full nonzero
city identity, signed owner equal to the supplied terrain observer, and W01's
weighted distance from this tile at most 2. Read city y before x for that
query; first success ends the scan. Indicate city radius on success, ordinary
grid otherwise. This indication
has no additional exploration gate and still runs in bare-terrain mode.

## 7. City names

Bare-terrain at entry bypasses every view/font/city read. Otherwise, when
supplied radius is nonnegative, add 3 with wrapping. Select the view's large
font, then scan city slots from 0 upward, rereading the signed city bound at
each loop test. Require a full nonzero identity; capture signed city x/y.
If the adjusted radius is nonnegative, skip beyond W01 distance; a wrapped
negative adjusted radius removes that filter. Require P01 view membership.

Use the city-overlay visibility procedure from §6, including remembered size,
but its only override here is unrestricted display: **scenario reveal does
not independently enable names**. Do not recheck bare-terrain after text calls.

For each admitted city, show its authored name and ownership indication. Preserve
the visibility and radius gates above. Name bytes must be safely readable;
missing terminators follow the declared input policy. Shadow passes, label
measurement and native helper return-register residue are reference details.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 8. Full and partial map redraw

A refresh is inactive when the session is inactive. While it owns the refresh
operation, preserve the busy/deferral gate, the selected observer and the input-
discard policy: discard queued input only when suppression is clear and discard
mode is zero. Normal completion releases the gate; required failure effects
remain with the caller. Network service must remain responsive and relevant
external events are applied before later semantic reads. Tile traversal, clipping,
repaint counts and native pump placement are optional presentation procedures.
A local refresh must not mutate knowledge or substitute a different active unit.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 9. Overview rendering

The overview is a distinct disclosure path; do not substitute the main map's
city/unit overlay filters. For a tile:

1. Invalid coordinates disclose nothing. Query W03 visibility to the local
   observer; when false and unrestricted display is off, disclose nothing.
2. If a city exists, capture its signed owner. Show city identity/ownership when
   unrestricted, its signed KnownTo mask includes the local observer, or its
   reread signed owner equals the observer's low unsigned byte. There is **no
   remembered-size test**. Ownership indication uses the captured owner.
3. Otherwise distinguish actual Ocean from other terrain. Scenario reveal has
   no independent override. The original overview does not independently reveal
   units. Selection highlighting and viewport geometry may be redesigned.

Suppression prevents overview updates. Full overview refresh also requires an
active session; partial updates have no extra session-active gate. Neither path
mutates knowledge. Native color lookup, blink phase, raster traversal and repaint
scheduling are reference procedures.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 10. Nominal and off-nominal conformance

Validate terrain and remembered improvements for each observer, city/unit/name
visibility separately, representative-stack tie breaking and active/moving
selection overrides. Test scenario reveal versus unrestricted display, private
knowledge, required polling/deferral and input-discard effects. Pixel, palette,
blink and redraw-traversal cases are optional reference validation.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## Appendix A. Evidence and source ownership

Primary OpenCiv2 pin:
`a71e8aefffe04e6f719e0e213b027b128a747711`; inspected worktree HEAD
`fc2155ccdb2123c1137babab6fa39e38152278c9`. Independent evidence repository
`civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. The original executable SHA-256 is
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Neither repository nor original asset was modified by this documentation work.

| Behavior | Primary navigation/original entry; independent owner |
| --- | --- |
| Terrain composition | [mapview.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/mapview.cpp), `0047a8c9`; `src/terrain_tile.cpp`, complete `draw_terrain_tile`. |
| Coast descriptors | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp), `tile_calc_coast_masks`; independent `map_ocean_quadrants` in `src/terrain_tile.cpp`. |
| Unit renderer/selection | [graphics.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/graphics.cpp), draw `0056baff`; selection `0056b96e` in [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp); `src/unit_renderer.cpp`. |
| City renderer | Same primary graphics file, `0056d289`; `src/city_renderer.cpp`, complete `draw_city`. |
| Map overlays | Primary mapview: city `0047ba1d`, stack `0047bba5`, blink `0047bbea`, location `0047bc59`, chooser `0047bd04`, tile units `0047be63`, complete tile `0047c103`; `src/map_window.cpp`. |
| Labels/redraw | Same primary: labels `0047c443`, neighborhood `0047c869`, all tiles `0047c9d4`, present-with-poll `0047cb53`, partial `0047cbb4`, full `0047cd51`; independent `src/map_window.cpp`. |
| Overview | Same primary: color `00406e61`, viewport `0040701e`, full `0040733c`, partial `004070f1`; `src/map_window.cpp` and partial region in `src/overview_interaction.cpp`. |
| Asset inputs | Primary [graphics.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/graphics.cpp); independent `src/terrain_sprites.cpp`, `src/city_sprites.cpp`, `src/sprite_initializer.cpp`. P03 owns producer rules. |
| Font selection and data | Primary [text.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/text.cpp), [gamedata.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/gamedata.cpp), [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h); independent `src/font.cpp`. |

The research inventory includes 52 P02 bodies and ten reused P01 geometry
bodies. The latter remain owned by P01. In particular, P02 does not restate its
projection or radius-rectangle formulas. Original instruction samples resolve
the asymmetric neighborhood scan and confirm the signed health calculation;
16 instruction probes and initial override/sample/gate data checks are
recorded with the research bundle.
The original overview instructions compare the raw phase byte to a Boolean;
the primary decompilation’s Boolean cast must not erase values greater than 1.
The original initial image also establishes all six override values as −1,
independently of reconstructed global initialization.
The source retains explicit native/unknown-value boundaries; a reconstructed
adapter's diagnostic exception is not claimed to be the original UI result.

## Appendix B. Representation and excluded bugs

The following values identify original snapshots and externally observable
palette/flag choices; they do not require a matching memory model.

| Logical item | Original value |
| --- | --- |
| Terrain observer unrestricted | Negative value, ordinarily −1; observer 0 still uses actual features with visibility checks. |
| Unit flags | ShowStack=1, SelectRepresentative=2, ShowDamage=4; consumed as a byte. |
| City flags | SuppressAnnotations=1, SuppressGarrison=2, ActualSize=`0x1000`. |
| Normal Combat | Rule `0x0010`; clear is Simplified Combat. |
| ShowCityRadius grid | gameFlags `0x20`. |
| Bare-terrain gate | Shared DWORD `0062bcd8` in the primary map-view evidence; use its canonical backing mapping when validating. |
| Components | City=`0x02`, Irrigation=`0x04`, Mine=`0x08`, Road=`0x10`, Railroad=`0x20`, Fortress=`0x40`, Pollution=`0x80`. Farmland and Airbase are component combinations. |
| Terrain/river | Low terrain nibble; River=`0x80` in the terrain byte. Ocean=10, Grassland=2, Forest=3, Mountains=4, Hills=5, Tundra=7. |
| Blend families | Unexplored=11, Ocean=12; ordinary terrain families 0–10. |
| Shared coast contacts | Preceding=1, axial=2, following=4. |
| Unit special sprite | Diplomat type 46 owned by barbarian 0 uses sprite 62. |
| Orders/attributes | Fortifying=1, Fortified=2, Sleep=3, Goto=11, AI-idle=16, NoOrder=255; AwaitingInput=`0x8000`. |
| Preview | Reserved unit slot 2048; ordinary capacity remains 2048 and this slot is not an allocated gameplay unit. |
| City style prerequisites | Industrialization=37; Automobile=5 and Electronics=24; Palace=1, City Walls=8, Great Wall=6. |
| Sprite placeholders | Primary=6, secondary=5; replacement consumes low color byte. |
| Palette colors | Black/UnknownBlack=10; stack-shadow=15; main-shadow=16; colored unit=26; AwaitingInput text=29; Highlight=41; health Green=42, Yellow=122, Red=106; overview Ocean=93, Land=48; overview clear=0. |

[P03's city-sheet producer](P03-graphics-and-asset-processing.md#cities-shields-and-units)
owns the three color samples and marker extraction. The fourth record component
is retained, not an additional supplied color. Unit/city marker tables remain
live inputs here; city marker index is `style*8+sizeClass*2+walls`.

Important bounded read cases:

- Map unit overlay and chooser can read a negative active/top result before
  deciding whether to draw. The original unit base is `006560f0`, stride 32;
  unit −1 fields can reach preceding civilization power-history storage.
  Record 2050 begins in subsequent map-region backing. Preserve evidenced
  byte/word reads from supplied canonical backing or stop. Neither implies a
  legitimate unit with that identity.
- Color helpers can read leader/color data outside their nominal tables. The
  independent adapter maps some signed leader indices into live order data,
  and color indices into saved-window/main-window/order/leader backing. Such
  reads require their actual produced bytes or a bounded stop, not zero-filled
  invented colors. Ordinary city-label helper admission is narrower; its stop
  is a reconstruction limit, not an original clamp.
- Unit marker indices beyond the nominal table can read adjacent marker/citizen
  sprite backing. A direct body receiver beyond the reconstructed sprite array
  is a separate boundary. An out-of-range type can read adjacent rule/event
  bytes before reaching that later receiver; retain preceding effects.
- Original city population sample strings are two four-byte spans beginning
  at `00633e58`, both initially `8` plus NUL/padding. A first sample without
  its early terminator can extend into the second span. Require backed NULs;
  out-of-bounds destination writes are excluded even when the read is mapped.
- City names may lack a terminator in their logical name field. Supplied
  continuation bytes can establish a read result; they do not authorize
  reproducing an overflowing downstream text write.

The name helper's bare-terrain return is incoming EAX. Full-tile traversal
supplies its terminal row limit as that incoming value. Otherwise the name
helper returns the signed live totalCities from its final comparison. Full
redraw can replace this result with overview/presentation or input-discard
results; void drawing callers discard it. Preserve an unknown result only where
an actual caller observes it, and retain preceding effects before any stop.

Safe drawing must exclude out-of-bounds surface stores, unsafe allocation-size
wrap leading to overwrites, invalid object writes and overflowing text buffers.
A malformed rectangle, sprite or count may be rejected/stopped under a declared
safe profile. Do not translate corruption of a neighbor record into an intended
palette/font/game-state mutation. Scalar wrap, deliberate preview storage,
backed reads and rendering quirks remain distinct from these excluded bugs.

## Appendix C. Validation record

The historical research bundle included pinned source inventories, complete
extracted bodies, the bounded harness and document/binary checks. That
scaffolding is not included in this repository or required for implementation.
Original input identities and source links above identify the evidence revisions.

The harness compiles 24 unchanged whole bodies: ten P01 geometry helpers and
14 P02 terrain/tile/redraw/overview helpers. Its view adapter preserves offsets
consumed by those bodies; graphics, rectangle backend, world queries, unit-stack
lookups, city/unit/label children, fonts, message delivery, network poll and
window services are explicitly controlled. No original executable code is
executed. It observes sprite identities/coordinates and ordered service traces,
not a real native frame buffer.

Both optimized and ASan/UBSan runs pass 39 assertions. They cover coast masks,
unknown/radius rejection, blend/river zoom thresholds, actual versus remembered
features, city irrigation underlay, road/rail selection, bare-terrain exceptions,
airbase occupancy, fog quadrants, actual tile pump/poll ordering, full city IDs,
row traversal and asymmetric neighborhood order, clip restoration, overview
owner colors/blink equality, inactive/suppressed returns, signed discard return,
actual P01 geometry joins and partial overview presentation.

Existing independent recipes `tests/terrain_tile.py`, `tests/city_renderer.py`,
`tests/unit_render_reference.py`, `tests/map_redraw_resources.py` and their
native fixtures cover additional resource-backed comparisons under their own
supplied maps, records, scales and platform services. They were inspected but
not rerun here, and are not counted as newly executed native conformance.
The 39 assertions do not validate the complete direct unit/city renderer,
actual text/raster output, production window initialization or arbitrary
reentrant gameplay. §10 lists the remaining finite joins. Source accounting,
bounded tests and whole-game compatibility remain separate claims.

## Appendix D. Original presentation reference

This appendix retains the original presentation procedures removed from the main
specification. Geometry, pixel/sample output, native controls and cosmetic timing
are optional reference behavior under X01. Required semantic fragments were kept
in the numbered main sections and are linked where omitted here; this appendix
does not relax their simulation, information, file or protocol obligations.
Original section numbers and local § references identify the recovered procedure
family, whether its retained fragment is in the main section or here. Historical
native/pixel tests are reference validation, not baseline acceptance criteria.

### Original §2. Terrain tile composition

Inputs are target, pixel origin `(px,py)`, map coordinate `(x,y)`, observer,
zoom and radiusIndex. The pixel origin is the upper-left corner of the terrain
bounding rectangle, not the taller city/unit rectangle.

#### Entry, dimensions and knowledge

If the receiver's signed stored zoom equals supplied zoom, capture its cached
tile width/height and half-width/half-height. Use them even when inconsistent
with the level. Otherwise compute width `scale(64,zoom)` and height
`scale(32,zoom)` using P01, then arithmetic-shift each right one for the halves.
These cached dimensions survive sprite callbacks throughout this tile call.

[Required behavior is retained in the main section.](#2-terrain-tile-composition)

Terrain and river connections below normally use actual neighboring terrain,
not remembered terrain. Initialize four blend choices to None. Then perform
the terrain-specific base composition, resource and feature layers, followed
by unexplored-edge blends. Reset sprite scale only after normal completion.

#### Coast quadrants

The coast helper clears all four shared quadrant descriptors, then returns 0
if the center is invalid. Otherwise scan W01's eight directions in order:
NE, E, SE, S, SW, W, NW, N. Wrap the neighboring x once. An invalid neighbor
counts as land; a valid neighbor counts as land exactly when terrain is not
Ocean. Increment landCount for each land neighbor.

Each quadrant descriptor records three independent land contacts: its preceding
diagonal, outward axial neighbor and following diagonal. The four quadrants
are top, right, bottom, left. For a diagonal direction `d=0,2,4,6`, set the
following-diagonal contact in quadrant `d/2` and preceding-diagonal contact in
quadrant `(d/2+1) mod 4`. For an axial direction `d=1,3,5,7`, set the axial
contact in quadrant `((d+1) mod 8)/2`. The descriptor's sprite index is the
sum of contact weights preceding=1, axial=2, following=4. Return landCount;
the rendering caller ignores it and uses the shared descriptors.

For an Ocean center:

- At zoom below −3, draw the plain Ocean base sprite and skip coastline and
  river-mouth scans.
- Otherwise calculate the shared descriptors, then draw quadrants 0–3 using
  their live descriptor at the moment of each draw. With captured half-width
  `hw` and half-height `hh`, their origins are `(px+sar(hw,1),py)`,
  `(px+2*sar(hw,1),py+sar(hh,1))`,
  `(px+sar(hw,1),py+2*sar(hh,1))`, `(px,py+sar(hh,1))`.
- Next visit the four diagonals NE, SE, SW, NW. For each valid neighbor whose
  actual River flag is set, draw that direction's river-mouth sprite at the
  original `(px,py)`. There is no bare-terrain or knowledge test in this scan.

A neighboring tile's Ocean/land test is independent of whether it is explored.
Callbacks may change later descriptors or neighbors; the descriptors are shared
working state, not four immutable locals captured before drawing.

#### Land base, connections and underlays

Begin same-terrain connectivity and river connectivity at zero. If zoom is
below −4, set river connectivity to all four contacts and skip neighbor queries.
Otherwise scan diagonals NE, SE, SW, NW with weights 1,2,4,8. For each valid
neighbor, capture its actual terrain/river value:

- If terrain matches the center, add that direction weight to same-terrain
  connectivity. Otherwise assign its terrain to the corresponding blend,
  substituting the Ocean-blend family for Ocean.
- If it has River or is Ocean, add that direction weight to river connectivity.

Draw center base terrain. At zoom greater than −5, draw each non-None blend in
quadrant order. The four blend origins are `(px+hw,py)`, `(px+hw,py+hh)`,
`(px,py+hh)`, `(px,py)`. Draw Forest, Mountains or Hills connection sprite for
that terrain, using same-terrain connectivity; other land has no shape layer.

Continue in this order:

1. At zoom greater than −4 and with bare-terrain off, draw Farmland if captured
   improvements contain both Irrigation and Mine components.
2. At zoom greater than −7 and with bare-terrain off, draw River when the
   captured center river flag is set, using river connectivity.
3. At zoom greater than −4, derive a city underlay. Only when captured
   improvements have City but no Fortress component, inspect actual improvement
   flags of valid SE then SW neighbors. Track whether any has Irrigation and
   whether any such neighbor also has Mine. The underlying component tests
   apply to neighboring Farmland too. With bare-terrain off, draw Irrigation
   if the center has Irrigation without Mine **or** the neighboring summary is
   Irrigation-only; otherwise draw Farmland if the summary includes Farmland.
   The first condition wins even if the neighboring summary contains Farmland.
4. At zoom greater than −4, if actual terrain is Grassland and W01's shield
   pattern holds, draw the Grassland shield resource. This ignores bare-terrain.

The adjacency underlay can therefore use actual improvements alongside a
remembered center. It is not a generalized smoothing of all irrigated neighbors.

#### Resources, roads and later features

After the Ocean/land branch, at zoom greater than −4 and invalid=false,
query W01's special resource. For a nonzero resource index, draw the matching
terrain/resource sprite at `(px,py)`. This is independent of bare-terrain.

At zoom greater than −4, bare-terrain off, and either captured Road or an
actual city owner present, draw connections as follows:

1. Railway is true if captured Railroad is present or the actual city-owner
   query succeeds. Capture this result; initialize roadCount and railCount 0.
2. Scan all eight W01 neighbors. For valid neighbors read actual features if
   observer<1, otherwise that observer's remembered features. A neighbor with
   Road **or City component** increments roadCount. Draw its road segment
   unless Railway is true and the neighbor has Railroad or City component.
3. If Railway is true, perform a separate eight-neighbor pass with the same
   actual/remembered choice. Every Railroad-or-City neighbor increments
   railCount and draws its railroad segment. Later reads are live after road
   drawing; don't combine these two passes into one snapshot.
4. Query actual city owner again. If no city, draw the isolated railroad dot
   when Railway and railCount=0; otherwise draw the isolated road dot when
   not Railway and roadCount=0. Cities suppress dots.

Then at zoom greater than −4 and bare-terrain off, draw Mine when captured
improvements have Mine without Irrigation.

At zoom greater than −5 and invalid=false, continue:

- Draw Pollution when captured Pollution is present, even with bare-terrain on.
- Draw a hut when W01's actual hut predicate succeeds and bare-terrain is off.
- Draw Fortress at `(px,py−hh)` when captured features have Fortress without
  City and bare-terrain is off.
- Draw Airbase at `(px,py−hh)` when captured features have both Fortress and
  City and bare-terrain is off. Scan the actual unit stack from its top until
  finding an Air-domain unit; choose occupied/unoccupied airbase accordingly.
  Form the rectangle `(px,py−hh,width,height+hh)`. Query the actual occupant
  owner and primary shield color, then replace the sprite's primary placeholder
  color throughout that rectangle. Neither unit visibility nor ownership of
  the observer filters the occupancy scan.

#### Unexplored edges

Finally, when zoom>−4 and observer>=0, examine diagonals NE, SE, SW, NW.
For a negative radiusIndex, skip a world-invalid neighbor entirely: ordinary
map boundaries do not automatically draw a fog corner.

For radiusIndex 0–20, mark the neighbor hidden if it is world-invalid. Add its
diagonal offset to W01's city-radius offset for radiusIndex, take wrapping
absolute values, and mark hidden if W01's distance-from-deltas exceeds 2.
For any larger index mark hidden (normally entry already returned). If not yet
hidden, query visibility to the supplied observer and hide on failure.
Draw the unexplored-edge blend for each hidden quadrant at the same origins
as land blending. Hidden terrain, city-radius clipping and world invalidity
are separate decisions.

### Original §3. Civilization colors and unit selection

P03 loads eight color records from the city asset. Each provides primary shield,
secondary shield and label/overview colors. Ordinary primary/secondary/label
queries select record 0 for civilization 0; otherwise read the civilization's
leader and that leader's configured color index. Each call resolves these inputs
again. Imported signed leader/color indices retain their read-domain boundaries;
there is no universal clamp to 0–7.

The overview's city-color path is deliberately different: it indexes the record
by cached **city owner directly**, bypassing the civilization's leader/color
mapping. Asset colors are live inputs; palette indices appear in Appendix B.

[Required behavior is retained in the main section.](#3-civilization-colors-and-unit-selection)

### Original §4. Direct unit renderer

Inputs are target, unit, drawing flags, pixel x/y, zoom and fortress-overlay
suppression. Flags are logically ShowStack, SelectRepresentative and ShowDamage;
only the original low-byte flags are consumed. This renderer is also used in
previews and dialogs, not just map tiles.

Select sprite scale first. If SelectRepresentative, call §3; if its result is
negative, reset scale and return before drawing-record reads. Otherwise continue
with the selected/supplied slot, without a universal nonnegative guard.
Capture signed owner and unsigned unit type. Barbarian Diplomat uses the special
barbarian-leader sprite; all other types use their corresponding unit sprite.
Resolve primary then secondary shield color. Use P03's live per-sprite shield
markers to compute `sx=x+scale(markerX,zoom)` then
`sy=y+scale(markerY,zoom)`. Shield width is scale(12), height scale(20).
Reread markerX; direction is −1 when below 32, otherwise +1.

#### Shield stack and borders

If ShowStack and either current previous/next stack link is nonnegative:

1. Let offset=`scale(4)*direction`. Form the secondary-shield rectangle at
   `(sx+offset,sy)` using the captured width/height.
2. If wrapping absolute offset exceeds 2, draw a shield at
   `(sx+offset+direction,sy+1)`. Temporarily offset its rectangle by
   `(direction,1)`, recolor both shield placeholders to stack-shadow, then
   move the rectangle back.
3. Draw the secondary shield at `(sx+offset,sy)` and recolor both placeholders
   to the captured secondary color.

Form the primary rectangle at `(sx,sy,width,height)`. Draw its shadow at
`(sx+direction,sy+1)`, offset the rectangle accordingly, recolor both placeholders
to main-shadow, and restore its rectangle position. Draw the primary shield
at `(sx,sy)`; recolor primary placeholder to primary and secondary placeholder
to secondary. These are ordered rectangle recolor operations, not necessarily
recoloring only pixels written by the immediately preceding sprite.

#### Health bar

Construct/fill a black band of height scale(2) at the shield top. Advance the
same shield rectangle's top by the band's measured rectangle height. Construct
a health rectangle of height scale(3), advance shield top by that height, then
construct/fill another black scale(2) band and advance top again. Those updates
also determine the later order-glyph rectangle.

Capture health-rectangle width. Read current unit type's signed hit-point rule,
then unsigned unit damage. Replace zero hit points with 10. Force local damage
to zero if Normal Combat is off, ShowDamage is off, or zoom<−3. All three gates
have that same effect; they do not erase the unit's stored damage.

```
barLength = trunc(wrap32((hitPoints − damage) * barWidth) / hitPoints)
color = Green  if damage < hitPoints/3
        Yellow if damage < wrap32(2*hitPoints)/3
        Red    otherwise
```

Use signed comparisons/division after wrapping; do not clamp damage, length or
hit points to ordinary ranges. If length differs from width, fill the entire
health rectangle black. If length is nonzero, copy the health rectangle,
reduce its right edge by `barWidth−barLength`, and fill that rectangle with
the chosen color. Signed quotient overflow is a stop/fault after prior shield
and band drawing, not a redraw rollback.

For hit points 10 and width 12, damage 0 gives length 12/Green, damage 4 gives
length 7/Yellow, damage 7 gives length 3/Red. Damage 12 gives length −2/Red;
a restrictive profile may reject that record, but compatibility does not
silently clamp it to an empty bar. P03 owns the resulting rectangle primitive.

#### Order glyph and body

Read the signed order byte's low four bits; separately reread full order and
force the local order to −1 when that full value is the AI-idle encoding.
Choose glyph:

- Local order below 1 or above 11: `*` if AwaitingInput is set, otherwise `-`.
- Orders 1–10: corresponding F03 order-table hotkey.
- Order 11: human owner uses Goto's hotkey; nonhuman ordinary display uses `-`;
  nonhuman unrestricted display uses the unit's raw AI-order character.

Owner membership uses the captured owner and live human set. A zero character
skips all glyph operations. Otherwise subtract scale(3) from shield.bottom.
Pass the signed character to the active CRT uppercase operation; if it returns
`M`, additionally subtract scale(1) from top and bottom. Select the shared
order font: size=`scale(32,zoom)/3`, replacing 10 by 11; replace font family 1
only if that size differs from its cache, then publish the captured size.
P04 owns creation/measurement. Select black text, changing to AwaitingInput
color if that attribute is now set. Draw the one-character NUL-terminated
string centered at `left+width/2, top+height/2`, using the live rectangle extents.

Choose a colored body when the current full order is Sleep. Also choose colored
body if the live unit domain is Air, its live coordinates are valid, a fresh
coordinate read finds an airbase, and it is not simultaneously the active slot,
mode exactly Move-units and blink-active nonzero. Draw the captured sprite at
original `(x,y)` with colored-body color or its ordinary palette. A full order
of Fortified, reread afterward, draws the fortification overlay.

Finally read current y then x. If valid, reread y then x for actual features;
when they are Fortress without City and fortress-overlay suppression is zero,
draw Fortress at `(x,y)`. Reset scale on normal return. A unit can move/change
orders in callbacks; captured owner/sprite/position remain captured, while the
late overlay predicates use their stated live fields.

#### Preview record

The preview initializer uses F02's reserved preview unit slot. Store low-byte
type then owner, x=y=−50, attributes 0, home Absent, spent movement 0, visibility
0, auxiliary timer 0, counter equal to that timer, both links Absent, full
NoOrder, and both Goto coordinates −1. Return the reserved slot. Preserve
ID, damage, direction, raw AI-order character and other untouched fields.
A flags-zero preview still reads damage, then suppresses its value in the
health calculation; it does not prove that the original initialized damage.
Only a backed, otherwise unobserved value can be folded away.

### Original §5. Direct city renderer

Select scale; capture width scale(64), height scale(48), then signed owner.
Form the entry rectangle and select target globally. Query secondary shield
color, primary shield color and unit owner at the live city coordinates, in
that order. Secondary and that first occupancy result are unused later, but
their reads/calls and faults precede the body even when annotations are hidden.
Cache primary color. Walls are present when City Walls exists or the owner has
active Great Wall; short-circuit in that order.

Start style from the owner's leader. If owner is currently human, replace it
with the tribe's configured style. Industrialization sets style 4; Automobile
and then Electronics set style 5, overriding the preceding style. [Required
behavior is retained in the main section.](#5-direct-city-renderer)

Choose size class by counting passed thresholds:

| Style | Thresholds for classes 1, 2, 3 |
| --- | --- |
| Below 4 (nominally 0–3) | 4, 6, 8 |
| 4 | 5, 8, 11 |
| 5 and other values in the final branch | 5, 11, 19 |

If Palace exists, size class is below 3, and either owner is local or difficulty
is below King and owner is not human, increment size class. This affects the
sprite, not stored city size. Draw sprite `(style,sizeClass,walls)` at `(x,y)`.
Nominal style is 0–5; an unsupported sprite index is a read/receiver boundary,
not a clamp to modern style.

Reread city coordinates and occupancy. If a unit owner is present and the
caller has not suppressed garrison shields, draw the captured owner's first
city shield at scaled marker-4 offsets for the captured style/class/walls.
This uses the actual occupancy query; no unit-visibility test is inserted.

Unless the caller suppresses annotations, reread city Disorder. If set, draw
the disorder sprite at `(x,y)`, then replace the primary placeholder by the
captured primary color over the **entry rectangle**, not the sprite's returned
rectangle. Otherwise draw a population badge:

1. Select the shared order font using §4's size rule; select its live font and
   target for text. Format captured signed size as decimal. Measure its width
   and read the font's cached height.
2. Construct a width sample from the first configured sample string (`8` in
   the nominal image); append the second (`8`) if decimal string length>1.
   A minus sign counts toward length. Measure this sample; set
   `extra=max(sampleWidth−populationWidth,0)`.
3. Badge width is `populationWidth+extra+5`; height is `cachedFontHeight+2`.
   Construct a rectangle at `(0,0)` of that size. Read/scale marker-3 Y and
   add y; then read/scale marker-3 X and add x. Offset the rectangle by these
   values, preserving Y-before-X marker reads across service calls.
4. Outline black; inset by `(1,1)`; fill primary color. Select text style
   `(black,black,1,0)` and draw the decimal string at
   `rectangle.left+sar(extra,1)+1, rectangle.top`.

Reset sprite scale on normal return. Cached owner/style/size survive drawing;
live city flags, occupancy, font handle and markers do not become a single
entry snapshot. P03 owns asset markers, P04 style and measurement semantics.

### Original §8. Full and partial map redraw

#### Full tile traversal

If either view margin is nonzero, fill the view target black. Starting row 0,
recompute `rowLimit=baseRows+drawRows+1` at each row test. For an admitted row,
capture map y=`top+row`, first x=`left+(row mod 2)` and column count:
baseColumns on odd rows, drawColumns on even rows. For columns from 0 to that
captured count, compute `wrapX(2*column+firstX)` using live topology and invoke
the complete tile routine. Per-row coordinates/count survive tile callbacks;
later row limits and geometry are live.

At the first failed row test, draw all city names with center `(0,0)` and
radius −1, passing that captured rowLimit as incoming residual result. Return
the name helper's result. There is no global map snapshot or batching away of
per-tile message/network pumps.

#### Full redraw controller

If the session-active byte is zero, return 0 without receiver access.
Otherwise:

1. Set map-redraw-busy=1; prepare the native surface/chrome, run P01 layout,
   then draw the window status/caption. P03/P04 own those services.
2. Read unrestricted display; capture drawing observer −1 if enabled,
   otherwise the supplied observer. Draw all tiles with that captured value.
3. If presentation requested, call the present-with-poll sequence below. Read
   view index afterward; if primary, redraw overview; otherwise retain the
   current view receiver as the original residual return value.
4. If input-discard suppression is zero, read the signed discard-mode byte.
   If zero, invoke P05 queued-input discard and use its result; otherwise
   use that sign-extended mode as result. If suppression is nonzero, preserve
   the preceding drawing/presentation result.
5. Clear map-redraw-busy and return the result on normal completion.

Present-with-poll is: native pump; if live game mode>=3, network poll `(1,0)`;
present the target; native pump; if live game mode>=3, network poll `(1,0)`.
The two gates are independent live reads. A stopped child leaves busy set and
preserves preceding drawing/input effects. Overview's final pump may have an
unrecovered residual return; a void caller may discard it, while a consumer
needs that input/result or a stop after valid cleanup.

#### Neighborhood traversal

Use P01's tile-radius rectangle and set it as the target clip, ignoring the
intersection Boolean. Capture `span=2*level+2`. Visit dy from −span through
+span by 1. For each dy, capture mapY=`centerY+dy` and compute:

```
rowWidth = span − (−dy)     when dy <= 0
           span − (dy & 1)  when dy > 0
```

Visit dx from −rowWidth through +rowWidth by 2. Skip when wrapping absolute dx
equals span. Compute wrapped mapX from centerX+dx; draw the complete tile only
if P01 view membership succeeds. Then draw city names around the supplied
center with radius level, and restore the **live** view innerClip on normal
return. Positive rows use parity, not magnitude. At level 1 with all candidates
admitted, this visits 27 tiles; a symmetric traversal would differ.

A negative/overflowed span follows the actual signed loop conditions; no
artificial minimum radius is inserted. Pixel clipping limits raster output,
not which admitted tile children perform state queries/message pumps.

#### Partial redraw controller

If session-active is zero, return without effects. Set map-redraw-busy=1;
capture unrestricted observer −1 or supplied observer; perform the neighborhood
traversal. If current mode is View (zero), blink-phase nonzero and blink-active
nonzero, and this is primary with the shared selection in view, project the
**live** selection, select live view scale, draw the map-selection cursor and
reset scale. This cursor overlay is after neighborhood names and clip restore.

If presentation requested, recompute P01's radius rectangle using current
geometry after drawing callbacks and present it. Finally clear busy.
The one-tile presentation wrapper uses level 0. Full redraw does not implicitly
invoke this partial-only cursor step.

All-view wrappers visit slots 0–7, always considering primary and testing live
Enabled on auxiliaries. One wrapper redraws level 0, another level 1, and a
general wrapper forwards supplied level/observer/presentation. Full redraw also
has an all-view wrapper. The two-coordinate helper completes the entire first
level-0/present pass using its captured local observer argument, then reloads
the observer for the second pass. No later view is skipped merely because an
earlier view failed to change pixels.

### Original §9. Overview rendering

P01 supplies overview dimensions, spans, origin/margins, projection and clicks.
Overview rendering below uses those live values and shared local visibility;
it does not call main-view terrain/city/unit sprite composition.

#### Tile color

Return UnknownBlack for invalid world coordinates. Query visibility to the
local observer; if false and unrestricted display is off, return UnknownBlack.
Look up a city. If present, capture its signed owner before later tests.
Show that city's direct-owner label color when unrestricted, when its signed
KnownTo mask includes the local observer, or when its reread signed owner
equals the observer's low unsigned byte. No remembered-size test is added.
The returned color still uses the earlier captured owner.

If that city color did not return, highlight the shared selected coordinate
only when blink-active is nonzero and
`(unsigned(mode)<1) == rawBlinkPhaseByte`. This is equality to 0 or 1, not
truth-value conversion of the phase. Otherwise return OceanColor for actual
Ocean, LandColor for everything else. Scenario reveal has no independent gate.
Nominal palette indices are in Appendix B.

#### Viewport outline

If either primary margin is nonzero, return. Project primary `(left,top)` and
`(right,bottom)` with P01 overview projection. If firstX<secondX use them as
left/right, otherwise swap only x endpoints. Form half-open bounds with right
and bottom increased by 1; draw outline in HighlightColor on the current target.
There is no cylindrical seam split or analogous y sorting. The selected target
is a caller input and can differ from the overview receiver after callbacks.

#### Full overview redraw

Capture the suppression byte; if nonzero, return that unsigned byte. If session
inactive, return 0. Prepare overview surface/chrome, select overview target,
calculate P01 layout, then fill the **current target** with palette index 0.
Capture pixelY as overview biasY+marginY.

For row=0 while row<live rows:

1. Capture mapY=`row+live originY`. Stagger is arithmetic half live tileWidth
   for odd mapY, otherwise zero. Set pixelX=biasX+marginX+stagger.
2. For column=0 while column<live columns, set
   `mapX=wrapX(2*column+live originX)`, make it even, then increment if mapY
   is odd. If world-invalid, continue without incrementing pixelX.
3. For a valid tile, compute §9 color; fill a rectangle at pixelX/pixelY using
   live tileWidth/tileHeight on the current target. Increment pixelX by the
   live tileWidth after filling.
4. After the row, increment pixelY by live tileHeight.

Invalid tiles therefore compress the drawn row's x positions. After all rows,
pump native messages, draw viewport outline, draw overview status/chrome,
present overview, then pump native messages and return that pump's result.
No explicit network-poll calls are inserted here beyond what native pumping
may deliver. Do not repair the origin/bounds quirks owned by P01.

#### Partial overview region

If overview suppression is nonzero, return. This helper has no session-active
gate. Initialize minX=minY=999 and maxX=maxY=−1; select overview target.
Visit dx from `−2*radius` through `2*radius` in steps of 2, recomputing the
wrapped expression as in F01. Capture wrapped mapX=centerX+dx; skip if its x
is out of world range. For each admitted x, visit dy through the same range
in steps of 1. Continue only when
`sar(wrap32(abs32(dx)+abs32(dy)),1)<=radius`; capture mapY=centerY+dy and
skip outside its y range.

If captured mapX is odd, decrement it **in the outer loop's local**, so later
dy visits retain that correction. If mapY is odd, increment **mapY**, not x.
Recheck world validity after those corrections. Project, compute color and
fill with live overview dimensions. These parity rules differ from overview
click/full redraw and can revisit a corrected tile more than once.

If presentation requested, accumulate min pixel x/y and max inclusive right/
bottom using dimensions read after drawing. After traversal, if presentation
requested and maxX>=0, draw the viewport outline, construct the bounding
rectangle with widths `max−min+1`, then present that rectangle. No native pump
or input discard is added. Radius-zero at an odd center y can draw the next
even row, while a last-row correction can invalidate it.

### Original §10. Nominal and off-nominal conformance

A conforming raster backend should expose draw calls or pixels plus observable
scale, clip, target, font, busy and callback traces. Use the same assets, palette,
font metrics, supplied retained state and callback schedule when comparing.
Do not treat a successful bounded source test as proof of every native state.

| Case | Expected distinction |
| --- | --- |
| Invalid coast center | Clears four descriptors, returns 0; invalid neighbors of a valid center instead count as land. |
| One NE land neighbor of Ocean | Top descriptor gets following contact; right gets preceding contact; landCount=1. |
| Ocean at zoom −4 versus −3 | Plain Ocean below threshold; four quarters and river mouths at −3. |
| Land at zoom −5 versus −4 | No neighbor blending at −5; live four-neighbor blends at −4. |
| Normal observer with actual Farmland, remembered none | No Farmland overlay; actual terrain/river still contribute. |
| Bare-terrain with pollution and grass shield | Both can remain; city/unit/name and selected improvement overlays are suppressed. |
| Railroad neighbor | Road pass suppresses its road segment, separate rail pass draws it. |
| Empty airbase versus actual hidden Air unit | Occupancy sprite changes despite unit visibility. |
| Unknown valid tile | Hidden sprite then scale reset; complete tile caller still pumps before later validity/overlay work. |
| All-Air stack on an airbase | Preferred representative can be −1, overriding active-unit preference. |
| Equal representative priorities | First stack member wins. |
| Nonzero high identity bits in last city slot | ShowCityRadius scan accepts the city; it is not a low-word identity test. |
| Scenario reveal with unknown foreign city | Body can appear while label/unit/overview rules remain independently gated. |
| Two temporary render overrides | Selected override takes precedence; each bypasses normal tile-overlay visibility. |
| Empty top result followed by owner read | Require retained backing or stop; no invented owner/default unit. |
| Health damage exceeds hit points | Preserve signed wrapped bar calculation and later rectangle semantics, or declare restrictive rejection. |
| Name/font changes during shadow draw | Foreground uses fresh width/name/owner with captured center/baseline. |
| Full redraw tile callback changes geometry | Current row's coordinates/count stay captured; subsequent row bound is live. |
| Level-1 partial redraw, all candidates in view | 27 tile calls in the asymmetric order, then names, then live clip restoration. |
| Full redraw with discard suppressed | Retain tile/overview residual result; no queued-input discard. |
| Native pump changes game mode | Present-with-poll independently evaluates each following network gate. |
| Overview city owner has remapped leader color | Overview uses direct-owner record; ordinary label/shield queries use leader mapping. |
| Overview blink phase is 2 | Does not equal either Boolean mode test; no highlight. |
| Overview invalid cell in a row | Does not advance pixelX. |
| Partial overview odd y | Increment y to even; do not apply full overview's x stagger rule. |

The current fixture joins 24 whole source bodies and passes 39 assertions in
optimized and sanitizer builds. It exercises actual terrain composition,
complete map-tile pump/poll order, full/partial traversal and controllers,
overview composition and shared P01 geometry. City, unit, label, raster/font,
window and map-query services are declared adapters; its sprite traces do not
establish actual native pixels or complete direct unit/city rendering.

Finite integration obligations remain: P03 actual asset/scale/palette pixels at
−7/−4/−3/0/8; P04 fonts and name/population widths; actual §3–§7 city/unit/label
children joined to redraw; P05/N02 reentrant native/network delivery; P10
publication/cleanup of both render overrides; and full/partial overview across
the cylindrical seam and restored F06 view state. Retain failure prefixes,
unknown-read inputs and excluded-write handling in those comparisons.

### Original drawing observations

A nominal call supplies a valid view/graphics target, initialized sprite/font
resources and a loaded map, with ordinary civilization/unit/city/rule indices.
Terrain drawing also accepts a negative observer for unrestricted terrain and
a city-radius index or negative ordinary-map selector. General unit/city drawing
can render valid preview records outside playable coordinates.

Observe draw order, sprite identity and coordinates, clipping, recoloring,
text/font/style changes, scale selection/reset, mutable graphics target,
visibility queries, message/poll calls, presentation and input-discard effects.
Changing a global drawing target or text style is not undone unless a step
explicitly does so. The sprite reset restores its default scale; it does not
restore an arbitrary previous scale selected by the caller.

### Original city-name composition

For each admitted name:

1. Project captured city coordinates; capture horizontal center as pixelX plus
   live stepX. Capture baseline as pixelY plus `sar(wrap32(3*tileH),2)`.
2. Select text style `(black,−1,−1,−1)`. Evaluate scale(2) at live view zoom;
   if greater than 2 use 2, otherwise call scale(2) again and use that result.
   Capture shadowY=`baseline+offset` before text measurement.
3. Read the city name, measure with the live font, then draw the shadow at
   `center−sar(width,1), shadowY`.
4. Repeat the scale-and-cap operation after the shadow draw. Reread city owner;
   resolve its leader-mapped label color. Select style
   `(labelColor,black,offset,0)`.
5. Reread the name and measure again with the live font; draw at
   `center−sar(newWidth,1), baseline`.

Name bytes, font and owner can change between passes. A missing terminator
requires supplied backed bytes or a stop, not a fabricated empty name. The
normal helper returns the final loop-test city bound. Its bare-terrain path
retains incoming result-register state; void callers need only its lack of
effects. Appendix B records when a caller supplies/consumes that residue.

### Original city overlay placement

Draw if that result is true, unrestricted display is enabled, or scenario
reveal is enabled. Subtract the view's live stepY from the supplied pixel y,
then invoke §5 with ordinary flags and the live view zoom. The caller's terrain
observer is not passed to the city renderer.

### Original unit overlay placement

Subtract live view stepY from pixel y before checking map validity. Return on
invalid coordinate; otherwise query top unit, including an empty result.

### Original §9. Overview rendering

Present the observer's permitted terrain/city/unit information and view position.
The overview must honor W03 knowledge and the distinct scenario/unrestricted
visibility gates, without revealing hidden unit state through colors or markers.
Its geometry, palette and redraw schedule may differ from the original.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

### Original unexplored-terrain raster gate

world-bounds query. Otherwise test W01 rectangular validity. Select the supplied
sprite scale. If observer is nonnegative or the tile is invalid, require a
valid tile and, for a valid tile, W03 visibility to that observer. On failure,
draw the unexplored tile sprite at `(px,py)`, reset scale and return.

### Original complete-tile projection/pumping

Project supplied map coordinate with P01. Draw terrain at the captured pixels,
using the supplied observer, live view zoom and ordinary radius selector −1.
Pump native messages even if terrain drew an unexplored/invalid tile. If the
now-live unsigned game mode is at least 3, poll network with `(1,0)`.

### Original radius sprite scale

If ShowCityRadius is now enabled, select the view's current sprite scale and
scan cities

### Original radius grid placement

query; first success ends the scan. Draw city-radius grid on success, ordinary
grid otherwise, at the captured tile pixels, then reset scale. This grid pass
