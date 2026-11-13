# P01: Map projection and interaction

This specification describes classic Civilization II Multiplayer Gold Edition
5.4.0f, patch 3 map-window geometry, coordinate conversion, centering, cursor
selection and map gestures. It covers the eight map views and the overview
window. A replacement can use different windowing and graphics representations,
while preserving world-coordinate identities and semantic interaction effects.
Original screen-coordinate formulas are reference presentation behavior.

[F01](F01-determinism-and-arithmetic.md) owns arithmetic conventions;
[W01](W01-map-geometry-and-spatial-state.md) owns world coordinates, bounds,
one-step horizontal wrapping, neighborhoods and rectangle membership.
[W03](W03-visibility-and-exploration.md) owns knowledge queries.
[U04](U04-unit-orders-and-activation.md) owns unit activation, View/Move-units
transitions and accepted Goto execution; [U05](U05-transport-and-special-movement.md)
owns paradrop execution; [U02](U02-movement-and-zones-of-control.md) owns
directional movement. [P02](P02-map-rendering-and-minimap.md) owns drawing and
redraw sequencing, [P03](P03-graphics-and-asset-processing.md) image, palette and
scaling services, [P04](P04-text-and-dialogs.md) fonts, captions and dialog
controls, and [P05](P05-input-and-command-dispatch.md) native window events, event translation, general key/menu
dispatch and deferred-event delivery. P01 owns the
map-click controller after event delivery, including its gates and continuation.
P05 must call that controller rather than reproduce its branches.

[F06](F06-save-load-and-file-formats.md) owns saved rectangles, centers and zoom;
[N03](N03-multiplayer-turn-sequencing.md) owns session timing and timer mechanics.
No operation specified here directly draws from either gameplay sampling law.
A child operation may do so under its own contract.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Required contract and reading guide

World-coordinate identity, topology, visible/selectable objects, active unit/city,
command eligibility/targets and accepted movement/Goto/paradrop effects; saved
view fields through F06.

The numbered main sections specify required behavior under
[X01](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy).
Original rendering, geometry, widgets, cosmetic media and native traces are in
[Appendix D](#appendix-d-original-presentation-reference); they are optional reference material.
Main-section numbers are retained for stable citations. Required calculations
that happen inside a presentation operation remain in the main text, including
state mutations, visibility/choice rules and file/protocol effects. Native event
traces do not replace semantic operation boundaries. Harmless UI failures and
rejected off-nominal inputs follow X01's declared safe policies.

## 1. Domain, state and arithmetic

### Intended inputs

Nominal inputs are a loaded world, valid logical view slots and identified user
commands. World dimensions/topology and doubled map coordinates satisfy W01.
The main view is slot0; auxiliary views are slots1–7. An iteration considers
slot0 even when Enabled is zero and an auxiliary when its live Enabled is nonzero.
A center or scroll request need not be a playable tile. Native coordinate spaces,
content biases and loaded hit masks describe the optional reference adapter.

Required view state is logical view identity, local/foreign follow preferences,
requested center, saved zoom and F06 view fields. Shared semantic state includes
observer, View/Move mode, active unit, selection, last accepted target, operation
busy/deferral state and active targeting mode. Screen rectangles, pixel caches,
font keys, masks, cursor art and native receivers belong to the reference adapter.

### Numeric behavior and off-nominal inputs

Use F01 reference results for intermediate sums, differences and products; signed division truncates toward zero. `sar(v,n)` is arithmetic right
shift, including negative values. `s16(v)` retains the low 16 bits and interprets
them as signed. All comparisons below are signed unless stated otherwise.
Map centers are W01 coordinates and nominal zoom is −7…8. Their original
storage annotations are in [Appendix E](#appendix-e-original-numeric-annotations); native pixel narrowing is in Appendix D.
The specified `s16` publications remain applicable to admitted extra values and
persistent state. Logical coordinates/zoom do not require 16-bit variables.

`wrapX` means W01's one correction, not arbitrary modulo. `clamp` means F01's
ordered lower/upper clamp, including its result when the bounds are reversed.
Do not introduce parity or world-bounds rejection into helpers that lack it.
Division by zero and signed quotient overflow are failure boundaries after
preceding writes. Negative/overflowed geometry does not acquire an invented
minimum size or zoom correction. A restrictive implementation may reject such
inputs before starting, provided it declares that compatibility limit.

Callbacks are observable boundaries. Captures explicitly identified below survive
callbacks; other fields are read when their step is reached. A fault/stop does
not run normal-return cleanup or undo earlier publication. Valid output aliases
follow the stated store order. Unknown receiver/table backing needs supplied
input or a stop; out-of-bounds writes are excluded bugs under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs).

## 2. Zoom and map geometry

The renderer chooses screen scale and projection. Preserve the selected world
tile, active unit/city and F06 view fields when a zoom operation changes local
presentation; W01 world geometry is unchanged.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 3. Projection, hit testing and clipping

Convert a gesture to an identified world tile or direction, then invoke §7. Reject
a gesture that has no target without issuing a game command. W01 owns
world-coordinate validity and direction offsets; screen masks, pixel rounding and
clipping are renderer choices.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 4. Centering, scrolling and following

Centering, scrolling and following change local view state. They must not issue
movement commands, change the shared selected object implicitly or reveal
concealed information. Retain distinct local/foreign follow preferences and F06
saved view values.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 5. Overview geometry and interaction

The overview conveys the same permitted world information and provides semantic
map selection/centering. Its screen geometry and click projection may differ; a
click must resolve to the world target used by the resulting action.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 6. Map cursors and the hold gesture

Provide semantic Goto, movement and paradrop targeting and cancellation. Gesture
duration, cursor artwork and timer implementation may differ. Once accepted,
dispatch the identified target through §7 and the U04/U05 owners.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 7. Selection and the map-click controller

### Change the shared selection

For requested x/y, first require W01 rectangular validity. On failure return 0
without effects. Otherwise clear blink phase, redraw the current selection in
primary view, store `selectionX=s16(x)` and `selectionY=s16(y)`, refresh main
status with argument 1, set blink phase 1, redraw selection in primary view
again, and return 1. Redraw/status children can observe the old/new selection
at the respective stages; later operations read live shared state.

A directional selection request reads W01's direction x offset, adds current
selectionX and applies wrapX; then reads the y offset and current selectionY.
Call the selection helper with these captured coordinates. If it returns
nonzero, scroll primary view using those same captured coordinates, even if a
child changed the selection meanwhile. Ordinary directions are W01's 0–7;
its stationary entry and bounded additional zero backing are covered in
Appendix B. An invalid endpoint does not wrap vertically.

### Entry and deferral

Delivered left click calls the controller with mode 0; right click uses mode 1.
Any nonzero mode follows the right-click branches below. If game mode is
nonzero and input-busy is already nonzero, return immediately.

Next query [N05](N05-transport-and-wire-protocol.md)
in-flush-send-buffer state. If true, or if any of the mutation
busy conditions in Appendix B is nonzero, log the blocked map-click message;
publish deferred kind Click, x, y, mode in order, then capture the live event
receiver into the deferred receiver and return. This is one shared deferred
record; a later event can replace it. The log callback occurs before publication.
The condition is a short-circuit query, including flush first. P05 owns later
delivery of that record.

Otherwise set input-busy 1. If a modal city/dialog is active, invoke its prompt,
clear input-busy and return. Capture the event receiver; capture whether its
cursor is Goto; capture the primary special-mode result. Then clear pending
hold/drag state; for mode 0 also reset special cursors. These happen **before**
hit testing, so even a rejected click can cancel the cursor gesture.

Convert the incoming point with the captured view. On conversion rejection,
clear input-busy and return. On conversion success set input-discard suppression
1. If the converted tile is invalid, clear suppression, clear input-busy and
return. Otherwise publish last-click x, then y. Unless a branch says otherwise,
all later mechanics read their live shared state.

### Accepted-coordinate branches

A normal-return early exit below clears input-discard suppression and then
input-busy. Child failures preserve preceding effects and bypass this cleanup.

1. **Captured special mode and nonzero click mode:** clear blink phase;
   redraw selection in the captured view; center that view on the converted
   coordinates; early exit. This does not execute paradrop.
2. **Current mode is View, or click mode is nonzero:** if the receiver's live
   index is 0, invoke U04's switch-to-View helper with argument 0, then change
   shared selection to the converted coordinates. An auxiliary click does not
   make this primary selection change. For a zero click mode, perform the
   city handling below; if a city was found, early exit regardless of whether
   anything was displayed. If no city, invoke U04's stack/unit activation
   controller. Continue to final centering of the captured view.
3. **Move-units/nonzero interaction mode and left click:** test captured Goto
   first. If true, invoke U04's accepted map-coordinate Goto operation, which
   captures active unit and, if nonnegative, writes destination/order and
   executes one step; early exit even if no unit existed.
4. Otherwise, if captured special mode, invoke U05 target paradrop with the
   **current** active unit and converted coordinates; early exit.
5. Otherwise, if the receiver's **live** cursor is one of the eight movement
   cursors, decode its direction and invoke U02 directional movement for the
   current active unit with source/interactive argument 3; early exit.
   Cursor changes during the preceding reset/conversion callbacks matter here.
6. Otherwise perform city handling; if any city exists, early exit. If no
   city, query the top unit at the coordinate. If nonnegative and locally
   owned, switch to View with argument 0, change shared selection, and invoke
   U04 activation. Whether or not a unit was found, clear blink phase and
   redraw the selection in all active views. Then continue to final centering.
7. Final centering calls center on the captured view with the converted x/y;
   then clear suppression and input-busy. This includes a valid empty-tile
   click and an ordinary right click. It is skipped by every early exit above.

City handling looks up a city at the converted tile. If none, continue as
specified. If one exists, open its city interface when its signed owner equals
the local observer, unrestricted/score-locked mode is enabled, or Investigated
is set. Otherwise require local Trade advance, then W03 visibility to the
local observer; only then show the informational city popup when its known
mask includes the local observer, its owner equals the observer's low unsigned
byte, or both scenario mode and whole-map reveal are enabled. P06 owns the
contents of those views. Merely finding an inaccessible city still consumes
the click; it does not fall through to unit activation or final centering.

### Double click

Capture whether game mode is nonzero and input-busy was nonzero at entry.
Unlike ordinary click, still query the same flush/mutation deferral predicate.
If blocked, log and publish deferred kind DoubleClick, incoming x/y and live
receiver, then return; no mode argument is published for this kind.

If not blocked, set input-busy only when this call did not inherit it. A modal
city/dialog invokes its prompt. Otherwise use **last accepted click coordinates**
for the terrain-inspection action, not a fresh screen position. The unused native
event-token read is reference behavior in Appendix D. If that tile is valid,
has no owner according to W01's combined owner query, and is visible to the
local observer, query terrain. For Grassland use whether its shield resource
exists as special index; for other terrain use W01 special-resource index.
Open P08 terrain help at `11*specialIndex+terrain`.

Clear input-busy only when this call owned it at entry; callback changes do
not alter that captured cleanup decision. It does not change last-click state,
center a view or run ordinary map-click conversion itself.

## 8. View lifecycle and screen placement

Keep logical view identity, follow/center preferences and saved F06 fields
distinct from native windows. Creation/release must establish and retire the
required U04/N03 input/turn services; decorative blink schedules need not match.
New windows do not change player/observer or active-unit identity.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 9. Conformance cases and integration limits

Validate world-target identity, View/Move selection and activation, owned/foreign
city visibility, Goto/paradrop/directional routing, busy deferral and cancellation.
Exercise changed/deleted targets and remote events at semantic boundaries. Test
F06 saved-view interchange and hotseat concealment independently of screen size,
zoom, gestures and cursor art. Original geometry cases are reference tests.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## Appendix A. Source and original instruction traceability

Primary reference: OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`. The inspected worktree was
`fc2155ccdb2123c1137babab6fa39e38152278c9`; selected reference bodies retain
that earlier pin. Independent evidence: `civ2` revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. Neither reference repository was
modified for this specification.

Original executable is the classic MGE patch-3 input with SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Nominal `TERRAIN1.GIF` SHA-256 is
`1562bfaf6b0a2982c00f7dc59dac7c5e990a0cca44d2d8f071982195b2fac248`.
These files are research inputs, not distributed requirements for reading this
specification or a claim to distribution rights over assets.

| Contract | Primary navigation and original entries |
| --- | --- |
| Main projection/layout | [mapview.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/mapview.cpp): init `00479ede`, layout `00479fbe`, inverse `0047a540`, forward `0047a6b0`, membership `0047c3e0`, radius clip `0047c7aa`, scale wrapper `0047dfb0`. |
| Scroll and mouse | Same file: center `00410402`, edge scroll `00410464`, all-view `004105f8`, direction `004106fd`, mouse move `00410835`, clear `00410a60`, expiry `00410b23`, down `00410bc3`, general release `00410cfa`. |
| Overview | Same file: layout `00406b4c`, projection `00406db8`, left click `004074dc`, right click `00407651`, default rectangle `0040785b`, creation `004079ab`. |
| View runtime | Same file: primary rectangle `004132b7`, close `00413350`, saved size/position `004133c2/0041341c`, title `00413476`, caption command `004135ab`, create `00413717`, session create/release `00413a90/00413bd1`. |
| Selection and command boundary | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp), original selection `00410d98`, click `00410f77`, double click `00411705`, selection step `00411a13`; independent `src/map_input.cpp` supplies the typed ordered bodies. |
| Asset producer | [graphics.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/graphics.cpp), first terrain-sheet loader and tile-cell helper `0044999c`; independent `src/terrain_sprites.cpp`, `src/gif_file_loader.cpp`, `src/gif_decode.cpp`. |
| World helpers and constants | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp), [gamedata.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/gamedata.cpp), [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h); W01 owns their behavioral contracts. |

The independent selected inventory contains 52 complete bodies from
`src/map_window.cpp`, `src/map_axis.cpp`, `src/overview_interaction.cpp`,
`src/map_input.cpp`, `src/menu_mode.cpp` and `src/window_startup.cpp`.
Headers, actual mask/pixel producers and retained test recipes supplement it.
Fifteen original instruction probes confirm pixel narrowing/division/table reads,
ordered output publication, sparse initialization, asymmetric height handling,
Shift testing and byte-only centering/scroll/selection flags. All 17 bounded
adjustment pairs and 4,096 nominal mask classifications match their pinned inputs.
No Ghidra annotation or reconstruction campaign was changed or resumed.

## Appendix B. Original representation and bounded exceptions

These details support reference snapshots; they do not prescribe cleanroom
storage. A native map record has stride `0x3f0`; event receiver is its presentation
subobject at `+0x48`. Logical fields correspond as follows:

| Logical data | Original record offsets |
| --- | --- |
| Bias/content | `+124,+128,+12c,+130`, signed DWORDs. |
| Inner clip | Rectangle at `+2bc`. |
| Index, Enabled, follow | DWORD `+2d8`; WORDs `+2dc,+2de`. |
| Center and zoom | Signed WORDs `+2e0,+2e2,+2e4`. |
| Left/top/right/bottom | DWORDs `+2e8..+2f4`. |
| Base/draw spans | DWORDs `+2f8,+2fc,+300,+304`. |
| Tile/step/quarter sizes | DWORDs `+308..+31c`. |
| Sprite/layout sizes | DWORDs `+320,+324,+328,+32c`. |
| Margins | DWORDs `+330,+334`. |
| Font size caches | DWORDs `+348,+34c`. |
| Cursor, cached zoom | DWORDs `+358,+35c`. |
| Picking/direction ports | Receivers at `+360,+3a8`. |

Cursor IDs are ordinary 513, Goto 510, valid/invalid paradrop 514/515, and
Unknown −1. Direction-mask result `d` maps to `500+((d+1)&7)`; accepted
movement cursor `c` decodes as `c−501`, replacing negative −1 by 7. Caption
commands 1–4 are Close, Zoom-in, Zoom-out, Follow. Follow permissions have
values local=1 and foreign=2. The flat-world bit is `0x8000`; movement pointer
preference is gameFlags low-byte `0x80`. A city has Investigated at `0x400000`;
Trade is advance 84. Scenario mode is mapFlags `0x80`, and whole-map reveal
is special-rules `0x0008`.

Shared blink-active/phase/skip low bytes are `0062804c/54/5c`. Centering's
save/clear/restore at `00410431..00410453`, edge-scroll stores at
`004105b2/004105c6/004105d2`, and selection phase stores at
`00410db6/00410de3` are byte accesses. They must not clear higher retained
bytes used by wider consumers elsewhere. Deferred/input flags and view cursor
IDs are separate full values, not this byte family.

The inverse converter indexes signed-byte adjustment backing at bases
`0062833b` and `00628343`. At the pinned initial image, picks −1 through 15
have these pairs (pick 0 is skipped by the algorithm):

| Pick | dx | dy |
| --- | --- | --- |
| −1 | 4 | 0 |
| 0 | 8 | 0 |
| 1 | 1 | −1 |
| 2 | 1 | 1 |
| 3 | −1 | 1 |
| 4 | −1 | −1 |
| 5 | 0 | 0 |
| 6 | 0 | 0 |
| 7 | 0 | 0 |
| 8 | 0 | 0 |
| 9 | −1 | 0 |
| 10 | 1 | 0 |
| 11 | 1 | 0 |
| 12 | −1 | 0 |
| 13 | 0 | 1 |
| 14 | 0 | 2 |
| 15 | 0 | 1 |

These span shared direction data and padding, not a separately clamped table.
If that canonical backing was changed, consume its current signed values.
Movement-selection direction offsets 0–8 are W01's neighbors plus stationary;
indices 9–15 read initial zero padding. Further indices have no declared input
backing in this specification: require supplied bytes or stop before selection
mutation. Do not silently manufacture additional stationary directions.

The map-click deferral predicate checks network manager flags in this order
after the transport flush query: unit-create, unit-delete, city-create, city-delete, map-lock, map-unlock,
stack-unlink, stack-link, stack-bottom, unit-relocate, city-acquire-wonder,
civilization-eliminate, civilization-create, civilization-check-destroyed,
gather-cargo, lift-off-map, delete-cargo and destroy-with-redraw. Their original
slots occupy `+7b4..+7fc`, skipping input-busy `+7cc`. N02/P05 own their
producer/delivery joins. Input-busy is `+7cc`, map-redraw-busy `+800`.

Invalid window/table accesses are not all interchangeable. A read of backed
neighbor/padding bytes above can be represented as a declared input. A write
through a view index outside 0–7, an invalid active-unit destination store,
an invalid output pointer, or storage overflow during native resource setup
is an excluded bug. Stop, omit the unsafe operation or reject the input under
a declared safe profile; keep preceding valid timer/lock/publication effects.
Do not turn a write into an assignment to an adjacent view or global merely
because the original address happens to be mapped. Null receivers remain
explicit failure boundaries after their documented entry effects.

## Appendix C. Validation record

The historical research bundle included source inventories, whole extracted
bodies, a bounded C++ harness, source/binary probes and Markdown checks. That
scaffolding is not included in this repository or required for implementation.
The recorded source pins identify the consulted revisions; links above provide
supplementary evidence.

The Linux harness compiles 28 unchanged complete independent-reconstruction
bodies. Its fixed-layout view adapter keeps the geometry offsets consumed by
those bodies. Graphics ports/fonts, native presentation, timers, observer
knowledge, W01 wrap/clamp/bounds and redraw are explicit fixture services.
It uses real layout→projection and button-down→timer→release→clear joins;
mask colors and platform callback results are supplied. It does not execute
the original binary, DLL, raster scaler, native timer implementation or GUI.

Optimized and ASan/UBSan runs pass 46 assertions, covering signed product wrap,
ordinary/animation rounding, sparse restore state, span/height asymmetry,
resource cache/order, forward/inverse conversion, valid aliases, clipping,
direction masks, knowledge gate, centering restoration, scroll aggregation,
overview conversion, hold/Shift/release sequencing, cursor reset and the
zero-dimension divide boundary. Invalid-pointer/corrupting-write paths are not
executed; checked safe stops are not original memory-corruption conformance.

Retained independent recipes `tests/map_projection.py`, `tests/map_input_cleanup.py`
and `tests/overview_initialize.py` describe broader original/native comparisons
with their own controlled inputs and limits. They were read as evidence and
were not rerun here; their native coverage is not attributed to this harness.
The current original-byte probes and asset classification checks establish
specific constants/instruction facts only. The outstanding real-child/native
joins are listed in §9; the finite checks do not establish whole-game conformance.

## Appendix D. Original presentation reference

This appendix retains the original presentation procedures removed from the main
specification. Geometry, pixel/sample output, native controls and cosmetic timing
are optional reference behavior under X01. Required semantic fragments were kept
in the numbered main sections and are linked where omitted here; this appendix
does not relax their simulation, information, file or protocol obligations.
Original section numbers and local § references identify the recovered procedure
family, whether its retained fragment is in the main section or here. Historical
native/pixel tests are reference validation, not baseline acceptance criteria.

### Original §2. Zoom and map geometry

#### Scaling and accepted zoom operations

For an integer size `v` and level `z`:

```
scale(v,z) = trunc(wrap32(v * wrap32(z + 8)) / 8)
```

A view passes its signed stored zoom to this helper. Level 0 produces 64×32
terrain rectangles, −7 produces 8×4, and 8 produces 128×64. A separate animation
scaler adds 4 to the wrapped product before dividing by 8 toward zero. For
example, at −7, ordinary `scale(4)` is 0 and animation scaling gives 1. P03/P10
must distinguish these operations.

Accepted map zoom actions have these effects; P05 owns which keys/menu items
invoke them:

| Action | Ordered effects |
| --- | --- |
| Increase current view | If its signed zoom is below 8, increment with `s16`, then redraw that view for the live observer, presenting the result. At the cap, no redraw. |
| Decrease current view | If zoom is above −7, decrement with `s16`, then redraw/present. At the cap, no redraw. |
| Primary overview scale | Store −3 in primary zoom; redraw/present primary. |
| Primary standard scale | Store 0, then copy shared selection x and y to primary center; redraw/present. |
| Primary minimum/maximum | Store −7 or 8; redraw/present primary. |

Increment/decrement are one-sided checks, not general clamping: a retained zoom
below −7 can still increment, and one above 8 can still decrement. Primary
preset operations overwrite a retained off-nominal zoom directly. No zoom
operation here changes another view's zoom.

#### Geometry pass

For one view, perform these assignments in order:

1. Copy content width, then content height to layout width and height.
2. Set `tileW=scale(64,zoom)`; set `tileH=scale(32,zoom)` using the live zoom.
   Set `stepX=tileW/2`, `stepY=tileH/2`, `quarterW=tileW/4`,
   `quarterH=tileH/4`, `spriteW=tileW`, `spriteH=tileH+stepY`.
3. Compute each span separately, with wrapping before division:

   ```
   baseColumns = (layoutWidth  + tileW - 1) / tileW
   baseRows    = (layoutHeight + tileH - 1) / tileH
   drawColumns = (3*stepX + layoutWidth  - 1) / tileW
   drawRows    = (3*stepY + layoutHeight - 1) / tileH
   marginX = 0
   marginY = 0
   ```

4. If `worldWidth/2 < baseColumns`, assign `baseColumns=worldWidth/2`,
   `marginX=sar(layoutWidth-tileW*baseColumns,1)`, then
   `drawColumns=baseColumns+1`.
5. If `worldHeight < baseRows`, assign `baseRows=worldHeight/2`,
   `marginY=sar(layoutHeight-tileH*baseRows,1)`, then
   `drawRows=worldHeight+1`. Preserve this asymmetric threshold and result;
   it is not the horizontal formula transposed.
6. Store `centerX=s16(wrapX(centerX))`. Set `top=centerY-baseRows`, then
   `left=wrapX(centerX-baseColumns)`.
7. On a flat world, if `worldWidth+1 < 2*baseColumns+left`, replace left with
   `worldWidth+1-2*baseColumns`. Independently, if
   `worldHeight+1 < 2*baseRows+top`, replace top with
   `worldHeight+1-2*baseRows`.
8. Replace each negative left/top with −1, storing left then top. If left is
   odd, decrement left. If top is odd, increment left. Do not adjust top's
   parity, and do not clamp again after these operations.
9. Set `right=wrapX(2*drawColumns-2+left)`, then
   `bottom=2*drawRows-2+top`.

If zoom differs from cachedZoom, allocate/replace pickMask at the current
`tileW,tileH`, then directionMask using dimensions reread after the first
allocation. Allocation results are ignored by this parent; the graphics service
owns failure effects. Publish cachedZoom from the live zoom after both calls,
select that sprite scale, draw the tile-picking sprite into pickMask at `(0,0)`,
then the direction sprite into directionMask at `(0,0)`, and reset sprite scale
on normal return. The two drawing-result rectangles are not consumed here.
A failure after publishing cachedZoom does not automatically invalidate it.

Then compute `large=wrap32(3*tileH)/5`. If it differs from largeFontSize,
replace the first font with face/category 0 and this size, then store the
captured size. Reread tileH; compute `small=tileH/4`. If changed, replace the
second font with category 1 and store that captured size. P04 owns font creation
and P03 sprite sampling; this parent owns refresh order and keys.

At 640×320, zoom 0, world 80×50 and center `(40,24)`, the spans are
`(10,10,11,11)`, origin `(30,14)`, last drawing coordinate `(50,34)`, and
margins zero. At content 300×400, world 8×10 and zoom 0, the oversized-world
branches instead give `(4,5,5,11)` and margins `(22,120)`.

### Original §3. Projection, hit testing and clipping

#### Map to pixel

Capture `dx=wrapX(x-left)` and `dy=y-top` before output writes. Then:

```
outX = stepX*(dx-1) + marginX
outY = stepY*(dy-1) + marginY
outX = outX + biasX
outY = outY + biasY
```

Read each step, margin, bias and output at the indicated assignment. Aliasing
outputs or receiver fields can affect later reads; do not replace these four
stores with a simultaneous pair. Projection does not test map bounds, parity,
knowledge or whether the result lies inside the view. Its point is the upper
left of a tile's bounding rectangle, not the diamond center.

For the 640×320 example above with bias `(8,20)`, tile `(40,24)` projects to
`(296,164)`. With both outputs sharing one otherwise separate variable, the
same call finishes with 172: Y's intermediate 144 replaces X, then the X bias
and Y bias are added in order.

#### Pixel to map

Start by narrowing incoming pixel x/y with `s16`. Replace x by
`s16(x-biasX)` and y by `s16(y-biasY)`. Reject with result 1 if x or y is
negative, x is at least contentWidth, or y is at least contentHeight.
Both output locations remain untouched on this rejection.

Otherwise:

1. Subtract margins, narrowing each result with `s16` again. There is no
   second bounds check after removing margins.
2. Capture `mapX=2*(x/tileW)+left+1`, then
   `mapY=2*(y/tileH)+top+1`.
3. Compute signed x and y remainders using separately reread tile dimensions;
   narrow each remainder with `s16`.
4. Read pickMask at those remainder coordinates. The P03 pixel-read service
   returns the stored unsigned color index when within its clipping rectangle;
   an outside read emits its coordinate warning and returns 0 without retry.
5. Let `pick=sar(wrap32(color-10),4)`. If pick is nonzero, add the signed
   x adjustment, then the signed y adjustment from the picking tables below.
6. Store `outX=s16(wrapX(mapX))`, then `outY=s16(mapY)`, and return 0.

Conversion success does not imply a valid world tile. Callers that require one
must perform W01's rectangular bounds query afterward; that query itself does
not enforce parity. Output aliasing makes the Y store win. A rejected native
client point does not sample the mask or partially publish an output.

The normal pick table is:

| Pick | Adjustment | Meaning within the rectangular cell |
| --- | --- | --- |
| 0 | `(0,0)` | Central diamond. |
| 1 | `(+1,−1)` | Upper right. |
| 2 | `(+1,+1)` | Lower right. |
| 3 | `(−1,+1)` | Lower left. |
| 4 | `(−1,−1)` | Upper left. |

A color outside the nominal groups is not clamped. Unsigned color indices give
pick −1 through 15. The original backing for those additional reads is recorded
in Appendix B. This is a bounded read issue, not permission to emulate a memory
overwrite. In particular, an accepted content point inside a centering margin
can produce a negative remainder, a warning, color 0 and pick −1.

With the previous view and bias, incoming `(328,180)` selects the base cell
`(41,25)` when its sampled pick is zero. A pick of 1 changes it to `(42,24)`;
a pick of 4 changes it to `(40,24)`.

#### Direction at the active unit

Project the supplied unit coordinates with the forward transform. Incoming
mouse coordinates are signed low 16-bit values. Return 8 if the point is left
or above the projected rectangle, or at/beyond its right/bottom edges using
live tileW/tileH. Capture the offsets from that projected origin.

Separately subtract biasX/biasY from the incoming point, narrowing again with
`s16`. Return 8 if it is outside the half-open content rectangle. Otherwise
sample directionMask at the captured tile-relative offsets and return
`sar(wrap32(color-10),4)` without a range clamp. This result need not be 0–8
for off-nominal mask data. The mouse controller accepts only 0–7 as directions.

#### Input masks

The masks are algorithm inputs from the terrain asset, not geometric estimates.
The terrain loader takes 64×32 cells at `(66,447)` for picking and `(261,447)`
for directions from the first terrain sheet. Coordinates are zero-based pixel
origins. P03 owns BMP/GIF selection, palette offset, transparency and scaled
sprite rasterization; P01 consumes their rendered color indices.

For the pinned nominal GIF, decoding adds 10 to each source color index.
Thus the classification above is simply the source index divided by 16, but
custom assets and clipped reads still use the rendered-index formula. The
following compact descriptions reproduce the nominal classification images
at zoom 0 without needing the original file:

- Pick image: for rows `r=0..15`, corner widths are
  `[31,29,27,25,22,20,18,16,14,12,10,7,5,3,1,0]`.
  Each row consists of that many 4s, then `64−2*width` zeros, then that many
  1s. Row `31−r` uses the same widths with 3 on the left and 2 on the right.
- Direction image rows 0–6 are 18 copies of 6, 28 of 7, 18 of 0. Row 7 is
  16 of 6, 32 of 8, 16 of 0. Row 8 is 14 of 6, 36 of 8, 14 of 0.
  Rows 9–15 are 14 of 5, 36 of 8, 14 of 1. For row `31−r`, reflect the
  upper row's classifications vertically with mapping
  `0→2,1→1,2→0,3→7,4→6,5→5,6→4,7→3,8→8`.

These are classification data, not a replacement rule for P03's pixel scaling.
A cleanroom implementation may scale categorical masks equivalently only after
establishing the same sample selection and untouched/transparent-pixel effects.
The actual nominal masks contain no transparent cells; modified assets may.

#### Membership and redraw rectangles

A view's membership query calls W01's rectangle-membership algorithm with
`left,top`, width `baseColumns+drawColumns` and height
`baseRows+drawRows+1`. It does not project the tile or test knowledge. The
all-view visibility query first requires W03 visibility to the live local
observer; then it scans active views 0–7 and returns 1 at the first membership
success, otherwise 0. These spans differ from the inclusive last coordinates
used for drawing and the thresholds used for scrolling.

For a tile and radius/level `r`, the redraw rectangle is constructed as follows:
project to `(px,py)`; subtract `(2*r+1)*stepY` from py; capture height
`(4*r+3)*stepY`; capture `horizontal=stepX*r`; subtract
`horizontal+horizontal` from px; capture live tileW and width
`(2*r+1)*tileW`. Form the half-open rectangle `(px,py,px+width,py+height)`,
then intersect it with the view's live innerClip after rectangle construction.
A nonempty intersection returns true; an empty one produces the platform
empty rectangle and false. P02 owns the subsequent draw/blit and whether the
caller consumes this Boolean. This helper adds no tile-validity or radius gate.

### Original §4. Centering, scrolling and following

#### Center on a coordinate

Store `centerX=s16(wrapX(x))`, then `centerY=s16(y)`. Capture the current
blink-active low-byte value, clear that low byte, redraw this view for the live
local observer with presentation enabled, then restore the captured low byte.
No y clamp, tile-validity or parity check precedes the redraw. P02's geometry
pass may adjust the resulting origin independently of the requested center.
High retained bytes of the shared blink backing are unaffected by these byte
operations. Normal-return restoration overwrites a callback's low-byte change;
a stopped redraw does not restore it.

The x-only wrapper captures the current signed centerY and calls center on
`s16(requestedX), capturedY`. The y-only wrapper passes current signed centerX
and `s16(requestedY)`. A separate primary-view helper redraws only when either
stored signed center differs from the full requested coordinate, comparing
before any narrowing/wrapping.

#### Scroll if near an edge

Capture left and top; initialize result 0. On a cylindrical world, if
`x<=2*baseColumns` and `worldWidth−2*baseColumns<=capturedLeft`, add worldWidth
to local x. Capture `rightThreshold=2*drawColumns+capturedLeft−2` and bottom.

If marginX is zero, clamp x to `[2,worldWidth−3]` only on a flat world.
If `x<=capturedLeft+1` or `rightThreshold−1<=x`, set result 1.
If marginY is zero, clamp y to `[2,worldHeight−3]`; set result 1 when
`y<=capturedTop+1` or `capturedBottom−1<=y`.

If result is 1, set skip-next-blink to 1, set blink phase to whether the live
mode is View (`mode==0`), then center on the **adjusted** local x/y. Return
result. Axis margins suppress the corresponding check and its clamp, but a
scroll triggered by the other axis still passes both local coordinates to
centering. Result means a center/redraw call occurred, not that the requested
tile was formerly outside the visible rectangle.

#### All views

Initialize aggregate 1 and set map-redraw-busy to 1. Capture the follow filter:
both permissions for a negative supplied civilization; local permission when
it equals the local observer; foreign permission otherwise. Scan slots 0–7.
For each active view, if its live follow permissions intersect that filter,
call its scroll helper and bitwise-AND the result into aggregate. Otherwise
set aggregate to 0. Do not short-circuit later views when aggregate is zero.
Clear map-redraw-busy on normal completion and return aggregate.

Consequently the result is 1 only when **every considered view** follows and
scrolls, not when any view scrolls. Closed auxiliaries are ignored. Callback
changes to later Enabled/follow fields are observed; the filter remains the
one captured before the scan.

### Original §5. Overview geometry and interaction

P01 owns overview layout and coordinate interaction; P02 owns its color selection,
painting and viewport outline. The overview uses rectangular tile marks rather
than the main view's diamond masks.

#### Layout

Capture content width/height. Let `sx=width/worldWidth`, replacing it by 1
when at most 1; similarly let `sy=height/worldHeight`, replacing it by 1 when
at most 1. Set `tileWidth=2*min(sx,sy)` and `tileHeight=min(sx,sy)`.

Compute `columns=(width−tileWidth/2)/tileWidth` and
`rows=height/tileHeight`; cap them by `worldWidth/2` and worldHeight
respectively. Set horizontal slack to
`clamp(width−columns*tileWidth,0,999)`, vertical slack similarly using height,
rows and tileHeight; store margins as arithmetic halves of those slacks.

If rows cover worldHeight, originY is 0. Otherwise use
`primary.centerY−sar(rows,1)`, clamped to
`[0,primary.layoutHeight−rows]`. This upper bound deliberately uses the main
view's **pixel layout height**, not worldHeight. Set
`originX=wrapX(primary.centerX−columns)`. On flat maps, set it to 0 when
`worldWidth<=columns`; otherwise clamp it to
`[0,primary.layoutWidth−columns]`, again using primary pixel layout size.
If originX is odd decrement it; if originY is odd increment originX.

These formulas permit off-map parts of the overview; P02's tile-color query
handles them. Do not repair these bounds to fit a conventional minimap camera.
With content 160×100, world 80×50 and primary center `(40,24)`, the result is
4×2 pixel marks, 39 columns, 50 rows, margins `(2,0)` and origin `(0,0)`.

#### Forward transform

Capture `dx=x−originX`; on cylindrical maps only, add worldWidth once if dx
is negative. Arithmetic-shift dx right by one. Capture `dy=y−originY`.
Write Y as `dy*tileHeight+marginY`, then X as `dx*tileWidth+marginX`.
If incoming y is odd, add `sar(live tileWidth,1)` to the current X output.
Add live biasX to X, then live biasY to Y. These ordered writes preserve valid
output/geometry aliases. Unlike main projection, a positive dx beyond the
world width is not reduced.

#### Click location

Subtract the overview's full biasX/biasY from incoming pixels without narrowing
to 16 bits. Reject outside its half-open content rectangle. Only after that
check, if a modal city/dialog is active, invoke its prompt/focus service and
return without a map action.

Set y to `originY+(pixelY−marginY)/tileHeight`, clamped to
`[originY,originY+rows−1]`. Let stagger be `sar(tileWidth,1)` for odd y and
0 otherwise. Set x to
`2*((pixelX−marginX−stagger)/tileWidth)+originX`, clamped to
`[originX,2*columns−2+originX]`. Apply wrapX; if x is odd decrement it;
if y is odd increment x. Require W01 rectangular validity afterward.
Margins therefore select the nearest clamped displayed cell; they are not an
independent reject zone.

On a successful left click, center primary view on the selected coordinates.
On a successful right click, scan auxiliary slots 1–7; initialize the first
whose Enabled is zero, then center that same slot on the captured coordinates
and stop scanning. If all auxiliaries are enabled, do nothing. Creation may
change live state, but does not replace the captured target coordinate.

### Original §6. Map cursors and the hold gesture

The special/paradrop-mode query looks **only at primary view's cursor**, returning
true for either valid or invalid paradrop. It does not scan other windows.
Setting paradrop cursors scans active slots 0–7, publishes valid paradrop in
each and calls its native cursor-load/apply service. Resetting them first tests
the primary special-mode query once, then, if true, scans the same active slots,
publishes the ordinary pointer and applies it. Later participation is live after
callbacks; the initial reset decision is not retested.

#### Mouse movement

Capture the native event's view receiver. If a modal city/dialog is active,
return before using it. If this view is in Goto cursor mode, return.
Otherwise choose the following candidate:

- If the primary special-mode query succeeds, start with valid paradrop;
  capture active unit, convert mouse coordinates using this view, and return
  without changing the cursor if conversion or world bounds fails. Change the
  candidate to invalid paradrop if the W01 distance from that unit exceeds the
  unsigned cosmic paradrop range; otherwise if the target is Ocean; otherwise
  if its unit owner is nonnegative and differs from the actor's signed owner.
  These checks short-circuit in that order. They are cursor hints; U05 still
  owns accepted target validation.
- Otherwise start with ordinary pointer. If movement-pointer preference is
  enabled and mode is exactly Move-units, capture active unit. When it is
  nonnegative, run direction-at-unit using its coordinates. For a direction
  0–7, choose that direction cursor; for every other result keep ordinary.

If the chosen candidate differs from the receiver's live cursor, publish it
before native load/apply. A callback cannot change which receiver this event
uses. No map-click coordinates or selection are published by mouse movement.

#### Clear pending gesture

If holdTimer is nonnegative, destroy that timer, then store −1 in holdTimer.
Reread holdView. If it is nonnegative and that view's cursor is Goto, publish
ordinary pointer in the live selected view, apply its cursor, then release
capture on the **live selected view after that callback**. Finally set holdView
to −1. A cursor-load/apply callback can change the selected index used for
release. Negative timer values mean no timer destruction; negative view values
mean no cursor/capture operation.

#### Left button down, timer expiry and release

Button down returns immediately if a modal city/dialog is active, or if the
game mode is nonzero and input-busy is already nonzero. Otherwise set input-busy
1, capture the native event receiver, and clear the pending gesture above.
If mode is exactly Move-units and the primary cursor is not special:

1. Copy the captured receiver's live index to holdView.
2. Query Shift. Any nonzero high byte of its native 16-bit key state qualifies;
   this is broader than testing only the high/sign bit.
3. If held, publish Goto in the live holdView, apply its cursor, then capture
   the mouse for the live holdView after that callback.
4. Otherwise create a 400-ms timer with callback count/parameter 1 and store
   its returned token in holdTimer, including a negative failure token.

Clear input-busy on normal return. No starting pixel is recorded, and this
operation does not pan the map or continuously issue movement orders.

The timer callback returns if holdTimer is negative. Otherwise destroy the
current timer, store −1, and, if the live holdView is nonnegative, publish Goto,
apply it and capture the mouse, reloading the selected view after callbacks.
It does not repeat button-down's modal, mode or input-busy gates.

The general button-release handler has button-down's entry gates. It sets
input-busy, captures the event receiver, and clears the pending gesture only
if holdTimer is still nonnegative. Then it releases capture for the captured
event receiver and clears input-busy on normal return. After expiry, therefore,
release alone leaves Goto mode armed for the ensuing map-click controller.
P05/P04 own native delivery order of release and click events; keep their
callbacks distinct. Moving the pointer while held does not itself scroll.

### Original §8. View lifecycle and screen placement

P04 owns native creation, control layout, window resources and destruction.
The logical preparation and placement policies here feed those services.
F06 restores saved values before these runtime steps; this is not a second
save-file decoder.

#### Sparse initialization and creation

Sparse initialization writes the slot index, sets follow to local-only for
slot 0 and neither for auxiliaries, sets cursor to retained Unknown, and, when
the restore flag is zero, sets zoom 0, centerX to the signed arithmetic half
of worldWidth and centerY to the signed arithmetic half of worldHeight.
Narrow each center; make centerX even, then increment it if centerY is odd.
Finally set cachedZoom to −999, forcing the next ordinary layout refresh.
It does not clear Enabled, geometry, font caches or every other view field.

Runtime creation repeats sparse initialization for its current index. If
restore is zero or the saved-window marker is zero, set zoom 0, replacing it
by 2 when screen width is at least 1000; for primary calculate its default
rectangle below. Set follow to both for primary, foreign-only for auxiliary.

For primary or an already-enabled auxiliary, read its saved rectangle:
use left/top and subtract current native frame width/height from its extent.
For a disabled auxiliary, choose x and y as `40*index`, content size 500×300,
and store a saved rectangle including live frame width/height. Publish Enabled
1 **before** resolving title label and creating the native surface. Pass the
captured x/y/width/height to P04 even if creation callbacks modify saved state.
Set minimum native width/height 256×128. Install redraw, mouse, key, character,
close, move and resize callbacks, compose title, and install Close only for
auxiliaries, Zoom-in and Zoom-out for all, and Follow only for auxiliaries.
Publish the map icon command handler, update the window and show it unless
live game mode is 1. An auxiliary then recomposes and updates the primary title.

Session creation scans slots 0–7: sparse-initialize each; when restore is zero,
set primary Enabled 1 and auxiliaries 0; runtime-create primary and each live
enabled auxiliary. After the scan enable primary turn-status display; if the
turn-time limit is nonzero, enable countdown display without otherwise clearing
its retained flag. Select the alternate timer mechanism, create the 150-ms unit
blink and 500-ms end-turn blink timers, each with repeat argument −1, then
deselect that mechanism. Timer delivery and callback effects belong to N03/P05/U04.

Session release selects that same timer mechanism, destroys unit-blink then
end-turn-blink timers using live tokens, deselects it, then destroys graphics
window storage for active slots 0–7. It does not clear the tokens or Enabled
flags. Later gates remain live after earlier destruction callbacks.

#### Default rectangles

Primary placement first queries main-frame client width and subtracts the
saved overview rectangle's width. Query main-frame client height separately
and add 1. Start top at 0; in wide/layout mode use `top=inset+1` and subtract
`live inset+1` from height. Store the primary rectangle with x=0 and this
width/height. The overview rectangle and inset may have changed during the
client-size callbacks.

Overview placement captures main client width; begin with content 160×100.
When `mainNonclientWidth+capturedClientWidth>1000`, use width 240 and, if wide
mode is enabled, height 150. Raise height to worldHeight if larger. Add twice
live spacing to width; add separately reread spacing plus status-row height
to height. Compute x as clientWidth minus width and native frameWidth; add
frameWidth/frameHeight to the rectangle extent. Start y=0; in live wide mode
add live inset+1. Store the saved overview rectangle.

Overview creation clears its redraw-suppression flag. Reuse its saved rectangle
only when restore and saved-window marker are both nonzero; otherwise calculate
it. Subtract frame dimensions for native content size, create using label 6,
set minimum size 40×25, install redraw/overview-click and shared key/character
callbacks, load its pointer/main icon, and install focus/close/move/resize
callbacks. Update, then show unless the live game mode is 1.

#### Closing, resize and titles

Ordinary close on primary minimizes it and returns 0 to refuse normal close.
Close on an auxiliary clears its Enabled and returns 1; the native close
continuation owns destruction. The Close caption command is distinct: after
its input-busy gate it clears Enabled, destroys this view's graphics storage,
recomposes primary title and updates primary. It does not call the normal-close
callback instead. Move/resize notifications copy the cached event receiver's
parent-relative rectangle into that slot's saved rectangle. P04 owns geometry
retrieval and its failure behavior; don't synthesize a successful new rectangle.

Closing overview sets redraw suppression before minimizing and returns 0.
Minimize also sets suppression. Focus first raises the city window if visible,
then clears overview suppression and redraws it. Its move/resize notifications
update the saved overview rectangle.

A Follow caption command cycles `follow=(follow+1) mod 4`, composes this view's
title, then redraws/presents it. Caption commands first reject a nonzero game
mode with input-busy already set; otherwise set busy, capture event receiver,
execute the command and clear busy on normal return. Unknown commands still
perform the busy publication/clear but need not dereference the receiver.

Title composition captures the two follow permissions as category 0–3, then
clears the shared text buffer. For primary only, unless title-language mode is
1, append local civilization adjective, append literal `e` if the mode reread
after adjective composition is 2, then append space. Primary needs a follow
suffix only if some auxiliary is enabled; auxiliaries always need it. Append
label 4; when needed append colon and label `300+capturedCategory`. Publish
that title through P04's bounded caption/status redraw service. The category
survives text callbacks, while language mode and auxiliary participation are
read at their stated steps.

### Original view and native state inventory

Each view retains:

| Logical field | Meaning |
| --- | --- |
| `index`, `enabled` | Slot identity and auxiliary participation. |
| `follow` | Two independent permissions: follow local and follow foreign movement. Neither, either or both may be enabled. |
| `centerX,centerY`, `zoom` | Requested center and scale level; ordinary zoom is −7 through 8. |
| `biasX,biasY`, `contentWidth,contentHeight` | Native content location and size. |
| `layoutWidth,layoutHeight` | Content size copied by the last geometry pass. |
| `tileW,tileH`, `stepX,stepY`, `quarterW,quarterH` | Tile rectangle, half and quarter dimensions. |
| `baseColumns,baseRows`, `drawColumns,drawRows` | Four distinct view spans computed below. |
| `left,top,right,bottom` | Derived doubled-coordinate origin and last drawing coordinates. |
| `marginX,marginY` | Centering slack; a nonzero value also suppresses edge scrolling along that axis. |
| `spriteW,spriteH` | Derived dimensions for map drawing. |
| `cachedZoom`, `largeFontSize,smallFontSize` | Resource refresh keys. |
| `pickMask,directionMask` | Rasterized, scaled input-classification images. |
| `cursor` | Ordinary pointer, Goto, valid/invalid paradrop, a movement direction, or retained other value. |
| `innerClip` | Current native content clipping rectangle, distinct from world bounds. |

Shared state comprises local observer, View/Move-units mode, active unit, map
selection, last accepted click coordinates, blink-active/phase/skip flags,
input-busy and input-discard suppression, map-redraw-busy, pending hold timer,
hold view, current native event receiver, saved rectangles, restore flag and
saved-window marker. The overview has its own content geometry, margins,
origin, tile dimensions and spans. Selection is shared; view centers are not.

### Original §9. Conformance cases and integration limits

Cases start with independent writable outputs and initialized view/resources
unless an alias or boundary is explicitly stated. No P01 random draws occur.
The following are observations a replacement should expose:

| Case | Required result/effects |
| --- | --- |
| Zoom 0, content 640×320, world 80×50, center `(40,24)` | Geometry and transform values in §§2–3; no global center change. |
| Same zoom and unchanged fonts on a second layout | Geometry recomputed; no mask allocation/draw or font replacement. |
| Asset/allocation callback changes zoom | cachedZoom captures the value after both allocations; subsequent resource steps observe live state. |
| Content point inside a margin | Initial client check may succeed; negative signed remainder warns/returns color 0; caller still tests converted world bounds. |
| Pixel outside content | Converter returns 1 and preserves both outputs. |
| Aliased pixel-to-map outputs | Y wins; forward converter instead performs its four ordered stores. |
| World 8×10, content 300×400 | Distinct oversized-height rule and margins `(22,120)`; no automatic common-axis rewrite. |
| Zoom −8 | Zero tile dimensions reach division failure after copied size and preceding scalar writes; no invented zoom clamp. |
| Center `(81,100000)` in cylindrical width 80 | Stored `(1,−31072)` before redraw; saved blink low byte restored only after normal return. |
| One active auxiliary doesn't follow | All-view result 0 even when primary scrolls; later views still execute. |
| Overview 160×100 over 80×50 | 39 columns, not 40; odd-row offset and primary-pixel-size clamp bounds preserved. |
| Auxiliary right-click creation | First disabled slot wins; later disabled slots remain untouched. |
| Hold for 400 ms, then general release | Goto cursor remains armed; release only ends capture after expiry. |
| Release before expiry | Timer destroyed and pending hold cleared; no Goto command created. |
| Shift high byte is 1 but sign bit clear | Immediate Goto/capture still selected. |
| Secondary paradrop cursor, primary ordinary | Shared special query false; reset helper leaves secondary unchanged. |
| Outside-client click with armed special/Goto cursor | Pre-conversion cleanup occurs; no last-click or tile-selection publication. |
| Accepted right click in primary | Selection/View transition as applicable, then center; no left-click city open. |
| Accepted left click on inaccessible city | Last-click coordinates published; city consumes click; no fallback activation/centering. |
| Double click after another accepted click | Uses retained last-click coordinates, not current event pixels. |
| Flush or mutation busy | One deferred record published after logging; no immediate command. |
| Invalid positive view index during gesture | Preserve preceding timer/busy effects; stop/reject before an out-of-bounds write. |

The finite harness recorded in Appendix C joins full layout, projection,
centering, scrolling, overview conversion and hold/cursor bodies. It exercises
46 assertions with controlled graphics/window/query services. This is not a
native GUI test, a real terrain-mask scaler test, or a test of the complete
map-click dispatcher, unit movement, city interface or deferred replay.

Required integration cases are finite: P03 must load and scale both nominal
masks at −7, 0 and 8 and compare edge/corner samples; P04/P05 must deliver
press→expiry→release→click and press→release→click through real windows,
including deferred replay; P02 must join redraw after center/zoom and clipping;
U04/U05/U02/P06 must join accepted Goto/paradrop/direction/city branches with
actual effects and cancellation; F06 must resume distinct auxiliary centers,
zoom and saved rectangles; N03 must join real timers and reentrant input.
These remain open validation obligations rather than missing formula owners.

### Original view inputs and coordinate spaces

Nominal inputs are a loaded world, valid map-window slots, initialized native
client/content rectangles, loaded terrain masks and delivered window events.
World dimensions/topology satisfy W01. The main view is slot 0; auxiliary views
are slots 1–7. An iteration considers slot 0 even when its Enabled flag is zero,
and considers an auxiliary view when its live Enabled flag is nonzero.

Map coordinates use W01's doubled horizontal axis: adjacent row centers are
staggered, and playable centers normally have matching x/y parity. Pixels have
x increasing right and y increasing down. Event coordinates and projected
coordinates share the window coordinate space. `biasX,biasY` locate the content
rectangle within that space; they are distinct from map-centering margins.
A center or scroll request need not be a playable tile.

### Original coordinate narrowing

Stored centers and zoom use `s16`; pixel arguments are narrowed where stated.

### Original double-click native event read

city/dialog invokes its prompt. Otherwise read the native event token without
using the receiver, then capture **last accepted click coordinates**, ignoring
the incoming double-click pixel position for selection.

## Appendix E. Original numeric annotations

These original annotations retain storage widths and reference-evaluation context.
They do not prescribe internal types or redundant casts. The main contract and
F01 determine logical ranges and behaviorally significant conversions; external
file/wire encodings remain normative in their owning mappings. Where an excerpt
includes an off-nominal conversion, it applies to that explicitly admitted profile,
not as a new requirement to accept unsupported inputs.

### Reference annotation 1

Stored centers and zoom use `s16`; native pixel narrowing is in Appendix D.
These are behavioral ranges, not requirements to lay out a particular object.
