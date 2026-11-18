# M2 map interaction performance acceptance

These are engineering targets adopted for the complete M2 renderer, not original
game timing requirements or results inferred from the Riverbend slice. They
complement [feature acceptance](../testing/map-rendering.md). The existing
[foundation budgets](budgets.json) still govern their own workloads. Rendering
cannot change game rules, observer knowledge or RNG to meet a timing target.

## Workloads

Use the production scoped projection, scene preparation, renderer and host input
path. Retain an authored 252×252 doubled-coordinate world (31,752 tiles), 2,048
units and 256 cities before observer filtering, plus a small interactive scene.
Populate all terrain/features, multiple city styles, color/status combinations,
effects and unknown/remembered/disclosed regions. Record distinct visible tiles,
wrapped instances, actual visible objects, labels and image/batch counts. Do not
substitute a six-unit or feature-free scene for the large-world workload.

Run at physical 1920×1080, 3840×2160 and 5120×2880. Include ordinary full detail,
a view containing approximately half the distinct world, and a more distant
overview-detail view. Report the actual visible fraction and displayed zoom;
physical resolution is not a logical window size. Exercise both flat and wrapped
edges, and 1×/2× density without multiplying physical resolution twice.

| Workload | Required change and measured boundary |
| --- | --- |
| Cold/full frame | Fresh renderer/cache after content admission, including projection, preparation, decoding/upload required by that frame, drawing and frame submission. Report content download/admission separately, never hide lazy decoding as preload. |
| Unchanged frame | Repaint the same observation and camera. Report this separately; it is neither cached pan nor dirty-world work. |
| Cached pan | At least one-pixel movement and sustained diagonal drag, crossing tile boundaries, a seam and a flat edge. Include newly exposed strips and object/label overhangs. The camera must actually change on measured frames. |
| Continuous zoom | Change displayed scale through fractional values and detail boundaries around a pointer anchor, with full required detail and correct hits. Record cache/texture work and input-to-visible latency. |
| Dirty world | Move a unit; change health/order/owner color; change a city label/walls/size; change terrain, a road and its neighbors; advance an effect. Include old/new bounds and dependent layers. Changing only the selection outline is a separate workload. |
| Replacement/recovery | Replace observer, world or content; resize/change density; evict/recreate resources. Verify stale data disappears, resources are released and the next correct frame is presented. |

For every changed frame compare against a fresh rendering of the same permitted
state and camera. Correctness failures invalidate timing acceptance. Include
actual host drawing/composition rather than timing only command recording or an
offscreen blit. Separate CPU preparation, raster/graphics submission, upload,
presentation/frame intervals, managed allocations, GC stalls and resource peaks.
Report GPU/compositor timings only when the measurement method observes them.

## Targets and sampling

The target is responsive interaction on a 60 Hz display with the complete stock
theme. Apply the following budgets separately to the native desktop graphics
path and browser WebGL path on each measured target machine. They apply to each
large-world resolution/detail workload, not an average across easier scenes.

| Metric | Acceptance target |
| --- | --- |
| Warm pan, zoom, selection, effect and dirty-world frame work, including projection/preparation and graphics submission | p95 ≤ 16.7 ms; p99 ≤ 33.3 ms |
| Input to the correct visibly presented camera/selection/inspection result | p95 ≤ 100 ms |
| Correct first frame after content admission, scene/observer replacement or settled resize/density change | p95 ≤ 250 ms |
| Cached navigation and unchanged frames | No whole-world projection/copy on each frame; projection refreshes have an explicit data-change boundary |
| Resource lifetime | Bounded declared CPU/GPU caches, no monotonic retained growth after 100 warmed scene/observer/content replacements, and no old-observer images retained in reusable presentation caches |

These targets do not require a particular cache algorithm. Full repaint on every
small pan is permissible only if it meets the same complete-workload budget;
label it honestly instead of claiming cached-pan behavior. Do not drop layers,
reduce the required object population or change detail policy to rescue a run.

Record OS, CPU/GPU, memory, display/refresh, driver/browser, power/load context,
SDK/packages, source/content identities and selected backend. Use at least three
independent runs. Each continuous workload has at least 30 warmup frames and
300 measured changed frames per run; first-frame/replacement workloads have at
least 20 measured repetitions. Retain raw samples, p50/p95/p99 and maxima using
nearest-rank quantiles. Report each run and workload separately, including GC
or compilation stalls; do not discard slow samples or pool away a failing run.

Hardware-accelerated native/browser execution supplies the interactive target
evidence. CPU raster and software WebGL runs remain required for supported
fallback correctness and useful for reproducible algorithm/resource measurements;
they do not establish physical display, GPU or compositor acceptance. State their
scope explicitly. If presentation latency cannot be observed, leave that metric
unverified rather than replacing it with draw-call duration.

## Failure and handoff

A repeated budget failure on a measured target is an open rendering issue. Fix it
or retain an explicit failed gate; no automatic budget relaxation is allowed.
Unmeasured hardware/platforms stay pending. Keep implementation, functional
acceptance, software measurements and target-hardware performance as distinct
claims in the [coverage ledger](../validation/map-visual-coverage.md).

The in-progress Riverbend slice's reported results measure software full
frames, unchanged-frame blits and selection-only dirty updates. They remain
historical measurements of that slice and do not pass these pan/zoom/world-change
workloads or the new complete-renderer budgets.
