# Browser zoom stall investigation

The preceding browser result is committed as `0fdfc5b`. This follow-up isolates
the multi-second zoom stalls and enables bounded, single-thread terrain batching
in WASM. It reduces the screened stalls substantially while preserving exact
pixels. It does not close the M2 performance gate.

The subsequent [GPU default validation](m2-browser-gpu-default.md) expands the
terrain-offload checks and identifies browser promotion blockers. This report
preserves the earlier tested configuration and results.

## Cause

The [previous study](m2-browser-performance.md) retained a 2.83-second 5K frame:
2.77 seconds were ground construction, including 83 ms of terrain selection;
foreground work was 27 ms. The longer diagnostic below exposes a 6.96-second
frame when zoom returns across a backing-resolution boundary. This is work
inside the production map draw, also visible in the browser callback timing.

Three separate transitions explain the costs at 5120×2880 / 2×:

- Zoom below 0.625 changes the ground backing tier from 2 to 1. The sampled
  frame at zoom 0.6165 constructs 170 newly required chunks.
- Zoom below 0.55 changes full detail to reduced detail. The sampled frame at
  zoom 0.5384 constructs 220 chunks with different visible terrain layers.
- Returning above 0.625 needs the larger backing again. The sampled frame at
  zoom 0.6482 constructs 144 chunks; the older tier was evicted while other
  detail/tier combinations occupied the bounded cache.

`GroundKey` includes position, raster tier, detail and bare-map state. The browser
ground cache remains capped at 240 MiB. At the worst frame, ground construction
accounts for 6.88 of 6.96 seconds; selection accounts for only 70 ms of that work.
The 144 larger backings represent about 148 MiB before overlapping layer work.
Both CPU and GPU composition use this synchronous CPU terrain preparation path.
The desktop's batching was previously coupled to worker availability, so WASM
issued separate Skia calls for each ordered texture group.

These counters identify terrain construction as the dominant stage, including
CPU rasterization, allocation, submission and snapshot work. They do not isolate
each Skia internal cost. Collections occurred, and the browser GC pause API still
does not provide trustworthy pause evidence. No claim relies on zero GC pauses.

## Bounded batching

`DrawGround` now permits its existing packing path on the single-thread browser.
Planning, source preparation and rasterization execute serially there. Native
worker scheduling is unchanged. The same layer/row/piece order, cropping and
gutter rules feed one atlas submission per eligible chunk. Unsupported source
formats or a failed packing attempt use the existing individual-image path.

The existing limits remain: at most 24 chunks, 8 MiB of chunk scratch per batch,
and a 1024² packed atlas. The ground-cache ceiling, zoom/detail thresholds and
visible populations are unchanged. The fresh reference disables packing, so
comparisons exercise an independent submission path. Recovery tests distinguish
packing from worker use and still require disposal after allocation failure.

## Initial paired 5K screen

Same machine, pinned content/toolchain and timing boundaries as the previous
study: Radeon 890M, hardware Chromium 151.0.7922.34, Release WASM. Runs are
sequential, with automatic clocks. Each configuration uses actual Shift-drag
input, five saved warmups and fifteen measured changes. This short population
is diagnostic: its p95 is its maximum. The first transition is a warmup, and is
reported separately rather than silently moving it into the measured population.

| Transition | Original frame ms | Packed frame ms | Original / packed ground batches |
|---|---:|---:|---:|
| Backing 2 → 1, warmup | 2,368.7 | 1,457.1 | 33,589 / 520 |
| Full → reduced detail | 2,719.7 | 1,719.3 | 42,815 / 220 |
| Backing 1 → 2, measured maximum | 6,964.5 | 3,942.4 | 29,282 / 144 |

The maximum falls about 43%, and measured median changes from 25.90 to 25.14 ms.
All 20 paired camera/geometry/population inputs match. The endpoint is identical
to the original renderer, a fresh unpacked frame and an actual texture reload.
The benefit comes with more managed allocation: the three transition draws rise
from 7.23/8.97/6.13 MB to 9.31/11.61/8.06 MB. The cache occupancy is identical at
those frames; a larger resident cache is not responsible for the improvement.

## GPU terrain experiment

The existing `Terrain` experiment reduces the three frames above to
328.8 / 266.0 / 189.5 ms. Adding `GroundPlans` gives
270.2 / 265.0 / 191.5 ms; the plan cache helps the first transition but does not
remove detail-specific planning. These are separate short diagnostic runs.

Both experimental endpoints match their own fresh render and actual host reload,
but differ from the original raster output, with maximum channel differences of
37. These endpoints remain inside the declared CPU/GPU raster bounds. A current
native `Terrain` rerun also passes all 294 exact retained/fresh comparisons and
the existing CPU/GPU raster bounds, plus 108 exact presentation frames. The
[earlier offload failures](m2-gpu-optimization.md) do not reproduce in this suite;
they must not be presented as a current failed result. This is new native
evidence, not the complete browser art/navigation-history matrix for `Terrain`.
Neither experiment is promoted. GPU terrain preparation is a strong next
candidate, with broader browser validation and platform-specific performance
policy still needed. The previous native timing study found terrain offload
slower for several workloads, so browser gains do not justify enabling it on
every platform.

Browser callback CPU intervals for the three terrain-offload transitions are
374 / 318 / 231 ms, so the improvement also appears outside the map-only timer.
Available GPU query intervals are about 12–17 ms; they do not measure physical
presentation or prove that all asynchronous work completed within that interval.

## Final verification and remaining work

Three final independent 5K WebGL/GPU processes repeat the same 5+15 zoom screen.
Their measured maxima are 3,677.46 / 3,705.64 / 3,667.15 ms, with medians
25.56 / 24.52 / 24.07 ms. All three endpoint/unpacked-fresh and actual texture
reload comparisons are exact. These repeats establish consistency of this
candidate screen, not a three-run complete workload acceptance result.

Both CPU browser backends also pass matched-input checks against the previous
study. The 4K Software2D baseline is `final-software-4k-reference`, with texture
reloads between cases. The 5K WebGL/CPU baseline is
`baseline-webgl-cpu-5120x2880`, without those reloads. Each final comparison
preserves its baseline's sequence. Entries below use five warmups and ten
measurements per case; the zoom p95 is therefore the maximum.

| Backend / viewport | Original zoom max ms | Packed zoom max ms | Original / packed pan median ms |
|---|---:|---:|---:|
| Software2D/CPU, 3840×2160 / 1× | 1,862.39 | 1,057.25 | 77.90 / 77.48 |
| WebGL/CPU, 5120×2880 / 2× | 2,878.32 | 1,643.98 | 111.04 / 121.54 |

The Software2D run covers all seven pan/zoom/world-update cases: 105 paired
warmup/measured inputs and seven endpoint pairs match exactly. All seven also
match unpacked fresh rendering and actual host texture reloads. Full-detail
dirty-world median improves from 130.29 to 104.60 ms in this short screen.
Pan tails are mixed; the WebGL/CPU pan median is worse. No general warm-frame
speedup is claimed, and clocks remain unlocked.

The first final WebGL/CPU cohort overlapped a screenshot-comparison audit and
is excluded from comparative timing conclusions. Its seven fresh endpoint
checks still pass. `final-webgl-cpu-5k-isolated` repeats full-detail pan/zoom
without competing work: all 30 paired inputs and both endpoint pairs match the
baseline and unpacked fresh frames exactly. The earlier cohort is preserved.

The complete native and WASM matrices pass: each renders all 31 boards and
asserts 317 sprite identities, including stock/native/classic art, exact fresh
comparisons, detail/backing boundaries, cache resizing and injected allocation
failure/recovery. The WASM run retains all 291 evidence files. Native worker
checks still run natively; browser packing does not claim worker execution.

The hardware GPU gallery passes 68 checks and all 39 presented/unpacked-fresh
comparisons. It covers actual input, independent maps, texture/context recovery
and invalid content. The separately trimmed production browser passes all 16
input checks, bound to 268 compiled inputs and 597 published artifacts. Native
build, both browser publications, root/App/browser formatting verification,
project boundaries, coverage metadata and identity tests pass.

Multi-second CPU terrain construction remains after batching. Further work
should separate terrain construction costs, improve browser raster execution,
or complete browser validation of the GPU terrain path. Cache changes must
account for the visible working set without hiding cost behind a larger memory
allowance.
Asynchronous preparation would also need explicit stale-generation and first
correct-frame behavior; drawing stale or less detailed terrain is not an accepted
substitute. The original performance contract is unchanged.

## Reproduction and evidence

Receipts are under `build/m2-browser-stalls/`. `screen.sh` preserves the original
GPU/terrain experiments; `final-timing.sh` and `isolated-repeat.sh` preserve the
final measurements. Every cohort has its driver hash, compiled identities, raw warmups/samples,
fresh/host screenshots and pixel results. `baseline-source/` and
`packed-screen-source/` retain the exact 288 compiled inputs for the first screen.
`final-source/` preserves the final inputs. `evidence.json` admits 13 report
identities, separately validates the two older CPU comparison baselines, records
the timing exclusion and hashes 1,814 local artifacts, both browser bundles and
the reporting tools. `summary.json` retains all cohorts, including the excluded
one; it is not an acceptance pass.

| Receipt under the artifact root | SHA-256 |
|---|---|
| `evidence.json` | `c2170ae38de4e1dc2806226150560047107167b68d0274337dd152b699b49615` |
| `summary.json` | `3e6f6911c809d95f26ca0305b4396f2222b33f8d6f2042fcd5d0c6bfe9d82ddb` |
| `packed-comparison.json` | `fff41e16c389bb09047e589f60622c2eb170a0d5266eaacb8ab7a6903521c985` |
| `software-4k-comparison.json` | `ab6927f6961dfbddefd69585c18c2a328fe7402b9c34b82d01a84d2cd7e11cd5` |
| `webgl-cpu-5k-comparison.json` | `a85819b77fb95d843c7b13d514a978716efe6daadd70cfb5b3c1e82f719ef146` |
| `native-terrain/gpu-matrix.json` | `3beb485de37023323e2a78c9af399eeb292429de2b330e1a97f4835c2d855d44` |

```sh
node tools/dotnet/measure_map_browser.cjs BUNDLE OUTPUT \
  --backend=webgl --renderer=gpu --size=5120x2880 --density=2 \
  --details=full --modes=zoom --runs=1 --warmup=5 --samples=15 --recover-check
python3 tools/dotnet/check_map_browser_performance.py OUTPUT
```

The experimental feature strings are
`Atlas,Direct,Textures,Encoded,Commands,Terrain` and the same list plus
`GroundPlans`. These switches belong to the test host. The production default
remains CPU composition; the supported GPU feature set is unchanged.
