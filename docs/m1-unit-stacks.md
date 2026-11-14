# Unit stack and deletion foundations for post-load cleanup

`unit::StackOperations` implements U01 sections 2–4 traversal, ordered validation,
detach/front attachment, relocation and bottom ordering with section 9 authority
and synchronization. It uses `LocalStacks`' real unit-pool and W01 map bodies. The
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

`StackOperations` supplies real detach/attach children to repairing traversal,
including recursive repair during destination lookup. Each child rechecks the
live mode and host flag independently. An admitted received-message callback can
change authority before the next child; diagnostics cannot. The already selected local
body continues after callbacks; it does not restart or discard received effects.
Standalone `LocalStacks` retains its explicit local/host precondition.

Detach, attach, relocate and move-to-bottom each own distinct activity/reply
fields. Their client requests carry the full signed arguments in six fields,
zeroing unused fields and omitting synchronization flags. The shared U01
request/wait implementation also serves basic deletion without combining its
state or request boundary. N02/N03 still own wire production, dispatch and
state-difference application. Local activity clears only after the selected
synchronization and flush sequence returns; a callback changing mode does not
cancel the already selected flush.

Relocation composes independently gated detach/attach with synchronization false,
then its own synchronization tail. Relocate-in-place captures coordinates first.
Move-to-bottom reads the successor before activity/authority entry, preserves its
terminal early return, then uses the real head/next/detach/last operations. Whole-stack
relocation captures each next member before relocation, has no combined request or
activity gate, and runs its outer synchronization even for a negative entry.
Front insertion reverses transferred members, including movement to their current
position. Child requests remain separate when authority selects a nonhost.

## Basic deletion and the F06 cleanup scan

`unit::UnitDeletion::erase` implements U01 section 6's basic deletion and its
section 9 request/wait boundary. Negative and lifetime-zero entries return before
activity or authority handling. Local deletion captures the selectors, applies
the guarded military/per-type/cached-total decrements at their specified widths,
detaches through the real stack primitive, clears the lifetime and shrinks only
the exact tail once. Its carrier-reference scan includes free records and changes
only matching sleep orders. Loss statistics are retained.

The optional local notifications include home slot zero. The later network tail
excludes home zero, tests mode after prior children and completes its selected
sequence without retesting mode between callbacks. The last exact settlement-type
gate invokes the required V01 destruction-check child even with notifications off.
Another settlement type does not suppress that call; V01 decides the outcome.
Captures survive callbacks while the later rule, count and mode reads remain live.

On a network nonhost, deletion clears its activity, assigns Pending, sends once,
reads the start tick even after a synchronous reply, and processes messages until
a reply or the signed wrapping 3,600-tick boundary. Timeout logs and notifies before
disabling the classic turn loop; the final queue drain can deliver a late reply.
There is no local mutation fallback and no final activity clear after reentrant
client callbacks. N02/N03 wire production, dispatch and synchronization remain
required bindings, as does V01's destruction cascade. Unbound children throw at
the invocation, retaining established effects; the tests supply explicit child
outcomes rather than claiming those owning algorithms are implemented.

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
Its placement children honor authority changes at real network boundaries.
Diagnostic-driven authority changes are not production cases. Required S02/P10/V01
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
