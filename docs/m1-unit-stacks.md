# Unit stack foundations for post-load cleanup

`unit::LocalStacks` implements the U01 sections 2–4 traversal, ordered validation
and local, unsynchronized detach/front-attachment paths needed by F06's eventual
accepted-file load wrapper. It operates on the real unit pool and W01 map. The
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
general repair conditions. An explicit returning diagnostic can change the reverse
link before the cut tests it. The infinite-stack branch uses the same real placement
and lookup paths, including recursive validation. A finite chain of exactly 2,048
units also enters that branch; no inferred cycle-only shortcut replaces it.

Placement preserves unrelated unit fields and tile information. Singleton detach
clears only the unit feature, retaining owner/fertility. Front attachment searches
with full coordinates before signed-16 publication and changes the tile only when
no same-owner head exists. Detached positions use the signed owner byte. The activity
markers clear on normal return only, and are boolean assignments rather than locks.

The caller must establish local/host authority before using this API. These are
explicitly the local paths, not completed U01 network commands: client requests,
reply waits, synchronization tails and broader lifecycle/cargo operations remain
open. F06 must establish its specified host state before invoking post-load cleanup.
The accepted-file wrapper and basic deletion are not connected by this increment.

## Backing and execution boundaries

Ordinary slots always resolve to stable pool backing. A passive optional resolver
can provide explicitly owned special or negative records; it does not override
ordinary slots. Missing backing throws at the access, preserving earlier effects.
In particular, the repair loop's negative-terminal read is performed even though
its value is discarded. Supplying that backing permits continuation; omitting it
does not turn the preceding repairs into a successful validation result.

The caller supplies a shared step budget and maximum validation depth. Exhaustion
throws `StackResourceStop`, with a separate reason for each limit. These are host
execution limits, not game state, network timeouts, repair rules or original-game
failure returns. They prevent malformed backward cycles and recursive repairs
from hanging or exhausting the host stack. All preceding gameplay effects remain;
the internal depth counter unwinds independently of retained activity markers.

## Evidence and remaining integration

The 17 cases in `tests/unit_stack_tests.cpp` are independently authored from U01 sections 2–4,
9–11 and Appendix A. It covers traversal/ties, dead and nonreciprocal edges,
diagnostic mutations and interruption, ordered positional cuts, suppression after
assertion, special backing, animation gating/double validation, full-coordinate
placement, signed owners, self-loop repair, and a complete 2,048-member chain
reversal with both supplied and missing negative-terminal backing. Resource-stop
cases check the retained links, coordinates and activity state.

Windows MSVC passes all 349 default-off and 351 SQLite-enabled CTests. Pinned
Emscripten 4.0.23 passes all six CTests, including 329 portable Catch2 cases
(182,545 assertions). The stack cases contribute 16,562 assertions. The 54
retained X02 native/WASM observations and consumed prefixes remain identical;
their reports were refreshed for the changed build and shared record header.
Formatting passes with pinned clang-format 20.1.8. The upstream Catch2 WASM
`__COUNTER__` warnings remain.

These are portable specification-derived Catch2 cases, not original executable
observations or newly registered X02 fixtures. Full unit creation/deletion,
remaining queries and placement operations, network routing, F06 post-load
composition and broader X02 adapter coverage remain separate work under the
[M1 completion audit](m1-validation.md).
