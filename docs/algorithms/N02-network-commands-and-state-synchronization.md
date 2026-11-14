# N02: Network commands and state synchronization

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in N02 |
| --- | --- |
| Gameplay | Authorized command effects, actor/target/reply identity, synchronization meaning and required semantic queue/barrier dependencies. |
| Original files/content | F06 supplies serialized state definitions reused by section images; opaque imported data belongs in codecs. |
| Original peers | Message fields/lengths, section IDs/encoded images, checksums, compression/deltas, routing, sequence rules, locks and replies; checksum encoded bytes, never native objects. |
| Executable comparison only | Memory mirrors as live structs, global offsets, receiver pointers, allocator callbacks and per-word copy/dirty-scan implementation. |

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

Target: Civilization II Multiplayer Gold Edition 5.4.0f,
patch 3. This document specifies the application's message producers, receive
queues, dispatch, state images and incremental synchronization. It also owns
map locks, tile batches and the queues that defer remote presentation. It does
not require an implementation to use the original memory layout.

[N01](N01-multiplayer-session-and-lobby.md) owns discovery, session setup,
civilization claims, joining, departures and host migration.
[N03](N03-multiplayer-turn-sequencing.md) owns multiplayer turn/timeout state
machines and the timer calls dispatched here.
[N05](N05-transport-and-wire-protocol.md) owns transport framing, delivery and
connection routing, using these application contracts.
[F01](F01-determinism-and-arithmetic.md) supplies arithmetic;
[F02](F02-game-state-and-invariants.md) supplies shared identity/state;
[F06](F06-save-load-and-file-formats.md) supplies persistent record encodings.
The message envelope and synchronization image are separate formats from a save.

Child simulation operations remain with their owners: W01/W03 for map fields
and knowledge, U01 for units/stacks/cargo, C01/C06/C09 for cities, V01 for civilization
lifecycle, V02/V03 for government/rates and advances, and V04/V05 for diplomacy.
P01/P02 own view coordinates and drawing, P04 text/dialogs,
[P05](P05-input-and-command-dispatch.md) native/deferred input,
P06 city windows, P07 reports and P10 sound/animation. This document specifies
when and with which arguments those children run; their results and callbacks
remain observable inputs to the continuation.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
semantic publications and failure effects. Only dependencies within the binding
scope above constrain evaluation; storage/API choices need no original-trace
equivalence proof. External file/wire definitions remain binding at adapters.

Message/queue dispatch, command authority, section images, checksums/deltas and
state publication retain their specified dependencies and bytes. Independent
local calculation does not authorize reordering the original wire stream.

## Compatibility policy

X01's [TCP/IP scope](X01-classic-compatibility-contract.md#required-and-optional-transports)
retains every application command, state-section format, authority rule and
synchronization boundary required by original peers. Deferred remote popups may
use redesigned controls under the [presentation policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy),
but preserve recipient, information, offered decisions, response encoding,
publication, cancellation and queue/lock release. New widget IDs/choice ordering
must be translated to the existing payload values. Receiving a presentation
request does not authorize skipping its gameplay effects or reply.

Malformed/unsupported complete messages may be rejected under the declared
input policy. Fragmented/coalesced delivery and valid original messages are
still required; retain assembly, dispatch and admitted-message failure behavior.
A safe rejection must not publish a partly validated command or leave locks and
pending peer requests unresolved. Native UI-only errors may differ; transport
errors and session state changes are not covered by that exception.

## 1. Inputs, state and compatibility domain

The network mode is the shared game mode: values above 2 enable the network
batch/diff helpers. Values 5 and 6 additionally normalize received connection
IDs as described below. A connection ID, a player-table slot and a civilization
ID are distinct. Local civilization, host status, slot mappings, setup-complete
state and the current session phase are live shared state from N01.

Required state comprises:

- A wrapping send sequence, last transport result, per-message receipt counts,
  reply/result fields and the active mutation guards.
- Two received-message FIFO rings, their cursors/counts and high-water marks;
  poll depth; one deferred-input request; a separate deferred-drawing ring;
  a linked popup FIFO; modal, drag, redraw and stack-processing guards.
- A registered ordered state-section collection, section checksums, an owned
  mirror, a retained scan position/end, a packet under construction and a send
  counter. The event section is registered for images but excluded from the
  ordinary mirror scan.
- Eight map-lock rows, tile synchronization/batch flags, a batch buffer/count,
  vote counters, turn phase/completion counters, parley state and queued offers.

A nominal invocation has valid identity/table indices and live objects, complete
message fields, terminated strings within the declared buffers, valid section
spans, matching image layouts, produced state/padding for every consumed byte,
valid ring invariants and successful allocations. External allocation sizes,
clock readings, transport results, modal choices and reentrant callbacks are
inputs. Preserve each specified read after a callback; do not snapshot an entire
object where the algorithm reloads a field. No N02 primitive itself draws a
random value; its simulation children can do so under their own contracts.

Original-game peer interoperability is required under
[X01](X01-classic-compatibility-contract.md#file-and-original-game-multiplayer-interoperability).
The RNG relaxation does not alter these message schemas, section registrations,
checksums, routing rules or publication order. Original generator contexts are
not synchronized sections. Stored outcomes, including diplomacy values, map
fields and combat damage, are game state and remain exact received values.

Distinguish a request that invokes a child on its designated executor from a
reply/state update that publishes an already resolved result. Execute or apply
it exactly as specified below; do not reroll a received result or add duplicate
execution of the resolving operation on other peers. Different PRNGs do not authorize
a new mandatory random-state section or packet. Preserve the existing protocol
in mixed sessions with either engine hosting; X02 requires actual peer tests.

The reference byte layouts are in the appendices. They matter for original-peer
interoperability and validation. A replacement using another representation must
supply equivalent serialization, checksums, logical mutations and ordering.
Unproduced transmitted padding is a retained-input boundary, not implicitly zero.
The send sequence is replaced before ordinary transport, but that does not make
all other uninitialized bytes irrelevant.

Accepted off-nominal scalar reductions retain F01 wrapping, signed comparisons
and truncation. A stricter declared input profile may reject malformed lengths,
indices, strings, section spans and unsupported encodings before processing.
The original does not contain the reconstruction's comprehensive packet
validator. Do not describe that validator as classic behavior.

[Out-of-bounds writes are excluded bugs](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs).
Stop/reject/omit an unsafe operation without requiring corruption of adjacent
logical state. Preserve valid preceding effects unless the declared safer input
profile rejected the whole operation beforehand. An invalid **read** is a
separate boundary: provide established backing/retained input, or stop before
it; mapped memory alone does not establish its value. Failed allocations,
assertions and callbacks do not imply rollback. An error report may return.

## 2. Message production and transport boundary

An application message has a kind, declared total size and sequence. Ordinary
argument messages carry six signed wrapping scalar arguments followed by a
payload-length scalar. Let `a..f` denote the first six and `g` the seventh.
Decode all seven fields according to the packet schema. Values unused by a
handler still occupy their wire fields. A decoded immutable record is permitted;
overlapping native source/destination copies are reference behavior, not a wire
feature or a required alias in production.

### Construction and ownership

Construct the schema's header, sequence, seven arguments and explicit logical
payload. The producer's message/image definition determines payload extent;
`g` and total size encode that extent and the envelope as specified in Appendices
A–C. Encode an argument-only message with zero payload. Copy/move/borrow and
allocation strategy are implementation choices, with safe ownership until send
completion. A failed allocation is a failure, not an empty successful packet.

The original generic helper inferred the extent of a nonnull zero-length payload
from its allocator, enlarged the allocation and shifted it in place. That helper
ABI and ownership trace are **executable reference only**. Production must not
use allocator capacity as protocol data or send uninitialized slack. Any required
padding/opaque image bytes come from the owning external definition. If their
meaning or valid generated value is missing, record a finite format gap rather
than inventing bytes or reproducing heap layout. Original header/argument store
order and per-allocation callbacks are not protocol events.

Ordinary allocated messages are sent and then freed. The full/delta/join/map
image producers also transfer their image to this packet builder, send, free,
and return the transport Boolean. Raw state-diff and parley-offer sends retain
caller ownership: on failure return zero; on success return the packet's **live**
declared size after sending. The raw route does not replace its embedded kind.
Unsupported send kinds return zero without sending.

### Sending

A directed send reports a recipient outside 0..7 or 255 and reports a zero size.
If reporting returns, it continues. Store the current send sequence in the
packet, increment the shared sequence with wrap, then call the secure transport
with the recipient reduced to 16 bits, the live packet and declared size, flags
zero. Store the transport result. Any nonnegative result is application success,
even a short result; a negative result logs the failure and, for a popup, reads
its kind-specific diagnostic name. Invalid diagnostic indices need read backing.

A discovery broadcast likewise assigns/increments the sequence, calls broadcast
transport and publishes its result. Its success test differs: the signed result
must be at least the packet's **live declared size after the call**. Neither
path undoes sequence allocation or other state on failure. Destination zero,
255 and particular connections are passed to N05; do not infer application
loopback, delivery order or successful receipt merely from send success.

The default generic producer forwards exactly the argument count shown in
Appendix B and writes zero to unused argument positions. Exceptions there
include record replies, variable payloads and state images. Important local
side effects precede transport:

- Sending LockData increments its local receipt/count field after constructing
  the packet and before sending.
- On a host, LockNation executes locally. If the requested claim bit is free,
  set it, broadcast LockData, increment NationAccept and return one. If occupied,
  increment NationReject **and continue into the host UnlockNation path**.
- Host UnlockNation first intersects human players with the current claim mask.
  If the bit is set, clear it, intersect human players again, broadcast LockData,
  then increment LockData once more. The broadcast producer already incremented
  it once. Return one. Client Lock/Unlock sends two arguments normally. N01's
  received-claim service is a different path, with different count updates.
- NationAccept/Reject packets contain no arguments; a claim-mask argument passed
  by a caller is not transmitted.
- PlayerInfo sends the retained session player-information record directly.
- StartWhoseTurn constructs its special record, invalidates the main-view timer
  caption cache and refreshes status before sending. Native caption geometry and
  blitting are in [Appendix F](#appendix-f-original-presentation-reference).

Rule synchronization sends, in order: cosmic settings, advances, improvements,
wonder expiration, unit types, terrains, government names, government titles,
commodity names, order names, difficulty names, attitude names. The separately
callable leaders and civilization-table packets are **not** part of this batch.
Receipt copies the transmitted table without reparsing F03 rules. The oversized
civilization-table copy is an excluded bug described in Appendix E.

## Synchronization adapter boundary

The section catalog, encoded images, checksums and delta/compression rules are
wire contracts. Build those images from logical state plus required opaque codec
data. Mirror offsets, padding and checksum coverage refer to encoded images,
not native objects. Preserve image revisions and meaningful scan/publication
ordering where peers observe them; choose any equivalent mirror/queue design.
Registration locks, pointer retention, allocation callbacks and native last-error
state do not become simulation fields. Network receipt and command dispatch are
real boundaries; copying one header word or allocating a queue node is not.
Validate bounded frames before exposing payloads. Diagnostic malformed/backing
and allocator-failure traces below are not required receive algorithms.

## 3. Receive callbacks and FIFO queues

### Callback acceptance

Session construction initializes PlayerInfo, constructs the receive queue, clears
its connection-title pointer and registers broadcast, secure, connected, new-client
and lost-connection callbacks in that order, then an oversized-message callback
with threshold 50000. That callback only formats/logs the size; it does not decide
whether delivery continues.
[N05](N05-transport-and-wire-protocol.md#incremental-frame-parser) owns the
fragment-dependent oversized callback and transport-parser continuation. Destruction frees
and clears the title, runs N01 shutdown with argument one, resets lost-error counts
and clears the queue. Do not merge construction with N01’s separate session reset.

The original broadcast callback first rejects captured sizes below the header
size. It then checks magic. For a non-datagram kind it enqueues immediately with
sender zero. For a datagram it accepts only if the target address is empty or
equals the local address, **and** the excluded address is empty or differs from
the local address. Comparisons are case-sensitive C strings. The initial header
check does not establish those later strings' backing.

The secure callback first requires nonnull data and captured size greater than
three, then checks magic. On magic match it enqueues using the 16-bit connection
ID, regardless of enqueue success. It then records captured callback size,
current **global send sequence**, the message's declared size and message kind.
That diagnostic sequence is not the received packet's sequence.

For nonmagic data it logs the preceding accepted-message diagnostics when the
last kind was nonnegative, the current send sequence/size and a bounded hex
prefix; resets last kind to -1; then searches offsets 0..31 for a possible magic
and logs a found candidate header/name. The original candidate search did not
bound all header/name reads by captured size. This is a read boundary, not a
second delivery attempt. At the callback tail, if ordinary queue count exceeds
300 or priority count exceeds 33, release mouse capture when dragging is active.
This tail also runs after nonmagic/null/tiny secure input.

### Enqueue

The priority predicate is `42 <= kind <= 102`. Two exceptions are processed
before either queue: HeartBeat increments its counter and sends HeartBeatAck to
the normalized sender; HeartBeatAck only increments its counter. Both return
success without allocating or enqueueing. Thus they bypass queue congestion,
setup-complete checks and ordinary poll dispatch.

In modem/serial modes 5/6, normalize a sender other than zero/255 to one on a
host and zero on a client. Use that value for heartbeat replies and stored slots.

Each ring is full exactly when head equals tail and count is nonzero. Log/drop
the new message and return zero; do not overwrite an old message. Otherwise:

1. Report out-of-range head and tail in that order, without an implied return.
2. Allocate the captured callback size and publish the resulting pointer in the
   live tail slot. On null allocation log and return zero, leaving counts and
   cursors unchanged. Do not cache the tail across allocation/report callbacks.
3. Copy exactly the captured bytes, then store sender and captured size.
4. Advance the tail with signed remainder at the ring's wrap point; increment
   count; increase its high-water mark if the signed comparison requires it.

The priority ring holds 400 messages; the ordinary ring 1600. No comparison
between the captured size and packet-declared size occurs in this original
algorithm. A nominal packet supplies all bytes later consumed. Original invalid
cursor writes remain excluded bugs, including writes that would land on another
queue field.

Dequeue always chooses priority first. If priority is empty, choose ordinary only
when `priorityOnly==0`. Empty returns zero without modifying output arguments.
For the selected slot: copy sender out, clear sender; copy pointer out, clear
pointer; copy size out, clear size; advance head and decrement count; return one.
Preserve this order for valid aliases. Reset scans all slots in index order,
frees each nonnull pointer and clears it, clears size then sender, and finally
resets both rings. Construction clears slot storage before reset. Clear calls
reset; it does not separately reset external high-water marks.

### Polling and deferred input

Polling does no work while any of these mutation guards is nonzero: unit/city
create/delete, map lock/unlock, stack unlink/link/bottom, relocation, wonder
acquisition, civilization elimination/create/check-destroyed, cargo gathering,
lifting, cargo deletion or destruction/redraw. It also requires the transport's
flush-in-progress query to return zero. Input busy alone is not this gate.

If a deferred request exists, deferred-input busy is clear, and a **second**
flush query is zero, run the deferred request before increasing poll depth. Then
increment depth and its peak. While the supplied budget is nonzero:

1. If stack checking and map redraw are idle and deferred drawings exist, set
   stack-processing enabled; execute one drawing when count <34, otherwise two;
   clear the processing guard.
2. Only when ordinary count <301 **and** priority count <34, pump native input
   and decrement budget. Congested iterations do not consume budget.
3. Dequeue priority-only if priority count is at least 34; otherwise use the
   caller's wait mode as `priorityOnly`. Break on no message.
4. Report a null packet if encountered. For a magic packet, increment its type
   counter first. If kind >53 and setup is incomplete, free it and **break the
   entire poll**, retaining that count increment. Otherwise dispatch, then free
   the current packet pointer, which image application may have replaced.
5. A nonmagic dequeued packet is logged; this original branch does not free it.

After the loop, wait mode zero permits a dirty discovery-list refresh when
listing is active. It also permits showing one queued popup when none is active.
Finally decrement depth. A budget of one can dispatch multiple messages under
congestion, or dispatch none after pumping input. There is no sequence-based
replay rejection, general sender/owner authorization check or transaction barrier
in this poller. Counters can change even when a handler does nothing.

Deferred input captures the request kind, sets busy, logs, then clears the shared
kind before calling a child. Kinds 1/2 inspect the target's enabled state, activate
it and dispatch map click/double-click; the original enabled-state access occurs
before its null check. Kind 4 captures the pending key kind and character,
dispatches the key, then dispatches ASCII if the captured kind was 3. Kind 5
executes a menu command with deferred arguments 1 and 2. Click uses deferred
arguments 1,2,3; double-click 1,2; key uses argument 1. City mouse uses arguments
3,4,5, and each city button uses argument 3; city identity/owner guards use
arguments 1 and 2. Kind 3 and other unlisted kinds have no dispatch action.
Kinds 6..10 require an open city window, an active referenced city and matching
requested owner, then dispatch city mouse/buy/change/view/rename respectively.
Clear busy after the child. P05/P06 supply those operations. The original key
scratch's unproduced upper bits require retained input unless the actual child
is proven to ignore them; do not invent an arbitrary key value.

## 4. Ordered command dispatch

Appendix B lists all message kinds and producer schemas. This section specifies
the child calls and state publication. Argument decoding precedes every listed
argument-dependent operation. Counts increment under §3 even for counter-only
messages; a later assignment of one is not an additional increment.

### Session and handshake commands

Use N01 for datagram discovery, player-list/node application, ServerInfo/Answer,
PlayerInfo, claim handling, cancellation and departures. Relevant dispatch joins:

- PlayerList copies its complete list aggregate, then scans slots 0..6 for the
  first active slot whose connection ID equals the retained server connection ID;
  publishes that slot locally. No match retains the old local slot.
- ServerReject resets the wait-start clock. ServerPath stores its signed file
  kind and copies name, then subdirectory. ServerQuery replies ServerAnswer to
  the sender. ServerQuitRequest and ServerQuit increment handoff stage;
  ServerQuitAck decrements players awaited.
- GameGoing assigns session phase. LockData stores the reduced claim mask,
  intersects human players with its low byte, then increments LockNation count.
- UnlockNation receipt intersects humans with the old mask; if its bit is set,
  clears it, intersects humans again, broadcasts LockData and increments
  LockNation count. Unlike host-local sending, it does not increment LockData a
  second time. QuitRequest sets the requested civilization's drop flag and runs
  N01's flagged-drop pass.
- ClientCanceledStart marks the sender's lost-connection entry -1, removes that
  player, then, when a lobby exists, increments its sender-indexed drop counter
  and refreshes if its dialog exists.
- MultiOptionsChange stores production/movement as signed 16-bit values, live
  simultaneous mode as a byte, and the saved simultaneous setting as a sign
  extension of that byte. For example, incoming 257 yields live/saved one.
- KillOrQuit calls civilization elimination `(a,0,0)`, then replies with its
  header-only acknowledgement. Receipt sets the acknowledgement to one.

EventsLoaded stores loaded state `a`, event count `b`, and relinks the event pool
only when `a!=0`; [S02](S02-event-language-and-execution.md) owns relinking. State
image reception alone does not perform this relink.

### Units, cities and civilization commands

The following table names the child operation in its owner's terminology.
Reply destination is the received sender. `force=1` means the owner's local
execution path. The explicit flush follows the reply, including a failed send.

| Request | Ordered child / reply | Explicit flush |
| --- | --- | --- |
| DeleteUnit | U01 delete `(a,1)`; UnitDeleted `(a)` | 5000 |
| DeleteCity | C01 delete `(a,0)`; CityDeleted `(a)` | 5000 |
| CreateCity | C01 create `(a,b,c)`; CityCreated `(result)` | 5000 |
| CreateUnit | U01 create `(a,b,c,d)`; UnitCreated `(result)` | 5000 |
| PickUpUnit | U01 detach `(a,1)`; UnitPickedUp | 5000 |
| PutDownUnit | U01 attach `(a,b,c,1)`; UnitPutDown | 5000 |
| MoveToBottom | U01 stack-bottom `(a,1)`; Bottomed | 5000 |
| RelocateUnit | U01 relocate `(a,b,c,1)`; UnitRelocated | 5000 |
| AcquireWonder | C06 acquire `(a,b,c,d,e)`; WonderAcquired `(result)` | 5000 |
| StackShip | U01 gather cargo `(a,b,1)`; ShipStacked `(result)` | none |
| StackUnit | U01 lift off map `(a,1)`; UnitStacked | none |
| DeleteSafely | U01 delete with cargo `(a,1)`; SafelyDeleted | none |
| DeleteVisibly | U01 destroy/redraw `(a,1)`; VisiblyDeleted | none |
| NewCiv | V01 create `(a)`; NewCivAck `(result)` | none |
| KillCiv | V01 check destroyed `(a,b)`; KillCivAck `(result)` | none |

DeleteUnit first reads the old coordinates if `a>=0` and the unit is active; those
locals do not change the child's arguments or published result. The read still
requires backing. Request/response payloads do not imply symmetric application:

- UnitDeleted only sets its acknowledgement to one. Its transmitted unit and
  unit-count fields are not applied here.
- CityDeleted sets its acknowledgement first, then copies the city record; it
  does not assign the transmitted city count.
- CityCreated assigns result `a`; unless `a==-1`, copies its city then stores
  signed-16 city count `b`.
- UnitCreated assigns result `a`; unless `a==-1`, stores signed-16 unit count `b`.
  It does **not** copy the transmitted unit record.
- UnitPickedUp, UnitPutDown, Bottomed, UnitRelocated, UnitStacked, SafelyDeleted,
  VisiblyDeleted and RealtimeStackerAcker overwrite their receipt fields with one.
- WonderAcquired, ShipStacked, NewCivAck and KillCivAck publish full result `a`.

RealtimeStacker `(a,b,c,d,e,f)` lifts unit `c` locally, clears its stack visibility,
sets visibility to `b` only if `d==0`, relocates the whole stack to `(e,f)` and,
when it has a next member and its type attack is zero, moves it to the bottom.
If simultaneous movement is live, unlock civilization `a` without waiting.
Then send an alternate/priority state diff to everyone with flags 2, reply
RealtimeStackerAcker and flush 60000.

BribedUnit `(unit,newOwner,cost)` captures old owner, subtracts cost from the new
owner's treasury, decrements/increments old/new per-type byte counts, increments
new-owner subversions, changes owner, clears home, movement used and orders.
Find the nearest city with unrestricted selectors; assign its byte home ID only
if that city's owner matches. Mark the unit seen by its **old** owner, set tile
owner to its new owner with forced update, then broadcast a priority diff with
flags 2. Preserve V06/U01 field reductions and W01's city query. DeathStack calls
the casualty-history child with all five arguments, without extra flush.

### Fields, phases, timers and reports

Direct Feature/Terrain/Seen/Site/Owner/Region/CityUsing/FeatLoc messages call,
respectively, W01/W03 improvement, terrain, visibility, fertility, owner,
landmass, worked-radius owner and remembered-field setters with every supplied
argument, including the final synchronization/force argument. Batch receipt
instead supplies a final one (§8).

SetAttitude stores the reduced byte `c` at civilization `a`'s attitude to `b`.
DoAIParley sets pending flag for `b`, then stores AI civilization `a`, civilization
`b`, x=`c`, y=`d`. SetSlideSound stores `a,b`. SetCiv calls advance grant
`(a,b,c,d,0)`. KingdomRates opens local-human rates; TaxCheck enforces its limits;
PickGovt invokes local-human government selection with `a`. PoliticalViolations
calls relationship review `(a,b)`; PoliticalAttitude calls war-ally seeking
`(a,b)`. Those children retain their own random draws and UI.

ProductionComplete increments civilization `a`'s phase-complete counter.
HumanMoveComplete increments that same counter, then scans unit IDs in ascending
order below the **live** unit count; each active unit owned by `a` has its moves
exhausted. TurnFinished increments `a`'s ended-turn counter. ProductionBegin,
AIMovement, HumanMovement, ContinueAfterJoin and ReadyToStart need no additional
handler mutation beyond their count. N03 consumes these signals.

StartWhoseTurn retains an active queued drawing if one is processing, otherwise
clears the drawing ring; appends AbortKaboom then DrawMap; publishes the payload
civilization byte and sign-extends it into network turn civilization; stops the
turn timer with argument one, updates menus, invalidates/draws/blits the caption.
The appended argument fields are retained scratch; these two normal consumers
do not use them. EndSequence stores reduced game flags `a`, game-over reason `b`
and spaceship winner `c`.

TimerChange assigns limit `a`; zero hides the timer and redraws/blits the caption,
nonzero shows it and starts with argument one. StartTurnTimer shows it only if
the live limit is nonzero, then starts with argument zero. StopTurnTimer stops
with zero; RestartTurnTimer resumes with zero. HoldForJoin constructs a modal,
saves popup-busy then sets it, stops timer with zero, clears ContinueAfterJoin
count, installs an idle poll `(1,0)` and displays its wait. Nonzero continuation
count closes the modal. On return close/destruct, restore busy and resume timer
with zero. N03 owns elapsed-time and turn-state effects of the timer children.

CanEscapeReply, DesertReply and ParleyRequestReply publish `a` into their separate
reply fields. NewTurnTimerReply and PMChangeReply decrement votes pending and set
vote status -1 only when `a==2`. DragHoldOn/Off store one/zero and log. DoApollo
runs W03's Apollo revelation. InfoStatus draws status with one; InfoUpdate redraws
it with one; SoundPlay calls effect `(a,b,0,0)`; AskAlly calls ally help `(a,b)`;
ReportsUpdate refreshes report `a` if open. CityWinCheckUnit passes `(a,b,c)` to
P06's unit-change hook, CheckCity refreshes city `a`, Deleted closes it, and
Redraw refreshes only when its refresh lock is zero.

### Parley dispatch

Chat passes `(a,b,c,payloadString)` to the chat UI. ParleyRequestCancel indexes
the player table by **sender as a slot**, not by searching its connection ID.
If that slot's civilization matches the other party, or the other-party value
is -2, or the parley window is open: close an open window; otherwise change -2 to
-1; then clear meeting-pending. Keep the actual disjunction.

ParleyTransaction frees and clears the old received offer, allocates the captured
packet size, copies it on allocation success, and polls negotiation replies even
if allocation failed. AcceptOffer, NoThanks and CounterOffer likewise poll replies.
ParleyExecute passes the complete offer to
[V05](V05-diplomatic-negotiation.md), which owns offer contents and transfers.

Pretext fills the two civilization names. If `b` does not regard `a` as an ally,
show PRETEXT; choice one sets the war-pretext treaty flag and stores current turn
in `b`'s last-contact entry for `a`. For an alliance show PRETEXTALLIED, choosing
its portrait according to globally known Communism; choice one clears alliance
and stores that contact turn. V04 owns the treaty mutations.

RevealCityInfo/HideCityInfo act only with an open window and matching other party
`a`: store byte `b` as remote-cities-revealed. In exchange mode 14 with right kind
3, or demand mode 15 with kind 3, the Reveal packet disables **both** information
buttons and redraws the right city list; Hide enables both and redraws. The
packet names do not invert this behavior.

RevealCityOrigin/HideCityOrigin similarly store remote-units-revealed. In the
corresponding kind-2 modes, Reveal disables both information buttons, enables
both sorting buttons and redraws units. Hide enables information, disables
sorting, sorts right units over indices 0 through `count-1`, then redraws.

## 5. State sections, checksums and mirrors

Section registration first ensures the scenario-event pool is locked. It installs
24 sections in the Appendix C order. Initialization writes data location, data
size, ID, checksum zero and size rounded upward to a multiple of four. It leaves
the previous mirror offset. Afterward registration assigns cumulative padded
mirror offsets only to sections 0..22; section 23's offset remains retained.
Rounding/addition wrap under F01. Allocations do not magically cover wrapped or
negative logical extents.

Mirror allocation frees any previous mirror, sums padded sizes for 0..22, reports
non-four-alignment, allocates, and returns zero on failure. On success zero the
entire allocation. Host scan-end is its full size; client scan-end is the offset
of section 15, so a client scans sections 0..14 only. The allocation still holds
all 23 mirrored sections. Copy **data size**, not padded size, for every section
0..22 into successive mirror spans, advancing by padded size; this copy pass
does not consult stored mirror offsets. Set scan to mirror start, section zero,
offset zero, and
return one. Free clears the pointer but does not reset every associated field.

Invalidation copies current section contents into the mirror, complements every
four-byte word across the full mirror including padding, and resets the scan.
Section padding on the live side must have established readable backing; mirror
padding was zeroed and then may have been complemented. Do not replace this with
an abstract dirty bit if it changes transmitted comparison results.

The image and packet mappings define external bytes independently of internal
state layout. Materialize their required encodings for checksum/delta/compression
operations; do not checksum a replacement implementation's native object bytes.
Wider counters or typed IDs are permitted only with the same published logical
values and protocol bytes. Sequence wrap and receipt-count semantics remain
required; a counter tested for nonzero is not automatically a Boolean.

### Checksum

Interpret size as signed 32 bits for loop limits and divisibility. Zero returns
zero. Otherwise accumulate with signed-32 wrap:

| Size condition | Elements summed |
| --- | --- |
| Divisible by four | `size >> 2` little-endian 32-bit words |
| Even but not divisible by four | **`size >> 2`** unsigned little-endian 16-bit words |
| Odd | `size` unsigned bytes |

Only iterations with nonnegative index below that signed bound execute. Negative
sizes therefore sum no elements. The even/non-four case deliberately sums fewer
bytes than the extent suggests: size two sums nothing; bytes `01 02 03 04 05 06`
at size six yield 513. A checksum match is not proof of equal state and does not
cause a byte comparison fallback in delta-image selection.

## 6. Whole-section images and application

Image records consist of a section descriptor prefix followed by exactly its
declared data size. The prefix carries size, padded size, ID, checksum and mirror
offset; only data size and ID select application extent/destination. Wire details
and F06 record mappings are in Appendix C.

### Producers

Civilization/join image order is game, leaders, civilizations, names, scenario,
view/multiplayer state, events: section IDs **1,7,3,2,9,11,23**. Map-header image
order is **1,12**. Allocate the sum of selected sizes plus descriptor overhead.
For each, compute/store its live checksum **before** appending the descriptor and
its current data. Then attempt byte-code compression (§7).

Full-image capacity is all 24 section sizes plus descriptor overhead, except
units use current unit count times their record extent and cities current city
count times theirs. Iterate sections 0..23. For each, first capture its descriptor
prefix; trim that captured size for units/cities; compute the checksum over the
selected extent and update the live descriptor; append the **previously captured**
prefix and data. Thus its transmitted checksum is the old checksum. Make a raw
copy for 16-bit-code compression; its destination capacity is the raw image size.
Compression failure returns -1 and retains the original allocation with any
partial encoded bytes already written into it; the raw copy is not restored.
Success resizes to compressed length and publishes the result, even if that resize
returns null. Failure to allocate the raw copy returns zero. Keep earlier checksum updates.

Delta-image iteration similarly captures each descriptor, trims unit/city size
and computes the current checksum. Emit only if the checksum differs **and**
selected size is nonzero; then update the live checksum and append the old prefix
and current data. A zero-sized section does not update its checksum. Resize to
written size; null reports/fails with zero, otherwise try byte-code compression.

Append tests signed `capacity < wrap32(position+length)` and reports on true.
Its copy and following position addition use the live position after reporting. The original could
continue to the unsafe copy after a returning report; that write is excluded.
Earlier descriptors, data and checksum changes remain. Producers are not atomic.

For every image kind the send layer sets compressed argument `a` to
`producerResult != -1`. Consequently a zero failure result also sets it to one.
It reports a null image but does not define a safe fallback packet; apply the
bounded failure policy before an unsafe access.

### Receipt and shared image application

The message handler captures argument `a` and computes payload extent from the
**captured receive size**, not declared size or `g`. Move that payload to the
packet start, resize to it and publish the possibly changed pointer. Full, Delta
and Join all call the same image application; MapStruct uses its own application.

Shared image application saves local game options and message options. Register
sections if the version section has no data pointer. Codec selection uses the
**global FullGameState receipt count**, already incremented by the poller:

- If that count is zero, optionally run byte-code decompression when `a!=0`,
  ignoring its return value; use the resulting allocation size as image extent.
- If it is nonzero, always use 16-bit-code size/decode, independent of the current
  message kind and `a`. Allocate decoded size. Null allocation returns early;
  decoder failure returns early. Success frees the old image and publishes the
  replacement. Those early returns precede the ordinary option-restoration tail.

Scan while signed consumed offset is below signed image extent. Capture the
next descriptor, add its data size plus overhead to consumed offset, advance
past its header; report an invalid ID, then, if continuing with valid backing,
copy data into that section, recompute/store checksum from the incoming payload,
and advance by data size. Neither transmitted checksum, padded size nor mirror
offset changes the destination or verifies integrity. Restore **message options
then game options** at the normal end. All other copied local fields persist.
Trailing/negative/wrapped sizes require the specified read/write boundaries;
there is no all-record prevalidation or rollback in the original.

Map-header application saves message options then game options, optionally runs
byte-code decompression, frees the existing map **before** applying records,
reads the first block and reports unless its ID is one, reads the second and
reports unless ID is twelve. Each block read advances past the descriptor before
ID diagnosis, copies data, recomputes its checksum, advances and returns its ID.
Restore game options then message options; allocate the map, register sections
and allocate its mirror, ignoring the latter result. It does not scan arbitrary
extra records or atomically retain the old map on failure.

## 7. Compression and incremental differences

### Bounded production codec

The following safety rules override the original encoder/allocator traces below.
Encoding consumes only the logical input span. At its final byte, finish the
remaining literal without reading a following byte. Word-code runs use chunks
of at most **32766**; literals may still contain 32767 bytes. Never emit run code
32767, whose original decoder interpretation is unsafe. These are deliberate
bug repairs using the existing token grammar, not original encoded-byte claims.
Retain the thresholds and token choices elsewhere. Verify decoding reproduces
the complete input, including distinct-byte tails and long runs; actual mixed-peer
validation remains a separate interoperability gate.

Image transforms prepare an owned result and replace the input only on success.
No-gain compression, invalid/truncated input, resource limits and allocation
failure retain the original image. Return semantic status and length, not original
native return integers. Allocation schedules, null publication and partial-image
damage are reference only and must not be exposed as production service inputs.
Word decoding rejects ambiguous code 32767. Bounded byte decoding retains its
documented treatment of zero-count tokens and an unmatched final byte.

### Two distinct run-length codecs

The token grammar and meaningful thresholds remain file/wire definitions; the
bounded production codec above overrides unsafe lookahead, token emission and
original allocation/failure traces in this historical procedure.

Byte-code compression has one-byte headers; 16-bit-code compression has unsigned
little-endian two-byte headers. Both use the following selection procedure, with
byte-code run threshold 2 and maximum chunk 127; 16-bit threshold 3 and maximum
chunk 32767. Values equal to the threshold remain literals.

Start `begin=0, scan=1`. While `begin<inputLength`:

1. Advance scan while it is below end and its byte equals byte at begin.
2. Let run length be `scan-begin`. If it exceeds the threshold, emit consecutive
   chunks up to the maximum as run header followed by one repeated byte; set
   begin to scan.
3. Advance scan while below end and byte at `scan+1` differs from byte at scan.
   The original reads the byte **one past the logical end** when scan reaches
   the final byte. Supply proven backing/retained input, or stop there. It affects
   token selection; equal decoded bytes do not establish equal encoded packets.
4. Emit the resulting `scan-begin` literal extent in chunks up to the maximum,
   each with a literal header followed by those bytes; advance begin by each
   emitted literal chunk. Resume from the retained scan position.

A run header is its count. A literal header is its count plus 128 or 32768.
For byte-code compression, allocate scratch `size + trunc(size/128) + 5` using
F01 signed arithmetic. Null returns -1 without replacing input. Each emitted
header/value/literal byte tests whether written length has reached the original
size; reaching it abandons scratch and returns -1. Success frees input,
allocates the encoded length, publishes its pointer, copies scratch, frees scratch
and returns length. Failure of that final allocation occurs after input was
freed; no rollback is implied.

For 16-bit encoding, each chunk checks both output capacity and that its new
encoded extent remains **strictly below** input size. The run header may already
have been written when the later value-byte check fails; similarly a literal
header can precede a failing payload check. Return zero without assigning output
length on failure; on success set output length and return one. The empty-input
loop reports success with output length zero. Capacity arithmetic is unsigned.

Byte-code expansion copies the allocation to scratch, resizes the destination
to twice the input allocation size, publishes it, and processes while source is
before `end-1`. Codes below 128 repeat the next byte `code` times; other codes
copy `code-128` literal bytes. Zero runs still consume their value. Before each
record, if available output is less than `(code & 127)+1`, grow once by 1000 and
publish the new pointer. At completion resize to written length, publish, free
scratch and return written length. Allocation failure returns zero at its
particular stage, retaining earlier pointer/byte effects; ignored return values
at callers must not become an invented transaction rollback.

The original 16-bit size pass reads a full code whenever source pointer is below
end, adds its low 15 bits to a wrapping total, and skips one value byte for codes
<=32767 or the low-15-bit literal extent for higher codes. It does not establish
that the token/payload fits. The decoder first compares that total with unsigned
output capacity and returns zero if too small, without assigning output length.
Otherwise it rereads tokens while source is below live end:

- Code **below 32767**: read one byte, repeat it code times.
- Code **at least 32767**: subtract 32768, reduce to unsigned 16 bits, copy that
  many literal bytes and advance by them.

At normal end publish written length and return one. Thus code 32767 is counted
as a 32767-byte run by the size pass but decoded as a 65535-byte literal. When
that exceeds the destination, the overwrite is an excluded bug. The decoder's
source overread is separately bounded. A stricter decoder can reject this token;
record that policy rather than claim the reconstruction's repaired decoding is
the original. The ordinary small diff runs cannot produce such a long token;
a full image can. Appendix E records the instruction evidence and repairs.

### Sending incremental differences

Return zero without work when mode <3, or when force's low byte is zero and the
network player count is one. Otherwise capture the **incoming scan pointer**,
increment diff-send count, optionally invalidate the mirror, and initialize limits.
The original flag mapping is in Appendix C. In behavior terms, flags select
invalidation/flush, deadline, changed-run limit and whether to leave mirror stale.
If deadline is selected, set `deadline=clock+limit`; otherwise, if run limiting
is selected, set remaining runs to limit. Both tests still run later if both
flags were supplied: the run counter then starts at zero.

Use current scan section/offset and its padded data end. Whenever entering the
game section, copy these live local fields into the game **mirror** before
comparison: active unit, moving unit, multiplayer mode, player civilization,
saved current civilization, turn civilization, game options, message options.
This excludes their local differences even after invalidation.

Start a difference packet of the selected ordinary/priority kind. Compare
four-byte words, advancing both live and mirror positions. On equality advance;
at section end move to the next section; at scan-end wrap to section zero and
mirror start. Preserve zero offset on transitions and normalize the game fields
on every entry. On inequality gather a contiguous run of differing words until
one of: section end, scan-end, an equal next word, or accumulated packet extent
plus current run extent reaches the flush threshold. Capture its section, offset
and raw size before transitioning. The threshold test omits the new record's
header, so a completed packet can exceed the threshold by that overhead.

Test/report a run larger than 1024 before and after deriving a still-zero run
size from the cursor difference; an already-known oversized boundary run can
therefore report twice. Unless the leave-mirror-stale flag is set, copy the
changed data into the mirror **before encoding and sending**. Try 16-bit run-length
encoding. On failure copy raw bytes and use the ordinary section tag; on success
use the compressed tag and encoded extent. Increment packet record count and
returned changed-byte total by raw extent. When packet size reaches threshold,
send the raw packet through the diff-send route; if invalidate/flush is selected,
flush 5000; reset packet payload/count. Send failure does not restore the mirror.

After each changed run only, stop if a selected deadline is strictly exceeded,
or if selected run count decrements to zero. No-change scanning does not test
the clock/run limit. Otherwise continue until the scan pointer equals the
**original captured start**, which may differ from the reset position after
invalidation. Send a final nonempty partial packet with the same flush rule.
Finally store offset from the current live section base to the live data cursor;
return raw changed-byte total, ignoring transport success. The retained scan
position affects subsequent calls. Flag 2 has no branch in this routine.

### Receiving incremental differences

Decode all seven arguments first. If no mirror exists, return. Otherwise iterate
`a` records from payload; each has tag, offset, encoded size and bytes. No original
all-record fit validator precedes the loop. For each:

1. If low-15-bit section is game, save the same eight local fields listed above.
2. Otherwise, on the first unit-section record received by a host, save every
   unit's x, y, next and previous links below the current live unit count, once.
   Otherwise, a view/multiplayer-section record sets a resume-update flag.
3. For an uncompressed record copy into the section, then its mirror, using the
   full tag as the section index. For a compressed record mask to 15 bits,
   calculate decoded size, decode directly into section data, ignore the decode
   result, and copy that decoded extent from section data to the mirror.
4. Restore the saved eight fields only when the effective section index equals
   one (raw full tag, or masked compressed tag). The mirror retains the
   incoming values until sending normalizes them. Mark city refresh after a city
   record. Advance source by **encoded**, not decoded, extent.

After all records, refresh city windows first if requested. Then a host that
saved unit coordinates/links restores them in ascending unit order below the
**then-live** unit count. Growth beyond the produced saved entries needs supplied
backing or a bounded stop; it does not authorize invented coordinates. If the
resume flag was set, assign resume civilization from saved-turn civilization.
An event-section record has no allocated ordinary mirror range; its retained
offset does not authorize writing into an unrelated section. Apply the span and
excluded-write rules independently to data and mirror destinations.

## 8. Map locks and tile batches

Each civilization's lock row holds state, replies, source point and destination
point. N01 initially fills all six fields with -1. A lock request first sets
lock-busy, replies zero and state zero.

On a client, clear busy, send LockMap `(civ,srcX,srcY,dstX,dstY)` to zero and flush
60000. No-wait returns one regardless of send success. A waiting caller captures
the clock, then repeatedly checks elapsed time and replies before polling `(1,1)`.
Stop when signed wrapped elapsed exceeds 3599 or replies is nonzero. Zero replies
at exit logs timeout, shows SERVERCONNECTTIME, clears turn-loop active and returns
zero; exactly one returns one; every other reply returns zero.

A host scans lock rows **1 through 7**, including the requester's own row, without
checking activity or row state. For each row test whether requested source equals
its source or destination; on match stop. Otherwise test requested destination
against those two points and stop on match. With no conflict, assign the four
requested coordinates. Reply `(civ,1)` only if requester differs from local human,
is in the human mask, and its mapped player slot is active; flush 60000 after
that reply. On conflict use the same recipient conditions and reply `(civ,-1)`.
Clear busy and return one/zero respectively. No successful-lock broadcast is
implied. Mapping reads require valid slots even when prior conditions passed.

Unlock sets unlock-busy. A client clears busy, zeroes its state and sends
UnlockMap `(civ)` to zero, with **no explicit flush**. Optional waiting uses the
same clock threshold/poll pattern but waits for state nonzero. Timeout logs,
shows SERVERCONNECTTIME and clears turn-loop active. A host sets its whole row
to -1, broadcasts LockMapSync `(civ,0,0,0,0)`, flushes 60000 and clears busy.
Thus local host clearing and remote sync do not produce identical row values.

LockMap dispatch calls lock without waiting. UnlockMap calls unlock without
waiting, then replies UnlockMapAck `(civ)` to its sender. LockMapAck assigns
replies; UnlockMapAck increments state. LockMapSync assigns the four coordinates,
then replies zero and state zero. Preserve asynchronous ordering: the sync can
arrive before or after the acknowledgement.

### Tile batching

In network mode, begin disables tile synchronization, enables batching, clears
the buffer and sets record count zero, used-word count one. Outside network mode
begin/flush/application do nothing. The append helper itself has no mode guard.

Appending reports if called on a host. If `256-usedWords < recordWords[type]+1`,
send MapPackets to zero, flush 5000, clear the buffer/count and reset used words
to one. Increment record count, append kind, then append fields in order:

| Kind | Field change / arguments | Words including kind |
| --- | --- | --- |
| 0 | Improvements: x, y, value, set/clear selector | 5 |
| 1 | Terrain: x, y, value | 4 |
| 2 | Visibility: x, y, civilization mask, set/clear selector | 5 |
| 3 | Fertility: x, y, value | 4 |
| 4 | Tile owner: x, y, value | 4 |
| 5 | Landmass: x, y, value | 4 |
| 6 | Worked-radius civilization: x, y, value | 4 |
| 7 | Remembered field: all five caller arguments in order | 6 |

The extra one in the capacity predicate is preserved even though record size
already includes kind. Increment used words after each store. Unknown kinds
still perform the initial size-table read/count/kind operations when their reads
are backed; they do not acquire an invented argument schema.

Flush first enables synchronization and disables batching. On a client, only a
nonempty buffer is sent/flushed/cleared. On a host, broadcast a normal diff with
flags 2 and flush 5000, regardless of buffer contents.

Receipt in network mode reports unless host, saves synchronization, disables it,
reads record count and processes that many records while positive. Read kind,
then exactly `recordWords[kind]-1` arguments, and call the matching W01/W03 setter
with a final forced value one. Restore saved synchronization and broadcast a
normal diff with flags 2; there is no extra flush here. Failure after some records
retains those effects. A stricter parser can reject a malformed batch before
application; it must identify that policy rather than claim original atomicity.

## 9. Deferred remote drawing

The drawing FIFO has capacity 100, separate from the received-message queues.
A record contains kind, seven scalar arguments and, for SlidePiece, a captured
unit. Append reports invalid cursors, stores kind first, copies seven arguments
sequentially using the live tail index, copies the unit when required, increments
count/peak and advances tail modulo 100. Its result is the signed quotient of
`oldTail+1` by 100, **not** a success Boolean.

On full ring, log and retain only the active head when stack processing is
nonzero, setting tail to head+1 modulo 100 and count one. Otherwise reset cursors
and count to zero. Append AbortKaboom followed by DrawMap instead of the original
requested drawing; return the second append's result. Their copied scratch
arguments were not produced. On an ordinary valid ring those consumers never
read them, so they can be omitted only when no alias, callback, diagnostic or
fault observation exposes the copy. Other domains require retained input or a
bounded stop. Do not zero-fill and claim an original value.

Receipt schedules SlidePiece unconditionally after decoding, except that when
`f!=0`, its directional destination has no city and ShowEnemyMoves is off, it
rewrites the packet kind to DrawSquare and replaces `a,b` with original source
`b,c`. The counter already incremented remains SlidePiece's. PossiblyCenter is
queued directly. DrawSquare requires its point visible in a map view; DrawTwo
requires either point visible; DrawArea and DrawMapRegion require their first
point visible. DrawMap and AbortKaboom do not decode arguments, retaining the
poll scratch. Kaboom and ShowNuke queue unconditionally; visibility is checked
when they execute.

Execute has no empty-ring guard. It dispatches the head, then decrements count,
advances head modulo 100 and returns the corresponding quotient. Ordinary calls
come from §3 with a positive count. Child mapping is:

| Kind | Execution |
| --- | --- |
| PossiblyCenter | If point `(a,b)` is visible to local human, ensure it visible with selector `c`. |
| DrawSquare | Redraw tile `(a,b)` in all views. |
| DrawTwo | Redraw combat tiles `(a,b,c,d)`. |
| DrawMap | Redraw all views `(localHuman,1)`. |
| DrawArea | Redraw neighbors of `(a,b)` in all views. |
| DrawMapRegion | Redraw area `(a,b,c,d,1)` in all views. |
| Kaboom | If point visible in a view, animate explosion `(a,b,c)`. |
| ShowNuke | If point visible in a view, animate nuclear blast `(a,b)`. |
| AbortKaboom | Increment its receipt counter again. |

SlidePiece first captures source x=`b`, y=`c`, direction=`d`, validates unit `a`
using U01 and checks source visibility. If visible, copy the queued unit into
the explicit special animation unit, detach its previous/next links, and ensure
source visible using that unit's owner. If `e==-1`, play move-piece sound. Obtain
the source tile and count its live stack; increment count if the captured unit's
position differs from source. A count of one clears the tile's unit-present bit.
For a larger count, set combat-animation origin to the captured position and
query the special unit's sea-unit aggregate: zero selects its next/previous
member or clears the bit if neither exists; one clears the bit; at least two
walks from the first stack unit to the first other sea unit, clearing the bit if
none is found. Keep U01 traversal behavior and live links.

Set move-animation active, animate the special unit to/source `(x,y,direction)`
with live sound fields, then clear active. Re-query source stack; if nonempty,
a client sets the tile bit directly, whereas a host invokes the forced
improvement setter `(x,y,1,1,1)`. The original special slot 2049 has dedicated
backing described by U01/F02; it is not an ordinary unit allocation or an excuse
to accept arbitrary out-of-range unit writes. P10 owns animation frames and
callbacks, P02 redraw details.

## 10. Queued popups and replies

The text sender runs only in network mode. Sum each selected string's length
plus terminator and four times numeric count; allocate that payload. Copy strings
in index order, rereading each string and length at copy time, then copy numbers
in order. Send Popup with `(kind,stringCount,numberCount,A,B,0)` and transfer the
payload allocation to §2. Nominal text capacities come from P04; negative counts,
length changes and overlong strings do not authorize an unsafe write.

Receipt of kind 61 (parley request) immediately replies choice two to sender if
local human is not in the human mask or a meeting is pending. Otherwise set
meeting-pending for kind 61. Drop kind 100 if the unit-origin prompt is already
open, and kind 101 if the city-information prompt is already open. Other requests
append an owned entry with kind, counts, A/B and copied payload; use null payload
when `g==0`. Link at tail or set head, then publish tail. Clear walks head links,
frees entries and finally clears tail. Allocation failure is not an original
successful empty enqueue.

Showing captures the head **before** constructing its dialog. If nonnull, set
popup busy, unlink it and clear tail if now empty. Load each string into its P04
argument slot by copy, then advance by a separate live length read plus one;
load numeric arguments afterward. The length is read from the destination
text-argument slot after copying, not from a cached incoming length. Install turn-popup-timeout idle callback and
execute the kind mapping in Appendix D. Normally free the entry, clear busy and
destruct the dialog. The parley acceptance path transfers this cleanup before
opening negotiation and returns a special completion indication to prevent a
second free/destruction. Unknown kinds have no presentation branch but still
complete ordinary entry cleanup.

### Parley request and reveal prompts

Parley request first updates both civilizations' last-contact turns, even when
already engaged elsewhere. If other-party is -1, set it to -2; fill argument zero
with the other title, space, leader, space, label 140, space, plural description, one with local leader, two with
local plural, three with attitude label (zero directly selects level zero,
otherwise use V04 attitude level). Install the parley auto-close callback and
show PARLEYREQUEST.

With no host handoff, if cancellation count is zero send its choice to A's mapped
connection. Reread cancellation count; if still zero and choice one, capture A,
free the popup entry, clear popup busy, open parley `(A,2)`, destruct the dialog
and return transferred-cleanup success. If cancellation count is nonzero, clear
it, update argument zero, restore turn-popup idle and show PARLEYCANCEL. Normal
completion sets other-party -1 and clears meeting-pending. With handoff active,
send choice two then perform that cleanup. If other-party was not -1 initially,
reply two without that cleanup; notably it does not clear pending in that branch.

The parley idle callback polls. Keep the dialog open only while handoff is zero,
cancellation count is zero and elapsed time is below 1201. Otherwise mark closed,
end modal and reset the timeout-start reading to a fresh clock value.

Kinds 100/101 act only on an open parley with A equal to the other party. Set
the matching prompt-open flag, load REVEALUNITORIGINS/REVEALCITYINFO, add the
appropriate reveal label (885/887) and common label 886, then show with flag 20.
Only choice one performs the following changes and send: set the local revealed
flag; in mode two, show/enable Conceal and hide/disable Reveal; otherwise hide/disable both. Send RevealCityOrigin or
RevealCityInfo to the other party with `(localHuman,signedRevealedByte)`.
For right-side exchange/demand kind two (units) or three (cities), show the left
information-hide button, hide the left information-show button and redraw that
list. Units additionally set local revealed and enable both sort buttons. After
either choice, clear the prompt-open flag at ordinary completion. No response on mismatch is implied.

## 11. Conformance cases and integration obligations


> Compare the gameplay/interchange observations in the
> [production scope](#production-contract-scope). Original memory/helper traces,
> arbitrary native callbacks and unsafe backing-byte cases in historical lists
> are optional reference, not additional production acceptance criteria.

| Input / schedule | Required observation |
| --- | --- |
| Ordinary message followed by priority message | Priority dequeues first; each ring remains FIFO. |
| Heartbeat while queues are full | Immediate counter/reply; no allocation or queue insertion. |
| Priority-only dequeue with ordinary messages only | Return zero; output variables unchanged. |
| Poll budget one and priority count 34 | Congested iteration does not decrement budget; later iterations can dispatch additional messages. |
| Kind 55 while setup incomplete | Count increments, packet is freed, poll stops without deletion child. |
| Directed transport returns zero | Send succeeds; broadcast of a nonempty packet with result zero fails. |
| Transport callback changes packet size | Broadcast compares result with changed size; raw-send success returns changed size. |
| Failed incremental send after mirror update | Mirror remains updated; a later ordinary scan can find no change. |
| Full receipt count already nonzero, later Join arrives | Shared application selects 16-bit codec regardless of Join's compression flag. |
| Full-image section checksum initially old | Transmitted prefix contains old checksum, live descriptor contains newly computed checksum. |
| Checksum of six bytes `01 02 03 04 05 06` | 513; size two yields zero. |
| Host lock requests coordinates already in its own row | Own row participates; request can conflict with itself. |
| Host unlock then remote sync | Host row -1, recipient coordinates/state/replies zero. |
| UnitCreated with record and count | Publish result/count, no record copy. |
| Failed CityDeleted copy span | Acknowledgement preceding the copy remains; overwrite excluded. |
| Drawing ring full outside a drawing | Discard pending drawings, queue AbortKaboom then DrawMap; original request lost. |
| Run of 32768 repeated bytes, 16-bit encoder | Two run tokens, counts 32767 and one; first is an original decoder discrepancy. |
| More restrictive peer-input profile | Record rejection policy separately; no claim to original malformed-input acceptance. |

Checks performed for this specification: 22 complete reference function bodies
were compiled into a finite Linux fixture, with **50 assertions** over headers,
packet payload ownership, overlapping argument reads, both queue rings,
heartbeat bypass, cursor wrap/full/allocation failure, send sequence/results,
section padding, checksums, supported compression tokens and batching. Actual
queue/priority and packet/header children were joined. Allocation, transport,
error/report and batch-send children were explicit adapters, not native services.
The optimized and AddressSanitizer/UndefinedBehaviorSanitizer runs passed with
wrapping arithmetic and strict-alias optimization disabled. Alignment checking
was disabled for the original unaligned x86 wire accesses; leak checking was
disabled under the tracing environment. The low-address mapped heap is an
adapter, not proof of arbitrary buffer bounds. No corruption behavior was tested
as a required outcome.

The 16-bit codec fixture uses the reference's repaired bodies only on the tested
subset where original semantics agree; it tests emission of the long token but
does not execute the original unsafe decode. Original instructions independently
establish the discrepancy in Appendix E. These cases do not prove native
transport, complete poll reentrancy, full-image/mirror joins, every popup, or
peer-to-peer conformance. X02 must join N01/N03/N05 with actual F03/F06/S01/S02
image producers, map/unit/city mutations, P04/P05/P10 modal/animation services,
failed sends and resumed scans, while retaining the documented failure prefixes
and unknown-input boundaries. A repaired port passing its tests is not sufficient
proof of original behavior.

## Appendix A. Message representations and constructors

These are original-peer boundary encodings, not mandatory internal records.
All multibyte integers are little-endian. Header offsets: magic 0, kind 4, total
size 8, sequence 12; each four bytes. Magic is `66606660` hexadecimal. Generic
arguments occupy offsets 16,20,24,28,32,36,40; payload begins at 44. Header-only
size is 16. A header constructor's receiver must have storage for size even
though the reference's small header type exposes only the first eight bytes.

| Record | Encoding and constructor effects |
| --- | --- |
| Address | Size 48; 32-byte local address at 16, cleared then bounded-copied; preserves sequence. |
| Named datagram | Size 116, outer kind 0; address at 16, target at 48, excluded local address at 80, inner kind at 112. Target/excluded fields cleared then bounded-copied. |
| GetServers | Named datagram, inner kind 1, empty target. |
| ServerInfo | Named datagram, inner kind 2, 292-byte game information at 116, total 408; sender explicitly stores session phase as signed 16. |
| ServerAnswer | Kind 19, information at 16, total 308. Filling game information does not set phase; retained phase is an input here. |
| PlayerList | Kind 4, total 640; 624-byte snapshot at 16: count, seven 84-byte slots, eight mappings. N01 owns slot fields and application. |
| PlayerNode | Kind 47, total 100; 84-byte slot at 16. Store ID; clear/copy address; string-copy player name; auxiliary/row/host/active zero, civilization -1. Name tail/padding remain retained in the original. |
| ServerPath | Kind 6, total 540; signed-16 file kind at 16, 260-byte name at 18, 262-byte subdirectory at 278. String copies do not clear unused tails. |
| PlayerInfo | Kind 46, total 280; civ/leader-index/gender/has-names at 16/20/24/28; three 24-byte strings at 32/56/80; has-titles at 104; seven 24-byte titles at 108; city style at 276. Constructor zeros IDs/gender/style, first byte of each name, all title bytes, then the two presence flags. Remaining name bytes/sequence are retained. |
| StartWhoseTurn | Kind 105, total 20; full 32-bit civilization at 16, reduced by receipt. |
| Parley offer | Header then signed-32 mode/civ/other/kind at 16/20/24/28; item at 32 has type/size/value, then its values. Exchange's second item follows first item's declared size. V05 owns item meanings and order. |

The game-information record has these offsets relative to its own start (hex):
address `00` (32 bytes), retained list links `20/24` (four each), last-seen `28`
and row ID `2c` (signed32), version `30` (64 bytes), game name `70` and host name
`90` (32 each), open-game `b0` (signed16), difficulty/barbarians/Alive/player-mask
`b2..b5` (bytes), padding `b6..b7`, timer `b8` (signed32), scenario name `bc`
(80 bytes), movement/production doubles `10c/10e` (signed16), flags `110`
(unsigned16), then signed16 first-launch/annual increment/start-year/turn/map
width/map height/simultaneous/phase at `112,114,116,118,11a,11c,11e,120`, and
padding `122..123`. Pointer-shaped list fields are retained serialized bytes;
a remote pointer is not a local list link to dereference.

N01 supplies the game-information and slot logical fields. F03 supplies label
ordinals used in rule tables; F06 supplies unit/city/leader and shared state
serialization. Fixed string copies can fill a field without termination. Treat
later string consumption according to its produced backing, not an implicit
terminator beyond the field. The complete-initialization repairs in the reference
are not evidence that original outgoing padding was zero.

## Appendix B. Complete kind and producer schema catalog

Names below are wire diagnostic names without their `NM_` prefix. `H` is a
16-byte header; `Gk` is a 44-byte generic envelope forwarding the first `k`
arguments and zeroing the rest, with no payload. Every G receiver still reads
seven fields. `Record` and `Image` entries use the specified special layouts.
Types 42..102 are priority except immediate heartbeat handling. An unimplemented
producer returns zero; a received unknown kind reaches the diagnostic default
only after its counter/setup checks. Unknown indices do not have a valid counter
allocation merely because the switch has a default.

| ID | Kind | Producer schema |
| --- | --- | --- |
| 0 | DATAGRAM | Named-datagram container; no direct producer |
| 1 | GET_SERVERS | Named datagram, inner 1 |
| 2 | SERVER_INFO | Named datagram + game info, inner 2 |
| 3 | CLIENT_SERVER | No producer / unknown dispatch |
| 4 | PLAYER_LIST | PlayerList record |
| 5 | SERVER_REJECT | H |
| 6 | SERVER_PATH | ServerPath record |
| 7 | FULL_GAMESTATE | G1 + full image |
| 8 | DIFF_GAMESTATE | G1 + delta image |
| 9 | JOIN_GAMESTATE | G1 + join image |
| 10 | MAP_STRUCT | G1 + map-header image |
| 11 | SERVER_CANCELED_START | H |
| 12 | LOST_SERVER_ON_JOIN | H |
| 13 | CLIENT_QUIT | H |
| 14 | SERVER_QUIT_REQUEST | H |
| 15 | SERVER_QUIT | H |
| 16 | SERVER_QUIT_ACK | H |
| 17 | SERVER_FULL | H |
| 18 | SERVER_QUERY | H |
| 19 | SERVER_ANSWER | ServerAnswer record |
| 20 | CIVILIZATION_PING | H |
| 21 | DIFFERENCE_ENGINE | Caller-owned raw diff packet |
| 22 | START_CIVILIZATION | H |
| 23 | COSMIC | G0 + 41-byte Cosmic table |
| 24 | CIVILIZE | G0 + 1600-byte Advances table |
| 25 | IMPROVE | G0 + 536-byte Improvements table |
| 26 | ENDWONDER | G0 + 28-byte Wonder expiration table |
| 27 | UNITS | G0 + 1240-byte Unit types table |
| 28 | TERRAIN | G0 + 792-byte Terrains table |
| 29 | GOVT | G0 + 28-byte Government names table |
| 30 | GOVT_TITLE | G0 + 56-byte Government titles table |
| 31 | LEADERS | G0 + 1008-byte Leaders table |
| 32 | KINGS | G0 + 29988-byte Civilizations; oversized copy is excluded bug table |
| 33 | CARAVAN | G0 + 64-byte Commodity names table |
| 34 | ORDERS | G0 + 104-byte Order names table |
| 35 | DIFFICULTY | G0 + 24-byte Difficulty names table |
| 36 | ATTITUDES | G0 + 36-byte Attitude names table |
| 37 | EVENTSLOADED | G2 |
| 38 | NATION_ACCEPT | H |
| 39 | NATION_REJECT | H |
| 40 | GAME_GOING | G1 |
| 41 | BEFORE_PRIORITY_ALPHA | Priority boundary marker; no producer |
| 42 | LOCK_DATA | G1 |
| 43 | HEART_BEAT | H |
| 44 | HEART_BEAT_ACK | H |
| 45 | QUIT_REQUEST | G1 |
| 46 | PLAYER_INFO | Retained PlayerInfo record |
| 47 | PLAYER_LIST_NODE | PlayerNode record built from argument a |
| 48 | LOCK_NATION | G2 on client; host-local branch (§2) |
| 49 | UNLOCK_NATION | G2 on client; host-local branch (§2) |
| 50 | CLIENT_CANCELED_START | H |
| 51 | MULTIOPTIONS_CHANGE | G3 |
| 52 | KILL_OR_QUIT | G1 |
| 53 | KILL_OR_QUIT_ACK | H |
| 54 | BEFORE_ALLOCATION | Allocation boundary marker; no producer |
| 55 | DELETE_UNIT | G1 |
| 56 | UNIT_DELETED | Unit record reply (below) |
| 57 | DELETE_CITY | G1 |
| 58 | CITY_DELETED | City record reply (below) |
| 59 | CREATE_CITY | G3 |
| 60 | CITY_CREATED | City result/record reply (below) |
| 61 | CREATE_UNIT | G4 |
| 62 | UNIT_CREATED | Unit result/record reply (below) |
| 63 | PICK_UP_UNIT | G1 |
| 64 | UNIT_PICKED_UP | H |
| 65 | PUT_DOWN_UNIT | G3 |
| 66 | UNIT_PUT_DOWN | H |
| 67 | MOVE_TO_BOTTOM | G1 |
| 68 | BOTTOMED | H |
| 69 | RELOCATE_UNIT | G3 |
| 70 | UNIT_RELOCATED | H |
| 71 | ACQUIRE_WONDER | G5 |
| 72 | WONDER_ACQUIRED | G1 |
| 73 | STACK_SHIP | G2 |
| 74 | SHIP_STACKED | G1 |
| 75 | STACK_UNIT | G1 |
| 76 | UNIT_STACKED | H |
| 77 | DELETE_SAFELY | G1 |
| 78 | SAFELY_DELETED | H |
| 79 | DELETE_VISIBLY | G1 |
| 80 | VISIBLY_DELETED | H |
| 81 | LOCK_MAP | G5 |
| 82 | UNLOCK_MAP | G1 |
| 83 | LOCK_MAP_ACK | G2 |
| 84 | UNLOCK_MAP_ACK | G1 |
| 85 | LOCK_MAP_SYNC | G5 |
| 86 | NEWTURNTIMER_REPLY | G1 |
| 87 | PMCHANGE_REPLY | G1 |
| 88 | DO_APOLLO | H |
| 89 | MAP_PACKETS | G0 + used batch words |
| 90 | REALTIME_STACKER | G6 |
| 91 | REALTIME_STACKER_ACKER | H |
| 92 | ALPHA_DIFFERENCE_ENGINE | Caller-owned raw diff packet |
| 93 | DRAG_HOLD_ON | H |
| 94 | DRAG_HOLD_OFF | H |
| 95 | NEW_CIV | G1 |
| 96 | KILL_CIV | G2 |
| 97 | NEW_CIV_ACK | G1 |
| 98 | KILL_CIV_ACK | G1 |
| 99 | BRIBED_UNIT | G3 |
| 100 | DEATH_STACK | G5 |
| 101 | HOLD_FOR_JOIN | H |
| 102 | CONTINUE_AFTER_JOIN | H |
| 103 | AFTER_PRIORITY_ALPHA | Priority boundary marker; no producer |
| 104 | CHAT | G3 + terminated text (length + 1) |
| 105 | START_WHOSE_TURN | StartWhoseTurn record |
| 106 | POPUP | G5 + owned popup payload (allocator size) |
| 107 | END_SEQUENCE | G3 |
| 108 | TIMER_CHANGE | G1 |
| 109 | START_TURN_TIMER | H |
| 110 | STOP_TURN_TIMER | H |
| 111 | RESTART_TURN_TIMER | H |
| 112 | SLIDE_PIECE | G6 + captured 32-byte unit |
| 113 | POSSIBLY_CENTER | G3 |
| 114 | DRAW_SQUARE | G2 |
| 115 | DRAW_TWO | G4 |
| 116 | DRAW_MAP | H |
| 117 | DRAW_AREA | G2 |
| 118 | DRAW_MAP_REGION | G4 |
| 119 | CANESCAPE_REPLY | G1 |
| 120 | INFO_STATUS | H |
| 121 | INFO_UPDATE | H |
| 122 | SOUND_PLAY | G2 |
| 123 | DESERT_REPLY | G1 |
| 124 | KABOOM | G3 |
| 125 | SHOW_NUKE | G2 |
| 126 | ASK_ALLY | G2 |
| 127 | REPORTS_UPDATE | G1 |
| 128 | PARLEYREQUEST_REPLY | G1 |
| 129 | PARLEYREQUEST_CANCEL | H |
| 130 | PARLEY_TRANSACTION | Caller-owned parley offer |
| 131 | PARLEY_ACCEPT_OFFER | H |
| 132 | PARLEY_NO_THANKS | H |
| 133 | PARLEY_COUNTER_OFFER | H |
| 134 | PARLEY_EXECUTE | Caller-owned parley offer |
| 135 | CITYWIN_CHECK_UNIT | G3 |
| 136 | CITYWIN_CHECK_CITY | G1 |
| 137 | CITYWIN_DELETED | G1 |
| 138 | CITYWIN_REDRAW | H |
| 139 | PRODUCTION_BEGIN | H |
| 140 | PRODUCTION_COMPLETE | G1 |
| 141 | AI_MOVEMENT | H |
| 142 | HUMAN_MOVEMENT | H |
| 143 | HUMAN_MOVE_COMPLETE | G1 |
| 144 | FEATURE_SET | G5 |
| 145 | TERRAIN_SET | G4 |
| 146 | SEEN_SET | G5 |
| 147 | SITE_SET | G4 |
| 148 | OWNER_SET | G4 |
| 149 | REGION_SET | G4 |
| 150 | CITY_USING_SET | G4 |
| 151 | FEAT_LOC_SET | G6 |
| 152 | SET_ATTITUDE | G3 |
| 153 | DO_AIPARLEY | G4 |
| 154 | SET_SLIDE_SOUND | G2 |
| 155 | SET_CIV | G4 |
| 156 | READY_TO_START | H |
| 157 | KINGDOM_RATES | H |
| 158 | TAX_CHECK | H |
| 159 | PICK_GOVT | G1 |
| 160 | POLITICAL_VIOLATIONS | G2 |
| 161 | POLITICAL_ATTITUDE | G2 |
| 162 | TURN_FINISHED | G1 |
| 163 | ABORT_KABOOM | H |
| 164 | PRETEXT | G2 |
| 165 | REVEAL_CITY_INFO | G2 |
| 166 | HIDE_CITY_INFO | G2 |
| 167 | REVEAL_CITY_ORIGIN | G2 |
| 168 | HIDE_CITY_ORIGIN | G2 |

For UnitDeleted/CityDeleted and successful UnitCreated/CityCreated, arguments
are `(a,currentUnitOrCityCount,c,d,e,f,payloadExtent)` and payload is the indexed
32-byte unit or 88-byte city. Original calls commonly supply zero for `c..f`,
but the producer forwards them. A creation result of -1 instead sends
`(-1,0,0,0,0,0,0)` with no record. Sending a deletion reply for an invalid record
index therefore has its own read boundary even if the delete child tolerated
that index. MapPackets payload starts with record count, then §8 records;
`g=usedWords*4`. Cosmic's 41-byte extent includes its 22 rule bytes and 19-byte
reserved tail; those bytes require produced/retained input, not an invented rule.

The [F03 table layouts](F03-rules-and-configuration.md#tables-markers-and-strings)
define cosmic, advance, improvement, unit-type, terrain and leader bytes. Additional
rule payloads are ordered arrays: 28 signed-byte wonder-expiration advances;
seven signed-32 government-name IDs; seven male/female pairs of signed-32 title
IDs; 16 signed-32 commodity IDs; thirteen order records (signed-32 name ID,
one-byte key, three retained bytes); six signed-32 difficulty IDs; nine signed-32
attitude IDs. IDs refer to the F03 label pool, not transmitted string pointers.

Full/Delta/Join/MapStruct set `a=(producerResult!=-1)`, other arguments zero,
and use allocator extent for the payload. Raw diff packets have their own
retained unused arguments; record count `a` is the only argument assigned by
the diff producer. Sequence is overwritten by ordinary sending. Do not normalize
all unused raw-packet bytes without a declared interoperability profile.

## Appendix C. Synchronization representation

Each registered section has data size, padded size, ID, checksum, mirror offset
and a data location. The original descriptor is six 32-bit words; images transmit
only the first five, followed by data. Mirror offset is an internal offset
included in the original wire prefix, not an address to trust on receipt.

| ID | Logical data | Original data extent |
| --- | --- | --- |
| 0 | Version marker | 4: `2c 00 00 00` |
| 1 | Game state | `0x14c` |
| 2 | Civilization names | `0x790` |
| 3 | Eight civilizations | `0x2ca0` |
| 4 | Landmass table | `0x400` |
| 5 | Ordinary unit array | `0x10000`; full/delta images trim to live count ×32 |
| 6 | City array | `0x5800`; full/delta images trim to live count ×88 |
| 7 | Leaders | `0x3f0` |
| 8 | Power history | `0x4b0` |
| 9 | Scenario parameters from flags | 100 |
| 10 | Destroyed-civilization count and records | `0x154` |
| 11 | Saved civilization views and multiplayer state | `0x494` |
| 12 | Map header and retained alignment tail | 14-byte F06 header followed by two retained bytes |
| 13,14 | Coarse map layers zero/one | quarter width × quarter height each |
| 15..21 | Remembered fields for civilizations 1..7 | map area each; civilization zero omitted |
| 22 | Map tiles | map area ×6 |
| 23 | Locked event pool | 50000; included in images, outside ordinary mirror |

Use F06's current record encodings and S02's event representation. Section 11 is
the same multiplayer aggregate described by F06, beginning with saved cursor
coordinates. Padding to four bytes participates in mirror comparisons but not
ordinary image data/checksum extents. Only image unit/city sizes are trimmed;
mirror scanning uses their entire registered arrays.

A diff packet has the 44-byte generic envelope, record count at offset 16 and
records beginning at 44. Each record is three 32-bit words: tag, offset, encoded
size, then encoded data. Compressed tag is `sectionID | 0x8000`; decoding masks
with `0x7fff`. The original raw branch uses the whole tag without masking.
The original packet area is 1040 bytes; default flush threshold is 1024. A new
12-byte record header is not included in the run-growth threshold expression.
The encoder's packet end is that fixed area end, not the dynamic mirror size.

| Send flag | Original value / behavior |
| --- | --- |
| Invalidate, flush after each send | `1` |
| Common caller marker with no branch here | `2` |
| Clock limit | `4` |
| Changed-run count limit | `8` |
| Leave mirror stale | `16` |
| Force / alternate kind | Low byte of separate arguments; alternate selects kind 92 instead of 21 |

Original receive rings use slots 0..399 for priority and 400..1999 for ordinary;
slot fields are sender, pointer, captured size. A drawing record is 64 bytes:
kind, seven scalars, 32-byte unit. Popup entry prefix is seven 32-bit words:
next, kind, string count, numeric count, A, B, payload extent; payload follows.
These storage choices are validation mappings. Capacity, ordering and visible
failure behavior remain required even in another representation.

## Appendix D. Popup-kind bindings

A/B are the popup entry’s two auxiliary arguments, not the generic envelope’s
first two arguments. These resource names and call flags bind §10 to P04/P06/
P07/P10. Modal child results, substitutions, event pumping and artwork come
from those presentation owners. Effects occur before the following dialog.
A plain popup uses the ordinary game-text dialog; a sprite popup has zoom zero
unless stated otherwise. Unit-instance and unit-type dialogs are distinct.

| Kind | Resource / content | Ordered presentation and response |
| --- | --- | --- |
| 0 | RETIREDIE | Plain game-text popup. |
| 1 | RETIREAI | Plain game-text popup. |
| 2 | BARBARIANS | Unit-instance dialog for A. |
| 3 | GLOBALWARMING | Information icon, zoom zero. |
| 4 | FEARWARMING | Information icon, zoom zero. |
| 5 | Historian | [Historian report](P07-advisors-reports-and-history.md#5-historian-report) (P07). |
| 6 | EAGLEHASLANDED | Information icon, zoom zero. |
| 7 | SCENARIOENDS | Tutorial icon, zoom zero. |
| 8 | SCENARIOEND | Tutorial icon, zoom zero. |
| 9 | PLANRETIRE | Replace string args 0/1 with local title/leader; tutorial icon, zoom zero. |
| 10 | DORETIRE | Replace string arg 1 with local leader; tutorial icon, zoom zero. |
| 11 | LAUNCHED | Plain popup, then run spaceship window with civilization selector -A. |
| 12 | SPACERETURNS | Plain game-text popup. |
| 13 | SPACEDESTROYED | Plain game-text popup. |
| 14 | DESTROYED | Play music track (2,1), then civilization portrait A, zoom zero. |
| 15 | CARAVAN | Effect 42 when B==0 else 22, parameters (1,0,0); unit-instance dialog A. |
| 16 | FOODCARAVAN | Effect 42 when B==0 else 22, parameters (1,0,0); unit-instance dialog A. |
| 17 | CARAVANOTHER | Unit-type dialog A with flags B. |
| 18 | STARTWONDER | Load text, add improvement sprite A, set zoom B, show modal zero. |
| 19 | SWITCHWONDER | Load text, add improvement sprite A, set zoom B, show modal zero. |
| 20 | ABANDONWONDER | Load text, add improvement sprite A, set zoom B, show modal zero. |
| 21 | PARADROP | Unit-instance dialog for A. |
| 22 | SCHISM | Plain game-text popup. |
| 23 | CANESCAPE | Information icon; send CanEscapeReply(choice) to civilization A’s mapped connection. |
| 24 | ESCAPE | City dialog A. |
| 25 | CAPTUREWONDER | Load text, add improvement sprite A, zoom 8, show modal zero. |
| 26 | LOSTWONDER | Load text, add improvement sprite A, zoom 8, show modal zero. |
| 27 | CITYCAPTURE | Play Barracks (0,0,0); city-picture popup (B,70,1,A). |
| 28 | CITYWINALLY | Play Barracks (0,0,0); city-picture popup (B,70,1,A). |
| 29 | CITYLOSEALLY | Play Barracks (0,0,0); city-picture popup (B,70,1,A). |
| 30 | CITYCAPTURE2 | Play Barracks (0,0,0); city-picture popup (B,70,1,A). |
| 31 | PARTISANS | Unit-instance dialog for A. |
| 32 | PROMOTED | Unit-instance dialog for A. |
| 33 | INCORRUPTIBLE | Democracy government sprite, zoom zero. |
| 34 | DESERTED | Unit-instance dialog for A. |
| 35 | DESERT | Unit-instance dialog B; send DesertReply(choice) to civilization A’s mapped connection. |
| 36 | DESERT2 | Unit-instance dialog B; send DesertReply(choice) to civilization A’s mapped connection. |
| 37 | BLEWITUP | Unit-instance dialog for A. |
| 38 | BOND007 | Unit sprite A, zoom B. |
| 39 | BOND | Unit sprite A, zoom B. |
| 40 | BONDGLORY | Unit sprite A, zoom B. |
| 41 | USEWEAPONS | Manhattan improvement, zoom 8. |
| 42 | SDI | SDI Defense improvement, zoom 8. |
| 43 | CHANGED | Government sprite A, zoom B. |
| 44 | OVERTHROWN | Government sprite A, zoom B. |
| 45 | BREAKCEASE | Unit-instance dialog for A. |
| 46 | SNEAK | Unit-instance dialog for A. |
| 47 | MISSILEATTACK | Unit-instance dialog for A. |
| 48 | PEARLHARBOR | Unit-instance dialog for A. |
| 49 | BATTERY2 | Improvement A, zoom 8. |
| 50 | BATTERY | Improvement A, zoom 8. |
| 51 | SCRAMBLE | Unit-instance dialog for A. |
| 52 | AMPHIBMOTIZE | Unit-instance dialog for A. |
| 53 | MULTIPLELOSE | Plain game-text popup. |
| 54 | MULTIPLEWIN | Plain game-text popup. |
| 55 | RANSOM | Unit-type dialog A with flags zero. |
| 56 | ACTIVATEALLY | Diplomacy portrait if any civilization knows Communism, otherwise allied icon; zoom zero. |
| 57 | ALLYHELPS | Diplomacy portrait if any civilization knows Communism, otherwise allied icon; zoom zero. |
| 58 | CANCELPEACE | Plain game-text popup. |
| 59 | ALLYUNDERATTACK | Plain game-text popup. |
| 60 | ALLYATTACKING | Plain game-text popup. |
| 61 | PARLEYREQUEST / PARLEYCANCEL | Parley request procedure in §10. |
| 62 | UPGRADE | Unit-type dialog A; flags 8 if B!=0 else zero. |
| 63 | NEWPLAYER | Information icon only if local civilization’s mapped connection differs from A. |
| 64 | NEWTURNTIMERCLIENT | Load section with flag 0x2000000; buttons 852,853; show 40; send NewTurnTimerReply(extraResult) to zero. |
| 65 | NEWTURNTIMERNO | Information icon, zoom zero. |
| 66 | NEWTURNTIMERYES | Information icon, zoom zero. |
| 67 | PMCHANGECLIENT | Load section with flag 0x2000000; buttons 852,853; show zero; send PMChangeReply(extraResult) to zero. |
| 68 | PMCHANGENO | Information icon, zoom zero. |
| 69 | PMCHANGEYES | Information icon, zoom zero. |
| 70 | MANHATTAN | Play LargeExplosion (1,0,0); city notification (A,1,city-notification sprite). |
| 71 | BUILT2 | Load text; use wonder sprite A if A>=0, else improvement B; zoom 8; modal zero. |
| 72 | MOVECAPITAL | Plain game-text popup. |
| 73 | ASTRONAUTS | Plain game-text popup. |
| 74 | ALMOSTWONDER | Load text, add improvement sprite A, zoom 8, show modal zero. |
| 75 | STILLWONDER1 | Load text, add improvement sprite A, zoom 8, show modal zero. |
| 76 | STILLWONDER2 | Load text, add improvement sprite A, zoom 8, show modal zero. |
| 77 | ENDWONDER | Load text; add advance A’s category/epoch sprite (both signed bytes), then improvement B; zoom 8; modal zero. |
| 78 | BARBARIANSLAND | Unit-instance dialog for A. |
| 79 | Received text lines | Default dialog; add each received string as a line with all three positioning selectors -1 and flags zero; modal zero. |
| 80 | WARENDS | Ceasefire icon, zoom zero. |
| 81 | MILITARYAID1 | Unit-instance dialog for A. |
| 82 | MILITARYAID2 | Unit-instance dialog for A. |
| 83 | LOSTCLIENT | Information icon, zoom zero. |
| 84 | GOLDENAGE | Philosophy advance, zoom 8. |
| 85 | ENEMYEMBASSY | Play SpySound (1,0,0); information icon, zoom zero. |
| 86 | ENEMYINVESTIGATE | Play SpySound (1,0,0); information icon, zoom zero. |
| 87 | STEAL | Play SpySound if civilization A lacks Gunpowder, else MediumExplosion, (1,0,0); information icon. |
| 88 | FOILEDAGAIN | Play SpySound (1,0,0); information icon, zoom zero. |
| 89 | SABOTAGEONE | Same sound decision using civilization B; improvement sprite A, zoom 8. |
| 90 | SABOTAGETWO | Same sound decision using civilization A; plain popup. |
| 91 | WATERSUPPLY | Play SpySound (1,0,0); information icon, zoom zero. |
| 92 | PLANTEDNUKE | Clear composed text, append this resource key, then Manhattan improvement popup with composed text and zoom 8. |
| 93 | PLANTEDNUKE2 | Clear composed text, append this resource key, then Manhattan improvement popup with composed text and zoom 8. |
| 94 | CIVILWAR | Play SpySound (1,0,0); plain popup. |
| 95 | UPMINE | Play Feedback03 (0,0,0); unit-instance dialog A. |
| 96 | UPYOURS | Play Feedback03 (0,0,0); unit-instance dialog A. |
| 97 | UPYOURSTOO | Play Feedback03 (0,0,0); unit-instance dialog A. |
| 98 | MERCDECLARE | Plain game-text popup. |
| 99 | TOOKCIV | Advance A, zoom zero. |
| 100 | REVEALUNITORIGINS | Matching reveal prompt in §10. |
| 101 | REVEALCITYINFO | Matching reveal prompt in §10. |

## Appendix E. Evidence, repairs and excluded defects

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is the classic paths in OpenCiv2 revision
`a71e8aefffe04e6f719e0e213b027b128a747711`; the inspected checkout at
`fc2155ccdb2123c1137babab6fa39e38152278c9` has matching reviewed bodies and
supporting layouts. Main navigation:

| Contract | Evidence |
| --- | --- |
| Messages, callbacks, queues, poll/dispatch, locks, popups | [network.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/network.cpp): message constructors, send/receive, `NetMsgQueue`, `NetSession`, batch and popup families |
| Images, mirror scan, section registry, 16-bit codecs | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): `diffeng_*`, `net_send_state_diff`, `DiffEngSection::init` |
| Checksum and byte codec | [support.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/support.cpp): `mem_checksum`, `mem_rle_compress`, `mem_rle_decompress` |
| Record schemas and kind numbers | [classes](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2classes.h), [types](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h), [enums](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h) |
| Defaults, literals, physical validation mappings | [data](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/gamedata.cpp), [network state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/network.h), [map state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/map.h) |
| Repairs requiring separation | [BUGS.md](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md), [network evidence](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/bugfix-evidence-network.md) |

The independent reconstruction was consulted at
`civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, particularly its network packet,
receive/enqueue/poll and state compression/image/registry implementations and
boundary annotations. Those annotations also contain repairs; they are not all
normative original behavior. Source links are evidence navigation for researchers,
not dependencies required to implement this specification.

The original `Civ2/civ2.exe` SHA-256 is
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`, carrying
`5.4.0f Multiplayer 26-March-99`. Direct instruction checks establish:

- Broadcast callback at `0059bb65` compares captured size with 16 and accepts
  equality before checking magic; it does not call the port's type/extent validator.
- Size pass at `004b24c0` loops when source is below end without a two-byte token
  bound; its `004b24df` comparison treats 32767 as a run.
- Decoder at `004b2584` instead branches to literal handling for code >=32767;
  `004b25e3` subtracts 32768 and stores the low 16 bits before the copy. Thus the
  65535-byte literal discrepancy is established, not guessed from a repair.

Relevant differences and safe boundaries:

| Evidence / condition | Classification |
| --- | --- |
| BF-092 size/decode bounds, corrected 32767 test; encoded-literal final-byte repair | Reconstruction changes. §7 gives original predicates and retained read input; oversized writes excluded. |
| BF-083 bounded append return | Safe replacement for an original report-followed-by-overwrite path; no corruption required. |
| BF-177/179/180/183/223/246/280 receive/poll checks and further type-specific network guards | Additional validation, not original blanket authority or packet acceptance. Use as a declared stricter profile if desired. |
| BF-277 diff extent validator and added decode-failure logging | Port changes preceding/inside the original record loop. Preserve original valid writes and local-field restoration; reject unsafe spans explicitly. |
| BF-181 diagnostic name bounds; BF-104 format handling | Diagnostic read/format repairs. Do not invent unknown name bytes or varargs; ordinary produced diagnostics can match. |
| BF-286/293 record clearing | Removes retained original name tails/padding/phase inputs. Zero is not a proven original value. |
| KINGS payload extent `0x7524` versus civilization array `0x2ca0` | Oversized source read needs backing; receiver overwrite beyond its civilization object is an excluded bug. Omit/reject that copy safely; it is absent from the normal rule batch. |
| Invalid queue/section/civilization/argument/string indices | Apply read boundaries separately from excluded writes. Do not model physical overlap with adjacent state as legitimate assignments. |
| Section 23's retained mirror offset; growth beyond saved unit snapshots | Requires valid produced spans for the actual operation. No inferred mirror storage or saved coordinates. |
| Failed full-image encoding | Partial writes within its owned destination are retained, not an excluded overwrite; do not restore the raw copy without declaring changed behavior. |
| Unproduced drawing/turn scratch arguments | Only proven unobserved copies may be folded; otherwise supplied retained input or bounded stop. |

Original storage offsets useful for validation include the diff packet at
`00679ff0` through `0067a3ff`, scan state around it, section descriptors beginning
`0067a410`, and the client mirror end aliased to section 15's mirror offset.
These aliases are evidence for the specified values, not a required memory map.
The Pretext treaty mutations use set `0x2000` and clear-alliance `0x0008`; tile
unit-present is `0x01`. F02/W01/V04 own their semantic meanings.

## Appendix F. Original presentation reference

The following original input geometry and drawing procedures are optional under
X01's presentation policy. The main text retains semantic selections, calculations,
file/wire effects and required ordering. This appendix is for original-adapter
validation, not a required UI design.

### Original StartWhoseTurn caption drawing

- StartWhoseTurn constructs its special record, invalidates the main-view timer
  caption cache, draws the caption, captures the frame caption rectangle and
  blits it before sending.
