# Browser map performance diagnostics

The hardware browser measurements find a useful, small optimization: ordered
foreground commands remove a second traversal and reuse atlas submission arrays.
`Commands` now joins `Atlas,Direct,Textures,Encoded` in the opt-in GPU renderer.
CPU composition remains the default. The complete M2 performance gate remains
open; no target, population, layer or detail policy was weakened.

The preceding browser content/functional work was committed as `1401d70`.
Its [WASM/WebGL acceptance record](m2-browser-map.md) remains distinct from this
hardware performance study. These results do not establish physical presentation
latency or complete performance acceptance.

The subsequent [zoom stall investigation](m2-browser-stalls.md) isolates terrain
cache/detail transitions and evaluates bounded single-thread ground batching.
The measurements and hashes below retain their original pre-batching scope.

## Machine, build and workload

Measurements on 2026-11-20 used Linux 7.0.0-43, Ryzen AI 9 HX 370 / Radeon 890M,
Mesa 26.0.8-1ubuntu0.3, Chromium 151.0.7922.34 and Playwright 1.58.2. Hardware
WebGL2 reports `ANGLE (AMD, AMD Radeon 890M Graphics (radeonsi strix1 ACO), OpenGL
ES 3.2)`. The laptop was on AC, with CPU `powersave` / `balance_performance`,
boost enabled and automatic GPU clocks. Clocks and thermal state were not locked.
Runs were sequential; build and other graphics checks were kept outside timing.

The SDK/runtime are 11.0.100/11.0.0, Avalonia 12.1.6 and SkiaSharp 4.156.1.
This is the untrimmed Release MapBrowser evidence host, using the production
browser entry point, controls and rendering libraries. Production remains trimmed;
its input checks are separate. Content is the pinned 2026.11.08.1 graphics package
and all twelve DejaVu 2.37 faces. No reconstruction sources were used.

`ScopedMapPerformanceWorld` is extracted without changing the existing native
fixture. It authors 31,752 tiles, 2,048 units and 256 cities, then observes them
through the production scoped client. The browser receives 1,831 units and 229
city records after filtering and the named authored memory provider. Actual
visible instances, objects and labels are retained per frame. Real mouse drags,
Shift-drags and the gallery's world-change button drive the measured paths.

The initial screen covers Software2D/CPU, hardware WebGL/CPU and hardware
WebGL/GPU. It includes full detail, approximately half the distinct world and
overview, at physical 1920×1080/1×, 3840×2160/1× and 5120×2880/2×. Density is
applied once. The 5K view has a smaller logical extent than 4K/1×; their displayed
populations and detail policies therefore differ. Receipts record actual zoom
and distinct/wrapped coverage rather than treating resolution alone as workload.

The screen contains 2,270 measured changes and 850 saved warmups. At 1080p the
three baseline full-detail runs use 10 warmups and 30 measurements per mode;
the GPU baseline also repeats half/overview three times. CPU half/overview uses
one 5+15 run. Command-only and combined mask/sparse candidates use one 10+30 run
at 1080p. Larger cells use one 5+10 run. These are deliberately abbreviated
screening populations, not the full three-run acceptance protocol.

Nearest-rank p50/p95/p99/max are reported separately for each run. No slow sample
is discarded. With ten samples p95 and p99 are the maximum. The initial pan
oscillates between two nearby camera positions to isolate warmed rendering;
the longer follow-up below uses sustained travel and newly exposed terrain.

## Timing boundaries

`frameWorkMs` spans map renderer entry through Skia lease disposal. Dirty-world
work additionally includes authored mutation/publication, scoped projection and
scene binding. It excludes lease acquisition and other host work. Browser rAF
callback CPU intervals are also recorded. Nested stage counters overlap and must
not be summed as independent phases. Fresh rendering, screenshots and resource
reload comparisons happen outside timing.

WebGL timer queries observe commands issued during browser callbacks. Their
elapsed intervals can include feed/scheduling gaps; they are not GPU utilization
or physical scanout. Missing/disjoint results remain null. Input-to-observed-rAF
includes driver/polling overhead and is not an input-to-photon measurement.

Draw-thread allocation counters are useful for matched comparisons. The initial
whole-process allocation counter was coarse; the final host requests its precise
value. Zero GC-pause API readings are not evidence of zero stalls: collections
occurred, so browser GC pause duration remains unverified. WASM capacity includes
content, UI and verification work, is monotonic, and is not process RSS. Skia cache
usage includes shared UI/reference resources and is not total graphics residency.

## Initial screen

Full-detail pan **p50 / p95 milliseconds** follow. Ranges are the three separate
1080p baseline runs, not pooled quantiles. Other entries are single diagnostic
runs. All raw p99/maxima and the wider-detail workloads remain in the artifacts.

| Physical viewport / density | Software2D / CPU | WebGL / CPU | Original WebGL / GPU | GPU + commands |
|---|---:|---:|---:|---:|
| 1920×1080 / 1× | 22.0–22.5 / 25.2–41.9 | 19.8–20.4 / 25.2–34.2 | 8.5–10.3 / 12.3–15.9 | 7.0 / 8.1 |
| 3840×2160 / 1× | 77.6 / 84.2 | 69.5 / 90.4 | 24.8 / 45.0 | 19.8 / 22.2 |
| 5120×2880 / 2× | 122.7 / 151.5 | 111.0 / 116.5 | 12.2 / 15.6 | 11.0 / 20.5 |

Commands reduce median pan work by about 29% at 1080p, 20% at 4K and 10% at
5K in the paired screen. They do not improve every tail: the short 5K pan p95 is
worse. At 1080p, median draw-thread allocation falls from about 55 KB to 20 KB;
foreground traversal count falls from two to one while sprite/batch counts match.
Across the three resolutions, all 490 paired warmup/measured inputs match camera,
dimensions, tile/object/label counts and observed revision; all 21 optimization
endpoint images are byte-identical to the original GPU path.

The combined `Commands,Masks,MaskBatch,Sparse` candidate did not produce a
consistent further gain and changes some edge pixels. Only commands are enabled.
The remaining experimental flags and cache budgets are unchanged.

WebGL presentation alone offers little relief for CPU composition. GPU composition
helps full-detail drawing, especially at 5K/2×, but dense overviews can favor CPU:
at 4K overview the initial CPU medians are about 43–46 ms versus 76–78 ms for GPU.
The command stream preserves ordering and does not reduce the atlas batch count.

Terrain/detail transitions remain much more expensive than warmed draws. The
initial zoom runs retain approximately 1.7-second spikes at 4K and 2.8–3.1-second
spikes at 5K, dominated by terrain rebuilding. Browser guards disable the current
desktop worker-band/parallel preparation paths. Commands do not remove that work.
At full detail, broad world edits remain roughly 100–140 ms at 1080p and reach
about 250–330 ms at 5K. Projection and binding alone often exceed 16.7 ms.
Meeting those cases needs terrain preparation/cache and incremental observation
work; a WebGL switch or foreground batching alone cannot do it.

## Sustained-pan follow-up

The final build repeats a 400×160 logical-pixel diagonal travel and return,
crossing tile boundaries and exposing new terrain. Each of three independent
processes has 30 warmups and 300 measured changes, including one physical-pixel
movement in the measured population. Original and command paths use the same
final binary and explicit feature selection. This strengthens one workload's
sampling; it still does not cover all acceptance workloads or every-frame pixels.

| Run | Original p50 / p95 / p99 / max ms | Commands p50 / p95 / p99 / max ms |
|---|---:|---:|
| 1 | 9.63 / 14.13 / 16.89 / 109.83 | 7.50 / 10.51 / 12.54 / 116.69 |
| 2 | 9.31 / 13.66 / 15.12 / 111.36 | 7.52 / 10.62 / 13.93 / 98.58 |
| 3 | 9.22 / 13.63 / 17.11 / 108.19 | 7.26 / 11.88 / 14.12 / 101.68 |

Both paths meet the draw-work p95/p99 thresholds for this full-detail 1080p pan;
commands provide more margin. Approximately 100–117 ms worst frames remain.
Those maxima are retained, and neither result establishes physical presentation
latency. Broader detail/dirty/zoom failures remain open.
All 990 paired warmup/measured inputs match, and all three optimization endpoint
images are exact. Each configuration also passes three fresh-render and three
actual host resource-reload comparisons.

## Correctness and validation

The original reference helper rendered at a different target size, origin and
clip from the host. Strict comparisons exposed small edge differences: 51/54
CPU endpoints were exact, while all 63 GPU endpoints differed (at most two byte
levels for GPU). Those failed records are preserved. The helper now reproduces
the host target and transform before cropping the map. No pixel tolerance was
changed. Follow-up verification also clears resources through the actual UI and
compares the same presented map before and after rebuilding its caches.

Final checks passed on the optimized build:

- 294 native GPU matrix cases: exact retained/fresh frames and exact comparison
  against the explicit pre-command GPU configuration; semantic counts and the
  existing CPU/GPU raster bounds pass. Another 108 GPU presentation frames pass.
- 800 renderer-option checks, including 100 warmed observer replacements.
- The extracted scoped native fixture retains all 54 fresh-frame comparisons
  across three sizes. This smoke run is correctness evidence, not timing evidence.
- Hardware WebGL/GPU stock gallery: 68 checks and 39 exact presented/fresh map
  captures, covering MV01–MV30 plus Mixed. A supplemental hardware run passes
  37 transition checks and eight exact captures, including independent 1×/2× GPU
  maps, texture/context recovery and invalid-content admission.
- All 20 sustained-pan and large-viewport endpoints match both fresh rendering
  and actual host texture reloads: six 1080p endpoints, seven Software2D/CPU 4K
  endpoints and seven WebGL/GPU 5K endpoints. GPU endpoints also remain inside
  the declared CPU/GPU raster bounds.
- The separately trimmed production browser passes all 16 real input checks.
- Required root/App/browser C# formatting and verification, native build, both
  browser publications, project boundaries, coverage metadata, identity tests
  and driver syntax checks pass.

This extends GPU evidence without claiming a new complete six-batch GPU art
matrix. The earlier complete stock/native/classic browser matrix used CPU
composition. The large-viewport follow-ups add 140 measured changes; the two
long-pan cohorts add 1,800, for 4,210 campaign measurements in total. Preliminary
`probe-*` reports, including an interrupted probe, remain outside those totals.

## Evidence and reproduction

Artifacts are under `build/m2-browser-performance/`. Each cohort retains its
actual driver copy/hash, compiled source identities, raw warmups/samples,
quantiles, screenshots and fresh-reference images. `measured-source/` and
`measured-source.json` preserve and validate all 288 initial compiled inputs;
these are not relabeled as the later optimized build. `summary.json`, paired
comparison files and `evidence.json` index the final records.
`final-source/` freezes the final 288 compiled inputs. The evidence index validates
25 report identities against their corresponding sources and hashes 1,408 local
artifacts, both final browser bundles and the reporting helpers.

| Receipt under the artifact root | SHA-256 |
|---|---|
| `evidence.json` | `46153eb3fbee730c3747ce60db6e986dd53b968dee44f650559cde545b5da40a` |
| `summary.json` | `2bb8647fff5a8bf44f25a7046320297e5e23fe2c83a9adb7b450b7b04bdfbd3b` |
| `commands-travel-1080-comparison.json` | `0f8bbd699223cb587727f66ec177eb470c378b5fa76c9b790bd3d85b92695eb3` |
| `travel-original-gpu-1080/performance.json` | `e5622aa98ef745783a42e678e4e34364063987de6de4d64bf1119fd3e8b5e5f3` |
| `travel-final-gpu-1080/performance.json` | `7510ad692c14656d16b88fcaba164ef95727e02c49b15411cd0170302fa847b7` |

Use the pinned SDK, Chromium and Playwright and stage content as described in
[BUILDING](../BUILDING.md#browser). A representative final comparison is:

```sh
node tools/dotnet/measure_map_browser.cjs BUNDLE OUTPUT \
  --backend=webgl --renderer=gpu --details=full --modes=pan \
  --pan-pattern=travel --runs=3 --warmup=30 --samples=300 --recover-check
python3 tools/dotnet/check_map_browser_performance.py OUTPUT
```

The test host accepts
`--gpu-features=Atlas,Direct,Textures,Encoded` to reproduce the original GPU path.
Use `--backend=software --renderer=cpu` and `--backend=webgl --renderer=cpu` for
the two CPU paths. The production URL supports `backend` and `renderer`, but
arbitrary feature selection remains test-only. `run-campaign.sh` preserves the
initial diagnostic commands. The comparison helper checks every paired sampled
input and exact optimization endpoints; it rejects missing baseline cases.

The [performance contract](../performance/map-rendering.md) is unchanged. Still
pending are complete per-cell sampling, every-changed-frame pixel verification,
small-scene/selection/effect/seam/flat-edge workloads, 20 cold/replacement trials,
100 warmed browser replacements, physical presentation and other hardware/OSes.
The fixed two-camera screen and sampled endpoint checks cannot close those gates.
