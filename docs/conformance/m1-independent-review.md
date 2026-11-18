# Reference fixture review

This dated review covers the reference fixture set, including its seven MT replay
vectors. The active C# selection uses seven independently generated PCG32 variants;
see [the selection contract](README.md). This review establishes fixture reasoning,
not execution of a current application binary.

Reviewer: **agent `/root/m1_fixture_review`**, acting solely as reviewer, not
author of these expectations. Review date: **2026-11-14**. Reviewed fixture revision:
`2827676d4e50ea0eea09a355baca8a49d6ce81b3`.
The fixed scope is the 57 fixture IDs and 15 operation IDs from `b125df5`.

Disposition: **review complete; all 57 fixtures accepted within their stated
scope after correction verification**. The initial review accepted 55 fixtures
and requested two domain-label corrections; both findings are now closed.
No numeric, retained-state, supplied-action or stop-prefix
expectation mismatch was found. This disposition does not close other M1 gates
or replace final native/WASM execution reports.

## Findings

| Finding | Fixture | Required correction |
| --- | --- | --- |
| M1-03-R1 | `X02.F01.sample-wrapped-range`, revision 6, SHA-256 `10cb55636e4db2f4e5af5bee5af44aeaa7fa3098df55496550b2b13f177a4973` | Change `profile.domain` from `nominal` to `accepted-off-nominal`. Endpoints -2147483648 and 1 have mathematical difference 2147483649. |
| M1-03-R2 | `X02.F01.sample-rounded-endpoint`, revision 6, SHA-256 `b0a13a809dcb056a49067116b44f77299f7c7d553e4d8c6159de555bb057a885` | Change `profile.domain` from `nominal` to `accepted-off-nominal`. Endpoints -2147483648 and 0 have mathematical difference 2147483648. |

[F01 §4](../algorithms/F01-determinism-and-arithmetic.md#4-secondary-sampling-law)
defines nominal intervals as having mathematical endpoint difference at most
2147483647. Both fixtures deliberately exercise the accepted wider interval
wrap. Their expected results, respectively 1639699275 and 1, are correct. The
pure `X02.F01.wrapped-range` fixture already uses the appropriate off-nominal
classification. Increment the corrected fixture revisions and refresh their
registry/inventory hashes and affected execution reports without changing the
numeric expectations. The original revision 6 findings and hashes remain in the
inventory's per-fixture `reviewHistory`.

### Correction verification

The same independent reviewer verified both working-tree corrections on
2026-11-14 against `2827676`. Each fixture is now revision 7 with
`profile.domain` set to `accepted-off-nominal`. A full parsed-document comparison
confirmed that **only revision and domain changed**: arguments, randomness,
expected results/state/traces, exclusions and provenance are unchanged. The
registry revision/hash entries match the independently computed file hashes.

| Closed finding | Corrected revision | Corrected SHA-256 |
| --- | --- | --- |
| M1-03-R1 | 7 | `3a6b78a03cb37f27b7b9ea82182ea300b748e30b49e072e9d67f732100ebd340` |
| M1-03-R2 | 7 | `11b78129e91cbd576ea43675905a30b7fb887e0269bb6b9fb82aef36877c1946` |

These corrections are reviewed as working-tree changes based on the candidate,
not as a newly committed revision. Final native/WASM report refresh remains
separate from this independent disposition.

## Method and evidence

The [per-fixture inventory](m1-review-inventory.json) contains a separate domain,
expected-value derivation, provenance assessment, excluded-state assessment and
disposition for **each of the 57 fixtures**. All packet fixture hashes and all
cited specification/evidence file hashes matched during this review. Stable
trace IDs, ordering edges and final snapshot/state agreement were checked across
the packet. The reviewed operation set is unchanged.

The reviewer read the owning repository behavioral contracts and the declared
registry boundaries. No production implementation output, passing conformance
report, reconstruction/decompilation source, external source link or historical
source-derived harness was used to derive an expectation. Independent one-off
Node calculations, authored during this review directly from the contracts,
checked 39 F01/W01 numeric cases and 10 F02 token/reservation/fresh-state cases.
The eight shared-workspace scripts were traced through their explicit inputs,
nested calls, output publications and stopping boundaries. These review
calculations were not added as production code or tests.

| Group | Count | Independent basis and result |
| --- | --- | --- |
| F01 pure mappings | 3 | F01 §§1,3,4: equal-bound result, modulo bias and wrapped range. Values agree. |
| F01 controlled sampling | 19 | Explicit semantic codes in all three declared domains; maximum/zero unit endpoints, reversed/equal/wide ranges, singleton and large divisor. Binary64 bits and exact event code/results agree. Two domain labels were corrected and independently rechecked. |
| F01 implementation replay | 7 | Recalculated FNV domain/name hash xor supplied seed to 5489 for each context. The repository random policy documents standard output 9981545732273789042 at position 10000; independent projections give primary code 17730 and secondary code 2324009717. F01 mappings and expected binary64 bits agree. Constant results retain position 9999; nonconstant results advance to 10000. |
| F02 lifetime tokens | 4 | F02 §3: return old token, increment modulo 2^32, no skip-zero behavior; preserve the other sequence. Values agree. |
| F02 reservation | 3 | F02 §3: first hole bypasses extension limits; 1948 permits extension and 1949 rejects it; only the bound changes on successful extension. Values agree. |
| W01 geometry | 10 | W01 §§2,4 and its explicit off-nominal scalar arithmetic: single correction, retained INT_MIN absolute value, signed shift, weighted re-absolute value, rectangular lookup/parity alias. Values agree. |
| F02 shared workspace | 8 | F02 §9, C03 full-entry boundary and X02 scripted actions: absence/cached dispatch, signed32 wrap, publication, failure without rollback, nested shared state and incomplete-provider stops. Values and prescribed trace prefixes agree. |
| F02 fresh-game projection | 3 | X01 fresh-game isolation, F02 §2 and registry projection: reset all supplied capacity, even outside empty scan bounds; preserve preferences/tokens; revoke old client. Empty goal role is -1 (encoded ff), consistent with A01's declared role definition. Values agree. |

The seven replay fixtures retain **implementation-regression** provenance.
Reviewing their derivation from the repository-documented standard vector does
not turn them into original-game captures or original PRNG trajectory evidence.
The other 50 retain **specification-vector** provenance. Controlled cosmetic
sampling is a declared local policy test, not a claim that X01 mandates the
original game's cosmetic choices. Finite code examples do not prove a sampler's
full probability distribution.

## Preserved limits

`F02.cityCalculationEntry` covers the registered shared workspace with explicitly
supplied `C03.fullCityRecalculation` responses. Its callbacks and observations
exercise the supplied schedule; they establish neither real C03 gameplay nor
where the normal pipeline reenters. In particular, the two unresolved cases
establish retained effects and an honest stop, not completed city calculation.
No finding asks to remove or weaken those disclaimers.

`F02.prepareNewGame` remains a **quiescent foundation projection**. Preserve all
three exclusions verbatim: `F02.otherFreshWorldState`,
`F02.otherFreshProcessState`, and `F02.otherFreshSessionState`. The cases observe
the listed full-capacity payloads, goals, counters, text/fallback fields,
preferences and old-client validity. They do not cover arbitrary pending work,
transport-output rejection, all RNG/session identities, all rule/text/world
state or playable-game setup. These limits are compatible with the fixed M1-03
scope and must accompany any acceptance statement.

The eight integration evidence families are mapped in
[m1-conformance-review.md](../m1-conformance-review.md). This independent review
does not rerun or certify those tests, original-file interoperability, or the
platform matrix. Final execution evidence remains the coordinating agent's work.
