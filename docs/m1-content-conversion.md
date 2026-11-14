# M1 classic content conversion

M1-02's three component conversions pass at `d65e659`, with the unchanged portable
core also tested on WASM at `597dec1`. These outputs are not complete playable packs. The owning
contracts are F03 section 8, C01 default naming, the native component schemas and the finite
[M1-02 acceptance matrix](m1-closeout.md#m1-02-content-and-conversion-matrix).

## CITYPREF

```text
oc2 content convert-preferences CITYPREF_SOURCE CONTENT_MANIFEST NEW_OUTPUT [--parent MANIFEST]...
```

The source is explicit Windows-1252 text. The manifest and explicitly mounted
parents select rule identities, names and classic slots. Conversion uses the
existing classic reader, emits `openciv2-city-preferences/1`, and admits that
output through the existing native validator before publishing it. The command
requires a new destination and uses atomic no-overwrite publication. Inputs are
preserved; an existing destination or source alias fails.

The JSON receipt on stdout uses `openciv2-content-conversion/1`, conversion
`classic-city-preferences/1`, and includes the selected profile, source/output
SHA-256 hashes, selected pack and rule/mapping receipts, gameplay semantic hash,
unsupported/lossy field arrays and limitations. Status is `converted` only after
publication succeeds. Rejection includes a diagnostic and nonzero exit status.
Receipt-output failure also returns failure, even if the component was published.

The component preserves the ordered active prefix, duplicates, first matching
improvement slot and retained NoDefend behavior. Absent NODEFEND emits `unchanged`;
presence emits `deprioritize`. The classic 32-entry limit and blank/comment/section
terminators remain effective. Formatting and unused text are not exported.
An unrecognized ordinary list entry is diagnosed rather than silently discarded;
the separate classic loader still accepts its established stop-at-miss behavior.
Undefined Windows-1252 bytes and unsupported controls reject. Non-ASCII exact
byte identity is supported; unresolved case equivalence remains an explicit
`comparison-unavailable` diagnostic.

`tests/content_conversion_tests.cpp` compares authored classic and converted
native updates, including retained list tails and NoDefend values, duplicates,
32-entry limits, missing sections, terminators, reversible accented text and
invalid references/identities. `tests/rules_cli.py` checks publication receipts,
native validation of the emitted file, source preservation, overwrite/alias
rejection and output failure. These are specification-derived checks, not
independent original-game observations.

Validation on Windows MSVC and pinned WASM/Node: four conversion cases with
41 assertions each pass; all 562 native CTests and seven WASM CTest targets pass
(538 portable cases, 206,224 assertions). The rules CLI suite passes twelve tests.
All 57 conformance observations and consumed prefixes match between native,
WASM and the prior committed reports. Local logs are
`build/conversion-{native,wasm}-{build,ctest,vectors}.log`; retained conformance
reports contain the adapter source hashes. The final M1-06 matrix is still open.

## CITY

```text
oc2 content convert-names CITY_SOURCE CONTENT_MANIFEST NEW_OUTPUT [--parent MANIFEST]...
```

The same explicit mounts, native admission, no-overwrite publication and receipt
rules apply. Conversion/profile identifiers are `classic-city-names/1` and the
selected content profile. The result is `openciv2-city-names/1` with source-spelled
plural selectors, ordered names and explicit null/empty/populated fallback.
It uses F03's actual CRLF translation, trimming and 254-byte reads. It preserves
duplicate names and names that happen to be other section headers; C01 does not
stop at an unrelated header or skip comment lines. EXTRA is retained as both the
fallback and a possible live plural selector. Repeated structural STOP markers
retain the first selector occurrence, matching a fresh classic open; other
duplicate selectors reject as ambiguous. No directory fallback source is inferred.

The native validator checks base-list completeness against selected rules, text
encoding, selector ambiguity and identities. A list that reaches EOF before the
255-entry cursor bound without a blank/STOP terminator rejects: the current
classic provider's exhausted-stream error cannot be represented as a native
exhausted list. EXTRA fallback may end at EOF, since its exhaustion requires no
further section open. A terminal STOP or EOF-ended EXTRA marker with unsupported
live-plural behavior is reported in the receipt's unsupported fields instead of
inventing a custom-plural list for it; the EXTRA fallback itself is preserved.
If that omitted list is required by a base's selected rules, validation rejects.
The classic reader/provider APIs and their declared error behavior are unchanged.

Portable tests compare all 256 cursor values for every starting tribe against
the classic provider and authored expectations, with duplicate/reversible names
and missing, empty and populated fallback. They also compare admitted STOP/EXTRA
selectors, test 254-byte read boundaries, unknown non-ASCII comparison, duplicate
selectors, incomplete base content and malformed input. CLI tests validate the
published component and its hashes, preserve sources, and exercise ambiguity,
overwrite/alias rejection and output failure.

CITY validation passes all 565 Windows native CTests and seven WASM targets
(541 portable cases, 290,481 assertions). The combined conversion tests pass
seven cases with 84,298 assertions; the rules/content CLI suite has fourteen tests.
Logs are `build/city-conversion-{native,wasm}-{build,ctest}.log`. The 57-vector
conformance execution and metadata checks pass on both platforms; their adapter
implementation/build inputs are unchanged from the retained CITYPREF reports.

## RULES

```text
oc2 content convert-rules RULES_SOURCE NEW_RULES NEW_COMPATIBILITY
```

Both destinations must be new, distinct from each other and from the source.
The command checks both before writing and publishes each through the existing
atomic no-overwrite writer. It prints a receipt with per-output `published`
flags. Failure before either publication reports `rejected`; if the first output
was published but the second fails, it reports `partial` and returns failure.
The published first output remains identified by its exact path and hash. Source
files and pre-existing destinations remain unchanged. Success requires both
outputs and receipt delivery. No complete-pack manifest is generated.

`formats::content::convert_rules` accepts an explicit source and comparison
profile. It reads all twelve F03 tables through the existing classic loader and
emits separate `openciv2-rules/1` and `openciv2-classic-compatibility/1` bytes.
Stable IDs use `classic.<lowercase collection>.<zero-based row>`; the mapping
records the actual external slot, including ORDERS slots 1–12. Names never define
identity. Short advance/unit tables materialize their classic defaults.

The parser optionally captures the installation operands a final snapshot loses:
pre-fixup prerequisites, unscaled movement and ordered title overrides. The
converter retains these, emits final HP once, preserves the defined ability bits
and records bit 15 in compatibility metadata. Other normalized scalar values
are idempotent native inputs. Unsupported numeric enum codes, HP results outside
the native domain and malformed classic references reject. Classic loading
without capture keeps its existing domain and behavior.

Before returning bytes, the core runs the native validator and installer and
requires equality of all resolved rule definitions with the classic result.
The receipt identifies conversion `classic-rules/1`, source and both output
hashes/formats, profile, builtin gameplay hash used for validation and explicit
unsupported/lossy arrays. This is component conversion, not a playable pack.

Portable tests compare all twelve tables with the independent authored native
fixture, and pair retained-name reloads against the classic loader after changing
the live road multiplier, advance availability and government titles. They cover
raw movement wrapping, once-only HP normalization, short-table defaults, reversible
text, ordered duplicate title overrides, retained ability bits and rejection.
CLI tests additionally admit the emitted files through `validate-rules`, verify
both hashes and publication flags, preserve the explicit source, refuse existing
outputs and source/output aliases, and check malformed input plus first/second
output failures.

Core validation passes five focused RULES/capture cases with 77 assertions on
Windows and WASM, all 570 native CTests, and seven WASM targets (546 portable
cases, 290,558 assertions). The 57 conformance observations and consumed prefixes
remain identical across platforms and prior reports. Local logs are
`build/rule-conversion-{native,wasm}-{build,ctest,vectors}.log`.

## M1-02 acceptance evidence

| Requirement | Authoritative implementation and checks |
| --- | --- |
| RULES, all twelve tables and slot mapping | `rule_conversion.cpp`; complete independent classic/native fixtures in `content_conversion_tests.cpp`; native validator and installer compare every resolved definition |
| Once-only scaling, fixup and retained-name reloads | RULES reload case changes the live road multiplier, advance availability and government titles; `classic_rules_tests.cpp` checks pre-fixup capture, duplicate overrides and capture reuse |
| CITY sequences, duplicates, wrap, missing/exhausted fallback | CITY portable cases compare all 256 cursor values for 21 starting selectors and admitted STOP/EXTRA selectors, including null, empty, populated and EOF-ended fallback |
| CITYPREF order, duplicates, first match and retained NoDefend | CITYPREF portable cases compare active and retained tails, NoDefend 0/7, duplicate names, first matching slot and 32-entry boundary |
| Reversible text and explicit comparison gaps | Accented Windows-1252 names/selectors; undefined bytes and unknown non-ASCII folding reject, without inventing a locale table |
| Invalid identities/references, ambiguous selectors and malformed inputs | Native mapping validation, classic reference/enum/HP rejection, repeated CITY selectors, incomplete base lists, NUL/control/undefined encoding cases |
| Explicit inputs, new outputs and receipts | `content_convert.cpp`; seventeen `rules_cli.py` tests cover native admission of all emitted formats, SHA-256 receipts, unchanged inputs, aliases, overwrite rejection and output failure/partial publication |
| Frozen regression surface | `d65e659`: all 570 Windows native CTests pass, including content/metadata and conformance. Portable implementation unchanged from `597dec1`: seven WASM targets, 546 cases/290,558 assertions; all 57 vector observations/prefixes equal native and prior reports |

Final CLI logs: `build/conversion-cli-native-{build,ctest}.log`. These authored
semantic/CLI checks pass M1-02; they do not substitute for M1-03 independent
fixture review, M1-04 original-file execution or M1-06's final platform matrix.
