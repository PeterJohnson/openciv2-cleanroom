# P09: Council and throne room

Target: classic Civilization II Multiplayer Gold Edition 5.4.0f, patch 3.

This document specifies the five council opinion selectors, chapter grammar,
presentation sequencing and throne-room qualification, composition and choices.
[F01](F01-determinism-and-arithmetic.md) owns arithmetic/RNG;
[F03](F03-rules-and-configuration.md) owns text/rule ingestion;
[V03](V03-research-and-technology.md), [V04](V04-diplomacy-and-treaties.md),
[C03](C03-city-economy-and-corruption.md), [C08](C08-buildings-and-wonders.md),
[A01](A01-ai-strategy-and-assessment.md) and
[V08](V08-scoring-victory-and-retirement.md) supply knowledge, treaties, city
calculation, buildings, power ranking and score. P09 preserves the ordered calls
and their side effects. [P03](P03-graphics-and-asset-processing.md),
[P04](P04-text-and-dialogs.md) and [P05](P05-input-and-command-dispatch.md) own
shared graphics/text/widget/input services; [P10](P10-audio-video-and-animation.md) owns the media operations invoked
here. The council's era selection uses the advance-variant query owned by
[P06](P06-city-interface.md).

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. Gameplay-dependent ordering and callbacks remain required; purely
cosmetic randomness and native traces follow the presentation policy.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Required contract and reading guide

Council opinion selectors/priorities and their city/trade/query effects,
meaningful advice, supported authored captions/media, throne qualification and
persistent piece/upgrade choices.

The numbered main sections specify required behavior under
[X01](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy).
Original rendering, geometry, widgets, cosmetic media and native traces are in
[Appendix D](#appendix-d-original-presentation-reference); they are optional reference material.
Main-section numbers are retained for stable citations. Required calculations
that happen inside a presentation operation remain in the main text, including
state mutations, visibility/choice rules and file/protocol effects. Native event
traces do not replace semantic operation boundaries. Harmless UI failures and
rejected off-nominal inputs follow X01's declared safe policies.

## 1. State and input contract

Nominal inputs are backed civilizations/cities/units/rules, initialized media
and presentation providers, fitting strings and valid chapter/resource files.
The council actor and the local human observer are distinct inputs. All scans
below preserve their stated order and do not gain an alive/contact filter where
none is specified. Re-read live fields after earlier callbacks unless captured
explicitly. F01 wrapping arithmetic and truncating signed division apply.
Rule bytes, stored counters and their sign interpretation follow their owners.

[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
excludes all out-of-bounds writes, including mapped adjacent-field writes.
Reject, omit or stop before such a write, preserving the valid prefix. Unknown
reads require established backing, supplied retained input or a stop. They do
not establish a default chapter, selected throne piece or callback pointer.
Safe initialization/stricter resource admission can be declared separately from
the original path. Original capacities and encodings belong in the appendix.

A council window contains base/video/prompt/resource objects, six buttons,
actor, advice variant, five opinion results, anarchy mode, five chapter keys,
three category/start/end/caption records, current chapter, caption offset and
advice rectangle. The active-window reference and callback-busy flag are shared;
both initially absent/zero. Busy is not a general reentry guard: the callbacks
below set it before their work, and some returns leave it set.

Opinion categories 0..4 are military, science, economic, foreign and domestic.
Results are chapter numbers 1..7, except domestic 1..6. They select text/media
branches; they are not a numerical happiness rating. Opening the council can
recalculate trade/cities, clear a shared refresh request and consume RNG during
chapter parsing, so it is not a pure read-only observation of simulation state.

## 2. Opinion selection

The dispatcher snapshots the active council's variant before category dispatch
when a council exists. Category 0 invokes military,1 science,2 economic,3 foreign,
and every other category domestic. Military/domestic do not consume the variant.
Without an active council, science/economic/foreign require the original retained
variant input or stop; no universal zero is established.

### Military

Scan cities in increasing ID below the live total. For existing cities whose
signed owner equals actor, count Barracks, remember the **last** Palace city,
and count cities. Query Barracks before Palace. Replace city count≤1 by 1.
Scan existing actor units in increasing ID and count all unit types. If
`trunc((cities−1+units)/cities) < 2+(government<5 ? 1:0)`, return 1.

Clear two eight-entry capability counts. For every civilization 1..7, without
an alive filter, add one naval capability for Seafaring(ID75), another for
Nuclear Power(59), then scan unit types 0..61. Query knowledge of the signed
prerequisite; if known, count sea-domain types as naval capability, otherwise
count attack/defend-role types as land capability. Domain/role are read after
the knowledge call. These count available types, not owned military strength.

Scan peers 1..7 except actor. Count actor→peer **War** relationships and peers
whose naval/land capability strictly exceeds actor's. Evaluate in this order:

1. If stronger-land count>`trunc((popcount(activeTribes)−1)/2)`, return 2.
2. Re-read activeTribes; if stronger-naval count exceeds that threshold, return 3.
3. If actor knows Masonry(47), has a remembered Palace city, that city lacks City
   Walls(8), and actor lacks Great Wall(wonder 6), return 5. Queries short-circuit.
4. If Barracks count 0, return 4.
5. If War count 0 and actor power rank≥5, return 7.
6. Return 6.

Civilization 0 has zero capability entries because population
starts at 1; arbitrary outside indices still require established read input.

### Science

Count peers 1..7 except actor whose unsigned stored number of advances≥actor's
fresh stored count. Inactive peers participate and equality counts. If none,
return 7. For government≥5 return 2 if science rate<4; otherwise, for government
not 4 return 2 if science rate<6.

Set trade capacity to unsigned active unit counts 48+49 (Caravan/Freight).
Choose building 6 for variant 0,12 for variant 1,26 otherwise. Scan actor cities:
count cities and that building, then add each signed trade-route count to
capacity. If actor knows the building's signed prerequisite and
`trunc(cities/2)>buildingCount`, return 3. If actor knows Trade(84) and
`trunc(cities/4)>capacity`, return 4. Finally return 5 when counted peers<
`trunc(popcount(activeTribes)/2)`, otherwise 1.

### Economic

Initialize taxes/upkeep/chosen-building count/city count 0 and trade capacity
from active unit counts 48+49. Select `(advance,building,divisor)` by variant:
0→`(20,5,2)`,1→`(6,10,3)`,otherwise→`(22,22,4)`. Clear counts for buildings 0..38.

For each existing actor city, in increasing ID: increment cities, add signed
trade routes, invoke C03 trade distribution with `(city,netTradeTotal,1,0)`,
then query buildings 0..38 and increment each present count. Query the chosen
building again and count it separately. If the city is not in disorder, add
its live taxes; the source also reads/accumulates science into an unused local,
which makes no later decision. The distribution call precedes these reads and
its state changes remain.

For each building 0..38 with nonzero count, query C08 civilization upkeep. When
cost is nonzero, add count*cost to upkeep with wrap. Return the first match:

| Predicate | Result |
| --- | --- |
| taxes<upkeep and live gold<100 | 1 |
| Required variant advance unknown | 2 |
| chosen-building count<trunc(cities/divisor) | 3 |
| Trade(84) unknown | 4 |
| trade capacity<trunc(cities/4) | 5 |
| wrapped taxes−upkeep<6 | 6 |
| Otherwise | 7 |

### Foreign

Scan peers 1..7 except actor. Separately count actor→peer Contact, Alliance and
War, in that order. For intelligence count, use the **live local human**, not
actor: count the peer when local→peer Embassy is set, otherwise when local owns
United Nations(wonder 24), otherwise Marco Polo(wonder 9). Re-read local human
after preceding wonder calls. No active/contact filter limits this count.

Evaluate in order:

1. Contact count 0 →1.
2. Actor lacks advance 27 for variant 2, or advance 88 otherwise →3.
3. Contact count≥2, reputation≤1, Alliance count 0, HasAllied absent and power
   rank≤6 →2.
4. Reputation≥3 →4.
5. War count==Contact count and reputation>1 →4.
6. TradedTechnology absent →6.
7. Intelligence count 0, or War count==Contact count with intelligence<Contact
   count →5.
8. War count==Contact count →6; otherwise 7.

### Domestic

Capture the shared “recalculate city moods for the attitude display” request and clear it before
any city or callback; it is never restored here. Scan existing actor cities.
If the captured request was set and live government>4, calculate that city with
mode 1 before inspecting happiness. Count unhappy cities when signed happy<
signed unhappy, and count disorder only within that branch when the disorder
flag is set. Otherwise, if a fresh equality comparison succeeds, count balanced
cities. Independently count celebrating cities. A city with more happy citizens
is neither unhappy nor balanced. The unused city total has no decision effect.

Start `spendLuxury=true`. For government≥5 replace it with
`scienceRate+taxRate<9`. For lower governments, set it false only when there
are no unhappy cities, some balanced cities, no celebrating cities and
scienceRate+taxRate==10. Then:

- No unhappy cities: false spendLuxury→4; otherwise celebrations→6, no
  celebrations→5.
- Unhappy cities and false spendLuxury→2.
- Otherwise, disorder count nonzero and live government==6→1; else 3.

## 3. Council construction and initialization

Create a council interaction with anarchy0 and publish it as active. Native
construction order, caption offsets and uninitialized presentation-only callback
fields are reference details. Retire the active reference at completion.

The public wrapper sets the shared spaceship-presentation suppression flag 1,
constructs a local council, stops the MCI device and sets the shared exit/playback
request latch to 1. Initialize the council and run only on nonzero result. Then
reconcile playback, clear suppression 0 and destroy the council. These are
ordered normal-path effects; failures do not imply an invented rollback.

Initialization stores actor and queries P06's advance variant for that actor.
Calculate the five advice values in category order0..4, rereading receiver actor
before every child. Display allocation/geometry and background staging are
reference behavior.

If cheat preview is enabled, repeat `COUNCILCHEAT0` while its ordinary result
is 0. Capture dialog selection;0 ends,6 sets anarchy 1 and ends. Otherwise repeat
`COUNCILCHEAT1` integer input until 1..7, additionally≤6 for selection 5. Numeric
cancellation returns initialization 0 immediately, retaining earlier advice and
window effects. For selection 1..5 publish advice[selection−1]. Other selections
would write outside that logical advice array and are excluded bugs, even when
an original wrapped address names a known receiver field. Do not use this to
initialize otherwise unknown chapter/callback state.

Still within cheat preview, repeat `COUNCILCHEAT2` until variant 0..2 is accepted;
cancellation continues with the retained variant. This second cancellation does not return 0.
If actor government 0, set anarchy 1. Otherwise clear the first character of all
five chapter keys, append prefixes `M,S,T,F,A`, then append their decimal advice
results in the same order. A cheat-forced anarchy with nonzero government still
builds these keys. Anarchy disables the first five button members.

Build media stem as `anarchy ` or `council` plus signed decimal variant. In normal
mode select category 0's chapter; failure returns 0. Set up the prompt over the
advice rectangle, create a parented video child, build
`civ2\video\`+stem+`.avi` and resolve it. Missing file returns 0. Open media;
nonzero status returns 0, additionally displaying `VFWNOTREGISTERED` for the
class-not-registered status. On success assign the application art palette to
video and its presentation, install Finished callback, capture panels and
return 1. Panel capture's early return does not turn this success into failure.

## 4. Council chapter grammar

[Native council content](../content/data-format.md#65-conditional-council-content)
represents these choices as named opinions and explicit response/chance
continuations. A converter resolves parser acceptance into those paths rather
than putting a retained parser flag in the native file. Preserve opinion tests,
seen-response categories, random-selection law and authored captions. The grammar
below remains the classic text adapter; new layouts/media do not change its selection semantics.

Select-chapter(category) returns 0 immediately only when signed category>4.
Nominal categories 0..4 select that advice key in the current media-stem text
file. A negative category is not an original safe rejection and needs established
section-string backing or a stop. Failed section open returns 0.

After successful open, if the first mode 1 line exists, clear category slots 0..2
and start frames 0..1 to 0; leave start frame 2 retained. Store category in slot 0,
read start/end integers, clear caption 0 and copy the trimmed remaining cursor
text. The first caption does not suppress a leading `*`. Set index 1,
accepted=true, reuseLine=false and seen-category set empty. Slot 0's category is
**not** inserted into seen. Then repeat:

1. If index≥3 or category[index]==−1, set success 1 and stop.
2. Unless reuseLine, read a mode 1 line; failure stops with success 0. Clear
   reuseLine. Read the next token through F03's live shared token/cursor.
3. Dispatch its first character. `#` sets reuseLine=accepted and continues;
   `;` continues to a fresh line. `E/e` stores−1 in category[index] and continues,
   causing success on the next loop. `M/m,S/s,T/t,F/f,A/a` set the current
   category 0..4; unknown initial characters leave that category unchanged.
4. `R/r` is special: parse the token suffix as P04 decimal threshold; consume
   F01 inclusive range(1,10). If draw>threshold set reuseLine=true, otherwise
   set accepted=false. Continue without storing a chapter. The greater-than
   branch leaves accepted unchanged.
5. For other category-bearing/default tokens, if that category is already seen,
   set accepted=false and continue to the next line. If token has a suffix,
   parse it as required opinion and compare to live advice[category]; mismatch
   also sets accepted=false and continues.
6. Store category, read start then end integers, clear caption[index], trim the
   remaining cursor. Copy it unless its first character is `*`. Increment
   index, set accepted=true and mark the captured category seen.

Always close after a successful section open, including a missing first line
or partial failure. Return 1 only through step 1. There is no requirement for
start≤end, no extra third response, and no deduplication against the initial
speaker. `#` and R can parse more tokens on the **same line**; do not replace
this grammar with simple line-level random inclusion. Mask shift encodings for
malformed categories belong to the appendix; no OOB advice/chapter write is
required compatibility behavior.

Panel capture constructs a temporary port then sprite even though these locals
do not supply the copied frames. In normal mode, for each category 0..4, open
its key in the current stem and read one mode 1 line. Failure destroys temporary
sprite then port and returns without an extra Close. Otherwise read its first
integer, seek that frame, copy it to parent at
`(S+1+(S+90)*category,R+1)`, then close the rule file. In anarchy seek frame 1 and
copy to `(S+2,R+2)`. Destroy temporary sprite then port at ordinary completion.

## 5. Council playback, captions and input

Allow the player to choose an available council category and receive its selected
opinion, authored chapter/caption content and supported media. Anarchy disables
the five ordinary categories. Use §4's resource interpretation; changing playback
layout or timing does not recompute advice or change simulation state. Process
required network/timeout events while awaiting completion; cosmetic caption and
frame timing are optional.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 6. Council geometry and controls

Council window geometry, fonts, button placement and native controls may differ.
Preserve category availability and selected advice/content from §§2–5, plus
required completion and network processing.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 7. Throne-room progress and admission

V01 initializes piece levels `[1,1,0,1,0,0,1,0,0,0]` and upgrade count 0.
The first eight values are piece levels; entry 8 is seven overlay ownership
flags. P09 does not reset those persistent values when merely viewing the room.

Threshold(stage) starts sum 0 and loops step 0 through stage inclusive. Add
`(7−liveDifficulty)*step` with F01 wrap every iteration; return
`trunc(signed(sum)/2)+1`. Do not replace this with an unbounded integer formula
for off-nominal values: difficulty is live and sum wraps. A negative stage
executes no iterations and returns 1. At ordinary fixed difficulty the formula
is `trunc((7−difficulty)*stage*(stage+1)/4)+1` when intermediate arithmetic fits.

Progress check returns for scenario mode or game mode exactly 1. Capture local
human actor. Scan existing owned cities for the first Palace, using signed
owner; absence returns. Require turn>`accelerationSelection*20+1`. Calculate
V08 score for captured actor, read its signed upgrade count and compare score
to Threshold(count+1). Insufficient score returns. Otherwise increment the
stored signed upgrade count with its defined narrowing wrap. Re-read it;
if≤37 and Throne Room preference is enabled, request upgrade display for the
**live local human**. The progress increment occurs even when preference is
hidden, beyond the visual limit, or a later display fails. Only one increment
is awarded per check; there is no catch-up loop.

Upgrade-display wrapper constructs a throne receiver first, then scans piece
levels 0..6, setting available when any unsigned level<4. Independently set
available if any of the seven overlay flags is absent. It does **not** scan
piece 7 in this admission test. If available, run the upgrade interaction;
otherwise only destroy the receiver. View wrapper constructs, views and destroys
without this admission test or any upgrade mutation.

## 8. Throne construction, windows and viewing

Retain actor and publish the active countdown interaction while it is open;
clear that publication on normal close. Set all-complete1 and scan all eight
piece levels0–7, clearing it for any value<4 without an early break. This captured
result is used by the upgrade interaction; viewing alone does not change piece
levels, overlay flags or upgrade count. Run required network idle in mode≥3 and
resolve completion/cleanup without depending on original window geometry.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 9. Throne image composition and selection art

Display persistent piece/overlay state and supported authored content. The
all-complete result captured by §8 gates overlay presentation and is not recomputed
after a piece increment. For upgrade choices, when not all-complete offer pieces
0–7 whose live unsigned level<4; otherwise offer absent overlays0–6. Input still
uses §11 eligibility and paired-overlay fallback. Drawing resources, layer order,
rectangles and native image lifetimes are optional.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 10. Upgrade interaction and persistent mutation

Begin an upgrade interaction for the current actor and captured all-complete
state. Preserve the following authored content, selection and mutation boundary;
visual scene preparation and native objects are optional.

Read `THRONE` in the game text file if available. Start message empty; consume
mode 1 lines until a line begins `@`. Append the live token cursor text and one
space for every preceding line; there is no additional comment/blank filter
here. EOF requires supplied behavior or a stop after F03's EOF/close effects,
not an invented section terminator. Present the assembled message and close
the stream. Aggregate overflow is excluded.

Set phase0 and await semantic Continue, discarding stale queued input and
servicing network idle in mode≥3. Then prepare the eligible selection; a declared
failure returns without committing an upgrade. Read `ADDTOTHRONE` using the same
concatenation and close rules. Set phase1, discard stale input and await an
accepted selection with the same network service. **Only after selection
completion mutate persistent state**, using the current actor and selection:

- If all-complete!=0, first read overlay flags. Require an established selected
  overlay. Set its ownership bit using the original low-five-bit shift semantics
  and narrow the resulting overlay field. Logical ownership is flags 0..6. A
  retained scalar selector outside that nominal range still has defined shift
  behavior: bit 7 can be stored, shifts 8..31 vanish on narrowing, and 32 selects
  bit 0. This is an in-bounds scalar update, not an adjacent-memory overwrite.
- Otherwise capture actor, require established selection and increment that
  selected piece's level with its defined byte wrap. A selected index outside
  the piece collection cannot overwrite adjacent civilization fields.

The original constructor does not initialize selection. An interrupted or
modal-depth-limited loop can return without a selecting click; preserve supplied
retained selection or stop before consuming it. Do not invent piece 0. A rejected
click may still have written a selection; cancellation is not an automatic
rollback of that temporary.

Present the upgraded result, then allow semantic Continue with phase0 and the
same required network service. Finish the interaction. Failure of final art or
presentation does not roll back the committed upgrade.

## 11. Throne click and key rules

Resolve an action to Continue or a piece/overlay group in the published room
for its current actor. In phase0, Continue completes that stage without selecting
an upgrade. In phase1, publish the chosen group before its eligibility test;
a rejected choice leaves that temporary selection but does not commit an upgrade.
Original pixel hit order, miss effects and physical keys are in Appendix D.

When all-complete is 0, reject group≥10 or a selected level≥4. There is no original
nonnegative check before the level read; invalid negative groups need read
backing or a stop, and never authorize an OOB increment. Otherwise accept.
When all-complete is nonzero, reject groups<10. Subtract 10 from selection and
capture overlay flags. For selection 4 or 5, reject if both are already owned;
if the requested one is owned but the other is absent, switch selection to the
other. Then re-read overlay flags and reject if the selected ownership bit is
set. All rejection branches play 94 and keep the modal loop running. Acceptance
clears the loop. Overlapping selection-table rectangles deliberately route the
paired overlays through these fallback rules.

A new keyboard, mouse or other accessible control may select the same eligible
piece/overlay and invoke Continue. Its semantic result must pass the rules above;
completion without an established selection cannot invent piece0 or apply a new
cancellation rollback. Exact physical key bindings are reference details.

## 12. Throne geometry tables

Piece/overlay identity and persisted levels come from §§7–11. Original
composition, choice-image and hit-region rectangles are optional reference data; a
new interface must map each offered choice to the same piece/overlay identity.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 13. Conformance cases and input profiles

Validate all five opinion selectors with their real query side effects, custom
chapter/caption interpretation, anarchy/cheat choices and network completion.
Check threshold/admission, one increment per progress check, piece/overlay choice
eligibility and persistent mutation including cancelled/interrupted selection.
New artwork, controls and timing must not change those decisions. Geometry,
frame sequencing and native lifetime traces are optional reference validation.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## Appendix A. Evidence and original entry map

Primary reconstruction is pinned at
`a71e8aefffe04e6f719e0e213b027b128a747711`:
[reports.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/reports.cpp)
contains council/throne bodies,
[civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp)
contains threshold/progress, and
[throneroom.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/throneroom.cpp)
provides the two public wrappers. These links are evidence navigation; the
behavioral sections and tables above stand on their own.

| Area | Original entries |
| --- | --- |
| Council wrapper/lifetime | `00516570`, constructor `0051661a`, destructor `005167d9` |
| Council initialization/run | `00516947`, `00516fd4` |
| Background/controls/chapter | `00517158`, `00517673`, `005179a3` |
| Caption/panels/buttons/position/finished | `00517dd3`, `00518007`, `005181d8`, `00518342`, `00518471` |
| Advice dispatch | `00518582` |
| Military/science/economic/foreign/domestic | `004bc480`, `004bc8aa`, `004bcb9b`, `004bcfcf`, `004bd2a3` |
| Progress threshold/check | `00486e15`, `00486e6f` |
| Upgrade/view wrappers | `0044cc80`, `0044cd9b` |
| Throne lifetime/windows | Constructor `0044ce16`, destructor `0044d027`, create `0044d162` |
| Upgrade/view/Continue | `0044d296`, `0044d9c1`, `0044dab5` |
| Finished images/selection images | `0044db92`, `0044e790` |
| Click/key | `0044f4c9`, `0044f717` |

Independent reconstruction inspected at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752` in `civ2`:
`src/high_council.cpp`, `high_council_advice.cpp`, `council_state.hpp`,
`window_constructor.cpp`, `power_ranking.cpp`, `throne_room.cpp`,
`throne_geometry.inc`, `throne_selection_tables.inc`, `city_window.cpp`,
`map_window.cpp`, `window_startup.cpp`, `quit.cpp`, `cd_playback.cpp`,
`include/civ2/high_council.hpp`, `throne_room.hpp`, the annotated layouts and
bugfix log. Stock `COUNCIL0.TXT` confirms that same-line directives occur in
ordinary files; they are not merely a malformed-input curiosity.

The independent military `allies` temporary tests **War**. In foreign advice,
`wars` counts **Alliance**, and `allies` counts **War**. Primary treaty masks and
original bytes resolve these inferred-name mismatches; §2 uses logical meanings.
The source's exit-named preparation service stops MCI and sets a shared latch;
calling it here does not by itself mean the council asks the game to terminate.

## Appendix B. Representation adapters and excluded bugs

Addresses, allocation sizes, packed fields and native IDs below aid original
trace comparison. A cleanroom implementation can use different representations.
F01 conversions in the main algorithm remain behavioral regardless of storage.

### State mapping

Council receiver's accessed extent is `0x1328`: video at `114`, prompt `b3c`,
resource owner `e30`, media stem 32 bytes at `e34`, variant `e54`, five advice words
`e58`, actor `e6c`, anarchy `e70`, five three-byte keys `e74`, categories `e84`,
start frames `e90`, end frames `e9c`, three 255-byte captions `ea8`, current chapter
`11a8`, caption offset`11ac`, advice rectangle `11b0`, six buttons `11c0`
with `0x3c` stride. The initial five-word chapter clear covers categories and
only the first two start frames. Active council and busy are PE-zero words
`00631acc/00631ac8`; suppression is `00631ad0`.

Throne receiver accessed extent is `0x1c3c`: palette `b8`, screen `4ec`, resource
owner `4fc`, actor `500`, update surface `504`, backing port `618`, regions `660`,
phase `1924`, completion `1928`, selection `192c`, prompt `1930`, text rectangle
`1c24`, font `1c34`. Published popup is `00626810`. Do not infer a class hierarchy
or change behavioral ownership from the primary wrapper's larger stack union.

Civilization stride is `0x594`; throne storage starts`3f3` with 10 bytes. Eight
unsigned level bytes precede overlay byte at `3fb`; entry 9 is not consumed by
these algorithms. Upgrade count is signed 16 at `3fe`. Defined level increment
is modulo 256 and progress increment modulo 65536 with signed reinterpretation.
Overlay shifts use `1u << (uint32(selection)&31)`, then low-byte narrowing.
Treaty Contact/Alliance/Embassy/War are `1/8/80/2000` hexadecimal; technology-traded
and ever-allied civilization flags are `80/100`. Mood recalculation request is
bit `4` of game request word `006`. Throne preference is game flag `20000`; scenario
is map flag `80`. These are validation encodings of the named properties above.

Rule/unit/owner/city sign conversions follow F03/F05/F06: owner, happiness and
trade routes are read as signed 8; knowledge count, government, rates, reputation
and active-type quantities as unsigned 8; city net trade/tax/science totals as
signed 16. Power rank lookup uses game backing `13a+civ`; actor 7 addresses its
retained gap byte. An adapter must supply that read if no producer has established
it; ordinary zero-filled host storage is not proof of the original input.

Finished image geometry is 624 bytes at `0061c068`; overlay block begins`0061c268`.
Selection's 15 image rectangles,34 hit rectangles and 34 groups are 920 bytes of
initialized local values in `0044e790`, reproduced logically in §12. The source
may expose later geometry pools for diagnostic reads; that does not authorize
writing any adjacent object.

Council native creation options are `c02`, embedded prompt `84008`, video`200`,
constructor prompt `4000`; buttons use style `21`. Finished callback token is
`0040188e`, advisor/Close callback token `00401b22`. Throne windows use `800`,
prompt `4000`, click/key tokens`004034bd/00401fe6`; network idle is `00403585`.
The media-open error singled out is `80040154` (class not registered). These
values map named behavior to the original platform, not a required host toolkit.

### Bugs and reconstruction policies

| Boundary | Classification and permitted treatment |
| --- | --- |
| Council current chapter never initialized before first caption clear | BF-067 initializes 0 in the independent production wrapper. This is a deliberate repair; original comparison consumes retained input. Explicitly supplied receivers remain distinct |
| Council video frame callback left incoming | BF-298 sets it null only in the production-owned wrapper. Preserve real setters/live callbacks; no original blanket constructor zero is established |
| Cheat selection outside 1..5 writing advice | Original wrapped destination can hit chapter/video/other fields. **Every such overwrite is an excluded bug**, even when mapped. It cannot become an alternate producer of logical state |
| Negative/outside category or chapter read | Original can read adjacent receiver bytes. A diagnostic adapter may supply established bytes; a restrictive implementation can reject. Never invent a caption, opinion or frame |
| Foreign actor/observer outside 0..7 | BF-195 rejects full selectors, rechecking the observer at each live use. This stricter admission is separate from original wrapped address arithmetic |
| Oversized chapter key/caption/media path/title or throne message | Keys/captions/media stem use3/255/32 bytes, file paths128, shared title/line storage2052 and throne message1024. All overflow excluded; safe storage, omission or rejection can replace it |
| Modal interrupted before selection producer | No original selection default. Supply retained value or stop at consumption after earlier effects; safe initialization must be declared |
| Selected piece outside the eight logical pieces | No adjacent-field mutation, including an apparent alias into overlay/count/another civilization |
| Missing text terminator | Original null-line read after EOF; safe stop or declared resource rejection, not a fabricated successful end |
| Prompt retains temporary caption-font pointer | Later unknown use needs established input or a stop. The destroyed object is not a persistent valid font just because its address remains mapped |
| Oversized image level/geometry selector | No clamp established. Backed diagnostic reads or rejection/stop; no overflow into adjacent storage |

## Appendix C. Validation record

Seventeen pinned independent files and four primary files were checked, with
hashes recorded for seven original executable/text/resource assets.
Nine unchanged whole independent function bodies were compiled with supplied
bounded state and service providers: advice dispatch, all five opinion scorers,
threshold, progress and chapter selection. **78 assertions** passed optimized
and address/undefined-behavior-sanitized builds. They exercise priority results,
War/Alliance distinctions, live observer and city mutation, request clearing,
progress ordering/wrap, chapter deduplication, same-line directives, random
threshold equality, partial EOF and failed-open cleanup.

These host-native fixtures use supplied civilization/city storage, knowledge,
upkeep, trade, score and rule-token providers. They do not execute the original
Win32 program, prove F03 parser integration, validate malformed original-address
aliasing or reproduce native media/modal/pixel output. Foreign advice's valid
selectors avoid the independent BF-195 divergence; absent variant checks exercise
its explicit unknown-input stop.

Direct checks against the original executable cover **240 data/instruction
checks totaling 2941 bytes**:156 geometry words,230 selection-table words encoded
in immediate stores, and nine branch/arithmetic probes. The latter confirm War,
seven-piece admission versus eight-piece completion, overlay mask, inclusive
threshold, signed division, narrow progress increment, visual limit and live
observer display. The table checks verify values and destination order, not
whole GUI execution. Source/asset/body hashes and probe records are retained
with the preparation artifacts. Neither reference source tree nor original
resource was modified.

The documentation set was checked for local/source links and anchors, unique
specification IDs and document presence. Actual AVI playback, window/palette
restoration, hit delivery, sounds, timer interruption and complete cross-owner
execution were not established by these checks. X02 distinguishes required
semantic integration from optional native presentation comparisons.

## Appendix D. Original presentation reference

This appendix retains the original presentation procedures removed from the main
specification. Geometry, pixel/sample output, native controls and cosmetic timing
are optional reference behavior under X01. Required semantic fragments were kept
in the numbered main sections and are linked where omitted here; this appendix
does not relax their simulation, information, file or protocol obligations.
Original section numbers and local § references identify the recovered procedure
family, whether its retained fragment is in the main section or here. Historical
native/pixel tests are reference validation, not baseline acceptance criteria.

### Original §5. Council playback, captions and input

Run selects the application art palette, fades game to gray, assigns that
palette to main and centers the council window. Show button members 1,2,4,3,0,5
in that order. Run the embedded prompt with mode 0, show council and flush paint.
In anarchy, position video at `(S+1,R+1)`, show it, flush and start unrestricted
playback. Install N03's modal-network idle callback only in game mode≥3. Run the
council modal loop in every mode and clear the idle callback after return. Reset video audio,
redraw background, hide council, flush, clean prompt, restore main's initializer
palette, fade game from gray and restore prompt palette, in that order.

Position-video(category) returns only for signed category>4. Otherwise move the
video to `(S+1+(S+90)*category,R+1)`, with wrapping arithmetic. A negative
category therefore has a defined coordinate calculation; it is not clamped.

Button callback first sets busy 1 and captures the active receiver, then resets
audio and stops its video. Missing receiver requires a stop after the already
completed audio/busy effects. Close(category 202) clears caption with mode−1,
flushes and clears modal loop, returning with busy still 1. Anarchy returns
immediately after stop, also retaining busy 1. Otherwise:

1. Clear caption(mode−1), then set current chapter 0.
2. Select the requested chapter. Failure clears caption again, flushes, clears
   modal loop and returns with busy 1.
3. Hide video and position it using the requested category. If category<5,
   draw caption(mode 0), seek the **live** current start frame, show video,
   read current end frame then start frame and start that range. Increment
   live current chapter after the start call.
4. Clear busy 0.

Finished callback sets busy 1 and captures active receiver. Outside anarchy,
hide video, then if signed current chapter<3 and its category≥0: position for
that category, draw caption(mode 1), seek start, show, read end then start,
start range and increment live current chapter. There is no independent
current chapter≥0 guard here; its indexed read needs backing if negative.
Flush paint and clear busy 0 whether or not another chapter was started.

#### Caption accumulation

Construct a temporary face 0 height 14 font before reading current chapter. If
current chapter<0 or≥3, destroy font and return. For mode!=1, fill advice area
color 29 and reset caption offset 2. Mode−1 then updates the window, destroys font
and returns without the normal flush.

Otherwise copy advice rectangle, inset horizontal edges by 2 and add caption
offset to its top. Publish temporary font as prompt metric font. Re-read current
chapter to select caption after preceding calls. If nonempty, offset rectangle
by 1,1 and draw rectangle text in color 37, mode 1; offset back, draw color 10.
Add one queried font line height to caption offset. Measure the live selected
caption; if its width exceeds rectangle width, add **one more** queried line
height. This reserves at most two lines regardless of how many lines the text
actually wraps into. Update, flush and destroy font. The original prompt retains
its published font reference after temporary destruction; a different object
representation must not invent continued valid font contents at a later unknown
read. Current chapter changes after the entry bounds check are not rechecked.

### Original §6. Council geometry and controls

Shared spacing defaults are R=24, S=8, B=2 before display-metric replacements;
read live values at the stated operations. Frame colors default to37 and18,
with reversed order18/37 for inset panels and37/18 for the outer frame.
Window width/height come from §3.
The five advisor controls have 75×22 rectangles, initial left=S+7 and
`top=asr(wrap(R−22),1)+R+121`. Create in visual category order 0..4 but store in
button members `[4,0,3,2,1]`. Labels are `[155,91,69,440,441]`. IDs equal the
category; use colored-button style and bind the council callback. After each
except last, offset x by live S+90. Close is member 5, ID202, label 255, rectangle
`(S,surfaceHeight−29,surfaceWidth−S,surfaceHeight−B−1)` and the same callback.

Background tiles the application art surface using destination coordinates as
source origins, in this order:

- Top band `(0,B,surfaceWidth,R−2B)`.
- Middle band `(0,B+R+122,cachedWidth,cachedHeight)` with the preceding band size.
- Bottom band `(0,surfaceHeight−(B+30),cachedWidth,30)`.
- Left strip `(B,0,S−2B,surfaceHeight)` and right strip at
  `surfaceWidth−(B+stripWidth)` with that size.
- Four dividers starting `(B+S+90,R−B)`, each width=stripWidth,
  height=122+2B, stepping x by S+90.

These tuples are `(x,y,width,height)`. Begin panel rectangle
`(S−B,R−B,S−B+90+2B,R−B+122+2B)`. For each of five panels, draw B inset borders
with reversed frame colors, shrinking 1 per border; fill remaining interior 10,
expand by B and offset by S+90.

Advice rectangle starts `(S−B,surfaceHeight−130−3B,
surfaceWidth−S+B,surfaceHeight−30−B)`. Draw B reversed-color inset borders,
fill 29, then inset 2 more and retain this smaller rectangle for captions/prompt.
Copy the surface's live clip rectangle and draw B outer-color borders with
insets. Compose title after all drawing: language 1 uses label 299, space, actor
adjective; other languages use adjective, append `er` only for live language 2,
then space and label 299. Capture surface width before title measurement;
x=trunc((width−textWidth)/2), y=live B+1. Draw normal application font at
x+1,y+1 color 10, then x,y color 26, both in text mode5. Label and adjective
calls remain live.

### Original §8. Throne construction, windows and viewing

Construct event window, palette, resource owner, update surface, backing port,
interaction regions, prompt and font in that order. The region constructor
leaves its contents retained; the font constructor initially clears its handle.
Query screen rectangle, store actor, publish this receiver as the shared countdown
popup, then set all-complete 1 and scan **all eight** piece levels 0..7. Any value<4
sets all-complete 0; do not break early. Set text rectangle `(0,450,640,480)` and
replace font with face 0 height 18, flags 3. Constructor leaves selection and modal
phase retained until their actual producers below.

Destroy first clears popup publication, closes resource module, then destroys
font, prompt, regions, backing port, update surface, resource owner, palette and
event window in reverse construction order. Region destruction is inert. Release
only completed components on an ordinary construction failure; no repeated
cleanup of already completed members is implied.

Create-windows resolves `pv.dll`; failure returns 0. Load the module without an
additional load-result gate. Create outer parented window at 0,0 with width=
screen.right and height=screen.bottom+5, initializer palette and main parent;
these are screen edges, not right−left/bottom−top. Set black background. Create
640×480 child surface with local palette, center it, bind Left Click and Key
callbacks and return 1.

View returns if window creation or image composition fails. Otherwise draw the
Continue label, set phase 0, show outer window, flush, assign local palette to
outer, show child and flush. Install modal network idle only for game mode≥3. In every mode run child
modal, clear idle afterward, hide child, flush, restore outer initializer palette,
hide outer and flush. Viewing does not capture the mouse or discard queued input
at this entry.

Continue text is `(` + label 456 + `)`. Draw it in the retained text rectangle
using the room font and rectangle-string mode 0, color 0. Offset the live rectangle
by−1,−1 and draw color 255, then offset back 1,1. This differs from the message
shadow used in the upgrade sequence.

### Original §9. Throne image composition and selection art

Composition constructs temporary image surface then sprite. It does not first
clear the destination or load selection background 100. For each admitted part,
load its resource using palette start 10/count 236. Failure destroys temporaries
and returns 0, retaining earlier images. Extract the specified `(x,y,width,height)`
rectangle with transparency 9, replace sprite color 7 by 9, then draw at the live
geometry's `(x−1,y−1)` on the child surface. Read extraction height,width,y,x
before extraction, and re-read destination y then x after color conversion.
Section 12 gives all geometry; no internal packed layout is required.

A piece with level 0 is skipped. Otherwise read its unsigned level again, use
level−1 as geometry index, and resource `105+5*piece+(level−1)`. Ordinarily
levels 1..4 have four geometries each; off-nominal geometry reads require backed
inputs or a stop, not a clamp. Overlays use resource 160+overlay and their own
geometry. Follow this exact draw/pump order:

1. Pieces 0,1,2, with a message pump before each.
2. Pump; overlay 0 if all-complete and owned.
3. Pump; piece 3.
4. Pump; overlays 1 then 2 if all-complete, testing live ownership for each.
5. Pump; piece 4.
6. Pump; overlay 3 if all-complete and owned.
7. Pump once; pieces 5 then 6, with no pump between them.
8. Pump; overlays 4,5,6 if all-complete, with no pump between them.
9. Pump; piece 7. Pump once more, destroy sprite then surface, return 1.

All-complete is the receiver's retained construction result; composition does
not recalculate it after a piece increment. It gates overlay visibility even
when persistent overlay flags are set. Piece 3's loop-derived extraction row
is 3, the same ordinary geometry as its separate destination row.

Selection composition constructs image then sprite, copies the fixed selection
tables, clears interaction regions and adds all 34 rectangles in table order,
with ID=row and its table group. Load background resource 100 into the child;
failure returns 0 with the already installed regions. When all-complete is 0,
for pieces 0..7 pump, read live actor/level, and draw choice resource 170+piece
only if level<4. Otherwise capture overlay flags once, then for overlays 0..6
pump and draw 178+overlay only if absent from those captured flags. Selection
extraction uses its table's rectangle and draws at x−1,y−1, transparency 9 and
color 7→9. A failed choice load stops after earlier effects. Destroy temporaries
and return 1 on completion. All hit regions exist even for choices not drawn;
input validation remains necessary.

### Original §12. Throne geometry tables

All tuples below are `(x,y,width,height)` in the 640×480 source coordinate
system. They are visual/interaction constants, independent of memory layout.
Drawing uses the extraction rectangle as stated in §9 and destination
`(x−1,y−1)`. A hit rectangle becomes the half-open region
`[x,x+width) × [y,y+height)`; do not apply the image destination's −1 here.

#### Finished piece and overlay geometry

| Piece | Level 1 | Level 2 | Level 3 | Level 4 |
| --- | --- | --- | --- | --- |
| 0 | `(145,1,322,318)` | `(118,1,360,311)` | `(137,1,348,298)` | `(169,1,267,325)` |
| 1 | `(1,278,640,203)` | `(1,277,640,204)` | `(1,277,640,204)` | `(1,277,640,204)` |
| 2 | `(1,301,640,180)` | `(213,299,202,182)` | `(247,315,110,166)` | `(204,306,217,175)` |
| 3 | `(1,1,640,296)` | `(1,1,640,303)` | `(1,1,640,300)` | `(1,1,640,298)` |
| 4 | `(177,1,276,195)` | `(173,110,264,187)` | `(207,36,189,266)` | `(184,1,234,299)` |
| 5 | `(1,1,640,311)` | `(1,33,640,274)` | `(1,1,640,310)` | `(1,1,640,313)` |
| 6 | `(236,269,125,39)` | `(299,234,40,72)` | `(200,198,209,123)` | `(242,154,122,151)` |
| 7 | `(1,1,640,480)` | `(1,1,640,480)` | `(1,1,640,480)` | `(1,1,640,480)` |

| Overlay | Rectangle | Resource |
| --- | --- | --- |
| 0 | `(1,311,640,43)` | 160 |
| 1 | `(78,14,449,240)` | 161 |
| 2 | `(1,235,640,67)` | 162 |
| 3 | `(209,142,192,155)` | 163 |
| 4 | `(148,251,312,70)` | 164 |
| 5 | `(32,273,576,65)` | 165 |
| 6 | `(1,139,640,342)` | 166 |

#### Choice images

Rows 0..7 are the eight piece choices; rows 8..14 are overlay choices 0..6.

| Row | Rectangle | Resource |
| --- | --- | --- |
| 0 | `(212,1,179,295)` | 170 |
| 1 | `(19,283,591,198)` | 171 |
| 2 | `(198,303,215,177)` | 172 |
| 3 | `(11,1,607,286)` | 173 |
| 4 | `(188,18,230,283)` | 174 |
| 5 | `(1,1,608,311)` | 175 |
| 6 | `(284,258,39,57)` | 176 |
| 7 | `(1,1,640,480)` | 177 |
| 8 | `(20,314,591,40)` | 178 |
| 9 | `(95,109,419,95)` | 179 |
| 10 | `(18,259,594,45)` | 180 |
| 11 | `(222,212,162,84)` | 181 |
| 12 | `(123,293,354,35)` | 182 |
| 13 | `(72,293,456,39)` | 183 |
| 14 | `(22,366,585,71)` | 184 |

#### Interaction regions

Install in ascending row order and stop at the first hit. Group 0..7 selects a
piece. Group 10..16 selects overlay 0..6 before §11's fallback and ownership
checks. Rows 6/7 precede identical rows 8/9, so the raw group at those two regions
is 15. If overlay 5 is already present and overlay 4 absent, the callback changes
that raw choice to overlay 4.

| Row/ID | Rectangle | Group |
| --- | --- | --- |
| 0 | `(1,2,22,412)` | 7 |
| 1 | `(2,404,54,72)` | 7 |
| 2 | `(604,1,35,407)` | 7 |
| 3 | `(572,405,68,73)` | 7 |
| 4 | `(541,365,65,70)` | 16 |
| 5 | `(28,366,71,68)` | 16 |
| 6 | `(71,292,100,39)` | 15 |
| 7 | `(429,292,99,39)` | 15 |
| 8 | `(71,292,100,39)` | 14 |
| 9 | `(429,292,99,39)` | 14 |
| 10 | `(284,258,39,57)` | 6 |
| 11 | `(37,18,45,288)` | 5 |
| 12 | `(147,18,44,292)` | 5 |
| 13 | `(413,20,49,289)` | 5 |
| 14 | `(523,20,47,286)` | 5 |
| 15 | `(14,3,207,12)` | 5 |
| 16 | `(383,1,211,14)` | 5 |
| 17 | `(227,212,14,81)` | 13 |
| 18 | `(367,213,11,82)` | 13 |
| 19 | `(190,17,21,282)` | 4 |
| 20 | `(389,18,26,281)` | 4 |
| 21 | `(222,36,150,85)` | 4 |
| 22 | `(17,276,193,25)` | 12 |
| 23 | `(400,263,208,38)` | 12 |
| 24 | `(94,117,54,71)` | 11 |
| 25 | `(267,108,70,94)` | 11 |
| 26 | `(460,117,51,73)` | 11 |
| 27 | `(11,2,147,283)` | 3 |
| 28 | `(450,2,167,283)` | 3 |
| 29 | `(20,316,184,34)` | 10 |
| 30 | `(403,317,204,35)` | 10 |
| 31 | `(222,303,168,175)` | 2 |
| 32 | `(19,291,592,189)` | 1 |
| 33 | `(212,1,179,295)` | 0 |

### Original throne-upgrade scene preparation

Create windows and compose current images; return on either failure. Allocate
640×480 backing port and copy the whole current scene into it. Construct temporary
message font face 1 height 24. The following operations retain earlier scene,
resource and input effects on failure.

### Original §13. Conformance cases and input profiles

Compare ordered calls, mutations, random draws, selected records and retained
prefixes, as well as final return values. Normal media availability and a
supplied retained receiver are separate fixture inputs. A successful absent-file
return does not validate the successful-playback path.

| Case | Required observation |
| --- | --- |
| Military actor has no cities/units | City divisor becomes 1; result 1 before capability scans |
| Peers have available military types but no owned units | Capability comparison still counts the types; no alive filter |
| Actor has two Palace cities | Walls check uses the last one; progress qualification separately stops at the first Palace |
| Military treaty Alliance versus War | Alliance alone does not block result 7; War does |
| Science actor strictly leads all peers | Result 7 before science-rate checks; equal stored advance count counts as a peer |
| Economic distribution changes taxes | Advice uses the changed total; disordered cities contribute no tax; distribution effects persist |
| Foreign actor differs from local human | Contacts/War/Alliance use actor; Embassy and wonders use the live observer |
| Foreign wonder 24 callback changes local human | Wonder 9 query uses the new observer |
| Domestic mood-refresh request set | Clear it before calculation; retain that clear even with no owned cities |
| Chapter begins with military and later repeats military | First response can repeat it; a second accepted response with that category is skipped |
| Rejected opinion, then `#, end` | Skip `end` on that line; after an accepted opinion the same directive ends the chapter |
| `r5, f4,20,30, caption`, draw 6 versus 5 | Six continues on the line; five rejects it and clears accepted; both consume one inclusive 1..10 draw |
| Initial caption starts`*` versus response caption starts`*` | Initial text retained; response caption empty |
| EOF after one response | Return 0 after Close; already published chapter fields remain |
| Missing media versus failed panel capture | Missing media returns 0; panel-capture early return still permits initialization 1 |
| Caption clearing with retained current chapter outside 0..2 | Font lifetime occurs; no advice fill/reset/update before early return |
| Ordinary council Close or anarchy advisor callback | Busy remains 1; only the ordinary continuation clears it |
| Difficulty 0, stages−1/0/1/2 | Thresholds 1/1/4/11; difficulty 5,stage 3 gives 7 |
| Turn equals acceleration×20+1 | No score/progress check; first permitted turn is strictly later |
| Throne preference disabled | Eligible progress increments once, with no display |
| Progress 37 qualifies | Store 38 before the visual-limit decision; do not display |
| Signed progress 32767 qualifies | Store−32768 under defined scalar wrap; subsequent signed limit test can pass |
| Pieces 0..6 complete, piece 7 incomplete, overlays all present | Wrapper declines upgrade although constructor all-complete is false |
| Completed room, point(80,300) | Raw group 15 from row 6; overlay 5 when absent, fallback 4 if 5 owned, rejection if both owned |
| Point on region right/bottom edge | That rectangle does not match; later overlapping regions can still match |
| Selection click on hidden/complete piece | Region can hit, but callback rejects; temporary selection may already have changed |
| Modal returns without a selecting click | Consume supplied retained selection or stop; do not fabricate an accepted choice |
| Last image composition fails after mutation | Upgrade remains stored; final success-only sound/reveal/modal omitted |

Nominal council files use the five recognized categories, fitting captions and
chapter keys, backed frame ranges and terminating response records. Nominal
throne inputs have eight piece levels 0..4 and seven overlay flags, valid resource
geometry and text ending at the next section marker. These conditions do not
assert that original stack locals were initialized: the missing chapter/frame
callback/selection producers are separately recorded below.

A declared restrictive profile may reject malformed files, invalid indices,
unsupported frame ranges or unreasonable loop bounds before entering their
consumers. A compatibility profile retains defined scalar conversion, priority,
partial effects and accepted syntax. At stage `INT32_MAX`, the threshold loop's
increment wraps and its signed termination comparison never succeeds; an explicit
safe stop/loop budget is allowed instead of a fabricated numeric result. Every
out-of-bounds overwrite is excluded in either profile, even if its destination
happens to be mapped or is another field of the same original object.

### Original council object construction

Construct base window, video, prompt with P04's council options, resource owner,
then six buttons in member order. Set caption offset 176 and anarchy 0; publish
this council as active. The original constructor does not initialize current
chapter or every video callback field. Independent BF-067/BF-298 initialization
is a repair; unknown consumed inputs follow §1. Destruction clears the active
reference first, then destroys buttons 5..0, resource owner, prompt, video and
base window in reverse construction order.

### Original council initialization layout

Initialization stores actor and queries P06's advance variant for that actor.
Let R be shared application row height, S spacing and B border width. Create
window at 0,0 with width `6S+450`, height `2R+B+252`, parent main window and the
application art palette. Set caption height R, publish prompt-button font,
set up controls, draw background and update. Calculate five advice values in
category order 0..4, re-reading receiver actor before every child.

### Original throne upgrade staging

not an invented section terminator. Draw message in rectangle
`(100,120,540,450)` color 0, mode 0; offset 2,2 and draw color 255. Close the stream
and restore whole-scene rectangle. Aggregate overflow is excluded.

Draw Continue, set phase 0, show outer, flush, assign local palette, show child,
flush, discard queued input and capture mouse on child. Run the child modal loop,
installing network idle only for game mode≥3; clear idle after return and release
capture. Then compose selection; failure destroys temporary font and returns
without the later palette/hide sequence.

Read `ADDTOTHRONE` with the same message concatenation/drawing/close behavior,
then draw Continue. Set phase 1. Reveal the whole child surface with block
parameters `(8,4)`, present, discard input, capture, run modal with the same idle
handling and release. Play effect 13 `(1,0,0)`; restore backed-up scene to child,
pump messages, reveal `(8,3)`, present and flush. **Only now mutate persistent
state**, using the current actor and selection:

### Original throne post-commit staging

Compose images again. On success play effect 13, reveal `(4,1)`, draw Continue,
present, set phase 0, capture and run a third modal loop (same idle handling),
release capture, hide child and flush. Whether or not this final composition
succeeds, restore outer initializer palette, hide outer, flush and destroy
message font. The upgrade mutation remains if final image loading fails.

### Original throne hit testing

Click captures the published room and actor. Phase 0 clears child modal loop and
returns without hit testing. In other phases query P06's interaction regions,
writing the first matching group to selected piece. Test regions in ascending
installation order, with left/top included and right/bottom excluded. A miss sets selection 0, plays
rejection effect 94
`(1,0,0)` and returns. A hit leaves its selected group published even if rejected.

### Original throne key bindings

Key input in phase 0 clears loop only for signed internal key codes 208..210.
Other keys do nothing. In selection phase every key plays rejection effect 94;
there is no keyboard piece selection or keyboard cancel. Both callbacks read
the published room; absence requires a stop rather than acting on a fabricated
window.
