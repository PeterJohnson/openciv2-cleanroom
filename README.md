# OpenCiv2 Cleanroom

A C#/.NET implementation of Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**, built from behavioral specifications. Avalonia supplies the
desktop and browser UI. The portable core and command-line tools also run without
a display, graphics assets, Lua or SQLite.

**This is not yet a playable game.** The M1 foundations implement classic file
codecs, content tools, state, scoped session clients, setup/load/save workflows,
bounded checkpoints and registered conformance operations. The application
exercises a real session command through Avalonia and a console; optional Lua
uses the same authority and command APIs. M2's rendering and platform development
is [closed with tracked deferrals](docs/m2-closeout.md); full performance/platform
acceptance remains open. M3 is the main workstream for the first playable slice.

## Build and run

Install the pinned **.NET SDK 11.0.100**. Run from the repository root:

```sh
dotnet restore --locked-mode
dotnet build -c Release --no-restore
dotnet test --project tests/OpenCiv2.Base.Tests -c Release --no-build --no-restore
dotnet run --project src/OpenCiv2.Cli -c Release --no-build -- --help
```

For the desktop application:

```sh
dotnet restore solutions/OpenCiv2.App.slnx --locked-mode
dotnet run --project src/OpenCiv2.Desktop -c Release
```

The [build guide](docs/BUILDING.md) covers Windows, macOS, Linux, browser
publication, optional Lua/SQLite, independent CLI tests and formatting.
[Packaging](docs/packaging.md) produces self-contained CLI archives.

## Development

- [Architecture](docs/architecture.md): cleanroom boundary, project dependencies,
  ownership, commands, presentation and networking.
- [Source guide](docs/code-guide.md): projects, entry points and where to add code.
- [Implementation plan](docs/implementation-plan.md): current scope and the next
  specification families, milestone dependencies and acceptance gates.
- [Implementation coverage](docs/testing/coverage.md): current APIs, sources and
  shared/native test coverage.
- [Validation](docs/validation/README.md): executed CI evidence and remaining
  native desktop checks; a successful headless CI job is not desktop acceptance.
- [Application](docs/application.md) and [Lua automation](docs/automation.md):
  current host behavior and bounded scripting contracts.
- [Algorithm specifications](docs/algorithms/README.md), [compatibility (X01)](docs/algorithms/X01-classic-compatibility-contract.md)
  and [conformance (X02)](docs/conformance/README.md): behavioral requirements,
  registered operations, fixtures and explicit evidence gaps.
- [Content](docs/content/README.md), [JSON formats](docs/content/data-format.md),
  [schemas](docs/content/schemas/README.md) and [gameplay data](docs/content/gameplay-format.md):
  data contracts and the standalone-content work still required.
- [Performance](docs/performance/README.md): workload budgets, measured costs and
  reproducible collectors.

Core/CLI, Lua/SQLite and self-contained packages have passed hosted CI on Linux
x64, macOS ARM64 and Windows x64. Browser tests execute in Chromium WebAssembly.
Actual Windows/macOS desktop interaction, shutdown and target-hardware performance
remain pending. Full menus, DPI/IME/accessibility, browser storage, complete
original-peer networking and playable gameplay retain their milestone gates.

Implement gameplay from this repository's specifications. Do not consult or
translate OpenCiv2 or Civ2 reconstruction/decompilation source. External source
links and filenames in specification provenance are historical research records,
not implementation or test inputs. Resolve missing behavior through the
specification contract or independent original-game observations. The designated
`PeterJohnson/openciv2-assets` content is an allowed data dependency.

[MIT license](LICENSE). See [third-party notices](THIRD_PARTY_NOTICES.txt) for
dependencies and publication-specific notices in generated packages.
