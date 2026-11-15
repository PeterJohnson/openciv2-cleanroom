# F05 premade-world preparation

Status: the accepted-file premade path now composes the real F06 raw-map reader
with F05 section 3 preparation in `session/premade_world.cppm`. It returns Premade
Cylindrical or Premade Flat for the later setup wizard; the shared source type
also includes Generated. The native chooser and outer menu remain separate
interactions.

## Ordered behavior

Cancellation leaves state untouched. An accepted path opens a binary input and
reads bounded owned bytes. Decode and close complete before installation. Short
or malformed inputs and close failures report failure without changing the map,
starts or setup dimensions. Trailing bytes are accepted. A close exception is
never retried.

Raw records convert to the common `MapInput`. New storage is prepared before
replacing the existing map, so allocation failure retains the old map. Successful
installation publishes starts and setup dimensions before F05 preparation.

Successful preparation clears tile features, the entire radius metadata byte,
revelation, regions and fertility, and makes occupants Unowned. Terrain/terrain
flags survive.
An independent [original-game observation](m1-original-premade-observations.md)
resolves the low radius bits as cleared by this F05 path. W01's ordinary claimant
setter still preserves them. Remembered layers 1 through 7 clear; the barbarian
layer, region summaries, coarse buffers and process fallback survive. F06 allocation itself has already
replaced coarse storage with unproduced cells on the normal file path. Calling
the post-read helper directly does not allocate or rewrite those cells.

Seeds below two take one F01 game-tick sample without a prompt. Larger seeds ask
Use Seed: only zero replaces the seed; every nonzero response retains it. Hints
are considered after that choice/clock operation, using current X values. Any
nonnegative X among all 21 entries prompts Use Start Locations. Only zero clears
all X and Y values to -1. The prompt does not validate coordinates or Y values.

Finally, the current stored topology controls the Flat World rule and returned
premade source. Other rule bits, game-status classifications/outcomes, preferences,
world parameters and gameplay randomness are retained. A later wizard may change
the rule. Parent dispatch samples parameters only after successful selection;
failed map selection returns without discarded RNG draws under F05/X01.

## Publication and verification

The owner may run setup before client admission. Actual choice/clock/notice
boundaries publish to active clients, as does completion. The operation does not
create or revoke leases. Pending commands and missing required service bindings
reject before input. Allocation admission is not gameplay reentry.

Portable tests use independently authored binary inputs and specification
expectations. They cover exact normalized tile bytes, retained backing, all
seed/response gates, malformed and absent hints, live callback changes, short
items, every allocation boundary, close errors, missing services, second-game
topology, client publication and the automatic parameter continuation. Native
integration exercises real case-insensitive binary files, unchanged input bytes
and a truncated tile item. The original-game comparison used an independently
authored 4,000-tile map with every radius byte set to `0xff`; the original saved
every radius byte as zero before the first human action, with no cities present.
The report distinguishes that whole-path result from isolated preparation tests.

Fifteen portable premade cases pass 715 assertions; one native filesystem case
adds 14 assertions. Full verification passes 494 native CTests and 496 with
SQLite enabled. Pinned Emscripten 4.0.23 passes six CTests, including 471 portable
Catch2 cases and 190,658 assertions. All 54 registered native/WASM X02 results
and consumed prefixes agree; this F05 path is not yet registered in X02.
Pinned clang-format 20.1.8 formatted 252 owned files. The F05 inventory hash
reflects the reviewed radius-byte clarification. Local final logs are
`build/premade-world-{native,sqlite,wasm}-final-build.log` and
`build/premade-world-{native,sqlite,wasm}-final-ctest.log`.

Premade preparation stops before W02. Terrain finalization, region/connectivity
rebuilding and start-location selection remain required. The
[player/rules wizard](m1-player-setup.md) and [new-world entry parent](m1-world-entry.md)
now compose this path. Outer menu/session lifecycle,
V01/F05 creation/placement/balancing/acceleration and application
interaction binding remain required. These tests do not establish a playable
generated world or complete M1.
