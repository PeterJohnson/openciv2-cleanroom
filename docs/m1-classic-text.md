# M1 classic text, rules and city preferences

The portable F03 text reader, P04 label loader, all twelve classic rule-table
readers and both city-production preference paths are implemented. Native JSON uses the pinned
jart/json.cpp admission wrapper; classic text uses supplied file, section-index
and byte-comparison services. Both rule paths produce `RuleDefinitions`, and
both preference paths publish `CityPreferences`. This supplies part of M1's
native/classic content boundary. The later [conversion slice](m1-content-conversion.md)
completes the three M1 conversions; playable session content admission remains
later work. [The completion audit](m1-validation.md) records the closed M1 scope.

## Text representation refactor

Current production text uses unpadded owned strings and local references with
explicit custom-name flags. The F06 stored leader codec is a separate type;
live export translates flags without narrowing text IDs. Label rebuilds preserve
unrelated rule references. Synthetic text-allocation callbacks, padding checks
and matching allocation traces have been removed from production interfaces/tests.
Original ordinal calculation remains only in `tools/check_original_rule_labels.py`,
using original data and the lossless stored inspector. The former C++ ordinal
comparison executable is removed. Earlier allocation/ordinal results below are
historical evidence, not current implementation or milestone requirements.
Semantic regression checks replace the historical allocation-trace assertions.

Refactor validation: 559 native CTests (547 Catch2 cases), all seven WASM CTests,
115 Python tests with two expected skips, and all 57 registered conformance
vectors on both targets pass. Pinned formatting and metadata checks pass.
These are implementation regressions and specification-derived checks. No new
original-executable run was performed for canonical live name markers; their
translation is checked against the specified F06 restoration consumer. Historical
original reports remain historical and are not relabeled as evidence for this build.

## Classic reader

`formats/classic_text_reader.hpp` implements F03 §2/9 and the required retained
state in Appendix A:

- Reads contain at most 254 text-mode bytes. The owned memory adapter translates
  CRLF to LF while retaining physical offsets for seeks; long lines split across
  reads. Only terminal LF and edge ASCII spaces/tabs are removed. Blank lines,
  comments, headers, quotes and byte-order marks have no general skipping rule.
- The shared workspace retains its token cursor. EOF/read failure clears only
  buffer byte 0 and closes the stream. A cursor within an established suffix can
  still read that suffix; a subsequent line read on a closed stream stops.
- Comma tokenization preserves empty fields and semicolons. Decimal/hex/binary
  integer parsing wraps modulo 2^32, including decimal negation; ability parsing
  independently consumes an unprefixed binary run modulo 65536. Empty padded
  names become one space, while ordinary empty names remain empty.
- Explicit opens append the language extension only if no dot occurs anywhere
  in the filename, uppercase through the supplied service, try search/home
  directories and restore the caller's working directory. Whole-line section
  matches leave the cursor at the header's end.
- Reused streams search forward and rewind once on EOF. A failed reused search
  closes the stream; the wrapper's retry then reports an unavailable-stream
  boundary. It does not silently reopen a home file. Explicit-file wrapper
  retries can select a home section absent from an existing scenario file.
- Supplied section-index offsets greater than zero cause a seek. No index is
  inferred from a dictionary: duplicate-header results retain the distinction
  between explicit, reused and indexed search. Index construction is separate.

`TextWorkspace.backed_bytes` identifies known buffer storage. A successful read
establishes the trimmed visible prefix and its terminator. Bytes beyond that
prefix are not a reconstruction of the original trimming scratch; a fixture can
supply explicit backing for such reads. Unknown/unbacked cursor accesses,
unterminated backing, failed seeks and inconsistent read-service results stop
with `TextError`, preserving preceding effects. A service reporting unspecified
partial buffer bytes on failure is rejected rather than assigned an invented
meaning. The memory adapter also stops on NUL/control-Z; extending those raw
text-mode cases needs an explicit contract or supplied service.

The default comparison is deliberately partial: equal lengths and bytes need
no folding, differing ASCII bytes use ASCII case equivalence, and an encountered
unequal high-byte pair reports `comparison_unavailable`. Path uppercasing accepts
ASCII only. Other comparison services can be supplied explicitly. This does not
close the [F03/C01 non-ASCII comparison gap](m1-rule-content.md#encoding-and-remaining-comparison-gap)
or turn the [ordered original observation](m1-original-text-observations.md) into
a complete folding table. Byte text and presentation encoding remain separate.

## Shared labels and startup

`formats::classic::LabelLoader` implements P04 §2 using the same reader and pool.
Initialization selects language and invokes the supplied GAME-cache builder.
Original STRINGHEAP size hints are ignored. Label loading is separate. Rebuild
replaces labels while preserving rule references and missing-popup fallbacks;
it does not reset the text pool, language or lookup configuration.

Classic resources use 888 label indices and three popup roles. Short tables
load installation fallbacks and then `[ERROR:LABELS.TXT]`; ordinary empty text
resolves to `<nil>`. Invalid writes are bounded, missing LABELS reports failure,
and scoped search overrides restore on failure. No original handle/lock errors,
heap capacity or allocation callbacks are modeled.

The GAME-cache builder remains a supplied service. F03 specifies header recognition
and indexed lookup but not the builder's finite capacity or duplicate insertion
policy. Those are specification gaps for building the original cache; no guessed
dictionary implementation is used. Uncached unique-header inputs can already run
through the reader. Full application startup and session reload wiring remain open.

## Classic rule tables

`formats/classic_rules.hpp` implements F03 §§3–7/9 and the declared Appendix A
reference vocabulary. `RuleLoader::load` requests COSMIC, CIVILIZE, IMPROVE,
ENDWONDER, UNITS, TERRAIN, GOVERNMENTS, LEADERS, CARAVAN, ORDERS, DIFFICULTY and
ATTITUDES in that order. The first three opens use an owned explicit filename;
later sections reuse the selected improvement stream. Physical section order
can differ. A successful full load closes the stream. Individual section readers
start immediately after an already selected header.

The loader updates existing definitions field by field. It preserves caller
identity IDs and never infers them from classic names. Shared cosmic, movement,
HP, cost and leader normalization is applied once; the cosmic helper also
supports individual line publication. Advances reset enablement/prerequisites,
apply only their documented tail defaults and fix references after the full
table. Improvement prerequisites follow disabled advances, while unit references
and wonder expiry do not. Enabled cycles remain accepted. Unit abilities assign
a zero-extended 16-bit result into the 32-bit logical field, clearing old high
bits. Special terrain rows retain existing work/transformation values.

Individual advance, improvement and unit readers support `keep_names`, including
default tails. Government definitions always change. Leader preservation consumes
and stores the new strings, then restores each previous field individually;
it neither selects a new current name nor installs default title pairs. Repeated
optional title overrides use the last value. The loader retains each leader's
usage count and city-name cursor.

`state::RuleState` owns decoded definitions, text storage, logical name references,
21 live leaders and the double-movement flag. Name references are full-width;
custom-name status is an explicit flag. Preserving names does not allocate or
publish discarded replacements. Appends retain existing views. Label rebuilds
preserve unrelated rule references; new-game preparation replaces the whole state.
The original parallel unit-name pointer table has no runtime consumer and is omitted.

The separate F06 `StoredLeader` type preserves every byte of an imported record.
Live name restoration consumes its sign markers, and live saving writes canonical
markers without depending on UI text allocation history. The original consistency
[report](conformance/reports/original-rule-labels-windows.json) is historical
evidence about original ordinals. Its independent Python predictor remains in
`tools/check_original_rule_labels.py`; the old C++ ordinal driver was removed.
No original source/harness is an implementation input.

Reference failures retain the specified prefix and report the module, code, row,
position and offending line. A returning diagnostic/cleanup callback is followed
by `RuleLoadError`; it cannot resume parsing. An invalid optional title index
stops before allocating that pair or attempting an out-of-bounds write. Missing
explicit sections stop at the failed open with a separate diagnostic, without
inventing an original fatal-reference code. Failed reused sections and unbacked
cursor/stream accesses retain the reader's bounded `TextError` behavior.
Fixed-row reads use the retained buffer after an EOF result until the next
unsupported access; they do not fill missing tables with stock rows. The safe
missing-section stop is not a claim about an original invalid-access result.

Stored names currently require reversible Windows-1252 presentation decoding.
Undefined bytes fail before storing that name, preserving earlier updates;
other language encodings and the broader comparison profile remain open. There
is no filesystem discovery, session reload orchestration or classic export command
hidden in this API. The separate [native rule installer](m1-rule-content.md#retained-rule-installation)
now applies admitted semantic input to the same retained state and pool.

## City-production preferences

`formats::classic::load_city_preferences` follows F03 §8 incrementally. It
clears the active count, retains unused list payload and NoDefend, checks
NODEFEND presence, closes, and independently opens AUTOBUILD. It reads current
improvement names 1…38 in ascending order, appends the first match, retains
duplicates and stops at the first unmatched token, EOF or 32 entries. Trailing
tokens are ignored. Missing content creates no stock list. Name reads remain
live across file callbacks; a callback-corrupted count stops at the attempted
out-of-bounds write after prior effects.
The `RuleState` overload resolves current improvement label IDs through the
shared pool. The definition-only overload remains available for direct semantic
content inputs; it is not the process name-reference lookup.

`formats::content::load_city_preferences` validates a selected resolved pack's
`openciv2-city-preferences/1` component and its complete native rules. IDs must
resolve to improvements 1…38; at most 32 entries are accepted. The validator
rejects an ID whose classic name first matches an earlier improvement, including
case collisions. Empty/padded-space names cannot round-trip through the trimmed
token and are rejected. Unestablished comparisons get a distinct diagnostic.
The immutable result retains the selected rules and preference source receipts.

`rules::apply_city_preferences` validates the complete native update before
publication, then replaces the active prefix while retaining unused payload.
`deprioritize` writes NoDefend=1; `unchanged` preserves its incoming value. This
does not choose city production, modify automation flags or reset other settings.
It does not change the classic loader's incremental failure behavior.

The native command accepts the same explicit parent mounts, locale and gameplay
profile gate as other content commands:

```powershell
build/headless-fetch/oc2.exe content validate-preferences path/to/content.json --json
```

Its report format is `openciv2-city-preferences-validation/1`, with scope
`rules-gameplay-and-city-preferences`, ordered `buildSlots`, the defense action,
the partial comparison policy and source receipts. Exit codes are 0 for admitted
content, 1 for semantic/comparison rejection, 2 for usage/source failure and 4
for malformed JSON. No files or live state are changed. This command does not
certify full classic export representability: complete row lengths and section
marker collisions still belong to the conversion boundary.

## Process preferences and CIV2.DAT

F03 section 8 preference initialization and persistence now operate on complete
ordinary and multiplayer records in `ProcessContext`. They are distinct from
live world options and saved multiplayer state. Existing core-load consumers use
the same multiplayer fields; the accepted-file load caller reads
`ordinary_preferences.game_options`. Client-session transitions retain both
records, and preference input alone changes no world options or random context.

Ordinary defaults use the supplied startup screen width and renderer animation
availability, including the width-greater-than-999 boundary and herald gate.
They assign the documented settings while retaining the preference text's tail.
Multiplayer defaults preserve the tails of emptied strings and three legacy
24-byte settings, write only `60` and its terminator into the custom timer,
and clear the remembered settings table. The logical startup inputs do not
emulate the original native memory API or prescribe renderer allocations.

`formats/preference_io.hpp` implements every field of F06's 72-byte ordinary
item and 444-byte multiplayer item. Successful reads retain arbitrary scalar
bits and unterminated fixed strings. A failed item overlays its transferred
prefix over post-callback live state before the F03 fallback runs. The first
failure does not rewind or skip the multiplayer attempt; bytes in unassigned
tails can survive defaults. Trailing file bytes are ignored.

`session/classic_preferences.hpp` opens `CIV2.DAT` through explicit services.
Opening failure defaults both records. Saving uses binary replacement, writes
ordinary then live multiplayer preferences, stops at the first failed write,
closes and deletes the partial output. Opening failure does not delete. Close
and delete return failures are ignored; required service exceptions stop with
prior effects retained. `io::ClassicFiles::remove_file` supplies the native
nonrecursive file deletion, using the same case-resolution policy as input and
output and rejecting directories.

Thirteen new portable cases cover field offsets, defaults, every transferred
prefix, invalid item-service results, live reads, cursor continuation, ignored
tails, failure/delete ordering and process lifetime. Another portable composition
case passes real F03-loaded options through the F06 accepted-file caller. A native
case exercises actual replacement, reload and failed-output deletion; three CLI
cases check complete/trailing input, truncated rejection, source preservation and
no-overwrite output. These are specification-derived inputs and expectations.

The native comparison driver uses actual F03 preference input and re-encodes both
records. It requires at least 516 input bytes so defaults cannot be mistaken for
a successful original-file round trip. Its optional output is a separate new
516-byte file; ignored trailing input is reported separately:

```powershell
build/headless-fetch/oc2_preference_roundtrip.exe path/to/CIV2.DAT path/to/new-copy.dat
```

On 2026-11-14 UTC, the driver read the existing 516-byte `CIV2.DAT` from the
isolated original-game runtime used in the
[original live-save exchange](m1-original-live-save.md). Both items reproduced
byte for byte. Input SHA-256 before and after input, and the separate output's
SHA-256, were all
`318a571574588fce7fda32feed0c84bd70b76988abe80867f041c92c92a5411a`.
The local input is `build/original-text-runtime/CIV2.DAT`; the output is
`build/original-preferences-roundtrip-2d22a52.dat`. Neither is committed. This
checks original-file input/re-encoding through the new C++ path; it is not a new
original-program execution or acceptance of edited preferences.

Accepted setup rules, application startup binding, native preference presentation
and original reload of an edited preference file remain separate work. Accepted
in-game changes now use the operations described below.

At the preference-persistence checkpoint, verification passed all 417 native
CTests and 419 with SQLite enabled. Pinned WASM passed all six CTests, including
395 portable Catch2 cases and 185,072 assertions. The 54 retained native/WASM
X02 observations and consumed prefixes matched; F03 preference operations are
not yet registered with X02. Pinned clang-format 20.1.8 passed.
Logs are retained locally in ignored
`build/preferences-{native,sqlite,wasm}-{build,ctest}.log`.

## Accepted in-game configuration

`session/configuration.hpp` applies F03 section 8's accepted game, graphics and
message choices to live session state and persists them through the real
two-item preference writer. Cancellation leaves both records alone. Game options
clear their eleven controlled bits, apply sound/music choices and the specified
music operations, then apply the remaining nine choices and save the full live
word. Graphics options replace their six bits in order; a new herald enable still
applies after an advisory
resource warning. Message choices replace the entire disabled-message word,
clearing unknown bits. Required child exceptions retain preceding effects;
reported preference-save failures do not roll back accepted options.

`rules/double_movement.hpp` implements the actual F03 table transformation.
It visits all 62 unit types and changes land movement, preserving byte wrapping
when doubling and dividing the signed current byte with truncation toward zero
when removing doubling.
It preserves air/sea types and existing units' spent movement. The retained
applied flag changes before the table, and status redraw follows the complete
table. Single-player mode and repeated requests leave the flag and table alone.

The accepted multiplayer boundary composes F03 with N03 section 11. It saves
the two chat choices before reading host authority or processing double options.
A changed network proposal with more than one player prepares the shared text
arguments, initializes the signed pending count, broadcasts, installs the shared
vote callback and enters the supplied modal operation. Negative modal results,
status -1 or any nonzero pending count reject and restore remembered double
settings from the live values. Acceptance installs live values and runs the real
movement transformation. The following network/player-count gate uses live state;
its selected tail sends units, sends all three signed option words, broadcasts
acceptance and displays the notice. Double options receive no second save.

The shared vote callback polls first, then tests handoff, signed wrapping elapsed
time, pending count and rejection status in order. Completion ends the supplied
modal and captures a fresh tick. Runtime player count, handoff and vote scratch
are separate from classic saved multiplayer records. Session publication precedes
external children, so scoped observers can see the specified partial effects.

Twenty-eight authored portable cases cover checkbox mappings, audio ordering,
save failures, required-child stops, all 256 movement bytes, signed removal,
authority changes during persistence, proposal rejection/acceptance, live callback
mutations, raw simultaneous words, clock wrap and the 2400/2401 timeout boundary.
The 3,881 assertions use specification-derived expectations and explicit child
outcomes. They are not new original-game captures or actual network/modal/audio
integration evidence. These operations are not yet registered with X02.

Application command/widget binding, P10 audio implementations, N02 option wire
dispatch and replies, P04 modal ownership and F05 setup remain required owning
work. The retained vote callback requires the modal owner to keep its runtime
alive while delivery remains possible.

Verification passes all 445 native CTests and 447 with SQLite enabled. Pinned
WASM passes all six CTests, including 423 portable Catch2 cases and 188,953
assertions. The 54 retained native/WASM X02 observations and consumed prefixes
remain identical. Pinned clang-format 20.1.8 passes for all 242 owned files and
the eight staged C++ files. Logs are retained locally in ignored
`build/configuration-{native,sqlite,wasm}-{build,ctest}.log`.

## Evidence and remaining work

Portable reader, classic/native rule, label and text tests cover authored parsing,
fallback, name preservation, malformed-input bounds and real I/O failures.
Paired installation compares semantic values with different preceding text
allocations. Large-ID tests verify intended names remain correct above 65535;
separate tests preserve signed codec fields and verify custom-name restoration.
Label rebuild tests retain old rule and popup text. Original allocation counts,
padding and manufactured allocator callbacks are no longer acceptance criteria.
The preference cases cover paired inputs,
first-match behavior, retained fields and live name changes.
Two additional native CLI cases check real mounted files, source preservation,
receipts and collision/comparison diagnostics. These are specification-derived
and regression checks, not new original-game observations or independently
reviewed X02 fixtures. F03 is not yet registered with X02.

The [city-name providers](m1-city-names.md) now implement C01's ordered default
naming through both paths, with shared counter/publication behavior and a native
validation command.

The [F03 reload context and native file services](m1-stored-saves.md#native-files-and-rule-context)
now compose label rebuilding, sprite callback, scenario/home selection, full table
loading and final name-preserving reloads over real native streams. Four portable
wrapper cases and seven native adapter/integration cases cover the ordering,
partial effects and full disk load-edit-save-reload. The graphical resource
consumer, GAME cache and browser persistence remain separate integration work.

M1 closeout is limited to [M1-02](m1-closeout.md#m1-02-content-and-conversion-matrix)
and regression checks for the implemented readers. The complete comparison
profile, other native components, GAME-cache construction, application wiring
and owning consumers remain M2–M5 work. This historical slice is not an additional
M1 work list.
