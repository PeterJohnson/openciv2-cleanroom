# Browser GPU default validation

The [sampling follow-up](m2-gpu-sampling.md) repairs the reproducible SwiftShader
terrain failures below. This page retains the original promotion attempt and
its evidence; the intermittent hardware failures remain unresolved.

**Promotion is blocked; CPU remains the default in browser and desktop.**
The broader native checks pass, but browser validation found reproducible
SwiftShader sampling differences, a hardware presentation mismatch and an
unresolved replacement failure. The final hardware history rerun passes.
The trial browser default was reverted. The existing opt-in GPU profile is
unchanged; terrain offload remains an experiment.

The candidate is `Atlas, Direct, Textures, Terrain, Encoded, Commands` (249).
The existing GPU profile is `Atlas, Direct, Textures, Encoded, Commands` (217).
Stable atlas placement, shared masks, retained regions and ground-plan experiments
remain disabled. No raster tolerance or exact-history requirement was relaxed.

## Executed coverage

| Configuration | Result |
|---|---|
| Native GPU profile 217 | 246 matrix frames and 108 presentation frames pass exact retained/fresh equality and CPU raster bounds |
| Native terrain candidate 249 | 294 matrix frames and 108 presentation frames pass the same gates |
| Native navigation/history, each profile | 204 cases pass; 100 warmed observer/density/resource replacements pass |
| Native renderer integration | 800 checks, including fallback/retry/context replacement and 100 warmed replacements, pass |
| Full native and WASM CPU matrices | Each renders 31 boards and checks 317 sprite identities; WASM retains 291 evidence files |
| Hardware browser candidate gallery | All six art/density batches complete: 373 functional checks, 229 captures; 228 captures match fresh GPU pixels exactly |
| Hardware browser candidate history | Initial replacement check fails after 204 passing navigation cases; final full rerun passes all 204 cases and 100 warmed replacements |
| SwiftShader candidate history | All 204 retained/fresh comparisons are exact; five CPU-bound failures; 100 warmed replacements pass |
| Trimmed candidate production host, hardware | All 21 input and renderer-configuration checks pass |
| SwiftShader candidate smoke | 30 functional checks and 14 exact presented/fresh captures pass |
| Restored production defaults | 21 CPU-default and 21 explicit-GPU input/configuration checks pass; 14 CPU-default smoke captures are exact |

The history suite covers stock/native/classic art at 1×/2×: fractional and
integer pans, diagonals, seams, detail/backing transitions and reversals,
separated edits, stale deltas, eight recolors, effects, recovery, art/observer
changes and flat-map edges. It borrows the native EGL context or the browser
control's live Skia lease and never retains it beyond that lease.

Each case requires exact retained/fresh GPU RGB pixels and matching CPU/GPU
semantic counts. The existing CPU/GPU raster bounds remain: mean absolute RGB
channel error at most 2, and at most 10% of pixels with a channel error above 8.
The native history suites' maximum mean errors are 0.671 for profile 217 and
0.868 for profile 249. GPU and CPU rasterization are not required to be identical.

Replacement checks alternate observers, density and viewport size, with periodic
recovery, ten warmups and 100 measured replacements. They check byte equality,
outgoing-cache release, bounded atlas/ground allocations and stable resource
sizes for each repeated phase. This is not a claim that all browser/driver
allocations stay constant indefinitely.

## Browser blockers

**SwiftShader nearest-sampled terrain.** At 1× density, zooms just below backing
tier boundaries exceed the unchanged CPU bounds:

| Art | Zoom | Mean RGB error | Pixels with channel error > 8 |
|---|---:|---:|---:|
| Stock | 1.2499 | 3.750 | 67,933 / 518,400 |
| Stock | 2.4999 | 5.263 | 97,161 / 518,400 |
| Native | 1.2499 | 3.067 | 52,199 / 518,400 |
| Native | 2.4999 | 5.204 | 95,856 / 518,400 |
| Classic | 2.4999 | 2.278 | 14,554 / 518,400 |

The isolated stock/2.4999 reproduction is exact against fresh GPU rendering,
but differs from CPU mainly in periodic terrain sampling. Candidate versus the
older GPU profile differs by only 0.100 mean RGB levels, so the failure is not
specific to terrain offload. Removing the existing nearest-neighbor source bias
changes the CPU error from 5.263 to 5.069 and still fails. That experiment was
reverted; no sampling change ships from this investigation.

**Hardware presentation history.** `native-2x/MV28` differs at 16 pixels in a
4×4 rectangle, map-relative physical bounds `(1424, 4)–(1428, 8)`. The presented
pixels are the host background `(237,233,223)` rather than the fresh map background
`(23,43,51)`; maximum channel error is 214. An audit of all 229 captures finds
one failed comparison. The checker excludes only the separately drawn overview,
whose footprint has its own assertions; this patch is not excluded.

Two separate native-art 2× batches, disabling encoded sources or sprite batching,
each pass all 38 exact captures. This is useful isolation evidence, but neither
run establishes the cause of an intermittent failure or validates a replacement
profile across the full matrix.

**Hardware resource history.** The initial full GPU history run completes all
204 navigation checks, then fails exact equality during repeated observer/density
replacement. Standalone 100-replacement runs pass for the unchanged candidate,
without encoded sources, and without sprite batching. The failure is therefore
not reproduced by replacement alone. The original aborted receipt is retained;
its navigation log is not relabeled as a complete passing history report.
The test now saves failing actual/fresh images, cycle number, both draw errors
and RGB differences before propagating a replacement failure. This distinguishes
visible differences from an opaque readback alpha discrepancy in future runs.
The final full rerun passes all 204 navigation cases and all 100 warmed
replacements, with maximum mean CPU error 0.858. No production rendering fix was
applied between these runs, and the original failure's cause remains unknown.

The auxiliary mask microtest is separate: it forces stable atlas placement,
which is not enabled in either profile. SwiftShader fails 11 of its 24 strict
checks; the final hardware run fails 13. Each failure is one alpha level;
native passes all 24. Browser diagnostics
retain those failures, while native enforcement stays unchanged. No stable-mask
experiment is promoted.

## Tooling and evidence

`GpuPixelChecks` shares native readback and pixel metrics. The WASM test host
executes bulk comparisons in JavaScript, cross-checked against the original C#
arithmetic for identical data, alpha-only changes and varied RGB differences.
Independent Pillow comparisons of four captured PNG pairs also reproduce every
reported difference metric exactly, including the failing terrain case.
This keeps bulk comparison arithmetic out of the managed WASM loop; an earlier
incomplete managed-comparator run is retained separately, not counted as a pass.
The production renderer does not reference this helper.

The GPU gallery uses pinned graphics and all twelve DejaVu faces. These are
authored regression checks, not original-game pixel compatibility evidence.
Functional suites run in separate processes where useful; their elapsed times
are not performance measurements. No new comparative performance conclusion is
made while correctness gates fail. The earlier
[stall investigation](m2-browser-stalls.md) remains the performance evidence.

Local execution uses Linux, Radeon 890M / Ryzen AI 9 HX 370, Mesa 26.0.8,
Chromium 151.0.7922.34, Playwright 1.58.2 and Release .NET 11.0.100 / runtime
11.0.0, Avalonia 12.1.6 and SkiaSharp 4.156.1. Hardware browser runs require
ANGLE/OpenGL and reject SwiftShader/llvmpipe. Headless and virtual-display checks
do not establish physical scanout behavior. Windows/macOS, Safari/Firefox and
other GPU drivers retain separate coverage gates.

Raw receipts are under `build/m2-browser-gpu-default/`. `candidate-source/`
preserves 307 compiled inputs for the trial default; `candidate-bundles.json`
records its publication digests. Its reports that say `rendererChoice=default`
refer to that frozen candidate, not the final checkout's restored CPU default.
The initial mask failure, incomplete slow run, isolated sampling/replacement
reproductions and feature-isolation batches remain distinct evidence.
`receipt-setup-source/` preserves the restored-default smoke build before a
test-only missing-file guard was fixed; `final-source/` preserves the final
compiled inputs. The evidence audit checks each receipt against its own source
snapshot and each retained browser driver against its recorded digest.

Root, application and browser formatting verification passes. The application
build and both browser publications succeed without warnings or errors; identity,
project-graph and coverage checks pass. The production browser publication
contains verified notices for 23 locked packages and 33 files.

The final audit verifies 32 reports against their compiled inputs and inventories
3,801 preserved artifacts. Failed or incomplete runs remain explicitly labeled.
Digests below are SHA-256, with paths relative to the evidence directory:

| Receipt | SHA-256 |
|---|---|
| `evidence.json` | `1ac6d4b6a8bcaf8ca5bf0074326cdb74455d6b6e5b9019038e8ed3075b351806` |
| `gallery/all-pixel-comparisons.json` | `ab239686b8b59575836d999d67c996bfee08dfb348645dd93988785d83438622` |
| `swiftshader-history/gpu-history.json` | `f7c82572662f4ca40732b5ef44c06d51adcced5cc746c5d8ae21a133306605f4` |
| `final-hardware-history/gpu-history.json` | `4f78b14dc1e1d60c242f27cf5c76f7fc5173feb34a97d468e3a3c1be3ccb5067` |
| `final-native-history/optimization-checks.json` | `f6b73e25beb7d4d00a5d2f7f6106dafd817b76c16f3852bec1fe94094ea7b328` |

## Reproduction

Publish and stage the browser acceptance host as described in the build guide.
Use explicit experimental features to reproduce the terrain candidate:

```sh
node tools/dotnet/run_map_browser.cjs BUNDLE OUTPUT \
  --hardware --renderer=gpu --gpu-history-only \
  --gpu-features=Atlas,Direct,Textures,Terrain,Encoded,Commands
node tools/dotnet/run_map_browser.cjs BUNDLE OUTPUT \
  --hardware --renderer=gpu --gallery-only --theme=native --density=2 \
  --gpu-features=Atlas,Direct,Textures,Terrain,Encoded,Commands
python3 tools/dotnet/check_browser_map_pixels.py OUTPUT
```

Repeat all three art sets at both densities for a complete gallery; omit
`--hardware` for SwiftShader. Native equivalents are
`MapProbe --gpu-optimization-checks ART`, `--gpu-matrix ART`,
`--gpu-experiment-matrix ART` with the candidate feature string, and
`--renderer-options` under the hardware EGL harness. Browser production defaults
and explicit opt-in are checked by `run_application_browser.cjs`, optionally
with `--renderer=gpu` and `--hardware`.
