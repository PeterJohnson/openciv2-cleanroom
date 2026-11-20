# Map visual feature coverage

The required capabilities are [P02 MV01–MV30](../algorithms/P02-map-rendering-and-minimap.md#minimum-map-visual-feature-set).
The [M2 acceptance matrix](../testing/map-rendering.md) specifies each row's
authored cases, pass criteria, backend evidence and completion rules; the
[map performance contract](../performance/map-rendering.md) supplies the separate
interactive workloads and budgets.
The stock input contract is [map-assets.md](../content/map-assets.md), with
368 exact, verified atlas bindings in the [catalog](../content/map-asset-bindings.json).
Art availability, renderer implementation, a correct observer projection and
executed visual acceptance are separate claims.

This initial assessment on **2026-11-17 Pacific** uses source inspection of the
in-progress Riverbend slice and its existing [execution record](m2-map-slice.md).
It is a work list, not a new renderer execution report. “Partial” means an
existing path exercises some of the feature; no row claims full acceptance.
“Open” means the full named capability has no retained acceptance evidence here.
Update rows with concrete scenes/options/backend evidence as M2 progresses.

**Historical acceptance baseline (2026-11-17):** every row below was pending for
complete native and browser acceptance. Partial Linux evidence did not pass an entire row.
The initial Riverbend evidence predates the expanded matrix. The execution
record below preserves which cases have run and which gates still need evidence.

**Browser execution, 2026-11-20:** all MV01–MV30 rows passed the shared exhaustive
WASM matrix and the production WebGL gallery on Chromium/SwiftShader. Six
stock/native/classic × 1×/2× batches passed 367 browser checks and 229 exact
presented/fresh map comparisons (186 gallery frames plus 43 state-change frames).
Pinned HTTP content/fonts, selected art, actual input, privacy/inspection, resource
reload, explicit context-loss recovery and content failures have retained evidence.
The [browser execution record](m2-browser-map.md) gives per-row results, visual
review, hashes, commands and limitations. This closes the named browser functional
gate; hardware performance, other platforms and the named live-provider joins
remain separate. Historical entries below retain their original scope.

**Hardware browser follow-up, 2026-11-20:** the
[performance study](m2-browser-performance.md) compares browser Software2D/CPU,
WebGL/CPU and WebGL/GPU, and promotes ordered GPU foreground commands after
exact comparison with the original GPU path. The stock hardware GPU gallery
passes all 39 presented/fresh captures, backed by 294 native GPU matrix cases.
Long-pan and 4K/5K endpoint/resource-reload checks also match exactly after the
fresh reference adopts the host target size, origin and clip. These checks extend
GPU evidence; the earlier complete six-batch CPU-composition matrix and the
still-open performance/platform gates keep their distinct scope.

**GPU presentation correction, 2026-11-18 Pacific:** the
[GPU follow-up experiments](m2-gpu-experiments.md) found that aliased CPU
foreground workers could leave Skia's uploaded viewport texture stale. Earlier
warm hardware-Xwayland timings that exercised this path are historical, not
accepted timing of correctly updated GPU-presented frames. Their CPU raster
comparisons still establish CPU pixel behavior; software-only and independently
read-back cold-frame evidence have their original scope. The follow-up repairs
invalidation, adds actual GPU-upload readback checks and repeats the native
navigation/edit measurements. Complete native presentation acceptance remains open.

| ID | Initial coverage | Work required for acceptance |
| --- | --- | --- |
| MV01 | Partial: eleven base terrain bindings | Individual terrain identity/custom-art and overlaid scenes. |
| MV02 | Open | All coastline contacts, channels/islands, flat edges and wrap seam. |
| MV03 | Open | Edge blends and all Forest/Hills/Mountains connection variants. |
| MV04 | Partial: connected river art | All junctions and river mouths, knowledge boundaries and distant zoom. |
| MV05 | Partial: resource and Grassland-shield bindings | Every terrain/special selector; independent predicates and simultaneous layers. |
| MV06 | Partial: improvement marks | Exact agricultural/mine art, distinct combinations and city underlays. |
| MV07 | Partial: road/rail arms | Mixed junctions, isolated marks, city connectors and remembered changes. |
| MV08 | Partial: generic feature marks | Exact huts/pollution/fortress/airbase art and occupied-aircraft owner colors. |
| MV09 | Partial: unknown versus known tiles | Directional feathered edges, remembered/current distinctions and hidden-state noninterference. |
| MV10 | Partial: six demonstration unit images | All 63 classic bindings and the separate leader selection, custom units. |
| MV11 | Partial: unit marks/stack count | Actual color-role banners, primary/secondary stack treatment and dark shadows. |
| MV12 | Open | Health inputs, three severity bands and nonordinary imported values. |
| MV13 | Open | Order, waiting, automation and veteran projection/visual combinations. |
| MV14 | Open | Both sleeping styles, fortified overlay and aircraft-at-base cases. |
| MV15 | Partial: one city appearance | All 48 variants, appearance policy and per-variant registration. |
| MV16 | Partial: own-city name/size and anonymous remembered site | Foreign memory, owner flag, garrison, disorder and population anchors. |
| MV17 | Open | All eight color roles; live color reassignment across every dependent layer. |
| MV18 | Partial: selected tile/object | Distinct active versus inspected state, focus treatment and reduced-motion choice. |
| MV19 | Open | Grid, city radius, ownership and bare/full modes with observer-safe toggles. |
| MV20 | Open | Path/trade overlay drawing and separate safe U03/C07 producer integration. |
| MV21 | Open | Work counters and richer inspection; safe W04/knowledge projections. |
| MV22 | Open | Viewport-fixed notifications with required semantic completion. |
| MV23 | Open | Overview disclosure, owner marks, selection and actual camera footprint. |
| MV24 | Open | Movement, combat and all explosion/nuclear frames; interrupted/skipped cleanup. |
| MV25 | Partial: drag, wheel, bounds and picking | Presets/steps, active-unit tracking and all flat/wrapped/fractional cases. |
| MV26 | Partial: detail cutoffs | Declared bands with coast/river/fog/health continuity and every full-detail feature. |
| MV27 | Partial: pinned Sans labels | Physical-resolution status glyphs, annotation clipping, long/custom names and dense views. |
| MV28 | Partial: RGBA textures and batched layers | Tint/silhouette/greyscale, thin details, overhangs and overlap-sensitive ordering. |
| MV29 | Partial: retained image and dirty selection | Every layer's old/new bounds, observer/color/content/options changes, foreground clipping and stable margins. |
| MV30 | Open | Color correction, backend/failure transitions and equivalent feature coverage. |

The [scoped projection](../../src/OpenCiv2.Query/MapProjection.cs) supplies
explored terrain, remembered improvements and city sites, detected units, and
admitted own-unit and own-city details. [Authored gallery cases](../../src/OpenCiv2.App/Map/MapGallery.cs)
supply precise inputs for gaps such as remembered foreign appearance, trade
paths, and movement and combat sequences.

M2 closes the rendering/interaction side with authored scenes for every row.
Gameplay milestones close actual producer/command joins. Exact art-key validation
is a data check; a nonblank frame, all-atlas decode, one city's rendering, cache
equality or Linux interaction performance is not evidence that all rows render.
Preserve the finite scope of existing reports and record additional evidence
instead of relabeling it as full feature coverage.

## Linux software execution, 2026-11-18 Pacific

The managed Avalonia desktop on isolated X11 opened 45 owned windows at 1×
across 31 stock boards, eleven classic-replacement boards and three native-only
replacement boards. Twenty-six input and lifecycle
checks covered overview navigation, drag pan, fractional and preset zoom,
flat and wrapped picking, world changes, texture reload, independent windows
and resize, worker inspection, admitted unit and city facts, region clipping,
color reassignment, the bare/full display toggle, pinned text, active-unit follow,
live theme changes and recovery from an unsupported input,
a visible terrain edit during an effect, notice and effect lifetimes, and
retained-terrain keyboard pan. The same checks passed at 1× and 2× backing
density. The mixed-density run opened two maps in one desktop process on
distinct Xvfb RandR monitors. The companion moved between 1× and 2× backing,
resized and closed while the primary kept its camera, revision and viewport.
The Skia CPU matrix rendered 31 boards and asserted 317 image identities.
Pillow independently matched every decoded
straight-alpha RGBA pixel across all 2,105 images in the pinned atlas.
Riverbend passed 20 native input and asset-admission checks at both backing
densities, including independent display toggles. The 459 core
tests passed, including W01 resource and hut predicates, W03 projection
privacy, stale targets and RNG.

The [gallery](../../src/OpenCiv2.App/Map/MapGallery.cs), [painter](../../src/OpenCiv2.MapRendering/MapPainter.cs),
[stock selectors](../../src/OpenCiv2.MapRendering/MapTileArt.cs), and [matrix probe](../../tests/OpenCiv2.MapProbe/NativeMapMatrix.cs)
share the production renderer. MV01–MV30 have passed local Linux software
feature acceptance. Interactive performance, browser acceptance and physical
backend/display checks remain open.

| ID and fixture | Implementation / live integration | Executed evidence and outstanding cases |
| --- | --- | --- |
| MV01 `terrain-identity` | Linux software passed / integrated | Eleven distinct isolated and mixed terrains. A separate composition matched the production frame for every stock and classic replacement terrain, with and without feature overlays. Removing the supporting terrain from the composition changed visible pixels in all eleven cases. Stock Ocean support includes the four pinned water quadrants. |
| MV02 `shoreline-neighborhoods` | Linux software passed / integrated | All 32 selectors matched independently placed pinned quadrant art. Complete image comparisons covered an island, a one-tile channel, a peninsula, a fog corner, flat edges and the corresponding wrapped seam. Visual review checked continuous banks and points. |
| MV03 `terrain-transitions` | Linux software passed / integrated | All 44 edge blends and 48 Forest/Hills/Mountains masks matched a separate unchunked composition for stock and classic art. Additional flat and wrapped scenes include overlapping edges and relief across the seam. Sprite batches preserve row and per-tile layer order across chunks. |
| MV04 `river-networks` | Linux software passed / integrated | At 1.0 and 0.5 displayed scale, all 16 river masks, four Ocean-mouth keys, and flat and wrapped coast intersections matched separate stock and classic compositions. Every river, road, rail and resource in the stock crossing contributed distinct pixels. Knowledge-edge and detail-cutoff coverage remains recorded separately under MV09 and MV26. |
| MV05 `resource-identities` | Linux software passed / integrated | All 22 specials and the Grassland shield matched a separate stock and classic replacement composition and each contributed visible pixels. Land cases include an overlapping river and road. Ocean resources and visually distinct variants passed. The pinned Grassland specials intentionally match each other. Scoped W01 suppression, second variant and live Grassland absence passed. |
| MV06 `agriculture-and-mines` | Linux software passed / integrated | Seventeen exact compositions per stock and classic theme cover distinct fallow, Irrigation, Farmland and Mine states, river/resource crossings, city underlays from either adjacent remembered field, concealed neighbors and bare mode. The underlay leaves the published improvement unchanged. In six classic combinations, the authored opaque foreground sprites cover the center field, which the independent composition verifies before and after those layers. |
| MV07 `route-junctions` | Linux software passed / integrated | 42 isolated, direction, mixed, city and airbase selector assertions. Flat, wrapped and unknown endpoints passed. The pinned road and rail arms contribute separate pixels to the full and reduced crossing. Scoped unpublished live route changes preserved the observation and pixels. A remembered upgrade and removal changed both the selected binding and the pixels, with dirty renders matching fresh output at 1× and 2×. |
| MV08 `tile-features` | Linux software passed / partial | Stock and classic art independently matched huts, pollution, fortresses, empty airbases and all eight occupied-aircraft colors. Withheld hut and occupancy facts gave the same pixels as absent hut and empty airbase. The shared City+Fortress bits produced no city marker. Alpha checks separated the fortified overlay from its status label and verified the standing fortress under the unit. W01 hut predicates passed; live occupancy and later hut events remain producer joins. |
| MV09 `knowledge-boundaries` | Linux software passed / integrated | Opaque fog and every feather direction and intersection match independent stock and classic compositions at full and reduced scale. Scoped hidden substitutions preserve map, overview, permitted effects, inspection and RNG at all three detail bands. Replayed flat and wrapped regions match fresh and cached pixels. Excluded terrain, objects and effects cannot alter admitted roads, labels, selection or city-radius coverage. Native region changes clear the stale selection and admit picking again when the region is restored. The later city-resource screen remains a separate join. |
| MV10 `unit-art-identities` | Linux software passed / integrated | All 63 ordinary bodies and the separate barbarian leader matched exact composition in stock, native-only and classic selections. Both stock anchor groups and the native and GIF registration overrides matched. |
| MV11 `unit-owner-and-stack` | Linux software passed / integrated | All eight roles and both stock anchor directions matched independent primary, secondary and shadow compositions. Classic primary and secondary colors used separate authored palette samples. Counts and inspection preserved unit identity and the existing representative, reorder and gameplay-neutrality checks. |
| MV12 `unit-health` | Linux software passed / partial | Exact colors, bounds and signed inspection at 1×/2× for full health, each threshold, zero and negative remaining health, zero and negative rule hit points, simplified combat, withheld and unavailable values. X11 hover reached the substituted, withheld and unavailable values. Combat and later unit-state providers remain separate. |
| MV13 `unit-order-status` | Linux software passed / partial | Pixel-distinct badges for each published order, no order, waiting and automation at 1×/2× while the stack count, health, work counter and veteran mark remained visible. Inspection retained all admitted facts, including both waiting and automation in the combined case. Scoped changes to private own AI encodings and foreign order data left pixels and inspection unchanged. Later order producers remain separate. |
| MV14 `sleep-and-fortification` | Linux software passed / partial | Sleeping and stationed aircraft matched the same body pose in both modes. Exact independent composition preserves alpha, runway, terrain and banners. Fortified units and standing fortresses passed their stock and scenario occlusion checks. Further live aircraft and command producers remain separate. |
| MV15 `city-appearances` | Linux software passed / partial | Exact body and population registration for all 48 variants in stock, native-only and classic selections. Threshold, era, Palace and wall cases are covered. Varying an unadmitted size leaves the generic city marker and admitted annotations unchanged. Foreign appearance remains an authored join. |
| MV16 `city-annotations` | Linux software passed / partial | Both pinned flags and population placement passed exact registration. Remembered and authorized-reveal sizes stay distinct, including a remembered zero and a withheld garrison. A city without an admitted appearance retains its admitted flag, disorder and number beside its caption. Anonymous sites and real X11 own/remembered inspection passed, and crowded named captions remain represented in the matrix. Foreign memory remains a producer join. |
| MV17 `owner-color-reassignment` | Linux software passed / partial | A single civilization changed through all eight roles at 1× and 2× in stock and classic art. Primary and secondary shields, bright and dark flags, disorder, population, caption accents, occupied aircraft, ownership, city radius and overview changed. Dirty frames matched fresh frames pixel-for-pixel and preserved the neighboring civilization. Native reassignment preserved the selected city and owner inspection. Installed rule recoloring, concealed unsupported roles, admitted unsupported-role diagnostics and exact recovery passed. Later foreign city and airbase producer joins remain separate. |
| MV18 `selection-and-active-focus` | Linux software passed / integrated | Selection, inspection and active pulse appeared at separate positions. The pinned tile mask retained terrain color. The active mark followed the moving unit, stayed stable with reduced motion and did not transfer to a recycled slot. Combat focus, native picking and stale command targets also passed. The overview outline matched software pixels at all three accelerated virtual-display sizes after the raster correction described below. |
| MV19 `map-display-modes` | Linux software passed / integrated | Eighteen exact grid/radius/combined compositions across the flat and wrapped side and both corners. W01 geometry admitted 12 flat edge or 21 wrapped edge tiles, and seven flat corner or 12 wrapped corner tiles. Bare mode kept the resource and pollution sprites while suppressing routes, fields, huts and cities. Ownership changed only its disclosed tile. The four real Riverbend options toggled independently at 1× and 2× while preserving frame selection, camera and world revision. |
| MV20 `route-and-trade-overlays` | Linux software passed / authored-only | Exact colors for the four meanings and gaps in the unsatisfied path. Pixel checks covered empty, region-restricted, wrapped and disconnected paths, active-unit change, slot reuse and route origin movement. Native capture used the same painter. U03/C07 production paths remain a separate join. |
| MV21 `work-and-inspection` | Linux software passed / partial | Scoped Settler and Engineer rules publish stored work of 0, 9 and 231. Large and zero counters produce distinct pixels at 1× and 2×. Changes to private foreign counters leave the rendered image unchanged. Pointer and keyboard return identical facts for all five worker cases, an own city, a remembered foreign city and an anonymous site at both densities. W04 work execution and further W03 producers remain separate. |
| MV22 `map-notifications` | Linux software passed / partial | Opaque glyph masks of two notices remained pixel-identical during actual pan and zoom at 1×/2×. Expiry, cancellation, observer replacement and constrained map clipping passed. In Riverbend, reduced motion preserved the cancel and accept path, the single committed effect and the success notice. Later gameplay notice providers remain open. |
| MV23 `overview-navigation` | Linux software passed / integrated | Actual pointer and keyboard recenter at 1×/2×. Exact land, Ocean and unknown colors, all eight admitted city role colors, anonymous remembered site, even/odd row selection and identical pixels with and without main-map units. Native footprint pixels match separate geometry for split wrap, flat edges, aspect-changing resize and zoom. Focus transfers the overview marker from its navigation cursor to main-map selection. |
| MV24 `movement-and-combat-effects` | Linux software passed / authored-only | Pixel-distinct 8 ordinary and 11 nuclear frames. Wrapped body and explosion overlap preserved settled pixels and exact completion accounting. Moving body, banner and shadow matched a fresh static midpoint frame while the other units and stack count remained visible. X11 pan and zoom retained concurrent moving and combat effects at 1×/2×. Cancellation, reduced motion, observer change, deletion and slot reuse cleared the old effect. Visible cosmetics left Riverbend gameplay and RNG unchanged. |
| MV25 `camera-and-picking` | Linux software passed / integrated | At 1×/2×, real drag, Shift-drag, wheel and keyboard presets preserved the pointer anchor and picked flat edge vertices, fractional wrapped copies and tall unit art beyond the tile. Flat clamp, outside miss, bare terrain selection and region clipping passed. Overlapping target order held within city tiles and across copies. Follow centered the nearest copy and preserved the already comfortable camera. |
| MV26 `detail-transitions` | Linux software passed / integrated | At both cutoffs and both densities, city and unit pixels, picking, selection, all three exact health colors and a distinct withheld badge survived. Toggling one neighboring observation at a time isolated river mouth, coastline and fog feather pixels inside their own tiles. City captions survive reduced detail. Full-detail irrigation and status cutoffs matched the declared unrounded scales. The order, large work counter and veteran mark occupy separate pixels. Admitted facts stay inspectable, and separate scoped comparisons preserve gameplay and RNG across all three bands. |
| MV27 `labels-and-density` | Linux software passed / integrated | Exact 10-DIP captions at .4 and .63 zoom at 1× and 2×, including accented names, ellipsis at a text element, crowd precedence, full-name inspection and region clipping. Work 255 and population 127 matched independently drawn badges at the 7- and 8-DIP floors. Pinned Sans, Serif and Mono were exercised in one notice, including the missing-glyph marker. Actual native wheel input preserved the viewport glyph mask. |
| MV28 `alpha-and-occlusion` | Linux software passed / integrated | Stock and classic checks verify alpha holes, thin flags and trim, selectively tinted aircraft, and the sleeping, shadow and banner treatments. Independently composited translucent native PNGs proved visible fortress, city, two overlapping units, a caption and an effect together at 1× and 2×. Display grids and ownership fills stay beneath tall art. Reversed depth order changed 845 pixels at 1× and 3,873 at 2×. A cached repeat matched the fresh composition exactly. |
| MV29 `cache-invalidation` | Linux software passed / integrated | Fresh comparisons cover retained terrain scroll, flat/wrapped edges, notices and effects, margins, fractional displacement, sampling ties, and denied scroll allocation. Dirty changes to terrain and road neighbors, units, city size/walls/name and label reflow match fresh pixels. At 5K the ground cache trims on resize. Same-process windows kept independent state across mixed-density migration, close and reload. All 114 stock/native/classic swaps released prior rasters, rejected stale generations, matched fresh map and overview pixels at both densities, and retained stable cache sizes. The live X11 switch restored its original art and selection. The full simulated replacement workload and 110 observer-lifetime checks completed. Physical replacement timing remains open. |
| MV30 `backend-and-recovery` | Linux software passed / integrated | All three color modes at 1× and 2× matched fresh viewport pixels after direct CPU fallback and recovery. Allocation failure produced an explicit limited frame, released partial rasters and withheld retries until requested. Native color changes and the unsupported-role fixture showed the limited frame and restored the chosen view and target. Reduced-motion completion and malformed art were verified separately. Physical backend-switch evidence remains pending. |

Current local evidence under `build/m2-native/` includes:

| Receipt | SHA-256 |
| --- | --- |
| `cutoff-coast-matrix/matrix.json` | `b47a1689157b9ad454b70acac21c13fe0e68df9f2cd5f0efddb991c6d09bee4d` |
| `cutoff-coast-matrix/MV26-cutoffs.json` | `8e9cc2eebaa77522e9a4aacec31b4f2bc3d43c23c3877a974a00ef78faa6f98f` |
| `tile-features-matrix/matrix.json` | `da12ad0f1dbac7915dd17e9a026adc1af534e35bac2a22a875a90150d2fa8d05` |
| `tile-features-matrix/MV01-MV05-stock-pixels.json` | `f7534c28f31efaf481889bf9d728ca7fefc3d277b2018770221d9b86966c9e52` |
| `tile-features-matrix/MV01-MV05-replacement-pixels.json` | `be79e1f72c9e009033dd6940ee4edb5365a54c69e0bbefdd5634bcc0ad4fce88` |
| `tile-features-matrix/MV08-stock-pixels.json` | `9e9f5663c03d15ec40596fab4f2b0696081a1cda65f519473100bbab15609a1b` |
| `tile-features-matrix/MV08-replacement-pixels.json` | `e25b832ce9520f2289b4d902326fcca386f03c89f690f5cffc9059975edc2e7e` |
| `resident-final-matrix/matrix.json` | `79329ab9066e72c453e0eae6c4c36b0fe2b9d6be16e4c9182f8d8efd6bac7ee7` |
| `resident-final-matrix/MV02-shoreline-pixels.json` | `603afaf797bb5d621c67fa0a1b85042423da5109162561aac58dd78aff2399b9` |
| `resident-final-matrix/MV07-route-memory.json` | `dc12fc0ae49422d9f95d98c3791fa6a0afbd24b4656688064b019d0028a2fbe3` |
| `resident-final-matrix/MV29-ground-resize.json` | `7e0da31ac62394fc1568fd726bdecaa2ca45a4a35ac8e59189eca59bc1ee59b6` |
| `object-complete-matrix/matrix.json` | `fcf181ec7ecb607db6eb09568881cbd5ca1bf2593e70e7d802b6613d883a553f` |
| `object-complete-matrix/MV12-MV13-unit-status.json` | `bf7aa0b802ad21e22b9fd369baf8ea636f8ff9c0e582af29aef7ccc2723fef65` |
| `object-complete-matrix/MV14-aircraft.json` | `c78f4f4c0ca5cabef8eb2771ea6fcc50fa36a6231c3146576d98408034b7ea3f` |
| `object-complete-matrix/MV29-left-shield-dirty.json` | `309c1c41b012daf453512ea9f13ab690a94f5aeff7d904848f1465bc394122c8` |
| `terrain-final-matrix/matrix.json` | `a8e36e48c72c78c3110acae1269ce66b231b48accfec110b5329b5ec0dc27e04` |
| `terrain-final-matrix/MV03-MV04-stock.json` | `1486ce936a007e8ee732385204eb9763e58ae4d83892025b362f9ccc851ee1a0` |
| `terrain-final-matrix/MV03-MV04-classic.json` | `e671e335350c9d2250c0ebea53361037fab76043b1d4481fd84c795c2f202410` |
| `terrain-final-matrix/MV04-pinned-crossing.json` | `d4b6899c20bac3d757db8920911967cfd05a3dcdef2743df1dd9f936b1107916` |
| `terrain-final-matrix/MV06-agriculture-stock.json` | `2d4a799edff36766a650441129a8501f77ecf5295a551d595c546d78c4f77003` |
| `terrain-final-matrix/MV06-agriculture-classic.json` | `f8d3d326304ef0c45135a9d8d4a3547bfaa7d9ba9088759de98bd4d9e38525ec` |
| `terrain-final-native-1x/native-gallery.json` | `7a1f6c70710684fdf525438df0eb252dd239ba8b18c16796fb7e10ab221b3462` |
| `terrain-final-native-2x/native-gallery.json` | `9ada1ba1d7d6736e0e9ef380e6bbe0361ac30c9b42647e072219789c76fde3e7` |
| `terrain-final-mixed-native/native-gallery.json` | `fbd92b19f3e73db5fa75ac31d94ff00c98c0e3c49e46fd87bb074f2a99f09d2c` |
| `terrain-final-riverbend.json` | `591d2c1fecd44fcd1f83871caa12e6205d2eb988adf16c2292aeb803fa4fd94b` |
| `terrain-final-riverbend.png` | `bc1057184b5a5ed421edaef9b3422d041b9eae2fb14d9448fabec312091e78fc` |
| `knowledge-wrap-matrix/matrix.json` | `fd0092a279b179f046f5cf5004fb86a0cb0930cc470f071b003fda559e3bee1d` |
| `knowledge-wrap-matrix/MV09-fog-stock.json` | `c24842f68603bea3f3ed39c0d83b8eee7664fb64891cd9f3272418b115231d1b` |
| `knowledge-wrap-matrix/MV09-fog-classic.json` | `1c90d751b18d2b03672b69e794dd5b95263c87b0f644504467a597f75fc531f1` |
| `knowledge-wrap-matrix/MV09-hidden-invariance.json` | `92489a034edf44523d71cb58bae930c626a70475cf14a0d31ee13418d673e258` |
| `knowledge-wrap-matrix/MV09-region-replay.json` | `de24b69933cd5db530e9760778df5f50829c6601cb974456efd13f6c5196dd66` |
| `knowledge-wrap-matrix/MV21-work-projection.json` | `f7cccab0c5e6dbb1250c989386436e7ed25ea737f8d63d5d2cc954cc04d69425` |
| `knowledge-final-native-1x/native-gallery.json` | `cb82f3e6c7ab15a0a5d9727e145a152f796bd0414e7b87dd5cd94f717c8a71fb` |
| `knowledge-final-native-2x/native-gallery.json` | `16cdc3683635beaa0578a730cddf0e4bce641a46ac30b6ff9635c5b656adf13b` |
| `knowledge-final-mixed/native-gallery.json` | `506d28470def88a168f551d2d35608ef7d0a8c16bcb20b19521bb83d421d669e` |
| `knowledge-final-riverbend.json` | `bb5dc631c21255a2370cc8382ea1941e8b0a30e17eaf73c7fbcef741c0c1cbc5` |
| `ownership-serial-matrix/matrix.json` | `4dd425aad80e9ec83b1cdd9fd76b4d4444e8043457712b88b0807742707f487a` |
| `ownership-serial-matrix/MV17-role-stock.json` | `35945cc708a318ba619af76899ba823089d2fd43194854c0939ecb8d97d7388b` |
| `ownership-serial-matrix/MV17-role-classic.json` | `ee84710c4753c63958f1b062bbbac75572d8917ec75c8a739d8edd081b0394e2` |
| `ownership-serial-matrix/MV17-scoped-colors.json` | `63b4d4d123b03f9231baf3e5530084e1f81e8cc4c90bc7b6b8dacaa5dd958ee8` |
| `ownership-serial-matrix/MV19-display-modes.json` | `b75334a870ec51687a6222966a3f83bd9852704199dc2d781f9054e7e8fe073c` |
| `ownership-final-native-1x/native-gallery.json` | `1003d78d93907b20960ececb6fa9117f9005c16e2e0ec9d3eb6f0d4cfc51a2d8` |
| `ownership-final-native-2x/native-gallery.json` | `b3db6063eaf919ec4480a994933fa3f2848fceca76911fd7f13126fd3fd4f923` |
| `ownership-final-mixed/native-gallery.json` | `15ab943553a15000552ea6c87b038ab2836c2dcd8345347e0e03ec8ab1dab87e` |
| `ownership-final-riverbend-1x.json` | `adf89ca404f8cce8517d09c5c798d6c78dc6a05798d8c9d7aa6c81d7edb5b626` |
| `ownership-serial-riverbend-2x.json` | `9c28d3f996da1e7a3ec4ddf3807e78f06ee2c67c30df50294b47cd88654ca5f3` |
| `caption-composite-matrix/matrix.json` | `6dcbc754ec30eef0dad9115f44baf5ce6331b8103e14f7f2d588b81f09173b04` |
| `caption-composite-matrix/MV27-labels.json` | `ca8081b9d927ee23afa7a6696da71e1aeb85e87620c49d99f997d9eb8b08a2a9` |
| `caption-composite-matrix/MV28-occlusion.json` | `4484519bafa39eab261824d34f41a02837becb627137176cf2c38ba0f99a11b0` |
| `typeset-native-1x/native-gallery.json` | `147223f67ed8629ed75ed28ec9643c1d2e1ca15ff561a50aae620e5e6735772f` |
| `typeset-native-2x/native-gallery.json` | `9038f6d56ea9b216f5815adb0b94a7766a61dc7b2b716a48142cdfad48203edd` |
| `typeset-mixed/native-gallery.json` | `a76170bea565f09a6ff943d2da61e3ca3286d345510c77aca9aabc0fce8bbd41` |
| `typeset-riverbend-1x.json` | `8b4eceed2af9bb68a23ee7700f985756a466b8e1a7f3f28436e8c72679d2666d` |
| `typeset-riverbend-2x.json` | `eb9b82959b238a77e292febce9291f1001d84986fe4c194120212a3a56912c72` |
| `renderer-lifecycle-matrix/matrix.json` | `adb6b6f1ed93e1c481079fbfd2e6aa8d9be1d786a6bec5802e5054bd0a0398fe` |
| `renderer-lifecycle-matrix/MV29-live-themes.json` | `13bbf333f18f816f745c76f5b9023557cf7a06c53ab4094caaf45a126527f3f8` |
| `renderer-lifecycle-matrix/MV30-full-recovery.json` | `dc38b5cc7156df7198e0380025bf33ee62a644992bb3be35a379c6ad2c17035a` |
| `renderer-lifecycle-native-1x/native-gallery.json` | `b076abaf7cc0f11aecdc7ca9df9c841ddf39b90bbde1db3e8499f2ed646311bf` |
| `renderer-lifecycle-mixed/native-gallery.json` | `76598211e938ee1c1226860ebc79a79e5e5d371e18860a5ec46942b3752abcf7` |
| `renderer-verified-native-2x/native-gallery.json` | `2137680e2ea01eeb99fb3237d80a20ba37740947f963234bd03eb2d8f3a7dc7c` |
| `renderer-verified-riverbend-1x.json` | `ed19807af31037a4033bfe9769bf20311274158ebbcec69de1d77579f26d87bc` |
| `renderer-verified-riverbend-2x.json` | `ad2dc9d13b0e028fab5834210ef015f520a56602c1dae1219f24ad589413c6bb` |
| `object-final-native-1x/native-gallery.json` | `0c1b8dd090b412b717e508428684257052ca4b8cc89ec6702483b92f240f4112` |
| `object-final-native-2x/native-gallery.json` | `daa505f2ba47406d2600e0396300b65616cc172fe64593e6d9e50e3f63ed32df` |
| `object-final-mixed-native/native-gallery.json` | `1944bb2efdb29d7d603bb609a096e76023107c564dc07422ada9ecaa4e6226be` |
| `object-final-riverbend.json` | `fb51b353ca2ce219620fa01da860dd1ed63818757e33ebd0e9297526077f0ec5` |
| `contrast-labels-2x/review.json` | `3251be64210b616bd29e7bf95adfe1f0a688dde9e5fe4036d01fbfdde0944396` |
| `atlas-pixels.json` | `8bbc42f4d55686ee20c3c0b18da3f7f4530efde33b5e20ba4d96863b7688d47d` |

The gallery reports bind their compiled inputs, captures and contracts.
Visual review included connected features in Riverbend, both sleeping styles,
the constrained HUD/map clips, both pinned crossing detail captures, native
crowded labels at 1×/2×, the four path colors, and the remaining stack with
moving and combat focus marks. At the detail cutoffs the city and unit stayed
recognizable, and the separated order and work labels and veteran mark
were readable in the 2× full-detail capture. The two
authored Grassland special keys select identical pinned PNGs. W01 excludes both
specials from live Grassland and supplies its separate shield predicate.
The stock and replacement contact sheets show each bare terrain above its
two resource variants. Visual review confirmed that the land resources leave
the supporting terrain recognizable and the Ocean resources remain visible
on the water quadrants.
The improvement contact sheets show the distinct aircraft colors above
the same runways. In the stock fortified pose, both the standing fortress
and the unit's fortification overlay contribute pixels. In the authored
replacement, foreground alpha covers those two sprites in the combined
pose while its independent fortified status label remains legible.
The shoreline captures show both banks of the one-tile channel and the
continuous outlines of the island and peninsula. The wrapped version picks up
the adjacent land at the physical edge. The relief and river-mouth captures
show continuous terrain and water branches across repeated copies at the seam.
The updated [Riverbend capture and input receipts](m2-map-slice.md#riverbend-feature-layout)
cover the pinned road crossing, forest resource and the continuous course to
the wrapped gulf. In the corresponding 213-image matrix, the ten changed
images came from tests backed by the updated Riverbend observation.
The stock farmland remains visible beneath the city and beside the river.
The fog contact sheet shows solid unknown tiles beside feathered known edges.
The clipped worker remains identifiable in flat and wrapped views. In the
unrestricted Riverbend capture, the badge shows all three digits of the large
work counter beside the order and stack badges.
The color comparison exposed 26 changed edge pixels on an adjacent ownership
diamond when Skia clipped an antialiased path during dirty redraw. The painter
now caches complete tile-sized ownership rasters in its existing texture budget.
All role changes match fresh output, including the adjacent tile. Visual review
checked the color spreads and separate trim on stock banners and the recolored
aircraft, with the runway intact in the selected classic art. The crowded
caption capture shows the accented title and its ellipsis. The notice above
shows the Serif arrow, the Mono arc and the Sans replacement glyph. The
translucent depth capture preserves both banners, the caption, and terrain
through the different source holes.
The native theme captures compare map art outside the selected tile's hover
highlight area. The offscreen references compare the complete map and
overview. The limited native frame shows the unsupported role and clears the
older art before the corrected observation is drawn.
In the Riverbend route captures, the
published change replaces the brown road arms with gray rail arms as expected.
The unit contact sheets show the two directions of the secondary banner and its
shadow. The city sheets show all body styles and both flags, including the
native-only registration override. The withheld-appearance capture shows the
remembered zero beside the city marker and above a separate name caption.
Removing a native shield anchored at `(-16, -16)` changed 340 pixels beyond
the former left-hand damage bound. The wider dirty rectangle matches a fresh
frame byte-for-byte.

### Software timing status

The completed pre-scroll offscreen baseline, `full-software-raster.json`
(SHA-256 `954bf65d11c184eb22e09126b1565835964be63a35f2e844786922875c408d17`),
contains 54 independent workload series and 8,469 pixel-equal comparison
frames. The continuous cases each measured 300 changed frames after 30 warmup
changes. The cold and observer-replacement cases each measured 20 frames.
Across 110 observer switches, the reported managed heap after collection
stayed between 273,702,408 and 273,704,024 bytes. Peak process working set was
1,623,855,104 bytes and included the candidate, independent reference, and
their input data. The report retains individual run quantiles and samples.

| Physical software raster | Cold full p95 | Unchanged p95 | Pan p95 | Continuous zoom p95 | Dirty world p95 | Observer plus resource replacement p95 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1920 × 1080 at 1× | 1,114–1,308 ms | 0.23–0.25 ms | 16.70–17.08 ms | 15.96–16.29 ms | 26.34–27.77 ms | 1,102–1,157 ms |
| 3840 × 2160 at 1× | 2,334–2,513 ms | 1.58–1.81 ms | 54.34–56.19 ms | 53.73–57.16 ms | 76.71–79.92 ms | 2,244–2,446 ms |
| 5120 × 2880 at 2× | 2,075–2,147 ms | 2.95–3.18 ms | 74.40–80.86 ms | 240.01–268.39 ms | 99.92–116.03 ms | 2,050–2,380 ms |

The current scroll path keeps at most three 32-bit viewport rasters, each
bounded by 16 million pixels. Its scratch allocation is optional. The
source-pixel boundary guard chooses a full transform when nearest sampling
could change the result. A scroll receipt records terrain repaint pixels
separately from final viewport composition.

Native keyboard pan at ordinary scale repainted 39,648 of 779,700 terrain
pixels at 1× and 144,400 of 3,118,800 at 2×. The display still composed the full
foreground. The separate 5K half-world X11 diagnostic used a 5120 × 2880
backing viewport at 2×. All ten genuine pointer-driven samples used full
repainting at the fractional sample phase. Software draw p95 was 100.32 ms,
and polled input-to-draw p95 was 209.75 ms. This diagnostic used three warmup
changes. Its receipt is `scroll-5k-2x-host/native-performance.json` (SHA-256
`e9a4c47892041a686ca9c00f817c83b297fff41a637e7ba78298767bf425d4c5`).

The focused retained-pan run matched all 2,700 measured frames to an
independently rebuilt viewport. Each of the nine 300-frame series reused the
ground in 280 frames and took the full path for twenty changes in physical
sample phase. Draw and placement remained deterministic at all three sizes.
The full-series p95 was 16.88–18.14 ms at 1080p, 51.37–54.16 ms at 4K, and
80.31–86.17 ms at 5K. The raw receipt is `retained-pan-full.json` (SHA-256
`e406f74f56055a16338e0d7c86934e5ff5a5b1585dd77d25e84a0d1d6433447c`).

The renderer now caches tinted alpha masks in the existing 96 MiB texture
budget. The cache keys include the binding, backing tier, and exact color.
The short full-featured A/B pan trials measured eighteen changed frames per
size after three warmup changes. All 54 frames in each trial matched fresh
viewports. Median time fell from 15.60 to 14.40 ms at 1080p, 45.59 to 41.36 ms
at 4K, and 70.91 to 63.61 ms at 5K. Peak working set for the two-painter
probe rose from 1,620,643,840 to 1,650,446,336 bytes. Receipts are
`scroll-tint-reference.json` (SHA-256
`bd842fc4b559093a9a239cce2a1a9f2f0db1f3c07cb01c0907f5bd1352527c32`)
and `scroll-tint-candidate.json` (SHA-256
`525c8ce818c4fc8ffd23c7b4226e75b02c8e3aa9c30a53e7bfcd6095dd0d8531`).

The subsequent 2,700-frame focused pan run on the tint-cache build also
matched every fresh comparison. The three series at each size had draw p95
of 16.64–19.99 ms at 1080p, 47.26–52.79 ms at 4K, and 74.19–83.33 ms at 5K.
The raw report is `tint-retained-pan-full.json` (SHA-256
`9904925863fa9076d0bfef417ec93c1e10d6217f7d37ca4fc84505ca94e70365`).

At 5K and 2×, the tint-cache build repeated the ten-change native pointer
diagnostic, including a recorded one-physical-pixel displacement. The
viewport contained 16,781 distinct tiles at the half-world start. All ten
samples used the full transform at this fractional sample phase. Draw p95
was 90.74 ms and polled input-to-draw p95 was 186.30 ms. The raw receipt is
`tint-5k-2x-host/native-performance.json` (SHA-256
`733f6f472f18b42cd24047d1734ed9c2113600d3a5bb85986d2db927afca7aae`).

The badge and overview build repeated pan, continuous zoom and dirty world
on actual X11 software viewports at 2×. The diagnostic recorded ten genuine
changes after three warmup changes per size and mode.

| Backing viewport | Distinct tiles at half-world start | Pan draw p95 | Zoom draw p95 | Dirty world draw p95 |
| --- | ---: | ---: | ---: | ---: |
| 3840 × 2160 | 16,587 of 31,752 | 66.25 ms | 196.89 ms | 63.89 ms |
| 5120 × 2880 | 16,781 of 31,752 | 88.79 ms | 903.28 ms | 85.00 ms |

The largest 5K zoom stall prepared 496 ground chunks on entry to the overview
detail band. The largest 4K zoom stall prepared 364 chunks on a backing-tier
change. Warm large-viewport redraws and cold transitions still exceed the
interactive budgets. The receipt is
`legibility-4k-5k-native/native-performance.json` (SHA-256
`c1cd2c3a0dd1789f60c6298dd9e59de3299adcfeb37febfb8c479a38b061ae62`).

The neighbor scan now keeps only the improvement fields and masks used by
ground composition. It skips the four axial lookups on tiles that need
neither coast nor route contacts. All 67 pre-existing matrix PNGs were
byte-identical. A repeated 2× X11 diagnostic measured the 5K overview crossing
at 890.32 ms, including 858.35 ms building 496 chunks. Selection and grouping
used 4.36 ms of that chunk time, and the texture cache recorded no preparation
on that frame. Cold offscreen draws at 5K still took up to 1,706.01 ms over
three fresh comparisons. The next bottleneck is rasterizing new chunks, with
texture decoding and raster preparation contributing on cold starts.
Raw receipts are `terrain-stage-5k-native/native-performance.json`
(SHA-256 `8110e0153bae987da57a9007b4033b2e179471fe6eb11835e3bd691d5c261c59`)
and `terrain-stage-cold.json`
(SHA-256 `9a758a455b36a56daa9fcbd0f8b35b8b220e959ef6125f048c0836a6411644eb`).

The rasterizer records consecutive missing chunks on the painter lane and plays
them back on separate CPU surfaces. Batches use up to 12 workers and 8 MiB of
scratch pixels per painter. It publishes and composites the completed chunks
in the same map order. Repeat the single-worker matrix using
`DOTNET_PROCESSOR_COUNT=1` on the same MapProbe command. All 86 serial and
parallel matrix PNG files match byte-for-byte. The short 2× X11 zoom diagnostic reduced the 496-chunk
5K overview transition from 890.32 ms to 222.25 ms. The 364-chunk 4K backing
transition fell from 196.89 ms to 71.56 ms. Both short runs used ten changed
samples after three warmups.

The longer 240 MiB-cache runs exposed repeated eviction at tier and detail-band
crossings. The new per-painter ground caps are 240 MiB for smaller viewports,
352 MiB at 8 million backing pixels, and 544 MiB at 14 million backing pixels.
The cap above 240 MiB is also limited to one sixteenth of the runtime's
available memory. The 96 MiB texture and three bounded viewport raster limits
apply independently. The visible detail thresholds and art sampling stayed the
same.

Three 300-change native runs after 30 warmup changes exercised each 2× size.
Every run prepared zero chunks after its 90th measured change. Peak ground
cache usage was 351.98 MiB at 4K and 543.67 MiB at 5K.

| Backing viewport | 240 MiB draw p95 | Viewport-sized draw p95 | Viewport-sized draw p99 | Input to polled draw p95 |
| --- | ---: | ---: | ---: | ---: |
| 3840 × 2160 | 62.81–70.71 ms | 44.82–47.42 ms | 67.73–69.65 ms | 105.04–106.34 ms |
| 5120 × 2880 | 245.75–253.56 ms | 75.40–94.02 ms | 147.37–163.89 ms | 184.49–207.64 ms |

First-time 2× chunk construction still produced frames above 800 ms at 5K.
The large warm frames and first-time tier builds still exceed the software
timing targets. These reports observe application draw receipts under Xvfb
software rendering. Physical display, GPU and presentation timing remain
pending.

The timing receipts are `bounded-ground-4k-5k-native/native-performance.json`
(SHA-256 `6595ae92efcd89e89423e3b428f64c1d76fbbc1170537b8b001c24129aa56216`),
`bounded-ground-full-native/native-performance.json`
(SHA-256 `5dca625a0a1b582a2594a99f650f4d2e7d35d62fff95d1e40fb0a3215f85196b`),
and `resident-ground-native-4k-5k/native-performance.json`
(SHA-256 `5f5c7080f22d7dc8501f3d2cf476e33e335a8273c5d5450200ce9b0aab6c065f`).

The final offscreen diagnostics matched 54 changed zoom frames and nine
replacement frames to fresh images. The 110-switch test released the old
observer's ground and viewport rasters in every iteration. Its final collected
managed heap was 264,461,240 bytes. Each short replacement series included
three samples, and the 5K replacement p95 was still 801.69 ms.
The receipts are `resident-final-zoom-raster.json`
(SHA-256 `a33dd8bff228872c90ea09b5e402b1ccb303d9fe38c53b8325d4e4dc650596a8`)
and `resident-final-replacement.json`
(SHA-256 `cbde5f5ebd0e3b549f055954d881078acb53a11b885e2a4599a1d7af2bff5547`).

The unit and city build repeated real pan, zoom and dirty-world input at 4K and
5K with 2× backing density. Each short series captured ten changed frames after
three warmups. At 4K the software draw p95 was 64.24 ms for pan, 77.76 ms for
zoom and 73.30 ms for dirty world. At 5K it was 91.64 ms, 216.40 ms and
94.29 ms respectively. These short runs include the first large chunk build
and keep the warm redraw and cold preparation issues open. The receipt is
`object-complete-timing/native-performance.json`
(SHA-256 `614aa274de9cd43d02f4fd36b3abf9cef98b30f3c9d6ad2bfa78c344c378cb53`).

The revised terrain batching repeated the same X11 software diagnostic at 2×.
Each mode captured ten changed frames after three warmups.

| Backing viewport | Pan draw p95 | Zoom draw p95 | Dirty world draw p95 |
| --- | ---: | ---: | ---: |
| 3840 × 2160 | 82.25 ms | 107.53 ms | 93.95 ms |
| 5120 × 2880 | 91.09 ms | 249.46 ms | 128.27 ms |

The largest zoom draws spent 68.29 ms building 364 chunks at 4K and
204.89 ms building 496 chunks at 5K. These short series establish the
current costs and keep the interactive budgets open. The raw receipt is
`terrain-final-timing/native-performance.json` (SHA-256
`066dc812615b3c27c86efb4e08bc43f55eaff47f79e12eecd376267cfd3f2d9e`).

The font and occlusion build repeated the isolated 2× software diagnostic.
The 4K start showed 16,587 of 31,752 tiles, and the 5K start showed 16,781.
Each mode captured ten changed frames after three warmups.

| Backing viewport | Pan draw p95 | Zoom draw p95 | Dirty world draw p95 |
| --- | ---: | ---: | ---: |
| 3840 × 2160 | 61.27 ms | 105.51 ms | 72.00 ms |
| 5120 × 2880 | 98.91 ms | 236.23 ms | 107.04 ms |

The receipt `typeset-timing/native-performance.json` (SHA-256
`69593abf45d8c28e3516748f096e06f73b3f96d88dedb2da616081da0c7a8d26`)
includes real X11 input to polled draw measurements. Interactive budgets
remain open, with physical presentation and GPU evidence pending.

The lifecycle build repeated the full offscreen replacement workload with
three independent 20-frame runs per resolution. The workload clears cached
textures before switching observers. All 180 frames matched independently
rendered current observations.

| Simulated backing viewport | Replacement p95 across runs |
| --- | ---: |
| 1920 × 1080 at 1× | 599.24–654.89 ms |
| 3840 × 2160 at 1× | 809.84–842.99 ms |
| 5120 × 2880 at 2× | 824.12–852.95 ms |

Texture preparation consumed 454–517 ms in the p95 frames. Ground build
time overlaps texture preparation. A further 110 observer switches rebuilt
the retained scroll buffers and held the collected managed heap at
264,721,248–264,723,320 bytes. The receipt is
`renderer-lifecycle-replacement.json` (SHA-256
`29ca74a946da6820eec1278f9fccc790b7f8cd296a54f6ea2610e0515bd352cb`).

Preparing missing ground textures with up to six workers reduced offscreen
replacement p95 in all nine runs. The painter plans the required ground
bindings, decodes and scales those images on separate CPU surfaces, then
publishes them in order within the existing texture budget. Each timed
replacement starts with empty texture and raster caches. The texture
preparation interval in the p95 frames fell to 345–415 ms.

| Simulated backing viewport | Earlier replacement p95 across runs | Batched preparation p95 across runs |
| --- | ---: | ---: |
| 1920 × 1080 at 1× | 599.24–654.89 ms | 516.99–526.10 ms |
| 3840 × 2160 at 1× | 809.84–842.99 ms | 731.59–742.82 ms |
| 5120 × 2880 at 2× | 824.12–852.95 ms | 755.78–760.00 ms |

All 180 frames matched fresh renders exactly. The 110-switch lifetime trial
released the previous observer's viewport and ground rasters and held the
collected managed heap within 264,840,824–264,842,280 bytes. The receipt is
`texture-prefetch-replacement.json` (SHA-256
`38f61b0ee3bc1d4575cee1b30a46c0d04acc6d721eb117302d20d2cf31f88e05`).
Cold replacement remains above the 250 ms target.

The atlas reader now reconstructs the four independent Paeth channels
together and calculates Adler checksums in sixteen-byte groups where
vector instructions are available. CRC validation processes four bytes at
a time. A sequential diagnostic decoded the 368 map bindings in
365.58–382.41 ms after warmup, down from 630.60–637.04 ms. Paeth work fell
from about 300 ms to 113–115 ms.

All 2,105 RGBA digests matched Pillow and the run with
`DOTNET_EnableHWIntrinsic=0`. The full decode-and-hash scan took 4.36 s
with vector instructions, down from 6.56 s on the earlier build.
Both paths passed 459 core tests, including Paeth tie cases and rejection of
an invalid IDAT CRC and an invalid zlib trailer with valid chunk CRCs.
All 213 renderer gallery images matched the preceding build byte-for-byte.
The vector and scalar receipts are `cold-png-release-pixels.json`
(SHA-256 `9fb80fb767ba04bebf7e883e5ff12f9a3c3b42157b4cc5ddee64030dbf82ac6f`)
and `cold-png-release-scalar-pixels.json` (SHA-256
`41925c2386bbd02bf8e3fad931bc01767cee83bda89e094f4130467dfd7ae379`).
The rebuilt desktop also passed 20 Riverbend input and production-command
checks at each backing density. The receipts are `cold-png-riverbend-1x.json`
(SHA-256 `9e7f9744e9cfe8f73dcf449353d383cc707ae72f0f548fc7ca3f49a0dd2a4a31`)
and `cold-png-riverbend-2x.json` (SHA-256
`82c93e24904545c3d40eec9509ff83d866ae29d06cea4efc20ccd6ed45477710`).

Two complete offscreen replacement series isolated the decoder change.
Each build ran three 20-frame trials per size and matched all 180 measured
frames to independently rendered serial images. The receipts show
`OpenCiv2.Formats` as the only changed assembly.

| Simulated backing viewport | Earlier replacement p95 across runs | Faster PNG decode replacement p95 across runs |
| --- | ---: | ---: |
| 1920 × 1080 at 1× | 504.42–568.10 ms | 380.44–537.22 ms |
| 3840 × 2160 at 1× | 723.08–727.99 ms | 557.79–585.25 ms |
| 5120 × 2880 at 2× | 719.08–741.08 ms | 581.43–592.88 ms |

In the p95 frames, texture preparation fell from 356–433 ms to
246–323 ms. The new 110-switch lifetime check released old observer
rasters and kept the collected managed heap at 265,405,496–265,407,856
bytes. The cold replacement budget of 250 ms remains open.
The receipts are `cold-png-before-replacement.json` (SHA-256
`b705b721fea306b516ab995842d5d7de172a62a3a09a63715e336cfea38d6031`)
and `cold-png-after-replacement.json` (SHA-256
`88a925d253b58f2cbe9ff25b7503078164c0262cd607f789510ff21f1e397b38`).

The first full draw at each backing tier prepares the visible unit and
city body images in a bounded queue, using the same observer-scoped body
selection as the painter. Up to six workers prepare distinct images, and
the painter publishes them in request order when the batch fits the
96 MiB texture cache. Ground workers can draw directly from borrowed
textures when all of a batch's images are resident. The painter joins the
workers before it publishes chunks or mutates the texture cache. The
picture-recording path remains available for cache-constrained batches.
All 213 gallery images matched the previous build with the normal worker
count and one processor. The recovery check also exercised an empty body
cache, reuse of the current frame, and texture recreation.
All 459 core tests and 26 native input and lifecycle checks at each of
1× and 2× passed.

The final offscreen trials matched all 180 replacement frames and 180
cold-full frames to fresh renders. Each size ran three 20-frame trials
with recovery between draws.

| Simulated backing viewport | Earlier replacement p95 | Replacement p95 | Cold-full p95 |
| --- | ---: | ---: | ---: |
| 1920 × 1080 at 1× | 380.44–537.22 ms | 185.74–573.72 ms | 191.48–324.71 ms |
| 3840 × 2160 at 1× | 557.79–585.25 ms | 347.79–356.34 ms | 346.41–357.36 ms |
| 5120 × 2880 at 2× | 581.43–592.88 ms | 364.54–369.64 ms | 356.57–362.39 ms |

Each replacement frame prepared 77–79 visible body bindings. Texture
preparation took 91–95 ms in the 5K p95 frames. The first 1080p replacement
trial retained its startup outliers, including the 573.72 ms p95.
All 110 lifetime switches released old viewport and ground rasters.
The collected managed heap stayed within 265,415,424–265,416,880 bytes.
The 250 ms first-frame budget remains open. The reports are
`body-ground-final-replacement.json` (SHA-256
`da0a0fb009368d62d58a8ce767c5e9ac17f7502582079135b262acb1f067b5b6`)
and `body-ground-final-cold.json` (SHA-256
`6f3fe9ddc9431b7af8b3b494ba02d5f1381c0e57e16e06430b43b6641a060d4f`).

The rebuilt desktop ran three warmups and ten measured changes per workload
at 2× in isolated X11. The pan draw p95 was 47.54 ms at 4K and 54.35 ms at
5K. Zoom draw p95 was 106.33 ms and 230.95 ms, and dirty-world draw p95 was
70.89 ms and 77.12 ms. These samples include first-time ground chunk builds.
They record software drawing and polled input receipts. The report is
`body-ground-final-native/native-performance.json` (SHA-256
`645178bd3ad196a8794ff6bc3852ad323c8e9ebe8c1886f2ca5cf791f76d4ab1`).

The ground pass now reuses its draw buffers and lays out missing chunks in
parallel. A frame-local atlas packs compatible premultiplied sprites.
It retains transparent padding and duplicates the edge texels for linear
sampling. Each chunk submits the packed sprites in the established layer,
row and piece order. The staging bitmap and immutable atlas image use at
most 8 MiB together and are released before the ground pass returns.
The existing per-texture path handles batches outside the packing limits.

All 180 cold-full frames and 180 replacement frames matched the unpacked
reference. Each of the three trials per size measured 20 frames. The 5K
cold frames submitted 264 atlas batches each, down from 50,205. The time
to build ground chunks in the 5K cold p95 frames fell from 249–257 ms to
156–163 ms.

| Simulated backing viewport | Cold-full p95 | Replacement p95 |
| --- | ---: | ---: |
| 1920 × 1080 at 1× | 149.73–329.75 ms | 155.25–517.38 ms |
| 3840 × 2160 at 1× | 257.86–268.67 ms | 260.08–286.75 ms |
| 5120 × 2880 at 2× | 261.47–269.09 ms | 258.99–260.94 ms |

The first 1080p trial reached 329.75 ms for cold-full and 517.38 ms for
replacement. The two later trials for each workload were under 163 ms.
All 110 observer switches released old ground and viewport rasters. The
collected managed heap stayed within 265,418,648–265,420,104 bytes.
The reports are `ground-atlas-release-cold.json` (SHA-256
`4574c25bc35f3e3e1ae08b2477db83d9335cebd8a534a40f2ae6b7e00b9e25e3`)
and `ground-atlas-release-replacement.json` (SHA-256
`0cae8cdb605e229840fe76803cfabb51dcdf22053c22b171e13f9b12ef423933`).

The short 2× X11 diagnostic used three warmups and ten measured changes per
workload. Pan draw p95 was 40.86 ms at 4K and 60.71 ms at 5K. Continuous
zoom was 82.18 ms and 239.44 ms, and dirty-world p95 was 73.20 ms and
73.90 ms. The report is `ground-atlas-release-native/native-performance.json`
(SHA-256 `28ba33b197976ebddfdfcecb93e7cc3bd862f6ab02a086a91c0952fe508182d9`).
The timing gates remain open. Physical presentation and GPU timings
remain pending.
The unpacked reference also matched all 54 changed zoom frames and 54
changed world frames in the short offscreen diagnostics. All 213 gallery
images matched the preceding build at normal and single-processor settings,
and all 459 core tests passed.
Linux X11 passed 26 native input and lifecycle checks at each backing
density.

The next cold-frame change prepares visible unit and city textures while
the painter builds ground chunks. It caps the prepared outputs at 16 MiB
and checks the remaining texture-cache capacity before publishing them.
The painter joins the jobs and releases unused images when a draw fails or
the cache is cleared. Texture preparation elapsed time overlaps ground
construction in the detailed samples. The frame time includes both.

| Simulated backing viewport | Cold-full p95 | Replacement p95 |
| --- | ---: | ---: |
| 1920 × 1080 at 1× | 117.15–301.49 ms | 118.38–223.64 ms |
| 3840 × 2160 at 1× | 222.39–226.13 ms | 222.79–223.84 ms |
| 5120 × 2880 at 2× | 223.38–230.41 ms | 233.86–239.03 ms |

All 180 cold-full and 180 replacement frames were pixel-equal to the
unpacked reference. Every 4K and 5K trial met the 250 ms p95 target in the
offscreen software probe. The first 1080p cold-full trial reached 301.49 ms
p95 and included a 920.45 ms frame. Across 110 observer switches, collected
managed heap stayed within 265,484,360–265,485,816 bytes. The current normal
worker and single-processor matrices reproduced all 213 previous PNGs
byte-for-byte. All 459 core tests passed.
Riverbend passed 20 X11 input and asset-admission checks. The current
desktop capture shows the pinned river, route and resource sprites.
The receipt is `pinned-feature-manual-check.json` (SHA-256
`e96d089633bf7e0303a45473bb625ac7efb85a8a31c01fd8d95289f1f7e0d294`)
and the capture is `riverbend-pinned-linux.png` (SHA-256
`bc1057184b5a5ed421edaef9b3422d041b9eae2fb14d9448fabec312091e78fc`).

The reports are `ground-body-overlap-final-cold.json` (SHA-256
`e41fbf2efbfd9469e5790a6ce123c6ff0723973fcc15d45b266de91dac1871ed`)
and `ground-body-overlap-final-replacement.json` (SHA-256
`d0b3f798dff8360f89e7433efe190329f2b7acdc4fee7e8e00a8c47dd2a786a8`).
Warm navigation, physical presentation, and GPU timings remain open.

Large pans that need to repaint the terrain can record the ground and
foreground separately and replay both into the same backing rows. Ground
cache changes stay on the painter thread. Workers draw the existing layer
order and keep the original device transform. Indexed picture bounds include
the antialiasing margin, and padded replay still repairs the band edges.
The per-frame `groundReplayBands` receipt distinguishes this from a retained
terrain scroll and from foreground-only replay. When it is present,
`foregroundMs` includes terrain replay and `terrainMs` includes recording
the ground commands and preparing any missing chunks.

In short 2× X11 trials, ten measured pointer pans followed three warmups.
At a roughly half-world view, median software draw time was 25.71 ms at 4K
and 30.63 ms at 5K. The earlier trials measured 32.71 ms and 42.23 ms.
Pan draw p95 was 34.35 ms at 4K and 49.64 ms at 5K. Zoom draw p95 was
66.50 ms and 224.16 ms. Dirty-world p95 was 94.26 ms and 71.92 ms.
The short runs show that large-view navigation and world changes still
exceed the warm budget. They time drawing and polled X11 draw receipts.
The receipt is `ground-replay-banded-native/native-performance.json`
(SHA-256 `b76be602a43c9c40415f97f13a31d6832e4230ec0934437d22a2faefd5af5e20`).
Presentation and GPU evidence remain open.

The scoped offscreen pan run matched all 2,700 changed frames against
fresh serial draws. Each trial used the 30 warmup and 300 measured changes
in the performance contract. The 5K series used six-band ground replay on ten
frames per trial. Each trial used terrain scroll on 280 frames. The parallel
and single-processor MV matrices both reproduced the 213 previous images
byte-for-byte. The replay test checks exact pan pixels at a fractional
sampling phase, including when the parallel run's scratch allocation is denied.

| Simulated backing viewport | Pan p95 | Pan p99 |
| --- | ---: | ---: |
| 1920 × 1080 at 1× | 10.69–12.05 ms | 25.11–28.25 ms |
| 3840 × 2160 at 1× | 27.61–30.04 ms | 66.04–72.16 ms |
| 5120 × 2880 at 2× | 35.51–38.39 ms | 42.59–43.19 ms |

The report is `ground-replay-final-pan.json` (SHA-256
`edad22323b825de79c2c9ca12ad30e71cbc09d607288ef45262f3d4134e91dbf`).

The navigation replay reconstructed the authored world at all 40 recorded
4K and 5K X11 pan and zoom cameras. World pixels matched independent serial
draws. All twenty pointer pans exercised banded ground. The replay checks
the world view with default transient state. Actual window and input behavior
are covered by the X11 driver and the native interaction matrix.
The replay report is `ground-replay-navigation-diagnostic.json` (SHA-256
`bd3f774b4a6a7e4e010e56927f8863fd58c4981ecaa9ccdbd512bb26facf9143`).

The full X11 half-world pan runs used actual pointer input, with 30 warmup
changes and 300 measured changes per trial. At each backing size all three
trials used banded ground replay on every measured pan. The 1080p and 4K
scenes started with 16,587 distinct tiles in view. The 5K scenes started
with 16,781 of the 31,752 tiles.

| X11 software backing viewport | Draw p95 | Draw p99 | Input to polled draw p95 |
| --- | ---: | ---: | ---: |
| 1920 × 1080 at 1× | 13.07–13.70 ms | 15.80–17.13 ms | 55.03–55.26 ms |
| 3840 × 2160 at 2× | 22.13–24.35 ms | 25.26–29.79 ms | 78.76–80.38 ms |
| 5120 × 2880 at 2× | 32.61–33.01 ms | 34.96–38.76 ms | 131.67–132.16 ms |

The 4K and 5K draw p95 values remain above 16.7 ms. The 5K draw p99 values
also exceed 33.3 ms. The reports are
`ground-replay-pan-native-1080/native-performance.json` (SHA-256
`63d50ea57960da49625078b10e8dcb82f06d15a9085a65ec6c87f5178c4f3a4e`) and
`ground-replay-pan-native-large/native-performance.json` (SHA-256
`39c5453197f7a88d3849f08093aa793b78091dcacafef75ee099c89fd8c446c9`).
All 2,700 recorded cameras matched fresh serial world pixels in the replays.
The reports are `ground-replay-pan-native-1080-compare.json` (SHA-256
`1c31f94221f1ae93550d7a9c5667b80338604b621a72f4a138899351610a061d`) and
`ground-replay-pan-native-large-compare.json` (SHA-256
`58bd7c4c07290a9c9325eebcd1812001efac4a3c1838c78e914b9cd0a86527ec`).

At 1080p with a 2× backing scale, the calculated half-world zoom is 0.1786.
The native driver uses the 0.25 limit and records the actual view. Its ten
changed frames followed three warmups with 8,610 of 31,752 tiles visible
at the settled start. The report is `min-zoom-1080-native/native-performance.json`
(SHA-256 `fb7199607bc0a39cf0242516e7ad50300ea6870faa43930a17735081fe0ee8a3`).
All 459 core tests passed. X11 repeated 26 input and lifecycle checks at
each density. The reports are `ground-replay-final-1x/native-gallery.json`
(SHA-256 `da643ba23d749923d7ae82c31cbf00982add082c919b63dd5411497044b42c13`)
and `ground-replay-final-2x/native-gallery.json` (SHA-256
`200c27b982fd768dadcde2cee571cb001e7df6fca75d1d834d90132d6257f4a3`).

Ground preparation now batches separated cache misses across resident chunks.
It retains the original draw and cache publication order and the 8 MiB limit
on parallel chunk scratch pixels. A large dirty patch also replays foreground
bands within the padded damage rectangle. The parallel and single-worker
matrices check a patch containing two separated changed chunks, and the
parallel test denies the optional band surface to check the serial fallback.
Full redraws of a retained viewport can replay ground and foreground together
during zoom and world changes.

In the scoped offscreen dirty-world diagnostic, 54 changed frames matched an
independent full draw. At 4K and 5K, each update prepared four chunks in
parallel. Median ground build time fell from 21.32 and 20.84 ms to 6.46 and
6.28 ms. End-to-end frame p95 fell from 52.94 to 35.53 ms at 4K and from
50.89 to 26.56 ms at 5K. The receipts are `zoom-dirty-baseline-scoped.json`
(SHA-256 `e37605d0e669782aaf039cc2d4e1b9d342b03554b4b401b843559c1d3a40360d`)
and `zoom-dirty-bands-scoped.json` (SHA-256
`98641e24beb87164feca078e3692f23b3531576a968baca9144343a8c1e7bac0`).

In the short 2× X11 software diagnostic, each size and mode used three
warmup changes and ten measured changes. Median zoom draw times fell from
26.91 to 14.95 ms at 4K and from 81.68 to 48.01 ms at 5K. Zoom p95 rose from
66.95 to 75.53 ms and from 204.01 to 222.11 ms, with first-time chunk builds
still contributing to both tails. Dirty-world draw p95 fell from 69.31 to
50.35 ms at 4K and from 68.00 to 53.48 ms at 5K. These receipts are
`zoom-dirty-baseline/native-performance.json` (SHA-256
`1fb25c57e205995798f01368e17eb5dd641ccc8af92deb8af06e644f6cf5e91e`) and
`zoom-dirty-final-native-diagnostic/native-performance.json` (SHA-256
`e8d7ebcea949b4fb6e0b93cbf205da6db2c0f469bb2a4384bd2223a0f6e4d46b`).

The observed showcase captures the authored update point, revision and
admitted explosion phases from the displayed draw. A replay reconstructs
the published revision and compares its world pixels with a fresh serial
renderer, including the recorded effects. All 40 diagnostic cameras and
revisions matched. The receipt is `zoom-dirty-final-diagnostic-replay.json`
(SHA-256 `ead7cab98450f6e6c3ca1ddd1aa9c13462a98dc325aa3baa24593a37ff3550ad`).

The full X11 zoom and dirty-world runs recorded 5,400 changed draws. Each
size and mode ran three trials with 30 warmup changes and 300 measured
changes each. Zoom crossed the three detail bands using a real pointer
gesture. The world changes used the showcase's observed-world button.

| X11 software backing viewport | Workload | Draw p95 | Draw p99 | Input to polled draw p95 |
| --- | --- | ---: | ---: | ---: |
| 1920 × 1080 at 1× | Continuous zoom | 10.70–11.78 ms | 13.92–14.56 ms | 53.72–54.41 ms |
| 1920 × 1080 at 1× | Dirty world | 15.02–17.52 ms | 19.27–21.74 ms | 55.72–55.78 ms |
| 3840 × 2160 at 2× | Continuous zoom | 24.30–24.92 ms | 27.26–31.42 ms | 79.84–80.59 ms |
| 3840 × 2160 at 2× | Dirty world | 29.32–30.19 ms | 30.96–35.38 ms | 106.84–107.73 ms |
| 5120 × 2880 at 2× | Continuous zoom | 34.76–35.96 ms | 73.82–81.76 ms | 131.69–132.27 ms |
| 5120 × 2880 at 2× | Dirty world | 39.97–40.38 ms | 41.10–42.56 ms | 135.94–157.16 ms |

One 1080p dirty-world trial and all 4K and 5K trials exceeded the warm draw
p95 target. The slowest zoom samples built 104 chunks at 4K and 152 chunks
at 5K on first use of the 2× terrain tier. The largest measured draws were
412.31 and 527.87 ms. The 4K trials stopped preparing chunks after the first
60 measured zoom changes, and the 5K trials after 51. These software timings
leave physical presentation and GPU execution pending. Raw reports are
`zoom-dirty-final-native-1080/native-performance.json` (SHA-256
`bdbff4db8b64d16c58f3fcb35b8c9bfc147ed3107a9ff6c69e0ff78c0226d1e7`) and
`zoom-dirty-final-native-large/native-performance.json` (SHA-256
`3e3901ef24deddad5cc95f97a34a94729718136439b6ea0af2c883f1548f80ab`).
All 1,800 recorded 1080p cameras and revisions matched fresh serial world
pixels. The replay included 4,499 recorded explosion instances. Its receipt is
`zoom-dirty-final-native-1920-compare.json` (SHA-256
`d44e119e0cdd159d909d2e77f99568e2f3ac3707b53affa060fc5a850bbd0d1c`).
The 4K and 5K replays each matched all 1,800 draws from the large X11 receipt,
including 2,989 and 1,903 explosion instances, respectively. The replay inputs
preserve the corresponding captured series and the original receipt hash.
The reports are `zoom-dirty-final-native-3840-compare.json` (SHA-256
`836c003377c6841eb2a269c4de570e6d9b37e6794db430283462ebf025d25566`) and
`zoom-dirty-final-native-5120-compare.json` (SHA-256
`f9b4fd40ff1b141f4ac8cd91f874ff0ba1323ec3223072895592c58abf9007e3`).

Both worker settings reproduced all 213 gallery PNGs. The rebuilt desktop
passed 45 native showcase windows at 1× and 26 native input and lifecycle
checks at each of 1× and 2×. Visual review of the current MV29 window checked
its connected rivers and routes, foreground overlap, captions, and overview.
Riverbend passed 20 checks against the existing production command and
asset-admission paths. All 459 core tests passed.

- `zoom-dirty-final-gallery-1x/native-gallery.json`, SHA-256 `901098091a720f88090e4b523f69e59cf07710bc91d3e3f1390f6889ca326436`
- `zoom-dirty-final-gallery-2x/native-gallery.json`, SHA-256 `f2fe6df40e8f3dceb3130775d3e1c9e34e3ec39d3025917d349ae595215491c2`
- `zoom-dirty-final-gallery-1x/MV29.png`, SHA-256 `e6b3532ebee001af90ead03867cee75e0ca34fdd5b0c395dfa334b0fbe33ad2f`
- `zoom-dirty-final-riverbend.json`, SHA-256 `fcf70e975274611a11fe437d6aa4de16740c1d2250793785a8bab32015d59247`

An aligned terrain path on Linux x64 with AVX2 uses the frame-local packed
atlas and the existing ordered placements. The worker blends one-to-one
integer placements into its fresh opaque 32-bit chunk. An independent
diagnostic of the pinned Skia renderer confirmed the premultiplied destination
rounding on 65,536 generated pixel pairs spanning every alpha value. Format,
stride, color space, and integer placement checks leave other draws on the
Skia atlas path. The atlas lifetime and chunk scratch limits are unchanged.

In the 18-change offscreen zoom diagnostic, the 4K 1× frame entering the
2× terrain tier built 91 chunks. Its build time fell from 295.80 to 39.11 ms.
All 91 used aligned compositing, and the complete draw fell from 317.54 to
59.32 ms. The comparison verified all 54 candidate frames against fresh
pixels. The short 5K series used already-prepared chunks in the measured
samples. The baseline is
`integer-ground-baseline-zoom.json` (SHA-256
`78c43fb5566c12ac0bb1485ce2364d2c1233b9afe2b7ff489a7c99b9622e7ff0`). The
aligned trial is `integer-ground-vector-zoom.json` (SHA-256
`8df7ad7bd2a4a5e5f894d12994060c4c704eb7ae097f22aebb5db27f4010e182`).

The full X11 rerun used 30 warmup and 300 measured pointer zooms in each of
three trials per size. At the first 2× terrain crossing, all 104 new 4K
chunks and all 152 new 5K chunks used aligned compositing. The 4K build
time fell from 346.78–384.05 ms to 71.73–76.41 ms. The 5K build time fell
from 414.60–499.99 ms to 33.42–36.19 ms.

| X11 software backing viewport | Earlier first 2× terrain draw | Aligned first 2× terrain draw | Zoom draw p95 | Zoom draw p99 |
| --- | ---: | ---: | ---: | ---: |
| 1920 × 1080 at 1× | — | — | 10.17–11.28 ms | 13.85–15.32 ms |
| 3840 × 2160 at 2× | 373.87–412.31 ms | 99.11–104.42 ms | 24.75–26.57 ms | 33.69–38.55 ms |
| 5120 × 2880 at 2× | 447.39–527.87 ms | 66.67–69.30 ms | 34.56–35.38 ms | 53.12–69.30 ms |

The largest 4K draw was 195.85 ms, including 167.35 ms of texture
preparation and zero new chunks. Warm large-viewport draws still exceed
the budget. Physical presentation, GPU timing and deferred platforms remain
open. The X11 receipts are
`integer-ground-final-native-zoom-1080/native-performance.json` (SHA-256
`108ffef985fb6981b9dc519ea61bb55b971e8b472f363976d2edfd517337a580`) and
`integer-ground-final-native-zoom-large/native-performance.json` (SHA-256
`191d5b00d4293c8bf7350f594d003ab8315ecec2ee11b0c6f6a009473d58b151`).

All 2,700 recorded zoom cameras matched the independent fresh renderer.
The 40 short pan and dirty-world draws at 4K and 5K also matched, including
49 explosion instances. Each short series used three warmup and ten
measured changes. Pan and dirty-world draw p95 were 31.96 and 46.73 ms at
4K, and 41.82 and 57.00 ms at 5K.

- `integer-ground-final-native-zoom-large-compare.json`, SHA-256 `2f54c7c34c252a3467b950e4f2857255abcd8ce727d1985126610a8648dc2d9e`
- `integer-ground-final-native-zoom-1080-compare.json`, SHA-256 `591b8cb8db48fa72e4c094fc4a30e308b72f3ae24f570ee37f78636e6f28a00c`
- `integer-ground-final-native-dirty-pan/native-performance.json`, SHA-256 `6515f1d645f8419eb5d2fca2b8be512df769a7e2a6ad3b8020d9c2194eb54290`
- `integer-ground-final-native-dirty-pan-compare.json`, SHA-256 `2e1dba6f443498d337329cd8c4f680fd9eb53e25a3d83bfa02fcbc93ab2bc73f`

The normal, AVX2-disabled and single-processor matrices each reproduced
all 213 previous PNGs byte-for-byte. A recovery test checks that a packed
Linux AVX2 frame exercises aligned compositing and that the reference uses
the separate unpacked Skia path. The rebuilt X11 desktop passed 45 showcase
windows, 26 input and lifecycle checks at each density, and 20 Riverbend
production input checks. All 459 core tests passed.

- `integer-ground-final-gallery-1x/native-gallery.json`, SHA-256 `89902ebb57a5bf0f796a9f6beab7401f643e497cbe5567889f40e38407d5225b`
- `integer-ground-final-gallery-2x/native-gallery.json`, SHA-256 `f98c2872fbf4133896359dfcb99e5b4e56697a04718c3e1c2d2828eb322b61e9`
- `integer-ground-final-riverbend.json`, SHA-256 `c6b423ca45b72b0a31e40841fa689cf70b027cd271ad74071d6de00f4fa9762a`

The aligned build also repeated the full offscreen first-frame and replacement
protocol. All 360 measured frames matched independent fresh draws. Each size
and mode ran 20 measurements per trial and three trials.

| Simulated backing viewport | Cold-full p95 | Replacement p95 |
| --- | ---: | ---: |
| 1920 × 1080 at 1× | 113.19–402.12 ms | 116.70–237.46 ms |
| 3840 × 2160 at 1× | 108.29–142.09 ms | 108.44–136.01 ms |
| 5120 × 2880 at 2× | 120.26–123.06 ms | 126.78–146.23 ms |

The first 1080p cold trial missed the 250 ms target and reached 679.01 ms
in its slowest frame. The first 1080p replacement trial reached 807.08 ms
in its slowest frame. The 110 observer switches released prior ground and
viewport rasters, with a collected managed heap of 265,512,160–265,513,616
bytes. Raw receipts are `integer-ground-final-cold.json` (SHA-256
`95586e85597b78a1348860d62e7ca29e610758419d5498b3e7e8bcc4193c66f4`) and
`integer-ground-final-replacement.json` (SHA-256
`6d89cde3ebcfe979f951157eabbb4cbb7eb045da3b73b14f0046594c07b0199f`).

The high-resolution texture trace found 26 demand raster loads on the
first measured 4× texture draw at 4K and 20 at 5K. The opening view had
previously used that tier for a smaller set of bodies. The trace recorded
zero body batch budget deficit and less than 36 MiB of cached pixels at
both transitions. Full redraws now prepare actual misses among the currently
visible, admitted bodies. The six-worker and 96 MiB cache limits are unchanged.
A same-tier pan regression draws disjoint unit variants at the same scale,
checks preparation on both views, and compares the result with a fresh draw.

Packed tint rasters use vector arithmetic when available. The tint tests
check the same integer opacity rule for opaque and translucent colors, source
immutability and cache reuse. The focused X11 trace used 30 warmup changes
and 36 measured pointer zooms. At the 4× texture crossing, batching prepared
24 bodies at 4K and 19 at 5K. Demand raster loads fell to two and one,
respectively. Draw time fell from 187.13 to 75.42 ms at 4K and from 93.00 to
60.96 ms at 5K. The body-only trial measured 76.75 and 47.49 ms. The tint
trial reported 4.75 ms of tint work at 4K, down from 12.48 ms in the
body-only trial. The 5K tint samples were 6.83 and 5.18 ms, respectively.
These are individual diagnostic crossings. The sustained workloads use
the full sampling protocol separately.

- `body-tier-trace/native-performance.json`, SHA-256 `72fd405d06985e011f2013e8ee4b13c1d1f51c29c3582053e11742090ef79b09`
- `body-visible-trial-native/native-performance.json`, SHA-256 `718b8e30b3dda0461d7bb75043c4f1af56272ffc4ec17381ee5c85208de46a50`
- `body-tint-trial-native/native-performance.json`, SHA-256 `6fe2b394ae43b194e843c4584311c3f7143533e79394a3fe35f88dd9caaafe1f`

Foreground replay uses up to twelve bands on backing regions of at least
eight million pixels. Each band and its edge-repair strip use disjoint
integer clips on their respective backing rasters. The paired X11 pan trial
used 30 warmup changes and 72 measured changes at each size. Median draw
time fell from 21.47 to 17.22 ms at 4K and from 31.52 to 23.70 ms at 5K.
Draw p95 fell from 25.28 to 23.62 ms and from 38.40 to 30.64 ms, respectively.
All 213 matrix PNGs matched the previous renderer. The records are
`body-tint-warm-baseline/native-performance.json` (SHA-256
`a7987a65a3014d9a1be995d896953f0348a6efc6c7e2def259269b43c7f1e2cb`) and
`body-tint-warm-bands/native-performance.json` (SHA-256
`180a162dd95ee0fa3b31f1349b211b47d610319737406adc2bea6730b21a4ae8`).

Full X11 trials used three runs per size and mode, each with 30 warmup
changes and 300 measured changes. All measured large pan and zoom draws
used twelve bands. The 4× texture crossing took 65.54–79.08 ms at 4K and
48.79–51.04 ms at 5K. Each crossing prepared 24 or 19 visible bodies in
parallel, respectively.

| X11 software backing viewport | Workload | Draw p95 | Draw p99 | Input to polled draw p95 |
| --- | --- | ---: | ---: | ---: |
| 1920 × 1080 at 1× | Pan | 12.10–12.85 ms | 15.24–17.67 ms | 54.64–55.08 ms |
| 1920 × 1080 at 1× | Continuous zoom | 10.45–10.95 ms | 13.97–15.10 ms | 54.05–54.84 ms |
| 1920 × 1080 at 1× | Dirty world | 15.05–16.05 ms | 18.45–20.04 ms | 55.73–55.89 ms |
| 3840 × 2160 at 2× | Pan | 18.38–18.94 ms | 22.59–23.45 ms | 78.24–81.08 ms |
| 3840 × 2160 at 2× | Continuous zoom | 21.29–21.69 ms | 32.87–40.37 ms | 77.65–79.86 ms |
| 3840 × 2160 at 2× | Dirty world | 19.45–21.26 ms | 21.32–26.50 ms | 106.34–106.83 ms |
| 5120 × 2880 at 2× | Pan | 25.96–26.65 ms | 30.33–31.32 ms | 131.51–132.26 ms |
| 5120 × 2880 at 2× | Continuous zoom | 26.11–26.81 ms | 48.79–51.04 ms | 131.33–131.76 ms |
| 5120 × 2880 at 2× | Dirty world | 28.05–28.80 ms | 31.22–31.30 ms | 134.61–135.17 ms |

All 1080p warm draw trials met both draw budgets. Large warm draws
still exceed the 16.7 ms p95 target. Physical display and GPU timings
remain pending. Receipts are
`body-tint-final-native-1080/native-performance.json` (SHA-256
`9d600882fbb66092e14744ce120c2e3d053f7fe3b6c1f8e794dd9741395807f9`) and
`body-tint-final-native-large/native-performance.json` (SHA-256
`38609debf007a193a86863aa0f69aa45a7709014f7357e0dfe753bc2c6fd4d3d`) and
`body-tint-final-native-dirty/native-performance.json` (SHA-256
`7a59cf468f7d281626c01cec79af2e56bae7802c8a8436660863f690563ec225`).
The normal, AVX2-disabled and single-processor matrices each reproduced
all 213 previous images. All 459 core tests passed.

The 1080p replay reproduced all 2,700 measured frames. The first 5K zoom
replay found seven pixel differences immediately above a 240-pixel foreground
band boundary. The repair strip for large viewports now extends 16 pixels
past either side of each boundary. A dedicated matrix case covers the two
recorded cameras around the mismatch. All 85 frames in the focused 5K replay,
including the original failing frame, matched the fresh serial renderer.
The full replays then matched all 3,600 native pan and zoom frames at 4K
and 5K. The comparison receipts are
`body-tint-seam-4k-navigation-replay.json` (SHA-256
`6c0e7db48f75411fb61887c642b18532f9496d326e92d7651e4a893231951843`) and
`body-tint-seam-5k-navigation-replay.json` (SHA-256
`e7de1137d42d27dd069b25a540c39fe340a0302322dfc9dcc584fcd65df41737`).
The 1,800 dirty-world frames at these sizes also matched with the wider strip.
Their comparison receipts are `body-tint-seam-4k-dirty-replay.json`
(SHA-256 `29afc355b656f4da7eb9ea7b8c72d1098f94d8ec882bb22614fecefb67398457`)
and `body-tint-seam-5k-dirty-replay.json` (SHA-256
`590aaaf5077319444f43723f1dab6e8c0e9466167e203047a6bcf3a9b9d2d8bc`).
The large timing figures above were recorded with the earlier repair strip
and remain provisional.

For a full repaint with ground replay, each worker now initializes its own
backing rows. Padded seam replays initialize their own rows as well, which
removes the initial copy from the viewport into the seam buffer. The fallback
still paints the entire frame when the optional scratch allocation fails.
The matrix matched all 213 preceding images, including a separately retained
5K frame that stayed pixel-identical after the next draw. The focused 5K
sequence also matched all 85 frames with the new clear path.

An isolated 2× X11 comparison used the same pointer sequences with 30
warmup changes and 72 measured changes per case. Both builds used the wider
seam repair. The change avoids one serial viewport fill and the initial
seam copy. The table records draw times in milliseconds.

| Backing viewport | Workload | Before p50 / p95 | In-place clear p50 / p95 |
| --- | --- | ---: | ---: |
| 3840 × 2160 | Pan | 18.19 / 25.35 | 17.07 / 23.93 |
| 3840 × 2160 | Continuous zoom | 21.30 / 38.56 | 19.87 / 30.18 |
| 5120 × 2880 | Pan | 25.75 / 32.57 | 22.69 / 29.45 |
| 5120 × 2880 | Continuous zoom | 25.61 / 52.81 | 23.05 / 46.99 |

This diagnostic includes the first texture and chunk transitions seen within
its short zoom sequence. Full-length large-viewport timing on the updated
renderer and the 16.7 ms warm p95 gate remain open. Receipts are
`parallel-clear-isolated-baseline/native-performance.json` (SHA-256
`1673a01268f36d5e6c6ca22b95a4c49e9a8882a0e33b5c1484f08d18816b3b27`) and
`parallel-clear-isolated-candidate/native-performance.json` (SHA-256
`82240ea45c2401c4bd0213d06f0a04cb957567b23a1cb25f755d4d80167e39e0`).
All 288 candidate draws matched fresh serial frames in
`parallel-clear-isolated-replay.json` (SHA-256
`ec2eaece5d43743bf1ba3b131478b836d5c4c08b04da8e88c408b7726890307f`).

The subsequent full X11 run with the in-place clear recorded 8,100 changed
frames, with 30 warmup changes and 300 measured changes per trial. The
capture predates the half-size terrain sampling change.

| Backing viewport | Workload | Draw p95 across trials | Draw p99 across trials |
| --- | --- | ---: | ---: |
| 1920 × 1080 at 1× | Pan | 11.59–14.00 ms | 13.65–18.22 ms |
| 1920 × 1080 at 1× | Continuous zoom | 9.96–10.40 ms | 14.25–21.88 ms |
| 1920 × 1080 at 1× | Dirty world | 14.75–16.96 ms | 18.31–21.69 ms |
| 3840 × 2160 at 2× | Pan | 18.06–19.81 ms | 23.49–24.06 ms |
| 3840 × 2160 at 2× | Continuous zoom | 21.40–22.22 ms | 25.77–30.34 ms |
| 3840 × 2160 at 2× | Dirty world | 20.01–21.65 ms | 23.37–26.22 ms |
| 5120 × 2880 at 2× | Pan | 25.43–34.42 ms | 29.33–38.15 ms |
| 5120 × 2880 at 2× | Continuous zoom | 26.12–35.82 ms | 42.93–57.35 ms |
| 5120 × 2880 at 2× | Dirty world | 35.58–36.17 ms | 38.67–39.64 ms |

The raw `parallel-clear-full-native-1x/native-performance.json` receipt
has SHA-256 `f1b83ef8eb5a49548a37d0826c88c158d3279bd7d647ddf3f31fd191a7613d85`.
The `parallel-clear-full-native-2x/native-performance.json` receipt has
SHA-256 `cf9c627a751501b292d982758878b4887b1228cb627bc2d6dde53d3042dff352`.
The large warm draws exceed the p95 target. The 1080p dirty-world run
also exceeded the target in one trial. Fresh-frame replay matched all
2,700 1080p draws and all 1,800 4K pan and zoom draws. The comparison
receipts are `parallel-clear-full-1920-all-replay.json` (SHA-256
`3702bde1d24fad9123cc60e4f543eee1673a9bce9d3f278f0da02efcbdeed5e3`) and
`parallel-clear-full-3840-navigation-replay.json` (SHA-256
`7cd945a94f29bc2623d2deb849811fbe278197007b1069f1ad7836d65e526c1b`).
All 900 4K dirty-world and 1,800 5K pan and zoom draws also matched. Their
comparison receipts are `parallel-clear-full-3840-dirty-replay.json` (SHA-256
`f3ce273d7d9c6dfed5c0441126fce32b343e28ce03cb4b2f1046ff5230101a97`) and
`parallel-clear-full-5120-navigation-replay.json` (SHA-256
`fd71f4126d5bfde6218085bd6a9a3f4916616312a5c6f98d7c0bbeae0e2a7155`).
The remaining 900 5K dirty-world draws also matched, completing the
8,100-frame comparison. The last receipt is
`parallel-clear-full-5120-dirty-replay.json` (SHA-256
`b9349ea86dabc89eeed92d0e83f12657062dafc33bf02b294c804a483a34bec0`).

The flat-edge X11 trial measured one physical pixel of movement and then a
sustained diagonal drag on the 252 × 252 Mixed board. It reached the flat
camera limit in 14 of 18 measured frames at each high resolution. All 36
draws matched the fresh serial renderer. The edge workload started with
582 visible tiles at 4K and 485 at 5K. The same board showed 13,774 and
13,871 tiles before staging the pan at the edge. The capture and replay are
`half-ground-flat-edge-smoke/native-performance.json` (SHA-256
`2888052d68e69e863eb13a97d2be84e2514c5f517c06c5af769cf9cf87c241ec`) and
`half-ground-flat-edge-smoke/replay.json` (SHA-256
`06e1d970d2490eb84a4876ea001c8b85a8394460b6bc4dfbdb39d2fbf95376fa`).
The separate flat half-world trial matched all 72 pan, continuous-zoom and
dirty-world frames at 4K and 5K. It used the full authored world and started
at the 13,774- and 13,871-tile views. The diagnostic capture and replay are
`half-ground-flat-half-smoke/native-performance.json` (SHA-256
`3b539c027de05df6381ef47ee7488843a54ad2202a4e71e982dbf42cddd67aa9`) and
`half-ground-flat-half-smoke/replay.json` (SHA-256
`23e678f0e632e9c6b4dee3f49671cee8ec3f9b0cc4a1ec17fc242637ff0bcba3`).

At the half-size terrain tier, integer-positioned sprites with exact source
registration can use the aligned compositor after one Skia linear sample per
binding. Other registrations still use the regular atlas path. A synthetic
check of 8,192 premultiplied pixels over four backgrounds matched direct
sampling. The stock and replacement matrix reproduced all 213 images, and
an independent ground recovery case checked the half-size dispatch and pixels.
The matrix receipt is `half-ground-trial-matrix/matrix.json` (SHA-256
`0b7bf1607bdb03b1e85ba53afd8c53c3bdf85da5ce6326adaf2d42a81215db27`).

Each paired 1080p X11 test used three 20-reload trials. The aligned compositor
built all 264 terrain chunks on each measured draw. The table records draw
times in milliseconds.

| Trial | Before p50 / p95 | Half-size sampling p50 / p95 |
| --- | ---: | ---: |
| 1 | 181.13 / 245.32 | 192.09 / 256.00 |
| 2 | 199.69 / 223.63 | 169.93 / 200.51 |
| 3 | 201.27 / 357.71 | 175.14 / 225.55 |

Receipts are `half-ground-recovery-baseline/native-performance.json`
(SHA-256 `11c2d0572a56da0411176ff6404972d99bbf7a56f965ab64a06b0afe00adc074`)
and `half-ground-recovery-candidate/native-performance.json`
(SHA-256 `91a2e0632d762f2f8d46ae98b3238c6b37ceb4b7077bcddd0388fe01a89c287a`).
All 60 draws in the candidate capture matched fresh serial frames.
The comparison is `half-ground-recovery-candidate/replay.json` (SHA-256
`ffa120152c2014ef37bc9c226e4b446c699dd3a56160dc1a94f72054fe8b240f`).

The PNG decoder now reconstructs Sub and Up rows with byte vectors. Sub
accumulates each of the four color channels independently and carries the
last decoded pixel into the next vector. Scalar fallback and full validation
remain available. In two alternating runs, decoding and hashing all 2,105
bindings took 4.42 and 4.26 seconds before the change and 4.14 and 4.19 seconds
after it. All hashes matched across builds, the scalar run, and independent
Pillow decoding. The latest vector and scalar receipts are
`fast-png-candidate-2.json` (SHA-256
`6786473444538b24f7da8b815f5b63d844f4386594ae796bcefe43da0a54c621`) and
`fast-png-candidate-scalar.json` (SHA-256
`42883fd73909203ff2afb9aa9319a1d0b69bc8e38377b0efda32017b164e3a34`).

The combined renderer draw matched fresh output on all 180 native resource
reloads. All three 1080p draw trials were below 250 ms at p95. Large-view
draws still exceeded 250 ms in some trials. Input-to-receipt times include
the polling interval. Physical presentation timing remains pending.

| Backing viewport | Draw p95 across trials | Input-to-receipt p95 across trials |
| --- | ---: | ---: |
| 1920 × 1080 at 1× | 215.51–225.39 ms | 264.43–265.97 ms |
| 3840 × 2160 at 2× | 207.82–302.51 ms | 240.16–339.64 ms |
| 5120 × 2880 at 2× | 240.74–272.77 ms | 320.72–340.57 ms |

| Combined recovery receipt | SHA-256 |
| --- | --- |
| `half-ground-decoder-native-1x/native-performance.json` | `32e0a1f608d29391e631dc2de042bd7ae53fe41c57f750ab4821ebef57298b91` |
| `half-ground-decoder-native-1x/replay.json` | `19b778d00a98ecfdd7f64f4ee973065c5f307386e79df688e8eb9841e0f1e796` |
| `half-ground-decoder-native-2x/native-performance.json` | `5d13226665f8a5509e6f43341d44dd7a8826743b8b3d2ec7da519cb02cf2ec43` |
| `half-ground-decoder-native-2x/replay.json` | `2dd3cc530d6ca8cae0bc3fa1894ad396edebf884775dfe16bd43a8792502112f` |

The combined build reproduced all 213 images with normal settings, with
hardware intrinsics disabled, and with one processor. The headless suite
passed all 459 tests.

At 2×, the theme-switch capture reached the X11 window before the overview
displayed the selected marker. The check now waits for that marker in the
window pixels before capturing each theme. Three repeated switches and
the full 2× input and lifecycle suite passed. The same build passed the
45-window 1× gallery, 22 Riverbend input checks, and three virtual
mixed-density checks.

| Updated native evidence | SHA-256 |
| --- | --- |
| `half-ground-final-normal/matrix.json` | `40723b6b4f564a254ec2fe42d0ac5913030309c13d824561a8d076a2a3e0ce11` |
| `linux-ci-half-ground/map-gallery-1x/native-gallery.json` | `068de78350bb70f9d2abf2590dc223a3d4558f4597324794780b1459244f33e1` |
| `half-ground-gallery-final-2x/native-gallery.json` | `5baee4ed66750db3d067b6a15ab008ae8fca9c7981558678eb8cbbde9d073ecd` |
| `half-ground-gallery-final-mixed/native-gallery.json` | `8e2f4efed5577ad5d824bd20da43779cc520c04a246a95bd74e90f621acf8434` |
| `half-ground-riverbend-final.json` | `f7ea2f772cdc104daa20a62975d4910189b6b059760a500b560a688d40aa5551` |

Large full redraws reuse the terrain scroll scratch backing for seam repair.
Each seam initializes its own rows. A 5K test alternates zoom and terrain
scroll, checks the three-backing limit, compares the pixels with a separate
renderer and verifies that recovery releases all three surfaces. All 213
matrix images matched, and all 432 measured frames from the paired candidate
run matched fresh output.

The paired 2× X11 diagnostic used 30 warmup changes and 72 measured changes
per case. The table records draw times in milliseconds.

| Backing viewport | Workload | Before p50 / p95 | Shared scratch p50 / p95 |
| --- | --- | ---: | ---: |
| 3840 × 2160 | Pan | 17.92 / 24.80 | 16.75 / 23.81 |
| 3840 × 2160 | Continuous zoom | 20.37 / 32.46 | 14.60 / 32.93 |
| 3840 × 2160 | Dirty world | 19.06 / 25.96 | 17.82 / 25.61 |
| 5120 × 2880 | Pan | 22.65 / 28.91 | 22.28 / 31.91 |
| 5120 × 2880 | Continuous zoom | 23.58 / 44.77 | 21.83 / 47.43 |
| 5120 × 2880 | Dirty world | 23.93 / 28.98 | 23.53 / 28.15 |

The candidate's median 5K pan spent 16.36 ms replaying bands, 1.16 ms recording
commands, 0.03 ms setting up the backing and 0.36 ms repairing the seams.
Timing tails varied between trials. The large-view warm budget remains open.

| Shared scratch receipt | SHA-256 |
| --- | --- |
| `seam-scratch-matrix/matrix.json` | `696be6f3dccd341804af3201eb4dcc34cc139da3ebdedd18c0351fcdc01bfa77` |
| `seam-scratch-native-baseline/native-performance.json` | `4fb5e34bdeb2a7bb2c871a79ceaecd1e7e54ad2616a56b394e6db9c03c61e3cf` |
| `seam-scratch-native-candidate/native-performance.json` | `62e04aa537182dee7fff78653d3a7d6dc3dd50e57d3f9d41adaf40c8bc8f6915` |
| `seam-scratch-native-candidate/replay.json` | `0d5b2a86f6bc9cf37cdf6f776d80705242327882ea9251383aa67fd0f6a25656` |

The final Linux software X11 run covered 45 gallery windows, 26 input and
lifecycle checks at each of 1× and 2×, and 22 Riverbend checks. Riverbend
pointer inspection identified the river and road crossing and the forest
resource. A stock desktop capture matched the earlier Riverbend course
capture byte for byte. The separate launch without assets showed the limited
preview. All three native 1080p reload frames matched fresh serial output.
With the same art fixture, all 213 Skia matrix images matched between the
default, scalar and single-processor runs.

| Final Linux receipt under `linux-ci-seam-scratch/` | SHA-256 |
| --- | --- |
| `map-matrix/matrix.json` | `4272c3aab0ebce4228b779d92b5177251ee85a3d95d7a2a834e9921d7f4a5a21` |
| `map-gallery-1x/native-gallery.json` | `964121af3ffaced2985419679fd954967fdaec4fb175345381db6a72b10d4987` |
| `map-gallery-2x/native-gallery.json` | `80c0cd81a0e57ef3da230bd13e7b918c6707bfa4793ad16bc3f3a9e117a9074d` |
| `application-map-native.json` | `db9d8c806c32c5a70b879e109a047078383c340318899d280a6ae02678f0a3db` |
| `riverbend-stock-art.png` | `6f61410c0f67dc30bd322a3a8501bdca8fbd9755d65f2c260f316eb45f607ade` |
| `riverbend-limited-preview.png` | `471ba418fed1f387fe33cbff1702f0f9c5001db6b72b906e4a5033e069c7f0ac` |
| `map-recovery/replay.json` | `508ec71d562b73f34042be10b133286657566f5cff02772c9bfe7b05b518deb6` |

The process-cold workload starts Mixed at the requested backing resolution
and camera before its first draw. Every sample uses a new managed desktop
process. The admission timer covers reading and validating the pinned package
and fonts. The next interval ends when the native map draw has returned its
Skia lease. The receipt records authored observation creation, scene binding
and the inner draw separately. Source-identity file writes and X11 readback
follow the measured draw. The OS file cache is left intact.

Each cold capture uses the normal map layer detail and a steady active marker
under reduced motion. The owned native window must contain the entire intended
map viewport. Its RGB pixels outside the overview are compared with a fresh
serial map render. The overview has separate native footprint and content
checks. The 1080p runs matched all 180 readbacks across three trials of 20
process starts for each view. The 4K full-detail runs matched all 60. The 4K
and 5K smoke runs matched all six additional readbacks. These are software
X11 timings. Physical presentation is unmeasured.

| Backing viewport | View | Scale | Distinct tiles | Draw p95 across trials | Admission end to draw completion p95 across trials |
| --- | --- | ---: | ---: | ---: | ---: |
| 1920 × 1080 at 1× | Full | 1.25 | 1,590 | 590.65–644.34 ms | 1,337.62–1,364.47 ms |
| 1920 × 1080 at 1× | Half-world | 0.35714 | 16,684 | 658.81–724.66 ms | 1,390.29–1,465.38 ms |
| 1920 × 1080 at 1× | Overview | 0.25 | 30,870 | 887.71–932.15 ms | 1,556.90–1,665.69 ms |
| 3840 × 2160 at 2× | Full | 1.25 | 1,590 | 625.91–661.72 ms | 1,308.05–1,401.01 ms |

Content admission p95 ranged from 217.68 to 275.57 ms. The managed software
first frame exceeds 250 ms in each measured series. **Reload textures** within
an existing desktop process remains a separate workload.

| Process-cold receipt | SHA-256 |
| --- | --- |
| `cold-host-1080-final/native-performance.json` | `ba2d3a7ff92789e94298f919e4bede2fd1cbe7b317d98478c8399191224c6e76` |
| `cold-host-1080-final/replay.json` | `0b893b6b74cf7a6d59660e7477427d652363bd24e72a0186aef5c038af7842e8` |
| `cold-host-4k-full-final/native-performance.json` | `96521c605693cf935f3e098b6812107283b65c56a08d02327d2b1cb2ca8e5f44` |
| `cold-host-4k-full-final/replay.json` | `23c3498c76f8bd9382e77ae9976fd5632f4ceb21761960760a706ae1db847cab` |
| `cold-host-large-smoke/native-performance.json` | `414b5c4b8a3281ddd3a2c57c2a347668ab0d3f25d929603d5c1ebe81414dbc17` |
| `cold-host-large-smoke/replay.json` | `a0332f9b9c29270528dfe4a6a38a7b99e7c01a59e39534218f1f3d838ef85311` |

The opt-in Linux Native AOT build restored with both signed compiler package
content hashes matching the existing lock. It passed all 22 scoped desktop
input and asset-admission checks. At 1080p all 180 AOT startup readbacks
matched the serial renderer across the same three views and trial counts.
All full-detail trials met 250 ms p95. The half-world trials and one overview
trial exceeded that budget. Both builds used software X11 and the same
content, backing and camera. The AOT result measures the optional publish.

| 1920 × 1080 at 1× | AOT map draw p95 across trials | AOT admission end to draw completion p95 across trials |
| --- | ---: | ---: |
| Full | 112.77–124.34 ms | 232.05–239.21 ms |
| Half-world | 159.39–165.43 ms | 263.38–276.70 ms |
| Overview | 134.51–139.11 ms | 232.25–255.95 ms |

| Signed Linux Native AOT receipt | SHA-256 |
| --- | --- |
| `cold-host-native-aot-smoke/application.json` | `2b88ee0cc5a1fdb4a3ba48e7855f3515a25d6b691e6e6496ca7392c6e6954eba` |
| `cold-host-native-aot-1080-final/native-performance.json` | `317f042284b80cad1a6d305d97c7548b085fd68a6cc9aaaf4aedcb75ebc8e095` |
| `cold-host-native-aot-1080-final/replay.json` | `e2376362e8ffd09de5dfc8730e9956b75c11ea340403a8932d708ea914aa302a` |
| `cold-host-native-aot-large-smoke/native-performance.json` | `e2f0fe460dabce1aeb4cce3284cbc8705b5bb85c8458496736d5838591984396` |
| `cold-host-native-aot-large-smoke/replay.json` | `8049c8dd132b0fcede0194dec2f7d3c863d4f6d495f16b544e65a363a43819bd` |

At 4K and 5K, all 18 AOT smoke readbacks matched. This diagnostic used
three fresh processes per resolution and view. The maximum from admission
to draw completion at 4K was 326.81 ms full, 376.89 ms half-world and
249.66 ms overview. At 5K it was 434.78, 436.05 and 324.09 ms in the same
view order. Full-length large-view startup timing remains open.

The AOT warm diagnostic used 30 warmup changes and 24 recorded changes
per workload at 4K and 5K at 2×. All 144 X11 input-driven camera and world
changes matched fresh serial output in replay. Draw p95 in milliseconds was
18.24 / 22.64 for pan, 95.07 / 169.93 for continuous zoom, and 23.91 / 32.30
for dirty-world changes at 4K / 5K. The first tier-2 zoom builds accounted
for 104 and 152 new chunks with zero texture preparation during those draws.
The diagnostic leaves the warm timing gates open.
`native-aot-warm-smoke/native-performance.json` has SHA-256
`3b529b6cfaa9428a4792b61dcfb9430b7c8918585c87c46a2bbc796e5b6ecdd9`. The
matching `native-aot-warm-smoke/replay.json` has SHA-256
`b83d1b4bbf1dd074f6239da7ab1f94e40d79cd1fdccb9ea2b2bab8a23293a659`.

The Linux aligned ground compositor now uses SSE2 when the compiler's
instruction set leaves AVX2 unavailable. It keeps the pinned Skia rounding
and existing runtime checks for opaque backing, premultiplied textures,
integer placement and color space. The packed atlas and chunk cache budgets
stay the same. For the signed Native AOT publish, the first tier-2 zoom
draw used aligned compositing for all its new chunks. No texture preparation
was recorded during those draws.

| Backing at 2× | New tier-2 chunks | First tier-2 draw before / after | Chunk build before / after |
| --- | ---: | ---: | ---: |
| 3840 × 2160 | 104 | 395.43 / 42.69 ms | 377.78 / 28.08 ms |
| 5120 × 2880 | 152 | 528.54 / 67.07 ms | 501.00 / 44.06 ms |

These paired X11 diagnostics each used 30 warmup and 42 measured zoom
changes per size. All 84 candidate camera states matched a fresh Skia
render with ground atlas packing disabled. The 213-image matrix matched
byte for byte with the usual AVX2 path, AVX disabled for SSE2, and all
hardware intrinsics disabled for Skia fallback. The batch recovery test
also checks exact fresh pixels on the first draw in the double-size tier.
The signed desktop passed 22 pinned-input checks and the core suite passed
459 tests.

| Aligned SSE2 evidence | SHA-256 |
| --- | --- |
| `sse2-native-baseline/native-performance.json` | `088b74b363d3b49d387e5a285fbb1f6b6c16b3a3917ba8ef25a431b990ed5100` |
| `sse2-native-candidate/native-performance.json` | `d7cc16c8f0038a0a2e1239e727a55ed5a9e97194e47958aa36a64aee8280d511` |
| `sse2-native-candidate/replay.json` | `00379b545e4af41888c94c726cad319040e97a0ecebdfee4d39c726d0b9b3020` |
| `sse2-matrix/matrix.json` | `f420642433e8ab30a7b983efa5c7062ae1bbfb2f0ad4bb4eb84c602a2e1c5838` |
| `sse2-matrix-avx2/matrix.json` | `dbc305ace14f4b0e82b4a1caa424a3c06e2e95ed94e6e2eb15003cec5a9bbbfb` |
| `sse2-matrix-skia/matrix.json` | `c627ec1eec0ec67d4dc816141aeb8fbec5bdf9a2495daa792054a6fe5e06db6d` |
| `sse2-native-functional/application.json` | `bed77af5b00ed33a6baf0e9a0274f9520f2d0c8c2836f501e22384c83736a910` |

The updated signed AOT build matched all 540 native cold readbacks across
1080p, 4K and 5K. Each view and size ran three trials of 20 process starts.
At 1080p all full-detail and overview trials met 250 ms p95. At half-world
scale one 1080p trial exceeded the target. The 4K and 5K measurements remain
software X11 evidence. Large cold views still exceed the target.

| X11 software backing | View | Distinct tiles | Draw p95 across trials | Admission end to draw completion p95 across trials |
| --- | --- | ---: | ---: | ---: |
| 1920 × 1080 at 1× | Full | 1,590 | 90.74–102.00 ms | 197.11–220.21 ms |
| 1920 × 1080 at 1× | Half-world | 16,684 | 120.43–135.23 ms | 225.45–257.35 ms |
| 1920 × 1080 at 1× | Overview | 30,870 | 94.10–97.11 ms | 202.11–211.33 ms |
| 3840 × 2160 at 2× | Full | 1,590 | 129.72–133.10 ms | 252.23–270.43 ms |
| 3840 × 2160 at 2× | Half-world | 16,684 | 155.00–164.26 ms | 283.70–294.17 ms |
| 3840 × 2160 at 2× | Overview | 30,870 | 111.71–117.46 ms | 241.94–260.24 ms |
| 5120 × 2880 at 2× | Full | 2,691 | 159.90–167.69 ms | 296.46–331.53 ms |
| 5120 × 2880 at 2× | Half-world | 16,587 | 171.96–184.10 ms | 313.90–330.33 ms |
| 5120 × 2880 at 2× | Overview | 31,752 | 128.71–132.52 ms | 270.33–280.28 ms |

| Updated AOT cold-start evidence | SHA-256 |
| --- | --- |
| `sse2-cold-1080/native-performance.json` | `3a5bf21636c3031e7213aeb2e9e89200721febd2c467ae17500201e1046804cb` |
| `sse2-cold-1080/replay.json` | `d3ad5c12f71dc4152a620e9ea4596391d70045a9c83da27d03ec3facb812b0ae` |
| `sse2-cold-4k/native-performance.json` | `fe196fdda1eeea3b956035b243f8bf4e46d04d13ab5a2b6a558e12b91cac8e1c` |
| `sse2-cold-4k/replay.json` | `5ec2ae61563ae83517a2d3e19d7324ac992ce452821c8e00222f65107f0130ce` |
| `sse2-cold-5k/native-performance.json` | `014500e8723a8056f71e7380c987d2b82f4fc48e982213c2a77d732514a8f93f` |
| `sse2-cold-5k/replay.json` | `73aec78f3981c588a2f2b805fbb884fb06e3db66acd2152b0d84c0215b8afeca` |

The signed SSE2 build also ran three trials of 300 recorded pointer zooms
at both 4K and 5K, each following 30 warmup changes. All 1,800 camera states
matched the fresh reference. The 4K map draw met both software target
thresholds in every trial. At 5K, both draw thresholds remain open.

| X11 software backing | Draw p95 | Draw p99 | Input to polled draw p95 |
| --- | ---: | ---: | ---: |
| 3840 × 2160 at 2× | 15.58–15.87 ms | 17.53–19.15 ms | 77.45–79.67 ms |
| 5120 × 2880 at 2× | 25.05–25.54 ms | 49.28–49.77 ms | 130.96–131.46 ms |

At 5K the slowest input frame in each trial prepared ten tier-2 chunks.
Seven used the aligned path. The final group of three fell below the
minimum for packing, sending these chunks to Skia's ordinary path. The
draw took 64.43–72.26 ms. No further ground misses appeared in those
trials after the 51st zoom change.

| Full SSE2 zoom evidence | SHA-256 |
| --- | --- |
| `sse2-zoom-4k/native-performance.json` | `2d09bd146873ddc0a4cd71a91f2fa9b07ae43654820003312dfc37c864a4723e` |
| `sse2-zoom-4k/replay.json` | `37305ea30642fc5ef9443f4e74f3feb76e82303f12709e29313c81060f1bf22e` |
| `sse2-zoom-5k/native-performance.json` | `e7d9ab77d900f388a63d6a93626ee26bea696c930a97ad39a8a550b5de5fe0c8` |
| `sse2-zoom-5k/replay.json` | `65aa318e471e3a9d8ac0e7e2cf17b7e01066ee64050ceba9f15bcdbf46111862` |

The double-size tier now allows aligned packing in a batch of two or three
when the textures qualify. The matrix includes a three-chunk scene with an
independent Skia reference. All 213 matrix PNGs matched again with the AVX2,
SSE2 and Skia fallback paths. All 900 recorded 5K zoom camera states matched
fresh renders, and six native 5K startup readbacks matched at the three
detail levels. The final signed desktop passed 22 pinned-input checks.

On the previously slow 51st zoom change, all ten chunks now use the aligned
path. The chunk build fell from 37.81–46.08 ms to 6.25–6.61 ms. The complete
draw fell to 28.71–30.06 ms. The three 5K trials meet the software draw p99
budget at 28.71–31.58 ms. Draw p95 remains 24.57–25.44 ms and poll-based input
to draw receipt p95 is 128.43–132.03 ms. Physical presentation is pending.

| Small-batch evidence | SHA-256 |
| --- | --- |
| `sse2-tail-zoom-5k/native-performance.json` | `420d0481d74131f3711d0a9fc00097ce96954e56f3e40b289202bae4a243a526` |
| `sse2-tail-zoom-5k/replay.json` | `e054ad31cf46b26a7e19166500cfd427ac5d7dab72022773101c2ea1b237b1ee` |
| `sse2-tail-cold-5k/native-performance.json` | `660225bdb9eec5df04e01ecb452bce3fa604eb231164daea721d66680768d3e4` |
| `sse2-tail-cold-5k/replay.json` | `886355486c7f7a401bc8c0cb306c005e618d38847297748f95c4ef2ab7ee6c21` |
| `sse2-tail-matrix/matrix.json` | `66b51679ef224b0457cbed494e8bd9d7dd0344d67c4fd89eab9244b32ead9b06` |
| `sse2-tail-matrix-avx2/matrix.json` | `4a7727e6ea76c03e19e1d001b46e93e245aee7a2991f12b1e286766d08607232` |
| `sse2-tail-matrix-skia/matrix.json` | `bc5bfca871e26422300266491e5fff12ab784b54877a3631f73751e59472f5f2` |
| `sse2-tail-functional/application.json` | `c5abba3bd7219961c05fbf705238c182b9a805d3fbda6b091d9c012c51473d51` |

Two later experiments exercised the same 54 recorded pan cameras per size
after 30 warmup changes. The first used full-viewport Skia sampling coordinates
to reuse integer-shifted terrain and repair changed strips. A clipped draw
selected a different texel in one 4K column, so the trial repaired those strips
from the full-viewport samples. The serial-copy and parallel-copy versions
matched the fresh pixels after repair. The second drew padded foreground
bands on alternating backings. It matched the 213 matrix images and the same
108 pan camera states. The renderer retains the existing full-band replay and
seam strips after the 5K timing comparison.

| AOT X11 software pan trial | 4K draw p50 / p95 | 5K draw p50 / p95 |
| --- | ---: | ---: |
| Existing replay | 16.24 / 17.74 ms | 21.54 / 24.80 ms |
| Terrain reuse with serial copies | 17.58 / 19.50 ms | 26.46 / 28.96 ms |
| Terrain reuse with parallel copies | 15.91 / 20.00 ms | 24.05 / 26.79 ms |
| Alternating padded bands | 16.04 / 18.56 ms | 24.10 / 26.51 ms |

The diagnostics are in `scroll-proof-baseline-{4k,5k}`,
`scroll-proof-serial-{4k,5k}`, `scroll-proof-parallel-{4k,5k}` and
`padded-bands-{4k,5k}` under `build/m2-native/`. The 5K timing receipts for the
existing path, parallel-copy reuse and padded bands have SHA-256 hashes
`2d172ca4bdbcb2a079791c1c8d1a1c317fed4d8c85db3992549223d5b39a3e8d`,
`1270e43c9aa9a1d5b5405ab54cf5f15c145a8d99c20acb9341c8fdb122bf61e1` and
`cc60552795a9e0f97f3c9150da291a198d53f9ee0b5c2dd5c09842813678028b`.

The decoder now inflates the filtered image in one read sequence, validates
Adler-32 before reconstructing the pixels and compacts the rows in that
buffer. SSSE3 accelerates the checksum when available. The independent Pillow
comparison matched all 2,105 pinned images with intrinsics enabled and
disabled. The 213 map matrix PNGs remained byte-identical. The PNG tests cover
narrow row compaction, exact copies, all five filters, truncation, extra data,
chunk CRCs and a corrupt zlib trailer. All 459 core tests passed on both paths.

The paired signed Native AOT cold run used three trials of 20 fresh processes
per view and configuration. All 240 native readbacks matched the fresh serial
renderer. The 1080p half-world view met 250 ms p95 across all three updated
trials. The 4K full-detail result still has runs on both sides of the budget.

| X11 software backing and view | Earlier admission end to draw completion p95 | Bulk decoder p95 |
| --- | ---: | ---: |
| 1920 × 1080 at 1×, half-world | 243.74–255.28 ms | 226.19–243.73 ms |
| 3840 × 2160 at 2×, full detail | 240.35–264.24 ms | 236.50–254.66 ms |

| Bulk decoder evidence | SHA-256 |
| --- | --- |
| `png-bulk-baseline-1080/native-performance.json` | `2f42b0cb6a6c7139f827e0136d0ec6eb17bbc5ef68d272b9c5c53761f08d5997` |
| `png-bulk-baseline-1080/replay.json` | `90c6e3a921143d04b60879061303988e41a50a43097a20c2746100cc49576735` |
| `png-bulk-candidate-1080/native-performance.json` | `9aae3efb0cee7ed52c6ec04fc65c22f3d696cc310bf544d30f1a00c253bf66a0` |
| `png-bulk-candidate-1080/replay.json` | `0995997a61190b4ed21c08b6f9636fc475f622da1fb4fe29ddc763e42fc10ef6` |
| `png-bulk-baseline-4k/native-performance.json` | `5512559e4d1894359b46e5b0fa46c55e5f366ceb0650c1623ec748a306f9fb6a` |
| `png-bulk-baseline-4k/replay.json` | `19fa593ad8cb9caab31aec117e969988070390e72bb69245e5547d7c5f3efa01` |
| `png-bulk-candidate-4k/native-performance.json` | `e1713da96c15229667e91d006288718c5398f406851fd88e9caa2af5a6c1ce0f` |
| `png-bulk-candidate-4k/replay.json` | `365d4b9c17c3601094d818b046738b695ad7cbfced2a650586615c59127f27c1` |
| `png-bulk-pixels.json` | `f92b73bf35da4bf1acd2cea0bfe863b61ab2d63ef8e7c1632758f27e5d640fe9` |
| `png-bulk-pixels-scalar.json` | `c287ebb7e569702a9561a9b70b655fd921dd85276707c44c2f3074ad8b9e2075` |
| `png-bulk-matrix/matrix.json` | `11926d9adabda56f8c130a7275b0ec6cab589ee6039ba101b5fc771b06d71179` |

The republished bulk-decoder build also met the 1080p full-detail and overview
startup budgets in three trials of 20 process starts per view. Full-detail
p95 was 184.34–196.89 ms and overview p95 was 208.00–212.62 ms. All 120 native
readbacks matched fresh serial map pixels and all 22 scoped native input
checks passed.

| Final 1080p receipt | SHA-256 |
| --- | --- |
| `png-bulk-final-1080/native-performance.json` | `0d375862e1e9e4b197a84c054de13d17638d11bb1c9615f75a2cf53f0eed04bf` |
| `png-bulk-final-1080/replay.json` | `940919f98091a9ecc1c0a723fb46b540ae1a487610d52acfeccde0c4406d6c2f` |
| `png-bulk-final-1080/application.json` | `22ffc9d399b7838d149976ad0cc04987443cb1ebf99990a680fb92d3afdf472b` |

An experiment sampled each ground axis separately with Skia and assembled
rows from those samples. A synthetic check matched 45,875,200 pixels across
200 rectangles, including near-tie positions. The sampled renderer also
matched 432 recorded 4K and 5K frames. Both vector implementations slowed 4K
pan. The gather raised median 5K pan from 21.45 to 23.17 ms. With contiguous
loads and shuffles, median 5K pan was 21.69 ms compared with 21.56 ms in its
paired baseline. The renderer retains ordinary Skia ground replay.

| Sampled replay trial | SHA-256 |
| --- | --- |
| `sample-ground-pair-baseline/native-performance.json` | `e62e514fd9fdad125fa6118e7e187fb920d56266c38abb961929eaa37b661936` |
| `sample-ground-pair-candidate/native-performance.json` | `b2a5ce7d6c51c73e5c9b2d3639031cc689ae7acf2b2700a918d8e76e61754597` |
| `sample-ground-shuffle-baseline/native-performance.json` | `890406ac5fa72bef24a74c858cff507b6adfa027abd0fa63537b0f1860cdc4d4` |
| `sample-ground-shuffle-candidate/native-performance.json` | `a615a3327de87ef5fbe594abfb80bd8604e28abd0a08ca3d643477aef45cdcd1` |

A second experiment painted the ground in a separate pass and used the
finished rows to initialize the seam buffers. The matrix and 288 recorded
navigation frames matched. Median draw time rose in all six paired cases.
At 5K it rose from 22.03 to 23.48 ms for pan, from 21.84 to 25.55 ms for
zoom, and from 22.99 to 25.57 ms for dirty world. The renderer
keeps terrain and foreground in one parallel replay.

The before and trial receipts are
`seed-seams-native-baseline/native-performance.json` (SHA-256
`4162415739d6ed131a5911405a3a1ed9de9e8aa76acf825d2d58ad96dca249bd`) and
`seed-seams-native-candidate/native-performance.json` (SHA-256
`76a60953b4268ed3af6409bf195052d2756b3c442a3271bca1dca6c0077f54d2`).

Native replay profiling at 5120 × 2880 and 2× used 30 warmup frames and 54
measured half-world pan frames for each configuration. In the 12-band run,
the last band to finish spent a median 4.11 ms on terrain and 11.26 ms on
foreground drawing, with 0.40 ms before it started. Allowing all 23 band and
seam jobs to run concurrently increased draw p95 from 26.30 to 27.76 ms.
Avalonia's X11 software framebuffer gave a 25.33 ms p95. All three runs used
virtual X11 and software rendering.

Clipping sprite draws to their alpha bounds changed pixels at a 5K foreground
band seam. The guarded source-rectangle variant cropped only when the
projected sampling transform was exactly equal. It passed the map matrix and
matched 54 earlier recorded frames. In separate paired native measurements,
the median rose from 21.40 to 21.75 ms and p95 moved from 23.72 to 23.58 ms.
All 108 frames from the pair matched the fresh serial pixels. The renderer
retains the full registered sprite draws. The restored build passed 459 core
tests and reproduced all 213 prior matrix PNGs byte for byte. The 5K warm
budget remains open.

| Native replay trial | SHA-256 |
| --- | --- |
| `band-profile-12/native-performance.json` | `f13dc5e9e831c37610a4a10bc7c7b2381613695dfb354d91a296fbcbaead7036` |
| `band-profile-24/native-performance.json` | `2e4db944d49efe225034beb21aaaff6665176697b712d1d8cbca7602623eeeb8` |
| `band-profile-framebuffer/native-performance.json` | `78455ddce91f30464c177a6d29c3802326366d659dd1ee467b8715c9f93f07d5` |
| `sprite-subset-matrix/matrix.json` | `0a588cfee624f2138afe4f02816cf28c4e062a83a9e14deeeda79a3fa55bfab3` |
| `sprite-subset-replay.json` | `a351c88554766abf4aa5559cf35c0fb381664fb6d26ba8ecf2f2dcd6db62d224` |
| `sprite-guard-baseline/native-performance.json` | `8ad21c3ca083837758d036678675e0a0eaad6b0a66b7664e8068690356f6a9da` |
| `sprite-guard-candidate/native-performance.json` | `8fc3b699dc457e8115f48976190fb8180d17bc4b916f8392c2b734e4f9c67161` |
| `sprite-guard-baseline/replay.json` | `0ac17cdd45847db201fd7eb9958aac27f03a97607e0c2595b8b388dcf288ecd3` |
| `sprite-guard-candidate/replay.json` | `9d795a4ca9c1a1b10e21b5b6561fc7c777ae575ff370dd57938ae4f33934cdbe` |
| `pinned-manual/matrix/matrix.json` | `5397f03e6e00a9d2fb5f1354470cd2a53f17aa7b8bbe55912cb48f29293ba268` |

Preserving the full sprite transform in an image shader still changed pixels
in the recorded 5K foreground band-seam test. That optimization was removed.
The renderer now instead prepares straight-RGBA tints directly into the
immutable raster shared with the cache image. SSE2 handles the portable x64
Native AOT build. AVX2 and the scalar fallback use the same opacity rounding.
Texture keys, limits and registration are unchanged.

Each 5K full-detail diagnostic collected the first frame of 12 fresh Native
AOT processes. All 36 X11 map readbacks had the same RGB digest. Median tint
preparation fell from 13.65 ms to 2.42 ms with SSE2 and to 1.19 ms with SSE2
and the shared bitmap. Median admission-end-to-draw time was 272.74, 272.14
and 262.83 ms, respectively. These short software runs leave the startup gate
open.

| Tint preparation trial | SHA-256 |
| --- | --- |
| `tint-output-base/native-performance.json` | `d6317a1f87f3bed0cd44188fa2946f5a29794435a3b84ba55c2b05aa6fb55f68` |
| `tint-output-sse2/native-performance.json` | `839844e4aacd4512a9b30b03a0cb97fe5a4ae4d76d5d0ba287c537e6e5230102` |
| `tint-output-bitmap/native-performance.json` | `7fefd62c77181614571780d14dd6e2667ba67e8fd5d36b57f32fba3e229376c0` |

The final signed build ran three 20-process trials at each of 4K and 5K full
detail at 2×. All 120 actual X11 map readbacks matched the fresh serial
renderer. Each 4K trial met the post-admission first-frame target. The 5K
full-detail gate remains open.

| Full-detail Xvfb backing | Draw p95 | Admission end to draw completion p95 |
| --- | ---: | ---: |
| 3840 × 2160 | 106.16–117.70 ms | 229.81–239.30 ms |
| 5120 × 2880 | 137.32–141.22 ms | 275.02–304.10 ms |

The 213 matrix captures matched byte for byte on AVX2, SSE2 and the scalar
fallback. The tint check verifies straight RGBA, source immutability and cache
reuse. The managed desktop passed 45 showcase windows and 26 lifecycle and
input checks at 1×, plus 26 at 2×. The signed desktop passed 22 Riverbend
checks, and all 459 core tests passed.

| Final tint preparation evidence | SHA-256 |
| --- | --- |
| `tint-output-final-4k/native-performance.json` | `a89031f557e959bb5ea0093f41886f415a9829945c15b270e3e4884381bc64b9` |
| `tint-output-final-4k/replay.json` | `08cc5202ee007aa362dd093189db43825ff1927a5bb15444cff22408e7596295` |
| `tint-output-final-5k/native-performance.json` | `bb49a0c72ba0cfa2770e31f06c0a43ad88a3417e15c22fa9ef0a755904607476` |
| `tint-output-final-5k/replay.json` | `f2ba8a98c25043197e5827d6312e8618e02a8b2b7607f5cc7f5f364dfa70f9af` |
| `tint-output-final-4k/application.json` | `635fc798ee40c55c06db69e3a6299e98d6d535c68f14192a48768821de1ce8b8` |
| `tint-final-matrix-avx2/matrix.json` | `68fa71eb4d1d9b155299ee3e681194d4132702dcaf8292cca8fd4efcb17b9aed` |
| `tint-final-matrix-sse2/matrix.json` | `0ec6109d3b251a2a1ea1e275d06d29363bfeb9f9b3e14d9389331e5d1d2f0df9` |
| `tint-final-matrix-scalar/matrix.json` | `727abd27f928ec992496f5f562c6aefab9e3b4fafab78d5748af13bef205afd1` |
| `tint-final-gallery-1x/native-gallery.json` | `1ff7576e4fddc47866acf1bdbc2e1c6be6bdb4a01af5d61d0b0a096676b9d5fe` |
| `tint-final-gallery-2x/native-gallery.json` | `355917e60766bacb0290a0d12d0f95a0040809cf701590dfa9029702b492ad14` |

An isolated KWin virtual output at 5600 × 3300 used Xwayland direct GLX on an
AMD Radeon 890M with Mesa 26.0.8 and kernel DRM 3.64. The driver also checked
the graphics-engine counter in each desktop process. During the 5K input
smoke trial it advanced by 50.60 ms across warmup and measured changes. All
four camera states matched fresh rendering. Four 5K process-cold captures
matched the fresh serial pixels and each desktop process reported GPU work.
These are accelerated virtual-display results. Per-frame graphics timing,
physical presentation and physical 4K/5K mode availability remain pending.

| Accelerated virtual-display evidence | SHA-256 |
| --- | --- |
| `isolated-gpu/verified-smoke/native-performance.json` | `6eaa756af12309780ec16d2d779773e969caf4768d6ed3cb9092f6fc8a0af8a4` |
| `isolated-gpu/verified-smoke/replay.json` | `8e64c77e5dbd66407a648c2fcc21e30b62209c98e479fb37f4cd0eac9d8c004f` |

The 5120 × 2880 accelerated virtual workload measured three trials of 300
changed frames per operation after 30 warmup changes. The desktop graphics
counter advanced in all nine trials. All 2,700 recorded states matched the
fresh serial renderer in replay, including 3,596 visible explosion instances.
The map draw still exceeds the 16.7 ms p95 budget. Pan met 33.3 ms p99 in all
three trials. Pointer receipts remained under 100 ms p95. They record polled
draw completion, and physical presented input latency remains unverified.

Every sample repainted the full viewport. Across the pan samples, median
band replay was 16.83 ms and the final viewport copy was 3.57 ms. Pan prepared
nine new ground chunks across 900 measured frames, all in the slowest 5%.
The dirty workload's median ground build was 1.48 ms.

| 5K accelerated virtual workload | Draw p95 | Draw p99 | Input to polled draw p95 |
| --- | ---: | ---: | ---: |
| Pan | 27.24–28.18 ms | 29.34–30.42 ms | 55.63–55.79 ms |
| Continuous zoom | 28.12–28.87 ms | 31.81–36.68 ms | 55.92–56.38 ms |
| Dirty world | 30.71–31.31 ms | 35.87–37.71 ms | 81.73–82.09 ms |

The isolated driver wrote all nine series before teardown encountered a
document-portal mount in its private runtime directory. The launcher now uses
a private D-Bus session without desktop service activation. Both subsequent
Xwayland diagnostics exited cleanly. The final harness also matched four
recorded 4K pan cameras and four actual first-frame readbacks at 2× against
the fresh renderer on the Radeon device.

| Accelerated workload evidence | SHA-256 |
| --- | --- |
| `isolated-gpu/full-5k-warm/native-performance.json` | `fc4f2aca673d8c0f72444b17df9a039b0a25306688f95890c1ff98f31408b0c0` |
| `isolated-gpu/full-5k-warm/replay.json` | `be7857129e5089249a62cc3eca98f1a562ea649039b1502da4362700573a0ec3` |
| `isolated-gpu/final-harness/native-performance.json` | `7cf0c0e309879f23a76f8003be84b6ac1bbc32d7a237e596077bcac4348fa1b2` |
| `isolated-gpu/final-harness/replay.json` | `f4c9b26c0098aaa9de3d3b77b002541171ae87ca3ad31b97cb24231fec2c3116` |

The existing direct painter was tried on the GPU-backed canvas at 5K. The
settled retained cold window matched the fresh serial result. The direct
window changed 3,737,829 RGB pixels outside the overview at the same scene
and camera. In the paired 30-change pan diagnostics, the retained path drew
in 24.60 ms p50 and the direct path drew in 26.23 ms p50. Reference replay
shared the machine during these short diagnostics. The renderer retains its
current viewport path.

| Direct GPU trial | SHA-256 |
| --- | --- |
| `gpu-direct-trial/retained-settled.json` | `98903b8092eed7e2e59caa9c55eea73c8acc404b0bfb3953bfa245ef1ed1f2e3` |
| `gpu-direct-trial/direct-settled.json` | `9696468491ac860b4eb5d3dd23dbcf26caba27c78f01ba2b42d60bf31e20c389` |
| `gpu-direct-trial/retained-timing/native-performance.json` | `d90ae242813b306e6dc25ad9d4fee557599679e2395893afbfebadcee0973981` |
| `gpu-direct-trial/direct-timing/native-performance.json` | `acad5b8a1599025b686e988d0cb3e1e8b921a0c8771c5691b32259ee5221f765` |

Large first frames now draw ground and foreground into the same worker bands.
The first eligible pan establishes the separate terrain-scroll raster if it
needs one. The next eligible integer pan can reuse that raster. Large combined
replays clear the background only where opaque ground draws cannot guarantee
coverage, including the rounded chunk edges and viewport margins.

The paired 5K Xvfb diagnostic ran 12 new processes per configuration and
detail. First-frame band replay improved draw p50 at all three detail levels.
The short full-detail post-admission p95 varied between configurations.

| Detail | Draw p50 before / with first-frame bands | Post-admission p95 before / with first-frame bands |
| --- | ---: | ---: |
| Full | 120.10 / 107.58 ms | 266.90 / 271.71 ms |
| Half-world | 146.52 / 117.74 ms | 312.66 / 282.90 ms |
| Overview | 102.49 / 98.17 ms | 274.70 / 262.82 ms |

An earlier trial also planned up to 64 ground chunks before requesting their
textures. It improved median full-detail draw by about 9 ms with separate
ground composition, and added about 2 ms of improvement when combined with
first-frame band replay. The combined trial was slower at half-world detail.
The existing bounded chunk planning and texture preparation stay in use.

The narrower background clear lowered draw p95 from 28.27 to 24.94 ms for pan,
from 51.04 to 42.64 ms for continuous zoom, and from 33.09 to 31.19 ms for dirty
world in paired 5K virtual Radeon diagnostics. Each used 30 warmup and 54
measured input changes. The short zoom trace includes tier construction. The
16.7 ms warm p95 gate remains open.

The trials matched 168 actual cold window readbacks and 324 recorded warm
states against fresh serial rendering. The final native and single-worker
scalar matrix each matched all 213 prior PNGs. The 5K matrix also checks
cold output at stable and fractional sampling phases, the first pan and the
subsequent retained scroll. The signed desktop passed 22 scoped input checks
and the core suite passed 459 tests.

| First-frame and background-clear trial | SHA-256 |
| --- | --- |
| `cold-plan-trial/baseline/native-performance.json` | `713d3b1496c30128b121bddb1a1eefaaa0dd7cd4f55391ced4a298ca3ef38c91` |
| `cold-plan-trial/plan/native-performance.json` | `03e6fe44dc6b1b1c3bb29b9cae6e7332c48610a31d44c4b2bb7a8b29f85e0582` |
| `cold-plan-trial/replay/native-performance.json` | `47a0794e54f828b7c300d00b26ec0dadb232f8986e5a3097e92ca733c1b756b2` |
| `cold-plan-trial/combined/native-performance.json` | `f543b1c23aed45495e06fd074cebe22eb71e5b5d85c2f0a04c500baf069af89c` |
| `cold-plan-trial/first-frame-baseline/native-performance.json` | `742ce58f8593dbeb7bc297af76d38c2f0a00e423bcb536b12b63d16aadf9e189` |
| `cold-plan-trial/first-frame-candidate/native-performance.json` | `d8e662d93d8adb6dfc4f5209da0af9ab1a325ccdeb4067f65fb44f492d400110` |
| `cold-plan-trial/clear-baseline/native-performance.json` | `cfb6bec989339bf5f032826dee8432321b597d3dea712eb9065f505a2bd32acc` |
| `cold-plan-trial/clear-covered/native-performance.json` | `a510dee65e7dafe5499dd489af598db8889fe28e1bf25c04b31b4240dd8b8fa7` |
| `cold-plan-trial/first-frame-baseline/replay.json` | `d4a20dcbdbe8cb9102e00a222432d059fb58a9879f55a453402b6090618a74e7` |
| `cold-plan-trial/first-frame-candidate/replay.json` | `c6a4a85ebfef6eecb8fdd3da8d9b91fb91fccbc97b9e42219cc17edc08f43fbb` |
| `cold-plan-trial/clear-baseline/replay.json` | `5e73d4ac32e3a235df6b902eaf5ab7b46993073e500233612c56c741ca605187` |
| `cold-plan-trial/clear-covered/replay.json` | `4b2991d88620fa810efa27f43a90faf3d81d182aad64da68a839fa70afcd5f02` |
| `cold-plan-trial/final-matrix/matrix.json` | `db1b735cae973959b654150a172738ff08858e874523d65f7ce988a68db7516e` |
| `cold-plan-trial/final-fallback-matrix/matrix.json` | `2ead640391f9a1dc4a430f6148c133b54b35070dc0ef07c329789877931bc331` |
| `cold-plan-trial/application.json` | `70abf612143aacc16ae2e60afccaccd12236fcea1acec87c61db3d594401afb4` |

The final software X11 build ran three 20-process cold trials per detail and
size. All 360 actual cold window captures matched the fresh serial renderer.
All 4K full-detail and overview trials met the 250 ms p95 goal. The 4K
half-world start and all 5K startup views still have at least one trial above
the target.

| Software backing at 2× | Detail | Draw p95 across trials | Admission end to first draw p95 |
| --- | --- | ---: | ---: |
| 3840 × 2160 | Full | 104.49–109.33 ms | 224.90–240.30 ms |
| 3840 × 2160 | Half-world | 127.40–129.32 ms | 254.56–260.26 ms |
| 3840 × 2160 | Overview | 93.50–99.05 ms | 231.14–241.31 ms |
| 5120 × 2880 | Full | 117.33–123.31 ms | 267.35–283.06 ms |
| 5120 × 2880 | Half-world | 133.57–143.44 ms | 280.01–296.85 ms |
| 5120 × 2880 | Overview | 98.39–106.11 ms | 248.33–259.13 ms |

The native texture reload workload met 250 ms p95 in all nine X11 software
trials, including receipt polling. Each trial exercised 20 actual reloads
and retained the selected map target. All 180 recorded reload states matched
fresh rendering. Physical presentation and other recovery scenarios are
separate gates.

| Software texture reload backing | Draw p95 across trials | Input to polled draw p95 |
| --- | ---: | ---: |
| 1920 × 1080 at 1× | 89.57–97.68 ms | 130.41–138.32 ms |
| 3840 × 2160 at 2× | 98.31–112.04 ms | 131.72–159.20 ms |
| 5120 × 2880 at 2× | 103.26–118.85 ms | 156.00–166.56 ms |

| Final X11 cold and recovery trials | SHA-256 |
| --- | --- |
| `cold-plan-trial/production-1920-recovery/native-performance.json` | `3fedbc1b562613b9e80e363b4a5ec6a17d1e7c9e90dedc62a5c69e2b56330859` |
| `cold-plan-trial/production-1920-recovery/replay.json` | `a53463653e9552d7dc05e7c1d2830cb918f65972718e344c03e0382ca3bc2417` |
| `cold-plan-trial/production-3840x2160/native-performance.json` | `a4b46c75f4da224e008e453d2e3784ed64794b13f74a22c6358483aa10102359` |
| `cold-plan-trial/production-3840x2160/replay.json` | `afc05448542a8e67152860fa9101e686469bf37b065146e60f13d8604a855d7c` |
| `cold-plan-trial/production-5120x2880/native-performance.json` | `0bc3bfd3fe389de00e26849f259bcc2f7ec202632192ab320f05b82be1938964` |
| `cold-plan-trial/production-5120x2880/replay.json` | `39fc9cca119326e4276530184f60359eebcd3331604e076923bed321b1740f3f` |

The accelerated 5K dirty-world workload measured three sets of 300 changes
after 30 warmup changes. It includes authored update construction and
production scene binding measured for the same revision as each changed
draw. The driver sums those three measured durations per frame before
calculating the combined quantiles. Scene binding includes the owned
presentation index, renderer invalidation and synchronization. All 900
recorded states matched the fresh serial renderer in replay.

| Accelerated 5K dirty-world stage | p95 across trials |
| --- | ---: |
| Authored update construction | 2.06–2.15 ms |
| Production scene binding | 13.28–13.61 ms |
| Map draw | 29.44–30.43 ms |
| Combined measured work | 42.80–43.57 ms |
| Input to polled draw receipt | 81.76–82.08 ms |

| Final accelerated dirty-world evidence | SHA-256 |
| --- | --- |
| `cold-plan-trial/production-radeon-5k-dirty/native-performance.json` | `635f201ab2d029c5d035e48301f92aa051e4ad788223801a9535b8a58f66d77a` |
| `cold-plan-trial/production-radeon-5k-dirty/replay.json` | `b348b31cddb1f8f55530f480fee416a26a8dd8650d0a7a09f4fc53a50a8a7c5d` |

A 90-change accelerated 5K profile split scene binding into view indexing,
renderer synchronization and ground invalidation after 30 warmup changes.
Synchronization was 10.31 ms p95, indexing 5.09 ms and ground invalidation
1.29 ms. The first attempt deferred cache invalidation while keeping the
publication lock through canvas submission. Synchronization still took
9.98 ms p95. Its total measured work was 42.93 ms p95. Both sets of 90
recorded states matched fresh pixels.

Same-layout updates can now publish while a raster is busy. A newly queued
generation supersedes earlier queued work. Main-map and overview draws
check the generation before committing output. The raster lock keeps a draw
that already committed ahead of later submissions. Observer, region and
layout replacements still wait for in-flight raster work and release old
view resources before returning. Deferred cache invalidation is included in
draw work when the map consumes the update.

In three accelerated 5K trials of 300 dirty changes, renderer wait was
0.0061–0.0063 ms p95 and scene binding was 5.47–5.77 ms p95. The driver
recorded 786 deferred binds across the 900 measured changes. Combined
update, bind and draw work was 35.75–36.55 ms p95. Input to polled draw was
81.81–82.13 ms p95. All 900 recorded states matched fresh pixels in replay.
The map draw and physical presentation gates remain open.

| Scene publication trial | SHA-256 |
| --- | --- |
| `bind-trial/profile/native-performance.json` | `1618b29bd871995b14dced2e49a917ad760574c4922eb3579f88eebcacb250a6` |
| `bind-trial/profile/replay.json` | `8ec4a7a5c3d83db1b8443047e90e5f61e143ea94ace382465d49b14261724790` |
| `bind-trial/candidate/native-performance.json` | `29f362198e9c46d5fe00b3b78211b082962d18ed1af46ad9a8bbffcb9ef9668c` |
| `bind-trial/candidate/replay.json` | `a6bd84a58f57f2a6cfe34f307d55b2bfe2f4ce79142f8db30c4f56e56f27db16` |
| `bind-trial/production/native-performance.json` | `acfc3b19ceedcf1c9b830a1d13d2e56f445bb4a6c84ccd923eeca5c7a79bc19c` |
| `bind-trial/production/replay.json` | `5b79ad115d65a67fc82df72aff3f622319e51564c46c2521265c9560dd66721b` |

The scene now indexes tiles by integer slot. Occupied cells store the full
object record and empty cells share slot zero. A managed allocation probe
constructed the same 31,752-tile observation 30 times per implementation.
Median allocations fell from 3,676,272 to 477,136 bytes per scene. The native
driver measures indexing allocations independently on the UI thread. All
900 measured states in the compact build matched fresh pixels in replay.

| Accelerated 5K dirty-world stage | Earlier renderer p95 | After deferred binding and compact indexing p95 |
| --- | ---: | ---: |
| Scene binding | 13.28–13.61 ms | 1.17–1.25 ms |
| Scene indexing | 5.09 ms in the diagnostic | 0.97–1.01 ms |
| Combined measured work | 42.80–43.57 ms | 30.30–31.70 ms |
| Input to polled draw receipt | 81.76–82.08 ms | 81.59–82.41 ms |

The combined work remains above 16.7 ms p95. Banded replay took
17.36–17.83 ms p95 and the viewport copy took 3.85–3.86 ms p95. The compact
AOT build reran the 4K half-world X11 software startup workload. Admission
end to first draw was 258.09–263.33 ms p95, so this startup gate remains
open. All 60 actual window readbacks matched the fresh serial renderer.

The concurrency check pauses an old raster and publishes two revisions, the
last withholding a previously disclosed tile. The old generation presents
the background alone. The newest overview and map match fresh pixels. A
concurrent observer swap waits for the raster writer, releases the previous
observer's rasters and prevents the old generation from drawing again. The
normal and single-worker scalar matrices both reproduced all 213 prior
captures byte for byte. The signed desktop passed 22 scoped checks. The
managed desktop passed 45 native case windows at 1×, 26 input and lifecycle
checks each at 1× and 2×, and all three mixed-DPI companion-window checks.
The core suite passed 459 tests.

| Compact scene evidence | SHA-256 |
| --- | --- |
| `bind-trial/allocation-probe/dense.json` | `55cb9819825a3789b3264c0e6aa07542d5bd20d6b3696dd3a8d89a8bc0ffa7e0` |
| `bind-trial/allocation-probe/compact.json` | `ceef5527c97fb7e7d313aeb6a5cc5f72c14a846057b1a8fc49b6b26a6fd97aff` |
| `bind-trial/compact/native-performance.json` | `c5764fcdfe8fbedc50958d5aa4d53a853acb03e786a62c3c6dffa98168c3c5f6` |
| `bind-trial/compact/replay.json` | `a7e51da4f5c762d7fa75816054d2261e6e542bc30d2a81f770e25924803f1d3f` |
| `bind-trial/compact-cold-4k-half/native-performance.json` | `9b973d77f090525a51a84ebcdd2af9cea4d4faea95f6b7231c72560045c9e7fd` |
| `bind-trial/compact-cold-4k-half/replay.json` | `4d4e711e30074186fd6cfb8703441260600b0092cb37502cd9ae5935b97def76` |
| `bind-trial/final-matrix/matrix.json` | `fe6a14689ee16a115b9e2614b1ba27a6abeb7a62acd70091f5fe45e79739a8e3` |
| `bind-trial/scalar-matrix/matrix.json` | `24bacc2455f6c91aa8daf5047ffaa99e397d4aa3c0abf44333e8161573366cf1` |
| `bind-trial/application.json` | `571abb37c6166e4658354698148be393a5902a0e19ab16d0fe489c7a114a603f` |
| `bind-trial/mixed-dpi/native-gallery.json` | `fa0f9ea0463de37a23cb21d1fee8112b728496c825b37d263b68b7443ce9eae5` |
| `bind-trial/gallery-1x/native-gallery.json` | `60efaa94295001cf68a0516c099d00f0e0ef8c336e4bbb7cabfbda24549bb592` |
| `bind-trial/gallery-2x/native-gallery.json` | `70b520b20c4e4ce593c204be1f0b3ebb85198d8e7cc0900cc788a5029083d941` |

### Scattered world changes

The 5K dirty-world diagnostic replayed the first 20 measured states from
the accelerated profile above. Each change includes a faction recolor and
localized terrain and object edits. A disconnected Skia raster clip changed
6,682 pixels in the first frame. Replaying individual rectangles removed
most differences. A vertical stripe of 216 differing pixels remained until
ground was painted at the full viewport width before the foreground patches.

The exact version redrew about 3.58 million padded foreground pixels per frame
and all 20 frames matched a fresh renderer. The quickest exact variant took
87.19 ms p95 in offscreen replay. The retained full redraw took 28.56 ms p95
on the same states and matched all 20 frames. The renderer keeps the faster
full redraw for these scattered changes. The replay receipt now records the
counts by redraw path and the painted pixel totals, so a successful pixel
comparison also shows which path ran.

| Scattered change diagnostic | SHA-256 |
| --- | --- |
| `sparse-damage-trial/diagnostic-first20.json` | `95dd00ce04d7592986b5801d3013ff7f9b8c3f3f19af498416144269005213cd` |
| `sparse-damage-trial/timed-control.json` | `6e4cbd7ccd2258afa5dba3623f3d168b4c730c22c558184df32db270cc41c1cb` |
| `sparse-damage-trial/timed-patches-pixmap.json` | `cd1a48ff229c65f026bacefa6e51139e07cb19193c01b752dc70b93e065e1669` |
| `sparse-damage-trial/timed-control.log` | `c04fe1cb1c649c863319ed9356a4fea4c54511d57d9a1dd0344bb06d688b4044` |
| `sparse-damage-trial/timed-patches-pixmap.log` | `bf41dedefc3aa691060272b7599d34ce4e382b6715633a428349582ee14deaba` |

A later trial used the retained terrain as a backdrop and recorded the
foreground once per revision. It replayed that picture into rectangular
patches in parallel and copied only the unpadded interiors to the viewport.
The first 4K replay differed at 36 pixels by one channel value. Wider
horizontal guards reduced that to 16. Complete caption bounds restored
exact pixels with both guard sizes across 108 recorded 4K and 5K revisions.
Combining the padded rectangles into one region clip per band changed 6,067
pixels on the first 4K frame, also by one channel value.

The exact narrow-guard trial painted an average 5.24 million padded pixels
at 4K and 7.00 million at 5K. Two paired Radeon virtual runs per size used
30 warmup and 54 measured world changes. The repeated clipped picture replays
took longer despite the smaller area. Production keeps full foreground
replay from the retained terrain.

| Accelerated virtual backing | Full foreground draw p95 | Exact patched draw p95 |
| --- | ---: | ---: |
| 3840 × 2160 at 2× | 18.78–19.26 ms | 23.45–24.20 ms |
| 5120 × 2880 at 2× | 27.37–28.65 ms | 28.72–32.52 ms |

| Foreground trial under `build/m2-native/sparse-foreground-trial/` | SHA-256 |
| --- | --- |
| `complete-captions-narrow.json` | `e5b5a834770b7b66bac4b29da5e177b2a00e6c4ab0570d1f935662a2cc0cbeed` |
| `region-captions.actual.png` | `3f3d1f6a37608212b55683ff7feb67cb0c1d6f56475b0ca0e257af554ebff9bc` |
| `baseline-1/native-performance.json` | `adbac20a0c6b05074cdaf02d4f998e0f0b2479426eed5c907936d5cfaa163e42` |
| `baseline-2/native-performance.json` | `8603f8c6181ea4e372fb6380c4016ae3979fb75af2c5b767bf211a221726e537` |
| `narrow-1/native-performance.json` | `7b8a12773b078c6caa3c281e339cb64a48124b5f301a874f36d396a9a45a2e46` |
| `narrow-2/native-performance.json` | `265c07c3f3d3efc325e0567632fc17a4aafc5b2eca6a01de765da0756f8a5a77` |

### Half-world startup phases

The signed Linux desktop captured 20 process-cold half-world windows at each
backing size on software X11. All 40 native RGB readbacks matched fresh serial
rendering outside the overview. The driver checked that the reported phases
sum to the same post-admission draw interval on every sample.

| Phase, p95 | 3840 × 2160 at 2× | 5120 × 2880 at 2× |
| --- | ---: | ---: |
| Pinned content admission | 205.61 ms | 207.58 ms |
| Framework setup after admission | 57.46 ms | 58.46 ms |
| Renderer and font setup | 25.10 ms | 38.09 ms |
| Showcase construction, including board and binding | 1.79 ms | 2.75 ms |
| Window setup and wait for map draw | 45.03 ms | 68.88 ms |
| Map draw through end of Skia lease | 124.19 ms | 143.94 ms |
| Total after content admission | 245.34 ms | 296.61 ms |

Each row uses its own nearest-rank quantile. Ground chunk preparation took
107.05 ms p95 at 4K and 114.11 ms at 5K within the draw. The 4K diagnostic
was within 250 ms. The earlier 4K software trials and the 5K diagnostic
exceeded the target.

| Half-world X11 diagnostic | SHA-256 |
| --- | --- |
| `sparse-damage-trial/startup-phases-final/native-performance.json` | `8a6310e3fbc0d03a20467d5b10c707b81d78d0a994e56ce949b1671d2350458f` |
| `sparse-damage-trial/startup-phases-final/replay.json` | `ba8bc213df9bbeca097f10b5eab5d7cf46d44207b1924d8a3063188fe3886791` |

The signed desktop also ran three sets of 20 cold processes per size on the
accelerated Radeon virtual Xwayland display. Every desktop process reported
DRM graphics activity. The 120 windows used for timing and another 60 windows
from the initial 5K run matched fresh serial RGB pixels outside the overview.
The 5K timing range uses a separate repeat after a core test briefly overlapped
the last series of the initial run.

| Accelerated virtual backing at 2× | Framework setup p95 | Ground preparation p95 | Draw callback p95 | Total after admission p95 |
| --- | ---: | ---: | ---: | ---: |
| 3840 × 2160 | 86.58–102.30 ms | 106.79–110.18 ms | 128.68–133.54 ms | 270.24–277.78 ms |
| 5120 × 2880 | 87.99–100.38 ms | 110.76–117.18 ms | 142.47–153.69 ms | 292.89–301.17 ms |

Ground preparation is part of the draw callback. Both sizes exceed 250 ms p95
in each accelerated trial. The readbacks establish the window pixels after
drawing. Physical-display presentation timing remains pending. The signed
Riverbend build passed 22 native input checks. All 213 renderer matrix images
matched the prior run byte for byte, and all 459 core tests passed.

| Half-world Radeon trials and verification | SHA-256 |
| --- | --- |
| `sparse-damage-trial/startup-radeon/native-performance.json` | `14dfae5f84681b983269072d33ee39aa5ff9ab0dcb0146cc89d3fa0df18fedc2` |
| `sparse-damage-trial/startup-radeon/replay.json` | `70b0546d178ff97f6eaa8d8e1786cb389411a1a92f4295ed9636d587c750f681` |
| `sparse-damage-trial/startup-radeon-5k-repeat/native-performance.json` | `b617876cb8bedf51d198bcad27f3ff5320c1b2fb3ed182718fb58493ed4dd01b` |
| `sparse-damage-trial/startup-radeon-5k-repeat/replay.json` | `29dc8942497b5e236c26abcb0f1680457560697aefe1c4e041fa39b802376249` |
| `sparse-damage-trial/final-matrix/matrix.json` | `d4a0707635e8eaf1479e07bdf0c1c40de2a5e0e7b286494e29fd5d44d13e26d6` |
| `sparse-damage-trial/application.json` | `73e5df7c434967c7493601e15b9c7927e3d649b707643bd9129b5573cab85a12` |

### Overlap desktop startup with texture preparation

After the desktop validates the package, the CPU painter now begins setup
alongside Avalonia. It prepares the first two texture tiers for terrain,
coasts, relief, rivers, routes, resources, improvements and fog. The two
tiers for 209 stock bindings occupy 6,164,480 bytes within the 8 MiB startup
budget and the existing texture cache limit. The first ground draw joins
the worker and publishes the images on the painter lane. Art changes and
resource recovery discard any pending request before releasing the cache.
The timing starts after package validation and includes all startup work on
the critical path. The receipt also tracks the interval from the beginning
of admission to the first draw.

The first diagnostic waited for early textures before creating the window.
That reduced first-draw p95 by 29–36 ms, with up to 32 ms p95 of waiting in
renderer setup. Moving the join to the ground draw made it less than 0.2 ms
p95 in the second diagnostic. Each diagnostic variant ran 12 fresh
processes per size at half-world scale on the accelerated virtual display.

| Half-world diagnostic, post-admission p95 | 3840 × 2160 at 2× | 5120 × 2880 at 2× |
| --- | ---: | ---: |
| Synchronous setup | 274.21 ms | 292.82 ms |
| Concurrent painter setup | 243.90 ms | 264.09 ms |
| Concurrent painter and texture preparation, with join in draw | 227.59 ms | 260.62 ms |

| Startup overlap diagnostics | SHA-256 |
| --- | --- |
| `startup-preparation-trial/none/native-performance.json` | `f811395bbda8cc1b78c03ec52ed3710da2ad2b14a2cd2af7be034987313c1521` |
| `startup-preparation-trial/ground/native-performance.json` | `67174384238cdfb80397623e661a3612fbccb96b3e8bf79bc34abc987f083091` |
| `startup-preparation-trial/late-none/native-performance.json` | `bdf90f117a46bea368c5074a99a6d17922aad7bad375d13f9eeb7ddf62282c4d` |
| `startup-preparation-trial/late-painter/native-performance.json` | `bba48904c6acc8f8d946bd9d7c53076b2c6e71ea1ba72a3ab6b6e277a7f2ac20` |
| `startup-preparation-trial/late-deferred/native-performance.json` | `143ee582e4da2debb34833a6cd6a6e35f1751965b20d6810ab5faa4f19b26eea` |

The first signed production build ran three independent trials of 20
process-cold windows for each size and detail level. Every process registered
Radeon graphics activity. Each of the 27 series timed below 250 ms
post-admission. Exact overview replay then identified a GPU raster difference
around the active unit. These initial timings precede the correction.

| Backing size | Full detail p95 | Half-world p95 | Overview p95 |
| --- | ---: | ---: | ---: |
| 1920 × 1080 at 1× | 142.70–163.41 ms | 194.51–211.27 ms | 189.95–200.29 ms |
| 3840 × 2160 at 2× | 182.67–201.28 ms | 219.44–225.37 ms | 207.23–220.60 ms |
| 5120 × 2880 at 2× | 216.65–230.94 ms | 231.37–241.02 ms | 217.32–239.00 ms |

For half-world views, the complete interval from admission start to draw
completion was 418.91–448.85 ms p95 at 4K and 439.62–450.59 ms p95 at 5K.
The corresponding p95 time joining early ground preparation was 1.33–6.24 ms
at 4K and 0.19–4.34 ms at 5K. Physical presentation timing and the warm
pan, zoom and world-change budgets remain open.

| Production startup timing | SHA-256 |
| --- | --- |
| `startup-preparation-trial/production-radeon-1080/native-performance.json` | `864547d6c9c1461c4782b170ef7c64c5cf6ee3adb5571588509ea46696f58675` |
| `startup-preparation-trial/production-radeon-large/native-performance.json` | `4382398b4fdac744ac01e390ca407af326be5cef446308207812e650d505d225` |

On the initial Radeon overview captures, coverage of the small active-unit
stroke differed at 61 pixels in the 1080p window and 115 pixels in the 4K
window. The retained map now composites the overview outline into a padded
opaque CPU patch over the admitted map pixels. Selection and inspection are
drawn in their usual order inside the patch. The patch then presents at the
same backing-pixel phase as the map. The matrix exercises overlapping
selection and inspection at fractional positions at both densities, including
denial of the patch allocation. All 213 earlier matrix images still match.
Nine accelerated overview checks reproduced the serial pixels at all three
backing sizes. The 4K and 5K readbacks also matched the earlier software-window
digests.

The final accelerated build repeated all 27 startup series and met the timing
target in 25. Every detail level met the target at 1080p and 4K. At 5K,
full-detail and overview passed all three trials. The 5K half-world series
measured 247.15, 251.26 and 255.16 ms p95, leaving that case open.

| Final Radeon virtual backing | Full detail p95 | Half-world p95 | Overview p95 |
| --- | ---: | ---: | ---: |
| 1920 × 1080 at 1× | 145.79–164.26 ms | 196.31–202.67 ms | 187.37–200.91 ms |
| 3840 × 2160 at 2× | 200.76–204.15 ms | 223.55–231.00 ms | 205.89–216.99 ms |
| 5120 × 2880 at 2× | 231.26–241.98 ms | 247.15–255.16 ms | 237.06–243.89 ms |

All 540 timed processes recorded hardware graphics work and admitted the
6,164,480-byte ground texture batch. For the 5K half-world view, time from
the start of content admission through the draw callback was 447.63–470.10 ms
p95. The callback took 108.68–111.79 ms p95 and joining early ground preparation
took 0.16–3.00 ms p95. These clocks cover framework work and submission
through the draw callback. Physical presentation remains a separate gate.

All 540 final window readbacks matched the fresh serial renderer outside the
overview inset. The normal and single-worker scalar matrices reproduced all
213 previous images. The startup cache check verified the selected theme,
the 8 MiB bound, and release of pending and admitted textures on replacement
or recovery. The signed desktop passed 22 Riverbend checks at each density,
including malformed asset admission. The managed desktop passed 26 input
and lifecycle checks at each density and all 459 core tests passed. The
startup-preparation build before the overview correction opened all 45 gallery
windows, including native PNG and classic BMP/GIF themes.

| Final native startup and correctness | SHA-256 |
| --- | --- |
| `startup-preparation-trial/final-radeon-1x/native-performance.json` | `a6efcc72859ee875cedac0e1edbf1b10a56e18544725615f4f67b247de20224e` |
| `startup-preparation-trial/final-radeon-1x/replay.json` | `4fcaa5f5d9be8057265605a44dd01c732652a7d513a59cd525c3216eedaac85b` |
| `startup-preparation-trial/final-radeon-2x/native-performance.json` | `1566e0f53ea134cbe2aeb9bbd0bec274a20c7b459e47e39d18664aff0218618a` |
| `startup-preparation-trial/final-radeon-2x/replay.json` | `aa99a949d33fff07ff74f3e2a167f1a48912f720346e73f0da10fc4cdb44141d` |
| `startup-preparation-trial/focus-matrix/matrix.json` | `c2a0e56eadde993c6b87b27b2ba54ce6a1f3b61cd51ee5b58cb1a429b1744042` |
| `startup-preparation-trial/focus-scalar-matrix/matrix.json` | `2abd2e42ca68cc89087d107847a1301a8430e90a137213e79d35c9d744091081` |
| `startup-preparation-trial/final-application-1x.json` | `c343d02952d061c8e4af7c2dab57321660ac870118516dc59900a5987be0b038` |
| `startup-preparation-trial/final-application-2x.json` | `c066c0890a60ceef527c356ec1fe113edbfd86bb5da408e3eb98ca0cf11069ec` |
| `startup-preparation-trial/final-interaction-1x/native-gallery.json` | `f3a4e13f177c2f07b1ed2acc667921e2dd55e48f9d096cbecf46eed9c3bf03f1` |
| `startup-preparation-trial/final-interaction-2x/native-gallery.json` | `17673e4b264c6fd11057ef7de3cd0597c7b01b2f77f3449c6f0e123fccec145c` |
| `startup-preparation-trial/gallery-1x/native-gallery.json` | `feb5677c5b5025873ccedc0a80913ea5cdf6bd0ce72942bb01fd3579a098bca7` |

### First-layout unit and city textures

The map control now starts bounded preparation for the visible unit and city
textures during initial layout of a large desktop viewport. Layout supplies
the actual camera and backing density. The first draw admits the work only
for the same scoped generation, camera, viewport, density, display and moving
unit set. Observer and theme replacement, recovery and detach discard any
queued preparation tied to the old view.

Two diagnostics on the signed desktop each captured 16 process-cold 5K
half-world windows. Layout queued preparation 35.08 ms before the draw at
the median. Median draw time fell from 90.76 to 70.66 ms. Median
post-admission time fell from 219.15 to 205.08 ms, and all 32 captured windows
shared the same map RGB digest outside the overview inset. These short timings
characterize the scheduling change.

| Layout diagnostic | SHA-256 |
| --- | --- |
| `first-body-trial/layout-0/native-performance.json` | `35a9061046ed3637f03134c6430522ec8f46f6a92ddcf8ba68c6db16fac5fb9c` |
| `first-body-trial/layout-1/native-performance.json` | `72dda648dd36d8bfeec2fc25c72883c155cc08a02afaa24821eb33d09401a77e` |

The signed production build ran three independent series of 20 cold launches
for each 4K and 5K detail mode on Radeon virtual Xwayland. Each of the 18
series met 250 ms p95 after content admission. Every process recorded hardware
graphics work, and every first frame consumed the preparation started during
initial layout. The 1080p tests above covered the smaller viewport path, where
the layout preparation is disabled.

| Accelerated virtual backing | Full detail p95 | Half-world p95 | Overview p95 |
| --- | ---: | ---: | ---: |
| 3840 × 2160 at 2× | 178.39–185.50 ms | 201.74–212.10 ms | 191.66–194.98 ms |
| 5120 × 2880 at 2× | 203.26–206.20 ms | 219.70–230.42 ms | 197.99–216.34 ms |

The 5K half-world draw callback took 88.25–96.92 ms p95. Time from content
admission start through the draw callback was 424.26–452.21 ms p95.
The warm camera and world-change budgets and physical-display presentation
remain open.

All 360 actual window readbacks matched fresh serial rendering outside the
overview inset. The matrix checks verify matching first draws and cancellation
after camera changes, same-layout revisions and observer replacement. Both
the normal and single-worker scalar runs reproduced all 213 prior images.
The signed Riverbend desktop passed 22 checks at each density. The native
gallery passed 26 input and lifecycle checks at each density, and all 459
core tests passed.

| Final first-layout timing and verification | SHA-256 |
| --- | --- |
| `first-body-trial/final-radeon/native-performance.json` | `2902267150d541120d0188c203a3b5e4eb29ae2ba21852441f36ee97c1969236` |
| `first-body-trial/final-radeon/replay.json` | `f16a509aa186716207fde3eb85b14d55cbd54c4e3a2043b30628ef395f6b5943` |
| `first-body-trial/normal-matrix/matrix.json` | `e7237720f48c51cd3a2fccb489871ad88d6ea372f9f6ba23133fc6901a69efff` |
| `first-body-trial/scalar-matrix/matrix.json` | `bc49ff385bb3e06f6fad28b4c39256f503b825677e8dde342c6594fcd32c934b` |
| `first-body-trial/application-1x.json` | `0673a6f5912f328390192194266f8d44b6ea94731888c266a8b4b10fec4b6777` |
| `first-body-trial/application-2x.json` | `7142d0fa9fcfc80ea3ca6024e0dd761e13bdec4c0cc6241b4bfbbee5f6009e` |
| `first-body-trial/interaction-1x/native-gallery.json` | `b12d8dbdbfa71665e2f6cfc0449a48a10929ca9f1b9623bccbfd7c9118e02925` |
| `first-body-trial/interaction-2x/native-gallery.json` | `ea001cb9b788a4f95f9e2474e8347b38b129d3ebc46cdda28a74c7f1b19a5235` |

On the same signed build, each accelerated warm diagnostic used 30 warmup
changes and 54 measured changes. All 324 recorded camera and world states
matched the serial renderer. All six series exceed 16.7 ms p95. The pan and
zoom columns measure drawing. The world-change column includes authored update
construction and scene binding as well.

| Accelerated virtual backing | Pan p95 | Continuous zoom p95 | World change p95 |
| --- | ---: | ---: | ---: |
| 3840 × 2160 at 2× | 19.77 ms | 33.83 ms | 25.14 ms |
| 5120 × 2880 at 2× | 27.36 ms | 37.64 ms | 30.19 ms |

Every sampled frame redrew the viewport. At 5K the pan band replay alone took
19.19 ms p95 and the viewport copy took 3.83 ms p95. Zoom built up to 120 new
ground chunks in a 4K draw and 220 in a 5K draw. The slowest 5K zoom draw
took 64.73 ms when it entered ground tier 2. The highest p95 input-to-polled
receipt was 81.66 ms. Visible presentation latency remains unmeasured.

| Warm diagnostic and exact replay | SHA-256 |
| --- | --- |
| `first-body-trial/warm-baseline/native-performance.json` | `2f3b5e3ce0b8409a3dd516c1565a82aa7513a81ae693cf78893e6da7b5b4f29f` |
| `first-body-trial/warm-baseline/replay.json` | `870ed0e243b3feb72f53208439a25ea628fe3221fd3b3d1ffb0e3e5a5c6aed2e` |

### Warm composition experiments

Two variants reproduced all 324 warm reference states on software Skia. On
accelerated virtual Xwayland, alternating padded bands between the viewport
and scratch backing increased 5K pan draw p95 from 25.60 to 29.76 ms. Copying
the odd band interiors raised copy p95 from 0.38 to 1.65 ms. Starting ground
replay during foreground recording increased 5K pan p95 from 28.33 to
32.70 ms. Foreground recording rose from 2.68 to 7.53 ms during overlap.
Both trials were reverted. The Alpha8 tint-mask trial also reverted after
its first 4K pan comparison differed at 108,895 RGB pixels, with a maximum
channel difference of three.

Three paired runs increased the warm tier-2 chunk scratch limit from 8 to
16 MiB. The same 5K tier-2 transition built its chunks 4.61–6.81 ms faster.
5K zoom draw p95 rose in all three runs. The first candidate run matched
108 reference states. The 8 MiB limit remains in production.

| Accelerated virtual trial | Baseline draw p95 | Candidate draw p95 |
| --- | ---: | ---: |
| 5K dirty world, alternating bands | 27.71 ms | 30.87 ms |
| 5K dirty world, overlapped ground and foreground | 28.31 ms | 37.92 ms |
| 4K zoom, larger tier-2 batch | 32.26–36.50 ms | 30.25–32.88 ms |
| 5K zoom, larger tier-2 batch | 40.21–40.90 ms | 41.61–45.02 ms |

The receipts under `build/m2-native/alternating-bands-trial/` each contain
30 warmup and 54 timed changes per measured workload. They are diagnostics
at simulated resolutions. Physical-display presentation remains unmeasured.

| Candidate timing and exact software replay | SHA-256 |
| --- | --- |
| `native-1/native-performance.json` | `4394e54fdf1f935c2b6811212dc2b277a5e844da80135747835b6e65262df3e2` |
| `native-1/replay.json` | `41a35856215a123bb693f14a239b170fdb9cbb72053af7422c7f87cb5607dbe4` |
| `overlap-1/native-performance.json` | `36109c532b8e6eb35584fd8adab618968a3f1f703b49509f5a26b580377d87c4` |
| `overlap-1/replay.json` | `d3fbc15d444a93073b18c8a15f86b6de353b13389fb1950266cdf7cefa4dd034` |
| `burst-1/native-performance.json` | `f70afc9b0e7b6bb95ee145f84764dc70c8d561f2001ce145bd1be7a2240ed4fe` |
| `burst-2-1/native-performance.json` | `db3d55d6a3611bac1abaeee68fc418ae3d1c134c95b389e9e89fbdd1c8c5157a` |
| `burst-3-1/native-performance.json` | `8b3244fa20ed6e8bbe9ccd54b630f2b9ce4d2b77dc82a60d3ab363166b7ad7cf` |

An accelerated 5K pan sweep kept the replay thread limit at twelve and
varied both the number of horizontal bands and the repaired seam width.
Each run used 30 warmup and 54 measured pointer changes. The smaller seams
matched a fresh serial draw on all 108 recorded pan states tested at twelve
and sixteen bands. No variant improved on the repeated twelve-band baseline
for both median and p95. Production retains the existing geometry.

| Bands and seam guard | Draw p50 | Draw p95 |
| --- | ---: | ---: |
| 12 bands, 16 pixels, two runs | 21.69–22.29 ms | 25.95–27.96 ms |
| 8 bands, 16 pixels | 26.81 ms | 31.10 ms |
| 16 bands, 16 pixels | 26.21 ms | 29.73 ms |
| 20 bands, 16 pixels | 29.25 ms | 32.74 ms |
| 12 bands, 8 pixels | 22.33 ms | 27.11 ms |
| 16 bands, 8 pixels | 22.91 ms | 25.99 ms |
| 20 bands, 8 pixels | 25.96 ms | 28.99 ms |

The offscreen `--diagnostic-pan-overlap RECEIPT` probe translates each
previous image by the integer physical pan and compares only its overlap
with the newly rendered image. All 53 adjacent 4K pairs and all 53 adjacent
5K pairs had some differing pixels. The 5K median matched 99.81% of the
overlap. Differences reached a median of 627 blocks of 64 × 64 pixels out
of 3,600. The controlled runs with caption names empty still differed on
every pair. Caption placement explains much of the 5K pixel count, while
sampling and other art still require correction for exact reuse.

| Viewport | Median differing pixels | Median differing pixels with captions absent |
| --- | ---: | ---: |
| 3840 × 2160 at 2× | 4,373 | 4,272 |
| 5120 × 2880 at 2× | 27,666 | 3,570 |

| Band sweep evidence under `build/m2-native/band-count-trial/` | SHA-256 |
| --- | --- |
| `12-16/native-performance.json` | `e1359e6f97f3e703cd5947e6f9a8d808522478ecb420efdc801172c8c08a4e49` |
| `12-16-repeat/native-performance.json` | `d0e62c8499e2d3ad0edbc9c47df7c265715025223c5d7cf4f37ce570cd6844ba` |
| `12-8/replay.json` | `364e978f45d43e95bc50e2c9d61c301fc3ef2f2ed06b960f3d16b8e77917a12f` |
| `16-8/replay.json` | `f5cc55e85d14d3ae4ac86a47058ec61fba97c19669b9fef6c75095d9639a9977` |
| `pan-overlap.json` | `180ac9384654b948712f4f25ae6647fe2c0308d0140dfae906399ffacdc50c0b` |
| `pan-overlap-final.json` | `8a13ec4f941f339c8f3f311c906cf67acc1f49e0c8d05a3ef9d9443c5f52ec6e` |

Splitting the finished viewport into Skia image subsets increased warm pan
draw time at both resolutions. Each subset copied its pixels, so the approach
duplicated all 8,294,400 pixels at 4K or 14,745,600 at 5K for each changed
frame. Baseline viewport transfer took 2.26–2.33 ms median at 4K and
3.61–3.65 ms at 5K. With subsets it took 6.26–11.87 ms at 4K and
12.67–18.09 ms at 5K. The trial was reverted.

| Viewport subset side in pixels | 4K draw p95 | 5K draw p95 |
| --- | ---: | ---: |
| Whole viewport, two runs | 19.15–20.78 ms | 25.56–26.50 ms |
| 512 | 32.45 ms | 37.07 ms |
| 1024 | 24.16 ms | 40.77 ms |
| 1536 | 30.28 ms | 45.81 ms |
| 2048 | 24.60 ms | 57.33 ms |

Each signed desktop diagnostic used 30 warmup and 54 measured pointer changes
at 2× on Radeon virtual Xwayland. The measurements cover the draw callback
with simulated display sizes. Raw timings are under
`build/m2-native/presentation-tiles-trial/<side>/native-performance.json`.
`0` and `0-repeat` are the baseline runs.

### Packed terrain reuse on settled views

Repeated world changes at a settled large viewport can reuse one 4 MiB
immutable packed art image. The atlas stores admitted source sprites. The
ground cache counts the atlas in its existing cap, and changes of camera,
scope, or content release it. The staging bitmap is allocated only while
packing new sources. If the atlas fills, packing retries with the current
batch and can fall back to individual images. The 5K MV29 case verifies
exact pixels after two settled edits and zero retained atlas bytes on resize.

The signed production diagnostic on accelerated virtual Xwayland used 30
warmup and 54 measured changes per workload. The two frame-local baseline
runs measured median dirty ground builds of 1.47–1.68 ms at 4K and 1.43–1.47
ms at 5K. The production run took 0.85 and 0.75 ms, respectively. The draw
and update budgets remain open.

All 324 recorded warm states matched separate fresh serial draws. The
normal and single-worker scalar matrices produced identical sets of 213
images. With the exact earlier authored fixtures, all 213 historical
captures also matched byte for byte. All 459 core tests and the 22 signed
Riverbend native input checks at each density passed. The Linux gallery
captured 45 native case windows and passed 26 input and lifecycle checks at
each density and three mixed-density companion-window checks.

| Accelerated virtual backing | Pan draw p95 | Zoom draw p95 | Dirty draw p95 | Dirty update and draw p95 |
| --- | ---: | ---: | ---: | ---: |
| 3840 × 2160 at 2× | 18.44 ms | 39.99 ms | 21.70 ms | 24.34 ms |
| 5120 × 2880 at 2× | 25.79 ms | 41.59 ms | 26.69 ms | 29.06 ms |

| Receipts under `build/m2-native/retained-ground-atlas-trial/` | SHA-256 |
| --- | --- |
| `baseline-1/native-performance.json` | `004a3ae67e7fb1965a1e44c73a6172bdc0344f5ce58cda1a25f1955dd0cc8e57` |
| `baseline-2/native-performance.json` | `5df5ebac0fb87c39111de9156260fc7d15c61c294d124fa70fa49e59cd2faef9` |
| `final-native/native-performance.json` | `aae1d83737f0e7288b69e66fe914170c9631537682ba08dc62b849e1a8e3157e` |
| `final-native/replay.json` | `6f342cdf1fa602a0ef8e9fe678a9c99743c7d6ca1632fd34f7edded9813fa1f3` |
| `final-matrix/MV29-ground-resize.json` | `26f0cc4bce2034d445329870022217699c130b08f7f788baeb5e717c4c4f03d1` |
| `final-matrix/matrix.json` | `5a2a6e7f504dc1894865896c44c6538fc940f3d3ade184e8df0f9e4e733c2fac` |
| `final-scalar-matrix/matrix.json` | `488e3d3930035385958ede6f353b1bc93f1dcb772b1a776c2fee2908cb14fb1b` |
| `archived-fixture-matrix/matrix.json` | `69d17193aa21362b8f6c941866ddd0c1525ea4f36250172d63063cf66a717ee9` |
| `riverbend-1x.json` | `825607f39ec7d5b6a5f526fa6bc6d4063189632df759e0262a0728f96d5f51d4` |
| `riverbend-2x.json` | `6d158e3ebc37a74dbb8d6d6b8b9713fe222e4fc7d4b946ca3ca7f1a6ad3d5573` |
| `gallery-1x/native-gallery.json` | `503de2cfcd3061da8e4ba9d80dce6ed8b70019a628b27dc690d108765f90aa89` |
| `gallery-2x/native-gallery.json` | `7503ecc39687aa7f7903fa28bef55c91f3447e9b66007a8b5b208f6d84ae5726` |
| `gallery-mixed-dpi/native-gallery.json` | `726eca0792629681d160a7fedddf625337cca607aac0a1c9986aa80694a06899` |

### Settled terrain across revisions

A large viewport at a settled camera now retains its composed terrain in
the existing terrain-scroll backing. Same-layout revisions accumulate
terrain, route and city-connection invalidations. The painter updates the
affected opaque chunks and copies the finished terrain into foreground
bands, including the guarded seam rows. The foreground still paints the
latest scoped observations in depth order. Observer, content and viewport
replacement release the backing. Ambiguous chunk overlaps near a device
pixel boundary use the full ordered replay.

Two paired 4K and 5K Radeon virtual trials used 30 warmup and 54 measured
world changes per size. Median terrain damage fell from 8,294,400 to 135,424
pixels at 4K and from 14,745,600 to 240,100 at 5K. A separate replay of 108
recorded world states matched fresh serial pixels. The matrix also exercises
two revisions before the next draw, a tile becoming unknown, the flat and
wrapped layouts, a near-tie fallback and observer replacement.

| Accelerated virtual backing | Full-ground draw p95 | Settled-terrain draw p95 | Settled update and draw p95 |
| --- | ---: | ---: | ---: |
| 3840 × 2160 at 2× | 22.02–22.39 ms | 19.80–19.93 ms | 22.44–22.52 ms |
| 5120 × 2880 at 2× | 26.91–27.31 ms | 25.82–26.76 ms | 28.74–29.05 ms |

| Paired diagnostics under `build/m2-native/settled-terrain-trial/` | SHA-256 |
| --- | --- |
| `baseline-1/native-performance.json` | `8d8c3af7f35f8fd9d8d699514452067bdcdff1b957b7d6acbd86eed44507383e` |
| `baseline-2/native-performance.json` | `e28f204247b251a244aa3e14354c11430a84018a32c451dccfe811f4f2fc6ca8` |
| `reuse-1/native-performance.json` | `bf4c11264b942d67570a71988eb335cb728f9c9d9fd2756aac07c4652660bf92` |
| `reuse-2/native-performance.json` | `b9cda9da2c826dc1770daf0fe45e6b71bbafcb98b475858f1ff558f61560af50` |
| `replay-input.json` | `c380ea4721662f36090b75b5af2c919e0a4d62eb2cce7773a79bc6e5cc904f49` |
| `first-replay.json` | `a61fa1da0d584a4c6946180e36361e710fadeaf6b7e910c456bf536632d46ebc` |

With settled reuse enabled above eight million backing pixels, the guarded
build ran three independent wrapped series per size, each with 30 warmup
and 300 measured changes on virtual Xwayland. The views
covered 16,587 distinct tiles at 1080p and 4K and 16,781 at 5K out of 31,752.
The median terrain damage stayed at 135,424 pixels at 4K and 240,100 at 5K.
The foreground still repainted the viewport. Every run exceeded the
16.7 ms update-and-draw p95 target. The 1080p draws alone fit 16.7 ms p95.
All nine runs recorded graphics-engine activity. At 5K the band stage,
including terrain seeding, took 17.86–18.80 ms p95 and the viewport copy took
4.76–4.88 ms p95.

| Accelerated virtual backing | Draw p95 | Update and draw p95 | Update and draw p99 |
| --- | ---: | ---: | ---: |
| 1920 × 1080 at 1× | 16.06–16.25 ms | 18.27–18.61 ms | 20.07–20.40 ms |
| 3840 × 2160 at 2× | 19.79–21.30 ms | 22.50–24.03 ms | 23.72–33.86 ms |
| 5120 × 2880 at 2× | 28.64–29.52 ms | 31.44–32.06 ms | 33.69–34.43 ms |

The two flat diagnostics used 54 measured changes after 30 warmup changes.
They covered 13,774 distinct tiles at 4K and 13,871 at 5K. Combined p95 was
22.21 and 30.26 ms respectively. The highest p95 from input to a polled draw
receipt was 82.42 ms across the wrapped and flat runs. Physical presentation
remains unmeasured. All 2,808 recorded revisions matched fresh serial
pixels. Both renderer matrices reproduced all 213 images, and all 459 core
tests passed. The signed Riverbend build passed 22 native input checks at
each density.

| Production settled-view evidence under `build/m2-native/settled-terrain-trial/` | SHA-256 |
| --- | --- |
| `wrapped-1x/native-performance.json` | `b8827fae884db3da9991b08ad196ff3fc6e4f082ae038a600cf383916bd31033` |
| `wrapped-1x/replay.json` | `f40f9078faa1c1b44c200f034b44ee5cd1374a21e7300b23b5f83a837209c090` |
| `wrapped-2x/native-performance.json` | `027a77beb9a2183ef95bc652a34a5c2c9371a8d77e347192cfcb614a8f0d5afc` |
| `wrapped-2x/replay.json` | `1c186c1a64c965dc0d03bd27e243d35095f08e81370ea334f11f5346b4c92591` |
| `flat-2x/native-performance.json` | `d7a6434f0c7f17c550fe03153c4571c359f8b155d471d473905d31379dc20a55` |
| `flat-2x/replay.json` | `c467b5b6cf9c568be4fde5b37d83ab36ea476e1351b50b10a6ed1e2c875346d6` |
| `final-matrix/matrix.json` | `0e0705c9bb1096db7f804de982b447fb5c02fa861193135f95fa97d759920e66` |
| `scalar-matrix/matrix.json` | `98486c09d714ab4d7d3da9e20f723c690db2475992d41d0f78b2db96973c4985` |
| `riverbend-1x.json` | `fa5ce3232b85701ba977113e913c52bfc90f58af30d1f55e79f2232328bcff0f` |
| `riverbend-2x.json` | `4700c22d689b0e638a0d0b2d0b9cd8b4febd607d72e3b0c8a431e83c61374a48` |

The final build extends the settled terrain, packed art and seam scratch
reuse to banded 1080p views. Median terrain damage was 34,225 pixels out of
2,073,600. Median terrain build fell from 2.48–2.82 ms to 0.84–0.86 ms in
the wrapped series. Three wrapped and three flat 1080p series of 300 measured
changes each met the combined update-and-draw budgets after 30 warmup changes.

| Accelerated 1080p at 1× | Update and draw p95 | Update and draw p99 | Input to polled receipt p95 |
| --- | ---: | ---: | ---: |
| Wrapped, 16,587 distinct tiles | 14.47–14.81 ms | 15.89–17.55 ms | 55.26–55.82 ms |
| Flat, 13,774 distinct tiles | 14.13–14.72 ms | 15.43–16.19 ms | 55.27–55.45 ms |

The final build's 54-change 4K and 5K rechecks measured combined p95 of
22.01 and 31.10 ms. Both still exceed 16.7 ms. Their median terrain damage
matched the earlier large-viewport trials. The expanded matrix verifies
1× and 2× cache correctness and reproduced the same 213 PNGs on the normal
and single-worker scalar paths. All 1,962 revisions recorded on the final
build matched fresh serial pixels in replay. The final signed Riverbend
build passed 22 native input checks at each density.

| Final settled-view evidence under `build/m2-native/settled-terrain-trial/` | SHA-256 |
| --- | --- |
| `extended-1x/native-performance.json` | `334a907b7dbace0a95d9eed8601950836b12161fc8cecab347f4747637b8564b` |
| `extended-1x/replay.json` | `95ab7fc273547bd31b207f42d5f9e7b5808462aeb37f46dd5d8941f2c743a517` |
| `extended-flat-full/native-performance.json` | `f87db60031a286991567d288bee2de59033e702b8462a5dd7b793ecb4a4750de` |
| `extended-flat-full/replay.json` | `875398c9f87a1eadb4f32a978ec179143432d4f672a0aefa7bfc365258912c0b` |
| `extended-flat-1x/native-performance.json` | `18b270366db509af081aba5b6d9a0e53556a9b0e82f7e01093e625eef2dc7870` |
| `extended-flat-1x/replay.json` | `59c188c8c57134e958624dae397b769c516b2098d26c24d567c5a46517fb575c` |
| `extended-2x/native-performance.json` | `8d683d4dc0fc461dc7ee19d06b7efcdea3508acbdc7562fb35f7635215193ac2` |
| `extended-2x/replay.json` | `e55579c570b0edb9a65ec783e4f5637da44788cd50e1db5d4bf22449c4d422fc` |
| `extended-matrix/matrix.json` | `5c1a7eb7d28226216b4c5f5712817ade548285dd2a3673f431b28b4ac103ca70` |
| `extended-scalar-matrix/matrix.json` | `5d00df18470970a31ccd536266028600b0f4e3d3ed74b1fd5fbe2474e387b94d` |
| `final-riverbend-1x.json` | `35766997ca9363505609bfcf7ed7d6006da0fd177f143ec53bf3460781242ca8` |
| `final-riverbend-2x.json` | `018d1ab337206158649fcb2b8d964b4a753e8f6ce5df724fb14a91b4d7f2b88e` |

### Higher-resolution body preparation during zoom

During an inward zoom in a large desktop viewport, the renderer prepares
admitted unit and city bodies ahead of the 4× texture tier. It prioritizes
the region near the center of the current view and holds one batch of up to
four images and 1 MiB of output. Publication fits within the existing
96 MiB texture cap. The draw consumes completed work and resolves any
required misses through ordinary texture lookup. Observer, layout or art
replacement and recovery discard pending work.

Two paired diagnostics on Radeon virtual Xwayland used 30 warmup changes
and 54 measured pointer changes per size. Baseline crossings decoded
24 bodies at 4K and 19 at 5K during the draw. The prepared builds had zero
body batch misses at those crossings. The remaining texture work was below
0.81 ms. The longest background batch took 9.39 ms elapsed.

| Accelerated virtual backing | Baseline first 4× draw | Prepared first 4× draw | Baseline zoom draw p95 | Prepared zoom draw p95 |
| --- | ---: | ---: | ---: | ---: |
| 3840 × 2160 at 2× | 43.15–43.78 ms | 13.73–14.28 ms | 37.13–42.01 ms | 16.95–18.48 ms |
| 5120 × 2880 at 2× | 40.34–40.41 ms | 20.97–21.75 ms | 40.34–40.41 ms | 33.38–35.69 ms |

| Paired diagnostics under `build/m2-native/zoom-body-preparation/` | SHA-256 |
| --- | --- |
| `baseline-1/native-performance.json` | `3bcc610dd6f9027e589fd95a7f4c5c0f7d2c5eb8fc5ad67101efd56736b3b038` |
| `baseline-2/native-performance.json` | `040b849eafd69e2f6f04b14956cc24145625af3f65ca9539965861254e004b5d` |
| `ahead-1/native-performance.json` | `67439429b1ef5fead10e6bb36c6f70c728d7ce7ff766458fcda5b8480e1aba9b` |
| `ahead-2/native-performance.json` | `12107c9f2657d9a3682fd99c1fd1ed9723c017a45ee8d34fed934de90bfc69ad` |

The production signed desktop ran three independent continuous zoom series
per size, with 30 warmup and 300 measured pointer changes per series. Every
desktop process recorded graphics-engine activity. The 1080p draw met both
budgets. The 4K and 5K draws met the 33.3 ms p99 budget and exceeded the
16.7 ms p95 budget. At 5K band replay alone took 18.66–19.05 ms p95, and a
ground tier change rebuilt up to 220 chunks. The first 4× body draw took
13.26–16.85 ms at 4K and 20.41–24.95 ms at 5K. All 2,700 recorded cameras
matched fresh serial pixels. The input interval ends at the polled draw
receipt. Physical presentation remains unmeasured.

| Accelerated virtual backing | Zoom draw p95 | Zoom draw p99 | Input to polled receipt p95 |
| --- | ---: | ---: | ---: |
| 1920 × 1080 at 1× | 9.52–10.01 ms | 10.78–11.53 ms | 51.40–53.54 ms |
| 3840 × 2160 at 2× | 17.48–18.50 ms | 18.59–19.76 ms | 55.15–55.44 ms |
| 5120 × 2880 at 2× | 25.97–27.24 ms | 29.51–30.68 ms | 55.75–56.21 ms |

The focused matrix check verifies batch admission, serial pixel equality
through the 4× crossing, and discard of the pending batch on observer
replacement. Both the normal and single-worker scalar runs reproduced all
213 prior images. The pan-overlap probe reproduced its earlier comparisons.
The signed Riverbend build passed 22 native input checks at each density.
All 459 core tests passed.

| Production zoom evidence under `build/m2-native/zoom-body-preparation/` | SHA-256 |
| --- | --- |
| `final-1x/native-performance.json` | `e556858be21ee3648eff65612c466249156df55a3d304047f65e389bab831177` |
| `final-1x/replay.json` | `3023708548b577a4b10b5eefe6215605e78a63cd3353818cc467422ba9095304` |
| `final-2x/native-performance.json` | `53549d48554386228def6cb90ded56d9123c44ff9ef892a6f6c0ff01c3f9d0a1` |
| `final-2x/replay.json` | `1afd18a7427dbbdaf47b3b43e99028883cb66fe3235a75aab1b644bd336da515` |
| `final-matrix/matrix.json` | `6f221480ad157db101cdc492d145e83f16e835269626ddc3cb39ac53764f422c` |
| `scalar-matrix/matrix.json` | `967c689dffc56d99f732e0e9226b1956e8ce737655065944e3c9a9c1fd1265cd` |
| `riverbend-1x.json` | `c51f1b58586b91bfaa3eadbd2c903c2f68e1859f576daec9abeff5f1ac12a52b` |
| `riverbend-2x.json` | `722dc05eff3c053ff9e9438434de88b08d34b5a2920fe3b6ca77c64a0ade6a1b` |

### Settled-view resource recovery

The native resource-recovery driver measures the **Reload textures** action
at the settled half-world camera. It records refreshed chunks, draw time,
input-to-receipt time and the retained selection. The 5K diagnostic exercised
three actual reload gestures and each frame matched a fresh serial renderer.
It ran concurrently with other validation, so those sample timings are
diagnostic. The capture and replay are
`parallel-clear-recovery-smoke/native-performance.json` (SHA-256
`5a97d78e29e1b3145466a88a639ac5c29b6e3c3fbd7110b7e060d47e2028700a`) and
`parallel-clear-recovery-smoke-replay.json` (SHA-256
`d41a9becac1689b7a95c69f5b376bc77795cf24cd707c5126a415242da39929b`).

An earlier full software X11 recovery test ran three independent trials at each
size, with 20 reloads per trial. All 180 measured draws matched a fresh
serial frame, including the selection. At least one draw trial at each
size exceeded 250 ms p95. The receipt latency includes polling. Physical
presentation timing remains pending.

| Backing viewport | Draw p95 across trials | Input-to-receipt p95 across trials |
| --- | ---: | ---: |
| 1920 × 1080 at 1× | 197.09–290.55 ms | 249.44–320.81 ms |
| 3840 × 2160 at 2× | 251.76–285.57 ms | 293.68–318.10 ms |
| 5120 × 2880 at 2× | 270.39–286.60 ms | 321.69–345.62 ms |

| Recovery receipt | SHA-256 |
| --- | --- |
| `parallel-clear-native-recovery-1x/native-performance.json` | `1d644cb24f476cc14422b598b4d388acdd9e5aadb27264b87a11d199f0436638` |
| `parallel-clear-native-recovery-1x/replay.json` | `f412805d3ecd262babf278111c1e76bb964f06926750bb81ba4022975c4146f6` |
| `parallel-clear-native-recovery-2x/native-performance.json` | `d17be119f257dca6be3e904c34a8f27be5d0333d1098b0652ccd3a917ef194b5` |
| `parallel-clear-native-recovery-2x/replay.json` | `c733a3895972a12377c24325008f590efced7ece0615e41927eccd71a7f6c7f7` |

The current signed desktop repeated three independent series of 20 actual
texture reloads at each size on Radeon virtual Xwayland. Each reload held
the settled camera, selection and observation revision and rebuilt the
needed ground. The settled views covered 16,587 distinct tiles at 1080p and
4K and 16,781 at 5K out of 31,752. Every process recorded graphics-engine
activity. All 180 states matched fresh serial pixels in replay. Every series
met 250 ms p95 through the draw. The polled receipt times include the click
and wait for the recorded frame. Physical presentation is unmeasured.

| Accelerated virtual backing | Reload draw p95 across runs | Click to polled receipt p95 across runs |
| --- | ---: | ---: |
| 1920 × 1080 at 1× | 98.60–100.31 ms | 135.06–155.76 ms |
| 3840 × 2160 at 2× | 101.97–116.44 ms | 141.47–160.19 ms |
| 5120 × 2880 at 2× | 103.76–108.22 ms | 154.49–156.79 ms |

| Reload receipt under `build/m2-native/retained-ground-atlas-trial/` | SHA-256 |
| --- | --- |
| `recovery-1x/native-performance.json` | `bf8576467b516569d9f835c047e155b7b7fc8128f96b155da7df1a3699adbe1a` |
| `recovery-1x/replay.json` | `d23d5e5429ecdc9d18a42045028f3f9597069b999a8a05504046d2a67e08713f` |
| `recovery-2x/native-performance.json` | `6dcd80c0d0716524363960391a4748f375e2775421bb833d9d9580208d5dc038` |
| `recovery-2x/replay.json` | `e2bb52976de25fefb0e5559704af34613ef07fd83b181d87c6a89dd7a17064f2` |

The Linux CI step was exercised locally with the checked archive hashes.
It passed 45 X11 gallery windows, 26 interaction and lifecycle checks at
each density, 22 scoped Riverbend checks, and three exact 1080p reload
replays. The full matrix produced 213 screenshots. Both the AVX2-disabled
and single-processor runs matched the normal matrix byte-for-byte. The
headless suite passed 459 tests.

| Linux CI step receipt | SHA-256 |
| --- | --- |
| `linux-ci-verify/map-matrix/matrix.json` | `03c53a5c1cdbfeac29694b754ff6ab1a995620fa979926f79b2c0a31a5d0fff7` |
| `linux-ci-verify/map-gallery-1x/native-gallery.json` | `3ebf0ae765a5addf0acc8b6fe837c7491acac8fcf7299b1f0876288d0ee24a05` |
| `linux-ci-verify/map-gallery-2x/native-gallery.json` | `fc15ffc29f697cb3d2bbe4c3db46a34cba1e65a6deca3d63a31714177a31f030` |
| `linux-ci-verify/application-map-native.json` | `869058022216078cd717834c6a2af7d479a59cb88f2991d7d2ab81c3abdb845d` |
| `linux-ci-verify/map-recovery/replay.json` | `93c5bd905b46e384037c1fa36ea7760028d54afb700ce71e7ce329f87da690e0` |

All 213 matrix PNGs matched the earlier run byte-for-byte with the normal
worker count and with `DOTNET_PROCESSOR_COUNT=1`. Both runs exercised stock,
native PNG and classic BMP/GIF art, partial ground allocation failure,
cache release, and recovery. The X11 suite passed 26 input and lifecycle
checks at each of 1× and 2×. All 459 core tests passed.

The rebuilt desktop repeated the 2× X11 diagnostic with three warmups and
ten changed frames per workload. Pan, zoom and dirty-world draw p95 were
62.35, 111.47 and 71.25 ms at 4K, and 90.91, 247.57 and 95.86 ms at 5K.
All sampled pans fell back to full redraws while preparing no textures.
Zoom peaks still included first-time chunk construction. These virtual
X11 runs measured drawing and polled input receipts. The receipt is
`texture-prefetch-native-performance/native-performance.json` (SHA-256
`d8bd0f1ee0498d5fa5c575f1235c8a0e6d4fc33da546ee76fbb8d4d462cb6156`).

An experiment kept terrain scroll reuse at fractional zoom by repainting
sample-boundary rows and columns. Its 54 offscreen pan frames matched fresh
renders. In the short 2× X11 trial, the repaint covered under 9% of each
viewport, but the complex clip raised pan draw p95 to 73.65 ms at 4K and
117.92 ms at 5K. The renderer keeps the earlier full-redraw fallback for
these sampling phases. The rejected experiment's receipt is
`pan-repairs-native/native-performance.json` (SHA-256
`d0a073fa978c4734646d07dd2e55b402977f4e9d37cecd07eb9bf9bef5c8119a`).

The current renderer indexes foreground cells and city labels by row when
it prepares the observer's scene. Pan and zoom use the indexed cells in
the same drawing order, including wrapped copies. All 213 matrix PNGs and
the per-board tile, object and label counts match the previous renderer.
An 18-frame offscreen pan comparison at each size found only a small
change in the median draw cost. The 4K and 5K 2× X11 diagnostic captures
ten measured changes after three warmups and reports time spent in
terrain, foreground, label layout, and the final viewport copy.

| Simulated backing viewport | Pan draw p95 | Median terrain | Median foreground | Median label layout | Median viewport copy |
| --- | ---: | ---: | ---: | ---: | ---: |
| 3840 × 2160 | 56.77 ms | 9.21 ms | 34.77 ms | 0.23 ms | 1.93 ms |
| 5120 × 2880 | 91.47 ms | 8.30 ms | 55.48 ms | 0.20 ms | 3.32 ms |

All sampled X11 pans redrew the viewport. Foreground drawing accounts for
most of the pan time. First-time chunk construction still contributes to
the zoom peaks. The diagnostic receipt is
`pan-stages-native/native-performance.json` (SHA-256
`9a6989a9e52887c83539033538748d7ee308bce44e262af80ea1750f5a8daa23`).
Physical presentation and GPU timing remain unverified.

Large full foreground draws now record the existing sprite and overlay
commands in depth order and replay them over disjoint rows of the opaque
viewport. The workers share the viewport's original sampling transform.
A temporary surface covers padded band edges so that antialiasing matches
serial drawing across the boundaries. It is released before the frame
returns and is bounded by one viewport. The dirty pass draws complete
intersecting city captions with an eight-backing-pixel margin and restores
the unchanged ring around the published damage. This fixed a pre-existing
seven-pixel difference in the 1080p world-change diagnostic.

All 213 gallery images matched the previous run with both the normal
worker count and one processor. Injecting allocation failure in the
optional foreground surface produced an exact serial frame, including
grid, ownership and city-radius overlays. All 459 core tests passed.
The X11 interaction suite passed 26 checks at each backing density.
The final offscreen zoom diagnostic matched 54 changed frames to fresh
serial renders. Its receipt is `foreground-label-guard-zoom.json`
(SHA-256 `d8ef447c3e7186092b0e03cd76d39c18d2321fd370a9e184b0c40f1db3ee9e13`).

The full offscreen pan workload compared 2,700 changed frames with
independent serial foreground renders. Each of the three runs per size
used 30 warmup frames and 300 measured frames. Every series included
280 integer-scroll frames and 20 frames that repainted the terrain.

| Simulated backing viewport | Earlier software pan p95 | Parallel foreground pan p95 | Parallel foreground pan p99 |
| --- | ---: | ---: | ---: |
| 1920 × 1080 at 1× | 16.64–19.99 ms | 12.84–21.38 ms | 28.08–34.81 ms |
| 3840 × 2160 at 1× | 47.26–52.79 ms | 29.01–30.80 ms | 69.33–71.28 ms |
| 5120 × 2880 at 2× | 74.19–83.33 ms | 39.94–41.54 ms | 49.00–50.96 ms |

The first 1080p series briefly shared the machine with regression checks.
The receipt retains every sample as
`foreground-final-pan.json` (SHA-256
`837c2facda14e36ce8be4d13a67408241dbd55c905393c883ccb82069646cf0a`).

The final offscreen dirty-world workload retained the partial redraw path
for all 2,700 changed frames. Every frame matched an independent full
serial render. Draw and preparation together gave p95 ranges of
16.73–17.24 ms at 1080p, 42.38–67.19 ms at 4K and 49.54–57.47 ms at 5K.
The third 4K run and first 5K run overlapped functional verification.
The receipt is `foreground-label-guard-dirty-full.json` (SHA-256
`accbdf08ce74bfd84de278c6a537025018df74e735c0214dfe81f65297b4140b`).

The 2× X11 diagnostic captured ten measured changes per workload after
three warmups. Full 5K pans fell from a 91.47 ms software draw p95 to
51.40 ms. At 4K, median foreground time fell from 34.77 ms to 23.46 ms,
with draw p95 at 57.36 ms. The X11 p95 values for zoom were 97.48 ms at
4K and 227.87 ms at 5K, and dirty-world p95 was 74.12 ms and 69.39 ms.
The sampled faction-color changes repainted the whole viewport.
These virtual X11 measurements leave physical presentation and
GPU timings pending. The receipt is
`foreground-final-native/native-performance.json` (SHA-256
`d3996ee6834b52f7bfc784d129896af2b59bb7e914f676c84637d2d99b51fa45`).

The final lifetime diagnostic released all retained viewport and world
rasters on each of 110 observer switches. Every iteration then rebuilt and
exercised all three viewport surfaces in a 960 × 600 scroll. The collected
managed heap remained within 264,377,296–264,435,344 bytes. The final sample
retained 6,912,000 viewport bytes, 3,656,640 ground-chunk bytes, and 4,543,328
texture bytes. The receipt is `tint-scrolled-lifetime.json` (SHA-256
`6f3c1e42fb9a703532cf66e060db4e76f8bdd74bf13d15f0e91504ae68952703`).

The diagnostic `performance-mips.json` (SHA-256
`3a3d3b65407a30d0653ea6008239c828336dda5ba9cc722b4cdda36d91f60f60`)
compared each measured changed frame to a fresh Skia raster. Preparing both
small mips from one decode reduced the 4K zoom p95 from 3235 to 2462 ms in
these 18-frame trials. The 5K 2× zoom p95 was 83 ms. Pan and world changes also
exceeded the interactive targets in software. Cache preparation and whole-view
rasterization remain performance work.

The baseline X11 host run completed three independent repetitions at each
simulated viewport size. All 8,100 measured frames changed camera or world
revision through real X11 input. Each series followed 30 warmup changes. The
raw `host-performance/native-performance.json` (SHA-256
`2311101ec001edd517e2b2496a02ac4828d4afedb681a9231b6ccda1b8e76be2`)
records individual runs and polled input-to-draw receipts. The largest 5K zoom
stalls built 220–264 chunks in one draw as backing resolution crossed 1×.

| Simulated physical viewport | Distinct tiles at half-world start | Pan p95 across runs | Zoom p95 across runs | Dirty world p95 across runs |
| --- | ---: | ---: | ---: | ---: |
| 1920 × 1080 at 1× | 16,587 of 31,752 | 19.71–21.46 ms | 20.38–21.09 ms | 22.15–26.07 ms |
| 3840 × 2160 at 1× | 16,781 of 31,752 | 60.42–68.32 ms | 610.20–617.19 ms | 54.92–57.44 ms |
| 5120 × 2880 at 1× | 16,587 of 31,752 | 91.05–94.72 ms | 4172.51–4225.48 ms | 87.50–90.26 ms |

The backing-tier adjustment and direct selection composition were remeasured
in `tier-host-zoom/native-performance.json` (SHA-256
`4445ea6fe7ede5435263576f0efef6d8a639cc2298eced6e910f50f565864231`),
with three independent sets of 300 changed zoom frames at each size. Displayed
detail, draw selectors and layer ordering were preserved. At 4K, the timed
frames had zero new chunks after warmup. At 5K, occasional cache eviction
still caused new chunk builds. That build used up to 240 MiB for ground
rasters and 96 MiB for image textures.

| Simulated physical viewport | Zoom draw p95 across runs | Zoom draw p99 across runs |
| --- | ---: | ---: |
| 3840 × 2160 at 1× | 52.83–53.11 ms | 60.02–64.14 ms |
| 5120 × 2880 at 1× | 85.52–88.18 ms | 539.60–548.99 ms |

With the 240 MiB ground limit, the separate 5K recheck
`resident-host-zoom/native-performance.json` (SHA-256
`337628fcb130b1b987f00b1e79b7e6b5b4cf229cf4b80122acc284a5dc970c50`)
retained the three half-density bands and the 1× working set. Each of the three
300-frame runs prepared ground on one measured frame. Draw p95 was
83.99–85.23 ms and p99 was 85.59–102.28 ms. Polled input-to-draw p95 stayed
between 183.29 and 183.91 ms.

These are Avalonia draw durations under X11 software rendering. The associated
input receipts are polled. The software draws above exceed the interactive
budgets. Accelerated Xwayland output and desktop graphics-engine activity now
have separate virtual-display evidence. Calibrated physical displays, visible
presentation and per-frame GPU measurements remain pending. Browser and
other-platform execution are deferred.

The offscreen pan diagnostic forced the supported direct viewport fallback
and compared its output with the usual retained viewport path. All 54 changed
frames in each report were pixel-equal to their fresh references across 1080p,
4K and 5K. Each size had 18 measured changes and three warmup changes. Retained
and direct 5K median frame times were 76.42 and 74.67 ms, with two new-chunk
spikes in each series. This short run shared the machine with other validation.
Receipts are
`navigation-retained-diagnostic.json` (SHA-256
`40117ec0a78f62ebe886a35d54a4d5576c60b62c5991752c4e4801d6903fe50c`)
and `navigation-direct-diagnostic.json` (SHA-256
`806b9fc23e4eb43fc5b24c6f946d4a70fde99ed86e7c5f08c53528cf44b24a78`).

An earlier dirty-world X11 diagnostic measured ten changed revisions after
three warmup revisions per size. All sampled draws included active effects
and covered approximately half the distinct world. The fixture reassigns a
faction color, so each sampled change repainted the viewport. Software draw p95
was 33.47 ms at 1080p, 78.77 ms at 4K and 98.68 ms at 5K. These short,
concurrent runs are diagnostic. The raw receipt is
`current-dirty-host-diagnostic/native-performance.json` (SHA-256
`7dc79695c1d574f059fc8887636f28c85d3c0ff23fecab3f32137c6e7f58584c`).

## Recording acceptance

As a row progresses, retain its initial assessment and append/update an evidence
record with these independently assessed fields:

| Field | Required value |
| --- | --- |
| Feature | MV ID and matrix case-family/variant IDs, including combined and failure cases |
| Implementation | `open`, `partial` or `implemented`; link production renderer/projection/binding sources |
| Native acceptance | `pending`, `passed` or `failed`, with executed OS/backend and source-bound case, screenshot and visual-review evidence |
| Browser acceptance | `pending`, `passed` or `failed`, with actual published WASM/browser execution evidence |
| Live integration | `authored-only`, `partial` or `integrated`; identify which cases consume real scoped game observations and which use authored values |
| Provider follow-up | Missing operation/projection, owning specification and milestone; no generic “later gameplay” omission |
| Remaining failures | Failed/skipped cases and unavailable hardware/platform evidence; a skipped required case leaves its gate pending |

Full feature acceptance requires all matrix cases and cross-cutting checks for
the named backend. Keep hardware performance in separate records keyed to the
same source/content identity. Neither `implemented` nor `authored-only` means
the live mechanic or all-platform milestone is complete.

## Producer and milestone boundaries

| Renderer rows | Owning inputs and current follow-up boundary |
| --- | --- |
| MV01–MV09 | W01 terrain/features and W03 knowledge, plus F03/P03 selected content. Extend the existing scoped projection and safe adjacency in M2, including huts and compound features. Later terrain-work/revelation producers do not block authored visual cases. |
| MV10–MV18 | P02 appearance policy, F03 rules/colors, W03 admission and U01/unit/city state. Supply permitted body/status/city/color inputs in M2. Live changes from orders, combat, city development and civilization rules join through their M3–M4 owners. |
| MV19, MV23 | W01 geometry and W03/P02 disclosure. Map modes, safe overview inputs and navigation are M2 work, not prerequisites for a later playable turn. |
| MV20 | U03 routes and C07 trade-bonus paths under W03 privacy. M2 draws explicit authored paths and integrates any available safe producer; missing live path/trade computations remain named M3–M4 operations. |
| MV21 | W04 work counters and W03-permitted details. M2 renders/inspects supplied values; later work progression is a distinct gameplay join. |
| MV22, MV24 | P04/P10 notice/effect lifetime and completion. M2 implements presentation, skip/cancel and authored event sequences; U02/U06 and other semantic event producers join in M3–M4. |
| MV25–MV30 | P01/P02 camera/composition/lifetime and P03/P04 art/fonts. These renderer capabilities and their native/browser tests are M2 work; no game-rule dependency justifies omitting them. |

These assignments do not create a parallel engine or authorize UI reads of
mutable state. Replace authored input providers with scoped production providers
when ready, preserving the same renderer and explicit information contract.

- W03 and N04 own observer-safe retained city facts. The current city-site bit
  has no retained name, owner/style, walls or recorded garrison in the scoped
  projection. C01 and C08 changes must update those facts through W03 before they
  can replace the authored remembered and authorized-reveal cases in M3–M4.
- W01 establishes the hut candidate from the coordinate and terrain. W03 and
  W06 own disclosure and consumption of its occupancy-dependent presence. Live
  airbase display uses admitted own aircraft and needs W03 filtering for other
  occupants as the M3 movement and observation producers are joined.
- U04 and W04 supply human unit order and work observations when the stored
  rule resolves. Autonomous decisions, moving-unit previews and combat need
  explicit U03/U04/U06 data and P10 completion integration in M3–M4.
