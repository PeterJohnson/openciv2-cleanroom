# Unit stack and deletion foundations for post-load cleanup

`unit::StackOperations` implements U01 sections 2–4 traversal, ordered validation,
detach/front attachment, relocation and bottom ordering for local/host authority. It uses `LocalStacks`' real unit-pool and W01 map bodies. The
runtime owns its suppression, animation and per-operation activity fields in
`GameState::unit_stacks`; constructing or replacing a client session does not
perform a classic lifecycle reset of these fields.

The implemented queries are raw forward count, next, first, last, owner/position
lookup and position lookup. Scans use exact coordinates and the current pool
bound; direct link accesses use backing independently of lifetime and scan bound.
Position lookup observes the animation tile-owner gate and performs both specified
validations. Negative accessor arguments and stored terminal links remain distinct.

Validation reports before mutation and follows saved neighbors after cutting an
edge. Its four passes retain the forward-dead asymmetry: a live member's link to
a dead terminal can remain. Owner mismatches and missing reciprocal links are not
general repair conditions. Diagnostics are passive output and cannot change
reverse links or other state. The infinite-stack branch uses the same real placement
and lookup paths, including recursive validation. A finite chain of exactly 2,048
units also enters that branch; no inferred cycle-only shortcut replaces it.

Placement preserves unrelated unit fields and tile information. Singleton detach
clears only the unit feature, retaining owner/fertility. Front attachment searches
with full coordinates before signed-16 publication and changes the tile only when
no same-owner head exists. Detached positions use the signed owner byte. The activity
markers clear on normal return only, and are boolean assignments rather than locks.

## Routed placement and composite operations

`StackOperations` supplies local detach/attach children to repairing traversal.
It rejects remote execution; gameplay methods do not pump messages or wait for
replies. Relocation and whole-stack ordering keep their local composition and
semantic completion notifications.

`session::UnitOperation` owns the routed lifetime. It chooses local execution or
an owned noncopyable `UnitRequestChannel`, preserves distinct operation reply and
activity state, sends each primitive once and yields. Whole-stack relocation captures the next
member before each child request and rechecks authority when that child completes.
Each poll performs bounded work. Wrapping
ticks determine the 3600-tick timeout; queued late replies drain across subsequent
polls. Timeout notification precedes disabling the turn loop. Cancellation,
failure and completion release the channel exactly once. Standalone operations
own a session activity. Unit children constructed from `OperationContext` borrow
the enclosing command's protected world/process lifetime instead; they can yield,
resume and cancel inside real `Continuation` execution without competing for a
second owner-workflow admission.

N02/N03 adapters own wire encoding, state-difference application and transport
scheduling. Local synchronization is a semantic notification to that adapter.
The graphical/browser caller can continue rendering and processing unrelated
work while a request is pending. Tests exercise local routing, all remote request
kinds, pending replies, wraparound, late replies, cancellation and service failure.

## Basic deletion and the F06 cleanup scan

`unit::UnitDeletion::erase` implements U01 section 6's local deletion.
`session::UnitOperation` owns its remote request boundary. Negative and lifetime-zero entries return before
activity or authority handling. Local deletion captures the selectors, applies
the guarded military/per-type/cached-total decrements at their specified widths,
detaches through the real stack primitive, clears the lifetime and shrinks only
the exact tail once. Its carrier-reference scan includes free records and changes
only matching sleep orders. Loss statistics are retained.

The optional local notifications include home slot zero. The transport adapter's
semantic synchronization notification carries the changed position and optional
home; its home notice excludes zero. The final settlement-type gate invokes the
required V01 destruction-check child even with notifications off. Another
settlement type does not suppress that call; V01 decides the outcome.
Captures survive gameplay children while subsequent rule, count and mode reads
remain live.

The session operation implements remote deletion with the same pending reply,
wrapping deadline and late-reply drain as placement, without local mutation
fallback. Required children are checked before admission; a bound child that
throws retains established effects. N02/N03 wire production, dispatch and
synchronization remain adapter bindings, and V01's destruction cascade remains
an owning gameplay child. Tests supply explicit child outcomes without claiming
those owning algorithms are implemented.

Runtime state now includes the separate eight cached unit totals, deletion
activity/reply, classic turn-loop activity and network-host flag. They are retained
logical values, not added save fields. Core loading does not rebuild these cached
totals or initialize the new lifecycle fields by inference. The ordinary deletion
domain uses coherent pool/global scan bounds; a tail change updates both.

`session::cleanup_classic_loaded_units` composes F06 section 11 step 5 with these
real U01 operations. It establishes host, visits ascending slots through the live
bound, clears links on free records, validates live records and deletes them with
notifications false when either coordinate is negative. It preserves positive
out-of-map records. A destruction-check callback may extend the bound and those
new slots are visited. Earlier effects survive a failed gameplay child; diagnostic
recording is optional.

This extracted cleanup stage is now called by the
[accepted-file dialog](m1-stored-saves.md#accepted-file-load-caller) after core input,
event replacement/execution, preference/audio/title effects and home-directory
return. Required owning children and application admission remain separate work.
Its placement children require local authority; the session owns remote request
continuations. Diagnostic-driven authority changes are not production cases. Required S02/P10/V01
implementations, native bindings and application admission remain open.

## Backing and execution boundaries

Record access is bounded by the owned unit pool regardless of suppression.
No special/negative record resolver exists. Negative saved successors end the
repair loop before any read; a finite 2048-member chain retains its specified
reversal and completes without invented adjacent memory. Other invalid record
indices reject safely. Optional diagnostics accumulate in a caller-owned vector;
there is no callback or reentry at each report. Diagnostic allocation failure
omits the record without interrupting repair.

The caller supplies a shared step budget and maximum validation depth. Exhaustion
throws `StackResourceStop`, with a separate reason for each limit. These are host
execution limits, not game state, network timeouts, repair rules or original-game
failure returns. They prevent malformed backward cycles and recursive repairs
from hanging or exhausting the host stack. All preceding gameplay effects remain;
the internal depth counter unwinds independently of retained activity markers.

## Evidence and remaining integration

Current tests cover traversal/ties, dead and nonreciprocal edges, ordered cuts,
bounded rejection even under suppression, animation gating, placement, self-loop
repair and 2048-member reversal. With and without diagnostic collection, repairs
produce the same game state. Synthetic report mutations, interruptions and fake
out-of-pool records were removed. Tests of real network replies/authority changes
remain. The following test counts record earlier revisions, not this refactor.

Windows MSVC passes all 445 default-off and 447 SQLite-enabled CTests. Pinned
Emscripten 4.0.23 passes all six CTests, including 423 portable Catch2 cases
(188,953 assertions). The stack cases contribute 16,562 assertions. The 54
retained X02 native/WASM observations and consumed prefixes remain identical;
their reports were refreshed for the changed build and shared record header.
Formatting passes with pinned clang-format 20.1.8. The upstream Catch2 WASM
`__COUNTER__` warnings remain.

Fourteen additional deletion cases and five initial F06 composition cases cover the
bookkeeping, captured/live distinction, zero/high homes, callback failures,
reentrant activity, special backing, client wait/timeout/drain and ascending
cleanup effects. The 19 cases pass 191 assertions on native Windows and WASM.

Fourteen placement cases and one additional F06 case cover local ordering,
activity through synchronization, independent replies and full argument payloads,
authority changes between children and inside recursive repair, received-state
retention, captured successors in client composites, interrupted children and
timeout/drain behavior. These 15 cases add 199 native/WASM assertions. All 34
deletion/placement/F06 cases pass together after sharing the request implementation.

These are portable specification-derived Catch2 cases, not original executable
observations or newly registered X02 fixtures. Full unit creation, remaining
deletion/cargo/query/ship-ordering operations, owning V01/transport bindings,
complete F06 post-load composition and broader X02 adapter
coverage remain separate work under the
[M1 completion audit](m1-validation.md).
