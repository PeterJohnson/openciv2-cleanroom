# Foundational module experiment (historical)

This records the experiment before the [full module migration](modules.md).
The tested experiment was merged into main at `7bfdad7`.

The experiment tested C++23 named modules after the header-based main baseline
`2044ada` passed [all 12 CI jobs](https://github.com/PeterJohnson/openciv2-cleanroom/actions/runs/43685032207).
It is a feasibility experiment, not a project-wide module migration.

`openciv.base.arithmetic` exports the existing F01 arithmetic API using the
[Clang export-using transition pattern](https://releases.llvm.org/20.1.0/tools/clang/docs/StandardCPlusPlusModules.html#export-using-style).
The declarations remain in the global module, so ordinary header consumers and
importing consumers share types and the compiled `oc2_base` implementation.
The importer tests constexpr evaluation, constrained constructors/operators,
compiled functions, exception identity, and calls across the header/module boundary.
It executes through CTest, including Node for WebAssembly.

With `BUILD_TESTING=ON`, CMake builds the `CXX_MODULES` file set and enables
dependency scanning only for the provider and its consumer. Tools-only builds
remain available without module scanning. Formatting and include-boundary checks
also cover `.cppm` files. A broader migration must extend boundary validation to
resolve project imports; this first module includes the checked existing API and
has no project imports of its own.

## Toolchains

Linux retains Clang 20.1.8 and Windows retains clang-cl 20.1.8 with MSVC libraries.
WebAssembly now uses emsdk 7.0.0 / Clang 24.0.0. CMake 4.5.0 supplies clang-cl dependency
scanning. Its AppleClang compiler configuration does not provide scanning, so
the experiment uses Homebrew LLVM 20.1.8 on macOS, including Objective-C++, with
the Xcode 16.4 SDK. Strict compiler version verification remains enabled in CI.
This macOS toolchain change is confined to the experiment branch.

See [CMake's module support matrix](https://cmake.org/cmake/help/latest/manual/cmake-cxxmodules.7.html).
Named modules and `import std` have different toolchain requirements. Standard
library module testing followed the named-module success on every platform;
no unsupported platform is silently skipped.

## Evidence

Local validation passed 537 native CTests, 123 Python tests, all 57 conformance
fixtures, and the Node/WebAssembly module test. All 12 jobs, including the
named-module test on all four platforms, passed in
[CI at `8dfb62e`](https://github.com/PeterJohnson/openciv2-cleanroom/actions/runs/43685834331).

`OC2_TEST_IMPORT_STD=ON` adds a consumer that imports `std` and the foundation
module, uses standard containers/algorithms/strings, and catches an exception
from the header-based library. A separate **Standard library module probe**
workflow runs this on all four platforms with the same compiler pins.
Its failures are separate from the named-module build matrix. The option defaults
OFF; unsupported toolchains fail the probe instead of skipping it.

The [initial hosted probe at `2ae1840`](https://github.com/PeterJohnson/openciv2-cleanroom/actions/runs/43686565528)
used each platform's default standard library configuration:

| Platform | Result |
| --- | --- |
| Windows | Compiles, links and runs with clang-cl 20.1.8 and MSVC's standard library |
| Linux | Cannot discover `libstdc++.modules.json` with the default CI library |
| macOS | Cannot discover `libc++.modules.json` through Clang's default search path |
| WebAssembly | Emscripten configuration leaves `CMAKE_CXX_STDLIB_MODULES_JSON` empty |

The local consumer passes with Clang 20.1.8 and libstdc++ 16, so Linux's default
CI result does not establish a compiler limitation. A local emsdk 4.0.23 retry
with explicit metadata also fails: the SDK's `lib/libc++.modules.json` points
to absent `share/libc++/v1/std.cppm` and `std.compat.cppm` sources.

The Linux probe now uses an `ubuntu:26.04` container on the Ubuntu 24.04 hosted
runner, with the distribution's `g++` development package supplying libstdc++ 15
and its module sources. Clang remains pinned to 20.1.8. The experiment branch's
Linux native, metadata and sanitizer jobs use this same container. WebAssembly
retains the Ubuntu 24.04 host because it uses emsdk's own standard library.

The macOS probe now explicitly selects Homebrew's installed module metadata
under `lib/c++`, along with its matching libc++ headers and runtime. This uses
the layout and linkage tested by [Homebrew's LLVM formula](https://github.com/Homebrew/homebrew-core/blob/master/Formula/l/llvm%4020.rb).
The regular named-module matrix still uses the platform's normal standard
library configuration; it does not depend on this `import std` setup.

The [follow-up probe at `b700c7a`](https://github.com/PeterJohnson/openciv2-cleanroom/actions/runs/43687229562)
passes on Linux, Windows and macOS. WebAssembly still fails before compiling
the consumer because its standard-library module is not available in the SDK.

Named modules are feasible with the experiment's macOS compiler change.

The emsdk 4.0.23 failures above predate upstream's
[standard-library module support](https://github.com/emscripten-core/emscripten/pull/27065),
merged in June 2026. The experiment now pins emsdk 7.0.0, whose SDK supplies
the module sources and metadata. The WebAssembly probe enables the SDK's
`CMAKE_CXX_MODULE_STD` toolchain opt-in. It uses the SDK directly, with no
copied module sources, generated metadata, or standard-library patches.
The local emsdk 7.0.0 `module_std` test passes with that setup. The SDK upgrade
also exposed the default 64 KiB stack limit in unoptimized save decoding; all
WebAssembly targets now share the conformance adapter's existing 1 MiB budget,
with explicit overflow checks in Debug builds.
All 10 local WebAssembly Debug CTests pass with emsdk 7.0.0, including the full
portable unit suite, both module tests, metadata, and conformance checks.
