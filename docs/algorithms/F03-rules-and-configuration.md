# Rules and configuration

## Production contract scope

Preference saving encodes both CIV2.DAT blocks from one coherent configuration
before output begins. A failed write or final close reports failure and closes
the output exactly once; accepted preference changes remain in memory. Native
write-call boundaries admit no game/configuration changes. Original ignored-close
results and partial-file damage are reference, not production requirements.

Apply [X01's three compatibility surfaces](X01-classic-compatibility-contract.md#production-contract-and-comparison-surfaces) to this owner.
The distinction is the consumer of an observation, not whether a detail appeared
in the original executable or a historical test.

| Surface | Binding scope in F03 |
| --- | --- |
| Gameplay | Interpreted rule values, defaults and accepted configuration effects, including deliberate in-game name preservation and movement-rule changes. |
| Original files/content | Classic section/token/line grammar, field conversions, cross-references, language and override precedence; native content uses its separate schema. |
| Original peers | N02/N05 encode any resulting synchronized state or commands; no additional native callback ABI is required here. |
| Executable comparison only | Singleton parser streams/cursors, process-directory changes, allocation order, native dialogs and partial parser-object mutations. |

Production decoders stage owned rule inputs and preference updates. A shared
installer applies successful rule tables or full loads, retaining the specified
keepNames/leader policies and live-dependent fixups. Failed decoding reports its
source/section/row diagnostic without publishing a partial live table. Parser
callbacks are not gameplay interleavings; original partial parser-memory traces
are comparison details under the architecture boundary.

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

**ID:** F03. **Target:** Civilization II Multiplayer Gold
Edition **5.4.0f, patch 3**, under the [X01 contract](X01-classic-compatibility-contract.md).

This specification describes loading classic rule tables, resolving their
references, initializing preferences and applying accepted option changes. It
defines the parsing algorithm independently of the representation chosen for
the resulting tables. Exact spelling, field order and flag positions in the
text file are part of the external input format.

Evidence is pinned to OpenCiv2
`a71e8aefffe04e6f719e0e213b027b128a747711` and the independent reconstruction in
`civ2` at
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. Original storage details, source
locations, differences between reconstructions and validation limits are in
the appendices.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
semantic publications and failure effects. Only dependencies within the binding
scope above constrain evaluation; storage/API choices need no original-trace
equivalence proof. External file/wire definitions remain binding at adapters.

Token/section consumption and name/reference resolution retain their external
meaning. Staging preserves successfully admitted effects; a decoding rejection
does not require installing a partial table.

### Logical text and classic name markers

Live names use full-width implementation-local references or owned strings and
explicit custom-name flags, never signed 16-bit pool ordinals. UI label rebuilds
must not rebind retained rule names. Reload policies preserve or replace semantic
names/flags; preserved fields need no discarded allocations. Original padding,
last-allocation pointers, minimum extents and heap-size hints are comparison-only.
Empty padded-name input still means one space; ordinary empty classic pooled
input resolves to `<nil>`. These content rules do not require padded storage.

F06 stored leader records retain their exact 48-byte encoding in a separate codec
type. Live loading rebuilds names from chosen rules and consumes saved signs at
the specified stages; saved magnitudes do not index replacement text storage.
Live export writes custom/noncustom markers as -1/+1 independently of local IDs.
Lossless stored-file decode/re-encode preserves original magnitudes, including
zero and -32768. This canonical live encoding follows F06's sign-only restoration
contract; it does not claim equality with original allocation-dependent exports.
Repeated selected-tribe effects retain their specified behavior.

Narrowing a text ID into an unrelated earlier name or an accidental custom-name
marker is a repaired storage defect. Preserve intended names and flags beyond
32767/65535 entries. Reference tools may report original ordinals/aliasing;
production tests assert text, reload behavior, bounds and file-marker semantics.
Unrelated scalar arithmetic and external field widths remain unchanged. This
contract overrides original-storage interpretations elsewhere in this document.


## Compatibility policy

X01's [input-admission policy](X01-classic-compatibility-contract.md#input-admission-and-rejection-policy)
requires nominal custom tables, documented defaults/fallbacks and external text
syntax; off-nominal acceptance is optional. A declared validator may reject
malformed/unsupported content before publication, with safe cleanup instead of
original partial parser mutations. The detailed partial-load/prefix/wrap rules
below remain required for admitted domains; supported-file I/O failures are not
reclassified as harmless UI failures. Do not reject nominal custom values merely
because they exercise signed narrowing or wrap in a consuming algorithm.

Options dialogs may use new presentation and control mappings under X01.
Preserve option meanings, admissible values, actual updates and network votes.
Stock control labels/layout and native dialog failures are reference behavior;
scenario-authored text and resource override lookup remain supported.

## Implementation boundary

The main sections use logical values, identities and operations. Original memory,
allocation and platform mechanics are recorded in [Appendix C](#appendix-c-original-implementation-reference). Preserve required
state changes and externally visible results without reproducing those mechanisms.
Exact numeric domains and file/wire encodings remain required wherever specified.

## Classic and native content paths

The text grammar below owns the classic compatibility adapter. Standalone play
uses [native JSON rules/preferences](../content/data-format.md#4-rules), decoded
through an explicit semantic-to-rule normalization boundary. JSON expresses
actual hit points, named global parameters and tribe definitions; it does not
mirror RULES columns. Preserve scaling, clamps, signed consumer views,
enabled-before-fixup behavior and identity positions exactly once. For example
JSON hitPoints 30 corresponds to classic HP input 3; it is not multiplied by
ten again. Identity mappings belong to the profile/compatibility descriptor.
Native table completeness and validation are defined by that format, not
retroactively imposed on classic files.

Keep caller-controlled name/leader preservation and retained-state lifetimes on
both paths. A classic reload retains this document's lookup/default/partial-effect
contract; native pack prevalidation does not make that operation transactional.
Actual complete stock data remains a separate content dependency, not a hidden
original-file requirement or a table inferred from these parser algorithms.

## 1. Boundary, inputs and retained state

Inputs are the rule files and their search directories, the language's file
extension (normally `TXT`), existing rule and leader tables, text storage,
the leader-replacement policy, and any already-open text stream and section
index. Configuration operations also take the current game mode, live options,
remembered preferences, accepted dialog values and external service results.

Outputs are interpreted tables/settings/names, the enabled-advance count and
accepted configuration effects. Token position matters to interpreting the
source grammar, not as a required global parser object. A replacement may parse
and validate privately before publishing a complete rule set; native partial
name/field writes after file/allocation failure are optional reference. Preserve
already accepted configuration actions and meaningful in-game reload policies.
A full table load
is also not a new-game reset: it does not recreate cities, units, civilization knowledge or leader usage/name
cursors. These lifetimes follow [F02](F02-game-state-and-invariants.md#2-state-layers-and-lifetimes).

Arithmetic follows [F01](F01-determinism-and-arithmetic.md#1-numeric-model-and-notation).
Here `u8(x)` means the least nonnegative residue modulo 256, `s8(x)` interprets
that residue in −128…127, and `s16(x)` similarly uses −32768…32767. These are
behavioral conversions, not required storage types. `clamp(x,a,b)` uses the
full parsed signed integer unless a narrower conversion is explicitly shown.

### Rule values and consumer views

Categories have their logical ranges: unit domain Land/Air/Sea, roles 0…7,
advance epoch 0…3 and category 0…4, and references to an advance or a named
sentinel. These values do not require byte-sized variables. The transformations
below still define the published rule codes, including scaled movement/HP and
clamps; physical storage is described in Appendix A.

A rule code may have more than one numerical consumer view. Keep the mapping
explicit: a nonnegative code 0…255 read with `s8` denotes code itself below 128,
otherwise code−256. For example, unit cost 200 survives its 1…200 ingestion
clamp but a signed cost consumer sees −56, while an unsigned consumer sees 200.
A replacement may represent these views directly, but must not globally replace
both by an unrestricted positive statistic. A signed rule input already in
−128…127 needs no repeated conversion until an operation changes its value.

Similarly, ordinary in-range categories need no redundant conversion. This is
not an extra parser rejection rule: nominal custom content remains supported,
and admitted extra input retains the explicitly ordered reductions/clamps below.
No consumer is allowed to discard a significant conversion merely because the
file token itself did not overflow during parsing.

This document distinguishes **nominal input files**, the original's
**off-nominal handling**, and an implementation's **acceptance policy**, following
[X01's input-domain contract](X01-classic-compatibility-contract.md#2-inputs-and-supported-domains).
The conversions below describe original behavior, not a recommendation that
every implementation accept every parseable value. Section 9 explains the
compatibility limits of stricter validation.

The actual names, statistics and dependencies in a stock or scenario rule file
are **input data**. This document does not substitute a second copy of that
entire data set. There is no general “use stock rules for a missing row” step.
Only the advance/unit tail defaults and preference defaults described here
are supplied by the algorithm.

The isolated parsers, reference fixups and movement-table transformation use
**no random draws or clock queries**. File operations, error reporting and semantic presentation are external services.
String allocation is an implementation detail and cannot mutate rule operands. The surrounding reload
command reloads labels, reloads game sprites, selects the scenario directory
or home directory according to the existence of `RULES.` plus the language
extension, loads the tables, returns to the home directory, and flushes input
events. Its rendering/event callbacks are outside the isolated zero-draw claim.

F05 owns new-world setup dialogs, random choices and orchestration;
[S01](S01-scenario-loading-and-parameters.md) owns scenario entry and its
post-load preference overrides. F06 owns serialized preferences and saved-game
restoration. [S04](S04-rules-and-asset-editors.md) owns rule-editor staging,
publication, name propagation and rule-text replacement. P01/P03/P04/P10 own
platform, graphics, dialog and audio services; N02/N03 own network authority and
voting. This document specifies the rule/configuration
operations at those boundaries, not the complete callers. The mechanics that
consume a rule parameter own its gameplay formula.

### Nominal input files

Nominal files express the intended definitions directly, including custom
rules; they need not contain the stock names or statistics. Their authoring
contract is:

| Aspect | Intended form |
| --- | --- |
| Sections and rows | All required sections, unique headers, and the fixed row counts in section 3. A shorter advance/unit table ends with an explicit blank/comment line. |
| Text | Byte strings in the selected classic language's encoding; ASCII syntax and abbreviations. No Unicode normalization, quoting or escaping is implied. Each row and its newline, when present, fit within one 254-character read after text-mode newline conversion. |
| Fields | The listed comma-separated fields in order. Required values are present; optional trailing leader-title triples can be absent. Blank/comment lines occur where the relevant reader permits them, not as general whitespace inside fixed-count tables. |
| Numbers | Integer tokens in the syntax below, expressing the intended field value without relying on overflow or narrowing to turn it into another value. Signed AI values/modifiers can legitimately be negative. Numeric suffix comments and the explicitly specified clamps/rounding still work as described. |
| References and categories | Recognized advance/terrain codes and markers; unit domains 0…2, roles 0…7, advance epochs 0…3, categories 0…4, and leader title indexes 1…6. Colors, styles, gender and other field meanings follow their section definitions. |
| Abilities | An unprefixed sequence of binary digits for the intended capability positions, without relying on discarded leading digits. |

Successful loading also requires readable files and sufficient storage. Those
are environmental preconditions rather than properties of file contents.
This authoring contract is distinct from the original's validation: some
out-of-domain values are accepted, some are transformed, and some fail. A
conversion range is not automatically an intended gameplay range. Where a
consumer's full custom-value limits are not established here, do not infer
them from the original field width or from a successful parse. Likewise, a
known normalization such as rounding food-box rows is part of the algorithm,
not automatically evidence of a malformed file.

## Parser implementation freedom

Section selection, case rules, line consumption, token delimiters, conversions,
defaults and resource precedence below define classic input interpretation.
In particular, the 254-character read rule can change how long source lines
are interpreted and remains an adapter rule; it does not require a fixed buffer.
Use an owned parser/document and independent cursors if desired. The process
working directory, open-file singleton, cursor addresses, stale EOF bytes and
per-read callbacks are reference mechanisms. EOF/invalid-stream defects require
safe diagnostics, not a retained pointer. A successful reload preserves the
specified logical values, not parser allocation history.

## 2. Finding sections and consuming text

### File and section selection

To open a named section with an explicit filename:

1. Close the previous text stream. Append the default language extension if
   the filename contains no dot anywhere; uppercase the resulting path for
   classic path lookup.
2. Save the working directory. Try opening the file in the scenario/search
   directory, then in the home directory if the first open failed. Restore the
   saved working directory. This is classic case-insensitive file lookup.
3. Form `@` followed by the section name. Scan trimmed lines for an exact,
   case-insensitive match of the **whole line**. Thus `@UNITS ; comment` does
   not match `@UNITS`. An available section-index entry may seek directly to
   the header before this scan.
4. On success, leave the stream immediately after the header and the token
   cursor at the end of the header text. Return success.
5. On end of file without a match, close the stream and return failure.

If the filename is omitted, reuse the open stream. Search from its current
position; at end of file rewind **once**, then search again. This makes normal
unique sections independent of their physical order in the file. It does not
require sections to appear in the order in which the loader requests them.

The section-opening wrapper retries a failed open/search with the scenario
directory temporarily replaced by the home directory. With an explicit
filename this can find a home-file section even when the scenario file exists
but lacks that section. With no filename, the failed search has already closed
the stream: the retry does not know which file to reopen. It can reach an
invalid-stream access. Do not implement a general per-section merge of a
partial scenario file with a stock file and call it this algorithm.

With no section name, the low-level opener returns success after its file-open
step without scanning. Duplicate headers and stale section indexes are outside
the ordinary unique-header domain; Appendix A describes the retained search
state needed to compare those cases.

### Lines

Read through the original text-mode stream, including its CRLF-to-LF conversion.
One read consumes at most **254 characters**, stopping sooner after a newline
or at EOF. Remove the last newline character, then remove leading and trailing
ASCII spaces and tabs. Set the token cursor to the beginning of that line.
A longer physical line is split across successive reads; its remainder is not
discarded. A byte-order mark is not specially recognized.

There is no general blank-line or comment skipping. Except for the two early
termination rules below, a fixed-count reader consumes the next line even if
it is empty, begins with `;`, or looks like another section header. Section
search, in contrast, keeps scanning until a matching header is found.

On EOF/read failure, clear the first character of the shared line buffer, close
the stream and return no line. The old token cursor is not reset. Several
callers ignore this failure; it is not a request to synthesize a default row.
See section 9 for the resulting failure boundary.

### Tokens and numeric conversion

`nextToken` copies characters from the cursor up to a comma or end of string,
advances past one comma if present, and trims spaces/tabs from the copied token.
The tokenizer accepts empty fields; whether a field may be omitted nominally
depends on its definition. Repeated reads at end of line return empty tokens.
There is no quoting, escaping or semicolon-comment grammar. A comma inside
quotes still separates fields; quotes themselves remain text.

`nextInteger` first calls `nextToken`, then parses it as follows:

| Initial token text | Conversion |
| --- | --- |
| `0x` or `0X` | Accumulate following hexadecimal digits, case-insensitive. |
| `0b` or `0B` | Accumulate following binary digits. |
| `0d` or `0D` | Apply decimal conversion to the remainder. |
| Anything else | Apply decimal conversion to the whole token. |

Decimal conversion skips ASCII space and characters tab through carriage
return, accepts one optional `+` or `-`, and accumulates decimal digits.
Hexadecimal/binary conversion accepts neither a sign nor whitespace after the
prefix. All conversions stop accumulating at the first invalid character,
yield zero if there are no digits, wrap accumulation modulo `2^32`, and return
the signed interpretation. Decimal negation also wraps. A leading zero does
not mean octal.

The unit ability field uses a **different** `nextBinaryFlags` operation: consume
a token, then accumulate its initial sequence of `0`/`1` characters modulo
65536. There is no prefix or sign. Treat the resulting residue as a set of
positions; any higher internal flag positions are cleared on assignment.

Consequently `12 ; note` is integer 12, but `nil ; note` is an invalid advance
abbreviation. Comments work after numeric fields only because accumulation
stops there. A whole-line comment can instead terminate an advance/unit table
or become data in a fixed-count section.

### Names

Most names use the padded-name operation: an empty token becomes a single
space; other text is retained as parsed. Padding provides editing capacity,
not a maximum name length. Ordinary empty classic text resolves to `<nil>`; the decoded source may retain
an empty token separately.
The tables below identify which operation applies. A cleanroom implementation
may use normal strings; Appendix A records allocation/identity effects needed
for reference-memory or save-format validation.

## 3. Full load order and reference vocabulary

Load the sections in this order, with ascending row indexes in every table:

| Step | Section | Rows | Filename argument |
| --- | --- | --- | --- |
| 1 | `COSMIC` | 22 parameter lines | Explicit rules filename |
| 2 | `CIVILIZE` | Up to 100, then defaults | Explicit rules filename |
| 3 | `IMPROVE` | 67 | Explicit rules filename |
| 4 | `ENDWONDER` | 28 | Reuse stream |
| 5 | `UNITS` | Up to 62, then defaults | Reuse stream |
| 6 | `TERRAIN` | 33 | Reuse stream |
| 7 | `GOVERNMENTS` | 7 | Reuse stream |
| 8 | `LEADERS` | 21 | Reuse stream |
| 9 | `CARAVAN` | 16 | Reuse stream |
| 10 | `ORDERS` | 12, stored at indexes 1…12 | Reuse stream |
| 11 | `DIFFICULTY` | 6 | Reuse stream |
| 12 | `ATTITUDES` | 9 | Reuse stream |

Close the stream after the final section. This order matters: unit movement
uses the newly loaded cosmic multiplier; advance and improvement fixups use
the completed advance table; leader titles use the new government titles.
An explicit reopen at steps 2 or 3 can select a different file through fallback.
Subsequent reuse follows whichever stream that step selected.

The full loader replaces names. The individual advance, improvement and unit
loaders also accept `keepNames`: consume each name token but retain its old
name reference, including for defaulted tail rows. Numeric fields and their
defaults still change. Leader replacement is a separate policy in section 7.

### Advances

Advance references are independent of the names in the file. Exact
case-sensitive `nil` denotes **None** (no prerequisite); `no` denotes **Never**
(unavailable). Otherwise match one of these fixed codes exactly. Unknown text,
including an empty token, produces **Invalid**. Renaming an advance does not
change its code or index.

| Index | Code | Index | Code | Index | Code | Index | Code | Index | Code |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 | AFl | 20 | Cur | 40 | Lab | 60 | Phi | 80 | Sup |
| 1 | Alp | 21 | Dem | 41 | Las | 61 | Phy | 81 | Tac |
| 2 | Amp | 22 | Eco | 42 | Ldr | 62 | Pla | 82 | The |
| 3 | Ast | 23 | E1 | 43 | Lit | 63 | Plu | 83 | ToG |
| 4 | Ato | 24 | E2 | 44 | Too | 64 | PT | 84 | Tra |
| 5 | Aut | 25 | Eng | 45 | Mag | 65 | Pot | 85 | Uni |
| 6 | Ban | 26 | Env | 46 | Map | 66 | Rad | 86 | War |
| 7 | Bri | 27 | Esp | 47 | Mas | 67 | RR | 87 | Whe |
| 8 | Bro | 28 | Exp | 48 | MP | 68 | Rec | 88 | Wri |
| 9 | Cer | 29 | Feu | 49 | Mat | 69 | Ref | 89 | FT |
| 10 | Che | 30 | Fli | 50 | Med | 70 | Rfg | 90 | U1 |
| 11 | Chi | 31 | Fun | 51 | Met | 71 | Rep | 91 | U2 |
| 12 | CoL | 32 | FP | 52 | Min | 72 | Rob | 92 | U3 |
| 13 | CA | 33 | Gen | 53 | Mob | 73 | Roc | 93 | X1 |
| 14 | Cmb | 34 | Gue | 54 | Mon | 74 | San | 94 | X2 |
| 15 | Cmn | 35 | Gun | 55 | MT | 75 | Sea | 95 | X3 |
| 16 | Cmp | 36 | Hor | 56 | Mys | 76 | SFl | 96 | X4 |
| 17 | Csc | 37 | Ind | 57 | Nav | 77 | Sth | 97 | X5 |
| 18 | Cst | 38 | Inv | 58 | NF | 78 | SE | 98 | X6 |
| 19 | Cor | 39 | Iro | 59 | NP | 79 | Stl | 99 | X7 |

### Terrain results

For irrigation, mining and transformation, case-insensitive `no` means
**NoResult**, and case-insensitive `yes` means **ImproveInPlace**. Otherwise
match exactly: `Drt`, `Pln`, `Grs`, `For`, `Hil`, `Mou`, `Tun`, `Gla`, `Swa`,
`Jun`, `Oce`, in base-terrain order 0…10. They denote desert, plains, grassland,
forest, hills, mountains, tundra, glacier, swamp, jungle and ocean. Other tokens
produce Invalid. The parser accepts ImproveInPlace for transformation too;
it does not validate whether a particular work operation can use that result.
[W04](W04-terrain-work-and-improvements.md#transformation-and-destination-cleanup)
specifies the execution consequences and unsupported negative-destination read
boundary; accepted parsing does not imply a safe or meaningful Transform job.

## 4. Cosmic parameters

Each parameter consumes a new line and its **first token only**. Parse the
integer and clamp to the inclusive range below. Extra tokens are ignored.
The stock-value column reports the stock parsed result for comparison, not a
missing-field fallback. The ranges are clamp bounds, not file-rejection rules
or proof that every value outside them was unintended. An absent required
numeric value is off-nominal; the original parses its empty token as zero before
clamping.

| Line | Parameter | Clamp range | Stock value |
| --- | --- | --- | --- |
| 1 | Road movement multiplier | 1…10 | 3 |
| 2 | Trireme loss chance denominator | 1…100 | 2 |
| 3 | Food consumed per citizen | 0…10 | 2 |
| 4 | Food-box rows | 4…20 | 10 |
| 5 | Shield-box rows | 4…20 | 10 |
| 6 | Settler food, lower governments | 0…10 | 1 |
| 7 | Settler food, higher governments | 0…10 | 2 |
| 8 | Initial unhappiness city-size parameter | 4…12 | 7 |
| 9 | Empire-size unhappiness parameter | 10…100 | 14 |
| 10 | Aqueduct size limit | 4…50 | 8 |
| 11 | Sewer-system size limit | 4…50 | 12 |
| 12 | Technology cost paradigm | 3…10 | 10 |
| 13 | Base transformation work parameter | 5…100 | 20 |
| 14 | Monarchy free unit support | 0…8 | 3 |
| 15 | Communism free unit support | 0…8 | 3 |
| 16 | Fundamentalism free unit support | 0…8 | 8 |
| 17 | Communism equivalent palace distance | 1…20 | 1 |
| 18 | Fundamentalism science loss, percent | 0…100 | 50 |
| 19 | Production-category change penalty, percent | 0…100 | 50 |
| 20 | Paradrop range | 4…100 | 10 |
| 21 | Spaceship mass/thrust paradigm | 25…200 | 75 |
| 22 | Fundamentalism science-rate cap, tenths | 0…10 | 5 |

After clamping food-box rows, add one if the result is odd. Do not apply this
rounding to shield-box rows. No other cosmic fields are reset by this reader.
The values are rule parameters; for example, this loader does not itself
calculate a city's food box, loss probability or spaceship velocity.

## 5. Advances, improvements and wonder expiry

### Advance rows and defaults

Set the enabled-advance count to zero. Initialize both prerequisites of all
100 slots to Never and mark every slot disabled. Then consume up to 100 lines.
A trimmed empty line or a line whose first character is `;` **ends** the table;
it is consumed, not skipped. All later slots receive the defaults below.

For each actual row, consume and assign these fields in order:

| Field | Conversion and action |
| --- | --- |
| Name | Padded name, unless keeping names. |
| Base AI value | `s8(integer)` |
| Civilized-leader AI modifier | `s8(integer)` |
| First prerequisite | Advance lookup; store, then report Invalid immediately. |
| Second prerequisite | Advance lookup; store, then report Invalid immediately. |
| Epoch | `u8(integer)` |
| Category | `u8(integer)` |

Set enabled to whether the **first** prerequisite differs from Never. A Never
second prerequisite does not disable the row. Ordinary consumer domains are
epochs 0…3 (ancient, renaissance, industrial, modern) and categories 0…4
(military, economic, social, academic, applied). The original parser does not
enforce those domains. It does not clamp AI values either.

For each remaining slot `i`, set name to `Advance {i+1}` unless keeping names,
AI value to 3, modifier/epoch/category to zero, both prerequisites to Never,
and enabled to false. This applies both after early termination and after a
file with fewer than 100 actual definitions terminated by a blank/comment line.

After all rows and defaults, resolve references:

```text
followDisabled(p):
    while p is an advance index and advance[p].enabled is false:
        p = advance[p].firstPrerequisite
    return p

enabledAdvanceCount = 0
for i = 0 through 99:
    if advance[i].enabled:
        enabledAdvanceCount += 1
        advance[i].firstPrerequisite = followDisabled(advance[i].firstPrerequisite)
        advance[i].secondPrerequisite = followDisabled(advance[i].secondPrerequisite)
```

Forward references therefore see the completed table. Follow only disabled
nodes, using their **first** prerequisite. Stop at an enabled node or a marker.
In an ordinary freshly parsed table, a disabled node's first prerequisite is
Never, so following such a node immediately reaches Never. Do not recompute
enabled or the count after a reference changes to Never.

This is not a topological sort or complete technology-graph validation.
Enabled self-references and cycles are not rejected by this parser. The research
and AI algorithms own traversal of those graphs and any unsupported boundary.

### Improvements

Read exactly 67 lines. Each contains padded name, `u8(cost)`, `u8(upkeep)` and
an advance prerequisite. Store the prerequisite, report Invalid if necessary,
then replace it with `followDisabled(prerequisite)`. There is no blank/comment
termination and **no cost clamp**. Costs remain shield-row counts; the loader
does not multiply them by the shield-box parameter. Upkeep is gold per turn.

Index 0 is the placeholder; 1…34 are ordinary buildings; 35…37 are spaceship
parts; 38 is capitalization; and 39…66 are wonder production entries.
Wonder index is production index minus 39. These meanings do not move when
names or prerequisites change.

### Wonder expiry

Next find `ENDWONDER` and read exactly 28 lines, one per wonder in index order.
Parse the first token as an advance reference and report Invalid immediately
after storing it. Ignore other tokens. **Do not follow disabled prerequisites**
here. None, Never and an index retain their distinct parsed values; the wonder
effect consumer decides whether an expiry condition holds.

## 6. Units and terrain

### Unit rows

Reset the “double movement already applied” state to false and set every unit
type's prerequisite to Never. Read up to 62 rows, ending at a trimmed blank
line or a line beginning with `;`, as for advances. Assign fields in this order:

| Field | Conversion |
| --- | --- |
| Name | Padded name unless keeping names. |
| Obsoleting advance | Advance lookup. |
| Domain | `u8(integer)`; ordinary inputs: 0 land, 1 air, 2 sea. |
| Movement | `u8(u8(integer) * roadMovementMultiplier)` |
| Range | `u8(integer)`; air endurance parameter. |
| Attack | `u8(integer)` |
| Defense | `u8(integer)` |
| Hit points | `u8(u8(integer) * 10)` |
| Firepower | `u8(integer)` |
| Cost | `clamp(integer, 1, 200)`; shield-row count. |
| Carrying capacity | `u8(integer)` |
| AI role | `u8(integer)` |
| Required advance | Advance lookup. |
| Abilities | `nextBinaryFlags` |

Only **after the entire row**, report an invalid obsoleting or required advance.
Neither reference is passed through `followDisabled`. Movement is in the
game's fractional movement units; hit points are in tenths of the file's value.
The inner conversion in the multiplication may be implemented signed or
unsigned because the final residue is the same. Later consumers can interpret
these residues as signed values; the parser does not protect against that.

AI-role input numbers are 0 attack, 1 defend, 2 naval superiority, 3 air
superiority, 4 sea transport, 5 settle, 6 diplomacy and 7 trade. Other numbers
are retained by the parser but are outside the ordinary role domain.

Ability positions in the external binary token count from the **rightmost
digit as position 0**:

| Position | Capability represented |
| --- | --- |
| 0 | See two squares |
| 1 | Ignore zones of control |
| 2 | Amphibious attack |
| 3 | Submarine restrictions |
| 4 | Attack air units |
| 5 | Coast-bound/trireme restriction |
| 6 | Ignore city walls |
| 7 | Carry aircraft |
| 8 | Paradrop |
| 9 | Alpine movement |
| 10 | Double defense against horse units |
| 11 | Free support under fundamentalism |
| 12 | Destroyed after attacking |
| 13 | Double defense against air units |
| 14 | Detect submarines |
| 15 | Retained; no named capability established here |

These are decoded capabilities, not their complete eligibility/effect formulas.
For each unfilled slot `i`, set name to `Unit {i+1}` unless keeping names,
obsoleting and required advances to Never, domain to land, attack to 1, cost
to 1, role to attack, all other numeric values to zero, and abilities empty.
The separate name lookup used by the original interface has a tail-row quirk
recorded in Appendix A.

### Terrain rows

Read exactly 33 lines. All rows contain padded name, movement cost, defense,
food, shields and trade, in that order. Apply `u8` to all five numeric fields.
Defense is a count of 50-percent units. No range validation or early
termination is performed.

For the first 11 rows only, also consume:

1. Irrigation result (terrain lookup), yield bonus, work turns and AI-government
   threshold. Apply `u8` to the three numeric fields. After assigning all four,
   report an invalid result.
2. Mining result, yield bonus, work turns and AI-government threshold, with the
   same conversions and error timing.
3. Transformation result. Store its terrain lookup and report Invalid.

These are base terrains in the order given in section 3. Rows 11…21 are the
first special for base terrain 0…10; rows 22…32 are the second special.
Special rows consume only the six common fields. Their existing work and
transformation fields remain unchanged. Additional columns on any row are
ignored once that row's expected fields have been consumed.

## 7. Governments, leaders and presentation tables

### Governments and leaders

Read seven government rows: padded name, padded male title, padded female
title. Their fixed order is anarchy, despotism, monarchy, communism,
fundamentalism, republic and democracy. Always replace these definitions,
including when the caller requests preservation of leader data.

Read 21 leader rows. With `replaceLeaderData` true, assign:

| Field order | Value |
| --- | --- |
| Male name, female name | Padded names. |
| Gender | `clamp(integer, 0, 1)`; 0 selects male, 1 female. Also set the current leader-name reference to the selected name. |
| Color | `clamp(s16(integer), 1, 7)` |
| City style | `clamp(s16(integer), 0, 3)` |
| Tribe plural, adjective | Padded names. |
| Aggression, expansion, civilization traits | `s8(integer)` each, without clamping to −1…1. |

Then initialize all seven male/female title pairs from the government defaults.
Read optional triples of `governmentNumber, maleTitle, femaleTitle`, using
ordinary pooled text for titles. Stop as soon as the government number is
nonpositive; a missing token is zero and therefore terminates. Positive
government numbers 1…6 replace that title pair. Repeated numbers overwrite
earlier overrides. Government 0 is a terminator, so this syntax cannot override
anarchy. A positive number greater than 6 is not checked by the original and
can write outside the title array. That overwrite is an excluded bug, outside
the successful supported domain; reject the invalid index without corrupting
other state, rather than treating adjacent storage as extra title slots.

With `replaceLeaderData` false, still read every field and allocate its strings,
but restore each previous leader field after reading its replacement. Do not
change the selected leader name or initialize the leader's default title pairs.
Read/allocate optional title overrides and restore those old pairs too. This
preserves leader data while consuming the required input. Discarded allocations
and temporary name publication are original-reference details, not required effects.

Neither policy resets the leader's times-used count or next city-name cursor.
The existing values are retained across this operation.

### Remaining sections

| Section | Per-row action |
| --- | --- |
| `CARAVAN` | Store first token as a padded commodity name, 16 rows. |
| `ORDERS` | Store first token as ordinary pooled text; take the first character of the next trimmed token as its key. Empty token gives the null character. Write slots 1…12; leave slot 0 untouched. |
| `DIFFICULTY` | Store the entire trimmed line as ordinary pooled text, six rows. |
| `ATTITUDES` | Store the entire trimmed line as ordinary pooled text, nine rows. |

Difficulty and attitude rows keep embedded commas and semicolons. Their text
does not define difficulty or diplomacy formulas. Order keys are not
uppercased by this reader; this specifies the stored label/key, not a global
rebinding of every input command.

## 8. Preferences and accepted configuration changes

### Preference initialization and persistence

Preference defaults are remembered choices, separate from a running world's
options. Ordinary defaults select normal combat, cylindrical topology, no
bloodlust and normal eliminated-player restart policy. Set difficulty to
chieftain, tribe/gender choices to 0, science to 6 tenths, tax to 4 tenths,
opponent choice to 0, map-size choice to 1, barbarian-activity choice to 2,
the five world-parameter choices to 0, rules choice and accelerated start to 0,
and the preference text field to empty. The legacy default-level preference
is 2 when screen width exceeds 999, otherwise 0; no further use is specified
at this boundary.

Enable sound, music, Enter-closes-city-window, instant advice, autosave and showing
enemy moves. Disable always-wait, tutorial, mouse-goto, fast slide and skipping
pauses in enemy movement. Enable all six graphic options: wonder movies,
throne room, diplomacy screens, advance Civilopedia displays, animated council and
animated heralds. Clear animated heralds if the startup animation-availability
check fails. The original native memory query is in Appendix C; its argument
does not prescribe the replacement renderer's memory allocation. All eleven
message options start enabled.

Multiplayer defaults clear the remembered menu/protocol/hotseat choices, open
game, kill-on-retirement, show-human-starts, chat-from-start, simultaneous moves,
double production and double movement. Empty the game/player/email/IP strings;
set timer choice to 0 and custom timer text to `60`. Clear remembered chat and
parley rectangles, both clear-chat preferences, the five legacy setting fields
and the remembered multiplayer settings table. Fields not assigned by these
default operations retain their old contents. This does not establish a
multiplayer session.

Load preferences from `CIV2.DAT`. If opening fails, initialize both ordinary
and multiplayer defaults. Otherwise read the ordinary block, using ordinary
defaults if that block read fails; then attempt the multiplayer block at the
current file position, using multiplayer defaults if that read fails. Close
the file. There is no original general value-range or string-termination
validation after successful reads. [F06](F06-save-load-and-file-formats.md#civ2dat) defines the block encoding,
retained tails and partial-I/O effects. These settings are not stored in `CIV.INI`.
Platform/startup INI settings are a separate service boundary.

To save, open `CIV2.DAT` for binary replacement, write ordinary preferences,
then multiplayer preferences. Return success only if both writes succeed.
Close an opened file; on a failed write, delete that partial output. An open
failure returns failure without deleting a file. Callers below do not roll back
live options if saving fails.

### Accepted setup rules

The normal-rules selection uses normal combat and clears the optional bloodlust
and no-restart settings. Advanced selection starts from the classic base rule
set, then assigns these independent choices:

| Accepted choice | Configuration result |
| --- | --- |
| Simplified combat | Clear normal-combat mode; unchecked sets normal combat. |
| Flat world | Select bounded horizontal topology; unchecked selects wrapping. |
| Select opponents | Set the setup selection policy, not a rules-table value. |
| Accelerated start | Set the setup accelerated-start choice. |
| Bloodlust | Set the conquest-only rule used by victory/setup consumers. |
| Do not restart eliminated players | Set the restart restriction. |

For a freely generated world, copy the resulting rules to remembered rules.
For a premade world with forced topology, copy the other rules but preserve
the **previous remembered topology**. The live game uses the premade world's
topology. Setup clears flatness for forced-wrapping input and sets it for
forced-flat input before accepting the choices.

The new-game consumer copies remembered game options while clearing cheat
mode. Chieftain forces tutorial and instant advice on; other difficulties
clear tutorial while retaining the advice preference. The remaining setup
selection, cancellation/backtracking, random choices, and publication of these
settings belong to F05.

### Accepted in-game option dialogs

The following changes occur only for an accepted dialog result. Cancellation
leaves live and remembered option sets unchanged by these commands. Dialog
scratch state and presentation callbacks are separate effects.

The game-options dialog replaces exactly its eleven controlled settings, in
checkbox order: sound, music, always-wait, autosave, show-enemy-moves,
skip-enemy-move-pauses, fast-slide, instant-advice, tutorial, mouse-goto,
Enter-closes-city-window. Clear all eleven settings first; preserve other
options, including map grid, cheat mode and the graphics group. Apply sound
and music first. Disabling music requests playback stop. Enabling it from off
requests music reinitialization through P10; enabling it requests music-option
synchronization even if it was already on. Native MCI sequencing is recorded
in [Appendix C](#appendix-c-original-implementation-reference). Then assign the remaining nine settings, copy the full
live option set to preferences and attempt a preference save.

The graphics dialog replaces only its six settings, in order: throne room,
diplomacy screens, animated heralds, advance Civilopedia displays, animated
council, wonder movies. Clear the six settings before rebuilding them. An
accepted herald-enable choice sets the preference even if a local resource
warning is displayed. The original advisory memory query is in the
implementation appendix. Copy the full live option set to preferences and attempt a save.

The message dialog starts with all messages enabled, then disables each
unchecked item: growth halted, improvement built, unit built, invalid build,
disorder, order restored, celebration, food low, pollution, production penalty,
and zoom-to-city availability. This replaces the entire message option set,
including clearing unrecognized disabled-message entries. Copy it to
preferences and attempt a save.

Multiplayer option changes require the caller's local/host authority and, where
applicable, player agreement. At the accepted-dialog boundary, update the two
clear-chat preferences and attempt a preference save **before** processing a
double-production/movement proposal. A rejected or unresolved proposal leaves
the live double settings unchanged and restores their remembered values to
the live ones. When accepted and different from the live settings, install
the double settings and invoke the movement transformation below. Network
voting and ordered proposal/publication belong to
[N03](N03-multiplayer-turn-sequencing.md#double-production-and-double-movement-proposals);
[N02](N02-network-commands-and-state-synchronization.md) owns message encoding
and reply dispatch. Double production is a consumer
setting; this command does not
multiply production or cost rule tables.

### Applying or removing double movement

Let `applied` be the retained state reset by the unit-table loader, and
`requested` the live double-movement option. Ordinary inputs use Boolean values.

```text
applyDoubleMovement():
    if mode is single-player:
        return
    if requested and not applied:
        applied = true
        for i = 0 through 61:
            if unitType[i].domain is land:
                unitType[i].movement = u8(unitType[i].movement * 2)
        redrawStatus(1)
    else if not requested and applied:
        applied = false
        for i = 0 through 61:
            if unitType[i].domain is land:
                unitType[i].movement = u8(truncTowardZero(s8(unitType[i].movement) / 2))
        redrawStatus(1)
```

Repeated requests in the same state do nothing. Single-player mode leaves
both the table and `applied` untouched, even if they came from a multiplayer
state. Air and sea types are unchanged. No existing unit's spent movement
changes. Removing doubling is not necessarily an inverse after wrap or signed
reinterpretation: do not reconstruct a remembered original table instead of
performing the specified division.

### Human city-production preferences

`loadCityPreferences` resets the ordered autobuild list's count to zero, retaining
its unused payload and the existing NoDefend value. Open `CITYPREF.TXT` section
`NODEFEND` using section 2's search rules. Success sets NoDefend=1; a missing
file or section **does not clear it**. Close the text stream, then independently
open `CITYPREF.TXT` section `AUTOBUILD`. A failed second open returns with no
additional close beyond the opener's own behavior.

After a successful second open, repeat:

1. Read one trimmed line. If the reader returns no line, end the loop. Otherwise
   read its first comma-separated token; trailing tokens are ignored.
2. Scan improvement identities 1…38 ascending. Compare the token case-insensitively
   with each improvement's current pooled name. The first equal name wins.
3. If a match exists, append its identity and increment the live count. Stop
   when count reaches 32. If no match exists, stop immediately without appending.

Close the stream after the loop, including the EOF path.

Nominal input contains optional `@NODEFEND` and an `@AUTOBUILD` list of recognized
current improvement names, at most 32 entries. The marker section needs no body;
it is presence-based. A first token with no matching name terminates the list,
including ordinary blank/comment lines; there is no comment-skipping step.
Duplicate names/rows are not
deduplicated, and changing language or rule names can change matches. Wonders
are not searched. Missing files leave an empty list and retained NoDefend.
No missing-file fallback populates a stock list or resets NoDefend to zero.

The list is consumed by [A02](A02-ai-city-production.md#4-candidate-order-and-common-weighting),
including human advice, after ordinary improvement scoring. The loader itself
does not choose production or alter city automation flags. A callback-corrupted
count cannot authorize writes past the 32-entry list: reject/stop at that write
after prior parsing effects under X01, without corrupting adjacent state.

## 9. Off-nominal handling and implementation policy

### Deterministic parsing and conversion

The original accepts many inputs outside the nominal authoring contract.
The following are recovered behaviors, not recommended ways to express rules:

| Off-nominal input | Original handling |
| --- | --- |
| Empty required numeric token, or token with no numeric digits | Produce zero, then apply the field's conversion or clamp. |
| Numeric token with unexpected suffix text | Stop accumulating at the first invalid character. This same mechanism also permits ordinary numeric suffix comments. |
| Integer accumulation or field conversion exceeds its numeric domain | Apply the specified modular reduction and signed interpretation, at the stated boundary. |
| Excess leading ability digits | Retain the last 16 binary positions accumulated before the first nonbinary character. |
| Value outside a named semantic domain, such as advance epoch 255 | Retain/convert it unless the particular reader explicitly clamps or rejects it; later consumer behavior is a separate question. |
| Physical line exceeds a single read | Split it across reads, potentially changing which fields or rows are consumed next. |

Clamps are field-specific. For example, improvement cost −1 becomes 255,
whereas unit cost −1 becomes 1. Leader color 65538 becomes 2 because narrowing
precedes the clamp. These results remain part of full compatibility even
though such inputs are outside the nominal authoring convention. Preserve
the formulas in sections 2 and 4–7 rather than replacing them with a generic
range check or clamp.

### Defined errors and retained effects

The abbreviation errors below report the offending line and zero-based row.
They are fatal error-service calls in the original; they are not recoverable
row rejection or requests to replace the row with stock data.

| Error | Point at which it is reported |
| --- | --- |
| Invalid advance prerequisite | Immediately after assigning that prerequisite; distinguish first/second. |
| Invalid improvement prerequisite | After assigning it, before disabled-reference fixup. |
| Invalid wonder expiry | Immediately after assigning it. |
| Invalid unit reference | After all fields, including abilities, have been assigned. |
| Invalid terrain work result | After that result and its three numeric fields have been assigned. |
| Invalid terrain transformation | Immediately after assigning it. |

Keep every earlier mutation if recording the state at a fatal boundary.
Original error handling records diagnostics, invokes cleanup and terminates;
its platform output and memory-sensitive failure details belong to X01/P04.
A test stub that returns from an error callback is not evidence that the
original resumed parsing.

### Failures requiring additional state or evidence

Missing sections, absent fixed-count lines, oversized path/name storage and
out-of-range leader title indexes do not share a single checked failure path.
Several can close a stream and later dereference it, or read a retained cursor
or adjacent storage. For those cases, preserve the observed prefix and report
the first unsupported access unless the fixture supplies the backing state
required by X01. Do not invent an original successful result or silently repair
the input. A cleanroom implementation may report a safe failure beyond this
bounded contract. Writes outside path/name buffers or the title array are
excluded bugs under X01, even when adjacent bytes are known. Backing-state
fixtures for reads do not make those writes required; safe failure may prevent
the overwrite.

Nominal authoring limits must not be mistaken for extra classic parser checks.
In particular:

- A blank/comment inside a fixed-count section consumes a row; it is not skipped.
- A header does not terminate advances/units by itself. Without a blank/comment,
  it can be read as a row and fail its prerequisite lookup.
- Numeric conversion applies wrapping and clamping in the specified order;
  a later clamp does not undo earlier narrowing. Editor UI limits do not become
  additional file-parser limits.
- Renamed rows retain index-based special meanings. Reordering rows changes
  their identities; there is no name-based remapping pass.
- Rule loading does not validate all possible research graphs or gameplay
  consequences of zero/negative-interpreted statistics.

### Stricter input policies

An alternative implementation may reject inputs that the original would parse,
wrap, clamp or partially load. For example, it may require known enum values,
reject missing numeric fields, or reject values whose conversion loses magnitude
or changes sign. Such a policy should state its accepted ranges and syntax,
its validation boundary, its diagnostic result and whether rejection leaves
state unchanged or preserves any earlier mutations. Preflight validation of
the whole file is a possible alternative policy; it is not the original's
incremental loading algorithm.

This permits a compatibility claim **over the declared accepted domain**.
It does not establish full input compatibility for rejected cases. Merely
accepting an off-nominal file and silently changing its result is a different
algorithm, not stricter rejection. For example, storing improvement cost −1
unchanged in a wider integer, or clamping it to zero, does not reproduce 255.

Restrictions on file inputs also do not justify omitting wrap during accepted
executions. Range checks must account for scaling and later operations: a
movement input that fits before multiplication can cross a signed boundary
after applying the road multiplier or double movement. Keep those arithmetic
rules unless equivalence has been established throughout the supported domain.
If intentionally excluding an otherwise playable scenario, declare that
restriction rather than calling it an original file error.

## 10. Worked cases by input domain

These cases use the isolated operation named in each row, fresh parser input,
adequate storage and no intervening callbacks. Unmentioned state is retained.
They consume no random draws or clock queries. For an entire section, supply
the remaining required rows as valid data; the single-row examples are not
complete files.

### Nominal loading and configuration

| Operation and input | Expected observation |
| --- | --- |
| Integer tokens `010`, `0d-12`, `0x10`, `12 ; note`. | 10, −12, 16, 12 respectively. A negative value is nominal only where the field's meaning permits it. |
| Food-box input 5 and shield-box input 5. | Food rows become 6; shield rows remain 5. |
| Road multiplier 3; unit row `Test,no,0,2,0,1,1,2,1,10,0,0,nil,101`. | Land; movement 6; range 0; attack/defense 1; hit points 20; firepower 1; cost 10; hold 0; attack role; required None; obsoleting Never; ability positions 0 and 2 set. |
| One complete valid unit row, then a blank line. | Slot 0 receives the parsed row; slots 1…61 receive the specified `Unit 2` through `Unit 62` defaults. |
| Preserve-leader load with new names/titles and different traits. | Existing leader values and cursors remain; new strings are still consumed/stored, and government definitions change. |
| Multiplayer; land movement 6, air movement 8; requested true, applied false. Apply twice, then set requested false and apply. | First: land 12, air 8, applied true, one redraw. Second: no change/redraw. Third: land 6, air 8, applied false, one redraw. |

### Off-nominal file parsing

These cases exercise permissive parsing and out-of-domain values. Some include
nominal tokens for comparison. They do not prescribe a stricter implementation's
diagnostics or rejection timing.

| Operation and input | Expected observation |
| --- | --- |
| Tokenize ` a , ,b;tail,` through five reads. | `a`, empty, `b;tail`, empty, empty. |
| Integer tokens `-0x10`, `0xG`, `4294967295`. | 0, 0, −1 respectively. |
| Ability tokens `101x11`, `0b101`, `10000000000000001`. | Residues 5, 0, 1 respectively. |
| Lookup `nil`, `NIL`, `AFl`, `afl`; terrain `YES`, `Drt`, `drt`. | None, Invalid, advance 0, Invalid; ImproveInPlace, desert, Invalid. |
| Food-box input 999; empty required road-multiplier token. | Food rows become 20; the road multiplier becomes 1. These are original transformations, not a requirement that a stricter policy accept either input. |
| Road multiplier 3; unit row `Test,no,0,43,258,-1,256,13,2,0,257,7,nil,101x11`. | Land; movement 129; range 2; attack 255; defense 0; hit points 130; firepower 2; cost 1; hold 1; trade role; required None; obsoleting Never; ability positions 0 and 2 set. |
| Improvement cost −1; unit cost −1. | Improvement cost 255; unit cost 1. |
| Leader replacement with color 65538, style 65538 and gender 2. | Color 2, style 2, female. Narrowing color/style occurs before clamping; gender clamps without that narrowing. |

### Reference, session and failure boundaries

These cases are not all malformed-file cases. Valid tokens can produce unusual
reference graphs, arithmetic can cross a boundary after loading, and an I/O
failure is independent of file contents. Preserve these distinctions when
defining a restricted input domain.

| Operation and input | Expected observation |
| --- | --- |
| Advance rows 0=`A,3,0,Alp,nil,0,0`, 1=`B,3,0,no,nil,0,0`, 2=`C,3,0,nil,no,0,0`, then a blank. | Rows 0 and 2 are enabled; count 2. Row 0's first prerequisite becomes Never after fixup, but row 0 remains enabled. Row 2's second remains Never. Slots 3…99 default disabled, beginning with `Advance 4`. |
| Enabled advances 0 and 1 require each other. | Both references remain; this loader does not reject the cycle. |
| Improvement prerequisite and wonder expiry both refer to disabled advance 1 from the case above. | Improvement prerequisite becomes Never; wonder expiry remains index 1. |
| Multiplayer; land movement 70, air movement 8; requested true, applied false. Apply twice, then set requested false and apply. | First call: land 140, air 8, applied true, one redraw. Second: no change/redraw. Third: land 198, air 8, applied false, one redraw, since `s8(140)/2 = −58`. |
| Same doubled state, but mode single-player and requested false. | No table, applied-state or redraw change. |
| Failed preference save after accepting an option change. | Live and remembered options retain the accepted values; a partial output is deleted if the save opened it. |

## Appendix A. Reference representation and validation mapping

These details allow reference snapshots and serialized state to be compared;
they do not require a cleanroom implementation to use these layouts.

### Tables, markers and strings

| Reference object | Original representation |
| --- | --- |
| Cosmic | 41 bytes; the 22 fields in section 4 occupy offsets 0…21, followed by 19 retained bytes. |
| Advance | 100 records of 16 bytes: fixed abbreviation at 0, name ID at 4, retained byte at 8, enabled at 9, AI value/modifier at 10/11, category/epoch at 12/13, prerequisites at 14/15. |
| Improvement | 67 records of 8 bytes: name ID at 0, cost/upkeep/prerequisite at 4/5/6, retained padding at 7. |
| Unit type | 62 records of 20 bytes: name ID at 0, flags at 4, obsolescence at 8, then domain, move, range, attack, defense, hit points, firepower, cost, hold, role, prerequisite at 9…19. |
| Terrain | 33 records of 24 bytes: abbreviation at 0, name ID at 4; move/defense/food/shields/trade at 8…12; transform at 13; irrigation/mining results at 14/15, bonuses at 16/17, work turns at 18/19, AI thresholds at 20/21; retained tail at 22/23. |
| Leader | 21 records of 48 bytes: traits at 0…2; usage at 3, gender at 4, city-name cursor at 5; signed 16-bit color/style/current-name/plural/adjective at 6/8/10/12/14, two name IDs at 16/18, seven title pairs at 20…47. |
| Advance markers | None = −1, Never = −2, Invalid = −3, stored as signed 8-bit references. |
| Terrain markers | NoResult = −1, ImproveInPlace = −2, Invalid = −3, stored as signed 8-bit results. |
| Ability set | Parser accumulates 16 bits, then zero-extends into the 32-bit unit-type flags field. |

Names normally have 32-bit string IDs, except the leader fields identified
above, which narrow IDs to signed 16 bits. String storage is append-only at
this boundary; no deduplication occurs. Ordinary nonempty pooled text appends
its bytes plus a null terminator and increments the string count. Empty pooled
text returns ID 0, allocates nothing and leaves the last-string pointer alone.
Padded text allocates `max(minimum, length+1)` zero-filled bytes, substitutes
`" "` for empty input and always increments the count. Its minimum sizes are:

| Strings | Minimum allocated bytes |
| --- | --- |
| Advances | 20 |
| Improvements | 25 |
| Units, terrain, government names/titles | 15 |
| Leader names/plural/adjective | 24 |
| Commodities | 10 |

Custom leader titles, order labels, difficulty names and attitude names use
ordinary pooling. Allocation failure is an external fatal boundary. These
minimums are not truncation limits.

Parsed unit rows with name replacement also update a parallel interface
name-pointer table to the last allocated string. Defaulted tail rows allocate
their `Unit n` names **without updating those pointers**. Keeping names leaves
both references alone. A reference snapshot must preserve this asymmetry.

The shared text line buffer is read with `fgets(..., 0xff, ...)`; the token
cursor survives a failed read. The section index holds file offsets for a
specific uppercased path. Its builder recognizes trimmed headers beginning
with `@` followed by uppercase A…Z and has finite capacity. A found offset
greater than zero seeks to the header; a missing entry falls back to scanning.
For unique complete headers this is an optimization. For duplicate headers,
an uncached explicit open finds the first match from the beginning, a reused
stream finds the first match from its current position before rewinding, and
a cached open starts at the cached match. Comparing duplicates or a stale
index requires the stream position, index contents and underlying file as
fixture inputs; a dictionary of section names alone is insufficient.

### Configuration and error encodings

The setup base rules value is `0x002f`, normal rules `0x003f`. The named bits
are normal combat `0x0010`, bloodlust `0x0080`, no restart `0x0100`, flat world
`0x8000`. Keep the base bits even though not all have a named checkbox here.
Ordinary default game options are `0x003f3258`; default disabled-message bits
are zero. The game-options dialog preserves `options & 0xffff8027` before
rebuilding its group; the graphics group is `0x003f0000`. Animated heralds
are `0x00200000`. The message dialog rebuilds its entire disabled-message
value from zero. Map these to the logical settings in section 8; they are
not required bit positions in a new application's state.

`CIV2.DAT` reads/writes an ordinary 72-byte block followed by a 444-byte
multiplayer block, each as one item. A failed first item does not seek back
before reading the second. Complete encoding and preservation of unassigned
fields are specified by [F06](F06-save-load-and-file-formats.md#civ2dat). Save returns 0 on success and 1 on failure.

All listed rule-reference errors use module 3 and the offending line as
context. Error codes are −4 for advance prerequisites and wonder expiry,
−5 for improvement prerequisites, −6 for unit references, −7 for terrain
results. Data fields are row and prerequisite position 0/1 for advances;
row and 0 for improvements/wonders/units; row and 0/1 for irrigation/mining
or 99 for transformation. On the original fatal path, cleanup precedes the
debug-break/exit boundary; no table rollback occurs.

## Appendix B. Evidence, differences and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

### Source traceability

The pinned source revisions at the start of this document determine the
evidence. Links below navigate the current checkout; addresses identify the
original entry points when source names move.

| Contract | OpenCiv2 source and original entry points |
| --- | --- |
| Clamp readers and cosmic | [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp): `rules_next_line_byte_clamped` `00419cbb`, `rules_next_byte_clamped` `00419cf4`, `rules_load_cosmic` `00419d23`. |
| Advance, improvement, unit, terrain loading | [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp): `rules_load_advances` `0041a046`, `rules_load_improvements` `0041a422`, `rules_load_units` `0041a5c4`, `rules_load_terrain` `0041a95f`. |
| Leaders, tail sections and reload wrapper | [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp): `rules_load_govts_leaders` `0041ab18`, `rules_parse_sections` `0041b00e`, `rules_load` `0041e864`. |
| Abbreviation lookup and double movement | [civ.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/civ.cpp): `rules_parse_advance_abbrev` `004b0720`, `rules_parse_terrain_abbrev` `004b07d1`, `rules_apply_double_moves` `004e1314`. |
| Files, lines, tokens and flags | [text.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/text.cpp): `text_open_file_section` `004a2055`, `text_open_section` `004a2379`, `text_read_line` `004a23fc`, `text_next_token` `004a24b1`, `text_next_int` `004a2534`, `text_next_binary` `004a25d5`; section index builder `004db23f`. |
| Numeric/string services | [text.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/text.cpp): hex `0046e8f0`, prefixed integer `00564bf0`, binary `005bad40`, trim `004cfff0`/`004d007e`, pooling `00428a95`/`00428b68`; [regargs.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/regargs.cpp): newline stripping `0056b810`; [crt.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/native/crt.cpp): decimal conversion. |
| Preference defaults/load/save, setup choices | [game.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/game.cpp): `004a70b0`, `004a71bb`, `004a733d`, `004a73d9`, `game_init_new` `004aa9c0`, `setup_choose_game_options` `0051dd97`. |
| Human production preferences | [city.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/city.cpp): `city_load_citypref` `00498d40`; independent `src/window_startup.cpp`, `load_city_preferences`. |
| Accepted option commands | [window.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/window.cpp): game `004e0ab0`, graphics `004e0d71`, multiplayer `004e0f18`, messages `004e1452`. |
| Fatal reporting | [support.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/support.cpp): `error_report` `00589ef8`, `err_fatal_exit` `0058a0ee`. |
| Layouts, codes and stock numeric data | [civ2types.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h), [enums.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/enums.h), [gamedata.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/data/gamedata.cpp), [rules.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/game/rules.cpp), [rules.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/rules.h). |

Independent reconstruction evidence at the pinned `civ2`
revision comes from `src/rules.cpp`, `src/rules_sections.cpp`,
`src/rule_tables.cpp`, `src/rules_full.cpp`, `src/application_error.cpp`, and
the advance/terrain initializer files. These corroborate original field order,
fixed codes, numeric conversion and invalid-stream boundaries. Their safety
checks and host file adapters are not automatically original behavior.

### Differences to keep separate

- OpenCiv2 uses built-in numeric tables and translated names when
  `rules_from_scenario()` is false. The classic loader reads the chosen rules
  file for ordinary games too. Its home-directory custom rules must not be
  discarded merely because the current port takes its built-in-data branch.
- BF-129 adds epoch/category validation absent from the original parser.
  OpenCiv2 checks after narrowing; the independent reconstruction also checks
  the raw domain. Neither extra rejection is part of section 5.
- BF-226 rejects unterminated multiplayer preference strings after a successful
  block read. The original only used the read result at that boundary.
- Native OpenCiv2 implements decimal conversion through host `strtol`. Its
  behavior outside the host's integer range is not the original modulo-`2^32`
  accumulation. The independent reconstruction explicitly preserves that
  accumulation; a parser must not inherit arbitrary host overflow behavior.
- Bounded path/section/error-context copies and safe host invalid-stream
  handling change memory-failure cases. This document bounds those cases
  explicitly instead of treating the safety behavior as an original result.
- Graph traversal fixes in downstream research/AI code do not establish a
  cycle rejection step in the rules parser. Real scenario data may contain
  cycles; parser acceptance and later traversal behavior are separate contracts.
  [V03](V03-research-and-technology.md#3-prerequisite-walks) specifies the original
  ordered ancestry, bounded termination handling and the visited-set repair.

The [bug ledger](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/BUGS.md) records these always-on changes and other relevant
consumer differences. Selecting classic mode alone does not remove all of them.

### Validation scope

The later [A02 source checks](A02-ai-city-production.md#appendix-b-evidence-and-validation-limits)
compile and execute the actual CITYPREF loader with supplied line/token/name
adapters, including retained NoDefend, duplicate names, first-invalid termination
and the 32-entry cap. Those checks are separate from the parser harness below.

Source review covered the loaders, text primitives, fixed abbreviations,
preference defaults and accepted-option paths in both reconstructions where
available. The worked cases describe focused conformance obligations for the
parser and configuration boundary; they are not claims about every later
consumer of custom values.

For this document, a temporary standalone harness executed 27 extracted
OpenCiv2 functions and passed **865 assertions**. Cases covered token/integer/
ability parsing, abbreviation lookup, uncached section searches and home-file
fallback, long-line splitting, cosmic clamps, advance defaults/fixups/cycles,
improvement versus expiry references, unit conversion/error timing, retained
special-terrain fields, leader replacement/preservation, double movement and
a complete synthetic rules-file load through the presentation tables. The
harness forced file loading, omitted only BF-129's extra rejection, and used
fixture adapters for file lookup, string storage, errors and redraws. It did
not validate original allocation layout, cached searches, CRLF translation or
all decimal-overflow inputs. All 100 advance codes were also checked against
the independent reconstruction's original-data initializers. Documentation
links, anchors and the specification IDs were checked separately.

The [unit-case inventory](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/tests/unit/cases.tsv) includes rule-unit loading,
integer parsing, scenario selection and double movement. Its existence is
coverage evidence, not a claim that the original executable was rerun for this
document. No new full original-program replay, audio/network integration run
or malformed-file memory-conformance claim is made here. End-to-end setup,
saved-state restoration, cyclic research traversal, title-index overruns,
stale section caches and resource-failure paths remain dependent on their
own fixtures and owning specifications. These checks do not establish full independent conformance.

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

### Original music-option native services

and music first. Disabling music calls the CD-stop service. Enabling it from
off calls MCI stop, close, open and layout
detection in that order; enabling it calls music-option synchronization even
if it was already on.

### Original advisory herald memory check

council, wonder movies. Clear the six settings before rebuilding them. If
enabling heralds from off and the memory service with argument 8,388,608 fails,
display the low-memory notice **and still enable**
heralds.

### Original startup animation memory query

animated heralds. Clear animated heralds if the available-memory service with
argument 10,000,000 reports failure. All eleven message options start enabled.
