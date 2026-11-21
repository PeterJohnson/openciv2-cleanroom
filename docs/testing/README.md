# Tests and evidence

Run from the repository root after the [build prerequisites](../BUILDING.md).
Shared behavior lives in `tests/OpenCiv2.FoundationCases`, executed by native xUnit
and the browser host. Native I/O/API boundary tests are in `OpenCiv2.Base.Tests`.
Optional Lua/SQLite and the X02 adapter have separate test hosts. See
[implementation coverage](coverage.md) for admitted APIs and exclusions.

## Complete M2 map renderer

Follow the [map-rendering acceptance matrix](map-rendering.md) for all P02
MV01–MV30 rows, [interactive workloads/budgets](../performance/map-rendering.md)
for performance, and the [visual ledger](../validation/map-visual-coverage.md)
for separate native, browser and live-provider status. The optional Linux
application CI step verifies the pinned archives, runs the full software raster
matrix, exercises the desktop gallery and scoped Riverbend controls, and checks
the first draw after texture recovery against a fresh frame. The 1× gallery run
captures every board. The 2× run repeats input and lifecycle checks. Full
performance trials and physical presentation measurements use the separate
performance protocol.

The optional `OpenCiv2.MapBrowser` host executes the same exhaustive matrix in
WASM and the production Avalonia gallery in WebGL2. Its driver captures every
board with stock, native and classic art at 1× and 2×, exercises actual input and
content/resource failures, and compares presented pixels with fresh WebGL frames.
The overview footprint is independently checked in each screenshot. See the
[browser commands](../BUILDING.md#browser) and [execution record](../validation/m2-browser-map.md).
This test host retains reflection metadata for evidence serialization; the
separate production browser publication remains trimmed. The
[browser performance diagnostics](../validation/m2-browser-performance.md) compare
Software2D/CPU, WebGL/CPU and WebGL/GPU with real input and the shared scoped
large-world fixture. They preserve correctness and timing failures separately
from the completed CPU-composition functional matrix.

`python3 tools/check_map_assets.py` validates binding metadata; supplying
`--pack /path/to/runtime/data/atlas-hd.pack` also checks the pinned bytes. Neither
command executes the renderer. Keep root headless tests independent of graphics
packages/displays; optional renderer jobs use explicitly pinned content and the
production host. Retain per-feature visible evidence as well as semantic tests.

## Independent CLI/helper drivers

After the root Release build, run the following. Append `.exe` to executable
paths on Windows; use `python` in place of `python3`. Set `DOTNET_ROOT` if apphosts
need a nonstandard SDK installation. SQLite commands require its optional build.

```sh
python3 tests/inspection_cli.py src/OpenCiv2.Cli/bin/Release/net11.0/oc2 tests/OpenCiv2.SaveRoundTrip/bin/Release/net11.0/OpenCiv2.SaveRoundTrip
python3 tests/content_cli.py src/OpenCiv2.Cli/bin/Release/net11.0/oc2
python3 tests/content_pack_cli.py src/OpenCiv2.Cli/bin/Release/net11.0/oc2
python3 tests/rules_cli.py src/OpenCiv2.Cli/bin/Release/net11.0/oc2
python3 tests/preference_cli.py tests/OpenCiv2.PreferenceRoundTrip/bin/Release/net11.0/OpenCiv2.PreferenceRoundTrip
python3 tests/roundtrip_helpers_cli.py tests/OpenCiv2.MapRoundTrip/bin/Release/net11.0/OpenCiv2.MapRoundTrip tests/OpenCiv2.WireRoundTrip/bin/Release/net11.0/OpenCiv2.WireRoundTrip
python3 tests/live_save_cli.py tests/OpenCiv2.LiveSave/bin/Release/net11.0/OpenCiv2.LiveSave
python3 tests/sqlite_cli.py src/OpenCiv2.Cli.Sqlite/bin/Release/net11.0/oc2-sqlite
```

These independently exercise complete JSON/status results, exact byte round trips,
publication, paths, malformed input and preserved sources. The bounded live-save
helper does not implement a full game load/play flow. See [helper contracts](helpers.md)
and [configuration coverage](configuration.md).

## X02 execution

```sh
python3 tools/dotnet/pcg_fixtures.py
python3 tools/dotnet/run_conformance.py --report build/test-results/conformance-native.json --adapter dotnet tests/OpenCiv2.ConformanceAdapter/bin/Release/net11.0/OpenCiv2.ConformanceAdapter.dll
python3 tools/dotnet/test_conformance.py --adapter dotnet tests/OpenCiv2.ConformanceAdapter/bin/Release/net11.0/OpenCiv2.ConformanceAdapter.dll
python3 tests/conformance_script_adapter.py dotnet tests/OpenCiv2.ConformanceAdapter/bin/Release/net11.0/OpenCiv2.ConformanceAdapter.dll
python3 tests/conformance_session_adapter.py dotnet tests/OpenCiv2.ConformanceAdapter/bin/Release/net11.0/OpenCiv2.ConformanceAdapter.dll
```

After installing the WASM workload and Node/Playwright as documented in BUILDING:

```sh
dotnet restore tests/OpenCiv2.ConformanceBrowser --locked-mode
dotnet publish tests/OpenCiv2.ConformanceBrowser -c Release --no-restore
python3 tools/dotnet/run_conformance.py --report build/test-results/conformance-browser.json --browser-bundle tests/OpenCiv2.ConformanceBrowser/bin/Release/net11.0-browser/publish/wwwroot
python3 tools/dotnet/test_conformance.py --browser-bundle tests/OpenCiv2.ConformanceBrowser/bin/Release/net11.0-browser/publish/wwwroot
python3 tools/dotnet/compare_conformance.py build/test-results/conformance-native.json build/test-results/conformance-browser.json
```

The selected 57 cases cover fifteen operations. The runner verifies compiled
source identities, fixture policy, invocation artifacts, result/trace comparisons
and consumed prefixes. Expected observations never enter the adapter. Follow
[conformance registration](../conformance/README.md) to extend the surface.

## Foundation reports and infrastructure

```sh
dotnet tests/OpenCiv2.FoundationProbe/bin/Release/net11.0/OpenCiv2.FoundationProbe.dll > build/test-results/native.json
python3 tools/dotnet/write_identity.py build/test-results/build-identity.json --report build/test-results/native.json --report build/test-results/browser.json
python3 tools/dotnet/test_identity.py
python3 tools/dotnet/test_coverage.py
python3 tools/dotnet/test_publish_notices.py
```

Create `build/test-results` before redirecting output. Generate the browser report
with the build guide's browser test command. Source changes invalidate prior
reports; rebuild/rerun rather than updating their hashes. Native dependency and
publication inventories also reject missing or modified inputs.

The [CI matrix](../../.github/workflows/ci.yml) runs the integrated commands.
[Platform acceptance](platforms.md), [packaging](../packaging.md) and
[performance](../performance/README.md) supply their own procedures. Keep authored
contract tests separate from external original-game observations and claims.


The optional renderer integration checks (`MapProbe --renderer-options`) exercise
MV29/MV30 fallback, retry, context replacement and bounded caches for 100 warmed
observer replacements. They require Linux hardware EGL. The CPU and experimental
GPU choices retain the existing native interaction and raster matrix suites;
[the integration report](../validation/m2-renderer-options.md) declares the GPU
pixel tolerance and distinguishes local evidence from deferred platform gates.

`MapProbe --gpu-optimization-checks ART_FIXTURES` adds navigation-history,
shared-mask alpha/opacity, sparse-delta and separated-damage regressions.
Experimental matrices record rejected history differences; require zero
`historyFailures` before treating a candidate as pixel-correct. Set
`OPENCIV2_REPLAY_ALL=1` for strict fresh-GPU comparison of every saved native
frame and warmup in `--replay-gpu-experiment`. The staged native, profiling,
pipeline and latency drivers and their evidence are described in the
[optimization report](../validation/m2-gpu-optimization.md).

The browser terrain-offload candidate remains experimental.
`run_map_browser.cjs --renderer=gpu --gpu-history-only` runs the same 204
navigation cases through the host's live WebGL context, requiring exact retained
versus fresh GPU pixels and the existing CPU/GPU bounds, plus 100 warmed scene,
density and resource replacements. Add
`--gpu-features=Atlas,Direct,Textures,Terrain,Encoded,Commands` to exercise that
candidate in the test host. Replacement failures save actual/fresh PNGs, draw
errors and the comparison receipt before the driver rejects the run. Stable-layout
mask experiments retain their separate strict diagnostic results and are not
promoted with this profile.
The [browser default report](../validation/m2-browser-gpu-default.md) identifies
executed configurations. `run_application_browser.cjs` checks default CPU (or
`--renderer=gpu`), explicit CPU, software CPU and rejection of software/GPU in
the trimmed host.

The same application input drivers now exercise the M2 shell menus and custom
confirmation dialog: every admitted action, menu/shortcut/button dispatch,
checked-state synchronization, focus cycling, Escape, repeat suppression,
text-entry isolation and resized hit regions. The browser runs at 1× and 2×.
`ShellInteractionCases` separately checks stale targets and interaction identities,
observer handoff, cancellation, deadlines and peer requests without graphics
dependencies. See the [execution record](../validation/m2-menus-dialogs.md);
macOS native menu tracking and real assistive technology are separate gates.

The [storage/service drivers](../BUILDING.md#application-storage-and-service-checks)
exercise the actual native and WASM file library, selection cancellation,
unchanged import/export, persisted preferences, removal and restart. Browser
fault injection aborts a write after request success and rejects quota writes;
only transaction completion can publish success. `PlatformServiceCases` runs
portable envelope/workflow/clock/silent-media contracts in both foundation hosts.
`PlatformStorageTests` adds actual native atomic writes and delayed selection/
commit lifetime cases. See the [execution record](../validation/m2-storage-services.md)
for remaining platform and playable-session joins.
