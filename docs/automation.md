# Lua automation

Status: stock Lua 5.5.1 / bridge ABI 1 and the managed task adapter pass the same
13 production-session groups on Linux x64 and Chromium WASM. Native sanitizer
checks also pass on Linux. [Hosted CI](validation/README.md) additionally passes all thirteen
groups on macOS ARM64 and Windows x64 with equal production-command outcomes.
This closes those native Lua gates, not the remaining platform input/OS gates.

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

Default limits are 2 MiB of Lua allocations, 100,000 instructions per resume,
200 resumes, 64 command attempts, 4 KiB output and 64 KiB user source. Hard maxima
are 16 MiB, one million instructions, 10,000 resumes, 128 command attempts and
64 KiB output. Instruction counts round down to 1,000-instruction hook quanta.
Prelude instructions count toward the budget. Native library routines operate on
bounded VM data; these limits are not a wall-clock guarantee or hostile-code
isolation claim. Oversize query responses fail explicitly; pagination remains a
later API addition rather than silently truncating observations.

Host request and reply payloads are capped at 64 KiB. Replies use a closed binary
grammar (nil, boolean, signed i32, byte string, array, string-keyed object), with
16 nesting levels and at most 4,096 entries per container. The managed writer has
a fixed-capacity buffer. Lua decodes replies under `lua_resume`, never by evaluating
source. A failed allocation or malformed reply stays within the protected native
boundary. The coroutine is rooted on the main Lua stack until explicit close.
A `SafeHandle` owns the interpreter; byte buffers are borrowed only during calls,
and native observation pointers are copied while the handle remains rooted.
There are no native-to-managed callbacks or retained managed pointers.

The interpreter opens restricted base/table/string/math/UTF-8 libraries. File,
process, package, debug, network and child-coroutine capabilities are absent.
Loaders, metatable mutation/access, user finalizers, random streams, pattern
search, table sorting/moving and unyieldable error handlers are unavailable.
Malformed UTF-8 host text fails with the stable `invalid_utf8` code on both
runtimes, independent of framework localization/trimming. String repetition permits at most 65,536 repeats, including empty strings; this
bounds native loops that would otherwise evade the allocator and instruction hook. Instruction
exhaustion yields to the native owner and stops the task even inside `pcall`.
The protected API checks string types before converting error objects, so error
reporting cannot allocate through an unprotected Lua call. Lua diagnostics name
the combined `automation` chunk; line numbers currently include the prelude.

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

See [managed commands](BUILDING.md). The native build
verifies the pinned archive SHA-256, extracts only regular immediate `.c`/`.h`
files, builds stock Lua plus the bridge, checks the loaded runtime version and ABI,
and writes source/compiler/artifact hashes. The consumer build refuses stale
bridge inputs or a changed library. Lua's full source-header license accompanies
the native artifact. The native dependency contains no gameplay implementation.

`tests/lua_bridge.c` runs separately with address/undefined/leak sanitizers on
Linux. It checks budget failure inside `pcall`, arbitrary error objects, syntax
and allocator failures, restricted capabilities, exact typed replies, all truncated
prefixes of a nested reply, resume exhaustion, coroutine GC rooting, allocation
failure during replies and repeated explicit cleanup.

`OpenCiv2.AutomationCases` contains 13 portable groups shared by the native and
browser hosts: command equivalence, observer filtering/descriptors, instruction
and memory limits (including typed-reply allocation), host/output/command bounds, script errors, cancellation before
and after effects, managed GC/disposal, revoked authority, task command ownership,
stale targets, restricted capabilities/limit admission, and error after effects.
Both runners execute these against the actual production session and bridge.
The [hosted receipt](validation/README.md) binds successful runs on all three desktop OSes
and WASM to their tested source revision, library receipts and report hashes.

The browser target links verified stock Lua and the bridge as C++ using Lua's
supported exception path. This avoids the pinned LLVM 19/Emscripten 3.1.56 setjmp
helper mismatch; gameplay remains in the portable C# projects. Generated wrappers include the exact
archive-verified C sources with source-set hashes (including headers), and verification rejects modified
or extra inputs before MSBuild compiles. Published output includes the Lua notice.
The browser uses the ordinary trimmed .NET interpreter host; desktop Native AOT
and managed browser AOT are not required by this automation boundary.

`run_automation.py` records 12 explicit assembly manifests, exact compiled inputs,
runner inputs and all invoked native/browser artifacts before/after execution.
The native report currently binds 191 compiled inputs and the browser report 288.
`compare_automation.py` validates current identities/artifacts and compares all 13
case results plus actual full production stack links, effect count, changed flag
and script output. Both hosts match the independently stated expected outcome.
The evidence tests reject missing/duplicate cases, failed cases, changed results,
stale/missing assemblies, altered prepared sources, extra wrappers and wrong Lua
archives. Browser runs have a process watchdog and reject page errors. Local
Chromium 151.0.7922.34 executes the WASM-linked bridge; no mocked interpreter or
expected results enter the bridge. Full automation UI and subscriptions remain M4.
