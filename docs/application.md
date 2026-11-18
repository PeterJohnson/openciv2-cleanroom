# Avalonia application

Status: the production command is implemented. Portable view-model/console clients
and a small Avalonia desktop/browser host pass local input checks. The optional
[Lua adapter](automation.md) passes matching Linux/Chromium command/resource
checks. Native macOS/Windows acceptance remains open. This is the bounded foundation application,
not playable-turn, full console (M3), or automation (M4) completion.

`LocalUnitCommands.StackBottom()` optionally registers version 1 of
`unit.stack-bottom`. Its only argument is an owned unit slot/lifetime token. It
uses the existing [U01 move-to-bottom](algorithms/U01-unit-lifecycle-and-stacks.md#move-to-bottom)
mechanic through `UnitOperation`, the same command dispatcher and scoped clients
as other session operations. The command changes stack order, never movement,
orders, coordinates, rules or RNG. A supplied confirmation is a presentation
choice, not a new game-rule decision.

The admitted profile is single-player, a canonical placed target, and a coherent
chain of 1–2047 live units all owned by the participant. Imported mixed-owner,
cyclic, broken or full-2048 repair-boundary chains are unavailable through this
command; the lower-level U01 contracts remain intact. Admission reads raw links
without repairing them. Rejection messages never disclose another owner's data.
Remote requests and multiplayer modes require their later client adapters.

Submission validates the typed descriptor, mode, lifetime, ownership and complete
chain before queueing. Execution validates again before prompting and before
mutation, so an asynchronous reply cannot revive a replaced target. Choice 0,
explicit cancellation and timeout retire without effects; choice 1 calls the
production primitive once. Results carry the target and a `changed` boolean.
Actual reorder publishes a `unit.stack-reordered` effect; an already-last unit
completes without that effect. Client authority and reply-recipient checks remain
in the session runtime. This command has no checkpoint continuation registration;
trying to save its pending interaction remains explicitly unsupported.

The hosts use only `IPlayerClient` observations and descriptors outside
the composition root. The integration world is authored and clearly labeled;
it is not a second engine. Direct API, console, Avalonia and optional Lua clients
must use this same registration and prove confirmation/cancellation, stale-target
failure, observer privacy and the same resulting production stack order. The
inspection CLI has no live-command mode, so this command adds no `oc2` switch.

## Client and host evidence

`OpenCiv2.ViewModels.SessionPanel` receives only a scoped `IPlayerClient`. Its
owned observations, descriptors and current command result are cleared on binding
replacement or revocation. It permits one active presentation command and retains
no unbounded history. `OpenCiv2.Console.CommandText` parses the closed descriptor
value alternatives with invariant canonical integers and bounded input; it has no
GUI or scripting dependency. Five shared native/WASM groups cover direct/text
command parity, cancellation, observer changes, revocation, typed quoting and
malformed input without submissions.

`OpenCiv2.App` composes the existing session and an authored three-unit own stack,
plus one foreign unit excluded from the presentation. Desktop and browser roots
use the same Avalonia controls and portable model. The owner advances at most one
queued operation per tick and refreshes observations only when session work runs,
rather than copying the world at every idle UI tick. Completed command records
are retired after the model consumes their updates.

Local evidence includes six native pointer-input checks on isolated Xvfb/X11 and
sixteen Chromium pointer/keyboard checks at actual 1× and 2× browser density.
They cover the asynchronous prompt, explicit cancellation, confirmation, repeated
no-op, decline and observer filtering; the browser additionally types a console
command, confirms by keyboard and rejects a foreign target. Native shutdown uses
the normal window-close protocol. `tools/dotnet/run_application.py` validates
compiled source/package/toolchain identities and binds all invoked artifacts and
reporting inputs before/after the checks. These are real rendered windows/canvas
controls over the production engine, not the comparison's toy core.

Native evidence is **isolated X11 at actual scale 1**. Synthetic input on the
existing desktop did not reach the controls; that desktop delivery remains
unverified. An Xvfb server DPI override did not change Avalonia's actual scale,
so it supplies no high-DPI evidence. Browser checks use software WebGL and make no
rendering-performance claim. Native macOS/Windows interaction, native menus,
accessibility and full display/DPI acceptance remain explicit platform/M2 gates.
The normal headless solution and CLI do not restore Avalonia packages.


The optional Lua native/browser hosts now run 13 matching production-session
case groups. Direct API, console and Lua compare actual stack links, command
results/effects and unchanged RNG; the Avalonia input checks above exercise the
same production registration. Lua cancellation/errors preserve completed effects,
and malformed UTF-8 produces a stable error on both platforms. Artifact-bound
reports and the [bridge contract](automation.md) record the evidence. No automation
editor, subscription system or VM checkpoint support is implied by this application boundary.
