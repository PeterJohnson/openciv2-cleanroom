# P04: Text and dialogs

**Required profile:** [X01 fresh-game isolation and bug repairs](X01-classic-compatibility-contract.md#fresh-game-isolation-and-supported-bug-repairs)
and [behaviorally significant ordering](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
override conflicting procedures and examples below. Retention applies within a
game or to explicit imported inputs, not across new games. Confirmed original-bug
stack/backing/stop cases are optional diagnostics; use documented safe repairs.
Only real effects and callbacks impose ordering; test adapters cannot invent them.
Unspecified safe continuations remain explicit gaps, not required arbitrary bytes.

This specification describes classic Civilization II Multiplayer Gold Edition
5.4.0f, patch 3 label loading, text expansion and formatting, fonts, wrapping,
dialog construction, controls, selection and modal results. A replacement may
use different string, widget and window representations and presentation.
Preserve authored content, semantic information, decisions and required effects;
original geometry and native control traces are reference behavior.

[F03](F03-rules-and-configuration.md#2-finding-sections-and-consuming-text)
owns named-section lookup and physical text-line reading. [F01](F01-determinism-and-arithmetic.md)
owns arithmetic; [P03](P03-graphics-and-asset-processing.md) owns palettes,
sprites and surface primitives. [P05](P05-input-and-command-dispatch.md) owns general message delivery, menus,
keyboard translation, focus traversal and default-button dispatch; this document
owns what dialog controls do with those events. Population quantities belong to
[V02](V02-governments-and-tax-rates.md#population-queries), and calendar progression to
[F04](F04-turn-sequencing.md). Their display formatting is specified here.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

## Required contract and reading guide

Scenario/resource text and substitutions, information values/units, offered
semantic choices, input domains, target identity and accepted/cancelled effects;
numeric protocol results mapped from named choices where required.

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

## Native text and classic resource compatibility

Standalone catalogs use [native JSON text and interaction records](../content/data-format.md#6-text-and-interaction-catalogs).
They preserve information, parameters, semantic choices and caller effects while
using new prose/layouts. Native inputs/defaults and named help/fact/advice
content also cover editor/help/tutorial resources; classic source addresses
belong to the adapter/compatibility descriptor, not the native text catalog. The
[per-file inventory](../content/text-resources.md) records those consumers. Classic
resource parsing, indexed substitutions and fallback below remain the compatibility-adapter contract for user-selected text;
they do not require native catalogs to reproduce classic control markup. Preserve
scenario-authored content and do not translate unsupported resource semantics
into a different command or a stock fallback silently.

## 1. Inputs, state and compatibility

Nominal inputs are complete classic text resources, valid label/parameter indices,
safe text storage and identified prompt operations. Preserve label/string pools,
six substitution numbers, ordinary string parameters, profile-height string,
shared text, authored titles/options/edits, default and current selection,
checkbox values, primary/secondary results, active prompt and required idle/time
state. Native fonts, rectangles, widget lists/stacks and focus are reference data.

Observe parsed/substituted text, selected values, edits/checkbox publication,
accepted/cancelled results and required caller/network effects. Dialogs are not
transactions: Cancel does not generally undo edit-buffer or checkbox publication.
Classic character classification/case rules remain relevant to authored content;
new stock chrome and fonts follow X01.

Unless stated otherwise, scalar expressions use F01's reference results; signed division truncates toward zero. `SAR(value,n)` means
arithmetic right shift, including its different rounding for negative values.
Logical palette indices lie in 0…255; source-asset/required color conversions
use the low-byte mapping for admitted extra codes. A replacement display's
native color representation is free. A rectangle is half-open except the
explicit inclusive pane-choice test. Do not introduce clamps, sorting, whitespace normalization or
Unicode case rules that are absent below.

[X01's overwrite exclusion](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
applies to every text, arena, control, stack and temporary-buffer write. An unsafe
write is an excluded bug even if it would land in mapped adjacent state. Stop,
omit it or reject the input under a declared safe profile, retaining the valid
prefix; do not turn the victim's bytes into a logical side effect. Off-nominal
scalar wrap and bounded quirks remain specified. Unknown reads require established
backing, supplied retained input or a stop. A stricter input profile may reject
unsupported resources explicitly. Appendix B separates original capacities and
corruption evidence from the representation-independent algorithms.

## 2. Labels and shared text parameters

### Label initialization

Text startup derives the status-language position from signed defaultLanguage−1,
resetting it to0 only when greater than2. Select TXT by default, then
TXT/FRE/GER/ITA/SPA for languages1..5. Build F03's GAME section cache. Read
LABELS/STRINGHEAP; a request greater than the retained logical capacity raises it
(initial35000). Initialize the logical pool empty with `<nil>` first. F03 owns
string identity and empty/padded-name interpretation. Capacity may be modeled
without allocating the original contiguous arena.

Rebuild repeats that capacity step without resetting language/cache, replaces
the logical pool and `<nil>`, clears old labels, loads labels below ignoring its
return, then loads popup labels. Original handles, locking and numeric UI-error
codes are in [Appendix E](#appendix-e-original-implementation-reference).

Open section `LABELS` of `LABELS` through F03. Missing section reports failure.
On success read and publish declared count. Starting0, read one pooled string per
live count and publish its identifier into the current label collection. Then,
if the resulting index is below the classic label capacity in Appendix B:

1. Save search directory, substitute installation directory and try that section.
2. If found, capture fallback count; skip the live current label count's lines,
   then append strings up to the captured fallback count.
3. Restore search directory. If still short, pool `[ERROR:LABELS.TXT]` once and
   use that same identifier for the remaining labels.

Return the resulting index and close the file on ordinary completion. A
nonpositive result clears the logical count and usable label collection. Native
stale table-reference retention is reference evidence; no successful table may be
invented on failure. Original count clamping is a reconstruction repair; nominal
custom content and the declared safe admission policy remain required.

Popup-button initialization separately looks up section `POPUPS` in `LABELS`.
If found, read three pooled strings and publish them, in order, as OK, Help,
Cancel. Missing section leaves the current labels; this routine does not close
the section file. Initial labels are the literal English `OK`, `Help`, `Cancel`.

### Parameter setters and decimal parsing

A number setter replaces the chosen number; the byte-valued setter zero-extends
its low byte into the full number. A string setter copies through the terminating
NUL. A label-valued string setter reads the live label index, obtains its pooled
text and copies it. Setters do not add separators. Substitution tables start
zeroed; later callers share them rather than receive private template contexts.

The decimal parser uses the live CRT classification table, or its multibyte
classification service when that mode is active. Skip classified whitespace;
reread the first nonspace byte; consume one optional `+` or `-`; accumulate
`value = value*10 + byte-'0'` while classified as a decimal digit, wrapping on
every operation. Negate with wrap for a minus sign. Return zero when no digits
occur. Stop at the first nondigit; `0x10` therefore parses as zero. Classification
inputs are bytes here; directive scans below sign-extend their source bytes.

### Variable expansion

Expansion consumes a **writable input** and an output string. Clear the output
first. Repeatedly find the next `%`, replace it in the input with NUL, and append
the preceding nonempty fragment. If no marker remains, finish. Otherwise advance
the input cursor past `%` and test these branches in order:

| Prefix after percent | Action and cursor advance |
| --- | --- |
| STRING, six characters, case-insensitive | Parse the decimal selector after STRING; append that live string. Advance the cursor seven characters, plus one more when the **parsed selector** is at least 10. |
| NUMBER, six characters, case-sensitive | Parse the selector, append its signed decimal number, advance seven characters. |
| HEX, three characters, case-sensitive | Parse the selector; format its unsigned numeric bits as lowercase hexadecimal. Append enough leading zeroes for four characters, then the digits; advance four characters. |
| percent | Append one literal percent and advance one character. |
| anything else | Perform no expansion or further advance; the next iteration processes the text after the removed marker. |

Advancement is not based on how many digits the parser consumed. For example,
with string 0=`alpha` and number 1=−12, `%STRING00!` gives `alpha0!` and
`%NUMBER01x` gives `-121x`. `%number0` gives `number0`. The input's inserted NULs
remain. Do not expand again after growing an output buffer or snapshot every
parameter before processing: later branches read current shared values.

A HEX value wider than four digits underflows the original unsigned padding
count. Its ensuing buffer overwrite is excluded; a safe implementation may
reject that width before padding. Ordinary string selectors and the separately
backed profile selector are listed in Appendix B. Other selectors require real
read backing or a stop, not a synthesized empty string.

## 3. String and quantity formatting

Basic appenders append pooled or label text verbatim. City name uses label 14
for a negative city argument, otherwise the selected city's name. A braced pool
append emits `{`, then resolves/appends the live pool string, then `}`; resolution
therefore follows the first write. Shared-line variants target the shared line.
The literal appenders are space, `+`, `{`, `}`, `-`, `x`, `$`, `: `, `, `, `(`,
`)`, `%`, LF and `.  ` (period followed by two spaces).

Integer and long-decimal appenders emit signed decimal without grouping or
padding; minimum signed integer remains printable. Binary formatting interprets
unsigned bits, pads to at least eight binary digits and appends them. The
original private binary buffer is too small for the widest results; its overflow
is excluded rather than a reason to truncate a valid number silently.

For population display, a negative supplied population first queries V02's
civilization total. Otherwise use the supplied quantity unchanged. If it is at
least 100, append `population/100`, a comma, set population to its remainder
modulo 100, and append `0` if that remainder is below 10. Append the remaining
integer and `0,000`. Thus 100 gives `1,000,000`; supplied zero gives `00,000`.

Year formatting uses the live scenario flag and signed scenario calendar:

- With an active scenario and negative calendar, let `m=abs(year%12)`, with
  truncating remainder. Append month label `420+m` for a positive year, otherwise
  `431-m`, **to the shared line**, followed by a space there. Append
  `abs(year/12)` to the requested destination and return that destination.
- Otherwise, for a nonnegative year with the language-position flag clear,
  append label 1 and a space before `abs(year)`. With that flag set, append a
  space and label 1 after the number. A negative year always appends a space
  and label 0 afterward. `abs(INT_MIN)` wraps to INT_MIN.

Read the language flag separately before and after formatting the number; a
callback or aliased write can change it. Monthly output to another destination
still modifies the shared line. Year zero uses the nonpositive monthly branch.

Gold formatting appends signed decimal to its requested destination, then a
space and label 323 **to the shared line**, and returns the shared line. The
mixed destinations are original behavior, not examples of a generalized formatter.

The single-character category helper maps either case of M,S,T,F,A to 0,1,2,3,4;
other initial characters return −1. This mapping alone does not establish a
new game-rule meaning for those categories.

## 4. Fonts, measurement and basic drawing

Render the required text and information using any fonts, measurement, framing and
caption widgets. Keep scenario text and substitutions intact; font/device failure
follows X01. Native font metrics remain inputs only for an explicitly retained
simulation admission calculation such as P07 scoring.

### Status information and disclosure

For autoplay with turn-status enabled, a changed cached countdown takes a fresh
signed autoplay-remaining value. Otherwise a human saved-player actor with countdown
enabled refreshes on a changed status timer: cache−1 selects signed turn-limit
milliseconds/1000, any other cache selects status timer; clamp negative cache0.
The displayed minute/second value derives from cache−1 with signed truncation,
clamping a negative seconds part0. Other cases omit timer text. Unchanged cached
value retains its existing status value. This display cache is separate from
N03's actual deadline. Original padding/colon placement is in Appendix D.

Copy the title into a fresh status string. When turn-status is enabled and game
mode is nonzero, append one of these suffixes, using the live network actor and
human set after child callbacks:

- Creation/lock gate clear: append ` (`. Show the actor's civilization adjective
  when local-to-actor relation has contact, actor equals local, or mode is one
  and the actor is in the shared identity mask. Otherwise show the human actor's
  mapped network-player name, or `IA` when language mode is one, otherwise `AI`.
  An AI adjective/AI literal branch clears timer text unless autoplay is active.
  Append space, label 873, optional `: ` plus nonempty timer, then `)`.
- Gate set and actor is AI: append ` (` plus IA/AI, space, label 873, optional
  `: ` plus timer, and `)`.
- Gate set and actor is human: append ` (` plus timer and `)` only if timer is
  nonempty. Otherwise append nothing.

The saved player that controls countdown eligibility and the network actor
shown in the suffix are different inputs. Invalid player/civilization reads
need established backing or a stop; do not select a substitute player name.

The countdown value and actor disclosure gates above remain required. The
original colon placement, zero padding and cached string representation are
optional formatting; a redesigned status display may format the same value
clearly. This status computation must not change N03's actual deadline.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 5. Dialog setup and resource parsing

A logical prompt contains authored text/title, identified options, edit values,
checkbox state, offered buttons, initial/default selection and completion/result
state. Parse resource content as below; layout directives are recognized syntax
and presentation hints, not mandatory screen geometry. Keep LENGTH, DEFAULT,
choice values and option ordering wherever they determine semantic results.

### Resource state machine

Copy the section name to the shared prompt-section label, then use F03 lookup.
On lookup failure, ordinary mode still calls setup; Append mode does not. Return
1. On success, ordinary mode calls setup, while Append mode ORs the supplied
flags into existing flags. Append requires a previously initialized arena.

Capture whether optional edit text is nonnull, including an empty string, and
copy it. Start in Text state with option index zero; repeatedly read F03 lines:

- EOF ends. An empty line advances Text to Options or Options to Done, unless
  PreserveBlank was supplied, in which case append an empty text node in either
  state and retain the state.
- A non-`@` line expands once using §2. In Text state append a flow text node.
  In Options state, optional-text mode appends an edit: a zero choice/length
  becomes 5; the first field uses the copied optional text, later fields use
  the live string parameter at the current edit count. Otherwise append an
  ordinary or checkbox option with the current option index as value, select
  it when that index equals choice, then increment the index. Edit rows do not
  increment the option index.
- An `@` line is interpreted below. Recognized directives do not themselves
  advance the state. An unrecognized single-`@` line ends parsing; an
  unrecognized double-`@` line is ignored.

| Directive after @ | Recognition and effect |
| --- | --- |
| OPTIONS / PROMPT | Whole OPTIONS is case-insensitive; whole PROMPT is case-sensitive. Enter Options. |
| TEXT | Case-insensitive whole match; enter Text. |
| TITLE / BUTTON | Case-insensitive prefix; seek first `=` or end, expand following text once, replace title / append custom button. |
| WIDTH / HEIGHT / COLUMNS | Case-insensitive prefix; skip to first classified digit or end, parse decimal, call setter. Minus signs are skipped. |
| LISTBOX | Same numeric scan; parsed zero becomes 16; configure rows with current content width and offset zero. |
| LENGTH | Same scan; change choice/length only while it is zero. |
| DEFAULT | With optional text, seek `=` and copy its suffix **without expansion** only if the original optional-text argument is still empty. Otherwise the numeric scan changes choice only while zero. |
| SMALLFONT | Case-insensitive whole match; recognized no-op. |
| X / Y | Case-insensitive first character with a nonalphabetic next character; scan to digit **or minus**, parse and store the corresponding requested coordinate. |
| CHECKBOX | Compare only its first seven characters, case-insensitively; set Checkboxes and Cancel. |
| SYSTEM | Compare first six characters, case-insensitively; set SystemList. |

Return zero on normal parse completion, including EOF or a terminating unknown
directive. The caller may proceed after a failed load; individual wrappers in
§10 state which ones do. Native buffer capacities and reconstruction prevalidation
are not extra syntax rules.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 6. Flow text and option-label drawing

Present authored text, semantic option labels and complete information.
Typography, wrapping, alignment, scrolling and text drawing may differ; they must
not change option identity, hide a required decision or alter parsed resource
semantics.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 7. Layout, window creation and painting

Construct and display the interaction with the information, choices and result
behavior in §§5, 9–10. Layout, button geometry, placement, native window creation
and painting are optional reference details.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 8. Control construction and shared widget behavior

Provide enabled semantic options, editable values and accepted actions. Widget
construction, native control messages, focus/capture and pressed-state painting
may differ. Preserve choice/result identity, edits and publication described in
§§9–10.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 9. Selection, mouse and keyboard algorithms

User input selects identified enabled options or explicitly offered custom
actions. New key/mouse bindings need not reproduce native focus/capture paths.
Preserve primary/secondary results, live callback effects and the action semantics
below. Convert new control identities to the declared option/button values;
action completion must not manufacture an accepted result after cancellation.

### Prompt actions and results

All following callbacks use the **live active prompt** after children unless
explicitly captured. Completing a prompt sets Complete, then reloads the active
prompt/window and clears its modal-loop flag; there is no initial null guard.

| Action | Effects before completion |
| --- | --- |
| Ordinary radio action(index) | Traverse all nodes, record matching ordinal; set primary result to −1 or that node's value. Set Activated, complete. |
| System-list action | If active and not Complete, set Activated and complete. Selection text is resolved on modal return. |
| Edit/radio change | Set shared activity timer to a fresh clock sample. |
| Sprite primary(controlID) | Walk signed controlID−300 steps from sprite head; a negative index selects the first. Set primary result=node value. Update selected sprite. If custom callback exists, call it(value), always prepare the live prompt, then Activated/complete if its captured result is true. Without callback, prepare only when selection changed and highlighting is enabled. |
| Sprite secondary | Call custom secondary callback(0) if present; true sets Activated/completes, false prepares. |

Button action first resets the timer. Index is controlID−100. A custom index
publishes secondary result=index−standardCount+1 and acts as OK; otherwise capture
the standard kind. Help sets HelpRequested but continues the same result/completion
path. For OK with sprite highlighting and a selected sprite, locate it and invoke
its primary action through 300+ordinal. Preserve captured button kind across that
callback. Cancel sets primary result −1. Other kinds set primary result zero when
sprite count≤1 or options exist, then ordinary radio options may replace it with
the selected control ordinal's value. Checkbox/list values wait for modal output.
Complete the live prompt in every ordinary button branch.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 10. Modal completion, wrappers and cleanup

While awaiting a decision, enforce the caller's gameplay/network gates and run
its required idle/timeout/network work. Completion, cancellation and cleanup must
resolve the pending interaction and any required protocol reply. The following
publication/wrapper rules remain required; native modal stacks, window enabling
and destruction order are reference implementation details.

### Prompt modal operation

1. If prompt mode equals 16, return the current primary result immediately.
   Otherwise obtain a clock sample. Set activity timer to ticks when timeout
   is zero, else `ticks+timeout*60−1200`, with wrap. Native restore is optional.
2. Clear Activated. BackgroundSuppressed also sets NoTitle. Prepare the dialog;
   if it is not Prepared or Immediate is set, return its current primary result.
3. With a predicate callback, while this receiver is not Complete call its live
   predicate(this). A truthy result completes the **active** prompt, which may
   differ from this receiver. Without a predicate, enter its generic modal
   window, then pump one message-or-idle repeatedly until this receiver is Complete.
4. If an output string and first edit control exist, copy that edit to output.
   If live edit count is greater than one, copy every edit into the corresponding
   shared string-parameter slot, starting at zero. This happens on Cancel too.
5. With List and primary result zero, custom lists replace the result with the
   selected node value. System lists fetch selected text, initially set result
   to the head option's value, then iterate **all option nodes** in parallel with
   native item indices starting at zero. Every equal text replaces result, so
   the last duplicate wins. The loop is not group-filtered. Failed getters
   retain prior scratch bytes; first-use unknown bytes require supplied input.
6. Only when List is clear and Checkboxes is set, clear each node's checked flag,
   read its control value, and set checked when truthy. Cancellation does not
   skip this pass. Reload each next link after mutations.
7. Unless Keep, retire the interaction and discard stale queued input. Service
   relevant external events, then return the **live primary result**, not the
   value captured at completion. Exact native pump/paint order is in Appendix D.

Network-cancel idle first polls `(1,0)`, then if received message 46 or 50 counts
are nonzero sets the manager's current prompt Complete, reloads its prompt/window
and clears that modal loop. Drag cancellation obtains a clock sample when a drag
is active and compares signed now against the wrapped drag-start+124 deadline; it
does not recheck active after the clock call. These callbacks stop presentation;
network/drag owners retain their own state-transition rules.

### Public convenience wrappers

- Choice wrapper constructs an ordinary arena dialog and loads the resource.
  On success, checkbox mode initializes marks from the shared option mask using
  index modulo 32, runs modal, publishes the dialog's **secondary** result to
  the shared dialog-result field, and rebuilds the shared mask from checkbox
  marks. Failed load returns the incoming choice. Destroy after capturing the
  return, so cleanup callbacks cannot replace that captured value.
- Text wrapper clears caller output first, loads with Cancel enabled and the
  provided initial text/limit, and runs modal on success. Afterward replace
  **leading** spaces and tabs in output with underscores, including on Cancel.
  Failed load leaves empty output and result zero. Destroy and return the
  captured modal result.
- Integer wrapper formats the supplied integer as initial text, uses text limit
  five, then parses and writes output **even when the modal result is Cancel**.
  Return the captured result, separately from the changed integer.
- Sprite-message wrapper constructs, loads, appends the supplied sprite
  **unconditionally**, sets sprite scale, runs modal, publishes secondary result,
  destroys and returns captured primary result. Failed load does not skip the
  sprite append; a null sprite is not automatically an empty message.
- Game-message wrappers supply file `GAME`. Advance illustrations select the
  advance's signed category/epoch sprite; unit illustrations select their supplied
  unit-sprite index. Their callers own eligibility and index validity.

Shared option-mask clear sets zero; set/query uses a shift count masked modulo
32. A replacement may represent choices differently internally, but imported
mask behavior and aliasing indices must agree. Ordinary option flag setters
find the first matching value and set/clear disabled, auxiliary or checked state;
whole-list clear traverses next after each flag mutation.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 11. Packed-label legacy dialog

Legacy packed-label controls need not be recreated. If a caller uses this service,
decode its offered labels/choices and return the corresponding semantic result
using the normal acceptance/cancellation contract. Native packed buffers and
widget construction are reference details.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## 12. Conformance cases and integration boundaries

Validate authored resource parsing/substitutions, values and units, option and
result identity, timeout/remote completion, cancelled edit/checkbox publication,
and wrapper return-versus-output distinctions. Test nominal customized labels,
safe malformed-input rejection and harmless UI failure cleanup. Original font,
layout, mouse/key and native allocation traces are optional reference tests.

Original presentation procedures: [Appendix D](#appendix-d-original-presentation-reference).

## Appendix A. Evidence identity and source navigation

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

Target executable SHA-256:
`edd3cead9eb76eeb49cb29a23029e7717807b626a1c88addf4c3432a21120c5c`.
Primary reconstruction revision is
`a71e8aefffe04e6f719e0e213b027b128a747711`:

- [text.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/text.cpp)
  supplies labels, substitutions, string helpers and formatting callers.
- [controls.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/controls.cpp)
  supplies native widget construction and control behavior.
- [window.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/window.cpp)
  supplies resource-dialog construction, layout and modal operation.
- [mapview.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/mapview.cpp)
  supplies shared caption and status-frame consumers.
- [dialog classes](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2classes.h)
  and [node types](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/civ2types.h)
  provide the semantic field mapping used to cross-check the independent layout.

Independent reconstruction: `civ2`, revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`. Relevant source modules are:

| Area | Files under src/ |
| --- | --- |
| Text resources and formatting | dialog_substitution.cpp, dialog_parameters.cpp, label_strings.cpp, string_pool.cpp, text_helpers.cpp, text_leaf_algorithms.cpp, text_extra_width.cpp, window_startup.cpp |
| Font, text and caption services | text.cpp, font.cpp, custom_font.cpp, map_window.cpp, main_status.cpp |
| Resource dialogs | prompt_dialog.cpp, prompt_layout.cpp, prompt_controls.cpp, prompt_window.cpp, prompt_cleanup.cpp, prompt_initial_selection.cpp, prompt_list_access.cpp, prompt_cancellation.cpp, network_client_discovery.cpp |
| Controls | control_button.cpp, control_option.cpp, control_option_array.cpp, control_edit.cpp, control_combo.cpp, control_system_list.cpp, control_text.cpp, ui_list_methods.cpp |
| Modal and legacy helper | window_modal.cpp, packed_control_dialog.cpp |

Original discriminator locations include substitution `00426ffc`, decimal
parsing `005f3120`, year formatting `00485210`, option metric `0059e376`, resource
loader `005a632a`, layout `0059fd2a`, control setup `005a211c`, modal output
`005a5f34`, generic window loop `005c61ab`, array-list label processing `005d9198`,
packed-label measurement `005e0148`, and edit dispatch `005d2a36`. These are
evidence locators, not required implementation addresses. Selected instruction
bytes are checked in the research bundle; source coverage and runtime coverage
are separate claims.

## Appendix B. Encodings, capacities and excluded defects

### Native representation adapters

These details support validation against original storage. Internal replacement
objects need not match them.

The original validation masks are Center1,OuterExtent2,SharedMargins4,Modal8;
native base flags are514 (standard) and2050 (modal), plus1024 for caption and4096
for resize. They encode the described options, not a required new object layout.
Original entry `005534bc`; Appendix A records the external provenance for
intro-style creation. This caller contract was source-reviewed during P07;
P04's earlier finite fixtures do not exercise complete native creation or these
callbacks.

| Logical flag | Original value |
| --- | --- |
| Cancel | 0x00000001 |
| Checkboxes | 0x00000004 |
| Immediate | 0x00000008 |
| ExternalWindow | 0x00000010 |
| Prepared | 0x00000020 |
| Help | 0x00000040 |
| HelpRequested | 0x00000080 |
| VariantText | 0x00000100 |
| ControlsReady | 0x00000200 |
| Complete | 0x00000400 |
| Keep | 0x00000800 |
| List | 0x00001000 |
| Activated | 0x00002000 |
| BackgroundSuppressed | 0x00004000 |
| Append | 0x00008000 |
| NoTitle | 0x00010000 |
| SpriteHighlight | 0x00020000 |
| SystemList | 0x00040000 |
| NoScale | 0x00080000 |
| FixedLayout | 0x00100000 |
| CenterFlow | 0x00200000 |
| TwoPanes | 0x00400000 |
| Sorted | 0x00800000 |
| ForceScroll | 0x01000000 |
| NoOK | 0x02000000 |
| PreserveBlank | 0x04000000 |

Node option flags are disabled=1, auxiliary=2, checked=4. Text forced=2,
centered forced=1. Standard button kinds OK/Help/Cancel are 0/1/2. Translated
prompt key pairs are Up A8/C0, Down A2/C1, Left A4/C2, Right A6/C3, Home A7/C4,
PageUp A9/C5, PageDown A3/C6, End A1/C7, with Enter D1 (hexadecimal). Point
sentinels above/below/left/right are −1/−2/−3/−4.

Font descriptor hints: charset 1, output precision 4, clipping precision 1,
quality 0, family/pitch 16 for Times,32 Arial,0 System,48 Courier. Bold is style
bit 1; italic/underline/strikeout fields receive raw masks 2/4/8. Wrapper ownership
is a DWORD exactly equal to one for owned fonts. A logical-font wrapper created
from an external descriptor starts with allocator-zero ownership; its separate
destroy routine always deletes a nonnull native font. Collection capacity is 64.

Point text anchor masks apply in this order after the initial negative half
shift: 1 adds half width, 4 adds half height, 8 subtracts half height, 2 subtracts
half width. These halves truncate toward zero. Port shadow flag is 16. Native
paragraph flags are WORDBREAK plus CENTER for input 0 or RIGHT for input 2.
Rectangle-string input 0 maps to CENTER|VCENTER|SINGLELINE; other inputs add
BOTTOM for bit 8 and RIGHT for bit 2. Native platform measurement/drawing flags
are separate from the logical prompt styles above.

Native EDIT starts CHILD|BORDER, adds VISIBLE when requested; edit feature masks
map 1→AUTOHSCROLL, 2/0x200→MULTILINE, 4→PASSWORD|AUTOHSCROLL, 8→UPPERCASE,
0x10→LOWERCASE, 0x20→VSCROLL|AUTOVSCROLL, 0x40→HSCROLL|AUTOHSCROLL,
0x80→WANTRETURN, 0x100→READONLY. The 0x200 variant also sets its separate internal
multiline marker. Default prompt edit uses feature 1. System lists are unsorted,
notifying native lists; comboboxes are dropdown lists with string items. Checkbox
bitmap resource IDs are disabled/unchecked/checked=306/305/304;
radio equivalents are 301/303/302. Native glyph and system-widget behavior remains
a declared service input.

### Original storage limits and safe treatment

- Label table has 888 identifiers. Substitution number table has six signed
  DWORDs. Ordinary strings are eleven 260-byte slots. The scenario text producer
  has a separate wider backing arrangement, but STRING11 addresses only the
  separately established eight-byte game-profile height field; it is not an
  ordinary extra 260-byte slot. Other reads need established backing.
- Text/option/edit/sprite nodes occupy 32/24/28/20 bytes in the evidence adapter.
  The dialog arena size and individual append requests narrow to unsigned 16
  bits. Ordinary convenience wrappers request 16384 bytes. Shrinking an
  allocation by wrap never authorizes the subsequent full string overwrite.
- Caption publication clears 132 bytes and copies at most 131; the composed
  status temporary holds 256. Caption identity mask is network-manager offset
  0x160a1; creation/lock gate is 0x57c. Autoplay enable/remaining are signed words
  at multiplayer offsets 0x468/0x46a. These are input mappings, not new mechanics.
- The original prompt section-label, optional/default-text and expanded-text
  temporaries include smaller capacities than modernized dynamic replacements.
  Resource optional/default text originally holds 80 bytes and expanded text
  256. Flow candidates hold 80 bytes, pending lines 256, edit strings
  256, native system-list output temporaries 132 each, and button/accelerator
  temporary strings 256. Packed-label input copy holds 1024; its measured
  fragment holds 128. The binary formatter's temporary holds 20.
- Plain radio setup supports 40 options. Originally, an excess count with screen
  mode clear reports −14 and continues; with screen mode set it stores count 40.
  The linked label-copy loop still follows every node, so neither branch proves
  the private 41-pointer scratch is sufficient. Reject/stop before its overrun.
  A shorter list than later consumed count leaves unproduced pointers; supply
  them or stop, never fill them with zero labels.
- There are six custom button labels and three standard kinds. Prompt stack
  contains the initial slot plus sixteen pushed prompts. The independent raw
  adapter maps a following word to prompt mode; an excess stack write to that
  word is still an **excluded overwrite**, not a valid mode mutation.
  The separate modal-window stack likewise cannot spill into neighboring state.
- Excess label counts, overlong native outputs, arena exhaustion, negative edit
  copy lengths, array-count/product wrap, custom-font overflow and all other
  out-of-bounds stores are excluded bugs. Reconstruction guards/dynamic strings
  may safely handle them without asserting original successful behavior.

The array-list accelerator routine accidentally reuses its **outer record
index** as the character shift index. At a tilde it replaces that index with the
record's accelerator index; each shift then uses `records[index].text[index]`
and the next character in that same newly selected record. It stops when that
record's strlen is no greater than index, and performs the final accelerator
check on `records[index]`; outer iteration resumes from the modified index.
Retain this scalar/traversal defect only while every selected record/read/write
is actually within its established logical object. Stop before an unbacked read
or any record/text overrun. Do not emulate an overrun as mutations of adjacent
records. The packed-list and ordinary button label shifters use separate indices
and do not share this defect. A safe profile may reject array-list tilde labels
at ingress or declare a corrected accelerator algorithm.

Other reconstruction differences include count clamps and loaded-selector bounds,
dynamic prompt text, prevalidation of system-list labels, failed-paint early
returns, fixed edit-message dispatch, full-size packed input allocations, and
negative-y placement abort after a failed geometry query. The original read
boundaries above distinguish supplied incoming state from those repairs. No
uninitialized-value replacement or overwrite is silently required for conformance.

## Appendix C. Validation record

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

The historical research bundle included extracted source bodies, adapters,
source hashes, original-byte probes and documentation checks. That scaffolding
is not included in this repository or required for implementation.

The harness compiles **28 unchanged whole bodies including helpers** from six
modules and passes **52 assertions** in optimized and ASan/UBSan builds. Leak
checking was disabled for the sanitizer run because the execution environment's
ptrace prevents LeakSanitizer operation. The address/undefined-behavior checks
completed normally. Static low-address tokens, ASCII CRT classification, supplied
label pool, one population-query adapter, font metrics, draw logs, scroll services
and division helpers are explicit fixture inputs; it is a native 64-bit harness,
not the original native 32-bit executable or a live GUI.

Cases cover whitespace/sign/decimal wrap, token case and fixed advancement,
destructive markers, profile-string backing and safe stops, population/year/gold
formatting, shared destinations, flow wrapping and node continuity, forced and
absolute lines, shadow width, pane lookup/border priority, viewport/scroll/callback
selection, disabled custom rows, packed labels and accelerator mutation. Dynamic
flow storage stays within original temporary capacities in these fixtures.

These tests do not execute the resource-loader parent, full geometry calculation,
native font creation/rasterization, platform controls, label-file fallback,
complete modal operation, shared status rendering or integrated cancellation.
Those descriptions rely on pinned source and selected original instruction checks.
Complete classic GUI conformance remains a distinct validation task.

Source checks confirm 40 pinned independent files, all 28 fixture bodies, six
primary files, eleven original instruction probes and 56 status-literal bytes.
Documentation checks validate links/anchors and specification IDs.

P06’s source review adds the shared frame-preparation/caption-control service
in §4, using `city_layout.cpp` and `map_window.cpp` at the independent revision
above and original `00552ed2/00553169/005532d7`. This addition is not exercised
by the 28-body harness. P06 records its own bounded checks and remaining native
control/layout joins.

## Appendix D. Original presentation reference

This appendix retains the original presentation procedures removed from the main
specification. Geometry, pixel/sample output, native controls and cosmetic timing
are optional reference behavior under X01. Required semantic fragments were kept
in the numbered main sections and are linked where omitted here; this appendix
does not relax their simulation, information, file or protocol obligations.
Original section numbers and local § references identify the recovered procedure
family, whether its retained fragment is in the main section or here. Historical
native/pixel tests are reference validation, not baseline acceptance criteria.

### Original §4. Fonts, measurement and basic drawing

Font creation accepts family, size and style. Nominal families are Times New
Roman, Arial, System and Courier. Height is the wrapped negative of size; width,
escapement and orientation are zero. Bold chooses weight 700 instead of 400;
italic, underline and strikeout select their corresponding fields. Native
character-set, precision and family hints are recorded in Appendix B. Unknown
families require the incoming face-name bytes; they do not default to Arial.

Create the native font, then allocate its wrapper, publish the font and mark it
owned. A failed native creation returns no handle. Do not invent cleanup of a
successfully created font when a later allocation or lock fails. Destruction
locks the wrapper, returns if that fails, deletes the native font only when its
ownership marker equals one, then unlocks/frees. It does not clear the receiver's
retained handle or cached height. Replacement destroys an existing handle before
creating the replacement; publish its handle before querying its height.

Height measurement first queries the wrapper's size; zero returns 1. Otherwise
lock, obtain a screen device context, read/select the font, query metrics,
restore the prior object, release the context, unlock, and **then read height**.
Line-height measurement omits the size guard and returns wrapped height plus
external leading after the same cleanup. Width measurement locks/selects the
font, computes the live string length and requests its extent; capture horizontal
extent **before** restoring/releasing/unlocking. Failed queries require supplied
incoming output at those reads, not a default zero. Cached font height and live
line height are distinct quantities.

Defaults are body Times New Roman 16 bold, title Times New Roman 16 regular,
list Arial 14 regular, and button/edit Times New Roman 16 regular. Resetting
prompt font defaults restores pointers to these objects without recreating them.
The general graphics default is System 14. Caller replacements remain inputs.

Custom-font collection import installs a resource, stores its filename and
three-character prefix, and enumerates native font families. Before comparison,
lowercase the stored prefix and provider face name **in place**. A matching
three-character prefix creates and appends a font at the live count, then
increments that count even if creation returns null. No match is silently
invented on failed enumeration. Borrowing a collection font checks only signed
`index<count`, creates a nonowned wrapper, and leaves the collection locked.
A negative index needs established backing or a stop. Collection destruction
walks the live count, deletes nonnull entries and clears each after deletion,
removes the resource, then unlocks/frees. Resource installation/removal broadcasts
font change only on the corresponding success branch. Capacity spills are bugs.

#### Drawing services

Selecting a text color uses the low byte and returns the previous byte. Selecting
extra width normalizes it to Boolean. Styled point text draws a shadow when its
color is nonnegative, at `(x+shadowX+extraWidth,y+shadowY)`, then foreground when
its color is nonnegative; extra width adds a foreground copy at x+1 before the
ordinary x copy. Return x plus the **post-draw live** font measurement.

Centered text measures first, adds absolute horizontal shadow offset when the
shadow is enabled, and adds extra width. Its start is
`x+SAR(availableWidth,1)-SAR(measuredWidth,1)`; return the captured measured width.
Right-aligned text starts at `x+availableWidth-measuredWidth` and returns that
start. These wrappers use the selected style; they do not trim text.

The graphics-port point primitive may itself apply a shadow: offset both axes by
`trunc(fontHeight/8)`, select shadow color, draw, restore the saved color, draw
main text. It reloads the font/backend/clip after callbacks. A missing initial
font reports and returns; a later replacement is not protected by a second guard.

The native point backend locks/selects the font and uses the indexed surface's
current color converted through its palette. It computes bottom from y plus
font height, then right from x plus font width. Start with the rectangle shifted
by negative **truncating halves** of its width/height; apply horizontal/vertical
anchor flags in their original order (Appendix B). Set update-current-position
alignment, move to the resulting origin, intersect the clip while ignoring the
intersection Boolean, then query the current text length and draw without
native layout flags. Restore alignment/font and unlock; retain text color.

Native paragraph text enables word breaking, centers for alignment value zero,
right-aligns for value two, otherwise left-aligns. Native rectangle-string text
uses centered, vertically centered single-line text for zero; other values
independently request bottom and right alignment. Its port wrapper can shadow
by the font-height offset; paragraph text has no corresponding port shadow.
Failed palette/geometry/metric queries need defined outputs before consumption.

#### Shared window captions and status frames

This service supplies P01/P02 map captions and compatible auxiliary/intro frames.
Title publication first clears its title storage, then copies a bounded title;
source aliasing that storage therefore observes the clear. Draw status, copy
all full-frame edges, replace bottom with the **post-draw live** client top, and
present that caption rectangle. Appendix B records the copy bound.

Display-metric initialization queries screen height then width and publishes
both. Start application font size 16 and unit-popup scale 0; screen width at least
1000 chooses size 24 and scale 1. Replace normal Times font at that size, small
Times font at truncated wrapped `2*size/3`, then title Times font at the live size.
Application row height is cached normal height plus twice icon gap plus twice
frame border. Spacing is twice icon gap plus twice border. Query native frame
width then height and double each. Initial frame light/dark colors are 37/18,
border 2, icon gap 2 and spacing 8; callers can replace them.

Status rendering resets clip, captures both frame colors and border, then tiles
top, bottom, left and right strips, reloading the frame texture each time. For
full dimensions W,H and general margin M, top margin T is a nonzero caption
margin or M. Top strip is `(0,border,W,T−2*border)`; bottom strip starts at
`H−(border+M−2*border)` with height M−2*border; side strips have width M−2*border,
at x=border and x=W−(border+M−2*border), spanning H. Texture origins match each
strip origin. Read live margins/dimensions at each phase; top/bottom share the
first captured strip width and left/right share the side height.

Copy full bounds, draw border rings with captured colors, insetting one each;
inset horizontally by M−2*border, move top by live T−2*border and bottom inward
by live M−2*border, then draw reversed inner rings. Starting x at shared spacing,
walk the live signed caption-icon count. Scale each stored width and height by
captionMargin/24 with wrapped multiplication and signed division, center y by
arithmetic halves, select that same sprite scale, draw the live sprite, reset
scale, and advance x by captured scaled width plus live icon gap.

[Status values and disclosure remain in the main text.](#status-information-and-disclosure)

Capture full available width. Measure with the normal caption font and add 4;
center with arithmetic halves, then raise x to at least the icon cursor. Reload
the normal font after this measurement. If x+measured width exceeds available,
choose the small caption font, measure and center again, clamp x to the icon
cursor, and add `SAR(normalCachedHeight−smallCachedHeight,1)` to y. Only in this
fallback branch, shorten the status string's last character until its live width
plus x+4 fits; do not recenter after shortening. Initial y is captured border+1.
Draw color 10 at x+2,y+1, select color 26 and draw x+1,y then x,y with the selected
font. Restore clip to the live client rectangle. Composition overflow is an
excluded bug; font-query output and retained timer text must be supplied.

#### Shared frame preparation and caption controls

Map, city, advisor and compatible intro windows use this common preparation
before layout/redraw. Capture surface width then height and form full bounds at
(0,0). Inner height is height−(caption margin+side margin); inner width is
width−2*side margin. Form inner bounds at(0,0), then translate using separately
reread side and caption margins through P03's engine offset helper. Publish
client left, top, width and height from the resulting rectangle in that order.

Release all six caption-control slots, testing/reloading each reference before
destruction/deallocation and clearing it after callbacks. Read shared spacing
only after the release pass. Iterate against the live signed icon count. Scale
stored icon width and height separately by wrapped multiplication with live
caption margin, divided by24; y=arithmetic half(caption margin)−half(height).
Form its rectangle at the current x. Allocate/construct a sprite control,
publish its reference before configuration, configure with ID=index+1000, then
reread the reference and install the caption-action dispatcher. Advance x by
captured scaled width plus live icon gap. Allocation failure retains preceding
publication and stops before an invalid receiver write. Six is the logical
capacity; an excessive count does not authorize overwriting adjacent state.

Caption-icon registration refuses count≥6. Otherwise capture index and
increment count before storing sprite and command. Query sprite width, store it,
then query/store height, then clear that slot's control reference. Negative count
or an excessive slot write is excluded corruption. P01/P06 supply commands;
these generic sprite controls are distinct from P06's specialized city cycling
buttons. Their paint/hit behavior remains with the ordinary P04 control owners.

#### Shared intro-style surface creation

Reports, tax-rate/history surfaces and compatible editor windows call this
creator with title, position, extent, caption/side margins, resize border and
logical options Center,OuterExtent,SharedMargins,Modal. Copy supplied title, or
empty text for null. Assign application title/normal/small fonts; clear countdown
and turn-status enable. Publish options and incoming margins/border, then clear
the private activity field and caption-icon count. SharedMargins replaces caption
with application row height and side margin with application spacing.

Select a parented standard or modal native surface using main window and
initializer palette. Include caption capability for nonzero caption margin and
resize capability for nonzero border. Unless OuterExtent, add2*sideMargin to
width and captionMargin+sideMargin to height. Center, when set, replaces both
incoming coordinates with arithmetic half(screenDimension)−half(outerDimension).
Create the surface. After creation callbacks, reread caption and border; apply
each native setting only if its current value is nonzero. Run the shared frame
preparation above, then install begin/end window-drag handlers. Do not restore
fields overwritten by callbacks or treat a failed title copy as successful
creation. A title crossing its logical destination is excluded corruption.

### Original §6. Flow text and option-label drawing

A dialog reports that it has flow text when any text node has x<0. The actual
measurement routine treats x>0 as absolute and **x=0 as flow**; retain this
mismatch when deciding whether layout calls measurement.

At measurement entry capture shadow-vs-body color difference as extra width
0/1, content width, first node, initial text y and one live body line height.
Maintain an initially empty pending line, pending measured width zero and total
height zero. Process linked nodes in order, reading the next link after the body:

1. Positive x: if drawing, draw the node at its stored x/y with its flags. Do not
   consume flow height; continue to the next node.
2. Forced or centered forced line: flush a nonempty pending line at x=0,
   consuming one height. For drawing, center only the centered variant, using
   the node's nonzero center or `SAR(contentWidth,1)`, minus
   `SAR(extraWidth+measuredTextWidth,1)`. Use current y when stored y is −1,
   otherwise stored y. Scan the text through NUL even in measure-only mode.
   Consume one height only when stored y is negative. No ordinary words remain
   to process in this node.
3. Ordinary text: while text remains, skip ASCII spaces only; find the next
   space and temporarily replace it with NUL. Capture the word length. Form a
   candidate consisting of a leading space if the pending line is nonempty,
   followed by this word. Measure the candidate with the live body font.
4. If `extraWidth+candidateWidth+pendingWidth` exceeds captured content width,
   flush the pending line **even when it is empty**, consuming one height.
   Center this flush only when CenterFlow is live, measuring its text again.
   Remove candidate leading spaces by forward overlapping shifts. Clear the
   pending line and width. Do not remeasure the stripped candidate.
5. Append the candidate, add its **pre-strip measured width** to pending width,
   restore the source space, and advance the source by the captured word length.
   Adjacent ordinary nodes continue the same pending line.
6. At the end flush a nonempty pending line, optionally centered under CenterFlow,
   and add one height. Return total height with wrap.

Measure-only and drawing runs execute the same wrapping decisions except for
extra measurements/callbacks in drawing. With unit-width characters, width 5 and
`aaa bb cc`, lines are `aaa`, `bb`, `cc`: the removed leading space still counts
in the second line's accumulated width. A first word wider than the available
width produces an empty first line. Tabs remain part of words.

Body drawing permanently replaces underscores with spaces. If body shadow color
differs from body color, draw at (x+1,y+1), then draw main text at (x,y). Main
color is emphasis when the node requests it, otherwise variant color when the
live variant flag is set, otherwise body color. Reload colors/variant/destination
after shadow drawing; retain the font argument. Return x plus the post-draw
measurement. Flow drawing adds the current text-origin x.

Option text is destructively shortened one final character at a time until its
measured width is no greater than live pane cell width minus 6. Compute strlen
**after** each measurement callback. If shadow differs from foreground, draw
shadow at (x+2,y+1), foreground at (x+1,y), then foreground or emphasis at (x,y).
Selected and ordinary colors come from their respective sets.

For a label with `|`, temporarily terminate at its first delimiter, draw the
left side at x plus pane indentation, then draw the suffix right-aligned to
`cellWidth+x-4`. Capture that right edge before measuring the suffix. Restore
the delimiter afterward; any truncation on either side remains. A node marked
disabled uses emphasis in a custom list; this drawing flag does not prevent that
list from selecting it.

### Original §7. Layout, window creation and painting

All maximum comparisons in this section are signed. Read flags again at later
phase gates: text/font/window callbacks can change them. FixedLayout preserves
caller geometry only in the branches explicitly skipped below.

#### Body geometry

1. Unless FixedLayout, raise content width for each forced text line's measured
   width plus shadow extra. If a nonempty title and title font exist, raise
   content width to title width plus 4. Traverse the sprite links. When both
   edits and options exist, clear edit count and edit head.
2. Without NoTitle, title height is cached title-font height plus twice bevel
   width plus twice vertical padding. With NoTitle, set outer border, bevel
   width and title height to zero. Initialize chrome height to title height
   and computed position to requested position.
3. Let left=outerBorder+horizontalPadding and top=titleHeight+verticalPadding.
   Unless FixedLayout, set window width=2*left, height=2*verticalPadding+titleHeight;
   all four content origins to (left,top); content width to the maximum of its
   current value, option width and edit width; copy it to option/edit width.
4. Rebuild standard buttons in the order Help when requested, OK unless
   Immediate or NoOK, Cancel when requested. If the sum of edit, option, sprite,
   standard-button and text counts is zero, add client origin to the text
   origin when a window exists and return 1. Custom buttons are absent from
   this emptiness sum. No options with List or Checkboxes also returns 1.
5. With sprites, scale maximum sprite width using P01's zoom arithmetic. If the
   **first** sprite has no label, add that width to body/option/edit x and window
   width; otherwise raise content width to it. Sum individually scaled sprite
   heights, adding section gap between nodes. Capture scale before each sprite
   height query.
6. With text and `hasFlowText`, measure §6; add text height plus section gap to
   option/edit y. Ordinary option height is
   `trunc((columns-1+optionCount)/max(columns,1))*optionMetric`.
7. List mode replaces option height with pane pixel height and computes the
   geometry below. Edit height, when an edit head remains, is
   `(bodyLineHeight()+10)*liveEditCount`.
8. Combined height is edit+option+text height, plus section gap when text and
   the sum of edit/option heights are both nonzero. Unless FixedLayout, add
   sprite gap to body/option/edit x when both sprite and combined heights are
   nonzero; add max(spriteHeight,combinedHeight) to window height and content
   width to window width.

For each active custom-list pane, query vertical scrollbar width and horizontal
scrollbar height. Full bounds start at `(optionX+paneOffset,optionY)` with pane
width/pixel height. Copy them to the interior. A scrollbar is required when
`paneRows*columns<totalOptionCount` or ForceScroll. This test deliberately uses
the total across groups. With one column, place a vertical scrollbar on the
right and subtract its width from the interior. Otherwise add horizontal
scrollbar height to pane/full/interior height, place it at the new bottom, and
subtract that height from the interior bottom again. Inset interior by one.

Cell origin is interior top-left; cell width is signed interior width divided by
columns; cell height is §5's pane row height. Start viewport at the first node in
that group. While selected index is at least viewport+rows*columns, advance
viewport by one for one column, otherwise by rows. Store that group's viewport
node. Repeat for pane 1 only with TwoPanes, take the maximum pane height, and
restore current pane to zero. SystemList omits custom geometry. Zero divisors,
null selected-node reads or nonprogressing off-nominal loops are not normalized.

#### Buttons and screen placement

With no buttons add outer border to both window and chrome height. Otherwise:

1. Right edge starts at windowWidth−(outerBorder−bevelWidth). Measure **all three**
   standard labels, then custom labels, even if some standard kinds are absent.
   Whenever a measured width is at least the current maximum, measure that same
   label again and store the second result.
2. Let count=standard+custom and
   available=windowWidth−2*(outerBorder−bevelWidth)−(count−1)*horizontalPadding.
   Signed-divide available by count. Button width is max(label maximum, quotient);
   retain the signed remainder only if the chosen width equals the quotient.
3. Unless FixedLayout, add twice section gap to window height when body/sprites
   are present, then button height. If outer border is nonzero, make the same
   additions to chrome height, including a fresh button-height query.
4. Starting accumulator=count−1, lay out standard buttons in descending index,
   then custom buttons in descending index. For each, subtract width from the
   right edge to obtain x, then subtract horizontal padding. Add the retained
   remainder to the accumulator; while it is at least the **live** total count,
   decrement the right edge and subtract the live count. This determines spare
   pixel distribution, including custom buttons lying to the left of standards.

Raise window height to requested minimum height. Capture that height, query
button height again, and set button y to captured height minus live bevel width,
button height and live vertical padding.

Unless FixedLayout, obtain screen bounds and main-window outer bounds. Ignore
the native success Boolean; needed unproduced values require supplied input.
For x=−999, center using `SAR(screenWidth,1)-SAR(windowWidth,1)`. Other negative x
adds screen.right−1−windowWidth; when the main window is ready and NoTitle, also
subtract its nonclient width. For y=−999, use the analogous centered height.
Other negative y computes d=screen.bottom−mainWindow.bottom; if −d<y, subtract
d from y; then add screen.bottom−1−windowHeight, and under the same main-window/
NoTitle condition subtract nonclient height. There is no final screen clamp.

Add client origin to body/option/edit origins in this nonfixed branch; sprite
origins do not get that addition. Always add client origin to button coordinates.
Normal layout returns zero.

#### Window and paint order

Creation returns zero immediately when a window is already present. Otherwise
allocate/construct and publish a surface window; use title or empty text, and
choose the frameless or ordinary native style. Parent is the explicit prompt
parent, otherwise the main window when ready, otherwise none. A parented window
uses the prompt palette if supplied, else the initializer palette; an unparented
window always uses the initializer palette. Create with the current computed
geometry. Unless NoTitle, set caption height. If requested size is at most the
reported actual size, query it again and publish the second actual size,
separately for width and height. Install redraw-active-prompt callback. Consume
one-shot idle/move callbacks, clearing each after installation; otherwise use
the default idle callback. Return one. Parent save/restore has one saved slot,
not an independent nesting stack.

Preparation calls layout/create/setup-controls only when not Prepared. A layout
return of one exits preparation. Unless BackgroundSuppressed, paint in order:

1. Outer bevel rings, using light/dark colors, insetting after each. Paint the
   surrounding region. Move inward by outerBorder−2*bevel horizontally, and
   titleHeight−2*bevel / chromeHeight−titleHeight−2*bevel at top/bottom. Paint
   reversed inner bevel rings and the client region.
2. Background copies from a provided background surface at identical coordinates;
   otherwise tile the shared frame surface for surrounding region and client
   surface for client region, when available; otherwise fill the background color.
3. Nonempty title when titles are enabled: center using measured width plus 4;
   draw shadow at x+2,y+1, title at x+1,y, then at x,y. Draw §6 flow text.
4. Sprites: select sprite scale, traverse in order, draw or call the custom
   sprite painter with value, extra field, x/y and sprite/port. Highlight the
   selected sprite when requested. Draw optional label to the right by sprite
   gap and vertically center by the wrapped height difference's arithmetic half.
   Advance by sprite height plus live section gap, then restore identity scale.
5. Edit labels: for each index, independently query edit/body line heights for
   row origin and vertical centering; draw using the current selected font.
   Draw custom list options when List is set and SystemList is clear.

A new window is shown; an existing window is presented and its live controls
are shown in group order. Mark Prepared only afterward. Custom painters and
native presentation are callback boundaries, not atomic rendering snapshots.

List drawing saves the current pane and visits pane 0, then pane 1 if enabled.
Clear its selection-drawn marker; fill when multicolumn, empty or custom-painted;
outline full bounds and clip to the interior. Traversal starts at the **global
active dialog's option head**, while lookup and geometry use the drawing receiver.
Draw matching-group entries in the visible range, bounded by the live total
option count, and OR their selected results. Advance the remaining area's top
by row height after every visible entry, even in multiple columns. Fill remaining
space only for a single-column list without a custom painter. Reset to full clip,
present the pane, and restore the saved pane after both passes.

For one option, reject absent/out-of-view entries; compute relative index,
column=trunc(relative/rows), row=relative%rows with separately read divisors.
Build its cell. Selection is pointer identity. Add pane indentation to the
interior left edge. A custom option painter receives dialog, port, value, index,
x,y,height; a nonzero result suppresses the default painter. Otherwise draw an
inset selection bevel, fill selected/ordinary background, and draw §6 label at
cell x+2,y with the node's disabled flag as emphasis. Optionally present the cell;
return the captured selection Boolean.

### Original §8. Control construction and shared widget behavior

#### Prompt control setup

When this dialog is not active, validate prompt-stack mode in 0..15, report a
fatal diagnostic if invalid, increment the live mode, publish this dialog as
active and as the network manager's prompt, then store it in the live stack slot.
A returning error reporter does not cancel these later steps; stop before any
out-of-bounds stack write. If ControlsReady is already set, return **after** this
activation. Otherwise set shared radio/checkbox fonts to the active body font and
shared button/edit fonts to the prompt button font. Capture Checkboxes and List
for the initial control-kind dispatch.

Construct controls in the following order; read linked next nodes and live
receiver fields after configuration callbacks:

| Kind | Construction and initial state |
| --- | --- |
| System list | For each pane, create a native list with control ID 10 at option x/y and that pane's width/height. Append labels only for matching-group nodes. Its selection index advances only for matching nodes; select the matching selected node. Install action callback. The second pane uses the same option origin. |
| Custom list | Create a vertical scrollbar for one column, horizontal otherwise, under §7's visibility test. Range maximum is group count−1, or its column quotient for horizontal; initial position is viewport index/column. Page size is rows−1 vertically, columns horizontally. Install both position callbacks. Install custom mouse handlers for the first custom pane. Second-pane setup restores pane zero only on normal return. |
| Ordinary radio options | Gather linked labels in order, compute rows with §7's formula and create a group, ID 200. Apply selected-node index and each disabled flag afterward. Install activity-timer and action callbacks. Excess plain options have the report/clamp distinction in Appendix B. |
| Checkboxes | Allocate one per option; IDs 200+index, common option x and y advancing by option metric. Configure label, set value from checked flag, then disable when the live node is marked disabled. This path does not distribute them into columns. |
| Sprite hotspots | Only when sprite count>1 and there are no options. IDs 300+index, sprite origin, scaled maximum sprite width and each scaled sprite height; advance y by height+live section gap. Install primary and secondary sprite actions. |
| Edits | IDs all 20. x=editX+maximumLabelWidth; y=editY+index*editLineHeight. Use the node's pixel width and initial text. The native control height is **30**, separate from spacing. Install the timer callback when live game network mode is at least 3, then set the node's character limit. |
| Buttons | Standard then custom, IDs 100+index, §7 geometry. Cancel becomes the escape default; OK becomes escape default only when Cancel is absent, and always becomes the enter default. Install button action. |

Control arrays use captured allocation counts, while subsequent linked-list
configuration follows links. Mismatches cannot justify writing beyond allocated
controls. Finally install prompt key/character handlers and set ControlsReady.
Default-key routing and traversal are P05's responsibility.

#### Labels, radio groups and checkbox controls

Ordinary button/checkbox accelerator parsing scans the copied label for the first
`~`. Remove it by shifting each following character left, recomputing current
strlen each iteration. Store the following character's lowercased byte and its
index when it exists. Otherwise mark the index absent and retain the prior
accelerator byte. Only the first marker is special.

Radio label creation strips a first `!` to disable an entry, or a first `^` to
select and enable it; other entries are enabled. Group selection initially is
zero, so later leading-selection markers win. Array-list and packed-list tilde
processing differ; the unsafe array-list defect is isolated in Appendix B.
Group cell width is signed rectangleWidth/columns. Array-list row spacing is
`rectangleHeight*columns/count`; packed-list spacing uses count rounded up to
even when columns is not one and count is odd. Text height is
max(native font height,20), captured before device-context
cleanup. Failed metrics require incoming output or a stop; the reconstruction's
failure-to-20 repair is separate. Place rows vertically, centered using arithmetic halves;
when the next row reaches the bottom, advance a column and reset y. Packed lists
include the centering offset in that bottom test; array lists do not.

Disabling a valid radio index first makes selection −1 if it was selected,
then clears enabled and invalidates that row. Negative or out-of-range indices
are ignored. Enabling marks enabled=1 and invalidates without selecting.

Checkbox measured width is text extent plus 37; height is max(native font
height,20). A fixed-width variant keeps its supplied width with the same height
rule. Configuration resets callback/value to zero and enabled to one after base
initialization, then creates the control. Initial values supplied later can be
non-Boolean; a toggle maps any nonzero value to zero, zero to one.

#### Native control services

EDIT, COMBOBOX and system LISTBOX clone the corresponding native class and keep
its lower procedure. Registration publication occurs even when registration
fails. Creation/setting font/text may synchronously invoke callbacks; fields
published afterward must not be moved before them. Native combobox/list append
increments the live item count even if insertion reports failure; reset clears
count after its native call. Error return bits from selection queries pass
through unchanged.

Copying edit text caches its window and output, queries text length, then asks
for wrapped length+1 characters. No caller capacity is passed to the original
helper. Native failure does not establish empty output unless the provider
actually writes a terminator. Copying selected list text queries selection;
selection −1 writes one NUL, otherwise fetch the selected row. Failed row fetch
leaves its scratch bytes incoming. No successful zero/default value is inferred.

Reading an edit line writes the low 16 bits of capacity−1 into its native request
prefix, calls the line getter, then terminates at the full returned count. Unsafe
terminator stores are excluded. Selection-end query extracts the upper half of
the native packed selection. End-of-line is lineIndex(lineFromCharacter(end))
plus that line's length. Collapsing a selection sets both endpoints to the
requested position, then scrolls the caret; the window stays captured across
those calls. Text end queries the last line using lineCount−1 without a zero
count guard.

System-list and combo key-down/up/character messages for Tab, Enter and Escape
are sent to the parent; other keys go to the native lower procedure. Left/right
mouse-down may focus and raise the parent according to its window flag, then
forward. Destroy clears the control's native window before forwarding. Native
notification 1 invokes selection-change callback, notification 2 invokes action,
with the current control ID. Other messages forward unchanged.

**The original EDIT dispatcher does not use the message argument for its local
switch.** It reads a previously unassigned local, masks to a byte and switches
on that value after querying the control/lower procedure. Value 2 clears the
control window when present; value 7 publishes its parent and selects all text
when the control is single-line. Every reachable tail forwards the full original
message to the lower procedure. Supply that incoming byte or stop at its first
use. The deterministic SDK-message dispatcher in the reconstruction is an
explicit fix, not recovered classic behavior. Appendix A pins the instructions.

#### Button and option painting

A push button prepares normal, pressed and disabled bitmaps in that order, each
sized to the control bounds minus two pixels in width/height. A failure logs,
destroys the window and returns no window; original earlier bitmap/device
allocations are not rolled back. Background is native light gray for the default
sentinel, otherwise the parent palette's selected RGB, filled by horizontal lines.

Normal and disabled surfaces draw two white top/left edge paths and two gray
bottom/right paths. For local bounds L,T,R,B, white paths are
`(L,B)→(L,T)→(R,T)` and `(L+1,B−1)→(L+1,T+1)→(R−1,T+1)`;
gray paths are `(L+1,B−1)→(R−1,B−1)→(R−1,T)` and
`(L+2,B−2)→(R−2,B−2)→(R−2,T+1)`. Pressed uses only those top/left paths in gray,
then adds two to text bounds' left/top. Native line endpoints and clipping apply.

Measure the label as vertically centered single-line text with rectangle
calculation enabled, then center horizontally. Normal uses truncating halves;
pressed and disabled use arithmetic halves. Normal/pressed use the device's
current text color. Disabled draws white, then gray at (−1,−1), and restores the
text rectangle. For an accelerator, measure the prefix before it and the prefix
through it to find the character center; draw a ten-pixel gray underline at
calculated text bottom−2 and bottom−1. Restore selected objects, delete temporary
pens/context, then publish the three bitmap states. Native text rectangle
calculation and initial device color are explicit provider inputs.

Radio/checkbox marks use supplied bitmap resources, drawn 32×20 at the local
left and arithmetic half of localHeight−20. Text starts 37 pixels to the right,
vertically centered on one line, with optional shared shadow offsets. Indexed
surfaces use configured palette text colors when nonzero; otherwise enabled
text is black and disabled text RGB(64,64,64). Other surfaces use configured RGB
triples when their presence markers are set, else the same defaults. Enabled
values other than 0/1 retain the previous device text color, although checkbox
bitmap selection treats any nonzero as enabled. Focus outlines start 32 pixels
right of the client left. Push buttons outline full client bounds and an inset
of four when focused with traversal enabled. P03 supplies bitmap/raster services.

### Original §11. Packed-label legacy dialog

This separate helper takes message text and underscore-separated labels. It is
not the resource-driven state machine. The original allocation for each copied
input is one byte short; the resulting terminator overwrite is excluded. A safe
implementation may reject before the write or provide adequate storage and
continue with the following explicitly repaired allocation policy. The later
layout/selection rules remain useful behavioral evidence.

Label measurement starts count=1, temporary fragment empty and maximum width=0.
On each underscore, terminate and measure the accumulated fragment, raise the
maximum, clear the fragment and increment count; then append that underscore
as the **first character of the next fragment**. Never measure the final fragment.
Thus `ab_cdef_z` measures `ab` and `_cdef`, count three; a single label yields
maximum width zero. Text rows are `trunc(messageWidth/(maximumLabelWidth+102))+1`.
The separate splitter replaces every underscore with NUL and counts empty labels,
including leading, consecutive and trailing separators.

Construct palette, font, window, OK, Cancel and radio group in that order. Copy
inputs, replace font with Times New Roman 17, publish it as shared button/radio
font, measure labels/message and set shared selection zero. Window width is
maximumLabelWidth+122; height is lineHeight*textRows+24*labelCount+30. An unparented
window centers on screen; a parented one centers with truncating half differences
inside the parent's outer bounds.

Fill color 7, draw an inset bevel of thickness −3 with colors 255 and 0, publish
the shared packed-dialog window, and draw paragraph text in
`(10,10,width−10,lineHeight*textRows+10)`. Options occupy text-left, text-bottom+10,
text-right−70, height−10, with one column. Packed option labels strip leading
`!`/`^` as in §8; their first tilde is removed with a separate shift index, unlike
the defective array-list routine. Set a valid accelerator from the following
lowercased character or mark it absent.

OK is 60×25 at `(width−70,height−35)`; Cancel is 35 pixels above it. Change callback
publishes the selected ordinal. Double-click publishes it and clears the modal
loop. OK only clears the loop; Cancel first publishes −1. Show and run the generic
modal loop. Delete copied inputs, capture shared selection, then destroy radio,
Cancel, OK, window, font and palette in reverse construction order. Shared font
and window references are not restored by this helper.

### Original §5. Dialog setup and resource parsing

#### Logical defaults and setters

Reset is sparse. It clears the window/background, font references, node heads,
counts, callbacks, controls, title, selections and both results; sets columns to
one, ratio to the shared default (initially 1/1), sprite scale to zero, and current
pane to zero. It clears row-height overrides, pane indentation and drag/selection
markers. It does not initialize every later-produced rectangle or row count.
Default requested coordinates use shared nonzero positions; zero means the center
sentinel −999. Computed coordinates initially use −999; client origin is (0,0).

Padding defaults: horizontal and vertical 2, section gap 4, sprite gap 4, bevel
width 2, outer border 8. Body/shadow are color 0, emphasis 247, variant 255,
background 248, bevel light/dark 255/0, title/shadow 255/0, ordinary list
fill/text/shadow 255/0/0, selected list fill/text/shadow 0/255/255. Colors and
ratios are copied from live shared defaults. Required dimensions and maxima
start zero; ordinary setup supplies content width 440 when no rectangle is given.

Setup cleans up and resets an already prepared dialog, then allocates its arena;
otherwise it allocates the current arena directly. Install the body font and
compute its option metric, then install title/list fonts from live defaults, set the caller window/background and flags, clear the
primary result, and copy optional rectangle x,y,width,height in order. A supplied
window marks the dialog externally owned; absent ownership uses the normal
creation/cleanup path. String allocations remain in the arena until its release;
replacing a title does not reclaim its old string.

Set width to the requested value, except when scale numerator differs from
denominator and NoScale is clear: use wrapped product divided by the signed
denominator. Set height and columns without normalization. Pane selection takes
0 or 1; any other unsigned value chooses 0. Configuring a list sets List, stores
pane width and horizontal offset, and sets rows only when the supplied count is
nonzero. Setting rows always sets List and stores the count; pixel height is
`rows*height+2`, using cached default-font height for SystemList, otherwise the
pane row-height override if nonzero, else cached list-font height plus one.
Reload the pane after the metric operation before storing the pixel height.

The base option metric is 32. Its font-adjusted form makes separate live calls:
start metric=32; if `SAR(wrap(5*lineHeight()),2)<=metric`, replace metric using a
**second** such call; if a third lineHeight is below metric return metric,
otherwise return a fourth lineHeight. Do not collapse these calls around a
callback. Button height is live body line height plus 4 plus twice live vertical
padding. Button width captures sprite gap before measuring the button font,
then returns measured width plus 4 plus four times that gap.

#### Nodes

Text append stores an ordinal starting at one, copied text, requested x/y and
optional center coordinate. Strip one initial `^` and mark a forced line; if a
second `^` follows, strip it too and mark a centered forced line instead. Let n
be copied text length plus one after stripping markers. If requested capacity
is positive and signed n is strictly less than it, reserve and record
requestedCapacity+1; otherwise reserve n and record a **fresh** length-plus-one
after allocation/publication. Copy afterward. Replacement searches by ordinal,
but reads the ordinal before checking for a null node: a missing ordinal can
require an invalid read rather than an ordinary not-found return. A found node
rejects negative capacity or signed length-plus-one exceeding capacity; otherwise
copy and return one. Rejection returns zero. Appendix B records allocation narrowing.

Option append stores value, label, group and doubly linked neighbors. Without
Sorted, append at the tail. With Sorted, advance while the new label compares
strictly greater, case-insensitively; insert before the first equal or greater
label. Inserting at the head makes the new node selected, even in an existing
list. An empty label marks the node disabled. Raise maximum option width to
`bodyTextWidth(label)+32+optionExtraWidth+5` when greater. Checkbox append also
sets Checkboxes and Cancel flags, and optionally marks the node checked.
Lookup by value returns the first match but still reads that node's next link.

Edit append copies the label, raises maximum label width, and stores a character
limit capped at 255 on the upper side only. Pixel width is control-font width of
`W` times that limit. Let combined=pixelWidth+maximumLabelWidth. If requested
content width is nonzero and below combined, subtract
`combined-requestedWidth-3` from pixelWidth, then recompute combined as
`pixelWidth+maximumLabelWidth+3`. Raise maximum edit width accordingly. Null
initial text writes only an empty terminator; nonnull initial text zeroes the
input buffer then copies up to the character limit. Negative/excess writes are
excluded. Store the edit's ordinal, then increment the edit count.

Sprite append stores the sprite, value and optional copied label, with its extra
field zero. The first sprite becomes selected when none was selected. Maximum
width is the sprite width plus, when labelled, measured label width and live
sprite gap. Append order determines display and hotspot order. Custom button
append ignores an additional button once the classic custom-button capacity is
reached. Its count is reloaded after allocation and copying.

[Required behavior is retained in the main section.](#5-dialog-setup-and-resource-parsing)

### Original §9. Selection, mouse and keyboard algorithms

#### Group lookup and selection primitive

`optionIndex(target)` counts preceding nodes in the **target's group**, returns
that count on pointer identity and returns zero if absent. A null target with a
nonempty list is not a valid first item. `optionAt(index)` counts only current-pane
nodes and returns no node when absent. `optionCount(group)` counts the specified
group. Column conversion returns index unchanged for one column, otherwise
signed index/rows. Alignment subtracts signed index%rows when multicolumn.
Clamp applies lower bound first, then upper bound, even if lower exceeds upper.

Choosing a pane by point first selects pane zero. With TwoPanes, use inclusive
left/top/right/bottom containment: pane zero wins a shared border, else pane one,
else restore the previous pane. Single-pane mode always leaves zero.

Point-to-index chooses the pane, then checks outside its interior. With a
horizontal scrollbar, test left/right first; otherwise test top/bottom before
left/right. Return the direction sentinel on the first failure. Inside, compute
`viewportIndex + rows*trunc((x-left)/cellWidth) + trunc((y-top)/cellHeight)`;
clamp to 0..**totalOptionCount−1**, not group count. Signed division faults remain
explicit; no zero-size substitute is supplied.

Selecting a node with `present`:

1. Null or already selected returns without effects. Compute current viewport
   index and target index. If target precedes viewport, set viewport=target.
   While target is at least viewport+rows*columns, advance by one (one column)
   or rows (multiple columns), recording that viewport changed.
2. Clamp viewport to 0..align(totalOptionCount−1); store its node, capture old
   selected node, then publish the new selected node. Do not skip disabled nodes.
3. If viewport changed **or present is false**, update vertical/horizontal native
   scrollbar positions from the current viewport index/column.
4. If present, redraw all options when viewport changed, otherwise redraw old
   then new node. After drawing, call the live selection callback with this
   dialog and the target node's live value. With present=false, neither drawing
   nor the selection callback occurs.

#### Custom-list mouse behavior

Mouse-down chooses a pane and clears its drag marker, then requires a nonempty
custom list. A nonnegative point index sets drag, captures the window and selects
the corresponding node with presentation. Reload the active dialog after capture
and lookup. Mouse-move acts only for a custom list whose current pane is dragging.
Inside the interior it selects with presentation. Outside, it scrolls only when
visible capacity is below the total count:

- Above/below, with one column, decrement/increment viewport by one within the
  original signed tests. Above selects the first viewport node; below selects
  the clamped last visible node. Publish selection/top and redraw directly.
- Left, with multiple columns and positive viewport, subtract rows, clamp to
  zero, and preserve the old selected index's signed remainder modulo rows.
- Right, with multiple columns and viewport below the aligned final total index,
  add rows and clamp to rows*column(totalCount−1). Preserve selected row, then
  repeatedly add rows while another column fits the visible span. Clamp selected
  index to the total range, publish selection/top and redraw.

These direct drag-scroll branches do not invoke the selection callback or update
native scrollbar position. Mouse-up first runs mouse-move, then clears the live
pane's drag marker and releases capture. Double-click requires an interior point,
a nonempty custom list and a dialog not yet Complete; set drag, call mouse-up,
mark Activated and complete the active prompt. Scrollbar callbacks choose their
fixed pane, change viewport by row or rows*column, and redraw when changed;
they do not change selected option.

#### Control activation

Radio mouse-down changes selection only when the row is enabled=1 and differs.
Invalidate old selection when not −1, publish new selection, focus, invalidate,
publish parent, then call change with live ID/selection. Capture afterward even
for an unchanged or disabled row. Mouse-up releases and clears the shared capture
marker. Double-click requires enabled=1, publishes parent and selection as needed,
then calls action without a separate change callback. Right-down captures and
calls right-action even for a disabled row, without selecting it.

Radio next/previous navigation starts at the event row's index±1, wraps at live
count and skips disabled entries for at most a captured count of attempts. If
none qualifies, return. Otherwise focus target, read and invalidate the live old
selection, publish/invalidate target, publish parent and call change with live
ID/selection. Space synthesizes mouse-down/up when traversal is enabled. Native
right/down/keypad-6/keypad-2 navigate forward; left/up/keypad-4/keypad-8 backward.
Other keys forward to the parent.

Checkbox mouse-down requires enabled and no shared checkbox capture; focus,
toggle, set capture marker, capture, invalidate, publish parent and call change
with the live value/ID. Mouse-up only releases/clears when still enabled. Space
synthesizes these mouse events under traversal. Direct programmatic activation
focuses/toggles/captures/invalidates/calls change without the mouse enabled/capture
gates. The two entry points are not interchangeable.

Push-button mouse-down requires enabled, marks tracking, focuses and captures,
then runs mouse-move. Movement while tracking updates pressed according to
half-open local containment. Packed native coordinates are **unsigned** halves.
Mouse-up requires tracking; release capture, clear tracking, invalidate local
bounds, clear pressed, then if inside force paint and call the outer captured
control's action. Space with enabled/traversal draws pressed, clears it, samples
key state, pumps messages once, waits for the sampled/refreshed high byte to
clear without another pump, redraws and calls action. Direct activation focuses,
draws pressed then clear, pumps once, waits eight platform ticks, redraws and
calls action. Preserve these callback and timing boundaries.

[Required behavior is retained in the main section.](#9-selection-mouse-and-keyboard-algorithms)

#### Prompt keys and characters

Reset the activity timer on key events unless List is set and no edits exist.
For highlighted sprites, next/down advances with wrap, previous/up finds the
previous with wrap, and Enter calls the sprite primary handler using the selected
sprite's **value as control ID**, rather than adding 300 to its ordinal. Preserve
that quirk and its bounds. Then reload the active prompt and flags; custom-list
handling can still run after the sprite handler.

For nonempty custom lists, current-pane navigation is:

| Key family | Algorithm |
| --- | --- |
| Up / Down | Previous/next linked node in the group, without wrapping or disabled skipping. Up crossing the viewport in multicolumn mode shifts top back by rows, selects without presentation, then redraws. Other changes use presented selection. |
| Left / Right | One column aliases Up/Down. Multiple columns move by rows. Left crossing the first visible column shifts viewport back by rows and uses nonpresented selection plus redraw. Right requires candidate below group count. |
| Page Up / Page Down | Multiple columns alias Left/Right. One column moves by rows, bounded to zero or groupCount−1, and selects with presentation. |
| Home / End | If movement is needed, set viewport directly to the first/last group node, select it without presentation, then redraw. |

Appendix B lists translated key codes. In particular, nonpresented keyboard
changes omit the selection callback; do not replace every branch with a single
"select and notify" operation.

Character handling requires alphabetic low byte. Reset timer, uppercase the key
and retain its low byte. Label comparison uppercases the **signed initial label
byte** and compares the full CRT result. For custom lists, begin after the current
selection, wrap through group nodes and stop at the first initial-letter match.
The outer match search allows 2000 attempts; the inner group-skipping loop has no
such limit. For multiple columns, shift viewport one column at a time until the
match is visible. With no viewport shifts select with presentation; otherwise
select without presentation and redraw.

For other prompts, highlighted-sprite mode completes on any alphabetic character.
Otherwise search custom buttons first; focus the first initial-letter match and
invoke button action using the **post-focus live** standard count. If none,
search all option nodes for the first matching initial. System lists receive its
global ordinal as native selection; ordinary radio groups receive that raw
selection ordinal; checkbox mode does nothing. This path does not test disabled
flags and does not filter the option search by group.

### Original §10. Modal completion, wrappers and cleanup

#### Generic modal window

This is separate from the prompt stack. Its depth starts at −1. At entry,
**depth==15** returns immediately; this is an equality test, not a range check.
Set the receiver's loop flag, increment depth and store the receiver in that
slot. Disable the previous modal window when one exists; otherwise, if the
current native parent is valid, look up that parent again and disable it.

While the receiver's loop flag is set, pump one message-or-idle operation, then
call its live idle callback when present **even if the pump just cleared the
loop flag**. N03 owns multiplayer idle work; P10 owns media work. Callbacks may
nest dialogs or change shared depth.

After the loop, reload the previous window from the live depth and enable it,
or re-query/validate/enable the actual parent. Decrement the **post-enable live**
depth. If the resulting signed depth is nonnegative, set the **same receiver's**
loop flag back to one; do not set the parent's flag instead. Retain the stack
slot. Invalid stack writes are excluded; unknown stack reads require backing.

[Required behavior is retained in the main section.](#10-modal-completion-wrappers-and-cleanup)

[Required behavior is retained in the main section.](#10-modal-completion-wrappers-and-cleanup)

#### Cleanup and destruction

Focus the explicit prompt parent, otherwise the ready main window. Delete and
clear controls in this order: radio group, checkbox array, edits, buttons,
system lists 0/1, sprite hotspots, vertical scrollbars 0/1, horizontal scrollbars
0/1. Each field clears after its deletion callback. Delete a nonexternal owned
window, clear its field and mark external ownership. Already external windows
remain caller-owned.

If this is the active prompt, validate mode in 1..16 and report if invalid;
a returning reporter permits continuation. Decrement the live mode, restore
the stack entry first through the network manager's prompt field, then restore
active from that field, and reset the timer with a clock sample. Do not replace
out-of-bounds restores with a guessed parent. Ordinary destruction calls this
cleanup and then releases the string arena. No exception rollback is implied
for a diagnostic stop in a reconstruction fixture.

### Original §12. Conformance cases and integration boundaries

A cleanroom suite should distinguish these observations:

| Case | Required discriminator |
| --- | --- |
| Mixed-case parameter names | STRING accepts mixed case; NUMBER and HEX do not. |
| Extra selector digits | Expansion advances by fixed token width/value rule, leaving unconsumed suffix digits. |
| Shared-line formatting | Monthly year and gold modify the shared line even when the numeric destination differs. |
| Wrapping | Spaces are temporary separators; tabs are words; overwide first words produce an empty line; pre-strip space width remains counted. |
| Layout | Forced lines, title and edit/option widths interact; two panes use total count for scrollbar tests; all standard button labels affect width. |
| Duplicate system text | Last matching node wins, including the unfiltered multi-pane traversal. |
| Custom vs radio list | Custom selection accepts a disabled-marked node; native radio navigation skips disabled entries; character selection has its own bypass. |
| Callback reentry | Live active dialog, fonts, pane, counts and result are reloaded at stated boundaries; cached button kind/font arguments survive. |
| Cancel | Edit outputs and checkbox marks still publish; integer wrapper still writes parsed output. |
| Nested modal exit | Final idle callback runs after pump-cleared loop; enable callback precedes live depth decrement; same receiver is rearmed. |
| Native failure | Unproduced edit/metric/geometry output needs retained input or a stop, not a fabricated default. |
| Unsafe text/count | Defined scalar wrap is retained, but any ensuing overwrite is excluded. |

The bounded fixtures in Appendix C exercise string/wrapping/selection kernels.
Resource-file-to-dialog construction, complete layout with actual fonts, native
widget paint/focus, modal cancellation and multiplayer/presentation reentry
still need integrated observations. P05 must join translated keys, Enter/Escape
and queued messages to these controls; consumer documents own which sections,
labels, choices and effects the game requests. This specification does not claim
that substitute metrics or a deterministic repaired edit dispatcher establish
pixel-exact original behavior.

### Original prompt state and native domain

Nominal inputs are complete classic text resources, valid label/parameter
indices, initialized fonts and dialogs, positive layout divisors, sufficient
string storage, and ordinary platform results. The supplied font files, ANSI
character classification/case mapping, font metrics, screen/window geometry,
clock samples, message sequence and native-control responses are external
inputs. Font glyph rasterization and native EDIT/COMBOBOX/LISTBOX internals are
platform contracts, not a request to duplicate a particular operating system.
Matching visible layout requires matching their measurements and responses.

Logical state includes:

- The label index and string pool; six substitution numbers, the ordinary string
  parameters, the short game-profile height string, and the shared text line.
- Font objects and cached heights; current drawing font, text color, shadow
  offsets/colors and extra text width; shared fonts used by each control kind.
- Each dialog's ordered text, option, edit and sprite nodes; title and button
  labels; fonts, colors, padding, dimensions, position, flags and callbacks.
- Two list panes, each with its group, bounds, rows, scrollbars, indentation,
  viewport, drag state and row metrics; one selected option shared by the panes.
- Selected sprite, primary result, secondary/custom-button result, active dialog,
  prompt stack, modal-window stack, idle callback and activity timer.

Observations include destructive string edits, shared-buffer writes, allocation
and cleanup order, geometry, drawing, focus/capture, callback arguments, selected
values, accepted/cancelled results, message pumping and retained state. Dialogs
are not transactions. Cancel does not generally undo edit-buffer or checkbox
publication.

### Original prompt window restore

   is zero, else `ticks+timeout*60−1200`, with wrap. When map input is enabled
   after that callback and the main window is iconic, restore it.

### Original prompt completion pumping

7. Unless Keep, clean up, discard queued input, pump messages, flush queued paint,
   then pump messages again. Return the **live primary result after all of these
   callbacks**, not the value captured at completion.

### Original timer string formatting

Timer text has two branches. With the shared autoplay flag and turn-status
presentation enabled, compare cached countdown with the signed autoplay-remaining
word; when different, update the cache and write decimal from a fresh read of
that word. Otherwise, for a human saved-player actor with countdown enabled,
refresh only when cache differs from the shared status timer. A cache other than
−1 takes that timer; cache −1 takes signed turn-limit milliseconds divided by
1000. Clamp a negative cache to zero. Compute minutes=trunc((cache−1)/60) and
seconds=cache−1−minutes*60. Write a leading zero for minutes below ten; place the
colon at character index 2 regardless of minute width. Seconds below ten get a
leading zero and negative seconds clamp to zero. Other cases clear only the
first timer character. Unchanged cache retains its existing text.

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

### Original label-pool allocation and native failures

Text startup derives the status-language position from signed defaultLanguage−1,
resetting it to zero only when greater than two. Select extension TXT by default,
then TXT/FRE/GER/ITA/SPA for default languages 1..5. Build the GAME section cache
through F03. Look up LABELS/STRINGHEAP and raise the retained pool capacity when
its parsed signed request is greater; initial capacity is 35000. Initialize the
pool, clearing its count after allocation, and append `<nil>` as its first entry.
Reinitialization releases an existing pool first. Ordinary/padded pooling follows
[F03's string identity contract](F03-rules-and-configuration.md#tables-markers-and-strings).
Rebuild repeats the STRINGHEAP capacity step without resetting language/cache,
reinitializes the pool and `<nil>`, releases old labels, loads labels below
(ignoring its result), then loads popup labels.

Use F03 to open section `LABELS` of `LABELS`. On failure report application error
−8, severity 4, and finish. On success read the declared count, publish it, and
allocate/lock a label-index collection with the classic capacity in Appendix B.
Allocation failure reports −9; lock failure reports −10. These branches retain
preceding publications and do not invent an empty successful table.

Starting at index zero, while index is less than the **live declared count**, read
one pooled string and store its identifier through the live label-table reference.
Then, if the index is below the classic capacity:

1. Save the search directory and substitute the installation directory. Try the
   same file/section there.
2. If it opens, read its fallback count. Starting at zero, skip the live current
   label count's lines; append pooled strings up to the captured fallback count.
3. Restore the search directory. If still short, pool `[ERROR:LABELS.TXT]` once
   and fill the remaining entries with that same identifier.

Return the resulting index; close the file on all ordinary finishes. If the
result is nonpositive, unlock and free the current label handle, clear the handle
and count, but retain the published table reference. Reads or writes beyond
established collections follow §1. The primary reconstruction's count clamp is
a repair, not an extra original parsing rule.

## Appendix F. Original numeric annotations

These original annotations retain storage widths and reference-evaluation context.
They do not prescribe internal types or redundant casts. The main contract and
F01 determine logical ranges and behaviorally significant conversions; external
file/wire encodings remain normative in their owning mappings. Where an excerpt
includes an off-nominal conversion, it applies to that explicitly admitted profile,
not as a new requirement to accept unsupported inputs.

### Reference annotation 1

Colors use their low byte. A rectangle is half-open except the explicit inclusive
pane-choice test.
