# V08 — Scoring, victory and retirement

Target: classic Civilization II Multiplayer Gold Edition
**5.4.0f, patch 3**. This specifies score components and scenario objectives,
civilization rating/title, Hall of Fame qualification/insertion, scheduled and
manual retirement, pending-outcome dispatch and continuation. F04 owns when these
operations run; the state decisions of the operations themselves belong here.

[F04](F04-turn-sequencing.md) owns turn/calendar progression and arrival ordering.
[V01](V01-civilization-lifecycle-and-civil-war.md) owns civilization loss/restart
and the mutations producing conquest or defeat. [V07](V07-spaceship.md) owns
ship calculation and arrival predicates. [V02](V02-governments-and-tax-rates.md#population-queries)
owns population conversion; [C03](C03-city-economy-and-corruption.md)/[C04](C04-city-happiness-and-disorder.md)
own city recalculation and moods. [F06](F06-save-load-and-file-formats.md)
owns serialized Hall of Fame bytes. [P07](P07-advisors-reports-and-history.md#15-civilization-score) owns report layouts/input and P10
movie/media execution; N01–N05 own session, message and authority services.
Their callbacks have real effects and cannot be universally replaced by no-ops.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Implementation boundary

The main sections use logical values, identities and operations. Original memory,
allocation and platform mechanics are recorded in [Appendix C](#appendix-c-original-implementation-reference). Preserve required
state changes and externally visible results without reproducing those mechanisms.
Exact numeric domains and file/wire encodings remain required wherever specified.

## 1. Inputs, logical state and compatibility domains

Game state supplies turn/year, difficulty, alive/human/start sets, enemy count,
barbarian activity, pollution/warming counts, peaceful-turn count, rule/map flags,
Won, Scored, Cheated and pending outcome. Scenario state supplies an objective
mode, whether wonders count, objective civilization, four victory thresholds,
turn limit and calendar settings. Civilization/city/wonder state supplies the
components below. Rating and score results occupy shared scratch, not permanent
per-civilization records; direct consumers can observe a previously calculated
result if their caller skipped recalculation.

Nominal inputs follow [F02](F02-game-state-and-invariants.md) and F03/F06 domains:
backed indices, retained tokens, six stock difficulties and accepted rules,
scenario thresholds and text. The algorithms do not universally enforce ordered
thresholds, positive counters, active wonder locations or current scratch ownership.
A stricter implementation may declare narrower accepted inputs.

Use [F01](F01-determinism-and-arithmetic.md): reference expression results,
truncating signed division and original clamp order. Score arithmetic and rating
have no general saturation. Stored turns/years and Hall of Fame numeric fields
retain their specified signed 16-bit values. Preserve in-bounds wrap, including a
positive score becoming a negative stored Hall of Fame score. Exclude invalid
writes and resulting memory corruption under [X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs).
Safe text storage or a declared rejection/truncation policy replaces an overflow;
unknown reads need backing or a bounded stop, not invented initialization.

Unless captured explicitly, read fields at each operation after prior children.
Human membership, local human, autoplay and game mode are separate conditions.
Scored and Won are separate retained flags; one does not imply the other.

## 2. City objectives and score components

### Shared city objective value

`objectiveValue(city)` starts at 1 when the city has Objective, else 0. If the scenario
WondersCount flag is set, scan all 28 raw wonder locations and add 1 for each equal
to this city index. This helper does not check Scenario, objective-scoring mode,
city token, ownership, wonder activity or obsolescence. Its callers choose their
admitted cities. S01–S03 and AI consumers reuse this query.

### Score calculation

`calculateScore(C)` writes the following shared results in order. It does not
itself recalculate city economy/happiness or select an outcome.

1. Set citizens=0. Scan live city slots ascending to the live bound; require
   nonzero token and owner C. Add signed city size + signed happy count − signed
   unhappy count. Angry/specialist counts are not separate terms in this sum.
2. Set wonders=0. Scan 28 raw locations; when location>=0 and that backed city's
   owner is C, add 20. There is no city-token/upper-live-bound check, active-wonder
   query or obsolescence gate beyond the location/index access.
3. Zero spaceshipInFlight and spaceshipArrived. Call V07 calculate(C,1), retaining
   its preview/Fusion/scratch effects. When current success!=0 and saved ship
   population!=0, set inFlight=population*success. If the ship's Won flag is set,
   move that value to arrived and zero inFlight. Do not add a Launched/Exists
   check. Only arrived contributes to the ordinary total below.
4. Set pollution=`(pollutedTiles-warmingPollution)*(-10)`. A negative difference
   adds points; do not clamp it first.
5. Set peace=0; when signed turn>199, set peace=clampAB(peaceTurns*3,0,100).
6. Set futureTechs=unsigned future-tech count*5; barbarianBonus=unsigned activity*25−50.
7. Set total to peace+pollution+arrived+wonders+citizens+barbarianBonus+futureTechs,
   retaining each sum. If total<0 replace it with 0.
8. Set conquestBonus=0. When the alive normal-civilization set equals exactly
   C's membership bit, capture endTurn=current turn. If NoRestartEliminated and
   endTurn<251, replace it with 250. Compute
   `conquestBonus=100*numEnemies + 2*(570-endTurn) + 400`.
   If LargeMap multiply by 5 then divide by 4 with truncation. If SmallMap multiply
   that result by 4 then divide by 5. Both flags apply sequentially when both are
   present. No final nonnegative clamp is added.
9. Set scenarioPoints=0. If not both Scenario and objective-scoring mode, return
   max(total,conquestBonus). Do not replace shared total with that maximum; they
   remain separate fields. Other objective scratch can remain from a prior call.
10. Otherwise run the objective-score procedure below and return its result.

Conquest bonus is a score query over current alive state, not the predicate
posting Conquest. It can be calculated without a pending conquest outcome. For
example six configured enemies, NoRestartEliminated and turn 1 use effective
turn 250 and give 1640 before map scaling.

### Objective-score procedure

Zero totalObjectiveValue, objectiveCivValue and civObjectiveValue. Scan live city
slots ascending; skip zero tokens. Call objectiveValue once per admitted city,
add it to the total, then add to objectiveCivValue if the city's current owner
matches the configured objective civilization, and to civObjectiveValue if owner
matches C. Re-read ownership after the child; the two comparisons are independent.
When objective civilization is 0, replace objectiveCivValue with civObjectiveValue.

Initialize objectiveOutcome=Draw. Test the configured objective civilization's
value V against the thresholds in this exact order:

1. V>=decisiveVictory: DecisiveVictory.
2. Otherwise V>=marginalVictory: MarginalVictory.
3. Otherwise V<=decisiveDefeat: DecisiveDefeat.
4. Otherwise V<=marginalDefeat: MarginalDefeat.
5. Otherwise retain Draw.

Do not sort custom thresholds. Preserve the stored objectiveOutcome from that
civilization's perspective. For points only, when configured objective civilization
is nonzero and differs from C, reverse victory/defeat, retaining Draw. Base points
are 1000/750/500/250/0 for DecisiveVictory/MarginalVictory/Draw/MarginalDefeat/
DecisiveDefeat. Add 10*civObjectiveValue. If the previously calculated conquestBonus
is positive, double scenarioPoints. Finally set conquestBonus=-1, total=scenarioPoints
and return scenarioPoints. This final total has no ordinary zero floor applied
again. The global objectiveOutcome can therefore differ from C's point outcome.

## 3. Score report, rating and title

The score report's draw callback captures its report civilization. If Scored is
set, show the already-scored message and skip the normal calculation/display
branch. Otherwise:

- Ordinary scoring first draws the citizen presentation. That child scans owned
  live cities and calls cityRecalc(city,1) before drawing each city's citizens.
  It stops when its row/display budget fills, so it may recalculate only a prefix
  of cities. Then calculateScore scans all owned cities using the stored mood
  fields now present. Do not replace this with an unconditional all-city refresh.
  P07 owns the original logical row/pixel admission calculation. Under
  [X01's presentation policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy),
  preserve this recalculated prefix using declared classic view inputs separately
  from the new renderer's geometry; exact drawing is optional. A larger new report
  must not silently change the score by refreshing more cities.
- Objective scoring calls calculateScore directly from the objective report;
  it does not first run the ordinary citizen-recalculation presentation.
- The ordinary report lists citizen/wonder components, spaceship points, pollution,
  peace when admitted, future technologies and barbarian adjustment. In-flight
  ship points are displayed with their pending qualifier but excluded from total.
  Wonder icons require a nonnegative location below the live city bound, a nonzero
  city token and matching owner. This is stricter than the raw wonder-score query;
  icon clipping does not reduce the numeric score.
- Objective display shows total objectives, configured/local objective counts,
  the four thresholds and the stored configured-civilization outcome. The total
  line uses scenarioPoints and conquestBonus=-1. Other total lines choose conquest
  only when conquestBonus>total; equal values use the ordinary total label.
  Scenario/Cheated annotations do not invalidate the score.

Opening the score report does not independently set Scored. Report callbacks can
recalculate or mutate scratch more than once. Rating and Hall of Fame callers
rely on the resulting shared fields rather than taking a frozen score snapshot.

The rating draw callback captures the report civilization, performs its window
layout/caption/blit work, then computes:

| Difficulty | Chieftain | Warlord | Prince | King | Emperor | Deity |
| --- | --- | --- | --- | --- | --- | --- |
| Factor |4 |5 |6 |8 |10 |13 |

The exact rule is factor=difficulty+4; if difficulty>Prince replace with difficulty+5;
if difficulty>King add 1; if difficulty>Emperor add 2. Take best=max(shared total,
shared conquestBonus), then rating=`s32(factor*best)/100`. Do not call calculateScore
inside rating. Initialize titleIndex=0; scan i=1…24 and whenever `(i*i)/3<=rating`
set titleIndex=i−1. Finally cap titleIndex at 23 and publish titleIndex then rating.
Negative rating keeps title 0; rating 192 reaches title 23. The percentage is not
clamped to 100.

Choose MaleFame/FemaleFame by the report civilization's leader gender. Publish its
leader name, then read/expand resource lines 0…titleIndex in order. If section open
fails, earlier lines use Error and the final line uses the leader-name placeholder.
Draw earlier titles in the subordinate style and the final one emphasized.
P07 owns placement, font metrics and native modal lifetime. Rating display starts
music track 3 after its initial repaint; the shared rating is later retained as a
signed 16-bit Hall of Fame field.

## 4. Hall of Fame records, insertion and display

### Record preparation

The Hall of Fame has six physical entries. The first five are the retained visible
ranking; the sixth also holds the current low-ranking result when highlighted.
Preparing a new record does not recalculate score or rating:

1. Store max(shared total,conquestBonus) with signed 16-bit retention. There is no
   classic positive-score cap or unsigned-display reinterpretation.
2. Store difficulty, adding the Cheated marker when set; current turn/year;
   V02 totalPopulation(C); objective count for Scenario+objective mode, else -1.
3. Store scenario year increment and start year, the Scenario flag, retained
   rating, current leader gender and titleIndex, in that order.
4. Look up/copy the leader name, then separately look up/copy civilization plural
   name. Nominal names fit their intended fields. The original unbounded copies
   are not an instruction to preserve name-field or adjacent-window overwrites;
   Appendix A gives the safe boundary and repair distinctions.

Scenarios and cheated games are still admitted; their markers affect display.
Signed score/rating wrap is retained. A score 40000 becomes -25536 and is hidden by
the classic row display even if its rating inserted it near the top.

### Storage and sparse clearing

[F06's Hall of Fame format](F06-save-load-and-file-formats.md#hallfamedat) owns byte
layout. `clearHall()` only sets score=-1 and rating=-1 in each row 0…5. It leaves
names, turn, metadata and other bytes unchanged.

`loadHall()` calls clearHall, opens HALLFAME.DAT for binary reading, then reads six
records sequentially. If open fails, return with the sparse reset. A short/failed
record read sparsely resets all six rows again; bytes already read, including
partial record data, otherwise remain. Close any opened file. Classic loading
performs no semantic rank/difficulty/name validation; appended bytes are ignored.
The modern BF-198 guards are separate admission repairs, described in Appendix A.

`saveHall()` opens for binary writing, which truncates/replaces the existing file
on successful open. Write rows 0…5, stopping on the first failed whole-record write,
then close. Failed open has no further effect. There is no atomic rename, retry or
rollback of a short write in the original algorithm. An implementation adopting
a stronger persistence policy should declare that difference.

### Insertion and clear/retry loop

Prepare the current record, loadHall, then force row 5's score/rating to -1. Initialize
newRank=-1. Repeat:

1. Scan row 0…5 for the first **stored rating strictly less than** the current
   record's retained rating. Do not skip rows by score or treat ties as better.
2. If found, shift complete records descending from row 5 to the slot after the
   insertion point, copy the current record into that point, and set newRank.
   If none, preserve its previous value, including across retries.
3. saveHall; showHall(newRank). Return when its result is 0. Any nonzero result
   calls clearHall and repeats insertion using the same prepared current record
   and retained newRank. It does not prepare or reload again at the loop head.

`showHall(highlight)` itself calls loadHall before opening, so a failed save or
intervening file change can affect what is displayed. Initialize the shared modal
result 0. A negative highlight offers only Close; nonnegative offers Close and
Clear. Clear sets result 1 then ends the modal; it does not clear the file directly.
The insertion parent handles the returned result. Show returns the live result
after modal cleanup. P07 owns the report's window/layout/event behavior.

### Record interpretation for display

Scan row 0…5; omit row 5 unless highlight==5, and omit every row with signed score<0.
Use stored gender/title to expand the fame text, the row number, civilization name,
completion date, population or objective count, score, difficulty and scenario/
cheat markers, then rating. The rank line loop uses the live stored title index
on each condition; it can observe callback changes. Ties preserve existing order.

For ordinary/year calendars, display absolute stored year with BC for negative;
AD ordering follows the language setting. For a scenario with negative stored year
increment, compute `m=s32(turn*abs(yearIncrement)+startYear-1)`. Use month index
`abs(m%12)` and year `abs(m/12)`, with truncating signed division/remainder. Do not
substitute F04's current scenario settings: these are the saved entry's settings.
Population is shown unless the entry is a scenario and objective count>=0.
Difficulty display uses its low four bits; the Cheated marker is separate.

A negative title on an admitted custom file can skip all title-line writers. A
first such row needs incoming original text backing or a stop; a later row can
reuse the prior produced title. This is a read/state issue, not a reason to invent
an empty title. Invalid difficulty/name reads require their own backing/stop or a
declared ingress restriction. Invalid writes during expansion remain excluded.

## 5. Outcome producers and scheduled retirement

Pending reasons are None, LocalSpace, OtherSpace, Conquest, Defeat and Retirement.
[F04's arrival transition](F04-turn-sequencing.md#spaceship-arrival-boundary)
selects at most one arriving ship per invocation, marks winner only before Won,
and posts the appropriate local/other space reason. V01's rejected-restart path
can post Conquest when fewer than two normal civilizations remain, and its human
elimination path posts/sends Defeat. Their admission, side effects and network
arguments stay with those owners. Do not add an all-purpose victory scan after
every city or unit mutation.

`checkRetirement()` returns 0 immediately if Won or a pending reason is already
set. Otherwise publish local leader title/name. If Scenario and a nonzero turn
limit are active, test turn==limit−5 for the warning, else turn==limit for the end.
Otherwise test year==2000 for the warning, else year==2020 for the end.
These are equality tests; jumping over a year skips the notice. No one-shot warning
flag or autoplay suppression is added.

A warning broadcasts its text in modes>2, shows the warning locally, then returns 0.
An end broadcasts its corresponding text, shows the end notice, sets Won, sets
pending Retirement, then in modes>2 broadcasts EndSequence with current flags and
that reason; return 1 after the send. Re-read mode for each gate. Scenario and
ordinary messages are distinct. Extended retirement-year/reached-year options in
the primary reconstruction are outside the classic target.

## 6. Pending outcome handling and continuation

`handleGameOver()` starts quit=0. Dispatch preliminary presentation from the
current pending reason:

- Either space reason: if winner==0 or winner==local human, request the local
  arrival presentation; otherwise request arrival with the negated winner index.
  The sign selects the competing-civilization presentation; it is not a negative
  index for the ship-state lookup.
- Conquest: show destroyed-civilization report for local human.
- Defeat: show defeat movie for local human.
- Retirement/None/unknown: no preliminary presentation in this switch.

Then show power graph. If current Scored is clear, show local score report,
then local rating report, then add the local Hall of Fame entry. Re-read local
human for each child. The reports are the score/rating producers; no separate
cached calculateScore result is injected here.

After those callbacks, read the **live** mode and pending reason. In mode 0:
if reason is either space outcome, Conquest, Defeat, or Retirement with current
year exactly 2020, enter the continuation branch. Defeat sets quit=1 without asking;
other admitted reasons ask KeepPlaying and set quit=(reply==0). Mark Scored after
that choice. For other reasons, retain quit 0 and do not set Scored here. In every
nonzero mode set quit 1 without the KeepPlaying/scored branch.

Finally clear pending reason unconditionally on normal return and return quit.
This child does not clear Won, reset the calendar or rebuild the world. A scenario
ending outside 2020 can show scores and return Continue without setting Scored;
a later invocation can therefore score again. Direct calls with None likewise
still execute the graph/scoring tail. Presentation callbacks can change later
reason/mode/Scored decisions; preserve their order.

Nonnegative arrival requests playback stop and the local Centauri presentation.
Its narrative uses live launch year, population, adjective, arrival year and
leader name. Negative arrival selects the competing-ship presentation and uses
the local leader for its final name substitution. Defeat requests its defeat
presentation. [P10](P10-audio-video-and-animation.md#17-ending-presentations)
owns media/content selection and completion; none of these branches changes the
ship's Won/Arrived flags itself. Preserve the caller's graph/scoring/continuation
order above. A replacement may report or recover from harmless local media
failure under X01 without changing those gameplay effects. Original movie-window
allocation versus media-creation fallback branches are in [Appendix C](#appendix-c-original-implementation-reference).

## 7. Explicit Quit and Retire commands

`quitOrRetire(retire)` immediately returns when mode>2 and host handoff is nonzero.
Otherwise ask ReallyQuit for retire==0 or ReallyRetire for any nonzero retire;
reply 0 returns. A confirmed Retire shows power graph and, if current Scored is
clear, score, rating and Hall of Fame in order. Quit skips those reports. This
command does not itself set Won or Scored, and does not post Retirement reason.
The original still asked confirmation when no game was active; the primary port's
no-game immediate exit/no-op guard is a separate convenience repair.

After confirmation/reports, branch on the live mode:

- Mode 0: stop CD playback, disable game/map display and return.
- Other modes: if mode 1, capture human membership and replace it with the hotseat
  human set. In modes>2 destroy a nonzero multiplayer timer then clear its handle,
  close parley and chat, publish local adjective/title/leader/plural, and broadcast
  Quit or Retire text according to KillCivOnRetire. The notification kind does not
  by itself prove that elimination happened.

For the latter branch, clear blink phase and redraw cursor. If no human bit other
than the current local human remains, clear that local membership in modes<3;
in network modes instead send UnlockNation to host. Then disable display and
stop CD playback, in that order. Otherwise invoke V01 eliminate(local,false,false);
after it, disable display if the live mode is network. The V01 child owns authority,
kill/restart decisions and state changes.

Then clear playerTurnActive and set playerHasQuit=1. If the live mode is 1, copy
current human membership back to the hotseat set and restore the captured human
mask. If a callback entered mode 1 after the initial capture was skipped, the
original restoration reads an uninitialized local: require supplied backing or
stop **after** the hotseat-set publication. Do not invent the saved mask. N04
owns surrounding hotseat handoff; N01/N03 own network departure/host transfer.

## 8. Conformance cases

Compare returned values, shared score/rating scratch, persistent fields and
ordered presentation/file/network calls. These are implementation targets;
the finite executed subset is stated in Appendix B.

| Case | Required result |
| --- | --- |
| Owned size 5 city, happy 2, unhappy 1, one raw located wonder | Citizens 6, wonders 20. The scoring query itself does not recalculate the city. |
| Same wonder location points to an inactive city record still owned by C | Wonder score 20 remains; the icon renderer has a stricter live-city filter. |
| Ship population 2, success 75, without / with ship Won | Pending 150 excluded from total / arrived 150 included. No extra Launched gate. |
| Polluted 5, warming 8 | Pollution contribution+30, not 0. |
| Turn 199/200, peaceTurns 40 | Peace 0 / capped 100. |
| NoRestartEliminated, sole normal civilization, six enemies, turn 1 | Conquest 1640 before map scaling; ordinary shared total remains separate. |
| Both SmallMap and LargeMap flags | Apply large scaling then small scaling with separate truncations. |
| Objective mode with a configured opponent | Store its original outcome, invert only the scoring perspective, add C's objectives. |
| Unordered custom scenario thresholds | Follow the stated branch order without sorting or inventing a rejection. |
| Positive conquest bonus in objective mode | Double scenario points, then replace conquestBonus by -1. |
| Citizen report fills before later owned cities | Recalculate only the displayed prefix; calculateScore still scans all cities using their then-stored moods. |
| Difficulty 0…5, best score 100 | Ratings 4,5,6,8,10,13. At rating 192, title 23. |
| Score report skipped, direct rating/Hall of Fame entry | Consume current shared scratch; do not silently recalculate. |
| Stored Hall of Fame score 40000 | Retain -25536 and hide the row under classic signed display; no B15 repair. |
| Same retained rating as existing row | Insert after equal ratings, if a later lower entry exists. Rank by rating, not score. |
| Lowest current result | Row 5 is forced empty before insertion and appears only when highlighted 5. |
| Hall clear or one-record short file | Only all score/rating fields become -1; successfully read metadata remains. |
| Clear requested from inserted-score window | Clear/reinsert the same prepared current record, retaining prior insertion variable. The display itself reloads disk. |
| Long name crossing its intended 24-byte field | Exclude the overwrite, even before it reaches the end of the 72-byte record. Apply the declared safe text policy. |
| Negative custom first title index | No title producer; require supplied incoming text or stop. Do not default to empty text. |
| Year 2000/2001/2020, no scenario limit | Warning / no notice / retirement. No reached-year substitution. |
| Scenario limit 10 at turns 5/10 | Warning / terminal retirement, with no autoplay suppression in this child. |
| Other-space outcome, winner 2, local 1 | Pass -2 to arrival presentation before graph/scoring. |
| Defeat in mode 0 | Reports precede unconditional quit; no KeepPlaying prompt. |
| Retirement outside year 2020 in mode 0 | Return Continue after reports without setting Scored; clear pending reason. |
| Any outcome in a nonzero mode | Return Quit, without this handler's single-player Scored mutation. |
| Graph callback changes Conquest to Retirement at year 2010 | Preliminary conquest report still occurred; later continuation uses Retirement and skips KeepPlaying. |
| Quit/Retire during network host handoff | Return before confirmation or scoring. |
| Callback enters hotseat after saved-mask capture was skipped | Publish current mask to hotseat storage, then require incoming saved-mask backing or stop before restoration. |

Further joined cases must use actual C03/C04 recalculation, V07 statistics,
V02 population, V01 removal, F04 calendar/session sequencing, scenario settings,
P07/P10 resources and real N02 receipt/authority. File checks must include failure
at each record and partial read/write effects, not only successful round trips.

## Appendix A. Original representations, options and bug evidence

- Pending reasons 0…5 encode None/LocalSpace/OtherSpace/Conquest/Defeat/Retirement.
  Game flags Won/Scored/Cheated/Scenario are `0x2`/`0x20`/`0x10`/`0x80`.
  LargeMap/SmallMap are `0x4`/`0x8`. Scenario objective scoring is mask 2 of its
  first flags field; wonder objectives mask 4. The four stored thresholds are
  decisive victory, marginal victory, marginal defeat, decisive defeat. Outcome
  values 0…4 map victory through defeat; this differs from pending-game reasons.
- Arithmetic scratch is signed 32-bit; city size/happy/unhappy are interpreted as
  signed bytes. Future-technology/activity/enemy counts are unsigned bytes.
  Score/rating and dates narrow on Hall of Fame storage as specified in F06.
  These in-bounds conversions are retained even when they hide an entry.
- OpenCiv2 options X50 retirement years/reached-year logic, X51 expanded population
  and B15 capped/unsigned Hall of Fame scores are outside this classic contract.
  The no-active-game Quit shortcut is also a port behavior. Their presence in
  inspected source does not replace the original branches specified above.
- Each Hall of Fame name field is 24 bytes inside a 72-byte record. Independent
  BF-056 permits a ruler copy to span 48 bytes before the second name overwrites
  part of it, then bounds the final record. Under this collection's X01 policy,
  the write beyond the ruler's intended 24-byte field is already an excluded bug.
  It is not required just because the enclosing record is backed. The primary
  reconstruction instead bounds both names to 23 characters plus NUL. A cleanroom
  implementation may adopt that declared repair or reject oversized names.
- Original rating/fame resource templates use 80-byte locals. Hall title expansion
  uses 132 bytes; later row composition uses shared text storage. Valid custom
  GAME/LABELS strings and substitutions can exceed them. BF-050/BF-064/BF-199
  describe repairs. Safe growing text, explicit bounded truncation or rejection
  can replace writes past the intended buffers; overwritten stack/window state
  is excluded. P07/P10 retain the text/rendering policy and callback order.
- BF-198 is not an original loader check. The independent repair checks each
  complete active record (score>=0) for title 0…23, difficulty 0…5 with optional
  Cheated and a civilization-name terminator; it permits a ruler string to read
  through the next field. The primary repair reads all records then validates
  rows with score!=-1 and requires both name-field terminators. Thus their
  negative-score and failure-prefix domains differ. Neither is a universal
  original semantic-validation rule. Reads spanning supplied fields are separate
  from excluded writes; require backing/termination or an explicit stop.
- The competing-Centauri text path originally appends pages into a 256-byte
  temporary and expands into shared text. BF-062/BF-063 add bounds. Long-page
  overwrites are excluded; their truncated source repairs are not evidence for
  original text limits. P10 owns the detailed page/media service.

## Appendix B. Evidence and validation

Primary pin: OpenCiv2 `a71e8aefffe04e6f719e0e213b027b128a747711`, inspected at
`fc2155ccdb2123c1137babab6fa39e38152278c9`. **26 complete source bodies** were
compared with the pin, covering score/rating/report helpers, objectives,
retirement/outcome/quit, Hall of Fame and selected presentation entry points.
This comparison does not claim that the P07/P10 rendering children are fully
specified here. Independent source revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752` at `civ2` was
consulted for score arithmetic, original file ingress, name/text repairs,
retirement continuation and the uninitialized hotseat restoration path.

Original executable: Civilization II MGE **5.4.0f, patch 3**, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
The hash and clean reference trees were checked. No new original-runtime replay,
native Windows build, Ghidra instruction investigation or annotation change was
performed. Archived runtime results were not re-read or credited as fresh tests.

| Evidence | Scope |
| --- | --- |
| [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp), score `004a28b0` | Shared components, separate conquest return, scenario branch order and perspective. |
| Same file, score draw/show `00434e39`/`00435d15`, rating draw/show `00435dc4`/`004361cc`, private score helpers | Recalculation prefix, scratch consumers, rating factors/title and report state boundaries. |
| [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp), objective `0043cef9` | Objective flag plus raw located wonders. |
| [reports.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/reports.cpp), Hall `004362e2`…`00436f5a` | Display admission, sparse clear, file loop, record preparation, insertion and modal retry. |
| Same file, defeat `004702e0`, arrival `004710d0`, competing arrival `00471dd8` | Presentation sign, allocation/create fallback and declared P10 boundary. |
| [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp), retirement `0048aedc`, outcome `0048b165`, Quit/Retire `004e22c9` | Exact equality triggers, live callback continuation and session mutations. |
| [civ2types.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h); [BUGS.md](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md), B15 | Record encoding and unsigned-score repair distinguished from classic wrap. |
| Independent `src/civ_score.cpp`, `src/high_scores.cpp`, `src/quit.cpp`, retirement/outcome fragments in `src/window_startup.cpp`, bugfix log BF-050/056/198/199 | Numeric/order agreement, differing guards, field-overwrite exclusion and unknown incoming restoration. |

### Checks performed

A standalone source-derived C++ harness compiled **eight complete original-entry
bodies**: objective value, score, retirement check, outcome handler, Hall clear,
load, save and add. It also compiled the primary BF-198 validator and the actual
rating arithmetic/publication prefix from its draw body. Both optimized and
AddressSanitizer/UndefinedBehaviorSanitizer builds passed **40 assertions**, with
leak detection disabled. X50/X51/B15 providers were fixed off. Short names and
nominal complete records exercised unchanged original paths; the validator was
not treated as evidence for original malformed-file handling.

The checks cover components, scenario perspectives/thresholds, conquest scaling,
retirement equality, pending-reason callbacks, score/rating/Hall ordering,
strict insertion ties, signed score retention, sixth-row handling, sparse clear
and short-file effects, difficulty factors and title thresholds. Hall insertion
joins actual load/save against a private temporary file and its 72-byte host record;
original name-copy pointer casts were adapted for the 64-bit host. No excluded
write was replayed. The manual Quit/Retire and graphical/report bodies were
source-reviewed, not executed by this harness.

V07 statistics, V02 population, report/movie/graph windows, text and network
services were explicit adapters. The score/rating report adapters do not establish
native recalculation/layout or score-production integration, and the primary
bounded name-copy helper is a declared safe adapter. Source-pin, Markdown links/
anchors, specification IDs, F06 layout and whitespace checks accompany the work.
Complete F04/V01/V02/V07, city, P07/P10 and multiplayer joins require independent
X02 state/draw/file/notification traces before whole-game conformance is claimed.

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

### Original ending-media allocation and fallback

Nonnegative arrival presentation stops CD playback, allocates/constructs its movie
window, then tries media creation. Creation failure after successful allocation
uses the Centauri text fallback; allocation failure does not. The fallback reads
live launch year, population, adjective, arrival year and leader name. Negative
arrival selects the competing-ship text/media path and uses the local leader
for its final name substitution. Defeat has no parallel text fallback on failed
movie creation. [P10](P10-audio-video-and-animation.md#17-ending-presentations) owns complete resource, playback, cleanup, input and timing
contracts; these branches do not alter the ship's Won/Arrived flags themselves.
