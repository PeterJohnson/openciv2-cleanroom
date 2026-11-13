# M0 implementation and validation

M0's repository implementation is complete. Its full platform exit gate remains
open until the pinned macOS and Windows jobs execute successfully. This Linux
checkout has no configured Git remote; CI definitions and compiler availability
checks are not execution evidence. See `native-platform-evidence` in the
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
index, font limits, layout-data boundary and `OC2_ASSETS_DIR` behavior. No old-game
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
font/layout reading, playable rules/text, audio and external original-game access
as named M1/M2/M5–M7 follow-ups; their absence is not hidden behind M0 test counts.
