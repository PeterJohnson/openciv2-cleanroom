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
| `OpenCiv2.Formats` | Classic codecs, content decoding/conversion, portable graphics readers and versioned checkpoints | `ClassicFiles.cs`, `Content/ContentPack.cs`, `LocalCheckpointCodec.cs` |
| `OpenCiv2.Session` | Authority, clients, continuations, setup/load/save and checkpoint ownership | `SessionRuntime.cs`, `CommandRuntime.cs`, `Checkpoint.cs` |
| `OpenCiv2.Network` | Messages, framing, queues, compression and state images | `Stream.cs`, `SendQueue.cs`, `Sections.cs` |
| `OpenCiv2.Query` | Stored inspection/diffs and observer-filtered live entity/map values | `StoredInspection.cs`, `LiveProjection.cs` |
| `OpenCiv2.IO.Native`, `OpenCiv2.Platform.Native` | Native filesystem and classic text adapters | `NativeFiles.cs`, `ClassicStorage.cs`, `ClassicTextFiles.cs` |
| `OpenCiv2.Cli`, `OpenCiv2.Export.Sqlite`, `OpenCiv2.Cli.Sqlite` | CLI composition and optional native export | `Program.cs`, `SqliteSnapshot.cs` |
| `OpenCiv2.Automation` | Optional managed Lua-CSharp interpreter, scoped player API and resource budgets | `LuaMachine.cs`, `ScriptTask.cs` |
| `OpenCiv2.ViewModels`, `OpenCiv2.Console` | UI-independent player-client model and command parser | `SessionPanel.cs`, `CommandText.cs` |
| `OpenCiv2.MapRendering` | Skia map drawing, scene indexing, camera, minimap geometry and presentation timelines | `MapPainter.cs`, `MapScene.cs`, `MapRendererSession.cs` |
| `OpenCiv2.App`, `OpenCiv2.Desktop`, `OpenCiv2.Browser` | Avalonia integration, authored map/gallery, controls and input | `SeamSession.cs`, `SeamView.cs`, `MapControl.cs`, `Program.cs` |

Shared cases are in `tests/OpenCiv2.FoundationCases`; native and browser
hosts execute the same portable cases. Native I/O and optional SQLite have their
own tests. `OpenCiv2.Conformance` and its two hosts exercise the selected X02
fixtures. Build commands and evidence checks are in the
[build guide](BUILDING.md). Public client APIs expose owned values;
mutable Core records and borrowed spans stay behind the session boundary.

`OpenCiv2.MapRendering` is an optional repository-local library over Base,
Contracts, Formats and SkiaSharp. It has no Avalonia or Session dependency;
Formats still brings Core transitively for its existing content/codec ownership.
App uses its public scene, camera, display, timeline and drawing APIs. Texture,
atlas, cache and GPU experiment types remain internal, with friend access for
MapProbe and MapBrowser acceptance. App retains inspection text, gallery fixtures,
settings/persistence and Avalonia controls. MapProbe remains a native integration
host over both projects. MapBrowser links the production browser entry point and
the shared matrix, with read-only result and fresh frame exports. Its performance-only exports
install the shared `ScopedMapPerformanceWorld` fixture; real gallery input then
drives production scoped projection, scene binding and drawing. BrowserContent
supplies asynchronous HTTP streams to Formats' pinned graphics/font readers.

The host owns each painter's lifetime and lends a current Skia canvas/context to
`MapRendererSession.Draw` inside its drawing lease. Observer replacement, render
epochs, fallback/retry and cache disposal retain their existing ownership. The
library never creates an Avalonia context or loads a gameplay session.

The two desktop renderer options share `MapRendererOptions`; the native host
owns preference I/O in `RendererPreferences`. `MapRendererSession` owns per-window
fallback/retry and context replacement. `MapGpuComposition` owns the shared
context cache policy without owning Avalonia's context. The GPU implementation is in `OpenCiv2.MapRendering/MapGpuComposition.cs`.
`MapPainter` remains the common selector/drawing implementation. The
[prototype report](validation/m2-gpu-composition-prototype.md) records its
historical environment switches, matched benchmarks and correctness limits; the software
compositor remains the default.
The [follow-up experiments](validation/m2-gpu-experiments.md) add sprite batching,
deferred GPU preparation, retained pictures, instrumented GL replay and native
input-to-window-image measurements. `GpuUploadChecks` verifies CPU raster updates
through actual GPU readback, covering the aliased-worker snapshot invalidation.
The [staged optimization report](validation/m2-gpu-optimization.md) covers
`MapForegroundCommands` (ordered per-frame commands), shared atlas masks,
`MapObservationDelta` (disclosed snapshot changes), and the experimental
`MapPainter.GpuRegions` / ground topology caches. Ordered commands are now
included in the opt-in GPU renderer after the
[browser measurements](validation/m2-browser-performance.md). The remaining
prototypes stay feature-gated; failed visual or performance experiments do not
change their supported defaults.

## Adding behavior

### Readable implementation and documentation

Production C# uses braces for control-flow bodies and puts each statement on its
own line. Keep short, pure forwarding members and simple computed properties as
expressions when they are easy to scan. Give mutations, validation sequences and
resource handling ordinary blocks. Use blank lines to separate steps with distinct
purposes rather than compressing several steps onto one line.

Aim for lines no longer than 120 characters. Wrap long parameter lists and calls
with one item per line; break conditions at logical operators and keep related
arithmetic together. Prefer a named intermediate value when it explains a concept,
but preserve evaluation order, checked/wrapping arithmetic and short-circuiting.
Long literal data, URLs and format definitions may justify an exception.
`.editorconfig` enforces braces and separate statements in `src/`; its line-length
setting is an editor guide, not a wrapping guarantee from `dotnet format`.

Use XML summaries on types and operations to explain their role and contract.
Document ownership, mutation, lifetime, failure behavior and meaningful return
values, especially at public boundaries. Add parameter/return details when the
names and types do not convey them. Private helpers benefit from documentation
when they encode a policy or invariant; avoid comments that just repeat a name.
Implementation comments explain why ordering, bounds checks, retained state or
apparently unusual arithmetic are required. Preserve specification references and
state the implemented scope without implying that a whole subsystem is complete.

### Implementation checklist

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

Game/runtime native interop is limited to the optional SQLite API.
Lua automation uses the managed Lua-CSharp package.
The independent Linux map GPU test probe additionally uses system EGL/OpenGL to
create its test context; production hosts continue to use Avalonia's context. Keep
protected native calls, safe-handle lifetimes and resource budgets intact. Tools
under `tools/dotnet/` build dependencies, execute hosts and verify evidence; root
Python tools validate specification/content data independently of the application.

Renderer integration and its Linux-only validation are recorded in
[the renderer-options report](validation/m2-renderer-options.md).
