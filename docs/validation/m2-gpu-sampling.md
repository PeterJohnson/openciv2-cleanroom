# GPU sampling failure investigation

The reproducible SwiftShader terrain failures are fixed by selecting source
texel centers explicitly and drawing each chunk over pixel-aligned physical
bounds. CPU composition remains the default. The earlier intermittent hardware
presentation and resource-replacement failures do not yet have a demonstrated
cause or repair; passing reruns do not close those gates.

## Isolation and repair

A coordinate-pattern image reproduces the failure without map content, scene
caching, tint masks or terrain preparation. For a 260-pixel image at scale
1.2499, ordinary nearest `DrawImage` differs from the CPU at 36,864 pixels;
scale 1.2501 is exact. The failing samples regularly select the preceding source
column or row. The same pattern occurs with a 520-pixel source and scale 2.4999.

Forcing generated GLSL from `mediump` to `highp` does not change the result.
An ordinary image shader does not repair it either. Explicit texel selection
alone still fails when drawing through fractional quad bounds. Combining integer
physical coverage with explicit source texel centers fixes the focused fractional
sampling regressions.

A separate raw WebGL probe reproduces the same 36,864 differences with only a
texture and two triangles, without Skia or Avalonia. Hardware WebGL is exact.
On SwiftShader, explicit centers derived from interpolated coordinates still
fail; centers derived from `gl_FragCoord` or pixel-aligned vertices are exact
at all three tested scales. This places the backend-dependent interpolation/
sampling interaction below Skia. It does not establish a WebGL specification
violation or identify an internal SwiftShader instruction. Both backends report
the same four subpixel bits and 23-bit high-float shader precision, so those
capability values alone do not distinguish their observed behavior.

`MapGpuNearest` now applies that combination to GPU terrain-chunk composition.
The original source transform and existing 0.001-texel tie bias remain; camera,
pick geometry, cache tiers and CPU rendering are unchanged. Adjacent chunks share
rounded physical edges. Rotation, skew and perspective retain the ordinary
image path; the map hosts use positive axis-aligned scaling and translation.
The shader effect belongs to the painter, has no retained context handle, and
is disposed with it. Per-draw images/shaders remain inside the current Skia lease.

The five previously failing candidate-profile cases now meet the unchanged
CPU bounds: mean absolute RGB channel error at most 2 and no more than 10% of
pixels with channel error above 8.

| Art | Zoom | Before mean RGB error | After mean RGB error |
|---|---:|---:|---:|
| Stock | 1.2499 | 3.750 | 0.482 |
| Stock | 2.4999 | 5.263 | 0.722 |
| Native | 1.2499 | 3.067 | 0.472 |
| Native | 2.4999 | 5.204 | 0.722 |
| Classic | 2.4999 | 2.278 | 0.176 |

These are authored renderer regression comparisons, not original-game pixel
compatibility claims. No exact-history or raster-error gate was relaxed.

## Checks and remaining failures

The shared focused probe records 40 primitive comparisons, enforces four exact
fractional-sampling regressions, checks 21 adjacent-chunk coverage cases, and
enforces CPU bounds for nine stock/native/classic map cases. Coverage includes
negative/fractional origins, half-pixel edges, clipping and 1×/1.5×/2× transforms.
Native hardware EGL, hardware WebGL and SwiftShader pass this probe.

Both native GPU profiles (217 and 249) pass 204 history cases and 100 warmed
replacements each. The supported profile also passes 246 MV matrix frames,
108 presentation frames and all 800 renderer integration checks; the terrain
profile passes 294 matrix frames and another 108 presentation frames. Hardware
WebGL and SwiftShader each pass all 204 history cases and 100 warmed replacements.
Their maximum mean CPU errors in that history suite are 0.847 and 0.925,
respectively. The focused SwiftShader checks also pass with the supported profile.
Three hardware WebGL native-art/2× gallery batches pass 183 functional checks and 114 exact
presented/fresh captures, including the previously failing MV28 sequence.
The earlier missing 4×4 patch does not recur in these batches.
The trimmed production browser host passes 41 input/renderer checks on each
backend, confirming the new runtime shader also works outside the untrimmed
acceptance host. Publication verifies notices for 23 locked packages and 33 files.

The unchanged hardware baseline passes all 204 navigation/history cases and
100 warmed replacements in this investigation. Its command exits nonzero only
because the live checkout changed during the run; its compiled identity was
subsequently verified against all 310 preserved baseline inputs. The original
exit status and separate source audit are retained.

Replacement diagnostics now preserve both raw readbacks, their pixel format,
an alpha-difference count and the existing actual/fresh PNGs and RGB metrics.
An opaque PNG alone cannot retain an alpha-only byte discrepancy. Strict byte
equality remains required; this change does not accept alpha differences.
The experimental stable-mask checks still report 11 SwiftShader and 13 hardware
failures, each one alpha level; native passes all 24. These remain separate
diagnostics for features disabled in the supported and terrain profiles.
Independent Pillow/NumPy comparisons reproduce the exact reported RGB metrics
for all 36 saved focused map pairs. No new workload-performance conclusion is
drawn from concurrently executed correctness checks.

## Reproduction and evidence

Build the optional App solution and publish/stage the browser acceptance host
using the [build guide](../BUILDING.md). Run:

```sh
OpenCiv2.MapProbe ASSETS FONTS OUTPUT --gpu-sampling-checks ART_FIXTURES
node tools/dotnet/run_map_browser.cjs BUNDLE OUTPUT \
  --renderer=gpu --gpu-sampling-only \
  --gpu-features=Atlas,Direct,Textures,Terrain,Encoded,Commands
node tools/dotnet/probe_webgl_sampling.cjs OUTPUT.json
```

Native Linux probes need the hardware EGL harness. Add `--hardware` for browser
hardware WebGL; omit it for SwiftShader. Sampling runs retain generated GLSL.
The test-only `--shader-highp` override is an isolation experiment, not a
production configuration or passing-validation requirement.

Local evidence is under `build/m2-gpu-investigation/`; `baseline-source/` and
`final-source/` preserve the compiled inputs for the baseline and full checks.
`reporting-source/` includes a subsequent test-only stale-receipt guard, verified
by another focused SwiftShader run. Production renderer inputs are identical
between those last two snapshots.
Prototype sampling modes and failed experiments remain separate from final
acceptance. The environment is Linux/Radeon 890M, Mesa 26.0.8, Chromium
151.0.7922.34, Playwright 1.58.2, .NET 11.0.100/runtime 11.0.0, Avalonia 12.1.6
and SkiaSharp 4.156.1. Virtual/headless checks do not establish physical scanout,
other-driver or cross-platform correctness. This investigation does not make
a new workload-performance claim.

The App build and browser publication pass without warnings or errors. Root,
App and browser formatting verification, identity admission tests, project graph,
coverage inventory and JavaScript syntax checks pass. The audit verifies compiled
inputs, browser-driver digests and captured-image digests against their preserved
receipts. Raw evidence and the standalone reproduction are retained locally.

The final audit covers 32 receipts and inventories 2,144 artifacts, including
failed and superseded experiments. SHA-256 digests, relative to the evidence root:

| Receipt | SHA-256 |
|---|---|
| `evidence.json` | `ec47d4f695b41605d2618086404dd63ad63448dc8002ab6e4dd7c65de8a8cd84` |
| `inventory.json` | `03c2368e963072d31130d592d8947eb24cf7a2d2ebaef56c32375c3da55db117` |
| `final-swiftshader-history/gpu-history.json` | `95dc79b6e8ea3705a741259b35fce789a84f74f70c6ff95ace89e9d6f902770c` |
| `final-hardware-history/gpu-history.json` | `61cf7593b78a66d44bfac0a02e7ba6b312c7a07bc1d86f2d8abc7ff8177f543c` |
| `final-webgl-interpolation.json` | `2454101378b49b84e3123d91e5305de7ae6a87ebf3f1c5885baa4d8b4964bbe0` |
