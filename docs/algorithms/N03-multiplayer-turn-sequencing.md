# N03: Multiplayer turn sequencing

Target: Civilization II Multiplayer Gold Edition 5.4.0f,
patch 3. This document specifies the sequential and simultaneous host/client
turn loops, their phase barriers, heartbeats, timed turns and setting votes.
The four loops have different resume and failure behavior; an implementation
must preserve those differences.

[N01](N01-multiplayer-session-and-lobby.md) owns session setup, civilization
claims, lost-connection handling, host migration and final session teardown.
[N02](N02-network-commands-and-state-synchronization.md) owns messages, receipt
counts, dispatch, state diffs, queues and remote popups. Those algorithms are
children here. [N05](N05-transport-and-wire-protocol.md) owns transport delivery
and flush/message-pump ordering. [F04](F04-turn-sequencing.md) owns
shared global advancement, civilization upkeep, start notices, retirement and
game-over handling; [U04](U04-unit-orders-and-activation.md) owns unit resets and
the active-input cycle. [S02](S02-event-language-and-execution.md) owns scenario event passes.
[P04](P04-text-and-dialogs.md#10-modal-completion-wrappers-and-cleanup) owns dialog presentation and the shared modal loop; P05 owns native events, timer delivery and window
interaction. This document specifies their call order and arguments.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

Host/client phases, simultaneous-turn barriers, timers, votes and incoming
operations retain their dependencies. Internal scheduling freedom does not
change which peer executes an operation or when another peer may observe it.

## Compatibility policy

X01's [required transport](X01-classic-compatibility-contract.md#required-and-optional-transports)
is TCP/IP with UDP discovery; the sequential/simultaneous loops and mixed-peer
host/client obligations here remain required. Legacy transports are optional.
The [presentation policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy)
permits different dialogs, animations and native event pumps, not different
turn deadlines, voting, authority, barriers, heartbeat/disconnect rules or resume
state. Preserve gameplay-dependent holds and gates even if no native drag/modal
loop exists. Test semantic decisions and externally delivered events instead of
requiring identical paint, focus or allocation callback traces.

## Implementation boundary

The main sections use logical values, identities and operations. Original memory,
allocation and platform mechanics are recorded in [Appendix D](#appendix-d-original-implementation-reference). Preserve required
state changes and externally visible results without reproducing those mechanisms.
Exact numeric domains and file/wire encodings remain required wherever specified.

## 1. State, inputs and compatibility domain

Required inputs include the established N01 session, game state, player-slot
and civilization mappings, queued N02 messages, callback results, clock samples,
input events and child algorithm results. A civilization ID, connection ID and
player-slot index are different values. The host is the server, not necessarily
civilization 1. Network game modes have values above 2.

Use these logical state names:

| Name | Meaning |
| --- | --- |
| `active`, `outcome` | Turn-loop activity and pending game-over reason. |
| `alive`, `humans`, `pendingHumans`, `claimed` | Civilization membership sets; normal IDs are 0–7, with barbarians at 0. |
| `local`, `playerCiv`, `turnCiv`, `networkTurnCiv` | Local human civilization, saved player actor, current simulation actor and published network actor. These need not agree during callbacks. |
| `resume`, `resumeNew` | Runtime resumed civilization and resumed-new-game flag. Separate saved fields exist; clearing one pair is not an implicit clear of the other. |
| `phaseDone[c]`, `turnEnded[c]` | Wrapping completion counters, not Boolean acknowledgments. |
| `received[kind]` | N02's shared receipt counters. |
| `handoff`, `quit`, `lost` | Host-transfer stage, player-has-quit flag and accumulated connection failures. |
| `casualties[c]`, `reported[c]`, `introShown[c]`, `newPlayer[c]` | Per-civilization presentation and join state. |
| `tileSync`, map locks | N02 synchronization controls. |
| `autoplay`, `remaining`, `savedHumans` | Autoplay enable word, remaining-turn word and shared temporary human-set storage. |
| `limit`, `seconds`, `blink`, `lastTick`, `timer`, `timerShown` | Turn-time limit in milliseconds, remaining seconds, blink state, previous clock sample, logical timer registration (absent or present) and caption visibility. |
| `ack`, `heartbeatTick`, `votePending`, `voteStatus`, `popupTick` | Shared heartbeat/vote counters and deadlines. `ack` is the HeartBeatAck receipt count. |

Nominal state has established player records, valid civilization and unit/city
indices, active dialog/window backing, terminated resource/player strings, and
Boolean flags except where a stage or count is specified. `resume` is 0–7;
zero means no partial civilization to skip. Session setup supplies the initial
clock/timer fields and saved resume state. Timers normally use no limit, a preset
30/60/120/180/300 seconds, or an accepted custom 10–3600 seconds.

[F01 arithmetic](F01-determinism-and-arithmetic.md) applies throughout: counters,
clock subtraction has modulo-2³² results, interpreted as signed where stated.
Other scalar expressions follow F01 reference evaluation; use sufficient-range
arithmetic wherever equivalent. Civilization sets
retain their low eight members. Resume is a retained byte; nonzero tests and
host comparisons use its unsigned value. Autoplay enable/remaining and remembered
timer choices retain their low 16 bits. Details that change a mathematical
result are stated below; storage offsets are validation evidence in Appendix A.

Every external call can mutate live state. A condition selected before a call
continues along that selected branch; later explicit tests read the new state.
Do not snapshot a whole turn, roster or option set. In particular, completion
scans use current `alive` and `humans`, and timer operations re-read their
notification gates after window/timer callbacks. A send or flush failure does
not add an abort or rollback to these loops: the stated continuation still runs
unless the child changes state, raises an error or stops.

A stricter input profile may reject unsupported indices, malformed strings,
arithmetic faults or invalid service references before accepting an operation. For accepted
off-nominal inputs, preserve defined scalar wrap and safe quirks. [Out-of-bounds
writes are excluded bugs](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
including string copies and completion-array writes. Omit/reject/stop an unsafe
operation, preserving valid preceding effects; do not translate corruption into
assignments to adjacent logical fields. Invalid or unproduced reads require
established backing, explicitly supplied retained input, or a bounded stop.
They are not covered by the write exclusion.

## 2. Shared operations and entry

The notation below means these ordered operations:

- **Sync**: N02 state diff to all peers, flags 2, with the other diff arguments
  zero. A following **flush(n)** calls the transport flush with timeout `n`
  milliseconds. They are distinct calls.
- **Drain**: while either receive queue has a nonzero count, poll with `(1,0)`.
  Check counts again after each poll. A busy/reentrant poll or continuing input
  can prevent termination; no extra deadline is implied.
- **Reload local**: read the local player's current slot civilization, publish
  `local`, then its low-byte value as `playerCiv`; set `turnCiv` only where stated.
- **Clear resume**: clear saved civilization, runtime `resume`, saved resumed-new
  flag and runtime `resumeNew`, in that order.
- **Exhaust(c)**: scan units upward from 0 against the live unit high-water
  count. For each active unit currently owned by `c`, invoke [U02's movement allowance](U02-movement-and-zones-of-control.md) and store its low byte as spent movement. Then scan cities downward
  from the entry-time city high-water count, clearing Investigated on every
  active city, regardless of owner. The unit-only variant omits the city pass.
- **Caption**: invalidate the main view's timer-caption cache and refresh its
  status information. Updating the title or maps is a separate operation. Native
  drawing geometry is in [Appendix C](#appendix-c-original-presentation-reference).
- **Show intro(c)**: if its shown flag is zero and autoplay is currently zero,
  invoke the introduction, then mark the shown flag. The flag is written after
  the callback. Fixed civilization arguments remain fixed; where the caller
  uses live local, re-read local for the flag write after the introduction.
- **Events**: run turn, interval, random-turn and received-technology checks in
  that order. The first three receive the live game turn at their respective
  calls. Test the outer events-loaded/resume gate once; do not retest it between
  the four children.

The N01 ReadyToStart barrier precedes selection of a loop. If simultaneous moves
is zero, a host enters the sequential host loop with takeover flag 0; a client
enters the sequential client loop. A nonzero client return enters the sequential
host loop with takeover flag 1. If simultaneous moves is nonzero, use the
simultaneous pair, again interpreting a nonzero client return as becoming host.
This selection does not re-run merely because an option changes during a turn.
Normal return proceeds to N01's common session cleanup.

### Host initialization

Construct the turn prompt. Log the local civilization, then ascending
civilizations 1–7 that are alive, human and different from `local`. Reset N01's
connection-error counters; rebuild map unit flags; close the advisor and
casualty windows; update menus; redraw all maps with argument 1; update the main
title. Center on the first active local unit in ascending index order, otherwise
the first active local city. Both scans compare against live collection counts.
If neither exists, leave the center unchanged.

If `resume` is nonzero, capture its initial byte and advance it while the
corresponding civilization is not alive. Increment the byte, changing an exact
8 to 0. If it returns to the captured byte, set outcome reason 1 and break the
search. Off-nominal bytes use F01's shift-count reduction and byte increment;
there is no clamp to a valid civilization before the search.

Both host loops enter their first outer iteration even if `active` is already
zero. Their later gates and retirement checks determine which children run.
Initialization itself does not clear saved resume fields.

### Client initialization

Construct the turn prompt. Log the civilization in player slot 0. For each
civilization 1–7, first read its mapped slot's connection ID, then test that the
ID is nonzero and the civilization is alive and human before logging it. The
mapping read precedes these guards; an invalid mapping is a read boundary even
for a dead civilization.

Reset connection-error counters. Reload `local`; copy saved resume civilization
and resumed-new flag to runtime state. Close advisor and casualty windows;
update menus; publish `playerCiv` and `turnCiv`; update title; center using the
same unit-then-city search. Draw status information with argument 1, draw the
local civilization color, and redraw all maps with argument 1.

If `local` is not in `humans`, begin a heartbeat to slot 0's civilization and
show CLIENTHOTWAIT with the appropriate sequential or simultaneous idle
callback (§8). Continue when the modal returns. The simultaneous client starts
with `firstTurn = true`; the sequential loop's similarly named temporary has
no behavioral use. Retain live resume state across initial waits and handoff
restarts. The reconstruction's temporary copies at these immediate boundaries
have no intervening callback and need not become separate logical state.

## 3. Sequential host loop

At the start of each outer iteration:

1. Sync, flush(60000), drain, then compute F04's human wonder ranges.
2. If `resume == 0`, perform F04 global advancement with reset selector −3,
   sync, and flush(60000).
3. Check retirement. If it returns nonzero, or the now-live outcome is nonzero,
   clear `active` and leave the loop.
4. If events are loaded and (`resume == 0` or `resumeNew != 0`), run Events,
   sync, and flush(5000).
5. Visit civilizations 0–7 in ascending order while `active != 0`, as below.

Before each civilization test, set `humans = (humans ∪ pendingHumans) ∩ claimed`,
clear `pendingHumans`, and reload local/player actor. Skip the civilization if
its ID is below unsigned `resume`, or if it is not alive. For an eligible `c`:

1. Reset U04 units for `c` if `resume == 0`, or `resume != c`, or the low byte
   of the takeover flag is nonzero. Even civilization 0 is reset when resume is
   zero. This reset gate is independent of `resumeNew`.
2. Sync, flush(5000), drain, then publish `turnCiv = c`.
3. If `c == local`, perform the local-host phase below. Otherwise, use the AI
   phase if `c` is not human, and the remote-human phase if it is. Local equality
   takes precedence over human-set membership.
4. If the local-host phase requested immediate exit, leave the civilization
   loop. Otherwise redraw status with argument 1 and call N01's lost-connection
   check. Its return value is not used here.
5. If `quit == 0` or `networkTurnCiv == local`, Exhaust(c).
6. Clear resume; clear only the low byte of the takeover flag; clear the game's
   end-turn request; pump UI messages. If inactive, leave. If an outcome is now
   pending, clear active and leave.

Repeat while active. On exit, stop the turn timer with notify 0, invoke N01's
host quit/handoff, and handle a pending outcome. Destroy the prompt. There is
no general restoration of earlier masks, completed upkeep or movement.

### AI phase

Clear `newPlayer[c]`; disable tile sync; publish `networkTurnCiv = c`; update
menus; Caption; send StartWhoseTurn to all with `c`; redraw maps for local with
argument 1; draw `c`'s status color. If `resume == 0` or `resumeNew != 0`, process
F04 civilization upkeep. For a nonzero resume with resumeNew, update title and
flush queued painting immediately before upkeep. Run the AI unit phase, then
enable tile sync.

The takeover flag does **not** force this AI upkeep. Do not import the local
host's extra gate below. These parent loops add no random draw before the AI
child; the child uses its declared F01 sampling law and implementation context.

### Remote-human phase

Capture `c`'s player-slot index first. Publish its network actor, update menus
and Caption. Clear StartCivilization receipt count and `turnEnded[c]`; broadcast
StartWhoseTurn(c); redraw local maps and draw `c`'s color. Send StartCivilization
to the **current connection ID in the captured slot**. A callback changing the
civilization-to-slot mapping does not change this captured destination slot.

If `newPlayer[c]` is nonzero, clear it, then copy the name from the now-current
mapped slot into text argument 0. Broadcast NEWPLAYER using that current slot's
connection ID and show the local NEWPLAYER information popup.

Begin heartbeat(c). While `turnEnded[c] == 0`, EndSequence count is zero, quit
is zero and active is nonzero, poll(1,0), then check heartbeat(c). Clear both
StartCivilization and EndSequence counts after this loop. There is no host-side
civilization upkeep in this phase, and no lost-connection child inside this
particular wait; the enclosing civilization tail calls it.

### Local-host phase

Clear `newPlayer[c]`. If inactive, send UnlockNation to host destination 0 with
arguments `(0, local)`, and request immediate exit. Otherwise Show intro(c).
If limit is nonzero, set `seconds = trunc(limit / 1000)` and invalidate the
caption timer cache. Publish network actor `c`, update menus and Caption;
broadcast StartWhoseTurn(c); update title; redraw local maps; draw `c`'s color.
Capture `delta = s32(casualties[local] - reported[local])`.

If autoplay is zero, perform the turn-start prompt in §7. Then test autoplay
again to choose:

- **Human**: check [P09 throne-room progress](P09-council-and-throne-room.md#7-throne-room-progress-and-admission) if resume is zero. Run upkeep if resume
  is zero, resumeNew is nonzero, or the low takeover byte is nonzero. Immediately
  before such upkeep, a nonzero resume with resumeNew updates title, redraws
  local maps and flushes painting. Run F04 start notices. If the live limit is
  nonzero, start timer with notify 1. Run F04's human movement loop.
- **Autoplay**: save the current human set in shared `savedHumans` and clear it.
  Apply the same upkeep gate and redraw prefix; start timer if limit is nonzero;
  run AI units. Restore the low byte of live `savedHumans`. Query Escape: if
  either its held bit or since-last-query bit is set, set remaining to 1. Decrement
  the remaining word with wrap; if it becomes zero, clear autoplay.

Finally, if the live limit is nonzero, stop timer with notify 1. Return normally.
Callbacks may change which branch the second autoplay test selects. Once a
branch is selected it continues even if its children change autoplay again.

## 4. Sequential client loop

For each iteration, first exhaust local units only if unsigned `resume != local`.
Begin a heartbeat to the current slot-0 civilization. Wait while
StartCivilization count is zero, handoff is zero, EndSequence count is zero,
quit is zero and active is nonzero. Each iteration polls, checks the heartbeat
against the newly read slot-0 civilization, then calls N01's lost-connection
check. A nonzero lost-check result destroys the prompt and returns 0 immediately;
it bypasses the ordinary completion tail below.

After waiting, a nonzero handoff invokes N01 client transfer. A failed transfer
returns 0 after prompt cleanup; becoming host returns 1. If still a client with
no StartCivilization, no EndSequence, no quit and active, restart this iteration
with the current resume state.

If EndSequence count is zero and active is nonzero, perform the local phase.
This gate does not additionally require quit to be zero:

1. Clear StartCivilization count. Reload local and publish player/turn actors.
   If unsigned resume differs from local, reset local units through U04.
2. If limit is nonzero, initialize seconds and invalidate the caption cache.
   Update title, redraw local maps, draw status information and local color.
   Capture the casualty delta; if autoplay is zero show the client turn prompt.
3. Test autoplay again. For a human, check throne-room progress if resume is
   zero. If resume is zero or resumeNew is nonzero, perform local upkeep and
   sync. Then run start notices, Show intro(local), start timer if limit is
   nonzero, and run human movement.
4. For autoplay, save and clear humans, run upkeep under that same resume gate,
   start timer if limit is nonzero, run AI units, restore humans from shared
   savedHumans, and query Escape. Escape sets remaining to 1. This path performs
   **no remaining decrement, no automatic autoplay clear and no explicit sync
   immediately after upkeep**.

If the local phase gate fails, skip it and proceed to the same ordinary tail:

1. Stop timer with notify 1 unconditionally; redraw status; pump UI messages.
   If outcome is nonzero, clear active.
2. Sync; send TurnFinished to destination 0 with local civilization; flush(60000).
3. Clear resume. If active, repeat. Otherwise send QuitRequest to destination 0
   with local, flush(60000), shut down the network with argument 1, handle a
   pending outcome, destroy the prompt and return 0.

EndSequence alone does not directly clear active or bypass this completion tail.
Without an accompanying state change, repeated iterations may skip movement
and still publish completion. There is no fabricated one-shot latch.

## 5. Simultaneous host loop

After host initialization, enter this outer iteration:

1. Merge pending humans into humans and clear pending, **without intersecting
   claimed yet**. Clear all eight N02 map-lock rows to their unlocked sentinel.
   Reset units with selector −3. Sync, flush(60000), drain, compute human wonder
   ranges. If resume is zero, advance globally with selector −3, sync and
   flush(60000). Thus a normal iteration can reach both −3 reset sites.
2. Check retirement/outcome and Events exactly as in the sequential host prefix.
3. Set humans to `(humans ∪ pendingHumans) ∩ claimed`, then clear pending.
   Disable tile sync. Scan `c = 0..7` while active: publish turnCiv for every
   scanned ID, and for each alive AI civilization perform upkeep and sync.
   This AI upkeep is **not gated by resume**. Enable tile sync; flush(60000).
4. Reset all 169 N02 receipt counters, not only phase counters. Clear all eight
   phaseDone entries. Send ProductionBegin to all. Reload local and publish
   player/turn actors. Check throne-room progress if resume is zero; always
   run local upkeep, including on a resumed turn. Sync; increment phaseDone[local].
5. Install the host production-wait idle callback and show WAITPRODUCTION.
   After it returns, execute the explicit phase-completion wait below. Clear
   ProductionComplete receipt count; sync; flush(60000).
6. Broadcast AIMovement. Reload local/player actor. Disable tile sync. If resume
   is zero, reset units with selector −1. Scan alive AI civilizations 0–7 while
   active. For each, publish turnCiv and networkTurnCiv, update menus and draw
   its color. If resume is nonzero and resumeNew is nonzero, update title,
   redraw local maps and flush painting. Run AI units. There is no upkeep here.
7. Clear all eight phaseDone and newPlayer entries. Sync; broadcast HumanMovement;
   flush(60000). Publish turnCiv = local and enable tile sync. If inactive,
   send UnlockNation to destination 0 with `(0, local)` and leave the loop.
8. Perform the simultaneous local-host movement phase below. Redraw status;
   sync; clear resume; clear the game's end-turn request; increment phaseDone[local];
   flush(60000).
9. Install the host human-wait idle callback and show WAITHUMANMOVES. Execute the
   explicit phase-completion wait after it returns. Clear HumanMoveComplete
   receipt count; run N01 lost-connection checking.
10. If quit is zero or networkTurnCiv equals local, perform Exhaust(c), where
    `c` is the **retained completion-scan index**, as described below. Pump UI.
    Leave if inactive; otherwise clear active and leave if an outcome is pending.

Repeat while active. Use the same stop(0), host quit/handoff, game-over and prompt
destruction tail as the sequential host.

### Explicit phase-completion wait

The parent carries a civilization scratch variable through its scans. The
completion helper receives that variable by reference:

```text
repeat:
    poll(1, 0)
    if signed playerCount < 2:
        return without modifying c
    for c = 1 through 7:
        if c is alive and human and phaseDone[c] == 0:
            restart repeat
    c = 8
    return
```

Poll at least once even when all players are already done. There is no active,
quit, outcome, heartbeat or elapsed-time gate. The modal wait does not replace
this explicit second check. A positive or negative nonzero completion count
satisfies the scan; a wrapped zero does not.

Before the final human barrier, the parent's phaseDone/newPlayer clearing scan
has left `c = 8`. A completed scan also leaves 8; a scan waiting for a player
may leave that player's ID, and a later playerCount-below-2 exit preserves it.
The final cleanup passes this retained value to Exhaust. Usually no ordinary
unit has owner 8, so no unit is exhausted there, but all active cities still lose
Investigated. Do not substitute local civilization. This safe comparison quirk
is not an out-of-bounds write and remains in the compatibility contract.

### Simultaneous local-host movement

Show intro(local). Initialize seconds/cache if limit is nonzero. Publish local
network actor, update menus and Caption, update title, redraw local maps and
draw local color. **Do not broadcast StartWhoseTurn here.** Capture casualty
delta; if autoplay is zero perform the host turn prompt, then test autoplay again.

For a human: run start notices, start timer if limit is nonzero, run human moves.
For autoplay: save/clear humans, start timer if limit is nonzero, run AI units,
restore humans, process Escape, decrement remaining with word wrap and clear
autoplay if it reaches zero. Stop timer with notify 1 if live limit is nonzero.
There is no upkeep or throne-room check in this movement helper: those occurred
before the production barrier.

## 6. Simultaneous client loop

After client initialization, each iteration first exhausts local units **even
when resume equals local**. Except on the first iteration, begin a heartbeat to
slot 0's civilization and show WAITHUMANMOVES with the client human-wait callback.
Then begin another heartbeat and wait for ProductionBegin, using the same
handoff/EndSequence/quit/active gates and lost-check early returns as the
sequential client's StartCivilization wait. Successful transfer to host returns
1; successful transfer remaining a client can restart the iteration.

If EndSequence count is zero and active is nonzero:

1. Clear both StartCivilization and ProductionBegin counts. Reload local and
   publish player/turn actors. Initialize seconds/cache if limit is nonzero.
2. Drain both queues. Publish local network actor; update title; redraw local
   maps; draw status information and local color.
3. Select human or autoplay once from the current autoplay flag. Run the
   corresponding helper below. An early lost-connection return from a helper
   destroys the prompt and returns 0 from the parent without the completion tail.

Otherwise skip the local helper. The ordinary tail is the sequential client's
stop(1), redraw, UI pump, outcome check and sync, but sends **HumanMoveComplete**
to host destination 0 with local, then flushes(60000). Clear resume and firstTurn.
If inactive, send QuitRequest, flush, shut down, handle outcome, destroy the
prompt and return 0; otherwise repeat. The local `turn_ok` temporary written by
skipped helpers is not tested by the parent and does not independently stop it.

### Human helper

1. If resume is zero, check throne-room progress. If resume is zero or resumeNew
   is nonzero, run local upkeep and sync. Always flush(60000), then run start
   notices. Send ProductionComplete to destination 0 with local.
2. Begin heartbeat to slot 0; install the client production-wait idle callback
   and show WAITPRODUCTION. Then explicitly wait while AIMovement count is zero,
   EndSequence count is zero and active is nonzero. Each pass polls, checks the
   heartbeat and calls lost-connection checking; nonzero loss returns early.
3. If EndSequence count is zero and active is nonzero, clear AIMovement count,
   begin another heartbeat, install the client AI-wait callback and show
   WAITAIMOVES. Explicitly wait for HumanMovement with the same three gates and
   poll/heartbeat/loss sequence.
4. If this second continuation still has no EndSequence and is active, clear
   HumanMovement count, Show intro(local), capture casualty delta and show the
   client turn prompt if autoplay is currently zero. Start timer if limit is
   nonzero, then run human movement. The already-selected human helper remains
   selected even if the introduction or prompt changed autoplay.
5. If the **second** continuation gate fails, clear EndSequence count. If the
   first gate failed instead, leave it unchanged. Neither branch adds a quit
   request itself; the parent ordinary tail decides that from active.

### Autoplay helper

Save humans in shared savedHumans, then clear humans. Run local upkeep if resume
is zero or resumeNew is nonzero. There is no explicit upkeep sync, flush or
start-notice call here. Send ProductionComplete; begin heartbeat; show
WAITPRODUCTION with its callback; explicitly wait for AIMovement with the same
poll/heartbeat/lost-check sequence as the human helper.

If EndSequence count is zero and active is nonzero, clear AIMovement count,
start timer if limit is nonzero and run AI units immediately. Restore humans
from the low byte of live savedHumans and process Escape, setting remaining to
1 when pressed. **Do not wait for HumanMovement, decrement remaining, or clear
autoplay.** If the continuation gate fails or lost checking returns early,
humans remain zero: restoration is inside the successful branch. An implementation
must not add an unconditional restoration as ordinary classic behavior.

## 7. Turn prompts and timeout callbacks

The turn prompt first installs the turn-popup timeout callback and publishes
leader title/name as text arguments 0 and 1. The casualty delta was captured
before these calls. Host paths decide whether to show casualties using a fresh
signed comparison `reported[local] < casualties[local]`; clients instead use
`delta > 0`. These tests differ after scalar wrap or callback changes.

For a casualty prompt, assign reported from the live casualty count; publish
the captured delta as number argument 0. Select CASUALTY for delta exactly 1,
otherwise CASUALTIES. For the other branch select OURTURNTOMOVE. Load that GAME
text section without flags; add the information sprite; set image zoom 0;
flush input events; play the turn-notice sound; show the modal. Only the casualty
branch opens the casualty timeline when the dialog's live extra result is
nonzero. P04 owns resource interpolation, response and rendering.

The idle callback polls(1,0), reads the clock, and closes the active modal if
`s32(now - popupTick) > 1200`. Closing sets the modal completion flag, ends its
loop, then stores a fresh clock sample in popupTick. The callback does not itself
initialize popupTick on entry or check active/quit; modal setup supplies the
initial value. Do not replace this strict comparison with `>= 1200`.

## 8. Phase-wait idle callbacks

Each callback polls(1,0) first. Client callbacks then check the heartbeat against
the freshly read slot-0 civilization. When its close condition is true, set the
active modal's completion flag and end its modal loop; do not infer a turn
completion message, count clear or return value from this alone.

| Callback | Additional work and close condition after polling |
| --- | --- |
| Host production wait | Only when ProductionComplete receipt count is nonzero or signed playerCount < 2, scan alive human civilizations 1–7. Close only if every phaseDone is nonzero. Even a small roster must pass this scan. |
| Host human wait | Gate the scan on HumanMoveComplete receipt count nonzero or playerCount < 2. Close if all scanned civilizations are done **or a fresh signed playerCount read is < 2**. |
| Client production wait | AIMovement count nonzero, or EndSequence nonzero, or inactive, or lost nonzero. |
| Client AI wait | HumanMovement count nonzero, or EndSequence nonzero, or inactive, or lost nonzero. |
| Client human wait | ProductionBegin nonzero, or handoff **exactly 1**, or EndSequence nonzero, or quit **exactly 1**, or inactive, or lost nonzero. |
| Client initial simultaneous hot wait | ProductionBegin nonzero, or any nonzero handoff/EndSequence/quit/lost, or inactive. |
| Client initial sequential hot wait | StartCivilization nonzero, or any nonzero handoff/EndSequence/quit/lost, or inactive. |

The host callbacks do not run heartbeat checking. Their scans and the explicit
host completion wait are different algorithms; neither may be replaced by the
other. Receipt counts are not a per-sender barrier set. N02 dispatch increments
phaseDone for ProductionComplete and HumanMoveComplete, and turnEnded for
TurnFinished. HumanMoveComplete additionally exhausts the indicated civilization's
units. Duplicate messages can increment again, and counter wrap can reach zero.

## 9. Heartbeats and drag holds

`beginHeartbeat(c)` performs:

1. If drag hold is zero, clear the shared HeartBeatAck count and send HeartBeat
   to the live connection ID mapped from `c`. Otherwise set the ACK count to 1
   and send nothing.
2. If the parley window's closed field is **exactly 1** and the popup queue head
   is null, clear pending multiplayer meeting state and the ParleyRequestCancel
   receipt count.
3. Store a fresh clock sample as heartbeatTick.

`checkHeartbeat(c)` reads the clock. If `s32(now - heartbeatTick) > 3599`, then:

- If ACK count is zero, increment turnEnded[c]. On a client, also increment
  lost. Leave heartbeatTick and ACK unchanged. Every further expired check can
  increment again; there is no one-shot failure flag.
- Otherwise call beginHeartbeat(c), which sends a new request or synthesizes an
  ACK during a drag hold and starts a new interval.

A nonzero ACK count is shared, not correlated with the requested civilization
or a per-request nonce. N02's immediate heartbeat receive/ACK path owns incoming
response ordering. N01 owns how accumulated connection errors remove players
or trigger transfer. The nominal 3600-clock-tick threshold is 60 seconds at
F01's 60 Hz clock; signed wrap can produce a negative elapsed value.

### Drag watchdog boundary

P05 supplies interaction begin/end and input delivery. Preserve these logical
hold transitions even when the interface uses no native drag hook:

- Begin sets drag-active, captures startTick, clears the secondary flag and
  records the active interaction target. A host broadcasts DragHoldOn and
  flushes(5000), then enables input observation for the watchdog.
- End clears active/start/secondary/target, disables that observation, then a
  host broadcasts DragHoldOff and flushes(5000).
- An observed input invokes the watchdog tick when drag is active.
- Starting the watchdog does nothing outside network mode. Otherwise cancel
  any registered watchdog, request a callback timer with a 500 ms interval and publish the
  resulting registration, including absence on failure. Stopping cancels a
  registered watchdog and clears its registration.
- A tick tests network mode, playerCount != 1 and active drag before reading
  the clock. If `s32(startTick + 620) < now` and quit is zero, release the hold.
  This is a wrapped deadline comparison, not heartbeat's elapsed subtraction.
  Then start the watchdog regardless of whether release was needed.
- Release does nothing when inactive. With a recorded target, enqueue its
  interaction-completion input, then release its capture/hold. Clear drag-active
  afterward. Do not clear other drag fields or send DragHoldOff here.

N02 congestion handling can also release the hold. Enqueuing completion is
separate from executing its eventual handler; preserve that ordering. Native
mouse messages, hook handles and timer APIs are reference mechanisms in [Appendix D](#appendix-d-original-implementation-reference).

## 10. Turn timer operations

Let **close windows** mean: close city window, end intelligence-report modal,
close throne room, end Civilopedia modal, in that order. Select the sequence
once; actor changes during a child do not cancel its remaining operations.
**Publish timer** means: cancel the live registration if present, request a
callback timer with a 500 ms interval, then publish its success/absence. Registration is
logical state distinct from whether a callback is already queued. Cancellation
alone does not prove an already delivered callback cannot run. Native handles
and timer flags are recorded in [Appendix D](#appendix-d-original-implementation-reference).

### Start, stop and resume

`startTimer(notify)`:

1. If networkTurnCiv equals local, close windows.
2. Set seconds to signed `trunc(limit / 1000)`; clear the entire blink scalar;
   invalidate the timer-caption cache and refresh the timer information.
3. Publish timer, then record a fresh clock in lastTick.
4. If notify is nonzero and the live mode is network, broadcast StartTurnTimer,
   even if scheduling failed.

`stopTimer(notify)`:

1. If networkTurnCiv equals local, close windows, even with no registration.
2. If registered, cancel the live registration, then clear it, discarding any
   replacement published during cancellation. Clear the displayed progress.
3. Within that registered branch only, if notify is nonzero, mode is network
   and human movement is active, broadcast StopTurnTimer. Read those gates
   after cancellation/refresh. Do not change seconds or blink.

`resumeTimer(notify)` unconditionally closes windows. If seconds and timerShown
are both nonzero, publish timer, record a fresh lastTick and broadcast
RestartTurnTimer when notify is nonzero and live mode is network. Preserve
seconds, blink and caption cache; negative seconds satisfy the gate. Otherwise
resume neither cancels nor schedules a timer.

### Tick

The timer callback is:

```text
if live limit == 0: return
blink = u32(blink XOR 1)
if the XOR result is nonzero:
    if live seconds != 0:
        elapsedTicks = s32(clock() - live lastTick)
        elapsed = truncTowardZero(x87(elapsedTicks / 60.0 + 0.5))
        seconds = s32(live seconds - elapsed)
        if that subtraction result is negative: seconds = 0
        lastTick = clock()
    if live seconds == 0:
        close windows
        humanMovementActive = false
        clearDisplayedProgress()
        return
    refreshTimerInformation()
percent = truncTowardZero(s32(live seconds * 100000) / live limit)
showProgress(percent)
publish timer
```

Use F01's x87 evaluation/truncation contract, not an integer division performed
before adding one half. The clock subtraction wraps before conversion. For
positive elapsed ticks, 29 rounds to 0 and 30 rounds to 1; negative elapsed is
also truncated toward zero after addition, so this is not symmetric rounding.
A clock callback can change seconds or lastTick before their later reads.

The first blink branch is selected from the XOR's result. The original uses the
full scalar, so accepted upper-bit values can make both alternating states
nonzero. Tick does not normalize such input to a Boolean.

Expiry returns without destroying or clearing the retained registration, updating its
visibility flag or scheduling another timer. The limit-zero early return also
does none of those operations. The next outer turn tail may subsequently stop
that retained registration. Percentage multiplication wraps; there is no caller clamp
to 0–100. A callback can change limit to zero before the later signed division;
zero divisors and an unrepresentable signed quotient are arithmetic fault
boundaries, not a required invented percentage. A restrictive profile may stop
safely after prior effects.

Original colors, drawing calls and native timer lifecycle: [Appendix D](#appendix-d-original-implementation-reference).

### Received commands and changing the timer

N02 owns receiving Start/Stop/Restart/TimerChange and Hold/Continue. Its local
calls use the start/stop/resume contracts here. In particular, stopping and
resuming around a hold preserves seconds, while starting resets seconds from
the full limit. Do not merge these commands into one toggle.

The local Change Timer command stops with notify 1, queries Escape and discards
its return, then invokes the timer chooser in in-game mode (§11). On cancellation,
resume with notify 1. On acceptance, a zero limit clears timerShown and redraws
and presents the caption; a nonzero limit sets timerShown and starts with notify
1. Then, in network mode, broadcast TimerChange with the **live** limit. Thus a
nonzero accepted change can send StartTurnTimer before TimerChange. Finally, if
autoplay is nonzero, clear humanMovementActive regardless of acceptance.

## 11. Timer selection and option votes

### Timer chooser

Construct the chooser prompt; capture the original remembered choice and limit.
At every outer retry, restore the remembered choice to that captured choice.
Load GAMETIMER, set its default selection and position, then show it. During
initial setup with resumeNew zero, first show the setup side picture and use
setup positioning; otherwise use centered positioning. A negative result closes
the setup picture in setup mode, destroys the chooser and returns cancellation.
It does not provide a general rollback of effects from prior child callbacks.

Store the accepted choice as a signed word. Ordinary choices are:

| Choice | New limit |
| --- | --- |
| 0 | 0 |
| 1 | 30000 ms |
| 2 | 60000 ms |
| 3 | 120000 ms |
| 4 | 180000 ms |
| 5 | 300000 ms |
| 6 | Custom input loop below. |

An accepted other choice leaves the current limit unchanged. For custom input,
load GAME/CUSTOMGAMETIMER using the remembered custom text. A failed text-section
load returns cancellation, closing the picture in setup mode. Position using
setup positioning whenever in setup mode, irrespective of resumeNew; otherwise
center. Run the text modal, then play the menu-accept sound even when its result
is negative. A negative result closes the setup picture if applicable and
restarts the outer chooser.

If an accepted text modal produced nothing, retain any previously produced text
from this invocation; first use needs supplied retained bytes or a bounded stop,
not an assumed empty string. Parse accepted text with [F03's integer parser](F03-rules-and-configuration.md).
For a negative result, toggle the low bit of the autoplay enable word, set the
remaining word to the negation of the parsed value narrowed to a signed word,
and repeat custom input. This is an autoplay control, not an accepted negative
time limit. For values 10–3600 inclusive, set limit to value × 1000 and accept.
All other nonnegative values repeat custom input. The sequence can run without
termination if every supplied choice repeats or cancels the inner prompt.

After acceptance in setup mode, close the side picture. Copy the custom text to
the remembered preference only if the **latest modal result is exactly 6**. The
custom text modal overwrites the original choice temporary, so ordinary text
acceptance returning 0 does not perform this copy. Return success. In-game
acceptance never performs that custom-preference copy.

For in-game acceptance with signed playerCount > 1, prepare a vote. Text argument
0 is the local player name. Argument 1 is the no-limit label when limit is zero;
otherwise format minutes and seconds from signed truncating divisions of
`limit / 1000`. Prepend `0` to each component less than 10, using the minute/second
separator between them. This also describes accepted off-nominal negative
formatting; do not silently apply an absolute value.

Set votePending to wrapping playerCount−1 and voteStatus to 0. Broadcast the
timer proposal with both strings; install the vote idle callback; capture
popupTick; show NEWTURNTIMERSERVER. After the modal, reject if its result is
negative, voteStatus is −1, or votePending is **anything other than zero**.
Rejection restores captured choice and limit, broadcasts the negative result,
shows NEWTURNTIMERNO and returns cancellation. Acceptance broadcasts the positive
result, shows NEWTURNTIMERYES and returns success. There is no vote when the
player-count gate fails. Destruct the chooser on normal return.

### Shared vote callback

Poll(1,0). Continue waiting only if handoff is zero, then
`s32(clock() - popupTick) < 2401`, then votePending is nonzero, then voteStatus
is not −1, evaluating these tests in that order. Otherwise set the modal
completion flag, end its loop and set popupTick from a fresh clock sample.

A nonzero handoff skips the first clock read. No quit, lost or active gate is
added. Negative votePending values still wait until another exit condition;
modal return with negative pending rejects the proposal. N02's reply dispatch
owns the decrement and rejection assignments. Neither this callback nor the
proposer deduplicates replies by player or counts current roster membership
anew after beginning the vote.

### Double-production and double-movement proposals

[F03's accepted-option contract](F03-rules-and-configuration.md#accepted-in-game-option-dialogs)
owns checkbox application, preference-save timing and the double-movement rule
transformation. This section owns the surrounding proposal and publication.

Initialize the two clear-chat boxes from preferences. Initialize production and
movement boxes from live settings only if the host flag is set. Network clients
use MULTIPLAYEROPTIONS2; other callers use MULTIPLAYEROPTIONS. Only dialog result
exactly 0 applies changes. The two chat preferences are saved first. Then a
network host, or any mode below 3, reads and normalizes the two double-setting
boxes into remembered preferences. A network client stops after the chat part.
If both proposed double settings already equal the live settings, finish.

For a changed proposal in network mode with playerCount > 1, prepare text
argument 0 from the local name and argument 1 from the production on/off label,
separator and movement on/off label. Set votePending = playerCount−1 and
voteStatus = 0; broadcast the proposal; install the same vote callback, capture
popupTick and show PMCHANGESERVER. Reject on a negative modal result, status −1
or nonzero pending count. Restore both remembered double preferences from their
live settings; broadcast the rejection, show PMCHANGENO and return. Earlier chat
preference saving remains done.

Otherwise install both proposed double settings and invoke F03's movement
transformation. With the live network/player-count gates still satisfied, send
the Units table to all, send MultiOptionsChange with signed production, signed
movement and the **full signed simultaneous-setting word**, then broadcast
acceptance and show PMCHANGEYES. There is no second preference-save call after
installing the double settings. This menu does not edit simultaneous mode or
choose another running turn loop.

## 12. Determinism, failure and conformance cases

These sequencing, heartbeat and timer parents make no direct random decision.
The host owns global advancement, turn-event passes and nonhuman AI phases.
Each human peer runs the local upkeep/movement calls selected by its own loop;
simultaneous autoplay retains the different schedule above. F04, S02, U04 and
AI/presentation children own their decision rules and state-dependent order.

Mixed sessions with unmodified original-game peers are required, with either
implementation hosting. Preserve the peer assignments above and each child's
network authority rules. A different local generator may select different
outcomes at operations executed by that peer; transmit/apply their effects
through the existing N02 commands, results and state publications. Do not rerun
an already resolved remote choice locally or move a simulation phase to another
peer to simplify random handling. This is not a requirement for every peer to
execute an identical random sequence.

Original loops do not imply a barrier reseed, synchronized turn seed or rollback
after disconnection. No new RNG-state message, policy-version handshake or shared
seed protocol is required of original peers. Presentation may use an independent
local context, but its real callbacks still follow the original ordering.

On join, reconnect or host transfer, preserve the original state, role, resume
and pending-work transitions. A cleanroom process continues or initializes its
own random contexts under its documented policy. It must not require the old
host's RNG state or repeat already applied simulation work. Future random
outcomes may change after authority transfers between engines; agreed published
game state and the protocol's ordering/barrier guarantees may not.

N02 publication and delivery/callback order remain replay inputs. For local
reproducibility, capture external original-peer messages/results rather than
requiring access to their RNG state. Original-peer interoperability is a required
integration target still awaiting actual mixed-peer validation; new-engine-only
sessions and matching packet layouts alone do not establish it.

Nominal conformance traces should include all four roles, a new and resumed turn,
local and remote humanity, at least one AI civilization, a turn-time limit,
several phase messages and a handoff. Record ordered child calls, sync/flush
calls, messages, roster/count changes, timer creation/destruction, UI selections
and return values. Do not compare only final state. Repeat with an original
host/cleanroom client and the reverse, in sequential and simultaneous modes.
Include save/resume, running join/reconnect, authority transfer in both directions,
AI phases, scenario events and randomized commands. Require convergence of
published fields at the applicable synchronization points, without comparing
private RNG contexts or requiring the same random outcomes as a separate run.

| Case | Required observation |
| --- | --- |
| Sequential host at resume 0, civilization 0 | Its per-civilization reset still runs; global advance and later AI upkeep have their own gates. |
| Sequential takeover on an exactly resumed local turn | Unit reset and local upkeep can be forced; the same flag does not force an AI civilization's upkeep. |
| Simultaneous host resumes | Initial −3 reset and eligible AI/local upkeep still occur; global advancement and pre-AI −1 reset are skipped. |
| Completed host barrier | Poll at least once; full scan leaves index 8, later owner comparison exhausts no nominal unit, but clears active-city Investigated flags. |
| Roster drops below two while phaseDone is zero | Production modal can remain open; human modal can close; explicit wait returns without rewriting its incoming scan index. |
| Handoff and quit values 2 in a client human wait | They do not satisfy its exact-1 gates; the initial hot-wait callbacks do accept any nonzero values. |
| Remote slot mapping changes during StartWhoseTurn | StartCivilization still uses the captured slot, with its live connection ID; NEWPLAYER and heartbeat use their own fresh mappings. |
| Client autoplay and Escape | Set remaining to 1; no client decrement or autoplay clear is added. Simultaneous autoplay moves after AIMovement without waiting for HumanMovement. |
| Simultaneous client autoplay encounters EndSequence before moving | Previously cleared human set remains cleared; the parent ordinary tail still depends on active. |
| No ACK at 3599 versus 3600 ticks | First does nothing; second increments completion/failure. Repeated checks at 3600 increment again without resetting the deadline. |
| Vote pending at 2400 versus 2401 ticks | First remains open; second closes, and any nonzero pending value causes proposal rejection. |
| Stop with absent registration and matching actor | Close the four windows, but do not clear progress or send StopTurnTimer. |
| Start timer scheduling fails | Publish absent registration, still update lastTick and send the notification when its live gates pass. |
| Resume timer | Preserve seconds and blink; always close windows, but schedule only for nonzero seconds and shown timer. |
| Tick elapsed 29 versus 30 | With an active blink branch, subtract 0 versus 1 seconds. Refresh lastTick using a second sample. |
| Countdown reaches zero | Close windows, clear movement activity and displayed progress; retain registration and do not schedule another timer. |
| 20 seconds left, blink becomes zero | Draw the colored progress bar; hide it only on the nonzero blink branch. This distinguishes original behavior from the primary tick reconstruction. |
| Custom timer accepted with modal result 0 | Accept its numeric limit but leave remembered custom text unchanged; the original copy tests the latest modal result for exactly 6. |
| Negative custom timer input then cancellation | Preserve the autoplay toggle/remaining mutation; restarting the chooser restores the captured selection, not every earlier effect. |
| First custom text modal accepts without producing text | Require supplied original retained bytes or stop at the parser read. Do not presume an empty original buffer. |

No barrier timeout, cancellation rollback or automatic mask restoration may be
added merely to guarantee termination. A replacement may offer a separately
declared more restrictive/safe profile, but must distinguish it from the classic
continuation. Native callback failures may interrupt cleanup; normal prompt
destruction is not a transaction reverting game/network state.

## Appendix A. Encodings and original presentation parameters

These details support original-peer interoperability and validation. They do
not require equivalent private layouts in a new implementation.

| Item | Original representation or value |
| --- | --- |
| Civilizations and sets | Eight civilizations; alive/human/pending sets are bytes. Player slots and connection IDs follow N01/N02. |
| Resume and activity | Runtime resume/activity are bytes; saved resumed-new is a signed word promoted into a runtime scalar. Host resume comparisons use the unsigned byte; client zero tests are independent of its sign. |
| Autoplay | Enable and remaining are words at `00654fa8`/`00654faa`; save-human scratch is a DWORD, restored through its low byte. |
| Timer state | Handle `00633a74`, signed seconds `00633a78`, clock `00633a7c`; signed millisecond limit `00654b70`; full blink DWORD `006ab5ac`. |
| Host phase completion | The loops clear/scan eight signed wrapping DWORD counters at `006ced20`; a larger reconstruction declaration does not authorize more civilization IDs. |
| Packet counters | 169 DWORD receipt counters. See N02's complete message catalog for byte encodings, payloads and receive side effects. |
| Named messages used here | StartCivilization 22; Units 27; HeartBeat 43; HeartBeatAck 44; QuitRequest 45; UnlockNation 49; MultiOptionsChange 51; DragHoldOn/Off 93/94; StartWhoseTurn 105; EndSequence 107; TimerChange/Start/Stop/Restart 108–111; ProductionBegin/Complete, AIMovement, HumanMovement/Complete 139–143; TurnFinished 162. |
| Popup bindings | N02 Appendix D owns binding/payload definitions: NEWPLAYER 63; timer proposal/no/yes 64–66; production/movement proposal/no/yes 67–69. |
| Vote reply interpretation | N02 owns kinds 86/87: receipt decrements pending, and reply argument 2 writes rejection status −1. |
| City cleanup | Investigated is flag `0x00400000`; other city flags are retained. |
| Caption | Main frame edges are at offsets `0x2ac..0x2b8`, then copied bottom is replaced by client top at `0x2c0`. Cache invalidation stores −1. |
| Progress colors | Red `0x6a`, yellow `0x7a`, green `0x2a`; no color −1. The P02 drawing child owns pixel work and its own callbacks. |
| Clock constants | IEEE double constants at `0061d6e8`/`0061d6f0`: 60.0 and 0.5. Thresholds: heartbeat >3599; vote <2401 to continue; turn popup >1200 to close; drag deadline start+620. |
| Modal completion | OR flags with `0x400`, then end the current modal window loop. Wait callbacks re-read the live active dialog when obtaining that receiver. |
| Prompt construction and flags | Turn dialogs use allocation argument `0x4000`; timer chooser `0x2000`. Initial client hot wait uses `0x2000000`; server vote uses `0x2000001`; timer-choice and multiplayer-option menus use 1; custom text uses resource flags 4. |
| Timer chooser positioning | Center sentinel is −999 in each coordinate. Setup position is x=−999, y=`s32(-(trunc(s32(screenHeight-480)/8)+1))`. Initial side picture uses arguments (−1,0). |
| Timer custom buffer | Private text capacity 16 bytes; remembered choice at preference offset `0x98`, remembered text starts `0x9a`. Accepted text copies/parser reads still require backing and a terminator. |
| Timer display label | No-limit label `0x285`; minute/second separator `:`. Production labels select `0x350 - (proposedProduction == 0)`; movement `0x352 - (proposedMovement == 0)`, separated by the separator ` / ` (space, slash, space). |
| Sound requests | Turn prompt plays NEWONDER (48); custom text acceptance/cancellation plays MENUOK (106), both with remaining arguments zero. |
| Input | Escape query is key `0x1b`, with result mask `0x8001`. Drag mouse hook kind is 7, thread ID 0; posted left-button-up message is `0x202` with zero parameters. |
| UI resources | GAME sections: GAMETIMER, CUSTOMGAMETIMER, CLIENTHOTWAIT, WAITPRODUCTION, WAITAIMOVES, WAITHUMANMOVES, OURTURNTOMOVE, CASUALTY, CASUALTIES, NEWPLAYER, NEWTURNTIMERSERVER/NO/YES, MULTIPLAYEROPTIONS/2, PMCHANGESERVER/NO/YES. N02/P04 own shared remote bindings and resource interpretation. |

### Original discrepancies and excluded unsafe behavior

The pinned primary reconstruction clears only the first blink byte in
`mp_turn_timer_start`. Original instruction `0055af6e` clears the full DWORD.
Its handwritten tick uses a pointer-valued operand in the warning-color test,
which behaves as a nonzero address rather than the blink value. Original
`0055b264` compares the full blink DWORD to zero. The specification uses those
original behaviors; neither discrepancy is an intentional gameplay change.

The original caption path copies all four frame edges before replacing bottom.
The reconstruction omits the discarded fourth read. For established ordinary
window backing this read has no side effect and cannot alter the presented
rectangle, so it can be folded. A supplied invalid backing/fault scenario needs
the original access boundary; the fold is not evidence that all invalid reads
are irrelevant.

The timer chooser reuses its selection temporary for the custom text modal's
result. After success in setup, the copy condition tests **that latest full
result for 6**, not the remembered menu choice. Original `0051ee2e` confirms the
comparison; normal text acceptance returning 0 does not update the remembered
custom text. This is a scalar control-flow quirk and remains in scope.

The independent reconstruction's BF-039 seeds the first byte of the private
custom buffer to NUL. It documents that failed EDIT creation can allow accepted
buttons with no produced text. This initialization is an intentional repair,
not original evidence. Later attempts can retain earlier produced text; first
use with no producer requires supplied retained input or a bounded stop. An
unterminated parse/copy is likewise not an implicitly empty string.

Unbounded player-name, proposal-text or custom-preference copies can overrun
original destinations; malformed civilization indices can overrun completion
or shown-state arrays. These writes and their resulting adjacent-state damage
are excluded bugs. Retain preceding valid sends, dialogs, mask changes and
scalar operations when a continuation stops at such a write. A declared safer
profile may reject earlier. This exclusion does not authorize treating unknown
input bytes as zeros or removing safe wrapped counter/owner comparisons.

## Appendix B. Evidence and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary source pin:
`a71e8aefffe04e6f719e0e213b027b128a747711` in the external OpenCiv2 repository.
The researched checkout was `fc2155ccdb2123c1137babab6fa39e38152278c9`; all 46
inventoried complete bodies, relevant headers/state declarations and the ten
compiled fixture literals were checked unchanged against that pin.

| Contract | Source navigation |
| --- | --- |
| Sequential host/client, local/AI/remote helpers, turn popup, timer chooser and outer application dispatch | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): `0048bfec`, `0048c9f3`, `0048237d`, `0051ea8e`. |
| Simultaneous host and its split helpers | [bigframes.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/bigframes.cpp): `005aa0e5`. |
| Simultaneous client/helpers, wait callbacks, heartbeat, votes, timer start/stop/resume and drag watchdog | [network.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/network.cpp): `005ab2d5`, waits `005a9f30`/`005ab0a3` onward, heartbeat `0048d9ad`/`0048da51`, vote `0051ea0c`, timer `0055ae80`/`0055af2e`/`0055b046`, change `0055b2c6`, watchdog `0055b5fa`. |
| Timer tick with the stated reconstruction discrepancy | [timer_display.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/timer_display.cpp): `0055b0fb`. |
| Double-setting proposal and publication | [window.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/window.cpp): `004e0f18`. |
| Named state/literals | [network state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/network.h), [game state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/game.h), [map state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/map.h), [literals](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/gamedata.cpp). |

Independent reconstruction pin: `4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752` in
`civ2`. Relevant files are `src/network_server_turns.cpp`,
`src/network_turns.cpp`, `src/application_timer.cpp`, `src/network_settings.cpp`
and `analysis/reference/bugfix-log.md` BF-039. Their production/backing stops
and explicit repairs must not be mistaken for original initializers or validators.
They corroborate host/client distinctions, full timer arithmetic, callback reads,
retained scan state, vote behavior and custom-output boundaries.

Original executable: MGE **5.4.0f Multiplayer 26-March-99**, patch 3; SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Direct instruction review checked the complete timer tick and focused timer-start,
custom-copy and host-cleanup sites. Six byte probes retain the full blink reset,
full blink XOR/test, live signed percentage division, signed owner comparison
against the retained scan variable, and custom-result-equals-6 test. The two
floating constants were checked in the PE data image. This is direct binary
evidence for those particular details, not execution of all four loops.

A temporary Linux fixture compiled **25 complete unchanged primary bodies**, plus
one separately identified original-corrected timer tick, and passed **55 assertions**
in optimized and AddressSanitizer/UndefinedBehaviorSanitizer builds. LeakSanitizer
was disabled for the execution environment. The unchanged set includes the
simultaneous host parent, its actual AI/barrier/cleanup helpers, both simultaneous
client phase helpers, sequential AI/remote helpers, all seven phase idle callbacks,
heartbeat/vote/popup callbacks, timer start/stop/resume/change, message-count reset
and the actual unit-exhaust child. The tick's corrected color predicate is not
counted as an unchanged extracted body.

The fixture uses explicit logical-state, native timer, clock, modal/window,
network send/poll, simulation, upkeep, AI/human movement and connection adapters.
The simultaneous host's initialization and local movement helper are adapters;
the complete sequential host and complete client outer loops were source-reviewed,
not executed by this fixture. The timer chooser, option menu and native drag
path were reviewed but are not compiled fixture coverage. Tested starts supplied
zero blink upper bytes, the subset on which the primary start agrees with the
original. Full blink clearing is established by the instruction evidence.

Checks cover finite normal/resume host phases, actual barrier/cleanup joins,
client human/autoplay continuations, the no-restore early exit, captured remote
slot routing, completion scans, repeated heartbeat expiry and counter wrap,
strict time thresholds, exact-1 callback gates, timer failure publication,
stop/resume behavior, rounding, expiry and alternating color. Native Windows
message delivery, timer races/cancellation, real dialogs, failed EDIT creation,
wire peers, save/reload during a phase, exceptional unwind and complete RNG
traces through child simulation remain integration obligations. The source and
fixture checks do not establish full native multiplayer conformance.

## Appendix C. Original presentation reference

The following original input geometry and drawing procedures are optional under
X01's presentation policy. The main text retains semantic selections, calculations,
file/wire effects and required ordering. This appendix is for original-adapter
validation, not a required UI design.

### Original timer caption refresh geometry

- **Caption**: invalidate the main view's timer-caption cache, draw its caption,
  then present the rectangle with frame left/top/right and client-top as bottom.
  Read coordinates after drawing. Updating the title or redrawing maps is a
  separate operation.

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

### Original turn-timer state representation

previous clock sample, retained native timer handle and caption visibility.

### Original drag hooks and watchdog timers

Native begin/end drag and mouse delivery belong to P05, but their timer/network
state is part of this contract:

- Begin drag sets active, captures a start clock, clears its secondary flag and
  captures the active child window. A host then broadcasts DragHoldOn and
  flushes(5000). Install the thread mouse hook afterward.
- End drag clears active/start/secondary flag/window, unhooks the retained hook
  handle, then, for a host, broadcasts DragHoldOff and flushes(5000). It does not
  clear the stored hook handle merely because it has been unhooked.
- The mouse hook invokes the watchdog tick when drag is active, then returns 0.
- Starting the watchdog does nothing outside network mode. In network mode,
  destroy a nonzero retained watchdog timer, create a timer with interval 500
  milliseconds and flag 1, and publish its result, including zero on failure.
  Stopping destroys a nonzero watchdog and clears its handle.
- A watchdog tick tests network mode, playerCount != 1 and active drag before
  reading the clock. If `s32(startTick + 620) < now` and quit is zero, release
  mouse capture. This uses a wrapped **deadline comparison**, not heartbeat's
  elapsed subtraction. Then call watchdog start regardless of whether release
  was needed.
- Release does nothing when inactive. With a captured window, obtain its current
  native handle, post left-button-up with zero parameters, then release that
  window's capture. Clear drag-active afterward. Do not implicitly clear other
  drag fields or send DragHoldOff here.

N02 queue-congestion handling can also invoke this release operation. Posting an
input message is not synchronous execution of its eventual handler.

### Original timer publication mechanism

Let **close windows** mean: close city window, end intelligence-report modal,
close throne room, end Civilopedia modal, in that order. The whole sequence is
selected once; a callback changing the current actor does not cancel its
remaining calls. **Publish timer** means: if the current handle is nonzero,
destroy the live handle; create a callback timer for 500 milliseconds with flag
1, then overwrite the retained handle with the returned value. Native scheduling
and queued callback cancellation belong to P05; these operations alone do not
prove that a stale callback cannot run.

### Original timer start, stop and resume

`startTimer(notify)`:

1. If networkTurnCiv equals local, close windows.
2. Set seconds to signed `trunc(limit / 1000)`; clear the **entire blink scalar**;
   invalidate the caption timer cache; draw and present the caption rectangle.
3. Publish timer, then record a fresh clock in lastTick.
4. If notify is nonzero and the live mode is network, broadcast StartTurnTimer,
   even if timer creation returned zero.

`stopTimer(notify)`:

1. If networkTurnCiv equals local, close windows, even when the handle is zero.
2. If the handle is nonzero, destroy it, then write handle zero, discarding any
   replacement published by that destroy callback. Draw the cleared progress
   bar with `(100, no color)`.
3. Within that nonzero-handle branch only, if notify is nonzero, mode is network
   and the human movement loop is active, broadcast StopTurnTimer. Read these
   gates after destroy/drawing callbacks. Do not change seconds or blink.

`resumeTimer(notify)` unconditionally closes windows. If seconds is nonzero and
timerShown is nonzero, publish timer, record a fresh lastTick, and broadcast
RestartTurnTimer when notify is nonzero and the live mode is network. Preserve
seconds, blink and caption cache. Negative nonzero seconds satisfy this gate.
When either gate fails, no timer is destroyed or created by resume.

### Original timer callback and progress rendering

The timer callback is:

```text
if live limit == 0: return
blink = u32(blink XOR 1)
if the XOR result is nonzero:
    if live seconds != 0:
        elapsedTicks = s32(clock() - live lastTick)
        elapsed = truncTowardZero(x87(elapsedTicks / 60.0 + 0.5))
        seconds = s32(live seconds - elapsed)
        if that subtraction result is negative: seconds = 0
        lastTick = clock()
    if live seconds == 0:
        close windows
        humanMovementActive = false
        drawProgress(100, no color)
        return
    drawCaption()
    capture caption rectangle and present it
percent = truncTowardZero(s32(live seconds * 100000) / live limit)
color = red if percent < 33 else yellow if percent < 66 else green
if (color == red or live seconds < 30) and live blink != 0:
    color = no color
drawProgress(percent, color)
publish timer
```

Use F01's x87 evaluation/truncation contract, not an integer division performed
before adding one half. The clock subtraction wraps before conversion. For
positive elapsed ticks, 29 rounds to 0 and 30 rounds to 1; negative elapsed is
also truncated toward zero after addition, so this is not symmetric rounding.
A clock callback can change seconds or lastTick before their later reads.

The first blink branch is selected from the XOR's result. The later color test
reads live blink again after possible drawing callbacks. The original uses the
full scalar, so accepted upper-bit values can make both alternating states
nonzero. Tick does not normalize such input to a Boolean.

Expiry returns without destroying or clearing the retained handle, updating its
visibility flag or scheduling another timer. The limit-zero early return also
does none of those operations. The next outer turn tail may subsequently stop
that retained handle. Percentage multiplication wraps; there is no caller clamp
to 0–100. A callback can change limit to zero before the later signed division;
zero divisors and an unrepresentable signed quotient are arithmetic fault
boundaries, not a required invented percentage. A restrictive profile may stop
safely after prior effects.

### Original service-input terminology

arithmetic faults or invalid handles before accepting an operation.
