# M1 classic text, rules and city preferences

The portable F03 text reader, all twelve classic rule-table readers and both
city-production preference paths are implemented. Native JSON uses the pinned
jart/json.cpp admission wrapper; classic text uses supplied file, section-index
and byte-comparison services. Both rule paths produce `RuleDefinitions`, and
both preference paths publish `CityPreferences`. This supplies part of M1's
native/classic content boundary, not complete rules conversion or session content
admission. [The completion audit](m1-validation.md) remains open.

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

`state::RuleState` now owns the decoded definitions, shared `TextPool`, numeric
name bindings, 21 live `Leader` records and the double-movement-applied flag.
`ProcessContext::rules` uniquely owns that state outside the caller's stack.
Independent processes have independent pools; client-session replacement and
entity-scan reset retain the installed rules, labels and leader counters.

The pool implements F02's ordinal identities and F03's append-only allocation
effects. Explicit `initialize()` clears it and stores P04's `<nil>` at ordinal
zero. Initialization is not implicit in construction or a client-session change.
Ordinary nonempty text appends bytes plus NUL; empty text returns ID zero without
allocating or changing the last allocation. Padded empty names become one space;
minimum capacities zero-pad without truncating. Equal strings append separately.
Existing text views remain valid across appends, while pool replacement invalidates
them. An allocation hook runs before publication; an exception preserves preceding
effects. Input bytes are owned across that callback, and the returned ordinal uses
the pool's resulting live size.

All classic name assignments publish their 32-bit bindings. Leader name, plural,
adjective and title bindings narrow to signed 16 bits; government default title
IDs narrow when installed in leaders. Gender selects the current leader label.
Preservation restores each previous binding and decoded field after reading its
replacement, while retaining allocations. Parsed unit names update the parallel
interface binding; defaulted tail names change the rule binding without updating
the interface binding. Order-name slot zero remains untouched.

Decoded `RuleText` values describe the loaded content. Live name consumers must
resolve numeric bindings, including sign interpretation at the owning operation;
they cannot substitute those decoded strings after narrowing, pool replacement
or F06 sign-marker changes. For example, an allocated ID 32769 narrows to −32767:
C01's absolute-label lookup reads ordinal 32767, not the newly allocated string.
Ordinary empty decoded text similarly has ID zero, whose initialized pool value
is `<nil>`. This distinction is explicit rather than hidden by a cached string.
The process overloads of city preferences and C01 naming use current pool bindings.

The live leader records pass through the existing 48-byte F06 codec without a
second representation. P04's full label-file startup/loading, S04 editing,
retained native rule installation, F06 sign-marker processing and complete
session reload orchestration remain open. Supplying an initialized pool alone
does not claim original startup ordinal equality; the preceding labels and
allocation sequence must also match.

A [retained original-save consistency check](conformance/reports/original-rule-labels-windows.json)
matches all 18 English leader name/plural/adjective/title label magnitudes against
the sequence `<nil>`, 888 labels, three popup strings and the ordered rule tables.
The independently authored helper pins the input bytes and decoder. This supports
the zero-based ordinal interpretation for those values; it neither executes the
C++ rule loader nor supplies a new original startup trace. The earlier original
capture pinned the rules and save, but not LABELS; the report identifies that limit.

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
is no filesystem discovery, session reload orchestration, native installation
policy or classic export command hidden in this API.

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

## Evidence and remaining work

Fifteen authored portable reader cases, sixteen classic rule cases and six
preference cases cover the above contracts. The rule cases include a complete
independently authored native/classic equivalence check with out-of-order
sections, explicit fallback/reuse, normalization, reference failures, name/leader
preservation across callbacks, allocation failure, tail interface bindings and
retained special terrain work. Three additional pool cases check ordinal/empty/
padding behavior, stable text views, allocation/reset failure and live callbacks.
Leader-boundary cases exercise IDs at 32768 and 65536 through the 48-byte codec
and actual C01 provider, and distinguish decoded text from a narrowed live label.
The session ownership case also checks retained and isolated rule/pool state.
The preference cases cover paired inputs,
first-match behavior, retained fields and live name changes.
Two additional native CLI cases check real mounted files, source preservation,
receipts and collision/comparison diagnostics. These are specification-derived
and regression checks, not new original-game observations or independently
reviewed X02 fixtures. F03 is not yet registered with X02.

The [city-name providers](m1-city-names.md) now implement C01's ordered default
naming through both paths, with shared counter/publication behavior and a native
validation command.

Full label startup, retained native rule installation, the complete
comparison profile, remaining native component admission,
conversion tools/receipts, session wiring and owning production consumers remain
required. The table readers are ready for those callers; they do not implement
them implicitly.
