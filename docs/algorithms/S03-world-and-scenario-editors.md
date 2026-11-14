# S03 — World and scenario editors

**Required profile:** [X01 fresh-game isolation and bug repairs](X01-classic-compatibility-contract.md#fresh-game-isolation-and-supported-bug-repairs)
and [behaviorally significant ordering](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
override conflicting procedures and examples below. Retention applies within a
game or to explicit imported inputs, not across new games. Confirmed original-bug
stack/backing/stop cases are optional diagnostics; use documented safe repairs.
Only real effects and callbacks impose ordering; test adapters cannot invent them.
Unspecified safe continuations remain explicit gaps, not required arbitrary bytes.

Target: classic Civilization II Multiplayer Gold Edition **5.4.0f, patch 3**. This document owns
cheat/world commands, scenario-mode/folder preparation, parameter editing,
event editing and event-text writing. It specifies accepted commands, their
ordered mutations and cancellation/failure effects. P01/P02/P04 own native
controls, input dispatch and resource/dialog realization; P05/P06/P10 own map,
city and media presentation. [S04](S04-rules-and-asset-editors.md) owns
rule-definition and asset editors.

[F02](F02-game-state-and-invariants.md) supplies shared state and identity;
[F01](F01-determinism-and-arithmetic.md) supplies arithmetic. Use normal gameplay
children only where named. Direct editor stores do not implicitly invoke
research grants, capture, elimination, economic recalculation or region repair.
[S01](S01-scenario-loading-and-parameters.md) owns parameter meanings;
[S02](S02-event-language-and-execution.md) owns event records, copying/compaction,
parsing and execution. [F06](F06-save-load-and-file-formats.md) owns scenario
save/load bytes. Their algorithms are not repeated here.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Compatibility policy

X01's [presentation policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy)
permits redesigned editors while retaining command availability, object identity,
edit values, committed effects, cancellation semantics and classic exports.
New controls do not authorize different scenario flags, event rules, map edits
or implicit gameplay recalculation. Native widget IDs, geometry and harmless
control failures are reference behavior. Apply the
[input policy](X01-classic-compatibility-contract.md#input-admission-and-rejection-policy)
to reject malformed/unsupported content safely; retain nominal custom scenarios
and the external writer formats. Published file/game changes survive cancelling
an enclosing UI when the original semantic operation has already committed.

## Implementation boundary

The main sections use logical values, identities and operations. Original memory,
allocation and platform mechanics are recorded in [Appendix C](#appendix-c-original-implementation-reference). Preserve required
state changes and externally visible results without reproducing those mechanisms.
Exact numeric domains and file/wire encodings remain required wherever specified.

## 1. Inputs, domains and cancellation

Inputs include the live world/rules, local human and current cursor, cheat and
scenario status, password membership, multiplayer timer state, retained editor
defaults, event collection/storage, text arguments/cursor, resource directories,
dialog replies, file-service results and callback effects. Keep CheatMode,
EverCheated, Scenario, saved-as-scenario and events-loaded distinct.

Nominal replies identify options offered by that invocation, names fit their
destination fields, selected records remain valid across callbacks, and files
and resources use their supported domains. Event records have supported trigger
tags, valid references and sufficient storage. Each editor's accepted scalar
range is specified below; it often differs from file-parser admission.

Working expressions follow F01 reference arithmetic unless specified otherwise. Direct
city size, unit damage/cargo/home and civilization attitude/reputation stores
retain their byte domains; turn/year, contact turns, stock/progress and scenario
parameters retain signed 16. Membership uses the low-five-bit shift followed by
its byte reduction. Only the significant conversion effects are required; a native field size is
not prescribed. Original masks and buffer extents belong in Appendix A.

A modal result is not a transaction. Each accepted substep commits immediately;
later cancellation normally keeps it. Most command pickers accept any
nonnegative result without a second membership/range check. Unknown or stale
reads require established backing, supplied input or a bounded stop. Do not
invent a missing choice, string, cursor or event. A stricter input profile may
be declared. Out-of-bounds writes and collateral corruption are excluded bugs
under [X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
including mapped adjacent fields and writes through invalid/retired records.
Reject, safely omit an established unsafe write, or stop before it while
retaining prior valid effects.

State is live at each access unless captured. UI, file, allocation and gameplay
children can mutate it. Normal cleanup and explicit cancellation are distinct
from a child fault or diagnostic stop; do not add rollback/cleanup not reached
by the original control flow.

## 2. Command admission and common selection

P02's map-key dispatcher calls the cheat-command group only with CheatMode set
and multiplayer mode 0, after its ordinary input/busy/city-window gates. A direct
command call does not repeat that gate. The mode toggle itself is available in
single-player or when the multiplayer cheat-menu allowance is set. Scenario-mode
toggle is separately dispatched in single-player. Rule/asset editor commands
delegate to S04; event editor commands use §8 onward.

The ordinary civilization picker offers alive slots in ascending order, starting
at 1 when barbarians are excluded and 0 otherwise. Label each by its live adjective;
set the default to the then-live local human. Return the full modal result.
Do not add a fallback when the default is absent or convert the result to a row
position. The commands below use the nonbarbarian form unless stated otherwise.

### Cheat-mode toggle

Count nonzero password-present entries for slots 0…7. If any, display the count
and the singular/plural password-block notice, then return without toggling.
Otherwise:

1. If both CheatMode and EverCheated are absent, ask REALLYCHEAT. Exactly zero
   cancels. Publish EverCheated; if Scenario was set at the publication step,
   show the scenario warning. A negative nonzero response is not cancellation
   under this parent's test.
2. Toggle CheatMode. In multiplayer, if it is now enabled, stop the timer with
   argument 1, save the live time limit and set it to 0. If now disabled, stop
   with 1, restore the saved limit, then resume with 1. Single-player does neither.
3. Update menu states. Do not clear EverCheated when disabling the mode.

The ASCII reveal shortcut in single-player cheat mode toggles only RevealMap
and redraws all maps for the local human. Shift-assisted throne/council commands
delegate to [P09](P09-council-and-throne-room.md); the throne cheat calls the normal upgrade display with the
local human. U04's disband command retains its own no-unit/city cheat branch;
C06's production dialog retains its own cheat completion choice. These existing
owners govern their effects, rather than a second S03 implementation.

## 3. Civilization, turn and knowledge commands

| Command | Ordered behavior |
| --- | --- |
| Advance technology | Pick a civilization; on nonnegative reply call [V03](V03-research-and-technology.md) research completion `(civ,0)`. This is the normal completion operation, not a raw knowledge-bit write. |
| Force government | Pick; on nonnegative reply invoke [V02](V02-governments-and-tax-rates.md) government selection `(civ,1)`. |
| Change money | Pick; publish adjective and old treasury as dialog arguments; ask with old treasury default. Only result 0 accepts. Clamp the full entered integer first above 30000, then below 0; store treasury, refresh city window, redraw status with 1. |
| Set game year | Display current **turn**, ask with turn default. Only result 0 accepts. Store the low signed 16 turn; call [F04 calendar conversion](F04-turn-sequencing.md) with that stored value; store signed 16 year; redraw status then refresh city window. The command does not search backward from a requested calendar year. |
| Destroy civilization | Offer alive slots 1…7. On nonnegative reply clear its Human membership; in network mode>2 send unlock-nation to recipient0 with `(0,civ)`. Capture city high-water count and delete owned live cities descending via [C01](C01-city-lifecycle-and-ownership.md) `(city,0)`. Then call [V01](V01-civilization-lifecycle-and-civil-war.md) destroyed check `(civ,liveLocalHuman)`. No independent all-unit purge is added here. |
| Destroy units at cursor | Query the live cursor's first unit, then invoke [U01](U01-unit-lifecycle-and-stacks.md) whole-stack deletion `(result,1)`, including the child's Absent handling. |

### Human and reveal selection

Set Human offers alive slots 1…7 plus No Human, without a set-default call.
Cancellation is a negative reply. No Human clears Human membership and
player-turn-active only. A civilization reply stores membership as its single
shifted bit, publishes its byte identity to saved player and current-turn
civilization, publishes the full identity to local human, redraws all maps for
it with 1, then invokes the overall map redraw with 1. Do not reset other player
state or initialize a new turn.

Reveal Map offers all slots 0…7 regardless of Alive, then Reveal All and Normal
View. The default is Reveal All exactly when RevealMap is clear and ViewCiv is
negative; otherwise Normal View. Reveal All sets RevealMap and clears ViewCiv;
Normal View clears both. A civilization reply clears RevealMap and narrows the
reply into signed 8 ViewCiv. If that stored value is nonnegative, publish it as
local human and replace Human membership with its single bit. Finally update
the map title and redraw all maps. These commands change viewing/control state;
they do not fill explored/remembered tile layers. Negative narrowed ViewCiv
does not change local human/membership.

### Technology editor

Disable active-unit blinking, then pick a civilization. Repeatedly build the
EDITTECH menu from enabled advance slots 0…99 in ascending order. Each label is
the name plus a space, then `*` if known, or `-` if unknown and either prerequisite
query is false; otherwise no marker. Prerequisite queries short-circuit in order.
Remember the last accepted advance as the next default. A negative modal reply
ends the editor; inspect the live extra-result only after nonnegative reply.

With extra-result 0, query the selected advance. If unknown, grant it using V03
`(civ,advance,0,0,0)`. If known, clear its local knowledge bit and that
civilization's global known-by membership directly. Then set the civilization's
advance count to 0 and rescan all 100 using V03's knowledge query, incrementing
the byte count for every true result. Refresh the city window. Removing a bit
does not reverse other grant effects, update research choice or re-enable expired
wonders automatically.

With a nonzero extra-result, toggle all knowledge based on the live FutureTech
count. If zero, grant all 100 advances in ascending order through V03, set
FutureTech count to 1, and show GAVETECH. Otherwise clear the entire stored
knowledge vector, clear the civilization from each global known-by entry0…99,
set FutureTech count 0 and advance count 1, and show TOOKTECH. Refresh the city
window in either branch. There is no equivalent grant loop on the removal branch.

## 4. Terrain and unit creation

### Terrain command and repeat mode

The ordinary terrain-edit command first sets the retained feature-choice value
to−1. The repeat command calls the same routine without resetting it. Construct
the dialog, require the live cursor in bounds and no city owner at that tile,
then capture its tile reference and original base terrain. A unit does not block
editing. Cursor coordinates are reread by later queries/redraws; the captured
tile reference is not reselected after a callback.

For non-ocean terrain, offer the current terrain and repeatedly follow its
IrrigateTo chain until a negative target, appending each name/value. There is no
cycle detection. If the last type differs from the original, a negative retained
feature choice causes a modal terrain selection and negative reply returns.
Otherwise use the chain's final type directly. Immediately replace terrain
with that type while retaining only the old River flag. This precedes feature
confirmation, so later cancellation can leave the terrain change.

Capture the tile's features and prefill checkboxes in this order: Irrigation,
Mine, Road, Railroad, Fortress, Airbase, Pollution. A negative retained choice
opens the feature dialog; negative reply returns. If its Help/extra indicator is
zero, retain the checkbox state for future repeat commands. In repeat mode,
replace the shared checkbox state with the saved value without a dialog; the
existing Help/extra indicator remains an input.

When that indicator is zero, perform the feature branch:

1. If the captured features had Pollution, decrement the global polluted-tile
   count with its signed 16 wrap. Start new features retaining Unit only.
2. Apply checked items in checkbox order. Railroad adds both Road and Railroad;
   Fortress and Airbase use W01's feature combinations. Checked Pollution sets
   it and increments the polluted-tile count.
3. Publish features. For civilizations1…7, if the **live cursor** tile is visible
   to that civilization, update remembered features using W03.
4. Redraw the cursor area with radius1, live local human and mode 1, then return.

For an initial ocean tile, or a nonzero Help/extra indicator after the land
feature dialog, close/reinitialize the dialog and offer all 11 terrain types,
defaulting to the **original** terrain. Negative reply does nothing further.
If accepted type equals the original, do not restore an earlier chain change.
Otherwise:

- To Ocean: replace the whole terrain encoding with Ocean (clear River and
  suppression flags). Find the minimum landmass among valid ocean neighbors
  in W01 D[0…7] order, starting at 63; store it.
- To another type: replace terrain retaining River only. If the original was
  Ocean, similarly select minimum landmass among valid non-ocean neighbors,
  starting at 63. Otherwise retain landmass.
- Redraw the cursor tile in all views.

This second branch does not clear features, adjust pollution counts, reveal
terrain, update remembered features, relocate units or recompute all regions.
Do not replace either branch with S02 ChangeTerrain or W04 work completion.

### Create unit command

Retain owner, last type, Veteran, ShowObsolete, IgnorePrerequisites and the last
local-human identity across invocations. If local human changed, publish the
new identity and invalidate retained owner; replace a negative owner with local
human. Rebuild the unit menu as needed, scanning types 0…61:

1. Admit when obsolete-by is negative, or the owner lacks that advance, or
   ShowObsolete is set. This is reference short-circuit order; X01 permits
   equivalent pure evaluation on stable, backed inputs, retaining any admitted
   read/failure dependency.
2. Require IgnorePrerequisites or the owner's prerequisite knowledge.
3. Add the type, prefixing the veteran label if selected. If last type is
   negative, set it to the first admitted type. Use a nonnegative retained last
   type as default even if it is no longer offered.

Store each modal reply into last type before testing cancellation. A negative
reply ends. Extra-result 2 toggles Veteran,3 toggles ShowObsolete,4 toggles
IgnorePrerequisites; each invalidates last type and rebuilds. Extra-result 1
opens an owner picker offering barbarian0 unconditionally and alive slots 1…7.
Its enumeration itself advances the retained owner through 0…8 before the modal
reply replaces it. Negative owner reply returns; otherwise continue with the
previously selected unit type. Any other extra-result creates immediately.

Call U01 creation with that type, retained owner and live cursor. On successful
unit index, apply Veteran if selected. Redraw the live cursor tile and disable
blinking even when creation returns failure. No additional bounds, ocean/land,
occupancy, home-city or production eligibility check belongs to this command.

## 5. Unit and city editors

Require cursor bounds and a present object. For units, if the stack count query
`(unit,2)` exceeds1, open stack selection with mode 1; negative selection cancels.
Capture the selected unit's owner once. Each menu iteration formats its current
type, veteran state and home name, using the captured owner's adjective. It
does not revalidate identity/ownership after callbacks. Choice0 exits; unsupported
choices exit. Successful substeps return to the menu.

| Unit choice | Mutation and refresh |
| --- | --- |
| 1 Veteran | Toggle Veteran; redraw live unit tile, redraw status1. |
| 2 Movement | Set moves-used0; same redraws. No order/fuel reset. |
| 3 Damage | Publish maximum HP as a prompt argument; ask with existing damage default. Result0 writes the low byte directly, without HP clamp. Redraw tile/status even on cancellation. |
| 4 Home | Offer live cities owned by the **captured** owner in ascending order. If none, skip the dialog. Nonnegative reply with extra-result 0 stores its byte city reference; nonzero extra stores Absent. Redraw tile/status only on accepted reply. |
| 5 Fortification | Only for a live type/domain query equal to Land. Toggle Fortified versus None; redraw tile/status. No normal fortify delay or movement cost. |
| 6 Cargo | Only for a live type/role query equal to Trade. Offer commodities0…15 plus Food, defaulting by signed cargo value; any negative cargo defaults Food. Nonnegative reply<16 stores that byte; any higher reply stores Food sentinel. Redraw tile/status. |

For cities, capture owner once and repeatedly display current name, captured
owner adjective and Objective status. Choice0/unsupported exits. Numeric prompt
result 0 accepts. Editing does not apply normal growth or production gates.

| City choice | Mutation and refresh |
| --- | --- |
| 1 Size | Store low byte of entered size. Redraw tile/status even on cancellation; no worker/growth recalculation here. |
| 2 Remove wonders | Scan all 28 wonder locations, replacing this city's references with Absent. Redraw tile/status. Do not clear global discovery/expiry history or invoke destruction effects. |
| 3 Clear unrest | Clear Disorder and Celebrating; redraw tile only. |
| 4 Copy improvements | Offer every other live city, any owner. On accepted reply capture destination's Palace status, copy the entire ordinary-building store from selected city, restore the captured Palace through C08's setter, then refresh city window. No wonder-location transfer occurs. |
| 5 Shields | Store low signed 16 shield stock. Redraw tile/status and refresh city window even on cancellation. |
| 6 Objective | Toggle Objective; next iteration updates its displayed argument. No immediate extra redraw call. |

## 6. Civilization editor

Pick a civilization, then repeatedly display its live leader name and Female
status. Choice0/unsupported exits. The selected civilization remains captured;
other records/names and dialog state remain live.

| Choice | Ordered work |
| --- | --- |
| 1 Treaties | Pick another civilization; prefill Contact, Ceasefire, Peace, Alliance, War, Vendetta, Embassy from the selected→other relation. On nonnegative flags-dialog reply, perform the treaty procedure below. |
| 2 Last contact | Pick another; show its leader and current turn, ask with selected→other contact turn. Result0 stores signed 16 directly. |
| 3 Attitude | Pick another; show its leader, ask with current attitude. Result0 stores unsigned 8 directly, without V04's normal clamp. |
| 4 Reputation | Ask with current reputation; result 0 stores unsigned 8 directly. |
| 5 Audience requests | Clear the selected civilization's request count. |
| 6 Reset contact clocks | Other index 0…7 outside civilization index 0…7: set every civilization's contact time with that other to the live turn. No Alive filter. |
| 7 Research choice | Call V03's research-choice dialog `(selected,1)`. |
| 8 Research progress | Query V03 cost for display, then ask with old progress. Result0 stores signed 16 and redraws status1. |
| 9 Clear research | Set researching to Absent directly. |
| 10 Names | Perform the materialization/name procedure below. |
| 11 Copy knowledge | Pick another; require full reply>0. Copy its entire local knowledge vector, set selected advance count 0, rescan all 100 with V03 query and increment the byte count for each true result. Do **not** change global known-by, FutureTech count, grant effects or research choice. |
| 12 Gender | Toggle Female flag; publish corresponding0/1 in the live tribe's female value; set portrait to the byte tribe ID and add21 if female. |

Treaties first call V04's clear primitive for HostileIntent and Broken. Set or
clear Contact according to checkbox0. If War is unchecked, clear War, Ceasefire,
Peace and Alliance through the primitive, then set checked Ceasefire, Peace,
Alliance in that order. If War is checked, set War through the primitive instead.
Their cascade/symmetry rules remain [V04](V04-diplomacy-and-treaties.md)'s. Then,
**only in the selected→other entry**, clear Vendetta and Embassy directly and
restore each according to its checkbox, in that order. Do not replace this
mixture of primitive calls and unilateral stores with a uniform treaty setter.

Name editing first obtains the plural name for display. For leader, plural and
adjective, in that order, if the live tribe string reference is nonnegative,
copy its current label into the selected civilization's local name field, then
negate that reference. Zero remains zero. Next overwrite all 7×2 tribe title
references from the current generic government titles, government outside gender.
Publish the current local plural/adjective, then run the three-name editor with
23-character limit and local leader default. On a nonnegative result copy its
three text arguments to local leader/plural/adjective. Cancellation retains all
earlier materialization, negations and title replacements. These direct name
changes do not independently rewrite event strings; S04 owns its distinct
rule-rename propagation paths. Name-field overflow is excluded.

## 7. Scenario mode, folders and parameters

Folder preparation first compares the scenario and home directories **case
sensitively**. If different, return1 without creating or saving. Otherwise ask
for an up-to-eight-character folder name, retrying empty names; negative reply
returns0. Uppercase it in place using the original character service, then
validate each character against the classification input in Appendix A. An
invalid character shows BADNAME and retries. Form
`home\SCENARIO\NAME`; any existing file attributes other than failure show
DIREXISTS and retry, regardless of whether the object is a directory.

After acceptance, call mkdir for the path; publish it as scenario directory;
mkdir its SOUND subdirectory; form `scenarioDirectory\NAME.SCN`; invoke F06's
core writer `(path,1)`. Ignore mkdir and writer return values. Return1. Do not
create the SCENARIO parent separately or undo publication on failure. Path
overwrites are excluded; high-bit filename classification needs actual backing
or a declared stricter character profile.

Scenario toggle has three paths:

1. If neither CheatMode nor EverCheated, ask REALLYCHEAT; zero cancels. Set
   EverCheated and CheatMode **before** folder preparation. If it returns nonzero,
   show the warning then set Scenario. If it returns0, retain the earlier flags.
2. Otherwise, if CheatMode is absent, Scenario is absent, or scenario directory
   equals home: if directories equal, show its warning; prepare folder. A zero
   result returns immediately. Otherwise set CheatMode and Scenario.
3. Otherwise clear Scenario only. Do not clear CheatMode, reset parameters,
   change directories or delete files.

Update menu states on normal completion, including folder cancellation in path1
but not the early return in path2. Save As Scenario simply invokes F06's save
dialog with scenario argument 1; its cancellation and file mutations belong there.

### Parameter menu

At the beginning of **every** iteration, recompute/store signed 16 year from the
live turn through F04, redraw status1 and refresh the city window. Then publish
paradigm, increment, starting year, max turns, revolution restriction, Scenario
label and name as menu arguments. Choice0/unsupported exits. S01 owns meanings.

| Choice | Work |
| --- | --- |
| 1…4 | Edit paradigm, calendar increment, starting year, max turns respectively. Only numeric result 0 writes signed 16; no extra range clamp. |
| 5 | Replace Unowned current-owner nibble with civilization0, retaining fertility. Redraw all maps. |
| 6 | Where current owner is not Unowned and neither Unit nor City feature is set, make current owner Unowned, retaining fertility. Redraw all maps. |
| 7 Reveal permanently | Physical tile order: set visibility to all civilizations and copy live features to remembered layers1…7. Then scan live cities ascending: set known-to all, revealed-size entries0…7 to current size, and their center visibility all. Set WholeMapReveal scenario option; redraw all maps. |
| 8 Cover | Clear visibility; if Unit or City feature is present and current owner<8, give visibility only to that owner. Leave remembered layers and city known-to/revealed sizes intact. Clear WholeMapReveal option; redraw all maps. |
| 9 Name | Text entry with limit 24 and old name default; result 0 copies it. |
| 10 | Toggle RevolutionRestriction. |
| 11 | Invoke the victory editor below. |
| 12 | Invoke special-rule editor below. |

For choices 5, 6 and 8, each tile's update depends only on that tile's entry
fields. With a fully backed map and no admitted intervening observer, their
iteration order may differ or updates may run in parallel; complete the map
publication before the subsequent redraw or command. Preserve original physical
tile order when an accepted missing-read/failure boundary exposes a partial
prefix. [Appendix D](#appendix-d-original-parameter-map-traversal) records the
reference wording. Reveal permanently still requires its tile pass before the city pass and option publication; any independent
work within a pass needs its own equivalence argument.

These direct publication loops differ from W03's gradual reveal/contact helpers.
They do not synthesize first-contact or region/ownership recalculation.

Victory editing recomputes total objective value by scanning all live cities
ascending through V08, and queries each matching objective civilization's city
**again** to accumulate its owned total. Publish totals, civilization name,
ObjectiveVictory/WonderObjectives and four thresholds. Choice<1 exits;
choice 1 toggles ObjectiveVictory,2 WonderObjectives,3 selects objective
civilization from None0 then slots 1…7 without Alive filtering. Nonnegative
selection stores signed 16. Choices4…7 edit decisive victory, marginal victory,
marginal defeat and decisive defeat respectively; result 0 stores signed 16.
Do not sort thresholds or enforce monotonic outcomes. Other choices exit.

Special Rules repeatedly displays GovernmentLock, NoConquestTechnology,
NoPollution and HistoricalAI. Choices1…4 toggle the corresponding option;
zero/unsupported exits. These toggles do not immediately recompute dependent
simulation; S01's effect-owner catalog governs subsequent consumption.

## 8. Event editor session and services

The event editor operates on the **live** S02 collection. On opening publish
Running and the active editor, then retain a backup with S02's logical copy
semantics. Present trigger rows in event order, select row0, populate actions and
availability, and clear ClampOccurred. Await actions until Running clears.
A failed backup cannot be treated as a successful editable session. Normal exit
releases the backup. The original temporary owner, allocation and widget lifetime
are recorded in [Appendix C](#appendix-c-original-implementation-reference).

Cancel resets the live collection and copies the backup back, then clears
Running and ends the modal loop. It does not write the event file. This restores
event payload using S02 copying, not all global state touched by editor callbacks.

OK first tests ClampOccurred. If set, show the NOTICE warning, clear the flag,
refocus/redraw and keep the session open; a second OK can proceed. Otherwise
compact, write the event file using §12, show an I/O error if writer returns0,
then clear Running, end the modal loop and update menus **even after that
reported failure**. No automatic retry or restoration of the backup follows.
Neither OK nor a successful edit sets the separate events-loaded indicator.

The shared clamp returns minimum if input<minimum, maximum if input>maximum,
otherwise the input. Any reduction sets ClampOccurred; an in-range call does
not clear it. Name creation checks the declared event-storage budget against
the low16 requested length; insufficient budget reports out-of-space and returns
Absent. Otherwise publish the requested string. Earlier selection/field stores
survive failure. The budget is logical admission state; it does not require a
contiguous pool or the original allocator.

The long-amount input formats the current signed value, requests text with
limit 6, applies decimal conversion and writes its output **regardless of modal
result**, then returns that result. Cancelled/unproduced text therefore needs
its producer/retained backing or a bounded stop, not fabricated zero. Coordinate
input uses F03's **current shared token cursor**, decimal conversion and clamp
0…32767 for each component. Copying an accepted string into the shared line
buffer does not itself reset the cursor. Preserve the cursor produced by the
dialog/resource path; do not silently bind every conversion to the new line's
start. These conversions do not recognize F03's hexadecimal/binary prefixes.

The option-mask dialog loads its named GAME section with the requested default.
On successful load, initialize checked options from shared check state if the
resource has checkbox style; disable each option whose index-modulo32 bit is
in the supplied mask; show the modal; publish extra-result; then collect checked
options into a fresh shared set when applicable. Clear the parent window and
return the full reply. A failed resource load has no produced original reply;
require supplied incoming result or stop after the normal cleanup boundary.

City-name validation opens CITY with no section and reads trimmed lines using
F03. Compare input and each line case-insensitively for the **greater** of their
lengths. On equality close and return1. No actual city-record lookup occurs.
After no match/open failure/EOF, set the editor parent, publish the name, show
NOCITY, clear the parent and return1 exactly when that notice returns0. Unknown
names can therefore be accepted by this confirmation. Repeated string-length
queries and callbacks retain their normal ordering.

## 9. Event rows, selection and availability

Trigger rows are numbered from 1 in current list order. Rebuilding clears the
trigger list, appends one row for each node, then updates buttons. The displayed
kind is uppercase; the optional payload is as follows:

| Kind | Detail; when absent, show number and kind only |
| --- | --- |
| UnitKilled | Unit name, `attacker=` Who name, `defender=` Whom name; require all three references present. |
| CityTaken | City name with attacker/defender names; require all three present. |
| Turn / TurnInterval | Always show the signed turn/interval in parentheses. |
| Negotiation | Talker name/class and listener name/class; require both names. Class1 renders Human,2 Computer, everything else HumanOrComputer. |
| ScenarioLoaded | No detail. |
| RandomTurn | `denominator=` value, unless it is 0. |
| NoSchism | Whom name if present. |
| ReceivedTechnology | Live advance name and `whom=` saved name, if that name is present; the advance lookup retains its input boundary. |

The original row buffer is not initialized for an unsupported trigger. On the
first such node it requires supplied/private incoming contents; after a supported
node it can reuse the previous row's contents. An empty IF does not imply a
defined numbered placeholder. The primary reconstruction's placeholder and
bounded formatting are declared repairs, not original row semantics.

An action-list rebuild resolves the current trigger selection by walking from
head, then clears the action list. For a present event, scan action flag positions
0…14 ascending and append rows only for recognized flags: the twelve parser
actions/JustOnce and Consumed. Names are saved strings except terrain/technology
labels and direct destruction/gift civilization names, which are queried live.
Text shows its first string, Move its unit/owner, Create its unit/owner/veteran/
home, Money receiver/amount, Aggression Who/Whom, Wave filename, CD nonzero track,
Terrain type name. Missing required references give a bare action name. Gift
receiver0 gives a bare name; otherwise show receiver and technology. Consumed
renders HASTRIGGERED. Update buttons after the scan.

For destruction/gift display, any negative recipient renders TriggerReceiver
when the trigger's received-technology bit is set; otherwise−3 renders
TriggerAttacker and any other negative renders TriggerDefender. This bit test
is distinct from the exact-trigger test used by action-owner selection below.

Selection helpers count **all** set action bits, not just displayed rows. The
Nth-set-bit helper walks upward to the requested zero-based set bit; an invalid
ordinal or missing bit can loop indefinitely after the mask is exhausted. The
bit-to-row helper shifts the selected bit right and counts overlapping event
bits at each step. Nominal single-bit arguments yield the count of lower set
bits. Unsupported set bits can make row mapping disagree with visible rows;
do not invent a normalized action set to hide this input defect.

Button update first enables Add Trigger. With negative trigger selection,
disable Edit/Delete Trigger and all three action buttons. Otherwise resolve the
node and enable Delete Trigger. Edit Trigger is disabled when the tag contains
the ScenarioLoaded bit, enabled otherwise. Find the first unused action bit:
for exact ScenarioLoaded, treat all flags except Wave, CD and DontPlayWonders
as already occupied; otherwise use the live action set. Enable Add Action only
when the first unused position is<12 and the tag contains neither Negotiation
nor NoSchism bits. A missing node is a read boundary after prior button effects.

With negative action selection, disable Edit/Delete Action. Otherwise enable
Delete and resolve its Nth set bit; disable Edit for JustOnce, DontPlayWonders,
Consumed or the reserved position 12, enabling it for other bits. Direct handlers
do not repeat every button gate. If every eligible ScenarioLoaded action is
present, the first-unused search has no terminating bit; retain a bounded
nontermination observation or declare a strict/repair policy.

A refresh presents current event choices and available storage. The original
315-unit bar, focus changes and divide-failure trace are optional reference
presentation in [Appendix C](#appendix-c-original-implementation-reference). The declared storage budget and an actual admission
failure remain distinct from the way the capacity is drawn.

### General pickers

All item lists use the editor parent, show a full-value modal result, then clear
the parent. No Alive, enabled-rule or prerequisite filter is added:

| Picker kind | Ordered options; default rewrite |
| --- | --- |
| Civilization | Slots1…7; default0 becomes1. |
| Unit | Types0…61. |
| Technology | Advances0…99. |
| Any unit | AnyUnit followed by types 0…61. |
| Any civilization | Anybody followed by slots 1…7; default0 becomes Anybody. |
| Attacker/defender owner | TriggerAttacker, TriggerDefender, slots 1…7; default0 becomes TriggerAttacker. |
| Receiver owner | TriggerReceiver, slots 1…7; default0 becomes TriggerReceiver. |
| Terrain | Types0…10. |

Action-owner selection chooses Receiver owner only for exact ReceivedTechnology
trigger, otherwise Attacker/defender owner. Exactly−1 cancels. Nonnegative replies
use the live plural name; other negative replies retain their numeric value but
choose the corresponding reserved display spelling (Receiver; else−3 Attacker,
anything else Defender). Trigger-civilization selection uses Any civilization;
any accepted negative reply is normalized to Anybody before name allocation.

Control-class selection defaults Human for stored0/1, Computer for2, Either
otherwise. A negative reply cancels; reply 0 stores Human,1 Computer, any other
nonnegative reply Either. These pickers are narrower nominal UI domains than
S02's file parser.

## 10. Adding, deleting and editing triggers

Add Trigger repeatedly shows TRIGGERS, beginning at option0. Clear Help before
each prompt; if the returned Help indicator is set, show contextual help for
the full returned option and repeat. Exactly−1 as the final reply cancels. Nominal
options 0…8 correspond to S02's nine trigger kinds in its numeric-tag order.
The original gate accepts every reply except−1; it does not impose the
reconstruction's added0…8 bound. Other negative or large replies shift1 by reply
modulo32 and may create unsupported tags.
Their later row/help behavior remains a separate read/termination boundary.

Check record capacity and append a zero-initialized S02 event. On failure show the storage
notice. On success set the trigger, rebuild trigger and action lists, select the
last trigger row, then invoke Edit Trigger. If it returns0, find the **current
last node** and unlink it, compact and rebuild both lists. This is not a rollback
of all effects produced by editing. Update buttons, then refocus/redraw. A
callback that changes the list can distinguish the current tail from the newly
created event.

Delete Trigger sets the parent and asks DELETEITEM. Any nonzero response accepts.
Capture the current selected ordinal, walk to that node, repair predecessor and
successor links, and replace head with its successor only for selected ordinal0.
Compact and rebuild both lists. Clear parent and refocus/redraw on both branches.
No independent count decrement precedes compaction.

Edit Trigger resolves the selected ordinal once and remembers it for the tail.
Each field step below publishes immediately; cancellation/allocation failure
stops that branch but keeps preceding changes. Changed becomes1 only at the
specified final successful step:

| Trigger | Dialog/field order and result |
| --- | --- |
| UnitKilled | Definite Unit picker; publish selector/name; Any civilization Who; Any civilization Whom. Changed only after all succeed. An unusual accepted negative unit reply is normalized to AnyUnit even though the nominal picker does not offer it. |
| CityTaken | If name absent, publish an empty default reference. Repeated name input, limit 23, until cancelled or nonempty and accepted by city-name validation. Allocate/copy name, then Who, then Whom. Changed only after all succeed. |
| Turn | Numeric prompt; result 0 stores clamp−1…32767 and Changed1. |
| TurnInterval | Numeric prompt; result 0 stores clamp0…32767 and Changed1. Unlike most paths, restore the live editor parent after the prompt before the common tail clears it. |
| Negotiation | Who, Talker class, Whom, Listener class, in order. Changed after all four succeed. Do not set its runtime Deny permission field. |
| ScenarioLoaded | No prompt, Changed1. |
| RandomTurn | **Before prompting**, clamp the existing denominator1…1000 and store it. Result0 then clamps/stores the new value and sets Changed1. Cancellation retains the initial clamp and ClampOccurred. |
| NoSchism | Whom picker; Changed on success. |
| ReceivedTechnology | Whom picker, then Technology picker; exactly−1 cancels the second. Store accepted technology and Changed1. |

For every supported/unsupported tag reaching the common tail, compact the
collection, refill trigger rows, restore the captured selection, update buttons,
refocus/redraw, clear parent and return Changed. Do not condition compaction
or repaint on Changed. Existing selected-node references may retire at this
boundary; S02's lifetime contract and Appendix A apply.

## 11. Adding, deleting and editing actions

Add Action resolves the selected event and finds its first unused flag with the
ScenarioLoaded mask described in §9. If the position is≥13, show the no-more-actions
notice. Otherwise show ACTIONS with used bits disabled (plus the loaded-event
mask when applicable), handling Help/repeat like Add Trigger. Exactly−1 cancels.
For another result, set its modulo32 bit, derive its set-bit row, refill actions,
select that row, then edit it. If editing returns0, reacquire the event from the
**live selected trigger ordinal**, clear that requested bit and refill actions.
Keep parameter changes made before cancellation. Always refocus/redraw.

Delete Action first captures the selected event, sets parent and asks DELETEITEM.
On nonzero reply, obtain current action selection and resolve its bit against
the event's live flags. If Text, clear text slot 0 only. Resolve the bit **again**,
clear it, refill actions, then compact. Clear parent and refocus/redraw. Trailing
text references/other payload and Consumed survive unless explicitly selected.

Edit Action resolves the selected event, captures its action flags **before**
querying the action-list selection, and resolves that ordinal against captured
flags. Construct its edit-control context. The following branch mutations are
immediate; Changed is initially0:

| Action | Parameter sequence and completion |
| --- | --- |
| Text | Invoke the multiline editor below; Changed is its nonzero return. |
| ChangeMoney | Owner picker, publish owner/name, then long-amount input. Only result 0 stores clamp−30000…30000 and Changed1. |
| MakeAggression | Ordinary Civilization Who, then Whom, publishing each selector/name. Changed after both succeed; these pickers offer no indirection/wildcards. |
| PlayCDTrack | Pre-clamp/store existing track2…24, publish range for the prompt. Only numeric result 0 clamps/stores new value and sets Changed1. Initial clamp survives cancellation. |
| JustOnce, DontPlayWonders, Consumed | No parameters, Changed1. |
| DestroyACivilization | Owner picker; accepted result stores selector and Changed1, without allocating a name. |
| GiveTechnology | Owner picker stores recipient, then Technology picker; accepted second result stores technology and Changed1. |
| Unsupported bit | No field work; Changed remains0. |

MoveUnit, CreateUnit, Wave and Terrain use the procedures below. After any
branch, compact, refill actions, derive the row for the captured action bit
using the selected event's **post-compaction** action flags, select it, update
buttons, refresh and clear parent. Retain the selected event by logical identity
through compaction and resolve its post-compaction payload before continuing.
The original retired-pointer hazard and reference-adapter rebind are recorded in
[Appendix C](#appendix-c-original-implementation-reference); a new storage model need not create that hazard.

### Movement and terrain parameter editing

MoveUnit chooses Any Unit, publishes its selector/name, then chooses owner and
publishes its selector/name. Format four existing coordinate pairs as eight
comma-separated signed decimals. Repeatedly request a nonempty rectangle string
with limit 31; exactly−1 cancels. Copy an accepted nonempty string to the shared
line buffer and read eight coordinates using §8's cursor/decimal/clamp contract,
storing each in order. No corner ordering or parity validation occurs.

Format the existing destination as two comma-separated decimals; request it
with limit 8. Exactly−1 cancels; accepted empty input leaves destination unchanged,
otherwise parse/store two coordinates. Next ask Number of Units: default−1 if
stored quantity is All, otherwise its stored value. Only numeric result 0 accepts:
input−1 stores All; otherwise clamp0…32767. Set Changed1 only at this final step.
The earlier type/owner/rectangle/destination changes remain on later cancellation.

ChangeTerrain chooses Terrain and stores it, then formats/requests a nonempty
rectangle with limit 31 as above. Accepted reply parses eight components when
nonempty and sets Changed1. Cancellation retains the earlier terrain selection.
Neither action editor invokes its runtime gameplay effect.

### Creation parameter editing

Choose a definite Unit and publish its selector/name; choose owner and publish
selector/name; show Veteran with the old value as default and store a nonnegative
reply directly. If home name is absent, publish an empty default reference.
Repeatedly ask Home City, limit 23, until cancelled or nonempty and accepted by
city-name validation, then allocate/copy it.

For location index 0…9, **first store location count=index**, format that pair,
publish index+1 as the prompt number, and ask with limit 8:

- Exactly−1 at index 0 ends immediately with count 0.
- Exactly−1 at a later index clears that pair and all remaining pairs through 9,
  then ends the loop. The already-published count equals the number of earlier
  accepted positions.
- Otherwise, a nonempty string parses/stores two coordinates; an empty accepted
  string retains that pair. Continue to the next index.

After the loop set Changed1 only if the stored count is nonzero. Accepting all
ten dialogs leaves count **9**, although pair9 was stored. Do not replace this
with a successful-dialog count or automatically add one. This differs from
S02's LOCATIONS parser.

### Wave and multiline text

Wave publishes an empty default reference if absent. Repeatedly request a
nonempty filename with limit 10; exactly−1 cancels. For nonnegative completion,
if the string contains no period anywhere append `.WAV`, uppercase the entire
string, allocate/copy it, then set Changed1. No existence check occurs. Overlong
append/format writes are excluded; a larger safe buffer is a declared repair.

The multiline Text editor publishes its output-array reference and Active flag.
Prefill the control from text slot 0 until the first absent slot or20, inserting
line separators between entries. Show its modal loop and wait until Active
clears; then close the frame and return the published Result. Cancel publishes
Result0, clears Active and ends the loop without changing the array.

OK clears all 20 output references **before compaction**. Then read control lines
0…19 individually. An absent/zero-length line produces an absent slot; otherwise
allocate length+terminator, publish the reference and copy. Allocation failure
leaves that slot absent and stops the remaining reads. Publish Result1 and
clear Active even after partial string creation; end the loop. The later slots
were already cleared. Retain logical event identity through compaction; original
pointer retirement is reference evidence, not a required storage hazard. The runtime Text action scans past holes, whereas S02 copying and
§12 text writing stop at the first hole.

### Help

With an action selected, resolve its set bit and find its least set-bit index,
then open the corresponding HELP action topic. Otherwise use selected trigger's
least set-bit index; with neither selected, open the general EVENTS topic.
Trigger topics follow indices0…8; action topics follow0…11. Topic names are
HELP plus the respective uppercase trigger/action word. Publish editor parent
before the notice, clear it afterward and refocus/redraw. An unsupported topic
index has an unproduced original topic pointer. A zero bit/tag makes the original
least-bit loop nonterminating. The reconstruction's zero-tag help suppression
is a repair; a bounded stop/declared stricter profile is appropriate.

## 12. Event text output and replacement

This writer is distinct from F06's binary persistence. Use signed decimal
formatting without numeric padding, no escaping/quoting, and the exact line
spellings below. Logical newlines become the text-file service's native CRLF
on the original platform. Retain partial writes and file-operation order.

### Record and action output

Walk events in live list order, reloading the current successor after writes.
For each, write a blank line then `@IF` and newline. Exact trigger tags select
the following output; every field occupies its own line:

| Trigger word (uppercase) | Following fields in order |
| --- | --- |
| UNITKILLED | `unit=<saved name>`, `attacker=<saved Who name>`, `defender=<saved Whom name>`. |
| CITYTAKEN | `city=<saved name>`, attacker, defender. |
| TURN | `turn=<integer>`; Every is written as−1. |
| TURNINTERVAL | `interval=<integer>`. |
| NEGOTIATION | `talker=<saved Who name>`, `talkertype=<class>`, `listener=<saved Whom name>`, `listenertype=<class>`. Classes1/2/4 write Human/Computer/HumanOrComputer. |
| SCENARIOLOADED | No parameters. |
| RANDOMTURN | `denominator=<integer>`. |
| NOSCHISM | `defender=<saved Whom name>`. |
| RECEIVEDTECHNOLOGY | `technology=<integer>`, then `receiver=<saved Whom name>`. |

A known trigger then calls the action writer. Unsupported tags emit neither
trigger text nor actions. All records that reach the tail emit `@ENDIF` and
newline. After the list ends emit one blank line. Negotiation's unsupported
class writes only its `talkertype=` or `listenertype=` prefix **without newline**;
the following output consequently joins it. Do not manufacture Either.

The action writer writes `@THEN` and newline **before** checking event presence
or empty actions. Then scan flag positions 0…14, reloading flags at each new position after the
preceding writes. A selected branch continues even if a write changes its flag. Emit recognized enabled actions in numeric-flag order, which differs
from S02 runtime execution order:

| Word (uppercase unless shown) | Following output |
| --- | --- |
| TEXT | Strings from 0 until first absent or20, each followed by newline; `ENDTEXT`. |
| MOVEUNIT | `unit=<saved name>`, `owner=<saved name>`, `maprect`; one line containing eight signed coordinates separated by commas; `moveto`; destination pair; `numbertomove=ALL` for All, otherwise signed integer. |
| CREATEUNIT | `unit=<saved name>`, `owner=<saved name>`, `veteran=yes` for nonzero or `no` for zero, `homecity=<saved name>`, `locations`; one X,Y line per index below the live count; `endlocations`. |
| CHANGEMONEY | `receiver=<saved name>`, `amount=<integer>`. |
| PLAYWAVEFILE | Saved filename on the next line. |
| MAKEAGGRESSION | `who=<saved name>`, `whom=<saved name>`. |
| JUSTONCE | No parameters. |
| PLAYCDTRACK | Signed track number on the next line. |
| DONTPLAYWONDERS | No parameters. |
| CHANGETERRAIN | `terraintype=<integer>`, `maprect`; one eight-coordinate line. |
| DESTROYACIVILIZATION | `whom=<resolved output spelling>` below. |
| GIVETECHNOLOGY | `receiver=<resolved output spelling>`, `technology=<integer>`. |
| HASTRIGGERED | No parameters; emitted for Consumed after the other recognized actions. |

For destruction/gift, nonnegative recipients use a live plural-name query.
Negative recipients use TRIGGERRECEIVER when the trigger's received-technology
bit is set; otherwise−3 uses TRIGGERATTACKER and any other negative value uses
TRIGGERDEFENDER. Other action/trigger names come from stored strings, even when
their resolved selectors have changed since parsing. Formatted null `%s`
uses the original CRT's `(null)` substitution; direct Text writes require a
present string. Embedded newlines are not escaped.

Each checked failed output returns from its **current helper**. The action
writer returns no success status, so the record writer can still write ENDIF
after an action failure. The record writer also returns no status, so the file
wrapper below may continue and report success after an earlier child write
failure. Its final blank-line write is unchecked. Do not replace these with
atomic serialization or infer success solely from the wrapper result.

### File replacement

1. Form `EVENTS.` plus the live language extension. Capture current directory,
   then change to the live scenario directory. Failed directory services retain
   their original output/continuation boundary; no invented saved directory.
2. If EVENTS.BAK exists, remove it; failure prevents later work. If the target
   EVENTS file exists, rename it to EVENTS.BAK; failure likewise prevents later
   work. Earlier successful removal/rename is not undone.
3. Open EVENTS.BAK for text reading, then open the target for text writing,
   even if no backup exists. A failed destination open returns failure after
   cleanup. A failed backup open is treated as no old file.
4. Without a readable backup, write `@BEGINEVENTS` and newline. With a backup,
   repeatedly read at most255 characters and immediately copy each chunk to the
   destination; only then strip newline/trim the local chunk and test it against
   `@BEGINEVENTS` case-insensitively. EOF or failed copy before the marker fails.
   Thus the preamble and marker retain their original spelling/spacing.
5. Write the current event list using the void child above.
6. Without backup, write `@ENDEVENTS` and newline; successful write sets result 1.
   With backup, read/discard chunks through its first trimmed ENDEVENTS marker;
   missing marker fails. Write the canonical `@ENDEVENTS` line, then copy all
   following chunks verbatim until EOF. EOF after successful copies sets result 1.
7. On normal exits close a present source, then a present destination, then
   restore the captured directory. Ignore close/directory-change results. Return
   the accumulated result, initially0. No backup restoration occurs on failure.

### Round-trip consequences

Do not claim editor text export/import preserves runtime state:

- S02 ignores HASTRIGGERED as an unknown action word. Binary F06 save/load
  retains Consumed, while a fresh text parse does not.
- Sticky wildcard bindings change selectors but not their saved names. Writing
  names and reparsing can restore Anybody or resolve renamed civilizations.
- A newly allocated Negotiation event starts with permission0; editor fields
  do not change it. S02 parsing a complete Negotiation block sets Deny. Export
  and fresh parse can therefore change negotiation permission.
- The writer stops Text at its first hole, while runtime display scans all 20.
- Signed off-nominal values written literally can fail S02's digit-gated fields;
  missing names, unsupported tags and unsupported control classes can produce
  files that do not complete the same parser path.
- Editor CreateUnit's all-ten-dialog case publishes count 9, so only nine pairs
  are written. Text parsing's location counting is a different algorithm.

## 13. Conformance cases and caller integration

Compare ordered state, callbacks and file operations, not only final screens:

- Password-blocked toggles, first cheat warning/cancellation and multiplayer
  timer disable/restore; every scenario-toggle path and folder cancellation.
- Changed directory during prompts, uppercase folder admission, existing file
  attributes, ignored mkdir/save failures and stop before path/name overflow.
- Technology add/remove/all/copy distinctions, enabled menu versus all 100 recount,
  retained FutureTech/global discovery and direct civilization numeric narrowing.
- Terrain irrigation-chain partial mutation, repeat flags/retained Help, cancelled
  second dialog, River/suppression handling, neighbor-region minimum and pollution
  counter wrap. Keep normal W04 and event S02 terrain actions distinct.
- Creation option filtering/default retention, owner-picker fallthrough, failed
  creation redraw; unit/city captured-owner changes and cancelled numeric redraws.
- Scenario reveal/cover versus view-only reveal, remembered-layer/city knowledge
  retention, repeated year recalculation and unsorted victory thresholds.
- Event backup/cancel/OK, clamp warning followed by second OK, writer failure
  still closing, child cancellation with prior fields retained and append/unlink
  selection changes. Exercise actual S02 copy/compaction where backing permits.
- Trigger/action picker domains, unsupported flags and row mapping, zero-mask
  termination boundaries, first unsupported row input and safe retired references.
- Repeated coordinate cursor state, decimal versus prefixed conversion, empty
  location retention, ten-location count 9, Text holes and partial allocation.
- Exact event output chunks/order, live flags/names/links, write failure swallowed
  across void-child boundaries, preserved preamble/suffix and non-atomic backup
  replacement; fresh S02 parse versus F06 binary persistence.

P02/P04 must supply actual menu/control selection and text-cursor producers;
F06/S01 the save/scenario context; S02 actual collection lifetimes and parser;
U01/C01/V01/V02/V03/V04/V08 gameplay children; P05/P06/P10 redraw/media services;
N01/N02/N04 timer, unlock and session effects. Finite adapted source checks do
not establish complete native runtime behavior. X02 retains the independent
whole-state, ordered call/output and failure fixtures.

## Appendix A. Original representations and defect boundaries

S02 owns event tag/action/selector encoding; S01 owns scenario options; F02/F06
own record formats. Edit actions order by flag positions 0…14; loaded-event
allowed flags are16,128,256, expressed as forced-occupied mask`fffffe6f` hex.
Button edit-disable mask is`0x3140`; add-disable trigger mask`0x90`; trigger edit
tests bit`0x20`. Original shifts use low five count bits. Add Trigger's BF-234
bound is a repair; supported original tags remain the nominal profile.

CheatMode is option`8000`, EverCheated game flag`0010`, Scenario game flag`0080`
hex. Current owner occupies the tile's high nibble; Unowned is 15. The direct
knowledge store contains 13 bytes, including its unused tail; ordinary-building
copy contains 5 bytes, then restores Palace. These storage facts support fixtures
without requiring the same representation in a new implementation.

Filename classification accepts, after uppercase, ASCII digits, A…Z,
`!`, `#`, `$`, `%`, `&`, apostrophe, parentheses, `-`, `@`, `^`, `_`,
backtick, braces and `~`. It does not accept
space, dot, slash or backslash. Original classification has 128 entries and
indexes with signed characters; high-bit input requires backing or strict
rejection. Original path locals hold 260 bytes. BF-080's bounded construction is
a safe repair; overflowing them or adjacent filename state is excluded.

Event backup/compaction uses50,000-byte pools and 452-byte nodes. Editor text
reads at most 20 lines into a256-byte temporary. Native names/default strings and
coordinate formats have fixed extents: eight signed coordinates can exceed the
original64-byte format buffer (BF-036); action writer's original80-byte and
record writer's256-byte lines can overflow on accepted names (BF-048); display
rows can exceed 256 bytes (BF-089). Long sound-name append and name materialization
can likewise spill. Such overwrites and their collateral effects are excluded.
Safe expanded/bounded formatting is allowed when identified; preserve well-defined
output and caller order inside the declared input domain.

BF-014 addresses pointers retained across event compaction, particularly Text
OK and Edit Action's final flags read. Original heap-address reuse is not a
logical guarantee. Reads require actual established backing or stop; invalid
retired-object writes are excluded. A safe implementation can preserve the
selected logical node/text-array identity across its own relocation, explicitly
recording that repair. BF-077's empty-trigger placeholder and help-loop escape
are separate read/nontermination repairs, not evidence of an original label.

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
| Cheat mode and civilization/turn commands | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp), `00554297`…`005569e3`; civilization editor `00556f54`. |
| Scenario parameters and saving | Same source, victory `00557e2c`, special rules `0055819b`, parameters `005582ad`, save-as-scenario `0055891d`. |
| Event writer | `events_write_actions`, `00548e23`; list `00549aee`; wrapper `0054a4c4`. |
| Event choices, session and editing | Same source, `0054a912`…`0054ffc8`; helpers are split from original trigger/action bodies `0054c4a1/0054d7ef`. |
| Text editor/common services | [editor.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/editor.cpp), option mask `004cdd3d`, clamp `004cdf4b`, Text OK/cancel/run `004ce196/004ce2f3/004ce38a`, city-name check `004ce72b`, long input `004ce8a4`, folder `00553dfd`, scenario toggle `00553ff6`. |
| Civilization picker/allocation | [support.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/support.cpp), `00554145/004cca35`. |
| Selection bits and filename classification | [graphics.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/graphics.cpp), `0054a874/0054a8d3`; [text.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/text.cpp), `00553db0`. |
| Input admission and literals | [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp), map-key/ASCII/command paths; [gamedata.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/gamedata.cpp), formats and classification. |

Independent read-only evidence is `civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`: `src/cheat_commands.cpp`,
`cheat_civilization.cpp`, `cheat_map.cpp`, `cheat_technologies.cpp`,
`cheat_editors.cpp`, `cheat_king.cpp`, `scenario_editor_commands.cpp`,
`events_editor.cpp`, its action/trigger/choices/text/relocation files,
`scenario_event_writer.cpp` and `scenario_editor_text.hpp`. Original executable
SHA-256 is `edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Its accepted reconstruction campaign is not resumed. Repairs and mapped
corruption evidence remain distinguished from the classic contract.

The historical Linux fixture compiled **15 complete S03 bodies**:
all three text writers; clamp and both selection-bit helpers; long-input and
coordinate conversion; CreateUnit and ChangeMoney editing; folder preparation,
scenario toggle, cheat toggle, filename admission and all-advance toggle. It
joins the **50 complete S02/F03 bodies** used by S02, including actual event
allocation/parsing and shared token conversion. **35 assertions pass** in
optimized and AddressSanitizer/UndefinedBehaviorSanitizer builds with defined
signed wrap. Leak checking is disabled for the allocation adapter.

The checks cover partial editing/cancellation, the supplied old token cursor,
ten locations/count 9, allocation failure after selector publication, folder
publication despite service failures, password/timer behavior, direct knowledge
clears, exact text output and real fresh parsing, Consumed/Negotiation differences,
write-error swallowing, backup/preamble/suffix order and live successor reload.
Twenty-four source records and 351 literal declarations retain their fields and
initializers. Host pointer carriers and event allocation sizes are widened;
Windows long-output storage is represented by signed 32. The fixture retains
safe source formatter repairs and tests bounded strings, not overflow writes.

Explicit adapters supply choices, text/parent controls, available strings,
allocation, file/CRT services and gameplay children. Coordinate cases separately
supply a new-line cursor and an old retained cursor; they do not establish P04's
actual producer. The filesystem is an ordered in-memory service, with logical
LF chunks; native CRLF, failures, retained outputs and callbacks still require
platform integration. All-advance tests verify ordered grant calls and direct
clears, not the complete V03 grant effects. No real event compaction, native
window lifetime or arbitrary stale pointer is exercised. Sanitizer checks cover
only these finite executed paths.

The initial compile probe resolved fixture literal declarations; an initial
expected-string check was corrected to the actual uppercase TRIGGERRECEIVER.
Bounded original instructions at `0054d578…0054d602` confirm Add Trigger's
exact−1 cancellation and low-five-bit shift, distinguishing BF-234. Source-pin
checks cover all 72 inventoried S03 bodies plus 50 S02/F03 fixture bodies, records,
literals and enums; reference cleanliness and executable hash are verified.
No fresh native gameplay or annotation campaign is claimed. Link/anchor,
index and whitespace checks complete document validation. Remaining
joins are the actual P02/P04 control/cursor paths, S02 backup/compaction, F06 file
services, gameplay mutation children and native network/presentation services.
X02 must retain independent ordered state, callback, file and failure traces.

## Appendix C. Original implementation reference

This appendix preserves original storage, allocation, native API and adapter
procedures for reference validation. The main text defines required behavior.
A different internal representation or platform is permitted; semantic identities,
ordered game effects, accepted input interpretation, file/wire values and required
failure/completion results remain governed by the main algorithms and X01.
Mixed excerpts retain context: their required effects are not made optional by
appearing here. Original memory/handle traces are not a second normative algorithm.
Optional providers and harmless UI failures retain X01's existing scope limits;
this reorganization introduces no new gameplay or interoperability relaxation.

### Original event-editor backup and construction

The event editor operates on the **live** S02 collection. On opening, publish
Running and the active editor, allocate/construct a temporary event owner,
reset it and copy the current collection into it. Then construct trigger/action
lists and buttons. Trigger rows follow list order; select row 0, fill actions,
update buttons, clear the shared ClampOccurred flag, show the editor and run
its modal/message loop until Running clears. Normal exit destroys the backup
and clears its reference. Backup allocation failure does not authorize an
invalid dereference; stop at that boundary. Native layout/lifetime is P01/P04.

### Original editor name-pool allocation

not clear it. Name allocation first checks available storage against the low16
requested length; insufficient capacity shows the out-of-space message and
returns Absent. Otherwise call the pool allocator. Selection/field stores that
precede allocation survive failure.

### Original event-editor storage meter

Refocus/redraw sets keyboard focus to the trigger list and redraws the event
window. Its storage meter displays free capacity and computes unsigned 32
`free * 315 / total`; the remainder of the 315-unit bar is used capacity. Zero
total reaches F01's divide-fault boundary. Native geometry/colors belong to P04.

### Original event-editor reference across compaction

buttons, refocus/redraw and clear parent. Original code retains a native pointer
across compaction; do not silently claim its address remains valid. A safe
logical rebind must be declared, or require established backing/stop at the
retired read. This does not authorize subsequent invalid writes.

### Original multiline-editor pointer retirement

clear Active even after partial allocation; end the loop. The later slots were
already cleared. Preserve the original pointer-retirement boundary across
compaction, or declare a safe logical rebind; no use-after-free corruption is
required.

## Appendix D. Original parameter-map traversal

The following sequence records original reference evaluation. X01's ordering
convention governs cleanroom conformance: retain the dependencies and failure
effects stated in the main contract; unobserved query/store order alone is not
an additional requirement. Historical source traces retain their evidence scope.

| Choice | Original evaluation |
| --- | --- |
| 5 | Physical tile order: replace Unowned current-owner nibble with civilization0, retaining fertility. Redraw all maps. |
| 6 | Physical tile order: where current owner is not Unowned and neither Unit nor City feature is set, make current owner Unowned, retaining fertility. Redraw all maps. |
| 8 Cover | Physical tile order: clear visibility; if Unit or City feature is present and current owner<8, give visibility only to that owner. Leave remembered layers and city known-to/revealed sizes intact. Clear WholeMapReveal option; redraw all maps. |
