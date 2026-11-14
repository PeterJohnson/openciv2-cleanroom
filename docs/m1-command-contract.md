# M1 command and continuation contract

Status: portable semantic command, interaction and authority-adapter foundations
implemented in `oc2_session`, from [architecture §§4–5](architecture.md#4-commands-interactions-and-event-delivery).
No fake gameplay command is installed in production. The authored test operations
prove the service contract; owning mechanics and classic mode loops must register
their actual operations as they arrive. The [M1 audit](m1-validation.md) records completion of the finite foundation scope.

## Admission and execution

`CommandDescriptor` version 1 carries a semantic name, help, access class and
argument/result schemas. Values distinguish signed 32-bit integers, booleans,
text, civilization slots, unit slot/token targets and city slot/token targets.
Structural validation rejects unknown/duplicate/missing values, wrong types and
out-of-domain civilization/entity slots. It does not establish that a target is
live, owned or currently actionable. Descriptor lists are owned, sorted by name
and filtered by the binding's access class. Unavailable command names do not
reveal their schemas to a less privileged caller.

The trusted engine registers definitions while the client session is inactive.
Every definition supplies authority admission and an execution factory. Admission
receives a const world view and the captured actor; any binding change during
that callback is rechecked before acceptance. Owning continuations perform actor,
busy-state, target and authority checks at their specified execution/resume
boundaries. A descriptor or an earlier admission is not an execution permission.

Each submission through a valid binding receives a runtime/session/command
correlation and either a rejected or pending result. Invalid bindings fail with
`AccessError` before a submission exists. Local acceptance copies the request
into an owned FIFO; it does not run the factory or imply completion. Multiple
controllers of a participant share that ordered lane. Factories create explicit
continuation objects with retained semantic state, not suspended native stacks.

`advance_one()` consumes at most one queued input and resumes at most one
continuation. A pending operation needs an explicit `continue_command`, decision,
cancellation or deadline input; it is not automatically spun each frame.
Recursive `advance_one` is rejected. An owning operation can call injected
services at its specified boundary, publish effects and reread the live world
after a callback. This does not implement every X02 scripted reentry domain or
replace the distinct F04/N03/N04 loops with a generic turn scheduler.

## Effects, decisions and privacy

An operation returns pending, awaiting interaction, completed, cancelled or failed.
Completed results must match their descriptor. An operation exception becomes
failed without restoring earlier world changes or committed effects. This is
not an allocation-failure emulation or a transaction guarantee. Engine mutation
still calls `publish()` or emits a semantic effect at its specified publication
boundary before clients observe state.

Notifications carry correlation, lifecycle status, result, diagnostic code and
only the committed effects not already delivered to that lease. Each command
owns one cumulative effect log; per-lease cursors select its next suffix without
copying earlier effects. Effect indices remain stable and one-based. A new
binding receives the complete prefix of any visible pending command once.
`result()` explicitly returns an owned cumulative snapshot. Polling notifications
never runs mechanics, and separate recipients still see only their own prompts.

The trusted owner calls `retire_command` after consuming a terminal local result.
Retirement refuses pending commands, unread notifications on any live binding,
untransferred requests, and incoming-peer duplicate receipts. It removes obsolete
queued cancellations and delivery cursors together with the command. Already
returned snapshots remain valid. Local drivers must drain/revoke their bindings
and retire results to bound long-session history; no automatic age-based eviction
changes result availability. Incoming-peer receipts remain until session end;
M6 must define its protocol acknowledgement/epoch retirement before pruning them.

An interaction has its own identity, explicit recipient/observer, optional typed
target, ordered allowed choices, prompt, cancellation permission and optional
deadline/default. The recipient defaults to the admitted actor. Replies capture
the validated provider identity and are queued as inputs; duplicate or stale
interaction IDs and disallowed choices are rejected. Timeout is a distinct input
with the optional default choice, not a fabricated player response. The engine
supplies the clock: this foundational deadline is reached at `now >= deadline`.
Classic timer arithmetic and mode-specific default policies remain with their
owning adapters.

Command owners see their effects/results but not a different recipient's private
prompt. A different recipient sees only its interaction and withdrawal updates,
not the actor's effect/result payloads. Developer-only actor prompts also require
the developer grant. Other participants receive neither. Revoked leases lose
access to queued notifications; an authorized replacement can observe and answer
an existing pending interaction without rerunning the prefix.

Player cancellation is permitted for an owned command or the recipient's
cancellable interaction. The trusted engine also has `cancel_command` for the
owning lifecycle/mode to supply cancellation after participant removal. This is
an input to the continuation, not forced completion. Previously queued inputs
retain FIFO order; cancellation delivered after completion cannot undo it.

## Authority adapter ports

Admission selects local execution or waiting for a designated peer. Local work
never uses a socket or serialization. Peer work uses owned `RoutedRequest` and
`RoutedReply` values for the mode adapter to translate; these are not a new wire
protocol. Runtime correlations do not add fields to classic packets. The adapter
must map them to its existing protocol state and validate endpoint generations.

Incoming requests require a current participant/connection mapping and owning
admission. They enter the execution factory and retain the original reply
destination/correlation independently of controller leases. Exact duplicate
requests do not reexecute; a completed duplicate can reissue its cached reply.
Reusing a correlation with changed arguments or participant identity is rejected.

Incoming results must match the waiting correlation, designated source and result
schema. They enter a separate `apply_result` factory, never the request-execution
factory. Duplicate results cannot execute or reroll work again. Failed/cancelled
peer outcomes remain explicit terminal outcomes. The owning result operation is
responsible for applying the received resolved values without drawing a new choice.

Disconnect/reassignment/revocation do not erase accepted continuations or their
required reply destinations. Session end/replacement rejects while work is pending
or a request/reply outbox still needs transfer. Taking an outbox transfers its
delivery/retry responsibility to the adapter. Destruction is owner teardown, not
a cancellation operation; owners must finish that protocol work first.

Cancellation of a command still awaiting a peer explicitly requires the mode
adapter; this foundation does not invent a cancellation packet or discard the
awaited result. N01–N03 wire routing, disconnect defaults, endpoint-generation
checks, timeouts and host transfer remain integration work. Hard queue/output budgets, peer-receipt retirement and full durable session
checkpoints remain M3–M6 work. Local result retirement and linear effect delivery
are implemented above. M1's only
serialization deliverable is the [bounded local checkpoint profile](m1-closeout.md#m1-01-checkpoint-profile),
now implemented with [eight-group evidence](m1-checkpoint.md#eight-group-acceptance-evidence).
It uses the real session/continuation boundary with test-only authored owner codecs;
it does not claim arbitrary-world or pending-peer restore.

The engine-only random ports now support [controlled choices](random-policy.md#controlled-choices-and-semantic-traces)
and bounded generated traces. Operations supply stable site/call/attempt keys and
exact law arguments; retries and retained choices belong to the continuation.
A choice mismatch terminates the command with `random_choice_<category>`, retaining
its already-published effects and consumed input prefix. The context checkpoint
owns the tape/trace and rejects consumed keys after restore. M1-01 now serializes
the declared local state and suspended owner continuation, with versioned codecs
and fresh client binding. Arbitrary world/process/queue and actual gameplay
continuation persistence remain outside this profile.

## Validation

Twelve authored command cases and eight existing session cases pass on Windows
MSVC and WASM. They cover descriptor/access validation, owned FIFO inputs,
acceptance versus completion, callbacks/live reads, cancellation and deadlines,
private prompts and withdrawal, stale targets, replacement controllers,
independent fake peers, result application without rerolling, duplicate handling,
authority changes and required replies after disconnect/removal or cancellation.
The two controlled-choice scenarios additionally cover retries, deterministic ties,
retained decisions across an interaction, random-context restore while suspended,
live sampling arguments after a reply, presentation-consumer invariance, and a
missing choice after a committed effect. These fake operations establish the
foundation contract, not any particular classic gameplay selection rule.

The full builds pass 121 default-off and 123 SQLite-enabled Windows CTests and
all five WASM CTests (114 portable Catch2 cases plus metadata, 46 X02 vectors
and compiled-adapter rejection checks). The tools-only build remains independent of GUI, assets, Python
discovery and dependency downloads. Retained native/WASM reports were refreshed
for build identity; these command scenarios are not yet registered X02 fixtures
and are not original-program observations.

## Post-M1 integration interfaces

Player code includes `session/client.hpp` and `session/command_types.hpp`;
`query/live_records.hpp` contains owned observations without exposing entity pools.
The engine retains `runtime.hpp`, operation definitions and mutable contexts.
`tools/check_boundaries.py` follows transitive includes, and CMake rejects graphical
or platform dependencies in portable targets.

`ClassicSaveOperation` is the first asynchronous lifecycle workflow. Its owned
`FileChooser` interface has nonblocking begin/poll/cancel operations. A pending
choice retains no native stack and draws no RNG. `SessionActivity` prevents
new-game/end/restore replacement until the workflow is cancelled or finished,
while ordinary commands, incoming requests and deadlines continue to run.
Acceptance rechecks the current core-save preconditions and captures current
notice/snapshot values. Cancellation, chooser exceptions and destruction dismiss
the chooser and attempt one restart after a completed timer stop. The synchronous
headless wrapper drives these same phases through an immediate chooser.

The application still needs ImGui/browser implementations of this interface in
M2. The broader F05 wizard migration accompanies its M3 UI; its existing supplied
interaction tests and semantic ordering remain intact.

Post-M1 refactor validation (working tree based on `91a4c82`, 2026-11-14):
577 native CTests, 577 ASan/UBSan CTests and all eight WASM CTest targets pass;
the WASM portable suite contains 552 cases. All 118 Python tests pass, including
transitive boundary rejection tests. A 1,000-effect regression delivers exactly
1,000 effect entries to each owner lease; cumulative snapshots remain available.
Pending workflow checks cover cancellation/destruction, chooser failure,
replacement/restore rejection, accepted live-state changes, deadlines and peer
replies. LeakSanitizer cannot run under this environment's tracer; sanitizer
execution uses `ASAN_OPTIONS=detect_leaks=0`.

The desktop application and native storage library build with
`OC2_BUILD_TOOLS=OFF`. New [native](conformance/reports/post-m1-native.json) and
[WASM](conformance/reports/post-m1-wasm.json) reports bind the same 57 fixture
observations and consumed prefixes to current compiled inputs. The original M1
hosted/original-game evidence remains unchanged and revision-bound; these local
checks do not claim a new Windows/macOS or original-game run.
