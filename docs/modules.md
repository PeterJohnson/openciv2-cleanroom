# C++ modules

Owned C++ interfaces live in `.cppm` files. The module name mirrors the path:
`src/formats/classic_rules.cppm` provides `openciv.formats.classic_rules`.
Test fixtures and tool interfaces use `openciv.tests.*` and `openciv.tools.*`.
All owned C++ code uses `import std`; there is no parallel set of owned C++
headers. Each declaration belongs to one named module. Export public declarations
with `export namespace` or `export { ... }`, and import a dependency's defining
module instead of redeclaring its types.

Prefer one `.cppm` for a small, cohesive module. Put its public declarations first
and definitions afterwards. A single-unit module may place implementation details
after `module :private;`; such a module must have no other module units. Keep
separate `.cpp` implementation units for larger algorithms, platform-specific
implementations, or dependencies that should stay out of the interface. These
units begin with `module openciv.<owner>;`, which implicitly imports the primary
interface. Merely importing the interface from an ordinary translation unit does
not give definitions the correct module ownership. Multi-unit modules must not
use a private module fragment.

Keep module boundaries aligned with architectural APIs. Classic and native
format adapters remain separate; client/value interfaces do not expose engine
state. `export import` exposes dependencies forming an interface's public API;
plain `import` supplies implementation dependencies without re-exporting names.
Extract shared values into a lower-level module when both sides need them. Keep
mutually dependent classes in one module when they share an invariant, as with
`LocalStacks` and `StackOperations`. Do not work around cycles with foreign
forward declarations, linkage blocks, or an engine-wide umbrella export.

Shared runtime implementation details live in the internal partition
`openciv.session.runtime:state`, listed in a private CMake module file set. Its
implementation units use `import :state;`. The partition can import the primary
interface; the primary interface must not import the partition back. A client's
copyable handle owns a noncopyable connection interface, whose runtime
implementation retains weak session/lease references and validates authority on
every operation. Neither the handle nor its connection contract imports engine
state. Only the public handle may import the internal connection contract at the
player boundary; frontend code must use the handle.

`tools/check_boundaries.py` rejects module import cycles and follows transitive
imports (including relative partitions) and includes across architecture
boundaries. It also rejects portable filesystem access when the standard library
is imported as a whole.

`cmake/Modules.cmake` explicitly assigns interfaces to existing libraries and
private implementation partitions and executable fixture/tool file sets. Add new
interfaces there. All owned C++ targets scan imports; upstream dependency source
builds retain their own settings. CMake 4.5 manages the standard-library module and matching BMI variants.
emsdk 7's Clang 24 defaults to reduced BMIs and crashes while generating imported
aggregate destructors in `leader_io_tests` and `save_aux_io_tests`. Web builds use
`-fno-modules-reduced-bmi` for full interfaces, including the standard library;
no tests or module imports are disabled. Re-evaluate this setting with SDK upgrades.
Keep compiler options, standard-library headers, module metadata and runtime
consistent; never copy BMIs between build trees or toolchains.

## Textual boundaries

Macros are not exported by modules. Include the required macro headers at the
use site. In any named module unit, textual dependency headers belong in the
global module fragment (`module;` before the headers, then the module declaration).
This includes Catch2/Test Engine macros and C standard-library macros such as
`FE_TONEAREST`. SDL and SQLite remain C APIs with textual headers. Do not replace
C/Objective-C declarations or preprocessor configuration with module imports.

Do not wrap owned declarations in `extern "C++"`: that attaches them to the global
module and defeats named-module ownership. Keep linkage specifications only at
real foreign ABI boundaries. Prefer including the foreign header in the global
module fragment and exporting a `using` declaration when its type is public;
never invent a second module-owned declaration of that foreign type. Any required
`extern "C"` or `extern "C++"` block has unindented contents, enforced by
`IndentExternBlock: NoIndent` in `.clang-format`.

The macOS adapter uses a narrow C ABI in `platform/menu_macos_bridge.h` between
an Objective-C AppKit implementation and the C++ menu implementation. ARC owns
the Cocoa objects; the C++ RAII handle restores the previous menus on destruction.
No C++ objects or standard-library types cross this bridge. Generated gameplay
and conformance data use textual literal fragments; gameplay consumers import
the module that owns the embedded data. Generated font C++ is an implementation
unit of `openciv.ui.font_data`.

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
