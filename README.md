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
  SDL3/ImGui presentation, platform menus, assets, persistence and networking.
- [Tools and automation](docs/architecture.md#5-inspection-tools-console-and-user-automation):
  planned save inspection CLI, in-game console and Lua player automation over
  shared portable queries and commands.
- [Implementation plan](docs/implementation-plan.md): phased delivery, dependencies,
  test gates and specification coverage.
- [Building and testing](docs/BUILDING.md): desktop, headless, WASM, and GUI automation.
- [Application shell](docs/application-shell.md): implemented UI/platform boundaries
  and current limitations.

The initial C++20/CMake/SDL3 shell provides native macOS/Windows menus, ImGui
Linux/browser menus and dialogs, and a development view with illustrative tiles.
It has no game simulation, asset-pack loader, save inspection, command console
or Lua runtime yet. Catch2 covers the portable shell and SDL integration; ImGui
Test Engine covers the drawn interface.

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

Implementation code must be written from this repository's specifications, not
copied or derived from OpenCiv2 or Civ2 decompilation/reconstruction source. The
original game may be used for comparisons and compatibility checks. The designated
`PeterJohnson/openciv2-assets` runtime content is an allowed data dependency.

These documents define requirements; they do not establish completed
interoperability or whole-game validation.

[MIT license](LICENSE). The specification collection's
[source provenance](docs/algorithms/README.md#source-provenance) identifies the
external reference repositories.
