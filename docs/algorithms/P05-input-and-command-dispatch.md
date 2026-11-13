# P05: Input and command dispatch

This specification describes classic Civilization II Multiplayer Gold Edition
5.4.0f, patch 3 menu construction and availability, keyboard and mouse delivery,
command routing, focus, queued input and timer ordering. Different event-loop and
widget implementations are permitted. Preserve semantic commands, their state
changes, arguments and required gameplay/network ordering. Original native input
traces and harmless UI failures follow the policies below.

[F01](F01-determinism-and-arithmetic.md) owns arithmetic;
[F03](F03-rules-and-configuration.md) owns text-file reading and option changes.
[P01](P01-map-projection-and-interaction.md) owns map geometry, zoom actions,
selection, click/hold controllers and view placement;
[P02](P02-map-rendering-and-minimap.md) owns redraws;
[P04](P04-text-and-dialogs.md) owns text, captions, controls' own behavior and the
shared modal loop. This document supplies events to those controllers.
[U04](U04-unit-orders-and-activation.md) owns unit activation, View/Move-units
transitions and unit commands. [N02](N02-network-commands-and-state-synchronization.md#polling-and-deferred-input)
owns network polling and deferred-request execution;
[N03](N03-multiplayer-turn-sequencing.md) owns the turn countdown. P06–P10 own the
city, report, encyclopedia, council/throne and media consumers named below.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Required contract and reading guide

Command availability and actor/target, View/Move meaning, busy/action gating,
committed effects, timer/deferred-network delivery, response mapping and
gameplay-dependent ordering.

The numbered main sections specify required behavior under
[X01](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy).
Original rendering, geometry, widgets, cosmetic media and native traces are in
[Appendix D](#appendix-d-original-presentation-reference); they are optional reference material.
Main-section numbers are retained for stable citations. Required calculations
that happen inside a presentation operation remain in the main text, including
state mutations, visibility/choice rules and file/protocol effects. Native event
traces do not replace semantic operation boundaries. Harmless UI failures and
rejected off-nominal inputs follow X01's declared safe policies.

## 1. Inputs and observable state

Nominal inputs are initialized game and semantic interaction state, valid
unit/city identities, complete resources and backed collections. Required state
includes mode, observer, active unit, movement mode, map selection, logical view
preferences, input-enabled/busy, city-processing and network-operation guards,
one deferred request plus optional attached character, available commands and
pending result. External gameplay/network events and relevant clocks remain
inputs. Native message records, keyboard bindings, widget focus/order and timer
slots are reference adapter details in Appendix D.

No general transaction surrounds dispatch. A callback can change the active
unit, observer, window, menu, flags or callback registrations. Unless explicitly
captured, read them at the operation where used. Clearing busy flags below is
ordinary-return behavior, not an exception/fault cleanup guarantee. GUI command
availability and the action's own validation are distinct algorithms.

Arithmetic uses F01: scalar wrap at the stated boundary, signed division toward
zero, and arithmetic shifts where named. `SAR(x,n)` means arithmetic shift.
Characters are bytes and use supplied classic CRT classification/case mapping.
The key adapter in Appendix B distinguishes ordinary Enter, Space and keypad
keys even where consumers share their actions.

[X01's exclusion](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
applies to every arena, text, record, stack, timer and message write. Overwrites
are bugs, including mapped adjacent objects; reject, omit or stop before them
while retaining preceding valid effects. Scalar wrap and bounded defects remain
in scope. An unproduced read requires established backing, supplied retained
input or a stop. A declared restrictive profile may reject unsupported input.
Do not silently fabricate zero geometry, stale messages or timer records.

## 2. Message pumps, polling and waits

Service relevant game/network events while a semantic operation is pending.
Apply those events before subsequent live state reads; preserve explicitly
captured values across them. A presentation-only media or paint refresh is not
itself a new game command. Platform message IDs, pump counts, queue filtering
and native mouse-release loops are in Appendix D.

Where a caller requires discarding queued input, discard its stale user input
without executing it. Effect preparation has this discard only when the
simultaneous-movement/creation gate is clear. Do not discard game/network events
as a substitute for this semantic input policy. Sample requested modifier intent
at its specified command boundary.

Cosmetic waits and button delays may be replaced. N03 owns turn/countdown and
multiplayer deadlines, using F01 time arithmetic; replacing a UI wait must keep
its required network work responsive and preserve the caller's completion,
cancellation and continuation effects.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 3. Native event delivery

Translate platform input into semantic commands, identified targets and explicit
modifier intent. Deliver gameplay/network events at their required boundaries.
Native message numbers, packed coordinates, focus/capture and paint dispatch are
reference details.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 4. Control focus and parent dispatch

Keep control availability and semantic action identity. Focus traversal,
accelerators, scrollbars and native parent dispatch may be implemented
differently; they must not bypass action gates or change the actor/target of a
command.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 5. Menu model, construction and presentation

[Native JSON menus](../content/data-format.md#63-menus) supply labeled action
trees; registered action identities preserve the command routes and live state
below. The classic MENU row-to-action mapping remains its adapter contract.
Resource labels and shortcut hints do not register new executable commands.

Expose the supported commands and apply §6 availability. Resolve an accepted
command to its semantic command identity, then invoke §7. Scenario-authored labels
and offered values remain supported; menu group geometry, widget storage and
native menu IDs are presentation/reference adapter details.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 6. Main-menu availability

An availability refresh performs these phases in order. `disable(name,b)` and
`hide(name,b)` replace that Boolean state; neither tests the other state.
Labels are P04 label strings expanded with the current substitution parameters,
then copied into the existing item and followed by its disabled-state update.
No unspecified item is reset merely because another gate changed.

### Session and government

First disable Spaceships when the custom rule suppresses them. Then:

| Game mode | Ordered assignments |
| --- | --- |
| Zero | Disable Join, Change Timer, Chat, Multiplayer Options, Game Profile; enable Load and About. |
| Nonzero | Disable About; enable Multiplayer Options and Game Profile; if menu-mode override is zero disable Toggle Cheat; disable Load. Enable Join only when join-allowed and mode=1. Disable Throne only for mode=1. Disable Chat when mode<3. For mode=2 disable Change Timer and Save; otherwise disable Change Timer when not server and enable Save. Enable Quit and Retire. |

Next disable View Pieces when movement mode is zero, and Move Pieces when it is
exactly one. Disable Revolution if scenario rules forbid government changes;
otherwise disable it when the live local civilization is in Anarchy. These
refresh checks do not by themselves add checks to direct command execution.

### Active-unit orders

Enter this branch only when movement mode is nonzero, total-unit count nonzero,
active slot nonnegative and **no greater than** the count, and its identity is
nonzero. The inclusive upper bound can read an extra backed slot; do not silently
change it to the directional-movement test in §8. Capture the active slot,
coordinates, signed owner, terrain and signed role. Later rule reads use the
cached slot's **live type**; owner, terrain and role remain captured.

First enable the entire Orders group. Then perform:

1. Hide Build/Join City unless role is Settle. Set labels Build City, Road,
   Irrigate and Mine, disabling each unless Settle.
2. Hide Fortify for Settle; hide Fortress unless Settle plus Construction;
   hide Airbase unless Settle plus Radio; hide Unload for Settle; hide Automate
   Settler unless Settle.
3. Hide Paradrop unless the owner has Combined Arms or the live unit type has
   the paradrop flag. Separately disable it unless that flag is set.
4. Hide Airlift unless owner has Radio; disable it when no city owner is at the
   captured tile.
5. For nonsettlers disable Transform, Fortress and Clean Pollution. For settlers
   perform the terrain branch below.
6. Use Home City label when a city owner exists, otherwise the alternate Home
   label. The city-present branch queries city ownership again for its disabled
   predicate. Both normally enable the item.
7. Disable Unload when live type has zero transport holds. Hide Pillage for
   settlers. If live type attack is zero or its domain is not Ground, disable
   Pillage. The domain read short-circuits after zero attack.

Without an eligible active slot, disable the whole Orders group, then enable
Disband and Activate Unit. Prior hidden states remain.

For the settler terrain branch:

- Visit all eight adjacent locations using W01 horizontal wrapping and validity;
  note whether any has a city owner. Find a city on the current tile. Without a
  city, disable Build City for ocean or an adjacent city. With a city, relabel
  Join City and disable only when signed city size exceeds 9.
- Disable Clean Pollution if no pollution. Independently disable Fortress and
  Airbase when the tile has a fortress without a city, an airbase, ocean or a
  city. These conditions are queried separately for the two commands.
- If road exists and Railroad is known, relabel Railroad and disable for existing
  railroad, city or ocean. Otherwise disable Road for ocean, city, existing road,
  or river without Bridge Building.
- Read signed irrigation target. A negative target with no irrigation disables
  Irrigate only for target −1, ocean or city. With existing irrigation, relabel
  Farmland and disable for missing Refrigeration, ocean or city. A nonnegative
  target copies that terrain's name into string parameter zero, relabels the
  transform-style irrigation label and enables it, without those extra tests.
- Read signed mining target. Negative disables Mine only for target −1, ocean or
  city. Nonnegative copies target terrain name, relabels and enables it.
- Query whether the owner can produce Engineers (unit type 1, no city) and hide
  Transform when false. Read signed transform target. Negative disables it;
  otherwise substitute target terrain name, relabel and disable unless the
  cached unit's live type is Engineers. These are UI predicates, not W04's work
  eligibility algorithm.

### Final cheat, editor and network pass

Disable the Cheat group unless cheat is on and game mode exactly zero; check
Toggle Cheat under the same condition. Then independently disable Toggle Cheat
only when game mode is nonzero and menu-mode override is zero. Read mode before
the full override value even when mode is zero. This matches the builder; the
original instruction sequence, rather than the independent port’s inverted
mode test, determines this rule (Appendix A).

For game mode at least 3, disable Orders again if the live network turn
civilization differs from the local observer. This overrides the earlier
Disband/Activate enabling. Check Scenario Flag under cheat + mode zero + scenario
+ unequal search/install paths; independently disable Editor unless those hold;
then disable Scenario Flag for nonzero mode. Finally present the existing menu
without a new window attachment. Directory comparisons are exact byte-string
comparisons, not canonicalized filesystem paths.

## 7. Main-menu command execution

### Entry, deferral and city processing

Query transport flush-in-progress **first**. If zero, short-circuit the live
operation guards in this order: unit create/delete, city create/delete, map
lock/unlock, stack unlink/link/bottom, unit relocation, wonder acquisition,
civilization elimination/create/check-destroyed, cargo gathering/lifting/deletion,
and unit destruction/redraw. Input-busy is absent from this scan.

If any blocks, log the deferred-menu notice, then store request kind Menu,
requested group and requested command, replacing the pending request. The stores
occur after logging callbacks. Return. N02 owns later execution and its order.
If unblocked but city-processing is active, show the city-modal notice below and
return. Otherwise query the primary special cursor; if set, reset eligible map
cursors through P01. If the requested command is Paradrop, return after reset;
other commands continue.

Dispatch uses the command ID alone; the group argument is retained for deferral
but does not restrict the switch. It does not run availability refresh, set
input-busy or validate that the menu item exists. Unknown commands do nothing.
Unless stated, child results are ignored and local-observer arguments are read
at each call.

### Command routes

| Commands | Action and owner |
| --- | --- |
| Game/Graphic/City Report Options | F03's corresponding option transaction. |
| Multiplayer Options; Game Profile | N03's multiplayer option dialog; N01 profile with argument 0. |
| Pick Music | P10's music chooser. |
| Save; Load; Join | [F06 menu-save(0)](F06-save-load-and-file-formats.md#save-menu-wrapper-and-native-file-choice), §9 current-game load, [N04’s local joining flow](N04-hotseat-and-player-handoff.md#6-joining-a-running-hotseat-game) respectively. |
| Password; Change Timer | N01 password(local observer); N03 timer-change chooser. |
| Retire; Quit | [V08](V08-scoring-victory-and-retirement.md) retirement/quit request with argument 1/0. |
| Hotseat exit | Only mode=1: P04 `REALLYQUIT` with information sprite, zoom 0; nonzero result requests exit, then clears game-active. Do not recheck mode after the prompt. |
| Tax Rate; Throne; Find City; Revolution | V02 tax(local observer); [P09 throne](P09-council-and-throne-room.md#8-throne-construction-windows-and-viewing)(local observer); find-city controller; V02 revolution controller. |
| Move Pieces; View Pieces | U04 focusActive(1); enterView(0). |
| Zoom In/Out; maximum/standard/medium zoom | P01 accepted zoom actions on primary map; standard uses current cursor as center. |
| Map Grid | Toggle option; update its menu check; P02 redraw all views(local observer,1); copy **live post-redraw options** to remembered options; F03 save preferences. |
| Arrange Windows; Hidden Terrain; Center View | Layout sequence below; temporary terrain display below; P01 center primary on live cursor. |
| Build/Join City | [C01](C01-city-lifecycle-and-ownership.md#player-build-city--join-city-command)'s active-settler city command. |
| Road, Irrigate, Mine, Transform, Airbase, Fortress, Clean Pollution | W04's active-unit work command with the matching order. |
| Automate Settler; Pillage | W04 automation; W04 pillage. |
| Unload; Goto; Paradrop; Airlift; Home; Fortify; Sleep; Disband; Activate; Wait; Skip; End Player Turn | Corresponding U04/U05/U01 command; preserve that owner's validation and partial effects. |
| Chat | N01/N04 human-chat controller with arguments (0,4). |
| City/Defense/Foreign/Attitude/Trade/Science/Casualty reports | P07 report for local observer. |
| High Council | [P09 council](P09-council-and-throne-room.md#3-council-construction-and-initialization)(local observer,cheatPreview). Preview eligibility checks cheat and mode=0 **before** asynchronous Shift high-byte query; no recheck afterward. |
| Wonders; Top Five; Score; Demographics; Spaceships | World-report choices below. |
| Toggle Cheat | S03 toggle only if mode=0 or menu-mode override nonzero. |
| Cheat actions | S03 actions only if cheat and mode=0. Terrain first sets the retained terrain selection to absent; Destroy Units follows deletion with U04 focusActive(0). |
| Scenario Flag; Advances/Cities/Effects/Improvements/Terrain/Tribes/Units/Events Editor | S03 toggle / S04 editor wrappers. These menu dispatch cases add **no** cheat/mode gate. |
| Civilopedia categories | [P08](P08-civilopedia-and-technology-tree.md#4-controls-opening-and-closing) opens the requested category with show=1. Game Concepts has an impossible Wonders-command test in its branch; it never runs that preview or Shift query. |
| About | Only mode<3: P04 `NEWCREDITS` mode 1; if signed result>=0, P10 About(result); close rule file regardless of that result. |

World-report commands query asynchronous Shift first, then test live cheat and
mode exactly zero. With all true: Wonders opens the wonder-movie chooser; Top
Five opens defeat ending(local observer); Score opens conquered ending(local
observer); Demographics opens spaceship ending with wrapping selector
`−((localObserver+1)&7)`; Spaceships opens spaceship ending(local observer).
Otherwise invoke the ordinary report. Ordinary Score then checks live score-lock;
if set, invoke Fame with freshly read observer, then History without another
score-lock test. Ordinary Spaceships opens its world chooser, which does not
consume the caller's pushed observer argument.

### City-modal notice and shared view actions

The city-modal notice returns immediately if its own byte guard is nonzero.
Otherwise set it, clear the shared text line, append `CITYMODAL` and decimal
`(cityModalMode==2)+1`, then show that GAME section. After the dialog, inspect the
**shared dialog result**, not its returned value: if nonzero close the city
window. Clear the notice guard on normal return. A consumer of its full return
gets the dialog/close child's result, or the initial nonzero guard on reentry;
unknown child return state requires a supplied value.

Hidden Terrain clears blink-active, sets show-hidden-terrain, redraws all views
for the live observer with 1, shows `HIDDENTERRAIN`, clears the display flag, then
redraws for the now-live observer with 1. It does not restore an earlier flag.

Arrange Windows may choose new geometry. Preserve its semantic effects: hide the
city; clear overview/status suppression; retire enabled secondary maps1..7,
clearing each Enabled state before reset; refresh the primary map and prepare
effect presentation (§2). Original maximize/move/frame order is in Appendix D.

## 8. Map keyboard and character controllers

A new input binding resolves to a semantic command before dispatch. Physical
key codes, case-sensitive shortcut spellings, keypad aliases and native key-state
queries are reference adapter behavior in Appendix D. They do not constrain new
controls. Preserve these routes and gates rather than mapping every action to a
single generic handler.

### Command admission and deferral

Require input-enabled. In nonzero game mode during city processing, a Close/Accept
city action closes an open city before input-busy or transport tests. Other
commands return for input-busy in nonzero mode. Use §7's transport/mutation guards;
a blocked key-route command replaces the one deferred request and clears its
pending attached character. N02 owns its later delivery. A character attached to
a pending key is retained with that request; it is not a separate FIFO.

Set input-busy for an admitted command. During city processing, Close/Accept closes
an open city; other commands show §7's modal notice. Clear busy on ordinary return.
Outside that mode, clear the pending map gesture and reset targeting cursors before
a new key-route action. A pure modifier/no-action event has no command effect.

With an open city and Enter-closes-city enabled, expose previous/next city, Close,
Change Production, Buy, Happiness, Central Information, Support Map, Rename and
City View. Invoke the P06 action with argument0 when it accepts a control ID,
ignore its return, then clear busy. Other commands retain their general routes.

### General actions and route-specific gates

Use §7's owners for reports, options, commands and edits. The direct key route
has the following additional conditions; do not use menu visibility alone as
callee validation:

| Semantic action | Direct key-route condition/effect |
| --- | --- |
| Close city | Hide only if open. |
| Score | Score(local observer); live score-lock then permits Fame(fresh observer) and History. |
| Spaceship chooser | Custom rules permit spaceships. |
| Chat | Mode>2; arguments(0,4). |
| Scenario toggle; current-game load | Mode0. |
| Hotseat exit | Mode1 and nonzero REALLYQUIT; request exit then clear game-active while busy remains set. |
| Join | Join-allowed and mode1. |
| Toggle cheat | Mode0 or menu-mode override. |
| Save | Mode!=2. |
| Change timer | Nonzero mode, mode!=2 and server. |
| Password | Local observer, with no added mode gate. |
| Multiplayer Options | Nonzero mode. |
| Council preview | Cheat and mode0, then fresh explicit preview-modifier intent; otherwise ordinary council. |
| Other reports/options/end-turn/quit/retire/grid/zoom | Corresponding §7 action; preserve Grid's redraw/remember/save sequence. |

Recheck live cheat and mode after an ordinary action. Editor/cheat commands need
cheat and mode0 on this route. Terrain-change has two semantic variants, one
resetting retained terrain selection to absent first; keyboard Destroy Units
omits the menu route's trailing focusActive call. The appendix records their
original bindings; S03/S04 own resulting edits.

### Direction and active-unit commands

For a direction, require active slot nonnegative, below live total and identity
nonzero. Movement mode exactly1 calls U02 move(active,direction,3); otherwise
move the view cursor: capture cursor x+direction delta and W01 wrap, then read
y+vertical delta; attempt P01 selection. A nonzero result ensures those captured
coordinates visible in primary view, even if selection callbacks changed state.

Character-route semantic actions retain their priority: direct Find City,
Disband, Throne (mode!=1), Revolution, Save (mode!=2), Tax Rate and Medium/Standard
Zoom finish without the View/Active dispatcher. Stack activation, Hidden Terrain,
View/Move toggle and one-step zoom continue to that dispatcher. Toggle chooses
View when current movement mode exactly1, otherwise focusActive(1). Cheat/mode0
also permits score-lock/reveal toggle followed by redraw for the live observer.
Cancelling an existing paradrop target consumes that cancellation action.

View-mode Accept during end-turn waiting clears the waiting latch and returns.
Otherwise Inspect City locates the city at captured cursor: open when local-owned,
score-locked or foreign-inspection permitted; show limited information otherwise
only with Trade or scenario inspection permission. Center uses current cursor.

Active-unit dispatch captures the active slot before gates. In network mode read
signed owner first; a foreign owner suppresses every action except Inspect City.
Inspection uses the same city-open rules without View's waiting-latch shortcut.
Other actions are Skip, Wait, Build/Join City, Center captured unit, Airbase,
Irrigate, Mine, Transform, Road, Goto City, Home, Automate, Airlift, Sleep and
Unload. Fortress/Fortify selects Fortress for Settle role, Fortify otherwise.
Paradrop intent with the unit's paradrop flag invokes Paradrop; otherwise Settle
role invokes Clean Pollution, otherwise explicit Pillage intent invokes Pillage.
Cheat/mode0 permits throne upgrade for the local human. Child owners perform
validation; no universal role/technology precheck is added. Clear busy on ordinary
return. Original byte spelling and ignored-read/native-failure traces are appendix
reference material; accepted semantic actions keep their game effects.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 9. Find City, loading and tutorial advice

### Find City

Construct one P04 prompt, then capture the local observer before loading
`FINDCITY` with Cancel and Sorted enabled. Ignore the load result. Scan city
slots ascending against the live total; skip absent identities. Capture signed
owner. Include a city if score-lock is set, scenario inspection is allowed, owner
equals captured observer, its sign-extended known-mask contains the observer bit,
or its freshly read signed owner equals the observer's low unsigned byte.
The last two comparisons remain distinct for off-nominal observers.

Build each label from city name; for a foreign captured owner append space and
parenthesized civilization adjective. Under scenario objective mode, query V08's
city objective count; when nonzero append space, `(`, label 432, optional `x` and
decimal count when greater than one, then `)`. Append with city slot as option
value and group zero. P04 sorted insertion owns ties. Count included cities even
if the resource already added options.

If none were added, destroy and return without a modal. Otherwise run modal(0).
Negative selection destroys and returns. Nonnegative selection is a city slot,
not its displayed row: read coordinates and center primary map. Do not add an
identity/total-city check. If the prompt's secondary/custom-button result is
nonzero and either the selected city's live signed owner equals captured observer
or score-lock is now set, open it. Destroy the prompt. Out-of-domain custom option
values require backing or a stop; treating them as no selection is a repair.
An application exception after successful construction performs the prompt's
ordinary owned cleanup before propagating; map and text effects are not rolled
back. An unrecovered-state stop does not assert an original exception occurred.

### Current-game load command

Stop countdown(notify=1); hide city; close diplomacy, chat and research help;
clear both auxiliary report selections. Capture whether music is enabled after
those callbacks. Invoke F06's accepted-file dialog with arguments `(0,0,0)`.
Any nonzero result sets game mode to zero, starts countdown(1), and returns.

After successful load, if live mode is nonzero invoke the loaded-player chooser
with argument 1. Copy the local observer's low byte to saved restore player,
then reload it into runtime restore player. Set game mode zero and humans to the
low byte of `1 << (localObserver & 31)`.

If live music is on, and captured music was off, stop the old media device,
close/open CD playback and initialize its track selection, in that order.
Then reconcile playback. If music is off, request the shared playback/exit stop.
Invoke N01's player admission for the live observer, ignoring its result.
[P10](P10-audio-video-and-animation.md) owns those media operations, including their shared latches.

Restore logical view preferences and saved F06 view fields, and make enabled
views available. The original arrangement/maximize/move/title sequence is in
Appendix D; a redesigned layout does not alter the loaded actor or game state.

Finally set movement mode to View, update status, U04 enterView(force=1),
U04 focusActive(0), refresh menus, populate P08's research-help names, and prepare
effect presentation. Only if live mode is now nonzero and live turn-time limit
nonzero, resume countdown(1). Those final gates allow callback mutation after
the earlier unconditional mode-zero store. No rollback of closed windows or a
partially successful load is implied. F06 separately owns the save-menu filename,
file choice and post-write notices.

### Settler tutorial advice

U04 invokes this with a selected settler. Return immediately if a worker hint
has already been shown this turn. Capture its coordinates, signed owner, terrain,
and signed terrain food/shield yields. A candidate city site requires their sum
at least 2 and W01 claimant-or-fertility at least 10. Visit **all eight** valid
neighbors; any strictly greater claimant-or-fertility disqualifies it. Ties do
not. If qualified, center the primary view only if center differs, show the unit
popup `BUILDCITY` from the live tutorial resource, set hint-shown after it returns,
and finish.

Otherwise, if signed current turn minus owner's expansion-advice turn is at
least 25 and its city count below 6, show `EXPAND1` without centering. Store the
**post-popup** turn into the captured owner's advice turn, set hint-shown and
return. Otherwise use C01 nearest-city with `(x,y,owner,absent,absent)`.
Proceed only at distance 1, or distance 2 with a special resource. Copy that
city's name into string parameter zero and capture tile improvements.

- Hills with neither irrigation nor mine: center if needed, show `MINING`, set
  hint-shown. This does **not** bypass the following independent decision.
- Plains, or Grassland under Monarchy, with neither irrigation nor mine and W04
  irrigation-source true: center, show `IRRIGATE`, set hint-shown.
- Otherwise, when owner's live Settlers+Engineers counts sum below 2, captured
  tile has no road, signed terrain movement cost is 1 and the live tile has no
  river: center, show `ROAD`, set hint-shown.

Each popup uses the captured unit but live tutorial filename. Mining and Road
can both be shown in one call; there is no recheck of hint-shown between them.
This is tutorial selection, independent of A03's AI work scoring and W04's
actual command validation. A nearest-city failure reaching name access requires
backing or a stop, not an invented unnamed city.

## 10. Timer scheduling and native service boundaries

Use any scheduler that delivers required game, multiplayer and semantic
completion events. N03 owns countdown registration, deadlines and pause/resume;
P01/P02 own logical map interaction effects; P10 owns media completion. A pending
operation retains its actor/target and captured values, applies relevant external
events before later live reads, and commits caller effects only at their required
boundary. Replacing a timer cannot extend a gameplay deadline or duplicate a
completion action.

Native slot allocation, window-versus-multimedia timers, periodic blink/status
intervals, DLL discovery, focus/capture and synchronous platform callbacks are
optional reference procedures. X01 permits safe native failure cleanup. It does
not permit silently skipping a required game or protocol callback.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 11. Conformance cases and limits

Validate semantic command availability separately from command execution,
actor/target routing, busy deferral, replacement of pending requests, accepted
and cancelled choices, timer/timeout delivery and live network events. Exercise
Find City, load and tutorial decisions with actual children. Native menus,
keyboard shortcuts, focus/capture and repaint traces are optional reference cases.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## Appendix A. Evidence and source identity

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Target executable SHA-256:
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Primary reconstruction revision:
`a71e8aefffe04e6f719e0e213b027b128a747711`.

- [window.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/window.cpp):
  menu execution `004e2803`, construction `004e3a86`, labels `004e4c92`, availability
  `004e4ceb`, menu storage/presentation, focus and control routing.
- [map.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/map.cpp):
  character `00411f91`, key `004125c6` and their mode-specific consumers.
- [controls.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/controls.cpp):
  key translation `005eb3ed`, native control adapters and event callbacks.
- [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp):
  worker advice `00489be2` and unit-cycle invocation.

Independent reconstruction is `civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. Evidence owners under `src/`:

| Area | Modules |
| --- | --- |
| Main menu | main_menu.cpp, menu_command.cpp, menu_storage.cpp, menu_presentation.cpp, menu_platform.cpp, menu_insertion.cpp, menu_geometry.cpp, menu_mode.cpp, menu_modal.cpp, ui_list_methods.cpp |
| Event dispatch | window_keys.cpp, window_events.cpp, window_proc.cpp, base_window.cpp, window_menu.cpp, control_window.cpp, window_show.cpp, window_sdk.cpp |
| Game input | map_key.cpp, map_input.cpp, worker_advice.cpp, city_search.cpp |
| Queue/timers | message_pump.cpp, input_polling.cpp, timers.cpp, window_startup.cpp, map_lock.cpp, network_poll.cpp |
| Native menu boundary | native_popup_menu.cpp, native_menu_resource_timers.cpp |
| File command joins | local_turn.cpp, menu_command.cpp; F06 owns save/load payloads |

The primary menu builder deliberately uses a keyed localization table in place
of original MENU line consumption. That port adaptation is not normative here.
Relevant independent repairs include rejecting excessive mutable menu text,
false-on-no-palette, failed-paint/geometry/class-query guards and invalid custom
Find City choices. Original resource ordering, unknown reads and bounded defects
above take precedence. P04 specifies controls' related repairs.

An additional evidence discrepancy affects Toggle Cheat: independent
`main_menu.cpp` lines 325–327 invert the mode test in availability refresh. Both
original sequences (`004e47ff..004e4825` and `004e5a3a..004e5a60`) zero-extend
the game-mode byte, form **mode==0** with `CMP 1; SBB; NEG`, OR the full override,
then test that result for zero. Thus both disable only for nonzero mode and zero
override. Section 6 follows those original bytes; this source discrepancy is
not a classic quirk. Reference repositories were not changed.

## Appendix B. Original adapter, constants and excluded bugs

### Keys and message codes

Native key map has 256 mutable DWORDs; unlisted entries are zero. Index is the
low byte of the native key. Modifiers are bitwise OR with Shift=0x100, Control=0x200.

| Native key/index | Mapped code |
| --- | --- |
| Backspace 08, Tab 09, Enter 0d | d6, d3, d0 |
| Shift 10, Control 11, Alt 12, Pause 13 | 100,200,400,d7 |
| Escape 1b, Space 20 | d2,d1 |
| PageUp 21, PageDown 22, End 23, Home 24 | c5,c6,c7,c4 |
| Left 25, Up 26, Right 27, Down 28 | c2,c0,c3,c1 |
| Insert 2d, Delete 2e | d8,d9 |
| ASCII digits 30..39; letters 41..5a | Same value |
| Keypad 60..6f | a0..af respectively |
| F1..F12 70..7b | b0..bb respectively |

Values in this table are hexadecimal. Media pump filters 0x3bd; paint 0x0f;
input discard uses mouse0x200..0x209 then keys0x100..0x108. Mouse release polling
uses the first range and nonclient0xa2..0xa5, with left releases0x202/0xa2 and
right0x205/0xa5. System-key query is selector0x400 and consumes0x104/105/106;
Shift/Control query selectors are0x100/200. Special event0x4c8 requires payload
0x20d. Timer messages are0x113 and0x52c. System minimize/restore exact commands
are0xf020/0xf120; base-menu suppression tests0xf100/0xf090.

Native scroll commands0..5 mean line up/down,page up/down,thumb position/track.
Control programmatic set is message0x111,command0x7f. Parent list notifications
1/2 forward0x4c8;0x300/501 invoke edit/selection callbacks. Control kinds used in
focus are checkbox2,radio-group3,edit4,button6,system-list7,scrollbar8,combo10,
special child11. The historical set-focus registration writes event slot14,
which the dispatcher uses for restore; actual focus delivery uses slot15.
The control traversal flag retains only the low byte of its setter argument.

Menu flags disabled/hidden/checked are1/2/4; materialized0x8000. Current event
receiver is original00637ea4. Attached-child behavior uses presentation0x200;
backend native-scroll flags are byte48 bit0x80 and byte49 bit1. Geometry and
native pointer/handle layouts are adapters, not proposed cleanroom storage.

### Menu IDs and resource order

IDs below are hexadecimal. Each ordered item consumes one text line except
`—`, which inserts separator ID0/null text. First line names its group.

| Group / section | Ordered IDs |
| --- | --- |
| 1 GAME | 101,102,103,104,105,—,106,—,110,120,130,—,131,132,—,1f0,1f1 |
| 2 KINGDOM | 201,—,205,210,—,220 |
| 3 VIEW | 301,302,—,310,311,—,320,321,322,324,—,327,328,330,340 |
| 4 ORDERS | 401,410,411,412,413,—,417,418,41b,420,421,—,430,440,441,442,445,—,450,451,—,460,468,470,480,—,490 |
| 5 ADVISORS | 507,500,—,501,502,503,—,504,505,506,—,508 |
| 6 WORLD | 601,602,603,—,605,606 |
| 7 CHEAT | 701,—,711,712,713,—,721,722,—,731,732,740,748,750,752,—,755,760,765,—,768,770 |
| 8 EDITOR | 801,—,802,803,804,805,806,807,808,809 |
| 9 PEDIA | 901,903,905,907,—,909,90b,—,90d,—,9f0 |

Semantic mapping in each group:

- GAME101..106: Game Options, Graphic Options, City Report Options, Multiplayer
  Options, Game Profile, Pick Music;110 Save;120 Load;130 Join;131 Password;
  132 Timer;1f0 Retire;1f1 Quit. The session-inserted1f2 exits hotseat.
- KINGDOM201 Tax;205 Throne;210 Find City;220 Revolution.
- VIEW301 Move;302 View;310/311 zoom ±1;320 maximum8;321 standard0;
  322 medium−3;324 minimum−7;327 Grid;328 Arrange;330 Hidden Terrain;340 Center.
- ORDERS401 Build/Join;410 Road(order5);411 Irrigate6;412 Mine7;413 Transform8;
  417 Airbase10;418 Fortress4;41b Automate;420 Pollution9;421 Pillage;430 Unload;
  440 Goto;441 Paradrop;442 Airlift;445 Home;450 Fortify;451 Sleep;460 Disband;
  468 Activate;470 Wait;480 Skip;490 End Player Turn.
- ADVISORS507 Chat;500 Council;501 City;502 Defense;503 Foreign;504 Attitude;
  505 Trade;506 Science;508 Casualty.
- WORLD601 Wonders;602 Top Five;603 Score;605 Demographics;606 Spaceships.
- CHEAT701 toggle;711 Create Unit;712 Reveal;713 Human;721 Year;722 Kill Civ;
  731 Advance;732 Edit Technologies;740 Government;748 Terrain;750 Destroy Units;
  752 Money;755 Unit;760 City;765 King;768 Parameters;770 Save Scenario.
- EDITOR801 Scenario toggle;802..809 Advances,Cities,Effects,Improvements,Terrain,
  Tribes,Units,Events. PEDIA901/903/905/907/909/90b/90d open categories1/2/3/4/6/5/7
  (Advances,Improvements,Wonders,Units,Governments,Terrain,Concepts);9f0 About.

Mutable-label IDs: Build/Join50/51; Road/Rail52/53; Irrigate/target54/55;
Mine/target56/57; Home City/alternate58/59; Transform60; Farmland61 (decimal).
Find City uses flags0x800001 and objective label432. Key End-turn wait tests
saved game word006 mask0x0001 (lowest bit). Foreign city inspection uses city flag0x00400000;
scenario option8 allows inspection. Objectives use scenario option2.
Worker advice terrain IDs are Hills4,Plains1,Grassland2; Monarchy2. Its yields and
movement cost are signed bytes. Menu technology IDs are CombinedArms13,
Construction18,Radio66,Railroad67,BridgeBuilding7,Refrigeration70,Trade84.

### Native menu grammar and IDs

The low-level menu-description service consumes titles with `{...}` child lists.
Spaces between entries are skipped; `_` ends a leaf and `}` ends a list. There
is no general escaping or end-of-string recovery inside a required delimiter
scan. An initial `!` disables; inside a child list a following `*` checks the
entry. A `{` recursively creates a child menu. Exact text `@SEP` creates a
separator, clearing other flags and command ID. Child parsing breaks columns on
every 23rd visited entry; separator conversion can remove that column break.

Leaf IDs consume and increment a global counter even for failed appends. Top-level
creation captures counter **after** creating its native menu and, after parsing,
replaces it with captured counter+100, discarding recursive progress. Dynamic
item insertion consumes a fresh counter; explicit-ID insertion does not. Dynamic
insert/append introduces a column break when existing item count is nonzero and
divisible by22. Popup insertion with item=−1 inserts a new submenu; group=−1
appends it. Positions passed to native APIs subtract one with wrap. The command
search packs `((itemIndex+1)<<16) | (traversalCounter−1)` without masking the latter
before OR. Direct native item mutators use command IDs; linked-model mutators
resolve positions. Keep these namespaces distinct.

### Storage, failures and excluded behavior

Original menu nodes occupy28 bytes; arena size7100, allocation requests narrowed
to unsigned16. The six mutable Orders labels reserve at least60 characters plus
NUL. A replacement may allocate dynamically, but original excess copies are
excluded overwrites. Menu-presentation description holds256 bytes; the label
expansion temporary holds80. Low-level parser allocates strlen(description)
bytes, not plus one; zero-length or excessive writes are likewise excluded.

Timer manager allocation is144 bytes. Start/construct use slots1..16, but destroy
checks callback slots0..15, resetting nonzero ones, then releases the live native
window. Slot0 requires supplied/established incoming state; slot16 is omitted.
This bounded loop defect is observable. Wrapped indexed stores outside the
logical timer collection are **excluded**, even if the original address aliases
its native window or another field. Do not reproduce that overlap as a timer
feature. Failed manager allocation, null native receiver, failed query output,
missing menu delimiters and unbacked indices are explicit read/failure boundaries.

Input-busy is network offset7cc and absent from the18 mutation-guard scan.
Deferred request kinds Key/Menu are4/5; pending character kind3 and low byte are
separate. N02 owns replay. Timer capability starts1 and forced mode0; library
`timerdll.dll` exports TimerCallBack,SetTimerID,GetTimerID,GetTimerIndex,
ResetTimerNotified. These describe the original adapter; a different timer
provider may satisfy the same scheduling/message contract.

## Appendix C. Validation record

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

The documentation work used 35 hash-pinned independent source/header files and
five primary files at the revisions above. Reference checkouts remained clean.
Original `Menu.txt` SHA-256 is
`cf1ac5f4f202a0afa3654e3abdfd3a5176a22e1fc002a9446cf4f52b5fd3a545`.
Its sections were compared with the builder’s consumption order. All 1,024 bytes
of the executable’s initialized key table match the independent table.

Fourteen exact instruction probes checked key masking and Control/Shift order,
the two translations around key-down delivery, signed click versus unsigned
left-release coordinates, the inclusive availability bound, both Toggle Cheat
predicate sequences, timer decrement/callback/reload ordering, and the native
file chooser’s low-byte-equals-one test. The Toggle Cheat derivation was also
checked for six mode values and four full override values. These probes support
those specific facts; they do not execute the original GUI.

Thirty-nine unchanged whole bodies from `window_keys.cpp`, `menu_storage.cpp`,
`menu_presentation.cpp`, `message_pump.cpp`, `worker_advice.cpp` and `timers.cpp`
passed 69 finite assertions in optimized and AddressSanitizer/UndefinedBehavior-
Sanitizer builds. The native64 harness supplies key states, message queues,
clock samples, audio/network calls, menu presentation callbacks, terrain/city
queries and timer-platform responses. Its timer storage preserves the tested
32-bit field offsets and backed slot-zero input; native function pointers are
explicitly constrained to the low address range. This is a source fixture,
not an original x86 runtime comparison. LeakSanitizer was disabled because the
execution environment uses tracing; address and undefined-behavior checks ran.

Cases include captured key mapping across modifier callbacks; duplicate IDs,
hidden-state rebuilds and live menu links; media-only filtering and double idle
updates; input-discard ranges; clock wrap and zero/negative waits; timer start
failure, null registrations, finite/indefinite counts, callback extension and the
0..15 destruction loop; signed terrain yields, all-neighbor scans, post-popup
turn reads, and Mining followed by Road with captured terrain flags.

Full main-menu availability/execution, map-key/character parents, Find City,
focus/native event loops and save/load window continuations were source-reviewed,
not dynamically joined by these fixtures. F06’s new save-menu section and U04’s
View entry are caller-contract additions; N04’s join tail was corrected to
**start**, rather than resume, the countdown. Native menu parsing/materialization,
actual widgets and media, nested destruction, network replay and complete game
commands remain the integration cases in §11. No fixture depends on an excluded
out-of-bounds write or turns an unproduced read into an invented value.

Documentation checks verify internal links/anchors, pinned primary source links,
and the unique specification IDs. Working fixtures and
source evidence remain outside this documentation repository.

## Appendix D. Original presentation reference

This appendix retains the original presentation procedures removed from the main
specification. Geometry, pixel/sample output, native controls and cosmetic timing
are optional reference behavior under X01. Required semantic fragments were kept
in the numbered main sections and are linked where omitted here; this appendix
does not relax their simulation, information, file or protocol obligations.
Original section numbers and local § references identify the recovered procedure
family, whether its retained fragment is in the main section or here. Historical
native/pixel tests are reference validation, not baseline acceptance criteria.

### Original §3. Native event delivery

#### Translation and the current event window

Translate a native virtual key using its low byte as an index into Appendix B's
mutable key map. Capture the mapped value. If zero return zero without modifier
queries. Otherwise sample Control, then Shift, setting their logical modifiers
when down. Setting an already present modifier leaves it set. Mapping changes during those samples do not replace the captured
base key. No Alt modifier is added here.

The event dispatcher resolves its window receiver once on entry. Without one,
invoke the base-window dispatcher. Most recognized events publish that receiver
as the current event window before invoking callbacks. Callback registrations
are read at delivery; absent callbacks do nothing, except absent close-request
returns zero. A delivered callback has no implicit receiver argument. Setters
normally replace a callback and return its previous registration; see Appendix B
for the historically misnamed focus setter.

| Event | Delivery and continuation |
| --- | --- |
| Destroy | Clear the event attachment, then base dispatch. |
| Move | Publish current window, call move(signed x,signed y), then base dispatch. |
| Size | Publish current; if not minimized, call the effect-free resize hook, then size(full size-kind). Base dispatch follows. |
| Focus gained | Publish current, call focus, consume event. |
| Close request | Publish current; call close-request. Nonzero permits base dispatch; zero consumes the close. |
| Key down | Publish current; translate and call key-down; **translate again** and call key-press. Both callbacks can receive zero. |
| Key up | Publish current; translate once and call key-up. |
| Character | Publish current; deliver low character byte. |
| Command | Publish current; only if the low half of the source-control payload is zero, run menu routing below, which resolves its receiver again. |
| System minimize/restore | Publish current; exact matching commands call minimize/restore, then base dispatch. Other system commands still base-dispatch. |
| Menu selection | Publish current; deliver command selection, or absent when the close-menu signature matches. |
| Mouse move | Publish current; deliver signed coordinate halves. |
| Left down | Resolve presentation/backend; publish current **before** attached-child focus/raise. Set leftPressed=1, call left-down(signed coordinates). |
| Left up | Publish current; if leftPressed is exactly 1, clear it then call left-click(signed coordinates). Always call left-up afterward with **unsigned** coordinate halves. |
| Left double click | Publish current; deliver signed coordinates without synthesizing another press latch. |
| Right down | Resolve presentation/backend; focus/raise attached child **before** publishing current. Set rightPressed=1, call right-down(signed coordinates). |
| Right up | Publish current; if rightPressed exactly 1 clear it then right-click. Always right-up afterward. Both receive signed halves. |
| Child mouse notification | Only left/right down publish current and call the corresponding down callback with unsigned halves; they do not set these press latches. |
| Enter/exit move-size | Read presentation's backend even though the value is otherwise unused; publish current; call matching callback; base dispatch. |
| Communication notification | Publish current, deliver low payload half, return 1. |
| Wave completion | P10's completion handler receives device/buffer; return 0. |
| Special callback notification | For the matching payload deliver special callback(0), without publishing current. |

All consumed branches above return zero unless stated otherwise. Other messages
fall through to base dispatch. P01 receives the map mouse callbacks; overview,
city and report receivers use their own callbacks. In particular, map left-click
runs before the later left-release cleanup from the same native event.

Window horizontal/vertical scroll first captures its stored position. For line
back/forward and page back/forward, subtract/add one or the live page size,
set native scroll position, query its accepted position, publish current window,
store it, then notify scrolling. Thumb-position sets the decoded signed thumb,
publishes/stores it without querying an accepted position, then notifies.
Thumb-track only publishes current and notifies tracking. **This window-level
switch compares the full command word**: a thumb payload in its high half can
prevent either thumb case from matching. The control scrollbar path in §4 uses
only the low half instead.

Minimum/maximum size requests capture nonclient height then width, query logical
minimum and maximum sizes, and publish maximum width/height before minimum
width/height, adding the captured nonclient amounts with wrap.

Palette-change from the same native window delegates to base dispatch. Other
palette-change/query events acquire a drawing context; if a palette exists,
select/realize it and invalidate the window. Release the captured context, then
return whether realized-entry count was nonzero. Without a palette the original
result was not produced: supply the retained value or stop. A replacement's
explicit false-on-no-palette policy is a repair, not a recovered original value.

#### Base-window behavior

Base paint begins painting and resolves the presentation. When minimized, query
the window rectangle (unused result), draw the installed icon or the default
application icon, optionally realizing its palette, then return **without ending
paint**. Otherwise tile a nonzero-size background surface across the paint
rectangle using truncating division for first/last tile indices, inclusive through
`paintEdge/tileExtent`; or fill with the brush when no tile surface exists.
Then center the optional foreground surface with arithmetic halves, intersect it
with the paint area, and copy the intersection. Requery client size when converting
the intersection back to source coordinates. End paint. P03 owns surface copies;
failed native outputs need retained input rather than invented rectangles.

Other base behavior, followed by the platform default unless stated:

- Destroy detaches its menu, marks the backend destroyed, and, for an attached
  child, clears its parent's active-child reference only if it still equals this
  native window. It then returns the default result.
- Move and size release the cached drawing context and acquire a new one. Cache
  the destination backend before acquisition; callback replacement does not
  redirect that final context store. Size first invalidates a centered image.
- Focus gain/loss sends active-child nonclient activation/deactivation.
  Child activation first deactivates the parent's former child, activates this
  child, then stores this child into the captured parent backend.
- Z-order changes, unless explicitly suppressed, keep an attached window behind
  a different visible designated top child. Query visibility before publishing
  the live top-child value. Mouse down in attached windows focuses and raises them.
- Native keyboard-menu/mouse-menu system commands are consumed when the backend
  does not allow a menu. Exact native command values are in Appendix B.

After default nonclient hit-testing returns Client, optional custom resize
hit-testing reads outer and client rectangles. Coordinates here use **unsigned**
packed halves minus outer origin. Border is configured value or 5 when zero.
Use strict signed `<border` for left/top and `>extent−border` for right/bottom.
Near a top/bottom edge, widen corner detection to four border widths horizontally;
near a left/right edge, widen it vertically. Return the corresponding edge or
corner. If still Client and caption hit-testing is enabled, query outer rectangle
again and return Caption when caption height is zero or y is strictly below it.
If still Client, apply the live cursor. Failed geometry queries need already
established/incoming outputs; the reconstruction's retain-default-result policy
is separate. No clamping of packed negative coordinates is implicit.

### Original §4. Control focus and parent dispatch

The extended control dispatcher runs before §3 and resolves its receiver once.
Without one it immediately delegates. Destroy clears the control list and its
native attachment, then delegates. Size sends the same event to the first
special child control if present, then delegates.

For key down, first sample asynchronous Shift and test its entire high byte:

1. Tab: if traversal is enabled, traverse forward without Shift, backward with
   Shift. Disabled traversal does not fall through to an accelerator.
2. Unshifted Enter/Escape: activate the registered default/cancel button if
   present. No accelerator fallback when the button is absent.
3. Other keys: translate the key and invoke accelerator search.
4. Query whether the original native window still exists. If false return zero;
   otherwise delegate the **same key event** to §3, even after control activation.

Command notifications for list selection/double click are synchronously forwarded
to the source control's notification event and consumed. Edit-change/combo edit
notifications resolve the source's callback object, invoke the appropriate
change/selection callback with its stored ID, and consume the event. Other
commands delegate. Native horizontal/vertical scrolling is sent to the source
control when neither native-window scrollbar flag is set; otherwise it delegates
to §3. Control behavior after these notifications belongs to P04.

An attached control window also keeps itself below the first special child of
its parent during z-order changes. This branch calls the native default directly.
During moving, when that child is visible, compute child top minus system caption
height. If this is no greater than the proposed top, replace top with it and
bottom with **new top plus proposed width**, not height. Preserve this bounded
geometry defect. Query failure requires supplied retained child geometry.

#### Focus traversal

Forward walks native next sibling, wrapping to first; backward walks previous,
wrapping to last. Each step first verifies its current native window. When no
starting window is supplied, query focus and keep it only if it is a child of
the receiver. With no starting child and no control-list head, finish without
focus change. Otherwise start at first native child, or last for backward.
With an existing starting child, take one step before the search.

Loop from this candidate, remembering the first:

- An invalid native window ends. An invisible window is skipped.
- A group-option window enters a consecutive-group search. Group identity is
  the owner recorded in native option state; class matching uses classic uppercase
  against the custom-control class. If a member's ordinal equals the owner's
  selected ordinal, focus it. This branch does not add a disabled-state test.
  If traversal leaves the group, recursively traverse from the preceding member.
- Other candidates must match a linked control. Enabled buttons and checkboxes,
  edit fields, system lists and comboboxes can receive focus. Other types skip.
- Stop the ordinary loop on returning to its first candidate. Group recursion
  has no independent cap; a safe profile may reject cyclic/unselectable setups.

Initial control selection chooses the first preferred linked control, otherwise
the head. If traversal is enabled, take the predecessor of that control's native
window (the group's first option for a group), then traverse forward. Focus-and-
select focuses/raises first and performs selection using the later live list.
Failed native class-name queries require retained bytes; treating them as
non-group is a separate safe policy.

#### Accelerators and scrollbars

Accelerator search takes the low key byte, lowercases it, and scans linked
controls. For a nonnegative accelerator index with matching character, activate
an enabled button or checkbox and return 1. When the index is negative and the
control is a radio group, scan its rows in order for a nonnegative matching
accelerator, activate that row and return 1 after reading the node's next link.
That row branch adds no enabled-state test here. No match returns zero. P04 owns
label marker extraction and activation side effects.

A control scrollbar stores a position and page size. Range setting asks the
platform to set `[minimum,maximum]` and then sets position to minimum. Page-size
setting queries the range and replaces page size only if signed wrapping
`maximum−minimum >= requestedPage`; negative requests are not rejected here.
Programmatic position setting first stores the request, then asks the platform
and stores its accepted result, without a change callback.

Native control scrolling reads stored position even for ignored events. Decode
only the low command half; thumb coordinates are signed high halves. Thumb
position sets native position, publishes the parent as current event window,
stores the requested thumb and invokes change; thumb track only publishes parent
and invokes tracking. Line up/down first checks asynchronous left-button state:
if not active, send a synthetic left-release using control left/top as the two
message arguments and return. Otherwise change by one. Page commands change by
page size. These four commands set native position, query accepted position,
publish parent, store and notify. Unknown commands do nothing after the initial
position read. Keep the initially resolved control across native callbacks.

### Original §5. Menu model, construction and presentation

A menu contains ordered groups, each with ordered items. Records have an ID,
text, hidden/disabled/checked state and links; each group counts presented items.
A null item text is a separator. IDs need not be unique. Group lookup finds the
first matching group. Item lookup finds the first matching item **within the
last group containing a match**. Disable-group walks that group's items but uses
this global item lookup for each ID, so duplicates can affect another group.

Append preserves order, clears new flags/count, and replaces every `|` in copied
text with a tab. It marks the menu unmaterialized. A missing parent group makes
item append return absent. Replacing text overwrites the existing text, normalizes
pipes, and updates the native item immediately only when materialized and visible.
Its original storage limit belongs in Appendix B; an expansion beyond it is an
excluded overwrite, not an implicit reallocation rule.

Changing group/item hidden state marks the menu unmaterialized only when the
Boolean state changes. Changing disabled/checked state stores the Boolean and
updates native state only when it changed, the menu is materialized and the item
is visible. Missing IDs do nothing. Item removal first computes positions and
removes the native item, then changes **only its predecessor's next link**. It
does not repair group head or successor's previous link, reclaim storage, or
special-case a null predecessor. Keep valid partial effects; missing predecessor
backing is a stop, not permission to overwrite unrelated state.

#### Loading the main menu

Reset menu storage, then read `MENU` sections in this exact order through F03:
`GAME`, `KINGDOM`, `VIEW`, `ORDERS`, `ADVISORS`, `WORLD`, `CHEAT`, `EDITOR`, `PEDIA`.
Only the first lookup supplies the filename; subsequent lookups reuse it. Each
section's first consumed line names the group. Subsequent lines supply the item
texts in Appendix B's fixed order. Separators are inserted by the algorithm and
consume no line. The loader does not parse shortcut suffixes into key bindings;
keyboard dispatch is specified independently in §8.

Immediately after the View section, set the map-grid check from the live option.
After Cheat, disable its items unless cheat mode and game mode exactly zero;
check Toggle Cheat under that same condition; then independently disable Toggle
Cheat only when game mode is nonzero and the retained menu-mode override is zero.
After Editor, check Scenario Flag when cheat is on, mode zero, scenario flag set,
and search/install directory strings differ. Independently disable its group
unless all four hold, then disable Scenario Flag only for nonzero game mode.
These gates are read again at each operation.

Only after successfully reaching the end of Pedia, hide Paradrop, Transform,
Airlift, Airbase, Fortress and Automate Settler. A failed section lookup jumps
directly to finalization, retaining all earlier groups, items and state. Always
close the rules file, present the accumulated menu on the main window, then bind
this menu and the command callback. Binding occurs after presentation callbacks.
Rebuilding removes the previously inserted hotseat exit item from the linked
model; session setup owns any later insertion.

#### Materialization and command resolution

Group/item positions are one-based among visible entries, including separators.
Position-to-ID lookup returns absent when no match; target-to-position counts
visible predecessors and can return the end position if a target is missing.

When unmaterialized, presentation first concatenates visible group titles with
an empty submenu marker. In this first pass, the native group position increments
for **every** group, including hidden ones. Without a new window attachment,
remove native item positions 30 down to 1 for each group and reset its visible
count. With a window, create a new menu from the description instead.

The second pass counts only visible groups. For a new window, remove the first
placeholder item of each visible group. Insert every visible linked item in
order, increment the group's visible count, then apply disabled and checked
states through ID lookup. Reload next links after callbacks. Mark materialized,
attach the new window if supplied, then redraw the currently bound menu window
if any. Already-materialized presentation only performs that last redraw; it
does not attach a newly supplied window. New-window presentation does not itself
reset existing visible counts before incrementing them.

Attaching a window publishes bound menu/window, sets the native menu, resizes its
client area, stores the menu association and installs positional command delivery.
The resize uses post-attachment client height plus native menu height plus one,
and current client width; add nonclient width/height to get outer size. Preserve
outer position, converting to parent coordinates for an attached child. Failed
SetMenu does not skip geometry. Unproduced geometry requires retained input.

Native command routing resolves the event receiver again. If a direct-command
callback exists, call it and finish regardless of its result. Otherwise reset a
shared traversal counter, get the native menu, and search recursively:

1. Increment the counter at **every menu entry**; never decrement on return.
2. Capture native item count. Visit positions in order. A submenu marker recurses.
   A leaf queries its ID a second time before comparing with the requested ID.
3. On a leaf match form `(oneBasedItemPosition, traversalCount−1)` in the native
   packed adapter. Stop traversal through its original wrapping loop-index update.
4. With a menu, invoke the positional callback even when search found nothing,
   passing zero/zero then. Without a menu, do not call it.

Positional delivery maps visible group position to group ID, then visible item
position in that group to item ID, and invokes the currently bound execute
callback. It does not check disabled/hidden flags again. The traversal counter
is visitation count rather than nesting depth; nested menus therefore preserve
that unusual group numbering. Native ID generation, parser grammar and packing
are validation-adapter details in Appendix B.

### Original multiplayer-window placement

Each multiplayer window chooses base width/height 520×335 for layout mode zero,
otherwise 800×460, with separate live mode reads for width, height and font.
Caption height is `2*outerPadding + 2*innerPadding + cachedFontHeight`; final
width adds `2*(horizontalPadding+spacing)+6`, final height adds
`2*outerPadding+2*captionHeight+14`. Set x to arithmetic half main-client width
minus arithmetic half window width; capture x; query main-client height and set
y similarly. Write the saved rectangle using captured x/y and dimensions, then
move using the live saved-rectangle reference. P01 owns default map/overview
rectangles; P02/P06 own status/city layouts.

### Original §11. Conformance cases and limits

| Case | Required distinction |
| --- | --- |
| Key-map callback mutation | Control is sampled before Shift; the base key is captured, but key-down and key-press translate independently. |
| Left release at packed negative coordinates | Click gets signed coordinates; subsequent left-up gets unsigned halves. |
| Default button on key down | Control activation can be followed by lower event dispatch of the same key. |
| Duplicate menu IDs | First match in last matching group; disabling one group can resolve another group's item. |
| Hidden menu group | Rebuild removal pass counts it; insertion pass skips it. |
| Toggle Cheat refresh | Both builder and updater leave it enabled for mode zero, including override zero; group disabling runs first. |
| Active slot equals total | Availability can read it; directional movement cannot. |
| Reentrant command | Transport query/log precede deferred writes; a character attaches to the one pending key. |
| Modal Escape | Nonzero-mode early city close precedes input-busy and transport tests. |
| Upper/lower P | Lowercase can paradrop; uppercase can pillage; a settler otherwise cleans pollution. |
| Deferred selection | N02 clears request before invoking this document's controller, with captured pending character metadata. |
| Finite timer callback | Count decrements before callback; live post-callback zero controls reset. |
| Media idle | A terminal drain iteration can invoke media update twice. |
| Worker hint | Mining does not prevent a later Road hint in the same call. |
| Corrupt text/timer index | Scalar wrap is observed; the resulting out-of-bounds store is excluded. |

The algorithms do not make menu availability an authorization mechanism for
internal actions. A cleanroom implementation should compare ordered event/action
logs and intermediate state, as well as final selection. Actual native menus,
fonts/widgets, full loaded-game controllers, callback-driven destruction,
network/deferred delivery and timer/media integration remain separate runtime
validation obligations. Appendix C records the bounded checks performed here.

### Original §2. Message pumps, polling and waits

These operations have different delivery sets; they are not interchangeable:

| Operation | Ordered behavior |
| --- | --- |
| One media message | Remove the first queued wave-output completion message. If absent return 0; otherwise translate, dispatch, return 1. |
| Media pump | Repeat the previous operation until 0, then update the live media context if media-active is nonzero. This does not drain ordinary input. |
| One ordinary message or idle | Remove the first queued message of any kind. If present translate/dispatch and return 1. Otherwise run the media pump, independently reread media-active and update media again if nonzero, then return 0. |
| Drain and flush | Repeat one ordinary message or idle until 0; then flush native drawing. The terminal idle path can update media twice. |
| Paint flush | Remove/translate/dispatch paint messages until none; then flush native drawing. |
| Discard queued input | Remove all client mouse messages, then all key messages, without translation or dispatch. Return 0. |
| Discard all pending messages | Remove messages of every kind without dispatch, including events that ordinary pumping would handle. |
| Prepare effect presentation | Unless simultaneous-movement/creation gate is set, discard input; then media pump, paint flush, media pump. |

Message filtering/removal is the supplied platform's operation, not a snapshot
of the initial queue. Messages added by a callback are eligible for later
iterations. The ignored residual return of an active media update is not a
known Boolean; a consumer needing it must supply that result. Appendix B gives
the exact native ranges.

The system-key-pending query drains **all** messages. It consumes system
key-down, key-up and system-character messages without dispatch and remembers
that one occurred. All other messages are translated and dispatched. Return 1
if any consumed system-key event occurred, otherwise 0. Shift/control queries
instead sample the corresponding synchronous key state and test its down bit.
Left/right-button-active queries sample asynchronous state and test the entire
high byte, not merely the usual down bit.

#### Mouse-release polling

Inputs choose left/right release, a particular window or all windows, whether to
remove messages, and whether to wait. Keep separate client and nonclient message
records. Repeatedly peek the client mouse range first. Only when it fails peek
the nonclient release range. Break the inner loop when both peeks fail.

After either succeeds, test **client record is chosen-button release OR nonclient
record is chosen-button release**, left operand first. A successful client peek
does not initialize the nonclient record, or vice versa. Earlier/incoming contents
of the other record remain relevant. On a match set result=1 and wait=0, but
continue the inner drain. Repeat the outer loop while wait remains nonzero;
return result, initially zero. Without removal, the same message can be observed
forever, even after a match. A safe polling profile may reject that combination;
it must not describe a bounded timeout as original behavior.

#### Waits

`waitWithMessages(ticks)` computes signed duration=`wrap(ticks*50)/3`, captures
native millisecond time, then executes at least once: media pump; capture time;
if live game mode is at least 3, N02 poll `(budget=1,waitMode=0)`. Continue while
signed wrapping elapsed time is below duration. The time comparison uses the
sample before the network callback. Negative or zero duration still runs once.

`waitPlatformTicks(ticks)` captures game ticks, then while signed current ticks
is less than signed wrapping `start+ticks`, runs only the media pump. It can run
zero times. Game ticks are unsigned wrapping `milliseconds*6`, then unsigned
division by 100. This absolute-deadline comparison differs from elapsed-time
comparison around wrap. P04's eight-tick button activation uses this wait.

### Original §10. Timer scheduling and native service boundaries

The timer manager has sixteen ordinarily allocatable slots, identified 1..16.
Each stores a callback and signed remaining count. Initialization clears these
slots before creating its native timer window. The separate slot-zero storage is
not initialized by that loop; its historical destructor access is in Appendix B.

Start scans ascending for the first zero callback. No free slot returns zero.
Capture that slot and start its native timer first. Failure returns zero without
publishing callback/count. On success publish the requested callback and count
into the captured slot, overwriting any mutations made during native start, then
return its ID. A null requested callback does not reserve the slot logically.
The global wrapper lazily allocates/constructs the manager and publishes it after
construction; failure does not establish a usable manager.

Dispatch a backed timer slot as follows:

1. Zero callback returns. Zero count also returns, leaving the callback installed.
2. Positive count decrements with wrap; negative count remains unchanged.
3. Capture the resulting count, reload callback, call `(slot,count)`; game
   callbacks that take no arguments ignore both.
4. Reread count. If zero, reset the slot. A callback can extend, clear or replace
   itself before this test; no snapshot-based automatic cancellation is added.

Reset clears callback **before** cancellation, then reads the live timer window
and cancels. It leaves remaining count untouched. Reset with no global manager
logs and returns. Native timer and alternate completion-notification messages
both resolve the native-window manager, then dispatch; the alternate notification
also invokes the live completion acknowledgement afterward, even when no slot
callback ran. Other messages or no receiver go to the native default.

Timer mode uses two independent Boolean values: platform capability and temporary
forced mode. Equal truth values choose native window timers; unequal values
choose periodic multimedia timers. Native start passes the requested interval
and slot; success is any nonzero platform result. A failed native or multimedia
start reports the timer-set failure before returning zero, if that error handler
returns. No callback/count publication follows that failure. Multimedia start requests
resolution 5 and periodic delivery with the native window as context; on success
associate the captured slot with returned event ID. Cancellation uses the
corresponding native route; multimedia cancellation looks up event ID, kills it,
then reloads the assignment service and stores event ID zero. Later mode changes
are not normalized to the mode originally used for that slot.

Factory behavior: try the timer library. Without it, create the timer window and,
only on successful creation/receiver attachment, clear capability mode. With the
library, resolve callback, assign, lookup, index and completion services in order.
A missing service reports the dynamic-link error, frees the live library and
returns no window. Otherwise create the window; failure reports creation error,
frees library and returns. Success requests period 5, attaches manager, then
tries a 100-ms periodic event: kill it if successful; otherwise clear capability
mode. Earlier values are not restored. Release of a nonnull timer window ends
period 5, destroys the captured window, clears the global event callback, then
frees the live library. Null window skips those effects.

Map setup temporarily sets forced mode, starts indefinite map-phase/status timers
at 150/500 milliseconds, then clears forced mode. Release sets it again, resets
the two live timer IDs in order, then clears it. P01/P02 own their callback
effects; P01 owns hold-timer registration, and N03 owns countdown registration.
No scheduling fairness, fixed frame rate or callback suppression during modal
pumping is inferred beyond the supplied native message stream.

Window creation/destruction, focus, capture, invalidation, menu display and timer
registration are synchronous service boundaries where the platform may deliver
messages. Preserve each caller's ordering and the receiver it captured. Other
than the bounded dispatch behavior above, native class-registration/allocator/
codec internals are outside this algorithm's required representation. Native
failures remain explicit inputs; suppressing their unproduced reads is a declared
safe policy. Tracking a popup converts caller x/y from client to screen using an
initialized point, then tracks on the **live** backend window; conversion failure
retains whatever point was supplied/written, rather than replacing it with zero.

### Original post-load window placement

When saved main-window state is zero, arrange windows (§7), then restore/move,
compose title and update the primary map. For enabled secondary maps in ascending
order, initialize runtime, move to their saved rectangle, compose title and update.
When saved main-window state is nonzero instead: maximize main; restore/move/hide
city; restore overview, clear suppression, move/update; restore status, clear
suppression, move/update; restore/move/title/update primary; initialize/move/title/
update enabled secondaries; update overview once more.

### Original command input and native state

Nominal inputs are initialized game/window/menu/control state, backed acyclic
collections, complete resources, valid unit/city identities, fitting strings,
positive native dimensions, valid timer slots and ordinary platform responses.
Native message sequences, key-state samples, sibling/focus order, menu handles,
geometry, clock samples, timer results and synchronous callbacks are inputs.
Native menu/widget internals are service contracts; a replacement need not
implement Win32 itself. It must deliver equivalent effects at the stated points.

Logical state includes the game mode, local observer, active unit, movement mode,
map cursor and per-view cursor/zoom; input-enabled, input-busy, city-processing
and network-operation guards; the one deferred input request and optional
character; ordered menu groups/items and presentation state; current event
window, per-window press latches and callbacks; control order, selection,
default buttons and focus; timer callbacks/counts and media activity.

### Original arrange-windows sequence

Arrange Windows executes: maximize main; calculate city default rectangle,
restore/move city and hide it; calculate/restore overview, clear its redraw
suppression, move/update; calculate/restore main status, clear suppression,
move/update; center the two multiplayer windows in order; close/reset enabled
secondary maps 1..7, clearing each enabled state before reset; calculate primary
map default, restore/move/update it; prepare effect presentation (§2).

### Original §8. Map keyboard and character controllers

#### Key-press entry and routing

Use the full translated key, including modifiers; do not strip them before
matching. If input-enabled is zero return. For nonzero game mode with city
processing and unmodified Escape or Enter, close an open city and return **before
input-busy or transport tests**. Next, nonzero game mode plus input-busy returns.
Query transport and the ordered mutation guards from §7. If blocked, log, store
deferred kind Key and the full key, clear pending-character kind, and return.

Set input-busy=1. If city-processing is active, Escape/Enter closes an open city;
other keys show the modal notice. Clear busy and return. Zero or Shift-only key
also clears busy and returns. Otherwise clear pending map gesture and reset map
special cursors, in that order.

If city is open and Enter-closes-city is enabled, unmodified Down/Right or keypad
2/6 invokes **previous city**; Up/Left or keypad 8/4 invokes **next city**. Clear
busy and return after those actions. Remaining keys perform the table below.

| Key | Action and extra gate |
| --- | --- |
| Escape | Hide city only if open. |
| F1,F2,F3,F4,F5,F6 | City, Defense, Foreign, Attitude, Trade, Science report(local observer). |
| F7,F8 | Wonders and Top Five report(local observer). |
| F9 | Score(local observer), then live score-lock permits Fame(fresh observer) and History. |
| F11 | Demographics(local observer). |
| F12 | Spaceship chooser only if custom rules permit spaceships. |
| Ctrl+C | Chat(0,4), only mode>2. |
| Ctrl+D,E | Casualty(local observer), City Report Options. |
| Ctrl+F | Scenario toggle only mode=0. |
| Ctrl+G | Grid toggle/check/redraw/remember/save sequence from §7. |
| Ctrl+H | Only mode=1 and nonzero REALLYQUIT result: exit, then clear game-active. Input-busy stays set through the prompt and children. |
| Ctrl+J | Join only if join-allowed and mode=1. |
| Ctrl+K | Toggle cheat only if mode=0 or menu-mode override. |
| Ctrl+L | Current-game load only mode=0. |
| Ctrl+N,O,P,Q,R | End Player Turn, Game Options, Graphic Options, Quit(0), Retire(1). |
| Ctrl+S | Save(0) only mode!=2. |
| Ctrl+T | Change timer only nonzero mode, mode!=2 and server. |
| Ctrl+W | Password(local observer), without an added mode gate. |
| Ctrl+X,Z | Primary zoom minimum/maximum through P01. |
| Ctrl+Y | Multiplayer Options only nonzero mode. |
| Ctrl+Shift+T | Council with preview=1 only if cheat, mode=0 and a fresh asynchronous Shift high-byte sample is nonzero; otherwise preview=0. |

After that action returns, **reread** cheat and mode. If cheat and mode zero,
apply this separate table:

| Key | S03/S04 action |
| --- | --- |
| Shift+F1,F2,F3,F4,F5,F6,F7 | Create unit, reveal map, set human, set year, kill civilization, advance research, force government. |
| Shift+F8 | Set retained terrain selection absent, then change terrain. |
| Shift+F9 | Change money. |
| Ctrl+Shift+1..8 | Advances, Cities, Effects, Improvements, Terrain, Tribes, Units, Events editor. |
| Ctrl+Shift+C,D,K,P,S,U | Edit city, destroy units, edit king, scenario parameters, save scenario, edit unit. Keyboard Destroy does not append the menu's focusActive call. |
| Ctrl+Shift+F6,F8 | Edit technologies, change terrain. The latter does not first set the terrain-selection sentinel. |

Finally derive a direction only for unmodified arrows/navigation or keypad:
PageUp/9→NE, Right/6→E, PageDown/3→SE, Down/2→S, End/1→SW, Left/4→W,
Home/7→NW, Up/8→N. Before testing whether a direction exists, require active slot
nonnegative, strictly below live total and live identity nonzero. Thus even
report/unknown keys can reach an active-unit identity read after their action.
With a direction, movement mode exactly one calls U02 move(active,direction,3);
otherwise move the view cursor. Clear input-busy on normal return.

Moving the view cursor captures horizontal cursor+direction delta and wraps x;
then reads vertical delta and current y, attempts P01 selection, and on a
nonzero result ensures the captured coordinates are visible in primary map.
Selection callbacks do not change those final arguments. Extra direction indices
are not generated by the key mapping; unsupported backing follows §1.

#### Character entry and case-sensitive shortcuts

Take the low input byte. Require input enabled; nonzero mode plus input-busy
returns. If the pending deferred request is Key, log, store pending-character
kind Character and this byte, and return. It attaches to that one pending key;
there is no separate character FIFO or transport query here.

Set input-busy. During city processing, Enter, Escape and linefeed are consumed;
other characters show the modal notice. Clear busy and return. Otherwise an open
city with Enter-closes-city routes these first, case-insensitively:
Enter/E/X close; A/C change production; B buy; H happiness; I central information;
M support map; R rename; V city view. Pass argument zero where the city control
accepts an ID, ignore its result, clear busy and return.

For other characters, clear the pending map gesture. Capture original byte. If
primary special cursor is active, reset cursors; original lowercase `p` then
clears busy and returns. Classify the byte; if alphabetic, uppercase it using the
live classic CRT and retain the result's low byte. Match the **original** byte
first:

| Original byte | Action |
| --- | --- |
| C,D | Find City; Disband. |
| H | Throne(local observer), unless mode=1. |
| R | Revolution. |
| S | Save(0), unless mode=2. |
| T | Tax Rate(local observer). |
| X,Z | Medium zoom; standard zoom with current cursor center. |

A matched original shortcut does not enter either character-mode dispatcher,
even if its mode condition prevents the action. For unmatched originals, use
the normalized character: A activates a stack unit; T temporarily shows hidden
terrain; V toggles View/Move via U04, choosing View when current mode exactly one,
otherwise focusActive(1); X/Z zoom out/in one level. These actions **continue**
to the mode dispatcher afterward. If cheat, mode zero and normalized Y, toggle
score-lock/reveal and redraw all views for live observer with 1. Finally dispatch
View when movement mode is zero, Active-unit otherwise; clear input-busy afterward.

View dispatch accepts Enter or Space: Enter during end-turn waiting clears the
waiting latch and returns. Otherwise locate the city at captured cursor. Open it
when owned by the local observer, score-lock is set, or the city permits foreign
inspection. Otherwise show its limited map information only with Trade or
scenario rules allowing inspection. Normalized C centers on the cursor.

Active-unit dispatch captures the shared active slot before any gate. In network
mode it reads signed owner **before** testing the Enter exception; a foreign
owner suppresses every character except Enter. Enter uses the same city-open
logic, without the View-mode end-turn latch shortcut. Other normalized commands:

| Character | Action |
| --- | --- |
| Space,W | Skip unit, Wait. |
| B | Build/Join City. |
| C | Center primary on cached unit's current coordinates. |
| E,I,M,O,R | Airbase, Irrigation, Mine, Transform, Road work. |
| F | Fortress work for Settle role, otherwise Fortify. |
| G,H,K,L | Goto City, Home, Automate Settler, Airlift. |
| S,U | Sleep, Unload. |
| P | Original lowercase p plus paradrop flag: Paradrop. Otherwise Settle role: Clean Pollution. Otherwise original uppercase P: Pillage. No other branch acts. |
| Q | [P09 throne upgrade](P09-council-and-throne-room.md#7-throne-room-progress-and-admission) for the local human, only cheat and mode=0. |

The table adds no universal unit-range, role or technology prevalidation. Child
commands own those checks. Supported backing is required before every reached
unit/rule read, even when a later condition would return.
