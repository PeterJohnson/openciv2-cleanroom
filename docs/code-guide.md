# Reading and extending the source

Open `OpenCiv2.slnx` at the repository root for the headless core, CLI and tests.
Optional solutions in `solutions/` add the desktop application, Lua, SQLite and
performance probes. SDK/runtime/package versions are pinned centrally.

Read the owning behavioral specification and [architecture](architecture.md)
before changing a gameplay operation. The [coverage inventory](testing/coverage.md)
links supported APIs and tests; [implementation milestones](implementation-plan.md)
identify what remains. Contracts describe ownership and partial failure as well
as successful results. Required file/wire layouts are explicit codecs, never
runtime object layouts.

## Project map

Projects live in `src/`; case libraries and executable hosts live in `tests/`.

| Project | Responsibility | Useful starting point |
| --- | --- | --- |
| `OpenCiv2.Base`, `OpenCiv2.IO` | Arithmetic, PCG32/choices, owned values and byte/storage contracts | `Arithmetic.cs`, `Randomness/RandomContext.cs`, `Storage.cs` |
| `OpenCiv2.Contracts` | Immutable client observations, commands and setup values | `Clients.cs`, `Commands.cs` |
| `OpenCiv2.Core` | Live state, map geometry/storage, rules and implemented gameplay leaves | `State/GameState.cs`, `MapStorage.cs`, `Rules/RuleInstaller.cs` |
| `OpenCiv2.Formats` | Classic codecs, content decoding/conversion and versioned checkpoints | `ClassicFiles.cs`, `Content/ContentPack.cs`, `LocalCheckpointCodec.cs` |
| `OpenCiv2.Session` | Authority, clients, continuations, setup/load/save and checkpoint ownership | `SessionRuntime.cs`, `CommandRuntime.cs`, `Checkpoint.cs` |
| `OpenCiv2.Network` | Messages, framing, queues, compression and state images | `Stream.cs`, `SendQueue.cs`, `Sections.cs` |
| `OpenCiv2.Query` | Stored inspection/diffs and observer-filtered live values | `StoredInspection.cs`, `LiveProjection.cs` |
| `OpenCiv2.IO.Native`, `OpenCiv2.Platform.Native` | Native filesystem and classic text adapters | `NativeFiles.cs`, `ClassicStorage.cs`, `ClassicTextFiles.cs` |
| `OpenCiv2.Cli`, `OpenCiv2.Export.Sqlite`, `OpenCiv2.Cli.Sqlite` | CLI composition and optional native export | `Program.cs`, `SqliteSnapshot.cs` |
| `OpenCiv2.Automation` | Optional stock Lua interpreter, scoped player API and resource budgets | `LuaMachine.cs`, `ScriptTask.cs` |
| `OpenCiv2.ViewModels`, `OpenCiv2.Console` | UI-independent player-client model and command parser | `SessionPanel.cs`, `CommandText.cs` |
| `OpenCiv2.App`, `OpenCiv2.Desktop`, `OpenCiv2.Browser` | In-progress production Avalonia integration host | `SeamSession.cs`, `SeamView.cs`, `Program.cs` |

Shared cases are in `tests/OpenCiv2.FoundationCases`; native and browser
hosts execute the same portable cases. Native I/O and optional SQLite have their
own tests. `OpenCiv2.Conformance` and its two hosts exercise the selected X02
fixtures. Build commands and evidence checks are in the
[build guide](BUILDING.md). Public client APIs expose owned values;
mutable Core records and borrowed spans stay behind the session boundary.

## Adding behavior

1. Choose the owning subsystem inside Core, Formats, Network or Session. Keep
   portable algorithms independent of filesystem, UI, database and Lua packages.
2. Put player-visible values and command descriptors in Contracts. Session owns
   authority, continuations and effects; ViewModels, Console and Automation receive
   scoped `IPlayerClient` bindings. Composition roots alone construct mutable worlds.
3. Add meaningful boundary/failure cases to FoundationCases when portable. Register
   them in `FoundationSuite`; native xUnit and browser hosts execute the same cases.
   Keep OS I/O tests in Base.Tests and optional interpreter/database tests separate.
4. Update `docs/testing/coverage.json` and the owning contract. For new X02 behavior,
   follow [conformance registration](conformance/README.md#updating-an-operation).
5. Run affected builds/tests and project-boundary checks from [the build guide](BUILDING.md).
   Rebuild after source/specification edits before accepting an identity-bound report.

Use explicit constructors, small service interfaces and C# XML documentation for
nonobvious ownership, mutation, failure and lifetime rules. Preserve explicit
integer wrapping and sampling laws rather than replacing them based on names.
No reflection-driven command dispatcher, global mutable service locator or generic
ECS is required. Avoid per-frame world copies and one UI control per map tile.

Native interop is limited to the optional stock Lua bridge and SQLite API. Keep
protected native calls, safe-handle lifetimes and resource budgets intact. Tools
under `tools/dotnet/` build dependencies, execute hosts and verify evidence; root
Python tools validate specification/content data independently of the application.
