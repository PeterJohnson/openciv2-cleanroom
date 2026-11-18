# Performance budgets and measurement

Measure production C# workloads separately from UI rendering and process startup.
The [budgets](budgets.json) define fixed latency, allocation, memory and transfer
ceilings. Keep raw samples, warmups, result identities, hardware, toolchain and
artifact hashes. Do not raise budgets, omit slow samples or reduce output to
manufacture a pass. Investigate repeatable regressions against comparable C# runs.

The [M2 map interaction contract](map-rendering.md) adds separate complete-renderer
workloads and fixed frame/latency targets. It distinguishes unchanged repaint,
actual cached pan, continuous zoom, dirty world changes and replacement/recovery.
Use the [visual acceptance matrix](../testing/map-rendering.md) alongside it;
timing a scene with missing layers cannot establish renderer performance.

## Recorded reference results

The recorded studies predate the .NET 11 upgrade. Rerun their full sampling
procedures before claiming performance acceptance for the current build.

The Linux x64 Ryzen AI 9 HX 370 study used .NET 10.0.404/runtime 10.0.15 and
Chromium/Playwright. [Warmed workloads](workloads.md) and [application startup](application.md)
retain their exact C# measurement receipts. They describe the recorded revisions,
not a fresh measurement after changes to code, project layout or packaging.
macOS/Windows target-hardware performance remains unmeasured.

A large admitted save contains 31,752 tiles, 2,048 units and 256 cities. Warmed
native decode is about 2.67 ms; full inspection/JSON takes 106–109 ms and allocates
about 160 MiB. A complete diff takes 153–154 ms and allocates about 130 MiB.
Owned live unit/city projections take about 0.15–0.16 ms and allocate 185 KiB.
These full-file tool costs must not become per-frame UI work. Preserve output,
ownership and cancellation behavior when considering streaming or batching.

Recorded fresh-process CLI medians were approximately 18–21 ms for help, 88–91 ms
for a full save round trip, 303–322 ms for inspection, 141–153 ms for validation,
316–327 ms for diff, 85–92 ms for pack resolution and 149 ms for rule conversion.
SQLite export of a verified 115 MiB analysis database took about 1.5 seconds and
peaked below 465 MiB RSS. All absolute workload budgets passed. Native AOT,
pooling and data-layout changes are optional optimizations, justified by measured
workloads rather than a requirement to reproduce another language's startup cost.

The application study uses software rendering and localhost. It establishes
bounded startup/transfer/input behavior, not compositor FPS, full-map performance
or real-network latency. M2 must measure actual scoped model production plus
zoomed-out/half-world maps, visible object counts, full/cached/dirty redraws and
4K/5K physical resolutions. Use the map contract's changing-camera/world cases;
the Riverbend selection-only dirty timings do not cover them. Avoid per-tile
controls and whole-world frame copies.

## Reproduce

Build/restore the root, SQLite and performance solutions and prepare browser
prerequisites using [BUILDING](../BUILDING.md). Use a new output path per run:

```sh
dotnet restore solutions/OpenCiv2.Performance.slnx --locked-mode
dotnet build solutions/OpenCiv2.Performance.slnx -c Release --no-restore
python3 tools/dotnet/measure_cli.py --report build/test-results/cli-performance.json
python3 tools/dotnet/measure_warm.py --report build/test-results/core-performance.json
dotnet restore tests/OpenCiv2.PerformanceBrowser --locked-mode
dotnet publish tests/OpenCiv2.PerformanceBrowser -c Release --no-restore
python3 tools/dotnet/measure_warm.py --browser-bundle tests/OpenCiv2.PerformanceBrowser/bin/Release/net11.0-browser/publish/wwwroot --report build/test-results/core-browser-performance.json
```

CLI resource collection currently requires Linux GNU `time`. Browser collectors
use `NODE_PATH` and `OC2_CHROMIUM_EXECUTABLE` as described in the build guide.
`--dotnet PATH` selects an SDK executable outside `PATH`. `--smoke` checks the
collector with one sample and never grants performance acceptance. Application
startup commands and memory-bound semantics are in [the application study](application.md).
