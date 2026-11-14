# M1 city-name providers

The C01 default-name operation now uses either classic CITY text or admitted
native JSON lists. Both paths apply the same retained counter and publication
rules. This closes the isolated naming/content-provider portion of M1; complete
city creation, founding prompts, cancellation, process rule installation and
session admission remain separate work. The [M1 audit](m1-validation.md) is open.

## Operation and state

`city::default_name` implements C01's [default naming contract](algorithms/C01-city-lifecycle-and-ownership.md#default-city-naming).
It captures the city's owner and resolves the magnitude of that owner's current
leader plural-label ID once, retaining the resulting section/selector text.
The resolver receives 32768 for the signed-16 value −32768. It does not assume
that a rule display name, tribe ID and label ID are interchangeable.

Each of at most two attempts increments the then-live leader's city-name cursor
modulo 256 before querying the provider. Civilization-to-tribe reads remain live
at each increment and exhaustion reset, even when callbacks change the tribe
or city owner. Exhaustion resets that live leader's counter to zero and retries;
an unavailable civilization list exits without resetting. A successful proposal
clears all 16 destination bytes and copies at most 15 classic bytes, retaining
a terminal zero. There is no uniqueness search, random suffix or generated name.
Unsuccessful normal completion preserves the old name payload.

Normal exits close the provider, including missing lists and two exhausted
attempts. Exceptions preserve effects already reached; they do not invent a
successful result, roll back counter changes or perform the normal close step.
Missing civilization/leader backing stops at the corresponding access. The
operation does not initialize city records or decrement a cursor for a cancelled
founding prompt; those are distinct C01 caller steps.

## Classic and native providers

`formats::classic::city_name_provider` uses the supplied F03 reader and directory
state. It tries the retained civilization section with an explicit CITY filename,
then retries from home on failure. Successful selection leaves the working
directory at home. It reads whole trimmed lines, stopping on an empty line or
case-insensitive `@STOP` prefix. EXTRA reuses the selected file and continues the
remaining count; it never restarts that count or selects an unrelated home EXTRA.
The low-level section opener permits C01's documented missing-EXTRA retry, rather
than invoking F03's wrapper on an already closed reused stream.

The provider tests the shared line after an EOF result, as C01 specifies. If a
later EXTRA lookup attempts to reuse that closed stream, it reports the reader's
bounded error. A blank/STOP terminator before EOF supports normal exhaustion.
Cursor 255→0 performs no name-line reads and publishes the live line left by
section lookup, normally the actual matching header with its stored spelling.

`formats::content::load_city_names` uses pinned jart/json.cpp to validate the
selected `openciv2-city-names/1` component and its complete native rules. It keeps
ordered proposals, duplicate city names, explicit plural selectors and nullable
fallback storage. It rejects ambiguous selectors, non-reversible Windows-1252,
empty/multiline/control-bearing text, changed edge trimming, names longer than
254 classic bytes and names that become an `@STOP` terminator. Base packs require
a nonempty list for every starting tribe plural. Resolved overlays retain whole
file replacement semantics and may lack such lists; coverage is reported.

The native provider owns immutable content. It selects by the requested live
plural name, consumes the remaining position across the civilization and fallback
lists, and computes the wrap result directly as `@` plus the stored matching
plural name. It creates no virtual text file. Missing and empty fallback lists
remain distinct data states even when both exhaust a particular attempt.

Both providers accept an explicit comparison service. The supplied default
supports exact byte equality and ASCII folding, and stops on an encountered
unestablished high-byte comparison. The [comparison gap](m1-rule-content.md#encoding-and-remaining-comparison-gap)
remains open. Native validation reports `comparison-unavailable` separately;
it does not substitute Unicode folding or the Windows host locale.

## Validation command and evidence

```powershell
build/headless-fetch/oc2.exe content validate-names path/to/content.json --json
```

The command accepts explicit parent mounts, locale selection and `--require-classic`.
Its format is `openciv2-city-names-validation/1`, with scope
`rules-gameplay-and-city-names`. It reports ordered selectors/counts, fallback
availability/count, starting-list coverage, comparison policy and source receipts.
Exit codes are 0 for admission, 1 for semantic/comparison rejection, 2 for
usage/source errors and 4 for malformed JSON. It does not write input files or
certify complete playable content. Full serialized row lengths and section-marker
collisions still need the classic export boundary; individual text admission is
not a full export certificate.

Ten portable authored cases compare native/classic names, counters and retained
bytes, including retries, wrap, truncation, duplicate proposals, missing lists,
live tribe changes and failure prefixes. Three additional CLI cases verify mounted
source receipts, unchanged files, rejection and overlay replacement. These are
specification-derived checks, not original-game captures or independently reviewed
X02 fixtures; C01 is not registered with X02 yet.

The process overload now supplies that resolver from `RuleState`'s shared pool
and live leaders. F03 loading publishes the signed-16 leader bindings consumed
by C01, including absolute-label aliasing after narrowing; a dedicated test runs
that path through a real classic provider and the existing leader record codec.
The explicit resolver overload remains available for supplied backing/services.
P04's full label startup, F06 sign-marker processing, native rule installation,
session/city-creation integration, conversion receipts and broader original-game
comparisons remain required.
