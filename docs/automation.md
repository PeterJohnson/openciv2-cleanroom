# Lua automation

The optional runtime is [Lua-CSharp](https://github.com/nuskey8/Lua-CSharp)
(`LuaCSharp` **0.5.8**), pinned in `Directory.Packages.props` and NuGet lock files.
It runs as managed code on native .NET and WebAssembly; no Lua C library, P/Invoke
wrapper or native build preparation is required. The optional desktop launcher
uses a private framed pipe between the task owner and its interpreter process.
Historical stock Lua 5.5.1 / ABI 1 receipts in [validation](validation/README.md)
apply only to their recorded revisions, not to this runtime migration.

`OpenCiv2.Automation` depends on the portable descriptor parser and Contracts.
It never references Core, Query, Session, Avalonia or native I/O. A trusted owner
constructs a `ScriptTask` with an exclusive `ControllerKind.Automation` binding,
then calls `Tick()` on the session mutation lane. Each tick starts or polls one
Lua resume and processes at most one yielded request. The owner advances session operations
separately, reads task `Results`/`SubmittedCommands`, drains relevant presentation
updates, and retires commands only after their consumers finish. `Results` uses
the same client lease; revocation still prevents queries. Tasks do not own worlds.
The optional native/browser test hosts supply the production session as their
composition root.
The normal headless solution, CLI, console and Avalonia host do not require Lua.
The [optional desktop launcher](BUILDING.md#optional-sqlite-and-lua) loads one
explicitly selected script for Alder Valley.

## API 1

The prelude exposes these explicit operations; dispatch never uses reflection:

- `game.units()` returns only own-unit observations: numeric slot/x/y/type and an
  exact decimal-string lifetime token. No mutable engine object crosses the API.
- `game.production_events()` drains the binding's own-city completion queue. The
  result contains `events` and a `dropped` count. Each event contains `city` with
  slot and token, signed production `item`, and decimal strings for the revision
  and originating command ID (`cause`).
- `game.production_choices(city)` returns `ready` and current `options` for a
  matching owned city token. Each option has an item `id` and `name`. The query
  returns nil for unavailable cities and games without this observation.
- `game.commands()` returns descriptor help strings.
- `game.command(text)` uses the ordinary bounded console grammar and typed
  descriptor validation; it returns admission status, not synchronous completion.
- `game.result(commandId)` returns current status, code, effect count, typed result
  fields and any interaction (ID, prompt, allowed choices).
- `game.reply(interactionId, choice)` and `game.cancel(commandId)` use ordinary
  session receipts. A task can operate only on commands it submitted, even when
  another controller belongs to the same participant.
- `game.wait()` yields until the next owner tick; `game.log(text)` appends bounded
  output. IDs/tokens are canonical decimal strings; signed command integers stay
  exact signed 32-bit values.

The representative operation is the production confirmed `unit.stack-bottom`
command from the [application seam](application.md). A script can submit,
poll for its asynchronous interaction, reply, and poll for completion. Admission,
foreign-target rejection, stale-target revalidation and actual U01 stack mutation
remain in the session. Direct API, console and Lua tests compare the resulting
full stack links, effect count and unchanged gameplay/presentation RNG. Existing
Avalonia input checks exercise the same command separately.

The [next production example](../examples/automation/next-production.lua)
waits for one city to finish a build, selects a different item from its current
choices, and waits for the result. Its test submits the same request by hand and
replays the recorded Lua request on a matching initial save and turn sequence.
All three games produce identical saved state. The task exposes the parsed
requests and admission receipts in `CommandAttempts`, including rejected
requests. The desktop recorder also captures replies and cancellations at the
session boundary.

## Limits and lifetime

Default limits are 100,000 instructions per resume, 200 resumes, 64 command
attempts, 4 KiB output and 64 KiB user source. Hard maxima are one million
instructions, 10,000 resumes, 128 command attempts and 64 KiB output.
Instruction counts round down to 1,000-instruction hook quanta; the hook counter
resets each tick. Prelude instructions count toward the budget. Exhaustion uses
Lua-CSharp cancellation, which propagates through `pcall` and stops the task.

Lua values use the .NET garbage collector. The default in-process machine has
**no per-task memory cap**. The desktop launcher instead runs each task in its
own interpreter process with a **128 MiB managed heap limit**, including the
interpreter and reply data. It verifies that limit before executing Lua. This
is not a total process/RSS limit or an OS security sandbox. Instruction limits
do not bound time or allocation inside a library function. String repetition remains capped at 65,536 repetitions
(including empty strings) to bound library loop work.

Host request and reply payloads are capped at 64 KiB. Replies contain owned
JSON nodes converted to Lua values (nil, boolean, exact signed i32, UTF-8 text, arrays and
string-keyed objects), with at most 4,096 entries per container. The fixed API
shapes bound nesting. Reply accounting charges one byte per scalar tag, four bytes
per integer/string length/container count, and UTF-8 text bytes, retaining the
previous API's payload budget independently of the interpreter transport. Oversize query responses
fail explicitly; pagination remains a later API addition.

The managed host binding yields its Lua-CSharp coroutine before session dispatch.
The default machine resumes inline with the pending reply on the next owner
tick. The desktop machine exchanges bounded frames asynchronously over private
redirected pipes; it receives only the script and observer-filtered replies,
never a session or client reference. Game dispatch remains on the owner lane.
A five-second deadline per exchange terminates a stalled worker without blocking
the UI. An allocation failure ends the task with `memory_budget`; timeout and
unexpected process exits have distinct diagnostics. Disposal cancels an inline
suspended yield or terminates the desktop worker. Completion, cancellation and
failure stop further requests while preserving completed game effects. Pausing
stops polling/dispatch; a resume already executing may finish in the worker.

Lua-CSharp implements the Lua 5.2 language/library surface, rather than the old
Lua 5.5 runtime. Byte-string mode preserves UTF-8 byte lengths and slicing;
strings crossing the host boundary are strictly validated. Malformed host text
fails with `invalid_utf8` on both runtimes. IDs/tokens remain decimal strings to
avoid loss of precision in Lua-CSharp's double-based numbers; signed i32 command
values remain exact. The previous stock `utf8` library is unavailable.

The interpreter opens restricted base/table/string/math libraries. File, process,
package, debug, network and child-coroutine capabilities are absent. Loaders,
metatable mutation/access, user finalizers, random streams, pattern search,
table sorting/moving and `xpcall` are unavailable. Lua diagnostics name the
combined `automation` chunk; line numbers currently include the prelude.

An owner can pause a task between ticks and resume it with the same Lua stack
and remaining tick budget. The owner keeps advancing the session while the
task is paused. Cancellation works from both running and paused states.
Cancellation/error/completion stops further submissions and requests ordinary
cancellation of unfinished task commands. The owner must continue pumping accepted
cancellations and required replies. Completed effects are never rolled back;
`Results` reports those effects through the original lease before retirement.
Revocation prevents the task from issuing further cancellation/replies; the
session owner retains responsibility for outstanding interactions. Dispose tasks
before disposing the session, and do not concurrently call task methods.

Script loading is explicit. The optional desktop launcher accepts a UTF-8
script up to 64 KiB. Its sidebar starts, pauses, resumes and cancels that
script. Each start uses a fresh automation binding on the human seat. The
session keeps advancing during a pause, and visible script interactions use
the same dialog and replies as player commands. A successful reload stops the
old task. Start the script again to attach it to the loaded game.

Use `--script PATH --record TRACE` in the optional launcher to write a local
replay journal on close. The journal starts with the fresh Alder Valley
profile and contains ordered player and automation inputs, receipts,
continuation steps, imported save bytes, and hashes of explicit saves. It
includes the selected script's SHA-256. The trace is versioned separately
from classic saves.

`--replay TRACE --output SAVE` runs headlessly, verifies receipts and recorded
save hashes, and writes a new classic save if the journal ends with no pending
command. Replay needs the journal and the matching game profile. It never
loads the script. Journal export stops at its size or step limit and reports
the error without interrupting the game. Both outputs require a new path.

Live task checkpoint restoration remains M5 work. Browser resource isolation and
Windows/macOS qualification of the desktop worker are deferred.

## Build and evidence

See [managed commands](BUILDING.md). The optional solution and browser host
restore the same pinned NuGet runtime. Published dependency inventories include
the Lua-CSharp runtime, annotations and source generator, with their pinned MIT
license attribution under `packaging/notices/`. Ordinary console/core builds
remain independent of these packages.

`OpenCiv2.AutomationCases` contains 14 portable groups shared by native and browser
hosts: command equivalence, observer filtering/descriptors, instruction limits,
managed runtime/UTF-8 and suspension behavior, host/output/command bounds, script
errors, cancellation before and after effects, GC/disposal, revoked authority,
task command ownership, stale targets, restricted capabilities/limit admission,
error after effects, and the city production policy with replay and pause/resume.
Both runners use the production session and Lua-CSharp.
The runtime group also exercises allocations beyond the retired 2 MiB cap,
delayed resumes, pause and authority revocation before dispatch. The Linux native
play driver additionally exhausts a real worker heap after a committed turn and
stalls a worker during a visible interaction, then continues playing.

`run_automation.py` records 12 assembly manifests, exact compiled inputs and
locked dependency graphs, runner inputs and invoked artifacts before/after
execution. Report format `openciv2-production-lua/2` distinguishes this runtime
from the retired native bridge. `compare_automation.py` validates identities and
compares all 14 results and actual production stack links, effect count, changed
flag and output. Evidence tests reject missing/duplicate cases, failures, changed
outcomes, stale assemblies/locks and old bridge reports. Browser runs have a
process watchdog and reject page errors. Full automation UI and subscriptions
remain M4; cross-platform claims require new receipts for this runtime.
