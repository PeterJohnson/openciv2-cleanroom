# N01 — Multiplayer session and lobby

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in N01 |
| --- | --- |
| Gameplay | Participant/tribe authority, admission/passwords, join/resume, dropped players and host transfer. |
| Original files/content | F06 network saves and F03 chosen content; discovery metadata has N02/N05 wire definitions. |
| Original peers | Original discovery, connection/claim/password/world exchange, routing, host roles and phase transitions; UI and socket backend are free. |
| Executable comparison only | Lobby screens, DirectPlay/native provider objects, allocation/reset traces and internal error-number conventions. |

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
network-session setup, discovery and lobby choices, player/civilization
assignment, password use, client joining, dropped players and host migration.
It specifies ordered state changes and logical message calls.

[F01](F01-determinism-and-arithmetic.md) supplies arithmetic and sampling laws;
[F02](F02-game-state-and-invariants.md) supplies game identities;
[F03](F03-rules-and-configuration.md) supplies rules/preferences;
[F04](F04-turn-sequencing.md) supplies calendar and simulation phases;
[F05](F05-game-setup-and-starting-positions.md) supplies new-world setup;
[F06](F06-save-load-and-file-formats.md) supplies files and password encoding;
[S01](S01-scenario-loading-and-parameters.md) supplies host scenario preparation.
[N02](N02-network-commands-and-state-synchronization.md) owns message schemas/dispatch,
command authority, vote-message replies and state transfer;
[N03](N03-multiplayer-turn-sequencing.md) owns turn sequencing and timer
mechanics; [N04](N04-hotseat-and-player-handoff.md) owns local hotseat/handoff;
[N05](N05-transport-and-wire-protocol.md) owns transport-library and wire behavior. P01/P02 own map views/rendering,
P04 dialogs, P05 input/native message delivery, P03 image primitives, and P10 audio. Calls to these children can fail,
reenter or change live state. Do not replace them by assumed-success operations.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
semantic publications and failure effects. Only dependencies within the binding
scope above constrain evaluation; storage/API choices need no original-trace
equivalence proof. External file/wire definitions remain binding at adapters.

Admission, slot claims, password decisions, host transfer and callback/timeout
boundaries retain their order. Internal preparation may be reorganized without
changing peer-visible requests, replies, failures or intermediate session state.

## Compatibility policy

The baseline network target is X01's [required TCP/IP path](X01-classic-compatibility-contract.md#required-and-optional-transports),
including UDP discovery and mixed original peers with either engine hosting.
IPX/SPX, modem/dial-up, serial and legacy launch-lobby/provider branches below are
optional extensions, not required setup choices. Hide or identify unavailable
providers without changing protocol-visible mode values for supported sessions.

X01's [presentation policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy)
permits a new lobby and setup UI; keep player/tribe identity, password semantics,
option negotiation, cancellation replies, busy/timeout gates, joining and host
transfer. Harmless local widget failures may clean up safely; required session
failure/disconnect effects remain. Reject malformed input under X01 without
rejecting normal partial delivery or valid original-peer messages.

## 1. Logical state and domains

Distinguish **player slot**, **connection ID**, **civilization** and **tribe**.
The ordinary session has seven player slots 0…6 and eight civilization indices
0…7; ordinary human civilizations are 1…7. Slot 0 initially represents the host.
The player table contains connection ID, address, player name, active/host flags,
civilization (−1 while unassigned), retained auxiliary data and lobby row ID.
A separate civilization-to-slot map has eight entries. Local slot, local human,
stored player and current-turn civilization are independent state variables.

Session state also includes protocol/connection type, connected/to-server flags,
host role, server ID, open-game setting, current phase, handoff stage, player
count, expected/pending reconnect counts, human/pending-human/claimed masks,
received-message counters, error counts, lists and current dialogs, directories,
loaded-file metadata, saved/live resume flags, and shared names/options. A zero
received count is not the absence of a connection. List row IDs are not player
slots. Do not infer extra consistency repairs beyond the stores below.

Nominal messages/resources provide valid identities, terminated bounded strings,
valid controls and expected file/image resources. Scalar expressions follow
F01 reference arithmetic; preferences and membership use their specified
publication conversions. Nominal menu choices need no redundant narrowing. For timing, signed differences and strict/non-strict comparisons
matter across wrap. Send/flush/open/close return values are checked only where
stated. Polling can synchronously dispatch messages and mutate the state being
awaited. State is live unless explicitly captured.

Off-nominal reads require established backing, supplied retained input or a
bounded stop. A stricter declared input policy may reject them. Out-of-bounds
writes are excluded bugs under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
including writes through an unassigned civilization or an invalid connection
ID. Preserve prior valid effects, safely omit/reject/stop at the unsafe write,
and never translate corruption into assignments to adjacent logical fields.
Cancellation does not imply rollback of settings, names, masks, received state
or transport effects already performed.

## 2. Entering network setup

### Multiplayer-type selection

Read the network-timeout preference; absent value−1 writes the default 30 and
stores 30 seconds. Other values are retained at this step. Read adapter index;
absent−1 writes 0 and initializes both adapter selectors to 0. On every menu
attempt, publish the captured adapter selectors, set maximum players 7, then
read MaxPlayers. A signed value>4 sets the maximum to `min(value,7)` and clears
that INI override. Values≤4 leave 7.

Clear game mode, Alive and Human sets, restore scenario directory to home,
clear events-loaded and reset passwords using F06 §9: **255 primary draws per
attempt**, including attempts later cancelled. Without lobby launch, show the
four-choice menu using its remembered preference. Any negative reply returns
failure 1; otherwise store the signed 16 choice. A lobby launch selects Internet
without this menu.

| Choice | Next operation |
| --- | --- |
|0|N04 local hotseat setup.|
|1|Network setup with connection type 1 (LAN).|
|2|Network setup with type 0 (Internet). Success returns 0 immediately. On failure, capture current adapter selectors; a lobby launch returns failure 1, otherwise retry this menu.|
|3|Set maximum players 2, then network setup with type 4.|

After other ordinary branches, capture current adapters; success returns 0 and
failure retries. An unsupported supplied choice reaches the original diagnostic
and, if that service returns, a success-valued default branch. It is not an
additional ordinary transport menu. Callable modem/serial paths below are
separate from the four nominal menu entries.

### Connection choice and reset loop

Network setup constructs dialog/palette and publishes incoming connection type.
For type 1 or 4, show protocol choice with the remembered preference; type 4
visually disables its second option. Exactly−1 returns failure 1. Other replies
select protocol 0 when zero, protocol 1 when nonzero, storing the Boolean in the
preference. Type 1 initially sets game mode 3. Type 4 initially sets mode 4, then
changes both local and shared connection type to 0. Direct type 0 selects
protocol 0/mode 4; type 2 selects protocol 2/mode 5; type 3 selects protocol 3/mode 6.
Transport initialization later publishes its own game mode (§3).

Build the connection-description string, release its previous allocation and
publish the new text. Clear live and saved resume-new-game flags. At the start
of each setup-menu attempt:

1. If live resume-new-game equals 1, restore setup background with its sound and
   paint operations. For each civilization 0…7 clear intro-shown and the first
   character of its setup-name entry.
2. Clear turn-time limit, live/saved resume civilization, live/saved
   resume-new-game, loaded-subdirectory first character and loaded-file kind.
   Reset S01 scenario parameters; set turn 0, first-launch Absent and game flags 0;
   restore scenario directory to home.
3. Except on the first attempt, reload rules with argument 1. Clear events-loaded
   and reset setup-dialog positioning. This is not a full new-world reset.

Show the network menu. Lobby host launch 2 disables Join and uses default 0 if
Join was remembered; lobby client launch 1 disables choices 0…4 and defaults to
Join 5. Other launches use the remembered choice. After the modal return, play
menu-accept, close the side picture and mark first-attempt false, even when the
reply is negative. Any negative returns failure 1. Store nonnegative choice as
signed 16; choices 0…2 change cwd to home before their child operation.

| Choice | Preparation and retry |
| --- | --- |
|0 random world|F05 size selection; on success map source 0, random world parameters, then game-option setup with mode 3. Any child failure retries network menu.|
|1 premade map|F05 map load; source 1 or 2 according to live Flat; random parameters; option setup with mode 3. Failure retries.|
|2 customized world|F05 size, source 0, custom parameters and option setup with mode 3; failure retries.|
|3 scenario|Clear suppress-wonder-movies request, invoke S01 network-host scenario preparation. Failure restores scenario directory to home/retries. Success honors the request by clearing WonderMovies.|
|4 saved game|Clear that request, invoke F06 load-dialog(false,3,true). A successful load proceeds only when its live mode>2; otherwise report incompatible game/retry. In this branch the suppress-wonder request is tested on **load failure**, not on the successful path.|
|5 join|Proceed to client setup.|

Random network parameter selection uses F05's five-draw land-mass/form,
temperature, climate and age procedure; no extra map generation occurs here.
After preparation clear claimed civilizations, save preferences, clear
setup-complete and clear Human membership. For hosting, capture the current
open-game setting, force it to 1 and invoke host setup. For Join invoke client
setup. Failure retries this reset/menu loop. On success restore open-game from
the captured local value, set setup-complete, save preferences and return 0.

The classic first-pass Join path does **not** initialize that captured open-game
local. A prior failed host attempt can have produced it; otherwise reaching the
restore needs incoming backing or a bounded stop. Reconstruction B18 captures
the current setting at entry and is an explicit repair. Similarly, scenario/
saved-game choices do not produce the local map-source argument; the ordinary
restored path skips its eventual use. If intervening state changes reach fresh
map generation, require the argument's producer/backing rather than invent 0.

## 3. Transport attachment and shutdown

The logical session wrapper calls N05 with the chosen protocol and host/client
role. Before attachment, diagnose an already-connected receiver, capture its
selected protocol, reset the message queue, clear the session's specified state
and clear the player-table/count/mapping block. `clearSession` sets Connected
false **before** calling shutdown, so that call takes shutdown's reset-library
branch. It does not implicitly free a list whose pointer it has already cleared.
Appendix A enumerates the exact reset/retained fields.

| Protocol | Child initialization and published mode | Timeout preference minimum/default |
| --- | --- | --- |
|0 TCP|N05 socket initialization with low-byte role; mode 3.|Internet 60 when connection type 0, otherwise TCP15.|
|1 IPX/SPX|N05 socket initialization with low-byte role; mode 3.|15.|
|2 modem|Publish mode 5 and timeout, then modem initialization.|30.|
|3 serial|Publish mode 6 and timeout, then serial initialization.|30.|

For each timeout preference, absent−1 or signed value below its minimum writes
the default preference and stores that minimum. Otherwise store the supplied
value. For TCP/IPX the initialization call precedes mode/timeout publication.
Underlying parameters and provider availability belong to
[N05](N05-transport-and-wire-protocol.md#9-modem-serial-and-launch-lobby-providers).
The TCP-only port's legacy-provider stubs are not original success/failure rules.

Initialization succeeds only on child result 0 and, for a nonzero low-byte host
role, server activation result 0. Publish host role, Connected 1, active and
selected protocol; reset error/received counters. Set the first slot's address
and player name from current preferences using the nominal bounded fields;
set player count 1, slot 0 active 1 and slot 0 host to the role. Return 1. Failure
returns 0 and restores **only selected protocol**; earlier clearing, mode,
preferences and transport effects remain.

A connected-to-server callback interprets the ID as signed 16. For 0…7 store
server ID, set ToServer 1 and send PlayerListNode to destination 0 with that ID.
Other values do nothing after logging. Connection-lost callback interprets ID
unsigned 16, increments total lost count and increments its per-ID count only
when ID<7. The new-client callback only logs; it does not allocate a player.

Shutdown first forces requested delay 0 when host role is nonzero. If Connected
is false, reset the N05 library, then take the common tail. Otherwise:

1. Free discovered games. If ToServer is true, flush 60000 milliseconds, reset
   receive queue/popups, close connection, reset queue/popups again and call the
   close hook. Return values do not stop this sequence.
2. Protocol 0/1 call socket shutdown,2 modem shutdown,3 the original TEN-named
   shutdown entry. Reset queues/popups, emit applicable peak diagnostics, reset
   library, and if delay remains nonzero perform the 120-tick UI delay.
3. Common tail clears Connected, ToServer and Listing, sets server ID−1, resets
   error counts and resets queue/popups again. Unmentioned session fields remain.

## 4. Names, addresses and host/client wrappers

Normal player-name and game-name prompts admit 31 characters, cancel only on
exactly−1 and repeat empty input. Player name stores the remembered value and
saves preferences. Game name stores remembered **and live** game name, then
saves. Lobby launch instead clears the remembered field and copies at most 31
characters from the lobby name; this branch does not save preferences or copy
the live game name as an extra step.

Address acquisition for no lobby or client lobby 1 queries the current protocol
address. If present, query it **again** and copy that second result. If absent,
prompt 31 characters with remembered default, exact−1 cancel and empty retry.
Other lobby modes copy the lobby address. Host Internet verification similarly
uses the lobby address directly, or queries first with the active adapter/
protocol then again with selected adapter/protocol, shows its verification
prompt and stores a nonempty result. Exact−1 verification shuts down and fails.
Append the accepted IP to the connection description. A failed second query
requires its actual read/failure boundary; do not reuse the first pointer.

Client setup initializes transport first. Failure reports connection failure.
Then obtain player name and, except for modem/serial, local address; cancellation
shuts down. Allocate/construct the discovery view, publish it and invoke §6;
destroy it and clear the shared pointer on normal return. Success returns 1.
Failure shuts down, clears loaded subdirectory, resets scenario parameters and
returns 0. Allocation/reporting cannot manufacture a valid missing receiver.

Host modem/serial obtains both names before transport initialization; other
connection types initialize first, then obtain the names. Failed initialization
shows the generic connection popup only outside modem/serial. Name/address
cancellation shuts down. Internet verifies the host IP; LAN obtains its local
address. Construct/publish a lobby and invoke §7 with the map-source argument;
destroy it and clear its pointer on normal return. Success returns 1. Failure
shuts down, clears loaded subdirectory and resets scenario parameters.

## 5. Discovery and advertisement

### Advertised game information

An advertisement contains version `5.4.0f Multiplayer 26-March-99`, current
remembered local address, live game name, remembered player name, open-game,
difficulty/barbarian settings, counts, timer, scenario name, production/movement
multipliers, calendar/game flags, map dimensions and simultaneous setting.
When Alive is nonzero, advertise that byte; otherwise use the low-byte mask
with the first `enemyCount+2` positions set. Player mask has its first
`playerCount` positions set; it does **not** encode civilization identities.
The original lookup table supports counts 0…8; other counts require actual
backing or rejection. Clear local list links, ID and timestamp. The server-info
send operation subsequently publishes signed 16 session phase into the outgoing
advertisement. N02 owns its encoding and destination/routing semantics.

A discovery request is answered only by a host outside handoff whose connection
type is not Internet 0. Receiving an advertisement can mark the list dirty even
if Listing is false; adding/updating the list itself is gated by Listing.

### List updates, expiry and selection

For an incoming advertisement while Listing:

1. Find the first old entry whose host address matches case-sensitively over
   at most 32 characters. Unlink and delete it, retaining its row ID. No match
   uses ID0. This deletion precedes allocation of the replacement.
2. Find insertion position sorted case-insensitively by game name, then by host
   address for equal game names. An exact sorting tie inserts before the old
   entry. Allocate/copy the advertisement, install proper previous/next links,
   stamp current ticks and publish retained ID. Allocation/reporting failure
   does not resurrect the removed entry.

A discovery-view constructor sets next ID1, empty option list and timer 0, leaving
other incoming fields untouched. Start its refresh timer after killing any
previous handle, using a 50-unit one-shot timer. Each tick sends GetServers,
expires stale entries, destroys the then-current timer if nonzero and schedules
the next one-shot callback after 250 units. P05 supplies timer-unit delivery;
N05 supplies discovery transport. Killing clears the stored handle after the
child call, even if a callback changed it.

Expiry captures current ticks once. Scan list order; remove entries satisfying
`signed32(now−lastSeen) > signed32(timeoutSeconds*60)`, relinking neighbors and
saving successor before deletion. If any were removed, refresh the displayed
list once afterward. Equality does not expire. Free-all repeatedly captures the
current head's successor, deletes the head and publishes that successor.

Before clearing displayed options, snapshot selected and top game/host **names**.
Resolve each option by row ID; if not found, start at current game-list head and
advance by its option-group index, stopping at the last entry. With no games,
clear the four saved names. Then delete option text/node pairs in list order,
clear option head/count/selection. Required live receiver/list backing is not
fabricated when callbacks leave those objects absent.

Refresh adds a waiting placeholder with ID0 when there are no games. Otherwise
scan game-list order, assigning a fresh monotonically increasing row ID only
when its stored ID is 0, and append the game name. Disable an option if closed,
if the population count of **all eight** player-mask bits is at least the
maximum players, or if that count is at least the population count of all eight
Alive bits.

Restore selection by exact case-sensitive game-name **and host-name** match,
scanning game entries and options together; no match selects the first option.
With fewer than 11 options, top is first; otherwise resolve saved top names the
same way. When the dialog's scrolling state is active, move top down if selected
is at/beyond the visible bottom, then up if selected precedes top. Set scrollbar
range 0…count−1 and position to the final top index. Refresh details and render.
Selection identity is not preserved by host-address key alone.

### Detail pane and profile

If no games exist, clear nine detail lines. Otherwise find the selected row ID;
missing ID retains the previous detail contents. The nine populated lines are:
game name/open status; host; scenario/year; difficulty/simultaneous state; map
size; production/movement options; barbarians; timer; civilization/player count.
Scenario name falls back to the default label; if resulting length>25, truncate
at character 21 before appending the year.

**Before computing that displayed year**, copy the selected game's flags,
first-launch turn, year increment, start year and difficulty into live game/
scenario state, in that order. Call F04's calendar using its advertised turn.
These shared stores are not restored after drawing or cancellation. Map display
uses signed width/2 and height. Timer 0 displays its no-limit label; otherwise
compute seconds=`trunc(ms/1000)`, minutes=`trunc(seconds/60)`, remainder modulo 60;
prefix a literal 0 to either component when below 10, with `:` between them.
This is not a general nonnegative-duration normalizer.

Displayed civilization count excludes bit 0, unlike admission's eight-bit count.
Displayed player count includes all eight bits; use the special pre-start label
when phase<3 or count 0. After details set scrollbar from current top and render.
P04 owns resource labels and native control placement.

The separate game-profile prompt uses current live names/options/calendar and
map width/2. It counts Alive bits 1…7. For the human count, use 1 in single-player,
the local hotseat-human mask in mode 1, otherwise the first-playerCount-bits mask;
count all eight bits, then subtract 1 when called for joining. It installs the
turn-popup idle callback and returns the profile dialog's result unchanged.
Thus a joining profile can display−1 when its count producer is zero; no clamp
is inserted. It does not use the discovery detail pane's temporary truncation.

## 6. Client connection and join phases

### Address entry

Internet joining starts retry count 0 and increments on each address attempt.
A lobby launch copies its address to view and shared server fields. Otherwise
prompt for host address, admitting 31 characters; exactly−1 clears the view's
address and fails, empty text repeats, and nonempty text updates both fields.
Open with `timeoutSeconds*1000` milliseconds. Negative result reports a connect
failure; the ordinary Internet branch retries. There is no retry-count limit
on repeated open failures.

For successful Internet open under a lobby launch, send lobby notifications 1
then 3; their errors are logged, not fatal. Capture wait-start ticks, install the
initial server-ID callback and show the waiting popup. Exactly−1 fails without
sending cancellation at this stage. If server ID remains−1, Internet/lobby
retries while count≤3, closing the connection first; otherwise report failure.
Thus the missing-ID branch can make four attempts, distinct from open failures.

After obtaining an ID, register synchronization blocks, reset received counts,
send ServerQuery to host, install answer-wait and show waiting again. Exactly−1
sends ClientCanceledStart and flushes 180000 milliseconds before failure. Server
reject/cancel reports the appropriate message and fails. Compare both length
and case-sensitive content of received version with the exact version above;
mismatch reports both versions and fails. Success returns to the common join.

### Discovery-list entry

LAN entry frees previous discovered games, constructs its dialog, temporarily
replaces the primary button label and creates the waiting/detail list. On each
modal attempt set Listing 1/start refresh timer; after return set Listing 0/kill
timer. Any negative reply restores the button label, clears/destroys the dialog,
clears the view address and fails.

Find the selected advertisement by row ID; a stale/missing ID retries. Apply
§5's full/closed tests again. Full capacity reports a notice; merely closed
retries without it. Version mismatch reports a notice with the original button
label temporarily restored, then retries. Accepted entry copies server/game/
host names, local player preference and **live map width/height** before opening
the connection. Ordinary LAN uses its advertised address; the callable modem/
serial form uses empty addresses. Negative open result reports failure/retries,
retaining those copied values. Success closes/restores/clears/destroys the list
and flushes paint before common join continuation.

### Common join state machine

The backdrop-restoration helper below acts only when loaded-file kind equals 2:
play menu sound, load the setup backdrop, set its surface/color and flush paint.
For other kinds the helper returns without those effects.

Construct the main join dialog/palette and reset received counts. Callable
modem/serial opens an empty address directly; negative result fails (generic
notice only when actual protocol is not modem/serial). Internet uses address
entry; other ordinary types use discovery entry. Then perform:

| Phase | Entry, success and failure rules |
| --- | --- |
|Wait for host/player list|Install server-wait, capture start ticks, reset counts only for non-Internet. Show waiting. Exactly−1 sends ClientCanceledStart/flush 180000/fails. Otherwise prioritize Reject, ServerCanceled, ServerFull notices and fail. Popup−2 reports timeout/fails. Other results continue.|
|Wait for initial state|Register sync blocks, install state-wait, capture time, reset counts only for non-Internet. Show WaitToJoin. Exactly−1 sends cancellation; otherwise ServerCanceled reports quit. Either case flushes 180000/fails. Other results continue.|
|Received scenario|If loaded kind>0 and subdirectory is nonempty, run the preparation below. A directory failure already sends cancellation. Briefing cancellation sends ClientCanceledStart then UnlockNation for the live local human, flushes 180000, destroys owned scenario dialog/palette and fails.|
|Profile and civilization|Refresh F06 password flags. Profile(join=true) exactly−1 sends cancellation/flush/fails. With saved resume-new-game 0, saved resume-civ 0 and phase<2 choose a new tribe; otherwise choose an existing civilization (§8). A nonzero chooser result restores backdrop, sends cancellation/flush and fails.|
|Publish player / wait for list|Send PlayerInfo, register sync blocks, install player-list wait, capture time and reset all received counts. Exactly−1 restores backdrop, sends cancellation then UnlockNation/flush/fails. Otherwise ServerCanceled or LostServerOnJoin restores backdrop and reports quit/lost-server without adding a flush, then fails.|
|Wait for start|Clear only StartCivilization count, install start-wait/capture time, show waiting. Exactly−1 has the same cancellation/unlock/flush path. ServerCanceled restores backdrop/reports quit/fails. Other results complete.|

On completion reset received counts, close the shared text file, end menu audio
and return 1. The surrounding wrapper handles shutdown after failure. Do not add
an implicit unlock, flush, timeout or generic negative-result branch where the
phase does not contain it.

### Wait callbacks

Each callback processes messages with limit 1/flags 0. Closing means set the
current dialog's completion flag and end its modal loop, using live receivers.
These callbacks are not interchangeable:

* Initial ID wait captures `elapsed=now−start` **before** polling; close on
  ID≠−1, or ID−1 with signed elapsed>1240 ticks.
* Answer wait closes on Answer, ServerCanceled or Reject count nonzero.
* Common server wait calls the clock (discarding its result), polls and closes
  on PlayerList, ServerCanceled, Reject or Full. It has no own timeout test.
* State wait captures elapsed before polling. Close on received initial-state
  or ServerCanceled; otherwise elapsed>2 advances the progress text, renders,
  then updates start time from a fresh clock call. Appendix A specifies its
  valid marker-text producer and excludes malformed-buffer writes.
* Player-list wait closes on PlayerList, ServerCanceled or LostServerOnJoin.
* Start wait closes on StartCivilization or ServerCanceled.

Except for initial ID and progress-animation timing, no deadline is introduced
by these callbacks. P04's modal producer and N05's transport errors retain their
separate effects. Counter resets after sends can erase responses delivered
synchronously; preserve the specified ordering.

### Received scenario preparation

Construct path as current scenario directory plus received subdirectory and
try changing to it. Failure sends cancellation/flush 180000, reports the data
path and returns failure. On success append the subdirectory to the shared
scenario directory and call F03 rules-load(0). Overlong path construction is an
excluded overwrite; the reconstruction's preflight length rejection is a safe
policy, not original path normalization.

If loaded kind is 2 or Scenario flag is set, construct a separate dialog/palette.
For civilizations 1…7, without Alive filtering, publish tribe gender from
FemaleLeader and portrait=low-byte tribe plus 21 for female. Compare the raw local
plural with the absolute rule plural reference. If equal, replace all seven
male/female title references with generic government titles. Then perform three
**separate live adjective-positive tests**, negating leader-name, plural and
adjective references respectively with signed 16 reduction. Shared tribes and
callback changes can distinguish those rereads.

Run S01-equivalent briefing presentation using this received directory/name,
not a local load dialog or alternative-file selection: optional TITLE.GIF,
menu audio/input flushing, home-directory return, language-extension text path,
preferences with Tutorial cleared and message preferences, scenario name/
paradigm and F04 calendar arguments. Max-turn 0 displays year 2000. Install the
briefing timeout callback/start time. A loaded SCENARIO text can display the
active unit sprite only if its slot/identity is valid and its owner equals the
objective civilization. Negative modal result restores backdrop and returns the
owned-object cancellation result. If text failed to load, negative fallback
ScenarioLoaded result cancels without that backdrop restoration. Success
destroys scenario palette/dialog and continues. The caller owns the cancellation
message/unlock sequence and destruction on those cancellation exits.

## 7. Host lobby and world publication

### Remote-player list and rejection

Construct the lobby with next row ID1, no entries and seven zero drop counters.
For launch modes 2/3 passed directly to the lobby operation, advertise once,
clear its server address and copy remembered game/player/host names without a
selection dialog. Other modes construct the seven-row lobby, install its
message-poll callback and temporarily replace the primary button label. Add a
waiting row only when the list is empty. Internet shows the remembered address
in the title. Lobby-service host launch 2 sends notifications 1 then 3.

Before each modal attempt advertise the game, clear the dialog's completion
flag/result/extra result, and show it. Extra result 1 means Reject: resolve the
selected row and the first slot whose retained lobby ID equals its ID. If the row
exists and is not the sole waiting placeholder, send Reject to
that connection, flush 180000, close that connection, select its next neighbor
(or previous, or itself if alone), remove the row, remove the player, then add a
waiting row if empty. Repeat the lobby. A rejected unassigned player reaches
the excluded mapping write described in §11; safe handling must retain the
preceding reject/flush/close and valid removals.

A negative ordinary reply broadcasts ServerCanceledStart, restores the button
label, clears/destroys the list, stops accepting connections and returns 0.
Acceptance copies the view's address and live game/player/host names, restores
and destroys the list, then flushes paint. List construction/destruction does
not substitute for transport shutdown in the surrounding wrapper.

Rows append in active player-slot order 1…6, not alphabetically. Refresh captures
the selected row ID, clears rows, assigns new monotonically increasing lobby
IDs only to slots whose retained ID is 0, and rebuilds those active rows. With
no rows add the waiting entry using the same ID generator. Restore an exact ID
match; otherwise choose the first row with ID at least the old selection, or
the last row. Publish list head/top, set default choice and render. Waiting-row
recognition requires a single row whose text exactly equals the current waiting
label. An ordinary player with that sole name is indistinguishable here. The
slot lookup does not test activity and returns 7 if no matching ID exists. The
Reject caller does not check that sentinel before reading a player record;
require established read backing or stop, rather than inventing a match.

### Claim barrier and initial world

After the accepted lobby:

1. If player count>1, broadcast PlayerList. Set phase 1 and broadcast GameGoing(1).
   Copy remembered player name into slot 0, register sync blocks, reset received
   counts, then broadcast ServerPath followed by JoinGameState. Set local slot 0.
2. Show profile(join=true), including its displayed count decrement. Exactly−1 sends **ClientCanceledStart to 0**, flushes
   and fails. Otherwise choose a new tribe when saved resume-new-game and saved
   resume-civilization are both 0; first reset custom names and clear Alive.
   For a restored game use the existing-civilization chooser. Choice failure
   broadcasts ServerCanceledStart only if count>1, stops new connections and
   fails.
3. Publish civilization-to-slot for the selected player as 0, slot 0 civilization,
   active 1 and host 1. If count>1, wait for remote choices. Its idle callback
   polls, counts slots 1…6 with **active exactly 1 and civilization−1**, and when
   none remain marks the dialog complete, sets phase 2, broadcasts GameGoing(2),
   then ends its modal loop.
   Exactly−1 broadcasts cancellation/stops connections/fails. This callback has
   no independent deadline. If count>1 afterward, broadcast PlayerList.
4. Test **live** resume-civilization and resume-new-game. If both 0, invoke the
   N04 [player-count chooser](N04-hotseat-and-player-handoff.md#3-player-count-chooser),
   pass its result to [shared opponent selection](N04-hotseat-and-player-handoff.md#shared-multiplayer-opponent-selection)
   without an added cancellation check, save preferences, reload
   advances/units/improvements with argument 1, end menu audio, generate the map using the captured map source,
   register sync blocks, allocate a mirror and run F05 new-game initialization.
   Nonzero acceleration invokes F05 with selection−1. If resume-civilization
   became nonzero, copy it into stored player and local human afterward. The
   restored branch only registers blocks and allocates a mirror here.
5. Close the shared text file. If count>1, broadcast MapStruct, send full state
   to all, flush 5000, broadcast EventsLoaded(loaded,count), then
   StartCivilization, then flush 180000. Reset received counts, set phase 3 and
   broadcast GameGoing(3), even with only one player. Return 1.

These sends are ordered calls to N02. Failure results do not imply an invented
rollback or a missing check. World generation and starting possessions remain
F05/W02; this section owns when their inputs are selected and published.

### Multiplayer game-option wizard

This wizard shares F05's numeric meanings, but has its own navigation. On entry
clear pick-opponents and acceleration once, restore default leader references
and clear the reserved setup field. Difficulty cancellation returns failure;
opponent or barbarian cancellation restarts Difficulty. Publish accepted
values/preferences immediately; random choices consume F05's specified draws.

Each Rules attempt first installs standard rules. Negative Rules returns to
Difficulty. Nonzero Rules choice enters Advanced with saved rules and the
premade map's topology constraint. Negative Advanced repeats Rules without
reading checks. Acceptance reads the six F05 checks in their listed order.
If acceleration is selected, compute F04 year magnitudes at turns 0/21/41, show
its three choices, and store the result; a negative reply resets acceleration 0
and repeats Rules. Accepted acceleration also updates its preference. Standard
Rules skips these stores, retaining entry-time or earlier accepted locals.

Then repeatedly show multiplayer options. Negative reply returns to Rules.
Read checked values in this order, storing signed 16 into live option and its
remembered preference: open game, kill civilization on retire, reveal human
starts, chat from start, double production, double movement; read simultaneous
movement seventh only if the startup INI gate allows it. When unavailable,
building the dialog clears both live simultaneous mode and its saved game
setting. For local mode 1 disable Chat and Simultaneous controls and, after
reading, force those two live settings 0; their preferences retain the values
read. The first six defaults come from remembered preferences; optional seventh
comes from its preference. Control checkbox extraction remains P04.

Mode 2 accepts immediately. Other modes invoke N03's timer-selection child;
result 0 accepts, failure repeats the multiplayer-options dialog, retaining
already published options. This callable mode 2 branch does not establish an
ordinary e-mail menu; [N04](N04-hotseat-and-player-handoff.md#9-dormant-e-mail-branches)
owns its reachability. No network packets are sent by
this options reader itself.

## 8. Civilization choice and claim

### Claim service and incoming session changes

When dispatched a LockNation request, obtain its civilization argument and
compute the F01 shift mask. If not already claimed, set that bit in the claimed
set, send NationAccept to the sender with the current signed claimed value,
broadcast LockData with the then-current value and increment LockNation count.
Otherwise send NationReject only. The request's leader argument is not checked
here. This service does not require Alive membership, an unclaimed Human bit or
an available slot; caller menus provide the nominal restrictions.

LockData reception replaces the claimed set, intersects Human with its low
civilization bits and increments LockNation count. UnlockNation first intersects
Human with the **old** claimed set; if the requested claim was set, clear it,
intersect Human again, broadcast LockData and increment LockNation count.
Thus even an unlock of an absent claim can change Human. NationAccept/Reject
have no additional handler store beyond N02's received-count update.

Other session dispatch effects used by these state machines are:

| Message | Logical effect after N02 reception counting |
| --- | --- |
|PlayerList|Replace count, all seven slot records and all eight civilization mappings. Scan active slots for the first connection ID equal to local server-connection ID; update local slot only on a match.|
|ServerQuery|Send ServerAnswer to sender. No extra open-game gate here.|
|ServerAnswer|If a discovery view exists, copy address, game name, host name and version there. Then copy shared server address/game name, local player name from its preference, and received version, in that order.|
|ServerPath|Publish loaded kind, filename and subdirectory.|
|GameGoing|Replace session phase with the supplied value.|
|ServerReject|Set wait-start ticks to a fresh clock reading.|
|ServerQuitRequest / ServerQuit|Increment handoff stage for either message.|
|ServerQuitAck|Decrement pending players awaited.|
|QuitRequest|Set the supplied civilization's drop request, then run §11's flagged drops.|
|ClientCanceledStart|Set sender-indexed lost count to−1; remove that connection. If a lobby exists, increment its sender-indexed drop count, then refresh only if its dialog exists. The two arrays have different domains; exclude any out-of-bounds write separately.|

N02 owns packet-size validation profiles, source authority, payload decoding,
image/rules application and count increments before dispatch. The classic
receiver has no extra open-game test in PlayerListNode or PlayerInfo admission;
closed-game filtering in the discovery UI must not be promoted to one here.

The lock-dialog idle callback calls the clock, polls once and closes on nonzero
LockData or ServerCanceled. It does not independently close on a deadline.

### Existing civilization

Clear player-info custom-name/title flags and start retained selected index 0.
On each outer attempt build choices from Alive civilizations 1…7 in ascending
order, recording civilization by returned option ID. Labels contain plural,
leader, city count and military-unit count. Refresh availability in an inner
loop: enable a choice only if both Human and Claimed lack its bit, updating the
native radio control too. None available reports JoinedMax/closes/fails.

Scan at most the number of choices from retained selection for the first enabled
option. Increment and wrap only on equality with that choice count. Reset
received counts, capture wait time, clear modal completion/result/extra, install
lock idle and show. Read the radio selection even for a negative reply. Negative
fails; otherwise nonzero LockData repeats availability before examining
ServerCanceled. ServerCanceled reports/fails.

Resolve selected civilization, publish player-info civilization, remember the
selection and send LockNation(leader=0,civilization). A client **then** resets
received counts and captures time. While neither Accept nor Reject is present
and signed elapsed<timeout*60, poll once. A host expects synchronous local
handling without this wait. No reply closes/reports connection failure/fails;
Reject reports AlreadyChosen and repeats availability.

Acceptance closes the chooser, sets Human membership, stored player and current
turn civilization, clears Tutorial and sets local human to the full selected
civilization. Check its password (§9). Failure clears Human, sends UnlockNation
and rebuilds the outer list. Success prompts Gender using live tribe gender.
Store the modal result in outgoing player-info before menu sound. A negative
reply clears Human/unlocks/restarts. Nominal choices are 0/1; BF-113's rejection
of greater values is a reconstruction repair (Appendix A).

For nonnegative gender, store its low byte in the tribe and set FemaleLeader
according to the **full result's nonzeroness**. Load the 23-character leader-name
prompt with the current leader name and install the network-pump callback.
If loading succeeds, only modal result 0 copies the entered name into local and
outgoing leader names, clears outgoing plural/adjective first characters and
returns success 0. Other modal results clear Human/unlock/restart. A text-load
failure instead rebuilds the list **without clearing the claim or Human bit**;
do not silently perform the cancellation branch for it.

### New tribe

Initialize result 1, last leader−1, retry tribe−1 and retained selection 0. On each
outer attempt restore the last leader's default references if any, prompt Gender
from its preference, poll for network modes, play menu sound and close the side
picture. Check server cancellation/loss before interpreting the reply. Negative
finishes. Store gender as signed 16 preference and sign-extended outgoing gender.
Load the 21-tribe text section; failure closes the picture/finishes. Check server
loss again, set player title and arrange three columns.

For each leader, if its preferred civilization color is neither Human nor
Claimed, restore its custom references. Its displayed plural uses the rule label
when that color is not Human or the plural reference is nonnegative; otherwise
use that civilization's custom plural. Availability refresh restores free
leaders, then **rereads** Human/Claimed to enable or disable each choice. No free
choice reports/closes/finishes. Preselection scans at most 21 choices from the
retained index, incrementing and wrapping only on equality with 21.

For network mode reset counts/capture time, clear completion/result/extra,
install lock idle, show, then read radio selection even on cancellation. If
LockData is nonzero, retain this reply as retry tribe and refresh choices
**before** server-loss or negative-result handling. Otherwise check loss, play
sound, and a negative tribe reply returns to Gender. A nonnegative leader:

1. Publishes selected/outgoing leader, stored player as the low-byte preferred
   color, and outgoing civilization from that stored player.
2. For network play, **reset counts before sending** LockNation(leader,civ).
   Client wait tests Accept/Reject and elapsed<timeout*60, checking server loss
   before each poll. No reply reports/closes/finishes with result 1; Reject
   records retry tribe/reports/retries the inner choices. Host uses synchronous
   response. This reset order differs from the existing-civilization chooser.
3. Store tribe preference/selected civilization tribe, low-byte portrait plus 21
   when the full gender choice is nonzero, replace only the low byte of local
   human, set Human membership and low-byte leader gender. Select that gender's
   default leader name, copy it locally, then unconditionally negate its rule
   reference with signed 16 reduction. Check password using the low-byte human.
4. Password failure checks server loss, then otherwise plays sound/closes the
   picture, clears Human, unlocks in network play and returns to Gender. Success
   clears outgoing custom-name/title flags and chooses ordinary or custom names
   from the **live** dialog extra result after the password child.

The ordinary name helper repeatedly loads/runs a 23-character prompt while the
resulting text is empty, even when the modal was cancelled. Missing text output
requires its producer/backing. Check server loss before menu sound. Nonzero
result clears Human/unlocks and returns to Gender. Zero copies local/outgoing
leader name and clears outgoing plural/adjective first characters.

Custom naming initially copies default plural/adjective if either outgoing
string is empty or mode 1; otherwise restore all three local names from outgoing
names. Negate plural/adjective rule references, copy seven default titles for
the original full gender choice, then negate their references. Show the three
23-character fields. Load failure clears Human/closes picture/unlocks/finishes;
a negative modal clears Human/unlocks/retries Gender. Server loss can finish
between these children. A nonnegative result copies all three text arguments
into local then outgoing fields and sets HasNames 1. Extra result other than 1
accepts names. Extra 1 opens seven titles, using nonempty outgoing defaults when
mode≠1, otherwise local titles. Load failure clears/unlocks/finishes; exactly 0
modal copies all seven to local/outgoing and sets HasTitles 1. After sound/picture
close repeat the three-name dialog, including after title cancellation.

City style presents four sprites/default live style, shows, pumps/checks server
loss, plays sound and closes the picture. Negative clears Human/unlocks/retries
Gender. Nonnegative stores full outgoing style, signed 16 local style, result 0
and chosen leader into `setupTribes[playerSlot+1]`, then finishes for mode≠2.
BF-102's rejection of nonnegative styles beyond 3 is an explicit repair. Mode 2's
e-mail continuation is owned by [N04](N04-hotseat-and-player-handoff.md#shared-tribe-choosers-address-continuation),
including cancellation after result 0 was already published.

On final result 1 restore the last leader's default references if any. If also
outside an active game and playerSlot>0, restore the leader from
`setupTribes[playerSlot]` and clear Human for **that** leader's preferred color.
This cleanup indexes the preceding setup slot, while success stores slot+1.
Default-reference restoration conditionally negates references<1 for leader,
plural, adjective and all seven pairs of titles;0 stays 0 and−32768 stays−32768
after signed 16 reduction. It is not a global name/mask rollback.

### Publishing a claimed civilization on the host

Capture local human, current-turn civilization and stored player. Report a
non-host receiver, but if the diagnostic returns continue. For saved resume
flags both 0 and phase<2, publish incoming civilization into stored player,
its tribe/portrait, local human, then Human (phase<3) or pending-human membership.
Publish leader gender, select/copy its default name, negate a positive
leader-name reference, then copy incoming leader name. If HasNames is nonzero,
negate plural/adjective references, copy all seven default titles from the
absolute rule references and negate those references, copy incoming plural/
adjective, then copy incoming titles only if HasTitles. Store signed 16 style.

The restored/running branch instead sets Human or pending-human membership,
stored player, current-turn and full local human, copies incoming leader name,
negates a positive leader-name reference, sets FemaleLeader from full incoming
gender and stores its low byte into the tribe. It does not replace tribe/style
or custom plural/titles in this branch.

Scan slots 0…6 for the first connection ID matching the sender, without an active
filter. Found: map stored civilization to that slot, set slot civilization,
active 1 and host flag according to sender==0; result 1. Not found: clear the
Human/pending bit selected by live phase, send UnlockNation with incoming
leader/civilization, result 0. Restore the three captured identities and return.
Missing-player failure leaves earlier tribe/name/gender/style changes intact.
Invalid identity writes are excluded; a bounded read failure does not promise
that the final identity restoration has already run.

## 9. Password interaction

F06 §9 owns password encoding, randomized reset and pending-flag refresh. These
wrappers own when decoding, prompts and timer calls occur. Password comparison
is case-sensitive; nominal input admits 31 characters. Dialog titles prepend the
civilization's local leader name to the operation's label.

Ensure-password stops the turn timer with argument 1, decodes, copies that
civilization's password and re-encodes. If empty it invokes Change Password;
otherwise returns **without a matching resume call here**. Change Password also
stops with 1. On its normal exits it refreshes password flags, clears that
civilization's pending flag, resumes with 1 and destroys its dialog.

Change Password first decodes/copies/re-encodes the current password. If nonempty,
prompt for the old password: negative cancels, mismatch reports Password 0 and
finishes without a new prompt. Otherwise prompt for a new password. A nonnegative
result decodes, copies the entered string into the password row and local copy,
then re-encodes. Report Password 4 only when the new string is nonempty; empty
input clears the password. Load failures and missing prompt output retain their
actual P04 producer boundaries; do not assume an accepted empty string.

The check-password prompt installs the network-pump callback. Negative returns 2.
Otherwise decode, compare, re-encode; equality returns 0, mismatch shows its notice
with pumping and returns 1. `checkPassword(civ)` behaves as follows:

* Pending flag 0: in mode 2 invoke Ensure Password; otherwise set game active 1.
  Return 1 regardless of whether that optional change prompt was cancelled.
* Pending flag nonzero: repeatedly set active 1 and prompt. Result 0 clears pending
  and returns 1; result 1 retries; result 2 clears active and returns 0.

No separate player-slot authentication or password hashing step is inferred.
N04 uses these same password interactions for local handoff.

## 10. Joining a running game

### Connection registration

PlayerListNode reception during handoff sends ServerCanceledStart. Otherwise,
if player count is at least maximum, send ServerFull. Below maximum, add to the
first inactive slot 0…6; if none exists the add helper simply returns. The helper
stores connection ID, address, host 0, active 1, civilization−1, player name,
auxiliary 0 and lobby ID0, then increments count and decrements awaited players.
It does not deduplicate connection IDs. The caller continues even if no free
slot was found.

With a live lobby/dialog, refresh its rows. Without one, if phase>0 send
GameGoing(phase) to sender, broadcast PlayerList, then send ServerPath and
JoinGameState to sender. No open-game check, claim or full-map transfer is added
at this point. The subsequent client profile/chooser/publication is §6.

### Player-info receipt and state transfer

PlayerInfo reception reports a non-host receiver. During handoff send
ServerCanceledStart; otherwise invoke §8's host publication. Failure sends
LostServerOnJoin and stops. Success increments LockNation count. For phase≤2
there is no further running-game transfer. For phase>2:

1. Broadcast HoldForJoin, stop turn timer with 0, and display the state-transfer
   wait dialog. Send multiplayer options to the joining sender, broadcast
   PlayerList, flush 5000, send incremental state to all, flush 5000.
2. Send MapStruct to sender, send full state to that sender, flush 5000. Send its
   EventsLoaded and complete rules tables, flush 5000. Then send GameGoing with
   live phase and StartCivilization to sender, and broadcast ReadyToStart.
3. Capture wait-start time. Repeatedly flush 5000; if result 0 break. Otherwise
   process up to 100 messages and read current ticks. Repeat **while signed
   elapsed>17999**. This is the original inverted continuation test: a prompt
   failed flush normally exits after one poll, whereas a sufficiently late one
   can repeat. Do not replace it with a conventional timeout loop.
4. Broadcast ContinueAfterJoin, close the wait dialog and resume timer with 0.
   Find the first slot matching the sender; if found set its civilization's
   new-player flag 1. An out-of-domain flag write is excluded separately.

Options/rules/state payloads, mirror mutations and Hold/Continue dispatch are
N02 children; their timer calls use N03's start/stop/resume contracts. Poll/flush
callbacks may change phase,
slots, counts or child receivers. They remain live at subsequent reads.

## 11. Dropped and missing players

### Removing a slot

Find the first slot whose connection ID matches, including inactive slots. Only
if that found slot is active proceed: if its civilization is 1…7 clear Human;
clear its connection ID, first address character, host flag and active flag;
set its civilization-to-slot mapping to−1; then set its own civilization−1 and
decrement count. Other slot fields remain. An earlier inactive ID match prevents
removal of a later duplicate active ID.

A newly connected, unassigned slot has civilization−1. Its mapping store is
out of bounds and is an **excluded bug**, even on ordinary pre-claim rejection.
Retain the valid earlier clears; safely omit the mapping store and finish the
logical removal, reject the operation, or stop at that boundary according to
the declared safe policy. Never require the original adjacent-memory damage.

### Flagged drops

Scan remote slots 1…6. For each active slot whose civilization's drop flag is
nonzero, increment local dropped count, capture its civilization, clear that
drop flag, clear lost count indexed by its **connection ID**, clear Human,
remove that player by connection ID, then clear its claim if present and
increment LockNation count. An invalid civilization read and any subsequent
invalid write have separate boundaries. After any drops, broadcast LockData
then PlayerList. Return the number of slots processed. This helper does not
recount activity or send state differences.

### Lost-connection check

A client with nonzero total lost count clears the turn loop's active flag,
shuts down with 1, reports LostServer and returns 1. A client with zero returns 0.
The host instead:

1. Runs flagged drops and retains that returned count. Scan civilizations 1…7
   whose Alive and Human bits are set. If no active slot 0…6 has that civilization,
   clear Human, and if claimed clear the claim, broadcast LockData and increment
   LockNation count; increment local dropped count either way.
2. If total lost count is nonzero, copy its **first seven counters** into a local
   snapshot, then reset live total and those seven counters. Scan remote slots
  1…6; a positive **snapshot indexed by slot** plus live active flag causes a
   drop. Capture civilization, publish player name as a text argument and send
   the lost-player notification to all; clear drop flag/Human, remove the player
   by its live connection ID, clear/broadcast claim and increment LockNation
   count if needed, then report LostClient. Negative snapshot counts do not
   trigger this branch. Notifications can change later live reads.
3. If any local drop was counted, recompute player count from active slots 0…6,
   broadcast PlayerList, then send the incremental state difference to all with
   the missing-player publication flags. Return 0, even if every remote was lost.

The callback records by connection ID, this snapshot loop tests by slot, and
flagged drops clear by connection ID. Preserve those distinct indexes for
in-bounds states; do not silently realign them. The eighth stored error counter
is retained by the seven-counter reset and is not in this snapshot.

## 12. Host handoff and reconnection

### Departing host

Set handoff stage 1, process flagged drops, merge pending-human into Human and
clear pending-human. With player count≤1 return. Otherwise clear the local
human's bit, set awaited=count−1, broadcast ServerQuitRequest, process flagged
drops again and wait. The callback polls once and remains open only while
awaited≠0, Human≠0 and count>1; it has no independent timeout. The original tests
the population count of Boolean `Human==0`, not the count of Human itself.

After the popup, if Human is nonzero, broadcast ServerQuit and set saved resume
civilization to the low-byte signed remainder `(currentNetworkCiv+1)%8`. Find
the first active remote slot 1…6. If found, copy its entire player record to
slot 0, set slot 0 host 1 and connection ID0, assign
`civToSlot[0] = civToSlot[selectedSlot]`, decrement count, assign
`civToSlot[selectedSlot] = −1`, and clear the selected remote record. These are
**slot-indexed mapping operations**, not assignments indexed by the selected
record's civilization. Then send incremental state to all with handoff flags,
broadcast StartCivilization then PlayerList, and flush 60000. No active remote
means no such copy or final publications, despite the earlier ServerQuit.

### Client acceptance and shared waits

Clear ServerQuit, StartCivilization and PlayerList received counts; send
ServerQuitAck to 0; wait with a callback that polls and closes only when handoff
stage equals 2. It has no independent timeout. After return clear stage, install
StartCivilization wait and capture its start time. The callback polls, then
closes on a nonzero count or signed elapsed>timeout*60. Missing count after the
popup reports connection failure, clears both player-turn-active and
turn-loop-active, and returns 0.

Copy saved resume civilization into the live resume field. Wait similarly for
PlayerList; a missing count fails the same way. Snapshot the complete count,
seven player records and eight mappings. Compare remembered local address with
new slot 0 address case-sensitively. Equality takes the new-host branch; otherwise
reconnect as a client. Addresses are not normalized or resolved for this choice.

### Becoming host

Shutdown with 0, then initialize the live selected protocol as host. Failure
reports and clears both active flags/returns 0. On success copy remembered local
address into server address, set server ID0/local slot 0, and set mirror end to
mirror base plus size. Set **expected reconnect count first** to saved count−1,
then copy it into pending awaited count.

If pending<1, map the saved slot 0 civilization to slot 0 and store it in slot 0;
this branch skips the options/list broadcast below. Otherwise wait for pending 0
or signed elapsed>timeout*60. After the popup, check time again. If timed out
and pending≠0, pending==expected reports NoOneShowed/clears both active flags/
fails; partial arrival reports SomeShowed and continues.

Scan each active new slot 0…6. Find the first saved address matching its address
case-sensitively. Use that saved civilization to assign civilization-to-new-slot,
then the new slot's civilization. Missing address reports the original fatal
diagnostic. If that service returns, the later read lies beyond the snapshot
and requires established caller backing (Appendix A) or a bounded stop; do not
invent a civilization. Any out-of-domain mapping store is excluded separately.

In this pending≥1 branch, publish the low byte of the saved simultaneous setting
as live mode, then broadcast multiplayer options using **signed 16 production,
movement and simultaneous settings**, broadcast PlayerList and flush 60000.
Finally both branches clear all 48 map locks to−1, set phase 3, reset errors and
clear ServerQuit/StartCivilization/PlayerList counts. Unless game-over reason 2,
show HostChangeServer. Return 1 even if that notice changes unrelated live state.

### Remaining client

Close the old connection. **Any nonzero result** reports failure, clears both
active flags and returns 0. Copy new slot 0 address into shared server address;
capture time and busy-wait until signed elapsed≥60 ticks, without a message pump
or an invented deadline. Open using timeout*1000 milliseconds; a negative result
reports/fails. Clear ServerQuit and StartCivilization counts, but **retain the
already received PlayerList count**. Install/capture the player-list wait. A
retained nonzero count can satisfy it without a new list. Missing count after
the popup fails. Otherwise publish current slot 0 player name for the notice,
reset errors, clear PlayerList count, show HostChangeClient and return 1.

All timeout arithmetic reduces as F01 specifies. Modal cancellation itself does
not add a generic failure branch to these migration waits; the state/counter
checks above determine their continuation. Snapshot reads and live reads must
remain distinct across callbacks and transport reinitialization.

## 13. Session continuation and teardown

### Application entry and ready barrier

P05 owns native startup; after clearing the network session and successful
application initialization, clear lobby-launch information and query the lobby
service. Results 1/2 select that launch mode;−3/−4/−5 select ordinary mode−1.
Other results send lobby failure notification 2 and return. A positive signed
Simultaneous INI override enables that menu choice and clears the override;
otherwise disable it. Apply the MaxPlayers rule from §2 and copy remembered game
name into the live field. Each main-menu attempt then uses F05's shared reset,
including F06 password reset and S02 event-pool reset. A menu failure performs
application shutdown/returns.

After setup, create common game windows and, for network modes, chat/parley
windows. In multiplayer, apply F03 double movement only for local modes or the
host. A network host publishes the low byte of the saved simultaneous setting as
live mode and broadcasts multiplayer options with the three signed 16 settings,
as in the migration publication above. Enable Load Game, install turn-popup idle, send rules tables from the
host, enable tile synchronization, flush 180000 and start the mouse watchdog.
Then begin the session below. If Tutorial is enabled, network simultaneous mode
is nonzero and its tutorial has not been shown, show it and mark shown 1. Set
setup-complete 1 after the button-font operation.

For local modes enter F04/N04. A network client flushes 5000 and polls once per
iteration until ReadyToStart count is nonzero, with no local deadline. A host
broadcasts ReadyToStart and flushes 5000. Use live simultaneous mode and host role
to select N03's sequential or simultaneous loop. A nonzero client-loop return
enters the corresponding host loop; sequential uses migration argument 1,
whereas an initial sequential host uses 0.

### Beginning visibility and contact

Set session-active 1. If live resume civilization 0, turn 0 and multiplayer, and
ShowHumanStarts is enabled or difficulty is Chieftain, begin a network batch.
Scan the map in [W01](W01-map-geometry-and-spatial-state.md) logical tile order. Whenever a tile's visible set intersects
Human, call [W03](W03-visibility-and-exploration.md) set-visible for the **live whole Human set** with reveal and
update arguments 1,1. Flush the batch after the scan.

Separately, for **any turn/resume state**, multiplayer plus the same
ShowHumanStarts/Chieftain condition loops source civilization 0…7 then target 0…7.
For every pair currently in Human, including a civilization paired with itself,
call [V04](V04-diplomacy-and-treaties.md) set-treaty-flags(Contact). These child effects are not folded into one
precomputed relation. Finally in network modes set local human from the live
local slot's civilization. Stored player/current-turn are not changed here.

### End of loop

Save preferences, clear default idle and events-loaded, stop turn timer with 1
and stop mouse watchdog. In network modes free the synchronization mirror and
shutdown with 1 **only when host role is nonzero**. Attempt to create CHATLOG.TXT;
nonzero create result writes the current log's string length and closes/maps
it. Clear all eight chat send/mute flags, anonymous/broadcast/mute-anonymous
options, then close parley/chat windows. Close pedia, advisor and casualty
windows. If application-shutdown-done is set, return before session teardown.

Otherwise flush input, remove Change Player menu item only in mode 1, destroy
map views/city/status/minimap windows, free the map, flush input again and clear
session-active. Repeat the main-menu loop only for lobby-launch mode−1. Native
window destruction, chat contents/file errors and application shutdown remain
P01/P02/P04–P08 children; no additional network close is inferred for the client.

## 14. Conformance and integration


> Compare the gameplay/interchange observations in the
> [production scope](#production-contract-scope). Original memory/helper traces,
> arbitrary native callbacks and unsafe backing-byte cases in historical lists
> are optional reference, not additional production acceptance criteria.

The required session partner includes the unmodified original target game.
Exercise discovery/direct connection, admission, new-world and loaded-scenario
startup, running join, departure and host migration with an original host and a
cleanroom client, then with the roles reversed. Preserve existing game-version,
identity and password checks. Admission must not require peers to advertise or
agree on the cleanroom RNG algorithm, policy version or private random state.
N02/N03/N05 own the unchanged command, authority and transport boundaries.
These mixed-peer runs are required validation work, not established results.

A comparison must record ordered sends/flushes/transport calls, clock reads,
message delivery and modal results alongside state, directories, names, masks,
preferences, controlled choices or implementation random context, and retained
input. Use separate runs for nominal inputs,
defined scalar edge cases and declared safe rejection policies. Useful
independent cases include:

* Cancel/retry each network menu; count F06's 255 password-reset draws per outer
  multiplayer attempt, F05's five random-parameter draws, retained open-game
  input and the scenario/load path that does not consume map source.
* Discover equal-name games, replace a case-sensitive address key, change only
  address case, expire exactly at/after the deadline, remove a selected entry,
  and cancel after viewing a different game's calendar/difficulty.
* Join by address after three missing-ID retries versus repeated open failures;
  inject a synchronous response before/after each counter reset. Exercise the
  different cancellation/unlock/flush paths and retained map dimensions.
* Race two civilization claims; refresh LockData during a modal; reject an
  existing civilization's password; fail its name-section load; cancel custom
  titles or style; reuse a tribe and confirm conditional/unconditional sign
  changes and distinct setup-slot cleanup indexes.
* Register a new slot while count and activity disagree; duplicate an inactive
  connection ID; remove an unassigned player using the declared excluded-write
  policy. Compare all valid effects preceding the unsafe mapping store.
* Deliver PlayerInfo during phase 1 versus 3 and during handoff; lose its slot
  during a child callback; distinguish the running-join flush loop's `>17999`
  continuation from a normal timeout. Inspect state-transfer order separately
  from N02's actual bytes and mirror application.
* Drop by connection ID while snapshot loss is indexed by slot; use a negative
  cancellation sentinel and retained eighth error count; verify recounting,
  claim broadcasts and client versus host return values.
* Migrate when slot index differs from civilization, with no peers, partial
  reconnects, an unmatched address with supplied caller backing, and a retained
  PlayerList count on the remaining client. Compare exact timeout equality,
  full-word options publication and low-byte local mode.
* Begin a resumed game with ShowHumanStarts/Chieftain: visibility sharing is
  turn 0/resume 0 gated but contact calls are not, and include self-pairs. End a
  client versus host loop and observe shutdown/chat-log/window order.

The fixture below exercises selected source paths with explicit service
providers. Complete integration still needs N02/N05 actual transport and
receipt, N03 timers/turn loops, F03/F05/F06/S01 loading/world/password children,
W03/V04 visibility/contact mutations and P01/P02/P04–P08/P10 native event,
control, chat and audio services. X02 must connect those owners with independently
specified expected traces; a script that repeats this implementation's formulas
is not sufficient evidence of original conformance.

## Appendix A. Representation, reset fields and excluded defects

These layouts and instruction details support validation. They are not required
internal representations for a cleanroom implementation. N02 owns complete
message encoding and state synchronization; F06 owns persisted multiplayer
blocks/password bytes.

### Scalar and record evidence

| Item | Original representation / boundary |
| --- | --- |
| Human, Alive, pending human | Eight-bit sets. Nominal human civilizations 1…7; advertised synthetic masks include bit 0. |
| Claimed civilizations | Unsigned 16 storage, signed 16 promotion on several sends/tests. Shift count is low five bits of the supplied low byte. For example civilization 16 can be accepted repeatedly because its bit is discarded on storage. |
| Player records | Seven records of 84 bytes: connection ID at 0, address 32 at 4, name 32 at 36, auxiliary 32 at 68, row ID32 at 72, host/active bytes 76/77, padding 78…79, civilization signed 32 at 80. |
| Player snapshot |624 bytes: count 32, seven records, eight signed 32 civilization-to-slot mappings. Session receiver offsets count `200`, first record `204`, mappings `450` (hex). |
| Advertisement |292 bytes with address 32, two original 32-bit links, timestamp/row ID, version 64, game/host names 32 each, open 16, difficulty/barbarian/Alive/player-mask bytes, timer 32, scenario 80, then signed 16 movement/production, flags 16, calendar, turn, map dimensions, simultaneous and phase fields. Padding is not an invented gameplay field. |
| Outgoing names | Ordinary player/game/IP dialogs 31 characters; tribe/leader/title dialogs 23. Password rows 32 including termination. Nominal gender 0/1, city style 0…3, tribe 0…20. |
| Received/error counts |169 signed 32 received counters indexed by N02 type; reset clears all 169. Lost-error storage has eight 32-bit entries, but callback accumulation and reset cover only indexes 0…6; total reset is separate. Lobby drop storage has seven entries. |
| Clock values |32-bit ticks with signed reduced subtraction. Typical wait bound is reduced `timeoutSeconds*60`; transport opens/flushes use milliseconds. The initial ID bound 1240 and running-join test 17999 are literal tick comparisons. |
| Native UI | Completion flag `0400`; lobby list seven visible rows, discovery 11; detail lines at x 230, advancing current body-font line spacing. New rows append copied text, disable empty text, and grow width to max(old, text width + icon width + extra width +5). |

The source's player-table aliases refer to the same records, not independent
copies. A portable implementation may use records and named fields while
preserving ordered reads/writes. Fixed-size address copies can fill their entire
field without a terminator; a later string read requires actual termination/
backing or a declared rejection. Nominal input producers avoid that condition.

The state-transfer child is called with original argument tuples
`(destination, kind, third, fourth, fifth)` as follows. N02 owns their complete
semantics and any sends/mirror changes inside the child; these are caller
validation vectors, not a required API shape:

| Caller | Tuple |
| --- | --- |
| Host lobby final world | `(255,1,0,0,0)` |
| Running join incremental / joining full | `(255,2,0,0,0)` / `(sender,1,0,0,0)` |
| Host departure | `(255,2,0,1,0)` |
| Lost-player recount | `(255,2,0,0,1)` |

### Session clearing

`clearSession` writes the following before shutdown, in source order within
each listed sequence. Entries not listed remain live; this is not a zero-all
operation:

1. Zero Connected, ToServer, popup-in-progress, Listing, phase, found-games head,
   discovery view, lobby pointer and dialog-stack top; server ID−1; active adapter 0,
   configured adapter 0, active protocol−1 and selected protocol−1.
2. Zero current network civilization, player-has-quit, poll depth/peak, normal/
   priority queue peaks, handoff stage, simultaneous mode, tile-sync and tile-batch.
   Set chat-missed 8, chat-unread 0, timeout 60, movement-animation sound/argument−1.
3. Zero mutation guards in order: unit create/delete, city create/delete, map
   lock/unlock, UI input, stack unlink/link/bottom, unit relocate, city wonder
   acquisition, civilization eliminate/create/check-destroyed and map redraw.
   Zero stacked-draw count/first/last, stack-check enabled and stacked-draw peak.
4. For each civilization 0…7 zero casualty count, reported count, first-log index
   and next-log index. Zero local-hotseat human set, cargo-gather/lift/delete/
   destroy-redraw guards, pending diplomatic meeting and pending-human set.
   For each civilization zero new-player, turn-ended and drop-request flags.
5. Zero deferred-input kind and drag-hold; call shutdown(1); reset errors again.

Shutdown's common tail is §3. This initial clear retains host role, selected
connection type, player table/count/mappings, ordinary options, address/name
buffers, received counters and other unmentioned fields; transport attachment
subsequently clears the player block and resets received counts on success.
The constructor has already discarded found-game/dialog references before
shutdown; do not add a traversal of the old objects to this operation. Discovery
view and lobby destructors both free the discovered-game list; their respective
option/row cleanup is performed by their callers, not by a generic destructor.

### Progress text

The original WAITTOJOIN second line is `>--------------------------------<`.
When §6's elapsed test passes, capture its string length and find the first `>`.
If at the last character change it to `<`; otherwise change it to `-` and its
next character to `>`. Then find the first `<` in the mutated string: if absent,
find `>` and write `<` immediately before it; if at the first character change
it to `>`; otherwise change it to `-` and its previous character to `<`. Render
using the live dialog, then capture a new start time. Nominal marker movement
stays within the produced writable line. Missing line/marker backing is a read
boundary; null/out-of-bounds writes are excluded, not a required error display.

### Repairs and bounded continuations

* **B18 open-game local:** the reconstruction initializes a value that the
  original first-pass Join does not produce. Require prior-attempt or supplied
  incoming backing, or choose a declared restrictive policy (§2).
* **BF-113 gender / BF-102 style:** reconstructions reject extra resource rows.
  Original accepted nonnegative values, storing the stated scalar reductions.
  Subsequent out-of-domain name/title reads need backing or a stop. Exclude any
  out-of-bounds writes; greater style alone does not authorize such a write.
* **BF-162 migration:** reconstruction bounds civilization mappings and stops
  after an unmatched-address diagnostic. The original read at `00482d15` can,
  on the sequential caller path, consume the caller's produced civilization
  beyond the local snapshot if the diagnostic returns. Model that supplied read
  or stop. Do not reproduce an invalid mapping write or assume the diagnostic
  necessarily terminates. Snapshot mappings are not replacement input for the
  missing saved-record read.
* **BF-222 paths / BF-040 password title:** reconstructions add a scenario-path
  preflight and enlarge the original 128-byte title scratch buffer. Oversized
  path/title overwrites are excluded. Preserve previous directory/option/name
  effects up to the chosen safe boundary. Nominal title concatenates local
  leader name and label without an additional adjective lookup.
* **Unassigned removal / malformed sender:** the original mapping write with
  civilization−1 and sender-indexed error/lobby writes can escape their arrays.
  These are excluded even where mapped storage has deterministic adjacent
  effects. The conditional Human clear and earlier slot clears are valid.
* **Allocation, strings and packet reads:** a returning allocation diagnostic
  does not produce an object; a missing terminator does not produce an empty
  string. N02 records packet-read validation additions and leaked padding;
  N01 does not turn either into default player state. Caller-supplied backing
  must identify its producer, lifetime and extent, or comparison stops.

Original instructions also resolve two decompilation differences: expected
reconnect count is stored before pending count (`00482b49/00482b53`); initial
startup and migration publish a byte locally but sign-extend the full 16-bit
simultaneous setting for the options message (`0041fe29/0041fe3d` and
`00482d4c/00482d60`). These are defined scalar/order semantics, not unsafe-write
behaviors or reasons to impose the original memory layout.

## Appendix B. Evidence and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is OpenCiv 2 at
`a71e8aefffe04e6f719e0e213b027b128a747711`, inspected at OpenCiv2 comparison revision
`fc2155ccdb2123c1137babab6fa39e38152278c9`:

| Family | Source and original entries |
| --- | --- |
| Session attachment/discovery/join/lobby | [network.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/network.cpp): `NetSession::clear` `0059a998`, attachment `0059adef`, discovery/join helpers and `NetGamelistView`, host lobby `0056e2e9`. |
| Claims, drop and migration | Same network source: publication `005233fc`, departure `004824e3`, migration `004828a5`, receive/drop/wait helpers and the session-specific dispatch cases. N02 owns the full dispatcher. |
| Setup, choices and continuation | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): network menu `00444310`, multiplayer choice `0051d9a0`, options `0051dd97`, new tribe `0051f19c`, profile `004259a6`, main `0041f8d9`, session begin/end and password title `004989d3`. |
| Password use | [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp): ensure `00498310`, change/prompt/check `00498a5c` family. |
| Records, widths and strings | [civ 2types.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h), [civ 2classes.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2classes.h), [network state](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/state/network.h), [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h), [gamedata.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/gamedata.cpp). |

Independent read-only evidence is `civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`: inspected portions of
`network_outer_startup.cpp`, `network_startup.cpp`, `network_join.cpp`,
`network_client_discovery.cpp`, and the host-migration implementation, together
with original `Civ2/Game.txt`. No accepted reconstruction campaign was resumed.
The original executable SHA-256 is
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Direct instruction inspection confirms the reconnect-store and options-width
distinctions above. No fresh native multiplayer session is claimed.

The historical Linux fixture compiled **26 complete source bodies**
for counter reset, connection callbacks, player add/remove, player-node/claim
receipt, flagged/lost drops, eleven join/claim/migration wait callbacks plus
the lobby barrier, advertisement construction, session visibility/contact,
reference restoration and multiplayer-option extraction. **52 assertions pass**
in optimized and AddressSanitizer/UndefinedBehaviorSanitizer builds with defined
signed wrap. LeakSanitizer cannot operate under the environment's tracing;
that component was disabled, and leak checking is not claimed.

Actual joins include player-node receipt→slot addition and lost-connection
check→flagged drops→slot removal. Cases distinguish duplicate inactive IDs,
full-table/count disagreement, send-time mutation of claim state,16-bit mask
reduction, slot-versus-ID loss, retained eighth counter, exact timeout equality,
pre-poll time capture, active==1 versus active!=0, advertised synthetic counts,
untouched phase, self-contact and resumed-game contact, signed 16 reference
restoration, and forced local options with retained preferences.

Explicit adapters supply modal completion, clock/poll/send/notice observations,
message-argument extraction, shutdown, text/CRT primitives, tile visibility and
contact calls, and checkbox results. Slot layout and count aliases are retained;
receiver pointer casts and dialog pointer carriers are widened for the host.
Advertisement links use host pointers because this fixture tests field
construction, not wire bytes. Two original record declarations and five source
literals are checked. The fixture does not execute unsafe writes, full native
menus, discovery allocation/reordering, real packet receipt, password encoding,
world generation, running-game image transfer or full host migration.

Source checks cover 108 inventoried complete bodies plus the referenced headers
and literal evidence at the pinned revision. The larger discovery candidate
inventory is not a claim of N01 ownership or complete review. Instruction bytes,
reference cleanliness, executable hash, Markdown links/anchors, specification IDs
and whitespace complete the bounded validation. Full native joins and independent
conformance remain the explicit §14/X02 obligations.
