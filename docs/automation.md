# Lua automation

The optional runtime is [Lua-CSharp](https://github.com/nuskey8/Lua-CSharp)
(`LuaCSharp` **0.5.8**), pinned in `Directory.Packages.props` and NuGet lock files.
It runs as managed code on native .NET and WebAssembly; no Lua C library, P/Invoke
wrapper, binary reply protocol or native build preparation is required.
Historical stock Lua 5.5.1 / ABI 1 receipts in [validation](validation/README.md)
apply only to their recorded revisions, not to this runtime migration.

`OpenCiv2.Automation` depends on the portable descriptor parser and Contracts.
It never references Core, Query, Session, Avalonia or native I/O. A trusted owner
constructs a `ScriptTask` with an exclusive `ControllerKind.Automation` binding,
then calls `Tick()` on the session mutation lane. Each tick resumes Lua once and
processes at most one yielded request. The owner advances session operations
separately, reads task `Results`/`SubmittedCommands`, drains relevant presentation
updates, and retires commands only after their consumers finish. `Results` uses
the same client lease; revocation still prevents queries. Tasks do not own worlds.
The optional native/browser test hosts supply the production session as their
composition root.
The normal headless solution, CLI, console and Avalonia host do not require Lua.

## API 1

The prelude exposes these explicit operations; dispatch never uses reflection:

- `game.units()` returns only own-unit observations: numeric slot/x/y/type and an
  exact decimal-string lifetime token. No mutable engine object crosses the API.
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

## Limits and lifetime

Default limits are 100,000 instructions per resume, 200 resumes, 64 command
attempts, 4 KiB output and 64 KiB user source. Hard maxima are one million
instructions, 10,000 resumes, 128 command attempts and 64 KiB output.
Instruction counts round down to 1,000-instruction hook quanta; the hook counter
resets each tick. Prelude instructions count toward the budget. Exhaustion uses
Lua-CSharp cancellation, which propagates through `pcall` and stops the task.

There is **no per-task memory limit or VM memory accounting**. Lua values use the
.NET garbage collector. Instruction limits do not bound time or allocation inside
a library function; this runtime is for explicitly selected user scripts, not
hostile-code isolation. String repetition remains capped at 65,536 repetitions
(including empty strings) to bound library loop work.

Host request and reply payloads are capped at 64 KiB. Replies contain owned
`LuaValue`/`LuaTable` values (nil, boolean, exact signed i32, UTF-8 text, arrays and
string-keyed objects), with at most 4,096 entries per container. The fixed API
shapes bound nesting. Reply accounting charges one byte per scalar tag, four bytes
per integer/string length/container count, and UTF-8 text bytes, retaining the
previous API's payload budget without a wire encoding. Oversize query responses
fail explicitly; pagination remains a later API addition.

The managed host binding yields its Lua-CSharp coroutine before session dispatch.
The next owner tick resumes it inline with the pending reply; no worker thread,
external asynchronous operation or session callback runs the script. Disposal
cancels the suspended yield, unwinds its call stack and disposes the states.
Task completion, cancellation and failure release the interpreter immediately.

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

Cancellation/error/completion stops further submissions and requests ordinary
cancellation of unfinished task commands. The owner must continue pumping accepted
cancellations and required replies. Completed effects are never rolled back;
`Results` reports those effects through the original lease before retirement.
Revocation prevents the task from issuing further cancellation/replies; the
session owner retains responsibility for outstanding interactions. Dispose tasks
before disposing the session, and do not concurrently call task methods.

Script loading must be explicit. This slice does not import scripts from saves,
serialize VM state, subscribe to events, or add automation UI. General task
persistence, metadata matching, subscriptions and UI remain M4.

## Build and evidence

See [managed commands](BUILDING.md). The optional solution and browser host
restore the same pinned NuGet runtime. Published dependency inventories include
the Lua-CSharp runtime, annotations and source generator, with their pinned MIT
license attribution under `packaging/notices/`. Ordinary console/core builds
remain independent of these packages.

`OpenCiv2.AutomationCases` contains 13 portable groups shared by native and browser
hosts: command equivalence, observer filtering/descriptors, instruction limits,
managed runtime/UTF-8 and suspension behavior, host/output/command bounds, script
errors, cancellation before and after effects, GC/disposal, revoked authority,
task command ownership, stale targets, restricted capabilities/limit admission,
and error after effects. Both runners use the production session and Lua-CSharp.
The runtime group also exercises allocations beyond the retired 2 MiB cap.

`run_automation.py` records 12 assembly manifests, exact compiled inputs and
locked dependency graphs, runner inputs and invoked artifacts before/after
execution. Report format `openciv2-production-lua/2` distinguishes this runtime
from the retired native bridge. `compare_automation.py` validates identities and
compares all 13 results and actual production stack links, effect count, changed
flag and output. Evidence tests reject missing/duplicate cases, failures, changed
outcomes, stale assemblies/locks and old bridge reports. Browser runs have a
process watchdog and reject page errors. Full automation UI and subscriptions
remain M4; cross-platform claims require new receipts for this runtime.
