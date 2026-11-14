# M1 native rule definitions

`oc2_formats::content::load_rules` now decodes a resolved pack's complete native
rule definitions into immutable typed values. It checks all ten fixed identity
collections, cosmic parameters, wonder expiry, rule references, government-title
overrides and the selected gameplay bundle's government advance references.
It returns no result on failure and performs no file writes or session updates.
The model lives in `oc2_game`; JSON decoding uses the pinned jart/json.cpp through
the [shared admission wrapper](m1-content-runtime.md#json-and-operand-admission).

The implementation follows [data-format.md sections 2/4 and appendices A/B](content/data-format.md)
and [F03 sections 3–7](algorithms/F03-rules-and-configuration.md). It is one
semantic admission stage, not complete pack validation, a classic text parser,
an installed ruleset or an implemented gameplay consumer. Source receipts retain
the selected rules and compatibility descriptor's declaring packs and exact
hashes, including inherited selections.

## Identity and normalization

Rule arrays may be reordered without changing external identities. A complete,
unique mapping is required for every collection. The five advance IDs explicitly
named by the gameplay contract have registered identities: monarchy 54,
communism 15, fundamentalism 31, republic 71 and democracy 21. Other rule IDs
currently require `compatibility.ruleSlots`. There is no inference from display
names, array positions or plausible English spellings. An explicit mapping
cannot relocate a registered identity. Selected custom advance IDs can supply
those slots; the builtin gameplay references still bind to the fixed identities.
Other gameplay advance references must resolve to selected IDs.

Integer admission preserves exact tokens in the signed32 authoring domain. The
shared normalizers apply F03's clamps, narrowing and scaling once: food rows
round up to even, shield rows do not; unit movement uses the loaded road
multiplier; native HP 30 produces 30, through the nominal classic token 3.
Improvement cost/upkeep narrow to bytes, while unit cost clamps to 1…200.
Signed AI/trait values and signed consumer views retain their specified meaning.
Terrain defense requires an exact integral doubled factor; base work definitions
are required only for the eleven base terrains.

Advance enablement is recorded before reference fixup. Enabled rows remain
enabled if a prerequisite follows a disabled row to Never. Improvement references
receive that fixup; unit references and wonder expiry do not. Enabled cycles and
self-references are permitted. Names and government defaults retain their padded
versus ordinary empty-string distinction, and ordered tribe-title overrides keep
the last value for each government. Special terrain definitions carry no work
update; applying them must retain the old work fields. No reload policy, retained
leader state or city-name cursor is reset by decoding.

## Encoding and remaining comparison gap

Rule names have separate UTF-8 and reversible Windows-1252 representations.
The codec follows the Unicode Consortium's published
[CP1252 mapping](https://www.unicode.org/Public/MAPPINGS/VENDORS/MICSFT/WINDOWS/CP1252.TXT).
Undefined bytes, malformed UTF-8 and unrepresentable scalars fail. Name admission
checks the single-field 254-byte boundary, line terminators, NUL/control-Z,
classic token commas, edge trimming and advance/unit comment termination.
Difficulty and attitude lines retain embedded commas and semicolons. The full
serialized row plus newline must also fit its classic read boundary; that
combined-length/export check remains for the classic converter. This command
does not certify classic export representability.

**Specification gap:** F03/C01 prescribe case-insensitive byte comparisons for
section names, build-preference lookup and city-name matching. The native format
selects Windows-1252 but does not define its non-ASCII comparison table or locale
behavior. A serialization mapping alone cannot establish case equivalence.
Before native city-name/preference admission depends on that comparison, resolve
it in the behavioral contract or capture independent original-game observations,
including accented pairs and nonletters. Do not substitute Unicode case folding
or the current host locale. This stage performs no case-insensitive name lookup.

An [original Windows observation](m1-original-text-observations.md) selected
`@pRoBeÉ` for the pooled plural `ProbeÉ` in an ordered three-header CITY input,
and repeated the proposal after loading a C++ export. Duplicate equivalent
headers and section-index behavior prevent treating this as a complete or
isolated comparison test. Single-candidate controls and the broader byte/locale
domain remain open; no comparison algorithm is inferred here.

## Command and checks

```powershell
build/headless-fetch/oc2.exe content validate-rules path/to/content.json --json
build/headless-fetch/oc2.exe content validate-rules path/to/overlay/content.json --parent path/to/base/content.json --require-classic --json
```

The command accepts the same explicit parent mounts, locale selection and classic
gameplay gate as `content resolve`. Its report format is
`openciv2-rules-validation/1`, with status `validated` and scope
`rules-and-gameplay-references`. It includes source receipts and selected normalized
operands. Exit codes remain 0 for success, 1 for semantic rejection, 2 for
usage/source failure and 4 for malformed JSON. The deliberately incomplete
repository format example resolves but fails this rule-validation stage.

Eight portable Catch2 cases check identity order, normalization, exact integers,
reference fixup, names/titles, codec round trips and invalid inputs. Four native
CLI cases load independently authored complete definitions from real files and
check readonly behavior, inheritance, receipts and rejection. These are
specification-derived and integration checks, not original-game observations or
independent fixture review. No F03 operation is yet registered with X02.

Remaining M1 work includes city names/preferences, full component admission,
classic F03/C01 text adapters and conversion receipts, equivalent native/classic
input tests, retained-state installation and reload policies, owning mechanics,
session admission and original interoperability. The
[M1 completion audit](m1-validation.md) remains open.
