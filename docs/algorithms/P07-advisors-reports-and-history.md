# P07: Advisors, reports and history

## Production contract scope

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in P07 |
| --- | --- |
| Gameplay | Report facts/formulas/privacy, identified choices, chat/negotiation meaning and layout-independent preparation. |
| Original files/content | P04 authored resources, F06 history/Hall records and N02 chat/Parley values. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Report layout/sorting/navigation, row-derived effects and private text/history buffers. |

The explicit presentation contract below selects the required definitions;
its detailed original procedures remain reference. The table above adds no
requirement to reproduce an original UI or private runtime representation.

## Required presentation contract

The product target is the same game, with a broadly familiar Civilization II
feel and support for original configuration and scenario content. It is **not
an exact recreation of the original interface**. Apply [X01's presentation
policy](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy).

The requirements are the semantic obligations and external-data definitions
explicitly adopted below. All numbered sections and appendices under
[Original behavior reference](#original-behavior-reference) describe the old
interface. Their imperative wording, including historical “required” and
“conformance” lists, does not make their UI procedures mandatory. An adopted
formula or file definition does not adopt the surrounding prompt, callback,
storage, drawing or navigation algorithm.

Design screens, navigation, controls, prompts, formatting and rendering freely.
No equivalence proof against the original UI state machine is needed. Preserve
game rules, meaningful information, privacy, available game decisions, target
identity, accepted game/file/network effects and actual protocol deadlines.
Local draft edits are not committed game commands. Cancellation may discard
them; it cannot undo an already accepted game operation unless its owner permits
that. Keep source option identities separate from their displayed order.

Original allocation sizes, pool ordinals, shared scratch buffers, control IDs,
modal flags, retained UI state and native event traces are comparison details.
Production code must not emulate them merely to satisfy a historical snapshot.
Optional original-comparison tools/tests remain outside production dependencies.
File/wire/resource encodings are translated by production adapters, with safe
storage and documented diagnostics for unsupported or invalid input.

## Report information and actions

Provide city status/attitudes, top cities, military/trade/science/intelligence,
wonders, demographics, history, score/rating/Hall of Fame, taxes and spaceship
information, plus diplomacy/chat decisions. These functions may be combined,
searched, sorted or presented as new panels/charts. Exact report pages, row
ordinals, history buffers, text assembly, navigation and modal transitions are
optional. Identity, privacy and game meaning are binding.

The following reference definitions are adopted for **information and game
operations only**, not their evaluation inside a draw loop:

| Definition | Required meaning / owner |
| --- | --- |
| §§3–11 city, top-city, military, trade, science, embassy, foreign and wonder reports | Rule/state values, numerical ranking formulas, intelligence/disclosure predicates and identified cities, units, technologies and civilizations; C03/C07/C08/V03/V04 own invoked mechanics |
| §12 demographics | Accumulation and thirteen numerical metrics, rounding, ranking and disclosure; correct units may replace retained/wrong suffix text |
| §§13–14 history | Actual loss/history records, dates, civilization identity and permitted disclosure; graph geometry and row grouping are optional |
| §§15–19 score, rating, Hall, tax and ship | V08/F06 scores and persistent records; V02 tax preview/commit and V07 ship statistics/actions. Keep pending versus awarded points and scenario/cheat meaning |
| §20 science information | Advance identities, possession/ranking and permitted disclosure; herald artwork and cache layout are optional |
| §§21–24 chat and Parley | Recipient visibility/routing, private versus public text, identified offer items, prices, negotiated state and accepted responses under N02/V04/V05; no required panes, row order, modal flags or edit history representation |

Stock historian topics, flavor wording and presentation order may change;
authored conditional content retains its declared interpretation. Preserve
authored text and allow all eligible decisions, including meaningful
automatic defaults at their game/protocol boundary. Choosing a sorted display
row resolves its stable item identity; sorting never changes a negotiated offer
or automatic game selection. Resolve pending operations after changed targets,
remote results and real deadlines. Local edits can remain drafts until accepted.

## Report preparation independent of layout

Rendering is read-only. Perform required calculation once per semantic report
opening/refresh against the current admitted state, and publish the resulting
observer-filtered model. Subsequent repaint, pagination, font change or resizing
does not recalculate or mutate simulation. Preserve actual calculation ordering
and arithmetic under C03/F01 where dependent; UI callbacks add no interleavings.

- City Status uses stored yields and does not introduce C03 recalculation.
- Attitude consumes its mood-refresh request once at preparation. If requested
  and the actor's government is Republic or Democracy, run C03(city,1) for **all**
  live owned cities in ascending slot order, not just a visible page.
- Trade retains its stored-income/upkeep calculations without C03. Prepare the
  research-turn estimate whenever the report is prepared, independently of row
  visibility. Floor the local estimate's cost/science as specified in §7; the
  presentation cache is local, not a new mutation of simulation science.
- Demographics runs the §12 city accumulation/C03 pass once per preparation over
  all admitted live cities, retaining its meaningful calculation effects.
- Civilization Score uses V08's explicit preparation: refresh every live owned
  city in ascending slot order for ordinary scoring; objective scoring skips
  that pass. Scored and direct-rating/Hall callers keep their separate contracts.

These are documented repairs of layout/paint-dependent effects. Do not retain
classic viewport/font inputs to select a city prefix. Other report formulas and
game operations above remain unchanged. Random flavor choices and authored
conditional content are resolved for an interaction, not on each repaint.

### Open specification gap: embassy and foreign-report appraisal

The attitude preparation in §§9–10 names a V04 “contact appraisal” whose inputs,
effects and resulting attitude are not defined by V04. V04 §2 defines stored
attitude queries and signed display categories; A08 §4 defines a separate,
mutating diplomatic-stance evaluation. Neither is specified as this report's
appraisal. Before implementing that preparation, establish its meaning through
the behavioral contract or independent original-game observations. Do not
silently invoke the full A08 evaluation or assume a stored-attitude lookup is
equivalent. This gap concerns report preparation, not the already specified
V05 emissary evaluation and greeting category.

## Original-file input contract

F03/P04 own resource parsing, substitution and original label meanings. Preserve
scenario/custom report text and named fame sections with their record values.
F06 owns Hall/history formats; N02 owns chat/Parley payloads and result codes.
Appendix C artwork records are source-asset definitions only when importing that
artwork. Screen placements and original report/private buffer layouts are not
file contracts. UI display errors, including the retained military-service
suffix, need not be reproduced as misinformation.

## Required verification

Test the adopted content definitions and semantic operations, including custom
content, observer restrictions, changed/deleted targets, accept/cancel, real
timeouts and remote responses where applicable. Verify that different layouts,
navigation paths, pagination, repaint counts and skipped cosmetics leave the
same accepted commands and gameplay outcomes. Compare resolved values and
identities, not UI buffers or callback traces. Qualitative play review checks
the familiar map/city/turn/advisor feel; pixel or original-navigation matching
is not an acceptance gate. Historical checks below are reference evidence,
not evidence that the new UI or a documented bug repair has been validated.

## Original behavior reference

The following record retains stable section anchors for research and existing
citations. Only the specific semantic rules and data definitions adopted above
are normative. Everything else is optional reference, including exact
user-facing sequences. Historical source links and harnesses remain provenance,
not implementation or test inputs. A missing UI design detail is a design
choice, not a specification gap; a missing game rule or external format is a gap.

### 1. Inputs, state and compatibility

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Nominal inputs are initialized game/rules/map state, valid indices, supported
assets/text and semantic report operations. External gameplay/network events,
relevant clock samples and file results remain inputs. Logical classic layout
values are supplied only where they gate game calculations; optional original
pixel comparisons use the asset/font/native inputs recorded in Appendix E.

Use [F01](F01-determinism-and-arithmetic.md) wrapping arithmetic, truncating
division, signed byte/word interpretation and arithmetic shifts where stated.
`half(x)` denotes arithmetic shift right1. `limit` denotes the specified F01
clamp helper, including reversed bounds. Unless captured explicitly, values are
read at their use after preceding calls. Report data is not globally snapshotted.
Cancellation, failure and reentry do not restore earlier calculations or flags.

Random choices and supplied-draw fixtures use F01’s sampling laws. Counts refer
to semantic choices, not raw generator calls; unused original samples are
optional. Gameplay-dependent ordering and callbacks remain required; purely
cosmetic randomness and native traces follow the presentation policy.

[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
excludes every out-of-bounds memory overwrite, even when mapped or reachable
from ordinary play. Reject, omit or stop before that write, retaining prior
valid effects. Unknown reads require established backing, supplied retained
inputs or a stop; do not invent zero/default values. A declared restrictive
profile may reject unsupported inputs earlier while preserving ordinary behavior.

Logical report state holds kind, actor/target, pending result, first item and
query-admission inputs. Shared state includes intelligence timeout/retry,
city-calculation scratch and ranking/history state. The primary/secondary report
identities matter to refresh routing below. Native windows, controls and fonts
are reference details; retain classic logical layout inputs only where they
admit simulation work, independently of the new viewport.

Refresh(selection) updates the second window only for selection12 and only when
its selection is12. Every other selection updates the primary window only on
exact selection equality. No match does nothing and retains the selector as the
original full return; a matched update returns its child result when consumed.
No refresh implicitly opens or switches a report.

### 2. Shared auxiliary window

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

An auxiliary report retains its semantic kind, actor/target, selection, paging
state needed by gameplay-dependent queries, pending result and active/closed
state. Opening/closing must preserve required caller state, N03 timeout/idle
processing and P06/report refresh effects. Native parents, frame widgets,
modal stacks, font creation and control disposal are reference procedures.
View parameters used to admit simulation work (Attitude, Trade, Score) remain
declared compatibility inputs independent of the redesigned viewport.

Original presentation procedures: [Appendix E](#appendix-e-original-presentation-reference).

### 3. City Status and Attitude

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

#### Shared heading, paging and city selection

Capture actor before report preparation and relevant external events. Show the
title, government-prefix label `limit(government−1,0,4)+328`, label140 and
civilization name, then ruler title/name and year. For the logical classic paging
calculation retain clientY and captured heading-font height: start y=clientY+2,
advance by the captured height for each of the three heading lines, then set
afterHeading=y and
firstRowY=afterHeading+2; bottom=clientY+372. These are compatibility admission
inputs, not constraints on the new fonts or window dimensions.

City Status title334 uses logical rowHeight24; Attitude title337 uses32.
Publish available height=bottom−firstRowY and visible
rows=`limit(availableHeight/rowHeight,1,99)`. Count live city slots with nonzero
identity and signed owner==captured actor. Page count is
`limit((rows−1+count)/rows,1,99)`; clamp first item to
`limit(first,0,limit(count−1,0,999))`.

Scan live city slots again in ascending index. Increment qualifying ordinal
before drawing; admit its pre-increment value only in `[first,first+rows)`.
No alphabetic sorting or unconditional recalculation is inserted. Paging/scroll
sets the supplied first item and refreshes. Native scrollbar creation and updates
are reference behavior.

A semantic row selection supplies an ordinal in the displayed page. Add live
first item; rescan city slots with identity and owner equal to **live report
actor**, then open the matching city through P06. Original pixel-to-row conversion
is in Appendix E and does not constrain redesigned hit regions.

#### City Status row

Show live city name and stored signed Food, Shields and net Trade; do not call
C03 for these values. A negative live production selects improvement name and
unsigned cost; nonnegative selects unit name and signed cost. Show shield stock
and full cost `live shieldBoxRows*captured cost` separated by a slash, not a
division or turns estimate. Read stock/box rows after composing production name.

#### Attitude redraw and citizen strip

Before capturing actor, capture the city-mood-recalculation request bit and
clear it in the live game word. After heading/paging, recalculate C03 `(city,1)`
only for a displayed row when the captured request is nonzero and captured
actor's **live** government is Republic or Democracy (government index>4). Offscreen owned cities are
not refreshed, and the request is consumed even if none are drawn.

Show city name and its mood information. Mark content/happy when unsigned unhappy
is0 and signed size−signed happy≤half(signed size); independently mark unhappy
when signed happy<signed unhappy, overriding the former indicator.

Publish shared specialist count from the worker-assignment count component.
Report Happy, Ordinary, Unhappy/Very Unhappy, then Specialists in P06's type order.
Ordinary count is recomputed as live size−(captured unhappy+live shared specialists
+captured happy); the specialist loop tests the live shared count. Retain external
events between semantic reads. Do not restore shared specialist count or add C03
work. Icon parity, art/style and spacing are in Appendix E.

### 4. Top Cities

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Display the ranked city identity, owner, mood information and wonders using the
ranking below. Do not introduce contact/visibility filtering or a new C03
recalculation. Citizen information uses the shared specialist-count publication
in §3; native art positions and frames are optional.

Complete ranking before any presentation callback. Initialize five city IDs and
scores to−1. Scan every live nonzero city identity, irrespective of actor,
visibility or owner. Score=signed size+signed happy−signed unhappy, plus10 for
each of28 wonder locations equal to that city. Insert at the first slot whose
score is **strictly less**; shift lower slots downward, discard the sixth.
Equal scores favor earlier city indices; scores≤−1 never replace an empty slot.

Original presentation procedures: [Appendix E](#appendix-e-original-presentation-reference).

### 5. Historian report

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

F04 owns scheduling and N02 remote invocation. Construct Prompt before opening
GAME's `HISTORIANS`. On successful section open, read one line (even if EOF),
parse its decimal count; zero skips the report. A nonzero count draws inclusive
range(0,count−1), then reads lines0 through the selected index, inclusive, and
copies the resulting shared line to substitution1. Draw range(0,4) as kind;
read that indexed `HISTORIES` line and copy shared text to substitution2, even
after failed/EOF reads. Load GAME `HISTORY`, append `^`, then initialize eight
totals to0. These choices determine report content; resource effects and callbacks remain
observable. Their samples need not advance simulation randomness.

Scan all live city slots and read identity even for kinds that do not use city
totals. For kind3, add signed size+signed happy−signed unhappy to signed owner's
total; for kind4 add signed size. An invalid owner's scratch write is excluded,
not a mutation of an adjacent original stack variable. Then scan civilizations
0–7: kind0 adds gold, kind1 unsigned unit quantity, kind2 counts each advance0–99
for which V03 possession succeeds. No alive/contact filtering occurs in this
accumulation, and no city recalculation is performed.

If `HISTORYRANK` opens successfully, for ranks1–7 first read/copy that rank's
text, then choose an alive civilization1–7 with maximum nonnegative total.
Initialize best0/found−1 and replace on `best<=total`; thus the **last equal**
civilization wins. If none, break after consuming/copying the rank line. Set
selected total−1 before composing text: `^`, rank, P04 sentence separator,
label372, space, rank text, space, label217; language2 adds colon, otherwise
space+label140+space; append civilization name. Reveal this line only if selected
civilization is local, the local civilization has Contact with it, or difficulty
is Chieftain, in that short-circuit order. Unrevealed entries still consume ranks
and are marked used. Run modal0 even when the ranking loop found no candidate.

Destroy Prompt normally on every ordinary exit; original exception cleanup
owns that constructed Prompt only. Failed outer section/count or failed rank
section avoids the modal. There is no extra explicit rank-stream close in this
parent. Overlong rank text crossing its logical scratch capacity is excluded;
do not adopt truncation or adjacent overwrite as a classic rank title.

### 6. Defense Minister

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Initialize actor and mode0. Toggling maps0→1 and any nonzero→0, retaining scroll.
Show title335 and label344 for mode0 or345 otherwise, with §3's ruler/civilization
information. This report does not install a city-row action.

Construct eligibility for unit types0..61 in ascending order. In mode0 admit a
nonzero actor owned-unit count or cities-building-that-type count. In comparison
mode admit if any civilization1..7 has nonzero losses for that type and is actor
or passes the following intelligence predicate; no alive filter is added.

The predicate tests in order: local Embassy with other, local United Nations,
local Marco Polo, actor Marco Polo, actor United Nations. Stop at first true;
reload local civilization for successive queries, retaining captured actor.
Requery when presenting columns; eligibility is not a global snapshot.

For admitted mode0 types, present name and:

- Signed attack/defense, mounted-defense ability indicator, signed movement divided
  by movement multiplier; signed hit-points/10 and signed firepower.
- Nonzero owned and production counts with labels346/347.
- Ordered ability text: nonzero holds uses348 and signed holds; then Air to Air350
  when that ability is set, otherwise Air349 for air domain; then the present
  abilities in Appendix A order (351–363). Reset separator state per row and set
  it after each appended item. These descriptions do not create mechanical rules.

Comparison presents own losses, then civilizations1..7 except actor admitted by
the live predicate, with distinct ownership identification. Omit zero loss numbers
without changing eligibility. Native page sizes, geometry, colors and scrollbar
lifetimes are reference procedures; changing display layout cannot change counts.

Original presentation procedures: [Appendix E](#appendix-e-original-presentation-reference).

### 7. Trade Advisor and commodity search

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Offer Close and Supply Search for the live report actor. Native controls and
window geometry are reference presentation.

Commodity search constructs Prompt even if actor lacks Trade84; in that case
destroy and return. Otherwise repeatedly load `SUPPLYSEARCH`, append all16
commodity names in index order with values0..15, and run modal. A signed negative
choice ends; otherwise invoke C07's city commodity supply report. Its negative
result ends; a nonnegative result returns to the chooser. Parent load failure
is not an extra early return. Ordinary exits and application exception cleanup
destroy the constructed Prompt; no game calculation is undone.

Capture actor before report preparation. Show §3 heading with Trade title338.
For the gameplay-dependent turns-row admission, retain classic logical
row origin=afterHeading+4, rowHeight24, bottom=clientY+372 and
visibleRows=limit((bottom−rowOrigin)/24,1,99), independent of new layout.

Initialize39 building counts and all totals to zero. Scan live cities in index
order; identity nonzero and signed owner==captured actor qualifies. Increment
city count. For every building0..38, query C08 civilization upkeep first, and
only if nonzero query possession; a true result increments its count. After
all these calls, add stored tax/science totals only if the city's live Disorder
flag is clear. Do **not** call C03. Next scan building counts0..38; each nonzero
count increments building-kind count and adds count times a **fresh** upkeep
query to total upkeep.

Total logical rows=max(cityCount+6,buildingKinds+3). Page count and first item
use §3's formulas. Admit indices from first through first+visibleRows−1 when
index<total. Row0 shows labels364/365. Other rows use ordinal=index−1 and
independent columns; physical row placement is in Appendix E.

| Left ordinal | Required content |
| --- | --- |
| 0..cityCount−1 | Rescan live owned cities to that ordinal; name and, if live Disorder clear, stored tax/science, otherwise label134. |
| cityCount | Blank |
| cityCount+1 | label135 and total upkeep |
| cityCount+2 | label136 and P04-formatted long tax total |
| cityCount+3 | label366 and science total |
| cityCount+4 | label137 and turns estimate with label44 |

The turns row queries V03 full research cost, replaces cost<2 by1, and **mutates
cached science to1 if science<2**;
then displays `(science−1+cost)/science`. Stockpiled beakers are not subtracted.
These operations occur only when that row is displayed.

For the right column, ordinals below buildingKinds select the nth nonzero count
in building-index order. Show count, name and parenthesized label132 with a fresh
upkeep query times captured count. Ordinal buildingKinds is blank;
buildingKinds+1 shows label135 and total upkeep. Other right rows have no content.
Scroll stores the supplied first item and refreshes. Native scrollbar behavior
is reference material; new layout must not change the turns-row mutation admission.

### 8. Science Advisor and shared technology grid

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Offer Research through V03 goal chooser `(live actor,1,0)`. Capture actor before
report preparation; show title339 and §3's ruler/civilization information. If the
live research advance is nonnegative, show label236 and its freshly reread name.
Query V03 full research cost before reading current beakers and present progress.

Query full cost again, then scan live owned cities and sum stored signed science,
without C03 and without a Disorder test. When total!=0, show labels368/369 and
`(total−1+cost)/total`. Do not floor either value to1 or subtract beaker stock.
Signed division faults retain preceding effects.

Science and Intelligence (§9) list possessed advances0..99, querying V03 in index
order. Show authored names and category/epoch identities. Science distinguishes
first discoverer==captured actor; Intelligence distinguishes advances the local
civilization does **not** possess. No hidden-knowledge filter is added beyond the
owning report's admission. Native three-column layout/paging is in Appendix E.

Selecting an advance maps the current semantic list ordinal to its advance ID:
Science reloads report actor for each possession query; Intelligence captures
target for its scan. Open P08 advance help for the match. Science then returns;
Intelligence clears its pending modal loop and sets shared retry1. New scrolling
or display sorting must preserve the selected advance's identity.

Original presentation procedures: [Appendix E](#appendix-e-original-presentation-reference).

### 9. Embassy Intelligence

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Open an intelligence interaction, then store actor and target. Offer Close and
Cities. Repeatedly reset retry0, install intelligence idle for mode≥3, set timeout
ticks and await a result; clear idle and repeat while live retry is nonzero.
Clear selection after the loop. Native controls and geometry are in Appendix E.

Cities constructs Prompt, captures target, puts target civilization name in
substitution0 and loads `INTELLCITY`. Scan live target-owned cities in index
order, identity required. Option text is name, with parenthesized label153 if
Palace exists. If production≤−39, append parenthesized label244,space,production
improvement name (the implementation rechecks sign and value after callbacks).
Option value is city ID. Disable parent input, run modal, **discard its choice**,
reenable input using fresh parent state, clear parent loop and set retry1.
Destroy Prompt. This button never opens the selected city window itself.

Capture actor and target before preparation. Present title336, ruler title/name,
label140, civilization name and parenthesized V04 attitude. Run contact appraisal
after name composition and use its resulting category. Treaty suffix priority:
War144, Alliance141, Peace142, Ceasefire143, otherwise none. Append trait labels
in order: aggression+1/−1→146/147, civilization+1/−1→150/151,
expansion+1/−1→148/149; other values append nothing.

Show government152, then find the first target-owned live city with Palace for
capital153 (C01/P04 no-city text for−1). Show treasury154; difficulty<2 also shows
military count155 with label25. Show research236, using label14 for a negative
target advance. Queries remain ordered and live; geometry/fonts are optional.

For each live civilization1..7 other than target, require target's Contact.
Choose relationship text in the same priority order; War and Peace first
prepend label156+space to shared line, then uppercase the relationship label in
scratch. With no special treaty use157. Append space,label123,space,ruler title,
space,ruler name,space,label140,space,civilization name. Center in that other's
label color and advance detail height. After all entries advance6 and draw §8's
Intelligence technology grid for captured target. All relation/technology queries
remain live at their stated positions; there is no extra secrecy filter here.

### 10. Foreign Minister chooser

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Construct Prompt. If mode≥3 and the multiplayer diplomacy window is open,
bring that window forward, destroy Prompt and return. Otherwise repeat the
following chooser until an exit action. Set substitution0 to power-rank label
`limit(actor.powerRank,1,7)+227` and1 to reputation label
`limit(actor.reputation,0,7)+220`. Load `REPORTFOREIGN` in single-player mode0,
else `REPORTFOREIGNMULTI`, with custom row rendering and Help75.

Capture `hasIntelligence = actor has United Nations OR Marco Polo`;
`allContacts = United Nations location != −1 OR actor has Marco Polo OR scoreLocked`.
Scan others1..7, skipping actor and dead civilizations. A human contact requires
both human membership and (signed multiplayer option word nonzero OR difficulty0).
Admit allContacts OR human contact OR directed Contact. Compose ruler title/name,
label140,civilization name, then run V04 contact appraisal and append parenthesized
attitude. Treaty suffix uses §9's priority. With neither Embassy nor captured
hasIntelligence append No Embassy145; otherwise append treasury and, on
difficulty<2, military count plus label25. Options retain civilization ID.
No eligible options shows `NOFOREIGN`, destroys Prompt and returns.

The optional city illustration uses that civilization's city maximizing signed
size+200 if Palace+100 if live production is Palace, starting best0 with strict
greater and earlier ties. Its artwork, coordinate offsets and native row return
are reference material in Appendix E; it adds no city selection or diplomacy effect.

Run chooser and retain its full result. A special prompt action flag forces
action2; otherwise use its action field. Action0 exits. Actions:

- **1 Intelligence:** if no live Embassy, no captured hasIntelligence and no
  score lock, show `NOINTEL`; otherwise open §9. Repeat chooser.
- **3 Attitude:** set substitution0 to selected ruler/civilization text; load
  `ATTITUDEMULTI`. On success use three columns and nine attitudes0..8; initial
  selection is0 for zero stored attitude, otherwise V04 category. A signed
  nonnegative choice maps through `[0,5,17,31,50,68,82,95,100]`, stores actor's
  directed attitude toward selection, then sends N02 attitude notification to
  all peers. Repeat chooser. Out-of-domain choices require recovered read
  backing; an out-of-range destination write is excluded.
- **2 Contact:** scenario prohibition may show `PEACENOBETRAY`. Otherwise use
  the ordered handshake below. Destroy Prompt and exit after this action,
  including branches that perform no dialogue.

The exact outer condition for the human
handshake branch is `(!human(actor) OR human(target)) AND
(!human(actor) OR mode≥3)`. In that branch only **both human and mode≥3** starts
the network request; its other combinations do nothing. The outer condition's
false branch publishes V04 contact treaty and invokes local diplomacy
`(actor,target,−1,−1,1)`.

For the admitted two-human request call [V05's request/wait continuation](V05-diplomatic-negotiation.md#request-and-wait),
using its explicitly specified **Foreign Minister** variant: publish Contact and
RecentContact first, retain stale cancellations, and omit the immediate handoff
check after `PARLEYWAITING`. Resolve each live recipient through N01's
civilization-to-player mapping. A vanished slot's original retained read requires
known backing or a stop, not a guessed network recipient. V05 owns both accepted
and failed audience continuations; N02 owns encoding, delivery and receipt.

### 11. Wonders of the World

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Present existing wonders0..27 whose location!=−1, including destroyed negative
locations, without actor/contact filtering. Group by era `wonder/7`, each nonempty
group contributing one heading and its wonder rows. No wonders produces title340
and label293. For each presented era heading read that indexed GAME `WONDERS`
line; failed reads omit the heading text but do not remove the group's entries.

Wonder text prefixes label215 and a space only for language0 and (era0 or
wonder19 or20). Append building name(wonder+39). A negative location adds
parenthesized label172 and owner0. Otherwise append label173 for era0/language0,
else216; then live city name and parenthesized owner adjective, reloading owner
after text helpers. Do not add a city identity/contact test. New paging, icons,
frames and fonts may differ; required identities, status and authored text remain.

Original presentation procedures: [Appendix E](#appendix-e-original-presentation-reference).

### 12. Demographics

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Open report/resource9 modal600×400 with standard Close and §2 lifecycle. Redraw
captures actor first. Initialize eight-entry totals to zero, except `citizens=1`
for **every** civilization. Count map tiles by territory owner, accepting owners
0..7, irrespective of terrain/city visibility. Scan all live nonzero cities in
index order; capture signed owner **before** C03 `(city,1)` and charge that
owner's totals using subsequent live city/scratch values:

- citizens += signed size;
- happiness += size+full happy−full unhappy;
- income += 2*luxuryTotal+taxTotal;
- foodSurplus += foodYield−2*size;
- productivity += shieldYield+tradeYield+foodYield; production += shieldYield;
- force shared pollution divisor to1 if<2; pollution amount is
  `shieldYield/divisor−20+(size*populationPollutionFactor)/4`; add only if>0;
- health += size for Granary OR active Pyramids, again for Aqueduct, again for
  Sewer; sanitation += size for Library and again for University. Each size is
  live after its query. The Granary/Pyramids condition short-circuits.

There is no owner-alive filter, no all-city snapshot, and no restoration of C03
scratch or the normalized divisor. Invalid-owner writes are excluded bugs.
After accumulation show the adjective only for language0, then label342.
Compute13 rows, each recomputing values for civilizations1..7 even if dead. All

arithmetic is wrapped signed32 with F01 division, except explicitly stored
unsigned counters. Let C=citizens,H=health,S=sanitation,I=income,N=ordinary
advance counter from V03. Read these live at each stated operation.

| Row / label | Value and ordered side effects |
| --- | --- |
| 0 Approval373 | 50*happiness / (C<2?1:C) |
| 1 Population374 | V02 total civilization population |
| 2 GNP375 | I |
| 3 Manufacturing376 | production |
| 4 Land377 | tile count |
| 5 Literacy378 | `(2*C+S)*2/C`; double successively for Alphabet1, Writing88, Literacy43, University85; clamp0..100 |
| 6 Disease379 | `50*C/(H+C)`; halve for Medicine50, halve again for Cure for Cancer27; replace H with `1800/(value+20)` |
| 7 Pollution380 | pollution; mutate H -= `10*pollution/C` |
| 8 Life expectancy381 | clamp(H,20,99) |
| 9 Family size382 | `40*foodSurplus/C+20`; actor displays quotient and signed remainder around a decimal point; then divide **every** civilization's ranking value by10 |
| 10 Military service383 | `10*militaryUnitCount/C` |
| 11 Annual income384 | if `I < 30000/(N+1)`, use `N*I/C`; otherwise `(I/C)*N` (different truncation/wrap order) |
| 12 Productivity385 | `10*productivity/C` |

Actor value uses P04 long
number formatting except Population's population formatter and Family size's
specified decimal composition. Suffixes: rows0/5/6 `%`; GNP space,label392,
space,`$`; Land `,000 `+label387; Pollution `0 `+label388; Annual income `$ `+
label393; Manufacturing space,label386; Life expectancy and Military service
space,label389; Family size space,label391. Others no suffix.

Rank starts1,maximum−9999,leader=actor. Disease/Pollution pass negative y to
request smaller-is-better. Negate actor's value first, then for each alive
other1..7 negate its value if not actor. Increment rank for every strict value
greater than actor; update leader only on strict greater than maximum, so the
first tied leader wins. This mutates the supplied row array. For display,
negate y if<1 and maximum if<1. Rank label is rank+393.

If leader differs from actor, disclose leader name/value only when, in order,
local Embassy with leader OR local UN OR local Marco Polo OR scoreLocked OR
actor Marco Polo OR actor UN. Present: `(`,name,`: `,maximum,suffix,`)`. Rank suffixes in row order are
`%`, `0000`, `M$`, label386, `,000`, `%`, `%`, `0 `+label388, label390,
empty, **the retained label390 scratch from row8**, `$`, empty. Military service
therefore uses the life-expectancy rank suffix. Do not normalize this to its
own displayed unit label. Actor0's ranking scratch was not initialized by the
1..7 pass: require supplied input or reject that off-nominal call.

### 13. Casualty timeline

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

N02/U06 own combat-history records and mutation. This report uses the second
auxiliary window, selection12/resource2. If already open, opening only updates
and shows it, **retaining its previous actor and scroll**. Otherwise setup
nonmodal600×400, store actor/mode0, standard Close and redraw callback, then
update/show. Shared setup still clears selection on the primary window.

Capture actor before preparation and show §6's Defense heading with label872.
The history-list state retains first/visibleRows for ordinal selection; native
row height, click offsets and text clipping are reference geometry.

Compute count=0. Capture head, compare live tail>captured head. If true, count=
fresh tail−fresh head. Otherwise capture head again and **repeat tail>head**;
if now true set count300. This is not the usual wrapped-ring length formula.
With stable tail≤head the reported count is zero. If count=0, retain previous
page/first fields; scrollbar still updates below.

For nonzero count update page/first as §3, then index=live tail−1−first; if
negative add300 **once**. Set displayed=1. Loop while all hold, in order:
`displayed<=live historyCount`, `displayed−1<first+visibleRows`, and
`live head!=0 OR index<live tail`. Notice the second comparison can admit more
than visibleRows rows when first>0. Do not silently repair it. For each row:

1. Read history signed unit type for preview identity, then reload it for the name.
2. Compose history year.
3. Compose signed x,y; reload y then x for C01 nearest-city with unrestricted
   selectors. If found append its live name as the approximate location.
4. Show stored opponent name and identity.
5. Increment displayed, decrement index; if negative set299, not add300.

The historical record can change between type/name/year/coordinate reads.
Scroll stores supplied first and refreshes. A semantic row activation uses
index=live tail for live actor−1−live first−row; if negative add300 once. Return
if fresh tail==index, or fresh head==0 AND fresh historyCount<299 AND index≥fresh
tail. Otherwise read record y then x, reloading report actor for each, and center
map0. Preserve this identity mapping independently of the optional native
scrollbar and double-click hit rectangles.

### 14. Power-history graph

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

A01 supplies 150 power-history samples; F04 supplies calendar conversion.
Use sample count `limit(turn/divisor,0,150)`, with divisor2 for objective scenarios
and4 otherwise. Include civilizations currently or previously alive; do not
replace history with current strength or omit an extinct civilization's series.
Maximum-reference selection scans all civilizations1–7 and admitted samples,
starting at50, even when a civilization is not displayed. Graph geometry, axis
spacing, polyline passes and native modal objects may differ. This report does
not itself install an intelligence timeout.

Original presentation procedures: [Appendix E](#appendix-e-original-presentation-reference).

### 15. Civilization Score

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

V08 owns numeric score calculations and the Scored/objective branches. Capture
actor before report preparation. If Scored, present label457 and return without
score calculation. Objective mode follows the separate branch below.

For ordinary city-query admission, retain the classic logical clientX/Y/width,
heading/detail font heights and top/left inputs. The original starts title at
clientY+2, advances its heading height, places ruler/civilization/year at the
detail height and advances that height+4 to establish top; left=clientX+2.
These inputs are compatibility state independent of the actual new viewport.

#### Ordinary citizen and wonder layout

The city-recalculation admission calculation below affects V08 score and remains
required. Run it with the declared classic logical view inputs independently of
new font metrics, window size or actual drawing. Retain the inputs through a
compatibility view record/adapter, including the F06 values where applicable;
the supplied operation inputs must cover any unsaved font/layout values used.
The new renderer may show all citizens/cities, but it must not thereby recalculate
additional cities or use a new canonical viewport to choose a different prefix.
Other raster/layout details in this section remain optional reference behavior.

Start logical x=left,y=top,parity0,row0. Scan live owned cities, identity required.
Call C03 `(city,1)`. Capture full happy/unhappy and content=
signed size−happy−unhappy−specialists−excessUnhappy. Loop citizen index against
live signed size, using captured boundaries then live excess-unhappy and specialist
lookup for citizen information. Advance parity even for specialists, logical x by20.
If live clientX+width−29<x, increment row, set x=left+10*(row mod2), y+=18.
Stop the entire city scan if signed row≥7 OR **absolute y>240**. Thus only the
visited city prefix is recalculated. If final citizen wrapped, subtract18 from y;
set top=y+34 and invoke V08 score calculation. Native art and drawing are optional.

Show population points, wonder points and eligible wonder identities. The optional
original wonder-icon cutoff must not limit V08's numeric wonder query. Show arrived
points when their freshly tested value is nonzero, otherwise in-flight points
when nonzero; show pollution penalty, nonzero peace bonus only for turn≥200,
nonzero future-technology points, and barbarian adjustment with its sign. Labels
and stock composition are recorded in Appendix E; values remain V08 outputs.

#### Objective and final lines

Objective mode skips the ordinary city-recalculation pass and calls V08 scoring.
Show total objective value; configured civilization's adjective/value if nonzero;
actor adjective/local value when configured civilization is0 or differs from actor.
Ranked civilization is configured if nonzero else actor. Show its adjective,
threshold labels435,436,438,439 with settings4..7 and outcome label435+stored outcome.

At final composition recheck objective mode. When true, set shared conquest−1
and ordinary=scenario points. Show conquest408 only when ordinary<conquest,
otherwise ordinary407. Include Scenario418 and Cheated410 when their live flags
are set. Native measurement/draw passes, spacing and fonts are reference behavior;
the score publication and flags above remain required.

### 16. Fame/rating ladder

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

V08 owns percentage and rank selection. Capture actor and run its percentage/rank
publication before composing heading text. Present label412 with percentage and
labels413/414. Native setup, CD track selection and modal geometry are optional.

Choose GAME `MALEFAME` for leader gender0, otherwise `FEMALEFAME`. Open section,
then set substitution0 to ruler name **even if opening failed**. For rows0 through
captured rank inclusive: on successful open read/copy the next line, ignoring EOF
return; on failed open use `Error` for rows below rank and `%STRING0` for final.
P04-expand substitutions and present the title ladder through the captured rank.
The highest title is distinguished from earlier entries. Original font sizes and
bottom-up layout are reference details. An oversized template/substitution uses
safe storage/rejection; expanded storage is not the original stack layout.

### 17. Hall of Fame presentation

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

V08 owns load/validation, insertion, rank/calendar fields and the caller's reset
loop. Reload the table for each display; retain requested highlight and initialize
result0. Negative highlight offers Close only; nonnegative also offers Reset.
Reset sets result1 and completes this interaction; it does **not** clear/write
records itself. Run required idle/timeout processing and return live result.

Capture highlight before report preparation. Scan records0..5, skipping record5
unless highlight==5 and all negative scores. Each admitted record provides:

1. Choose gender fame section as §16, then publish stored ruler in substitution0.
   Read/substitute lines0 through **live stored rank**; each loop condition
   reloads rank. Failure uses `Error` except `%STRING0` on the final rank.
   Keep the last expanded string; a negative rank reuses prior record's retained
   output, or requires supplied incoming text for the first such record.
   Compose rank ordinal+`. `+expanded ruler title+space,label140,space,stored
   civilization+space+`(`+label443+space+record date+`)`. Distinguish the highlighted
   record without prescribing a color.
2. If ordinary record or stored localObjectives<0, label34:population; otherwise
   label434:localObjectives. Then two spaces,label444:score,space,`(`,difficulty
   name; append colon+label418 for scenario and colon+label410 for cheated;`)`.
3. Label445 and percentage.

Ordinary dates use stored
year: negative magnitude+space+BC label0; nonnegative language0 uses AD label1+
space+magnitude, other languages magnitude+space+AD. For scenario records with
negative stored calendar increment, compute
`calendar=wrap(turn*abs(increment)+start−1)`, month label420+abs(calendar%12),
space,abs(calendar/12), preserving signed truncation/remainder. No live game's
calendar replaces record fields.

### 18. Tax-rate window layout

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

V02 owns rate normalization, held-rate state, rate-change callbacks, preview
calculation and commit/close behavior. Present Tax, Science and Luxury choices
in0..10, their percentages, government/cap, preview income/upkeep and the full-
cost research-turn estimate. Preserve V02's warning consumption and previewScience
floor/mutation. Hold controls use the persistent hold flags. New widgets and
layout must invoke those semantic operations, including cancellation effects.

Original presentation procedures: [Appendix E](#appendix-e-original-presentation-reference).

### 19. Spaceship presentation

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

V07 owns construction/launch decisions and success calculation. Semantic states
are Incomplete0, Ready1, LaunchRequested2 and InFlight3. Publish the selected
civilization and call V07 calculation `(owner,1)` before presenting the statistics.
Show component counts and the values formatted below; raw/capped part counts
remain distinct, and over-capacity state is not silently normalized.

Offer Launch only for the local owner in Ready state after information completion;
accepted Launch sets LaunchRequested and ends the pending interaction. Exit
completes any in-progress information reveal before closing. In LaunchRequested,
Exit/skip ends the launch video, without independently performing the V07 launch
mutation. V07 executes launch at its declared continuation and handles the new
InFlight view. Cosmetic typing/blink/star timers can be replaced.

While waiting, use the network idle rule below. Retain pending result, caller
cleanup/publication and exactly-once launch behavior on completion, cancellation
or playback failure. Screen component assembly and native button/mouse lifetimes
are reference procedures.

Formatting first scans six **raw** counts for any count exceeding V07 capacity,
reloading owner for each capacity query. For parts0..5 append part label,colon,
fresh raw count, adding `*` only after part0 if any over-capacity part was found;
append newline after every part. Then append these lines in order:

| Content | Composition |
| --- | --- |
| Population | label34:raw habitation count followed literally by `0,000` |
| Support | label66:V07 support ratio`%` |
| Energy | label205:energy ratio`%` |
| Mass | label206:mass/10,comma,**reloaded** mass%10,`00 `,label209 |
| Structural completeness | label200:structural ratio`%`; if spaceship flag carries the V07 qualifier, append space,parenthesized label235 |
| Flight time | label207:flight/10,period,**reloaded** flight%10,space,label210 |

Separate every line with newline except after Flight. Success text is
label208,colon,space,success`%`. Publish both texts then pump. Label/rate and
string-buffer capacities are validation details in Appendix B; a new
implementation can allocate sufficient safe text storage rather than reproduce
an overwrite.

Network idle polls(1,0), then if no handoff signal compares signed wrapped elapsed
against1200; return when≤1200. Otherwise clear active **outer** loop, not the
embedded inner loop. The start timestamp is not reset here.

Original presentation procedures: [Appendix E](#appendix-e-original-presentation-reference).

### 20. Diplomatic herald artwork and science display

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

V05 owns the audience, actors and diplomacy outcomes. Graphical Diplomacy may
change the presenter's artwork but not its information or required science-cache
updates below. Preserve supported resource overrides and P10 media completion;
herald resource/window allocation, palette transitions and staging are optional.

#### Science ranking and cache

Herald art/font allocation is optional. The following ranking and cache mutations
remain required even when text disclosure is false.

Initialize six `(advance,score)` pairs to `(−1,−1)`. Cache whether text is visible:
local→other Embassy, otherwise local active United Nations, otherwise local
active Marco Polo, otherwise Chieftain. These are short-circuit ordered queries;
icons and text formatting below still run when this result is false. A local
civilization−1 treaty access requires the established retained backing described
in Appendix B; it is not automatically false.

Scan advance IDs0..99, querying ownership by the **live** other civilization.
For each owned advance increment owned count. A positive shared cached score
is reused. Otherwise clear V03's prerequisite-visit counter, perform its recursive
prerequisite-count operation, increment the counter once and cache that result
for this advance. The cache begins at zero and is not invalidated here when
rules or ownership change; this is a visit count, not a longest-path depth.
If pair0's score is at least the new score, discard this candidate. Otherwise
replace pair0 and insertion-sort all six pairs by increasing score, moving a
preceding pair only when its score is strictly greater. Do not replace this
with a general stable “best six” sort: replacement of the smallest pair and
later reverse traversal determine ties.

Clamp owned count to6. Zero yields an empty summary. Otherwise traverse ranked
slots5 down0, skipping negative advance IDs. Present category identity and, only
when cached intelligence permits text, the advance's authored name. P03 interprets
custom source icons; native herald allocation and load-failure traces are optional.

#### Science row text

Preserve the selected advance identity, signed category and the text disclosure
gate above. Original uppercasing, word splitting, two-line clipping, ellipses and
58-pixel bounds are optional reference procedures in Appendix E. A new renderer
may display the full permitted name. It must retain the prerequisite-score cache
calculation and must not expose the name when its intelligence gate is false.

Original presentation procedures: [Appendix E](#appendix-e-original-presentation-reference).

### 21. Chat visibility, messages and history

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

N01 owns session state, N02 packet receipt/send and V05 parley transactions.
The separate Chat and Parley windows share history/input storage. Their logical
state includes Chat-only mode4, open/closed, peer, per-civilization recipient and
mute flags, and Anonymous/MuteAnonymous/AllRecipients options. The control named
snoop in the original UI acts as a **receive suppression** flag here; it is
separate from V06's directed SpyChat treaty used for interception.

#### Opening and options

Chat-only opening captures allContacts=(raw UN location!=−1 OR local active
Marco Polo OR scoreLocked). Scan others1..7; exclude local,dead and nonhuman.
Admit when allContacts, otherwise when the signed multiplayer chat-contact option
is nonzero, otherwise when **candidate→local** has Contact. This differs from
Foreign Minister's option word/direction and has no Chieftain exception. No
candidate shows `NOFOREIGNHUMAN` and returns. Otherwise select canonical Chat
as current, clear missed-message counter then unread-message counter. Non-chat
opening selects canonical Parley, resets selected action and disclosure modes
as V05 specifies; ComposeAction rebuilds transaction state before capturing the
current window for opening.

If the selected window is already open, only show it; retain peer/mode/panes.
Otherwise set open,store peer, and except ComposeAction store requested mode and
its two requested panes. Build title,mark both cached panes invalid,update/show,
collapse history selection to its end and input selection to its existing end.
Then play effect `((unsigned map seed+civilization)&7)+83`, also for already-open
windows. No RNG draw. Chat title is label725; Parley title is label724,space,
peer civilization,space,parenthesized attitude from **peer→local**. Stored zero
attitude uses category0; otherwise V04 category, with a fresh attitude read.
An oversized title must not overwrite following window state.

Option callbacks focus/select the incoming current window first, then mutate
canonical Chat. Anonymous and MuteAnonymous copy their control values. Changing
AllRecipients copies its value; when nonzero, scan1..7 and for each existing
recipient control set1,show,then store recipient1 after callbacks. Turning it
off leaves recipients unchanged. A single recipient copies its control value;
turning one off while AllRecipients is nonzero clears/shows that option then
stores AllRecipients0. Receive-suppression callbacks accept only their seven
civilization control IDs and copy the corresponding value. Appendix A records
IDs without prescribing native control classes.

#### Received text and interception

Receipt first copies canonical Chat history edit into the cached history-buffer
destination. Clear shared composition text, then re-read history; prepend CRLF
when nonempty. Recipient names use label729 for civilization0; otherwise ruler
title,space,ruler name. Test only the incoming snoop argument's low byte:

- Snooped: label726,space,sender name,label727,space,target name,`) (`,year,`) `.
- Ordinary: label728,space,sender name,` (`,year,`): `.

Append incoming message. **Only after composition**, suppress sender0 when
MuteAnonymous is set. For ordinary messages, if Parley is closed OR its peer
is not sender, consult the receiving window's per-sender suppression flag and
return if nonzero. A matching open Parley bypasses that suppression.

Then scan peers1..7. Exclude sender and live local; require human but not alive.
If peer→local SpyChat is set, resolve its live network recipient and forward the
**original message text**, sender,target,snoop1. This forwarding is performed
for received snooped messages too; do not add a new deduplication rule at this
layer. N02 owns dispatch/queue constraints. Append composed text to history.
If Chat is still closed, increment missed then unread counters separately with
wrap. If both Parley and Chat are now closed, request Chat-only opening(0,4),
which can itself fail its contact filter or clear those counters.

#### Sending and input dispatch

Macro commands are handled below and return0. Close invokes focus-and-close
and returns0. Other unhandled actions return1. Send copies the current window's
input edit to shared input; empty text returns0. Copy Chat history edit to shared
history,clear composition,prepend CRLF iff history nonempty. Append label730,
space. Chat-only mode enumerates every nonzero recipient flag1..7, separated by
`, `, using recipient names; this first pass does **not** filter alive/human/local.
Parley appends its current peer and counts one recipient. Zero recipient count
returns0 without clearing input. Append ` (`,year,`): `,fresh input and update
shared history **before network sends**.

Recheck live mode after history callbacks. In Parley, send ordinary chat to
live peer with local sender; then scan human peers1..7 other than local and live
peer, forwarding snoop1 to each with peer→local SpyChat. In Chat-only mode, scan
human nonlocal selected recipients1..7. Sender is0 when live Anonymous else
live local; send ordinary target=index. For each, scan human snoopers1..7 except
local and that target, require snooper→local SpyChat and send snoop1; reread
Anonymous/local for each send. Do not coalesce repeated snoop deliveries for
multiple selected targets. Resolve recipient and read text at each send; earlier
callbacks may alter later gates. Finally clear live input buffer, set the
current window's input edit to that empty text and return0. Native focus and
physical Enter/Escape bindings are reference procedures in Appendix E.

#### Shared history storage and logging

Append-history captures capacity, measures current history, computes unsigned
remaining=`capacity−(historyLength+1)`,then measures incoming text. If remaining
<textLength+1, remeasure incoming text and start removal offset=textLength+1
(**not the actual shortage**). Scan live history at that offset until CR,LF or
NUL; if not NUL skip all consecutive CR/LF. Copy retained suffix down to the
beginning, even when offset0, then append incoming text using fresh history
storage. Publish updated history to Chat then Parley. This content-removal rule
is not a general ring buffer or automatic maximum-line truncation algorithm.
Invalid history reads need established input or safe rejection; native edit
selection and buffer access are reference details.

Clear-history empties the logical history and updates both Chat and Parley views.
Native byte clearing, edit selections, focus and buffer allocation are reference
implementation details in [Appendix F](#appendix-f-original-implementation-reference). The append/removal policy above remains
the history-content rule, independently of storage layout.

Initialization reads `CIV.INI`, section `Civilization Gold`, key `ChatShowSize`,
default−1. If −1 or signed<8192, write8192 to profile then publish8192; if
signed≥57344, write57344 then publish57344, including equality. Otherwise retain
the supplied capacity. Establish empty history and input without requiring fixed
buffers. Unless no-load-history is set, open `chatlog.txt`; on success compare
signed size to live capacity, seek by size−capacity if larger, read up to the
live capacity into history and close. Failed reads/seeks do not fabricate bytes;
retain the logical empty/received prefix and a safe terminator. The separate
message-input domain is unchanged by using extensible storage.

N01's session-log operation creates `chatlog.txt`; failure returns before reading
history. Success writes current history and closes even after ordinary write
failure. Closing the Chat-only window with save-history enabled and established
history has a distinct file effect: clear history first, then create/log the empty
text. Retire its interaction/storage afterward. Do not replace that empty log
with the nonempty session-log behavior. Native destructor/deallocation order is
reference machinery; the file effects and preceding committed messages remain.

#### Name and file macros

Adjective and ruler selectors each start1 and rotate independently1..7. Every
macro invocation clears shared composition and captures allCivilizations=(raw
UN location!=−1 OR local Marco Polo OR scoreLocked). For either name macro,
while attempts<8 AND selected civilization!=live local AND !allCivilizations,
accept selected if chat-contact option is nonzero and it is human, otherwise
if selected→local Contact; else increment attempts and selector,wrapping exactly
8 to1. Alive is not tested. Attempts==8 returns without replacing input.

Adjective macro captures selector, increments it **before** adjective lookup,
appends adjective+space; ruler macro first appends title for live selector+space,
then captures/increments live selector and appends ruler name+space. Finally
wrap a live selector exactly8 to1; callback replacements survive other values.
For three file macros use `chatmac1.txt`..`chatmac3.txt` and separate retained
offsets. Open failure sets offset0 and destroys file. If signed size≤signed
offset,reset0. Seek relative offset; failure resets0,closes/destroys. Otherwise
read one line with limit256 into shared text,close,then update **live** offset
by measured resulting length+2,assuming CRLF even for other line endings;destroy.
A resulting zero offset makes caller return.

After a successful macro, replace selected text in the captured input control
with shared composition,query selection end,measure **live** composition and
select `(end−length,end)`. Unrecognized direct macro IDs still perform this
replacement with the cleared line after the allCivilizations queries. No
trim/newline normalization or automatic contact-record creation is added.

### 22. Chat and Parley windows and panes

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

V05 owns negotiation modes, action transitions and admission. P04 supplies
controls, native text and surface services; P07 owns their composition here.
Use two persistent windows, canonical Chat and canonical Parley. Each has
closed flag, peer, layout mode, saved rectangle/position, requested and cached
left/right panes, per-side readiness, controls and redraw rectangle. Capturing
current before a focus callback does not make all later child operations use
that receiver: children that recapture current do so independently.

#### Pane selection and shared framing

The mode→(left,right) table is in Appendix A. A pane-selection call first reports
an invalid side or signed-negative pane through P04's assertion service; if it
returns, no out-of-bounds write is permitted. Clear side readiness. Capture
the side identity before retiring prior controls. BarterItem and DemandItem
redirect choice0..6 to
Gold,Advances,Units,Cities,Maps,WarTargets,Treaty respectively. Barter uses this
side's choice; Demand always uses the fixed requested-choice field. Retain
which redirection was used even after resolving the concrete pane. An unmapped
choice leaves the redirect pane unresolved; it still receives cleanup and tail.

Clear side controls and publish resolved pane. If canonical Parley is open and
the pane is neither Null nor Chat, configure shared action visibility: Review
hides/disables both Send controls, hides NeverMind and Intelligence, shows
Accept/Decline/Counter; other panes show/enable Send, hide/disable alternate
Send, show NeverMind/Intelligence, hide Accept/Decline/Counter. Later pane
branches and button update may override this. Finish every pane, including an
unsupported nonnegative one, by updating buttons and presenting the retained
redraw rectangle.

Choice panes initialize their specified current selection, provide activation
and the change callback, then set readiness1. Gold and asset lists derive readiness
elsewhere. Geometry, page sizes and native control order are in Appendix E.

| Pane | Required content and effects |
| --- | --- |
| Action | Heading739, labels740..744; current None displays0; no change callback. |
| LeftKinds/RightKinds | Header771/772, choices747..751 then752/753 respectively then834; disable opposite Gold/Treaty per V05, initialize side choice, kind-choice callback. |
| Treaty | Title740 or834 when redirected, labels755..758 omitting last when redirected; V05 disable/selection adjustments, scalar-choice callback. |
| CurrentTreaty | Heading759; live local→peer relation priority Alliance760, Peace761, Ceasefire755, War762, else763; readiness1. |
| GiftKinds | Title764, labels765..769, current gift kind, gift-kind change callback. |
| ProposalKinds | Title780, labels781/782, selected map/joint-war choice, proposal-change callback. |
| DemandKinds | Title791/subtitle792, seven choices747..751,753,834, requested choice, requested-choice callback. |
| Advances/Units/Cities | §23 eligible lists; Advance receives for Demand or right Barter, City/Unit offering is the opposite boolean. Keep distinct identity and disclosure rules below. |
| Maps | Title784 only for right Barter, else783; labels785..787, per-side map scope, map-scope callback, readiness1. |
| WarTargets | Title788 ordinarily,789 left Barter,790 right Barter,835 Demand; §23 civilization list with V05 target filter. |
| Gold | Label773 for Demand else774, live local treasury and775; edit label776, initialize signed per-side gold, limit6, §23 change callback. Readiness remains0 until callback. |
| Awaiting | Peer adjective substituted into738; disable Send and NeverMind. Both local Unit/City disclosure flags0 permits RevealAll, otherwise ConcealAll. |

Unit disclosure controls use left labels879/881, right880/880; left enables or
disables local disclosure, both right controls notify the peer. Set disclosure
shows Conceal and hides Reveal; right Conceal is also disabled when set. Type778
and Home779 sorting are disabled independently when canonical mode is not GiveUnit
and that side's disclosure flag is clear. Redirected City controls use left882/884,
right883/883 with the same visibility/disable policy and City disclosure callbacks.
Do not turn native overlapping rectangles into different semantic choices.

**Description / Review:** Review first shows canonical Parley. Build §24 text
with mode0 for Description,1 for Review. Present read-only text (original limit8192)
with semantic completion through §21. Review then sets **canonical** reviewAllowed1 and examines the incoming
receiver’s retained request. For nonnull Demand or Barter whose right item is
Advances, capture its signed count and scan all entries in order. Advance the
item cursor before querying live local knowledge; each unknown advance clears
the incoming receiver’s reviewAllowed flag. Do not break after the first unknown
one, reset that receiver’s flag independently, or test another item kind.
Finally enable Accept iff that receiver’s live reviewAllowed is nonzero, then
run the common button-update tail. This is a UI gate; V05/N02 still own offer
admission and execution.
Null adds no content. Other unresolved/nonnegative pane values only receive
the shared setup and tail, not a fabricated default pane.

#### Chat option pane

Reevaluate §21's allContacts result and candidate filter. Publish canonical Chat
Anonymous745, MuteAnonymous865 and AllRecipients746 values and their callbacks.
Scan civilizations1..7; clear canonical recipient/suppression flags for ineligible
ones. Eligible civilizations expose both options with their canonical values and
civilization adjective. These eligibility updates can clear prior choices.
Native columns, spacing and checkbox objects are reference behavior.

#### Button refresh and cleanup

If either Send control is absent, button refresh returns. Unless modeAwaitOffer,
hide/disable both global disclosure buttons. AwaitResponse/AwaitOffer hide/
disable alternate Send and show/disable Send; AwaitOffer then shows/enables
RevealAll when both local disclosure flags0,otherwise ConcealAll. ComposeAction
and ChooseBarterKinds hide/disable alternate Send,show/enable Send. Concrete
one-sided offers and Demand show alternate Send,hide/disable Send,enable the
alternate only when right readiness is nonzero. Barter requires both readiness
fields nonzero. Mode Review’s Accept eligibility is set by the pane’s explicit
advance-knowledge pass above, not by this general button refresh.

Retire the side's obsolete controls when changing its pane. Full cleanup releases
the retained request and clears its reference before retiring the whole interaction.
Native deletion order, widget-slot replacement and repaint geometry are optional
reference behavior. Preserve any already committed negotiation effects and
required caller/network completion; cleanup is not a rollback.

### 23. Parley selection lists

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

V05 owns eligible rows, stable IDs and transaction admission. Keep per-side
values, stable IDs, selection flags, range anchor, first item and readiness.
Nonzero flags count for readiness; V05 packs only flags exactly1. Display sorting
must not change semantic identities or automatic selections; the original
ordering algorithms below retain their contractual role where callers use them.

#### Semantic row selection

Resolve a user action to a row ordinal in the current semantic list, using stable
identity for unit/city rows. Reject negative or out-of-count ordinals; City also
rejects ordinal0. With Shift, clear selections then select the inclusive old-
anchor-to-row range without changing the anchor; City skips indices≤0. Otherwise,
with Control set anchor and XOR this row's flag with1; without Control first
clear other rows, then set anchor and XOR this flag. Do not normalize a nonbinary
flag before XOR. These are semantic modifiers; physical key bindings may differ.

#### Required row information and refresh

Recompute readiness from any nonzero selection. Empty lists clear readiness.
Unit rows resolve their stable IDs; a missing unit (negative result) repopulates
through V05 with preserve1. City rows do the same for exactly−1. After rebuild,
count0 sets first−1; otherwise the original count-versus-count comparison only
sets first to fresh count−1 when the later count grew. Restart after rebuilding;
do not silently change that to a first-versus-count correction.

Advance rows show advance names. Unit Type shows type, adding home only in
GiveUnit, left/right Barter with the corresponding disclosure, or Demand with
peer disclosure. Unit Home shows home and type. City rows show name; Palace is
identified, otherwise disclosed details include stored unit count. GiveCity forces
details; other modes use the side's city-detail disclosure. Civilization rows
show civilization/ruler identity. These rules prevent a new layout exposing
concealed asset information. Button availability uses V05 plus readiness.

Original scrollbar geometry, hit testing, drawing and decorative representative
city art are in the appendix.

#### Sorting and rebuilds

Unit construction stores the side's offer/receive selector, captures the input
rectangle, resets shared sort mode to Type, and invokes V05 population with
preserve0. City construction captures the rectangle, stores its selector and
populates preserve0. Population caches the current receiver before callbacks;
a separate incoming receiver supplies the peer for ownership filtering. After
rebuilding, units use the live shared Type/Home mode; cities always sort below.

Sorters capture the current receiver on entry. With count≤1 return without
changing sort mode. Otherwise Unit Type sets mode0, Unit Home sets mode1.
For left from first through last−1, and right from left+1 through last, perform:

- **Type:** query right unit's current type name before left's. Swap when
  case-sensitive bytewise comparison says left>right. Swap row values, then
  stable IDs, then selection flags, using live entries at each operation.
- **Home:** if left unit's home is None, continue without reading right home.
  Otherwise swap when right home is None; if not, query right home name before
  left and swap when left>right. Use the same three-array swap order.
- **City:** query right city's Palace first. If present, swap unconditionally,
  even when left also has a Palace. Otherwise query left Palace and, only if
  absent, compare left name to right name, swapping for left>right. Swap values,
  selection flags, stored unit counts and stable IDs, in that order.

These are nonadjacent exchange sorts. Strict comparison does not imply stable
ties: Type input`B₁,B₂,A` becomes`A,B₂,B₁`. Home None rows come first; no secondary
type-name tie break is added. Multiple Palace cities can reorder by the
unconditional right-Palace rule. Type/Home buttons focus/select the captured
current window, read its live count, then sort0..count−1 and redraw; sorter and
renderer each independently recapture current after callbacks.

#### Gold edit and disclosure controls

Gold edit targets canonical Parley: its left control ID selects side0, every
other ID side1. Clear both temporary text buffers, capture edit selection end,
then recapture the edit and copy its text. Format live local treasury in decimal.
Scan text by signed character value and remove non-digits by copying the suffix
over them; **increment the scan index even after removal**, so adjacent invalid
characters can remain. Parse using P04's decimal operation. Enforce the treasury
upper bound for left Barter and all modes except Demand/Barter: if text length
exceeds treasury-string length or parsed amount exceeds live treasury, mark
changed, decrement caret with wrap, then remove the character at that position.
Caret underflow is an excluded before-buffer write, not a mutation of the
neighboring changed flag. Set readiness from a fresh parse>0, update buttons;
if changed, clamp caret down to current text length, set live edit text and
collapse its selection. This does not impose a general numeric lower clamp.

Peer intelligence first evaluates local active UN or Marco, then still reads
local→peer Embassy even when a wonder already granted access. Embassy, that
wonder result or scoreLocked opens §9 with live local/peer; otherwise show
`NOINTEL`.

Enable/disable all disclosure captures current, writes both local Unit and City
flags1/0, sends City then Unit disclosure messages through N02, resolving live
peer for each. After sends, enable shows/enables Conceal and hides/disables
Reveal; disable does the reverse. Individual Unit enable focuses/selects,
shows Conceal/hides Reveal, stores flag1, enables both sort buttons, sends Unit
reveal, then redraws side0. Disable reverses visibility, stores0, disables sort
buttons, sends conceal, then reads captured receiver's count, Type-sorts0..count−1
using fresh current and redraws side0. Individual City enable/disable performs
the same visibility/flag/send sequence without sort buttons or sorting, then
redraws City side0. Notify buttons focus/select, substitute local civilization
name and send the corresponding game notification to **canonical** Parley's
peer. N02 owns packet construction/receipt; these callbacks do not transfer
assets or retroactively change a packed offer.

Original presentation procedures: [Appendix E](#appendix-e-original-presentation-reference).

### 24. Parley offer descriptions

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

V05/N02 own the admitted offer and its ordered execution. This renderer produces
text from that offer; it does not execute or revalidate it. A null request
returns without clearing prior description. Otherwise read/capture right-item
extent and derive the left item's position **before** clearing output, even
for modes that use neither item. Clear output,check empty,then for outgoing
mode0 append label796 and two CRLFs. Check the original prefix limit,then read
the live request kind and dispatch below. All other mode values omit the prefix.
Afterward check final length. Original capacities/assertions are validation
facts in Appendix B; they do not authorize overwriting output or request.
An alternative may grow independent text storage or stop safely before overflow.

Write `NL=CRLF`, `BL=NL+NL`, `I=eight spaces`. Scalar, map, list and amount helpers
below each append their own final NL. A header followed by NL therefore adds to,
not replaces, that final break. Text is built by ordered appends and provider
lookups; later names or item values can change after earlier callbacks.

| Offer kind | Append sequence after optional outgoing prefix |
| --- | --- |
| Treaty | Scalar(right value,standalone). |
| GiveAdvance | label806,BL,label807,NL,AdvanceList(right). |
| GiveGold | label806,BL,label809,NL,I,Amount(right value). |
| GiveUnit | label806,BL,label808,NL,UnitList(right). |
| GiveCity | label806,BL,label810,NL,CityList(right). |
| GiveMap | label806,BL,Map(right value,giving). |
| JointWar | label801,BL,label805,NL,CivilizationList(right). |
| MapExchange | label801,BL,then label802/803/804 for value0/1/2 respectively,then NL. Other values add no label. |
| Barter | label813,BL,LeftItem(left),NL,RightItem(right). Description order is left then right, although packing stores right first. |
| Demand | label811,space,label838 for WarTargets or837 for Treaty,otherwise812; BL,Item(right,receiving) without an item header or extra gold indentation. |
| Other | No further body; still perform final length check. |

For Barter each supported item appends its header,NL,then the matching helper.
Gold also appends I after NL. Unsupported items append nothing, so the separator
NL between left and right remains even if either is unknown.

| Item | Left header | Right header | Helper |
| --- | --- | --- | --- |
| Treaty (both admitted treaty forms) | 839 | 840 | Scalar(value,barter) |
| Advance | 814 | 820 | AdvanceList |
| Gold | 815 | 821 | Amount |
| Unit | 816 | 822 | UnitList |
| City | 817 | 823 | CityList |
| Map | 818 | 824 | Map(value,giving/receiving respectively) |
| WarTargets | 819 | 825 | CivilizationList |

Scalar standalone maps values0..3 to labels797..800; barter maps0..2 to755..757.
Other values add no label; all append NL. Map values0/1/2 map to828/830/832 on
giving side,829/831/833 on receiving side; invalid values add only NL. Amount
formats the full signed value in decimal,space,label827,NL without clamping it
to a treasury.

Advance,City,Civilization lists capture signed incoming count once and iterate
index0..<count in supplied order. Advance resolves each advance name. City
resolves stable ID using V05's reverse lookup, appends live city name,`(`,
the supplied paired signed unit count read **after** those appends,`)`.
Missing City does not silently disappear: original immediately read before the
city array; require established retained input or stop. Civilization resolves
live civilization name; advance its input cursor before name lookup/appending.
After each entry, only when count>1: append`, ` if index<count−2, append space,
label826,space if index==count−2, otherwise append`.`. Thus a one-entry list has
no period. Every list appends NL, including zero/negative count.

UnitList first initializes62 counters0,then captures signed incoming count.
For each supplied stable ID invoke V05's unit lookup; ignore negative lookup
results. For a nonnegative result read current type; if that counter0 increment
distinct-types,then **reread type** and increment that counter with wrap. An
unsupported type may not overwrite the counter array. Scan types0..61 in order,
skipping zero counts; append live type name,`(`,signed count,`)`. Punctuation is
gated by **incoming unit count>1**, but compares printed-type ordinal against
`distinctTypes−2` using signed wrapping arithmetic, with the same comma/and/
period sequence above. Multiple input units of one type therefore end with a
period; a single input unit does not. Increment printed ordinal after appends,
then finish with NL. This groups units by current type and does not preserve
the supplied row order in the text, although execution still uses V05 order.


### 25. Required conformance

> Original behavior reference. Only the definitions adopted in the
> [required contract](#required-presentation-contract) are binding.

Check report values/ranks, privacy, query-induced recalculation and scratch/cache
publication, including layout-dependent admission retained as logical inputs.
Exercise tax, ship, intelligence, chat and Parley actions with live remote events,
stable identity rebuilds, disclosure and cancellation. New report layout/sorting
must preserve protocol selections and must not change Score's recalculated prefix.
Original geometry, native controls and raster tests are appendix reference cases.

Original presentation procedures: [Appendix E](#appendix-e-original-presentation-reference).

### Appendix A. Reference encodings and control identities

These values support original-program validation and asset/control adapters.
They do not require byte-packed game objects or native widgets. Rule, label and
resource IDs in the body select external data; F01 arithmetic widths describe
observable arithmetic rather than a required host-language type.

Defense abilities are appended in this order after Carry/Air:

| Logical ability | Original mask | Label |
| --- | --- | --- |
| See two squares | 0x0001 | 351 |
| Ignore zones of control | 0x0002 | 352 |
| Ignore city walls | 0x0040 | 353 |
| Amphibious | 0x0004 | 354 |
| Carry missiles | 0x0008 | 355 |
| Carry air units | 0x0080 | 356 |
| Paradrop | 0x0100 | 357 |
| All terrain as roads | 0x0200 | 358 |
| Double versus horse | 0x0400 | 359 |
| Double versus air | 0x2000 | 360 |
| Coastal restriction | 0x0020 | 361 |
| Fundamentalism only | 0x0800 | 362 |
| Submarine characteristics | 0x4000 | 363 |

Air-to-Air uses0x0010; otherwise domain1 supplies Air. GraphicalDiplomacy and
DiplomaticAnimation are game-option masks0x00040000 and0x00200000. Shared treaty,
city, unit, score and spaceship encodings remain with F02/V04/V07/V08; do not
infer mechanical effects from a displayed ability string.

Pane IDs: Null0,Chat1,Action2,LeftKinds3,RightKinds4,Treaty5,CurrentTreaty6,
GiftKinds7,Advances8,Gold9,Units10,ProposalKinds11,WarTargets12,Maps13,
BarterItem14,DemandKinds15,DemandItem16,Cities17,Awaiting18,Description19,Review20.
Cached-invalid21 is distinct from Null. V05 defines the mode names and packing.

| Mode | Left pane | Right pane |
| --- | --- | --- |
| ReviewIncoming | 0 | 20 |
| AwaitResponse | 0 | 19 |
| AwaitOffer | 0 | 18 |
| ComposeAction | 0 | 2 |
| Chat | 0 | 1 |
| ChooseBarterKinds | 3 | 4 |
| Treaty | 5 | 6 |
| GiveAdvance | 7 | 8 |
| GiveGold | 7 | 9 |
| GiveUnit | 7 | 10 |
| GiveCity | 7 | 17 |
| GiveMap | 7 | 13 |
| JointWar | 11 | 12 |
| MapExchange | 11 | 13 |
| Barter | 14 | 14 |
| Demand | 15 | 16 |

Original Chat/Parley control identities:

| Control | ID |
| --- | --- |
| Caption Close,Shrink,Grow | 1001,1002,1003 |
| Bottom Close,history,input,SendChat,ClearChat | 1005,1006,1007,1008,1054 |
| ConcealAll,RevealAll | 1073,1074 |
| Intelligence,Send,alternate Send,NeverMind | 1064,1009,1063,1010 |
| Accept,Decline,Counter | each1010, with distinct callbacks/objects |
| Action,LeftKinds,RightKinds,Treaty,GiftKinds | 1011,1012,1013,1014,1015 |
| Gold | 1016+side |
| Unit scrollbar,click surface,Type,Home,Reveal,Conceal | 1018,1020,1024,1026,1028,1030, each+side |
| City Reveal,Conceal,scrollbar,click surface | 1032,1034,1056,1058, each+side |
| ProposalKinds | 1036 |
| Civilization scrollbar,click surface | 1037+side,1039+side |
| Advance scrollbar,click surface | 1041+side,1043+side |
| Anonymous,MuteAnonymous,AllRecipients | 1045,1072,1046 |
| Recipient,suppression | 1046+civ,1064+civ for civ1..7 |
| DemandKinds,MapScope,description | 1055,1060+side,1062 |

Chat internal Enter208,Escape210 and native Escape27 are distinct input-layer
codes. Macro commands688,689 select adjective/ruler;690..692 select file1..3.
List controls use P04's single-choice style1. Read-only history/description edits
use original style0x122; input uses0x200 in Chat,0x220 in Parley and its flag24
key path. P04 owns those flags' native mapping. Surface creation uses0x602 for
Chat/Parley and0x800 for herald background/art. P03 owns palette indices and
sprite transparency; an alternative surface object need not use those masks.

For inspecting original list storage only: each side reserves2049 elements;
values,IDs,selected flags and city-unit counts start at receiver offsets0x3f0,
0x43f8,0x8400,0xc408; side stride0x2004. Rectangles are at0x3c0,counts0x3e8,
first0x10410,anchors0x10418,pageRows0x10420. Empty selected[-1] therefore reads
side0's preceding last right-ID slot or side1's last left-selected slot.
Empty Advance value[-1] reads the other side's count for side0,or last left-value
slot for side1. Those are retained inputs, not synthetic zero sentinels. Preserve
only supported reads; any write outside a logical row array is excluded.

### Appendix B. Nominal inputs, retained reads and excluded bugs

Ordinary shipped resources, valid live identities and successful native providers
are the nominal domain. Parsed custom rules can still expose an original defect;
“accepted by the parser” does not authorize a destination overwrite. Preserve
defined scalar overflow, unsigned counter wrap, signed-byte interpretation,
query order and ordinary partial failures where the accessed objects remain
valid. A restrictive profile may reject extra inputs earlier; document that
choice separately from classic results on the nominal domain.

| Boundary | Original behavior and cleanroom treatment |
| --- | --- |
| Defense comparison outline with no displayed row | Frame x was never assigned. Primary B17 suppresses the frame; independent BF-023 derives geometry. Neither proves a missing original x. Supply retained x or stop; either repaired display must be labeled separately. |
| Failed cursor-position query | Original list handlers could use unwritten coordinates. BF-021 and the advance-list repair ignore such clicks. That is an admissible safe profile, not recovery of the original coordinates. |
| Herald first-line trim | BF-024 changes the wrong-buffer length; primary initializes second line empty, independent trims the current line. Original needs retained second-line text, including preceding-row text. Do not label either repair as original. |
| Herald token/line buffers | Five50-byte word slots and short private line buffers do not bound RULES name length. BF-084 changes token ownership, caps or extends tokens and index widths. Keep signed scalar scan/trim semantics only while accesses stay valid. Every overlapping-slot copy, line underflow and resulting icon-x corruption is excluded, even if deterministic. |
| Hotseat herald local−1 | Original treaty low-byte address0x64c12c+4*other aliases tribe4 ruler-title storage for other0..7. An original adapter can obtain it from canonical tribe bytes at offset0x434+4*other. A logical implementation can supply that retained input; it must not pretend a real civilization−1 has a normal treaty record. No writes are authorized by this read. |
| Intelligence text | Original private80-byte scratch can overflow with accepted rule/resource names. BF-044 expands it. Bounded reject, omission or separate growing text are safe alternatives; preceding uppercase/query effects remain. |
| Fame and Hall text | Original template scratch80 bytes; Hall expanded local132 bytes; shared composition2052 bytes. BF-050/BF-199 enlarge/grow storage and primary truncates some outputs. None makes original overflowing writes normative. A first negative Hall rank requires retained expanded text; later negative ranks reuse the preceding expansion. |
| Signed Hall scores | Classic signed-word negative scores are hidden before display; primary B15's unsigned-score repair is outside this target. V08 owns file admission, insertion and BF-198's stricter loaded-record checks. |
| Foreign selectors/audience | BF-100/BF-103 reject nonexistent targets/actions. Primary B6 clears stale cancel state only with its UI fix enabled; classic V05 request sequencing remains the owner. Unsupported live target/mapping reads need backing or a stop, not an adjacent civilization write. |
| Spaceship strings | Original statistics512 bytes,success128 bytes. BF-190 rejects excessive combined labels at ingress; primary uses bounded composition. A growing logical string is also safe. Preserve ordered formatting for fitting inputs, never overwrite following ship-window state. |
| Chat/title/offer text | Native title128 bytes,input257 bytes,history uses retained allocated capacity; original offer description2048 bytes. The shared text owner is2052 bytes, not its smaller parser view. BF-200 and other growing-output repairs do not reproduce original overflow. Checks after appending cannot make a prior overwrite safe. |
| Gold edit | Original two32-byte text arrays; caret0 decrements to unsigned maximum before removal. Copying before text would corrupt a neighboring changed flag. Exclude that write even though the independent source models mapped backing. |
| Oversized lists, selection ranges or unit-description types | Reject/grow logical storage before an excessive write; never reproduce cross-side flag/count changes. Unit-description type counters cover0..61. Wrong native IDs or unsupported pane selectors do not permit writing another owner. |
| Failed arithmetic or reentrant redraw | Zero divisor, signed minimum/−1 division, repeated stale-ID rebuild or callback-destroyed receiver is not an ordinary empty result. Preserve prior valid effects and stop or declare the accepted domain. No fabricated count clamp or generic rollback. |

For offer text, the original asserts empty after clearing, then length<2048 after
outgoing prefix and after the completed body. A returned assertion handler could
continue; this does not authorize unsafe appends. Physical request/output
aliasing is not a logical offer-mutation mechanism. N02's admitted request and
a separate text owner provide a safe cleanroom domain; unsupported aliases may
stop after the valid prefix. Read-only missing-ID name accesses likewise need
established original bytes or explicit supplied input, not a fabricated name.

### Appendix C. Spaceship artwork records

These ordered data inputs determine composition with classic `ss.dll`; their
original stack/receiver encodings are not implementation requirements. Part IDs
are V07's Structural0,Propulsion1,Fuel2,Habitation3,Life Support4,Solar Panels5.
Columns are ordinal threshold,resource ID,destination x/y,width/height,source x.
The resource field is used for the background-selection pass; component passes
select the sheets specified in §19. Preserve row order.

| Row | Part | Ordinal | Resource | x | y | Width | Height | Source x |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | 2 | 6 | 463 | 178 | 170 | 43 | 51 | 516 |
| 1 | 2 | 6 | 464 | 393 | 104 | 61 | 40 | 560 |
| 2 | 0 | 7 | 407 | 479 | 316 | 58 | 34 | 68 |
| 3 | 2 | 2 | 457 | 478 | 324 | 25 | 54 | 146 |
| 4 | 2 | 2 | 458 | 506 | 313 | 38 | 39 | 172 |
| 5 | 0 | 9 | 409 | 482 | 325 | 66 | 55 | 127 |
| 6 | 1 | 2 | 443 | 479 | 319 | 77 | 78 | 105 |
| 7 | 0 | 0 | 400 | 312 | 157 | 253 | 240 | 0 |
| 8 | 0 | 1 | 401 | 312 | 157 | 253 | 240 | 0 |
| 9 | 0 | 2 | 402 | 312 | 157 | 253 | 240 | 0 |
| 10 | 0 | 3 | 403 | 312 | 157 | 253 | 240 | 0 |
| 11 | 0 | 4 | 404 | 312 | 157 | 253 | 240 | 0 |
| 12 | 0 | 6 | 406 | 312 | 157 | 253 | 240 | 0 |
| 13 | 0 | 8 | 408 | 312 | 157 | 253 | 240 | 0 |
| 14 | 0 | 10 | 410 | 312 | 157 | 253 | 240 | 0 |
| 15 | 0 | 12 | 412 | 312 | 157 | 253 | 240 | 0 |
| 16 | 0 | 14 | 414 | 311 | 157 | 254 | 240 | 0 |
| 17 | 0 | 16 | 416 | 311 | 157 | 254 | 240 | 0 |
| 18 | 0 | 18 | 418 | 311 | 157 | 254 | 240 | 0 |
| 19 | 0 | 20 | 421 | 311 | 157 | 254 | 240 | 0 |
| 20 | 0 | 22 | 424 | 311 | 157 | 277 | 240 | 0 |
| 21 | 0 | 23 | 425 | 187 | 114 | 401 | 283 | 0 |
| 22 | 0 | 24 | 426 | 187 | 114 | 401 | 283 | 0 |
| 23 | 0 | 25 | 427 | 186 | 114 | 402 | 283 | 0 |
| 24 | 0 | 26 | 428 | 186 | 114 | 402 | 283 | 0 |
| 25 | 0 | 27 | 429 | 187 | 114 | 401 | 283 | 0 |
| 26 | 0 | 28 | 430 | 187 | 114 | 401 | 283 | 0 |
| 27 | 0 | 30 | 433 | 187 | 114 | 401 | 283 | 0 |
| 28 | 0 | 32 | 436 | 187 | 114 | 401 | 283 | 0 |
| 29 | 0 | 36 | 439 | 187 | 114 | 401 | 283 | 0 |
| 30 | 0 | 38 | 440 | 186 | 114 | 402 | 283 | 0 |
| 31 | 1 | 6 | 449 | 442 | 117 | 33 | 43 | 462 |
| 32 | 5 | 4 | 496 | 398 | 165 | 48 | 22 | 400 |
| 33 | 3 | 3 | 473 | 381 | 164 | 62 | 53 | 210 |
| 34 | 4 | 4 | 487 | 453 | 169 | 48 | 36 | 360 |
| 35 | 4 | 3 | 485 | 480 | 189 | 50 | 36 | 259 |
| 36 | 5 | 4 | 497 | 429 | 180 | 71 | 38 | 449 |
| 37 | 4 | 1 | 481 | 508 | 210 | 43 | 32 | 48 |
| 38 | 4 | 2 | 483 | 529 | 225 | 55 | 42 | 150 |
| 39 | 5 | 2 | 492 | 431 | 199 | 74 | 36 | 151 |
| 40 | 3 | 2 | 471 | 431 | 206 | 69 | 58 | 74 |
| 41 | 5 | 2 | 493 | 458 | 233 | 80 | 47 | 226 |
| 42 | 1 | 6 | 448 | 178 | 200 | 62 | 40 | 399 |
| 43 | 5 | 3 | 494 | 300 | 197 | 18 | 24 | 307 |
| 44 | 3 | 4 | 472 | 310 | 188 | 65 | 56 | 144 |
| 45 | 5 | 1 | 490 | 339 | 232 | 37 | 41 | 1 |
| 46 | 5 | 3 | 495 | 300 | 217 | 73 | 60 | 326 |
| 47 | 3 | 1 | 470 | 356 | 233 | 72 | 64 | 1 |
| 48 | 5 | 1 | 491 | 350 | 251 | 111 | 69 | 39 |
| 49 | 4 | 4 | 486 | 265 | 237 | 49 | 44 | 310 |
| 50 | 4 | 3 | 484 | 287 | 263 | 52 | 46 | 206 |
| 51 | 4 | 1 | 480 | 311 | 290 | 46 | 40 | 1 |
| 52 | 4 | 2 | 482 | 328 | 310 | 57 | 53 | 92 |
| 53 | 2 | 8 | 468 | 526 | 246 | 81 | 54 | 820 |
| 54 | 0 | 33 | 438 | 587 | 276 | 21 | 25 | 721 |
| 55 | 1 | 8 | 453 | 582 | 273 | 47 | 43 | 645 |
| 56 | 0 | 31 | 435 | 528 | 253 | 48 | 38 | 642 |
| 57 | 2 | 7 | 466 | 503 | 258 | 77 | 58 | 683 |
| 58 | 0 | 29 | 432 | 557 | 291 | 25 | 25 | 579 |
| 59 | 1 | 7 | 451 | 552 | 285 | 47 | 46 | 546 |
| 60 | 2 | 5 | 462 | 497 | 253 | 78 | 57 | 437 |
| 61 | 0 | 21 | 423 | 552 | 285 | 24 | 26 | 524 |
| 62 | 1 | 5 | 447 | 548 | 280 | 49 | 46 | 349 |
| 63 | 0 | 19 | 420 | 496 | 263 | 52 | 53 | 441 |
| 64 | 2 | 1 | 456 | 474 | 267 | 75 | 65 | 70 |
| 65 | 0 | 11 | 411 | 463 | 264 | 66 | 61 | 194 |
| 66 | 2 | 3 | 459 | 462 | 265 | 77 | 64 | 211 |
| 67 | 2 | 1 | 455 | 444 | 283 | 68 | 68 | 1 |
| 68 | 0 | 5 | 405 | 484 | 303 | 66 | 49 | 1 |
| 69 | 0 | 13 | 413 | 507 | 299 | 32 | 31 | 261 |
| 70 | 1 | 1 | 442 | 516 | 297 | 47 | 51 | 57 |
| 71 | 1 | 3 | 444 | 505 | 293 | 55 | 53 | 183 |
| 72 | 1 | 1 | 441 | 479 | 318 | 51 | 52 | 1 |
| 73 | 0 | 19 | 419 | 446 | 287 | 44 | 64 | 396 |
| 74 | 2 | 5 | 461 | 428 | 279 | 66 | 70 | 370 |
| 75 | 0 | 21 | 422 | 465 | 322 | 29 | 29 | 494 |
| 76 | 1 | 5 | 446 | 461 | 316 | 52 | 51 | 296 |
| 77 | 2 | 7 | 465 | 413 | 300 | 60 | 75 | 622 |
| 78 | 0 | 31 | 434 | 414 | 299 | 36 | 65 | 605 |
| 79 | 0 | 29 | 431 | 445 | 348 | 29 | 29 | 549 |
| 80 | 1 | 7 | 450 | 441 | 343 | 49 | 52 | 496 |
| 81 | 2 | 8 | 467 | 392 | 297 | 58 | 78 | 761 |
| 82 | 0 | 33 | 437 | 421 | 348 | 28 | 30 | 691 |
| 83 | 1 | 8 | 452 | 416 | 342 | 50 | 53 | 594 |
| 84 | 0 | 15 | 415 | 465 | 263 | 67 | 42 | 294 |
| 85 | 2 | 4 | 460 | 463 | 257 | 80 | 50 | 289 |
| 86 | 0 | 17 | 417 | 513 | 282 | 33 | 26 | 362 |
| 87 | 1 | 4 | 445 | 507 | 278 | 56 | 45 | 239 |

In-flight overlay rectangles, indexed by raw cached propulsion count1..8:

| Count | x | y | Width | Height |
| --- | --- | --- | --- | --- |
| 1 | 497 | 311 | 88 | 78 |
| 2 | 483 | 311 | 109 | 112 |
| 3 | 483 | 311 | 109 | 112 |
| 4 | 483 | 288 | 115 | 135 |
| 5 | 477 | 288 | 143 | 135 |
| 6 | 186 | 125 | 434 | 298 |
| 7 | 186 | 125 | 448 | 298 |
| 8 | 186 | 125 | 452 | 303 |

An off-nominal count outside1..8 has no synthetic clamped rectangle. Established
original adjacent read bytes can be supplied to a validation adapter; otherwise
stop before consuming unknown geometry. Every resulting out-of-bounds write is
excluded irrespective of whether the source read was recovered.

### Appendix D. Evidence and validation

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Target: Civilization II Multiplayer Gold Edition5.4.0f,patch3. Primary reference
is OpenCiv2 revision`a71e8aefffe04e6f719e0e213b027b128a747711`; the selected
source files are identical in the inspected checkout at
`fc2155ccdb2123c1137babab6fa39e38152278c9`. Independent reconstruction is
`civ2` revision `4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`.
These source references are evidence navigation, not required inputs for a
cleanroom implementation. No reconstruction source was changed for this document.

| Evidence | Covered behavior |
| --- | --- |
| [reports.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/reports.cpp) | Auxiliary reports, filters, grids, ranking, city/report callbacks, tax window, score/fame/Hall and history. |
| [diplomacy.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/diplomacy.cpp) | Herald setup, palette/art, cached prerequisite ranking and original text dataflow; V05 owns negotiation. |
| [spaceship.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/spaceship.cpp) | Ship art/statistics, region controls, timers and modal sequencing; V07 owns calculations/launch. |
| [network.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/network.cpp) | Chat/Parley windows, selection sorts and text descriptions; N01/N02/V05 retain session/transaction ownership. |
| [window.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/window.cpp) | P04 shared intro-style creator and report-window services. |
| Independent `src/auxiliary_advisor.cpp`, `city_status_report.cpp`, `top_cities_report.cpp`, `defense_report.cpp`, `trade_report.cpp`, `science_report.cpp`, `world_wonders_report.cpp`, `demographics_report.cpp`, `casualty_timeline.cpp`, `history.cpp` | Ordered reads, aggregate arithmetic, visibility, input and retained-state boundaries. |
| Independent `src/civ_score_window.cpp`, `civ_fame_window.cpp`, `high_scores.cpp`, `tax_rate.cpp`, `civ_spaceship*.cpp`, `civ_spaceship_render_records.inc`, `herald_window.cpp` | Score/art composition, local lifetimes, callback timing, primary repair differences and original data inputs. |
| Independent `src/multiplayer_chat.cpp`, `multiplayer_window.cpp`, `multiplayer_dialog.cpp`, `multiplayer_selection.cpp`, `multiplayer_request.cpp`, relevant headers, `font.cpp`, `intro_surface.cpp`, `civilopedia_detail.cpp`, `analysis/reference/bugfix-log.md` | Chat history/options, pane geometry, identity refresh, selection/description order and excluded corruption. |

#### Checks performed for this document

Eleven **unchanged whole source bodies** were extracted and checked with declared
external query, text, drawing and logical-storage adapters. Forty assertions
passed in optimized and AddressSanitizer/UndefinedBehaviorSanitizer builds
(leak detection disabled). Eight report bodies supply25 assertions: empty and
equal Top Cities ranks, earlier ties and wonder weights, signed size/sentinel
behavior, ordered ability separators, strict demographic rank/leader ties and
visibility-query order, negative-y ranking mutation, science column-major hits,
live actor versus captured intelligence target, help-loop/retry, division stop
and scrolling wrap. Three complete sorters supply15 assertions: count≤1 retains
sort mode, nonadjacent equal-name reordering, right-before-left home queries,
None-home priority, Palace query order and unconditional right-Palace exchange,
and synchronized row/ID/flag/unit-count swaps.

These adapted64-bit fixtures test selected source behavior, not native original
GUI execution or full-window conformance. They do not independently validate
all of the prose or the repaired branches excluded above. The test harnesses
and transcripts are external research artifacts, not included in this repository;
the algorithms and expected cases here are the repository's enduring contract.

Direct checks against the pinned original executable verified all704 immediate
assignments forming Appendix C's88 records, plus276 bytes of ship geometry,
mode/pane table and layout defaults. Ten instruction probes verify the repeated
casualty tail/head test, Science actor reload, Intelligence target capture,
unassigned Defense comparison-x reads, signed Hall score test, City row0 click
rejection and the Unit/City count-versus-count redraw comparisons. Source
integrity checks matched31 independent files and7 primary files to their pins;
asset identities below were hashed. Relative links, anchors, source references,
unique specification IDs and document presence were checked separately.

#### Remaining integration and useful conformance cases

Join actual C03/C04/C06 city calculations, V02/V03/V05/V07/V08 controllers,
N01/N02 network receipt, P03/P04 assets/fonts/controls, P05 event delivery and
P10 timers/media. Compare ordered observations, not only final text or pixels.
The following cases remain integration targets, not newly executed traces:

- Trade versus Science with Disorder and zero research cost; confirm their
  distinct stored totals/divisors without introducing a city recalculation.
- Demographic redraw mutating shared city scratch and displayed lower-is-better
  arrays; verify all row formulas and retained Military Service suffix.
- Foreign audience cancellation/timeout with network callbacks changing live
  peer, actor and mapping; exercise V05's exact owner sequence.
- Herald cache ties, custom multiword names, hidden intelligence, first-row
  retained second line, hotseat alias input and safe termination before any
  overwrite; compare palette imports and partial initialization failures.
- Ship typing reentry, half-completed Exit/Launch presses, blink timers, second
  render failure, state3 overlay and P10's separate visual-choice/media effects.
- Parley ordinary/Shift/Control selection, protected row0, stale identities,
  live layout/pane replacements, empty sentinel reads and unsupported geometry.
- Chat anonymous/muted/snooped forwarding, repeated recipients, macro offsets,
  history trimming, allocation failure and the destructor's empty log.
- Offer descriptions with one versus several items, grouped units, unknown
  identities, changed callbacks and bounded long text. Rejection must retain
  the valid prefix and must not corrupt the admitted offer.

#### Input identities

| Input | SHA-256 |
| --- | --- |
| `Civ2/civ2.exe` | `edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c` |
| `Civ2/Labels.txt` | `b614ab5e34853a10a0d05f1d0b4a1a722936c9b3cd8e1bc75452858fa5fb1fdd` |
| `Civ2/Game.txt` | `431132fc67b9f7b917bba0dc6f1f36c546cb55b88d43854bc4e11fc3b74c0ced` |
| `Civ2/ss.dll` | `2a163a486ccfe90ee4adfdb14408d2d7637949a2dfbbe2afc334301b4c86db6a` |
| `Civ2/mk.dll` | `1a1d503e70f411a891fdb834011f91bcbdbbafd9750d4aeea2d20e3d7576e196` |

### Appendix E. Original presentation reference

This appendix retains the original presentation procedures removed from the main
specification. Geometry, pixel/sample output, native controls and cosmetic timing
are optional reference behavior under X01. Required semantic fragments were kept
in the numbered main sections and are linked where omitted here; this appendix
does not relax their simulation, information, file or protocol obligations.
Original section numbers and local § references identify the recovered procedure
family, whether its retained fragment is in the main section or here. Historical
native/pixel tests are reference validation, not baseline acceptance criteria.

#### Original §2. Shared auxiliary window

##### Setup and cleanup

Setup takes resource, selection, modal flag, requested background width/height
and requested window width/height. Construct a temporary Palette, then clear
selection on the **canonical primary auxiliary window**, even if setup's
receiver differs. Publish the common control font. Reset receiver scrollbar
created0, private state0, pageCount1, first item0, actor1, target0; publish
selection and modal. Query native scrollbar width. Capture caption and margin
as two separate shared-spacing reads before pumping/loading.

If resource<0 negate with wrap; otherwise add49 unless it is the credits-file
sentinel. Store requested background dimensions, pump messages, load resource
art (or `scredits.gif`) with palette start10/count192, then pump again. Ignore
the loader's ordinary return. Each requested background dimension<1 is replaced
by the loaded surface's corresponding dimension. Publish requested window
dimensions; zero means the live background dimension, negative means
`backgroundDimension−requestedDimension` (it increases the extent).

Center using outer width=window width+2*captured margin and outer height=window
height+margin+caption. Capture screen height before querying main nonclient
height; available height is their difference. x is half(live screen width)−
half(outer width), y=half(available height)−half(outer height). Create the shared
intro-style native surface with the **live background dimensions**, captured
x/y/caption/margin and the live modal flag, then destroy Palette. Completed
effects remain if a subsequent provider fails. [P04 shared intro creation](P04-text-and-dialogs.md#shared-intro-style-surface-creation)
owns the surface options and live post-create margin handling.

Clear-selection returns if selection<0. Otherwise set selection−1 before
removing controls, resetting background storage and releasing compound window
storage. Background-region copy takes source x/y/width/height unchanged and
destinations client origin+x/y. Whole-background copy captures height then
width before calling it.

The standard Close button starts `(2,0,596,24)` and is translated to client x
and `clientY+400−height−2`. Configure ID100 with label327, install Close,
register as default and cancel. Close captures the current shared control parent;
its auxiliary receiver determines the action: modal => clear its event loop;
nonmodal => clear selection. Missing parent requires a stop at its original
access, not an invented close of the canonical window.

##### Ordinary and modal opening

City Status and Attitude call setup with matching report/resource IDs1 and4,
nonmodal,600×400 background and zero requested window dimensions. Store incoming
actor **after** setup callbacks; prepare Close, install the corresponding draw
callback, update, then show. Top Cities uses ID8 and modal1 with the same size.
After update/show it installs intelligence idle only if live game mode≥3,
samples shared timeout ticks, runs modal, clears idle and clears selection.
Other report-specific button/scroll setup is specified with its renderer.

Intelligence idle polls network `(1,0)` first, samples ticks, and tests signed
wrapping `now−live start >1200`. If true, clear the auxiliary loop then obtain
a **fresh** tick sample and publish it as start. This differs from P06's
production timeout, which compares an absolute signed deadline.

#### Original §4. Top Cities

[Required behavior is retained in the main section.](#4-top-cities)

Prepare frame/status/background; y=clientY+2,bottom=clientY+372, heading font and
style37/18 offset2/1. Center label341, advance heading height, then10. Publish
row height24, click top=afterHeading+18 and available height=bottom−y, although
this renderer installs no city-row click handler.

For each nonnegative ranked city, preserve rowTop. Center rank+P04 sentence
separator+live city name+space+parenthesized live owner adjective. Select heading
font and current city-label color/18 offset1/1. Advance y24; draw city at
clientX+5,y−15 with flags4096, zoom0. Let left=cityX+66, right=clientX+width−5,
midpoint=left+half(right−left). Draw report citizens at left,y over
midpoint−2−left, with live signed happy/unhappy and excess0; there is no recalc.
Starting x=midpoint, scan all28 live wonder locations; if equal to city and
right−x>35, draw its icon at x,y+6 and advance x38. Advance y32, outline
`(clientX+3,rowTop,clientX+width−3,y)` with live owner shield color, then advance
y6. Missing ranks consume no row space. Present.

#### Original §14. Power-history graph

A01 owns publication of the150 samples; F04 owns calendar conversion. Construct
window, graph font, legend font and Prompt in that order; replace fonts with
face0, sizes12/14,weight1. Allocate/construct background port then palette,
load resource51 at palette10/count192 (ignore ordinary failure), and create a
centered modal intro surface600×425 titled label214, using shared caption/margin.
Draw, show, then create a prompt over the live surface dimensions/origin and run
modal0, discarding choice. Normally delete background port then palette,
destroy Prompt, legend font, graph font, window. Application exception cleanup
owns successfully constructed local objects and pending raw allocation, not
completed heap graphics objects; preserve prior effects without inventing a
rollback or successful render after null allocations.

Capture client origin before frame/status callbacks. Copy background600×400,
fill the live inner rectangle below clientY+400 with20, outline rectangle
(clientX+8,clientY+2,584,366) in10. Objective-scenario mode chooses sample divisor2,
otherwise4; capture sampleCount=limit(turn/divisor,0,150). A **separate** live
mode test chooses grid step25 or50. Maximum starts50 and scans every
civilization1..7 and every sample below sampleCount, including dead/unshown
civilizations. A larger sample replaces it via a fresh read.

For ticks0..live turn AND≤600, step by captured grid step. Determine x using
live objective mode (2*tick if true, tick otherwise). Draw vertical line at
clientX+x+8 from clientY+2 to+368,color10. On ticks divisible by2*gridStep,
format F04 year(tick+1), style37/10,offset2/1, draw at clientX+x,clientY+370.
Set clip to graph rectangle. For each civilization currently alive OR previously
alive, draw its shadow polyline: previous=(0,0), then samples0..sampleCount−1,
x=4*sample,y=history*360/maximum; segment endpoints add clientX+9 and
clientY+369−height,color18. Even first sample draws from zero baseline.

Restore clip to the **live** inner rectangle. Select legend font. Rescan
civilizations1..7 with the live alive-or-previously-alive predicate. Capture
shield color, select color/10,offset2/1, draw civilization name at
clientX+10,clientY+1+14*legendOrdinal (increment ordinal before name callback).
Draw a fresh polyline with that color and endpoints clientX+8,clientY+368−height.
The foreground can observe changed samples/alive sets after shadow callbacks.
Do not rescale horizontal spacing to window width or omit extinct civilizations
from maximum computation. This window installs no intelligence timeout itself.

#### Original §18. Tax-rate window layout

[V02](V02-governments-and-tax-rates.md#8-rate-dialog-lifecycle) owns
opening guards, normalization, held rates, callbacks, preview effects and commit
lifecycle; this section specifies its controls and drawing. After V02 has
published normalized rates, initialize background400×256,horizontalMargin10,
verticalGap6,scrollHeight=native horizontal-scroll metric. If live popup scale≥1,
replace each dimension/margin/gap/scrollHeight by trunc(wrap(3*value)/2) and use
resource66, otherwise65. Load with palette10/count192, ignore ordinary result.
FontHeight is cached application title-font height. Measure Hold label258;
scrollWidth=backgroundWidth−2*margin−(fresh margin+cached labelWidth).
Create centered modal intro surface with title257, shared caption/margins and
captured background width/height.

Checkbox x=clientX+scrollWidth+2*margin+half(cached labelWidth)−16.
Capture scrollX=clientX+margin and store textX. Start y=clientY+gap;
governmentY=y,budgetY=y+2*fontHeight,researchY=budgetY+fontHeight,
taxLabelY=researchY+2*fontHeight. Advance fontHeight to first scroll y;
checkboxY=that y+half(scrollHeight)−16. Configure horizontal scrolls101/102/103
for Tax/Science/Luxury, each width scrollWidth,height scrollHeight, range0..10,
initial **live** rate after range callbacks and its V02 rate-change callback.
Between scrolls advance y by live gap+scrollHeight, publish next labelY, advance
live fontHeight. Capture scrollX through all three setups.

Close height=P04 button-metric font height+8,width=clientWidth−4; x=clientX+2,
y=clientY+clientHeight−(height+2). Disable control traversal before rectangle
creation. Close201,label254 installs V02 close and default/cancel roles. If live
popup scale≤0 after Close setup, add6 to checkboxY. For three Hold options300..302,
copy shared persistent hold flag to window field, configure empty label at
checkbox position,set value,install V02 hold callback; advance checkboxY by
live gap+scrollHeight+fontHeight after each, including the last. Then calculate
V02 preview, install paint callback,update/show and enter its lifecycle.

Paint shared status first. If no background port or inactive, fill live inner
rectangle29; otherwise copy background at live client origin, capturing height
before width. Select application title font. Government/max line uses37/18 or
warning106/10,offset1/1; consume warning when selecting its warning style.
Compose label152:government,three spaces,label256:10*cap`%`; center across live
textX/scrollWidth at governmentY. Reset37/18. Draw `0%` left and `100%` right at
all three rate-label y positions. Draw Hold258 at clientX+scrollWidth+2*margin,
taxLabelY. Center labels89/91/90:10*Tax/Science/Luxury`%` respectively. Center
budget label136:preview income,three spaces,label135:preview upkeep. Research
line label137:V02 full-cost turns,space,label44; V02 owns its cost/science floors
and previewScience mutation. Present. Every text helper uses freshly read
layout fields at its own call, not a captured whole-layout snapshot.

#### Original §19. Spaceship presentation

V07 owns selectors, construction/launch mechanics, success calculation and the
local/general/negative-argument entry decisions. Its presentation states are
Incomplete0,Ready1,LaunchRequested2 and InFlight3. This section owns rendering,
statistics typing and interaction; [P10](P10-audio-video-and-animation.md#spaceship-launch) owns media/timer services, launch-video
playback and the moving star-field effect. Timer callbacks and typing use F01
sampling laws and may use an independent presentation context. Preserve real
callbacks/state effects without requiring the original shared gameplay sequence.

##### Construction, assets and statistics

Construct outer window, inner surface, resource module, palette, title/success/
statistics fonts, composite port, component sprite, three Exit sprites, three
Launch sprites and P06 interaction list, in that order. Publish civilization,
statisticsTimer0, query screen rectangle, textOffset0, then publish active window.
Fonts use face0,heights24/20/16,weight1. Set blink0,typingComplete0; allocate
457×304 composite and fill9. Set starTimer0,statisticsTimer0 again,blinkTimer0;
statistics rectangle=(10,20,300,479),video token0,state0,ExitPressed0,
LaunchPressed0,labelPhase0. Fields not initialized here retain their established
incoming state until their producer runs.

Resolve `ss.dll`; failure returns0 before resource/window setup. Load module,
load icon508, call V07 spaceship calculation `(owner,1)`, then format statistics.
V07 readiness/Launched checks choose presentation state. Pump; create black
outer window over screen rectangle with height increased5. Create640×480 inner
surface with spaceship palette, load resource499 at palette10/count236; failure
returns0. Center it, install key and move/down/up handlers. Prepare button sprites;
its ordinary asset failure does not make the enclosing load fail. Reset/clear
star,statistics,blink timers in that order if nonzero, set textX10,textY40,
rebuild regions,return1.

[Required behavior is retained in the main section.](#19-spaceship-presentation)

Button preparation constructs a temporary port, selects color139, loads497
at palette10/count236; ordinary failure destroys port and returns. Draw Exit
label254 into rectangles of size52×36 at(1,1),(54,1),(107,1), extracting sprites
0,2,1 respectively with transparent index9. Select color198. Launch text is
label213 for language0, `Lancer` for1, `Starten` for2, empty for every other
full language value. Draw/extract67×36 at(1,38),(69,38),(137,38),again0,2,1.
Destroy temporary port. Font is statistics font; rectangle text flags0.

##### Component assembly

Cache six signed raw part counts in order0..5 when the V07 modal caller requests
it. Rendering uses this shared cache for **admission**, and fresh V07 capped
part counts for **primary versus overflow artwork**. Appendix C contains the
88 ordered logical records; no original stack layout is required.

Construct12 temporary graphics ports; ordinary/exception cleanup destroys only
successfully constructed ports in reverse order. Load resources in order
`441,480,455,490,470,442,481,456,489,471,491`, each palette10/count236. Any ordinary
failure cleans all constructed ports and returns0; pump after each success,
including the last. Primary/overflow resource pairs for parts1,4,2,5,3 are
(441,442),(480,481),(455,456),(490,489),(470,471). Part0 uses491. Extraction uses
transparent9,sourceX from record,sourceY1,width/height from record. For a nonzero
part, capture its ordinal before querying the **live owner**'s capped count;
choose overflow image when count<ordinal, otherwise primary.

1. Rows0..6: pump once per row; if ordinal≤live cached count for its part,
   extract component and draw onto composite at(recordX−175,recordY−99).
2. Start selected0. Scan rows7..30 and replace selected whenever ordinal≤live
   cached count. Pump, then load **that selected record's resource**, failing
   with cleanup/return0. Pump again; extract at record x/y/width/height using
   transparent9, draw to composite at x−175,y−99. Selection survives both pumps.
3. Rows31..87: pump **twice unconditionally per row**, then apply the same
   admission/extraction/draw operation as step1.
4. Extract457×304 from composite origin with transparent9, pump; if live state3
   clear destination to0. Draw at175,99, draw title for live owner, cleanup,
   return1. Do not clear the composite on every render; construction filled it.

Title composition clears shared line, appends civilization name, then forcibly
terminates after its first character (empty name stays empty). Append `.S.S. `,
ruler name,space,`(`,adjective,`)`. Color250; draw with title font in
(0,0,639,40). Active window is reloaded separately for font and drawing target;
pump afterward.

##### Buttons and hit regions

P06 owns first-match interaction-list semantics. Clear regions. For local owner
and state neither0 nor3, add Exit0 at(501,417,52,36) then Launch1 at
(559,417,67,36). Otherwise add only Exit0 at(559,417,67,36). This admits Launch's
region even in state2; its action gates remain separate.

Exit art uses the same location condition, with normal52×36 sprite even in the
67-pixel-wide fallback rectangle. Launch drawing returns unless local owner and
state1. For either draw, secondFlag0 chooses sprite0, else firstFlag0 chooses
sprite2, else sprite1. Present its rectangle and flush paint. Redraw-buttons
first clears(501,417,553,453) to0 when state neither0 nor1, then Exit(1,1),
Launch(0,1). Blink toggles its retained boolean, then Launch(0,1) when nonzero,
Launch(1,1) otherwise.

##### Statistics typing

A timer step uses randomGate1; if inclusive random(0,1) is nonzero, return1
immediately with no text/font/message activity. Forced completion uses gate0.
Capture statistics-font height, pump, then copy current signed text byte into
a one-character NUL-terminated string. Select color249, then color249 for live
labelPhase or254 for value phase. Draw at live textX/textY. Form a present area
using live y−capturedHeight,y+2*height,live x and x+fresh text width. Query text
width **again** for x advancement. If the freshly read current byte is colon,
set labelPhase0; increment live offset.

Consume all following newlines: labelPhase1,textX10,textY+=fresh font height,
offset++. If the next byte is NUL, reset/clear a nonzero statistics timer. Unless
live state3, set success rectangle(0,449,639,479),draw success text at offset(1,1)
in10, restore rectangle and draw in249 if live success<40,251 if<75,else250;
present that rectangle,flush. If live state1, register repeating blink timer500ms
and publish its result. Set typingComplete1. Finally present the previously
captured character area and return whether the **live** next byte is nonzero.
There is no early empty-text guard before drawing/increment; out-of-range text
reads require backing/supplied state or a stop.

##### Pointer and key actions

Callbacks capture the active ship window once. Key and click/release also capture
the native current window before subsequent drawing. Only internal keys208..210
act. In state2 clear video's modal loop and return. Otherwise draw pressed Exit.
If typing incomplete, reset/clear statistics timer, force typing to completion
with at least one step, set complete1, redraw raised Exit; else clear the
captured current window's loop. No launch action is bound to these keys.

Press returns if either pressed flag already set. Find region; no match returns.
Exit sets ExitPressed1 and draws pressed Exit. Launch sets LaunchPressed1 only
if state1 AND typingComplete, and draws pressed Launch. In either admitted case,
reset/clear blink timer if state1/nonzero, then present whole surface.

Release in state2 clears video's loop immediately. Otherwise return when neither
pressed flag set; redraw each pressed button raised before hit testing. On Exit
hit with ExitPressed: if incomplete, reset/clear statistics timer and, unless
state3, force typing with at least one step and set complete1; if already complete,
clear captured current loop. On Launch hit with LaunchPressed set state2 and
clear current loop, without repeating readiness/type-complete checks. Then
process ExitPressed and LaunchPressed independently in that order: clear each
still-live flag and, when state1 with blink timer0, register500ms repeating blink.
Do not cache LaunchPressed across Exit's timer registration callback.

Move never clears pressed flags. No hit redraws each pressed button raised and
restarts a missing state1 blink timer after each. Over Exit: if LaunchPressed,
raise Launch/restart blink; otherwise if ExitPressed, draw pressed Exit and
reset/clear nonzero state1 blink. Over Launch is symmetric with Exit taking
priority. An unpressed pointer entering a button has no effect.

The separate Click handler is used by video: state2 clears video loop. Otherwise
hit Exit draws pressed; incomplete typing resets/clears timer and, unless state3,
forces completion,set complete1,raises Exit; already complete clears current loop.
Hit Launch only acts in state1/complete: draw pressed,set state2,clear current loop.
Video-finished callback clears video's loop only for nonzero live video token.

##### Modal sequencing and timer boundaries

V07 owns launch/no-launch decisions. General modal constructs temporary port and
sprite, caches counts, renders; render0 destroys sprite then port and returns.
In state3, load resource19999+live cached propulsion count, ignoring ordinary
return; extract and redraw the selected rectangle from Appendix C using separate
live count reads for height,width,y,x and later draw y/x. Capture dispatch state:
0/1 draws Launch(0,1);3 invokes P10 initial star field and redraws live-owner title.
Draw Exit(1,1). Reset existing statistics timer without clearing before registering
its replacement. State3 registers star timer5ms, other states typing timer60ms.
Show outer,flush,assign its palette,show inner,flush,assign main palette; rebuild
regions,redraw buttons,set complete=(live state3),labelPhase1. Capture mouse,
discard queued input, optionally install network idle when mode>2,record ticks,
run inner modal,clear idle,release capture. Reset/clear star,typing,blink timers
in that order. V07 then either invokes launch video,sets state3,launches live
owner and recursively enters this general modal, or hides inner/flushes.
Restore main/outer standard palettes,hide outer,flush,destroy sprite then port.

Local-build modal rejects nonlocal owner, resets existing typing timer, returns
in state3, renders and returns on0. Draw both buttons,show/palette sequence as
above,regions/buttons,complete1,labelPhase1. Capture state,store0,restore captured
state; cache counts and render again. **Second render failure returns with shown
windows/palettes retained.** Draw Exit pressed,Launch(0,1),P10 block reveal of
640×480 `(5,1)`,raise Exit; register typing timer10ms. Rebuild regions/buttons,
complete0,labelPhase1,then capture/discard/idle/modal/cleanup as general modal.
Restore main palette **before** its V07 launch/no-launch branch; restore outer
palette/hide/flush afterward.

[Required behavior is retained in the main section.](#19-spaceship-presentation)

P10 star initialization
sets text offset0,x10,y115,labelPhase1,forces typing,then replaces success text
with label454:space,arrival year; draws it in250 with shadow,draws composite art
at175,99 and,if state3,sets textY140. These assignments are the join between its
star effect and this report; P10 specifies all star random draws and pixel bounds.

Normal destruction closes resource module, clears active window, resets/clears
star/typing/blink timers in order, then destroys Launch sprites reverse,Exit
sprites reverse,component,composite,statistics/success/title fonts,palette,module,
inner surface,outer window. P10 launch-video failure effects are preserved; a
successful outer report return is not proof that media playback succeeded.

#### Original §20. Diplomatic herald artwork and science display

V05 owns audience decisions. This section owns their graphical presenter; P10
owns [animation frames, media opening and sound playback](P10-audio-video-and-animation.md#15-herald-video-and-spaceship-stars). Art, palette, font and
media-provider results are explicit inputs. Construction creates background,
art surface, tile port, palette, resource module and embedded video, queries the
screen and initializes requested animation0.

##### Contact opening and cleanup

Clear audio gate. Set pending sound to `((unsigned map seed+other)&7)+83` without
an RNG draw. Resolve the absolute signed leader index into the 21-entry sound
category table (P10), then base sound=`category*4+112`; retain base+1,+2,+3.
Stop existing MCI playback through the application exit-playback hook. Delete
an existing herald, then clear its shared reference. Allocate/construct and
publish the replacement; null returns. Only now test GraphicalDiplomacy: when
off, leave the constructed object without initializing or showing its art.
Art initialization failure also returns without the successful-open tail.
On success set audio gate1, publish the main herald art/palette as the default
prompt parent/palette, and show the herald.

Art initialization clears active animation, copies initializer palette entries
10..41, and stores actor, other and leader as unsigned low-byte values. Resolve
`civ2\mk.dll`; failure returns0. Open the module, ignoring its ordinary result,
and select P06's citizen-art group for the live other civilization. Invoke tile,
background, main art, military, science and portrait initialization in that order.
Each zero result shows `init_NAME failed.` with title `NOTICE` and returns0;
completed earlier children remain. If DiplomaticAnimation is enabled, call
P10's video initialization and publish only its low-byte result as active
animation. A video failure does **not** change the overall art success1.

Show background, flush, assign herald palette to background then main art;
show video if active, show main art, flush; if still active, play P10's intro
range using the live leader. Close the module, clear the shared herald transient
flag and play pending sound with the continuation that, if audio gate remains
set, plays the live base sound with arguments `(1,1,0)`.

Close hides main art and flushes, assigns initializer palette to background then
main art, hides background and flushes, closes background and module, clears
default prompt parent/palette, resets main-art storage, closes background again,
and resets prompt position to0,0. Cleanup returns immediately when audio gate0;
otherwise clear it before calls, close the shared herald, recapture and delete
the live shared object, clear the shared reference, clear pending sound, wait
with messages30, cancel the **live** negative base-sound ID, then reconcile exit
playback. Destruction clears active animation and destroys video, module,
palette, tile, main art and background in that order. Reentrant replacement is
not automatically restored.

##### Static composition

Tile allocates64×64, ignores allocation's ordinary result, loads resource299
with palette start10/count32 into the initializer palette, and returns whether
loading succeeded. Background creates a main-window child at0,0 with width
`screen.right` and height`screen.bottom+5`, then tiles it; it does not subtract
screen.left/top and returns1.

Main art constructs a temporary palette. Its640×480 surface is positioned at
`((screen.right−640)/3,(screen.bottom−480)/3)` with signed truncation. Load
resource`200+live other government`, palette start10/count236. Failure destroys
the temporary palette and returns0. Query the live other's shield color,
copy that initializer RGB into temporary entry145, import temporary entries
106..145 into the herald palette, assign it to main art, destroy temporary
palette and return1.

Military constructs sprite, palette and port, then captures
`count=unsigned powerRank(other)−1`. Zero skips loading and succeeds. Otherwise
load215 with palette start10/count236; failure cleans up and returns0. Extract
transparent-color9 sprite `(group*109+1,156,108,30)`, draw `count` rows at
`(45,130+31*i)` for signed `i=0; i<count; ++i`, then destroy port, palette and
sprite. A power rank0 produces count−1: loading/extraction still occur but no
row draws. Do not reinterpret it as a huge unsigned iteration count.

Portrait constructs sprite, palette and port; loads
`220+unsigned gendered leader` with palette start10/count236. On success import
entries42..105 into the herald palette, assign that palette to the port,
extract transparent-color9 `(1,1,225,275)` and draw at208,57. Destroy port,
palette and sprite, returning the load success.

[Required behavior is retained in the main section.](#20-diplomatic-herald-artwork-and-science-display)

[Required behavior is retained in the main section.](#20-diplomatic-herald-artwork-and-science-display)

#### Original Chat and Parley geometry, redraw and controls

Shared layout inputs initially are vertical gap`v=2`, horizontal margin`m=8`,
inner border`b=2`, button padding`p=2`, spacing`s=2`. Each use is live unless
captured below. Fonts all use face0: small/large heading20/30, pane16/24 with
weight1, list14/21 ordinary, buttons16/24 ordinary. Font metrics are provider
results; replacing a font need not preserve its previous dimensions.

##### Window geometry, redraw and controls

Region calculation queries surface width then height and publishes both.
Caption=`2*v+2*b+headingHeight`. Set full rectangle0,0,width,height. Inner size
is `(width−2*(m+s),height−(2*v+2*caption+m))`, positioned at
`(m+s,caption+v)`. Layout uses base520×335 for mode0, otherwise800×460, adds
`2*(m+s)+6` to width and`2*v+2*caption+14` to height. When saved rectangle width0,
center against main client dimensions, storing x after the width query and y
after the height query. Otherwise use retained x/y. Store rectangle and caption.
Choose-position writes layout0 to **current** window, changes it to1 if screen
width≥1000, then lays out the incoming receiver. Retained nonzero x/y avoids
this automatic mode choice.

Initialization sets closed1, selects receiver as current and stores mode. Chat
performs §21's history/profile initialization. If x==0 and y==0 choose position.
Clear title, create the surface with saved rectangle and initializer palette,
lay out, move to saved rectangle, invalidate cached layout, request the
ComposeAction pair, set both cached panes invalid, install close/redraw/move/key
callbacks, calculate regions and refresh controls. Modes AwaitOffer,
AwaitResponse and ComposeAction disable NeverMind before updating buttons.
Move callbacks query/store the actual native rectangle and copy its x/y.

Redraw returns when closed. Otherwise calculate regions, build §21 title,
select drawing target, reset clip, then draw background: copy full rectangle,
draw `b` nested borders using P04's first/second border colors, insetting1 each
time, and fill region1. Inset horizontally by`m−2*b`, set top to the resulting
top+caption−2*b and bottom=surfaceHeight−caption−2*b. Draw `b` nested reversed
borders and fill region2. Region1/2 use their respective supplied texture ports
when present, otherwise P04's default fill color. Draw the three caption icons
Close/Shrink/Grow from x=m; width/height are each `sourceExtent*live caption/24`
with wrapping multiplication and truncation. y=`half(caption)−half(height)`;
select scale(caption,24),draw,reset scale,advance x by captured width+live s.
Subtract s from final x, then center title in the remaining span
`(surfaceWidth−x−m)/2`. Title y=b+v: shadow10 at+2,+1, foreground26 at+1,0
then0,0, flags5. Set clip to inner rectangle, refresh controls,present,show,
collapse history to end,then input to its existing selection end.

Refresh captures layout. If it differs from cached layout, clear all controls,
create them, select requested left then right pane, and cache the **live** pane
IDs and layout after callbacks. Otherwise independently compare, rebuild and
cache left, then right. Pane updates alone do not rebuild the bottom controls.

Control creation publishes each allocation before configuring it and recaptures
its owner slot before installing callbacks. Null allocation does not imply a
successful skipped control; stop at the original required access. Create
caption click surfaces over the three icon rectangles, then choose button font.
Bottom Close has x=b+s+2, width=surfaceWidth−(2*s+2*b+5),
y=surfaceHeight−(2*(v+p)+buttonLineHeight), height=2*p+buttonLineHeight;
label715 in Chat,716 otherwise. Then create history and input edits:

- Capture x=inner.left+s,width=inner.width−2*s. History rows are9 in Chat,
  otherwise6. y=inner.bottom−rows*(v+listHeight), height=rows*listHeight+(rows−1)*v.
  Initialize from shared history, read-only multiline, text limit8192, §21
  edit-key callback,show.
- Retain that width and y across history callbacks. Input width becomes
  `width−width/10`; input rows3 in Chat,otherwise2. Subtract
  `rows*(v+listHeight)+v` from y; height=rows*(listHeight+v). Initialize shared
  input with its multiline style (Appendix A), text limit256, §21 send-key
  callback and key-handling flag,show.
- Send label717 occupies the strip immediately right of input, width=inputWidth/9,
  height=inputHeight/2, aligned at its top. Clear label718 occupies the equal
  rectangle immediately below. Both divisions truncate. Set y back to input top,
  set actionHeight=2*p+buttonLineHeight, subtract v+actionHeight; publish
  dialogBottom=y−v.

Non-Chat then creates these controls in order. ConcealAll(label877),
RevealAll(878) each cover the full inner width less2*s, at(y−actionHeight,y),
and start hidden/disabled. Below them at y create Intelligence104 in the first
third, Send719 in the second third, alternate Send836 overlapping Send but
hidden/disabled, NeverMind720 in the third. Then create Accept721, Decline722,
Counter723 in those same three thirds, initially hidden. Their callbacks are
V05/N02 actions. Appendix A records control IDs; duplicated IDs do not merge
these distinct control objects.

Caption click focuses/selects captured current first. Close invokes close using
fresh current. Shrink only acts for signed mode>0; Grow only for mode<1 and
screen width≥1000. Hide captured surface, decrement/increment its **live** mode
with wrap,layout,move to saved rectangle,show. Escape closes through the same
focus-first route. Close's negotiation cancellation and messages remain V05;
its final interface tail stores closed1,peer=None,hides captured window,
focuses/raises main and prepares queued-effect presentation. Double-click on a
choice list queries Send enabled, then alternate Send enabled only if needed;
if either is enabled, focus/select captured current and invoke V05's action.

#### Original §23. Parley selection lists

[V05's parley selection rules](V05-diplomatic-negotiation.md#parley-modes-and-local-selection)
own row eligibility, stable-identity preservation and transaction admission.
This section owns list ordering, display and local selection. Each side retains
count, values, stable IDs where applicable, selection flags, first row, range
anchor, page rows, list rectangle and readiness. City rows additionally retain
unit counts. A selected flag is tested as nonzero for highlighting/readiness,
but V05 packs only flags exactly1. All loops use signed comparisons of F01
wrapping counters. No array may grow by overwriting another logical field.

[Required behavior is retained in the main section.](#23-parley-selection-lists)

##### List controls and clicking

After population, set first0 when signed count>0, otherwise−1. Set list kind
Unit0, Civilization2, Advance3 or City4 and copy the incoming rectangle. When
count>pageRows, query native scrollbar width and subtract it from the list's
right edge; query width **again** to set the scrollbar's left edge from the
captured input rectangle's right. Create vertical scrollbar with initial0;
range maximum/page size are:

| List | Maximum | Page size | Rendered rows |
| --- | --- | --- | --- |
| Advance | count−1−pageRows | pageRows | pageRows+1 |
| Unit | count−pageRows | pageRows−1 | pageRows |
| City, Civilization | count−pageRows | pageRows | pageRows |

Install the same position callback for both scrolling notifications. Create a
click surface over the resulting list rectangle, install the same handler for
both click notifications, then draw. Appendix A records original control IDs.
Scroll focuses/selects a captured current receiver, writes its first row from
the full supplied position without clamping, then redraws using fresh current.

Click captures current, focuses/selects it, derives side from control ID and
queries client cursor coordinates. The original failed query leaves incoming
coordinates unspecified; a declared safe profile may ignore the click after
those effects (Appendix B). A zero count returns side. Hit testing recaptures
current and checks y<top,y≥bottom,x<left,x≥right in that order, returning
−1,−2,−3,−4 respectively. Otherwise:

```
rows = pageRows + (Advance ? 1 : 0)
rowHeight = rectangleHeight / rows
selected = (y - top) / rowHeight + firstRow
```

Both divisions truncate and can fail separately. Preserve the completed first
division before a second-divisor failure. The caller rejects negative selected,
and **City rejects zero too**, regardless of whether row0 is actually a Palace.
Reject selected≥the captured receiver's live count. Then sample Shift first:
if set, clear every current selection, set the inclusive range between old
anchor and selected, leaving anchor unchanged. City skips indices≤0 in this
range; other lists do not impose that filter. A range beyond established rows
cannot overwrite adjacent state. Otherwise sample Control: when set, store
anchor=selected and XOR that row's flag with1. Without Control, first clear
all other rows, then store anchor and XOR the chosen flag. A second ordinary
click can therefore leave no row selected. Finally redraw, forwarding its
ordinary result if consumed. Do not normalize nonbinary flags before XOR.

##### Shared row renderer

Capture current and its port, save clip and set the list clip. Fill list with
P04's ordinary list-background color. Select small/large list font by live
layout mode, capture left,width,height, calculate rowHeight as above and
vertical text offset=`rowHeight/2−fontHeight/2`, using truncation for both halves.
Only after geometry clear readiness, then scan all flags and set readiness1
on the first nonzero flag. For each live page row, set y=top+row*rowHeight;
Advance adds shared vertical gap. Build a full-width row rectangle.

When first is−1, only row0 is eligible: later rows break Unit/City rendering,
but continue the Advance/Civilization loop. Otherwise skip when
signed(first+row)≥count. Read the selection flag at first+row **before** rendering
an empty-row label; this sentinel read needs established preceding storage or
an explicit retained input. No zero flag is inferred. Select ordinary or
selected foreground/shadow/background colors, and fill only the text portion
of the row after the icon inset described below. City row0 when unselected
uses the disabled foreground and selected shadow.

Text drawing uses flags5. If shadow differs from foreground, draw shadow at
`(textX+2,textY+1)`, then foreground at`(textX+1,textY)`; always finish with
foreground at`(textX,textY)`. After all rows restore saved clip, update dialog
buttons and show the side click surface, returning that show's ordinary result.
The icon target is the **canonical Parley port**, even if text uses another
current receiver.

**Advances:** read row advance and signed category/epoch before testing empty
sentinel. Select the category×epoch icon. Text inset is
`2*iconContentWidth+3*spacing`; fill starts exactly there. For a real row, icon
inset is spacing on even displayed index, `iconContentWidth+2*spacing` on odd;
center icon vertically in the row. Text is advance name. For an empty row use
label875 when side0 or canonical mode GiveAdvance, otherwise874, clear readiness,
and center text using `(live listWidth−measuredTextWidth)/2`. Even an empty row
therefore requires backed advance/icon inputs before its empty label.

**Units:** initialize resolved unit0. When count>0 resolve the row's stable ID
through V05's unit lookup. A negative result repopulates with preserve1. If new
count0 set first−1; otherwise the original compares two successive reads of
**count against count**, not first against count, and only if the later count
is greater stores a fresh count−1 in first. Restart rendering from fill/geometry
without restoring clip. A stable positive count therefore does not fix a stale
first row, and repeated failed lookup can repeat the redraw indefinitely.
Read the resolved unit's type and sprite even for an empty row. Small layout
uses spriteWidth=`2*contentWidth/3`, large uses contentWidth; text inset is
`2*spriteWidth+(small?6:10)` and fill starts inset−3. Empty uses label735,
clears readiness and centers the text. Real rows select sprite scale
`(2*fontHeight,48)`; odd displayed indices add`(fontHeight<<7)/48` to icon x,
even add0. Icon y is row center−`contentHeight*fontHeight/48`. Draw then reset
scale1/1. In Type mode start with type name and append` (home)` only in GiveUnit,
left Barter with local unit disclosure, right Barter with peer disclosure, or
Demand with peer disclosure. Home mode always displays`home (type)`.

**Cities:** resolve stable ID when count>0; exact−1 triggers the same preserve1
rebuild and count-versus-count restart as Units. Empty uses label734 and clears
readiness. Text inset=`(small?60:90)+spacing`, fill starts inset−3. Detail flags
are P02 SuppressAnnotations/SuppressGarrison when this side's city-detail disclosure is
clear, otherwise0; GiveCity forces0. Real row city art uses zoom−4/−2 for
small/large, x=listLeft+2 plus24+2/36+2 for odd indices; y subtracts
`(scaled(48,zoom)−fontHeight)/2`. Append city name and a space. Query Palace;
if absent and flags0, append`(`,stored unit count,space,label732 for count≤1
otherwise731. There is **no closing parenthesis** on that branch. Otherwise
query Palace again and, if present, append label733. Do not deduplicate the two
Palace queries across callbacks.

**Civilizations:** same text inset/fill as Cities; empty uses label737 without
centering and clears readiness. For a real row, choose its representative city
by scanning ascending live cities of that civilization: score=signed size,
+200 for Palace,+100 when signed production item==−1. Initial bestScore0,
bestCity−1; replace only for strictly greater score. If none positive, draw no
icon. Otherwise use the same small/large city positioning as Cities, flags0.
Then resolve live civilization name and ruler name separately and display
`civilization (ruler)`.

[Required behavior is retained in the main section.](#23-parley-selection-lists)

#### Original report window and font inventory

The primary auxiliary window holds report selection, actor/target, modal state,
background/client geometry, controls, first displayed item, row origin/height,
visible rows, page count and scrollbar-created state. A second auxiliary window
is used by specific reports. Shared report fields include intelligence timeout
ticks/retry, city-calculation scratch and ranking/history state. Report fonts initially use face0: heading18 with
ordinary weight, detail14 with weight1, Hall16 with weight1, fame-small12 with
weight1, and controls16 with ordinary weight. P04 defines their metric and
replacement semantics; provider results remain inputs.

#### Original report heading composition

Capture actor before frame/status/background callbacks. Prepare P04 shared
frame, draw status, copy background. Set y=clientY+2, bottom=clientY+372,
capture heading-font height, select target/font and style37/18 with offset2/1.
Center report title; advance by captured height. Center government-prefix
label `limit(government−1,0,4)+328`, space, label140, space, civilization name;
advance. Center ruler title, space, ruler name, colon and current year. Let
afterHeading=y+height and first row y=afterHeading+2.

#### Original city-report row placement and scrolling

City x=live clientX+2+64*(post-increment ordinal mod2), y=current drawing row;
draw P02 city flags0/zoom0. No alphabetic sorting or unconditional recalculation
is inserted. Rows advance only when displayed.

Only when scrollbar-created is clear, create vertical scrollbar ID200 at
`(innerRight−scrollbarWidth−2,firstRowY,innerRight−2,bottom)`, set range
0..`limit(count−1,0,999)`, current first item, page=live visible rows, callback,
show it, install left-click and set created1. Later redraws still recalculate
paging fields but do **not** update that native scrollbar through this branch.
Present after drawing. Scroll stores the full supplied first item and redraws.

#### Original city-report click mapping

Both click handlers ignore x. Compute relative=y−live click origin; negative
returns. Divide by live row height with the original signed division boundary;
row≥visible rows returns. Select ordinal=row+live first item. Scan city slots
again, requiring identity and signed owner==**live report actor**; on matching
ordinal invoke P06 open city and return. There is no separate negative-row
test after division or native scrollbar validation.

#### Original City Status row rendering

After city art, select detail font and style37/18 offset1/1. Draw live name at
clientX+140,rowY+9. Set resource start=nameX+105. For Food, Shields and net Trade,
clear shared text, append the stored signed city value, draw at current x and
capture returned end+2; draw the corresponding large positive resource icon,
then advance x by18. This renderer does not call C03 for those values.

Production text begins resourceStart+120. A negative live production chooses
its improvement name and unsigned cost; ordinary buildings keep the current
style, wonders use94/10. A nonnegative production uses122/10, unit name and
signed cost. Append space and draw, retaining text end+4. Draw
`(live shieldStock/live shieldBoxRows*captured cost)` there in33/18 with retained
text offsets. The slash separates current stock and full cost; it is not a
division or a turns estimate. Advance row y by24.

#### Original Attitude citizen-strip rendering

After city art select detail font37/18, offset1/1. If unsigned unhappy count=0
and signed size−signed happy≤half(signed size), select41/18. Independently, if
signed happy<signed unhappy select106/18, overriding the former style. Draw
name at clientX+140,rowY+9; preserve nameX across that draw. Citizen strip
starts nameX+105,rowY+3; width=live innerRight−live scrollbarWidth−32−stripX.
Pass signed happy/unhappy and excess0; advance row y by32.

The report citizen helper uses P03 spacing(signed size,28,extent) without new
sprite-scale selection. Publish shared specialist count from the city's worker
assignment count component. Draw Happy, Ordinary, Unhappy/Very Unhappy, then
Specialists in P06's parity/type order, **without** the city-window shadow.
Each glyph caches coordinates before querying P06 owner art style and any
specialist type. Ordinary count is recomputed as
`live size−(captured unhappy+live shared specialists+captured happy)` at every
iteration; specialist loop also tests the live shared count. Advance x by
captured spacing and return it. Do not restore the overwritten shared specialist
count or synthesize another C03 calculation.

#### Original §6. Defense Minister

Open the primary window with resource/report2, nonmodal,600×400. Store actor,
then mode0. Two half-width bottom button positions are `(2,0,297,24)`, moved
to client origin and `clientY+400−height−2`, with the right position299 pixels
farther right. Right Close100 uses label327 and is default/cancel. Left
buttons101/102 both toggle: labels345/344 respectively; hide102 initially.
Install redraw, update and show. Toggle maps zero to1 and any nonzero to0;
mode1 hides101/shows102, mode0 hides102/shows101, then redraws. Scroll position
is retained across toggles.

Redraw captures actor before shared frame/status/background preparation. Use
§3's three-line heading, with title335, colon, label344 in mode0 or345 otherwise.
Start rows at afterHeading+2; publish click origin afterHeading+26, rowHeight24,
available height to clientY+372, and visible rows clamped1..99. This report
does not install a row click action.

Construct a retained eligibility array for unit types0..61 in ascending order.
In mode0, admit nonzero actor owned-unit count or cities-building-that-type count.
In comparison mode, admit if any civilization1..7 has nonzero type losses and is
actor or passes the intelligence predicate below. This scan does not require
alive civilizations. Count types, not civilizations. Clamp page count and first
item as in §3. Draw only retained eligible types in the visible interval.

The intelligence predicate checks, in order: local civilization's Embassy with
other, local United Nations, local Marco Polo, actor Marco Polo, actor United
Nations. Stop at first true. Reload local civilization for successive queries;
actor remains captured. It is used again when drawing, so eligibility and the
later visible columns need not reflect one snapshot.

Each row draws a P06 unit preview of `(type,actor)`, alternating city-style
x positions using the post-increment eligible ordinal. Name at clientX+140,
rowY+17; detail font37/18,offset1/1. Statistics start96 pixels after nameX.
In live mode0 draw:

| Offset from statistics start | Content |
| --- | --- |
| 0 | signed attack, `/`, signed defense, `*` if defense-versus-mounted ability, `/`, signed movement rate divided by movement multiplier |
| 55 | signed hit-points/10, `/`, signed firepower |
| 87 | if owned count nonzero: count, space, label346; style122/10 |
| 148 | if production count nonzero: count, space, label347; style94/10 |
| 213 | ordered ability text; style122/10 |

Ability text starts empty. Nonzero holds appends label348, space, signed holds.
Then append Air to Air350 if that ability is set, otherwise Air349 for air-domain
units. Follow with the present abilities in Appendix A's ordered table (labels
351–363). The separator state is reset each row and
set by every appended item. Draw the text only if at least one item exists.
Do not reinterpret these report strings as new mechanical rules.

In comparison mode own losses occupy statisticsX+20, followed by other
civilizations1..7 except actor, each admitted by the live intelligence predicate.
Every admitted other advances x30 even when its losses are zero; only nonzero
numbers draw, in that civilization's label color. Capture own-column x from
each displayed comparison row. After all rows outline x−8..x+16 across the full
row area using actor shield color. If no comparison row supplied x, the original
read is unknown; require retained input or stop. Computing a plausible x is a
separate repair, not original compatibility.

Advance displayed row y24. Create vertical scrollbar200 only on first redraw,
but **update its range, position and page size on every redraw**. Range is
0..limit(count−1,0,999); page=live visible rows. Show/set-created only once.
Present. Scroll stores the supplied position then redraws.

#### Original Trade Advisor construction

Open resource/report5 nonmodal600×400, actor after setup. Use §6's two positions:
right Close100/default/cancel; left Supply101,label367 invokes commodity search
with live report actor. Install redraw, update, show.

#### Original Trade heading and row geometry

Redraw captures actor before frame/status/background. Use §3 heading with
Trade title338. Row origin=afterHeading+4, stored click origin=afterHeading+12,
rowHeight24; bottom=clientY+372, visible rows clamped1..99. No row-click action.

#### Original Trade row placement

Row0 headings at y+10: label364 at
clientX+140 and365 at clientX+335, heading font/style41/10,offset1/1. For every
other row set ordinal=index−1, textY=rowY+9. The left and right columns are
independent:

| Left-column ordinal | Content at clientX+140 unless specified |
| --- | --- |
| 0..cityCount−1 | Rescan live cities to that owned ordinal; city sprite at clientX+2+64*(index mod2),rowY. Name detail font37/18; values start105 pixels after name. If live Disorder clear, stored tax then science, each followed at returned end+2 by its large icon at textY+2, and18-pixel advance. Otherwise label134 in106/18. |
| cityCount | Blank |
| cityCount+1 | label135, colon, total upkeep, gold icon at returned end |
| cityCount+2 | label136, colon, formatted long tax total, gold icon |
| cityCount+3 | label366, colon, science total, science icon |
| cityCount+4 | label137, colon, turns estimate, space, label44 |

#### Original Trade right column and scrolling

Right column starts clientX+335. Ordinals below buildingKinds choose the nth
nonzero count in building index order. Draw count, space, name, space, `(`,
label132, colon, **fresh upkeep query times captured count**, then gold icon
at returned end,textY+2 and `)` fourteen pixels farther right. Use detail
font122/10. Ordinal buildingKinds is blank; buildingKinds+1 draws label135,
colon,total upkeep,gold icon in the same style. No other right row draws.
Create vertical scrollbar200 with range0..limit(total−1,0,999), position first
and page visibleRows only on its first redraw; later redraws leave native
scrollbar properties unchanged. Present. Scroll stores position then redraws.

#### Original §8. Science Advisor and shared technology grid

Open resource/report6 nonmodal600×400 and actor. Use §6's two button positions:
right Close100/default/cancel, left Research101,label371 calls V03 goal chooser
`(live actor,1,0)`. Install redraw, update, show.

Redraw captures actor before shared frame/status/background. Use §3 heading
with title339, then y=afterHeading+6. If actor's live research advance≥0, center
label236,colon,the **reloaded** advance name; advance heading height. Draw the
large science sprite row at innerLeft+5,y+9 with current beakers, V03 research
cost, nominal spacing14, extent=innerRectangle width−10 and row mode1. The
cost query precedes reading beakers and sprite x. Advance y32, outline from
innerLeft+2,researchTop to innerRight−2,y in41, then advance6.

Query full research cost again. Scan live owned cities and sum stored signed
science totals without C03 and without a Disorder test. If total!=0, center
label368,space,`(total−1+cost)/total`,space,label369; advance heading height+6.
Unlike Trade this does not floor total or cost to1 and does not subtract stored
beakers. Signed division faults keep prior effects.

Science and Intelligence (§9) use the following technology grid. Reserve native
horizontal-scrollbar height+2 from bottom=clientY+372. Row height22; publish top,
available height, rows=limit(height/22,minRows,99), where Science minRows1 and
Intelligence minRows0. Intelligence skips the entire grid/control branch if
rows=0. Left=clientX+2,right=clientX+width−2; columnWidth=(right−left)/3,
visibleCount=3*rows. Count possessed advances0..99 by V03 query, set pageCount
limit((rows−1+count)/rows,1,99), and clamp first to0..limit(count−1,0,999).
Rescan advances in index order, increment possessed ordinal before admission;
draw those in `[first,first+visibleCount)`, filling each column top to bottom
before moving right. Sprite is the signed category/epoch art selector; name is
at spriteX+38,spriteY+2, detail font,offset1/1. Science name color41 if signed
first discoverer==captured actor, else94; Intelligence color41 if local
civilization does **not** possess it, else94. Query that knowledge after the
sprite callback.

On first grid redraw create horizontal scrollbar200 across left..right at
reserved bottom; range0..limit((rows−1+count)/rows−1,0,999), position=first
itself (not first/rows), page1. Install scroll and left-click, show, set-created.
Subsequent redraws do not update native control properties. Present. A scroll
callback stores **live rows times supplied position**, then redraws; it moves
one column per scrollbar step.

Click computes row=(y−live top)/live rowHeight after negative relative-y guard,
then rejects row≥live rows. Subtract literal2 from x, independently of client
origin; negative returns. Divide by live columnWidth, reject column≥3. Selected
ordinal=live rows*column+live first+row. Rescan possessed advances0..99. Science
reloads report actor for every query; Intelligence captures target after the
coordinate arithmetic. Matching ordinal opens P08 advance help. Science returns;
Intelligence then clears its modal loop and sets shared retry1. Neither adds
negative quotient tests after signed division.

#### Original Intelligence construction

Open resource/report3 modal600×400; then store actor and target. Right Close100
is default/cancel; left Cities101,label197, uses §6 positions. Install redraw,
update,show. Repeatedly reset retry0, conditionally install intelligence idle for
mode≥3, set timeout ticks, run modal, clear idle; repeat while **live** retry
nonzero. Clear selection after the loop.

#### Original Intelligence heading and statistics rendering

Redraw captures actor and target before shared preparation. Center label336 at
clientY+2 using heading font37/18,offset2/1; next line ruler title/name, label140,
civilization name and parenthesized V04 attitude. Run V04 contact appraisal after
the name composition and use its resulting category. Append a treaty label in
priority order: War144,Alliance141,Peace142,Ceasefire143, or none. Use target
label color. Next line appends leader trait labels, each with trailing space:
first aggression+1/−1 =>146/147; then civilization+1/−1 =>150/151; then
expansion+1/−1 =>148/149. Other trait values append nothing. Advance height+6.

Switch to detail font; left=clientX+2,right=clientX+width−4. On one line draw
label152 colon in122/18 and government name37/18; right-align label153 colon
and the first target-owned live city with Palace, or C01/P04's no-city text for
−1. Search occurs after government drawing. Next line: label154 colon, treasury;
on difficulty<2 append at prior end+6 label155 colon,military count,space,label25.
Right-align label236 colon,current research name (label14 for negative target).
Scratch text is bounded as Appendix B; a long nominal rule name does not make
its original overwrite compatible. Advance detail height+6.

#### Original foreign-chooser city illustration

The row renderer independently finds that civilization's city maximizing signed
size+200 if Palace+100 if **live production is Palace**. Query Palace after
capturing size; production is read after the query. Start best0; strict greater
means earlier ties and no nonpositive winner. Draw selected city at zoom−2,
flags0; selected-style rows shift x38. y is incoming y minus truncation toward
zero of `(scaledMapHeight(48,−2)−rowHeight)/2`. Return0 so normal option text
also draws. No selected city produces no art.

#### Original §11. Wonders of the World

Open report/resource7 modal600×400 with standard Close and §2 modal lifecycle.
Redraw reads actor before preparation but does not use it for filtering. Use
heading font/style41/18,offset2/1, y=clientY+2,bottom=clientY+372. Count every
wonder0..27 whose location!=−1, including destroyed negative locations. Each
nonempty era group `wonder/7` contributes a heading plus its wonder rows.
If total=0, center label340, advance height+4, center `-- `+label293+` --`,
advance height. Otherwise there is no separate global title row.

Publish row height40, top=y, available height to bottom, visible rows clamped
1..99; page/first follow §3. Rescan all wonders; the first admitted wonder in
each era adds one logical heading. If that heading is visible, read indexed
GAME `WONDERS` line for era, draw on success at rowY+15 centered across
width−scrollbarWidth−2 in41/18,offset1/1, and advance y40 **even on read failure**.
Every heading increments ordinal. Every admitted wonder then consumes one row;
only displayed rows advance y.

Wonder text optionally prefixes label215+space when language0 and (era0 or
wonder19 or20). Append building name(wonder+39),space. A negative location
appends parenthesized label172 and uses owner0. Otherwise append label173 when
era0/language0, else216; space,live city name,space,parenthesized owner adjective;
reload owner after text helpers. No city identity or contact test is added.
Use heading font and owner label color/18,offset1/1. Total width=text width+67;
available width=clientWidth−scrollbarWidth−2. x=clientX+half(available)−half(total).
Draw icon x,rowY+10 and text x+66,rowY+8. Outline from clientX+2,rowY+2 to
clientX+width−scrollbarWidth−4,rowY+38 using owner shield color; capture rectangle
coordinates before querying color. Create/configure/show vertical scrollbar200
only on first redraw, range0..limit(total−1,0,999), position first,page visibleRows.
Present. Scroll stores supplied first and redraws.

#### Original Demographics heading and geometry

After accumulation prepare shared frame/status/background. Heading is adjective
plus space only for language0, then label342; center at clientY+2 in37/18,
offset2/1. Advance heading height+10. Publish rowHeight25/top/available height.
Columns: label clientX+2,value clientX+180,rank clientX+322,right clientX+width−2.
Draw13 rows, each recomputing values for civilizations1..7 even if dead. All

#### Original Demographics label style

Label style resets37/18 retaining offsets each row. Actor value uses P04 long

#### Original Demographics leader style

actor Marco Polo OR actor UN. Render right-aligned in leader label color/18,
offset1/1:

#### Original Demographics separators

own displayed unit label. Draw separator color99 at rowY+23 except final row;
advance25 and present. Actor0's ranking scratch was not initialized by the

#### Original casualty heading and row geometry

Redraw captures actor before preparation. Heading is §6's Defense heading with
label872 after colon; this report uses the heading font's **cached** height.
First draw y=afterHeading+2; rowHeight24, clickTop=firstY+24, visible rows
limit((clientY+372−firstY)/24,1,99).

#### Original casualty row rendering

1. Prepare unit preview from the history record's signed unit type and captured
   actor; draw at clientX+2+64*((displayed+1) mod2),rowY.
2. Detail font37/18,offset1/1; x=clientX+140,textY=rowY+17. Reload history type,
   obtain unit name, shorten one trailing character repeatedly until measured
   width≤96; draw. Advance x96.
3. Compose history year, shorten to76, draw. Set94/10, advance x76.
4. Compose signed x,comma,signed y. Reload y then x for C01 nearest city with
   unrestricted selectors; if found append ` (~ `,live name,`)`. Shorten to170,
   draw; select history opponent color/18, advance x170.
5. Append stored opponent name; repeatedly compare live
   innerRight−scrollbarWidth−2−x against freshly measured width, shortening if
   necessary. Draw. Empty-string underflow is an excluded overwrite.
6. Increment displayed, decrement index; if negative set299 (not add300),
   advance rowY24.

#### Original casualty native scrolling and selection

The historical record can change between its art/name/year/coordinate reads.
Create vertical scrollbar200 and double-click callback only once, but set range
0..limit(count−1,0,999),position first,page visibleRows **every redraw**. Present.
Scroll stores supplied first then redraws.

Double-click ignores x. Relative y=`y−(live clickTop−live rowHeight/2)`; negative
returns. Divide by live rowHeight, reject row≥live visibleRows. Index=live tail
for live actor−1−live first−row; if negative add300 once. Return if fresh tail==
index. Also return if fresh head==0 AND fresh historyCount<299 AND index≥fresh
tail. Otherwise read record y then x, reloading report actor for each, and center
map window0. It does not require that a row was actually drawn.

#### Original Score heading and view preparation

V08 owns all numeric score calculations and the Scored/objective branches.
Open report/resource10 modal600×400 with standard Close and §2 lifecycle.
Redraw captures actor before frame/status/background. Center label343 at
clientY+2 using heading font37/18,offset2/1. Switch to detail font, capture its
height, and center ruler title,space,ruler name,space,label140,space,civilization
name,comma,year. Advance detail height+4; publish row height/top/available height
to clientY+372. Left=clientX+2. If Scored, center label457 and present; return
without score calculation. Objective mode follows the separate branch below.

#### Original Score citizen art and point-line layout

Capture P06 citizen art group once for actor. Start x=left,y=top,parity0,row0.
Scan live owned cities, identity required. Call C03 `(city,1)`. Capture full happy,
full unhappy, and content=`signed size−happy−unhappy−specialists−excessUnhappy`.
Loop citizen index against **live signed size**. Choose happy/content/unhappy
by those captured boundaries, then live excess-unhappy, else live specialist
lookup+7. Ordinary kind uses global parity modulo2 with bases0/2/4/6; increment
parity even for a specialist. Draw full citizen art at x,y and advance x20.
If live clientX+width−29<x, increment row, set x=left+10*(row mod2), y+=18.
Stop the entire city scan if signed row≥7 OR **absolute y>240**. Thus only the
visited city prefix is recalculated. If the final icon caused wrapping, subtract
18 from y; set top=y+34 and invoke V08 score calculation.

Draw adjective+space unless language1, then label403,space,`(`,population points,
`)` at left,top. Advance detail height+4. Scan wonders0..27, requiring live
nonnegative location<live city bound, nonzero identity, and signed owner==actor;
reload location for each of these tests and again for owner. Draw icon x,y,
name at x+38,y+3 in94/18 with retained offsets. Start x=left; advance x196 per
icon, wrap after third to x=left,y+=22; break only after a wrap with **y>290**.
After the pass, add22 to y unless last icon wrapped, then4. Even no wonders
therefore reserves26 pixels. Numeric wonder points still include V08's full
query. Draw adjective (unless language1),label404 and parenthesized wonder score
in37/18; advance detail height.

If arrived or in-flight points are nonzero, draw label405,colon,parenthesized
arrived value if its **retested** value is nonzero, else in-flight value,space,
label409. Advance one line. On the next line compose, in order: nonzero pollution
label380+space+parenthesized penalty+four spaces; if turn≥200 and peace bonus
nonzero, label406+colon+`(+bonus)`+four spaces; nonzero future points, Future
Technology name+colon+`(+points)`+five spaces; always label15+colon+parenthesized
barbarian adjustment, adding `+` for nonnegative value. Draw and advance one line.

#### Original Score objective and final-line rendering

Objective mode skips all citizen art/recalculations, starts top+38, calls V08
score calculation, then centers label433:total objective value. If configured
objective civilization!=0, center its adjective,label434:its value. If configured
civilization==0 or differs from actor, center actor adjective,label434:local
value. Advance detail height per row. Capture ranked civilization (configured
if nonzero else actor). Add6, style94/18,offset1/1. Draw its adjective and four
threshold labels435,436,438,439 with settings4..7, one line each. Add6, switch
to heading font122/18, measure its height, center ranked adjective and
label435+stored outcome. Advance this height+6.

Final line rechecks live objective mode. If true, set shared conquest−1 and
ordinary=scenario points, and first perform a measurement pass for centering.
Each pass selects heading font, composes label408:conquest only when
ordinary<conquest, otherwise407:ordinary. Drawing uses37/18,offset1/1, starting
left. If Scenario append label418 in122/18 after a25-pixel gap. Then advance
another25 even if Cheated is absent; if Cheated draw label410 in106/18. In the
measurement pass count text widths plus25 only for present suffixes, then
left=clientX+half(width)−half(measuredWidth), and redo the entire composition as
a drawing pass. Later callbacks can change flags/values between passes. Private
x updates in the measurement pass are dead: no unknown x input is required.
Present only after final rendering.

#### Original Fame setup and heading

V08 owns percentage and rank selection. Open resource/report10 as for Score,
but request CD track3 **after update and before show**. Modal cleanup is §2.
Redraw captures actor, prepares frame/status/background, top=clientY+1,
bottom=clientY+372, heading height=measured height−3. Run V08's percentage/rank
publication before any heading text. Center label412:percentage`%`, then labels
413 and414 on successive lines. Add height+4 after the third, publish row fields.

#### Original Fame typography

P04-expand substitutions. Rows below rank use fame-small font29/18 and height
measured−3; final uses heading font, height measured−4, then style37/18. Subtract
height from bottom and center each text there. The highest title consequently
appears above earlier entries. Present. An oversized template/substitution is
an excluded write; expanded-storage repairs are not original stack layouts.

#### Original Hall setup and heading

V08 owns load/validation, record insertion, rank/calendar fields and the caller's
reset loop. Every display reloads the table, then opens resource/report8 modal
600×400, stores requested highlight in target and return-result0 in actor.
Negative highlight gets full-width standard Close. Nonnegative highlight gets
right Close100 and left Reset101,label446 using §6 positions. Reset sets result1
and clears the modal loop; it does **not** itself clear/write records. Install
redraw,update,show and §2 modal idle/cleanup, then return live result field.

Redraw captures highlight before shared preparation. Center label442 at
clientY+1 with heading font37/18,offset2/1,height measured−2. Advance height,
switch to Hall font,height measured−3,advance6,publish row fields. Scan records
0..5; skip record5 unless highlight==5, and skip negative scores. Each admitted
record supplies three centered lines followed by6 pixels:

#### Original Hall highlight style

civilization+space+`(`+label443+space+record date+`)`. Use122/18 when highlighted,
   else175/18, retaining offsets.

#### Original Hall row styling

   Keep previous row style.
3. `-- `,label445,colon,percentage,`% --` in37/18.

#### Original Hall row spacing

Advance the captured Hall height after each line. Ordinary dates use stored

#### Original Hall present call

calendar replaces record fields. Ordinary redraw ends with present.

#### Original herald science allocation

Construct face0/size11 ordinary font, five category sprites, palette and port.

#### Original herald science sprite and text rendering

Clamp owned count to6. Zero destroys port, palette, sprites4..0 and font and
returns1. Otherwise load215 with palette start10/count236; failure performs
that cleanup and returns0. Assign herald palette to the port. Extract108×30
sprites at x=`group*109+1`, with transparent color9, mapping successive y values
1,32,63,94,125 to categories3,0,1,2,4 respectively. Set icon position485,130
and text rectangle `(531,137,589,157)`, width58. Traverse ranked slots5 down0;
skip negative advance IDs. Draw each admitted row below, then move text
rectangle and icon y down31. Finally perform the same cleanup and return1.

##### Science row text

Read the advance's signed category and draw that category sprite first. Nominal
categories0..3 have text colors19,98,19,37 by art group0..3; category4 has a valid
extra icon. Other indices require backed inputs or a stop. Resolve the name,
uppercase it using P04's platform character mapping and split at literal spaces
using the following original sequence, subject to Appendix B's bounds:

1. Clear five word slots; copy the full name into slot0. Starting at slot0,
   scan until space or NUL. On space replace it with NUL and copy the entire
   suffix into the next slot. Increment word count. Repeat while the scanned
   prefix length was nonzero. This includes a terminal empty-slot iteration;
   multiple spaces can terminate the split early.
2. Clear first line. For successive nonempty slots append the word then one
   space and measure the line. Stop on the first width>58. The overflowing
   word remains appended. Record its index as `fit`; stop also on an empty
   slot. A read beyond the five established slots is not invented as empty.
3. Use a single line when slot`fit` is empty or word count==2. In the latter
   case only, apply the truncation operation below with initial index equal
   to this first line's length. Copy text rectangle. If cached intelligence permits
   text, draw first line in10 with flags0, offset−1,−1 and redraw
   in the group's color with flags0.
4. Otherwise clear first line. If fit==0, append word0, set nextWord1 and
   truncate first line using the **length of retained second line**, not
   first-line length. This second line is not initialized before the first
   displayed advance; require supplied retained text or stop at that read.
   If fit>0, concatenate words0..fit−1 **without spaces** and set nextWord=fit.
5. When text is visible, copy text rectangle and offset y by
   `half(rectangleHeight)−cachedFontHeight+1`; draw first line in10 with flags1,
   offset−1,−1 and draw in group color with flags1.
6. Clear second line, concatenate slots nextWord..4 **without spaces** and
   truncate using its own length. If text visible, continue from the first
   line's foreground rectangle, offset y by`cachedFontHeight−3`, draw second
   line in10 with flags1, offset−1,−1 and draw in group color with flags1.
   Retain second line for the next advance, including when text is hidden.

Truncation interprets the initial length as a signed low byte. While index is
nonzero and the current measured line is wider than58, write NUL at that index,
decrement the signed-byte index with wrap and mark truncated. The first
iteration commonly writes the existing terminator and does not shorten the
line. If truncated, write NUL at `index−1` and append `...`; there is no final
width check. Any write outside the logical destination is an excluded bug:
stop/omit/repair safely at that point, retaining earlier icon/query effects.
In particular, second-line index−1 must never be represented as a change to
icon x, even where the original stack placed those values adjacently. Appendix B
distinguishes this exclusion from the retained-text read and scalar wrap.

Original presentation procedures: [Appendix E](#appendix-e-original-presentation-reference).

#### Original Parley pane origin

pane x as inner.left+s for left or inner.left+inner.width/2+s for right, before
cleanup callbacks. BarterItem and DemandItem redirect choice0..6 to

#### Original Parley pane framing and controls

The following notation expresses framing without prescribing native widgets.
`H` is a fresh pane-font cached height, `L` a list-font height and `B` a button
line-height query. Unless stated otherwise draw text in10 with flags5. Half
width `q=(inner.width−2*s)/2`; narrow width`w=q−5*s`; pane top`t=inner.top+v`.
A centered group of height h starts at`inner.top+(dialogBottom−inner.top−h)/2`;
a group centered below a caption instead starts at
`t+(dialogBottom−t−h)/2`. “Double outline” draws the rectangle in10 on canonical
Parley, insets1 and draws again. The rectangle remains inset once. “Interior”
then moves left/right inward2*s, bottom upward2*v, top downward the specified
heading allowance, draws one outline,insets1 and fills ordinary list background
on the incoming receiver. Retain intervening reads and child results; these
formulas do not authorize a single entry-time geometry snapshot.

Choice lists are P04 single-choice controls. Initialize the specified current
selection, install double-click dispatch and the indicated change callback,
then set readiness1; Gold and list-of-assets panes derive readiness elsewhere.

| Pane | Content and geometry |
| --- | --- |
| Action | Capture labels740..744. Heading739 centered in full inner width at t using heading font. Measure all five choices with that font; list x is the smaller of title x and centered maximum-choice width, stored as redraw.left. Width=maximum+maximum/2; y=inner.top+2*v+headingHeight, bottom=dialogBottom. Current action None displays0. No change callback. |
| LeftKinds / RightKinds | Header771/772 at captured x,t. Choices747..751, then752/753 respectively, then834. Width q,y=inner.top+v+button cached height,bottom=dialogBottom. Disable opposite Gold/Treaty per V05, initialize respective side choice, install kind-choice change. |
| Treaty | Optional redirection caption771/772 at x,t; then independently center group of5*H+4*v normally or4*H+3*v when redirected. Title740 normally or834 redirected. List starts one H+v below, width w,height4*H+3*v normally or3*H+2*v redirected. Labels755..758, omit last when redirected; V05 owns disable/selection adjustments; scalar-choice callback. |
| CurrentTreaty | Center group height2*v+4*H(first query)+H(second query),width q−s; double outline. Heading759 centered at original y+v+H/2. Interior top+=v+H; center live local→peer relation by priority Alliance760,Peace761,Ceasefire755,War762,else763. Read each treaty test in order. Set readiness1. |
| GiftKinds | Center group height5*v+5*H(first)+H(second); title764. List one H+v below,width q,height5*H+4*v,labels765..769,selected gift kind, gift-kind change callback. |
| ProposalKinds | Center group3*H+2*v,title780; list one H+v below,width q,height2*H+v,labels781,782,selected map/joint-war choice,proposal-change callback. |
| DemandKinds | Center group8*H+7*v. Title791; advance H+2*v,subtitle792; advance H+v,list width q,height6*H+5*v **for seven entries**,labels747..751,753,834. Select requested choice,requested-choice change callback. |

**Advances, Units and Cities:** set page rows to6/5/6 for Barter redirection,
otherwise7/6/7. Start x,t,width w,bottom=dialogBottom. Barter adds caption771/772
and advances t by H+v; Demand does not. Double outline. Center heading at
original y+H/2; Advance always uses770, Unit uses777 except Barter, City uses794
only when neither redirection applies. Advance/City interior top+=H and uses
dialogBottom−2*v as bottom. Advance builds §23 with receiving true for Demand
or right Barter. City uses the opposite boolean as offering and constructs its
extra disclosure controls below. Unit interior top is the bottom of its sort
buttons; it uses that same offering boolean.

Unit sort/disclosure controls: without redirection, controlTop=outlined.top+H,
halfWidth=w/2,height=2*p+B. With redirection, controlTop=outlined.top+2*v−1,
nextTop=outlined.top+2*p+H+B,disclosureHeight=(nextTop−controlTop)/2;
initial halfWidth=w−3*s. First rectangle left=outlined.left+2*s,
right=outlined.left+halfWidth,top=controlTop. Create overlapping Reveal and
Conceal with left labels879/881,right labels880/880. Left callbacks enable/
disable Unit disclosure; right callbacks both notify the peer. Apply current
local/peer flag: reveal hidden/conceal shown when set, opposite otherwise;
right conceal is also disabled when set. Then move controlTop down disclosure
height, set halfWidth=w/2 and recompute the first rectangle. Create Type label778
there; create Home779 immediately right, right edge=oldRight+halfWidth−3*s.
Disable each sort button separately when canonical mode is not GiveUnit and
this side's disclosure flag is clear. Interior top becomes the last button's
bottom; build §23 Units.

Redirected City controls share a single rectangle:
left=outlined.left+2*s,right=outlined.left+w−3*s,
top=outlined.top+2*v−1,height=H−(2*v−1). Create overlapping Reveal/Conceal with
left labels882/884,right883/883; use City disclosure callbacks and identical
visibility/disable policy. The City interior still starts outlined.top+H.

**Maps:** optional Barter caption771/772 and t+=H+v; title variant index1 only
for right Barter,otherwise0. Height=3*L+3*v+5*v+v+H, centered below t,width w.
Double outline; title783/784 at original y+v+H/2. Interior top+=v+H. Configure
three labels785..787 in that interior, selected per-side map scope; install
map-scope change and set readiness1.

**WarTargets:** page rows5. Optional Barter caption and t advance as above.
Title788 normally,789 left Barter,790 right Barter,835 Demand. Height=
pageRows*L+pageRows*v+4*v+H, centered below t,width w,double outline,title at
original y+v+H/2. Interior top+=v+H; unlike ordinary Interior, bottom becomes
`interiorTop+live pageRows*L+live pageRows*v+2`, then horizontal inset2*s,
outline,inset,fill. Build §23 Civilization list with V05's target filter.

**Gold:** center height2*v+4*L+H,width w. Barter caption at x,inner.top+v does
not change that centering. Double outline; heading at original y+v+H/2 is
label773 for Demand,otherwise774,space,live local treasury,space,775.
Interior top+=v+H. Measure `000000` with pane font; edit width=3*measurement/2.
Compose label776 plus a space; center the combined label and edit widths in
interior. Draw label at interior vertical center with flags1; edit follows
its **freshly measured** width and is centered vertically with height H.
Initialize from signed per-side stored gold, limit text to6,install §23 gold
change callback,focus. Readiness remains initially0 until a callback changes it.

**Awaiting:** substitute peer adjective into label738; center heading font over
full inner width and vertically at
`inner.top+(dialogBottom−inner.top)/2+v−headingHeight/2`. Hide/disable alternate
Send,show/disable Send,disable NeverMind. Then consult live local Unit and City
disclosure flags: both0 shows/enables RevealAll and hides/disables ConcealAll;
otherwise reverse those states.

#### Original Parley description widget

with mode0 for Description,1 for Review. Create read-only multiline edit in
`(inner.left+s,inner.top+v,inner.right−s,dialogBottom)`,list font,limit8192,
§21 edit-key callback,show.

#### Original Chat option geometry

Use §21's allContacts result and candidate filter, evaluated again while building
controls. At inner.left+s,inner.top+v, place Anonymous(label745),
MuteAnonymous(865),AllRecipients(746) in thirds of inner.width−2*s using pane
font. Publish canonical Chat's values,mark enabled,install option callbacks.
Row step=2*v+H. Move down once and draw label843 over each of two columns;
second title x=captured pane x minus23 only in large layout. Move down again.
Recipient column width=(inner.width−2*s)/2; initial inset5 small,23 large.
Scan civilizations1..7. For each ineligible one clear canonical recipient and
suppression flags. For an eligible one create a width35 unlabeled suppression
option at x+inset,then recipient option at x+inset+35,width=columnWidth−35−inset,
label=civilization adjective; copy canonical values,enable,install callbacks.
Alternate columns after each admitted row; second-column x is the captured
pane x,first-column x=inner.left+s. On each completed pair advance y by row step.
Small inset remains5; large inset is0 in second column,23 in first. Excluded
civilizations leave no gap. These eligibility updates can clear prior choices.

#### Original Parley cleanup and repaint order

Side cleanup first removes shared description/chat controls when sideRight:
description edit,Anonymous,MuteAnonymous,AllRecipients,then for each index0..7
recipient and suppression controls. Then remove this side's option,list,edit,
scrollbar,click surface,Type,Home,Reveal,Conceal in that order. Every removal
tests/reloads its slot, deletes the captured nonnull object and only afterward
clears the slot, overwriting callback replacement. Fill region2 over inner
rectangle with bottom=dialogBottom; left side sets right to inner.width/2,
right side sets left to inner.width/2 **without adding inner.left**.

Full cleanup first deletes retained request and clears its reference,then
caption controls0..2,Close,SendChat,ClearChat,RevealAll,ConcealAll,Send,
Intelligence,alternate Send,NeverMind,Accept,Decline,Counter,history,input,
Anonymous,MuteAnonymous,description,AllRecipients; then each index's recipient
and suppression; then left and right side controls in the order above. It does
not invoke side cleanup's repaint between each item. Ordinary callback effects
remain; compiler exception-unwind paths are not a recovered platform contract.

#### Original report input provider domain

Nominal inputs are initialized game/rules/map state, backed indices, terminated
fitting text, supported assets, usable fonts/windows and positive geometry
divisors. External services supply event delivery, clock/cursor/key samples,
file/resource results and ordered callbacks. Pixel comparison requires identical
asset, palette and font inputs; an alternative widget implementation may use a
different representation with the same observable behavior.

#### Original city-report click offsets

City Status title is334 and row height24. Its click origin is afterHeading+10,
**eight pixels below drawing origin**. Attitude title337, row height32, click
origin=first row y. Publish available height=bottom−first row y and visible

#### Original Trade summary font

Summary rows use detail font41/10,offset1/1. Turns queries V03 full research

#### Original Chat input bindings

Macro commands are handled below and return0. Escape invokes focus-and-close
and returns0. Other than Enter returns1. Enter copies the current window's

#### Original Chat edit-key bindings

current window's input edit to that empty text,focus it,return0. Send-button
routes to Enter. The edit-key handler only intercepts native Escape27 or
internal Escape210, closes and returns0; all other keys return1.

#### Original Trade row traversal placement

Total logical rows=max(cityCount+6,buildingKinds+3). Page count and first item
use §3's formulas. Draw indices from first through first+visibleRows−1,
advancing y24 only when index<total.

### Appendix F. Original implementation reference

This appendix preserves original storage, allocation, native API and adapter
procedures for reference validation. The main text defines required behavior.
A different internal representation or platform is permitted; semantic identities,
ordered game effects, accepted input interpretation, file/wire values and required
failure/completion results remain governed by the main algorithms and X01.
Mixed excerpts retain context: their required effects are not made optional by
appearing here. Original memory/handle traces are not a second normative algorithm.
Optional providers and harmless UI failures retain X01's existing scope limits;
this reorganization introduces no new gameplay or interoperability relaxation.

#### Original chat buffers and destructor logging

Clear-history zeros capacity+1 bytes with unsigned wrap (zero length accesses
nothing),updates Chat then Parley history edits using fresh storage,then focuses
Parley's input. Capacity growth without matching allocation is not permission
to overwrite the old buffer.

Chat initialization reads `CIV.INI` section `Civilization Gold`,key `ChatShowSize`,
default−1. If −1 or signed<8192,write8192 to profile then publish8192; else if
signed≥57344,write57344 then publish57344, including equality. Allocate history
capacity+1 only when its pointer is null,then clear it; allocate input257 only
when null,then clear it. Existing smaller allocations need safe handling before
any excessive clear. Construct file object. Unless the no-load-history option
is set, open `chatlog.txt`; on success compare signed file size to live capacity,
seek by size−capacity if larger,read live capacity bytes into history,close.
Ignored read/seek return values do not add success bytes; previously cleared
terminator remains beyond the nominal payload. Otherwise set history's first
character NUL. Destroy file object.

N01's session log call creates `chatlog.txt`; failure returns before reading
history. On success measure established history,write its current contents,
close even after ordinary write failure. The **window destructor differs**:
for Chat-only mode, if save-history option and nonnull buffer, first clear
history's first character,then create/log its now-empty text. Free history then
input if present,clearing each shared pointer after deletion callbacks. Destroy
file object,clear window controls,reset graphics storage,destroy base window.
The empty destructor log is not silently replaced by N01's nonempty session log.

#### Original chat edit selection after append

storage. Set Chat history edit,then Parley history edit. Query Chat end and
collapse Chat selection; query **Chat end again** and collapse Parley selection
to that result. This is not a general ring buffer or automatic maximum-line
truncation algorithm. Reading beyond established history or overwriting its
logical allocation requires a bounded stop or declared safe policy.
