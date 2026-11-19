# M2 GPU composition prototype

**Superseded performance conclusion:** the follow-up
[GPU experiments](m2-gpu-experiments.md) found that the CPU renderer's aliased
worker writes could leave a stale GPU upload of its viewport. The original CPU
timings below sometimes omitted the required changed-image upload. They remain
historical receipts, not a valid basis for choosing a compositor. The follow-up
fixes that invalidation and reruns the comparison with GPU readback checks.

This experiment compares the current software map compositor with an opt-in
GPU composition path. It reuses the cleanroom P02 selectors, observer-filtered
scenes, draw ordering, assets, font preparation, gallery and input workloads.
It is not complete M2 native/browser acceptance.

## What runs on the GPU

`MapGpuComposition` allocates a budgeted, physical-pixel Skia GPU target inside
Avalonia's graphics lease. It asks `MapPainter` to draw cached terrain chunks,
foreground sprites, labels, paths, selection and effects directly into that
target, then composites its texture into the host canvas. It does not build or
upload a full CPU viewport image on each changed frame. Target/image references
are released before leaving the graphics lease; no application-owned GPU handle
crosses to scene publication or a preparation worker. Skia manages its texture
upload and resource caches.

The experiment configures a 512 MiB Skia GPU cache ceiling for **both** measured
paths and records usage after each synchronized frame. This is a cache budget,
not a reservation or a total-process VRAM measurement. Ordinary CPU launches keep
the host default. An initial interrupted trial used Avalonia's approximately
[28 MiB UI default](https://github.com/AvaloniaUI/Avalonia/blob/master/src/Skia/Avalonia.Skia/SkiaOptions.cs),
which is smaller than one 5K RGBA image; its receipts remain
in `build/m2-gpu-prototype/default-cache-incomplete/` and are excluded from the
final comparison. Giving the two paths the same explicit budget avoids comparing
GPU composition under that undersized cache with a mostly single-image upload.

This is a **hybrid prototype**. PNG decoding, tint/scale preparation, tile
selection, layout and cached terrain-chunk rasterization still run on the CPU.
The experiment measures moving per-frame composition to the GPU, not a rewrite
of terrain generation or the asset pipeline. There are no new content sources,
reduced object counts or omitted layers.

CPU composition is the default. Set `OPENCIV2_MAP_COMPOSITION=gpu` for the
prototype and `OPENCIV2_MAP_GPU_SYNC=1` for measurement. A missing Skia GPU
context reports an error instead of silently measuring the CPU path. The
benchmark also requires hardware GLX and increasing DRM graphics counters in
the actual desktop process. Each receipt records its composition mode and
synchronization status, and the driver rejects a mismatch.

## Measurement method

Both paths use the same signed Linux x64 Native AOT executable, admitted assets,
fonts and authored Mixed world. Native pointer input exercises pan, continuous
zoom and dirty-world updates. Physical backing sizes are 1920×1080 at 1×,
3840×2160 at 2×, and 5120×2880 at 2×. Each workload has three independent runs,
30 warmup changes and 300 measured changes per run. The driver alternates which
composition runs first between resolutions.

Before timing, the host drains preceding GPU work. The timed interval includes
CPU preparation/drawing and a synchronous Skia GPU flush/submission; it cannot
report only command recording while leaving that frame's GPU work outstanding.
`gpuWaitMs` is the CPU time spent submitting and waiting, **not** a GPU timestamp
query. Dirty-world summaries additionally include authored update construction
and production scene binding. Input-to-polled-receipt includes polling overhead.
Neither value measures physical display presentation or compositor frame pacing.
These synchronized measurements should not be compared directly with the earlier
unsynchronized draw-callback results in the general visual ledger.

Raw reports include samples, source/content identities, driver hashes, machine
and graphics-driver details, and per-run quantiles. Do not pool separate runs
into an apparent pass. `comparison.json` summarizes the median of the three
run p95 values, while preserving each run's quantiles.

## Results

Measured on the AMD Ryzen AI 9 HX 370 / Radeon 890M, with approximately 94 GiB
system RAM, Linux 7.0.0-43, Mesa 26.0.8 radeonsi, and isolated KWin virtual
Xwayland. The executable uses .NET 11 Native AOT, Avalonia 12.1.6 and SkiaSharp
4.156.1. All 54 series completed: **16,200 measured changed frames**, excluding
warmup. Every receipt confirmed the requested composition, GPU synchronization,
512 MiB cache ceiling and increasing process graphics-engine counters.

Times below are milliseconds, the median of three independent run p95 values.
Each cell shows **CPU / GPU**. Dirty world includes update preparation and binding.

| Physical map size | Pan | Continuous zoom | Dirty world |
| --- | ---: | ---: | ---: |
| 1920×1080 | 12.51 / 25.60 | 10.76 / 20.77 | 15.50 / 36.69 |
| 3840×2160 | 19.92 / 25.14 | 17.95 / 22.31 | 22.02 / 36.22 |
| 5120×2880 | 25.25 / 30.29 | 25.77 / 38.83 | 26.75 / 36.80 |

The prototype is **1.20–2.37× slower on this median-p95 comparison**. This
experiment does not justify changing the default renderer. It also does not
show that a different, more thoroughly batched GPU renderer cannot improve
performance. This prototype retains CPU terrain preparation and issues the
existing foreground primitive stream directly to Skia's GPU backend. Warm pan
usually builds no new ground chunks; its receipts still show substantial
foreground command work and GPU submission/completion time.

Peak Skia GPU-cache usage across these runs, in MiB:

| Physical map size | CPU composition | GPU composition |
| --- | ---: | ---: |
| 1920×1080 | 20.65 | 130.52 |
| 3840×2160 | 69.68 | 471.56 |
| 5120×2880 | 162.92 | 511.92 |

The 5K GPU workload reaches the configured ceiling. A future experiment should
profile foreground submission and texture residency, then test a persistent
sprite atlas and batching before expanding the GPU path. Increasing a cache
budget alone did not produce a win in this experiment.

Per-run p95 values retain variability rather than hiding a slower run:

| Size | Workload | CPU runs 1 / 2 / 3 | GPU runs 1 / 2 / 3 |
| --- | --- | ---: | ---: |
| 1080p | Pan | 12.28 / 12.51 / 12.98 | 22.92 / 25.87 / 25.60 |
| 1080p | Zoom | 10.76 / 10.74 / 11.16 | 20.77 / 20.76 / 21.44 |
| 1080p | Dirty | 14.93 / 15.88 / 15.50 | 36.82 / 36.69 / 36.50 |
| 4K | Pan | 19.53 / 19.92 / 19.96 | 25.14 / 24.39 / 32.10 |
| 4K | Zoom | 17.57 / 18.38 / 17.95 | 22.31 / 21.75 / 22.67 |
| 4K | Dirty | 21.93 / 22.02 / 22.16 | 36.20 / 37.09 / 36.22 |
| 5K | Pan | 25.30 / 24.76 / 25.25 | 30.29 / 32.84 / 24.93 |
| 5K | Zoom | 26.14 / 25.77 / 25.47 | 38.03 / 41.91 / 38.83 |
| 5K | Dirty | 26.75 / 26.73 / 26.83 | 37.12 / 36.80 / 36.67 |

Raw p50/p95/p99/max values and every sample are in
`build/m2-gpu-prototype/benchmark/<physical-size>/<cpu|gpu>/native-performance.json`.
The compact summary is `build/m2-gpu-prototype/benchmark/comparison.json`.

## Correctness and sampling

The existing software matrix remains the regression suite for the default path.
The GPU matrix reuses all MV01–MV30 boards and Mixed, the existing native and
classic replacement fixtures, and the existing effect fixture. At both 1× and
2× density it checks semantic tile/object/label/effect counts and exact equality
between retained and freshly constructed GPU renderers. Additional cases cover
edits, pan, zoom, resource recovery and observer replacement: 246 cases in total.

CPU/GPU pixel differences are recorded separately, never treated as exact
equality. An independent small Skia image experiment identified a nearest-
neighbor tie difference: the CPU chooses the lower texel, while GPU normalized
coordinates can choose either side. The prototype offsets ground source lookup
coordinates by -0.001 source texel, preserving the source extent and world
geometry. For the stock distant Mixed board this reduced mean absolute RGB
error from 12.6088 to 0.1316 on the 0–255 scale in the initial sampling diagnostic.
The final independent context also matches Avalonia's stencil/antialiasing
policy (`AvoidStencilBuffers = true`) and measures 0.1310 for that board.
All 246 corrected GPU cases
match fresh GPU draws exactly. Remaining CPU/GPU differences include fractional
sampling, antialiasing and glyph rasterization; the worst gallery mean RGB error
is 1.4882 in the native-art Mixed zoom case at 2×. This remains a separate visual
acceptance question, not a claim of pixel-identical software/GPU output.

`--replay-gpu-map` reuses the native navigation replay's observations, camera
states, changes and effect phases. It exercises every recorded frame and checks
its semantic counts. It compares the first, every thirtieth and last frame
exactly against a fresh GPU renderer and reports CPU pixel differences at those
same samples. This is explicitly sampled pixel evidence. The CPU-only replay
rejects GPU receipts so it cannot accidentally validate a software substitute.

All **8,100 GPU benchmark states** replayed successfully. Each resolution has
2,700 checked observation frames and 99 exact sampled fresh-GPU comparisons:
**297 pixel comparisons** total. The maximum mean RGB error against CPU at those
samples was 0.1822 at 1080p, 0.1470 at 4K and 0.1765 at 5K (rounded upward).
These are not bit-identical CPU/GPU frames. The broader gallery includes more
demanding fractional registrations, as reported above.

The final software matrix passes and reproduces all **213** reference PNGs from
the paused M2 work byte for byte. The existing **22 native Riverbend input and
lifecycle checks** also pass with GPU composition enabled. Root and optional
application formatting verification, the Release solution build, project-boundary
and coverage validators, Python compilation and `git diff --check` pass.

## Retained evidence

The local evidence manifest is `build/m2-gpu-prototype/evidence.json`. Reports
carry compiled source identities and the benchmark reports also carry the
content and harness hashes. These files remain local build artifacts; the table
below records their SHA-256 digests. Paths are relative to
`build/m2-gpu-prototype/`.

| Receipt | SHA-256 |
| --- | --- |
| `benchmark/comparison.json` | `77471d286b1dc5b5f49720d26367670eea18a14732e89b64c8ec947ce4b8879e` |
| `benchmark/1920x1080/cpu/native-performance.json` | `9dc04d5b8237f161294fa8542a6899399bbda934208f61d239cd4a0a5947b616` |
| `benchmark/1920x1080/gpu/native-performance.json` | `32c56d2885a0bcfa6c38508bf603274156c399d88e48700a2f4e0eddcd38da40` |
| `benchmark/3840x2160/cpu/native-performance.json` | `7cb4da9a63c264452e16fefe0216b8c83b092cbae521cc9c79fb22910a4cd032` |
| `benchmark/3840x2160/gpu/native-performance.json` | `7f26aaad50594bd870de28b5e2f022fc74b1231312f9b8b23ebaf7b2773ad678` |
| `benchmark/5120x2880/cpu/native-performance.json` | `1034e819e7abbe332dc545ac8b97794c0f76e02dc4c0913315a9b31a5d8e3efa` |
| `benchmark/5120x2880/gpu/native-performance.json` | `344147883dd40b616b15c4fc5eaa21ce606a48793ce3fc5f2d3c6a1f7d62dcfa` |
| `host-gpu-matrix/gpu-matrix.json` | `9688133dbfc3b26fd2d5e96a801e71d64c66a18d3c8ee2bf9cad32b23a5e3520` |
| `cpu-matrix-verified/matrix.json` | `2770fc0bb7b8efbeab834bb642454e14c1a6e1486d9e15bb8231e3c7bfdf8bfc` |
| `native-input/application.json` | `031cbb130e44c30c71a6f7c0f794ad79ced55ca8f3ba753ce9274e58dad6ab37` |
| `replay/1920x1080/replay.json` | `7723c0335e3c235c458ac40d87e3fa61689cdff6f085de3f23e0261dcc7ddf1e` |
| `replay/3840x2160/replay.json` | `8a6eb10e89039ba508c678db2347a84941a26911e754091cb123b4174ae3d0b1` |
| `replay/5120x2880/replay.json` | `f23ed8fd6c4ccf47d90f786ed6e2b3d786083405e23e6b5c335ac781c6df013f` |

## Reproduction

Use the pinned SDK and signed AOT package cache described in BUILDING. Build
before timing, and run correctness jobs separately from benchmarks.

```sh
dotnet format OpenCiv2.slnx --no-restore
dotnet format solutions/OpenCiv2.App.slnx --no-restore
dotnet build solutions/OpenCiv2.App.slnx -c Release --no-restore
dotnet restore src/OpenCiv2.Desktop -p:Oc2DesktopAot=true --locked-mode
dotnet publish src/OpenCiv2.Desktop -c Release -p:Oc2DesktopAot=true --no-restore

python tools/dotnet/compare_map_composition.py \
  --assets ASSET_ROOT --fonts FONT_ROOT --output build/m2-gpu-prototype/benchmark \
  --adapter NATIVE_AOT_EXECUTABLE

dotnet tests/OpenCiv2.MapProbe/bin/Release/net11.0/OpenCiv2.MapProbe.dll \
  ASSET_ROOT FONT_ROOT build/m2-gpu-prototype/cpu-matrix --matrix ART_FIXTURES

python tools/dotnet/run_xwayland_virtual.py \
  --compositor-log build/m2-gpu-prototype/gpu-matrix/compositor.log \
  dotnet tests/OpenCiv2.MapProbe/bin/Release/net11.0/OpenCiv2.MapProbe.dll \
  ASSET_ROOT FONT_ROOT build/m2-gpu-prototype/gpu-matrix --gpu-matrix ART_FIXTURES

python tools/dotnet/run_xwayland_virtual.py \
  --compositor-log build/m2-gpu-prototype/replay/compositor.log \
  dotnet tests/OpenCiv2.MapProbe/bin/Release/net11.0/OpenCiv2.MapProbe.dll \
  ASSET_ROOT FONT_ROOT build/m2-gpu-prototype/replay/replay.json \
  --replay-gpu-map NATIVE_GPU_RECEIPT
```

The GPU correctness context is a Linux EGL test utility; it is not a new game
dependency. The prototype's rendering code uses Avalonia's existing
[Skia graphics lease](https://api-docs.avaloniaui.net/docs/T_Avalonia_Skia_ISkiaSharpApiLease)
and SkiaSharp's GPU surface and synchronous context-submission APIs. Browser,
other operating systems, context-loss acceptance, long-session GPU resource
behavior and physical presentation remain outside this experiment.
