# Implementation plan

The implementation is C#/.NET with Avalonia desktop and browser hosts. Develop
from the repository specifications and [architecture](architecture.md), preserving
the cleanroom boundary. A specification describes required behavior; it does not
establish that its entire family has been implemented or validated.

## Milestones and dependencies

| Milestone | Delivery | Current state |
| --- | --- | --- |
| M0 | Reproducible build, specification/content inventories and evidence boundaries | Implemented with pinned .NET tooling and cross-platform CI |
| M1 | Finite portable foundations, classic codecs, content/inspection CLI, session ownership and bounded checkpoints | Implemented; current test families and exclusions are in the coverage and validation guides |
| M2 | Complete map renderer, Avalonia interaction, platform menus/storage and content installation | Linux and WASM/WebGL authored rendering evidence exists; performance, remaining platform/services and live-provider joins retain their separate gates |
| M3 | First playable local game slice | Planned; depends on M1 and the required M2 surfaces |
| M4 | Complete single-player mechanics, AI and expanded automation | Planned |
| M5 | Scenarios, editors, complete UI and hotseat | Planned |
| M6 | Original-peer multiplayer and browser transport | Planned |
| M7 | Complete conformance evidence and distributable releases | Planned |

Platform checks grow with each slice. CI passes core/CLI, Lua/SQLite and package
checks on Linux x64, macOS ARM64 and Windows x64; Chromium executes portable,
Lua and application-input tests. Native Windows/macOS desktop input/shutdown,
OS interruption behavior and target-hardware performance remain open. See
[validation](validation/README.md) and the [platform checklist](testing/platforms.md).

## Current implementation status

The [source guide](code-guide.md) maps existing projects and APIs. The
[coverage inventory](testing/coverage.md) maps implemented families to specifications
and tests. The root solution builds headless libraries and the CLI; optional
solutions build Avalonia, automation, SQLite export and performance probes.

The application host exposes one bounded production command over an authored
world. It proves the scoped-client, console and asynchronous interaction path.
It is not a playable game or a completed renderer. The optional Lua-CSharp adapter
exercises the same session APIs with explicit resource and authority limits.

## M0 — Build and evidence infrastructure

Preserve locked builds, project dependency checks, source-identity verification,
metadata/content validators, publication notices and reproducible package tests.
The [build guide](BUILDING.md) is the human entry point; CI executes the same
commands. Test upgrades to SDK/runtime/packages together. Original-game evidence
remains distinct from authored contract vectors and implementation regression tests.

## M1 — Portable foundations

Maintain the finite implemented surface rather than recursively implementing all
reachable gameplay children. Supported services include explicit arithmetic and
PCG32 contexts; entity/world state; classic binary/text codecs; content validation,
resolution and conversion; stored inspection/diff/SQLite; network primitives;
scoped clients and continuations; setup/load/save; and bounded local checkpoints.
The X02 adapter executes fifteen registered operations and 57 selected fixtures.

Key contracts are [foundations](m1-foundations.md), [state/session ownership](m1-session-foundations.md),
[commands](m1-command-contract.md), [checkpoint profile](m1-checkpoint.md),
[stored saves](m1-stored-saves.md), [content conversion](m1-content-conversion.md),
[inspection](m1-inspection.md) and [network primitives](m1-network-foundations.md).
The [finite scope](m1-closeout.md) records exclusions. Complete terrain generation,
civilization creation, balancing, acceleration, event execution, turn processing
and multiplayer sessions remain the responsibilities of later owning milestones.

### State representation boundary

Game status, tile features, unit/city/civilization status, rules/options,
relationships, scenario/event settings, unit abilities and regional presence
now use named logical values. Membership sets cover civilizations, technologies,
improvements, worked positions and neighboring regions; specialists and their
count have separate fields. File bit positions and opaque values live in the
adapters, with lifetime checks and explicit configuration copy/reset handling.
Lossless stored codecs and live import/edit/export are tested separately.
Do not reintroduce raw masks or `BitN` members when extending these APIs.

`MapFeatures` and `MapPoint` are shared between Core and Contracts. The feature
name includes occupancy and environmental changes; observer output excludes
remembered unit presence. Cosmic parameters are named properties, with ordering
and normalization in Formats. Rule definitions and live leaders own their text;
parallel name bindings and the shared text pool have been removed. Label rebuilds
and rule reloads are checked for independent text retention. All paired map
locations now use `MapPoint`, including entity positions, destinations, goals,
starts, views and events. Text in Core/Contracts uses C# strings; classic byte
encoding, termination, widths and password scrambling belong to Formats.
Unicode names, coordinate/carrier conversion and adapter round trips have regression
coverage. The authored fresh-game conformance projection observes eight logical
passwords, separately from F06 encoding tests.
Nullable slot references replace signed link wrappers. F06 retains raw negative
sentinels in adapter metadata while core absence uses null. Shared entity, rule,
city and map limits now drive bounds, allocation and domain inline-array extents;
preference-field counts, password image sizes and history/AI capacities remain
with their respective owners.

One specification terminology gap remains: P02 calls the `0x8000` unit state
AwaitingInput, while F02/U03 name it Automated and F02 assigns Waiting to
`0x4000`. The projection preserves P02's existing marker using `Automated`;
resolve the naming conflict before changing that visible behavior.

### Compatibility scope for every milestone

Implement each owning specification's admitted domain, meaningful failure prefixes,
ordering, lifecycle and sampling laws. Use explicit codecs for classic bytes and
protocol values. Public projections are owned and observer-filtered; mutations
enter through scoped Session commands. A GUI callback or script does not bypass
authority or expose mutable records. New features require native/browser behavior
checks and independent provenance for compatibility claims.

## M2 — Prove the graphical and platform boundaries

Owners: P01–P05 foundations, P10 service boundary; F03/P03 content lookup.

The Riverbend slice establishes rendering infrastructure: scoped W01/W03
observations, pinned atlas/fonts, Skia drawing, pan/zoom, picking and the shared
local stack interaction on Linux. Its [validation record](validation/m2-map-slice.md)
retains that finite scope and simulated 4K/5K results. It does not complete the
map renderer. Pinned browser content and WASM/WebGL execution are now recorded
in the [browser map acceptance report](validation/m2-browser-map.md).
Interactive performance and the full platform/services gate below remain open.

The shared Skia renderer now lives in the optional `OpenCiv2.MapRendering`
library. App retains Avalonia controls, input, authored galleries and session
composition. This dependency extraction preserves the existing renderer scope;
it does not close visual, performance or platform acceptance gaps.

Map completeness is [P02 MV01–MV30](algorithms/P02-map-rendering-and-minimap.md#minimum-map-visual-feature-set),
with the mandatory [acceptance matrix](testing/map-rendering.md), tracked in the
[visual coverage ledger](validation/map-visual-coverage.md). The stock theme uses
the [exact atlas mapping](content/map-assets.md). All thirty rows, their enumerated
selectors and combined cases are required; a loaded pack or representative scene
does not close a row. Small implementation slices are progress checkpoints, not
permission to stop the complete-renderer task early.

The Linux showcase now has one authored board per MV row plus the large mixed
world. Stock and classic replacement selectors run through the same painter as
Riverbend. Pixel comparisons cover every terrain and special resource,
all coast, edge-blend, relief, river and mouth variants, agricultural
combinations, remembered route updates, and river/road/rail/resource crossings.
The authored Riverbend map also demonstrates a continuous river with a
tributary, a road crossing and a wrapped gulf mouth, with resource art chosen
from the projected W01 facts.
They also cover localized world changes with label reflow and whole-pixel
terrain scrolling with foreground recomposition. Isolated Linux input exercised both
1× and 2× backing density, including same-process windows with independent
cameras and a window moved between virtual monitors. The local selection,
route, notification, overview, camera, detail-band and effect checks pass,
including native navigation during effects and decision completion under
reduced motion.
The Linux application CI step verifies the archive pins and runs the software
matrix, native gallery, scoped map input and fresh-frame comparisons for
startup and recovery.
Exact unit, stack, health, order and city comparisons cover the stock theme,
native-only replacements and classic sheets. City annotations remain visible
when the body selector is withheld, and dirty redraw clears a custom stack
shield's overhang.
Flat and wrapped map-region replays preserve admitted pixels and inspection
after changes outside the region. Native pointer and keyboard inspection reach
the same worker and city facts, including withheld and unavailable work.
Configured role changes update flags, shields, aircraft, annotations, overlays
and overview in place. Ownership edges match fresh frames after clipped
repaints, and W01 radius overlays match the flat and wrapped city footprints.
Crowded captions use a documented size floor and complete text elements.
The pinned font fallback, numerical badges and overlapping translucent layers
match independent compositions at both Linux backing densities.
The live theme selector replaces terrain, foreground, textures and overview
resources together. Recovery keeps the selected target and camera and shows
an explicit limited frame when resources cannot be recreated.
Current evidence and outstanding combinations are
recorded in the [visual coverage ledger](validation/map-visual-coverage.md).
Parallel chunk rasterization shortened the large overview transition. Bounded
viewport-sized cache caps retain the active backing tiers during repeated
4K and 5K zoom changes. The renderer prepares missing ground textures in
bounded batches from the admitted images needed by the frame. Large warm
redraws, first-time high-tier construction and cold resource replacement still
exceed the interactive budgets. Parallel foreground replay reduces warm
half-world pan time at 4K and 5K. Padded redraw clips preserve the serial
pixels across parallel band edges and around dirty-world updates.
Vectorized Paeth and Adler calculations and grouped CRC checks reduce
cold PNG preparation. The scoped renderer still validates each image
before preparing textures.
Full redraws prepare missing visible body images in parallel. A camera move
can expose additional bodies within an existing backing tier. The cache
uses its resident keys to find those misses. Ground workers use resident
textures to draw their chunks directly when the batch fits the cache.
Compatible ground sprites use a temporary atlas with padded alpha bounds.
Workers keep world draw order. On Linux x64, integer-aligned sprites can blend
one-to-one into an opaque chunk with the pinned Skia rounding rule. The
portable Native AOT build uses SSE2 and AVX2 uses wider lanes where available.
The regular Skia atlas path handles other layouts and formats.
Double-size chunks also use aligned packing in parallel batches of two or
three when the textures qualify. In software X11 zoom trials the signed
Native AOT build meets the 4K p95 and p99 budgets and the 5K p99 budget.
The 5K p95 remains open.
At the half-size terrain tier, exactly registered sprites can be sampled
once per build and then use the same aligned compositor. The full Skia
sampling path handles other registrations. Vectorized PNG row reconstruction
preserves the exact decoded atlas pixels and reduces cold decode work. Bulk
inflation and checksum validation allow in-place row compaction in one bounded
buffer.
Cold full draws can prepare visible unit and city textures while building
ground chunks. The simulated 4K and 5K replacement trials now meet the
250 ms p95 budget. Large redraws of a retained viewport can replay ground and
foreground in the same worker bands. Backing regions of at least eight
million pixels can use up to twelve bands with independent edge repairs.
When the bands also paint ground, they clear their own rows and initialize
the repair strips without copying the previous background. The final native
texture reload workload met the 250 ms p95 target at 1080p, 4K and 5K under
X11 software rendering.
The process-cold desktop workload measures through the first map draw after
admission, with separate timings for content validation, authored observation
creation and scene binding. It compares the native window's map pixels with a
fresh serial frame at each tested detail level. Keep process startup, resource
reload and physical presentation timings separate.
The signed Linux Native AOT publish meets the 1080p first-frame budget at full
detail, half-world scale and overview in the measured X11 software trials.
Straight-RGBA sprite tints now use SSE2 in portable x64 builds and share their
immutable bitmap with the texture cache. Desktop painter setup and bounded
CPU texture preparation now overlap platform and window setup after content
admission. Large viewports prepare visible unit and city textures during
first layout and admit them for the matching draw generation. Accelerated
virtual-display startup meets 250 ms p95 at all three detail levels in the
1080p, 4K and 5K trials. Physical presentation timing remains pending.
Large first frames replay ground and foreground into the same worker bands.
The first eligible pan prepares the separate terrain-scroll raster. Combined
replay clears only the background outside guaranteed opaque ground coverage.
Large full draws share one scratch surface between seam repair and terrain
scroll. Viewport replacement and resource recovery release those backings.
Separated dirty chunk misses prepare in bounded batches, and large damage
patches preserve the viewport outside the patch. Repeated edits in a banded
view can retain one immutable 4 MiB terrain atlas within the ground cache cap.
At a settled camera, the viewport updates its composed terrain by changed
chunk and uses it to initialize the foreground bands. Near sampling ties
it uses the full ordered replay. Camera changes and resource replacement
release the settled terrain. Flat and wrapped 1080p dirty-world runs meet the
combined p95 and p99 budgets on Radeon virtual Xwayland. The 4K and 5K
update-and-draw p95 budgets remain open.
Texture reloads at 1080p, 4K and 5K meet the 250 ms draw budget on that
accelerated virtual output. The 5K pan, zoom and world-change trials still
exceed the warm draw p95 budget. Physical presentation remains open.
Inward zooms on large desktop viewports can prepare the next unit and city
texture tier in bounded background batches. The 4× texture crossing is shorter
in paired native diagnostics. The full continuous zoom runs meet the draw
p99 budget at all three sizes. Draw p95 exceeds the budget at 4K and 5K.
Dirty-world receipts now measure authored update
construction and production scene binding alongside rendering. Same-layout
publication can advance while the renderer is busy. The render generation
guards canvas submission, and observer replacement still releases old
resources synchronously. The scene indexes full object records only for
occupied cells. On the isolated Radeon display, 5K dirty binding now takes
1.17–1.25 ms p95, with the combined p95 still at 30.30–31.70 ms.
Native startup receipts include framework initialization, font and renderer
setup, draw scheduling and first-map-draw time. The receipts capture admission
start through draw completion and the time spent joining background work.
Independently clipped dirty patches matched fresh 5K pixels with full-width
ground and exceeded the cost of the full redraw.

The opt-in [GPU experiments](validation/m2-gpu-experiments.md) reuse the same
art, selectors and foreground ordering and compare batching, preparation offload,
retained content, cache budgets and pipelining at 1080p, 4K and 5K. They exposed
and repaired stale GPU uploads after aliased CPU viewport writes. The first
prototype's performance conclusion and earlier warm hardware-Xwayland timings
using that path are superseded as evidence of correctly updated frames.
Software-only and independently read-back cold-frame measurements retain their
stated scope. CPU composition remains the default. The desktop now offers a saved,
startup-only experimental GPU option with controlled CPU fallback and context
cache ownership; see [integration evidence](validation/m2-renderer-options.md).
That integration's other-platform tests were deferred. The subsequent
[browser performance study](validation/m2-browser-performance.md) compares
Software2D/CPU, WebGL/CPU and WebGL/GPU at 1080p, 4K and 5K. Ordered foreground
commands now join the supported opt-in GPU configuration: three sustained 1080p
pan runs improve p95 from 13.6–14.1 ms to 10.5–11.9 ms with exact paired
endpoints. Large zoom/dirty updates and dense overviews still miss budget.
The [zoom-stall investigation](validation/m2-browser-stalls.md) traces the
multi-second browser frames to synchronous terrain construction at detail/backing
transitions and tests serial atlas batching without increasing the cache budget.
The [browser GPU default validation](validation/m2-browser-gpu-default.md) covers
broader native/WASM navigation, art, density and lifetime cases. Native gates
pass, but browser pixel failures block promotion: CPU remains the default and
terrain offload remains experimental. Full M2 visual, performance and platform
acceptance remains open.

- Complete all MV01–MV30 capabilities in the production desktop/browser renderer:
  terrain/coasts/relief, every feature/network/resource, all supported unit/city
  art and annotations, ownership/status, knowledge edges, overlays, overview,
  effects, camera, labels, clipping and recovery. Deliver a reproducible authored
  scene gallery and a mixed large map using the same production renderer as the
  actual application. Preserve visible results and target identity at full,
  reduced and overview detail. Generic placeholders are not completed coverage.
- Extend owned observations with the permitted facts each visual needs, including
  huts, compound improvements, health/orders, city appearance/status, color roles,
  routes, work and effects. Integrate existing production facts through scoped
  clients now. Exercise missing producers with explicitly authored observations;
  track their W/U/C/P owners and M3–M5 joins separately. Do not infer hidden world
  state, synthesize game rules, or require playable combat/trade to test drawing.
- Complete stock and map-facing native/classic scenario content selection,
  including authored BMP/GIF sheet overrides, exact bindings, registration,
  transparency and F03/P03 precedence. The browser now stages and asynchronously
  admits the pinned graphics/fonts and selected art; the shared matrix and actual
  WebGL gallery/input driver supply execution evidence. Keep non-map
  screens/media/editor formats in their own scope.
- Implement and test incremental redraw against fresh output for every layer.
  Satisfy the [map interaction workloads and budgets](performance/map-rendering.md):
  full/unchanged frames, actual cached panning, continuous zoom, dirty world
  changes and replacement/recovery, including half-world 4K/5K views. Record
  complete frame work, input latency, allocations, GC and cache/resource lifetime.
  Fast unchanged blits or selection-only updates cannot substitute for navigation.
- Use the pinned DejaVu Sans/Serif/Mono TrueType faces and runtime glyph
  rasterization through the Avalonia text stack. Exclude the asset package's
  historical bitmap font caches. Extend font roles/measurement to custom dialogs;
  verify DPI/zoom, glyph fallback, size/cache lifetime, native/WASM rendering and
  required text coverage. Font metrics and window geometry must not select
  simulation work; do not infer full shaping from glyph availability.
- Implement the single action/menu model with macOS NativeMenu and
  in-window Avalonia menus on Windows/Linux/web, with optional
  Linux global-menu export. Route native and drawn selections to the
  same semantic commands, with P05 availability and execution rules distinct.
- Implement dialog lifecycle and stable testable custom hit regions. Exercise
  keyboard/focus routing, double-dispatch prevention, accept/cancel, and a target
  changed while the dialog is open.
- Add platform storage/time adapters and a browser import/export/persistence
  flow. Check high DPI, window resizing, menu insets, atlas/texture limits,
  browser startup/download costs, and case-independent classic resource lookup.
- Keep a pending interaction responsive to fake network traffic and deadlines.
  Test real native menu tracking rather than assuming the ordinary application
  dispatcher keeps running inside platform menu loops. Test desktop file dialogs and
  asynchronous browser file selection through their respective adapters.
- Introduce silent media and fake-clock services; verify that unavailable audio
  or skipped cosmetics cannot suppress semantic completion.

**Renderer functional gate:** all MV01–MV30 rows pass the acceptance matrix on
the production native and browser paths, with semantic assertions, visual review,
input/lifecycle cases and reproducible source/content-bound evidence. Exact
art-key validation and aggregate test counts are not renderer acceptance. Live
producer joins may remain explicitly assigned to later milestones; required
rendering capabilities and available backend tests may not. Report unavailable
OS/hardware execution separately without calling it passed.

**Renderer performance gate:** run the prescribed changing-camera/world workloads
and pass the fixed budgets on measured target hardware. Software/offscreen
correctness measurements remain separately labeled. Measured failures require
resolution; unmeasured targets remain pending rather than weakening the budgets.

**Full M2 exit gate:** both renderer gates and the remaining services pass; the
same shell and content run on macOS, Windows, Linux, and in a
browser. Menu selections produce the expected action once. Avalonia and browser
automation can operate the custom dialog; real desktops validate native menus,
IME, accessibility and mixed-DPI/fullscreen transitions. Headless tests still
reference no graphical library. Resolve the comparison's browser dropdown issue.
Any renderer or native-loop issue has a resolved approach before full UI work.

## M3 — Deliver the first playable slice

Owners: F04/F05 subset, W01/W03/W04, U01–U04, C01–C06, V02/V03, P01–P06.

Use a small authored map and rules fixture so this slice does not depend on
finished W02 generation or a full stock content bundle. Implement real rules
for the chosen domain; keep unsupported mechanics visibly unavailable.

- Select and move a unit, respect movement/zone/visibility rules, activate orders,
  found a city, assign workers, choose production/research, and end a turn.
- Compose an embedded single-player runtime with lifecycle control and a human
  participant binding. Route frontend, console and scripted test inputs through
  its client API, including scoped queries and interaction replies. The frontend
  must not own or directly mutate game state. Run the same slice headlessly with
  transport disabled; no separate server process or loopback connection is needed.
- Render the world and minimap from observer-filtered models. Open a city dialog
  whose controls invoke the same tested economy/production operations used by
  the headless session.
- Grow F04's explicit phases and F05 setup around these mechanics. Exercise
  retained state and resume gates, not just a fresh-process happy path.
  The mode adapter owns phase progression; controllers supply admitted decisions
  rather than driving upkeep or independently advancing turns.
- Save and reload the slice through F06. Continue after reload and compare its
  specified transformations. Keep replay checkpoints separate from classic saves.
- Add the portable console parser and an Avalonia console panel with help, queries,
  completion, history and structured command results. Expose the slice's normal
  player actions through the same registry/dispatch as menus, including pending
  interactions and replies. Verify typed input cannot leak into map shortcuts.
- Add player-scoped city/unit/known-map queries and semantic event subscriptions
  with explicit ordering and delivery cursors. Filter query results, completion,
  events and errors by the session's observer context. Keep developer/editor
  diagnostics separate from the normal player's API.
- Enable explicit loaded-state CLI analysis for the implemented F06 domain in
  an isolated session with declared rules, entry state and RNG/services. Label
  load transformations and partial failure separately from the stored view.
- Extend optional SQLite export to that loaded view and selected headless
  session snapshots. Capture immutable records at declared boundaries with
  revision and observer metadata; export them without mutating state or consuming
  RNG. Compare snapshots across turns using explicit identities and test that
  player-scoped exports, including diagnostics, cannot reveal hidden data.
- Add interface seams for combat, diplomacy, AI, events, and network authority;
  no placeholder child counts as a completed integrated operation.

**Exit gate:** an automated headless and graphical script completes multiple
turns with a city and units, saves, reloads, and continues. Key state/results
agree between frontends. Cover changed/deleted targets, fog privacy, cancellation,
and the first G03/G05/G08/G19/G20 joins. This is a bounded playable prototype,
not complete single-player compatibility. Direct session calls, console commands
and GUI actions agree for the supported player operations, including rejection,
pending decisions and partial effects. Queries leave state and RNG unchanged;
fogged information and stale identities cannot escape through console results
or completion. Loaded-state inspection is reproducible for declared inputs.
Local gameplay and pending decisions work with transport disabled on native and
WASM builds. Session replacement invalidates old bindings and queued client
inputs without leaking an earlier game's view or duplicating a completed action.

## M4 — Complete single-player mechanics and AI

Owners: all F/W/U/C/V/A mechanics, with S02 and presentation hooks integrated
as their callers require them.

Implement in dependency groups, with real parent/child integration in each:

1. Complete W02 generation and F05 setup/start placement; test all supported
   topology/size/custom-rule variants and starting another game in one process.
   Track X02's W02 original-target reconciliation gap explicitly.
2. Complete city economy, happiness, growth/support, buildings/wonders, trade,
   research, government, and full F04 city/civilization/global turn passes.
3. Complete combat, capture/revolt, civilization elimination/civil war, diplomacy,
   espionage, transport, air operations, nuclear effects, pollution/warming,
   huts, and barbarians. Preserve ownership, event, and network publication hooks.
4. Implement A01 assessment/orchestration and A02–A08 policies against those
   mechanics. Preserve score formulas, scan consequences, scratch retention,
   candidate retries and tie rules. Do not substitute a generic strategy AI.
   Associate policies with participants but execute them as engine operations
   at the specified mode/phase boundaries. Test mutating assessment/production
   advice, including human callers; preserve AI-specific admission and knowledge
   rules. A controller abstraction must not force snapshot-only decisions, human
   menu checks, per-AI threads or network connections.
5. Complete spaceships, scoring, victory, retirement, history and Hall of Fame;
   connect semantic reports and end-game choices.

Use controlled-choice cases for exact conditional behavior and same-policy
replay for complete runs. Long AI runs and generated-map invariants catch
integration defects but do not independently establish original behavior.

**Exit gate:** a complete human/AI game reaches supported end conditions without
supplied gameplay children. Full-turn snapshots and G01/G04–G12 integration
families have concrete cases, including custom rules and failure variants.
Any unclosed evidence gap remains scoped and reported rather than hidden by
successful playthroughs.

### Initial Lua automation slice

Owners: the extension API, with P05 player dispatch, W03 query privacy, F04 event
boundaries and each command's existing mechanic. This work depends on M3's
contracts, not on the completion of every classic AI policy.

- Extend the optional `OpenCiv2.Automation` integration; pin the Lua-CSharp
  runtime and binding API for native/WASM through central NuGet versions and locks. Keep
  basic console and tools builds independent of the interpreter.
- Bind permitted queries, events, commands and correlated results. Implement
  an example production policy using an authored fixture: observe own-city
  production completion, query current choices, submit a normal choice, wait
  for its result. Do not replace A02 or change automated opponents' decisions.
  Attach automation to the existing human participant context and verify that
  it neither changes human-control flags nor gains classic AI capabilities.
- Implement task start/pause/cancel and waiting at declared session boundaries,
  stable event ordering, bounded queues/output and task diagnostics. Bound
  instructions, memory and host calls, including infinite-loop and allocation
  failures, without blocking protocol/deadline service. Preserve committed
  effects and normal interaction defaults when tasks stop.
- Expose a restricted library/capability set with explicit script selection;
  block direct state writes and implicit file/process/network/native-module
  access. Test permission and observer boundaries independently of the GUI.
- Record emitted commands for gameplay replay without executing scripts again.
  Specify explicit versioned task state and script identity for later resume;
  keep Lua VM/coroutine state out of classic save and wire formats. Provide a
  script-private RNG only with recorded policy/state, separate from game RNG.

**Automation exit gate:** the same example policy runs headlessly, in the game
and in WASM, issuing the same command sequence for the same ordered observations.
Compare its outcomes with equivalent manual commands. Cover rejected commands,
changed/deleted targets, script errors after a completed command, runaway code,
memory limits, cancellation and pending interactions. M4's initial scripts may
be session-local; saved task restoration and hotseat isolation close in M5.

## M5 — Complete scenarios, editors, UI, and hotseat

Owners: S01–S04, N04, P01–P10; F03/F06 and the actual mutation owners.

- Complete scenario setup, authored resources, native JSON events and classic
  event grammar into the same S02 execution model: one-shot state, fixed action
  order, reentry and load/save continuation. Exercise real mechanics for reactive
  events and random-turn laws. Native event object key order must not reorder
  actions or move Consumed publication before effects.
- Supply complete redistributable native rules/city names and all runtime text
  families in the [per-file inventory](content/text-resources.md), with provenance.
  Implement semantic JSON messages/inputs/defaults, labels, named help/facts and
  production advice, menu action trees, encyclopedia, scenario-ID briefings/credits
  and council dialogue. Keep slots/source addresses in the compatibility descriptor
  alongside their classic adapters. Register every reachable required resource
  and test standalone startup without original files, five-component locale
  selection, dynamic advice keys, all credits modes and final-alternative briefing
  lookup. Exercise council accepted-state retention, same-line continuations,
  opinion/seen-category gates and the three-chapter limit. Keep documentation-only
  files out of runtime input selection and report unclassified legacy inputs.
  Partial format examples do not satisfy this playable-content gate.
- Complete world/scenario/rules/asset editor operations, apply/cancel behavior,
  and classic exports. Load exported maps/scenarios/assets in the original game;
  a cleanroom import of its own output is insufficient.
- Complete advisors/reports/history, diplomacy/chat presentation, Civilopedia
  and technology links, council/throne choices, and meaningful media flows.
  Implement P01–P10's concise semantic contracts, not their original procedural
  reference. Design navigation, prompts, sorting and drawing freely with a
  familiar overall game feel. Preserve original configuration/scenario decoding.
  Move required calculations into explicit preparation/refresh operations and
  keep drawing read-only. Apply P06/P07/V08's layout-independent repairs, with
  no old viewport/font compatibility state. Test different layouts, page sizes,
  navigation and repaint counts against the same game outcomes.
- Implement N04 roster, passwords, handoff, per-player views, save/resume and
  concealment. Test cached map/dialog/report information across two handoffs.
  Keep one runtime/world and switch the frontend's participant binding only at
  the prescribed handoff points. Revoke outgoing input access before exposing
  the incoming view; test queued input and replies tagged with an old binding.
  Preserve N04's distinct active human set, roster, actor and resume transitions.
- Extend concealment to console history, output/completion caches and script
  observations. Suspend outgoing-player tasks and detach their subscriptions;
  resume only under that player's restored authority with current target checks.
- Implement explicit automation checkpoint/sidecar state, schema/version and
  script-hash checks, event cursors and pending-command reconciliation. Verify
  that resuming does not duplicate a command already accepted or completed.
  Missing/mismatched metadata leaves automation stopped while classic saves
  still load normally. Provide separately enabled editor commands through the
  same descriptors without granting those capabilities to player scripts.
- Add sound from the asset repo when available. Complete supported authored
  media decoding independently of stock audio availability; test fallback,
  completion, skip/cancel, and incoming events during playback.

**Exit gate:** G13–G15 and G19–G22 have integrated coverage with custom content,
changed-owner/deleted targets, timeout/reply cases, discarded unsubmitted drafts
and retained already accepted effects after cancel. Original interchange works
for the advertised scenario/editor domain. Qualitative play review checks the
familiar map/city/turn/advisor feel, not original dialog/navigation replication.
GUI tests exercise semantic outcomes and custom controls; desktop smoke tests
cover both native menus and browser tests cover persistence/import/export.
Scripted and console-driven play pass hotseat concealment and save/resume tests,
including absent sidecars, changed scripts and interrupted pending work.

## M6 — Complete multiplayer and the browser transport path

Owners: N01–N05, with F04/F06, simulation, scenario, and P05 interaction joins.

- Finish native TCP/UDP adapters and discovery, lobby/admission, player versus
  connection identities, password/setup exchanges, and both host roles.
- Complete sequential and simultaneous turns, per-operation authority, state
  sections/diffs/checksums, locks, deferred inputs/popups, barriers and clocks.
  Apply received outcomes exactly once and never reroll them.
  Implement these paths behind the session mode/authority adapter, preserving
  N03's distinct host/client loops. Keep controller and transport identities
  separate; map to classic fields without a mandatory protocol extension.
  Prove requests execute only on the designated peer and resolved results do
  not reenter player-command execution. The logical service boundary does not
  imply that the host owns every simulation operation.
- Complete save/resume, running join/reconnect, disconnect recovery, and host
  transfer while operations are pending. Test both migration directions and
  failure prefixes, including required replies after dialog failure/timeout.
- Run multiple cleanroom peers under a deterministic fake transport, then live
  original/cleanroom mixed sessions in both host roles. Replay captured transcripts
  for regression; retain live runs as a distinct interoperability requirement.
  Give each simulated peer its own runtime and process context; exercise binding
  and pending-interaction changes during disconnect/reconnect and host transfer.
  No peer independently runs F04's single-player loop as a multiplayer substitute.
- Exercise local console/script commands with original peers in both host roles.
  Verify ordinary N02 authority, request/result routing and exactly-once effects
  with no new wire fields or peer runtime requirement. Test stale authority,
  disconnect/reconnect, host transfer and task suspension/resume; received
  resolved outcomes must never cause a script to apply or reroll them again.
- Build the browser bridge with explicit endpoint/session access and native
  TCP listeners/UDP discovery. Prove transport address/routing semantics, bridge
  loss, browser suspension, and host migration. Publish the bridge deployment
  and reachability requirements with the web build.

**Exit gate:** G16–G18 pass against the original target for sequential and
simultaneous play, random operations, join/resume, discovery, and host transfer
in both directions. Complete the same browser-backed cases before advertising
full browser multiplayer. Native support can be released with a clearly scoped
local-play web preview if bridge evidence is still incomplete.

## M7 — Close conformance and package releases

- Complete the X02 G01–G23 ledger across the advertised nominal domain. Give
  each unresolved retained read, specification ambiguity, original-run gap,
  or restricted feature an explicit disposition and evidence needed for closure.
  Keep optional legacy transports and original-pixel fidelity separate.
- Run bidirectional save/map/scenario interchange with absent/stale replay
  metadata, real mixed-peer sessions, and native/WASM replay consistency.
- Run sanitizer, parser/codec/protocol fuzz, partial-I/O, resource-failure,
  long-session, browser memory/performance, and packaging smoke tests appropriate
  to the implemented domain. Ensure diagnostics preserve committed effects and
  cannot manufacture success or drop required replies.
- Produce desktop bundles/packages and web artifacts with pinned content,
  checksums, notices, dependency inventory, documented save locations, and
  capability limits. Verify clean installation without a development checkout.
- Package the native inspection CLI and document output/API versions, exit
  statuses, console commands and example player scripts. Exercise tools-only,
  scripting-disabled and headless scripting configurations. Include script
  import/export and task-state persistence in browser packaging tests; do not
  bundle Lua into targets that do not use it.
- Document the optional native SQLite exporter, analysis schema and sample
  cross-snapshot queries. Verify enabled/disabled export packaging, source and
  access provenance, and opening an export with an independent SQLite client.
  Keep SQLite out of core, ordinary web and non-export tool packages; analysis
  databases are not supported save files or replay checkpoints.
- Publish bounded test reports with specification/fixture/policy/content hashes,
  original executable identity where used, exact tested platforms, and remaining
  gaps. Never infer whole-game conformance from test counts alone.

**Exit gate:** the release's stated feature and platform claims match the
evidence. A full X01 compatibility claim requires closing its required gaps;
an earlier release must name its narrower supported scope.

## Coverage map

This routes every specification family into implementation work. It supplements,
and does not replace, X02's operation-level coverage ledger.

| Specifications | Main delivery milestones |
| --- | --- |
| F01–F03 | M1 foundations; consuming-rule variants through M3–M7 |
| F04–F05 | M3 turn/setup slice; M4 completion; M5/M6 mode integration |
| F06 | M1 codecs; M3 playable round trip; M5/M6 scenario/session coverage |
| W01–W06 | M1 geometry; M3 visibility/work; M4 complete world behavior |
| U01–U07 | M3 lifecycle/movement/orders; M4 complete unit/combat behavior |
| C01–C09 | M3 city slice; M4 complete economy/trade/capture |
| V01–V08 | M3 basic government/research; M4 full civilization/outcome behavior |
| A01–A08 | M4, with real mechanics and later scenario/network variants |
| S01–S04 | Foundations/hooks in M1/M4; full scenario/editor domain in M5 |
| N01–N03, N05 | M1/M2 protocol experiments; ongoing hooks; M6 mixed peers |
| N04 | M5 hotseat and mode adaptation |
| P01–P03 | M2 complete map-rendering capabilities, interactions and map-facing content; M3–M5 live mechanics/editor joins and wider content |
| P04–P05 | M2 map text, input, shell and dialogs; M3 live player-command joins; M5 remaining screens and interactions |
| P06–P10 | M3 city UI; M4 caller integration; M5 reports/media/content |
| X01–X02 | M0 boundary; M1 runner; every milestone; M7 claim audit |

## Immediate implementation backlog

1. Keep every MV01–MV30 row in the [map acceptance matrix](testing/map-rendering.md)
   current as rendering changes. The [WASM/WebGL functional gate](validation/m2-browser-map.md)
   now passes with pinned content, stock/native/classic bindings, the authored
   gallery and exact presentation comparisons. Extend native evidence to the
   remaining platforms; retain the explicit live-provider joins and their owners.
2. Close [interactive map performance](performance/map-rendering.md) on available
   target hardware and complete browser startup/download optimization and resource
   budgets. Browser content admission and functional resource checks have
   [their own executed record](validation/m2-browser-map.md).
   The [three-backend measurements](validation/m2-browser-performance.md) identify
   terrain rebuilds and projection/binding as remaining costs after GPU command
   batching. Extend the diagnostic pan/zoom/world-update coverage to the complete
   protocol; retain failed/pending gates honestly.
3. Connect file-choice/load/save workflows to native and browser storage and
   verify cancellation, asynchronous replies and session deadlines. Add macOS
   native menus and Avalonia Windows/Linux menus with real platform acceptance.
4. Finish actual Windows/macOS desktop input/shutdown, OS interruption and full
   platform acceptance using [the checklist](testing/platforms.md); retain explicit
   evidence for target-hardware performance and additional runtime identifiers.
5. Continue M3's smallest playable vertical slice, then expand mechanics and AI
   through the owning specifications and milestone gates below.

For each slice, read the owning specification, record any gap, implement the
smallest coherent operation, test its boundary and failures, update coverage and
integrate a real caller before declaring the family delivered. Do not count source
files, assertions or green headless CI as proof of complete gameplay or desktop UX.
When the assigned task is the complete renderer, continue through all its matrix
rows and gates; completing one coherent slice does not complete that assignment.
