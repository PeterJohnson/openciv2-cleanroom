# C++ modules

Owned C++ interfaces live in `.cppm` files. The module name mirrors the path:
`src/formats/classic_rules.cppm` provides `openciv.formats.classic_rules`.
Test fixtures and tool interfaces use `openciv.tests.*` and `openciv.tools.*`.
Implementation files import their interfaces and use `import std` for standard
library declarations. There is no parallel set of owned C++ headers.

Interfaces use `export extern "C++"` declaration blocks, the
[Clang ABI-preserving module style](https://releases.llvm.org/20.1.0/tools/clang/docs/StandardCPlusPlusModules.html#export-extern-c-style).
Declarations live directly in the module interface; these are not wrappers around
old headers. Global-module attachment preserves type identity for the existing
cross-interface forward declarations and ordinary implementation translation
units. A type still has one definition. Imports control visibility and CMake
orders the compiled interfaces. Do not redeclare a type owned by a dependency's
named module; import its defining interface instead.

Keep module boundaries aligned with architectural APIs. Classic and native
format adapters remain separate; client/value interfaces do not expose engine
state. `export import` retains dependencies forming an interface's public API;
implementation-only dependencies belong in `.cpp` files. Do not introduce an
umbrella module that bypasses these boundaries. `tools/check_boundaries.py`
follows transitive imports and includes and rejects portable filesystem access,
including when the standard library is imported as a whole.

`cmake/Modules.cmake` explicitly assigns interfaces to existing libraries and
private executable fixture/tool file sets. Add new interfaces there. All owned
C++ targets scan imports; upstream dependency source builds retain their own
settings. CMake 4.5 manages the standard-library module and matching BMI variants.
emsdk 7's Clang 24 defaults to reduced BMIs and crashes while generating imported
aggregate destructors in `leader_io_tests` and `save_aux_io_tests`. Web builds use
`-fno-modules-reduced-bmi` for full interfaces, including the standard library;
no tests or module imports are disabled. Re-evaluate this setting with SDK upgrades.
Keep compiler options, standard-library headers, module metadata and runtime
consistent; never copy BMIs between build trees or toolchains.

## Textual boundaries

Macros are not exported by modules. Include the required macro headers at the
use site, before imports (or in an interface's global module fragment). This
includes Catch2/Test Engine macros and C standard-library macros such as
`FE_TONEAREST`. SDL and SQLite remain C APIs with textual headers. Do not replace
C/Objective-C declarations or preprocessor configuration with module imports.

The macOS adapter uses a narrow C ABI in `platform/menu_macos_bridge.h` between
an Objective-C AppKit implementation and the C++ menu implementation. ARC owns
the Cocoa objects; the C++ RAII handle restores the previous menus on destruction.
No C++ objects or standard-library types cross this bridge. Generated gameplay
and conformance data use textual literal fragments; gameplay consumers import
the module that owns the embedded data. Generated font C++ also uses imports.

## Dependencies

ImGui 1.93.0 uses the upstream-recommended
[stripe2933/imgui-module bindings](https://github.com/stripe2933/imgui-module),
revision `9cf21888dc3e139b9b371b7bfeda91148da718a1`, hash-pinned in
`dependencies.json`. Presentation imports `imgui`, `imgui_internal` and
`imgui_impl_sdl3`. A small owned adapter exposes the SDL renderer backend and
version-check macro, for which these bindings provide no module.
Production and Test Engine builds compile separate module interfaces with their
matching ImGui configuration; they never link both variants into one executable.

Catch2 3.8.1, ImGui Test Engine, and jart/json.cpp have no supplied named-module
interfaces in the pinned packages. Their required headers remain at the adapter
or macro boundary; upstream code is not rewritten. The JSON adapter explicitly
exports the dependency type used in its public data. SDL3 and SQLite are C
libraries. DejaVu is a data dependency.

## Toolchains and validation

The CI matrix builds the full application/tests on Linux, Windows, macOS and
WebAssembly. The focused arithmetic and `import std` checks also remain available
as `oc2_module_tests` and `oc2_module_std_tests`; the latter defaults on with
testing. All owned production targets require `import std` regardless of that
test option. Unsupported toolchains fail compilation instead of selecting a
header fallback.

On macOS, select Homebrew LLVM and its matching standard library explicitly:

```sh
llvm_path="$(brew --prefix llvm@20)"
cmake -S . -B build/desktop -G Ninja \
  -DCMAKE_C_COMPILER="$llvm_path/bin/clang" \
  -DCMAKE_CXX_COMPILER="$llvm_path/bin/clang++" \
  -DCMAKE_OBJC_COMPILER="$llvm_path/bin/clang" \
  -DCMAKE_OSX_SYSROOT="$(xcrun --show-sdk-path)" \
  -DCMAKE_CXX_STDLIB_MODULES_JSON="$llvm_path/lib/c++/libc++.modules.json" \
  -DCMAKE_CXX_FLAGS="-isystem $llvm_path/include/c++/v1 -stdlib=libc++" \
  -DCMAKE_EXE_LINKER_FLAGS="-L$llvm_path/lib/c++ -Wl,-rpath,$llvm_path/lib/c++"
```

Use separate build directories for each toolchain. Activate emsdk before the web
preset so its compiler and Node runner are selected. The project sets emsdk 7's
module opt-in before `project()` loads the toolchain.
