# M1 command and continuation contract

Status: portable semantic command, interaction and authority-adapter foundations
implemented in `oc2_session`, from [architecture §§4–5](architecture.md#4-commands-interactions-and-event-delivery).
No fake gameplay command is installed in production. The authored test operations
prove the service contract; owning mechanics and classic mode loops must register
their actual operations as they arrive. The [M1 audit](m1-validation.md) remains open.

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

Updates carry the correlation, lifecycle status, result, diagnostic code and a
cumulative list of committed effects. Effect indices are stable and one-based
within a command; repeated copies in later updates are observations of the same
effects, not instructions to apply them again. Updates and results are owned
values; polling consumes that lease's notification queue without running mechanics.

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
checks, timeouts and host transfer remain integration work. Queue/output retention
budgets, durable checkpoints and controlled-choice tapes are also still open.

## Validation

Ten authored command cases and eight existing session cases pass on Windows
MSVC and WASM. They cover descriptor/access validation, owned FIFO inputs,
acceptance versus completion, callbacks/live reads, cancellation and deadlines,
private prompts and withdrawal, stale targets, replacement controllers,
independent fake peers, result application without rerolling, duplicate handling,
authority changes and required replies after disconnect/removal or cancellation.

The full builds pass 111 default-off and 113 SQLite-enabled Windows CTests and
all four WASM CTests (105 portable Catch2 cases plus metadata and twenty existing
X02 vectors). The tools-only build remains independent of GUI, assets, Python
discovery and dependency downloads. Retained native/WASM reports were refreshed
for build identity; these command scenarios are not yet registered X02 fixtures
and are not original-program observations.
