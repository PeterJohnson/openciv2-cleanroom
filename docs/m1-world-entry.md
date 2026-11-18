# F05 new-world entry composition

`src/OpenCiv2.Session/WorldEntry.cs` implements the section 2 parent after selection of
Random World, Premade World or Customize World. It first prepares a fresh game:
replace old world/workspace state, clear mutable rules/text and file scratch,
revoke previous clients, and retain only preferences/configuration/identity
generators. Every new attempt repeats this boundary. A nested world guard then
protects inactive setup storage through all choice and gameplay children. One
content factory returns owned `LoadedContent`; the shared `RuleInstaller` installs
it into fresh rule/text state. The parent composes the real size/premade-map and
parameter helpers, then runs the player/rules wizard. Failed size/map selection skips discarded parameter
choices; a cancelled custom-parameter sequence also skips the automatic alternative. All reached effects survive returning failure to the menu.

After player acceptance, the parent writes preferences through the real F03
writer, reinstalls advances, units and improvements from that same input in order,
retaining names, then stops menu sound,
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
parameter completion and final player acceptance. Previous clients are revoked
before setup; partial setup state is visible only to its owner. Successful setup
requires explicit admission of new client bindings.

## Validation and open bindings

Shared integration cases use independently authored complete RULES sections, the actual F03 parser/reloaders, binary premade input, real preference
encoding and exact controlled choices. They cover stage ordering, the two
custom-world saves, size/map/parameter/player cancellation, partial premade
input, both premade topology modes, preference failures, partial reloads,
retained negative acceleration, old-client rejection, fresh-game
isolation and introduction filtering/order/live labels. W02, V01, balancing,
acceleration and presentation are explicitly supplied children in these cases,
not completed gameplay implementations or original-program captures.

Fresh-game reset is implemented. Non-new-world menu choices, application dialogs
and content selection adapters, full W02/V01/F05 gameplay children, cooperative
command adaptation and checkpoint restore remain at their owning milestones.
Main-menu rendering has no AI lifecycle prerequisite.
The earlier parser-argument uncertainty no longer requires an opaque load/reload
service protocol. Under F05's fresh-game and selected-content contract, the full
install populates fresh rule/leader/name state. Final table updates reuse that
same input and retain names; they cannot reopen a changed source or import
previous-game names. This is the shared semantic installation policy, not a claim
about original parser flags or allocation traces. Tests change the backing source
after its one load and verify final definitions, name retention and closed readers.
The interactive choice adapters and full owning gameplay children still belong
to their later milestones.


Current C# sources and tests are mapped in [implementation coverage](testing/coverage.md).
Run the [native/browser and CLI checks](testing/README.md);
[validation status](validation/README.md) records executed evidence and remaining gates.
