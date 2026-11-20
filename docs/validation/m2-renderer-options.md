# CPU and experimental GPU renderer integration

This integrates two startup choices into the existing desktop map. CPU remains
the default. GPU uses the measured `Atlas,Textures,Encoded,Direct` configuration;
the terrain-offload and retained-frame experiments remain research options.
The earlier [experiment report](m2-gpu-experiments.md) retains its historical
source identities and measurements. This integration does not complete M2.
Windows, macOS and browser testing is deferred at the user's request.

Subsequent [browser measurements](m2-browser-performance.md) add `Commands` to
the opt-in GPU configuration and verify native regressions. Browser startup
selectors now independently choose Software2D/WebGL and CPU/GPU composition.
The configuration and receipts below describe the original integration; their
hashes and measured results are retained unchanged.

## Configuration, fallback and ownership

`MapRendererOptions` validates immutable startup values. The desktop loads saved
preferences, applies CLI overrides and injects the result before background map
preparation begins. CPU is the missing-preference default. Malformed preferences
warn and fall back to defaults; malformed CLI values reject launch. The UI saves
an atomic replacement for the next launch and reports failed writes. It also
reports the active renderer and the GPU fallback reason.

The per-window rendering session falls back to CPU if the Skia GPU context is
missing/abandoned or a GPU-specific allocation fails. Fallback is sticky until
the user reloads textures. Resource reset runs inside the rendering lease;
context replacement invalidates cached images. Invalid content remains a visible
content error, not a successful capability fallback. Explicit benchmark mode
fails rather than silently measuring CPU. Normal launches ignore experimental
feature and timing environment switches.

Skia cache policy is owned per context, registered through a weak table. Shared
windows use the smallest requested ceiling; one window cannot repeatedly raise
another's budget. The default is 512 MiB, configurable from 128 to 1024 MiB.
This is an evictable-cache limit, **not a total process/VRAM limit**: live targets,
UI resources, CPU terrain, encoded sources and driver allocations are additional.
CPU mode leaves Avalonia's ordinary cache policy alone unless an explicit timing
experiment or a GPU window has registered a budget on that context.

Atlas page allocation now checks a hard 64 MiB estimated pixel ceiling before
admitting a page. Up to 16 deterministic layouts share that ceiling under an
LRU policy; revisiting a sprite set reuses its exact pages. Current-frame source admission has a separate 64 MiB pixel
estimate, with up to one previous frame retained during preparation. Pictures can
retain their source images; these estimates do not measure all native allocations.
Observer/world replacement, art replacement and recovery clear atlas pages and
sources. No application-owned GPU context or extra frame queue is introduced.

## Pixel acceptance declared for the integration run

Same-backend fresh-versus-retained comparisons require **zero differing pixels**,
including local edits, eviction, observer replacement and recovery. No tolerance
is used to hide cache-history differences. The atlas now sorts stable logical
sprite keys for the current frame, so page coordinates no longer depend on
cached image IDs or past viewport order. Unchanged and recently visited sprite sets reuse their pages.

CPU/GPU comparisons use exact semantic counts (tiles, objects, labels, effects).
For the experimental GPU backend, the integration run additionally requires a
mean absolute byte-channel error at most **2/255**, and at most **10% of pixels**
with any channel error above **8/255**, per image. Individual antialiasing and
resampling edge pixels may span the byte range. These are image-level regression
bounds informed by the pilot images, not a declaration that arbitrary semantic
errors within 10% of a frame are acceptable. Combined-scene, unit, effect and
color-correction images are visually reviewed alongside the semantic checks.
The pinned CPU regression images continue to require exact equality.

The pilot atlas repair passed 246/246 exact fresh/retained GPU cases. The pilot's
largest cross-backend mean error was 1.492/255; the largest fraction above 8/255
was 7.270%. These include native art at 2× and fractional zoom. Representative
Mixed/MV12/MV22/MV30 CPU/GPU sheets were inspected for object placement, missing
art, knowledge boundaries, effects and text. Their visible content agreed;
resampling and edge coverage differ. These are implementation regressions, not
original-game compatibility captures.

## Local validation

Evidence is collected under `build/m2-renderer-options/` using the existing pinned
assets, fonts, native/classic override fixtures and correctness drivers. The final signed Native AOT publication uses the pinned Microsoft compiler and
has no build warnings. Root and optional App formatting verification, project
boundaries, coverage inventory, Python syntax and diff whitespace checks pass.
The coverage inventory is metadata validation, not execution of core gameplay.
Existing staged work was preserved; this integration does not create a commit.

The final renderer checks pass:

- 213 CPU regression PNGs exactly match the existing reference images.
- 294 GPU fresh/retained comparisons are exact across stock, native and classic
  art at 1×/2×, with local edits, observer changes and recovery. Cross-backend
  tolerances and semantic counts pass. Maximum mean error remains 1.492/255;
  maximum fraction above 8/255 remains 7.270%.
- 20 actual CPU-to-GPU upload/readback cases remain exact.
- 680 option/lifecycle assertions pass, including 100 warmed replacements,
  returning to prior atlas layouts, eviction, two real EGL contexts, abandoned
  contexts, injected allocation failure and content-error separation. Repeated
  same-phase retained cache sizes do not grow; outgoing atlas ownership is empty
  on each observer replacement. This is not a physical device-reset test or a
  proof that driver RSS cannot retain allocations.
- Both final AOT renderer choices pass 26 native gallery input/lifecycle checks,
  including independent windows, resize, art changes, texture recovery,
  selection, effects, knowledge regions and continuous zoom. The final GPU AOT
  build also passes all 22 Riverbend input/command checks. The CPU version passed
  the same 22 before the GPU-only layout-cache optimization.
- Native settings tests cover save/restart, CLI precedence, a 256 MiB hardware
  cache override, malformed settings, invalid arguments and failed filesystem
  writes. The final hardware run passes 14 checks; the software-display run
  separately checks visible automatic CPU fallback and manual retry.

The X11 driver now waits for window placement and requests activation of an
obscured owned client before clicking it. It also recognizes GPU direct drawing
in the navigation test while retaining the CPU scroll-cache assertion. Hardware
reports identify their actual GLX backend. These changes repair harness
assumptions rather than weaken visible-content assertions.

## Targeted performance after integration

These are single-run regression diagnostics: 30 warmup changes and 120 measured
changes for each workload/resolution/density/renderer, using the same final AOT
binary and a 512 MiB Skia cache ceiling. Both paths drain earlier GPU work and
include completion of the measured draw. There are 3,600 measured changes across
30 series. Runs are sequential, clocks are not locked, and this is not the
three-run complete M2 acceptance protocol. Native input remains driven through
the existing external X11 harness.

The first deterministic-atlas version rebuilt pages whenever the visible sprite
set changed. Caching up to 16 such layouts within the same 64 MiB page budget
improved the 1× dirty-world p95 from 36.95 to 29.17 ms at 4K and from 37.65 to
29.39 ms at 5K. It preserves exact fresh/retained pixels. The first and final
integration diagnostics are retained separately, rather than overwritten.

Earlier 4K/5K experiments used 2× density. A 1× high-resolution viewport exposes
more of the world at the minimum logical zoom, so the two densities are separate
workloads. In particular, the much slower 1× zoom numbers below cannot be treated
as a regression against an earlier 2× series.

Draw p95 in milliseconds (CPU / GPU):

| Physical viewport | Density | Pan | Continuous zoom | World edits |
|---|---:|---:|---:|---:|
| 1920×1080 | 1× | 13.90 / 16.80 | 11.14 / 15.77 | 13.93 / 22.79 |
| 3840×2160 | 1× | 24.34 / 22.09 | 23.95 / 42.34 | 24.67 / 29.17 |
| 5120×2880 | 1× | 32.17 / 23.03 | 35.36 / 65.60 | 33.43 / 29.39 |
| 3840×2160 | 2× | 22.41 / 15.80 | 22.85 / 22.89 | 23.85 / 22.69 |
| 5120×2880 | 2× | 30.64 / 17.33 | 30.97 / 37.24 | 30.24 / 23.45 |

GPU composition benefits high-resolution panning and edits; CPU remains better
at 1080p and generally for zoom. Full M2 frame-time targets are not met by either
option across all workloads. This is why CPU remains the default and GPU is
explicitly experimental. Further batching, zoom and cache-pressure work remains
possible without adding more user-facing renderer variants.

No additional application frame queue was introduced. Input-to-polled-receipt
measurements include driver/polling overhead and do not measure physical display
latency. The earlier fixed-rate latency/pipeline experiments retain their stated
scope; this run does not re-establish physical input-to-photon latency.

At 1×, peak reported Skia cache usage was 283.4 MiB for CPU and 383.8 MiB for GPU. The worst series
input-to-polled-receipt p95 was 81.7 ms / 130.9 ms (CPU/GPU).

At 2×, peak reported Skia cache usage was 191.6 MiB for CPU and 512.0 MiB for GPU. The worst series
input-to-polled-receipt p95 was 82.5 ms / 81.9 ms (CPU/GPU).

These cache values exclude other process/driver allocations and are not an RSS
or total graphics-residency comparison. GPU still uses materially more graphics
memory. Raising the configurable ceiling is an explicit tradeoff; the integration
does not automatically select the earlier 768 MiB experiment on every device.

`build/m2-renderer-options/evidence.json` indexes final source-bound reports,
binary/driver hashes and the raw distributions. `run-final.sh` and
`run-density2.sh` record the executed native command cohorts. Windows, macOS,
browser, physical-display latency and complete M2 acceptance remain pending.
