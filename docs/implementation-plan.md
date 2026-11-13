# Implementation plan

Status: roadmap for the [architecture](architecture.md), with an initial SDL3
application shell and build/test infrastructure integrated. The repository also
contains 65 algorithm specifications and three portable conformance vectors.
The session service/controller model, gameplay, save inspection/SQLite export
tools, the in-game console, Lua user automation and the X02 runner are not
implemented. Milestones below describe delivery gates, not completed compatibility
claims.

Deliver vertical slices through the portable game and graphical frontend while
growing the same conformance corpus. Establish file/wire formats and external
interoperability experiments early, even though complete sessions depend on
later mechanics. Keep all four platform builds active from the first shell.

## Milestones and dependencies

| Milestone | Result | Depends on |
| --- | --- | --- |
| M0 | Cleanroom workflow, content inventory, build/test skeleton | Existing specs |
| M1 | Portable state, session service/controller contracts, arithmetic, fixtures, rule/file foundations and save inspection | M0 |
| M2 | SDL/ImGui shell, native menus, asset adapter on four platforms | M0; M1 interfaces |
| M3 | Embedded single-player session, interactive map, bounded city/unit turn slice and command console | M1 + M2 |
| M4 | Complete single-player mechanics/classic AI; initial Lua player automation | M3 |
| M5 | Scenarios, editors, complete semantic UI and hotseat | M4; incremental work after M3 |
| M6 | Original-compatible native multiplayer and browser bridge | M1 protocol foundations + M4/M5 integration |
| M7 | Conformance closure, packaging, release evidence | M5 + M6 |

M2 can progress alongside M1. N05 framing and mixed-peer handshake experiments
start during M1/M2; do not wait for M6 to discover protocol problems. Implement
each mechanic's UI and network publication contracts as it arrives, and maintain
the M6 integration backlog throughout M3–M5. This is a dependency plan, not a
calendar estimate.

The [tools and automation architecture](architecture.md#5-inspection-tools-console-and-user-automation)
adds clients of the portable implementation. Deliver stored-save inspection in
M1 and the console in M3; start Lua automation once M3's query, command-result and
interaction contracts work. The automation slice can progress alongside M4
mechanics. It does not require moving classic AI or map generation out of C++.
Track these extension features separately from evidence of original-game
compatibility; they must preserve the owning operation's classic behavior.

Runtime state uses ordinary C++ records and containers. Add optional native
[SQLite export](architecture.md#optional-sqlite-analysis-export) after M1's
decoded-record/query foundation; extend it to loaded and selected live snapshots
with M3. SQLite remains a derived analysis output, with no core dependency and
no requirement to keep a second live state store synchronized.

Make `oc2_session` the [embedded session service](architecture.md#embedded-session-service).
M1 establishes lifecycle/client contracts and independent participant/controller
identities. M3 connects the frontend and headless drivers to the same local
runtime; M4 schedules classic AI policies within it. M5 adds hotseat bindings
and concealment; M6 completes the classic network mode/authority adapter. Local
play needs no transport or separate server process. Network mode preserves
per-operation executor assignments rather than moving all simulation to the host.

## Current implementation status

The shell was integrated from `codex/sdl3-core` through `c01bcef`, preserving the
current algorithm specifications rather than merging that branch's older copies.
See [application-shell.md](application-shell.md) for the code boundaries and
imported commit range, and [BUILDING.md](BUILDING.md) for reproducible commands.

| Milestone | Implemented subset | Remaining before the exit gate |
| --- | --- | --- |
| M0 — complete | Build/test skeleton and formatting workflow; pinned CI CMake/compilers; validated X02 operation/state registry, fixture provenance and coverage ledger; verified asset release and source inventory; pack/font/layout contract with explicit gaps; asset-directory inspection; stock-content and original-access plan; all 11 hosted CI jobs pass | None for M0; see the [completion review and retained reports](m0-validation.md#m0-completion-review--2026-11-13-utc). Graphics/layout readers and playable-content work remain M1/M2/M5; runtime TrueType fonts have a separate pinned dependency. |
| M1 — not implemented | Portable shell test executable also runs as WASM; this is test infrastructure only | C++ F01/F02/W01 state, randomness/checkpoints, X02 fixture execution, rules/save/wire codecs, save inspection CLI with optional SQLite export, scoped queries, SessionRuntime/controller contracts and continuations |
| M2 — partial | SDL3 callback shell, SDL_Renderer/ImGui, native AppKit/Win32 menu sources, Linux/web menu bar, pinned DejaVu TrueType runtime rasterization, custom modal content, shared action state, input ownership, high-DPI regression coverage | Assets/representative map, platform storage/time/media services, real custom game hit regions, changed gameplay targets/deadlines, native menu tracking/service strategy and native menu/display interaction validation |
| M3–M7 — not implemented | No gameplay or compatibility claim | Playable mechanics, console/Lua automation, AI, scenarios/editors, hotseat, original file/peer interoperability, browser bridge, conformance closure and packaging |

[CI run 43493400894](https://github.com/PeterJohnson/openciv2-cleanroom/actions/runs/43493400894)
at `a2592a0` passes all 11 jobs. Linux, macOS and Windows each pass 13 desktop
CTests and seven headless CTests; each desktop also passes seven ImGui scenarios
at three densities. The sanitizer job passes, and Emscripten 4.0.23 builds the
shell/test targets and passes Node tests and Chromium smoke. Formatting,
inventories, schemas and all 73 Python checks pass on each native platform.
The [M0 completion review](m0-validation.md#m0-completion-review--2026-11-13-utc)
records verified compiler/SDK identities, reports and the headless dependency
boundary. These results cover infrastructure and the shell; native menu/display
interaction and gameplay compatibility remain separate gates.

## M0 — Establish the project and its evidence boundary

- Add the architecture's CMake target skeleton, dependency lock mechanism,
  presets, formatting/warnings policy, and minimal CI jobs. Select and pin
  compatible CMake, compiler, SDL3, ImGui, Catch2 v3, test-engine, and emsdk
  versions; prove that they build together rather than choosing moving latest
  versions at every configure.
- Keep the pinned clang-format policy, explicit format/check commands, staged
  pre-commit check and CI enforcement active as sources grow. Review formatting
  before staging and verify partial staging remains untouched. Normal builds
  do not run formatters or require their development dependencies.
- Make headless configuration independent of GUI dependencies and assets.
  Introduce the proposed `src/`, `include/`, `tests/`, `tools/`, and `cmake/`
  organization only as their first targets need them. Group game code by
  foundation/world/unit/city/civilization/AI/scenario ownership.
- Create the X02 operation/state registry and coverage-ledger structure, with
  specification anchors, fixture provenance, affected domains, and unresolved
  gaps. Keep specification coverage, implemented operations, and executed tests
  as different statuses. Implementation work references repo specs, never their
  reconstruction-source links.
- Inventory runtime asset files and document the pack/font/layout data contract.
  Specify the new cleanroom reader and `OC2_ASSETS_DIR` integration; do not reuse
  the old game's renderer or package resolver. Choose a runtime package pin
  only after verifying it exists and matches the inspected contracts.
- Inventory stock rules/text and approved distribution inputs. Record authored
  test data separately from user-supplied classic content. Plan external test
  access to the target original executable without adding it to source packages.

**Exit gate:** a fresh checkout configures and runs one owned Catch2 smoke case
on native platforms and compiles a WASM test target. A headless build needs no
SDL, ImGui, graphics clone, reconstruction checkout, or original game. All
dependency and content gaps have named follow-up work.

**Completed 2026-11-13 UTC:** the [completion review](m0-validation.md#m0-completion-review--2026-11-13-utc)
maps these requirements to passing hosted reports and the existing inventories.

## M1 — Build the portable foundations

Owners: F01–F03, F06, W01, X02; early N02/N05 contracts.

- Implement arithmetic and logical types, F02 pools/lifetimes and retained state,
  W01 topology/coordinates, and explicit process/new-game/load boundaries.
  Use C++ records, arrays/stable slot pools and contiguous map storage with typed
  identities. Represent scan bounds, retained summaries, knowledge layers and
  operation workspaces explicitly. Keep file/wire layouts in codecs and verify
  that state/mechanics targets have no database dependency.
- Define the versioned random policy and controlled-choice injection. Implement
  [separate gameplay, presentation-content and cosmetic contexts](architecture.md#randomness-ownership),
  with independent initialization and no gameplay sampler exposed to rendering.
  Classify sites by their effects, retaining gameplay ownership for shared
  gameplay/presentation outcomes and for gameplay callbacks invoked by dialogs.
  Test original sampling laws, ties, retries and numeric boundaries in each
  applicable domain, plus implementation replay and pending-interaction restore
  without requiring original seed-to-sequence equality.
- Implement the X02 v3 loader, operation adapters, scripted external services,
  comparator, and bounded run reports. Execute the three existing F01 fixtures;
  add independently reviewed cases for state and geometry.
- Bind the [six built-in gameplay components](content/gameplay-format.md) to their
  owning mechanics. Default JSON, schemas, source pins, normalization and offline
  bundle validation exist; runtime loading, consumer bindings, checkpoint/profile
  admission and original-peer gates still need implementation. Keep original
  defaults, AI heuristics and map generation unchanged.
- Implement the [native JSON content contract](content/data-format.md), using the
  existing [schemas and structural checks](content/schemas/README.md) as the file
  boundary and adding semantic pack validation: versioned
  manifests, explicit dependency/hash resolution, rules/names/preferences and
  diagnostics. Implement F03/C01 classic text compatibility adapters into the same
  logical models, accepting nominal custom content and retaining specified
  conversions, fallback and partial effects. Test equivalent authored inputs
  through both paths, including movement/HP scaling exactly once, stable classic
  slots, city-name order and retained NoDefend. Native validation does not narrow
  the classic adapter's required domain. Add explicit content validation and
  conversion tools with separate outputs and provenance receipts.
- Implement classic record codecs, raw maps, and the first supported save
  load/export path. Keep content IDs/hashes outside classic payloads and original
  peer negotiation; expand version/mode coverage and label limitations.
- Separate decoded file records from F06's ordered session load. Add native
  `oc2 inspect`, `oc2 validate` and `oc2 diff` for the supported record domain,
  with human-readable and versioned, lossless JSON output, documented exit
  statuses, format/field diagnostics and no input writes. Report unsupported
  sections and missing rules explicitly; keep stored values distinct from
  computed interpretations. Compare both bytes and decoded fields by file slots.
- Introduce `oc2_query` with owned records, stable ordering and provenance for
  file inspection and live-state queries. Establish observer/authority contexts
  and prove queries neither mutate retained state nor consume gameplay RNG.
  Expose live queries to graphical and other clients through scoped session
  bindings; share value types without exposing mutable world state.
- Add optional `oc2_export_sqlite` and `oc2 export ... --sqlite ...` for the
  supported stored-record domain. Gate it with `OC2_ENABLE_SQLITE_EXPORT`,
  default OFF, and pin SQLite when adding the implementation. Export a
  separate analysis file with versioned schema, source hash/view mode/access
  scope and coverage metadata. Preserve identities, slots, retained values and
  exact integers; label unknown values and distinguish computed fields. Make
  export failure leave source files/state intact and no completed output claim.
- Introduce `SessionRuntime` with local world/session ownership and an injected
  F02 process context. Define lifecycle operations separately from participant
  bindings, and distinguish civilization, participant, controller and optional
  connection identities. Controller changes must not implicitly change the
  effective human set. Give bindings explicit session/lifetime/access scope.
- Define command/effect/interaction records and cooperative continuation APIs.
  Prove a fake decision can resume an operation, a callback can change a live
  read, and cancellation can retain an already-published effect.
  Add descriptor schemas for semantic names, typed arguments, access classes
  and help, plus correlated submission/pending/interaction/completion results.
  Keep descriptor parsing separate from the owning execution checks.
- Define mode/authority adapter contracts for local sequencing and N01–N03
  routing, with separate request-execution and resolved-result application
  paths. Use direct C++ calls/owned queues for local clients and injected
  services for headless drivers. Prove pending decisions, stale-binding rejection
  and revocation without losing committed effects or required replies; do not
  serialize local calls through a socket or replace the specified turn loops.
- Implement N05 incremental framing plus N02 envelope/state-image primitives
  against fake streams. Schedule independently authored original-peer discovery
  and handshake captures as soon as these boundaries are available.

**Exit gate:** exact arithmetic/codec vectors and controlled-choice fixtures pass
on native and WASM builds. Checkpoint repeat/restore is reproducible under the
chosen policy. Presentation initialization and draws cannot advance gameplay
contexts; cosmetic draws also leave presentation-content contexts unchanged.
Controlled semantic operations agree with and without a presentation consumer.
Partial I/O and fragmented/coalesced frame cases pass. Start bidirectional original
raw-map/save checks for the implemented subset, recording unavailable captures or
unimplemented fields as gaps. A tools-only build runs
basic save inspection without SDL, ImGui, assets, Lua or SQLite. Catch2 covers
truncated and unsupported records, unchanged input bytes, stable diffs, exact
JSON integers, and stored-state inspection without load-time identity/counter
transformations.
Do not advertise a loaded-state view before its actual load path exists.
With SQLite export enabled, its logical rows agree with the source query records,
including holes, numeric boundaries and stale retained summaries. Schema/source
metadata and failure/cancellation tests pass. Disabling export removes SQLite
from configuration and linkage without disabling text/JSON inspection.
Session contract tests distinguish acceptance from completion and reject stale
or unauthorized client contexts. Independent virtual peers have isolated process
contexts, while starting a second game in one process retains the specified F02
state. These are foundation tests, not completed mode conformance.

## M2 — Prove the graphical and platform boundaries

Owners: P01–P05 foundations, P10 service boundary; F03/P03 content lookup.

- Create the SDL3 callback-based shell, SDL_Renderer drawing path, and ImGui
  integration. Render a representative map region, text, and a custom dialog
  using the asset package through the new CPU reader and texture cache.
- Use the pinned DejaVu Sans/Serif/Mono TrueType faces and runtime glyph
  rasterization already connected to the shell. Exclude the asset package's
  historical bitmap font caches. Extend font roles/measurement to custom dialogs;
  verify DPI/zoom, glyph fallback, size/cache lifetime, native/WASM rendering and
  required text coverage. Preserve classic logical admission independently of
  visual font metrics; do not infer full shaping from glyph availability.
- Implement the single action/menu model and native AppKit/Win32 menu adapters;
  use ImGui main menus on Linux/web. Route native and ImGui selections to the
  same semantic commands, with P05 availability and execution rules distinct.
- Implement dialog lifecycle and stable testable custom hit regions. Exercise
  keyboard/focus routing, double-dispatch prevention, accept/cancel, and a target
  changed while the dialog is open.
- Add platform storage/time adapters and a browser import/export/persistence
  flow. Check high DPI, window resizing, menu insets, atlas/texture limits,
  browser startup/download costs, and case-independent classic resource lookup.
- Keep a pending interaction responsive to fake network traffic and deadlines.
  Test real native menu tracking rather than assuming the ordinary SDL frame
  loop keeps running inside platform menu loops. Test ImGui file dialogs and
  asynchronous browser file selection through their respective adapters.
- Introduce silent media and fake-clock services; verify that unavailable audio
  or skipped cosmetics cannot suppress semantic completion.

**Exit gate:** the same shell and content run on macOS, Windows, Linux, and in a
browser. Menu selections produce the expected action once. ImGui automation
can operate the custom dialog. Headless tests still link no graphical library.
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
- Add the portable console parser and an ImGui console panel with help, queries,
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

- Add optional `oc2_automation` and `OC2_ENABLE_SCRIPTING`; pin a stock Lua
  runtime and binding API for native/WASM in the dependency manifest. Keep
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
  Move required query/recalculation effects into session operations while keeping
  new drawing read-only. Retain gameplay-relevant classic view inputs.
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
changed-owner/deleted targets, timeout/reply cases, and retained edits after
cancel. Original interchange works for the advertised scenario/editor domain.
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
| P01–P05 | M2 shell; M3 interactive map; M5 semantic completeness |
| P06–P10 | M3 city UI; M4 caller integration; M5 reports/media/content |
| X01–X02 | M0 boundary; M1 runner; every milestone; M7 claim audit |

## Immediate implementation backlog

M0 is complete. Build the next implementation changes on the integrated shell,
maintaining the [registries/ledger](conformance/README.md) and
[content inventory](content/README.md) as operations and inputs arrive:

1. Implement F01 sampling/arithmetic adapters and execute the existing three
   X02 vectors with the version 3 loader/comparator.
2. Implement F02 identities/state and W01 geometry using C++ records and
   collections, with retained-state fixtures and no database dependency.
3. Resolve layout data gaps and implement the independent graphics reader
   against the [inspected package contract](content/asset-format.md); connect it
   to the existing SDL3/ImGui shell and finish native menu/platform verification.
4. Add F03/F06 codecs and early N05 framing/original-peer experiments, then
   expose stored-record queries and the inspection/validation/diff CLI.
   Build optional SQLite snapshot export on those same records and queries.
5. Establish the embedded session service, lifecycle and participant/controller
   bindings, scoped queries, command descriptors and correlated results as real
   playable operations arrive. Connect graphical/headless clients and the console
   in M3; keep mode scheduling and authority behind the service boundary.
6. Add the M4 Lua production-policy slice after those interfaces work, then
   close task persistence/hotseat and mixed-peer cases in M5/M6.

For each change: cite the owning specification, implement the smallest coherent
operation, test its meaningful boundary and failures, update its coverage row,
and integrate a real caller before treating the family as delivered.
