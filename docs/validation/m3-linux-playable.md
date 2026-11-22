# Alder Valley on Linux

Alder Valley is a 24×16 flat authored map with one human seat, Chieftain
difficulty, Despotism and three ordinary advances. The Pioneer founds one
city. The Outrider and Watch explore by land and can fortify. The city works
revealed tiles, supports units, grows within its facility limit and produces
Watch or Outrider units. City trade yields science and tax under fixed rates.

Start the desktop with `--game alder-valley`. The city dialog and console use
the same commands as a direct client. The command list and console completion
include only entitled commands and known owned target handles. The scoped
update queue delivers committed effects in order, once per controller, with an
effect index local to each command. The game uses the existing map projection,
minimap, CPU renderer by default and correlated interaction dialog.

## Implemented joins

| Join | Admitted paths |
| --- | --- |
| G03 | A classic `.SAV` is staged through F06 under the explicit Alder Valley rules. Reload renews unit and city lifetimes, rebuilds the applicable counts and city outputs, and enters the saved human phase. The saved turn, movement spent, orders, stocks and research remain available for continuation. |
| G05 | U01 placement, eight-direction U02 land steps, terrain/river/road/rail costs, exhaustion and conditional partial movement, bounded ZOC denial/revelation, U04 selection and fortification, and W03 exploration and feature memory. |
| G08 | C01 founding with a preaccepted name, C02 ordered worked tiles and specialists, one capital's C03 output and C04 mood, growth and support under C05, C06 ordinary unit stock and completion, and V03 research progress and advance choice. |
| G19 | Actual Avalonia controls, map and minimap, descriptors, console history and completion, focus containment and decision replies. Save waits for durable application-store publication. |
| G20 | The own-city dialog projects current worked tiles, specialists, production, support and output. Commands validate city slot and lifetime again on execution. Session replacement discards the outgoing city selection and console history. |

The profile admits manual land orders in the player phase. Combat, foreign
contact, diplomacy, air/sea transport, Goto, terrain work, hut rewards,
trade routes, facilities beyond the initial Palace, additional civilizations
and multiplayer remain unavailable. The authored fixture supplies no scenario
events or AI turns. The optional loaded CLI operates only on this admitted
profile. A pending required research choice keeps the surrounding turn command
open and blocks saving or replacement until it is answered. A declined or
cancelled turn confirmation leaves the world at the previous player phase.
Classic files and local replay checkpoints are separate contracts.

The native save slot resides in the application file library. The F06
operation writes bytes before the native store wraps them for durable
publication. The file library exports the enclosed bytes as a normal
`.SAV`. A rejected load leaves the live session and its bindings intact. The
profile admits entertainers. The loader rejects saves containing a scientist
or tax collector in any specialist slot before city recalculation. The CLI
declares the entry mode, rules and RNG seeds. Classic reload initializes
its own RNG contexts. The saved file supplies the retained classic values.

When a partial-movement roll exhausts the active unit, the failed command
commits the movement spent and publishes the next eligible unit selection.

## Reproducing the Linux checks

Build the root, App and optional SQLite solutions in Release as described in
the [build guide](../BUILDING.md). The headless suite runs the same scenario
through direct and descriptor-parsed console requests, compares the classic
bytes and checks cancellation, stale identities, scope and continuation. The
native driver compares both sides of reload against the direct-client probe
and against an isolated CLI load. The optional exporter emits only its scoped
rows with observer, runtime, session and revision metadata.

```sh
dotnet tests/OpenCiv2.Base.Tests/bin/Release/net11.0/OpenCiv2.Base.Tests.dll
dotnet tests/OpenCiv2.Sqlite.Tests/bin/Release/net11.0/OpenCiv2.Sqlite.Tests.dll

python3 tools/dotnet/run_xvfb.py --screen 1440x1000x24 \
  python3 tools/dotnet/run_local_play_native.py \
  --report build/test-results/m3-linux-play.json \
  --desktop src/OpenCiv2.Desktop/bin/Release/net11.0/OpenCiv2.Desktop.dll \
  --cli src/OpenCiv2.Cli/bin/Release/net11.0/oc2.dll \
  --probe tests/OpenCiv2.FoundationProbe/bin/Release/net11.0/OpenCiv2.FoundationProbe.dll \
  --sqlite-cli src/OpenCiv2.Cli.Sqlite/bin/Release/net11.0/oc2-sqlite.dll
```

Pass the pinned extraction roots with `--assets ROOT --fonts ROOT` to exercise
stock map art and fonts in the same driver.

## Linux result on 2026-11-21

The locked root, App and SQLite Release builds and formatting verification
passed on Linux x86-64 with .NET SDK 11.0.100 and runtime 11.0.0. The Linux x64
CLI project graph also passed a locked framework-dependent RID restore. The
headless suite ran 506 cases with two existing skips for Windows file sharing
and opt-in native audio. All other cases passed, including the direct and
console scenario. The five SQLite unit tests, eight stored inspection CLI
checks, five SQLite CLI checks and 57 selected native X02 vectors passed.

The Alder Valley XTest run used an isolated 1440×1000 Xvfb display, CPU
composition, graphics package 2026.11.08.1 and DejaVu 2.37. It moved the
Outrider from (10, 8) to (12, 8), expanded exploration, founded Alder Crossing,
changed a worker and production, resolved research, and saved on turn 7.
The direct client, console and native UI agreed at the save and resumed
boundaries. The restored city grew to size 2 on turn 8, with three units and
43 known tiles. The run checked rejected stale city commands, cancellation,
required decisions, loaded CLI and SQLite rows, and normal desktop shutdown.
The Riverbend regression suite passed all 32 native input checks after keeping
its original starting selection. The CPU raster check exercised MV01–MV30 and
the Mixed board.

Receipts under `build/test-results` record the executed inputs and results.

| Receipt | SHA-256 |
| --- | --- |
| `m3-linux-play.json` | `e5f370c953e86ab8df8a27a7f7bec16ad73dd309d6c20a5c7e003a3984561097` |
| `m3-linux-riverbend.json` | `de537598c239886cf21c88d3f5eab922219b11a398e0162840f964dd2dae519a` |
| `m3-linux-headless.xml` | `aa0089ae3dd19992dc57d9e38bbf23054923f53e0c07cfab9af1ac9e4e3f497f` |
| `m3-linux-sqlite.xml` | `3970ec1817708802d53a5b95405731154fa69a421ebc3f24146de713c0db4b4d` |
| `m3-linux-x02.json` | `9f2a3e69cfb0f78951255dca2e99d54173687e3d901d530fd6b440ddae31f814` |

## Specialist and movement follow-up

The Linux headless suite passed 506 cases with the same two skips. The new
coverage checks that unsupported specialist kinds leave the current session
intact and that the E, S, S sequence reselects the Pioneer through direct and
console commands. The native round trip now runs the same sequence. It passed
with the loaded CLI and SQLite checks, and all 32 Riverbend input checks
passed. The follow-up receipts in `build/test-results` use the
`m3-correctness-` prefix.

## Acceptance boundary

Full M3 acceptance remains pending. Browser-specific work, WASM runs and further
Windows/macOS qualification are deferred. The Linux result covers this authored
game and observer scope. Original-file interoperability on the complete game
domain, expanded F04/F05 gameplay, peer modes and the full G03/G05/G08/G19/G20
matrix retain their milestone gates. The [M2 deferrals](../m2-closeout.md#explicit-follow-up-work)
remain tracked and CPU remains the renderer default.
