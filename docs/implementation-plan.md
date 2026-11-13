# Implementation plan

Status: roadmap for the [architecture](architecture.md), with an initial SDL3
application shell and build/test infrastructure integrated. The repository also
contains 65 algorithm specifications and three portable conformance vectors.
Gameplay and the X02 runner are not implemented. Milestones below describe
delivery gates, not completed compatibility claims.

Deliver vertical slices through the portable game and graphical frontend while
growing the same conformance corpus. Establish file/wire formats and external
interoperability experiments early, even though complete sessions depend on
later mechanics. Keep all four platform builds active from the first shell.

## Milestones and dependencies

| Milestone | Result | Depends on |
| --- | --- | --- |
| M0 | Cleanroom workflow, content inventory, build/test skeleton | Existing specs |
| M1 | Portable state, arithmetic, fixtures, rule/file foundations | M0 |
| M2 | SDL/ImGui shell, native menus, asset adapter on four platforms | M0; M1 interfaces |
| M3 | Interactive map and bounded city/unit turn slice | M1 + M2 |
| M4 | Complete single-player mechanics and classic AI | M3 |
| M5 | Scenarios, editors, complete semantic UI and hotseat | M4; incremental work after M3 |
| M6 | Original-compatible native multiplayer and browser bridge | M1 protocol foundations + M4/M5 integration |
| M7 | Conformance closure, packaging, release evidence | M5 + M6 |

M2 can progress alongside M1. N05 framing and mixed-peer handshake experiments
start during M1/M2; do not wait for M6 to discover protocol problems. Implement
each mechanic's UI and network publication contracts as it arrives, and maintain
the M6 integration backlog throughout M3–M5. This is a dependency plan, not a
calendar estimate.

## Current implementation status

The shell was integrated from `codex/sdl3-core` through `c01bcef`, preserving the
current algorithm specifications rather than merging that branch's older copies.
See [application-shell.md](application-shell.md) for the code boundaries and
imported commit range, and [BUILDING.md](BUILDING.md) for reproducible commands.

| Milestone | Implemented subset | Remaining before the exit gate |
| --- | --- | --- |
| M0 — partial | C++20/CMake targets, dependency archive/hash manifest, offline/download policy, desktop/headless/release/sanitizer/web presets, Catch2, ImGui automation, four-platform CI definitions | X02 registry/coverage ledger, content and asset-format inventory, full compiler/platform execution evidence |
| M1 — not implemented | Portable shell test executable also runs as WASM; this is test infrastructure only | F01/F02/W01, randomness/checkpoints, X02 fixture execution, rules/save/wire codecs and semantic session continuations |
| M2 — partial | SDL3 callback shell, SDL_Renderer/ImGui, native AppKit/Win32 menu sources, Linux/web menu bar, custom modal content, shared action state, input ownership, high-DPI regression coverage | Assets/representative map, platform storage/time/media services, real custom game hit regions, changed gameplay targets/deadlines, native menu tracking/service strategy and native desktop validation |
| M3–M7 — not implemented | No gameplay or compatibility claim | Playable mechanics, AI, scenarios/editors, hotseat, original file/peer interoperability, browser bridge, conformance closure and packaging |

Local merge verification uses Linux/GCC 15.2, SDL3 3.4.2, and the dependency pins
in `dependencies.json`. Native CTest passes 11 entries, including seven ImGui
scenarios at each of three densities (21 scenario runs). The separate headless
build passes five Catch2 cases, also passing with ASan/UBSan. These results cover
the application shell only. The Emscripten 4.0.23 Release build passes all five
portable Catch2 cases under Node and the Chromium smoke test with pinned
Playwright 1.58.2 (startup, menus/dialogs, resizing, 2x density, Quit/Restart).
The browser harness now fails explicitly if its local HTTP listener cannot bind.
A release shell build/smoke run also passes; headless library-only configuration
needs no dependencies. macOS/Windows compilation and native menu interaction
remain unverified locally; CI definitions alone are not execution evidence.

## M0 — Establish the project and its evidence boundary

- Add the architecture's CMake target skeleton, dependency lock mechanism,
  presets, formatting/warnings policy, and minimal CI jobs. Select and pin
  compatible CMake, compiler, SDL3, ImGui, Catch2 v3, test-engine, and emsdk
  versions; prove that they build together rather than choosing moving latest
  versions at every configure.
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

## M1 — Build the portable foundations

Owners: F01–F03, F06, W01, X02; early N02/N05 contracts.

- Implement arithmetic and logical types, F02 pools/lifetimes and retained state,
  W01 topology/coordinates, and explicit process/new-game/load boundaries.
- Define the versioned random policy and controlled-choice injection. Test
  primary/secondary laws, ties, retries, numeric boundaries, and implementation
  replay without requiring original seed-to-sequence equality.
- Implement the X02 v3 loader, operation adapters, scripted external services,
  comparator, and bounded run reports. Execute the three existing F01 fixtures;
  add independently reviewed cases for state and geometry.
- Implement F03 rules and text-encoding foundations, accepting nominal custom
  content and retaining specified consumer conversions. Implement explicit
  classic record codecs, raw maps, and the first supported save load/export path.
  Expand version/mode coverage incrementally and label limitations.
- Define command/effect/interaction records and cooperative continuation APIs.
  Prove a fake decision can resume an operation, a callback can change a live
  read, and cancellation can retain an already-published effect.
- Implement N05 incremental framing plus N02 envelope/state-image primitives
  against fake streams. Schedule independently authored original-peer discovery
  and handshake captures as soon as these boundaries are available.

**Exit gate:** exact arithmetic/codec vectors and controlled-choice fixtures pass
on native and WASM builds. Checkpoint repeat/restore is reproducible under the
chosen policy. Partial I/O and fragmented/coalesced frame cases pass. Start
bidirectional original raw-map/save checks for the implemented subset, recording
unavailable captures or unimplemented fields as gaps.

## M2 — Prove the graphical and platform boundaries

Owners: P01–P05 foundations, P10 service boundary; F03/P03 content lookup.

- Create the SDL3 callback-based shell, SDL_Renderer drawing path, and ImGui
  integration. Render a representative map region, text, and a custom dialog
  using the asset package through the new CPU reader and texture cache.
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
- Render the world and minimap from observer-filtered models. Open a city dialog
  whose controls invoke the same tested economy/production operations used by
  the headless session.
- Grow F04's explicit phases and F05 setup around these mechanics. Exercise
  retained state and resume gates, not just a fresh-process happy path.
- Save and reload the slice through F06. Continue after reload and compare its
  specified transformations. Keep replay checkpoints separate from classic saves.
- Add interface seams for combat, diplomacy, AI, events, and network authority;
  no placeholder child counts as a completed integrated operation.

**Exit gate:** an automated headless and graphical script completes multiple
turns with a city and units, saves, reloads, and continues. Key state/results
agree between frontends. Cover changed/deleted targets, fog privacy, cancellation,
and the first G03/G05/G08/G19/G20 joins. This is a bounded playable prototype,
not complete single-player compatibility.

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

## M5 — Complete scenarios, editors, UI, and hotseat

Owners: S01–S04, N04, P01–P10; F03/F06 and the actual mutation owners.

- Complete scenario setup, authored resources, event grammar/triggers/actions,
  one-shot state, action ordering, and load/save continuation. Exercise real
  mechanics for reactive events and random-turn laws.
- Complete world/scenario/rules/asset editor operations, apply/cancel behavior,
  and classic exports. Load exported maps/scenarios/assets in the original game;
  a cleanroom import of its own output is insufficient.
- Complete advisors/reports/history, diplomacy/chat presentation, Civilopedia
  and technology links, council/throne choices, and meaningful media flows.
  Move required query/recalculation effects into session operations while keeping
  new drawing read-only. Retain gameplay-relevant classic view inputs.
- Implement N04 roster, passwords, handoff, per-player views, save/resume and
  concealment. Test cached map/dialog/report information across two handoffs.
- Add sound from the asset repo when available. Complete supported authored
  media decoding independently of stock audio availability; test fallback,
  completion, skip/cancel, and incoming events during playback.

**Exit gate:** G13–G15 and G19–G22 have integrated coverage with custom content,
changed-owner/deleted targets, timeout/reply cases, and retained edits after
cancel. Original interchange works for the advertised scenario/editor domain.
GUI tests exercise semantic outcomes and custom controls; desktop smoke tests
cover both native menus and browser tests cover persistence/import/export.

## M6 — Complete multiplayer and the browser transport path

Owners: N01–N05, with F04/F06, simulation, scenario, and P05 interaction joins.

- Finish native TCP/UDP adapters and discovery, lobby/admission, player versus
  connection identities, password/setup exchanges, and both host roles.
- Complete sequential and simultaneous turns, per-operation authority, state
  sections/diffs/checksums, locks, deferred inputs/popups, barriers and clocks.
  Apply received outcomes exactly once and never reroll them.
- Complete save/resume, running join/reconnect, disconnect recovery, and host
  transfer while operations are pending. Test both migration directions and
  failure prefixes, including required replies after dialog failure/timeout.
- Run multiple cleanroom peers under a deterministic fake transport, then live
  original/cleanroom mixed sessions in both host roles. Replay captured transcripts
  for regression; retain live runs as a distinct interoperability requirement.
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

The next implementation changes should build on the integrated shell:

1. Complete M0's operation/state registry, coverage ledger, and content inventory;
   retain the existing CMake/Catch2/platform build boundary.
2. Implement F01 sampling/arithmetic adapters and execute the existing three
   X02 vectors with the version 3 loader/comparator.
3. Implement F02 identities/state and W01 geometry with retained-state fixtures.
4. Specify and implement the asset data reader and content manifest; connect it
   to the existing SDL3/ImGui shell and finish native menu/platform verification.
5. Add F03/F06 codecs and early N05 framing/original-peer experiments, then
   connect the first playable slice.

For each change: cite the owning specification, implement the smallest coherent
operation, test its meaningful boundary and failures, update its coverage row,
and integrate a real caller before treating the family as delivered.
