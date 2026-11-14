# F05 new-world entry composition

`session/world_entry.hpp` implements the section 2 parent after selection of
Random World, Premade World or Customize World. It calls the ordinary F03 rule
operation, composes the real size/premade-map and parameter helpers, then runs
the player/rules wizard. A failed size/map selection still consumes the five
parameter choices; a canceled custom-parameter sequence does not enter that
alternative. All reached effects survive returning failure to the menu.

After player acceptance, the parent writes preferences through the real F03
writer, reloads advances, units and improvements in that order, stops menu sound,
calls W02 with the source's retain flag and invokes the existing inner new-game
initializer. It then invokes acceleration for every nonzero retained choice,
including a negative result, and presents the human introduction. Returned
preference-write failure does not cancel setup. Required-child exceptions stop
at their boundary and retain the completed prefix.

The introduction list uses the actual V03 knowledge predicate and live pooled
advance labels. It scans down from 99 to 1, selecting at most twelve known
advances whose prerequisites are not both No, then emits the selected names
ascending with a comma and space after each. Future Technology is excluded by
V03 even if its bit is set; advance zero is never inspected. The original's
extra advance-100 prerequisite probe cannot contribute a name because V03
rejects 100, so the helper does not reproduce the unbacked read. This computation
does not grant technology, mutate state, draw randomness or update the year.
The presentation child owns resolving and displaying leader/civilization names.

The caller supplies the selected calendar and gameplay context. Child call
identities append `/parameters`, `/players` and `/initialize` to the supplied
unique entry identity. The shared preference service is used at both custom
parameter completion and final player acceptance. Active clients see publication
before required services and after completion or exceptions; this operation
does not create or revoke client leases.

## Validation and open bindings

Twelve portable integration cases pass 201 assertions. They use independently
authored complete RULES
sections, the actual F03 parser/reloaders, binary premade input, real preference
encoding and exact controlled choices. They cover stage ordering, the two
custom-world saves, size/map/parameter/player cancellation, partial premade
input, both premade topology modes, preference failures, partial reloads,
retained negative acceleration, exceptional client publication, second-game
retention and introduction filtering/order/live labels. W02, V01, balancing,
acceleration and presentation are explicitly supplied children in these cases,
not completed gameplay implementations or original-program captures.

Windows MSVC passes all 539 default-off and 541 SQLite-enabled CTests. Pinned
Emscripten passes all six CTests, including 516 portable cases with 192,940
assertions. All 54 registered native/WASM observations match. The retained logs
are `build/world-entry-{native,sqlite,wasm}-final-build.log` and
`build/world-entry-{native,sqlite,wasm}-final-ctest.log`.

The outer application/session reset, pre-choice menu loop, all non-new-world
menu choices, application dialogs and content installation, full W02/V01/F05
child bindings, cooperative command adaptation and checkpoint restore remain.
The F05 section 2 load/reload calls do not yet spell out their F03 name/leader
preservation arguments. The parent therefore requires those operations from
the content owner instead of inventing a classic application binding; tests
declare replacement on full load and name retention on table reload. Resolve
that precise call-site gap by specification clarification or independent original
observation before claiming complete classic entry wiring. M1 is still open.
