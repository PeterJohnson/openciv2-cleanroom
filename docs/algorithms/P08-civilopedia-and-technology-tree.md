# P08: Civilopedia and technology tree

Target: classic Civilization II Multiplayer Gold Edition 5.4.0f, patch 3.

This document specifies Civilopedia population, name ordering, list selection,
history, links, rule-derived pages, descriptions and the prerequisite tree.
[F03](F03-rules-and-configuration.md) owns the rule records, section scanner,
line/token reader and resource search. [V03](V03-research-and-technology.md) owns
technology knowledge and research decisions; opening a page or a tree does not
research its topic. [P03](P03-graphics-and-asset-processing.md),
[P04](P04-text-and-dialogs.md) and [P05](P05-input-and-command-dispatch.md) own
sprite/palette, font/text/control and native input services used below.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Required contract and reading guide

Rule-derived facts, prerequisites, knowledge visibility, customized
names/descriptions and accessible topic/link relationships. Browsing remains
separate from researching.

The numbered main sections specify required behavior under
[X01](X01-classic-compatibility-contract.md#presentation-and-semantic-interaction-policy).
Original rendering, geometry, widgets, cosmetic media and native traces are in
[Appendix D](#appendix-d-original-presentation-reference); they are optional reference material.
Main-section numbers are retained for stable citations. Required calculations
that happen inside a presentation operation remain in the main text, including
state mutations, visibility/choice rules and file/protocol effects. Native event
traces do not replace semantic operation boundaries. Harmless UI failures and
rejected off-nominal inputs follow X01's declared safe policies.

## Implementation boundary

The main sections use logical values, identities and operations. Original memory,
allocation and platform mechanics are recorded in [Appendix E](#appendix-e-original-implementation-reference). Preserve required
state changes and externally visible results without reproducing those mechanisms.
Exact numeric domains and file/wire encodings remain required wherever specified.

## Native topic and classic text compatibility

[Native JSON encyclopedia topics](../content/data-format.md#7-encyclopedia)
supply standalone prose and links with explicit rule identities. PEDIA prompt
templates and named facts use the
[text catalog](../content/data-format.md#61-text-and-interaction-catalogs); they
are distinct from DESCRIBE topic descriptions. An unavailable description does
not remove an otherwise eligible rule entry or its computed facts. This document
continues to own category/availability, rule facts, prerequisite relationships,
visibility and required effects. DESCRIBE/PEDIA parsing remains the classic
adapter path; those source files are not mandatory for native content. Selected
classic scenario descriptions must survive that path instead of being silently
replaced by native stock prose.

## 1. Inputs, state and compatibility

Nominal inputs are initialized rules, populated description indices, supported
assets and safely terminated text. Native font/window geometry is reference input.
`Describe` supplies index-to-description mappings and prose;
`PEDIA` supplies rule-dependent fact strings. Names come from the active rules,
not from a fixed English dictionary. Labels are active P04 label strings;
parenthesized English names below identify the stock labels, not replacements
for customized text. The text extension and scenario/install directories are
F03 inputs. The local human civilization supplies knowledge colors in the tree.

The algorithms use [F01](F01-determinism-and-arithmetic.md) wrapping arithmetic,
signed byte interpretation, division truncating toward zero and signed remainder.
Unless specified otherwise, read a field when used after preceding callbacks.
In particular, clearing controls can pump messages and change category,
selection or the live list. A page is not an immutable snapshot of game state.
There are no random draws in these algorithms.

[X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
excludes every memory overwrite beyond its logical destination. Do not reproduce
an overlong title by changing the next category's state, even when the original
address is mapped. Reject, omit or stop before the invalid write, preserving the
valid prefix. Unknown reads require recovered backing, supplied retained input
or a stop; they do not imply zero or an empty name. A declared restrictive input
profile may reject malformed files earlier. Appendix B distinguishes source
repairs from the original branches and lists original storage for validation.

A logical Civilopedia window contains:

- `closed`, `category`, `view`, `selected`, `focused`, `modal`, history count and
  ordered history triples `(selected,view,category)`.
- Seven ordered category collections, each with count, description IDs, item IDs
  and names. Name lookup keys are distinct from the item identities they label.
- Semantic title, category/topic identity and available actions. Native rectangles,
  fonts, title icons and widgets are reference state in Appendix D.
- Copied list item IDs, list count, first visible index and ordered detail links.

Input callbacks, shared name lookup and several presentation operations use the
canonical logical Civilopedia state even when a helper receives another state
object. Nominal callers use that canonical state, whose initial fields are zero.
This initial-value rule does not zero other receivers or later scratch text.

| Category | Meaning | Nominal item identities |
| --- | --- | --- |
| 1 | Advances | 0..99, subject to the enabled field |
| 2 | City improvements | 1..38 |
| 3 | Wonders | Improvement IDs39..66 |
| 4 | Units | 0..61 |
| 5 | Terrain/resources | 0..32, excluding24 from the list |
| 6 | Governments | 0..6 |
| 7 | Concepts | File order, starting0 |
| 8 | Prerequisite tree | The selected advance-list index |

`view=0` is list/tree, `view=1` is rule detail and `view=2` is description text.
Category8 always dispatches the tree. Government/concept activation passes
through view1 but goes directly to view2. Shared history state initially has
`replaying=0`, `pendingIndex=−1`; pending category/view begin zero.

## 2. Construction and resource population

Initialize closed1, view0, selected0, modal0, no detail links and category0.
Native window/button construction is reference behavior. Fill all description slots with−1 in category order
1,2,3,4,6,5,7; clear the seven name collections in the same order, list control and
history count. Counts and item tables are not initialized by these explicit
stores; canonical initial zeros come from static storage.

### Index sections

Population first resets all description slots to−1 and clears all old name
lists. It does not first clear category counts or item tables. For each section
below, use F03 section lookup on `Describe`; success is return0. A failed lookup
leaves that category's previous count/items, with the already cleared names and
descriptions. A successful lookup performs the stated initial line reads,
parses an integer, resets count0 and starts at the stated item.

| Section lookup argument | Initial reads | First item | Include current row when |
| --- | --- | --- | --- |
| `@ADVANCE_INDEX` | 1 | 0 | Advance enabled field is nonzero |
| `@IMPROVEMENT_INDEX` | 2 | 1 | Signed prerequisite ≥−1 |
| `@WONDER_INDEX` | 1 | 39 | Signed prerequisite ≥−1 |
| `@UNIT_INDEX` | 1 | 0 | Signed prerequisite ≥−1 |
| `@GOVERNMENT_INDEX` | 1 | 0 | Always |
| `@TERRAIN_INDEX` | 1 | 0 | Item !=24 |

These arguments identify file lines beginning `@@`: F03 prepends one `@` to
the lookup argument. Process the sections in this table's order. The extra
improvement read skips improvement0. For every integer other than−2, optionally
append the current row, then increment item, read another line with mode1 and
parse another integer. The−2 sentinel ends the section;−1 is a stored unavailable
description, **not a general exclusion predicate**. Close that section's stream
and sort its names when nonempty. A missing sentinel does not gain an implicit
count-based stop in the original; apply the off-nominal policy before unknown
reads, excessive loops or an excluded destination write.

Appending a row publishes its description ID and item ID at the current count,
then associates that position's lookup key with the resolved authored name.
Resolve the rules/pool name at its semantic publication boundary after relevant
external events, then increment the live count. Do not silently skip a failed
row or report complete population after partial publication. The linked nodes,
remembered previous pointer and allocation-failure traces are in [Appendix E](#appendix-e-original-implementation-reference).

For terrain, compose the shared line from its name. When item≥13 and abbreviation
is exactly `Gls`, append space and label704 `(Shield)`. Publish the resulting
name. This disambiguates shield grassland without adding a second item24 entry.
Safe storage may grow with the text; original malloc/copy timing is not required.

### Concepts

Open `CONCEPT_DESCRIPTIONS` in `Describe`. On success reset concept count0 and
item0. Read mode1 lines and examine the first two characters. `@@` starts a
concept: append a row with description=current count, item=current item and
name=the line after its first two characters; then increment item/count. The
name is read from the live shared line when published. Original measurement and
allocation callbacks are reference implementation details.
Any other line beginning `@` terminates; other lines are ignored. Sort a
nonempty list. This path **does not close the stream** on completion.

### Ordering and lookup

Sorting is an in-place selection-by-pair algorithm, not a stable sort. For each
position `i=0..count−2`, compare that position's text with each later position
`j=i+1..count−1` using case-sensitive bytewise CRT comparison. When text[i]>
text[j], swap description IDs, then item IDs, then the two text references.
Keep lookup keys fixed at their positions. Thus equal names can reverse when an earlier
larger name is exchanged: `B₁,B₂,A` becomes `A,B₂,B₁`.

Count≤1 normally performs no comparisons; arithmetic such as `count−1` still
follows F01 for admitted off-nominal values. The original malformed linked-list
traversal is reference evidence in [Appendix E](#appendix-e-original-implementation-reference), not a required collection format.

Name lookup selects the entry whose key equals the requested index, rather than
treating that index as an item ID. No matching key returns the live fallback string,
initially empty. Keys retain their identity when item/name values are sorted.

Terrain item lookup independently scans the canonical terrain item table in increasing
index, using signed index<count, returning the first match or−1.

## 3. Window initialization and frame

Initialize the logical current category, selected topic, view and navigation
state. Fonts, rectangles, frames, titles and native windows may differ. Preserve
rule-derived information and scenario-authored names/descriptions.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 4. Controls, opening and closing

Expose each supported category and topic, open the selected item, and close or
return from the current interaction. P05 supplies semantic commands; changing
native buttons or modal windows does not grant research or mutate rule
definitions.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 5. Lists, selection and keyboard

Resolve list selection to the category item identity populated in §2. Display
sorting, scrolling and keyboard navigation may differ while preserving selected
topic and available decisions; never confuse a display ordinal with a rule ID.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 6. List rendering

Display populated category names and permitted facts with their topic identities.
Sprite arrangement, category colors, font metrics and row geometry are optional;
knowledge indications and custom names remain correct.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 7. Activation, tree navigation and history

Provide access to topic activation, description/detail views, prerequisite
navigation and a usable return/history path. Selection maps through the current
category item table. Navigation cannot itself research a topic or change
simulation state. Original history-buffer/widget mechanics are reference details.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 8. Hyperlinks and description availability

Link creation takes receiver, item, text x/y, target kind and hit rectangle.
Kinds0..4 mean advance, improvement, wonder, unit, terrain; other kinds return.
Scan the corresponding receiver item table for the first matching item. Return
only when final index equals the fresh count. The link's stored target is this
**sorted display index**, not the rule ID.

Associate the matching item with its semantic target. Display its authored name
and local knowledge indication; text clipping, underlining, link rectangles and
native hyperlink objects are optional rendering details.

A link callback decodes its ordinal and walks the canonical current link list.
If that node exists, set canonical category=target kind+1, view0, focused=node's
stored index, then Activate. Terrain's target category is5. Missing nodes do
nothing. A stale control event is resolved against the current link sequence,
not a separately retained target snapshot.

Description-button refresh first tests scenario mode, category!=7 and unequal
scenario/install directories. If all hold, enter the scenario directory, check
`describe.`+text extension, then restore the install directory. If absent,
disable **advance-detail Description** and return, even for another category.
Otherwise read that category's description ID at the supplied selection. Enable
if signed ID≥0, disable if negative; select advance-detail Description only for
live category1, ordinary detail Description otherwise. Invalid category leaves
the original incoming description temporary unassigned: require supplied value
or stop, rather than choosing−1. Filesystem checks and button changes retain
their order and preceding effects.

## 9. Common detail-page conventions

Resolve the selected item through the renderer's category table after relevant
external events and control configuration. Retain that item for the page, reading
individual rule fields at their later use; do not add a stale-category cancellation
guard. Clear shared text before each new semantic row unless specified. Labels
use P04's colon suffix and signed decimal without grouping. Field signedness
comes from F03 and the explicit conversions below.

Prerequisite/name links use §8 lookup and target mapping. A name absent from the
populated category produces no link or replacement ordinary label. The name and
its target ID can differ after external events because the ID is read again at
link creation. Font metrics, clipping, underlining and row placement are optional.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 10. Terrain and resource details

Read the terrain item and let base=signed(item)%11. Display its authored terrain
or resource identity; source asset interpretation is P03. For base items0..10,
show signed movement cost (label659), then defense bonus (660): value6→`+200%`,
3→`+50%`,4→`+100%`,5→`+150%`, all others→label706 Normal. For a special item,
show Terrain Type(661) and a link to its base name, found through canonical terrain
item lookup then name-key lookup; omit movement/defense rows.

Both branches show Food64, Shields662 and Trade69 from signed rule fields,
except **item2 shields is forced0**.

### Base-terrain work and resources

Process irrigation then mining. Effects labels are663/665 and time labels664/666.
For each effects value, link a nonnegative signed destination terrain; otherwise
show a strictly positive signed yield bonus prefixed `+`; otherwise show label667
(N/A). Time displays its signed value only when positive, otherwise N/A. Reread
rule fields at their use after external events; these are page rules, not the
outcome of running a worker command.

Show Engineer Transformation668 with N/A for a negative transform ID, otherwise
a terrain link. Unless item10, show Effects of Roads669: movement label659 and
label707 (`1/3 of a point`); items0,2,1 also show Trade `+1`. These are explicit
page claims, not a call to tile-yield calculation. Show Possible Resources670
with a link to item+11 and, unless item2, item+22. Refresh description availability.

### Special-resource comparison

Capture base name, base food/shields/trade (base2 shields forced0), then special
food/shields/trade, in that order. Produce these fact strings:

1. Special food>base food: format708 with `(baseName,baseFood,specialFood)`;
   otherwise copy709.
2. Special shields>base shields: format710 with `(baseShields,specialShields)`;
   otherwise copy713.
3. Special trade>base trade: format711 with `(baseTrade,specialTrade)`;
   otherwise use712 when shields increased,714 when they did not.

Use these exact format arguments; unsupported conversions need an established
contract or safe rejection, not invented arguments. Refresh description
availability. Overlong text follows X01; geometry and icons are reference details.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 11. Improvement, wonder and unit details

### Improvements and wonders

For the cached improvement ID, show prerequisite label121 and NONE14 for a
negative signed prerequisite, otherwise link the advance. Improvement cost132
is10 times **unsigned** cost; maintenance158 is unsigned upkeep. High-bit cost
therefore remains a positive display value. Wonders show cost and Expires159:
NONE if item<23 or signed expiration advance≤0, otherwise link that advance.
Nominal wonder IDs39..66 use their associated expiration records; advance0 is
suppressed by this strict positive test.

Read and display nonempty authored improvement facts, then refresh description
availability. Source sprite selection is P03; scale, spacing and resource icons
are reference presentation.

### Improvement facts

Construct section `PEDIAIMPROVE` followed by the signed decimal item and open it
in `PEDIA`. Start local aggregate text empty. On success clear shared line and
read mode1 lines until a line begins `@`. Skip `;` lines and empty lines. Strip
all leading `^` and `*`, one character per copy through independent scratch,
then trim rule whitespace. If the aggregate is nonempty, append one space;
append the resulting line even if stripping made it empty. At completion copy
the aggregate to the shared line. A failed section lookup publishes empty facts.
This routine does **not close the stream**, including after consuming the next
marker. Original EOF passes a null line into further parsing; a safe profile
may reject/omit that facts block, preserving prior file effects.

The original private aggregate and published shared-line destinations have
different capacities. No overflow of either is part of compatibility. A dynamic
aggregate can support longer safe custom text, or a restrictive implementation
can omit/reject it. The original byte extents are validation facts in Appendix B.

### Units

Show the signed prerequisite as a link or NONE. Cost132 shows NONE when signed
cost<0, otherwise cost*10. Show these raw encyclopedia statistics in order:
Attack127=signed attack; Defense128=signed defense; Hit Points130=
trunc(signed hit-points/10); Firepower131=signed firepower; Movement129=
trunc(signed movement/3). Read each field at its semantic row, after relevant
external events; do not substitute instance damage, veteran strength or remaining
movement. Source sprite geometry is optional presentation.

Clear shared line and append the signed decimal unit ID before opening
`PEDIAUNITFACTS` in `PEDIA`. If successful, start aggregate empty and perform
**all fifteen** mode1 reads. After each read test that unit's corresponding
capability bit0..14, using the live flag field. For each set bit, copy/append
that line **starting at character1 unconditionally**, then append one space.
It does not check that the skipped character was a caret. An empty line can
therefore expose retained shared-line tail; do not assume it supplies a new
empty string at character1. Bit15 is not processed. PEDIA row order is the
capability-bit mapping; no independently hardcoded prose is substituted.

Close the stream after those fifteen reads and display a nonempty aggregate.
Failure to open skips facts and close. Refresh description availability. Use
safe storage/rejection for overlong authored facts.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 12. Advance detail and fact selection

Resolve and retain the selected advance. Its category then epoch identify the
source illustration; list presentation reads epoch then category. Show NONE when
both signed prerequisites are negative. Otherwise link nonnegative prerequisite0
then prerequisite1, separated by a comma when both exist. Do not deduplicate them;
second-slot tests/ID reads remain live after the first link.

### Dependents

Scan advances0..99, improvements1..66, then units0..61, retaining index order
within each group. Admit prerequisite equality without enabled, ownership or
availability filtering. An advance matching either prerequisite is admitted once;
slot0 takes precedence when both match. Capture its other prerequisite, link the
advance, and if the other prerequisite is nonnegative also show its parenthesized
label123 and link. An excluded link target can omit that name under §8; it does
not change the prerequisite rule. Improvements use improvement links below39,
wonder links otherwise; units use unit links. Show Allows122 when at least one
dependent qualifies. Counting, two-column layout, sprite density and text trimming
for the original renderer are in Appendix D; they do not add research effects.

### Scenario checks and fixed facts

If scenario and install directory strings differ, enter scenario,
check `describe.`+extension, restore install. If absent, disable advance-detail
Description and return immediately, skipping later facts. Unlike §8's general availability
check, this check does not require scenario-mode flag.

Compare directories again. When different, check `pedia.`+extension inside
scenario, restoring install afterward. Set `overridden` true only if directories
differ and that file exists. Open `PEDIACIVFACTS` in `PEDIA`. On success perform
twelve unconditional mode1 reads. Draw a row only when overridden is false and
cached item matches this table:

| Zero-based fact row | Advance IDs |
| --- | --- |
| 0 | 54,15,31,71,21 |
| 1 | 18 |
| 2 | 66 |
| 3 | 67 |
| 4 | 70 |
| 5 | 56 |
| 6 | 24 |
| 7 | 89 |
| 8 | 59 |
| 9 | 32 |
| 10 | 60 |
| 11 | 15 |

For an admitted row skip its first character only when it is `^`; present the
remaining authored text. An override suppresses these draws, **not the reads**. Advance15
can therefore receive both row0 and row11.

Read a thirteenth line unconditionally and append a space to that returned
line. Copy it to private expiration-prefix text. If it starts `^`, move the
remaining text left by one using a count that **excludes the terminator**.
The previous final character remains: with the appended space, an ordinary
`^Expires` becomes `Expires  ` with two trailing spaces. This is a defined
in-bounds overlap, not an excluded overwrite.

Scan wonders0..27. For each whose signed expiration advance equals cached item,
show that prefix, link improvement ID wonder+39, then append P04's sentence
suffix. This scan still occurs
when scenario facts were overridden. The routine does not close this PEDIA
stream. Refresh description availability on ordinary completion.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 13. Technology-tree layout

Display the selected advance and its prerequisite relationships from the active
F03 rules, with knowledge indications for the local observer. Preserve
repeated/cyclic references without unbounded traversal. Original reference
expansion is bounded to four levels (root plus up to2,4,8 prerequisite slots); a
different layout must not invent/remove relationships or use V03 research-path
cost as a prerequisite definition.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 14. Description text

Expanded description takes a **display index**. Start section text empty, clear
detail links and pump messages before reading the receiver's category. For
categories1..7 replace the supplied index with that category's mapped description
ID and select `@ADVANCE_INDEX`, `@IMPROVEMENT_INDEX`, `@WONDER_INDEX`, `@UNIT_INDEX`,
`@TERRAIN_INDEX`, `@GOVERNMENT_INDEX`, or `CONCEPT_DESCRIPTIONS`. For any other
category use section `Unknown Topic Group` and retain the supplied index.
Open the selected section in `Describe`; failure returns before setting/showing
edit text, preserving scanner effects.

Set remaining=descriptionID+1 with wrap. Repeatedly read mode1 lines and compare
the first two characters: `@@` decrements remaining; another leading `@` marks
not-found and stops; other lines do not decrement. This scan counts description
headers after the index section. It does not parse the prose header's name or
seek by rules item ID. A description ID−1 wraps remaining to0 and skips this
scan, so direct off-nominal calls can read from immediately after the index
section. UI disablement is not an added callee rejection branch.

When found, start output empty and read the current stream directly in chunks
of at most2047 characters (the original read request includes a terminator):

1. If stream EOF is already set or a line read fails, stop. A line beginning
   `@` also stops; the consumed boundary remains in shared storage.
2. Ignore lines beginning `;`. Other lines, including blank lines, are retained.
3. Find the last newline character. If present, replace it with a terminator,
   then append CRLF. Text-mode stream translation is the F03/P04 provider input;
   do not additionally strip an arbitrary preceding carriage return here.
4. If current output length+line length≥4095, terminate the shared line at
   `4095−outputLength`. Append it to the output. Stop exactly when output reaches
   4095 characters; this intentional cutoff belongs to observable behavior.

The read loop preserves carets, asterisks, leading spaces and ordinary text.
It does not run the improvement-facts stripping or P04 prompt expansion. EOF,
boundary and full-output exits retain the open stream; they do not synthesize
Close. The source has a close on a cleared live found flag, but no ordinary
producer clears that local flag within this loop.

When the header scan marks not-found, append label14 NONE to the **existing
shared boundary-marker line**, then copy that result as the displayed text.
Do not replace the entire fallback with a clean `NONE`. Set edit text to the
assembled result and show the edit control. Stream mutations, shared text
truncation and a previously consumed boundary are visible to later readers.

## 15. Conformance cases and integration boundaries

Validate category/index ingestion, custom names, description availability and
text, topic/link identity, rule-derived facts, prerequisite relationships and
observer knowledge. Browsing must not grant research. Verify nominal resource
fallbacks and safe rejection outside the admitted domain. Native list/history,
font, hyperlink geometry and tree-raster cases are reference tests.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## Appendix A. Evidence identity and source navigation

Behavioral target: original `Civ2/civ2.exe`, SHA-256
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Primary source navigation is pinned to
`a71e8aefffe04e6f719e0e213b027b128a747711` in the original openciv2 repository.
Most entries are in
[pedia.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/pedia.cpp);
the hand-adapted expanded-description body is in
[retvals.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/retvals.cpp).
These are validation navigation, not dependencies for implementing the logical
algorithms above.

| Area | Original entries |
| --- | --- |
| Lifetime/setup/frame | Constructor `004f3feb`; destructor `004f44a7`; create `004f4b9f`; frame `004f6244`; background `004f6564` |
| Population/order | Load `004f7c99`; sort `004f896a`; name lookup `004f8a9b`; terrain index lookup `00491d61` |
| Categories/lists | Advance `00564d00`; improvement `00599a20`; wonder `005261a0`; unit `005ac840`; terrain `00490590`; government `00518f00`; concept `004a69b0` |
| Drawing/control dispatch | Body `00451af0`; category dispatch `00452188`; controls `00451bf0`; selection redraw `004f6646`; list `004f66c6` |
| Navigation | Exit `004f5b24`; Back `004f5b6f`; Tree `004f5dd1`; Update `004f5e52`; Close `004f5ed2`; Activate `004f5f23`; Scroll `004f7313` |
| Input/title/history | Hit test `004f734a`; scroll-to-item `004f7454`; initial search `004f74eb`; key `004f76ce`; click `004f7a30`; title `004f7ac7`; open `004f7bd1`; history `004f8af9`; modal idle `004f8d04` |
| Hyperlinks | Class/window `004529df`, `00452ae2`; member `00452b89`; link creation `00452c14`; callbacks `004531b8`, `0045323a`, `004532bc`, `0045333e`, `004533c0`; detail cleanup `004f4793`; names cleanup `004f4809` |
| Detail/text | Terrain `004906fd`; wonder `0052630d`; improvement `00599b8d`; facts `0059a15d`; unit `005ac9ad`; advance `00564e6d`; expanded text `00452315`; description availability `00452768` |
| Tree | Layout `005666da`; connector `005674b9`; continuation `005675b7` |
| Direct modal entry | Terrain `00491c20`; wonder `00526913`; advance `00566584`; unit `005ad998`; improvement `0059a2e6` |

Independent reconstruction inspected at commit
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752` in `civ2`:
`src/civilopedia_window.cpp`, `civilopedia_names.cpp`, `civilopedia_links.cpp`,
`civilopedia_detail.cpp`, `research_internal.hpp`, `window_constructor.cpp`,
`font.cpp`, `control_edit.cpp`, `include/civ2/civilopedia.hpp` and
`civilopedia_layout.hpp`, plus the bugfix log. The two source trees can differ
in repairs, types and inferred names; original instruction/data evidence takes
precedence. In particular, the misleading primary name `draw_improvement` at
`0052630d` is the wonder renderer, and the Tree action's older “categories” name
does not mean a category overview menu.

## Appendix B. Representation adapters and excluded defects

These facts aid comparison with original memory/traces. A cleanroom
implementation need not use these addresses, layouts, fixed arrays or native
control classes. Behavioral character limits and arithmetic already specified
in the main text remain meaningful regardless of representation.

### State, labels and native control mapping

Canonical window address is `006a6668`; accessed prefix extent `0x1f44` does not
prove the allocation's full class hierarchy. Relevant offsets:

| Fields | Original receiver offsets |
| --- | --- |
| closed/category/view/selected/modal/count | `114/118/11c/120/124/128` |
| Three100-entry history arrays: index/view/category | `12c/2bc/44c` |
| Working width/height/caption | `5dc/5e0/5e4` |
| Window/detail/text rectangles; title | `5e8/5f8/608`; title64 bytes at`618` |
| Category records, count then description and item arrays | Advance`658` capacity100; improvement`97c`39; wonder`ab8`28; unit`b9c`62; government`d90`7; terrain`dcc`33; concept`ed8`256 |
| Name heads, government before terrain | `16dc`, seven words |
| Private initialized1; icon width/height/sprite; title control | `16f8`; `1704/1708/170c`; `1710` |
| Buttons0..14; edit; scrollbar | `1714` stride`3c`; `1a98`; `1ae0` |
| List-hit control; list rectangle/count/items | `1b20`; `1b24/1b34/1b38`,256 item slots |
| Focused/first/link head | `1f38/1f3c/1f40` |

Name nodes are12 bytes: text pointer, key, next. Link nodes are`0x48` bytes:
index at0, control at4 and next at`44`. History replaying/pending index are
`0062f00c/0062f010`; pending category/view `006a85ac/006a85b0`. The fallback
pointer at`0062f008` initially addresses the12 zero bytes at`0062f014`;
initialization/edit empty strings also use offsets4/8 of that backing.
Do not reproduce a malformed array write through an adjacent field.

Spacing data at`0062d858` are five words `[2,2,8,2,2]`, mapped to `[m,v,b,p,s]`
in this document. Fonts are title`0069b018`, detail`0067a798`, auxiliary
heading`0069b028`, button`0068abe0`. Prompt palette defaults used by lists are
background`00635a18`, text`00635a1c`, shadow`00635a20`, selected background
`00635a24`, selected text`00635a28`, selected shadow`00635a2c`. Frame fallback
background is`00635a04`, border colors`00635a08/00635a0c`; tiled backgrounds
are bindings`00635aa0/00635aa4`.

Title sprite is`00645120`; improvement sprites start`00645160`, advance icons
`00646cb8`, and unit sprites`00641848` with63 loaded slots. Resource icons in
the main text are entries1/3/5 of`00644f00`; maintenance uses entry1 of`00648860`.
Native entry/setup uses parented options`0xe02`; title control ID4000, list-hit
4009, edit4010 and scrollbar4008. Button IDs in construction order are
`4001,4002,4003,4004,4005,4006,4007,4018,4011,4012,4013,4014,4015,4016,4017`.
The edit receives flags`0x122`, visible0: multiline, vertical scrollbar/automatic
vertical scrolling, read-only. Hyperlink ID is4020+ordinal. Its class registration
name is `MSHyperTextClass`, style`0x88`,16 extra bytes, resource cursor`0x212`;
creation uses extended style4 and child-visible style`0x50000000`. Registration
failure is not retried by resetting its flag.

Internal callback key values: previous`a8/c0`, next`a2/c1`, previous9
`a4/a9/c2/c5`, next9`a3/a6/c3/c6`, Home`a7/c4`, End`a1/c7`, ignored Enter`d0`.
Scenario mode in description availability is map flag`0x80`; use the logical
scenario property in a different representation. Rule-field sign conversion is
behavioral; original advances/improvements/units/terrains have strides16/8/20/24
and their field layouts belong to F03's validation appendix.

### Bugs and deliberate reconstruction changes

| Original boundary | Treatment in this specification |
| --- | --- |
| Title64; terrain base-name scratch40; format scratch256; section32/256; filename/advance fact-prefix260 | Any overwrite excluded. Safe storage or declared rejection; no neighboring field mutation |
| Improvement aggregate4096, then shared line2052 | Both overflows excluded. Independent BF-069 omits the whole block beyond2051 payload while consuming to marker; this is an allowed safe policy, not an original branch |
| Unit-facts scratch1000 | Overflow excluded. Independent BF-053 enlarges this storage; no need to retain that particular allocation size |
| Shared parser view255 versus complete line owner2052 | Preserve defined reads/tail only when backed; the smaller view is not a license for unsafe publication or an invented EOF suffix |
| Index arrays/node payloads/history | Reject/stop/grow safely before destination overflow, including mapped aliases; this includes BF-140's category-capacity cases |
| Negative-width link trimming of an empty line | Original would write line[−1]; excluded. BF-139 rejects it |
| Failed cursor query | Original ignores failure and can consume retained point data. BF-021 ignores the click; do not claim the repair is the original return/coordinate behavior |
| Overlong terrain names | BF-043 expands private scratch; original overwrite remains excluded even though the name can arise from an otherwise accepted RULES line |
| Unterminated improvement facts | BF-121 publishes unavailable facts at EOF; original null-line access is not an ordinary end-marker branch |
| Primary port translation hook | `retvals.cpp` can substitute `lang_description` prose and a different cutoff. This is a port extension, not the original file-read algorithm in §14 |
| Stale renderer after nested category switch | BF-071 adds category-change cancellation to improvement, wonder, unit, advance and tree renderers. The main algorithm describes original continuation; this repair can be declared separately. Every consequent OOB write is still excluded |
| Extra scanned identities despite filtered-out rows | BF-212/214/215/217/220 reject wonder/improvement/unit/advance/terrain identity escape before rule lookup. Their stricter ingress domains are not original count-based terminators |

Initializers and independent boundary adapters sometimes expose adjacent mapped
bytes for diagnostic reads. Such evidence can establish a read input for a
validation adapter; it never authorizes writing into another logical object's
state. Ordinary failures leave the completed prefix, unless a declared safe
input policy deliberately omits a whole resource as described above.

## Appendix C. Validation record

The documentation pass checked eleven pinned independent source/header/evidence
files and three pinned primary files. Original executable, LABELS, DESCRIBE and
PEDIA asset hashes were recorded separately; source repositories remained at
their pinned commits with clean tracked/untracked status. Twelve direct original
binary checks cover459 bytes: spacing, population/description marker blocks,
fallback/history defaults, expiration's strict-positive test, unsigned wonder
cost, description cutoff/shared-line truncation, the overlapping expiration
prefix copy and the absent-Describe early return.

A focused fixture compiled **ten unchanged whole independent source bodies**:
name sort/lookup, initial search, scroll-to-selection, keyboard, history, hit test,
connector, continuation and tree layout. Forty-five assertions passed optimized
and address/undefined-behavior-sanitized builds. Cases include unstable equal-name
ordering, fixed keys/mappings, search wrap,98→100 history publication, count99
suppression, replay clearing, hit-test boundaries/residual pixels/divide failure,
Home/End ordering, empty End, live post-redraw scrolling, connector shadows,
full15-node tree, sparse lower slots and bounded cyclic repetition.

These are host-native fixtures with supplied receiver/name storage, bounded CRT,
font/sprite/control/query providers and static-address adaptation. The tree
fixture covers stable categories and therefore does not exercise its independent
BF-071 repair. Its link provider records requests rather than proving the whole
native hyperlink implementation. These passes are not execution of the original
Win32 program, whole-window integration, file-parser integration, pixel equality
or proof for every malformed input. Source/asset hashes, extracted body hashes,
fixture inputs and instruction probes are retained with the preparation artifacts;
no reconstruction source or original resource was modified.

The documentation set was checked for links/anchors, unique specification IDs
and document presence. Complete runtime integration remains X02 work; the
bounded checks do not establish a finished cleanroom implementation or passing
native replay.

## Appendix D. Original presentation reference

This appendix retains the original presentation procedures removed from the main
specification. Geometry, pixel/sample output, native controls and cosmetic timing
are optional reference behavior under X01. Required semantic fragments were kept
in the numbered main sections and are linked where omitted here; this appendix
does not relax their simulation, information, file or protocol obligations.
Original section numbers and local § references identify the recovered procedure
family, whether its retained fragment is in the main section or here. Historical
native/pixel tests are reference validation, not baseline acceptance criteria.

### Original §3. Window initialization and frame

The spacing inputs initially are horizontal spacing `m=2`, vertical gap `v=2`,
side inset `b=8`, border thickness `p=2`, and button padding `s=2`. Keep them
live where their use follows calls. Fonts initially use face0: title height30,
list/detail height21, auxiliary heading height24 with weight1 and button
height24 with ordinary weight. P04 owns font construction/measurement; requested
height does not substitute for returned metrics. Write `f` for cached detail
height and `B` for the button font's queried line height.

Initialization clears names and details, resets history and deletes an old title
control. Set working width640, height400 and caption height `titleCached+2p+2v`.
Store window rectangle `(0,0,640,400)`. The detail rectangle has:

```
left = b+m
top = caption+v
width = 640−2*(b+m)
height = 400−(caption+v)−(B+2*(v+s)+2p+v)
```

Center the requested640×400 surface in the main client using separate truncating
halves. Set title from the canonical empty initialization string. Create the
parented surface, install Close, Update and Key callbacks, set caption height
and disable control traversal. Configure the exit title icon from its sprite
with scale caption/24. Its top is `asr(caption,1)−asr(scaledIconHeight,1)` and
left=b. Create the title control and bind Exit.

The bottom button strip is computed after the title control's callbacks:

```
x = p+m+2
y = workingHeight−(B+2*(v+s))
w = workingWidth−(2m+2p+5)
h = queriedButtonHeight+2s
```

Each group in §4 partitions that width into equal truncating halves, thirds or
quarters; a remainder is left unused. Create all controls even when their
rectangles overlap. Set common button font, drawing receiver and edit font.
Create the initially hidden, read-only multiline description edit with a vertical
scrollbar and automatic vertical scrolling over the detail rectangle. Configure a
horizontal scrollbar over its bottom native caption-metric-height strip;
bind both scroll callbacks and page size2. Populate the name tables last.

#### Frame painting

Reset clip. Copy the window rectangle, draw p nested borders with outer colors
from P04's prompt defaults (first light, second dark), insetting1 after each.
Fill this interior with background region1. Inset horizontally by `b−2p` and
move top by `caption−2p`. Set bottom to window-rectangle height minus
`B+2*(v+s)+v`. Draw p reversed-color inset borders and fill region2.

Region fill tiles background1 only for region1 when that binding exists; tiles
background2 only for region2 when it exists; otherwise fills the default prompt
background. Tiling starts at source `(0,0)` and respects the supplied rectangle.

Recompute and publish **working width and height as the scaled title-icon
width/height**, each `trunc(sourceDimension*caption/24)`. These fields no longer
mean640×400 after painting; the window rectangle retains those dimensions.
Draw the icon at `(b, asr(caption,1)−asr(workingHeight,1))` with scale caption/24,
then reset sprite scale. Start title left at b+workingWidth (the source performs
an add and subtract of live m). Add half of `(windowRight−left−b)` and subtract
`asr(measuredTitleWidth,1)`. At top=p+v draw the title at offsets `(2,1)` color10,
then `(1,0)` and `(0,0)` color26. Set clip to the detail rectangle.

#### Titles and update

Setting a category title clears the shared line. Category1 or8 uses label671;
2..7 use label `category+670`; other categories use
`This Should not Happen.....`. Copy to the window title subject to the excluded
write policy.

Update returns if closed is nonzero. For view0 set the category title; then
clear detail links, select the canonical drawing target and prepare the frame.
Capture live view then category and dispatch: categories1..4 prepare a list
when view0, otherwise render the corresponding rule detail; categories5..7
prepare their list only when view0; category8 renders the tree regardless of
view; other categories do nothing. Present the stored text rectangle and then
the whole window. Do not replace this dispatch with an inferred automatic
restoration of description text. The original computes a provisional half-width
text rectangle from detail geometry and font heights before dispatch, but its
children do not read that argument or assign it to the stored text rectangle.

### Original §4. Controls, opening and closing

Button instances are distinct even when their action/label is identical:

| Construction order | Logical name | Group position | Label | Action |
| --- | --- | --- | --- | --- |
| 0 | list Info | half0 | 39 | Activate |
| 1 | list Close | half1 | 327 | Exit |
| 2 | detail Back | third0 | 854 | Back |
| 3 | detail Description | third1 | 705 | Activate |
| 4 | detail Close | third2 | 327 | Exit |
| 5 | text Back | half0 | 854 | Back |
| 6 | text Close | half1 | 327 | Exit |
| 7 | tree Back | half0 | 854 | Back |
| 8 | advance-list Info | third0 | 39 | Activate |
| 9 | advance-list Tree | third1 | 864 | Tree |
| 10 | advance-list Close | third2 | 327 | Exit |
| 11 | advance-detail Back | quarter0 | 854 | Back |
| 12 | advance-detail Tree | quarter1 | 864 | Tree |
| 13 | advance-detail Description | quarter2 | 705 | Activate |
| 14 | advance-detail Close | quarter3 | 327 | Exit |

Creation order after the title is0,1,7,8,9,10,11,12,13,14,2,3,4,5,6. Register
list Close, advance-list Close and advance-detail Close as default and cancel
controls. The ordinary detail/text Close instances do not receive those two
registration calls here.

Configure controls by hiding buttons0..6, scrollbar, buttons7..14, then edit.
Set edit text empty, even when its native handle is absent. Delete an existing
list-hit control and clear its owner only after deletion returns. Capture view
once for branching; category tests stay live after earlier callbacks.

| View/category | Show in order |
| --- | --- |
| 0, category8 | tree Back, list Close |
| 0, category1 | advance-list Info, Tree, Close, scrollbar |
| 0, other | list Info, scrollbar, list Close |
| 1, category6 or7 | text Back, text Close |
| 1, category1 | advance-detail Back, Tree, Description, Close |
| 1, other | detail Back, Description, Close |
| 2 | text Back, text Close |
| Other view | No show calls |

For view0 first copy detail rectangle to list rectangle and subtract the native
caption-height metric from its bottom. For category!=8, after showing controls,
create a list-hit control if still absent over a newly copied detail rectangle
with the same bottom subtraction. Publish it, bind click and double-click,
and show it, using live owner reloads between calls. Finally pump messages for
**every** branch. A failed allocation is not an ordinary successful empty list.

Open-menu(receiver,category,show) sets closed0, view0, selected0, category,
history0, pendingIndex−1 and replaying0. Set title and configure controls, then
restore the requested category after those callbacks. Invoke the receiver's
update service; show it if show is nonzero. Disable traversal and focus/select
the canonical event window, then record canonical history. It does not first
reset focused, modal or the list's previous scroll position independently.

Close sets canonical closed1 and history0, deletes/clears its list-hit control,
hides the window, focuses/raises the main window, and prepares queued effect
presentation. Return0 after these calls. Close alone does not clear all name
or detail lists. Exit first clears the modal loop and modal0 only when modal is
exactly1; then clears details, history0 and calls Close.

Destroy clears names then details, deletes/clears title and list-hit controls,
resets graphics storage, then destroys scrollbar, edit, buttons14 down0 and
base window. Name cleanup processes categories1,2,3,4,6,5,7: cache next, delete
nonempty text, reload/delete the head node, then publish cached next. Detail
cleanup caches next, deletes the node's control/storage, publishes next and
repeats, then flushes queued paint even for an initially empty list. Completed
cleanup effects remain on failure; no second teardown of already completed
members is implied by a different implementation's exception mechanism.

### Original §5. Lists, selection and keyboard

Preparing a category list clamps a negative selected index to0, and any selected
index≥category count to0. Capture/copy the category count into list count;
set focused=selected and first=selected. Decrement first until its signed
remainder modulo9 is0. The original loop tests canonical first but writes the
receiver; ordinary singleton calls coincide. Copy item IDs in increasing index
while index<the live category count. Set scrollbar range `0..trunc(listCount/9)`
and position `trunc(first/9)`. An exact multiple of9 retains that extra endpoint.
If signed listCount≤0, set first−1 **after** setting scrollbar position.
Configure controls, focus the canonical presentation, focus/select its event
window and redraw selection. Concept preparation reads count before the selected
upper-bound comparison; the other category adapters capture selected first.

Scroll(page) computes page*9 with wrap; if different from first, publish it and
render the list. Scroll-to-selection aligns first downward to a multiple of9
when selection<first or selection≥first+18, then always redraws canonical
selection. It does not add an independent clamp.

Selection redraw renders the list for view0 except category8, which is a no-op.
Views1 and2 also do nothing. Other view values call canonical Close. These
helpers' incidental return values are not additional navigation actions.

#### Keyboard

Ignore keys unless view0. Capture focused and uppercase the incoming character
using the CRT mapping. Letters A..Z perform initial-letter search: start after
the focused index, wrap at live list count, traverse/wrap the selected category's
linked list, then examine at most its captured category count entries. Compare
the uppercased signed first text byte with the requested letter. Node keys are
not consulted. Return the matching scan position, or−1. Categories outside1..7
return−1. The source recomputes the scan position from live focused/count after
its initial traversal; negative malformed counts are not a new empty-list rule.

Other recognized internal keys perform the following. P05 owns translation from
native keyboard events; Appendix B records these callback encodings.

| Operation | Candidate and boundary |
| --- | --- |
| Previous/next | focused−1 / focused+1; reject negative previous or next≥count |
| Previous/next column | focused−9 / focused+9; same respective boundary |
| Home | selected0, focused=selected, first0; set scroll0, redraw, return |
| End | first=count−1; cache that selection; decrement first to a multiple of9; publish selected/focused; redraw, then set scroll first/9 |
| Other, including Enter | Return without changes |

For a successful letter/ordinary movement, set selected=candidate, then
focused=live selected, scroll-to-selection(candidate), then set scrollbar
position from live first/9 after redraw callbacks. Empty End consequently
selects−1 and aligns first to−9; do not substitute an invented clamp.

#### Hit testing and clicks

Hit test checks y<top, y≥bottom, x<left, x≥right in that order, returning−1,−2,
−3,−4 respectively. Otherwise choose column1 when x≥left+trunc(width/2), else0.
Set rowHeight=trunc(height/9), row=trunc((y−top)/rowHeight) and
candidate=first+9*column+row. Return−5 when candidate≥signed count, otherwise
candidate. There is no lower-bound repair and no row≤8 clamp; residual pixels
at the bottom of a nonmultiple-of9 height can calculate row9. A zero rowHeight
has the F01 divide failure.

Click returns immediately outside view0. Capture the canonical event backend,
query the cursor, then read the captured backend's native window and convert
screen to client coordinates. The original ignores query/conversion success.
A failed cursor query needs retained coordinates or a stop; a declared safe
profile may ignore that click. Do not fabricate `(0,0)`. If list count is0,
return without selection. Otherwise hit-test; accept only a nonnegative result
below the live signed list count. Set selected=result, focused=live selected,
and redraw. Double-click activation is the independent algorithm in §7.

### Original §6. List rendering

Record canonical history first. Save clip, clip to list rectangle and fill with
the unselected background. Use halfWidth=trunc(rectWidth/2), rowHeight=
trunc(rectHeight/9), and textYOffset=trunc(rowHeight/2)−trunc(f/2).
Visit the left column's nine rows, then the right column's nine rows. For each,
cell is `(columnLeft,top+row*rowHeight,columnLeft+halfWidth−2,top+(row+1)*rowHeight)`.
Skip if first is−1 or signed(first+column*9+row)≥signed count. Otherwise reload
that display index and its copied item. Selection compares the recomputed index
to canonical focused. Capture selected/unselected foreground and shadow colors,
then save sprite scale.

| Category | Icon and text inset from column left |
| --- | --- |
| Advance | Sprite `4*signedCategory+signedEpoch`, scale3/2; inset `trunc(3*spriteWidth/2)+2m` |
| Improvement/wonder | Sprite indexed by improvement ID, scale3/2; same inset |
| Unit | Unit sprite at current scale; inset `2*spriteWidth+3m` |
| Terrain | Composite below; inset `trunc(3*forestSpriteWidth/2)+m+2` |
| Government/concept | No icon; inset `5m+2` |

Increase cell.left by the inset and fill the text portion with selected or
unselected background. Scaled advance/improvement icons draw at columnLeft+m,
cell.top+1 on the canonical target. Units alternate x by **display-index parity**:
even uses m, odd uses spriteWidth+2m; y centers sprite content height in the row.

Terrain uses base `signed(item)%11`. Its x offset is m for an even display index,
m+trunc(forestSpriteWidth/2) for odd; y is the row top. Draw base, then for items
below11 overlay forest3, mountains4 or hills5 as appropriate. For resources,
13 or24 draws the grassland shield and skips the special sprite. Otherwise
14/25 also overlays forest,15/26 also overlays mountains, then draw special
sprite `2*base+(item>21 ? 1:0)`. Other base overlays are not inferred.

Restore the saved sprite scale, clear only the shared line's first character,
reload category/display index and find the canonical name by key. Invalid
name categories append `This Should Never Be Seen!!!`; an earlier unsupported
icon category appends `This Should not Happen.....` then reaches an unknown
sprite dependency, not a successful blank icon.

Clip to the text cell. At `(columnLeft+inset,rowTop+textYOffset)`, if shadow and
foreground differ draw shadow at `(2,1)` and foreground at `(1,0)`; always draw
foreground at `(0,0)`, all with P04 text mode5. Restore list clip after each
cell. After each column recompute its next left from the live list rectangle.
Finally restore saved clip and show the live list-hit control. Do not restore
saved state early after an intervening failure.

### Original §7. Activation, tree navigation and history

Activate captures focused **before** testing view. For view0, set selected from
a fresh focused read, set view1, clear details and configure controls. Capture
category after those calls. Invalid category outside1..8 appends the unexpected
category diagnostic to the existing shared line, presents and records history.
For6/7 set view2 before name lookup. Resolve title with the captured focused key
from that category's canonical names (category8 also uses advance names), then
prepare frame. For6/7 fill detail region1 and render description using captured
focused. Otherwise render unit4, improvement2, wonder3, terrain5, or advance1/8.
Present the canonical window after this branch.

For entry view1, set view2, clear details, configure controls, prepare frame,
fill detail region0 (plain background) and render description with captured
focused. There is no extra whole-window present in this branch. Other views do
no rendering. Record history at the end in all these cases.

Tree action sets view0, category8, configures, updates, sets focused=selected,
redraws selection and records history. The selected index remains the advance
being explored; this is not a menu of all categories. Clicking a tree link
returns to an advance detail page through §8.

#### Recording

The following rules use signed history count and F01 arithmetic. The source
uses three parallel arrays, but a cleanroom representation may use triples.

```
if count < 99 and replaying == 0:
    if count == 0 or previous.view != view or previous.view != 0
       or previous.category == 8 or category == 8:
        if count == 0 or previous.category != category
           or previous.view != view or comparedPreviousIndex != selected:
            if pendingIndex > -1:
                append(pendingIndex, pendingView, pendingCategory)
            append(selected, view, category)
    else:
        previous.index = selected
pendingIndex = -1
replaying = 0
```

For ordinary canonical calls, `previous` is history[count−1], and
comparedPreviousIndex is its index. In the noncanonical receiver case the
comparison's index subscript uses **canonical count−1** while other fields use
the receiver count. Each append stores index, view, category in that order at
the live count, then increments it. A pending append at count98 can therefore
reach100; entry count99 prevents any append. Ordinary same-list selection
updates replace the last index even when the category comparison would differ;
the outer condition is the precise gate. Negative counts do not authorize
writes outside the history sequence.

#### Back

If count!=0, decrement it. While count!=0 and the triple **at index count**
equals the current category, view and selected, decrement again. If the resulting
count!=0, set replaying1 and capture that triple into pending index/view/category.
Use the triple at count even when count became0. If its view0, restore its
category, set view1, focused=its index, selected=focused, then continue below.
Otherwise restore category, set view=storedView−1, focused/index, selected,
call Activate and return.

With no replay return above, branch on the current view:

- View0: if modal==1, clear loop then modal0; Close and return.
- View1: set view0, configure, update, focused=selected, redraw.
- Other: set view1 first. If live category6 or7, set view0 and perform the same
  configure/update/focus/redraw sequence. Otherwise decrement live view and
  Activate.

Record history after the latter two branches. Count0 is not automatically an
absent history record once Back has entered its nonempty path; it can restore
record0. The pending/replaying fields deliberately coordinate nested redraw's
history recording with the final record call.

#### Direct item help and modal lifetime

Direct help returns unless canonical closed is nonzero. Advance, improvement,
wonder and unit help also return when network map-window update is active;
terrain lacks that second guard. Improvement help delegates IDs≥39 to wonder
help. Scan the receiver's appropriate item table for the first equality using
signed index<count; return only when final index equals the freshly read count.
A malformed negative count skips the scan but is not equal to0, so it does not
supply an invented not-found result.

Open the category menu: terrain/wonder show immediately, advance/unit/improvement
use show0. After it returns, publish cached matched index to selected/focused,
modal1 and view1. Clear details, configure controls, copy canonical matching name
to title, prepare frame and render that detail. Show the receiver now for the
three delayed-show categories. Present and focus/select canonical window,
install modal-idle callback, run its modal loop and clear the callback only
after return. Modal idle polls network `(1,0)` first; if modal is0 or closed is
exactly1, clear loop and then modal0. Partial effects and a published idle
callback remain if a later provider stops.

### Original §13. Technology-tree layout

The tree is a fixed four-level expansion: selected advance plus at most2,4,8
prerequisites. It draws repeated IDs separately and does not use a visited set,
a topological sort or V03's research-path cost. Even cyclic custom prerequisites
have bounded expansion here. A negative prerequisite leaves an empty slot;
its descendants remain−1. Later levels retain their geometric slots rather than
packing upward around missing branches.

Save clip, clip to detail, clear existing links, configure controls and fill
region2. Resolve the selected advance through the receiver's advance item table.
Use detail font and actual queried line heights at the start of each level.
For every node compose its current name, measure text width, select sprite
`4*signedCategory+signedEpoch`, and measure content width. Define icon span as
contentWidth+3. Sprite drawing returns a rectangle used for connector anchors;
do not substitute the nominal text rectangle for that returned geometry.

| Level | Node slots and order | Icon x | Text-row y |
| --- | --- | --- | --- |
| Root | Selected advance | D.right−3−(iconSpan+textWidth) | D.top+trunc(H/2)−trunc(fontHeight/2) |
| Parents | Prerequisite0 then1 | D.left+trunc((2*W)/4)+trunc(W/8) | Start D.top+trunc(H/4)−trunc(fontHeight/2); add trunc(live H/2) after each slot |
| Grandparents | Parent0's slots, then parent1's slots | D.left+trunc(W/4)+trunc(W/8) | Start D.top+trunc(H/8)−trunc(fontHeight/2); add trunc(live H/4) after every slot |
| Great-grandparents | Four parents, each slot0 then1 | D.left+2 | For slot j: D.top+j*trunc(H/8)+trunc(trunc(H/8)/2)−trunc(fontHeight/2) |

The root text starts iconSpan to the right of its icon. Draw its hyperlink with
rectangle from icon left to textRight+5, y..y+fontHeight, thus including the icon
in the clickable area. Only **after** that link completes, capture root's two
prerequisite IDs. Capture its parent anchor from the root sprite rectangle's
left and vertical center. Query the next level's font height and quarter-height.

For each nonnegative parent slot, draw its sprite, connect it to its parent's
anchor, then capture its two live prerequisite IDs and publish two copies of
its sprite-left/vertical-center anchor for the following level. Create the
node link last, covering icon through textRight+5. Consequently a child's link
callback cannot retroactively change the already captured IDs for its next
level. Root/parent/great-grandparent sprite selection reads category then epoch;
the grandparent level reads epoch then category.

The root and parent pairs are cached at their stated points; descendant pairs
are read after their sprite/connector effects. At the final level, after drawing
a node's connector, test live prerequisite0≥0 or, only if false, prerequisite1≥0.
If either is nonnegative, draw a continuation mark from the sprite's left at
its vertical center to the detail rectangle's left. Then create its link.
No fifth level is expanded. Restore clip and present at the end.

#### Connectors

To connect `(childX,childY)` to `(parentX,parentY)`, shift childY by+1 when
parentY<childY, otherwise−1. Draw color10 vertical lines at childX−1 and childX+1
from that shifted y to parentY; then color122 at childX. Only when childX<parentX,
draw two horizontal color10 lines from childX+1 to parentX at parentY−1 and
parentY+1, then color122 at parentY. There is no mirrored horizontal branch for
childX≥parentX. A slot0 connector starts at the child's sprite bottom; slot1
starts at sprite top−1. Parent anchors are sprite left and vertical center.

A continuation draws from the supplied x to the live detail left, color10 at
y−1 and y+1, then color122 at y. It reloads detail left between draws. Known
technology link color106 versus unknown85 is §8's live local-human query; the
connector color does not change with knowledge.

### Original hyperlink drawing and allocation

Repeatedly measure the shared line with the detail font. While it exceeds the
hit rectangle's width, remove its final character. This changes shared text;
it does not adjust the rectangle. If even empty text cannot fit, the original
would write before the line: treat that as an excluded bug. All following
measurement/drawing uses the resulting live line.

Choose color106 only when receiver category8 and the local human knows `item`;
otherwise85. Draw an underline from x to x+measured width at y+f−2. Draw text
color10 at x+1,y; re-evaluate category/knowledge and draw the foreground at x,y;
reset text color10. Allocate/construct a link node, set its target index and
next absent, and append it to the current tail. Its ordinal is the number of
preexisting nodes. Configure a hyperlink control over the **supplied** rectangle,
assign a control identity based on ordinal, bind both click and double-click to
the selected target kind, and show it. Drawing occurs before allocation/linking;
failed creation does not roll those effects back.

### Original native hyperlink registration

Native hyperlink class registration is attempted once: publish the registered
flag **before** loading the hand cursor and registering the class. Failure does
not clear the flag for retry. The control uses the shared P04 control procedure
and then the sprite input procedure, with visible child behavior and the caller's
rectangle. Clear the five callback slots on configure before creating/publishing
the native handle. Native class/style encodings are validation adapters in
Appendix B; other implementations need the same event/hit behavior.

### Original §15. Conformance cases and integration boundaries

Use active RULES, labels and text resources as inputs. Compare logical state,
ordered queries/stream effects, control operations, text and drawing commands.
Only compare pixels when font, palette, sprites, native metrics and clipping
providers are held identical. No test should require an excluded overwrite.

| Case | Required observation |
| --- | --- |
| Missing one index section after prior population | Cleared names/descriptions, retained count/items for that category |
| Description−1 on an enabled row | Name/item remains listed; description button disabled |
| Three names `B₁,B₂,A` | Sorted text `A,B₂,B₁`; item/description mappings follow text; keys/links fixed |
| First-letter search at the end | Wrap to list head, compare case-folded first characters; return scan position, not arbitrary node key |
| Same-list movement | Replace previous history index rather than append a record |
| Pending history at count98 | Append pending and current, reaching100; entry99 appends neither |
| Replaying record call | Suppress append and reset both replaying and pending sentinel |
| Nonmultiple-of9 list height | Residual bottom pixels can compute row9; bounds follow §5 |
| Exact midpoint x | Right column; right/bottom rectangle edges excluded |
| Home versus End | Home sets scroll before redraw; End redraws before reading scroll position |
| Empty End | selected−1, first−9 and scroll−1; no invented clamp |
| Link to filtered-out item | No link allocation or fallback text draw |
| Overwide link text | Trim shared line in place before drawing; rectangle unchanged |
| Negative unit versus high-bit improvement cost | Unit displays NONE; improvement uses unsigned field times10 |
| Base terrain2 | Shields0; omit the second special-resource link |
| Expiration advance0 | Wonder page shows NONE; advance detail still finds matching wonder expiration IDs |
| Dense dependent units, odd count | No unit icons; final spacing still queries sprite62 content height |
| Scenario Describe absent on advance page | Disable advance Description, restore install directory, return without clip restoration/presentation |
| Scenario PEDIA present | Consume twelve fact rows without their fixed draws, then still process expiration text/wonders |
| Tree with only second prerequisites | Keep lower geometric slots; do not compact |
| Repeated/cyclic prerequisites | At most15 nodes, repeated IDs retained, continuation marks on last-level nonnegative prerequisites |
| Description exceeding4095 characters | Truncate shared line at remaining room, show4095 characters, retain stream position/open state |
| Description marker missing | Append NONE to the consumed boundary-marker text |
| Long title/name/facts | Safe storage or rejection/omission/stop; never adjacent-state corruption |
| Failed cursor query or selected EOF fact | Supplied retained inputs or stop; a documented safe profile may omit the click/facts |

P05's Civilopedia menu commands call Open-menu with show1. Direct-item help
callers use the modal paths in §7. V03 research-goal selection is a separate
controller: Civilopedia links display prerequisites but do not change the chosen
research goal. Rule reload repopulation must use §2, including cleanup and stream
behavior. Repeated callbacks, missing fonts/assets and native failures must
retain completed effects rather than assuming page drawing is transactional.

### Original Civilopedia display domain

Nominal inputs are initialized rules, populated description indices, terminated
fitting text, supported sprites, a usable window/font provider and positive
geometry divisors.

### Original Civilopedia display state

- Window/detail/text/list rectangles, caption height, title, title icon and its
  dimensions; working width/height; title, list-hit, edit, scrollbar and buttons.

### Original Civilopedia construction

Construct the base window, the fifteen buttons in the order in §4, edit and
scrollbar. Set closed1, view0, selected0, modal0, title control absent, link head
absent and category0.

### Original §9. Common detail-page conventions

Rule-detail renderers save clip, clip to detail rectangle, configure controls,
then fill region2. The original continues after configuration even if a nested
event changed category; a reconstruction's stale-category guard is a repair,
not an original cancellation test. Read selected and map it through the specific
renderer category's item table **after** these calls. That item remains cached
for the page; individual rule fields are read at their later use. Restore saved
clip and present the detail rectangle at ordinary completion, except the
explicit advance-page early return in §12.

Use detail font, cached height f and text mode5. Clear shared line before each
new row unless specified. Labels gain the P04 colon suffix; numeric formats are
signed decimal with no grouping. Rule-field signedness below matters for accepted
custom rules. Item/rule layouts and byte sizes are Appendix B details; the
logical conversions are F03 field values interpreted as stated.

For a plain name link at `(x,y)`, measure shared text width and queried font line
height, then use rectangle `(x,y,x+width+padding,y+height)`. Padding is5 unless
specified. Create the link through §8, which can omit it when the item was
excluded from the populated category. Do not draw a replacement ordinary label
when this helper returns without creating the link. A displayed prerequisite
name and its link's ID can differ after callbacks because rule IDs are read
again just before link creation.

### Original §10. Terrain and resource details

Let D be the detail rectangle, W its width, H its height, r=trunc(H/12),
V=trunc(W/2)−100 and X=D.left+10. V and the later right-column origin are
coordinates measured from zero; the source does not add D.left to them.
Read item from the terrain table and let base=signed(item)%11. Save sprite scale
and set its numerator to twice the previous numerator, retaining denominator.
Read base sprite0 content height/width under that scale. Start the image at
`(X,D.top+8+trunc(r/2)−trunc(baseHeight/2))`. Draw the same base/overlay/special
composition as §6, then restore scale. Cache `iconWidth=2*baseWidth+2m`.
Set foreground to unselected list text.

For a base item0..10, advance y from the image origin by `2*baseHeight+10` and
remember this as baseTextTop. Draw Move Cost(label659) at X and signed movement
rule value at V. Advance y by f; draw Unit Defense Bonus(label660). Defense
value6 displays `+200%`,3 `+50%`,4 `+100%`,5 `+150%`; every other value displays
label706 Normal. This is the page's explicit formatting table.

For a special item, first move x to `X+iconWidth+30` and y by f. Draw Terrain
Type(label661), then its base-name link after measured label width+10. Find that
name through canonical terrain item lookup followed by name-key lookup. Move y
by `2*baseHeight+10−f`. No movement/defense rows are added for this branch.

Both branches then draw three rows, each advancing y by f: Food(label64),
Shields(label662), Trade(label69), labels at X and values at V. Values are signed
food/shield/trade fields, except **item2 shields is forced0**. Beside each value
at measured width+3 draw respectively food, shield and trade resource art;
its y is rowY+trunc(r/2)−2−trunc(resourceContentHeight/2).

#### Base-terrain work and resources

After the yield rows, process irrigation then mining, each as an effects row
and a time row separated by f:

| Work | Effects label | Time label | Yield icon |
| --- | --- | --- | --- |
| Irrigation | 663 | 664 | Food |
| Mining | 665 | 666 | Shields |

For effects, if the signed destination terrain≥0, link its name at V. Else if
the signed yield bonus>0, display `+` followed by its decimal value and resource
icon; otherwise label667 `(N/A)`. For time, show the signed time when>0,
otherwise label667. Re-read destination/bonus/time after the branch and after
name measurement where the source uses it; these are not precomputed gameplay
work outcomes.

Start the right column at x=trunc(W/2), y=baseTextTop. Draw Engineer Transformation
label668, then on the next row at x+20 either label667 for a negative transform
ID or a terrain link. If item!=10, advance a row, draw Effects of Roads(label669)
at the column origin, then on the next indented row draw Move Cost, colon,
space and label707 (`1/3 of a point`). For item0,2,1 also add another row with
Trade, colon and literal ` +1`, followed by its resource icon. These displayed
road claims are explicit page rules, not a call to the tile-yield algorithm.

Advance one row and draw Possible Resources(label670), then at x+20 on the
next row link item+11. Unless item2, add another row linking item+22. Item2's
second special is omitted consistently with population's omitted item24.
Refresh the description button, restore clip and present.

#### Special-resource comparison

After the yield rows, advance y by2f and use X. Capture the base terrain's name,
base food/shields/trade (base2 shields forced0), then special food/shields/trade,
in that order. Produce three lines:

1. If special food>base food, format label708 with `(baseName,baseFood,specialFood)`;
   otherwise copy label709.
2. If special shields>base shields, format label710 with `(baseShields,specialShields)`;
   otherwise copy label713.
3. If special trade>base trade, format label711 with `(baseTrade,specialTrade)`.
   Otherwise use label712 when shields increased, label714 when they did not.

Advance y by f between lines. The label formats consume the actual argument
sequence above; unsupported conversions or reads beyond supplied arguments need
an established formatting contract or a stop. They are not arbitrary format
strings with invented extra arguments. Finish with button refresh, clip restore
and presentation. An overlong copied base name or formatted result is an
excluded overwrite, regardless of where its original destination lay.

### Original §11. Improvement, wonder and unit details

#### Improvements and wonders

Start image at `(D.left+10,D.top+8)`. Save sprite scale, set2/1, measure and draw
the improvement-ID sprite on the canonical target, restore scale, and select
unselected text color. Move y by `2*spriteHeight+5`. At X=D.left+10 draw
Prerequisites(label121), colon; let V=X+measured label width+5. If signed
prerequisite<0, draw label14 NONE there; otherwise link its advance name.
The computed preliminary half-width value coordinate is discarded: **V is the
end of this label**, including customized label width.

For an improvement, add rows at y+=f for Cost(label132) and Maintenance(label158).
Cost is10*the **unsigned** cost field; maintenance is its unsigned upkeep field.
Each has a resource icon at V+measured value width+3, vertically centered on f:
shields for cost, the maintenance/coin icon for upkeep. The source's initial
negative tests read zero-extended values and cannot take a negative branch.
A wrapped high-bit improvement cost therefore remains a positive display value.

For a wonder, add cost only, with the same unsigned conversion, then an Expires
row(label159). Display NONE if item<23 or its signed expiration advance≤0.
Otherwise link that advance. Nominal wonder items39..66 use their associated
wonder expiration records; **advance ID0 is suppressed** by the strict>0 test.
Do not replace this test with generic nonnegative-prerequisite handling.

Move y by2f and read facts for the cached improvement ID. If the resulting shared
line is nonempty, draw wrapped text in `(X,y,X+W−10,D.bottom)` using P04 rectangle
text mode5. Refresh description availability, restore clip and present.

#### Improvement facts

Construct section `PEDIAIMPROVE` followed by the signed decimal item and open it
in `PEDIA`. Start local aggregate text empty. On success clear shared line and
read mode1 lines until a line begins `@`. Skip `;` lines and empty lines. Strip
all leading `^` and `*`, one character per copy through independent scratch,
then trim rule whitespace. If the aggregate is nonempty, append one space;
append the resulting line even if stripping made it empty. At completion copy
the aggregate to the shared line. A failed section lookup publishes empty facts.
This routine does **not close the stream**, including after consuming the next
marker. Original EOF passes a null line into further parsing; a safe profile
may reject/omit that facts block, preserving prior file effects.

The original private aggregate and published shared-line destinations have
different capacities. No overflow of either is part of compatibility. A dynamic
aggregate can support longer safe custom text, or a restrictive implementation
can omit/reject it. The original byte extents are validation facts in Appendix B.

#### Units

Let r=trunc(H/12), X=D.left+10, V=trunc(W/2)−75. Save sprite scale and set2/1.
Draw the unit sprite at X and
`D.top+15+trunc(r/2)−trunc(contentHeight/2)`. Cache icon width=contentWidth+2m,
then restore scale. Set foreground to unselected text.

Place Prerequisites at x=`X+cachedIconWidth+liveContentWidth+50`,
y=`D.top+liveContentHeight`; append the prerequisite link or NONE as above.
The statistic block starts at `(X,D.top+2*liveContentHeight+f)`. Draw Cost first:
if signed cost<0 display NONE, otherwise cost*10 and a shield icon after width+5.
Its icon y uses the terrain-style r-based centering. Each of the following rows
advances by f, with labels at X and values at V:

| Label | Value |
| --- | --- |
| 127 Attack Strength | Signed attack field |
| 128 Defense Strength | Signed defense field |
| 130 Hit Points | trunc(signed hit-points field /10) |
| 131 Firepower | Signed firepower field |
| 129 Movement Rate | trunc(signed movement field /3) |

Read each rule field after its label has been drawn. These values are raw
encyclopedia statistics, not unit-instance damage, veteran adjustments or
available movement for the current turn.

Clear shared line and append the signed decimal unit ID before opening
`PEDIAUNITFACTS` in `PEDIA`. If successful, start aggregate empty and perform
**all fifteen** mode1 reads. After each read test that unit's corresponding
capability bit0..14, using the live flag field. For each set bit, copy/append
that line **starting at character1 unconditionally**, then append one space.
It does not check that the skipped character was a caret. An empty line can
therefore expose retained shared-line tail; do not assume it supplies a new
empty string at character1. Bit15 is not processed. PEDIA row order is the
capability-bit mapping; no independently hardcoded prose is substituted.

Close the stream after those reads. Draw a nonempty aggregate in the right-half
rectangle `(D.left+trunc(W/2),D.top+2*liveContentHeight+f,
D.left+trunc(W/2)+trunc(W/2),D.bottom)`. Failure to open skips facts and close.
Refresh button, restore clip, present. The source's fixed private facts buffer
can overflow; use safe storage/rejection instead of reproducing corruption.

### Original §12. Advance detail and fact selection

Save/clip/configure/fill as in §9. Start top=D.top+5. Resolve the selected advance
and draw its category/epoch sprite at D.left+m,top using the current sprite scale.
This page reads category then epoch; the list reads epoch then category.
Count dependents in three scans: advances0..99 with either prerequisite equal to
item (count once even if both match), improvements1..66 with prerequisite equal,
then units0..61 with prerequisite equal. Do not add enabled, ownership or
availability filters to these scans. This total controls later unit density.

Draw Prerequisites at D.left+50. When both signed prerequisites are negative,
display NONE. Otherwise link nonnegative prerequisite0 and prerequisite1 in
that order; when both exist, place comma after first measured width+3 and move
past its measured width+4. First-link measurement is captured before creation;
the second test and ID reads remain live. The two links are not deduplicated.

Advance top by f. At D.left+2 compose Allows(label122) plus colon; define
L=that x+measured label width+2. Draw the heading only if dependentCount!=0.
The heading is still measured for L when there are no dependents.

#### Dependents

Scan advances0..99 again. If prerequisite0 equals the cached item, select that
advance and capture prerequisite1 as the other prerequisite; otherwise test
prerequisite1 and capture prerequisite0. This gives the first slot precedence
when both match. For each selected advance:

1. Link its name at `(L,top)` with rectangle width+2.
2. Advance x by **live shared text** width+2 after the link, which may have trimmed
   the name. If captured other prerequisite≥0, draw space, `(`, label123, space;
   advance by measured width+2, link that other advance with width+2, then append
   closing `)` at live text width+2.
3. Increase top by f. Disabled advances still consume this row even if link
   lookup later finds no populated target.

Next scan improvement IDs1..66 in order, selecting prerequisite equality. Use
two columns, starting at L and then at trunc(W/2). Draw each improvement sprite,
advance x by its live content width+5, compose its name and measure it. In the
first column, when width+x+5≥trunc(W/2), replace available width by
`trunc(W/2)−5−x`; then give link creation a rectangle of that width+5. Thus
trimming uses the actual resulting rectangle. Link kind is improvement for
item<39, wonder otherwise. After each second-column item increase top by f and
return to L. If there is a final first-column item, advance top by f before
unit processing.

Scan units0..61 in the same two-column order. For dependentCount<11, draw the
unit sprite centered within its full sprite height at the current row, advance
x by spriteWidth+5, and center text using queried font height. At count≥11 use
text only at the current top. Link the unit name with padding5. For a first
column capture its sprite content height, even in text-only mode. After a second
column, advance by max(first,current content height) for count<11, otherwise f.
After an **odd final unit**, compare the cached first height to **sprite62's**
content height and advance by their maximum even in dense mode. Sprite62 is the
extra loaded sprite slot; this read is not the previous unit's height.

#### Scenario checks and fixed facts

Reset left=L. If scenario and install directory strings differ, enter scenario,
check `describe.`+extension, restore install. If absent, disable advance-detail
Description and return immediately: **the saved clip is not restored and the
rectangle is not presented** on this path. Unlike §8's general availability
check, this check does not require scenario-mode flag.

Compare directories again. When different, check `pedia.`+extension inside
scenario, restoring install afterward. Set `overridden` true only if directories
differ and that file exists. Open `PEDIACIVFACTS` in `PEDIA`. On success perform
twelve unconditional mode1 reads. Draw a row only when overridden is false and
cached item matches this table:

| Zero-based fact row | Advance IDs |
| --- | --- |
| 0 | 54,15,31,71,21 |
| 1 | 18 |
| 2 | 66 |
| 3 | 67 |
| 4 | 70 |
| 5 | 56 |
| 6 | 24 |
| 7 | 89 |
| 8 | 59 |
| 9 | 32 |
| 10 | 60 |
| 11 | 15 |

For a drawn row skip its first character only when it is `^`; draw at left,top,
then top+=f. An override suppresses these draws, **not the reads**. Advance15
can therefore receive both row0 and row11.

Read a thirteenth line unconditionally and append a space to that returned
line. Copy it to private expiration-prefix text. If it starts `^`, move the
remaining text left by one using a count that **excludes the terminator**.
The previous final character remains: with the appended space, an ordinary
`^Expires` becomes `Expires  ` with two trailing spaces. This is a defined
in-bounds overlap, not an excluded overwrite.

Scan wonders0..27. For each whose signed expiration advance equals cached item,
draw that prefix, advance left by its measured width, link improvement ID
wonder+39 with padding0, then draw P04's sentence suffix at live name width+2.
Reset left=L and advance top by f per wonder. This scan/drawing still occurs
when scenario facts were overridden. The routine does not close this PEDIA
stream. Refresh description availability, restore clip and present on ordinary
completion.

## Appendix E. Original implementation reference

This appendix preserves original storage, allocation, native API and adapter
procedures for reference validation. The main text defines required behavior.
A different internal representation or platform is permitted; semantic identities,
ordered game effects, accepted input interpretation, file/wire values and required
failure/completion results remain governed by the main algorithms and X01.
Mixed excerpts retain context: their required effects are not made optional by
appearing here. Original memory/handle traces are not a second normative algorithm.
Optional providers and harmless UI failures retain X01's existing scope limits;
this reorganization introduces no new gameplay or interoperability relaxation.

### Original Civilopedia name-entry representation

- Seven category tables, each with count and parallel description IDs, item IDs
  and name entries. A name entry has fixed key, owned text and next entry.

### Original Civilopedia row allocation

Appending a row performs these effects in order:

1. Store description ID and item ID at the current count.
2. Allocate a name entry and set its key to the current count. Append through
   the previous entry when the head is nonempty, otherwise publish the head;
   set next absent.
3. Resolve the current rules name and measure it. Allocate text including its
   terminator. Resolve the rules/pool name again after allocation, then copy it.
4. Set the remembered previous entry and increment the live count after copying.

The remembered previous entry persists across section processing. Ordinary
cleared heads make the first append independent of it. An unexpected nonempty
head without an established previous entry needs supplied retained input or a
stop. Allocation failure does not mean silently skip the row; partial table,
link and allocation effects precede the subsequent failed access.

Terrain appending composes the shared line from the terrain's name. If item≥13
and its abbreviation is exactly `Gls`, append a space and label704 `(Shield)`.
Measure that line, allocate, then copy the live shared line after allocation.
This deliberately disambiguates shield grassland without inventing a second
item24 entry. A callback that lengthens text beyond the allocated destination
would trigger the excluded-overwrite policy.

### Original concept-name allocation

name's length is measured before allocation and its text copied afterward.

### Original Civilopedia linked sorting storage

Keep node keys and links fixed. Thus equal names can reverse when an earlier

### Original Civilopedia traversal and name lookup

Traversal advances only when a next node exists; malformed shorter lists retain
the last candidate rather than automatically ending comparisons. Count≤1
normally performs no comparisons; arithmetic such as `count−1` still follows
F01 at extreme off-nominal values. Stop on unestablished reads.

Name lookup follows links until a node's **key equals the requested index**.
It is not an ordinal array read. If exhausted, return the live fallback string;
the canonical fallback initially points to empty zero-backed text.
