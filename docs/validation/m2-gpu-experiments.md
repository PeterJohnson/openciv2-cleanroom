# M2 GPU composition, offload and latency experiments

This is a follow-up to the [first GPU prototype](m2-gpu-composition-prototype.md).
It reuses the admitted assets, fonts, P02 selectors, gallery, authored art
replacements and recorded native input states. It is an experiment, not a claim
of full M2 or cross-platform acceptance. CPU composition remains the default.

## Corrected baseline

GL instrumentation exposed a correctness error in the CPU baseline: worker
canvases write directly into the viewport surface's backing pixels, but do not
invalidate the owning surface's cached image generation. `Canvas.Discard()`
did not invalidate that raster pixel reference in the pinned Skia build. After
the first upload, GPU presentation could reuse stale pixels while the CPU
renderer produced a correct new raster. CPU-only snapshots did not expose it.

`GpuUploadChecks` now compares actual GPU readback of CPU-composed frames with
a fresh CPU renderer across pans, zoom and edits at 1× and 2×. Before the fix,
18 of 20 cases failed, with millions of stale pixels. The fix makes an identity
one-pixel write through the owning canvas before workers borrow its storage.
This notifies Skia and performs any required copy-on-write without changing the
pixels. All 20 cases then match exactly and changed viewport uploads appear in
the GL counters. SkiaSharp does not expose `SkSurface.notifyContentWillChange`.

The earlier GPU/CPU performance ratios are therefore superseded. Historical
receipts are retained rather than silently rewritten.

## Experiments

All features are opt-in with `OPENCIV2_MAP_COMPOSITION=gpu` and a comma-separated
`OPENCIV2_MAP_GPU_FEATURES` value. A missing GPU context is an error.

| Feature | Experiment |
| --- | --- |
| `None` | Original direct primitive stream into a physical-pixel GPU target, then a host composite. |
| `Atlas` | Immutable sprite pages and consecutive ordered `DrawAtlas` batches, with edge gutters and a no-pixel preparation pass so cold/warm frames use the same sampling path. CPU-backed sources retain their alpha representation. |
| `Textures` | Foreground sprite resizing and tinting represented by picture-backed images, rasterized and cached by Skia on the GPU. Terrain retains the existing CPU preparation path. |
| `Terrain` | Terrain chunks represented by picture-backed images; topology selection remains on the CPU, chunk rasterization moves to the GPU. The estimated chunk cache is capped at 192 MiB to leave headroom. |
| `Offload` | Both terrain and sprite preparation use picture-backed images. |
| `Retain` | Reuse the composed GPU viewport and apply local damage. Deferred patch chains have at most four predecessors before a full rebuild. |
| `Direct` | Draw to the leased host canvas without the additional full-viewport offscreen target/composite. |
| `Encoded` | Keep bounded encoded PNG source images for GPU preparation. Skia owns decoding and texture residency; PNG decoding is still CPU work. Scenario raster replacements use their existing pixels. |

The `streamed` benchmark variant combines `Atlas,Textures,Encoded,Direct`.
`combined` uses `Atlas,Offload,Retain,Direct`. The later `full-streamed` control
uses `Atlas,Offload,Encoded,Direct`, including terrain while retaining encoded
sources. A `textures-direct` control uses `Textures,Encoded,Direct` to isolate
foreground preparation without atlas batching. None of these introduces game
native interop or a new content source. Immutable pictures/images can outlive a
draw; Skia owns the associated GPU cache. Application-owned GPU targets remain
inside Avalonia's graphics lease. GL instrumentation is test-only.
Topology/visibility selection, scene indexing, label layout and command
generation remain CPU work in every variant. “Full offload” below means terrain
chunk rasterization plus sprite resizing/tinting, not an entirely GPU-run map.
Picture-backed images defer materialization until drawing; Skia can create and
cache their pixels on the CPU or GPU according to the drawing context, as
described in its [image API](https://api.skia.org/namespaceSkImages.html).
These experiments draw them through the hardware GPU context. Encoding and
recording a picture alone is not evidence that its execution cost disappeared.

## Measurements and limits

The hardware and software stack are the same Radeon 890M / Ryzen AI 9 HX 370,
Mesa 26.0.8, .NET 11, Avalonia 12.1.6 and SkiaSharp 4.156.1 used in the first
report. Power management remains automatic; clocks are not locked. A context
snapshot during the cache sweep recorded 81°C and 1,487 MHz, with load averages
2.18/3.83/3.42. This is not a per-frame thermal trace or evidence that a specific
outlier was thermal throttling. Raw per-run distributions remain available;
medians below do not erase slow runs. Results distinguish these measurements:

- Instrumented offscreen replay uses the existing native camera/observation
  receipts, three source runs, 30 additional warmup frames and 120 measured
  frames per series. Variant order rotates across runs. The added `local-dirty`
  workload changes one tile's road while preserving faction colors and objects;
  it is explicitly distinct from the existing faction-recoloring dirty workload.
- GL wrappers count submitted draw calls, texture bindings, allocations/deletions
  and estimated uncompressed texture upload payloads, including PBO offsets.
  They do not measure driver-internal transfers or identify every deletion as
  an LRU eviction. Per-process DRM counters separately measure graphics-engine
  time. GL timestamp intervals include CPU command-feed stalls. The wrappers
  and queries add instrumentation overhead; the independent Native AOT timings
  do not use those wrappers.
- Pipelining replays rotate one, two or three output targets and bound outstanding
  frames with GL fences. Throughput includes draining all measured frames.
  Completion latency uses GPU timestamps correlated with the host monotonic
  clock before each series; the shortest of twelve synchronous timestamp reads
  supplies the offset and a reported half-round-trip uncertainty. A separate
  host-observed completion upper bound is retained and checked. Neither metric
  is merely submit time. The reported calibration uncertainty does not include
  subsequent clock drift; correlation checks allow up to 1 ms of additional
  discrepancy against the observed bound. The timestamp behavior follows
  [ARB_timer_query](https://github.com/KhronosGroup/OpenGL-Registry/blob/main/extensions/ARB/ARB_timer_query.txt).
  This isolates rendering throughput; it has no compositor or display scanout.
  Timing starts after backpressure permits the next update/draw to begin, so
  this is not an externally timestamped input queue. Native input tests measure
  that separate path.
- Native comparisons use the same Native AOT binary and existing X11 input
  driver. Synchronized timing includes GPU completion. Dirty-world timing also
  includes authored updates and scene binding.
- Native latency uses an independent fixed-rate XTest injector and reads a
  test-only 32-bit stamp from the rendered X11 window image. The stamp contains
  the applied pointer coordinate and its complement. It does not rely on a draw
  receipt. The final method caches the stationary window's root coordinates
  before injection and reads only after owned-window X Present completion
  events. Event delivery and image-read overhead are included; the event itself
  is not taken as proof that a particular input was displayed. Injection
  lateness and readback cost are retained. Inputs absent from
  the samples can have been coalesced or missed by sampling. These are input-to-
  window-image measurements, **not physical input-to-photon latency**.
  Each configuration has three runs of 300 drag-pan motions at each of 60 and
  120 Hz, after 30 warmup motions. The pointer moves two physical pixels per
  event in one direction; the virtual output runs at 60 Hz. This differs from
  the native draw benchmark's short oscillating pan and does not measure zoom
  or command-button latency. Fixed-rate injection and the compositor remain
  active while frames are rendered.
  The event subscription follows the public
  [X Present protocol](https://cgit.freedesktop.org/xorg/proto/presentproto/tree/presentproto.txt).
- During native latency runs, the owned desktop's DRM resident/requested memory
  and process RSS are sampled every 100 ms. Duplicate DRM descriptors are
  deduplicated by device/client ID. Resident memory includes application, host
  and driver resources plus shared buffers, but excludes the compositor's other
  clients. RSS can overlap that accounting; the values are not additive. Sampled
  peaks are lower bounds, not exhaustive allocation peaks. Field meanings follow
  the [kernel DRM usage-stat specification](https://kernel.org/doc/html/next/gpu/drm-usage-stats.html).

All controlled comparisons use the same 512 MiB Skia resource-cache budget;
the cache experiment also uses 256 and 768 MiB. This is a soft cache budget,
not reserved memory or total VRAM. The Radeon shares system memory. Estimated
chunk/texture pixel sizes, encoded-source bytes and Skia GPU usage are different
accounting categories and must not be summed as a total GPU allocation.
One RGBA8 viewport's uncompressed pixel payload is 7.91 MiB at 1080p,
31.64 MiB at 4K and 56.25 MiB at 5K. Moving from one to three output targets adds
15.82/63.28/112.50 MiB before any host buffers, retained images or textures.
The device reports a 512 MiB VRAM region and roughly 46.8 GiB GTT capacity;
these are driver regions on a system-memory-sharing GPU, not a 512 MiB hard
limit on all GPU allocations. The snapshot is retained in `machine-context.json`.

Application sources were frozen before the Native AOT publish. The exact probe
sources used for the primary sweeps are archived under `measured-probe/source`,
with all 81 source/build inputs checked against its embedded identity. Later
probe changes add the full-offload encoded-source variant and clarify receipt
wording, plus a foreground-without-atlas control; they do not change the
application or primary measurement algorithm.
Every receipt retains its own compiled identities.

## Correctness

The original CPU matrix was rerun with the exact prior art-fixture hashes
(`build/m2-native/art-cases-raster-tier`). All 213 reference PNGs remain
byte-identical after the CPU presentation repair. The separate GPU-upload
regression passes all 20 cases. The GPU experiment matrix includes 294 cases
per variant: existing gallery/theme/density/recovery checks and eight repeated
local edits for each Mixed theme/density combination. Semantic checks pass in
all measured variants; pixel equality is reported separately:

| Variant | Retained/fresh exact cases | Worst fresh channel error (0–255) | Worst fresh mean channel error |
| --- | ---: | ---: | ---: |
| Original GPU | 294/294 | 0 | 0 |
| Atlas + direct | 142/294 | 2 | 0.0001722 |
| Full preparation offload | 294/294 | 0 | 0 |
| Retained viewport | 294/294 | 0 | 0 |
| Combined | 146/294 | 3 | 0.0002202 |
| Streamed foreground | 142/294 | 3 | 0.0002446 |
| Full offload with encoded sources | 142/294 | 3 | 0.0002444 |
| Foreground offload without atlas | 294/294 | 0 | 0 |

Atlas page placement changes floating-point texture coordinates and hence a
small number of filtering-rounding results. Preserving straight-alpha sources
and adding edge gutters removed the larger interpolation differences found in
the first experiment. Remaining differences are **not** a strict pixel pass;
the production correctness gate remains open. Fresh GPU/CPU differences are
also recorded independently, as in the first prototype; exact retained/fresh
GPU results do not imply exact CPU/GPU equivalence.

CPU-versus-GPU pixel differences are substantially larger than those
retained-versus-fresh GPU rounding differences. For example, stock `Mixed-zoom`
at 2× has 602,989 of 8,294,400 pixels with a channel error above 8/255 in the
streamed variant, with mean channel error 1.4844 and maximum 217/255. The
original GPU variant already has 602,443 such pixels and mean 1.4811 in that
case. This is not an atlas-only regression, nor evidence of pixel equivalence.
These CPU-reference differences require separate visual/behavioral acceptance;
matching semantic counts alone does not resolve them.

The native-camera replay covers all 8,100 measured GPU observation states with
matching tile, object, label and effect counts. It samples 297 frames for fresh
GPU and CPU pixel comparisons: 51 are fresh-GPU exact. The largest fresh-GPU
channel errors are 6/255 at 1080p, 1/255 at 4K and 2/255 at 5K; no sampled
pixel has an error over 8/255. The largest mean channel error is 0.0001393.
These remain rejected strict-equality cases. The archived replay console's
older wording calls the attempts “exact comparisons”; the per-frame
`freshGpuExact` and `freshGpuDiff` fields contain the actual outcomes. Final
probe source clarifies that wording. This is sampled offscreen pixel evidence,
not full native-window pixel acceptance.

All 22 existing Native AOT application interaction checks also pass with the
streamed GPU variant. The first two attempts missed the initial Map options
click because the driver acted before initial window placement had settled.
The X11 driver now waits for a viewable window with stable position and size
before that first interaction. The diagnostic and failed logs are retained;
this does not change the application or the benchmark's input path.

## Matched Native AOT results

The desktop comparison uses the same executable, admitted content, physical
viewport sizes and input driver for both modes: three runs of 30 warmup and
300 measured changes for each workload, or 16,200 measured frames in total.
GPU means `Atlas,Textures,Encoded,Direct`; CPU includes the upload correction.
The values are medians of per-run **p50 / p95 milliseconds**. Pan and zoom cover
draw plus synchronous GPU completion; global edits also include authored update
and scene binding. Ratios greater than one favor GPU.

| Physical viewport / workload | CPU p50 / p95 | GPU p50 / p95 | CPU/GPU p95 ratio |
| --- | ---: | ---: | ---: |
| 1080p pan | 12.11 / 16.18 | 11.90 / 14.16 | 1.14× |
| 1080p zoom | 9.82 / 13.98 | 7.31 / 15.53 | 0.90× |
| 1080p global edit | 15.04 / 18.38 | 20.32 / 23.84 | 0.77× |
| 4K pan | 19.54 / 22.93 | 13.43 / 16.13 | 1.42× |
| 4K zoom | 17.39 / 21.44 | 8.94 / 16.86 | 1.27× |
| 4K global edit | 22.43 / 25.74 | 21.16 / 24.00 | 1.07× |
| 5K pan | 26.85 / 31.13 | 17.50 / 22.24 | 1.40× |
| 5K zoom | 27.24 / 31.59 | 17.57 / 36.82 | 0.86× |
| 5K global edit | 30.81 / 33.92 | 24.69 / 29.12 | 1.16× |

This is a workload-dependent improvement. GPU has a faster typical 5K zoom
frame but a worse tail at the 512 MiB budget, which it reaches in both 4K and
5K native zoom. CPU wins the 1080p edit workload. World-update/binding costs
remain significant even when drawing improves.

Per-run variation matters. The 5K CPU p95 ranges are 30.08–43.76 ms for pan,
31.53–47.87 ms for zoom and 33.57–47.11 ms for edits. GPU ranges are
21.51–22.45, 33.26–38.83 and 29.04–29.42 ms respectively. At 4K, GPU pan is
16.04–16.15 ms and zoom 16.62–17.05 ms. No row is promoted to full M2 acceptance:
the GPU pixel gate, unmeasured platforms and physical presentation remain open.

## Instrumented renderer costs

The primary profiling sweep contains 47,520 measured frames. The table below
shows 4K p95 milliseconds from update/draw start to correlated GPU completion,
using the median of three per-run p95 values. Cache usage is the largest sampled
Skia usage across these workloads, not total driver memory. These are diagnostic
120-frame series; the separate Native AOT comparison uses 300-frame series.

| Variant | Pan | Zoom | Global edit | Local edit | Peak Skia cache, MiB |
| --- | ---: | ---: | ---: | ---: | ---: |
| CPU | 20.41 | 21.96 | 21.08 | 18.70 | 103.1 |
| Original GPU | 21.42 | 24.86 | 26.04 | 23.52 | 429.2 |
| Atlas | 22.51 | 27.31 | 32.37 | 26.47 | 433.0 |
| Direct | 20.93 | 22.50 | 23.40 | 22.36 | 397.6 |
| Atlas + direct | 21.47 | 23.89 | 30.30 | 24.85 | 401.4 |
| Atlas + foreground preparation + direct | 14.85 | 19.36 | 15.80 | 15.21 | 480.0 |
| Atlas + terrain preparation + direct | 25.23 | 42.03 | 25.63 | 24.74 | 402.0 |
| Full preparation offload | 17.78 | 96.87 | 23.58 | 21.68 | 512.0 |
| Retained viewport | 23.71 | 26.19 | 29.57 | 23.21 | 480.4 |
| Combined | 14.66 | 93.69 | 19.19 | 15.90 | 480.8 |
| Streamed foreground | 14.95 | 21.23 | 15.65 | 15.50 | 480.1 |

Batching reduces actual GL draws substantially, but batching alone is not the
main performance win. GPU-prepared foreground pages also change the image and
texture-handling path, reducing warm texture bindings and submission cost even
when no new resizing is required. The foreground atlas currently traverses the
visible foreground twice, once to prepare pages and once to draw. That leaves
CPU work to optimize.

For 4K pan, the original GPU stream averages 4,406 GL draws and 4,417 texture
bindings per frame. Atlas alone brings draws to 307 but still performs 2,241
bindings; streamed foreground gives 306 draws and 294 bindings. Graphics-engine
time averages 3.88, 3.92 and 2.31 ms/frame respectively, while their CPU submission
p95 values are 19.89, 20.82 and 12.24 ms. Engine means and submission p95 are
different statistics and are not additive. Together with the native timings,
they point to command/texture handling rather than exhausted GPU arithmetic.

Full offload's zoom tail has a specific source-preparation problem. In the
second 4K run it reopened 656 source rasters in 120 measured frames and spent
up to 94.6 ms preparing textures in a frame. The combined variant had the same
656 loads and a 100.5 ms preparation maximum. Selective foreground preparation
had eight loads and a 4.1 ms maximum. The follow-up below tests encoded sources
with both full and selective preparation offload.
`DemandRasterLoads` counts C# raster loads or encoded-image admission; it does
not count Skia's internal lazy decodes. Encoding caches do not move PNG decoding
onto the GPU. Complete frame timings include that deferred work when it occurs.

Retention helps the median local edit, but the bounded patch chain periodically
requires a full rebuild. It therefore gives less benefit to the p95. Camera
changes rebuild the retained viewport, and global faction recolors often damage
most of it. Lower times in picture-recording subphases must not be mistaken for
eliminated work: deferred pictures move execution into later Skia submission.

### More terrain offload with cached sources

The follow-up adds 5,760 measured frames, with the same atlas, encoded-source
index, direct output and 512 MiB GPU cache in both candidates. Only the full
candidate also offloads terrain preparation. Values are median per-run p95 ms.

| Viewport / candidate | Pan | Zoom | Global edit | Local edit |
| --- | ---: | ---: | ---: | ---: |
| 4K selective | 19.20 | 22.44 | 20.41 | 18.95 |
| 4K full | 18.94 | 36.64 | 18.78 | 19.07 |
| 5K selective | 22.32 | 31.91 | 21.88 | 21.64 |
| 5K full | 18.35 | 144.06 | 21.07 | 22.08 |

The full candidate admits 16 sources in each 4K zoom run, with a largest C#
preparation time of 1.82 ms, versus 656 loads and roughly 95–100 ms in the
earlier full-offload candidates. Those earlier candidates also differ in atlas,
retention or direct-output settings, so their complete-frame timings are not a
pure encoded-cache ablation. The follow-up's full/selective comparison is matched.

Source caching does not resolve 5K terrain churn. The full candidate's zoom p95
ranges from 143.26 to 145.44 ms; graphics-engine time averages only 6.81 ms/frame.
Some frames rebuild 464 chunks, and the worst measured submissions exceed
330 ms. Its application chunk cache is limited to 192 MiB, versus the CPU
path's larger viewport-dependent budget, while Skia reaches its 512 MiB limit.
GPU ground preparation also records chunks serially. These are limitations of
this candidate's cache allocation and CPU command preparation, not evidence
that the GPU cannot rasterize terrain efficiently. Protecting the visible
working set, retaining useful zoom tiers and reducing repeated chunk planning
are further optimization opportunities. Native Skia decoding/driver costs are
included in complete timing but are not individually attributed by this probe.

Selective zoom has a 20.16–23.97 ms p95 range at 4K and 29.97–46.29 ms at 5K
in this follow-up. These later trials are retained separately from the primary
sweep; their medians are not substituted into it.

### Foreground preparation without the atlas

Another matched 5,760-frame control keeps `Textures,Encoded,Direct` in both
candidates and toggles only `Atlas`. Median per-run p95 ms:

| Viewport / candidate | Pan | Zoom | Global edit | Local edit |
| --- | ---: | ---: | ---: | ---: |
| 4K without atlas | 25.97 | 24.97 | 23.96 | 24.56 |
| 4K with atlas | 17.10 | 26.37 | 20.01 | 18.89 |
| 5K without atlas | 24.50 | 38.26 | 25.65 | 28.14 |
| 5K with atlas | 17.77 | 33.94 | 20.73 | 21.05 |

At 4K pan, omitting the atlas gives 2,195 draws and 4,134 bindings per frame,
versus 306 and 294 with it. Thus batching contributes materially once combined
with GPU preparation, even though atlas-only was insufficient. The 4K zoom
distributions overlap: 23.89–26.13 ms without the atlas and 21.98–27.57 ms with
it. These trials do not establish an atlas win for every workload.

## Pipelining

The bounded-depth sweep contains 25,920 measured frames. Each cell below is
**throughput FPS / p95 GPU-completion milliseconds** for streamed foreground,
with medians of three independent series. All work, including the final queued
frames, is drained before throughput is calculated.

| Workload | One frame | Two frames | Three frames |
| --- | ---: | ---: | ---: |
| 1080p pan | 107.6 / 11.08 | 114.6 / 10.85 | 116.0 / 10.16 |
| 1080p zoom | 140.3 / 17.08 | 149.6 / 17.47 | 151.2 / 17.61 |
| 4K pan | 89.6 / 16.70 | 99.6 / 16.97 | 104.0 / 15.35 |
| 4K zoom | 115.4 / 19.95 | 121.4 / 27.20 | 130.9 / 21.22 |
| 5K pan | 54.0 / 23.09 | 89.2 / 21.67 | 94.1 / 22.23 |
| 5K zoom | 57.2 / 35.40 | 62.0 / 44.49 | 49.9 / 66.12 |

Pipelining helps throughput, especially large pans, but a deeper queue is not
a consistent tail-latency improvement. Three targets hurt 5K zoom throughput
and nearly double its p95. Its peak Skia cache is at the 512 MiB budget. Runs
also vary: 5K pan's three-frame per-run p95 values are 18.20, 22.23 and 49.98 ms;
two frames give 22.69, 21.67 and 20.77 ms. The one-frame run reaches 41.24 ms in
its slowest series. These distributions support a bounded, short queue and
workload-aware testing, not a universal triple-buffering recommendation.

Host-observed fence completion can be much later than actual GPU completion
when the CPU checks a finished fence after preparing another frame. For example,
1080p streamed pan at depth two reports 24.87 ms p95 at the host but 10.85 ms
from correlated GPU timestamps. The former is not evidence that the GPU took
24.87 ms to finish that frame.

No new frame queue is added to the application by this experiment. Native
`OPENCIV2_MAP_GPU_SYNC=0` uses Avalonia's normal asynchronous pipeline; the
one/two/three-target experiment is a separate controlled offscreen harness.

## Native input latency and observer effects

The final presentation-driven sweep contains 32,400 injected inputs across
108 independent series. Each cell shows **synchronized / asynchronous p95 ms**,
the median of three run p95 values for observed input stamps. These are the
window-image proxy described above, not physical display latency. In particular,
the 5K rows have substantial capture interference and are diagnostic only.

| Viewport / input rate | CPU | Original GPU | Streamed GPU |
| --- | ---: | ---: | ---: |
| 1080p / 60 Hz | 44.7 / 46.5 | 66.2 / 55.7 | 60.5 / 58.5 |
| 1080p / 120 Hz | 43.7 / 46.6 | 57.7 / 53.8 | 54.2 / 57.5 |
| 4K / 60 Hz | 66.1 / 65.7 | 72.9 / 68.0 | 61.2 / 58.0 |
| 4K / 120 Hz | 62.3 / 64.6 | 65.9 / 62.6 | 56.3 / 59.4 |
| 5K / 60 Hz, intrusive capture | 96.1 / 298.7 | 124.2 / 269.2 | 144.4 / 149.8 |
| 5K / 120 Hz, intrusive capture | 155.8 / 286.0 | 127.4 / 226.2 | 116.6 / 149.6 |

The optimized GPU path does not yield a consistent input-latency win at 1080p;
at 4K it improves this proxy modestly. Removing synchronous waits does not
uniformly improve the tail. Input and presentation phase alignment, coalescing,
host scheduling and capture overhead matter alongside map draw time. These
tests do not establish an optimal application queue depth.

The initial 1 ms polling method performed a synchronous coordinate translation
before every injection and repeatedly read the window. At 4K CPU async, image
reads took roughly 17–19 ms p95 and the median reported input age was about
204 ms. It coupled injection to X-server readback stalls. Those 32,400 inputs
are retained under `latency-poll` as rejected latency evidence. Caching input
coordinates and capturing after presentation events reduced the same CPU
configuration to 65.7 ms, with readback p95 below 3 ms. Initial latency driver
snapshots and the interrupted descriptor-enumeration trial are retained.

At 5K, even presentation-driven readback can block: maximum per-run readback
p95 is 27.2 ms for CPU async and 67.1 ms for streamed GPU async. Direct injection
itself stays within 0.4 ms of schedule at p95, but X-server processing can still
queue behind readback. Consequently the 5K numbers cannot be attributed wholly
to the application's normal pipeline. The reduced-frequency control below
assesses this observer effect; physical input-to-photon measurement remains open.

A separate 4K control keeps the cached-coordinate injector and reads every
4 ms without waiting for presentation events. At 60 Hz input, CPU async's
per-run p95 values are 101.4, 100.9 and 244.5 ms; streamed GPU gives 64.1, 66.7
and 173.4 ms. Readback p95 reaches 31.3 and 24.5 ms respectively. This reinforces
the need to retain the capture method and its overhead beside every latency
number. Input timing alone cannot make synchronous window readback passive.

At 5K, reading only every fourth Present notification also fails to produce
stable low-overhead window-image latency. For async CPU, median run p95 is
359.2 ms at 60 Hz and 273.3 ms at 120 Hz; for streamed GPU it is 121.2 and
104.6 ms. Per-run ranges remain broad (CPU 320.5–457.8 ms and GPU
114.0–174.0 ms at 60 Hz), and readback p95 can still reach 33.8 and 61.7 ms.
Only 55 CPU and 138 GPU input stamps out of 900 are observed at 60 Hz with
this sparse capture. This control does not prove the remaining delay belongs
entirely to either the application or the observer.

The final 5K control performs **no pixel readback**. It passively counts Present
notifications and samples the existing draw metadata while independently
injecting the same inputs. Each row covers three runs of 300 inputs, with
asynchronous GPU submission and the same 512 MiB cache. Values are medians of
run p95 values, except the final column's median notification rate.

| Mode / input rate | Injection to sampled receipt, ms | UI input to CPU drawing end, ms | Map drawing on CPU, ms | Present notifications/s |
| --- | ---: | ---: | ---: | ---: |
| CPU / 60 Hz | 97.61 | 78.09 | 32.89 | 32.8 |
| Streamed GPU / 60 Hz | 74.29 | 58.53 | 20.40 | 41.0 |
| CPU / 120 Hz | 90.55 | 68.05 | 31.17 | 33.2 |
| Streamed GPU / 120 Hz | 72.50 | 51.67 | 20.61 | 39.6 |

The first metric includes host observation scheduling and polling. The second
is measured inside the application from its pointer handler to the end of map
drawing calls, before releasing the Skia lease; it excludes pre-dispatch input
delay and later host work. Neither waits for GPU completion in this asynchronous
mode. Notification rate is not a unique-input delivery
rate or a physical scanout measurement. Metadata-read p95 stays below 0.5 ms.
At 60 Hz, CPU receipt p95 ranges from 97.0 to 98.8 ms and GPU from 57.7 to
77.0 ms; GPU notification rate ranges from 40.8 to 59.6/s. These observations
support a real GPU improvement before presentation and show substantial
application scheduling/queue delay even without image capture. They do not
validate the intrusive 5K window-image values as physical latency.

Not every injected coordinate is sampled. For async CPU / original GPU /
streamed GPU at 60 Hz, observed counts are 892 / 825 / 770 out of 900 at 1080p,
and 769 / 805 / 748 at 4K. At 120 Hz they are 453 / 417 / 389 and
386 / 407 / 385 respectively. Missing samples can reflect coalescing or capture
timing; they are not automatically dropped application frames. Percentiles
describe observed stamps, not a delivery guarantee for all injected inputs.
No duplicate or out-of-order matched stamp transitions occurred in the final
primary sweep.

## Cache pressure

The 5K zoom cache sweep contains 4,320 measured frames. Entries are p95 complete
frame milliseconds; all other settings and input sequences are held fixed.

| Variant | 256 MiB budget | 512 MiB budget | 768 MiB budget |
| --- | ---: | ---: | ---: |
| Original GPU | 72.51 | 41.63 | 35.98 |
| Atlas + direct | 47.78 | 39.18 | 36.69 |
| Full preparation offload | 188.80 | 182.44 | 175.10 |
| Streamed foreground | 61.18 | 34.34 | 27.38 |

Streamed foreground uses 256.0/512.0/693.6 MiB at its sampled peaks under those
budgets. Average upload payload falls from 10.93 to 3.51 to 1.66 MiB/frame.
Original GPU falls from 29.05 to 3.31 to 1.66 MiB/frame, with a 660.8 MiB peak
under the largest budget. This is evidence of cache pressure, not GPU memory
exhaustion. Increasing the GPU cache barely fixes full offload's long tail:
the separate CPU source-preparation churn remains.

The matched Native AOT 5K zoom follow-up uses the same executable and input
driver for another three runs of 300 changes at a 768 MiB budget. GPU p50/p95
becomes **16.88 / 28.65 ms**, with per-run p95 27.99–28.65 ms and peak Skia
usage 743.6 MiB. At 512 MiB it was 17.57 / 36.82 ms; corrected CPU was
27.24 / 31.59 ms. The larger budget removes this measured native zoom regression,
at the cost of roughly 232 MiB more peak Skia cache. This follow-up measures map
work, not the input-latency effect of that budget. It does not justify reserving
768 MiB unconditionally on every device.

During async native pan-input trials, sampled peak **driver-resident MiB /
process RSS MiB** across both input rates were:

| Viewport | CPU | Original GPU | Streamed GPU |
| --- | ---: | ---: | ---: |
| 1080p | 98.6 / 651.3 | 206.2 / 647.6 | 276.8 / 690.4 |
| 4K | 277.7 / 818.2 | 460.0 / 785.6 | 520.1 / 799.0 |
| 5K | 465.9 / 871.0 | 625.3 / 806.2 | 665.6 / 827.3 |

These categories overlap and must not be added. The GPU candidate increases
resident graphics resources, while CPU raster caches and backing buffers also
consume substantial memory. There was no observed allocation failure or device
loss. Cache churn, repeated preparation and queue buffers are the demonstrated
concerns; these measurements do not establish a total-system memory budget.

## Result and next engineering steps

The strongest candidate is selective foreground preparation with atlas batching,
encoded sources and direct output. It improves the matched 4K map workloads
and 5K pan/edit workloads; a larger cache fixes the measured 5K zoom tail.
1080p is mixed, and actual host input/presentation behavior does not follow
offscreen throughput automatically. CPU composition remains the default.

Before promotion, make atlas placement/sampling independent of cache history
and resolve CPU/GPU visual acceptance. For further speed, prioritize stable
GPU image residency and less repeated foreground command generation. Full
terrain offload needs working-set-aware chunk budgets and less serial planning;
merely moving rasterization to the GPU does not remove those CPU costs. Keep
frame queues short and test a latest-input policy in the host before adding
more buffers. Actual display latency needs a nonintrusive presentation trace
or physical measurement; the intrusive X11 image values are not a release gate.

## Validation and retained evidence

The final checks include root and optional application formatting/verification,
a Release probe build with zero warnings/errors, the Native AOT publish,
40-project boundary/package-lock validation, 54-family/437-group coverage
validation, Python compilation and `git diff --check`. No gameplay compatibility
claim follows from those infrastructure checks. Renderer evidence comprises
213 unchanged CPU PNGs, 20 exact CPU-to-GPU upload cases, 22 native interaction
checks, 2,352 GPU matrix cases across eight candidates, and all 8,100 native
observation states with 297 sampled fresh-render comparisons. Rejected strict
pixel comparisons are explicitly listed above and are not counted as passes.

`build/m2-gpu-experiments/evidence.json` hashes the 62 retained result/identity
receipts and the Native AOT executable. It verifies all 240 native compiled
inputs against the final application sources. The follow-up probe's 256 unique
compiled inputs also match; the primary probe's earlier 81 assembly inputs are
archived separately. `summary.json` is generated by
`tools/dotnet/analyze_gpu_experiments.py` and includes per-run ranges and separate
latency-control scopes. The sweep has 105,480 instrumented/native measured
frames plus 900 native cache-follow-up frames. The final input sweep has
32,400 inputs, with another 9,000 in the three observer-control groups. Failed,
interrupted and superseded trials are retained separately.

Final SHA-256 values:

| Artifact under `build/m2-gpu-experiments/` | SHA-256 |
| --- | --- |
| `evidence.json` | `040351cc2d9077fb69a47c53976b40d87f106ede31f929d923459ae1eb734697` |
| `summary.json` | `13f4396e89f84516ed0d12a383fbb2f61070701aadc8c15ef71c39abb0294968` |

## Reproduction

Use the pinned SDK, signed AOT package cache and admitted content roots described
in [BUILDING](../BUILDING.md). The existing native receipts are under
`build/m2-gpu-prototype/benchmark`; new artifacts are under
`build/m2-gpu-experiments`.

GPU/window trials require the system KWin virtual backend, Xwayland, `glxinfo`,
`xrandr`, XTest and `libXpresent.so.1`, in addition to the existing Python/Pillow
and native application prerequisites. The driver isolates its display, session
bus and XDG directories. `--baseline` can also point at a newly generated native
comparison directory; only its recorded GPU input states are replayed, not its
old timing conclusions. Exact replay uses the retained input hashes.

```sh
dotnet format OpenCiv2.slnx --no-restore
dotnet format solutions/OpenCiv2.App.slnx --no-restore
dotnet build solutions/OpenCiv2.App.slnx -c Release --no-restore

python tools/dotnet/run_gpu_experiments.py --stage profile \
  --assets ASSET_ROOT --fonts FONT_ROOT \
  --baseline build/m2-gpu-prototype/benchmark \
  --output build/m2-gpu-experiments/profile
# The same driver supports --stage pipeline, cache, matrix and latency.
# Matrix needs --fixtures ART_FIXTURES; latency needs --adapter NATIVE_AOT_EXECUTABLE.
# For the final latency method also pass --latency-capture present --frames 300.
# --resume keeps complete latency configurations, preserving their driver hashes.

python tools/dotnet/run_gpu_experiments.py --stage profile \
  --variants full-streamed,streamed --size 3840x2160 --size 5120x2880 \
  --assets ASSET_ROOT --fonts FONT_ROOT \
  --baseline build/m2-gpu-prototype/benchmark \
  --output build/m2-gpu-experiments/offload-followup

python tools/dotnet/compare_map_composition.py \
  --assets ASSET_ROOT --fonts FONT_ROOT \
  --gpu-features Atlas,Textures,Encoded,Direct \
  --output build/m2-gpu-experiments/native \
  --adapter NATIVE_AOT_EXECUTABLE

python tools/dotnet/run_xwayland_virtual.py --compositor-log UPLOAD_LOG \
  dotnet tests/OpenCiv2.MapProbe/bin/Release/net11.0/OpenCiv2.MapProbe.dll \
  ASSET_ROOT FONT_ROOT UPLOAD_REPORT --gpu-upload-check

OPENCIV2_MAP_COMPOSITION=gpu \
OPENCIV2_MAP_GPU_FEATURES=Atlas,Textures,Encoded,Direct \
OPENCIV2_MAP_LATENCY=1 OPENCIV2_MAP_GPU_SYNC=0 AVALONIA_GLOBAL_SCALE_FACTOR=2 \
python tools/dotnet/run_xwayland_virtual.py --compositor-log RECEIPT_LOG \
  python tools/dotnet/measure_map_latency.py \
  --assets ASSET_ROOT --fonts FONT_ROOT --output RECEIPT_DIRECTORY \
  --size 5120x2880 --expected-density 2 --native-aot --frames 300 --runs 3 \
  --capture receipt --adapter NATIVE_AOT_EXECUTABLE
# Use --capture present --present-stride 4 for the sparse window-image control.
# Use --capture poll --poll-ms 4 --direct-input for the timed-polling control.
# Repeat with OPENCIV2_MAP_COMPOSITION=cpu and features=None for CPU controls.
```

The original `--gpu-matrix` still requires exact retained/fresh GPU pixels.
`--gpu-experiment-matrix` runs the same checks plus repeated local edits, records
every retained/fresh difference, and continues to quantify rejected candidates;
it never reports differing frames as exact. Semantic count failures still fail
the experiment immediately. CPU/GPU differences remain separately reported.
