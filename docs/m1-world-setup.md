# F05 world size and parameter selection

Status: F05 section 3's generated-size and customized-parameter helpers, and
section 2's five-draw parameter path and post-selection dispatch, are implemented
in `session/world_setup.hpp`. The owning size/dialog/file interactions are explicit
services. This increment does not generate terrain or complete the player wizard.

## Implemented behavior

The size helper clears game-status flags, resolves all three presets, and
repeats custom input until accepted, canceled or unavailable. Decimal prefix
conversion reduces to signed 16-bit values before dimension/area checks. This
is distinct from F03's radix-prefixed rule-token conversion. Every custom prompt
starts at 50×80, including retries. Custom Cancel returns to the preset menu;
unavailable custom content returns failure. Ordinary presets update the remembered
preset; Custom preserves it. The helper does not save preferences.

On every normal exit, including cancellation, the helper classifies the retained
area and doubles the working width with signed 16-bit reduction. Invalid custom
dimensions survive until overwritten. A missing service or exception stops at
that boundary with prior effects, without pretending that a normal exit occurred.

The five-draw route visits land mass, land form, temperature, climate, age.
Customized parameters visit climate before temperature. Random land form starts
at zero and copies live land mass only for residue zero. Other randomized values
use primary-modulo-three minus one. Each site has a semantic name under
`F05.world.*`, attempt zero, and a caller-supplied unique invocation identity.
There is no reseeding or rollback of consumed choices.

Accepted ordinary parameter rows store normalized values in both setup and
preferences. Negative responses retain `choice-1` in setup and stop without
updating that preference or visiting later dialogs. Random preserves that
parameter's remembered value. A positive row outside the specified 0..2 domain
stops at consumption with prior effects; it is not converted into a generated-world
parameter. After all five succeed, the existing F03 writer
saves both CIV2.DAT items, with its actual partial-write cleanup. Returned file
failure does not retroactively cancel accepted choices.

A failed size/map selection returns without discarded parameter draws. These
samples and abandoned scratch have no gameplay consumer after fresh-game isolation.
Cancellation within customized parameters also returns without the automatic
alternative. Accepted preference edits retain their documented effects.

## Ownership and boundaries

`GameState::world_setup` retains working dimensions and five parameters separately
from the currently allocated map. The size helper consumes its current width as
U and leaves W; it neither halves nor resets it at entry nor reallocates map storage.
The outer setup composition must supply the retained working dimensions at that
boundary. The [premade path](m1-premade-world.md) now synchronizes them at its real
F06 allocation boundary; other load/menu callers remain separate work.
Construction zeroes are not original setup defaults.

Services see live state after preceding writes. Active sessions publish before
each interaction and file callback, and at normal or exceptional completion;
inactive setup also works with the owner's explicit gameplay RNG. No client
admission, lease changes or implicit new-game resets occur here.

## Evidence and remaining work

Eighteen portable cases cover 519 assertions on Windows and WASM: preset/area
boundaries,
decimal reduction and rejection, custom retries and cancellation, unavailable
content, retained dimensions, normalized persistence, distinct random orders,
all land-mass/form residue pairs, cancellation at each parameter, missing choices,
live callback changes, actual F03 failure cleanup, and active-client publication.
The [original Windows observations](m1-original-setup-observations.md) independently
confirm repeated custom defaults, acceptance of 65586×80, normalized preferences,
remembered preset behavior and Random preserving preferences. Their report
explicitly distinguishes observed outcomes from private-state/specification tests.

Full final verification passes 478 native CTests and 480 with SQLite enabled.
Pinned Emscripten 4.0.23 passes all six CTests, including 456 portable Catch2
cases and 189,943 assertions. All 54 registered native/WASM X02 results and
consumed prefixes still match; setup is not yet registered in X02. The F05
inventory hash was updated after reviewing the specification clarifications.
Pinned clang-format 20.1.8 formatted 249 owned files. Local ignored final logs
are `build/world-setup-{native,sqlite,wasm}-final-build.log` and
`build/world-setup-{native,sqlite,wasm}-final-ctest.log`.

The [premade path](m1-premade-world.md) now composes its real F06 reader and
preparation. The [player/rules wizard](m1-player-setup.md) and
[new-world entry composition](m1-world-entry.md) now join these helpers.
Outer new-session/menu lifecycle,
W02 generation, V01 creation, F05 placement/balancing/
acceleration, application interaction binding and X02 setup registration remain
required work. The [inner initializer](m1-new-game.md) is already implemented
with required creation/balancing children. M1 remains incomplete.
