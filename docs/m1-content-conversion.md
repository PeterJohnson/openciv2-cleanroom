# M1 classic content conversion

M1-02 requires three component conversions. CITYPREF is implemented here; RULES
and CITY remain open. These outputs are not complete playable packs. The owning
contracts are F03 section 8, the native city-preference schema and the finite
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
