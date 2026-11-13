# Building the application

This builds the initial application shell described in
[application-shell.md](application-shell.md). Gameplay, graphics-pack loading,
classic file/wire codecs, and X02 execution remain implementation-plan work.

## Dependencies and presets

Use CMake 3.24+, a C++20 compiler, and Ninja for the supplied presets. macOS
requires the Xcode command-line tools/AppKit; Windows needs a Visual Studio
developer shell for Ninja, or the Visual Studio generator shown below.

[dependencies.json](../dependencies.json) pins source archives and SHA-256 values
for SDL3 3.4.2, Dear ImGui 1.93.0, Catch2 3.8.1, and the matching ImGui Test Engine
revision. It also records the Emscripten and Playwright versions used by CI.
Source downloads are disabled unless `OC2_FETCH_DEPENDENCIES=ON` is explicit.
A preset whose name ends in `-fetch`, plus `gui-tests` and `web`, opts into downloads.

| Preset | Purpose |
| --- | --- |
| `desktop` | Debug GUI; installed SDL3 3.4.2+ and offline source dependencies |
| `desktop-fetch` | Debug GUI; download and build the pinned dependencies, including SDL3 |
| `gui-tests` | Same as desktop-fetch, with ImGui Test Engine enabled |
| `release` | Release GUI; offline dependencies, no tests or test-engine instrumentation |
| `headless` | SDL/ImGui-free Catch2 tests; installed pinned Catch2 or offline source |
| `headless-fetch` | Same headless build, allowing the pinned Catch2 download |
| `sanitize` | Native headless ASan/UBSan; offline dependencies |
| `web` | Release browser shell and WASM Catch2 tests, using the activated emsdk |

All presets have matching build/test presets except `release`, which has no test
preset. Keep machine-specific paths in ignored `CMakeUserPresets.json` or pass
cache settings explicitly. Relevant switches are `OC2_BUILD_GUI`,
`OC2_FETCH_DEPENDENCIES`, `OC2_FETCH_SDL`, `OC2_GUI_TESTS`, `OC2_SANITIZERS`, and
standard `BUILD_TESTING`. GUI automation is opt-in and native-only.

For an offline build, supply upstream source trees matching the pins:

```sh
cmake --preset desktop \
  -DFETCHCONTENT_SOURCE_DIR_IMGUI=/path/to/imgui-1.93.0 \
  -DFETCHCONTENT_SOURCE_DIR_CATCH2=/path/to/Catch2-3.8.1
```

Supply `FETCHCONTENT_SOURCE_DIR_SDL3` to build SDL from a local source tree, and
`FETCHCONTENT_SOURCE_DIR_IMGUI_TEST_ENGINE` with `OC2_GUI_TESTS=ON` for offline GUI
tests. A populated build's dependency cache also works with downloads disabled.
Source overrides are explicit trusted developer inputs: URL hashes verify
fetched archives, not the contents of an override directory. Invalid explicit
paths fail rather than falling back to a download. Installed Catch2 must match
3.8.1; installed SDL3 may be a compatible newer version. Record such overrides
when reporting results.

`OC2_BUILD_GUI=OFF` configures without SDL, ImGui, the asset clone, or a display.
With tests enabled, Catch2 is the only third-party dependency. With
`BUILD_TESTING=OFF` as well, this builds just the portable view-model library
and needs no third-party dependency. Future game libraries inherit this boundary.

## Desktop

For a fresh checkout with network access:

```sh
cmake --preset desktop-fetch
cmake --build --preset desktop-fetch --parallel 4
ctest --preset desktop-fetch
```

Use `gui-tests` in all three commands to include GUI automation. To use installed
SDL3, use `desktop` and supply the offline sources above (or explicitly enable
`OC2_FETCH_DEPENDENCIES`). `SDL3_DIR`/`CMAKE_PREFIX_PATH` can locate an installed
SDL package. Linux source builds need X11/Wayland development libraries for the
backend being shipped; CI lists the Ubuntu packages.

Run `build/desktop-fetch/openciv2` on Linux, the `.exe` on Windows, or
`open build/desktop-fetch/openciv2.app` on macOS. Substitute the selected preset's
build directory. For Visual Studio without Ninja:

```powershell
cmake -S . -B build/windows -DOC2_FETCH_SDL=ON -DOC2_FETCH_DEPENDENCIES=ON -DOC2_GUI_TESTS=ON
cmake --build build/windows --config Debug --parallel 4
ctest --test-dir build/windows -C Debug --output-on-failure
.\build\windows\Debug\openciv2.exe
```

Source builds use static SDL. For installed shared SDL on Windows, the build
copies its DLL beside the executable. Installed shared SDL on macOS must remain
available at runtime. These are development builds; relocatable packaging,
signing/notarization, assets, and release installers remain M7 work.

## WebAssembly

Activate the [Emscripten SDK](https://emscripten.org/docs/getting_started/downloads.html)
version in `dependencies.json` (currently 4.0.23). `EMSDK` must name its root; the
preset uses its toolchain file and test-time Node runner.

```sh
cmake --preset web
cmake --build --preset web --parallel 4
ctest --preset web
python3 -m http.server 8000 --directory build/web
```

Open `http://localhost:8000/openciv2.html`. Deploy `openciv2.html`, `openciv2.js`,
`openciv2.wasm`, and `THIRD_PARTY_NOTICES.txt` together, serving WASM as
`application/wasm`. The Catch2 JS/WASM test executable is not a deployment asset.
Do not open the application through `file://`. The shell supplies loading/error
status and Restart; Quit exits to the page instead of closing the browser tab.

SDL callbacks yield to the browser. The build uses C++ exceptions, memory growth,
and a single browser thread. No Asyncify or cross-origin isolation is needed
for this shell. Browser-reserved shortcuts remain accessible through drawn menus
and buttons. Asset preloading, persistent saves, and the network bridge are not
implemented. WASM tests run under Node at test time; CMake does not try to
execute WASM test discovery during configuration.

## Tests and diagnostic runs

```sh
cmake --preset headless-fetch
cmake --build --preset headless-fetch --parallel 4
ctest --preset headless-fetch
cmake --preset sanitize -DOC2_FETCH_DEPENDENCIES=ON
cmake --build --preset sanitize --parallel 4
ctest --preset sanitize
```

`oc2_tests` uses Catch2 for five portable shell/menu cases. `oc2_runtime_tests`
uses Catch2 for SDL event ownership, input routing, modal blocking, fractional
framebuffer density, and context/backend teardown. CTest also exercises startup
and clean initialization failure. Runtime/startup tests use dummy video and
software rendering, without a display server. These are application tests, not
classic simulation conformance fixtures.

```sh
SDL_VIDEO_DRIVER=dummy SDL_RENDER_DRIVER=software \
  ./build/desktop/openciv2 --smoke-test --screenshot /tmp/openciv2.bmp
```

`--smoke-test` uses the drawn menu and exits after three frames. `--screenshot`
saves the next frame as BMP. Native menu integration needs actual desktop checks:
macOS application menu placement, Command keys, Dock Quit and Retina behavior;
Windows Alt/menu selection, Ctrl shortcuts, menu insets and DPI; Linux X11/Wayland,
F10/keyboard navigation, resizing and focus loss. Confirm responsiveness while
tracking native menus before adding gameplay/network deadlines.

## GUI automation

Enable `OC2_GUI_TESTS=ON` or use the `gui-tests` preset. The separate
`oc2_ui_tests` executable contains ImGui Test Engine hooks; production
`openciv2` and `oc2_frontend` link the uninstrumented ImGui library. Seven
scenarios run at 1x, 1.65x, and 2x framebuffer density, using widget paths and
semantic state. The fractional dimming regression is retained.

```sh
ctest --preset gui-tests -L gui
./build/gui-tests/oc2_ui_tests --visible --filter about_dialog
./build/gui-tests/oc2_ui_tests --density 1.65 --filter modal_blocks_background \
  --output build/gui-tests/gui-results/manual
```

JUnit reports and failure captures appear under `gui-results/<density>/`.
An unmatched filter, timeout, or failed scenario returns nonzero. Visible tests
use the OS display scale; synthetic headless densities exercise geometry, not
native DPI event delivery. Assertions stay active in the GUI test engine even
for Release builds. See its upstream
[setup guide](https://github.com/ocornut/imgui_test_engine/wiki/Setting-Up).

ImGui Test Engine has a separate
[license](https://github.com/ocornut/imgui_test_engine/blob/176a41dd07985ccb65287a569047ab93f2c7f5d0/imgui_test_engine/LICENSE.txt).
CMake copies it as `IMGUI_TEST_ENGINE_LICENSE.txt`; fetched Catch2's license is
copied as `CATCH2_LICENSE.txt`. Neither library is linked into the shipped game.

The browser smoke test drives real Chromium, including modal/menu interactions,
resizing, 2x pixel density, and Quit/Restart:

```sh
npm install --no-save --package-lock=false playwright@1.58.2
npx playwright install chromium
node tests/web_smoke.cjs build/web
```

Node/Playwright are test-only dependencies. Screenshots go under
`build/web/screenshots/`. CI reads tool versions from `dependencies.json`.

## Validation scope

The workflow builds macOS, Windows, Linux, and WASM, including a separate headless
matrix and sanitizer job. A configured CI job is not evidence that it ran.
Current merge verification is recorded in the
[implementation plan](implementation-plan.md#current-implementation-status).
Native AppKit/Win32 interactions and real desktop DPI behavior remain platform
checks; dummy-driver and browser tests cannot establish them. No existing test
establishes X02 gameplay or original-peer/file interoperability.
