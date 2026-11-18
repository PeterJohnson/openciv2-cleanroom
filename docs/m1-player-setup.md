# F05 player and rules setup

`src/OpenCiv2.Session/PlayerSetup.cs` implements the section 4 wizard through accepted
city style and opponent overrides. The engine owns ordered mutations and retry
destinations; injected services supply modal responses and the P03/P04 portrait
child. The caller supplies the selected validated calendar and gameplay random
context. This helper can run before client admission; an active runtime publishes
before every service, on completion and on exceptional exit.

Difficulty, opponent count, barbarians, rules, identity, custom text and style
are written when reached. Canceling a later dialog retains earlier effects.
The two entry-time locals survive retries, including a negative acceleration
result followed by Standard Rules. Advanced reads checkbox state even when its
modal result is cancellation. Premade topology controls initial Flat selection
while preserving the previously remembered topology bit. Acceleration uses the
real F04 calendar helper, including its first-launch write and incoming scenario
state; accepted preferences follow the independently observed F05 contract.

Leader markers restore before Gender. Tribe selection installs the preferred
human color and gendered portrait without normalizing unrelated civilization
flags. Custom names and seven gendered titles use classic byte strings with a
23-byte limit; shorter copies preserve bytes after the new terminator. Name
edits survive entering Titles, canceled title edits do not, and a required
portrait child can retain its accepted effects. Unavailable text and unsupported
backing stop at the reached boundary with prior writes retained.

Random opponents, random barbarian activity and roster selection use separate
`F05.setup.opponent-count`, `F05.setup.barbarians` and `F05.setup.opponent-slot`
sites. Each key combines the supplied call identity with its per-site draw
ordinal and attempt zero. Ordinals continue across retries. Roster draws consume
barbarian, human and duplicate hits without a finite retry cap. Pick Opponents
visits admitted nonhuman slots ascending and filters tribes by preferred color;
Random and later cancellation retain earlier pending overrides. Those eight
signed-byte overrides belong to world state and begin Absent.

## Validation and remaining integration

Shared player-setup cases cover retry destinations, immediate
state/preference writes, all 192 combinations of advanced rule bits/source/old
topology, entry-local retention, calendar binding, custom name/title/portrait
ordering, byte limits and partial failure, exact controlled-choice consumption,
opponent redraw/override retention and active-client publication. Expectations
are authored from F05/F03/F01. The separate
[original UI/file observations](m1-original-player-setup-observations.md) establish
the newly clarified preference and name-dialog behavior; no original executable,
screenshot or historical harness is an implementation-test input.

The [new-world entry parent](m1-world-entry.md) now joins this wizard to size/map
selection, parameters, preference saving, required rule reloads, inner
initialization and the introduction list. Outer menu/session lifecycle and
W02 generation, V01 creation/balancing and acceleration bindings remain.
The synchronous service API does not yet provide a
cooperative command continuation or pending-wizard checkpoint restore. Complete
M1 lifecycle, content admission and original-peer profile gates still require
their owning integrations. This helper does not claim full new-game equivalence.


Current C# sources and tests are mapped in [implementation coverage](testing/coverage.md).
Run the [native/browser and CLI checks](testing/README.md);
[validation status](validation/README.md) records executed evidence and remaining gates.
