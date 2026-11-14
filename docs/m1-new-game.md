# F05 inner new-game initialization

Status: the section 5 initialization parent is implemented in
`session/new_game.hpp`, with required civilization-creation and start-balancing
children. This is not a complete setup wizard, generated world or playable
starting position. The [M1 audit](m1-validation.md) retains those obligations.

## Ordered initialization

The caller enters after W02 has finalized the map. The operation performs the
specified partial resets on existing world/process records:

1. Reset scenario parameters through the existing S01 operation; clear destroyed
   civilization history's count, turns, killer slots and name starts, preserving
   portraits and name tails; clear the eight civilization color-record flag sets.
2. Clear all tribe usage counts, retaining city-name cursors and name references.
3. Initialize turn/year/first-launch state, copy remembered options with cheat
   mode cleared, apply difficulty-specific tutorial/advice choices, copy message
   options and reset the specified transient, movement, scan, selector, pollution
   and reserved fields. Live unit/city records and lifetime generators survive.
4. Clear the first-discoverer and known-by summaries; set every wonder to Not Built.
5. Clear the specified spaceship, aggregate and four regional census fields and
   current research target for every civilization. Unassigned fields, including
   research progress, future technologies, flags, trade-region summaries, policy
   and goals, remain for their owning creation/initialization children.
6. Consume the F01 primary-modulo-50 history decision and then clear all power
   history samples. An unavailable controlled choice retains the earlier resets
   and leaves incoming history samples and deadline unchanged.
7. Invoke required V01 creation for slots 0 through 7, including absent slots,
   ignoring returned failures. Child exceptions stop with completed effects.
8. Copy the live alive mask to the at-start mask, then invoke required F05 start
   balancing. Later child mutations do not retroactively change that copy.
9. Validate the live remembered rates using the specified upper bounds only.
   With one human bit, assign the byte-reduced rates to `playerCiv`; with other
   cardinalities, assign 6/4 to human slots 1 through 7. Chieftain gold follows
   the actual human bits, also excluding slot 0.

Required children see the state produced by earlier steps. Their later mutations
are not silently reset or recomputed; rate and difficulty gates after balancing
read live state. Session publication precedes each external child and occurs on
normal or exceptional exit when a client session is active.

The owner supplies its declared gameplay RNG and a unique semantic call identity.
The history site is `F05.new-game.history`, with attempt 0. No context is seeded
or reset here. An explicit context permits setup before client-session admission;
the parent neither creates nor revokes client leases. An empty call identity is
rejected before mutation. A single-human `playerCiv` outside backing stops only
when the final rate assignment consumes it, retaining earlier valid effects.

Movement mode and civilization color-record flag sets are retained logical world
state. They do not add bytes to classic records or require native palette handles.
The map, knowledge layers, process fallback, record payloads, unrelated multiplayer
continuations and standing process preferences are not wholesale cleared.

## Evidence and remaining work

Fifteen portable cases use independently authored specification expectations.
They check all civilization bytes against F05 reset/retention expectations using
F06 byte offsets, scenario
and elimination string tails, all pool backing records, name cursors, selectors,
map retention, difficulty options, rate boundaries, zero/one/multiple human bits,
creation and balancing stops, live child changes, second-game state and shared
gameplay choice ordering. Active-client tests verify publication and retained
leases; an inactive runtime verifies that initialization does not require client
admission. Supplied child outcomes are not V01 or balancing implementations.

The 15 new cases pass 471 assertions on Windows and WASM. Full verification passes
460 native CTests and 462 with SQLite enabled. Pinned Emscripten 4.0.23 passes
all six CTests, including 438 portable Catch2 cases and 189,424 assertions.
All 54 retained native/WASM X02 observations and consumed prefixes match; these
reports still cover the registered F01/F02/W01 operations. Pinned clang-format
20.1.8 formatted all 245 owned files and checked the four staged C++ files.
Local ignored logs are `build/new-game-{native,sqlite,wasm}-build.log`,
`build/new-game-{native,sqlite,wasm}-test-build.log` and
`build/new-game-{native,sqlite,wasm}-ctest.log`.

The [world-size and parameter helpers](m1-world-setup.md) are now implemented
separately. The outer new-session boundary, full menu/wizard cancellation state
machine, complete V01 creation, F05 placement/balancing/acceleration,
application composition and X02 registration remain separate required work.
No new original-program observation is claimed by these tests.
