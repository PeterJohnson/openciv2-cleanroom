# OpenCiv2 Cleanroom

A new implementation of classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**, built from behavioral algorithm specifications.

The compatibility target preserves classic gameplay, AI decisions and map
generation, with original save/map/scenario interchange, TCP/IP multiplayer
with original-game peers, and hotseat play. Presentation and internal
representation may differ within the compatibility contract. Equivalent internal
work may be reordered while preserving behaviorally significant dependencies;
X01 and X02 define that convention and its conformance checks.

- [Algorithm specifications](docs/algorithms/README.md): subsystem catalog,
  ownership, writing conventions and source evidence.
- [Compatibility contract (X01)](docs/algorithms/X01-classic-compatibility-contract.md):
  required behavior, supported inputs and permitted differences.
- [Conformance (X02)](docs/algorithms/X02-whole-game-conformance.md): fixtures,
  comparison rules, integration cases and evidence gaps.
- [Architecture](docs/architecture.md): cleanroom boundary, C++20/CMake libraries,
  embedded session service and player controllers, SDL3/ImGui presentation,
  platform menus, assets, persistence and classic networking.
- [Tools and automation](docs/architecture.md#5-inspection-tools-console-and-user-automation):
  planned save inspection CLI, in-game console and Lua player automation over
  shared portable queries and commands.
- [Implementation plan](docs/implementation-plan.md): phased delivery, dependencies,
  test gates and specification coverage.
- [Building and testing](docs/BUILDING.md): desktop, headless, WASM, and GUI automation.
- [Conformance inventory](docs/conformance/README.md): validated operation/state
  registrations, fixture provenance, coverage rows and unresolved integration gaps.
- [Content inventory](docs/content/README.md): verified asset release, data contracts,
  stock-content gaps and external original-game comparison procedure.
- [Standalone JSON content](docs/content/data-format.md): semantic rules, names,
  runtime text and events, with separate classic compatibility mappings/adapters.
- [Content JSON Schemas](docs/content/schemas/README.md): document validation,
  examples and remaining semantic checks.
- [Built-in gameplay data](docs/content/gameplay-format.md): six classic-default
  components, customization boundary and offline validation.
- [Classic text-file inventory](docs/content/text-resources.md): per-file runtime
  mappings, documentation-only files and explicit legacy/companion-tool limits.
- [Application shell](docs/application-shell.md): implemented UI/platform boundaries
  and current limitations.

The initial C++20/CMake/SDL3 shell provides native macOS/Windows menus, ImGui
Linux/browser menus and dialogs, and a development view with illustrative tiles. Drawn text uses pinned DejaVu
TrueType fonts rasterized at runtime; headless builds need no font data.
It has no game simulation, asset-pack or game-content loader, save inspection,
command console or Lua runtime yet. JSON content formats and the classic text
compatibility boundary are specified; a complete standalone content set remains
part of the implementation plan. Catch2 covers the portable shell and SDL
integration; ImGui Test Engine covers the drawn interface.

To build with explicitly enabled pinned dependency downloads (CMake 3.24+,
Ninja, and a C++20 compiler required):

```sh
cmake --preset desktop-fetch
cmake --build --preset desktop-fetch --parallel 4
ctest --preset desktop-fetch
```

Use `headless-fetch` for tests without SDL/ImGui, or `gui-tests` to include GUI
automation. Offline source overrides and platform prerequisites are described
in the build guide. The [current status](docs/implementation-plan.md#current-implementation-status)
distinguishes implemented infrastructure from remaining milestone gates.

Before committing C++ or Objective-C changes, run `python3 tools/format.py`,
review and stage the result. Enable the staged-format check once per clone with
`git config --local core.hooksPath .githooks`. See
[formatting setup](docs/BUILDING.md#formatting-and-commits) for the pinned tool,
Windows commands and existing-hook integration. CI also checks formatting.

Implementation code must be written from this repository's specifications, not
copied or derived from OpenCiv2 or Civ2 decompilation/reconstruction source. The
original game may be used for comparisons and compatibility checks. The designated
`PeterJohnson/openciv2-assets` runtime content is an allowed data dependency.

These documents define requirements; they do not establish completed
interoperability or whole-game validation.

[MIT license](LICENSE). The specification collection's
[source provenance](docs/algorithms/README.md#source-provenance) identifies the
external reference repositories.
