# S04 — Rules and asset editors

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in S04 |
| --- | --- |
| Gameplay | Accepted rule edits, renaming/reference propagation and necessary consumer refresh; preserve game meaning after editing. |
| Original files/content | F03 rule grammar/conversions, P03 source-sheet pixels/markers, sound identities and valid original exports. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Scalar-editor controller, property capture order, private scratch, click/tool geometry and native media/provider calls. |

The algorithms below specify game values/decisions and their real dependencies,
plus explicitly identified external data definitions. They do not prescribe
function boundaries, object/global layout, allocation strategy or incidental
helper/store order. Preserve retained data where an admitted game/file/peer
consumer uses it; represent that data flow explicitly rather than emulating
original memory. Native or synthetic callbacks do not create new game boundaries.
Historical backing-byte/fault/trace cases are reference unless an actual external
format or supported semantic operation consumes the value. Missing gameplay or
format meaning is an owner gap, not permission to invent it or require arbitrary
original bytes. This scope controls stronger historical wording below.

**Required profile:** [X01 fresh-game isolation and bug repairs](X01-classic-compatibility-contract.md#fresh-game-isolation-and-supported-bug-repairs)
and [behaviorally significant ordering](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
override conflicting procedures and examples below. Retention applies within a
game or to explicit imported inputs, not across new games. Confirmed original-bug
stack/backing/stop cases are optional diagnostics; use documented safe repairs.
Only real effects and callbacks impose ordering; test adapters cannot invent them.
Unspecified safe continuations remain explicit gaps, not required arbitrary bytes.

Target: classic
Civilization II Multiplayer Gold Edition **5.4.0f, patch 3**. This document owns
rule-editor staging, controls, publication, name propagation and text replacement;
city/icon editing, import/export and unit-sound replacement. It specifies state
and file effects, including partial completion and cancellation.

[F03](F03-rules-and-configuration.md) owns rule meanings, reading, names and
reference vocabulary. [F01](F01-determinism-and-arithmetic.md) owns exact scalar
arithmetic; [F02](F02-game-state-and-invariants.md) owns shared identities.
[S03](S03-world-and-scenario-editors.md) owns world/cheat commands and event-text
output; [S02](S02-event-language-and-execution.md) owns event records and their
lifetimes. P01/P02/P04 own native controls and input delivery; P03 owns palette,
surface, sprite and image-file primitives; P05/P06/P10 own map/city/audio
presentation. These children remain ordered operations, not assumed-success
services. This editor does not implicitly execute normal gameplay mutations.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
semantic publications and failure effects. Only dependencies within the binding
scope above constrain evaluation; storage/API choices need no original-trace
equivalence proof. External file/wire definitions remain binding at adapters.

Rule/name publication, apply/cancel effects, source-image output and file failures
retain their dependencies. Independent conversions or pixel work may be grouped
only within a boundary that exposes no intermediate result or failure prefix.

## Compatibility policy

X01's [presentation policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy)
permits new rule/asset controls, preview art, layout and rendering, while keeping
rule meanings, names/references, staged versus committed edits, nested child
publication and cancellation effects. Supported authored sprites/sounds retain
their identity/content; classic sheet/text/media exports remain original-readable
with their required dimensions, palettes, transparency and external encoding.
On-screen scaling/blitting may differ from export processing. The
[input policy](X01-classic-compatibility-contract.md#input-admission-and-rejection-policy)
permits validation and safe rejection outside nominal custom content. Harmless
widget failures may simplify cleanup, but failed writes and already committed
asset/file changes are not rolled back by that exception.

## Implementation boundary

The main sections use logical values, identities and operations. Original memory,
allocation and platform mechanics are recorded in [Appendix D](#appendix-d-original-implementation-reference). Preserve required
state changes and externally visible results without reproducing those mechanisms.
Exact numeric domains and file/wire encodings remain required wherever specified.

## Rule and asset edit contract

The binding definitions are field meanings/domains/conversions, accepted edits
and reference updates, plus original rule/image/sound formats and source-sheet
geometry. The scalar-controller recipe, screen tool geometry, native controls,
shared scratch and capture order are reference. Implement draft documents and a
validated Apply if desired. A separate accepted icon/sound/export command keeps
its effects when another draft is cancelled; merely changing a control need not
commit it. No failed widget getter supplies old private bytes to a game edit.
Revalidation, safe cleanup and staged file replacement follow X01. Source-image
coordinates remain binding even when the editor uses a different screen layout.

## 1. Domains and publication boundaries

Inputs are the live rule tables, name service, civilizations, event list,
scenario/home directories, shared editor scratch/defaults, control descriptors,
control replies, file results, sprites/palette, sound resources and callbacks.
Nominal editor rows and selections are the offered domains below, strings fit
their admitted fields, resources provide the expected sections, and record/
control identities remain valid across calls. Directly edited values may have
narrower admission than accepted F03 input files. All full working integers use
F01 signed/unsigned32 behavior; byte and signed16 reductions are stated where
they affect subsequent reads, independent of the implementation's storage.

Rule editors share scratch names and values. A session loads only its specified
fields; other fields retain prior contents. Most scalar edits change scratch
until OK, but nested icon and sound commands can change live assets or files
immediately. The old Cancel/Running/shared-scratch sequence is reference. Preserve separately
accepted child effects; draft state can be discarded. The effects editor's output
meaning remains file-only. New editor transaction boundaries are permitted for
unsubmitted work, without undoing an already accepted command.

State is live unless explicitly captured. In particular control IDs, the active
editor, current row, names and rule values may be reread after callbacks. A
failed text getter supplies no accepted edit; keep the draft unchanged or report
the error. Original retained private input is diagnostic evidence, not a required
production argument.
Unsupported references, cycles and missing resources retain their separate
read/fault/termination boundaries. A declared stricter input profile is allowed.
Out-of-bounds writes are excluded bugs under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
even when an ordinary editor choice reaches mapped adjacent globals. Preserve
prior valid effects and safely reject, omit the established unsafe write or stop;
do not translate corruption into assignments to unrelated logical state.

## 2. Shared scalar-editor controller

Technology, improvement, unit and terrain sessions publish Running and the active
editor, initialize current row0 (improvements start at row1), construct the
ordered controls, load scratch from live rules and load their displayed values. P04 realizes the modal and
paint lifecycle. A normal exit releases its background resources. Opening an
editor does not reread all rules; the effects editor is the exception in §7.

The field tables below give capture order. Numeric fields use decimal conversion
of the control's current text, clamp to the stated inclusive bounds, publish to
scratch and count a correction when the stored value differs from the parsed
value. Combo fields publish their selection after the stated offset, without a
second membership/range clamp. Hidden controls are still captured. Numeric
controls nominally limit input to three characters; the input filter admits
ASCII digits, minus and P02's translated key214. It does not enforce minus
position or require a digit. Missing or overlong control output remains a
producer/read boundary or excluded overwrite, not invented zero.

Capture traverses field/control descriptor order, excluding the row selector.
For technology, numeric capture reads text, converts, reads the field ID,
clamps/stores, **rereads the ID** and compares that field with the parsed value.
Unit numeric capture reads the ID before getting text. Terrain/improvement
capture gets text, reads the ID, then converts. Combo capture reads selection
before field ID. These are live control queries; a callback can distinguish
them. Original control IDs and table layouts are validation details in Appendix A.

On row-selector change, first capture the old row. With no corrections, read
the selector, publish the new row, load controls and perform the editor-specific
visibility/dependency refresh, then redraw. With corrections, restore the old
selector, reload its corrected values, redraw, show NOTICE with editor parent,
clear parent and focus the row selector. Remain on the old row. The terrain
error branch restores its **raw terrain index**, without the usual compressed
row mapping. Other specified combo callbacks need not capture all fields.

On OK, capture controls. If corrections occurred, reload/redraw, show NOTICE,
clear parent and refocus, keeping the session open. Otherwise perform the
editor-specific publication and writers below. A writer failure shows an I/O
error but still closes the session and updates menus. Earlier table, event or
file changes remain. There is no automatic retry, reload or rollback.

Ordinary rename takes a bounded default from the captured current row, retries
empty replies, breaks on exactly−1 and commits only a nonnegative final reply.
It changes the staged name and rebuilds the relevant row/reference labels while
restoring their captured selections. Limits are technology19, improvement24,
unit14 and terrain14 characters. Renaming does not reindex rule identities.

### Name publication

Nominal apply replaces each name through its existing name identity, with the
editor's maximum admitted text. Original fixed-width pool prefix copying,
long-file-name tails and aliasing are recorded in Appendix A; a safe ordinary
string representation need not reproduce the physical pool. Preserve defined
name-service results inside the declared profile. Do not silently treat a pool
spill into a different name as legitimate propagation. Event-name propagation
is the explicit separate operation in §5.

## 3. Technology editor

Load all100 rows ascending. Copy at most39 characters of the current name and
terminate. Prerequisites are advance identities or No/None; nominal epochs
are 0…3 and categories 0…4. AI values/modifiers have the admitted range
−128…127. Use those logical values in the controls. When accepting additional
raw rule codes, retain the original signed view at capture before interpreting
the control selection; the reference annotations are in [Appendix E](#appendix-e-original-numeric-annotations). The
ordered fields are:

| Field | UI values / capture |
| --- | --- |
| Prerequisite1, prerequisite2 | No, None, then advance0…99; selection−2. |
| Epoch | Four entries0…3; direct selection. |
| Category | Five entries0…4; direct selection. |
| AI value, civilized modifier | Decimal, clamp−128…127. |

Apply all100 names and six fields in that order per row. Preserve the rule
code associated with each logical choice; nominal selections need no redundant
narrowing. For admitted extra selections, apply the original modulo-256 field
conversion before subsequent reads. Do **not** recompute the separate Enabled flag from newly
edited prerequisites. Then set global achievable-advance count0 and scan all100
live rows ascending. For each row whose live Enabled is nonzero, increment the
count; process prerequisite1 then2. While the signed prerequisite is nonnegative
and points to a disabled advance, replace the candidate with that advance's
first prerequisite. Stop on a negative candidate or an enabled advance and
store the resulting signed8 reference. There is no cycle detection or original
pre-apply graph validator. A cycle through disabled nodes can fail to terminate;
an out-of-domain nonnegative reference needs actual read backing or a stop.
The independent reconstruction's graph validation is a declared repair.

Register CIVILIZE and run §8 replacement. This writes the applied/relinked live
tables, not the pre-relink scratch. Do not rerun player research, grant effects,
unit obsolescence or the entire F03 loader here.

The usage list first scans live unit types0…61 whose prerequisite equals the
selected advance, inserting one units header before the first match. Then scan
live advances0…99: match first prerequisite preferentially, otherwise second;
insert one advances header before the first match. Show the dependent name alone
if its other prerequisite is negative, otherwise its name plus the conjunction
and other advance's name. This uses **live tables/names**, not unsaved scratch,
even after a rename or control edit. Epoch/category selection callbacks capture
all fields, ignore the correction count, refill usage and redraw. Other
non-selector combo changes have no additional handler work. Rename rebuilds
all three advance-name combos, restoring their three captured selections, then
refills usage and redraws.

Icon editing selects the live staged category/epoch sprite and calls §10 mode0
with the technology-sheet writer, then redraws. It edits a shared category/epoch
icon rather than a unique icon per advance.

## 4. Improvement and wonder editor

Load67 rows0…66 ascending: names up to39 characters, unsigned8 cost/upkeep and
signed8 prerequisite. Load expiry into scratch **only for row>39**. Row39 is the
first wonder but retains the previous scratch expiry field. Ordered controls
are cost, upkeep (clamp0…255), prerequisite and expiry (No/None/advance0…99,
selection−2). Both combos are captured even when expiry is hidden.

Apply all67 names and cost/upkeep/prerequisite, reducing scalars to their byte
domains. Apply expiry only for row>39. Neither editing nor saving row39 changes
its live expiry. Register IMPROVE then ENDWONDER; the latter writes all28 live
expiry values, including unchanged row39. Do not execute building acquisition,
maintenance, wonder expiry or other C08 effects immediately.

After selecting a row, disable Icon only at row0 and enable it otherwise. Hide
expiry for rows<39, show it for rows≥39, then load controls and redraw. Thus the
first wonder exposes an expiry control which its apply loop does not publish.
Rename rebuilds only the row-name combo and retains selection. The ordinary
icon command selects that row's building/wonder sprite using §10 mode4 and the
building-sheet writer. Miscellaneous selection routes to map-cursor/explosion
assets as specified in §9.

## 5. Unit editor and event-name propagation

Load62 rows0…61 ascending: name up to39 characters; signed8 prerequisite,
obsolescence, role, domain, cost, holds, movement, range, attack, defense, HP and
firepower; flags remain unsigned32. Movement scratch is signed movement divided
by the live unsigned road multiplier, truncating toward zero. HP scratch is
signed HP divided by10. A zero road multiplier faults after the preceding
fields of that row have already been copied.

| Field order | UI values / capture |
| --- | --- |
| Prerequisite, obsolescence | No, None, advance0…99; selection−2. |
| Role, domain | Ordered role/domain choices; direct selection (Appendix A). |
| Cost | Decimal clamp1…127. |
| Holds; movement; range; attack; defense; HP; firepower | In this order. Lower bound0 for each; upper bound127 except movement and HP below. |

After constructing controls and before loading unit rules, set the movement
upper bound to `127 / liveRoadMultiplier` and the HP upper bound to12. These
bounds remain in the shared descriptor table; capture does not recalculate them
on every edit. A zero multiplier faults at this setup division, before the
normal load loop. A later change to the multiplier can still distinguish the
setup bound, the load division and the apply multiplication.

The domain callback only changes visibility: Land hides holds and range, Air
hides holds/shows range, Sea shows holds/hides range. Other domain values retain
the current visibility. It does not capture controls or clear hidden values.

Abilities uses the captured current row. Clear shared checks, prefill each of
15 capability positions, publish the staged unit name and show ABILITIES.
Exactly result0 accepts. Reconstruct a new set from those checks in ascending
position order and store it, clearing all other flag positions. Other replies
retain the old set. Clear parent on normal return.

On successful OK, before applying any unit rules:

1. Scan62 types ascending. Compare the current live name with its staged name
   case-sensitively. For differences run the event rename below and accumulate
   its signed32 count.
2. If the accumulated count is nonzero, run S03's event-file writer. Report its
   failure, then continue. No event-loaded gate is added.
3. Apply all62 staged names/fields. Reduce ordinary fields to their byte domains;
   movement becomes the low byte of staged movement times the **live** road
   multiplier; HP becomes the low byte of staged HP times10. Flags retain32 bits.
4. Register UNITS, write rules, report failure if needed, then close/update menus.

The movement/HP divisions and later byte products need not round-trip unusual
input files. Do not rescale existing units' moves/damage, refresh every unit or
reinitialize research/production as an unstated side effect.

Event rename walks live list order, reloading each successor. In each node,
visit trigger unit name, MoveUnit name, CreateUnit name and the retained fourth
unit-name reference, in that order. If present and exactly case-sensitively equal
to the old name, copy the new bounded name into that stored string and increment
the count. Do not change numeric selectors, filter by enabled action flags or
compact the event pool. Multiple references may alias; earlier renames can
change later comparisons, and sequential type renames can cascade. Original
15-byte padded writes into shorter event strings are excluded overwrites; use
safe replacement or stop rather than corrupting the next event/name allocation.

Rename changes only the staged row-name combo. Icon editing uses the selected
unit sprite, mode11 and the unit-sheet writer. Sound editing uses §12's staged
attack/domain/range/flags at the selected row. These child effects survive
cancelling the parent unit-rule editor.

## 6. Terrain editor

Load33 rule rows ascending. Names retain at most39 characters. Copy movement,
defense and three yields as signed8 into scratch for every row. For base rows
0…10 also copy irrigation result/bonus/time/AI-government, mining equivalents
and transform result; references are signed8 and other bytes are reinterpreted
signed8. Resource rows retain all other scratch fields.

The row picker offers0…32 except13 and24. To map terrain to selection, decrement
if>23, then decrement the resulting value if>12. To map selection to terrain,
increment if>12, then increment the resulting value if>23. No range clamp is
added. The ordered fields are movement, defense, food, shields, trade,
irrigation result/bonus/time/government, mining result/bonus/time/government,
transform result. All nine numeric fields clamp0…127. Result combos offer
ImproveInPlace, NoResult and terrain0…10 (selection−2); transform offers NoResult
then terrain0…10 (selection−1). Government choices are0…6 directly. These
controls are all captured regardless of visibility or resource-row status.

For base rows, show irrigation/mining/transform selectors. In each improvement
branch: selection0 shows bonus/time/government; selection1 hides all three;
other selections hide bonus but show time/government. For resource rows hide
all improvement numeric fields and all five auxiliary combos. Improvement-result
callbacks change visibility/redraw only; they do not clear or capture values.

Apply33 names and the five basic fields, reducing each scalar to its byte
domain. For rows0…10 additionally apply both improvement groups and transform.
Do not apply resource rows' retained auxiliary fields. Register TERRAIN, write,
report failure if needed and close/update menus. No current-map transformation,
region repair or city yield recalculation is inserted.

Rename rebuilds the row selector and all three terrain-result combos while
restoring their captured selections; retain the two omitted resource rows and
their mapping. Icon routing is in §9.

## 7. Tribe, government, commodity and cosmic editors

### Tribe session

For each tribe0…20, find the first civilization1…7 whose tribe matches, without
an Alive test. If found, load current plural/adjective through its name queries
and its current city style. Otherwise resolve the tribe's plural/adjective
through the absolute value of their signed16 string references and use its
rule city style. Retain at most23 characters of these staged names and terminate.
Load attack, expansion and civilization traits plus1 into scratch.

Load each government0…6 name and male/female generic title into separate staged
strings; load commodities0…15. The original title termination defect and exact
capacities are in Appendix A. The nominal controls are tribe, city style0…3,
attack0…2, expansion0…2 and civilization0…2. All five descriptors are combos;
the generic adjective-text branch is inactive in the nominal descriptor table.
Selecting a tribe stores the previous controls, publishes selection, loads the
new controls and redraws. Other selections store/redraw immediately. No scalar
clamp/notice gate is applied.

Government rename offers7 entries, commodity rename16, title rename14 ordered
by government then male/female. A negative choice cancels. Retry empty input,
break on exactly−1, commit only nonnegative replies. Government/title names
admit14 characters and commodities9. These operations change scratch only.

On OK, capture controls, perform the CITY rewrite below and ignore its result,
then apply tables. For each tribe0…20, find the first matching civilization1…7
again; if present publish signed16 city style to it. Always publish signed16
tribe style and low-byte `(scratchTrait−1)` for all three traits. Do not write
the staged plural/adjective or leader names into those live records. Apply
government names and male/female generic titles, then commodity names. Register
GOVERNMENTS, LEADERS and CARAVAN in that order, write rules, report an error if
needed, close and update menus. Custom per-tribe title references are not
replaced merely by changing a generic government title.

### CITY header replacement

Form CITY plus the language extension and capture current directory; change to
scenario directory. If it equals home case-sensitively, do no rewrite and retain
failure result0. If distinct but CITY is absent, set result1. Otherwise delete
existing CITY.TMP if possible, require successful rename of CITY to CITY.TMP,
open the temporary for reading then CITY for writing, and require both handles.

Read chunks of at most255 characters. For a chunk starting with `@`, skip rename
matching when its next four characters match STOP case-insensitively. Otherwise
scan tribes0…20. Find first matching civilization1…7 without Alive filter; use
its raw local plural-name field, or the absolute tribe plural reference if no
match. Compare that old name as a case-insensitive **prefix** after `@`, using
its length, without requiring the header to end there. On first match replace
the entire chunk with `@` plus staged tribe plural plus newline and uppercase
it. An empty old name can match any header. Copy other chunks verbatim. EOF
sets result1; a failed write leaves result0 and stops.

On normal exit close source then destination. Regardless of the rewrite branch,
probe CITY.TMP and remove it if present, ignoring removal failure. Restore the
saved directory and return. Thus a failed rewrite can retain a partial CITY
and delete the temporary original. No rollback is added.

### Cosmic/effects session

Opening this editor saves the22 live parameters, invokes F03's cosmic loader,
copies the loaded parameters to editable scratch, then restores the22 saved
live parameters in order. Loader effects outside those parameters remain.
Consequently defaults come from the selected rules resource, while the running
game's parameter values are restored. The displayed rows combine edited value,
saved live value and successive DEBUG/EDITCOSMIC descriptions. Clear the list,
read trimmed resource lines until their length is below3 or22 rows have been
added, then close the resource. Failed reads retain F03's reader boundary rather
than manufacturing descriptions. This is not the other editors' live-table
snapshot behavior.

| Parameter order | Inclusive editor bounds |
| --- | --- |
| Road movement multiplier; trireme loss chance; food per citizen | 1…10; 1…100; 0…10 |
| Food-box rows; shield-box rows; low/high-government settler food | 4…20; 4…20; 0…10; 0…10 |
| Unhappy-size threshold; riot factor; aqueduct/sewer sizes | 4…12; 10…100; 4…50; 4…50 |
| Technology paradigm; transformation time | 3…10; 5…100 |
| Monarchy/Communism/Fundamentalism free support | Each0…8 |
| Communism capital distance; Fundamentalism science loss; production-change penalty | 1…20; 0…100; 0…100 |
| Paradrop range; spaceship mass/thrust paradigm; Fundamentalism max science | 4…100; 25…200; 0…10 |

Editing captures the selected row, publishes parent and its bounds, formats the
staged value as default, then requests three characters. A nonnegative reply
converts decimal, clamps with F01 and stores scratch, refills the list and
restores selection. All normal replies focus the list, clear parent and redraw.
OK registers COSMIC and writes §8, reports failure if needed and closes. It does
**not** copy scratch to live cosmic parameters or call the loader afterward.
Cancel only closes. Neither handler adds the other rule editors' menu update.

## 8. Rule text and replacement

### Section registration and file procedure

A section consists of a name and an optional writer. Setting the first section
replaces entry0 and terminates the list at entry1. Adding searches existing
entries in order, appends if fewer than7 are registered and installs the next
terminator. These operations do not sort or deduplicate sections. Writers
receive destination then source handles; some consume source rows themselves.

Form `RULES.` plus language extension, capture current directory, change to
scenario directory and begin with result0 and no handles. Directory change
failures are not independently checked. The filename probes/opens therefore
use the directory actually established by the host operation.

* If scenario equals home case-sensitively, or scenario RULES is absent: change
  to home and open RULES for text reading. For equal directories, remove an
  existing RULES.BAK, requiring success, then open RULES.BAK for text writing;
  show the saving-to-backup warning even if that open failed. For distinct
  directories, change back to scenario and open RULES for text writing.
* Otherwise remove existing RULES.BAK, requiring success; rename scenario
  RULES to RULES.BAK, requiring success; open backup for reading then RULES for
  text writing. Neither rename nor truncation is subsequently undone.

With both handles, process registered sections **in registration order**:

1. Form uppercase `@` plus section name. Read source chunks of at most255
   characters and write each to destination **before** stripping its newline,
   trimming whitespace and comparing case-insensitively with the desired
   header. EOF or failed copy stops with result0. Missing/out-of-order sections
   are not inserted or found by restarting at the beginning.
2. Invoke the section writer if present. Ignore its return value. Its source
   consumption and shared-reader changes take effect before the next step.
3. Read and discard chunks until raw chunk length≤1, requiring such a chunk;
   write that separator to destination. A whitespace-only line longer than1
   is not a separator. The LF text-mode view of an ordinary blank line has
   length1. EOF here is failure, even after successful replacement output.

After all sections, copy the remaining chunks. EOF now sets result1; failed
write retains0. On normal exit close source then destination and restore the
captured directory. Failed capture needs supplied prior bytes or a stop; it
cannot invent a restore path. No temp-file transaction, automatic rollback or
successful-child requirement is added. In particular home edits produce
RULES.BAK and leave the original RULES unchanged.

### Writer vocabulary and exact nominal text

All writers append to shared line text in the stated order, then attempt one
text write per row. The ordinary section writers ignore those individual write
results and return1. Names and scalars are read from live tables as each append
occurs; they are not a second table snapshot. Nominal text uses F03's character
encoding and text-mode newline conversion. The following notation describes
file content, not a mandatory buffer layout:

* `D(v)` is signed decimal without padding; `D2(v)` is decimal padded on the left
  with spaces to minimum width2. Width is a minimum, never truncation.
* `Name(n,w)` appends the resolved name, `,`, then `max(0,w−nameLength)` spaces.
  The name is queried again to measure padding; when the first measured length
  is below `w`, it is queried once more for the subtracted length. Preserve
  those live name-service queries if a supplied producer can change them.
* `Adv(a,c)` is the F03 advance abbreviation, followed by a comma if `c`, then
  spaces to abbreviation width3. Special values: −2 gives `no, ` / `no `;
  −1 gives `nil,` / `nil`; values≥100 give `err,` / `err`. Other negative values
  enter the original indexed-read path and need backing or a stop.
* `Ter(t,c)` uses the F03 terrain abbreviation and optional comma, without
  general padding. −1 gives `no, ` / `no `; −2 gives `yes,` / `yes`; values≥11
  give `err,` / `err`. Other negative values have the same read boundary.
* `+` below means append, quoted spaces are literal, and `NL` is one newline.
  Numeric byte values are signed8 unless explicitly marked unsigned.

| Section and row order | Text assembled for each row |
| --- | --- |
| CIVILIZE, advance0…99 | `Name(name,19) + D(ai) + "," + D2(civilized) + ",  " + Adv(pre1,true) + " " + Adv(pre2,true) + " " + D(epoch) + ", " + D(category) + "    ; " + Adv(row,false) + NL`. |
| IMPROVE, improvement0…66 | `Name(name,25) + D2(unsignedCost) + "," + D2(unsignedUpkeep) + ",    " + Adv(prerequisite,true) + NL`. |
| ENDWONDER, wonder0…27 | `Adv(expiry,true) + eightSpaces + "; " + liveImprovementName(wonder+39) + NL`. |
| GOVERNMENTS, government0…6 | `Name(name,15) + Name(genericMaleTitle,12) + genericFemaleTitle + NL`. |
| CARAVAN, commodity0…15 | `liveName + "," + NL`. |

UNITS emits62 rows0…61. For each row append:

```text
Name(name,13), Adv(obsolete,true), D2(domain), ", ",
D2(move / liveUnsignedRoadMultiplier), ".,", D(range), ", ",
D2(attack), "a,", D(defense), "d,", (defense<10 ? " " : ""),
D2(hitPoints / 10), "h,", D(firepower), "f, ",
D2(cost), ",", D(holds), ", ", D2(role), ", ",
Adv(prerequisite,true), " ", flagsInDescendingPositions14…0, NL
```

Commas separating expressions in this display are notation only. Each flag
position contributes `1` or `0`; higher positions are omitted. Movement and HP
use signed-byte dividends and truncating division. Zero road multiplier can
fault after the earlier appends, without a completed row write.

TERRAIN emits33 rows0…32. Start `Name(name,11) + D(move) + "," +
D(defense) + ",  "`, then each of food, shields and trade as `D(value)+","`.
For base rows0…10, append irrigation then mining: three spaces,
`Ter(result,true)`, `D2(bonus)+","`, `D2(time)+","`, one extra space for
mining only, then `D2(government)+","`. Append two spaces,
`Ter(transform,true)`, `"   ; "`, `Ter(row,false)`. All rows end with NL.
Resource rows stop after the three basic yields and NL.

LEADERS preserves selected input rather than serializing only live rules. Save
F03's current reader handle, bind the supplied source and repeat21 times:
read/trim one rule line, ignoring the read result; consume male name, female
name, gender integer clamped0…1, color integer, style integer, plural name,
adjective name and three trait integers. Capture the remaining cursor text.
Then emit live male name padded13, live female name padded20, the **old-file
clamped gender**, `", "`, live color, `", "`, live style, `", "`, live plural
padded12, live adjective padded11, signed attack with `D2`, `", "`, signed
expansion with `D2`, `", "`, signed civilization with `D2`, `","`, the captured
old tail and NL. Each padded name uses `Name`; resolve leader name references
through signed16 absolute values. Restore the saved reader handle on normal
completion, but not the shared cursor/text buffer. Do not substitute live
gender, discard custom title tail fields or invent rows after a failed read.

COSMIC consumes one source chunk of at most127 characters for each of22 rows.
Find its first semicolon and emit the staged decimal value padded **on the
right** to width8 followed by the suffix starting at that semicolon. The
suffix carries its original newline. With no semicolon the original formatter
emits `(null)` without a newline. An ignored read failure retains the preceding
chunk; first-read failure requires incoming scratch backing or a stop. The
source reconstruction's empty-line/newline fallback is a repair. Individual
writes are unchecked and result1 follows a completed loop.

## 9. Asset routing and sheet geometry

Asset edits operate on the following live targets. City graphics is an asset
browser: style0…5, size0…3 and variant0…1 selections publish immediately and
redraw. Its OK closes/updates menus; Cancel only closes. Neither is an asset
transaction. A normal rules/city-editor wrapper return repaints the main map.

| Command | Target, pixel-editor mode, save family |
| --- | --- |
| Technology icon | Shared icon at staged category/epoch;0; ICONS technology writer. |
| Unit icon | Selected unit sprite;11; UNITS. |
| Improvement icon | Selected improvement/wonder;4; ICONS building writer. Row0's button is disabled. |
| Improvement miscellaneous | Choice0 map cursor mode5; choices1…8 explosion[choice−1] mode6; ICONS building writer. |
| City icon | City[style][size][variant];1; CITIES. |
| City miscellaneous | Choice0 fortify,1 airbase,2…9 normal civilization flags0…7,10 fortress,11 occupied airbase,12…19 dark flags0…7. Flags use3, other targets2; CITIES. |
| Terrain icon | Base row0…10 or resource row11…21 /22…32 at row modulo11;7; TERRAIN1. For Forest3, Hills4, Mountains5, first choose base0 or overlay1…16; overlays use8 and TERRAIN2. |
| Rivers | Choices0…15 river variants,16…19 river mouths;8; TERRAIN2. |
| Coasts | Choices0…31 grouped quarters;9; TERRAIN2. |
| Terrain miscellaneous | Choices0…2 land improvements,3…11 roads,12 pollution,13 shield,14 hut,15…23 railroads;10; TERRAIN1. |

These choices use their modal help/retry loop and cancel boundary; a direct
out-of-domain reply is not silently clamped to the last offered asset. Original
adjacent reads and added reconstruction bounds are distinguished in Appendix A.
Terrain handlers clear their dialog parent before entering the pixel editor;
city/improvement miscellaneous retain it until return. Their normal return
redraws the parent, including after child cancel.

The atlas contract below is required to import/export classic sheets. Each
rectangle is `(x,y,width,height)` with top-left origin. For nominal cell `n`,
`q(k)=floor(n/k)` and `r(k)=n mod k`. Off-nominal arithmetic uses F01 signed32
truncation/wrap, not an unchecked unsigned array index. The rectangle query
first zeroes outputs in order height,width,y,x, then assigns mode-specific
outputs and returns the count; aliased outputs observe that order. Unsupported
mode returns0 and zero outputs.

| Mode | Count | Rectangle / layout |
| --- | ---: | --- |
|0 technology|20|`(343+37*q(4),211+21*r(4),36,20)`.|
|1 cities|48|If n<24, baseX1 and m=n; otherwise baseX334 and m=n−24 **once**. `(baseX+65*(m mod4),39+49*floor(m/4),64,48)`.|
|2 city miscellaneous|4|`(143+65*n,423,64,48)`.|
|3 flags|18|`(1+15*((n>>1) mod9),425+23*(n&1),14,22)`.|
|4 buildings|66|For n<38: `(343+37*r(8),1+21*q(8),36,20)`. Otherwise m=n−38: `(343+37*(m mod7),106+21*floor(m/7),36,20)`.|
|5 cursor|1|`(199,256,64,32)`.|
|6 explosions|8|`(1+33*n,356,32,32)`.|
|7 terrain|44|`(1+65*q(11),1+33*r(11),64,32)`.|
|8 overlays/mouths|68|For n<64: `(1+65*r(8),67+66*q(16)+33*[r(16)≥8],64,32)`. Otherwise `(1+65*(n−64),331,64,32)`.|
|9 coasts|32|Base `(1+66*q(4),429)`, offsets for r(4)=0,1,2,3 respectively `(0,0),(33,34),(0,17),(0,34)`; size32×16.|
|10 miscellaneous terrain|24|For n<6: `(456,100+33*n,64,32)`. Otherwise m=n−6: `(1+65*(m mod9),364+33*floor(m/9),64,32)`.|
|11 units|63|`(1+65*r(9),1+49*q(9),64,48)`.|

## 10. Pixel and marker editing

### Session, undo and completion

Publish mode, target and save callback. Query target width/height/key, replace
Original with a copy of target, create the working surface at that extent, fill
its backdrop and draw Original. Every mode reads unit shield offsets at the
active editor's current row into marker and undo-marker state; this is not
restricted to unit mode. Initialize undo from the working surface, set current
and remembered paint tool to line, brush0, ordinary cursor, Running1 and
Painting0. Left/right and previous colors retain their existing shared values.

Backdrop fills the entire surface with its current transparency key, then
draws the unit backdrop for modes1,2,11 or tile backdrop for5,7,8,10. Other modes
have no backdrop overlay. P03 owns keyed drawing, clipping and color decoding.

Rebuild-undo frees the previous Undo, captures the whole working surface with
key254 and saves marker X/Y. Undo is a **swap**, not a history stack: capture
current surface with key254, draw old Undo onto the surface using its key,
replace Undo with the captured image and exchange marker state with the
mouse-down captured old marker. Redraw, including the marker overlay if still
in marker tool. Pixels equal to254 in an undo sprite remain keyed, not an
unconditional complete pixel-array replacement.

OK performs surface color replacement with encoded source/destination colors
`0x109` and `0x107` (P03 decoding), replaces the live target from the original
editor extent, then writes marker X followed by Y using the live current row
for each access. Invoke the live save callback if non-null, clear Running, end
modal processing and repaint the main map. Non-unit rows0…63 can therefore
change valid unit-offset entries. Writes for rows≥64 are excluded overwrites,
not legitimate writes into another asset; preserve earlier valid sprite work
and safely omit/reject/stop as specified by the chosen safe profile. A read
outside those offset arrays has a separate established-backing boundary.

Cancel clears Running and ends the modal loop. It does not call the writer,
restore import-all changes or undo already replaced files. Single-image edits
normally remain in the working surface until OK.

### Mouse input and strokes

Resolve interaction to an identified tool, palette entry, or source-image pixel.
Original window offsets, swatch rectangles and4×4 display scaling are in
[Appendix C](#appendix-c-original-presentation-reference); source-image coordinates
and saved pixels remain exact. Tool actions precede palette/canvas actions. A tool
that consumes the action suppresses a second palette/canvas action. Release ends
Painting.

A palette selection updates the chosen side's previous/current color, preferring
left if both sides are requested, leaves marker mode and restores the remembered
paint tool. Eyedropper samples the source pixel and applies the same color-side
rule. Line/dots start rebuilds undo, sets Painting1, stores source position,
chooses left paint color preferentially else right (retaining old paint color
if neither), then plots once. Marker selection rebuilds undo, clamps X to5…47
and Y to0…14 and updates its overlay.

Moving over the palette does not paint or reset last pixel. Moving outside both
palette and canvas resets last X/Y to−1. On a source-image pixel with Painting
nonzero:

* **Line:** if either last coordinate is negative, replace both with current.
  Let dx=currentX−lastX, dy=currentY−lastY, fixedX=lastX×256,
  fixedY=lastY×256, previous integer pair=(−1,−1). Repeatedly arithmetic-shift
  the fixed coordinates right8. **Break before plotting** if both equal the
  current endpoint. Otherwise plot if the integer pair differs from previous,
  remember it, add dx/dy to the fixed coordinates and repeat. Present the whole
  window. A zero-length move plots nothing and the endpoint is omitted.
* **Dots:** plot only the current pixel and present its dirty rectangle.
* Other tools perform no move-time plotting. Publish current last coordinates
  after the painting branch. Cursor changes include the original input flush;
  P02 owns that flush's queued-input effects.

Plot color0 means the current surface transparency key; other colors are used
unchanged. Brush0 draws one pixel. Brush1 draws horizontal3×1 at `(x−1,y)` then
vertical1×3 at `(x,y−1)`. Brush2 draws horizontal4×2 at `(x−1,y)` then vertical2×4
at `(x,y−1)`. Dirty extents are respectively1×1,3×3,4×4 around those shapes;
window painting clips to the enlarged canvas and surface drawing uses P03's
clip. Unsupported brush performs no plot and returns the retained dirty
rectangle. Do not reinterpret ordinary clipped drawing as raw memory access.

### Tool dispatch

Subtract `(286,20)` for tool hit testing. Admit inclusive local x0…108,y0…190,
with `x mod36≤32` and `y mod38≤32`; tool=`trunc(x/36)+3*trunc(y/38)`.
Unsupported edge indices have no dispatch action. Nominal tools are:

| Index | Ordered effect |
| --- | --- |
|0 transparent|Leave marker overlay, restore paint tool; independently test left then right and set each pressed side to0, retaining its previous color. Refresh swatches/old palette cells and suppress later hit tests.|
|1 line;2 dots|Set current and remembered paint tool.|
|3 undo|Swap surface/marker undo as above.|
|4 eyedropper|Set current tool to picker; keep remembered paint tool.|
|5 clear|Restore paint tool, rebuild undo, draw backdrop and redraw.|
|6 replace|Restore paint tool, rebuild undo, replace raw right color with raw left and redraw. Zero is not converted to the transparency key here.|
|7 swap|Restore paint tool, rebuild undo; replace left→1, right→left, then1→right; redraw. Preexisting palette1 pixels participate in the final replacement.|
|8 import|Restore paint tool, invoke §11 and redraw on return.|
|9,10,11 brushes|Restore paint tool and select brush0,1,2 respectively.|
|12 previous-left swatch|Test left then right independently. Left swaps currentLeft/previousLeft. Right sets right=the now-current previousLeft and previousRight=oldRight. Refresh and suppress later hit tests.|
|13|Restore paint tool only.|
|14|In mode11: rebuild undo, enter marker tool, clamp existing marker to X5…47,Y0…14 and draw. Otherwise previous-right swatch: left sets left=previousRight, previousLeft=oldLeft; right swaps right/previousRight. Test independently in that order, refresh and suppress later hit tests.|

Leaving marker mode triggers its specified repaint before ordinary tool work.
The marker is a window overlay, not written into sprite pixels. Its guide and
cross geometry is in Appendix A; saved offsets are encoded by the UNITS writer.

## 11. Import and sheet output

### Import picker

Expected family prefix is ICONS for modes0,4,5,6; CITIES for1,2,3; TERRAIN for7…10;
UNITS for11. Prompt for BMP/GIF repeatedly; result0 cancels. Uppercase the whole
selected path and locate basename after the last backslash only. Require that
basename starts with the expected family. TERRAIN modes7/10 additionally require
the next character to be an odd decimal digit; modes8/9 require an even digit.
Failure shows the corresponding notice and repeats. Unsupported modes have an
unproduced expected-family boundary.

Search the **whole uppercased path** for `.BMP` first, otherwise `.GIF`; this is
not an extension-at-end test. Load BMP into the requested640×480 surface with
palette range10…201, or GIF with that same palette range, using
[P03 image loading](P03-graphics-and-asset-processing.md#9-bmp-sheet-input-output-and-alternate-loaders). Unknown type or failed
load reports a notice/retries. On success run the picker, redraw the editor and
release the import surface/dialog/palette on normal cleanup.

The picker obtains the cell count from §9, offers cells 0…count−1 and initially
selects cell0. Import-all is disabled only for mode5. Selecting a cell queries
its rectangle, retires the current pick, extracts the replacement from the
imported sheet with key7 and displays it. A failed extraction does not restore
the retired pick. Native client dimensions and aliased count-query scratch
outputs are reference details in [Appendix D](#appendix-d-original-implementation-reference).

Single OK replaces picked sprite color9 with7, reads its width/height/key,
publishes that key, redraws the editor backdrop and picked sprite, then replaces
Original by a capture at the **picked** dimensions. It does not resize the
working surface/editor extent or refresh Undo. Close the picker. Picker Cancel
just closes; the outer editor remains open.

### Import all

Ask RUSURE with the picker parent, restoring the prior parent afterward. Only
exact result0 proceeds. For every target below, **free its live sprite first**,
then grab the indicated rectangle with key7 and replace color9→7. Failures may
therefore leave a released or partially replaced live family. Traversal order is:

| Mode | Targets and cell order |
| --- | --- |
|0|All20 cells; category=n/4, epoch=n mod4.|
|1|All48 cells; variant=n/24, m=n mod24, style=m/4,size=m mod4.|
|2|Fortify, fortress, airbase, occupied airbase at cells0…3.|
|3|Civilization0…7, each normal cell2*c then dark cell2*c+1. Last two offered cells are not bulk-imported.|
|4|Improvements1…38 at cells0…37, then28 wonders at38…65.|
|5|No target loop; direct invocation still reaches the common tail below.|
|6|Eight explosion cells0…7.|
|7|Terrain0…10, each base celli, resource1 i+22, resource2 i+33; cells11…21 are omitted.|
|8|For i0…15, river i, forest i+16, mountain i+32, hill i+48; then four mouths64…67.|
|9|Group0…7, each four coast quarters in §9 order.|
|10|Three land improvements, pollution, shield, hut at cells0…5; then each road i+6 and railroad i+15 for i0…8.|
|11|All63 unit cells0…62, including the sprite beyond the62 editable rule rows.|

Then replace Original by a copy of the current live target, recreate working
surface at its **old editor extent**, draw backdrop/Original, replace Undo by
another target copy, close picker and repaint map. Do not refresh marker offsets
or invoke the sheet save callback. These live sprite changes survive outer
Cancel; disk publication requires a later save operation.

### Saving sheet families

Every writer initializes its palette, captures cwd, changes to scenario,
tries family.BMP then family.GIF, and if both fail changes to home and tries
home family.GIF. The final load result is ignored; an unproduced/retained sheet
has its own P03 boundary. Change to home before painting even after successful
scenario loading. Existing sheet regions outside the write targets are retained.

For each target, query key, height, width in that order; fill its destination
extent with that key, draw the specified backdrop if any, then draw the sprite.
Dimensions come from each live sprite, not a forced atlas cell size. Traverse:

* UNITS: all63 sprites ascending at mode11 positions with unit backdrop, then
  encode each sprite's shield marker immediately. Fill the64 pixels above its
  cell with palette8, set the X-offset pixel there to4; fill the48 pixels to its
  left with8, set the Y-offset pixel there to4. P03 governs clipping.
* ICONS technology: epoch0…3 outer, category0…4 inner, at mode0 positions;
  no backdrop. ICONS building: improvements1…38, wonders0…27, cursor, then
  explosions0…7; no backdrop even for cursor. Each writer retains other ICONS
  regions from the loaded sheet.
* CITIES: style0…5 outer, size0…3 inner, each unwalled then walled with unit
  backdrop. Then civilizations0…7 normal/dark flags without backdrop; finally
  fortify, fortress, airbase, occupied airbase with unit backdrop.
* TERRAIN1: terrain0…10, each base/resource1/resource2; land improvements0…2;
  pollution, shield, hut; roads/railroads paired for i0…8. Use tile backdrop.
  The original land-improvement height query uses stale index11; Appendix A
  separates that read defect from the repaired own-sprite-height behavior.
* TERRAIN2: for i0…15 each river/forest/mountain/hill, then mouths0…3, all with
  tile backdrop; finally eight groups of four coast quarters, without backdrop.

Return to scenario. If family.BMP is absent or removal succeeds, attempt BMP
save with palette10…201 through
[P03 sheet output](P03-graphics-and-asset-processing.md#sheet-output), ignoring its
result. Restore saved cwd, release sheet
and palette. Removal/open/write failure does not undo live sprite replacement,
restore a deleted BMP or roll back earlier cells. Writers do not save GIF.

## 12. Unit sound editor

The sound editor reads the **staged** selected unit's attack/domain/range/flags.
Initialize six slots (attack, orders, explosion, bomb, crash, sputter) to−1 and
local orders to−1. If attack is zero, skip all assignment. Otherwise use the
first applicable branch in this ordered table. Effect numbers identify P10
resources, not mandatory storage offsets.

| Test | Slot assignment |
| --- | --- |
| Test unit1…8 | Attack125…132. |
| Extra land, ship, air | Attack101,102,103 respectively. |
| Missile capability | Attack41 if staged attack<99, otherwise50. |
| Air, range0 | Attack33; crash23 for unit type<30, otherwise79. |
| Air, nonzero range, type<30 | Attack0,bomb24,crash23,sputter26. |
| Other Air | Attack82,bomb80,crash79,sputter78. |
| Sea with submarine capability | Attack77. |
| Other Sea | Orders46 for Battleship/Cruiser/AEGIS Cruiser/Destroyer, otherwise6. |
| Elephant | Attack25. |
| Horsemen/Chariot/Knights/Crusaders | Attack74. |
| Dragoons/Cavalry | Attack12. |
| Musketeers/Riflemen/Alpine Troops/Partisans | Attack34. |
| Fanatics/Paratroopers/Marines/Mechanized Infantry | Attack38. |
| Other type outside22…26 | Attack73. |
| Catapult23 | Attack10. |
| Remaining types22,24…26 | Orders40; additionally Attack28 when type>23. |

Publish local orders. If orders≥0, set explosion35. Enable exactly the buttons
whose slot effects are nonnegative. Resource choice is identity-based even if
rule names have changed; a unit need not be alive or present on the map.

For an enabled slot, publish sound-window parent and repeatedly choose a WAV.
Picker result0 returns, leaving that parent until the enclosing dialog cleanup.
For a selected file, repeatedly show PLAYSOUND after clearing its extra-result
field; a nonzero extra result previews that file and repeats the dialog. Once
extra is zero, nonzero modal choice goes back to the file picker; exact0 accepts.

Form scenario directory plus `SOUND`. Require its attributes query to succeed;
this tests existence, not specifically that the object is a directory, and does
not create it. Append the selected effect's fixed basename plus `.WAV`. If
absent or removal succeeds, copy the selected file there, ignoring copy result.
There is no same-source/destination guard or backup. Failed copy after removal
can leave no destination. This publishes immediately; sound-dialog and parent
unit-editor Cancel do not undo it. Sound OK and Cancel both only end that modal
session. Normal outer cleanup clears parent.

Double-click scans the six slot hit regions in order and previews the first
nonnegative matching slot. Capture the **entire** game-options word, enable
sound, play that effect, then restore the captured word on normal return. Thus
callback option changes can be overwritten by restoration; there is no invented
fault unwinding. P10 owns waveform decoding/playback and effect-name lookup;
P04 owns label-region geometry and modal delivery.

## 13. Conformance and integration


> Compare the gameplay/interchange observations in the
> [production scope](#production-contract-scope). Original memory/helper traces,
> arbitrary native callbacks and unsafe backing-byte cases in historical lists
> are optional reference, not additional production acceptance criteria.

These procedures contain no direct random draws. Named resource, modal,
rendering, file and gameplay children retain their own ordered effects; do not
attribute a whole native editor session a zero-draw guarantee merely from its
scalar loops. Record shared state before and after callbacks, especially the
current row, active editor, file cursor, name pool, target sprite and directories.

A cleanroom implementation should distinguish at least the following cases:

| Case | Required distinction |
| --- | --- |
| Load rules with negative/saturated numeric residues, then open/save | Signed unit/terrain reads differ from unsigned improvement reads. Movement/HP divide then multiply; unit startup bounds are separately captured. |
| Change prerequisite to No | Enabled is not recomputed on editor apply; relinking/count follows the retained Enabled flags. Disabled cycles are not silently repaired. |
| Edit first versus second wonder expiry | Row39 retains scratch and does not publish expiry; row40 does. ENDWONDER writes both live entries. |
| Clamp a hidden numeric field | It still changes scratch and keeps OK/row switch in the correction branch. |
| Select excluded resource rows through a supplied reply | Normal selector compression and the raw-index error restoration are different operations. |
| Rename aliased event strings or successive unit names | Exact case, four-field order, live successor reload and rename cascades matter. Short-allocation padding overwrites do not. |
| Edit a tribe already present in a dead civilization slot | First matching civilization is still selected. Plural/adjective staging does not imply live name publication. |
| Save rules from home versus scenario | Home writes BAK; scenario may rename the original and leave partial new text. Child returns and child row-write errors can be swallowed. |
| Input section missing, out of order or lacking a short blank separator | Copy/child effects already performed remain; no implicit insertion or rollback. |
| LEADERS old gender/titles and COSMIC missing comments/EOF | Preserve the specified input tail or retained-read boundary, not a freshly formatted complete replacement table. |
| Import one versus import all, then Cancel | One normally affects working pixels; all replaces a live family and survives Cancel, without automatic disk save. |
| Marker OK from a non-unit editor | Valid unit-offset cells can change; writes outside their array are excluded bugs. |
| Paint a zero-length or short diagonal stroke | Line omits the endpoint; dots includes it. Brush overlap and clipped drawing remain P03 operations. |
| Swap colors when palette1 is already present | Three ordered replacements affect preexisting1; this is not an abstract simultaneous two-color permutation. |
| Sound source equals destination or replacement copy fails | Removal precedes unchecked copy. Cancel does not restore the old WAV. |

X02 should join actual F03 parsing/name lookup, S02 pool lifetime and S03 event
writing, P01/P02/P04 native input/control producers, P03 sprite/palette/image
services and P10 audio resources. Preserve independent ordered state, file,
callback and failure traces. The bounded fixture below does not establish all
these runtime joins or full native window lifetime.

## Appendix A. Original representation and excluded defects

The algorithmic bounds, file formats and arithmetic in the main text are
observable contracts. The following addresses, capacities and layouts identify
original validation cases; they do not require a cleanroom implementation to
adopt the same object layout.

### Scratch, controls and names

The original shared name scratch uses100 rows of40 bytes, with explicit final
byte termination on technology/improvement/unit/terrain loads. Numeric scratch
has22 signed32 slots per row (stride88). Rule controls start their descriptor
IDs at201; numeric conversion uses a retained8-byte local buffer and ordinary
numeric text admission is3 characters. Control descriptor tables encode kinds9
(edit) and12 (combo). The unit descriptor table begins with row/prerequisite/
obsolete/role/domain, then eight numeric fields; the latter's IDs206…213 map to
cost, holds, movement, range, attack, defense, HP, firepower. Unit role/domain
choice order is F03's0…7 roles and0…2 domains, not a name-based reorder.

Original executable min/max tables are at technology`00625198/006251a0`,
improvement`0062e3e8/0062e3f0`, terrain`00631c70/00631ca0`, unit numeric bounds
`00635ec8/00635ee8`, cosmic`00634590/006345e8`. The unit table's initial maxima
are all127, but setup writes movement at`00635ef0` and HP at`00635f00`; static
data alone would give an incorrect specification. Terrain's unused numeric
bound slots contain999, while the nominal numeric descriptors use the nine
0…127 fields. Cosmic's last three minima are4,25,0, matching §7's parameter
order. The signed/unsigned rules follow F01, regardless of host integer widths.

Ordinary name apply uses original prefix-copy widths: advances20,
improvements25, unit/terrain15, government/generic titles15, commodities10.
F03 nominal padded allocations support these widths. Long input names can have
longer allocations: a short rename zero-pads only the copied prefix, leaving
an old nonzero suffix inside that same allocation. The original pool lookup
scans strings and zero runs by ordinal, so the retained suffix can appear as
another ordinal and shift later lookups. That is a defined read consequence
where all accessed bytes are established; model the resulting name-service
sequence, or declare a stricter name-admission/repair profile. B32 in the primary
reconstruction and BF-047 independently clear the old suffix and are repairs.
The fixture's independent name arrays test retained bytes, not complete pool
ordinal traversal. A subsequent fixed-width write spilling beyond its intended
allocation is still excluded; do not preserve it by writing into the next name.

Tribe loading copies15 bytes into each generic male/female title but mistakenly
terminates the government **name** at character14 after each title copy. An
input title of length≥15 need not be terminated in its own scratch field.
Established adjacent read bytes or a bounded stop are required, rather than
asserting a terminator was written. Government names, staged tribe names and
commodities have their specified own terminators. All overlong scratch/path/
line/output writes are excluded bugs. The LEADERS old-tail copy has128 bytes;
COSMIC incoming line has128; section search chunks256, header80, filename16
and saved directory260. These sizes locate failure boundaries, not general
cleanroom storage requirements.

Event rename uses15-byte padded copies, even when an event name was allocated
only to its actual length plus terminator. That possible overwrite is excluded.
Safe bounded string replacement must still preserve documented logical aliases,
comparison order, count and prior valid effects. Do not transplant reconstruction
models of adjacent corruption into gameplay assignments.

### Asset offsets, palette and geometry

Original unit shield X/Y arrays each have64 signed32 cells; normal sheet output
visits63 sprites. Offset accesses occur on entry for **all editor modes** and
on OK before the save callback. Higher Y indices can read the following X
array; higher X indices can read citizen-sprite rectangles, padding or pointers.
The independent reconstruction models these mapped addresses, including
writes. Such mapping is evidence of the bug, not authorization to assign those
unrelated fields in the cleanroom game. Defined backing reads remain separate;
array-exceeding writes are always excluded under X01, even for ordinary advance
rows64…99. A safe profile may skip the invalid writes and then continue with
the save callback, or reject/stop at that boundary, recording its choice.

Original initial left/right/previous-left/previous-right colors are10,41,0,0;
opening another editor retains current values. Cursor resources are ordinary513,
paint516 and selection517. These numbers can be represented by logical roles.
Undo key254, sheet key7, imported-color conversion9→7 and UNITS marker colors8/4
are file/sprite semantics. The surface replacement arguments on OK include the
surface API's color encoding, so do not infer a raw9→7 operation without P03.

The marker overlay saves window clip, restricts it to the enlarged canvas and
restores clip before presenting. Relative to origin, palette106 guide rectangles
are `(32,16,4,60)`, `(32,76,180,4)`, `(208,16,4,60)`. For marker `(x,y)`, outer
palette10 cross rectangles are `(4*x+8,4*y+16,20,4)` and
`(4*x+16,4*y+8,4,20)`; inner palette41 rectangles are
`(4*x+9,4*y+17,18,2)` and `(4*x+17,4*y+9,2,18)`. They affect presentation only.

TERRAIN1 original instruction`00570b2b` asks height from land-improvement
sprite index11 after the base-terrain loop has exited. The nominal family has
three entries. Primary/independent BF-013 use the currently selected improvement
instead. That is an explicit read repair: classic validation needs the original
established backing result or stops at the read. Subsequent graphics fills
follow P03; if an implementation produces an actual memory overwrite, exclude
it rather than making corruption a required raster operation.

Added selection checks BF-114/115/116/117/118 (building, city, miscellaneous
terrain, overlays and coast) and related primary bounds reject custom resource
choices. Nominal offered choices are fully specified in §9. For out-of-domain
replies, a stricter profile may reject them; an original-read profile must
resolve the selected address/identity from evidence instead of pretending all
such checks were present originally. Read success never licenses an unsafe
later write. Technology-cycle validation, long usage-line formatting bounds and
COSMIC EOF fallback likewise remain distinct repairs, not classic nominal steps.

### Text output and service adaptation

Writer literals were read from original executable data, including composite
fields initialized as zero in parts of the reconstructed data source. Their
actual commas/spaces/newlines are specified in §8. `D2` corresponds to `%2d`,
COSMIC to `%-8d%s`; original null-string formatting emits `(null)`. Output
buffers and native text handles may be replaced by safe strings/streams so long
as observable text, failure order, read cursor and callback effects are retained.

The original sound callbacks receive control IDs201…206 and address the six
slots through an offset alias; this is an intended slot mapping, not six
independent effects at indices201…206. The sound resource table uses9-byte
basenames. A cleanroom implementation can use slot0…5 and P10's effect-name
service. Full-options restoration after preview remains required on normal
return, even if a callback changed another option.

## Appendix B. Evidence and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is OpenCiv2 at
`a71e8aefffe04e6f719e0e213b027b128a747711`, inspected at OpenCiv2 comparison revision
`fc2155ccdb2123c1137babab6fa39e38152278c9`:

| Family | Source and original entries |
| --- | --- |
| Technology, section writer, cosmic | [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp): load/save `004151e0/00415307`, relink `00415952`, file replacement `004ccf2d`, cosmic capture/write `005866d3/00586d0a`. |
| Improvement and city graphics | [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp): improvement load `004d8bc0`, city graphics wrapper `00429e77`. |
| Unit rules and sound editing | [unit.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/unit.cpp): load `005aef20`, setup `005b1037`, sound selection `0058afb6`. |
| Terrain rules and asset routing | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp): load `00519200` and associated editor/writer bodies. |
| Tribe, names and CITY replacement | [editor.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/editor.cpp): LEADERS `004a3a21`, CITY `004cd8a6`; [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): event rename `004cefe9`. |
| Pixel editor, atlas, import/export | [graphics.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/graphics.cpp): entry `00573e59`, OK `005722ab`, move `00573b1d`, cells `005746a1`, bulk import `00574f50`, file picker `00576267`. |
| Types, helper and literals | [civ2types.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h), [auto_common.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/auto_common.h), [gamedata.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/gamedata.cpp) and state headers. |

Independent read-only evidence is `civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`: advances/improvements/units/terrain/
tribe/effects/city-graphics/rules editor files, `rules_write_file.cpp`,
`string_pool.cpp`, `icon_editor.cpp` and `unit_sound_editor.cpp`. Original
executable SHA-256 is
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Its accepted reconstruction campaign is not resumed. Original data reads
confirm the ten min/max tables and writer literals; repairs are distinguished
above. No fresh native editor session or whole-program equivalence is claimed.

The historical Linux fixture compiled **25 complete source bodies**:
technology load/apply/relink, improvement/unit/terrain load/apply, both terrain
selection mappings, event rename, advance/terrain abbreviation appenders, five
rule-section writers, section set/add/file replacement, atlas geometry, sound
slot selection and mouse-move strokes. **44 assertions pass** in optimized and
AddressSanitizer/UndefinedBehaviorSanitizer builds with defined signed wrap,
including all396 nominal atlas cells. Six original record declarations and58
literal declarations are retained/verified; composite literal storage is
represented by equivalent arrays containing original bytes.

The fixture joins the actual rules-file wrapper to the actual unit writer and
the actual abbreviation child. It checks exact technology/improvement/wonder/
unit/terrain output, ignored child errors, backup/home/partial output, signed
loads and byte products, first-wonder expiry, disabled prerequisite traversal,
resource scratch retention, event alias/count order, sound branch priority and
fixed-point endpoint omission. Pointer carriers and one window-handle temporary
are widened for the host; logical scalar widths are retained.

Explicit adapters provide the name service, primitive text appends, CRT/file
operations, controls and graphics presentation/plot observations. File service
uses a logical directory map and temporary host streams with LF chunks; it does
not prove native Windows CRLF/error behavior. Name arrays do not reproduce F03
pool ordinal scans. Event strings have sufficient allocated padding in the
executed alias case; excluded overwrites are not executed. Mouse-move tests
observe ordered plot calls, not P03 raster pixels. Sound tests use supplied
staged fields/button adapters, not native playback or replacement files.

Initial fixture probes corrected a duplicate literal and the byte-view adapter
for staged flags. Expected cases were corrected to Catapult23 versus Armor22
and to the line algorithm's endpoint omission. These corrections do not alter
the source bodies. Native controls, LEADERS/COSMIC reader joins, CITY rewrite,
full pixel undo/tool/import/export operations, actual pool aliases and audio
file replacement remain inspected contracts with integration work for X02.
Source-pin checks cover all177 inventoried bodies and the record/literal
evidence. Reference cleanliness, executable hash,
Markdown links/anchors, specification IDs and whitespace complete the bounded
validation. Sanitizer success covers only the finite executed paths.

## Appendix C. Original presentation reference

The following original input geometry and drawing procedures are optional under
X01's presentation policy. The main text retains semantic selections, calculations,
file/wire effects and required ordering. This appendix is for original-adapter
validation, not a required UI design.

### Original pixel-editor mouse geometry

Coordinates below subtract the editor's current window origin. Mouse-down
captures the window, captures entry marker coordinates, dispatches a hit tool,
then tests palette before canvas. Some tool commands reset x=y=0, suppressing
those later hit tests. Mouse-up clears Painting and releases capture.

Palette bounds are inclusive x9…391,y223…293. Its selected color is
`10 + 32*trunc((y−223)/12) + trunc((x−9)/12)`. Test left button first; otherwise
test right. Store that side's previous color, update current color, redraw
swatches and old/new palette cells, leave marker tool and restore remembered
paint tool. Neither button leaves color locals unproduced; require established
input/backing for their later presentation uses, not a guessed selection.

Canvas bounds are x≥16,y≥16 and x<16+4*width,y<16+4*height. Pixel coordinates
are `trunc((x−16)/4),trunc((y−16)/4)`; displayed pixels are4×4. Eyedropper samples
the surface and updates left preferentially, else right, saves that side's old
color, refreshes swatches/palette and restores remembered paint tool/cursor.
Line/dots mouse-down rebuilds undo, sets Painting1, stores pixel position,
chooses left paint color preferentially else right (retaining old paint color
if neither), plots once and presents the dirty rectangle. Marker click rebuilds
undo, clamps X to5…47 and Y to0…14, and redraws its overlay.

During mouse-move, palette takes precedence and changes to selection cursor
without painting or resetting the last pixel. Outside both regions use ordinary
cursor and reset last X/Y to−1. Inside canvas choose selection cursor for
picker or paint cursor for line/dots; when Painting is nonzero:

## Appendix D. Original implementation reference

This appendix preserves original storage, allocation, native API and adapter
procedures for reference validation. The main text defines required behavior.
A different internal representation or platform is permitted; semantic identities,
ordered game effects, accepted input interpretation, file/wire values and required
failure/completion results remain governed by the main algorithms and X01.
Mixed excerpts retain context: their required effects are not made optional by
appearing here. Original memory/handle traces are not a second normative algorithm.
Optional providers and harmless UI failures retain X01's existing scope limits;
this reorganization introduces no new gameplay or interoperability relaxation.

### Original import-picker window and scratch representation

Picker publishes its dialog, constructs a240×240 client, queries cell count via
§9 with all four outputs aliased to one scratch word, installs scrollbar range
0…count−1, selects cell0 and starts modal processing. Import-all is disabled
only for mode5. Selecting a cell queries its rectangle, frees the current pick
and grabs from the imported sheet with key7, then redraws.

## Appendix E. Original numeric annotations

These original annotations retain storage widths and reference-evaluation context.
They do not prescribe internal types or redundant casts. The main contract and
F01 determine logical ranges and behaviorally significant conversions; external
file/wire encodings remain normative in their owning mappings. Where an excerpt
includes an off-nominal conversion, it applies to that explicitly admitted profile,
not as a new requirement to accept unsupported inputs.

### Reference annotation 1

terminate. Capture prerequisites as signed8; epoch/category reinterpret as
signed8; AI value and civilized modifier are signed8. The ordered fields are:

### Reference annotation 2

Apply all100 names and six fields in that order per row, reducing each field to
its byte domain.
