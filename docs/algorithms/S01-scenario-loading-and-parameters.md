# S01 — Scenario loading and parameters

**Required profile:** [X01 fresh-game isolation and bug repairs](X01-classic-compatibility-contract.md#fresh-game-isolation-and-supported-bug-repairs)
and [behaviorally significant ordering](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
override conflicting procedures and examples below. Retention applies within a
game or to explicit imported inputs, not across new games. Confirmed original-bug
stack/backing/stop cases are optional diagnostics; use documented safe repairs.
Only real effects and callbacks impose ordering; test adapters cannot invent them.
Unspecified safe continuations remain explicit gaps, not required arbitrary bytes.

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. This document owns scenario parameter meanings/reset,
single-player and multiplayer scenario entry, briefing, setup choices and local
human-civilization selection. It describes the mutations and retry boundaries
around loading an existing scenario world.

[F06](F06-save-load-and-file-formats.md#11-caller-integration-boundaries) owns the
file dialog, alternative-file selection, core reader, reconstruction of loaded
state and post-load events/stack cleanup. [F03](F03-rules-and-configuration.md)
owns resource search and rule parsing. [F04](F04-turn-sequencing.md) owns calendar
conversion and resumed turn execution; [V08](V08-scoring-victory-and-retirement.md)
owns objective evaluation and scheduled retirement.
[S02](S02-event-language-and-execution.md) owns event parsing and execution;
[S03](S03-world-and-scenario-editors.md#7-scenario-mode-folders-and-parameters)
owns parameter-editor commands, folder preparation and world edits.
[N01](N01-multiplayer-session-and-lobby.md) owns network session continuation,
received scenario preparation and shared password use; N04 owns local handoff.
P01/P04/P05/P10 own native dialogs, asset loading, rendering and sound. Their callbacks are ordered
children, not assumed-success operations.

Original scenario interoperability remains required under
[X01](X01-classic-compatibility-contract.md#file-and-original-game-multiplayer-interoperability).
Load and run supported original scenarios with their F03 rules/resources and S02
events, and preserve the original-readable exports produced through S03/F06.
The new RNG does not change scenario flags, event conditions, one-shot state,
objectives, map-pattern features or the file/resource conventions. It may change
future sampled outcomes, including the turns on which random events fire.
Scenarios must work without cleanroom-only RNG metadata, including in mixed
multiplayer sessions following N01/N03's original authority assignments.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Compatibility policy

X01's [presentation policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy)
permits new briefing/setup dialogs and rendering while preserving scenario text,
substitutions, supported custom assets, available choices, observer restrictions
and load/continuation effects. Nominal original scenarios and custom rules remain
required; malformed/unsupported content may be rejected under its
[input policy](X01-classic-compatibility-contract.md#input-admission-and-rejection-policy)
before publication. Rejection does not require replaying the original partial
setup. Required mixed-peer scenarios use TCP/IP; omitted legacy transports do
not justify rejecting their otherwise supported scenario data.

## 1. Logical state, inputs and domains

Keep these distinct:

- Scenario status, saved-as-scenario status and the scenario option set.
  Loading a file selected with a scenario filter does not itself set every flag.
- Local human identity, saved player identity, current-turn civilization,
  human membership, saved resume civilization and live resume civilization.
- Saved resume-new-game value and live resume-new-game value. These affect F04's
  event/resume gates; they do not mean that a new map must be generated.
- Loaded world, live options and remembered preferences. A later cancelled
  dialog does not restore the pre-load world or undo earlier preference writes.
- Scenario resource directory, install/home directory, selected file path and
  the text resource associated with that file. The final selected alternative
  file supplies the path used by the briefing.

Use [F01](F01-determinism-and-arithmetic.md) arithmetic and
[F02](F02-game-state-and-invariants.md) identity/state rules. Unqualified expressions
have F01 reference results; division truncates toward zero. Scenario numeric
parameters retain signed16. Nominal difficulty is 0…5, player/current-turn/resume identities are 0…7,
human membership is a set of those identities and gender is 0/1. These need no
particular field width. Preserve `u8` at the specified publications for admitted
extra replies; hotseat completion reads the retained resume value unsigned.
Local human identity and modal replies retain full working integers. Government
title references retain signed16. These describe conversions at the stated boundaries, not storage types;
Appendix A records original encodings without requiring the same representation.

Nominal inputs are complete F06 scenario files and F03 rule/text resources,
valid live civilization/tribe/unit identities, terminated names, difficulty
choices 0…5 and gender choices 0…1. The scenario name fits its file field, and
the leader-name dialog supplies at most 23 characters plus a terminator. A
human-selection reply identifies an option actually created for that invocation.
Normally a local session requests between two and the available number of human
players; the selection routine itself does not enforce that range.

Accepted unusual scalar values use the stated narrowing, flags, branches and
partial publication. A stricter implementation may declare a narrower input
domain. Invalid record/option/name writes and adjacent corruption are
**excluded bugs** under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs),
including writes into allocated neighboring fields. Stop before an invalid
store, or use an established safe omission, retaining prior valid effects.
An unproduced or invalid read is separate: require established backing, supplied
input or a bounded stop. Do not manufacture an empty name or option mapping.

Named state is live at each access unless a step captures it. Dialog/resource,
audio and file callbacks can change state before the caller resumes. A routine
returning cancellation reports control flow, not transaction rollback.

## 2. Parameters and their consumers

| Parameter | Meaning and owner of its effects |
| --- | --- |
| Name | Briefing/editor description. Loading does not constrain it to the editor's shorter text-entry limit. |
| Technology paradigm | Research-cost multiplier in tenths; nominal default 10. [V03 research cost](V03-research-and-technology.md#7-research-cost) owns operation order, clamps and overflow. A zero or negative accepted value is not silently replaced here. |
| Calendar increment | Zero requests the ordinary calendar. Positive values use years; negative values select monthly presentation with magnitude in months. |
| Calendar start | Zero also requests the ordinary numeric calendar. A nonzero value is the year or month-based starting parameter. [F04 conversion](F04-turn-sequencing.md#5-turn-to-calendar-conversion) owns both gates, launch adjustment and arithmetic; display is a separate P04 operation. |
| Maximum turns | Zero uses ordinary scheduled retirement; a nonzero signed value is the scenario's turn deadline. [V08](V08-scoring-victory-and-retirement.md#5-outcome-producers-and-scheduled-retirement) owns warnings and outcome checks. The briefing's zero case displays the literal calendar value 2000, not a simulated deadline. |
| Objective civilization | Civilization whose objective holdings are evaluated; zero makes V08 use the civilization whose score is being queried. It also controls whether the active unit's sprite is eligible for the scenario briefing. It does not select or restrict the human civilization. |
| Four victory thresholds | Decisive victory, marginal victory, marginal defeat and decisive defeat, in that order. [V08 objective scoring](V08-scoring-victory-and-retirement.md#objective-score-procedure) owns comparison order; do not sort or clamp the loaded values. |

The option set has these independent consumers. A name below is not a promise
that every operation in the game obeys it; each linked algorithm retains its
own Scenario gate and exceptions.

| Option | Behavior ownership |
| --- | --- |
| Revolution restriction | V02/V03/V04/V05/A08 govern the specific revolution/senate paths. C08 also treats Eiffel Tower as inactive under its stated scenario gate. |
| Objective victory | V08 owns objective scoring/reporting and outcome interpretation; city/objective presentation consumes the same setting. |
| Wonders count as objectives | V08 owns the shared city objective value; C08 owns wonder visibility exceptions. |
| Whole-map reveal | W03 and P05 consume visibility/reveal rules; related W04 action and A04 exploration gates retain their own tests. S03's reveal/cover commands additionally mutate tile/city knowledge. Merely loading the flag is not those editor commands. |
| Government lock | V02 governs government availability, V03 its prompting and A08 its chooser. Relevant production/UI restrictions keep their own gates. |
| No technology from conquest | C09/V03 own the capture path. A08 also checks this option to suppress AI-to-AI technology exchange. The editor's short label does not describe that whole scope. |
| No ordinary pollution | [W05](W05-pollution-and-global-warming.md#ordinary-city-pollution) owns risk/workspace suppression. It does not suppress meltdown fallout universally. |
| Historical WWII AI | A01/A04/A06/A08 and V04–V06 own fixed-civilization/turn overrides; C08 owns the Manhattan visibility exception. Renaming tribes does not renumber these civilization slots. |

`resetScenarioParameters` clears the option set, makes the name logically empty,
sets technology paradigm to 10, and zeros increment, start, maximum turns,
objective civilization and all four thresholds. It does not clear Scenario
status, events, directories, world records or the whole preference record.
[F06](F06-save-load-and-file-formats.md#scenario-parameters) owns the retained
name payload and external encoding of this field-specific reset.

## 3. Loaded world and resource boundary

Invoke F06's **complete load-dialog wrapper**, with scenario selection enabled
and title display enabled. Its middle legacy argument is passed as stated for
each entry below; the wrapper does not use it to choose session authority.
In particular:

- F06 owns `.ALT` selection and its primary draw before the core read. Keep
  service enumeration order and the selected path; do not choose a new variant
  merely because a later name/gender dialog is retried.
- F06 selects the scenario directory/resource stem and invokes F03 rule loads.
  F03 owns fallback at the file/section boundaries, not a general merge of
  missing scenario rows with stock data. P05/P10 own image/sound lookup.
- Saved-as-scenario normalization, leader/name/title repair, unit counts,
  layout invalidation, loaded events, title and stack cleanup remain F06
  effects. This entry does not rebuild the loaded map, replace its civilizations
  or add new-game starting units.
- The scenario-loaded event hook occurs inside F06's successful wrapper, with
  its **music-enabled and events-loaded gates**, before this entry overwrites
  options again or chooses a human. S02 owns matching/action effects. Do not
  move the hook to the end of human selection or invoke it unconditionally.

The scenario entry routines consume no random numbers directly. File loading,
alternative selection, events and other children retain their ordered draws.
A full reload repeats those children; a local dialog retry does not.

### Common briefing after a successful load

[Native briefing documents](../content/data-format.md#64-scenario-briefings-and-credits)
associate a semantic scenario identity with a registered text message; the
compatibility layer associates identity-less classic worlds after final-file
selection. They supply the same information and continuation choices. The associated language-file
lookup below remains the classic adapter path, including its generic fallback;
native content must not bypass the surrounding load/setup mutations.

Each of the three entry routines below performs this ordered phase:

1. Replace the final selected path's extension with the active language
   extension, producing its associated text-resource path. Use F06's path rules.
2. If CD audio is available, play the menu-accept sound.
3. Replace live game options with remembered game options **with CheatMode
   cleared**, then replace live message options with remembered message options.
   This overwrites changes to these fields made by the successful load wrapper
   or scenario-loaded event. Other event effects remain. Network-host entry
   additionally clears the human set at this point; the other two do not.
4. Fill the briefing's name and technology-paradigm arguments. Convert the
   current turn through F04, format that numeric year/month value, and retain
   it as the start-description argument. For the end-description argument,
   format 2000 directly when maximum turns=0; otherwise convert the live
   maximum-turn value through F04 and format it. These are sequential calls.
5. If CD audio is available, end the menu sound. Initialize the dialog, then
   load the associated resource's SCENARIO section. A successful text load
   returns zero. Capture the active unit; if it is nonnegative, below the live
   unit scan bound, nonfree, and its signed owner equals objective civilization,
   add the sprite for its live unsigned type. Then show the dialog.
6. If the text load failed, show the generic ScenarioLoaded dialog instead.
   This fallback does not attach the active-unit sprite. A negative reply in
   either branch restarts at file selection; any nonnegative reply continues.

The briefing does not force selection of the objective civilization or require
that it be alive. Out-of-range sprite/record reads require backing or a stop.

## 4. Single-player scenario entry

Construct the dialog first, then its palette. On each file-selection iteration,
if CD audio is unavailable stop CD audio and start the menu loop sound. Load
the setup background, install it and its background color, then invoke
F06 loadDialog(scenario=true, legacyArgument=0, showTitle=true).

A nonzero load result exits this routine with that result. Otherwise perform
the common briefing and, after its nonnegative reply:

1. Reset the dialog and scan civilizations 1…7 ascending. For each live Alive
   membership, append an option labelled with its plural name and leader name.
   Associate the **returned option identifier** with that civilization. No
   objective-civilization or existing-human filter is added.
2. Show the civilization dialog. Negative reply reloads the scenario. Otherwise
   resolve the reply through the populated association **before** changing
   resume/player fields. Copy the old local-human byte to saved resume
   civilization, then copy that saved byte to live resume civilization.
3. Replace human membership with the selected civilization's bit. Set stored
   player identity to its unsigned byte, local human to its full identity,
   current-turn civilization to the local-human byte, then clear Tutorial.
4. Enter the difficulty/gender/name loop below. Cancellation leaves the
   preceding selection and loaded-world effects in place until another load
   or caller changes them.

### Difficulty, gender and name loop

1. Load Difficulty, defaulting to live difficulty; show it. A negative reply
   returns to **file selection**. Otherwise store its unsigned byte. Set
   Tutorial if that stored value is zero; clear it otherwise.
2. Load Gender, defaulting to the current player tribe's live gender; show it,
   then play menu-accept sound. Negative reply returns to **difficulty**.
   The original adds no upper-bound rejection here; nominal choices are 0/1.
3. Store the reply's unsigned byte as the current player tribe's gender. Clear
   FemaleLeader on the live local-human civilization, then set it when the
   **full reply**, not its narrowed byte, is nonzero.
4. Visit governments Anarchy…Democracy ascending; within each, male then female.
   Copy the corresponding current default government title reference into
   the live selected player tribe, retaining signed16. This resets both title
   columns regardless of the chosen gender and does not change government.
5. Ask for the leader name, using the live player civilization's current name
   and a 23-character input limit. Nonzero dialog result retries **difficulty**.
   On zero, copy the produced terminated string into the live player name,
   set saved resume-new-game=1 then live resume-new-game=1, and return success0.

At either normal exit, play menu-accept sound only for nonzero result. Reload
and install the setup background, retain the result, destroy palette, then
destroy dialog. Retries within the routine do not destroy/reconstruct that pair.
P04/P05 own actual lifecycle failures; a missing-input diagnostic is not proof
of a successful normal cleanup sequence.

## 5. Hotseat scenario preparation

`prepareLocalScenario(mode)` constructs dialog then palette, and repeats the
file/background/briefing phase. It additionally flushes background painting
before each load. Invoke F06 loadDialog(true,mode,true). A failed load plays
menu-accept, restores/paints the setup background, destroys palette then dialog,
and returns1. Briefing cancellation reloads; successful briefing enters:

1. Difficulty selection with the same byte/Tutorial logic as §4. Negative
   reply reloads the file.
2. The advanced-option loop below. A negative advanced-dialog reply returns
   to difficulty. A cancelled timer retries advanced options. Neither case
   reloads the file or rolls back already stored preferences.
3. On accepted options/timer, destroy palette then dialog and return0, without
   another background restore. This routine leaves both resume-new-game values
   zero. The later human-selection routine publishes them.

This entry does not set the game mode. Its hotseat caller sets mode1 after
success, chooses the local player count, then invokes §7. The callable mode2
branch skips the timer; the dormant e-mail menu is an N04 reachability issue,
not evidence that ordinary hotseat skips it.

### Advanced-option loop

At the beginning of every attempt, set saved resume-new-game=0 then live=0.
Clear dialog checks and seed these six from remembered menu preferences:
open game, kill civilization on retirement, show human starts, chat from start,
double production and double movement, in that order. Load AdvancedMP.

If the live session configuration disallows simultaneous movement, clear the
live network simultaneous flag and the saved simultaneous setting. Otherwise
append its option and seed its check from the remembered simultaneous preference.
For a check-style dialog, apply the collected check set to all its live options
in ascending order. Show it, then play menu-accept sound.

A negative reply cancels without reading check results, retaining the zeroed
resume values and any simultaneous-state clearing. For a nonnegative reply,
query and publish options in the six-option order above. Each result is retained
as signed16 in its live option, then copied to the matching menu preference.
The double options use the shared production/movement multipliers. If the
**now-live** configuration allows simultaneous movement, query that seventh
check, store the saved simultaneous setting and then its menu preference.
Do not cache the earlier configuration gate across the dialog.

For the local entry, mode2 accepts immediately; other modes invoke N03's
game-timer chooser with initialization argument0. Zero accepts; nonzero repeats
the advanced loop. This phase publishes options; their gameplay effects,
network propagation and timer mechanics remain with their owners.

## 6. Network-host scenario preparation

`prepareNetworkScenario(legacyMode)` is a distinct routine. It constructs dialog
then palette and uses the same background/briefing phase, but does not add the
local entry's pre-load paint flush. It calls F06 loadDialog(true,**3**,true),
regardless of its argument, and clears human membership during the successful
post-load preference override. Load failure restores/paints background and
returns1 with normal palette/dialog cleanup.

After a nonnegative briefing reply, select difficulty and run the same advanced
option attempt. The retry destinations match §5. After publishing nonnegative
option results, **set saved resume-new-game=1 then live=1 before calling the
timer chooser**. Always call that chooser with argument0; there is no mode2
skip. Nonzero result retries advanced options, whose first step clears the
resume flags again. On zero, copy the live local-human byte to live resume
civilization, then to saved resume civilization, and return0 after normal
palette/dialog cleanup. Human membership stays as left by intervening callbacks;
this routine does not choose the host's civilization.

N01 owns the following lobby/host assignment and the client receipt path. A
client receives scenario state/assets and has its own briefing/cancel messages;
it does not run the local file chooser or pick another `.ALT` file.

## 7. Choosing local human civilizations

`chooseScenarioHumans(count)` constructs one dialog, clears human membership,
sets live resume civilization to zero and starts playerIndex=0. The requested
count is captured; other state is live. Repeat while playerIndex<count:

1. Reset the civilization dialog, prefix its title with this player's label,
   and scan Alive civilizations 1…7 ascending. Label each with plural name,
   leader name, signed city count and signed military-unit count. Associate
   its returned option identifier with that civilization. Disable an option
   already in the human set; select the first unselected option as default.
2. Negative modal reply destroys the dialog and returns1. Previously accepted
   players and other partial effects remain. A nonnegative reply resolves the
   populated association, and for playerIndex0 stores its byte as live resume
   civilization. Add its bit to human membership, set player/local-human/
   current-turn identities to the selected values, and clear Tutorial.
3. Load the Name resource with a 23-character limit and the current player name.
   If loading fails, restart this player's civilization selection **without
   clearing the just-published human bit**. It can therefore appear disabled
   on retry even though this player has not completed setup.
4. Prefix the name dialog title with the player label and run it. Nonzero result
   clears this selected civilization's human bit and restarts its selection.
   On zero copy the produced terminated name to the **live player identity**.
5. Load Gender, prefix the player title, use the live player tribe's gender as
   default, show it and play menu-accept. Negative reply clears the selected
   human bit and restarts civilization selection. The already copied name and
   first-player resume choice remain. Nonnegative reply applies §4's gender,
   FemaleLeader and all-government-title mutations, then increments playerIndex.

After enough players, copy live resume civilization to saved resume civilization;
read it unsigned into local human and current-turn civilization; set saved
resume-new-game=1 then live=1. Destroy the dialog and return0. **Stored player
identity remains the last selected one**; it is not synchronized to the first
human on this return. Zero/negative count takes this completion path immediately,
leaving the cleared human set and resume/local/current-turn zero while preserving
the incoming stored player identity. Do not add a synthetic selected player.

The disabled UI option is the normal prevention of duplicate choices. The
caller adds no second duplicate/alive/available-slot test after the modal reply.
An exhausted list does not automatically reduce the requested count.

## 8. Caller continuation and off-nominal distinctions

The ordinary main-menu scenario branch clears the event's suppress-wonder-movies
request before entry. On success it clears live WonderMovies when that request
was set by loading/events; on failure it restores the scenario directory to
home. This runs after the entry's option overwrite. Hotseat similarly applies
the request, sets mode1, chooses a player count and calls §7. Successful hotseat
completion adds its change-player menu item, saves preferences and restores/
paints the setup background. N04 owns that surrounding session flow. Network
host/client continuation belongs to N01.

Scalar reduction is independent of memory safety. For example, difficulty256
stores0 and enables Tutorial. Gender256 stores0 in the tribe but its nonzero
full reply sets FemaleLeader. The original entry accepts that publication;
later gender-indexed name/title reads have their own backing boundaries.
The reconstructions' BF-113 upper-bound rejection is a declared repair, not an
original validation rule. Rejecting nonnominal menu values is a permissible
stricter acceptance policy.

Returned option identifiers populate a finite mapping; only written entries
have established values. A malformed option identifier that overruns that mapping
is an excluded-write bug even if it lands in another allocated local. An
unwritten returned selection requires supplied original backing or a stop before
the dependent publication; it is not civilization0 by default. Likewise, an
accepted name dialog that failed to produce bytes does not establish an empty
name. The independent reconstruction's BF-041 zero-start name policy is separate.
Name/path/filter spills and writes through invalid civilization identities remain
excluded regardless of the original destination's mapping.

## 9. Discriminating conformance cases

| Initial state and input | Required distinction |
| --- | --- |
| Reset a nonempty scenario name with nonzero settings | Logical name becomes empty and numeric defaults reset; later name payload survives. World/events/Scenario status are not reset. |
| Scenario start100, increment−12, turn2, maximum5, no first launch | Briefing formats F04 numeric values123 and159. Maximum0 formats2000 directly. |
| Event changed options during successful load | Post-load entry replaces them with preferences minus CheatMode; other event effects remain. |
| Alive civilizations1 and3; returned option1 maps to3; old local human5 | Single-player selection publishes human3 while both resume-civilization fields capture5. |
| Gender or name cancelled after selecting a civilization | Retry difficulty with one loaded world. Cancelling difficulty instead restarts file selection. |
| Gender256, valid player/tribe and name output | Tribe gender0, FemaleLeader set, both title columns reset. No original upper-bound rejection. |
| Briefing custom text missing | Use generic ScenarioLoaded; do not attach the active-unit sprite. |
| Active nonfree unit belongs to objective civilization | Eligible for briefing sprite; human choice still includes all Alive civilizations. |
| Advanced options accepted, timer cancelled | Preferences already changed; repeat advanced options. Reinitialization clears both resume flags. |
| Hotseat preparation completes | Resume-new-game values remain0 until human selection. Network preparation sets them1 before timer acceptance. |
| Select humans3 then1 | Completion restores local/current-turn/resume3, retains stored player1, and keeps both human bits. |
| Name resource fails for selected civilization1, then retry selects3 | Human bit1 remains; successful count1 can leave both bits set. |
| Name accepted, gender cancelled, then civilization dialog cancelled | Copied name survives; selected human bit is cleared; routine returns1. |
| Requested human count0 | Success with empty human set and local/current-turn0; stored player identity is retained. |
| Option mapping or name copy would overwrite adjacent state | Excluded bug: stop/omit safely at the write, retaining prior valid effects. No corruption oracle is required. |

## Appendix A. Original encodings and instruction distinctions

F06 owns the 100-byte scenario file block. Option masks are RevolutionRestriction
`0001`, ObjectiveVictory `0002`, WonderObjectives `0004`, WholeMapReveal `0008`,
GovernmentLock `0010`, NoConquestTechnology `0020`, NoPollution `0040` and
HistoricalAI `8000` hex. Other option bits remain retained unless an owner
changes them. CheatMode is game-option `8000`, Tutorial `0100`, FemaleLeader
is civilization flag `0200`. These sets must not be conflated.

The setup background is resource90 with palette start10/count192 and color158.
Briefing resources use SCENARIO/SCENARIOLOADED; setup uses DIFFICULTY, GENDER,
NAME and ADVANCEDMP. The network/local advanced checkbox order is0…6 as in §5.
Palette/dialog construction and normal destruction are ordered; private stack
sizes, native widget layouts and array offsets do not constrain a new design.

Original `0041e3aa` resolves the full civilization-option value before the old
local human is saved at `0041e3b4`…`0041e3c3`. It publishes full local human at
`0041e3eb` before current-turn byte and Tutorial clearing. The primary source
reorders these operations and narrows an intermediate; §4 retains original
order at an invalid-read boundary. Ordinary valid options yield the same values.
Original `0041e540` tests only whether the gender result is nonnegative before
the byte store at `0041e578`. BF-113 is absent there.

F06's original load-dialog filter overwrite/BF-038 and missing-path-separator
failure are distinct from `.ALT` draw/order semantics. Adjacent-frame choice or
name spill behavior recorded by either reconstruction is excluded-write evidence;
it must not become logical assignments to unrelated fields in a cleanroom port.

## Appendix B. Evidence and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is OpenCiv2 at
`a71e8aefffe04e6f719e0e213b027b128a747711`, unchanged for the selected bodies at
OpenCiv2 comparison revision `fc2155ccdb2123c1137babab6fa39e38152278c9`:

| Behavior | Source and original entry |
| --- | --- |
| Single-player scenario entry | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp), `setup_start_scenario`, `0041dfe1`. |
| Network-host preparation | Same source, `setup_mp_load_scenario`, `00445712`. |
| Hotseat preparation and human selection | `scenario_start_dialog`, `005218cb`; `scenario_pick_human_civs`, `00521fe0`. |
| Parameter reset and calendar consumer | `scenario_reset_params`, `00484cc0`; `game_turn_to_year`, `00484fec` (F04 owner). |
| Loading and surrounding callers | F06's `game_load_dialog` at `00477d8c`; main-menu/hotseat/network branches in the same source. |
| Parameter UI labels and consumers | `cheat_scenario_params` at `005582ad`, victory editor `00557e2c`, special rules `0055819b` (S03 owners); F04/V03/V08/C08/W05 and A08 provide effect contracts. |

Independent read-only evidence is `civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`: `src/startup_scenario.cpp`,
`src/startup_scenario_frames.hpp`, `src/network_outer_startup.cpp`,
`src/scenario_parameters.cpp` and `Civ2/Game.txt`. The accepted reconstruction
campaign is not resumed or modified. Original executable SHA-256 is
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Bounded original instructions resolve option-publication order and the gender
admission gate. No fresh original gameplay or saved annotation cycle is claimed.

The historical Linux harness compiled **six complete bodies**:
all four entry/selection routines, parameter reset and the actual F04 calendar
consumer. **31 assertions pass** in optimized and AddressSanitizer/
UndefinedBehaviorSanitizer builds with wrapping arithmetic and disabled strict
alias optimization; leak detection is disabled under sandbox tracing. Actual
saved record definitions and calendar data are retained, with explicit dialog,
file-load/path, text, timer, image and sound adapters. The fixture's calendar
initializer copies the source initializer into zeroed backing, as the source
does; an initial harness over-read of the short initializer was corrected.

Declared adaptations remove BF-113's added rejection, restore the original
single-player publication order, and clear the four contiguous threshold fields
without host array-bound pointer arithmetic. Host pointer carriers are widened.
The fixture does not force invalid mapping/name writes or certify native dialog
layout, retained private-frame inputs, arbitrary callbacks or exception unwind.
It joins the real calendar but supplies the F06 load result and resource/modal
outcomes; it does not certify full file/rule/event/asset loading.

Source-pin/record checks, original executable hash, clean evidence repositories,
links/anchors, specification IDs and whitespace are checked. Remaining integration
is actual F03/F06 loading, S02 event actions, P04/P05/P10 callbacks and N01/N04
session continuation, followed by F04 resumed turns. X02 must retain ordered
state, RNG and callback traces across repeated selection and failure. These checks
do not establish complete original-program conformance.

## Appendix C. Original numeric annotations

These original annotations retain storage widths and reference-evaluation context.
They do not prescribe internal types or redundant casts. The main contract and
F01 determine logical ranges and behaviorally significant conversions; external
file/wire encodings remain normative in their owning mappings. Where an excerpt
includes an off-nominal conversion, it applies to that explicitly admitted profile,
not as a new requirement to accept unsupported inputs.

### Reference annotation 1

Difficulty, stored player/current-turn identities,
human membership and tribe gender retain unsigned8. Resume civilization has
the saved byte domain; the hotseat completion below explicitly reads it unsigned.
