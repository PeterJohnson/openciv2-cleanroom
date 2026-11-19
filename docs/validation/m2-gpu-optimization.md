# GPU map optimization experiments

This is a Linux renderer optimization experiment, not M2 completion. Other
platform testing remains deferred. CPU is still the user default and the
supported GPU feature set remains `Atlas,Textures,Encoded,Direct` while the
candidates are evaluated. No historical reconstruction sources were used.

The measured host is Linux on Ryzen AI 9 HX 370 / Radeon 890M, Mesa
26.0.8-1ubuntu0.3 (`radeonsi`, ACO), with .NET SDK 11.0.100/runtime 11.0.0,
Avalonia 12.1.6 and SkiaSharp 4.156.1. Native timing uses the signed-package AOT
publication; the separate GL probe is a Release managed build with tiered
compilation disabled. Clocks were not locked. The read-only power snapshot
records AC online, CPU `powersave` with `balance_performance` and boost enabled,
and GPU performance level `auto`; it does not establish thermal throttling.

## Evidence and comparison paths

Artifacts live under `build/m2-gpu-optimization/`. `baseline/aot` and
`baseline/source` preserve the incoming executable and sources;
`baseline/manifest.json` preserves its source/artifact identity. The corrected
CPU upload path is included. The original GPU path remains selectable by its
four original flags in the current executable.

The new command/planning counters measure the map draw thread. They exclude
managed allocations on CPU preparation workers and native Skia allocations.
No GC-pause trace was captured; allocation counts alone do not attribute tail
latency to GC. Slow samples remain in the reported distributions.
Native receipts record physical dimensions, density, source/content identity,
cache limits, memory high-water marks and raw changed-frame inputs. Saved
warmups allow the GL profiler to replay all 30 warmup and 300 measured states
instead of consuming the first 30 measured states as its warmup.

The controlled per-change profiles use these configurations. `sparse` is
compared with `commands`, and `mask-batch` with `commands`, to isolate those
additions; their names do not imply an otherwise feature-free renderer.

| Profile name | Features added to original GPU |
|---|---|
| `streamed` | None: `Atlas,Textures,Encoded,Direct` |
| `commands` | `Commands` |
| `mask-batch` | `Commands,Masks,MaskBatch` |
| `sparse` | `Commands,Sparse` |
| `candidate` | `Commands,Masks,MaskBatch,Sparse` |

The `cpu` profile is the corrected CPU renderer in the same GPU host context.
Native run cells launch independent processes. Controlled profiles use a fresh
painter and purge unlocked GPU resources for each configuration within one
separate probe process per size/density; they rotate variant order across the
three saved native runs. They are not three process-cold profile launches.

The native input audit checks every paired warmup and measured camera, revision,
viewport, density, tile/object count and label count. Those match across CPU,
original GPU and candidate. The interactive host uses its real-time presentation
clock: pulse phase and the progress/overlap of dirty-world explosions differ
between native runs. Native dirty-world timing therefore includes that source of
variation; it is not an identical-effects causal comparison. The separate
instrumented profiles consume the same saved presentation, including explosion
progress/order and pulse, for every variant. Use those controlled profiles to
attribute per-change costs. `native-input-audit.json` records the native scope.

GPU instrumentation is separate from native timing. GL upload-byte counters
estimate API payload, not bus traffic. Fence completion and graphics-engine
counters are GPU observations; draw duration and polled input-to-receipt timing
are not physical presentation latency.

The asynchronous native `ms` timer surrounds map drawing inside the Skia lease;
it does not include the host's complete frame submission. Instrumented
`submitMs` includes replay update/preparation, drawing and an explicit context
flush. GL timestamp intervals include CPU feed stalls; DRM graphics-engine time
is reported separately. Native dirty-world component sums are work measurements,
not input latency.
The native DRM engine increase covers the collection interval, including warmup;
it is a hardware-work check, not a per-frame completion measurement. The
instrumented profiler starts its engine interval after draining warmup work.
`AtlasHits` counts stable-page content lookups; original-layout reuse is reflected
by zero new page admissions, rather than that counter. Admissions/evictions
count pages, whose granularity differs between the two atlas experiments.

Each density uses the existing displayed-detail and minimum-zoom policy. At
1080p/2× the requested half-world zoom is below the minimum; receipts record the
clamp and actual distinct visible tiles. Compare candidates within each physical
size/density cell, not densities with different displayed detail/populations.

Two preliminary native cohorts are explicitly rejected and preserved:
`rejected-stale-guard-native` exposed an added late-publication guard that cleared
caches on same-observer updates (1080p dirty-world p95 approximately 212 ms).
Removing that guard restored the existing publication-lock contract and reduced
the next diagnostic p95 to approximately 19 ms. `rejected-cache-budget-native`
exposed asynchronous CPU runs retaining the host's approximately 28 MiB GPU
ceiling while GPU runs used 512 MiB. Explicit experiments now apply the requested
ceiling to both paths; the driver requires it in every receipt. Normal CPU
defaults are unchanged. Neither cohort is pooled with the final frozen build.
A later harness review found pan/zoom replay had defaulted the active-unit pulse
instead of restoring it from the receipt. `rejected-default-pulse-profile`,
`rejected-default-pulse-pipeline` and the interrupted replay preserve that
cohort. Only the probe changed: `probe-v6` restores recorded pulses in all
workloads and also asserts label counts. The affected offscreen measurements
are repeated; native AOT timing and latency are unchanged.

The comparison preserves these limits:

| Layer | Declared limit |
|---|---:|
| Shared Skia GPU resource cache in explicit experiments | 512 MiB |
| Prepared texture images | 96 MiB |
| Admitted encoded-source index | 64 MiB |
| Sprite atlas layouts/pages | 64 MiB |
| Existing CPU ground cache at 1080p / 4K / 5K | 240 / 352 / 544 MiB, also bounded by the existing machine policy |
| Command-plan retired-image protection | At most another 96 MiB until that foreground submission ends |
| Experimental composed regions | 128 MiB output-image estimate |
| Experimental GPU terrain chunks / topology plans | 192 MiB / 16 MiB estimated managed storage |

The ground limits are the same for CPU, original GPU and combined candidate;
the separate terrain-offload experiment uses its existing smaller limit. Cache
estimates can overlap. The Skia setting is not a bound on total process or DRM
memory, and these numbers must not be summed as measured residency.

## Stages

### Ordered foreground commands

`Commands` records sprites, primitives, text, clipping, and explicit atlas flush
boundaries during one traversal. Submission does not repeat object selection,
badge fitting or texture lookup. Mutable paint/font state is captured; commands
are cleared after each paint. There is no command retention across frames.

The pinned SkiaSharp atlas API takes exact-length arrays. Common batch lengths
reuse bounded arrays rather than allocating two arrays on every flush. The
command buffer permits at most 262,144 commands. Texture wrappers retired while
planning remain alive until submission, with at most another 96 MiB of prepared
image storage; oversized plans fall back to the original traversal before
foreground submission. Encoded-index rollover also takes that fallback.

The original 294-case matrix and the subsequent 204-case focused history suite
found zero command/current-GPU pixel differences. The first diagnostic profiles
show substantially reduced draw-thread allocations. Final acceptance timing is
recorded separately below.

### Shared masks

`Masks` uses image alpha and per-instance color with `DrawAtlas`'s `DstIn` blend:
the image is its source and instance color its destination. Ordinary multicolor
art remains ordinary image content. `MaskBatch` instead shares one white mask
per binding/tier and uses `Modulate`, allowing ordinary white-colored instances
and colored masks to share an ordered batch. No color participates in mask
texture or atlas-slot identity. Ownership diamonds retain their existing path;
unit shadows, silhouettes, shields, disorder and radius sprites use masks.

The first `SrcIn` experiment was wrong (white shields/shadows) and remains
rejected in `masks-compared-matrix`. Corrected raw-alpha and white-mask variants
have exact cache-history output in the focused tests. Their separate comparison
to the original GPU path measures small edge-rounding differences, rather than
silently replacing reference pixels. Authored RGBA tests include transparent
RGB, fractional sampling, every stock color, custom colors/opacity and ordered
translucent overlap. Recoloring asserts no new resident mask page/slot.
The isolated draw-API fixture uses one 13×11 mask on a fixed single-content
page: 1,020 resident bytes including gutters, still one page across all twelve
colors and twenty-four color/transform cases. Holding layout fixed isolates
alpha/blend behavior; it does not accept the separate stable-layout experiment.
That assertion concerns an already-resident mask. Whole-scene faction-role
changes can still select different ordinary flag/airbase artwork and rebuild
the original visible-set atlas; ownership diamonds also retain color-specific
images. This stage does not eliminate every recoloring allocation or layout
rebuild. The failed stable-placement experiment is kept separate.
The timed dirty-world workload includes recoloring alongside other changes;
its timing does not isolate recoloring alone.

### Stable placement

`StableAtlas` prototypes bounded, immutable single-content pages with fixed
two-texel clamped gutters. Placement depends on content/size/tint representation,
not the visible set. Admission never copies unrelated residents, and pages are
evicted under the same 64 MiB atlas budget. Pages are CPU/picture images; Skia
owns GPU residency.

This trades cross-image batching for less reconstruction. It was slower in the
diagnostic profile and failed exact history checks at several zoom boundaries
(15 focused cases without masks, 10 with raw masks). These are failed gates even
though only a few edge pixels differed. The prototype is not promoted.

### Sparse damage

`MapObservationDelta` is constructed solely from two disclosed observations.
It binds observer/controller identity, consecutive revisions, dimensions and
the actual immutable input lists. A record copied with different lists cannot
reuse stale metadata. It holds list references, not observations, so there is no
chain retaining all prior snapshots. Missing/skipped/replaced/uncertain data
takes full comparison or invalidation.

The opt-in `Sparse` path attaches metadata at the UI observation publication
boundary. It does not change Session observation behavior or CPU defaults.
Changed entity positions cover old and new body bounds. City-label reflow is
compared separately; changed paths, colors and uncertain radius dependencies
use conservative fallback. Ground dependencies include neighboring tiles.
Overlapping damage regions merge; distant edits remain separate. Tests compare
two-index sparse damage with full comparison and reject stale metadata.

On the combined `Direct` path, sparse metadata reduces invalidation comparisons;
foreground composition still redraws the viewport. The experimental region
cache consumes separate regions to retain unaffected backing images. This is
not an accepted incremental compositor while that cache's visual gate fails.

Projection and `MapScene` indexing still construct the complete disclosed
snapshot at a data-change boundary. Delta construction can compare whole input
lists once; this stage removes repeated renderer scans, not all observation
publication work.

The controlled `local-dirty` fixture changes one road and supplies a disclosed
delta to every variant; all include its construction in `updateMs`. Variants
without `Sparse` ignore it. This isolates renderer consumption and is not a
measurement of the current CPU application's observation pipeline. The native
broad dirty-world path attaches deltas only for the opt-in sparse candidate and
includes that work in scene-index timing.

### Independently retained regions and zoom

`PanCache` prototypes 768-pixel regions with two-pixel gutters and a 128 MiB
output-image budget. Coverage is world aligned; display transforms are separate
from backing origins. Region fringes supply surrounding coverage. Independently
replaceable images avoid recursive viewport/patch chains and oversized 5K
textures. City captions are laid out at display resolution. Moving-unit frames
use direct composition to preserve depth; effects and selection remain live.

Simple linear resampling of composed terrain failed the visual comparison on
fractional pans (mean channel error over 13/255 in one focused case). `PanPhase`
keys backing images by subpixel camera phase and presents at physical pixel
alignment. Covered pans at an existing phase need no base reconstruction; a
new phase requires admission. The same camera/state is independent of history.
Rebuilding every region on zooms and unlocalized scene changes was expensive.
An adaptive switch to the original direct path was discarded before acceptance:
the two representations differ, so such a switch could make pixels depend on
navigation history. Region retention remains an explicit experiment, outside
the combined command/mask/sparse candidate.

`TierCache` separately evaluates deterministic power-of-two backing selection
while keeping displayed detail independent. Resampled terrain/status pixels
show substantial visual changes. It is not promoted, and speculative future
GPU preparation is not enabled after this failed visual gate. There is no new
frame queue or application-owned GPU handle surviving a graphics lease.

Region hits mean reuse of an immutable backing descriptor without CPU scene
reconstruction. Skia can evict and regenerate its GPU backing; they are not
proof of GPU residency. Region output estimates exclude shared images retained
by pictures. Atlas, texture, ground and region estimates overlap and must not be
added as a claim of total process/graphics memory. Native RSS and GPU cache
usage are reported separately.

### Terrain planning

`GroundPlans` caches world-coordinate topology plans independently of GPU chunk
images and resolution tiers, with a separate 16 MiB conservative managed-storage
estimate. Dirty neighbor dependencies invalidate both layers. It preserves the
192 MiB experimental GPU chunk budget and the 512 MiB context budget; no budget
increase conceals churn. This remains an offload experiment, subject to the
existing offload pixel failures and measured GPU costs.

### Diagnostic stage comparison

`stage-v3-profile.json` is the earlier screening pass (30 warmup + 90 measured
states per configuration), not the three-run acceptance cohort. Its 5K/1× pan
case illustrates why some prototypes were stopped:

| Variant | Submit p50 / p95 ms | Mean draw-thread allocation KiB | Mean Skia atlas calls / GL draws |
|---|---:|---:|---:|
| Original GPU | 11.511 / 14.437 | 459.8 | 1,345 / 320 |
| Commands | 10.950 / 13.798 | 176.9 | 1,345 / 320 |
| Raw-alpha masks + commands | 20.068 / 24.125 | 177.1 | 3,369 / 322 |
| White-mask batching + commands | 11.047 / 13.639 | 176.4 | 1,345 / 322 |
| Stable single-content pages + commands | 26.544 / 32.247 | 177.6 | 4,474 / 320 |
| Phase-aligned retained regions | 0.677 / 0.696 | 110.0 | 0 / 45 |
| Retained zoom tier | 1.942 / 2.557 | 110.0 | 0 / 45 |
| Terrain topology plans | 11.042 / 14.277 | 179.1 | 1,345 / 320 |

Raw masks and single-content pages increased Skia API submissions substantially;
GL draw counts stayed near 320 in this case. Their slowdown cannot be described
simply as extra hardware draw calls. White-mask modulation preserved the original
ordered batch boundaries. Retained-region pans reused 40 images without region
reconstruction or uploads, but the visual gate failed and other camera phases,
zoom and broad edits required expensive rebuilds. None of these screening
numbers substitutes for the final controlled/native comparisons below.

## Reproduction

Use the pinned SDK (`source build/dotnet11/env.sh` locally) and the existing
asset/font/fixture directories. Examples, from the repository root:

```sh
dotnet build solutions/OpenCiv2.App.slnx -c Release --no-restore
OPENCIV2_MAP_GPU_FEATURES=Atlas,Textures,Encoded,Direct,Commands,Masks,MaskBatch,Sparse \
  python tools/dotnet/run_xwayland_virtual.py --compositor-log OUTPUT/compositor.log \
  dotnet tests/OpenCiv2.MapProbe/bin/Release/net11.0/OpenCiv2.MapProbe.dll \
  ASSETS FONTS OUTPUT --gpu-optimization-checks FIXTURES
```

For this run, `ASSETS` is
`build/m2-native/linux-ci-content-seam-scratch/runtime`, `FONTS` is
`build/m2-native/linux-ci-content-seam-scratch/fonts`, and `FIXTURES` is
`build/m2-native/art-cases-raster-tier`. The frozen measured desktop is
`build/m2-gpu-optimization/aot-v5/OpenCiv2.Desktop`; the final replay/profile probe
is `build/m2-gpu-optimization/probe-v6/OpenCiv2.MapProbe.dll`.
`source/` and `source-manifest.json` under that artifact root bind the compiled
native source snapshot. `probe-v6-source.json` and `probe-v6-source-overlay/`
bind the two later probe-only corrections (recorded navigation pulse and label
assertions). Production renderer sources are identical. Report/analyzer edits
do not alter either frozen binary.

To rebuild from the reviewed working sources into new output directories:

```sh
source build/dotnet11/env.sh
dotnet restore solutions/OpenCiv2.App.slnx --locked-mode
dotnet build tests/OpenCiv2.MapProbe -c Release --no-restore \
  -o build/m2-gpu-optimization/probe-rebuilt
NUGET_PACKAGES="$PWD/build/desktop-aot/upstream-nuget" \
  dotnet restore src/OpenCiv2.Desktop -p:Oc2DesktopAot=true --locked-mode
NUGET_PACKAGES="$PWD/build/desktop-aot/upstream-nuget" \
  dotnet publish src/OpenCiv2.Desktop -c Release -p:Oc2DesktopAot=true \
  -p:CppCompilerAndLinker=clang-22 --no-restore \
  -o build/m2-gpu-optimization/aot-rebuilt
dotnet restore solutions/OpenCiv2.App.slnx --locked-mode
```

The signed AOT package cache and native prerequisites are described in
[BUILDING](../BUILDING.md). Point a new measurement cohort at the rebuilt
artifacts; preserve the original frozen outputs and receipts.

The exact full-cohort invocation is saved in
`build/m2-gpu-optimization/run-acceptance.sh`. It uses:

```sh
source build/dotnet11/env.sh
common=(--assets build/m2-native/linux-ci-content-seam-scratch/runtime
        --fonts build/m2-native/linux-ci-content-seam-scratch/fonts
        --binary build/m2-gpu-optimization/aot-v5/OpenCiv2.Desktop
        --source-root build/m2-gpu-optimization/source
        --probe build/m2-gpu-optimization/probe-v6/OpenCiv2.MapProbe.dll)
for stage in native cold profile latency; do
  python tools/dotnet/run_map_optimization.py --stage "$stage" "${common[@]}" \
    --input-root build/m2-gpu-optimization/native \
    --output "build/m2-gpu-optimization/$stage" --resume
done
python tools/dotnet/run_map_optimization.py --stage pipeline "${common[@]}" \
  --input-root build/m2-gpu-optimization/native \
  --output build/m2-gpu-optimization/pipeline \
  --size 5120x2880 --profile-variants streamed,candidate
for stage in native cold; do
  python tools/dotnet/run_map_optimization.py --stage replay "${common[@]}" \
    --variant candidate --input-root "build/m2-gpu-optimization/$stage" \
    --output "build/m2-gpu-optimization/replay-$stage"
done
python tools/dotnet/analyze_map_optimization.py build/m2-gpu-optimization
```

The final artifact index is
[`evidence-index.json`](../../build/m2-gpu-optimization/evidence-index.json).
It records hashes for accepted receipts, frozen binaries, source identities,
admitted assets, authored fixtures, final drivers and this report, and verifies
that incoming staging was preserved. The main entry points are:

| Evidence | Location under `build/m2-gpu-optimization/` |
|---|---|
| Per-run distributions and comparison tables | [`measurements.md`](../../build/m2-gpu-optimization/measurements.md), [`summary.json`](../../build/m2-gpu-optimization/summary.json) |
| Saved native inputs, cold/recovery runs and process memory | `native/`, `cold/`, `native-input-audit.json` |
| Separate instrumented submission/completion and pipeline profiles | `profile/`, `pipeline/` |
| Input-to-image and receipt latency, sampled process/driver memory | `latency/` |
| Every-frame candidate history checks and sampled CPU comparisons | `replay-native/`, `replay-cold/` |
| Preserved incoming work and compiled source snapshots | `baseline/`, `source/`, `probe-v6-source-overlay/` |

Run stages sequentially without concurrent builds or other GPU workloads. Use a
new output root for a new cohort. `--resume` checks native/cold counts, executable
hash, feature flags and cache ceilings; profile, latency and replay stages rerun
their outputs. Each stage directory contains the exact child invocation and
compositor log. The analyzer rejects missing/duplicate cells and keeps each
independent run's percentiles separate.

`--gpu-experiment-matrix FIXTURES` retains the full existing matrix and adds a
separate current-GPU comparison. `--renderer-options` runs 100 warmed ownership
and context replacements. Experimental failures are recorded, not reclassified
as accepted pixels. `run_map_optimization.py --stage native` interleaves three
independent 30+300 native runs for CPU, original GPU, and combined candidate at
all three physical sizes and both densities. `--stage cold` runs 20 cold and
20 recovery repetitions per run; `--stage profile` consumes the native inputs
in a separate instrumented process. Every invocation records its exact command,
environment and binary hash.

Strict native replay uses `OPENCIV2_REPLAY_ALL=1`. Every recorded frame and saved
warmup is read back and compared with an independent fresh reference. Identical
replay inputs may reuse the reference's SHA-256; its key includes the deterministic
world-mutation inputs, revision, camera, dimensions/density, display, selection
and complete recorded effect presentation. Reference pixels are not retained.
CPU/GPU cross-backend comparison remains separately sampled. This verifies map
replay, not every pixel of a presented native window.

The 5K pipeline experiment uses one, two or three offscreen targets. Its frame
timer starts after waiting for a target slot; completion includes admitted CPU
work and GPU queueing, but excludes any input waiting before admission. Throughput
includes the waits and the final drain. It does not implement or measure a deeper
input queue in the application. The native map path already avoids an explicit
per-frame GPU-completion wait, so gains over the probe's depth-one wait are not
an estimate of additional application speedup. Native latency is measured separately with
300-event horizontal-pan streams at 60/120 Hz, using both Present-triggered image readback and
receipt-only observation; coalesced/unobserved events and capture cost are
reported. The latency driver checks the actual cache ceiling before and after
each stream.
The existing host publishes receipt snapshots on a 25 ms timer, so receipt-only
observation can miss intermediate draws even when presentation is faster. Its
observed-input fraction is not presented FPS. The recorded UI-input-to-draw
duration excludes that publication delay but still does not wait for GPU
completion. The two capture modes use independent streams; subtracting their
percentiles would not isolate compositor latency.
This does not establish zoom input-to-image latency or physical display latency.

## Final checks and acceptance measurements

Completed correctness evidence under `build/m2-gpu-optimization/`:

| Check | Result | Artifact |
|---|---|---|
| Existing CPU visual matrix | 213 PNGs exactly equal to preserved references | `cpu-matrix-v3/`, `cpu-reference-comparison.json` |
| Combined GPU matrix | 294 exact fresh/history comparisons; existing CPU/GPU tolerances pass | `candidate-matrix-v3/` |
| Focused navigation/art/density history | 204 cases, zero history failures | `candidate-focused-v4/optimization-checks.json` |
| Authored mask alpha/blend checks | 24 cases; exact alpha, RGB error at most one level; recolor keeps one resident mask page | Same focused receipt, `masks` |
| Sparse damage | Two distant changes produce two equal-to-full regions with 2 comparisons instead of 31,752; stale metadata falls back to 31,752 | Same focused receipt, `damage` |
| Renderer lifecycle | 800 assertions; 100 warmed replacements; released observer caches and stable retained size estimates | `candidate-lifecycle-v4/renderer-options.json` |
| CPU raster presentation through GPU | 20 exact upload/readback comparisons | `cpu-upload-v3.json` |
| Final recorded-input replay | 18,540 exact fresh/history comparisons; all 720 sampled CPU comparisons within existing tolerances | `replay-native/`, `replay-cold/` |
| Headless regression/build checks | 459 base tests, 437 foundation groups, 40-project graph, 5 identity tests; root/App formatting and builds pass | `base-tests.log`, `foundations.json`, `graph.log`, `identity-tests.log`, `verify-root.log`, `verify-app.log` |

The mask-enabled candidate's separate comparison with current GPU output has a
maximum focused mean channel error of 0.0061455/255 and maximum channel difference
3. These rounding changes are recorded independently of the exact history gate.
The command-only path has zero differences from current GPU in the focused suite.
The lifecycle assertions bound application cache estimates; they do not establish
that process RSS or all driver memory is identical after every replacement.

The frozen v5 host differs from the v4 focused/lifecycle build in the explicit
experiment cache-ceiling policy. The painter, contracts and focused tests are
unchanged. Native gallery checks (26 per renderer), Riverbend checks (22),
hardware preferences (14), and software fallback/retry also passed in earlier
host validation. The final frozen AOT host also passed 14 hardware preference checks and 13
software preference/fallback checks (`options-hardware-v5/`,
`options-software-v5/`). These include saved choice, CLI override, restart,
recovery retry, malformed settings and visible save failure.

The final cohort is complete: 162 native series, 108 cold/recovery series,
432 controlled profiles, 144 pipeline configurations, 216 latency streams and
90 strict replay series. The analyzer found no missing, duplicate or unexpected
cells. Screening diagnostics above remain separate from this full cohort.

Strict replay passed all 17,820 navigation frames (including saved warmups) and
720 cold/recovery frames with exact fresh/history pixels. All 720 separately
sampled CPU comparisons pass the existing cross-backend tolerances: maximum
mean channel error is 0.187568/255 and the largest fraction of pixels differing
by more than eight levels is 0.436343%. These remain distinct checks; the CPU
comparison does not relax the exact candidate-history gate. Final source and
artifact hashes are recorded in the evidence index linked above.

### Native timing, cold/recovery and input latency

The native cohort contains 162 series (48,600 measured changed frames and 4,860
saved warmups). The cold cohort contains 1,080 process-cold starts and 1,080
resource-recovery events, spread across three independent runs in every cell.
The separate latency cohort contains 216 streams and 64,800 injected inputs.
All three renderer paths use the same 512 MiB experimental Skia ceiling.
Timing uses the existing large authored `Mixed` world. Seams, flat edges,
small worlds, custom art, moving units and replacements also have dedicated
correctness fixtures; this cohort does not time every acceptance-matrix case.

Representative native draw p95s below are the worst of the three runs, in ms.
These exclude the host's complete submission/presentation. Full per-run
p50/p95/p99 and all physical-size/density/workload cells are in `summary.json`
and `measurements.md`; dirty-world additionally reports update/bind plus draw.

| Physical viewport | Density | Workload | CPU | Original GPU | Combined candidate |
|---|---:|---|---:|---:|---:|
| 1080p | 1× | Pan | 12.20 | 12.46 | 11.75 |
| 4K | 1× | Pan | 22.43 | 19.47 | 16.58 |
| 5K | 1× | Pan | 27.79 | 17.54 | 15.55 |
| 5K | 1× | Zoom | 34.18 | 61.63 | 63.13 |
| 1080p | 2× | Pan | 8.56 | 5.60 | 6.39 |
| 4K | 2× | Pan | 21.18 | 15.03 | 14.57 |
| 5K | 2× | Pan | 26.51 | 15.32 | 14.80 |
| 5K | 2× | Zoom | 27.38 | 33.65 | 34.36 |

The new stages do not establish a general frame-time improvement. Some pan
cells improve, 1080p/2× pan regresses, and 5K/1× zoom remains substantially
slower than CPU. The 16.7 ms p95 / 33.3 ms p99 full-frame-work targets are not
met across required workloads. A draw-only result below those limits would not
by itself demonstrate full-frame acceptance.

Post-admission-to-first-draw p95 ranges across independent GPU runs are
304–434 ms for the original GPU and 304–426 ms for the candidate, already
above the 250 ms first-correct-frame target. CPU callbacks are 187–239 ms;
those callbacks still precede presentation. The worst resource-recovery
input-to-receipt p95s are 183.85 ms CPU, 293.56 ms original GPU and 288.80 ms
candidate. Receipt publication is included; these are not image observations.

At 5K/1× and 60 Hz input, image-observed pan p95s are:

| Renderer | Independent run p95s (ms) | Observed events per 300 |
|---|---|---|
| CPU | 92.04 / 95.68 / 92.67 | 168 / 162 / 169 |
| Original GPU | 56.81 / 58.48 / 62.17 | 275 / 269 / 268 |
| Candidate | 67.94 / 71.35 / 63.14 | 266 / 263 / 270 |

No observed stream had an out-of-order or duplicate input-stamp transition.
The worst image-observed p95 across all tested sizes, densities, rates and runs
is 95.68 ms CPU, 67.43 ms original GPU and 71.35 ms candidate. Those observed
pan samples stay below the 100 ms target, but unobserved/coalesced events have
no latency sample. This is not complete navigation latency acceptance: zoom
input-to-image and physical scanout remain unmeasured. The candidate does not
consistently improve the existing GPU path.

For the six 5K/1× image-capture streams per renderer, sampled peak RSS / DRM
resident memory are 767.9 / 465.9 MiB CPU, 693.8 / 579.3 MiB original GPU and
681.6 / 589.3 MiB candidate. This is no demonstrated GPU residency reduction.
RSS and DRM overlap; per-heap/shared counters are retained separately. The
reported 512 MiB VRAM heap and much larger GTT capacity on this integrated GPU
must not be added and described as dedicated VRAM. Native post-series memory
snapshots are not peaks; periodic latency samples are lower bounds on peaks.

### Corrected controlled profiles

The final `probe-v6` cohort contains 432 configurations with 30 saved warmups
and 300 measured changed frames each. The table shows 5K/1× submit p95 for each
independent run, and the largest run mean of draw-thread allocations. The slow
first original-GPU pan run and slow third candidate pan run remain included.

| Workload | Variant | Submit p95 runs 1 / 2 / 3 (ms) | Draw allocation mean KiB |
|---|---|---|---:|
| Pan | Original GPU | 22.964 / 16.759 / 15.716 | 457.0 |
| Pan | Commands | 12.918 / 14.292 / 12.505 | 173.5 |
| Pan | Mask batch | 13.748 / 14.227 / 14.670 | 173.7 |
| Pan | Sparse | 12.817 / 14.091 / 14.010 | 173.7 |
| Pan | Candidate | 12.857 / 13.704 / 20.183 | 173.7 |
| Zoom | Original GPU | 47.340 / 51.131 / 48.862 | 678.1 |
| Zoom | Commands | 46.866 / 49.221 / 49.259 | 243.7 |
| Zoom | Mask batch | 49.153 / 51.089 / 50.999 | 243.4 |
| Zoom | Sparse | 47.273 / 48.522 / 50.018 | 243.8 |
| Zoom | Candidate | 49.968 / 51.708 / 50.262 | 241.1 |

The candidate reduces this allocation metric by 62% for pan and 64% for zoom.
Across the six size/density cells, reductions are 62–84% for pan, 44–70% for
zoom, and 28–42% for broad dirty-world work. These are managed allocations on
the draw thread, not total managed/native allocation or process memory savings.

The command stream reduces foreground traversals from two to one. In the
single-road 5K/1× fixture, trusted sparse metadata reduces median ground
comparisons from 31,752 to one. Direct composition still draws the whole view.
Its candidate submit p95s are 17.704 / 18.109 / 17.698 ms, versus original GPU
20.229 / 19.165 / 18.561 ms; the command-only path is 18.606 / 18.838 / 19.069 ms.
The separate two-edit correctness fixture verifies spatially separate damage.

For 5K/1× zoom, mean submission is 28.6–29.8 ms for original GPU and 29.0–29.5 ms
for the candidate, while average graphics-engine busy time is 3.3–3.7 ms/frame.
Terrain submission p95 alone is approximately 28–31 ms. Command-only foreground
submission p95 falls from 14.4–15.1 to 12.6–12.9 ms, while atlas preparation
rises from about 0.02–0.04 to 0.74 ms. These independently measured phase
percentiles must not be added as a frame percentile. The remaining cost is
largely on the host submission path; these timers do not distinguish CPU
computation, scheduling delays and driver waits.

GPU completion remains separately recorded: the three 5K/1× zoom p95s are
49.874 / 53.369 / 51.604 ms original and 52.766 / 53.826 / 52.388 ms candidate.
Host-observed fence time and calibrated GPU completion can differ by the
recorded clock uncertainty and polling delay. Neither measures presentation.

Uploads barely change in this cell: roughly 111.5 to 111.1 KiB/frame for zoom,
11.5 to 11.4 KiB/frame for pan, and 1,056.2 KiB/frame for broad/local dirty
work on both paths. Peak tracked Skia cache usage for zoom falls from 378.2 to
370.7 MiB, while pan stays approximately 206 MiB. Those small cache changes do
not establish lower total GPU memory usage; the native residency observations
above show no such reduction.

### Corrected pipeline experiment

The final cohort contains 144 configurations (three runs, two densities, four
workloads, two GPU variants and three target counts), each with 30 warmups and
300 measured frames. All viewport targets are physically 5120×2880.

| Density | Workload | Variant | One target FPS | Two targets FPS | Three targets FPS |
|---:|---|---|---|---|---|
| 1× | Pan | Original GPU | 59.9–63.9 | 81.4–84.3 | 78.9–82.3 |
| 1× | Pan | Candidate | 62.8–69.4 | 80.1–86.6 | 83.1–87.8 |
| 1× | Zoom | Original GPU | 31.8–31.9 | 34.3–34.9 | 33.8–33.9 |
| 1× | Zoom | Candidate | 31.8–32.9 | 34.2–35.0 | 34.0–34.3 |
| 2× | Pan | Original GPU | 73.7–76.0 | 116.8–120.4 | 115.7–117.7 |
| 2× | Pan | Candidate | 76.8–81.7 | 114.1–117.7 | 115.2–118.0 |
| 2× | Zoom | Original GPU | 57.2–57.6 | 72.6–73.2 | 65.4–67.5 |
| 2× | Zoom | Candidate | 56.7–57.7 | 71.8–74.3 | 66.9–69.4 |

A second offscreen target overlaps useful work; a third provides no consistent
improvement. Each additional target adds 56.25 MiB. For original-GPU 1× pan,
tracked cache peaks rise from 205.8 to 262.1 to 318.3 MiB. One-target versus
two-target GPU-completion p95 ranges improve from 18.8–19.5 to 16.3–17.4 ms.

At 2× zoom the cache reaches its unchanged 512 MiB ceiling at all depths.
Original-GPU mean upload payload rises from about 7,223 to 7,787 to 8,562
KiB/frame, and texture creations rise from 27.64 to 29.86 to 31.07 per frame.
Candidate uploads likewise rise from about 7,215 to 7,630 to 8,541 KiB/frame.
This supports cache pressure as a contributor to the third target's slowdown;
it is not evidence that a larger budget is the best fix. Completion p95 also
increases in several runs despite higher throughput than depth one.

No deeper native input queue is integrated. The native host already submits
asynchronously, and the offscreen frame timer excludes waiting for target
admission. These throughput gains therefore do not predict application latency
improvements. Native input-to-image results are reported independently above.

### Retained changes and remaining gates

The ordered command stream, reusable batch arrays, white-mask batching and
trusted sparse metadata remain available as opt-in experiments. Their bounded
work/allocation improvements are demonstrated. The combined candidate has mixed
native timings and no consistent input-latency or total-GPU-memory win, so the
supported GPU flags and CPU default remain unchanged. Command-only native
latency has not been isolated by this cohort.

Stable atlas pages, composed pan regions, zoom tiers and topology-plan offload
remain separately reproducible experiments. Their failed visual or performance
gates are retained explicitly. No speculative GPU preparation, deeper native
input queue, or cache-budget increase is integrated.

The next useful work is to reduce the remaining terrain/host submission cost,
resolve deterministic region sampling before relying on retained pixels, and
preserve atlas batch locality while stabilizing placement. Larger caches alone
would not address the measured 1× zoom bottleneck. The 2× zoom results also show
that extra render targets compete with the texture working set.

Remaining validation gaps include native-window pixel equivalence for every
recorded frame (replay checks offscreen map pixels), zoom input-to-image latency,
GC-pause attribution, and process/driver residency across 100 warmed
replacements. Existing lifecycle checks bound application cache estimates and
verify pixels across replacements, not every memory layer. Other platforms
remain deferred. This is an optimization report, not full M2 acceptance.
