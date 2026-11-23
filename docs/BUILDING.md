# Building and testing

Run commands from the repository root. `global.json` pins **.NET SDK 11.0.100**
and Microsoft Testing Platform; projects pin runtime **11.0.0**. Install that
SDK on Windows, macOS or Linux; SDK roll-forward is disabled. Python **3.12+**
runs independent validators, CLI drivers and packaging. Use `python` instead of
`python3` on Windows. Commands below work in a terminal with the SDK on `PATH`.

The root `OpenCiv2.slnx` builds the core and CLI without a display, native compiler,
assets or optional UI/runtime packages. Optional solutions are in `solutions/`.
Package versions and hashes are locked; restore explicitly before `--no-restore`.

## Core and command-line tools

```sh
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test --project tests/OpenCiv2.Base.Tests -c Release --no-build --no-restore
dotnet run --project src/OpenCiv2.Cli -c Release --no-build -- --help
dotnet run --project src/OpenCiv2.Cli -c Release --no-build -- inspect path/to/game.sav --json
```

The suite combines portable groups, native I/O checks and API-boundary checks.
See the [validation records](validation/README.md) for dated test counts.
Test counts describe the current bounded implementation,
not whole-game compatibility. The CLI supports stored inspection, validation,
diff, content resolution/validation and classic rule/name/preference conversion.
An explicit loaded view is available for Alder Valley saves. It stages the
classic file under the authored rules and returns the local player's observation.

```sh
dotnet run --project src/OpenCiv2.Cli -c Release --no-build -- \
  inspect /path/to/alder-valley.sav --view loaded --rules alder-valley --json
```

The SQLite-enabled CLI takes the same options with
`export FILE --sqlite NEW_DATABASE`. The live application console issues
gameplay commands through the player's session.

Apphosts are `src/OpenCiv2.Cli/bin/Release/net11.0/oc2` (`oc2.exe` on Windows).
Framework-dependent apphosts require the runtime discoverable; set `DOTNET_ROOT`
when using an SDK outside the normal installation location. You can always invoke
`dotnet src/OpenCiv2.Cli/bin/Release/net11.0/oc2.dll`. See
[packaging](packaging.md) for tested self-contained archives.

## Avalonia desktop

The optional App solution includes `OpenCiv2.MapRendering`, the shared Skia
renderer library. App supplies the Avalonia controls and host integration;
MapProbe references both for raster and application-boundary checks. The root
headless solution remains independent of graphics packages.

```sh
dotnet restore solutions/OpenCiv2.App.slnx --locked-mode
python tools/dotnet/build_audio.py
dotnet build solutions/OpenCiv2.App.slnx -c Release --no-restore
dotnet publish src/OpenCiv2.Desktop -c Release --no-restore
```

Launch the Alder Valley playable profile with the desktop host. The pinned
graphics and font arguments are optional and use the same renderer as Riverbend.

```sh
dotnet run --project src/OpenCiv2.Desktop -c Release --no-build -- --game alder-valley
```

To see Riverbend with its river, road, rail and resource sprites, verify the
pinned archives against `dependencies.json`, extract them, and pass the package
root containing `package.json` and the font root containing `LICENSE` and `ttf/`.

```sh
dotnet run --project src/OpenCiv2.Desktop -c Release --no-build -- \
  --assets /path/to/runtime-package --fonts /path/to/dejavu-fonts-ttf-2.37
```

If startup reports **Asset package identity does not match the pinned release**,
compare the version in that directory's `package.json` with `assets.version` in
`dependencies.json`. Download the pinned `assets.url`, verify the archive against
`assets.sha256`, and extract it into a fresh directory. Point `--assets` to that
directory. The current pin is **2026.11.08.1**; an existing **2026.11.07.1** package
must be replaced because its atlas bytes also differ. Editing the manifest's
version does not update the assets and will fail the file hash checks.

The host validates the input pins before drawing. With stock graphics loaded,
Riverbend shows a blue river crossing a brown road and small resource sprites,
including pale Grassland shields. The header reads **Authored map / Riverbend**.
A launch without content opens **Riverbend / Limited graphics preview**, which
uses a fallback terrain palette. The
[bounded application](application.md) exposes one authored stack-order command.

Add `--showcase MV04`, `--showcase MV05`, or `--showcase MV07` to open the
authored river, resource, or road/rail board in the same desktop renderer. The
showcase selector also offers the other MV boards and the large `Mixed` world.

`--map-art ROOT` selects a native theme directory with `map-art.json`. Its
`openciv2-map-art/1` manifest lists exact catalog keys in an `images` object.
Each entry has a relative PNG `path`, `sha256`, and optional two-integer `marker`
and `flag` anchors. For classic map sheets use `--scenario-art ROOT`. Each
sheet tries the scenario BMP, scenario GIF, and then the installation GIF from
`--installation-art ROOT` if supplied. A successfully selected sheet provides
its declared bindings over native and pinned stock art. A malformed candidate
produces a diagnostic before fallback.

With art overrides loaded, **Use supplied art** in Map options or the showcase
switches between the admitted art and stock within the open window. **Reload
map textures** recreates drawing resources for the current selection.

Linux input automation uses an isolated X11 display (install `xvfb` and `libxtst6`):

```sh
python3 tools/dotnet/run_xvfb.py python3 tools/dotnet/run_application.py --report build/test-results/application-native.json --adapter dotnet src/OpenCiv2.Desktop/bin/Release/net11.0/OpenCiv2.Desktop.dll
```

To exercise the pinned content, pass `--map-assets ROOT --map-fonts ROOT` before
`--adapter` in the input command above. The input driver also checks the Orders
and View menus and the custom confirmation dialog. The browser application driver
exercises the same routes at both densities. See the
[menus/dialogs record](validation/m2-menus-dialogs.md) for the current scope.
Run the software raster probe with the same
two extracted roots to measure preparation and redraw workloads:

```sh
dotnet run --project tests/OpenCiv2.MapProbe -c Release --no-build -- \
  /path/to/runtime-package /path/to/dejavu-fonts-ttf-2.37 build/test-results/map-raster.json
```

Generate the authored P03 map-sheet cases with Python and Pillow, then run the
Skia matrix. The same fixtures are accepted by the native window driver.

```sh
python3 tools/dotnet/build_map_art_cases.py build/test-results/map-art-cases
dotnet run --project tests/OpenCiv2.MapProbe -c Release --no-build -- \
  /path/to/runtime-package /path/to/dejavu-fonts-ttf-2.37 build/test-results/map-gallery \
  --matrix build/test-results/map-art-cases
```

To compare every pixel decoded by the production PNG reader with Pillow,
generate an atlas receipt and check it against the pinned pack:

```sh
dotnet run --project tests/OpenCiv2.MapProbe -c Release --no-build -- \
  /path/to/runtime-package /path/to/dejavu-fonts-ttf-2.37 build/test-results/atlas-pixels.json \
  --atlas-pixels
python3 tools/check_map_assets.py --pack /path/to/runtime-package/data/atlas-hd.pack \
  --decoded-report build/test-results/atlas-pixels.json
```

Reproduce the Linux window captures and input checks with the authored art
fixtures. To rerun input at 2× backing density, set
`AVALONIA_GLOBAL_SCALE_FACTOR=2`, pass `--screen 3000x2100x24` to Xvfb's
launcher, and add `--interaction-only --expected-density 2` before
`--adapter`. This exercises the Avalonia backing scale on a virtual display.

```sh
LIBGL_ALWAYS_SOFTWARE=1 python3 tools/dotnet/run_xvfb.py \
  python3 tools/dotnet/run_map_showcase_native.py \
  --assets /path/to/runtime-package --fonts /path/to/dejavu-fonts-ttf-2.37 \
  --art-cases build/test-results/map-art-cases --output build/test-results/map-gallery-native \
  --expected-density 1 --adapter dotnet src/OpenCiv2.Desktop/bin/Release/net11.0/OpenCiv2.Desktop.dll
```

With RandR's `xrandr` command installed, the mixed-density run opens two maps
in one desktop process on independent 1× and 2× Xvfb monitors. It checks
the actual draw receipts after resize and display migration and compares
overview footprint and selection pixels.

```sh
LIBGL_ALWAYS_SOFTWARE=1 python3 tools/dotnet/run_xvfb.py --mixed-dpi \
  python3 tools/dotnet/run_map_showcase_native.py \
  --assets /path/to/runtime-package --fonts /path/to/dejavu-fonts-ttf-2.37 \
  --art-cases build/test-results/map-art-cases --output build/test-results/map-gallery-mixed-dpi \
  --mixed-dpi-only --expected-density 1 --adapter dotnet src/OpenCiv2.Desktop/bin/Release/net11.0/OpenCiv2.Desktop.dll
```

For the scoped offscreen workloads, append `--performance` to the raster probe
command and use a separate output path. Append a workload name such as
`dirty-world` to repeat one series. For shorter software diagnostics, use
`--diagnostic-performance pan` and `--diagnostic-direct-performance pan` with
separate output files to compare retained-viewport rendering and the supported
direct fallback against independent fresh frames. Run Linux host timing separately to
observe actual pointer and world-update events through the desktop window:

```sh
LIBGL_ALWAYS_SOFTWARE=1 python3 tools/dotnet/run_xvfb.py --screen 5600x3300x24 \
  python3 tools/dotnet/measure_map_native.py \
  --assets /path/to/runtime-package --fonts /path/to/dejavu-fonts-ttf-2.37 \
  --output build/test-results/native-map-timing \
  --adapter dotnet src/OpenCiv2.Desktop/bin/Release/net11.0/OpenCiv2.Desktop.dll
```

The Linux timing driver records event-to-draw receipts and the actual viewport
pixel dimensions. The draw receipt includes ground chunk build time, the time
within that build spent selecting and grouping sprites, and texture preparation
time across all layers. It also records demand raster loads, tint preparation
time and retained texture bytes. `--diagnostic` runs one short trial.
For `--mode dirty-world`, the receipt separates construction of the authored
update, production scene binding and map draw by observation revision. It
also reports the sum of those measured stages, the scene index's allocations,
renderer wait and ground invalidation on either side of the UI handoff.
Foreground band timings cover backing setup, command recording, parallel
replay and edge repair.
Add `--diagnostic-warmup 30 --diagnostic-frames 36` to observe the first
high-resolution body transition during pointer zoom. Texture preparation
can overlap the chunk-build measurement. Set `AVALONIA_GLOBAL_SCALE_FACTOR=2` and pass
`--expected-density 2` before `--adapter` for a 2× backing run at the same
physical dimensions. If half the world needs a scale below the minimum zoom,
the receipt records the limit and the actual visible tile count. Xvfb measures
software execution.

On Linux with KWin, Xwayland, `glxinfo`, `xrandr` and DRM graphics counters,
the same driver can exercise hardware acceleration on an isolated virtual
output. `--virtual-gpu` requires accelerated direct GLX and checks graphics
engine activity in the desktop process itself. The counters cover whole
workloads. Physical display presentation and per-frame GPU timing remain
separate checks under the [performance contract](performance/map-rendering.md).

```sh
AVALONIA_GLOBAL_SCALE_FACTOR=2 python3 tools/dotnet/run_xwayland_virtual.py \
  --compositor-log build/test-results/virtual-gpu/compositor.log \
  python3 tools/dotnet/measure_map_native.py --virtual-gpu --expected-density 2 \
  --size 5120x2880 --mode pan \
  --assets /path/to/runtime-package --fonts /path/to/dejavu-fonts-ttf-2.37 \
  --output build/test-results/virtual-gpu \
  --adapter dotnet src/OpenCiv2.Desktop/bin/Release/net11.0/OpenCiv2.Desktop.dll
```

Pass `--flat` to measure the flat Mixed board at half-world scale. Use
`--flat-edge` for a separate pan that stages the same board near the camera
limit. The edge trial records its smaller starting tile count and requires
the measured gesture to reach the clamp.

Use `--mode resource-recovery` to repeat the native **Reload textures**
action at the measured camera. Each of three regular trials reloads
ground and textures 20 times while keeping the selected target. This
measures first redraw after resource loss within the running application.

Use `--mode cold-first-frame` to start a new desktop process for each sample.
It opens Mixed directly at full, half-world or overview detail and at the
requested backing size. Use `--cold-detail` to select a specific view. Three
regular trials take 20 first frames each per view and size. The receipt times
content validation separately from the interval through the end of the host's
Skia map draw. It also reports authored observation creation, scene binding
and inner map draw time. The benchmark uses reduced motion to keep the active
marker stable for screenshots. X11 readback happens after the measured interval.
Pass `--native-aot --adapter /path/to/published/OpenCiv2.Desktop` to verify
and measure the Linux Native AOT executable built from the signed lock.

Replay the first frames, pan and zoom cameras, dirty-world revisions and
resource recoveries from the Linux timing receipt against fresh serial renders.
The replay uses the authored Mixed scene, recorded backing dimensions,
dirty-world effect phases and any selected target recorded during recovery.
For cold startup it also compares the native map pixels outside the inset
overview. GPU and physical display presentation require separate measurements.

```sh
dotnet run --project tests/OpenCiv2.MapProbe -c Release --no-build -- \
  /path/to/runtime-package /path/to/dejavu-fonts-ttf-2.37 \
  build/test-results/native-map-replay.json \
  --replay-native-map build/test-results/native-map-timing/native-performance.json
```

Use the [platform checklist](testing/platforms.md) for actual Mac/Windows desktop
acceptance. Compilation or headless CI cannot establish native input, menus,
shutdown, IME, accessibility or mixed-DPI/fullscreen behavior.

### Opt-in Native AOT desktop

The commands above remain managed. To compile the desktop host to native machine
code, use `Oc2DesktopAot=true` on **both restore and publish**:

```sh
dotnet restore src/OpenCiv2.Desktop -p:Oc2DesktopAot=true --locked-mode
dotnet publish src/OpenCiv2.Desktop -c Release -p:Oc2DesktopAot=true --no-restore
```

The build selects the SDK's portable host RID. Checked-in AOT locks cover
`linux-x64`, `osx-arm64` and `win-x64`. Use Microsoft's SDK distribution and signed
NuGet compiler packages: distribution-rebuilt SDK/compiler packages can have
different hashes and dependency graphs even with the same version number.
Build on the target OS with the pinned SDK
and a native toolchain: Clang on Linux, Xcode command-line tools on macOS, or
Visual Studio's Desktop development with C++ workload on Windows. See the
[.NET Native AOT prerequisites](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/).
If Clang has a versioned executable name, append, for example,
`-p:CppCompilerAndLinker=clang-22` to the publish command.
Avalonia's OS libraries are still required. `Oc2DesktopAotRid` can select the RID
for intentional lock maintenance; cross-OS compilation is not supported.

Run `src/OpenCiv2.Desktop/bin/Release/net11.0/<rid>/publish/OpenCiv2.Desktop`
(`OpenCiv2.Desktop.exe` on Windows) directly. Keep the accompanying native graphics
libraries and dependency notices. The publication needs no installed .NET runtime.
`dotnet run` is still a managed development command; use the published executable
to run Native AOT.

On Linux, verify real pointer input, confirmation/cancellation and clean shutdown:

```sh
python3 tools/dotnet/run_xvfb.py python3 tools/dotnet/run_application.py --native-aot --report build/test-results/application-aot.json --adapter src/OpenCiv2.Desktop/bin/Release/net11.0/linux-x64/publish/OpenCiv2.Desktop
```

This also requires Native AOT dependency metadata, disabled dynamic code support,
and a publication without a managed entry assembly or CoreCLR library. CI publishes
AOT on all three listed hosts and runs this interaction check on Linux; macOS and
Windows interactive acceptance remains in the platform checklist. Local executed
evidence is recorded in [validation](validation/README.md).

AOT restore uses `packaging/locks/desktop-aot/<rid>.json` for the desktop host;
referenced libraries retain their ordinary locks. Restore assets are shared, so
run the ordinary application restore before returning to managed builds. Neither
mode changes the other mode's checked-in dependency graph.

## Browser

All Avalonia hosts use centrally pinned SkiaSharp **4.156.1** and HarfBuzzSharp
**14.4.0.101**, including matching Linux, macOS, Windows and WebAssembly native
assets. Central transitive pinning applies these versions to Avalonia's dependency
graph. The browser assets support the .NET 11 workload's Emscripten 6.0.2 linker.
Avalonia 12.1 supports explicitly selecting SkiaSharp 4;
its default SkiaSharp 3 WebAssembly assets reference older `setjmp` helpers and
cannot link with this workload. See the [Avalonia compatibility guidance](https://github.com/AvaloniaUI/Avalonia/issues/21696)
and [SkiaSharp package guidance](https://github.com/mono/SkiaSharp/blob/main/documentation/dev/packages.md).

Install the workload matching the pinned SDK, then publish the application:

```sh
dotnet workload install wasm-tools --skip-manifest-update
dotnet restore src/OpenCiv2.Browser --locked-mode
dotnet publish src/OpenCiv2.Browser -c Release --no-restore
python3 tools/dotnet/stage_browser_content.py \
  --bundle src/OpenCiv2.Browser/bin/Release/net11.0-browser/publish/wwwroot \
  --assets /path/to/runtime-package --fonts /path/to/dejavu-fonts-ttf-2.37
python3 -m http.server 8000 --directory src/OpenCiv2.Browser/bin/Release/net11.0-browser/publish/wwwroot
```

Open `http://localhost:8000`. Serve the published directory over HTTP; opening
`index.html` as a local file will not load the WebAssembly application correctly.
Stop the server before continuing if using the same terminal.

The browser requires the pinned content; missing or invalid content produces a
visible startup error. The staging helper verifies the release manifest and every
pinned file before copying. The browser independently validates the same pins
through asynchronous HTTP streams, retaining the atlas once and verifying the
remaining release files without keeping their payloads. The historical bitmap
font caches are checked as release files but never used to render text. All twelve
pinned DejaVu faces supply runtime text rendering.
Graphics-context loss presents a visible **Reload preview** action outside the
canvas. Reload reopens the preview and revalidates content; it does not restore
an unsaved session in place.

Browser CPU composition remains the default. `?renderer=gpu` selects the
existing opt-in GPU profile on WebGL; `?backend=software` uses Software2D/CPU.
Explicit software/GPU is rejected. The broader
[browser GPU validation](validation/m2-browser-gpu-default.md) found promotion
blockers, so terrain offload remains test-only and no default is changed.

Append `?showcase=MV01&observe=1` to open the shared authored gallery and enable
read-only draw receipts. Optional `--art-cases ROOT` staging admits the independently
authored native/scenario/installation fixtures. `&art=native` selects native PNGs;
`&art=classic` applies scenario BMP/GIF and installation fallback over native art.
The index preserves filenames; lookup rejects ambiguous case collisions.

Browser automation uses Node **24.19.0** and Playwright **1.58.2**:

```sh
npm install --prefix build/browser --no-save --package-lock=false playwright@1.58.2
node build/browser/node_modules/playwright/cli.js install chromium
```

On Linux CI use `install --with-deps chromium`. Set `NODE_PATH` to the absolute
`build/browser/node_modules` path (`export NODE_PATH="$PWD/build/browser/node_modules"`
in bash; `$env:NODE_PATH = "$PWD/build/browser/node_modules"` in PowerShell).
`OC2_CHROMIUM_EXECUTABLE` can select an existing compatible Chromium; reports
record its actual version. Execute the portable tests and real application input:

```sh
dotnet restore tests/OpenCiv2.Browser.Tests --locked-mode
dotnet publish tests/OpenCiv2.Browser.Tests -c Release --no-restore
node tools/dotnet/run_browser.cjs tests/OpenCiv2.Browser.Tests/bin/Release/net11.0-browser/publish/wwwroot build/test-results/browser.json
python3 tools/dotnet/run_application.py --report build/test-results/application-browser.json --browser-bundle src/OpenCiv2.Browser/bin/Release/net11.0-browser/publish/wwwroot
```

The portable suite runs the same shared groups as native. Browser hosts exclude
native filesystem, process CLI and SQLite projects. Release publication trims
assemblies and includes source-generated JSON; AOT is not an acceptance claim.

For the complete map matrix, build the separate browser acceptance host. It
links the production browser entry point and the existing exhaustive matrix,
and adds read-only fresh-frame and result exports. Reflection metadata is retained
only in this test host for its anonymous evidence records. Production remains
trimmed. Both use the same App and MapRendering libraries.

```sh
dotnet restore tests/OpenCiv2.MapBrowser --locked-mode
dotnet publish tests/OpenCiv2.MapBrowser -c Release --no-restore
python3 tools/dotnet/build_map_art_cases.py build/test-results/map-art-cases
python3 tools/dotnet/stage_browser_content.py \
  --bundle tests/OpenCiv2.MapBrowser/bin/Release/net11.0-browser/publish/wwwroot \
  --assets /path/to/runtime-package --fonts /path/to/dejavu-fonts-ttf-2.37 \
  --art-cases build/test-results/map-art-cases --acceptance
node tools/dotnet/run_map_browser.cjs \
  tests/OpenCiv2.MapBrowser/bin/Release/net11.0-browser/publish/wwwroot \
  build/test-results/map-browser
python3 tools/dotnet/check_browser_map_pixels.py build/test-results/map-browser
```

The WASM matrix runs the production CPU composition and semantic/pixel cases.
The browser gallery separately exercises WebGL2 presentation, real input and
resource lifecycle at both densities with stock/native/classic content. SwiftShader
evidence is functional fallback coverage, not hardware performance acceptance.
Presented frames after drag, wheel zoom, resize, texture reload and display-color
changes also compare exactly with fresh WebGL frames. `--matrix-only`,
`--gallery-only`, and `--transitions-only` can produce separate diagnostic receipts;
the default command executes the complete set.
For bounded runs, add `--gallery-only --theme=stock --density=1`, writing to
`build/test-results/map-browser/stock-1x`. Repeat for `stock`, `native`, `classic`
at both densities, then run `python3 tools/dotnet/merge_map_browser.py
build/test-results/map-browser` before the pixel checker. Merging requires all six
completed batches with identical compiled inputs, browser and driver. Interrupted
runs retain an explicitly incomplete checkpoint and cannot pass the pixel checker.
The native worker-race check is inapplicable to this single-thread browser host;
stale-generation, observer replacement and cache privacy cases still execute.

The browser also accepts `?backend=software` or `?backend=webgl` (default),
and `renderer=cpu` (default) or `renderer=gpu`. GPU map composition requires
WebGL. These choices are independent: WebGL can present a CPU-composed map.
For example, `?showcase=Mixed&observe=1&backend=webgl&renderer=gpu` opens the
large authored gallery with GPU composition and draw receipts.

Browser performance diagnostics use the same scoped authored world as the native
probe. Run configurations sequentially on an otherwise idle machine:

```sh
node tools/dotnet/measure_map_browser.cjs \
  tests/OpenCiv2.MapBrowser/bin/Release/net11.0-browser/publish/wwwroot \
  build/test-results/browser-map-performance \
  --backend=webgl --renderer=gpu --size=1920x1080 --density=1 \
  --runs=3 --warmup=30 --samples=300 --pan-pattern=travel --recover-check
python3 tools/dotnet/check_map_browser_performance.py build/test-results/browser-map-performance
```

Select `--backend=software --renderer=cpu` for Software2D, or
`--backend=webgl --renderer=cpu` for CPU composition in the WebGL host. Dimensions
are the physical map viewport; `--density=2` divides the logical extent once.
Hardware WebGL runs reject SwiftShader/llvmpipe. `OC2_CHROMIUM_EXECUTABLE` can select
the pinned Chromium executable. The test host alone accepts `--gpu-features=...`
for controlled feature experiments; production ignores these experiment switches.
`--pan-pattern=oscillate` (the default) screens two nearby warmed positions;
`travel` crosses tile boundaries and includes a one-physical-pixel change.
`--recover-check` also compares the actual host after reloading textures.

The driver records raw warmups and measured frames, source identities, per-run
quantiles, browser callback/GPU-query observations and WASM capacity. Its fresh
pixel checks sample workload endpoints. Even the larger sampling command above
is **not** the complete acceptance protocol: effects, edge/replacement/lifetime
workloads, every-changed-frame comparison and physical presentation remain
separate. See the [browser performance record](validation/m2-browser-performance.md)
for measured results and limitations. `run_map_browser.cjs --hardware
--renderer=gpu` also exercises the existing gallery on the hardware GPU path;
the driver's ordinary default remains CPU composition on WebGL/SwiftShader.
Use `--renderer=default` to omit the renderer override and verify the production
browser default. `--gpu-history-only --renderer=gpu` runs the shared 204-case
GPU navigation suite and 100 warmed resource replacements inside the production
Skia lease. To test terrain offload, also pass
`--gpu-features=Atlas,Direct,Textures,Terrain,Encoded,Commands` (acceptance host
only). Add `--hardware` to require hardware WebGL. The optional stable-layout
mask experiment is reported separately; it is not enabled by the default profile.

## Optional SQLite and Lua

SQLite needs its optional native dependency; Lua-CSharp restores through NuGet:

```sh
python3 tools/dotnet/build_sqlite.py
```

The SQLite builder verifies the archive hash in `dependencies.json`; `--archive PATH`
uses an offline download. Use a native C compiler (`cc` on Linux/macOS, Visual
Studio Developer Command Prompt/PowerShell on Windows). `--cc PATH` selects another
compiler. Build receipts and notices accompany the optional SQLite library.
Lua automation needs no C compiler or separate runtime preparation.

```sh
dotnet restore solutions/OpenCiv2.Sqlite.slnx --locked-mode
dotnet build solutions/OpenCiv2.Sqlite.slnx -c Release --no-restore
dotnet test --project tests/OpenCiv2.Sqlite.Tests -c Release --no-build --no-restore
dotnet restore solutions/OpenCiv2.Automation.slnx --locked-mode
dotnet build solutions/OpenCiv2.Automation.slnx -c Release --no-restore
dotnet test --project tests/OpenCiv2.Automation.Tests -c Release --no-build --no-restore
```

SQLite adds four tests and the independent export CLI driver. Lua adds fourteen
production-session/resource groups. To test Lua-CSharp in WebAssembly:

```sh
dotnet restore tests/OpenCiv2.AutomationBrowser --locked-mode
dotnet publish tests/OpenCiv2.AutomationBrowser -c Release --no-restore
python3 tools/dotnet/run_automation.py --report build/test-results/lua-native.json --adapter dotnet tests/OpenCiv2.AutomationProbe/bin/Release/net11.0/OpenCiv2.AutomationProbe.dll
python3 tools/dotnet/run_automation.py --report build/test-results/lua-browser.json --browser-bundle tests/OpenCiv2.AutomationBrowser/bin/Release/net11.0-browser/publish/wwwroot
python3 tools/dotnet/compare_automation.py build/test-results/lua-native.json build/test-results/lua-browser.json
```

The browser command uses the Node/Playwright environment above. See the
[Lua contract](automation.md) for budgets, permissions and lifetime rules.

The optional desktop launcher uses the existing Avalonia host and an explicit
script path. Build it separately from the normal desktop solution.

```sh
dotnet restore solutions/OpenCiv2.Desktop.Automation.slnx --locked-mode
dotnet build solutions/OpenCiv2.Desktop.Automation.slnx -c Release --no-restore
dotnet src/OpenCiv2.Desktop.Automation/bin/Release/net11.0/OpenCiv2.Desktop.Automation.dll \
  --script examples/automation/next-production.lua
```

Pass the usual graphics and storage options after the script path. The launcher
opens Alder Valley. Use **Start script** in the sidebar after opening any save
you want the script to run on. The regular desktop build retains its existing
dependency graph. Linux verification uses the native play runner with
`--automation-desktop` pointing at the optional launcher assembly.

Add `--record TRACE` immediately after the script path to record the local
session. Replay the journal with the same optional launcher and
`--replay TRACE --output SAVE`. The trace includes imported save bytes and
the commands issued by the player and the script. Both output paths must be new.

## Independent CLI and conformance checks

[Test instructions](testing/README.md) list all eight CLI/helper suites, X02
execution and rejection checks, source-identity verification and package tests.
Round-trip helpers under `tests/` are test executables, not product CLI commands.

## Formatting and commits

```sh
dotnet format OpenCiv2.slnx --no-restore
dotnet format solutions/OpenCiv2.App.slnx --no-restore
```

Format the optional solution/project you changed too. Use `--verify-no-changes`
in checks. Review the diff before staging; formatting never authorizes unrelated
changes. Nullable analysis, checked arithmetic and warnings-as-errors are enabled.

The Lua-CSharp adapter shares task/resource tests between native and browser
hosts. Do not format dependency downloads or generated files.

## CI and metadata

[Build and test](../.github/workflows/ci.yml) runs core/CLI, optional Lua/SQLite,
packages and desktop compilation on Linux, macOS ARM64 and Windows x64; Chromium
runs portable, conformance, Lua and actual application input checks. Linux also
runs X11 input and signal-cancellation checks. [Repository checks](../.github/workflows/repository.yml)
validate specifications and content on all three operating systems.

```sh
python3 -m pip install -r tools/requirements-schemas.txt
python3 tools/check_conformance.py
python3 tools/check_assets.py
python3 tools/check_content_schemas.py
python3 tools/check_gameplay.py
python3 tools/dotnet/check_graph.py
python3 tools/dotnet/check_coverage.py
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

[Validation status](validation/README.md) distinguishes executed evidence from
pending platform work. Publication includes verified full package/runtime/native
and font notices. SDK/package upgrades require updated locks and complete affected
checks; changing source or build inputs invalidates older compiled-source reports.


### Selecting the desktop map renderer

Pass `--map-renderer cpu` or `--map-renderer gpu` to the desktop executable (or
after `--` with `dotnet run`). GPU is experimental; CPU is the default. Use
`--map-gpu-cache-mib 512` to select a 128–1024 MiB Skia resource-cache ceiling and
`--renderer-settings build/renderer-test.json` to isolate preferences in tests.
The UI saves a choice for the next launch. CLI overrides do not rewrite it.
Research scripts additionally set `OPENCIV2_MAP_EXPERIMENT=1` before using
`OPENCIV2_MAP_COMPOSITION`, `OPENCIV2_MAP_GPU_FEATURES`, `OPENCIV2_MAP_GPU_SYNC`
and `OPENCIV2_MAP_GPU_CACHE_MIB`. Such experiments fail if GPU timing or the GPU
renderer is unavailable; they never silently benchmark CPU fallback as GPU.

After building the optional App solution, run `OpenCiv2.MapProbe ASSETS FONTS
OUTPUT --renderer-options` under `tools/dotnet/run_xwayland_virtual.py` for Linux
hardware context, preference codec, fallback and 100 warmed replacement checks.
`--gpu-experiment-matrix ART_FIXTURES` now requires exact fresh/retained frames
for the supported GPU configuration, as well as the declared cross-backend
raster tolerance. See [the integration report](validation/m2-renderer-options.md).

For opt-in optimization experiments, `--gpu-optimization-checks ART_FIXTURES`
exercises history, mask and sparse-damage cases. Use
`tools/dotnet/run_map_optimization.py` for separate `native`, `cold`, `profile`,
`pipeline`, `latency` and strict `replay` stages. Pass `--source-root` when
validating a frozen executable against archived sources. The
[optimization report](validation/m2-gpu-optimization.md) lists feature gates,
rejected experiments and exact local reproduction commands.

`--gpu-sampling-checks ART_FIXTURES` runs the focused nearest-sampling and chunk
coverage regressions under hardware EGL. The equivalent browser driver option
is `--renderer=gpu --gpu-sampling-only`, optionally with `--hardware`. Both
require the existing CPU raster bounds; see the
[sampling investigation](validation/m2-gpu-sampling.md).

### Application storage and service checks

**File → Files and preferences** stores imported bytes, exports unchanged copies
and saves local display preferences. With `--game alder-valley`, **Open selected
game** also resumes an admitted classic save from the library.
Native application storage defaults to the OS application-data folder under
`OpenCiv2/storage`; use `--storage-directory PATH` for an isolated profile.
Browser data is origin-scoped IndexedDB; renderer query overrides do not rewrite
saved preferences. Downloads report request, not durable completion.

After the ordinary desktop build and production browser publish/content stage:

```sh
python3 tools/dotnet/run_xwayland_virtual.py --compositor-log build/test-results/storage-compositor.log \
  python3 tools/dotnet/run_storage_native.py --output build/test-results/storage-native.json \
  --adapter dotnet src/OpenCiv2.Desktop/bin/Release/net11.0/OpenCiv2.Desktop.dll
node tools/dotnet/run_storage_browser.cjs \
  src/OpenCiv2.Browser/bin/Release/net11.0-browser/publish/wwwroot \
  build/test-results/storage-browser.json
```

The Linux driver uses XTest, the actual GTK picker and ImageMagick `import` for
owned-window screenshots. Run it again with `AVALONIA_GLOBAL_SCALE_FACTOR=2`.
The browser driver tests both densities, process restart, byte equality and
IndexedDB failures with the pinned Playwright/Chromium environment above.
Continue running the existing application input and shared foundation suites.
See [scope and evidence](validation/m2-storage-services.md).

Native WAV playback uses the pinned miniaudio build copied with the desktop.
See [the audio profile](content/media-profile.md) for offline builds, admission
limits, cancellation semantics and isolated native/browser validation. For
startup transfer measurements, run `node tools/dotnet/compress_browser_content.cjs BUNDLE`
after staging content; serve `.br` files with `Content-Encoding: br` and the
original MIME type. This changes transport bytes only, not decoded content pins.
