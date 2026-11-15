# Foundational module experiment

This branch tests C++23 named modules after the header-based main baseline
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

Linux retains Clang 20.1.8, Windows retains clang-cl 20.1.8 with MSVC libraries,
and WebAssembly retains emsdk 4.0.23. CMake 4.5.0 supplies clang-cl dependency
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
workflow runs this on all four platforms with the same compiler/stdlib defaults.
Its failures are separate from the named-module build matrix. The option defaults
OFF; unsupported toolchains fail the probe instead of skipping it.

The local `import std` consumer passes with Clang 20.1.8 and libstdc++ 16.
The default emsdk 4.0.23 configuration fails with an empty
`CMAKE_CXX_STDLIB_MODULES_JSON`. Hosted probe results are pending.
