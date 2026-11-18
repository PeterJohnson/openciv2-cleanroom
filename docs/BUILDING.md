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

The suite contains 454 tests: 432 portable groups, twenty native I/O checks and
two API-boundary checks. Test counts describe the current bounded implementation,
not whole-game compatibility. The CLI supports stored inspection, validation,
diff, content resolution/validation and classic rule/name/preference conversion.
It does not expose live commands or playable gameplay.

Apphosts are `src/OpenCiv2.Cli/bin/Release/net11.0/oc2` (`oc2.exe` on Windows).
Framework-dependent apphosts require the runtime discoverable; set `DOTNET_ROOT`
when using an SDK outside the normal installation location. You can always invoke
`dotnet src/OpenCiv2.Cli/bin/Release/net11.0/oc2.dll`. See
[packaging](packaging.md) for tested self-contained archives.

## Avalonia desktop

```sh
dotnet restore solutions/OpenCiv2.App.slnx --locked-mode
dotnet build solutions/OpenCiv2.App.slnx -c Release --no-restore
dotnet run --project src/OpenCiv2.Desktop -c Release --no-build
dotnet publish src/OpenCiv2.Desktop -c Release --no-restore
```

This opens the [bounded session application](application.md). A graphical desktop
and Avalonia's platform runtime dependencies are required. The authored three-unit
world demonstrates observation, selection, console input and asynchronous command
confirmation. It is not a map renderer or a playable scenario.

Linux input automation uses an isolated X11 display (install `xvfb` and `libxtst6`):

```sh
python3 tools/dotnet/run_xvfb.py python3 tools/dotnet/run_application.py --report build/test-results/application-native.json --adapter dotnet src/OpenCiv2.Desktop/bin/Release/net11.0/OpenCiv2.Desktop.dll
```

Use the [platform checklist](testing/platforms.md) for actual Mac/Windows desktop
acceptance. Compilation or headless CI cannot establish native input, menus,
shutdown, IME, accessibility or mixed-DPI/fullscreen behavior.

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
python3 -m http.server 8000 --directory src/OpenCiv2.Browser/bin/Release/net11.0-browser/publish/wwwroot
```

Open `http://localhost:8000`. Serve the published directory over HTTP; opening
`index.html` as a local file will not load the WebAssembly application correctly.
Stop the server before continuing if using the same terminal.

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

The portable suite runs the same 432 groups as native. Browser hosts exclude
native filesystem, process CLI and SQLite projects. Release publication trims
assemblies and includes source-generated JSON; AOT is not an acceptance claim.

## Optional SQLite and Lua

Build either native dependency independently:

```sh
python3 tools/dotnet/build_sqlite.py
python3 tools/dotnet/build_lua.py
```

These builders verify the archive hashes in `dependencies.json`; `--archive PATH`
uses an offline download. Use a native C compiler (`cc` on Linux/macOS, Visual
Studio Developer Command Prompt/PowerShell on Windows). `--cc PATH` selects another
compiler. Lua supports Unix `--sanitize` checks. The small bridge contains no game
model; build receipts and notices accompany the optional library.

```sh
dotnet restore solutions/OpenCiv2.Sqlite.slnx --locked-mode
dotnet build solutions/OpenCiv2.Sqlite.slnx -c Release --no-restore
dotnet test --project tests/OpenCiv2.Sqlite.Tests -c Release --no-build --no-restore
dotnet restore solutions/OpenCiv2.Automation.slnx --locked-mode
dotnet build solutions/OpenCiv2.Automation.slnx -c Release --no-restore
dotnet test --project tests/OpenCiv2.Automation.Tests -c Release --no-build --no-restore
```

SQLite adds four tests and the independent export CLI driver. Lua adds thirteen
production-session/resource groups. To test the stock Lua bridge in WebAssembly:

```sh
python3 tools/dotnet/prepare_lua_wasm.py
dotnet restore tests/OpenCiv2.AutomationBrowser --locked-mode
dotnet publish tests/OpenCiv2.AutomationBrowser -c Release --no-restore
python3 tools/dotnet/run_automation.py --report build/test-results/lua-native.json --adapter dotnet tests/OpenCiv2.AutomationProbe/bin/Release/net11.0/OpenCiv2.AutomationProbe.dll
python3 tools/dotnet/run_automation.py --report build/test-results/lua-browser.json --browser-bundle tests/OpenCiv2.AutomationBrowser/bin/Release/net11.0-browser/publish/wwwroot
python3 tools/dotnet/compare_automation.py build/test-results/lua-native.json build/test-results/lua-browser.json
```

The browser command uses the Node/Playwright environment above. See the
[Lua contract](automation.md) for budgets, permissions and lifetime rules.

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

The optional native Lua bridge follows its existing C style. Its sanitizer and
native/browser resource tests validate that boundary. Do not format dependency
downloads or generated files.

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
