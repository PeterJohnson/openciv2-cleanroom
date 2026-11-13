# S02 — Event language and execution

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. This document owns event-file interpretation, event-list
operations, trigger matching, action dispatch and the event-specific parts of
each action. Events are ordered, mutable records. They are not independent
rules evaluated against a frozen world.

[F03](F03-rules-and-configuration.md) owns resource search, section selection,
line/token reading and prefixed integer conversion. [F06](F06-save-load-and-file-formats.md)
owns serialized event records, pointer relocation and load/save orchestration.
[S01](S01-scenario-loading-and-parameters.md) owns scenario entry and parameters.
[S03](S03-world-and-scenario-editors.md#8-event-editor-session-and-services) owns
event editing/writing and [S04](S04-rules-and-asset-editors.md#5-unit-editor-and-event-name-propagation)
owns rule-name propagation. The gameplay children below keep their existing
owners. P04/P10 and N02 own actual dialogs,
audio services and network message realization; calls to them can fail or change
state before execution resumes.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

Event-list matching, sticky wildcard bindings, action order and late Consumed
publication remain dependencies. Pure trigger calculations do not authorize
freezing later actions or parameters before an earlier action/callback runs.

## Compatibility policy

X01's [input-admission policy](X01-classic-compatibility-contract.md#input-admission-and-rejection-policy)
permits declared rejection of malformed/unsupported event files before executing
their actions; nominal custom event syntax, supported defaults and stored state
remain required. If unusual input is explicitly admitted, preserve its recovered
interpretation and relevant partial effects. Normal-play wrap, action ordering,
one-shot consumption, probability laws and triggers are unchanged.

Under the [presentation policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy),
Text/PlayWave/PlayAVI and other presentation actions may use new dialogs and
playback services. Keep authored text/substitutions and selected supported media,
recipient/visibility, meaningful information and gameplay/protocol completion
and cancellation. A shortened cosmetic animation must not skip later event
actions, rerun a consumed event or apply a network result twice.

## Implementation boundary

The main sections use logical values, identities and operations. Original memory,
allocation and platform mechanics are recorded in [Appendix C](#appendix-c-original-implementation-reference). Preserve required
state changes and externally visible results without reproducing those mechanisms.
Exact numeric domains and file/wire encodings remain required wherever specified.

## Native definitions and classic event grammar

[Native JSON event definitions](../content/data-format.md#8-scenario-events)
provide a standalone authoring path into these logical records. Classic EVENTS
text remains supported with the grammar, name resolution and parsing effects
below. Native completeness checks do not narrow classic nominal acceptance.
Both paths use the same list matching, live action flags/parameters, fixed action
order, wildcard retention and late Consumed publication. JSON object member
order is not action execution order. Content definitions never replace F06's
saved event continuation state on an ordinary resume.

## 1. State, value domains and input profiles

An event has one trigger kind, an enabled-action set, a JustOnce flag and a
Consumed flag. It retains all trigger/action parameters, including parameters
irrelevant to its current kind. Keep stored display names separately from their
resolved civilization/unit selectors. Runtime matching uses the selectors
except where a name comparison is explicitly required. Changing a trigger does
not clear its other fields or actions.

The event collection has a defined order with successor/predecessor queries,
an event count, logical string identities and a separate loaded-events indicator. Each record has:

- Trigger unit selector/name; city name; Who and Whom civilization selectors
  and names; talker/listener control classes; turn/interval, random denominator
  and received-technology selector; negotiation permission value.
- Twenty optional text strings. An absent string differs from an empty string.
- Separate movement unit/owner selectors and names, quantity, four coordinate
  pairs and a destination; separate creation unit/owner selectors and names,
  ten location pairs, location count, veteran value and home-city name.
- Aggression Who/Whom selectors and names, money recipient/name and amount,
  destruction recipient, technology gift/recipient, wave filename, CD track,
  terrain value and four terrain coordinate pairs.
- Retained ancillary payload, including two additional name references used
  by copying/renaming. Their storage alone does not give them an execution role.

Use [F01](F01-determinism-and-arithmetic.md) arithmetic and
[F02](F02-game-state-and-invariants.md) identity rules. Event numbers and
working counters follow F01 reference arithmetic; their significant wrap remains
required, independent of the implementation's variable sizes. Creation
tests its resolved owner using the corresponding unsigned value. Membership
tests shift by the selector modulo32; an invalid selector is not automatically
clamped to a civilization. Relevant unit coordinates retain signed16; home
references retain their byte domain and Absent sentinel. Move eligibility reads
attack and orders as signed8. These are observable value domains, not a required
record layout. Appendix A contains original encodings.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. State-dependent ordering and callbacks remain required.

Original scenario files and exported scenarios retain this language, trigger
probabilities and persistent event state. A different generator may change which
eligible random trigger matches without changing its formula. Native JSON
authoring is separate from the classic scenario/event-text format; classic
exports and original peers continue to use the syntax specified here. In mixed
multiplayer, keep each caller's specified execution authority and state publication. A received event effect is not a
request for every peer to roll the trigger again. N01–N03 own those boundaries;
F06 owns persistence and S01 scenario interchange.

Nominal event files have a BEGINEVENTS section, complete IF/THEN/ENDIF blocks,
one complete trigger per block, complete action parameters, resolvable names,
valid gameplay selectors and bounded strings. Coordinates identify supported
map locations; creation supplies one through ten contiguous location pairs;
movement/terrain rectangles use ordered corners. Random denominators are 1…1000,
technology and terrain ordinals identify actual rule entries, and CD tracks are
valid for the selected disc. Name spellings and resource lines obey F03's
character and line contracts. Network text must also fit its output-string
service; the parser's twenty-line capacity is not proof of that capacity.

The accepted parser domain is substantially wider. The sections below describe
incomplete outer blocks, repeats, shared recognition flags, ignored lines,
numeric wrap, zero/negative quantities and partial failure. A new implementation
may declare stricter admission. Do not silently call that full compatibility
for the excluded input profile.

Out-of-bounds writes and resulting corruption are **excluded bugs** under
[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs).
Do not turn an overlong text/path or invalid record write into assignments to
neighboring logical state. Reject the unsafe input, omit an independently
established unsafe write, or stop before it while retaining valid prior effects.
Unknown or out-of-range reads are a separate boundary: require established
backing, supplied input or a bounded stop. In particular, accepting AnyUnit in
CreateUnit does not manufacture a valid unit type for its later lookup.

State is live at every stated access unless explicitly captured. Callbacks,
actions and nested event checks may mutate records, list links, names, counts,
options and the world. A fault/stop preserves preceding effects; there is no
general event transaction or automatic rollback.

## 2. Collection, copying and lifetime

The event collection is ordered and has a count, payloads and associated string
identities. Reset makes it empty and establishes its logical storage/admission
budget; it does not set the separate loaded-events indicator. A replacement may
use any safe collection and string representation.

### Event creation

Append one event after the current last event and increment count only on success.
Initial numeric fields, selectors and flags are zero, not named wildcard/Absent
values; string references are Absent. Failure adds nothing. Parsing appends to an
existing collection without an implicit reset. Required resource-limit failures
and preceding published edits remain observable; no linked-node allocation is
prescribed.

### Copying

The nominal destination is empty. First append each source event's complete
payload in source order, establishing destination order independently of copied
references. At this stage string identities still refer to source strings. Then
walk source and destination from their respective beginnings and duplicate strings
in this order:

1. Trigger unit, city, Who, Whom.
2. Text slots from 0 up to the first absent string or 20.
3. Move owner, move unit; aggression Whom, aggression Who; creation owner,
   creation unit, home city; ancillary civilization name, ancillary unit name;
   money recipient; wave file.

Publish each new logical string identity before continuing; copy through the
first terminator. Stop when source ends. Preserve action flags, Consumed,
selectors, coordinates and other payload. Do not truncate content to an original
allocation capacity. A failure retains preceding appended events and copied
strings without inventing a successful empty value.

If the implementation admits a nonempty destination, string rewriting still
starts at its old first event although payloads were appended at its end. A hole
in the text slots ends duplication even though Text execution visits all 20 slots;
later strings can retain source identities. Preserve those logical relationships
for admitted inputs. Self-copy or invalid references need an established domain
and safe failure boundary; original dangling-pointer traces are appendix evidence.

### Compaction

Compaction preserves nominal event order, payloads and string values, including
Consumed and bound wildcard selectors. It is not a reset of one-shot events.
For supported alias-sensitive inputs, retain the reference relationships produced
by two successive Copy operations above, including their text-hole stopping rule.
Distinct copied fields acquire distinct string identities even if their source
names were shared; values are not truncated. A direct compaction or representation
that needs no physical compaction is permitted only when it preserves those
observable relationships and supported capacity/failure effects. S03 callers
retain logical event identity across it; F06 owns serialized references.
The original temporary collection, two copies, pool reset and destruction sequence
is reference implementation machinery, not a required allocation strategy.

Original implementation: [Appendix C](#appendix-c-original-implementation-reference).

## 3. Names and shared reader

All keyword, field-prefix, enumerated-value and name comparisons below are
case-insensitive. No prefix abbreviation or additional whitespace normalization
is implied. For example, `Owner=Romans` matches the field prefix; `Owner =Romans`
does not. Whole-line trimming does not trim the value separately after `=`.

Civilization resolution tests, in order:

| Spelling | Resolved meaning |
| --- | --- |
| Anybody | Wildcard, numeric selector−2. |
| TriggerAttacker | Indirect Who, selector−3. |
| TriggerDefender or TriggerReceiver | Indirect Whom, selector−4. |
| Otherwise | Scan civilization identities 0…7 in order, comparing their live plural names; return the first match. Alive membership is irrelevant. |

Failure is−1. These reserved spellings win over an identical civilization name.
Unit resolution tests AnyUnit first, returning−2; otherwise scan all 62 classic
unit-type names in ascending order and return the first match, regardless of
prerequisites/enabled status. Failure is−1. UnitKilled rejects both−1 and−2;
CreateUnit and MoveUnit reject only−1. Most civilization fields reject only−1,
so wildcard/indirection selectors can enter fields where execution gives them
no special meaning.

F03 supplies trimmed lines and a mutable shared line buffer/cursor. A read can
replace the contents observed by an earlier pointer to that buffer. EOF clears
the buffer and closes the stream. The classic reader consumes at most 254
characters per read; a longer physical line can become multiple logical reads.
Blank/comment-like lines are not automatically skipped by this event layer.

`Integer(value)` means F03's decimal or `0X`/`0B`/`0D` prefixed conversion with
its exact stopping and wrap rules. A “digit gate” tests the first character
against 0…9 **before** conversion. A “signed gate” additionally accepts `+` or
`-`. Thus `0D-1` passes a digit gate while `-1` does not; a bare sign passes a
signed gate and converts to 0. Coordinate/CD readers use F03's next comma token
and integer conversion. Missing comma items convert from empty tokens to 0.

## 4. Outer parser and recognition state

Start with failure result 1, phase Outside and debugging disabled. Open the
requested filename/section using F03. Failure takes the common error tail.
The load caller normally supplies EVENTS/BEGINEVENTS. The parser does not infer
or repair the section from the file's contents.

For each outer line, first handle a leading `@`:

| Directive after `@` | Operation |
| --- | --- |
| IF | Allocate and select a new zeroed event; reset the21 recognition flags below; enter Trigger phase. Do this regardless of the previous phase. Allocation failure takes the error tail. |
| THEN | Enter Action phase; do not allocate an event or reset recognition flags. |
| ENDIF | Enter Outside-after-block phase; do not validate completeness or discard the selected event. |
| DEBUG | Enable debugging, then report the filename. |
| ENDEVENTS | Finish successfully, without forcing an EOF read/stream close. |
| Any other directive | Ignore it. |

An outer EOF also succeeds, even with an unfinished or entirely empty event.
For an ordinary line in Trigger/Action phase, dispatch as below; otherwise
ignore it. A helper's failure ends parsing with result 1. Earlier records,
allocations, overwritten references and fields remain. A new IF does not remove
an unfinished preceding record.

The original has no initialized current event/recognition state before IF.
An action after a bare THEN can consume lines or allocate strings before its
first missing-input read. Unknown action words require no event and are ignored.
Require original retained input or stop at that first dependency; a declared
strict malformed-file rejection is also suitable. Do not claim that an early
blanket rejection reproduces all preceding original effects.

The error tail changes to the live home/install directory, shows BADEVENTSFILE,
then changes to the **live** scenario/search directory after the notice returns.
Ignore both directory-change results and the notice result. Do not add rollback
or an unconditional stream close. EOF in an inner helper has already closed the
stream through F03; an illegal directive or allocation failure need not have.

### Shared recognition flags

Reset these only at IF, not when a trigger/action keyword repeats:

| Logical recognition flag | Fields/verbs sharing it |
| --- | --- |
| Killed unit | UnitKilled Unit. |
| Attacker | UnitKilled Attacker; CityTaken Attacker; Negotiation Talker. |
| Defender | UnitKilled/CityTaken Defender; Negotiation Listener; NoSchism Defender; ReceivedTechnology Receiver. |
| City | CityTaken City. |
| Talker class, Listener class | Negotiation's two type fields. |
| Received technology | ReceivedTechnology Technology. |
| Terrain | ChangeTerrain TerrainType. |
| Unit, Owner | **Shared across CreateUnit and MoveUnit**, despite their distinct record fields. |
| Home city, Veteran | CreateUnit. |
| Destination, Move quantity | MoveUnit MoveTo and NumberToMove. |
| Money receiver, Amount | ChangeMoney. |
| Aggression Who, Aggression Whom | MakeAggression. |
| Destruction recipient | DestroyACivilization Whom. |
| Gift receiver, Gift technology | GiveTechnology. |

A helper loops until its flags and any local counters are complete. Within the
loop, recognized valid fields overwrite prior fields and set their flag; invalid
fields normally leave both the old field and old flag intact. Unknown lines are
ignored. A complete repeated helper can return without consuming any parameters,
except the numerical triggers and local-coordinate counters described below.
CreateUnit can therefore accept a block with zero creation selectors after
MoveUnit has set the shared Unit/Owner flags, and conversely. Do not copy values
between those distinct payloads to “complete” them.

For ordinary named fields, resolve the name, allocate a fresh string, publish
its reference, check allocation success, copy the spelling, store its selector,
then set recognition after any diagnostic. Duplicate fields abandon the old
allocation; there is no automatic reclamation during parsing. Two trigger
fields deliberately resolve twice, as specified next.

## 5. Trigger grammar

Ordinary parameter loops fail on EOF or a line beginning `@` before completion.
These lines are consumed; the outer parser does not reprocess them. Each
completed trigger **replaces** the event's trigger kind; it does not add another
kind. Parameters can appear in any order subject to the live recognition flags.

| Trigger word | Required parameters and publication |
| --- | --- |
| UnitKilled | `Unit=name`, `Attacker=civ`, `Defender=civ`. Unit must resolve to a definite type. Store names/selectors, then set UnitKilled. |
| CityTaken | `City=name`, `Attacker=civ`, `Defender=civ`. City is copied without a city lookup or nonempty test. Set CityTaken when all three flags are set. |
| Turn | `Turn=Every` stores−1; otherwise a signed-gated integer. Store turn and kind before the diagnostic. |
| TurnInterval | `Interval=` signed-gated integer, including0/negative. Store interval, diagnostic, then kind. |
| RandomTurn | `Denominator=` digit-gated integer. Store it **before** checking1…1000. Outside that range, print the invalid-denominator diagnostic even when debugging is off and fail. Otherwise diagnostic then kind. |
| Negotiation | `Talker=civ`, `TalkerType=Human/Computer/HumanOrComputer`, `Listener=civ`, `ListenerType=Human/Computer/HumanOrComputer`. Store identities in Who/Whom. Completion sets Negotiation and its Deny permission value. |
| ScenarioLoaded | No parameters; set ScenarioLoaded immediately. |
| NoSchism | `Defender=civ`; store Whom, then set NoSchism. The field is not named Whom. |
| ReceivedTechnology | `Receiver=civ`, `Technology=` digit-gated integer; no technology-range check here. Store receiver in Whom and set ReceivedTechnology when both flags are set. |

ReceivedTechnology Receiver and CityTaken Attacker first resolve to reject−1,
then allocate/copy the name, **resolve again**, and store the second result without
revalidation. Other civilization parameters cache their first result across
allocation/copy. Callback-driven name changes can distinguish these cases.

Turn always reads until it finds a `Turn=` prefix, even if the event already has
kind Turn. After such a line, an invalid value can leave an old Turn kind/value
and satisfy the loop. TurnInterval and RandomTurn also read at least one line,
but test their existing kind after **every** ordinary line, including an unknown
prefix. Consequently a repeated interval/random verb can finish after an ignored
line. Their inner EOF/directive checks still precede that test.

Trigger dispatch is unusual: compare UnitKilled and invoke it if matched; then
compare the **current** line against Negotiation and invoke it if matched; then
test NoSchism, else ReceivedTechnology, else CityTaken, else Turn, else
TurnInterval, else RandomTurn, else ScenarioLoaded. Earlier helpers can replace
the shared buffer and, for UnitKilled/Negotiation/NoSchism, the current-line
reference. Do not preselect one trigger from a saved original line and skip the
remaining comparisons. Unknown words do nothing.

Debugging uses a retained “last parsed civilization” temporary. IF does not
initialize/reset it. ReceivedTechnology Receiver and CityTaken Attacker can
print that stale/unproduced value even though their actual selector store is the
fresh second lookup. Require its supplied value or stop before the dependent
diagnostic. CityTaken City's extra argument is unused by its format and has no
result/effect/fault dependency once the ordinary private-frame read is established;
it can be folded. Negotiation's invalid ListenerType diagnostic tests the Talker
recognition flag. These diagnostic defects do not change the specified selectors.

## 6. Action grammar

Action dispatch is a single exclusive chain in this order: Text, ChangeTerrain,
CreateUnit, ChangeMoney, JustOnce, DontPlayWonders, MakeAggression,
DestroyACivilization, GiveTechnology, MoveUnit, PlayCDTrack, PlayWaveFile.
Unknown words are ignored. Completing an action enables its flag without
clearing existing actions or Consumed. It replaces that action's parameters
only where the helper actually writes them. File order is not execution order.

### Text and audio

Text resets a local output index to 0 and reads its first content line. EOF
fails. Until a line equals ENDTEXT, copy it into the next text slot if fewer
than 20 have been stored; otherwise discard it. Read again after each line;
subsequent EOF or a leading `@` fails. Thus the **first** content line may begin
`@`, while later ones may not. ENDTEXT enables Text. Longer blocks consume all
remaining lines through ENDTEXT. A repeated shorter/empty Text block leaves the
old trailing string references intact; it does not insert an absent terminator.
Partial text stored before failure remains, without newly enabling Text.

PlayCDTrack consumes exactly one line, converts its first comma token, and
stores the track. Enable the action only if the stored value is positive;
nonpositive repeats do not clear a previously enabled action. PlayWaveFile
consumes one line, allocates/copies it as the filename and enables the action.
Neither helper rejects an empty or `@` line. Neither probes or plays media
during parsing. EOF fails. JustOnce and DontPlayWonders simply enable their flags.

### Civilization effects

| Action | Required fields |
| --- | --- |
| ChangeMoney | `Receiver=civ`; `Amount=` signed-gated integer, with no amount bound. |
| MakeAggression | `Who=civ`; `Whom=civ`. |
| DestroyACivilization | `Whom=civ`. Store only the selector, not a name string. |
| GiveTechnology | `Receiver=civ`; `Technology=` digit-gated integer, without ordinal validation. Store selectors, not name strings. |

All civilization fields reject only resolver−1; ordinary parameter-loop rules
apply. Completion enables the respective action. The shared flags mean a
complete repeat can enable again without consuming fields.

### ChangeTerrain

Reset a local pair count to 0. Loop until the Terrain flag is set and count is
exactly 4. `TerrainType=` requires a first digit, then converts its integer.
The original validation is `value >= 0 OR value < 11`, which accepts every
converted signed value. Store the value and set the flag.

The exact word MAPRECT consumes one additional line and eight comma integers,
storing four consecutive X/Y pairs. This nested line has no leading-`@` or
numeric gate. After each pair, increment the local count. Repeated MAPRECT
starts storing again at pair0 but retains that count, so count 8 does not satisfy
the completion test. A repeated ChangeTerrain verb resets count and needs a
new rectangle even when Terrain was already recognized. Completion enables
ChangeTerrain. Ordinary outer parameter EOF/`@` still fails.

### CreateUnit

Reset a local successful-location count to 0. Loop until Unit, Owner, Home city
and Veteran are recognized and this count is nonzero:

- `Unit=name` uses the permissive unit resolver, including AnyUnit.
- `Owner=civ` uses the civilization resolver.
- `Veteran=Yes/True` stores1; `No/False` stores0. Other values leave prior state.
- `HomeCity=name` copies any string, including empty, without a city lookup.
- LOCATIONS starts an inner loop with slot index0. Read at most ten lines.
  A signed-gated line supplies two comma integers into that slot and increments
  the successful count. Otherwise ENDLOCATIONS ends the inner loop; any other
  line, including `@`, is ignored. Every consumed nonterminating line advances
  the slot, including invalid lines. EOF fails. After ten reads, no terminator
  is required or consumed by this inner loop.

Repeated LOCATIONS restarts at slot0 but accumulates the local successful count.
Holes remain zero/old pairs and are not compacted. Thus the published count is
neither the highest written slot nor a bounded count of distinct populated
slots. On completion, publish that count, then enable CreateUnit. Runtime
iteration beyond the ten logical pairs needs its read boundary; no adjacent
state should be invented as additional locations. A repeated CreateUnit verb
starts the local count at0 but retains all four shared flags.

### MoveUnit

Reset a local rectangle-pair count to 0. Require shared Unit/Owner, count exactly 4,
Destination and Move quantity:

- `Unit=name` and `Owner=civ` write movement's distinct payload.
- MAPRECT consumes eight integers on the next line exactly as ChangeTerrain.
- MOVETO consumes one next line; if it passes the signed gate, read two comma
  integers, store destination and set its flag. An invalid nested line,
  including `@`, leaves prior state and is not an outer directive.
- `NumberToMove=All` stores−2; otherwise a first digit admits integer conversion
  with no range check. A leading sign alone is not admitted. Invalid repeats
  retain the earlier quantity/flag.

Completion enables MoveUnit. As with terrain, repeated MAPRECT accumulates the
local count and a repeated action resets it. The shared Unit/Owner flags do
not populate movement fields from creation fields.

## 7. Trigger matching

Each checker begins at the live collection head, tests exact trigger-kind
equality, and after any actions reads the **current node's live successor**.
Do not snapshot the list, restart at its head after effects, or interpret
combined numeric kinds as multiple triggers. A callback that invalidates the
current node reaches a lifetime boundary, not an implicit iteration repair.

Ordinary checkers return 1 if any event matched, even if its action dispatcher
returned0 because it was already consumed; otherwise0. Negotiation and
NoSchism instead return permission as described below. Checkers do not filter
Consumed before testing conditions or drawing randomness in the original.
Under F01, an otherwise unused RandomTurn sample for an already consumed event
may be omitted; preserve the checker's return, visits, wildcard mutations and
any other specified effects.

| Check | Match and parameter effects |
| --- | --- |
| Turn(captured turn) | Stored turn is−1 or equals the argument. Run every match. |
| TurnInterval(captured turn) | Stored interval 0 always matches; otherwise signed remainder of argument/interval equals0. Preserve F01's division fault domain. |
| RandomTurn(unused argument) | Start roll=1. If denominator>1, draw once from F01's primary sampling law, then compute `R % liveDenominator + 1`. Match if the live denominator equals roll. Denominator1 always matches without a draw;0/negative never matches. |
| ScenarioLoaded | Every event of that kind matches. |
| UnitKilled(type, winner, loser) | Exact stored unit selector equals type; Whom equals loser or−2; Who equals winner or−2, in that test order. Before actions, replace wildcard Who with winner, then wildcard Whom with loser. Bindings persist for future checks. |
| CityTaken(name, new owner, old owner) | Case-insensitive city-name equality first; Who equals new owner or−2; Whom equals old owner or−2. Bind wildcard Who then Whom before actions. The name input can refer to live city storage; do not freeze its contents across callbacks. |

UnitKilled's Who/Whom mean **winner/loser**, regardless of which unit initiated
combat. CityTaken has no reserved AnyCity spelling at runtime. Neither checker
restores wildcard selectors after dispatch. A consumed matching record can
still receive its first wildcard binding.

### ReceivedTechnology

For each record of this kind, choose one branch from its initial Whom value:

- If Whom is−2, scan civilizations 0…7 in ascending order. For each, test live
  Alive membership and [V03 knowledge](V03-research-and-technology.md). On success,
  publish Whom=civilization **before** actions and set matched. Continue the
  scan after actions even though Whom is no longer−2. Later tests observe live
  membership/knowledge; multiple civilizations can run the event on this first
  pass. The last successful civilization remains bound. JustOnce can suppress
  later actions without suppressing this binding.
- Otherwise the original “range” test is `Whom > 0 OR Whom < 8`, which rejects
  no signed value. Test Alive membership using its low-five-bit shift; if set,
  query V03 with the full selector and live technology. Run on success. An
  invalid full selector does not become valid merely because its membership
  shift aliases a real civilization; apply the read/write boundary of its child.

This is a polling condition. Giving a technology does not itself invoke this
checker; the enclosing turn phase calls it. Knowledge can continue to match
on later turns unless one-shot state suppresses the actions.

### Negotiation

Check list order and stop at the **first matching record**. A side matches when
its stored identity equals the supplied participant or−2, and its stored
control class admits the human/computer classification obtained by testing the
**stored selector** against Human membership. Human admits only human, Computer
only nonhuman, HumanOrComputer either. Thus Anybody normally tests absent
membership bit30 and is classified as computer, even for an actual human.
Who is Talker; Whom is Listener. No wildcard binding occurs here.

If the normal orientation fails, network mode>2 and both actual participants
are human, retry with the actual talker/listener reversed. Continue using the
stored selectors for classification. Local hotseat does not enable this retry.
On the first match, capture whether its permission value is Deny, run actions,
then return 0 for captured Deny or 1 otherwise. Ignore later records and action
return values. With no match return 1. A consumed JustOnce record still denies
negotiation on every match; action failure does not grant permission.

### NoSchism

Run every NoSchism event whose Whom equals the supplied civilization or−2.
Do not bind the wildcard. Return0 if any matched, otherwise1, independently of
Consumed or the action return. This is a veto query with side effects, not an
ordinary “triggered” boolean.

## 8. Action dispatch and reentrancy

If both JustOnce and Consumed are set at entry, return 0 without actions.
Otherwise, check each action's **live enabled flag**, invoking it in this order:

1. PlayWaveFile.
2. PlayCDTrack.
3. CreateUnit.
4. MoveUnit.
5. ChangeTerrain.
6. MakeAggression.
7. ChangeMoney.
8. DestroyACivilization.
9. GiveTechnology.
10. Text.
11. DontPlayWonders.

Ignore each child's ordinary return value; a failed/no-op action does not skip
later enabled actions. Child faults/stops still stop execution. After all
children return, test the live JustOnce flag and, if set, publish Consumed.
Return1. Empty action sets also return 1.

Consumed is published **after** all effects. An action/callback that re-enters
matching can execute this same event before the outer invocation marks it.
There is no automatic recursion guard or early reservation. Marking before
execution would change behavior. Likewise, capture neither the action set nor
later parameters before earlier children run.

## 9. Gameplay actions

For actions with civilization indirection, resolve once:−3 reads event Who,
−4 reads event Whom, otherwise keep the stored selector. Do not recursively
resolve the result or give Anybody a new meaning. There is no fourth selector
kind hidden in the original's repeated−4 comparison.

### ChangeMoney, MakeAggression, destruction and technology

ChangeMoney requires resolved recipient1…7 and Alive membership. Compute the
signed32 wrapped sum of live treasury and amount. If the sum is outside0…30000,
store0 when the live amount is negative, otherwise30000; else store the sum.
This chooses the saturation side from the amount's sign, including after wrap.
Return1 on the accepted branch,0 otherwise; no extra treasury redraw occurs.

MakeAggression resolves Who, then Whom; validate **Whom first**, then Who, each
in1…7 and alive. Call [V04's force-war operation](V04-diplomacy-and-treaties.md)
with Who/Whom and return its result. No separate distinct-civilization test is
added here; the child owns its own gates and effects.

DestroyACivilization requires recipient1…7 and alive. If it is a human and
equals the live local human, publish game-over reason 4 before calling
[V01 elimination](V01-civilization-lifecycle-and-civil-war.md) with `(recipient,1,1)`.
Return1 after the child; rejected recipient returns0.

GiveTechnology resolves the recipient and captures the technology selector,
then requires recipient1…7 and alive. Invoke
[V03's grant](V03-research-and-technology.md) with recipient, captured technology,
source0, quiet0 and network0. Return1 after the child; otherwise0. This parent
does not validate technology, avoid duplicates or perform an immediate
ReceivedTechnology check. Child semantics and unsafe-write exclusions apply.

### CreateUnit

Resolve owner and test Alive membership using its low-five-bit shift; there is
no explicit1…7 gate. Civilization0 can qualify. Iterate location indices from 0
while less than the **live** count, in order. For each pair:

1. Read X/Y and require [W01 bounds](W01-map-geometry-and-spatial-state.md).
2. Read the live requested unit type's domain. For sea units, accept an ocean
   tile immediately, with **no occupancy test**. Otherwise require a city,
   query that city again for Coastal, and require tile city owner=resolved owner.
3. For non-sea units, require non-ocean or a fresh type/domain query saying Air.
   Require no city or tile city owner=owner. Query tile unit owner; require
   Absent or a repeated query equal to owner.
4. Stop at the first accepted location. If none qualifies, return 0; do not
   fall back to the last rejected pair or nearest legal location.

Use [C01](C01-city-lifecycle-and-ownership.md) city/tile-owner and
[U01](U01-unit-lifecycle-and-stacks.md) unit-owner queries in their stated
order. Invoke U01 creation with the live unit selector, resolved owner and
chosen coordinates. Failure−1 returns0. Otherwise set Veteran if its live
event value is nonzero, then set home to Absent. If a home name is present,
scan live city slots in ascending order; on the first live case-insensitive
name match, set the new unit's byte home reference only if that city's owner
equals the resolved owner, then break **even for a foreign city**. Return1.
No additional occupancy, supported-unit eligibility or home-distance policy is
inserted. Creation may have normal U01 visibility/contact consequences.

### MoveUnit

Resolve owner. Require Alive membership and absence from Human membership,
using the selector's low-five-bit shift; otherwise return 0. Capture the four
rectangle pairs and the initial unit-slot high-water count. Scan slots in
descending order from count−1, reading each slot live:

1. Require a live unit. The event's movement unit selector must equal the
   **unit slot number**, or be AnyUnit. It is not compared to the unit's type.
2. Require matching owner, signed attack≤98, orders other than AIIdle,
   signed orders≤3 and orders other than Sleep. Negative accepted orders can
   pass these tests. The attack lookup uses the unit's live type.
3. Capture unit X/Y; require W01 bounds and all eight comparisons:
   `x>=x0`, `x<=x1`, `x<=x2`, `x>=x3`,
   `y>=y0`, `y>=y1`, `y<=y2`, `y<=y3`.
   This is not a polygon containment algorithm or wrapping rectangle.
4. Set orders=Goto, AI task=55, then destination X/Y to the live event values
   narrowed to signed16. Increment the moved count.
5. Continue if moved is less than the live requested quantity **or** quantity
   is All; otherwise stop. Quantity0 or another negative value therefore still
   allows the first qualifying unit. All scans every eligible slot.

Return the assignment count. No unit moves immediately; [U04](U04-unit-orders-and-activation.md)
and its movement children later execute these orders. Preserve this classic
slot-selection quirk; a type-based selector is an optional repair.

### ChangeTerrain

Capture all four rectangle pairs, but use only X from corner0 through corner1
and Y from corner0 through corner3, inclusive. Iterate X outside Y, retaining
signed32 increment/wrap. Other captured coordinates do not constrain this
action. A reversed bound can produce an empty pass; a maximal bound can make
wrapping iteration nonterminating. Do not add a polygon or implicit clamp.

Perform these ordered phases:

1. First complete rectangle pass: at every W01-valid coordinate, query city;
   if present call C01 deletion `(city,0)`. Then query the tile's first unit;
   while present call U01 deletion `(unit,1)` and **after deletion returns** ask
   for that unit's next-in-stack. Do not cache the next link before deletion
   or replace this with an atomic whole-stack erase.
2. Second complete rectangle pass: for each valid coordinate, replace its
   terrain/river encoding with the low byte of the live event terrain value.
   The low terrain nibble becomes terrain; river, resource suppression and
   retained terrain flags follow that value; do not preserve the old river or mask the input to a valid terrain
   ordinal. With fresh tile queries, clear improvements, radius ownership and
   landmass. Other tile fields remain. Update remembered tile features for
   civilizations 0…7 using [W03](W03-visibility-and-exploration.md), redraw the
   tile in all views, then increment the changed count.
3. Scan all live cities in ascending order against the live city count and
   refresh their surrounding area through C01.
4. For each civilization0…7, scan all cities to determine whether it owns any
   live city; do not stop the scan at the first match. If it has none, capture
   the live unit count and delete all its live units in descending order.
   If it is the local human and a human member, publish game-over reason 4.
   For every X then Y within live map dimensions, clear that civilization's
   visibility using W01's tile accessor, including its deliberate dummy-tile
   behavior for invalid parity. Call V01 civilization creation/reset for that
   civilization, then redraw all views for the live local human with mode 1.

Return the changed-tile count. The cityless-civilization phase has no Alive gate
and runs even for a completely empty rectangle or civilizations elsewhere on
the map. It invokes creation/reset, not normal elimination. There is no explicit
continent relabel, terrain-range repair or transaction around these phases.

## 10. Presentation actions

PlayWaveFile returns0 if Sound is disabled. Otherwise, if a filename is present,
form scenario directory + `\SOUND\` + filename and probe it; play a found file
with `(0,0,0)`. If absent, form the live home directory + the same suffix + the
**live filename**, probe again and play if found. Return1 even with no filename
or no found file. Music is not the gate. Long-path overwrites are excluded;
preserve well-defined path/probe order without reproducing their corruption.

PlayCDTrack makes up to ten track-count queries. Accept counts 10,12,18 or24;
after each unaccepted answer close the CD service, including after the tenth
failure. If no accepted count is obtained return 0. Otherwise read the live
event track; require2≤track≤count, call forced-track playback `(track,1)` and
return 1. Invalid track returns0. There is no parent Music-option gate.

Text initializes its dialog with default zero inputs, then scans all 20 slots
in ascending order. Add every present string as a line, including empty strings
and strings beyond a hole. In network mode>2, after each add-line callback,
reserve the next contiguous network text argument by incrementing the count,
reload that slot's live string, then copy it to the reserved argument. These dialog/copy effects
occur even during autoplay. If live autoplay is inactive, send the text-args
notification in network mode>2, then show the local modal dialog. Ignore the
modal result. Return1. The source's unsafe overflow of the output-string service
is excluded; see Appendix A for its original boundary and safe handling.

DontPlayWonders sets the shared suppress-wonder-presentation request to 1 and
returns1. It does not directly erase wonders or change their gameplay effects.
S01 owns the setup caller that consumes the request.

## 11. Caller hooks and persistence

| Caller boundary | Inputs, timing and continuation |
| --- | --- |
| F04 single-player turn; N03 network/local variants | When events are loaded and resume civilization is0 or resume-new-game is set, run four full passes: Turn, TurnInterval, RandomTurn, ReceivedTechnology. Each turn-taking call receives the live turn at that call. Earlier actions can affect later passes. Network callers additionally synchronize afterward under their own contracts. |
| F06 post-load wrapper | Parse replacement EVENTS when its documented conditions select that path. Only successful parse leads to compaction and loaded-indicator publication. Its post-load ScenarioLoaded call additionally depends on Music enabled. A direct checker call has no Music gate. Saved ordinary continuation can preserve existing event state rather than reparse. |
| U06 combat casualty | After loss accounting, casualty notification and kill-count increment, when events are loaded call UnitKilled with live victim type, opposing unit owner as winner and victim owner as loser. Then delete the victim. Effects can mutate those records before that deletion. |
| C09 capture | After radius transfer, city-owner publication and reveal to the former owner, when events are loaded call CityTaken with the live city-name reference, new owner and old owner. Later capture announcements/consequences retain the caller's order. |
| V05 parley city cession, including N02 transaction receipt | After building removals, civilization city-count changes, radius/visibility transfer, city-owner publication and reveal to the old owner, invoke CityTaken if loaded. Then set center-tile owner and continue cession's supported-unit/production/wonder work. This is distinct from capture aftermath. |
| V01 civil war | Query NoSchism before searching for a rebel civilization. This caller does not add a loaded-events gate. A veto returns from civil war without its later setup. |
| V04 contact | After barbarian/turn-zero rejection, when events are loaded query Negotiation with the contact's civilization pair; denial returns before later contact behavior. |
| V05 AI audience | Publish meeting-active first, then when loaded query Negotiation with the audience's opponent/local orientation. Denial clears meeting-active and returns before stance/audience work. |
| Foreign-minister contact UI, P07 | Its own preliminary report/attitude work precedes the loaded-events negotiation gate. A permitted result continues to the appropriate human/network or AI meeting. UI contact is not a side-effect-free availability query. |

An ordinary grant, rename, turn increment or ownership store is not permission
to insert another event check. Preserve these explicit hook boundaries. Effects
that reach a hook through their normal gameplay children can cause nested checks.

F06 persists the ordered event payload and relevant flags/selectors. A consumed
event stays consumed after an ordinary save/reload; wildcard bindings stay bound.
Reparsing a fresh event file creates new zeroed records and fresh flags only to
the extent that the caller reset/replaced the collection. Parsing itself appends.
S03 backup/cancel/edit operations retain §2 copy/compaction semantics. Its
[text writer](S03-world-and-scenario-editors.md#12-event-text-output-and-replacement)
is a different boundary: fresh parsing of exported text can lose Consumed,
change Negotiation permission and restore saved wildcard names. Do not substitute
text round-tripping for binary persistence or event-editor backup copying.

## 12. Conformance cases and remaining joins

Use whole-state and semantic child/decision traces with required dependencies,
including partial failures:

- Append to an existing list; empty IF; outer EOF success versus helper EOF
  failure; ENDEVENTS without a closing read; error-notice directory mutation.
- Repeat each action/trigger, share CreateUnit/MoveUnit flags in both orders,
  retain trailing Text lines, use a first `@` Text line, ten location reads with
  holes, repeated locations/rectangles and accepted numeric prefixes/wrap.
- Change a name during allocation to distinguish cached from repeated lookup;
  supply or stop at stale debug input. Keep invalid writes excluded.
- Multiple matching records, live successor replacement, consumed matches,
  post-action one-shot publication and bounded nested dispatch. Compare all
  action calls even when an earlier child returns0.
- Sticky UnitKilled/CityTaken identities; ReceivedTechnology's first wildcard
  scan and later fixed scan; consumed-event return behavior with unused samples
  omitted; zero interval.
- Anybody with Human/Computer negotiation, reversed network-human matching,
  first-match permission capture and repeated one-shot vetoes; all NoSchism
  matches and absence of its loaded gate.
- Money wrap and sign-directed saturation; creation sea occupancy exception,
  first suitable location, failed creation and first foreign home-name match;
  slot-vs-type movement selection, signed eligibility, zero quantity and narrowed
  destinations; terrain's two passes, post-delete next read and empty-rectangle
  cityless reset phase.
- Wave probe fallback/live filename, CD retry/close order, autoplay Text copies,
  sparse Text versus copy behavior, and safe network-text/path capacity handling.
- F06 save/reload and replacement parse, F04 consecutive turns, U06 casualties,
  C09 capture, V05/N02 cession, V01 civil war, V04/V05/P07 contact and S03 edit/cancel joins.

Finite source checks and their adapters are recorded in Appendix B. Full native
file/allocator/dialog/media/network execution, arbitrary callbacks, actual
gameplay children and original-program state/RNG traces remain integration
work for X02. These checks do not establish complete original-program conformance.

## Appendix A. Original encodings, differences and excluded writes

The original event record is452 bytes with successor/predecessor at444/448;
copy's payload prefix is444 bytes. F06 owns its full serialized layout and
pointer conversion. Trigger values are UnitKilled1, CityTaken2, Turn4,
TurnInterval8, Negotiation16, ScenarioLoaded32, RandomTurn64, NoSchism128,
ReceivedTechnology256. These are exact tags at runtime, despite their powers
of two. Negotiation's Deny value is4096, compared by equality.

Action flags are Text1, MoveUnit2, CreateUnit4, ChangeMoney8, PlayWaveFile16,
MakeAggression32, JustOnce64, PlayCDTrack128, DontPlayWonders256,
ChangeTerrain512, DestroyACivilization1024, GiveTechnology2048 and Consumed8192.
Talker/listener classes are Human1, Computer2, Either4. The parser's outer phases
are1/2/3/5/10; an apparent phase 6 gate exists but no reached assignment selects
it. Text uses its own inner reader, not phase 6 outer dispatch.

Compaction requests50,000-byte storage twice for the temporary (construction
then reset) and once for the destination. Name-copy minimum allocations are 15
for unit names,24 for civilization/city/home names,1 for text/wave strings.
Parser string allocations use length+1 narrowed to the allocator's length
domain; ordinary reader lines fit it. Pool failure order matters, but a new
implementation need not use native addresses or the same heap design.

Goto orders encode11, AIIdle16, Sleep3; event movement assigns AI task55.
Creation's internal no-location coordinate is10000/10000. Its exhausted loop
restores that sentinel, rather than returning the last failed coordinates.
Terrain stores its low byte without a terrain-nibble mask; the river bit is128.
Text's network notification is opcode79, broadcast recipient255, with the
contiguous present-string count. P04/N02 own its transport/display format.

Relevant reconstruction differences and defect boundaries:

- Primary B23 is a selectable type-based MoveUnit repair; classic compares the
  slot. Independent BF-015 also uses the repaired type comparison. Preserve the
  original comparison in §9, supported by instruction004fb3d9.
- BF-030 initializes parser state/rejects missing IF. Original missing-input
  reads can follow earlier work, as §4 explains. Do not reproduce a resulting
  arbitrary-pointer write.
- BF-235 corrects stale debug arguments; actual Receiver/Attacker selectors
  already use the fresh second resolver result in the original.
- The original wave path local has 240 bytes. BF-081 enlarges/bounds it; the
  primary's larger bounded append and independent larger buffer are repairs.
  Beyond its safe input extent, the original overwrite and its collateral
  effects are excluded, even if the path would otherwise name a real file.
- Original multiplayer Text writes successive260-byte argument slots while
  accepting 20 lines. The original general temporary-string extent has 11 slots;
  later slots cross into other state, including setup/map-size backing. Some
  reconstructions provide contiguous backing to observe these writes. This is
  **excluded corruption**, not required publication of setup/map dimensions.
  A safe port can reject an over-capacity network text before its unsafe copy,
  preserving previous lines/effects, or provide a separately declared expanded
  text/message service. Do not claim the adjacent-state changes as compatibility.
- Invalid unit/city/technology selectors or excessive loaded counts may reach
  reads outside a table. Establish their input/read boundary separately. Any
  eventual write outside the intended unit/city/technology object remains an
  excluded bug, regardless of available contiguous mapped backing.
- A failed event/string allocation can be followed by a null copy if the error
  service returns. That invalid write is excluded; retain earlier valid effects.
  The port's compaction block-reuse helper is an address/lifetime repair, not
  proof that the original guaranteed stable event pointers.

## Appendix B. Evidence and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Primary evidence is OpenCiv2 at
`a71e8aefffe04e6f719e0e213b027b128a747711`, with selected bodies unchanged at
OpenCiv2 comparison revision `fc2155ccdb2123c1137babab6fa39e38152278c9`:

| Behavior | Source and original entry |
| --- | --- |
| Event copy/compaction | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp), `events_alloc_string`, `004ce903`; `events_copy`, `004ce98e`; `events_compact`, `004cef35`. |
| Names and lifetime | Same source, `events_parse_civ`/`events_parse_unit_type`, `004fa250/004fa359`; pool/constructor/destructor/reset/allocate, `004fa47e/004fa4be/004fa569/004fa5d9/004fa617`. |
| Actions | Wave `004fa707`, suppression `004fa82d`, CD `004fa854`, money `004fa944`, Text `004faab0`, aggression `004faba6`, destruction `004fad02`, grant `004fadfb`, creation `004faed4`, movement `004fb29f`, terrain `004fb5b2`. |
| Matching and dispatch | Turn `004fba0c`, interval `004fba9c`, random `004fbb2f`, received technology `004fbbdd`, loaded `004fbd2b`, killed `004fbd9d`, negotiation `004fbe84`, schism `004fc20d`, city `004fc2bb`, actions `004fc3ae`. |
| Parser | `ScenarioEvents::parse_file`, `004fc516`, and its extracted trigger/action helpers in the same source; original is one parser body. |
| Reader/integers | [text.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/text.cpp), F03-owned line `004a23fc`, token `004a24b1`, integer `004a2534`, prefixed conversion `00564bf0`. |
| Layout and literals | [civ2types.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h), ScenarioEvent; [gamedata.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/gamedata.cpp), event keywords/diagnostics. |
| Hooks | F04/F06, U06/C09, V01/V04/V05 and S01 trace the caller entries; parley cession and report paths are in [network.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/network.cpp) and [reports.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/reports.cpp). |

Independent read-only evidence is `civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`: `src/scenario_event_parser.cpp`,
`src/scenario_turn_events.cpp`, `src/unit_exploration.cpp`, event-name/writer
sources, `analysis/scenario/event-parser/README.md` and its referenced original
instruction evidence, and `analysis/reference/bugfix-log.md`. The accepted
reconstruction campaign is not resumed. Its repaired branches and mapped
overwrite observations do not override this document's classic/excluded-bug
profile. Original executable SHA-256 is
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.

The historical Linux fixture compiled **50 complete bodies**:
45 event bodies (all parser helpers/parent, resolvers, allocator, all checkers
and actions/dispatcher) plus five actual F03 token/integer bodies. **52 assertions
pass** in optimized and AddressSanitizer/UndefinedBehaviorSanitizer builds with
wrapping arithmetic and disabled strict-alias optimization. Leak detection is
disabled under sandbox tracing. Twenty-four complete record definitions and 222
literal declarations retain their source fields, sizes and initializers; host
pointer carriers/record allocation sizes are widened for the fixture.

Explicit providers supply section/line reads, allocation, names, map queries,
unit/city/civilization effects, knowledge/grants, media, dialogs and network
messages. These are adapters, not complete gameplay/runtime implementations.
The line provider supplies already bounded logical lines; actual F03 token and
prefixed/hex/binary conversions run. Decimal conversion supplies defined wrap.
The primary generator is a prescribed draw queue, not a second PRNG validation.
The fixture selects classic MoveUnit/debug branches and the classic treasury
limit, and restores original grant capture and Text argument-count order.
It does not exercise missing-IF/stale-debug private inputs, copying/compaction,
long physical lines, pool exhaustion accounting, unsafe text/path writes,
arbitrary aliases, native callbacks or unwind.

Initial fixture corrections fixed the section-open success convention and
preserved zero-padded keyword-array extents caught by the sanitizer. Bounded
original instructions confirm slot-based movement and Text's pre-copy count
increment. No fresh original gameplay, full native parser/action join or saved
annotation cycle is claimed. Source-pin bodies/records/literals, executable hash,
clean evidence repositories, links/anchors, specification IDs and whitespace are
checked. Remaining integration is the real F03/F06 loader, U01/C01/V01/V03/V04
children, F04 turns, U06/C09/V05 hooks, S03 editor/copy lifetimes and P04/P10/N02
services. X02 must retain independent state, draw and ordered callback traces.

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

### Original §2. Collection, copying and lifetime

Reset clears the collection head and count, then replaces its string/record
storage with the requested capacity. Reset itself does not set the separate
loaded-events indicator. Construction initializes the owning object and pool;
destruction releases them. Native dialog/allocator layout is a service detail.

Allocate an event by finding the current last node, requesting one zeroed
record, appending it and incrementing the count. Set its predecessor to the
captured last node and its successor to Absent. Failed allocation appends
nothing and leaves the count unchanged. Initial numeric fields, selectors and
flags are zero, not the named wildcard/Absent values; string references are
Absent. Parsing appends to an existing collection: it does not reset first.

The copy operation is nominally used with an empty destination:

1. Traverse source nodes in order. For each, allocate a destination node and
   copy its complete payload except the destination's newly established links.
   At this stage the string references still refer to source strings.
2. Traverse source and destination from their respective **heads**, in lockstep.
   Duplicate each present string in this order: trigger unit, city, Who, Whom;
   text strings from 0 up to the first absent string or 20; move owner, move unit;
   aggression Whom, aggression Who; creation owner, creation unit, home city;
   ancillary civilization name, ancillary unit name; money recipient; wave file.
   Publish each new reference before continuing. Copy only through its terminator.
3. Stop when the source ends. Preserve action flags, Consumed, selectors,
   coordinates and all other payload exactly.

Strings reserve at least the name-class capacity in Appendix A or their actual
length plus terminator, whichever is larger. Allocation failure invokes the
fatal allocation-error service. If it returns, the original can attempt an
invalid copy; that write is excluded. Earlier appended nodes and copied strings
are not rolled back.

Accepted unusual inputs matter here: with a nonempty destination, the second
pass still starts at its old head, so strings are rewritten there while new
payloads were appended at the tail. A hole in the text array terminates text
duplication even though the Text action later scans all twenty slots. Later
present strings can therefore retain source references. Self-copy, dangling
references and callback-invalidated nodes require their actual lifetime/input
boundary; do not silently substitute a safe deep copy and call it original.

Compaction constructs a temporary collection, resets it, copies the global
collection into it, resets the global collection, copies back, then destroys
the temporary collection. Nominal ordered payload and strings survive, including
Consumed and previously bound wildcard selectors. There is no semantic reset
of one-shot events. Original allocation addresses and the port's address-reuse
repair are not a new requirement on a cleanroom representation. S03 owns editor
callers retaining references across this operation; F06 owns persisted relocation.
