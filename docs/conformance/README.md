# Conformance inventory

M0 established the checked metadata. M1 now executes twenty-nine F01, seven F02 and
ten W01 [X02 version 3 fixtures](../algorithms/conformance/) through real C++
operations on native and WASM. The [bounded runner](../m1-foundations.md) checks
pure mappings, keyed sampling and policy replay, stateful token/reservation leaves
and scalar geometry. Composite operations and providers/reentry remain later M1 work. Metadata validation does not
execute gameplay.

```sh
python3 tools/check_conformance.py
python3 tools/check_assets.py
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

Use `python` on Windows. Python 3.9+ and the standard library suffice for the
metadata checks. The existing formatter tests additionally use pinned
clang-format. With Python available, CMake exposes `inventory-check` and CTest
metadata tests; normal builds and headless Catch2 tests do not require Python.
With testing and Python enabled, CTest also runs `conformance_foundations` and
writes `conformance-results.json`. Its test-only adapter links `oc2_game` and
`oc2_base`, never SDL. `conformance_sampler_rejections` additionally executes
negative input and watchdog checks against the compiled native/WASM adapter.

## Versioned registries

[registry.json](registry.json) schema 1, inventory 4 contains:

- All 65 specification families, exact file hashes, specification status and
  separate registration status. This denominator counts documents, not behavior.
- Six F01, three F02 and four W01 operation contracts, with typed
  arguments/results, state closure, preconditions, callbacks, stop/event boundaries,
  dependency edges and explicit
  empty independence claims. All have C++ implementations and executable
  adapters. `implementation: null` still means no implementation exists.
- Six F02 keys: two process token counters, the unit scan bound and its fixed
  entry token view, eight recorded military counts, and the effective-human mask.
  Three W01 keys cover dimensions and the current flat-world rule. Their type,
  lifetime, ordering and reference rules are registered. Full pool
  records and retained workspaces still need registrations. `F01.randomContexts`
  registers a complete reachable generator through policy v2 origin/position;
  it is omitted in controlled-choice mode. Sampling events are conditional on
  a nonconstant decision; their registered dependency edges apply when present.
- Forty-six fixture identities/revisions/hashes and their provenance: 39
  specification vectors and seven explicitly labeled implementation-policy
  regression vectors, with a passing [native report](reports/f01-native.json)
  and a separate [WASM report](reports/f01-wasm.json).
  The target executable hash in a vector is not proof of access to that image.

[coverage.json](coverage.json) uses X02 §8's required row fields and dispositions.
Rows own the thirteen registered operation contracts. `evidenceEntries` identify the
local specification path/hash and operation; no original function-address or
source-fragment inventory is claimed. Do not follow the specs' reconstruction
source links to fill these fields or extract/translate their harnesses into tests.
Historical source-reference sections are provenance only. Specification
clarification and independently captured original-game observations are the route
for missing behavior/evidence. A future independently supplied source-coverage audit
must remain distinguishable from this cleanroom operation inventory.

The ledger also records all G01–G23 integration families, with their required
comparisons and no executed cases. Each gap has a bounded domain, known prefix,
closure evidence and delivery milestone. A family description, registration,
implementation, supplied vector and successful execution are separate facts.
Never convert one into another or report a percentage over different denominators.

The sampler cases cover all four F01 laws in each ownership domain, constants,
reversed and wrapped ranges, endpoints, modulo bias, and replay at a known stream
position. Replay uses the standard engine vector documented in the
[random policy](../random-policy.md#portable-fixture-replay-contexts), not outputs
captured from this adapter. This does not supply independent fixture review or
an original-program observation. Context reconstruction is linear in the recorded
position and bounded by the host watchdog; it is not a general session-save codec.

The logical-value validator recognizes every X02 scalar tag, nested records and
ordered arrays, including exact f64 bits and bounded blob references. Only the
registered operation closures execute. Blob loading/hash resolution, scripted
providers/reentry, safe-stop prefixes, projections and independence comparisons
remain unsupported; accepting a tag does not imply those execution capabilities.

## Updating an operation

1. Read the owning repository spec. Register the actual arguments, result,
   complete shared state closure, aliasing, callbacks, failure/partial effects,
   stop boundaries, observation dependencies and justified independence domains.
   Add every shared state key once, with its lifetime/order/reference rules.
2. Add its coverage row with the X02 disposition, bounded domain, evidence,
   comparison surfaces and concrete unresolved gaps. Split mixed dispositions.
3. Author fixtures with independent provenance and pin their exact bytes and
   owning spec hashes. Do not relabel implementation snapshots as original goldens.
4. Set `implementation` to the actual repository entry file when it exists;
   retain the operation ID independently of source layout. Add its callable
   adapter and fixture validation; the current runner deliberately supports only
   the registered F01/F02/W01 leaf primitives and their bounded state closure.
5. Only set execution status and `lastRun` after an actual run. Store its report
   with implementation revision, fixture ID/revision/hash, spec hashes, policy,
   platform and bounded observations, following X02. Metadata checks validate
   fixture/spec/source identities, comparison scope, actual trace/result and
   input consumption in the referenced foundation reports (retaining their initial
   `f01-native.json` / `f01-wasm.json` filenames). Format version 2 also
   checks the adapter's embedded compiled-source/build identity; the runner
   rejects stale binaries before executing fixtures. Rebuild and rerun after source
   changes; never edit an expected vector to make an implementation pass.
6. Review spec/fixture changes before updating hashes. Bump `inventoryVersion`
   when changing the inventory denominator; schema changes bump `schemaVersion`.
   Run the checks above and the operation's Catch2/conformance tests.

The validator rejects stale specification/fixture bytes, duplicate JSON keys,
missing owners/anchors/state references/cases/gaps, cyclic event dependencies,
inconsistent implementation statuses and execution claims without an adapter.
These are bookkeeping checks, not a v3 gameplay fixture parser or compatibility
claim. Original interoperability and integration cases remain mandatory.
