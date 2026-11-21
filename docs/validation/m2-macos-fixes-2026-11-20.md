# Mac M2 blocker fixes and performance iterations — 2026-11-20

This follows the [initial Mac validation](m2-macos-2026-11-20.md), on the same
Apple M4 Mac mini, macOS 27.0, pinned .NET 11 and Chromium installation.
Conformance metadata and native initial focus are fixed. Two renderer iterations
reduce the measured workload, but **M2 performance still fails its budget**.
The changes were tested as an uncommitted patch over
`b332fe11ed32947a8bb16dd718ea1af0284ff909`. Local command receipts, source snapshots,
raw samples and images remain under `build/m2-macos/`.

## Conformance

Reviewed the P02 change in `228c95c`: MV14 makes greyscale the default for sleeping
units and places fortification above the body; MV16 makes flags depend on a
disclosed garrison and suppresses stationary unit bodies on city tiles while
preserving inspection and commands. These behaviors already have the existing
presentation and matrix regressions. The pre-change file hashes to the registry's
old value; the reviewed current file hashes to
`6c232ea022c8cf87de1791867ba4d7b1042fb734a649b9addca62d4b21c6641e`.

Updated only that specification hash. P02 remains `not-registered`, with no
registered operations or fixture changes; the inventory denominator and version
stay unchanged. `check_conformance.py` passes, and all 99 repository Python tests
pass. This repairs metadata integrity, without making a new gameplay-conformance
claim.

## Native dialog focus

`InteractionDialogView` now posts initial focus at dispatcher input priority,
after layout has arranged the new choices. The callback checks the interaction
identity, and the existing focus helper checks visibility, submission state and
whether focus is already inside the dialog. A stale callback cannot take focus
from another interaction or override an already selected choice.

Actual Mac input on the frozen source passed 20 console/menu/pointer checks and
nine map-input checks with each of CPU and GPU composition. Every run reported
zero initial-focus failures. Console Enter followed by Space completes once;
map Enter followed by Escape cancels without effects. No Tab assistance is used
to open these dialogs. The ordinary Tab-cycle test remains part of menu coverage.
All completed sessions exited normally through the native close button.

The published Native AOT executable also passed all 29 checks without a focus
failure, including fullscreen entry/restore with a pending dialog, native file
selection/export and persistence across restart. Its three sessions verified
292 compiled inputs and exited through the native close button. The separately
published production browser passed all 41 input checks and verified 285 compiled
inputs. The AOT dialog capture visibly shows the initial Confirm focus outline.

Early map retries passed the interaction assertions but rejected source identity
while renderer edits were in progress. The final `final2-native-*` runs rebuilt
those sources and passed identity validation; the earlier receipts are retained.

## Performance iteration 1: viewport damage detection

The original dirty-world workload compared all 31,752 tiles twice: once to
invalidate ground chunks and again to find viewport damage. Damage detection now
enumerates canonical tiles whose terrain joins or object overhangs can intersect
the viewport. Wrapped copies are deduplicated before the existing projection
loop. Label layout and path eligibility retain their separate checks. Ground
invalidation still covers offscreen cached chunks, and stale sparse metadata
falls back to the viewport scan. Route checks compare both whole-path eligibility
and segment eligibility, including knowledge outside the viewport.

The measured damage scan fell to 2,725 tiles. New shared regressions cover
flat/wrapped edges, three zooms, both densities, changed labels, disappearing
edge units and a subsequent pan into changed offscreen ground. The full native
matrix passed. All three timed endpoint images exactly matched fresh renders.

Final review identified that an offscreen knowledge gap can remove the visible
part of a long route. A new MV20/MV29 pixel regression failed against the first
implementation, then passed after restoring global segment-eligibility checks.
The complete native matrix also passed after this correction. The earlier
timing cohorts below are preserved as preliminary measurements; final timing
is repeated with this guard.

## Performance iteration 2: retain packed terrain sources

The 1080p browser dirty path rebuilt a packed terrain atlas on each update.
Settled browser views of at least two million physical pixels now reuse the
existing bounded atlas, already used for larger views and native band rendering.
The atlas remains capped at 4 MiB and included in the ground cache budget.
Camera changes, content replacement, observer replacement and resource recovery
keep their existing release paths.

New shared checks compare twelve successive stock/replacement-art updates
against a fresh unpacked renderer, assert the atlas/cache bounds, and verify
content/observer release. The native matrix passed with these checks.

An initial browser assertion incorrectly required atlas reuse after switching
to classic artwork. The diagnostic isolated step 11: four chunks rebuilt with
exact pixels, using the valid unpacked fallback for original unpremultiplied
replacement images. The corrected regression requires reuse for eligible stock
art and keeps exact-pixel/cache-budget assertions for both paths. The failed
receipts are retained in `fixed-wasm-matrix/` and `atlas-diagnostic-wasm/`.

## Timing and acceptance

Each cohort uses hardware WebGL with default CPU composition, physical
1920×1080 at 1×, the full-detail authored dirty-world workload, three independent
runs, 30 warmup frames and 300 measured frames per run. Other validation workloads
were stopped during timing. The workload, population, detail and target are
unchanged.

| Cohort | p95 by run (ms) | p99 by run (ms) |
| --- | --- | --- |
| Original | 72.855 / 72.360 / 72.385 | 74.010 / 73.455 / 73.345 |
| Iteration 1, preliminary | 56.095 / 56.405 / 58.465 | 56.800 / 57.310 / 62.055 |
| Iteration 2, preliminary | 54.360 / 54.855 / 55.500 | 55.415 / 55.650 / 56.085 |
| Final, including route guard | 54.890 / 54.580 / 55.235 | 55.700 / 55.565 / 55.795 |

The final p95 is approximately 24–25% lower than the original by corresponding
run. All nine optimized endpoint images match fresh rendering exactly. Every
cohort has identical measured cameras, density, physical extent, tile/object
counts and label counts across all 900 samples; see
`fixed-performance-comparison.json` and `final-summary.json`. Raw samples and source snapshots are in
`perf1-browser/`, `perf2-browser/`, `perf1-source/` and `perf2-source/`.
`final-source/` preserves the route-corrected implementation and revised
regressions; its published desktop and browser input checks passed again.
`final-browser-timing/` contains the final three runs and exact endpoint checks.

The second iteration removes some packing work, but rasterizing the rebuilt
terrain chunks still costs about 19 ms. The separate authored-mutation/projection
and binding boundaries together remain about 26 ms. These are remaining
profiling targets; the draw itself also exceeds the warm-frame budget.

The acceptance target remains p95 ≤ 16.7 ms and p99 ≤ 33.3 ms. The timer includes
renderer work through Skia lease disposal plus authored mutation, projection and
binding; lease acquisition and other host work are excluded. Endpoint comparisons
are sampled after timing, not every timed frame. Physical scanout, native timing,
other resolutions/workloads and the remaining platform gates are separate.

## Verification

- Conformance inventory and all 99 repository Python tests passed.
- Root and optional App formatting, followed by verification, passed.
- App Release build and production browser/Native AOT publication passed.
  AOT retained the dependency debug-symbol warnings from the original run.
- Native core tests: 485 passed, zero failed, one device-audio test explicitly skipped.
- Each performance iteration passed the full native 31-board map matrix with
  317 explicitly asserted sprite identities, including the new shared regressions.
- The final WebAssembly matrix passed all 31 boards and shared regressions,
  preserving 291 evidence files and verifying 310 compiled inputs. Both the
  offscreen-route and stock/classic atlas groups passed in the browser runtime.
- Managed CPU/GPU interaction: 20 shell checks and nine map checks per renderer;
  AOT: 29 checks; browser: 41 checks. All final input receipts passed.
- All three benchmark endpoints per cohort matched fresh pixels exactly.
  All timing reports verified 310 compiled inputs against their source cohort.

The renderer changes preserve CPU composition as the default, existing cache
limits, authored workload detail and gameplay state. The original validation
record and its manifest remain unchanged. Existing untracked stack-comparison
work was preserved; no commit or staging was performed during validation.

## Evidence integrity

`build/m2-macos/fixed-evidence-manifest.json` records 64 command receipts and
SHA-256 digests for 1,864 evidence files, including failed diagnostic runs.
The final audit checked source hashes, all 900 workload samples, native focus
results, normal window closure, WebAssembly completion and endpoint pixels.
The final summary explicitly records `performanceTargetMet: false`.

Paths below are relative to `build/m2-macos/`.

| Evidence | SHA-256 |
| --- | --- |
| `fixed-evidence-manifest.json` | `1cc421e32e662e8c91a7b5aaf2ec61ee27f3d298e4fb402d958d517b78ad370d` |
| `final-browser-timing/performance.json` | `e29e49bdc82098759da350c10b035f951007603f5e08605dc1f85b67926b78c1` |
| `final-wasm-matrix/matrix.json` | `c2aa743ae0b7c8709ebfe08f0bf578abbca32f42fe60a40a7b18e9a0c2ac16e6` |
| `native-input-aot-cpu-verified/report.json` | `780955842f09fb7d8769fa53f6b09f4b97b6c687a3061efc253f033cc7e274a9` |
| `final-application-browser.json` | `cc3cedf654a82173c631b0ce9d209c4d1840d3925b411d847af09fd70a8b7d00` |
