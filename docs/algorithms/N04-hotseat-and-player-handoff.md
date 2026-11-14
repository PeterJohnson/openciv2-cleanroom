# N04: Hotseat and player handoff

**Required profile:** [X01 fresh-game isolation and bug repairs](X01-classic-compatibility-contract.md#fresh-game-isolation-and-supported-bug-repairs)
and [behaviorally significant ordering](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
override conflicting procedures and examples below. Retention applies within a
game or to explicit imported inputs, not across new games. Confirmed original-bug
stack/backing/stop cases are optional diagnostics; use documented safe repairs.
Only real effects and callbacks impose ordering; test adapters cannot invent them.
Unspecified safe continuations remain explicit gaps, not required arbitrary bytes.

Target: Civilization II Multiplayer Gold Edition 5.4.0f,
patch 3. This document specifies local multiplayer setup, participant selection,
concealed player handoff, the hotseat turn loop, joining a running local game,
and hotseat quit/save transitions. It separately records dormant e-mail paths;
their presence does not establish playable PBEM support in this target.

[F04](F04-turn-sequencing.md) owns global advancement, civilization upkeep,
start notices, movement orchestration, retirement and game-over handling.
[F05](F05-game-setup-and-starting-positions.md) owns map-selection/setup children,
new-world initialization and acceleration;
[W02](W02-map-generation.md) owns generation. [N01](N01-multiplayer-session-and-lobby.md)
owns the shared multiplayer option/tribe wizard, password interactions and outer
session setup/teardown. [N03](N03-multiplayer-turn-sequencing.md) owns timer
selection, start/stop/resume and the turn-popup timeout callback.
[F06](F06-save-load-and-file-formats.md) owns serialization and load/save mutations.
P01/P02 own map views and drawing, P04 modal/dialog services, P05 command/native
input delivery. They remain live children, not assumed-success operations.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

Player handoff, concealment, password decisions and save/resume publication
retain their dependencies. Independent UI preparation must not expose the next
player's information or change a command's captured actor.

## Compatibility policy

Hotseat remains required under X01's [presentation policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy).
The handoff UI, window arrangement and password controls may change; effective
observer/human sets, concealment, password acceptance/cancellation, turn/resume
state and published edits must agree. Preserve saved per-player view data through
F06 even when the new UI translates it to different windows. Harmless local UI
failures may use safe cleanup without revealing the next player's information.
The optional-transport policy does not make hotseat optional.

## 1. State and input domain

Hotseat is mode 1, distinct from single player 0, the dormant e-mail mode 2 and
network modes above 2. The selected local turn loop also serves modes 0 and 2;
its mode tests are live after callbacks. This document describes mode-1 behavior
and the cross-mode boundaries that affect it. F04 owns mode 0; N03's four network
loops are separate entry points.

Distinguish:

- `participants`: the retained hotseat participant set.
- `humans`: the active simulation human set. It can temporarily contain one
  civilization, no civilizations, or the whole participant set.
- `alive`: civilizations presently alive.
- `local`: the signed full-width input/view actor. It normally names 0–7;
  the loop deliberately publishes −1 during an AI phase.
- `playerCiv`, `turnCiv`, `networkTurnCiv`: stored player, simulation actor and
  multiplayer turn-publication actor. These are not interchangeable with local.
- `resume`, `resumeNew`: runtime partial-turn state, separate from the saved pair.
- `cursor`, main-view center, eight saved cursor pairs and eight introduction
  flags. An introduction flag of **exactly 1** selects restoration at handoff.
- `viewsHidden`: one local-loop variable retained across civilizations and
  outer iterations; initially zero.
- `savedActor`: a retained private byte written by the AI-prefix operation.
  The original does **not** initialize it at loop entry.
- `active`, pending outcome, human-movement activity, end-turn request, casualty
  counters, autoplay, timer state and the first-email-human latch.

Nominal setup resources provide the five installed hotseat actions, two leader
genders, four city styles, player counts fitting the available civilizations,
valid option mappings and terminated strings. Participant civilizations normally
use IDs 1–7, and all saved-view accesses have valid backing. The application
initializes the maximum player count to 7 and applies N01's configuration rules.

Use [F01](F01-determinism-and-arithmetic.md) arithmetic: reference expressions have
its specified cyclic results and signed division truncates toward zero.
Civilization membership is a set of identities 0…7, not a required byte. Menu
choices and coordinates use their logical domains; preserve the specified
conversions for admitted extra values and persistent encodings. A displayed
or returned scalar is not automatically clamped merely because the normal UI
produces a smaller range. Loop civilization comparisons use the unsigned resume
byte. Local actor publication uses the full scalar; only specified captures,
file fields and masks narrow it.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

Calls can change mode, actor, roster, options, counts, dialog results or views.
Re-read at the stated boundary. A selected branch continues across its children;
a mode change does not retroactively undo entry-time mask swapping. There is no
general rollback on cancellation or child failure.

A stricter input profile can reject malformed resources, unsupported indices or
unproduced values. For accepted states, retain safe arithmetic and ordering.
[Out-of-bounds writes are excluded bugs](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
including invalid cursor/mapping/name writes. Stop or omit unsafe operations
without assigning their corruption to adjacent logical fields. Invalid reads
require established backing, supplied retained input or a bounded stop; they
are a separate question from the write exclusion. Appendix A separates the
original saved-actor and casualty-read boundaries from reconstruction repairs.

## 2. Hotseat startup

The shared multiplayer-type chooser in N01 routes its local option here.
Construct a prompt and palette. Clear all eight introduction flags, set the turn
time limit to zero and set mode 1. Do not clear every saved cursor or password
here; their producers are separate session/setup children.

Each outer menu attempt:

1. Load the setup background into the shared backdrop, attach it to the main
   frame, set its background color, and flush painting. Set dialog-position
   state 3 and show the side picture with arguments (−1,0).
2. Load HOTSEAT1, select the remembered hotseat action, copy home into the
   scenario search directory, position the prompt and show it.
3. Play the menu-accept sound, then close the side picture, even on cancellation.
   A negative result returns 1. Otherwise remember its signed-word value.
4. For actions 0–2, change working directory to home and load general rules with
   argument 1. Clear events-loaded state. Dispatch the full modal action value.

### World and game choices

| Action | Ordered continuation |
| --- | --- |
| 0: new random world | Choose map size; failure restarts the outer menu. Set map source 0; choose the five random world settings below; run the shared game-option wizard with `(source, mode=1)`. Failure restarts the outer menu. |
| 1: premade world | Run F05's premade-map dialog; failure restarts. Set source 1, or 2 when the loaded flat-world flag is nonzero. Make the same five world-setting draws, then run the shared options with mode 1; failure restarts. |
| 2: custom world | Choose map size; set source 0; run the custom-world chooser; run the shared options with mode 1. A failure at either chooser restarts the outer menu. No five random-setting draws are added. |
| 3: scenario | Use the scenario continuation below. |
| 4: saved game | Use the saved-game continuation below. |

Random settings consume five primary-law choices in this order: land mass is
`draw % 3 - 1`; land form starts at 0, and a second draw divisible by 3 replaces
it with the current land mass; temperature, climate and age each use a separate
`draw % 3 - 1`. All five draws occur on the premade-world branch too. Child
chooser/load draws are separate and precede or follow these according to their
call sites. Cancellation retains published choices; random context continuation
follows the implementation's declared policy.

The option wizard is [N01's shared wizard](N01-multiplayer-session-and-lobby.md#multiplayer-game-option-wizard),
called with mode 1. Its local-mode disabled controls, timer chooser and
backtracking still apply. Do not substitute F05's single-player wizard.

For scenario action 3: clear the suppress-wonder-movies indicator, run S01's
scenario chooser with mode 1, and on failure restore the scenario directory to
home and retry. On success, clear Wonder Movies if that indicator is nonzero,
set mode 1, choose a player count, then run S01's scenario human-civilization
selector with that count. Zero count or selector failure restores the directory
and retries. Success adds the local Change Player menu item, saves preferences,
redraws the setup background and returns 0.

For saved-game action 4: clear the same indicator and invoke F06's load dialog
with `(0,1,1)`. On failed load, clear Wonder Movies if requested, then retry.
A successful load whose live mode is not 1 shows HOTSEATNOT, conditionally clears
Wonder Movies, then retries. A successful mode-1 load adds Change Player, saves
preferences, redraws the background and returns 0. That successful branch does
not add the other branches' explicit suppress-movies assignment.

### Participant choices and world creation

After actions 0–2 complete their options, clear the claimed-civilization word
once, before the player-count retry loop:

```text
repeat player-count attempt:
    alive = 0; humans = 0
    count = choosePlayerCount()
    if count == 0: restart outer hotseat menu
    restore default leader/name references
    for player = 0; player < count; player++:
        while chooseTribe(player, mode=1, inGame=0) != 0:
            player = s32(player - 1)
            if player < 0: restart player-count attempt
            # Retry that preceding player immediately, without another count test.
    chooseOpponents(count)
    save preferences
    load advances(1), units(1), improvements(1), in that order
    play menu-end sound
    generateMap(mapSource)
    initializeNewGame()
    if acceleration != 0: accelerate(s32(acceleration - 1))
    mode = 1
    if resume != 0:
        playerCiv = resume
        local = signed interpretation of the stored player byte
    add Change Player menu item
    return 0
```

The [shared tribe chooser](N01-multiplayer-session-and-lobby.md#new-tribe) owns
name/title resets, availability, gender, tribe/color publication, passwords,
custom names, city style and cleanup. Here its explicit mode argument is 1 and
inGame is zero: it uses setup pictures/positions and skips the network claim,
lock/reply and network-pump branches gated on mode above 2. Its other stated
checks, including live server-loss counters outside those gates, remain; do not
remove them merely because this caller is local. Mode 1 also selects its
local-default custom-name/title behavior. The shared opponent selector below owns the remaining player slots and
tribe overrides. F05/V01 own world population; W02 owns generation.

On every normal return, destroy the palette before the prompt. Generated-world
success has no extra preference save or background redraw after initialization;
the scenario/save success branches do. Errors and retries retain preceding
rules, path, preferences and RNG changes.

An accepted extra HOTSEAT1 row above 4 originally falls through to the participant
and generation path with a retained `mapSource`. A previous attempt may have
produced it; first use without a producer requires supplied input or a stop
at its eventual read, after preceding valid effects. The reconstruction's
immediate retry for these rows is a repair, not an original rejection rule.

### Shared multiplayer opponent selection

This child is used by hotseat, network new-game setup and dormant mode-2 setup.
Its supplied human count is a captured scalar; it is not recomputed from the
mask. Construct a prompt and set alive to `humans union {0}`. For indices 1–7
whose matching human bit is absent, normalize the **tribe row at that index**:
negate each nonpositive leader-name, plural-name and adjective reference, then
both title references for governments 0–6, with signed-word reduction. These
are not the tribes looked up through each civilization. Zero stays zero and
−32768 remains −32768; rows outside 1–7 are untouched.

Starting with successful-draw count 0, repeatedly compare that count against
`s32(unsigned opponentCount + 1 - suppliedHumanCount)`, reloading opponentCount
at each test. Each iteration consumes one primary random draw and chooses
`draw % 8`. If its bit is absent from live alive, add it; otherwise retry that
same successful-draw count. Bits already human or barbarian therefore consume
draws without filling a slot. There is no retry cap. A nonpositive bound adds
none; an impossible larger bound may never finish. Changing inputs to make the
loop terminate is not an implicit classic recovery.

If Pick Opponents is nonzero, scan slots 1–7 ascending, admitting those presently
alive and not human. Increment the displayed opponent number and publish number
argument 0; show side picture 9; load OPPONENT; change its first option's ID to
21 (Random). The load result does not guard that first-node access. A missing
node requires a safe stop, not an invented menu. Append tribes 0–20 whose
preferred color equals the slot, in tribe order, using their plural and leader
names as `plural (leader)` and their tribe indices as option IDs. Set the next
setup position, run the modal, play the accept sound and close the side picture.

A negative reply destroys the prompt and **returns from this void child**;
the caller still saves preferences and creates the world. Replies 0–20 store
their low byte as this slot's pending tribe override. Any reply at least 21
leaves its previous override unchanged; there is no implicit reset to Random.
Continue scanning, re-reading masks at each slot. Destroy the prompt on normal
completion. Previously chosen slots, overrides, references and random draws
survive cancellation. Unsafe option-node/name writes are excluded; unsupported
name reads need produced backing. F05 owns how initialization consumes the
resulting alive set and pending overrides.

## 3. Player-count chooser

Construct the chooser even for the network counting branch. For mode below 3,
show the side picture and load HOTSEAT2. Its installed entries represent two,
three and four humans, with returned values 0,1,2. Append choice 3 when maximum
players >4, choice 4 when >5 and choice 5 when >6, representing five through seven
humans. Set the remembered default and setup position.

Disable choices by a fall-through switch on the current opponent count:

| Opponent count | Choices disabled, subject to appended entries existing |
| --- | --- |
| 1 | 1, 2, 3, 4, 5. |
| 2 | 2, 3, 4, 5. |
| 3 | 3, 4, 5. |
| 4 | 4, 5. |
| 5 | 5. |
| Other | None by this switch. |

Each optional disable repeats its corresponding maximum-player test. If
`rememberedChoice + 1 < unsigned opponentCount`, set the **same remembered
default again**; this is not a clamp to a valid option.

Show the prompt. A negative result destroys the prompt and returns 0 **without
playing the accept sound or closing the side picture**. Otherwise store the
result's signed-word reduction as the remembered choice, compute the full-width
wrapping result plus 2, play the sound, close the side picture and return that
computed count after prompt destruction. Do not reconstruct the count from its
narrowed preference or add a post-modal count clamp.

For mode above 2, skip the menu and count human bits 1–7, irrespective of alive;
ignore barbarian bit 0. Return that count after destroying the prompt.

## 4. Hotseat turn loop

Initialize viewsHidden to zero and the first-email-human latch to false.
Construct the turn prompt, close advisor and casualty windows and update menus.
If the live mode is 1, copy humans to participants. The original savedActor byte
remains unproduced until the AI prefix below writes it.

Enter the outer iteration without an initial active gate:

1. Clear the human-moved flag; compute F04 human wonder ranges.
2. If resume is zero, advance globally with reset selector −1.
3. Check retirement. For a retirement result or pending outcome, execute the
   outcome transition below; leave if it selects exit.
4. If mode is 1 and humans is empty, show HOTHUMANSDEAD and leave, even if its
   callback changes the roster afterward.
5. If events are loaded and (`resume == 0` or `resumeNew != 0`), run S02's turn,
   interval, random-turn and received-technology checks in order. The first
   three receive the live turn at each call. Do not retest the outer gate between
   the four children.
6. Visit civilizations 0–7 while active. Admit only an alive civilization `c`
   with `unsigned resume <= c`, then execute its phase below.
7. F04's fallback human-movement phase applies only when no human moved **and
   live mode is 0**. It does not give hotseat an extra movement phase.
8. Intersect humans with alive. Leave if inactive; otherwise repeat.

### One civilization phase

Publish turnCiv = c. If mode is 1, copy the current humans to participants and
publish networkTurnCiv = c. Set viewsHidden to 1, then hide views in the following
order: close the city window; hide status; hide minimap; scan all eight map views
upward, hiding each whose open flag is nonzero at its current test. Hiding does
not erase map knowledge or clear any open flag.

If `c` is not human, and mode is 1, load HOTSEATAITURN and show the modal; ignore
its return. If `c` is human, perform §5's handoff. A dormant e-mail completion
can request an immediate loop exit. After either ordinary branch, draw `c`'s
civilization indicator. The human branch therefore draws that indicator twice,
with its window restoration between the two calls.

Now re-read mode and human membership. If mode is 1 and `c` is not human:

1. Clear humans entirely.
2. Save the low byte of the live local actor into savedActor.
3. Publish full local actor −1.

If resume is zero or resumeNew is nonzero, run upkeep. A nonzero resume with
resumeNew first updates title, redraws local views with the live actor and
flushes painting. Then report unreported local casualties through F04 and run
civilization upkeep for the captured `c`. The sentinel casualty-read boundary
below applies even before an AI civilization's upkeep.

After upkeep, test human membership again, without an alive recheck:

- If `c` is not human, run AI units. If the live mode is now 1, close/reset the
  turn prompt, then restore local from the **zero-extended savedActor byte**.
  The original does this even when no AI prefix in this civilization wrote the
  byte. It may restore a value from an earlier civilization or outer iteration;
  if no prior writer exists, consume explicitly supplied incoming state or stop
  at this read after AI and prompt cleanup. Do not invent zero or preserve the
  current actor as an unstated classic repair.
- If `c` is human, and mode is 1, intersect humans with just `c`'s bit. Run F04
  start notices. If the now-live mode is 1 and time limit is nonzero, start
  timer(1), run human movement, then stop timer(1). This selected timed tail
  runs even if movement changes mode or limit. Otherwise run human movement
  without those timer calls. Mark that a human moved.

If the live mode is 1, restore humans from the live participants set. Clear
saved resume civilization, runtime resume, saved resumed-new flag and runtime
resumeNew, then clear the end-turn request. If inactive, finish and return.
Otherwise a pending outcome executes the outcome transition. Continue the scan
only if it does not select exit.

### Outcome transition and finish

Capture the count of human bits **before** calling F04's game-over handler.
Afterward, if mode is 1 and any human bit other than the live local actor's bit
is present, clear the local actor's bit from humans. This test does not require
that the local bit was present. Exit if the handler returned nonzero and either
live mode is 0, or live mode is 1 with the captured human count below 2.
A changed post-handler count does not replace that captured count. Otherwise
continue, discarding the handler return for later iterations.

Every normal loop finish stops timer with notify 0 and destroys the turn prompt.
It does not automatically show views still hidden by the last AI phase. N01's
outer session cleanup remains a separate caller operation.

### Sentinel casualty reads and excluded write

F04 casualty reporting uses local, not the captured civilization being processed.
The AI prefix's local −1 therefore reaches two original reads before the autoplay
gate: the preceding casualty-count word and the preceding reported-count word.
Their backing is established in Appendix A. The former is retained deferred-draw
state; the latter is civilization 7's casualty count. Preserve their supplied or
established values when determining the wrapped delta. Do not pretend a ninth
ordinary civilization or an automatic zero count exists.

Leader name/title queries for an actor below 1 have their ordinary fallback-label
behavior. If the captured delta is positive and autoplay is zero, the original
subsequent reported-count write at actor −1 is **out of bounds**. It is excluded
as a bug, even though it aliases a real neighboring counter. Stop or omit that
unsafe notification operation without changing civilization 7's casualty count.
The safe nonpositive-delta path still reaches upkeep. Other out-of-domain local
actors require their own backed-read/safe-write analysis; memory mapping alone
is not a value producer.

## 5. Human handoff and concealed views

For a captured admitted civilization `c`:

1. Publish playerCiv = c. If mode is nonzero, call N01's check-password operation
   for c. **Ignore its return here.** It may clear active on cancellation, but
   the following handoff/presentation and later upkeep calls still run until
   the parent reaches an explicit active gate.
2. If the live mode is 1, publish leader name and civilization plural as text
   arguments 0 and 1, then show HOTSEATTURN. After it returns, save current cursor
   X under the full live local actor; then save current Y under a fresh local
   actor read. These are the outgoing player's saved coordinates.
3. If introduction flag[c] is exactly 1, restore its saved X into main-view
   center X and then cursor X; restore Y into main-view center Y and then cursor Y.
   Otherwise show c's introduction, scan active cities upward against the live
   city count, and use the first city owned by c to set main center/cursor X and
   Y in that order. There is no unit fallback. If no city qualifies, retain the
   current coordinates. Mark introduction flag[c] = 1 afterward in either case.
4. Capture the low byte of local as previousActor, then publish full local = c.
   Apply the dormant e-mail continuation in §9 if live mode is 2.
5. If zero-extended previousActor differs from the live local actor, or resume
   is nonzero, initialize remaining seconds/cache when the time limit is nonzero,
   then update the main title and redraw local maps. The seconds calculation is
   signed milliseconds divided by 1000, truncated toward zero.
6. Draw status information with argument 1 and c's indicator. If viewsHidden is
   nonzero, clear it **before** showing status, minimap and each presently open
   map view in ascending order. Later open-flag reads observe earlier callbacks.
   Clearing the hidden flag during a callback does not cancel this selected show
   sequence. Return normally to the caller's second indicator draw.

Passwords and the handoff popup happen while the parent has hidden views. The
view code's visibility/knowledge choices remain [W03's](W03-visibility-and-exploration.md)
and P01/P02's contracts. Concealment here changes window visibility and actor
selection, not remembered terrain, ownership, contact or discovered-map data.

An introduction flag of 2 takes the introduction path, despite being nonzero.
A cancelled password does not itself undo saved coordinates or restore the
previous player. Those are consequences of explicit subsequent calls or exits,
not an assumed authentication transaction.

## 6. Joining a running hotseat game

The local Change Player command constructs its prompt, captures full local,
playerCiv and active humans, then replaces humans with participants. Stop
timer(1), close the city window, hide status/minimap and all currently open map
views in ascending order, then flush painting. This command does not share the
turn loop's private viewsHidden variable.

Count alive civilizations 1–7, splitting them by current human membership. If
maximumPlayers is **exactly equal** to the human count, publish that maximum as
number argument 0 and show JOINGAME1. Otherwise, if the AI count is zero, show
JOINGAME0. Otherwise invoke the local civilization picker below with
`playerIndex = humanCount - 1`. A zero picker result publishes the live local
civilization's plural in text argument 0 through the second text-argument setter,
then shows JOINGAME3. Other results skip that success notice.

The common tail restores the full captured local actor, restores playerCiv,
publishes the live humans as participants, and restores captured active humans,
in that order. Show status, minimap and each currently open map view upward;
start timer(1); destroy the prompt. This uses N03’s start operation, not its
separate resume operation. These restores are unconditional with
respect to live game mode. A successful joining player therefore joins the
participant set without taking over the currently active human phase.

Do not replace the equality maximum test with `>=`. The computed total-alive
count is unused. A dialog/loader failure does not imply that all picker mutations
were rolled back before the common tail publishes participants.

### Local civilization picker

This is a separate algorithm from N01's network claim chooser. It sends no
LockNation/UnlockNation messages and does not consult the claimed mask.

Construct a prompt. Each attempt resets it, prepends the player-number title,
and builds a list from alive civilizations 1–7 in ascending order. Each row
contains plural, leader, city count and military-unit count. Associate its
civilization with the **returned option ID** in an eight-entry local mapping;
only appended entries produce mapping values.

Refresh each found option: enable when its civilization is not human, disable
when human, updating the native radio control if present. The availability
counter is computed but does not guard the next dialog. Starting from selection
0, inspect at most the number of appended choices, wrapping only on equality
with that count, to select the first existing enabled option. Set both dialog
default and radio selection when found. Show the modal. A negative result
destroys the prompt and returns 1.

For a nonnegative result:

1. Resolve the produced mapping entry. Publish full local = selected c, then
   add c's bit to humans. Do not change playerCiv here.
2. Load NAME with the current leader name and a 23-character limit. Load failure
   restarts the list **without clearing the newly set human bit**. Otherwise
   prepend the player-number title and run the text modal. Any nonzero result
   clears c's bit and restarts, leaving earlier effects intact.
3. For result exactly 0, copy the entered name into c's local leader name. If
   its tribe's rule leader-name reference is positive, negate it with signed-word
   reduction. Zero and negative references remain unchanged.
4. Check the password of the **live local actor**. Failure clears the originally
   selected c's bit and restarts. Name/reference changes are not undone.
5. Load GENDER, prepend the player title, use the tribe belonging to **current
   playerCiv** for the default gender, set position, show and play the accept
   sound. A negative result clears selected c's bit and restarts.
6. For a nonnegative gender result, store its low byte into the tribe belonging
   to current playerCiv. Clear FemaleLeader on the live local civilization and
   set it when the full modal result is nonzero. For governments 0–6 and sexes
   0 then 1, copy the corresponding default government title into that
   playerCiv tribe's title entry with word reduction. Destroy the prompt and
   return 0.

The gender/title recipient can be the previously active player's tribe while
the name, claim and female flag belong to the joining civilization. Both are
valid identities; do not silently replace playerCiv with the selected c.
Normal gender results are 0/1. The reconstruction's rejection of greater
nonnegative results is an explicit input repair. The original stores the
reduced gender and nonzero flag; later invalid gender-indexed reads remain
separate bounds/input obligations.

A missing mapping entry or first accepted name prompt without produced text
requires retained input or a stop at the read. The original does not initialize
every mapping slot or the first name byte. Later attempts can retain earlier
produced text. Copies that exceed their actual destinations are excluded writes;
a larger surrounding allocation does not make cross-record overwrite required.

## 7. Quit, retirement and window close

The menu Quit command and main-frame close callback both ask REALLYQUIT;
Retire asks REALLYRETIRE. A zero response cancels. The main-frame callback
returns 0 regardless of whether its actions run. Menu Retire, after a nonzero
response, first shows the power graph and, when the game has not been scored,
shows score, rating and adds the hall-of-fame entry for the live local actor,
in that order. V08 owns these children.

The common admitted hotseat continuation is:

1. Capture the current active humans; replace humans with participants.
2. Clear the unit-cursor blink phase and redraw map cursors.
3. If no human bit other than the live local actor's bit is present, clear that
   actor's bit, disable map display, then stop CD audio. Otherwise invoke
   [V01 civilization elimination](V01-civilization-lifecycle-and-civil-war.md)
   with `(local,0,0)`. That child owns its live options, removals and notifications.
4. Clear human-movement activity and set player-has-quit to 1.
5. If live mode is 1, publish the resulting humans as participants, then restore
   the captured active humans.

There is no unconditional direct clear of turn-loop active in this shared tail;
the display-disable/elimination children and the enclosing turn loop supply
those transitions. While other participants remain, the command can remove the
current participant and let the local loop continue to another human.

Mode is re-read throughout the actual shared quit commands. Single-player paths
stop audio/disable the map directly; network paths have N01's timer/meeting/chat,
message and authority operations. If a callback changes mode into 1 without an
entry-time hotseat capture, the final saved-mask read is unproduced: require
explicit incoming state or stop after prior effects. If it changes away from 1,
that final hotseat restoration is skipped. There is no inferred transaction
restoring the old mask for all modes.

The primary port's preflight that exits immediately on Quit, or ignores Retire,
when no game is in play is not the original command. Classic command dispatch
and the original confirmation behavior belong to the P05 boundary; do not use
that port guard as evidence for a classic hotseat phase.

## 8. Save, load and shared consumers

The participant/active-human distinction crosses other algorithms:

| Consumer | Required join |
| --- | --- |
| F06 game writer | In entry mode 1, temporarily serialize participants as humans and restore the captured active mask at live-mode cleanup. Preserve F06's partial failures. |
| F06 multiplayer block | Immediately before writing it, temporarily replace saved resume civilization with networkTurnCiv; restore only after that item succeeds. A failed item can leave that resume mutation live. |
| F06 file choice/load | Hotseat uses `.HOT`; mode adaptation, saved cursor/intro fields, password flags, resumed civilization and loaded actor follow F06/S01. The hotseat startup loader then applies §2's mode check. |
| N01 session entry/exit | Shared visibility/contact setup and window/session cleanup surround the local loop. End-of-session cleanup removes Change Player only for live mode 1. |
| N01 game profile | Its hotseat player count comes from participants, not the temporarily narrowed humans set. |
| C06 civilization production reset | The shared reset skips a civilization that is a hotseat participant, even when its active human bit is currently clear. |
| V04 attitude assignment | In hotseat, participant membership suppresses the ordinary attitude assignment unless the addressed civilization is the local actor. |
| W03 knowledge and map views | Human-mask/actor changes affect consumers that consult them; hiding a view does not mutate its remembered map. |

[F06's writer](F06-save-load-and-file-formats.md#5-ordered-save-operation) is the sole owner
of byte order, extensions, temporary swaps and ordered write failures. The
turn loop clears saved/runtime resume fields only at its listed phase tail.
A save callback during upkeep, movement or player handoff therefore observes
those phases' then-current fields. Do not synthesize a universal end-of-turn
snapshot or automatically rewrite a failed save's resume mutation.

## 9. Dormant e-mail branches

The installed MGE multiplayer-type chooser routes local hotseat and supported
network choices; it does not route to the separate PBEM startup routine. X01
therefore does not treat e-mail mode 2 as an established playable mode. These
branches are recorded for file interpretation and supplied continuation tests,
not as a claim that users can complete an MGE e-mail game through the shipped UI.

### Local-loop continuation

At local-loop entry the first-email-human latch is false. After a human handoff
publishes its new local actor, if live mode is 2:

- On the first human phase, set the latch and continue.
- On the next human phase, invoke the save dialog with argument 0 and ignore its
  result. Test the selected civilization's e-mail row's first byte. If nonzero,
  publish leader and address and show EMAILDONE1; otherwise publish leader and
  show EMAILDONE2. Clear active and finish the local loop. This exit occurs
  before its ordinary title/status/window-show and upkeep continuation.

The latch survives outer turns. It is not reset for each civilization or by a
failed save. No mail transport or actual e-mail sending occurs in this branch.

### Shared tribe chooser's address continuation

After accepting city style, the shared tribe chooser has already stored style,
result 0 and the chosen leader in setup slot `playerIndex+1`. Mode 2 then shows
its address prompt. Outside a running game, show the e-mail side picture.
Use the remembered address for player index 0, otherwise the blank default;
load EMAILADDRESS with the 31-character limit, ignoring the text-load result,
prepend the player title, set setup position when applicable and run the modal.

Server-cancel/lost counters can finish with result 1 before ordinary sound and
picture close. Otherwise play the accept sound and close the side picture when
applicable. Modal result exactly 0 copies the text to the current playerCiv's
address row; for player index 0 also copy the remembered address and set both
saved/runtime resume civilization from the local actor's low byte. Finish.
A nonzero result clears the current playerCiv's human bit and retries the outer
tribe chooser. The previously written result 0 can survive that retry; later
cancellation is not guaranteed to convert it back to failure.

Missing output, retained text and unsafe copies have the same produced-input
and excluded-write boundaries as the other text dialogs. The independent port's
first-byte-empty repairs are not recovered original values.

### Unrouted startup inventory

The separate PBEM startup clears all introduction flags and only the first byte
of each address row, clears the time limit and selects mode 2. Its PBEM1 menu has
new random, premade, custom, scenario and load choices. The new-world count/tribe/
opponent/rules/generation sequence parallels §2 but calls the shared wizard and
tribe chooser with mode 2. Scenario/load continuations use mode 2, show EMAILNOT
for a successful wrong-mode load, and save preferences on success. They do not
add Change Player or the hotseat background/palette refresh. Unlike hotseat's
new-world common prefix, this routine does not clear the claimed-civilization
word there. Extra menu rows have the same retained/unproduced map-source boundary;
BF-079's retry is a separate reconstruction repair.

F06 owns `.EML` storage and the save dialog's mode-2 notices. Remaining e-mail
command/address UI is an unsupported-entry inventory for P05/P04, not a new
mail delivery requirement or an extension of the nominal classic input domain.

## 10. Conformance cases and integration

Compare ordered observations, not only final masks: player/view identities,
window hide/show order, prompt selection, rule/RNG changes, simulation calls,
timer calls, participant publication, save/load effects and return values.

| Case | Required result |
| --- | --- |
| Cancel player-count selection | Return 0 with prompt destruction; no accept sound or side-picture close at that return. |
| Player-count result 3 | Remember 3, return 5. Return uses the full result plus 2, independent of preference narrowing. |
| Network count with human bit 0 and dead human bit 7 | Ignore 0 and include 7; alive is not tested. |
| Earlier player's tribe choice cancelled | Decrement player index and retry that previous slot immediately; backing out of player 0 restarts count selection. |
| Opponent chooser cancelled | Return from its void child with preceding draws/overrides retained; startup still continues to preferences, rules and world creation. |
| Premade map accepted | Consume the five random setting draws before shared options despite retaining the loaded topology. |
| Introduction flag 2 | Introduce again; select first active owned city, not first unit; set flag 1 even if no city exists. |
| Password cancelled at handoff | Active can become zero, but the ignored password return does not skip the following popup/cursor/presentation or selected upkeep prefix. |
| Timed movement changes limit to zero | The already-selected stop(1) tail still runs. |
| AI phase | Save the outgoing actor byte, publish full −1, mask humans away, run selected children, then restore the saved byte as a full nonnegative actor. |
| Human removed during upkeep after an earlier AI phase | AI cleanup restores that earlier saved actor, even when it differs from the current human. |
| Human removed before any savedActor producer | Require supplied incoming byte or stop after AI and prompt reset. No assumed zero/current actor. |
| No hotseat humans | Show HOTHUMANSDEAD and finish; do not execute F04's single-player fallback movement. |
| Outcome handler changes roster | The exit test still uses the pre-handler captured human count; local-bit removal uses the post-handler mask/actor. |
| Join with maximum below current count | The equality limit test is false; AI availability decides the next branch. |
| Successful local join | Publish the new participants while restoring the old active mask and full local/player identities. |
| Local picker chooses a different civilization | Name/claim use selection; gender/title default and writes use existing playerCiv's tribe; female flag uses live local. |
| Local picker text-load failure | Its newly set human bit remains while the list is rebuilt. |
| Local quit with another participant | Elimination child updates the participant roster; restore the old active mask and let the enclosing loop determine continuation. |
| Save during a narrowed human phase | F06 writes the participant set; successful cleanup restores the narrow active set. Short multiplayer-block write retains its replacement resume byte. |
| Second mode-2 human phase | Attempt save and show the address/no-address notice, then exit even when the save was cancelled. No mail is sent. |

The startup parent makes five parameter decisions on the specified branches;
retain their published settings even on a premade map. Opponent selection keeps
its duplicate/retry logic. Child generation, initialization, password reset,
upkeep, AI, events and presentation retain their decision rules. F01 permits
independent contexts, without requiring original global draw counts. A handoff
continues the implementation's declared random policy and must not repeat already
applied decisions. Complete hotseat tests require F04/F05/S01/S02/V01/W03 and
presentation/input children with their real mutations and callbacks.
## Appendix A. Representation, original differences and bugs

Private records can be redesigned. These mappings support validation and
interoperability with F06; they are not a mandatory in-memory implementation.

| Item | Original representation/evidence |
| --- | --- |
| Mode and masks | Mode, alive and humans are bytes; retained participants is byte `006c31a9`. Civilization shifts use the low five bits of the low-byte shift count. |
| Local actor | DWORD `006d1da0`; ordinary actor publications and the −1 AI sentinel use full-width stores. Saved actor at original local-loop frame `EBP-320` is a byte, restored by zero extension. |
| Cursor state | Eight signed words each at `00654b40` (X), `00654b50` (Y), `00654b60` (introduction flags). Main center X/Y at `0066ca88`/`0066ca8a`; cursor X/Y at `0064b1b4`/`0064b1b0`. |
| Prompt state | Main local loop and player-count/join prompts use construction argument `0x4000`; shared tribe chooser uses `0x2000`. HOTSEATAITURN flags `0x02000008`; HOTSEAT1/HOTSEAT2 and GENDER flags 1. |
| Menu item | Append Change Player to menu 1 with command `0x1f2` and label `0x363`. The label alone does not define its command behavior. |
| Optional count labels | Choice IDs 3,4,5 use labels `0x365`, `0x366`, `0x367`. Remembered hotseat action is preference word offset `0x04`; player-count choice is offset `0x0a0`. |
| Position | Setup X/Y margins are `s32(-(trunc(s32(screenDimension-base)/8)+1))`, bases 640/480. Hotseat menu X comes from next-dialog-X(0); shared center sentinel is −999. |
| Background | GIF resource 90, palette/color arguments 10 and 192, main background color `0x9e`; side-picture arguments (−1,0). |
| Name picker | Private entered-name capacity 260, nominal field limit 23; title label is playerIndex + `0x25f`. Eight local mapping scalars are written by returned option IDs. |
| Gender/title publication | Female flag `0x0200`; source government titles are DWORD label IDs reduced into signed-word tribe title entries. |
| Sound | MENUOK 106 after menu/custom modal acceptance or cancellation at the stated sites; casualty notice NEWONDER 48. Other sound arguments are zero. |
| E-mail state | Eight 32-byte address rows start at `00654da4`; only first bytes are cleared at dormant setup entry. Shared field limit 31. F06 owns full retained row bytes. |
| Sentinel count read | At actor −1, `casualtyCounts[−1]` reads `006af21c`: last N02 deferred-draw record's final argument, index 99/argument 14. |
| Sentinel reported read/write | `reported[−1]` addresses `006af23c`, the ordinary casualty count of civilization 7. The read is backed; the attempted write through the reported array is an excluded overwrite. |

### Original actor width and private saved state

The primary reconstruction often casts the local actor to its low signed byte
in the local-loop/handoff helpers. Original `0048b719`/`0048b72d` read the full
actor for outgoing cursor indexing; `0048b891` publishes a full actor;
`0048badb` stores full −1; `0048bd5e` reads the private saved byte and `0048bd64`
publishes its zero-extended full value. This specification uses those original
operations. The full source/header width alone is not proof that each recovered
cast in a function is faithful.

Original B34/BF-164 behavior restores the saved byte unconditionally on the
post-AI live-hotseat branch. The primary classic-mode repair keeps the current
actor when no prior save exists; its fixed mode additionally rejects a stale
save from an earlier civilization. The independent reconstruction guards
unproduced saved state. None recovers the original first stack byte. Supplied
incoming byte cases and safe stops must be separate from initialized histories.
The restoration itself is an in-bounds scalar store. It is not excluded merely
because it selected the wrong player; later out-of-domain writes are separately
excluded at their own boundaries.

The two sentinel casualty reads have concrete neighboring backing. If the
retained deferred-draw argument has not been established for a comparison,
provide it as input or stop at the read. Do not assume a convenient value because
it usually suppresses a popup. Original `0048bbbf` writes the positive-delta
notification result through the invalid reported index. That corruption is
excluded, including when ordinary hotseat scheduling reaches it. This differs
from the safe retained savedActor scalar above.

The primary handoff stores both cursor coordinates before both center
coordinates, whereas the original writes center X/cursor X/center Y/cursor Y.
The primary join command restores local after its player/mask stores, whereas
original `00522d36` restores local first. For disjoint, backed fields, neither
sequence has a callback or fault between these stores; the finite fixture folds
these order differences and observes the same state at the next child call.
The specified order above remains the original order. This fold does not apply
to invalid destinations or aliasing storage. Likewise, the unchanged close
fixture uses nominal actors, for which its primary low-byte actor reads agree
with the original full actor; it does not establish off-domain equivalence.

### Setup and text repairs

BF-075 retries extra HOTSEAT1 actions before count/world work. BF-079 makes the
analogous PBEM1 repair. Original default switch paths can reach generation with
retained or unproduced map-source state. Neither a retry nor source 0 is inferred
as original behavior. Nominal installed actions have defined producers.

BF-113 rejects added gender rows; BF-102 retries city styles outside the four
normal choices. Their accepted-scalar behavior and later indexing boundaries
remain distinct. BF-041 seeds the local join name buffer's first byte when
native EDIT creation yields no output; that is a repair. BF-120's option-map
validation does not establish that unused original slots were zero or valid.

Unsafe name/title/path appends, invalid mapping IDs, invalid saved-cursor writes
and gender-indexed writes are excluded bugs. Keep the original evidence here
and preserve safe preceding state transitions. Do not turn overflow into
required updates of another civilization, name row, title or view field.

## Appendix B. Evidence and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary source pin:
`a71e8aefffe04e6f719e0e213b027b128a747711`; researched checkout
`fc2155ccdb2123c1137babab6fa39e38152278c9`. The 22-body inventory and corresponding source/header/literal records were
compared against that pin.

| Contract | Source navigation |
| --- | --- |
| Hotseat startup | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp), `setup_hotseat_game` `00419170`. |
| Count, local join and picker | Same [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp), `setup_choose_hotseat_players` `005227e3`, `setup_choose_opponents` `00522f8f`, `game_join_dialog`, `setup_join_pick_civ` `00523f02`. |
| Local turn and handoff | Same [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp), `game_play_loop` `0048b340` and its split presentation/hide helpers. |
| Quit/retire and close | Same [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp), `game_quit_or_retire`, `main_frame_on_close`. |
| Shared tribe/password and dormant e-mail branches | Same [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp), `setup_choose_tribe` `0051f19c`, `setup_tribe_city_style`, `setup_pbem_game` `005ae580`; N01/F06 own shared consumers. |
| Password gate | [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp), `civ_check_password`; N01 owns its common contract. |
| State and scalar encodings | [game state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/game.h), [network state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/network.h), [types](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h). |

Independent evidence is pinned to `civ2` revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`: `src/local_turn.cpp`,
`src/application_session.cpp`, `src/network_tribe_selection.cpp`,
`src/menu_command.cpp`, `src/pbem_startup.cpp`, `src/quit.cpp`, plus the named bugfix ledger
entries. Its explicit backing checks and repaired initializers are evidence
boundaries, not classic success/failure rules.

Original executable: MGE **5.4.0f Multiplayer 26-March-99**, patch 3; SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Twelve pinned instruction probes support the focused original review of full
actor indexing/stores,
word introduction comparison, unconditional saved-byte restoration, sentinel
casualty read/write sites, local-join actor-before-mask publication and the
join command's actor-first restoration. This
is direct evidence for those sites, not native execution of a complete game.

A temporary finite Linux fixture compiled **six complete unchanged primary
bodies** and **two separately original-adapted bodies**, passing **29 assertions**
in optimized and AddressSanitizer/UndefinedBehaviorSanitizer builds. LeakSanitizer
was disabled for the execution environment. The unchanged bodies are view hiding,
casualty prompt, password gate, player-count chooser, join command and main-frame
close. The adapted bodies are the complete local parent and human-handoff helper:
full actor accesses replace the primary byte casts; the parent restores a prior
saved byte or consumes explicitly supplied incoming state at the original read.
Fixture definedness tracking does not manufacture an original initializer.

The fixture joins the actual handoff/password/hide/casualty bodies to the complete
adapted parent. Clock/timer operations, simulation/upkeep/AI/movement, world
setup, name/label lookups, dialogs, window methods, join picker, elimination and
outcome services use explicit adapters. The sentinel counter adapter supplies
its identified backing; tests stay on the nonpositive-delta continuation and do
not execute the excluded write. Diagnostic stops preserve preceding effects
without adding a simulated original unwind.

Checks cover normal and resumed handoff, exact-1 introduction state, first-city
selection, no-city retention, ignored password cancellation, selected timed tails,
participant joins/removal, player-count wrap/cancellation, original stale/supplied
saved actor and unresolved-read stop, outcome count timing, no-human exit and
the supplied dormant second-human e-mail continuation. Startup, shared opponent selection, the complete
local civilization picker, actual save/load, world generation, native privacy
windows and real password EDIT failures were source-reviewed or delegated to
existing owners; they were not exercised by this fixture. Full native UI/gameplay,
RNG and save/resume integration remains open. These checks do not establish native
conformance or runnable PBEM support.
