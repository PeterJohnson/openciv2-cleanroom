# M1 classic text and city preferences

The portable F03 text reader and both city-production preference paths are
implemented. Native JSON uses the pinned jart/json.cpp admission wrapper;
classic text uses supplied file, section-index and byte-comparison services.
They publish the same logical `CityPreferences` state. This supplies part of
M1's native/classic content boundary, not complete rules conversion or session
content admission. [The completion audit](m1-validation.md) remains open.

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

## City-production preferences

`formats::classic::load_city_preferences` follows F03 §8 incrementally. It
clears the active count, retains unused list payload and NoDefend, checks
NODEFEND presence, closes, and independently opens AUTOBUILD. It reads current
improvement names 1…38 in ascending order, appends the first match, retains
duplicates and stops at the first unmatched token, EOF or 32 entries. Trailing
tokens are ignored. Missing content creates no stock list. Name reads remain
live across file callbacks; a callback-corrupted count stops at the attempted
out-of-bounds write after prior effects.

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

Fifteen authored portable reader cases and six preference cases cover the above
contracts, including paired native/classic inputs, first-match behavior,
retained fields, live name changes and a bounded failure after earlier writes.
Two additional native CLI cases check real mounted files, source preservation,
receipts and collision/comparison diagnostics. These are specification-derived
and regression checks, not new original-game observations or independently
reviewed X02 fixtures. F03 is not yet registered with X02.

Full classic rule ingestion, string-pool identity/allocation effects, retained
rule installation, city-name providers, the complete comparison profile, native
component admission, conversion tools/receipts, session wiring and owning
production consumers remain required. The low-level reader is ready for those
callers; it does not implement them implicitly.
