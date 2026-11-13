# M0 implementation and validation

M0's repository implementation is complete. Its full platform exit gate remains
open until all pinned native desktop/headless jobs execute successfully. Hosted
runs now pass every job except the macOS desktop runtime input test, described
below. See `native-platform-evidence` in the
[coverage ledger](conformance/coverage.json).

## Delivered boundaries

The existing C++20 target skeleton remains intentionally small: portable
`oc2_viewmodel`, separate SDL frontend/platform adapters, app and Catch2/ImGui
Test Engine targets. Game/session/format libraries are introduced with their first
real M1 operations; M0 does not create empty libraries or invent gameplay.

The [registry and ledger](conformance/README.md) catalog 65 specification families,
two pure F01 operation contracts, two F02 token-state contracts, three existing
specification vectors and 23 required integration families. They distinguish
specification, registration, implementation and execution. No game operation is
claimed implemented or executed. The original specification and fixture files
were not changed.

The [content inventory](content/README.md) records the selected source artwork,
verified release and payload hashes, stock-content gaps and original-access
procedure. The [asset contract](content/asset-format.md) defines the observed pack
index, original font observations, layout-data boundary and `OC2_ASSETS_DIR` behavior. No old-game
code, runtime assets or original executable is included in this change.

## Executed locally — 2026-11-13 UTC

Environment: Linux x86_64, CMake 3.31.10, Ninja 1.13.2, Clang 20.1.8 for native
baseline builds; Python 3.14.4 for metadata/format checks. Archive/source overrides
were explicit local upstream dependencies, with network fetching disabled.
Native GUI used installed SDL 3.4.2; ImGui 1.93.0, Catch2 3.8.1 and the pinned
ImGui Test Engine revision came from local source trees. WASM used emsdk 4.0.23
(Clang 22.0.0), Node 24.19.0 and the pinned SDL/ImGui/Catch2 source trees. These
local installations are developer inputs, not a claim that CI's host images or
all upstream override directories are byte-identical to this machine.

| Check | Observed result |
| --- | --- |
| Fresh native headless build, strict toolchain check | 5 owned Catch2 cases pass; Python discovery disabled; missing `OC2_ASSETS_DIR` ignored; no SDL/ImGui/asset input |
| Fresh native library-only build | Succeeds with tests, GUI and Python discovery disabled; no third-party dependency |
| Fresh Linux desktop Debug build, strict baseline | All 13 CTests pass: 5 portable cases, runtime, startup/safe startup failure, 3 GUI density runs, 2 metadata checks |
| ImGui Test Engine | 7 scenarios at each of 1, 1.65 and 2 density, 21 scenario executions total |
| Reconfigured/rebuilt WASM app and tests, strict baseline | HTML/JS/WASM links; 3 CTests pass (5 Catch2 cases under Node plus 2 host metadata checks) |
| Fresh headless WASM build | Catch2 JS/WASM builds and all 5 cases pass under Node; Python discovery disabled and no graphical dependency |
| Chromium browser smoke | Startup, dialogs, menu input, resizing, HiDPI, Quit and Restart pass using Playwright 1.58.2 |
| Python developer tests | All 21 pass: 7 formatter/index/hook cases and 14 metadata/content cases, including stale evidence, false execution claims, invalid bounds, path/symlink escape and corrupt manifests |
| Formatting | clang-format 20.1.8 check passes for all 19 owned source files |
| Source assets | Explicit 840-file selection and inspected metadata hashes match the recorded baseline |
| Published runtime assets | ZIP hash matches publisher; eight payload hashes, 2,105 atlas index entries, PNG headers/proxy bounds, font signatures and 62 city rectangles verified |
| Configuration rejection | Wrong CMake version, wrong native compiler and invalid explicit GUI asset directory each fail with the intended diagnostic |
| Workflow files | YAML parses; installer catalog contains Linux LLVM 20.1.8; hosted native jobs have not run |

The WASM compiler emits existing upstream Catch2 `__COUNTER__` extension warnings
under `-Wpedantic`; they do not fail the build. This change does not suppress
warnings or alter vendored dependencies. No renderer/font decoding, game
algorithms, original interoperability or full semantic UI coverage is claimed.

## Reproduction and retained reports

Use the [build guide](BUILDING.md) for library overrides and commands. For the
native baseline, select `clang`/`clang++` 20.1.8 explicitly and CMake 3.31.10:

```sh
cmake --preset headless -DOC2_VERIFY_TOOLCHAIN=ON \
  -DCMAKE_C_COMPILER=clang-20 -DCMAKE_CXX_COMPILER=clang++-20 \
  -DFETCHCONTENT_SOURCE_DIR_CATCH2=/path/to/Catch2-3.8.1 \
  -DCMAKE_DISABLE_FIND_PACKAGE_Python3=ON -DOC2_ASSETS_DIR=/missing/assets
cmake --build --preset headless
ctest --preset headless --output-junit test-results.xml
python3 tools/check_conformance.py
python3 tools/check_assets.py
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

Local build directories used were `build/m0-headless`, `build/m0-library`,
`build/m0-desktop`, `build/web` and `build/m0-web-headless`. Their configuration
identities and CTest logs/JUnit outputs remain local and ignored; this document
records bounded results, not portable goldens. CI uploads `build-identity.txt`,
CTest JUnit/logs and GUI reports under the workflow's source revision. The identity
file explicitly says `configured-only`; inspect the accompanying execution
reports before treating it as a passing build.

The chosen hosted images expose the required platform SDKs; versions are selected
explicitly and checked instead of following their defaults. Reference inventories:
[macOS 15](https://github.com/actions/runner-images/blob/main/images/macos/macos-15-Readme.md),
[Windows 2022](https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md),
and [LLVM installer releases](https://github.com/KyleMayes/install-llvm-action/blob/v2/assets.json).
These sources inform configuration, not the pending platform test result.

Close M0's platform gate by running native desktop/headless jobs on macOS and
Windows, reviewing SDK/compiler identities and attaching passing reports. Keep
graphics/layout reading, runtime font platform checks, playable rules/text, audio and external original-game access
as named M1/M2/M5–M7 follow-ups; their absence is not hidden behind M0 test counts.

## TrueType follow-up — 2026-11-13 UTC

The subsequent font change replaces the proposed bitmap reader with pinned
DejaVu 2.37 TrueType inputs and runtime ImGui rasterization. The initial M0 table
above records the earlier package inspection; bitmap signatures are no longer
validated as a rendering contract. The upstream eight-file package inventory is
retained, but schema 2 excludes both bitmap caches from selected runtime content.

The DejaVu archive and twelve face/license hashes were verified independently.
Linux desktop CTests and all three ImGui density runs pass with the new fonts;
the added runtime test exercises all twelve faces, two sizes, three densities,
Latin/accented/Greek glyphs, SDL atlas updates and context destruction/recreation.
The WASM shell builds and its Node/metadata CTests and Chromium smoke pass.
All 23 Python checks pass; a deliberately corrupted local font override fails
configuration before embedding. Headless tests pass with an intentionally missing
font path and Python discovery disabled, confirming no font acquisition/linkage. The frontend uses 5,349,360 bytes of embedded
TTF data before executable/transfer compression; it does not require a new runtime
filesystem path or an original bitmap-cache parser. The architecture, M2 plan and
content ledger now track remaining font platform/text work rather than an unknown
bitmap format. macOS/Windows execution remains pending.

## First GitHub Actions run — 2026-11-13 UTC

[Run 43489505500](https://github.com/PeterJohnson/openciv2-cleanroom/actions/runs/43489505500)
tested revision `c0aa93e`. Six jobs passed: Linux/macOS metadata, Linux/macOS
headless, Linux sanitizers and WASM (including Node tests and Chromium smoke).
The five failed jobs have three shared setup causes:

- Windows metadata, headless and desktop jobs failed specification byte hashes
  because Git converted Markdown to CRLF. Both Windows builds and all their C++
  tests passed, including desktop startup, runtime fonts and all three ImGui
  density runs using SDL's dummy video/software render drivers.
- Linux desktop configuration stopped because SDL requires `libxtst-dev` for
  the enabled X11 XTest support.
- macOS desktop generation lacked Objective-C++ compile/archive rules in SDL's
  directory scope because the parent enabled that language after loading SDL.

The follow-up fixes enforce LF for text checkouts (retaining binary detection),
install the missing Linux package, and enable Objective-C/Objective-C++ in the
parent GUI scope before loading dependencies. Expected specification/fixture
hashes and compiler/dependency versions are unchanged. A temporary Git checkout
with `core.autocrlf=true` reproduces the original hash failure and passes with the
new attributes, including a binary-preservation check. An isolated CMake language
scope reproduction produces the same three missing-rule errors with the old
ordering and configures/builds with the new ordering on Linux; it does not verify
the macOS SDK or native menu implementation.

Local follow-up validation passes all 73 Python tests, schema/gameplay-data
checks, formatting and workflow YAML parsing. A fresh strict Clang 20.1.8 /
CMake 3.31.10 Linux desktop build in `build/ci-fix-desktop` uses pinned SDL 3.4.2
from source with X11/XTest and Wayland enabled; all 13 CTests pass, including the
three GUI density runs. Source overrides and asset/font inputs match the local
setup above. This does not substitute for the hosted Ubuntu package-install step.

Passing hosted reports after these fixes are still required to close the platform
gate. The successful Windows GUI tests exercise the dummy video driver; they do
not establish native-display or native-menu interaction coverage.

## macOS runtime input follow-up — 2026-11-13 UTC

[Run 43491903539](https://github.com/PeterJohnson/openciv2-cleanroom/actions/runs/43491903539)
tested revision `09ab9a4`. Ten of eleven jobs passed, confirming the checkout and
Linux dependency fixes. macOS desktop also configured and built successfully;
12 of its 13 CTests passed, including all three GUI density runs. The remaining
runtime assertion injected physical Control+G, although ImGui's macOS behavior
maps the application's primary shortcut to Command+G.

The SDL input test now exercises both `ConfigMacOSXBehaviors` settings on every
host and selects the corresponding physical modifier: `SDL_KMOD_GUI` for Command
or `SDL_KMOD_CTRL` for Control. It also checks that the other modifier cannot
trigger the action. Existing application shortcut behavior is unchanged.
Enabling macOS behavior reproduced the hosted failure on Linux before the fix;
after the fix the rebuilt runtime suite passes all three cases and 3,069
assertions. Formatting and inventory checks pass. The hosted macOS rerun remains
pending; this input-mode regression does not verify native AppKit interaction.
