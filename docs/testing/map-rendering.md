# M2 map-rendering acceptance matrix

This is the implementation and validation acceptance contract for
[P02 MV01–MV30](../algorithms/P02-map-rendering-and-minimap.md#minimum-map-visual-feature-set).
P02 owns required visible behavior; [P01](../algorithms/P01-map-projection-and-interaction.md)
owns target identity and interaction; [P03](../algorithms/P03-graphics-and-asset-processing.md)
and the [stock map bindings](../content/map-assets.md) own art inputs.
The [coverage ledger](../validation/map-visual-coverage.md) records results, not
exceptions to this matrix. No row is accepted merely because its assets decode.

## Scope and stopping rule

The M2 renderer includes the main map, overview, map inspection/overlays,
notifications, animation/effect composition, camera, labels, map-facing content
selection and all MV01–MV30 capabilities. All rows must work through the production
renderer and its owned observation contracts. A showcase or test harness may
supply authored inputs; it must not contain a separate renderer or access hidden
world records on behalf of the painter.

The complete stock domain is eleven terrain identities, every connection and
image selector in the map catalog, 63 ordinary unit-art slots plus the separate
barbarian-leader selection, 48 city appearance variants, eight owner-color roles,
and all ordinary/nuclear effect frames. It is not the six-unit/one-city Riverbend
domain, nor every non-map image in the full atlas. All procedural/text roles in
the catalog count as required features too.

Include map-facing scenario replacements and their F03/P03 precedence: native
selected content and independently authored scenario BMP/GIF terrain, unit and
city sheets. Preserve dimensions, anchors, alpha/key colors and identity. Other
P03 formats not consumed by the map, full editors, city interiors, advisors,
palace/spaceship screens and the full game rules remain with their owning work.
Map renderer acceptance does not require those screens or a playable turn loop.

Use actual scoped projections for facts the engine already supplies. For missing
live producers, define explicit permitted inputs and exercise them with authored
observations/effect sequences through the same production path. Examples include
trade paths, worker progress and combat effects. Record each live-provider gap,
its owning specification/milestone and the missing join. Do not invent a gameplay
algorithm, show fabricated data as a live result, or defer the renderer row because
the producer is unfinished. Withheld, unavailable, zero and absent are distinct.

Implementation may proceed in reviewable increments, but an increment is not the
completion criterion. No MV row can be dropped, marked not applicable in its
entirety, or silently moved to M3–M5 to close this renderer task. An unimplemented
capability or unresolved required content definition remains open.

## Per-feature acceptance matrix

The fixture names below are stable case-family identifiers, not prescribed file
or API names. Enumerated selectors require exhaustive coverage; named combined
states and boundary cases require explicit vectors. There is no requirement to
take the Cartesian product of unrelated features. Each row also runs under the
common evidence and backend requirements below.

| ID | Authored case family | Required cases and pass criteria |
| --- | --- | --- |
| MV01 | `terrain-identity` | Render all eleven terrain identities individually and in a mixed world, with stock and selected replacement art. Assert resolved identity and visible art, both alone and under overlays; no generic substitution for a successful binding. |
| MV02 | `shoreline-neighborhoods` | Cover 4 quadrants × 8 contact combinations, islands, one-tile channels, peninsulas, corners, flat edges and wrap seams. Verify selector/placement and visually continuous outlines; unknown neighbors cannot affect the frame through hidden live data. |
| MV03 | `terrain-transitions` | Cover every declared edge-blend family/direction and all 16 connection masks for each of Forest, Hills and Mountains. Check unlike-terrain boundaries, relief overhangs, overlap and seams. Ground texture without the connected relief is a failure. |
| MV04 | `river-networks` | Cover all 16 river masks and four Ocean-mouth directions, isolated ends, forks, coast intersections and road/rail crossings. Assert resolved joins and visible continuity at ordinary and reduced detail. |
| MV05 | `resource-identities` | Exercise all 22 catalog special-resource bindings and the Grassland shield in authored art cases. Separately assert W01-derived presence/suppression and selector values, including no live Grassland special resource. Check Ocean resources, river/network overlap and differing resource identities. |
| MV06 | `agriculture-and-mines` | Show none, Irrigation, Farmland and Mine distinctly, including river/resource/city combinations. Irrigation+Mine components resolve to Farmland, not two unrelated marks. City underlay styling must leave the observed improvement unchanged. |
| MV07 | `route-junctions` | Cover eight directions, isolated endpoints, road↔road, road↔rail, rail↔rail and city connectors with and without explicit route bits. Reject airbases as cities. Check flat/wrapped/unknown endpoints and remembered changes; live hidden road changes must not leak. |
| MV08 | `tile-features` | Render huts, pollution, fortress, empty airbase and occupied airbase, including all eight permitted aircraft colors. Exercise W01 hut presence/suppression, City+Fortress as airbase, and a fortified unit over a fortress. No missing hut field or generic feature dot closes this row. |
| MV09 | `knowledge-boundaries` | Exercise opaque unknown tiles, all four feathered edges and their intersections, remembered improvements, disclosed objects and constrained map-region clipping. Substitute hidden terrain/entities without changing permitted inputs; neither pixels, sampling, labels nor inspection may change. Test the clipping capability now; integrating the later city-resource screen is a separate join. |
| MV10 | `unit-art-identities` | Render every ordinary slot 0..62, the owner-0 Diplomat/leader appearance, and selected custom replacements. Assert the exact body binding and registration; include slots beyond 5 and both shield-anchor groups. The extra atlas slot 63 is not a new classic rule. |
| MV11 | `unit-owner-and-stack` | Cover every owner-color role, single/multiple units, primary/secondary shields and body/banner shadows. Verify alpha-mask coloring, trim/poles, count and inspection identity; a display representative must not change gameplay activation or stack order. |
| MV12 | `unit-health` | Cover full, partial, zero and negative remaining health, zero-rule-hit-point substitution, damage beyond hit points, withheld/unavailable values, and boundaries of the documented green/yellow/red presentation policy. Check clipping/legibility without normalizing stored combat values or inventing healthy status. |
| MV13 | `unit-order-status` | Distinguish no order, waiting for input, each published order, goto, automation and veteran state, with combined status/health/stack cases. Text/icons must fit the banner and remain inspectable; hidden foreign/AI orders must not appear. |
| MV14 | `sleep-and-fortification` | Exercise silhouette and detailed greyscale modes for sleep/sentry and eligible aircraft, plus fortified overlays. Verify alpha, retained background, owner/banner visibility and combinations with terrain fortresses and airbases. |
| MV15 | `city-appearances` | Cover all 6 styles × 4 size classes × 2 wall states and every population anchor. Check disclosed population thresholds, era and Palace/wall policy boundaries from the stock contract, plus selected replacements. Withheld foreign inputs must not be read to choose an appearance. |
| MV16 | `city-annotations` | Show own and known foreign cities with admitted names/sizes, stale/zero remembered size, anonymous sites, visible/absent occupation flags, hidden stationary garrison bodies, population and disorder. Test overlapping annotations and long names. Neither a current foreign population nor a live garrison may be inferred from an unavailable field. |
| MV17 | `owner-color-reassignment` | Exercise all eight color roles on flags, shields, disorder, occupied airbases, labels, ownership/radius overlays and overview legends. Reassign color without changing civilization slot; every dependent cache must update. Keep owner identity inspectable without color and diagnose unsupported custom colors explicitly. |
| MV18 | `selection-and-active-focus` | Distinguish inspected tile, selected object and active unit, including when they differ. Test blink/pulse and reduced-motion alternatives, moving/combat focus, stale/deleted targets and observer replacement. Rendering/focus must not issue activation commands. |
| MV19 | `map-display-modes` | Toggle grid, W01 city radius, permitted ownership and bare/full terrain modes on the same world. Verify resources/pollution retained in bare mode, required clutter suppressed, radius geometry at edges and no new territory or disclosure rule. Restore full detail without losing selection/state. |
| MV20 | `route-and-trade-overlays` | Supply explicit ordered active-unit routes, trade-partner lines and satisfied/unsatisfied road/rail trade-bonus paths, including empty/disconnected/wrapped paths and changed targets. Verify distinct meanings, clipping and disclosure. Authored lines prove rendering only; safe U03/C07 computation gets separate integration evidence. |
| MV21 | `work-and-inspection` | Show own-worker accumulated work, zero/large/unavailable values, and permitted city/unit quick information. Verify that work is not labeled as turns remaining, that hidden details cannot leak, and that keyboard inspection reaches the same facts as hover/pointer inspection. |
| MV22 | `map-notifications` | Exercise multiple eligible notices during pan/zoom, expiry, cancellation and observer replacement. Verify viewport anchoring and readability. Decision-bearing interactions retain an admitted reply/completion path; skipped cosmetic presentation cannot drop semantic completion. |
| MV23 | `overview-navigation` | Show unknown/land/Ocean, permitted city ownership, selection and the actual displayed viewport. Test seam-split footprints, flat edges, different aspect ratios, zoom and click/keyboard recentering. Main-map unit visibility must not automatically create overview unit disclosure. |
| MV24 | `movement-and-combat-effects` | Exercise moving body/banner/shadow, participant emphasis, all 8 explosion and 11 nuclear frames, overlapping effects and wrapped movement. Skip, cancel, interrupt, delete/replace the target and switch observers mid-effect. Check completion exactly once, correct settled output and cleared old overhangs; no gameplay mutation by animation. |
| MV25 | `camera-and-picking` | Exercise drag, continuous and preset/step zoom, pointer anchoring, flat clamping, wrapping and eligible active-unit tracking. Include edges/corners, multiple wrapped copies, tall-art hits and a unit already comfortably visible. Picking follows the displayed transform at fractional zoom and every density. |
| MV26 | `detail-transitions` | Declare full/reduced/overview bands and test both sides of every boundary at equal displayed scale with different backing densities. Full detail exposes every feature; reduced views retain P02's required coastline/river/knowledge/object/health/selection information. Missing detail stays inspectable; picking and gameplay remain invariant. |
| MV27 | `labels-and-density` | Exercise names, population, orders and work counters at fractional zoom and 1×/2× density, with long/custom text, supported fallback glyphs, crowds and clipped annotations. Verify legibility floors, the crowded-label policy and shared font measurement. Keep HUD text independent of world zoom. |
| MV28 | `alpha-and-occlusion` | Exercise selective faction tint, alpha holes, thin poles/trim, shadows, silhouette/greyscale and overlapping tall/translucent objects, labels and effects. Compare correct visual occlusion across texture-batch orders; no halos, erased terrain or full-image tinting. |
| MV29 | `cache-invalidation` | Compare cold full, unchanged, cached pan and dirty world updates with fresh renders. Change tile/network neighbors, unit location/status, city size/name/walls, effects, content/color/options, observer, zoom, resize and density. Exercise multiple windows with distinct cameras/densities. Invalidate old/new overhangs, preserve source sampling under clipping, keep foreground panels and blank margins intact during scrolling/blinking, and release caches across repeated replacement. |
| MV30 | `backend-and-recovery` | Exercise display color correction, reduced/disabled cosmetics, cold/warm resource loads, cache eviction/recreation and supported backend fallback/recovery. Require equivalent feature/target coverage or an explicit limited/error state with recovery. Missing images cannot silently become generic successful lookups. |

## Common evidence requirements

For each MV row retain its case IDs and enumerated variants, the authored input
or captured scoped observation, expected semantic selectors, content/theme pins,
observer identity, options, displayed scale/detail band, physical pixel dimensions,
density, backend, build/source identity, commands and results. Bind the executed
P02, acceptance matrix, performance contract, catalog and fixture hashes in the
report as well as compiled inputs; a link to a subsequently edited contract is
not an execution identity. Link screenshots or image-region comparisons and a
recorded visual review of legibility/composition.
An image hash changing after a flag changes does not prove the right art was used.
Use explicit selector/state assertions as well as visible-result checks.

Exercise flat and wrapped maps, unknown/known neighbors, scene replacement,
own/foreign/remembered/authorized-reveal observations, and both individual and
combined features. W03's explored tile state is not a continuous line-of-sight
set: terrain/river and remembered improvements follow its separate channels.
Do not invent a new fog-memory rule to produce a visual test. Apply cross-cutting
privacy tests to caches, detail modes, labels, inspection, effects and overview.

Use the same production frame model and renderer for full and incremental paths.
Build the comparison frame afresh without reusing the cache under test. Declare
backend tolerances before comparison, and visually inspect representative combined
scenes: identical fresh/cached omissions cannot pass visual coverage. Compare
world/session and gameplay/presentation-content RNG captures around navigation,
redraw, inspection and cosmetics. Real accepted commands retain their normal
authority, stale-target, cancellation and exactly-once rules.

Retain a reproducible scene gallery, accessible in a test/showcase host, covering
every row and its combined cases. Include a large mixed map with all major layers,
not just a contact sheet. The application must use that same renderer with real
scoped observations, including selection/inspection and the currently admitted
command path. Keep authored-only routes/effects visibly identified as such.

## Backend and completion gates

| Gate | Evidence needed | What it establishes |
| --- | --- | --- |
| Content/contract | Catalog checks, malformed input tests, stock and authored scenario replacement cases; explicit projections and producer gaps | Correct inputs and supported domain; no rendering claim by itself |
| Native renderer | Every MV row on the production desktop rendering path; actual Linux pointer/keyboard/overview interaction and clean shutdown; CPU fallback correctness | Native functional rendering acceptance for the executed platform/backend |
| Browser renderer | Published WASM application with pinned content and fonts; every MV row plus actual browser input, resizing, density and resource lifecycle | Browser functional acceptance; compilation alone is insufficient |
| Interactive performance | Every workload and budget in [map performance acceptance](../performance/map-rendering.md), with full correctness retained | Responsiveness on the recorded hardware/backend; unchanged-frame blits are not cached-pan evidence |
| Other desktops/full M2 | Native macOS/Windows display/input, mixed DPI/fullscreen, menus, IME/accessibility, storage/deadline/media and remaining platform gates | Full milestone/platform acceptance; Linux or browser results cannot substitute |

The default complete-renderer task includes native and browser functional gates,
all row implementations, automated redraw/performance workloads and actual
performance measurements on available target hardware. A user may explicitly
scope an execution platform out of a task, but the corresponding ledger cells
remain pending and the result must name that limitation. Unavailable hardware
does not authorize dropping features, missing backend implementations or tests
that can run locally. Report unavailable target measurements as unverified.

The task may report “renderer implemented; native/browser functional acceptance
passed” only when every MV row has that evidence. It may report performance
acceptance only when the measured budgets pass. Full M2 completion additionally
requires the milestone's platform/services gate. A known functional defect or a
failed measured performance gate remains work, not a documentation-only deferral.

## Coverage ledger updates

For each MV ID record implementation (`open`, `partial`, `implemented`), separate
native/browser acceptance (`pending`, `passed`, `failed`) with evidence links,
and live integration (`authored-only`, `partial`, `integrated`) with the producer
owner and follow-up where needed. Keep target-hardware performance/platform
results separate. A passed row requires all its cases and applicable cross-cutting
checks on that backend; a failed case cannot be hidden by a row-level pass.

Changes to bindings, contracts, selectors, projections or rendering invalidate
affected earlier acceptance. Preserve old reports as historical evidence and
rerun the affected cases; do not rewrite their source hashes. Keep the aggregate
`m2-shell` inventory partial until its broader scope is satisfied, and never
relabel the Riverbend slice's existing report as complete-renderer acceptance.
