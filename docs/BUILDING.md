# Building the application

This builds the application shell described in
[application-shell.md](application-shell.md) and the portable
[M1 foundation libraries](m1-foundations.md). Playable gameplay, graphics-pack
loading, full save/wire codecs and the general X02 runner remain plan work.

## Dependencies and presets

Use CMake 3.24+, a C++20 compiler, and Ninja for the supplied presets. macOS
requires the Xcode command-line tools/AppKit; Windows needs a Visual Studio
developer shell for Ninja, or the Visual Studio generator shown below.

[dependencies.json](../dependencies.json) pins source archives and SHA-256 values
for SDL3 3.4.2, Dear ImGui 1.93.0, Catch2 3.8.1, the matching ImGui Test Engine
revision, and the DejaVu 2.37 TrueType data archive. It also records the Emscripten, Playwright and clang-format versions
used by CI.
The optional native analysis exporter pins SQLite 3.54.1; it is configured only
with `OC2_ENABLE_SQLITE_EXPORT=ON`, which defaults OFF.
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
cache settings explicitly. Relevant switches are `OC2_BUILD_GUI`, `OC2_BUILD_TOOLS`,
`OC2_FETCH_DEPENDENCIES`, `OC2_FETCH_SDL`, `OC2_GUI_TESTS`, `OC2_SANITIZERS`, and
standard `BUILD_TESTING`. GUI automation is opt-in and native-only.

For an offline build, supply upstream source trees matching the pins:

```sh
cmake --preset desktop \
  -DFETCHCONTENT_SOURCE_DIR_IMGUI=/path/to/imgui-1.93.0 \
  -DFETCHCONTENT_SOURCE_DIR_CATCH2=/path/to/Catch2-3.8.1 \
  -DFETCHCONTENT_SOURCE_DIR_DEJAVU=/path/to/dejavu-fonts-ttf-2.37
```

Supply `FETCHCONTENT_SOURCE_DIR_SDL3` to build SDL from a local source tree, and
`FETCHCONTENT_SOURCE_DIR_IMGUI_TEST_ENGINE` with `OC2_GUI_TESTS=ON` for offline GUI
tests. A populated build's dependency cache also works with downloads disabled.
GUI builds embed twelve hash-verified DejaVu TrueType faces, then rasterize
glyphs at runtime with ImGui. The extracted font root contains `ttf/` and
`LICENSE`; the build checks these bytes even for a local override. Fonts add no
Python or graphics-pack dependency.
Source overrides for code libraries are explicit trusted developer inputs: URL hashes verify
fetched archives, not the contents of an override directory. Invalid explicit
paths fail rather than falling back to a download. Installed Catch2 must match
3.8.1; installed SDL3 may be a compatible newer version. Record such overrides
when reporting results.

`OC2_BUILD_GUI=OFF` configures without SDL, ImGui, the asset clone, or a display.
The portable formats target uses the vendored, hash-pinned `jart/json.cpp` and its
bundled double-conversion. No download or package-manager setup is needed for
those sources; CMake checks them against `dependencies.json`. Base/game mechanics
remain independent of the JSON library. With tests enabled, Catch2 is the only
additional headless dependency. With
`BUILD_TESTING=OFF` as well, this builds the portable base, game, formats, query
and view-model libraries plus the native `oc2` inspection CLI, using only the
vendored JSON dependency. `OC2_BUILD_TOOLS=OFF` omits the CLI and native I/O library. Native tools
default ON; Emscripten defaults them OFF while retaining the portable query API.
See [inspection commands and output](m1-inspection.md) for a tools-only build.
See [SQLite export](m1-sqlite-export.md) for the optional dependency and schema.

## Pinned CI toolchains and inventories

CI installs CMake **3.31.10** and enforces the versions in `dependencies.json`
with `OC2_VERIFY_TOOLCHAIN=ON`. Linux uses Clang 20.1.8; Windows uses clang-cl
20.1.8 with Visual Studio 2022's SDK/libraries and the Ninja generator; macOS selects Xcode 16.4 / AppleClang
17.0.0.17000013; WASM uses emsdk 4.0.23 / Clang 22.0.0. Runner labels are
`ubuntu-24.04`, `windows-2022`, and `macos-15`, not moving `*-latest` aliases.
Hosted images/OS libraries may still change; compiler drift fails configuration
instead of silently redefining the baseline. The macOS/Windows baseline awaits
actual execution evidence. Normal developer builds retain CMake 3.24+ and other
compatible C++20 compilers; strict verification is opt-in outside CI.

Every configure writes `build-identity.txt` with the actual compiler, CMake,
platform, lock hash, source overrides and build options. It records
`configured-only`, not a successful build/test claim. CI uploads it alongside
CTest JUnit/logs and GUI reports, associated with the workflow's source revision.
[M0 validation](m0-validation.md) records the locally tested scope and gaps.
The CI matrix selects the baseline; presets remain portable developer workflows.

M0's stdlib-only metadata checks need no external checkout or content:

```sh
python3 tools/check_conformance.py
python3 tools/check_assets.py
cmake --build --preset desktop --target inventory-check
```

Use `python` on Windows. CMake adds the metadata target/tests only when Python
3.9+ is available. These checks validate bookkeeping and hashes; they do not run
X02 algorithms or establish gameplay compatibility.

With `BUILD_TESTING=ON` and Python available, CTest also runs `conformance_foundations`:
the [M1 runner](m1-foundations.md) invokes the test-only C++ adapter for
three F01, seven F02 and ten W01 fixtures and writes `conformance-results.json`.
WASM runs the same adapter under Node. This is separate from metadata checks and has no GUI
or asset dependency. Ordinary library builds and Catch2 tests still work with
Python discovery disabled.

The adapter embeds its compiled-input hashes and build identity. The runner
checks `--identity` before executing any fixtures and rejects an old executable
after source changes. Rebuild the adapter before rerunning conformance; supplying
a newer `--build-identity` file cannot substitute for rebuilding.

The full developer Python suite also checks content schemas and formatter/hooks.
Use Python **3.11+**, install the pinned formatter below and the separate schema
dependencies, then run:

```sh
python -m pip install -r tools/requirements-schemas.txt
python tools/check_content_schemas.py
python tools/check_gameplay.py
python -m unittest discover -s tests -p 'test_*.py' -v
```

The [schema guide](content/schemas/README.md) describes document validation and
its semantic limits. These packages are not needed for ordinary builds or the
stdlib-only metadata checks above.

The [M1 stored-save checks](m1-stored-saves.md) add native read-only
`oc2_save_roundtrip INPUT.SAV` comparisons. They currently support stored version
`0x2c`, including optional scenario, multiplayer and event blocks; they do not
perform session loading or replace the planned inspection CLI.

`OC2_ASSETS_DIR` can explicitly name the approved asset source root or an extracted
runtime package in GUI configurations. This enables `asset-check` and requires
Python for that optional check. It does not yet load/copy/install graphics.
Invalid explicit GUI paths fail; headless builds do not inspect the path. See the
[asset contract](content/asset-format.md#cmake-and-runtime-integration) and
[inventory](content/README.md) for exact scope, the graphics/font pins and
remaining layout/text work. The legacy bitmap font caches are not consumed.
No automatic sibling lookup, asset downloads or old-game tools run during builds.

## Formatting and commits

Use **clang-format 20.1.8**, pinned in `dependencies.json`, and Python 3.9+ for
development formatting. [.clang-format](../.clang-format) defines four-space
indentation, a 100-column limit, attached braces and preserved include order.
The formatter covers owned C/C++/Objective-C sources and headers under `src/`,
`include/`, `tests/` and `tools/`, including new untracked files. It excludes
ignored files, other roots, generated builds and downloaded dependencies.
Source and hook line endings are LF on every platform.

An existing matching LLVM installation works. Otherwise, install the pinned
[clang-format package](https://pypi.org/project/clang-format/20.1.8/) in a local
virtual environment:

```sh
python3 -m venv .venv
. .venv/bin/activate
python -m pip install clang-format==20.1.8
git config --local core.hooksPath .githooks
```

On Windows, use `python -m venv .venv` and activate with
`.venv\Scripts\Activate.ps1` in PowerShell; use `python` for the commands below.
Hook setup is once per clone and is not automatic on checkout. If another
`core.hooksPath` or custom pre-commit hook is already configured, integrate
`python tools/format.py --staged` there instead of replacing it.

Before committing:

```sh
python3 tools/format.py          # Format working-tree sources.
python3 tools/format.py --check  # Read-only full check, also run by CI.
git add <intended-files>
python3 tools/format.py --staged # Read-only check of the proposed commit.
git commit
```

The pre-commit hook runs the staged check automatically. It reads source blobs,
style and the version pin from the index, so unstaged edits cannot hide a
formatting error in the proposed commit. Changes to the style, manifest or
formatting script check all indexed sources. The hook never rewrites files or
stages changes; after formatting, review and restage the intended edits, taking
care with partially staged files. Commits with no relevant changes need no
formatter executable. Missing/wrong formatter versions otherwise fail clearly.

The script tries `clang-format` and `clang-format-20` and requires the exact
pinned version. Set `CLANG_FORMAT` or pass `--clang-format /path/to/clang-format`
to select another installation. It performs no downloads.

Configured builds with Python available also provide `format` and `format-check`
targets, for example `cmake --build --preset desktop --target format-check`.
Formatting is an explicit development step, not a source mutation during normal
compilation. Ordinary builds require neither Python nor clang-format.
CI checks formatting and the staged-file/hook regression tests on Linux, macOS
and Windows; a local hook does not replace that check.

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
