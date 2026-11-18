# Production application startup measurements

The measured Avalonia desktop and browser seam passed its fixed startup,
transfer and memory budgets on the Linux reference machine. The
[receipt](application.json) preserves 54 verified launches/navigations:
three warmups and fifteen measured samples for each of desktop, cold browser and
cached browser. Every sample verifies actual pointer input after readiness,
completes the real stack command, and observes exactly one effect and a changed
result. This is the existing production host, not the portable benchmark UI.

| Population | Median readiness | p95 / maximum readiness | Transfer / memory |
| --- | ---: | ---: | --- |
| Linux desktop, new process | 867 ms | 1,015 ms | Idle RSS and startup high-water ≤186.1 MiB |
| Browser, empty context cache | 830 ms | 945 ms | Network encoded bytes ≤4.37 MiB; response bodies 4.35 MiB |
| Browser, retained context cache | 785 ms | 873 ms | No new network/body bytes |

The desktop ceilings are 2,000 ms readiness and 512 MiB idle RSS. Browser ceilings
are 8,000 ms cold readiness, 3,000 ms cached readiness, 25 MiB transferred data
and 768 MiB managed heap. The observed application WASM memory capacity is
66.5 MiB, including native runtime/graphics allocations as well as the managed
heap. Every measured sample meets the limits; there is no averaging away a failed
launch. With fifteen samples, nearest-rank p95 is the maximum.

## What was measured

The measured Release artifacts used SDK 10.0.404/runtime 10.0.15 and Avalonia 12.1.6.
These measurements predate the .NET 11 upgrade and its graphics packages;
rerun the full procedure below for current-build performance acceptance.
The browser is Chromium 151.0.7922.34 with Playwright 1.58.2. The machine is the
same Linux x64 Ryzen AI 9 HX 370 reference used by the CLI and warmed workloads.
Compiled identities, artifact hashes, source manifests, host details, load and
raw distributions accompany the reports.

Browser readiness is measured from navigation time origin until the real scoped
model has its three owned units, an enabled positive-size command control, and
another animation-frame opportunity after layout. The checked ready screenshot
shows the rendered UI, and real pointer input then confirms that it functions.
Each cold sample uses a new isolated browser context with empty HTTP/IndexedDB
caches. The warm navigation returns to the same URL in that context after visiting
`about:blank`, retaining cache contents but constructing a fresh application.
OS file caches and browser-engine caches are not controlled.

The local server serves the actual published `.br` files with content encoding
and a one-hour cache lifetime. Cold navigation requests 56 files. Both server
body-byte accounting and Chromium's encoded network-byte accounting are retained;
all fifteen cached navigations request no files from the server. The logical
uncompressed publication is 25.92 MiB, including files not necessarily needed at
startup. Its size is reported separately from transfer. This localhost experiment
has no bandwidth/latency throttling and is not an internet-speed prediction.
Deployment needs appropriate compression/cache headers to reproduce these transfers.

Desktop timing starts before launching the apphost and ends at the first valid
read-only observation of the model/control layout. A real owned X11 window must
exist, and subsequent input must complete the command. After one second of
settling, ten RSS samples are read at 100 ms intervals from that process's
`/proc/<pid>/status`; the process's `VmHWM` supplies startup high-water. This avoids
inherited Python launcher RSS. The observation timer remains enabled during idle
sampling. The app closes through its window protocol and must exit successfully.

Both graphics runs use software rendering: isolated Xvfb for desktop and
SwiftShader for headless Chromium. The desktop environment logs unavailable AMD
acceleration before falling back; the window/input checks pass. These results
make no display-FPS, native-compositor, macOS, Windows or mixed-DPI claim. Existing
1×/2× browser input evidence are distinct from future 4K/5K map-rendering acceptance.
One desktop attempt terminated before its final report after 14 completed samples;
its log is retained, but no samples from it are included in the accepted population.

## Peak WASM memory bound

`observe_wasm_memory.cjs` observes standard WebAssembly construction/instantiation
and retains the actual exported/import-created memory objects. Their allocated
capacity cannot shrink, so capacity after a run bounds its managed heap at all
earlier points, including between managed sample counters. It also includes native
allocations and is deliberately not labeled browser RSS. No private .NET heap
layout, forced collection or engine state access is used. The observer's own test
covers constructor, synchronous/async/streaming module exports, in-WASM growth
and duplicate accounting.

The same observer was applied to a second full portable workload run. All operation
budgets and result identities still pass. Capacity reaches **531.7 MiB** across
large inspection/diff and the remaining workloads, below the 768 MiB ceiling.
This closes the earlier limitation of post-call managed heap estimates for that
run. The [warmed receipt](warm.json) preserves both browser populations
and both native populations (976 calls including warmups); the earlier observations
are retained. Browser GC pause duration remains unavailable and is still `null`.

## Reproduction and remaining work

Build the current Release desktop and publish the browser host, then run:

```sh
NODE_PATH=/path/to/pinned-playwright/node_modules \
OC2_CHROMIUM_EXECUTABLE=/path/to/pinned-chromium \
python3 tools/dotnet/measure_application.py --node /path/to/node \
  --browser-bundle src/OpenCiv2.Browser/bin/Release/net11.0-browser/publish/wwwroot \
  --report build/test-results/performance-application-browser.json
DOTNET_ROOT=/path/to/pinned-dotnet \
python3 tools/dotnet/measure_application.py --xvfb /path/to/Xvfb \
  --desktop src/OpenCiv2.Desktop/bin/Release/net11.0/OpenCiv2.Desktop \
  --report build/test-results/performance-application-native.json
python3 tools/dotnet/test_application_performance.py \
  --report build/test-results/performance-application-browser.json
python3 tools/dotnet/test_application_performance.py \
  --report build/test-results/performance-application-native.json
node tools/dotnet/test_wasm_memory.cjs
```

`--smoke` performs three warmups and one measured launch per population and never
grants acceptance. Five evidence-validation groups pass against each full report;
they reject missing samples, stale compiled identities, empty/nonfunctional UI,
invalid timings, absent resource measurements and ineffective cache reuse. Slow
samples remain budget failures. Full reports/build logs are under
`build/test-results/`; the compact receipt binds them by hash.

Packaging and functional validation are recorded separately in [validation](../validation/README.md).
These measurements do not close native Mac/Windows hardware or full M2
window/menu/accessibility gates.
