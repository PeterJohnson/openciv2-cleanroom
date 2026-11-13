# M1 initial foundations

M1 is underway, not complete. The first slice implements F01 numeric primitives
and isolated random contexts in standard-library-only `oc2_base`, plus a bounded
developer runner for the two registered X02 pure operations. No reconstruction
source, legacy harness, original executable or asset checkout is a code input.

## Implemented boundary

- F01 §1: explicit wrapping i32 addition/subtraction/multiplication, truncating
  signed division/remainder with exceptional-input rejection, both clamp rules
  and signed variable shifts including the minimum signed shift count.
- F01 §§3–4: primary-code modulo bias and the exact binary64 secondary unit/range
  mapping, including reversed, equal and accepted wrapped intervals. Separate
  rounding points and disabled contraction preserve the specified evaluation;
  non-nearest host rounding is rejected before a sampled operation advances.
- F01 §5: pure millisecond-to-game-tick and tick-to-map-pattern conversions.
  The library does not read a clock or decide when to reseed a map.
- F01 §§2,6 and the architecture: [versioned random policy](random-policy.md)
  using `std::mt19937_64`,
  explicit seeds, distinct gameplay/presentation-content/cosmetic context types,
  in-memory checkpoints, repeat/restore and isolation tests. No frontend links
  a gameplay context; real mechanics and semantic presentation consumers are
  still future work.

The two registered mapping adapters produce actual entry/return/exit traces.
`oc2_conformance_adapter` is a test executable, not a save-inspection CLI or game
console. It receives only operation names and canonical decimal arguments; it
never reads expected results. C++ implementations are identical under native
and Emscripten builds.

## X02 runner scope

`tools/run_conformance.py` is a Python-standard-library developer host driver;
it invokes the adapter directly or through Node and compares its JSON output.
The game library, ordinary builds and Catch2 tests have no Python dependency.
The driver does not implement the tested arithmetic in Python.

The `pure-f01-v1` scope validates exact X02 v3 structure, target/specification
hashes and evidence anchors, registered argument types/preconditions and empty
state closure, canonical integers, mandatory trace identities and dependencies.
It compares typed results and complete empty state, matches events by identity,
and checks chronological dependency edges. Unknown fields, duplicate keys,
stale pins, omitted edges and unconsumed declared inputs cannot produce a pass.
Extensions at the fixture root are metadata only. The adapter subprocess obeys
the fixture's host-time watchdog; insufficient event budgets fail before entry.

Nonempty state/blobs, supplied children, provider/reentry scripts, semantic choice
tapes, implementation-replay and legacy fixtures, projections/normalizations,
independence declarations and non-return outcomes are explicitly unsupported.
They are not silently discarded or treated as successful comparisons. General
typed values, stop-prefix capture and those features remain `x02-general-runner`
work. F01 code arguments in the three existing vectors are controlled inputs,
not seeds or a test of PRNG quality.

Reports retain raw fixture hashes/revisions, target/spec/evidence identities,
implementation revision plus exact source hashes and dirty status, adapter
binary hashes, build identity, actual traces, consumption, comparison scope,
limits and first mismatch. The fixture expectations remain unchanged. Retained
[native](conformance/reports/f01-native.json) and
[WASM](conformance/reports/f01-wasm.json) reports describe the tested working-tree
sources by hash; their base commit is not a claim that uncommitted code was in
that commit. The registry points to the native report, and metadata checks
reject a changed implementation or a recorded pass that disagrees with a fixture.

Report format `openciv2-conformance-run/2` binds compiled-source hashes and the
build identity to a manifest embedded in the adapter during its build. Before
executing fixtures, the runner queries `--identity` and rejects missing or stale
manifests. An optional `--build-identity` file must match that embedded identity;
it cannot relabel an older executable. Python driver hashes describe the driver
actually run, separately from the compiled inputs. Source or adapter changes
during a run prevent publication of its report. CMake regenerates the manifest
when its declared compiled inputs change; rebuild before rerunning conformance.

## Running and extending

```sh
cmake --preset headless-fetch
cmake --build --preset headless-fetch --parallel 4
ctest --preset headless-fetch --output-on-failure
python3 tools/run_conformance.py \
  --report build/headless-fetch/conformance-results.json \
  --build-identity build/headless-fetch/build-identity.txt \
  --adapter build/headless-fetch/oc2_conformance_adapter
```

On Windows use `python` and the `.exe` executable. For WASM pass the pinned
Node executable and adapter `.js` file after `--adapter`. `--fixture` selects
individual repository-bundle cases; absent that option all current vectors run.
Exit status is 0 only when every selected case passes, 1 for case failures,
invalid/unsupported cases or budget exhaustion, and 2 for command/setup errors.
CTest writes a fresh report in its build directory; CI uploads it with the test
evidence. Updating the retained reports is an explicit reviewed step after an
implementation change, not automatic regeneration of expected results.

F02 pools/process lifetimes, W01 spatial state, content codecs, scoped queries,
save inspection/SQLite export, session/continuation contracts and networking
remain M1 work. These primitives alone cannot play a turn, load a save, restore
a pending interaction or establish original-game interoperability.

## Local validation — 2026-11-13 UTC

Linux Clang 20.1.8 / CMake 3.31.10 passes all 15 headless CTests: 12 Catch2 cases,
two metadata checks and execution of the three X02 vectors. A separate ASan/UBSan
build passes the same 15 tests with leak detection enabled (outside the local
ptrace sandbox). Emscripten 4.0.23 / Clang 22.0.0 passes four CTests under Node:
the 12 Catch2 cases, two host metadata checks and the three adapter vectors.
The Windows/macOS CI jobs are configured to run the new tests, but these M1
changes have not yet run on those hosts.

A fresh library-only build also passes with GUI, tests, Python discovery and
dependency fetching disabled and an intentionally missing asset directory.

All Python tests and pinned clang-format checks pass. Negative cases include
forged/stale execution evidence, bad integer encodings, duplicate JSON keys,
unsupported state/providers/normalization, missing dependencies, reordered or
duplicated events, mismatched results, adapter failure, stale or missing embedded
build identities, source changes during execution and watchdog exhaustion.
The unchanged WASM Catch2 macros still emit their upstream `__COUNTER__` warnings.
