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
or session admission. The separate installer described below applies admitted
rules to retained process state. Source receipts retain
the selected rules and compatibility descriptor's declaring packs and exact
hashes, including inherited selections.

The separate [classic text reader and preference adapters](m1-classic-text.md)
now supply F03's byte grammar, section services and native/classic city-production
preferences and all twelve classic rule-table readers.

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

## Retained rule installation

`formats::content::RuleInstaller` owns an immutable `load_rules` result and applies
it directly to `RuleState`, without virtual files or classic token serialization.
Its full load follows F03's twelve-table order and explicit leader-replacement
policy. The individual advance, improvement and unit methods also support
`keep_names`. Native identity IDs are installed independently of display-name
preservation. The shared pool must already be initialized by its owning setup.

The decoder now retains advance/improvement prerequisites before fixup, whole
movement points, and every ordered title override. The installer uses those
operands at the owning boundary: advance enablement precedes fixup, improvement
fixup reads the current advance table, and movement uses the current cosmic road
multiplier. Already normalized HP, costs and scalar codes are not scaled again.
These dependencies remain explicit without allocation-time callbacks.

Names use full-width local references and explicit custom-name flags. Preserved
leader fields require no discarded allocations; repeated title overrides retain
their final semantic value. Usage and city-name counters remain. Special terrain
rows retain existing work values, and order-name slot zero stays unchanged.

Rule admission precedes installation. The installer owns its admitted input and
does not invent event/reentry boundaries while storing text. Setup, full pack
admission, session reload and mechanic consumers remain separate owning work.

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
Before admission depends on an unresolved comparison, resolve it in the
behavioral contract or capture independent original-game observations, including
accented pairs and nonletters. The preference adapter admits comparisons resolved
by byte equality or ASCII folding and rejects other encountered high-byte pairs.
Do not substitute Unicode case folding or the current host locale. This rule
definition stage itself performs no case-insensitive name lookup.

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
CLI rule cases load independently authored complete definitions from real files and
check readonly behavior, inheritance, receipts and rejection. These are
specification-derived and integration checks, not original-game observations or
independent fixture review. No F03 operation is yet registered with X02.

Portable installation tests cover name preservation, custom markers, counters,
special terrain retention and final title overrides. A paired native/classic
test uses different preceding text allocations and compares resolved names and
decoded rules. Large-ID tests prevent signed-name aliasing. F06 codec tests
remain separate from live text identity tests. These are authored regressions,
not new original-program observations.

The [city-name providers](m1-city-names.md) now select by live plural name and
preserve C01's counter, fallback, retry and publication behavior on both paths.

Remaining M1 work includes full comparison/component admission,
conversion receipts, broader equivalent native/classic inputs,
application startup/GAME-cache construction, full content installation and session
reload policies, owning mechanics,
session admission and original interoperability. The
[M1 completion audit](m1-validation.md) remains open.
