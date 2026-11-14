# M1 initial foundations

M1 is underway, not complete. The subsequent [stored-save slice](m1-stored-saves.md)
adds civilization/global records and full current stored-file decoding, with
Windows validation; the Linux/WASM evidence below describes the earlier slices.
Implemented foundations include F01 numeric
primitives and isolated random contexts in standard-library-only `oc2_base`,
F02 entity records/pools and W01 map foundations in `oc2_game`, initial F06 codecs
in `oc2_formats`, and a bounded developer runner for nine registered X02 operations. No reconstruction
source, legacy harness, original executable or asset checkout is a code input.

## Implemented boundary

- F01 §1: explicit wrapping i32 addition/subtraction/multiplication, truncating
  signed division/remainder with exceptional-input rejection, both clamp rules
  and signed variable shifts including the minimum signed shift count.
  `WrappingI32` composes these helpers into typed expressions with explicit
  native-integer conversions.
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
- F02 §§2–3: separate slot/token identity types, process-owned unit/city token
  sequences and the unit-slot reservation prefix, including first-hole reuse,
  retained military counts and effective-human extension limits.
- F02 §§3,5–6,9: unit/city records and stable pools, deletion finalization,
  ordered lifetime renewal and shared mood-workspace dispatch. The F05 entity
  reset portions distinguish outer new-session invalidation from inner scan reset.
- W01 §§1–6,8: doubled-coordinate geometry, ordered neighborhood inputs,
  contiguous logical tiles, remembered/coarse layers, retained map state,
  allocation/release, spatial queries, coordinate features and primitive tile
  mutations with ordered synchronization effects.
- F06 §§2–4,7: explicit unit/city/tile/region/header codecs, raw-map stored views
  and export, ordered raw/embedded map item I/O and versioned entity item loading.
  The [record/codec handoff](m1-windows-handoff.md) documents the implemented
  boundary, external original-map comparisons and remaining save/session work.

The nine registered adapters produce actual entry/return/exit traces.
`oc2_conformance_adapter` is a test executable, not a save-inspection CLI or game
console. It receives only operation names and canonical decimal arguments; it
never reads expected results. C++ implementations are identical under native
and Emscripten builds.

## Wrapping integer expressions

`openciv::base::WrappingI32` in `base/arithmetic.hpp` owns a signed 32-bit value
and defaults to zero. Explicit construction from an integer reduces modulo
2^32 and takes the signed view, including for wider inputs. Floating-point and
boolean construction are rejected. Explicit casts to `std::int32_t` and
`std::uint32_t` expose the signed value or unsigned bit view respectively.

```cpp
using openciv::base::WrappingI32;
const auto result = (WrappingI32{value} * WrappingI32{multiplier}
                     + WrappingI32{adjustment}) / WrappingI32{divisor};
const auto stored = static_cast<std::int32_t>(result);
```

Addition, subtraction, multiplication and unary negation wrap at each operation;
comparisons are signed. Division and remainder truncate toward zero and retain
the helpers' `std::domain_error` for zero divisors or `INT_MIN / -1` (including
remainder). Compound assignments have the same semantics and leave their target
unchanged on failure. Mixed native-integer arithmetic/comparisons require an
explicit conversion; there is no implicit boolean conversion.

The overloads `clamp_ab`, `clamp_c` and `shift_signed` take and return wrapped
values, preserving reversed bounds and the signed count convention. No shift
operators are provided. Existing raw-integer helpers and floating-point sampling
remain available. Apply the type where an algorithm specifies i32 arithmetic;
IDs, container sizes and narrower conversion boundaries keep their own contracts.
Its object representation is not a file or wire encoding.

## F02 identity and reservation boundary

`state/identities.hpp` defines distinct civilization/unit/city slots and unit/city
lifetime tokens. Zero tokens mean free records; any nonzero token is occupied,
including values with the high bit set. `ProcessTokens` owns separate u32 counters
starting at 1. Assignment returns the current counter and increments modulo 2^32,
without skipping zero or searching for collisions. A process owner retains this
object across games; independent peers own separate objects. Other process state
and the actual new-game/load lifecycle remain unimplemented.

`reserve_unit_slot` accepts a read-only token view of exactly `[0, unit_end)` at
entry, the mutable exclusive scan bound, owner, recorded i16 military count, and
effective-human mask. It first reuses the lowest free slot. Only extension checks
the 2048-slot capacity, military count greater than 1948, and nonhuman bound 2040.
The return is an optional `UnitSlot`; rejection leaves the bound unchanged.
Invalid owner/view/bound inputs throw before mutation. The primitive has no RNG,
clock, database or presentation dependency.

Reservation does not assign a token, clear stale payload, mark a hole occupied,
update counts or emit a limit notice. Callers own the full pool and must rebuild
the entry view after changing its bound. The X02 `F02.unitTokenScan` key is this
fixed entry read window, so its exit array retains the same length even when the
bound extends. Newly covered payload is neither read nor initialized here.
The complete registered closure also includes all eight recorded military counts
and the effective-human set, with unchanged values compared at exit. Counter
fixtures preserve the other process counter explicitly.

Seven new specification-derived fixtures cover initial/token-wrap/zero assignment,
independent city assignment, first-hole reuse, extension and military-count refusal.
Compact reservation fixtures label their retained-summary inputs accepted-off-nominal;
their recorded counts deliberately exceed the small scanned populations.
Catch2 additionally covers the 2039/2040/2047/2048 bounds, both sides of 1948,
effective-human bits, negative retained counts, invalid views and a caller-driven
scan reset retaining records and token positions. These are leaf-operation tests,
not full U01 creation, C01 city reservation, deletion or F06 token-remapping tests.

The subsequent record slice adds five pool/workspace and eleven codec/I/O Catch2
cases. These cover deletion finalization, entity reset portions, current/legacy
lifetime renewal, full unit/city payloads, explicit little-endian field vectors,
retained bytes, truncated records and short-item publication prefixes. Full
creation/deletion policy, civilization/global records and session load remain open.

## W01 geometry and map boundary

`world/geometry.hpp` provides rectangular lookup, canonical row-order coordinates,
single-correction horizontal/coarse wrapping, step and weighted distances,
sign-based direction selection, half-open spans/rectangles and ordered neighbors.
It accepts positive even signed-16 widths, positive signed-16 heights and an area
below 32768. This is the declared backing domain, not a claim about F05/F06
acceptance of new-game or malformed file dimensions. Scalar query coordinates
retain the full i32 domain and Appendix A's wrapping reductions.

Lookup deliberately aliases noncanonical parity without changing the coordinates
used by other predicates. Topology is supplied from the current rule on each
geometry call, independently of a stored file header. Neighborhood tables are
explicit mutable inputs; order, duplicates, altered offsets and center positions
are retained. None of these operations accepts a random context or clock service.

`world/map.hpp` owns contiguous C++ tile records, remembered-improvement layers
1..7, four coarse grids and separately retained region summaries/seed/header
topology. Codecs must encode logical fields explicitly. Civilization-0 memory
and the shared mutable fallback are supplied separately; allocation/release
preserves them. Fresh tiles are Ocean **owned by civilization 0**, not Unowned.
Coarse cells remain explicitly uninitialized until a producer writes them;
reading one throws rather than inventing a zero value. Direct memory/coarse
access checks the computed linear index against actual backing, without adding
a rectangular-coordinate check, fallback or implicit wrap.

Allocation publishes dimensions, tiles, memory layers and coarse grids in order,
setting the allocated flag last. A failure service can interrupt each allocation;
preceding publications survive. Replacement retains old backing until the map
owner is destroyed, because allocation does not free a preceding map. Normal
setup/load callers should release before replacement. Release follows W01's
coarse-3-to-0, tiles, memory-1-to-7 order and clears the flag last; it does nothing
when the flag is clear, including after a failed initial allocation. Accesses
after partial replacement reject unavailable backing before indexing it.

Spatial reads preserve stored owner, region, feature and revelation semantics.
Resource/hut/shield predicates use the supplied coordinates and saved seed;
negative intermediate pattern coordinates use floor division. Primitive edits
retain unrelated fields and derived state. Synchronization is a typed semantic
effect after mutation: client batching takes precedence over immediate sending,
host batching does not take the client path, and unchanged fields emit nothing.
Coordinates and operands remain unreduced in effects except for normalized owner.
A publisher failure retains the completed mutation. N02 wire encoding and
session authority remain separate future integrations.

Eleven Catch2 cases exercise these contracts, retained state, allocator failure,
fallback/parity aliasing, unsupported backing, sync routes and callback failures.
Ten new X02 vectors cover wrap, step/weighted distance and nullable tile indices,
with the dimensions/current topology included in the registered state closure.
The expectations come from W01 §§2,4,9 and Appendix A, not implementation output
or original-game captures. Region labeling/border rebuilding, the treaty-based
foreign-radius query, bulk field adapters and complete W03 memory policy remain
unimplemented; this slice does not claim all of W01 or its gameplay consumers.

## X02 runner scope

`tools/run_conformance.py` is a Python-standard-library developer host driver;
it invokes the adapter directly or through Node and compares its JSON output.
The game library, ordinary builds and Catch2 tests have no Python dependency.
The driver does not implement the tested arithmetic in Python.

The `f01-f02-w01-state-v1` scope validates exact X02 v3 structure, target/specification
hashes and evidence anchors, registered argument types/preconditions and the exact
scalar/ordered-array state closure, canonical integers, typed booleans, mandatory trace identities
and dependencies. It compares typed results, absence and complete registered state,
matches events by identity, and checks chronological dependency edges. Unknown fields, duplicate keys,
stale pins, omitted edges and unconsumed declared inputs cannot produce a pass.
Extensions at the fixture root are metadata only. The adapter subprocess obeys
the fixture's host-time watchdog; insufficient event budgets fail before entry.

Unregistered state types/blobs, supplied children, provider/reentry scripts,
semantic choice tapes, implementation-replay and legacy fixtures, projections/normalizations,
independence declarations and non-return outcomes are explicitly unsupported.
They are not silently discarded or treated as successful comparisons. General
typed values, stop-prefix capture and those features remain `x02-general-runner`
work. F01 code arguments in the three existing vectors are controlled inputs,
not seeds or a test of PRNG quality.

Stateful adapter inputs use canonical numeric fields on stdin in the registered
protocol order. This avoids command-line size limits and carries no expected
result or snapshot. Typed fixture validation precedes execution; the C++ adapter
also rejects missing, out-of-range and trailing state input. The original three
F01 and seven F02 fixtures and their expectations are unchanged. Boolean inputs
use exact JSON true/false in fixtures and 0/1 only in the internal adapter protocol.

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

Remaining F02 world/civilization/process lifecycle, complete setup/load integration, content codecs,
scoped queries, save inspection/SQLite export, session/continuation contracts and networking
remain M1 work. These primitives alone cannot play a turn, load a save, restore
a pending interaction or establish original-game interoperability. The
[M1 completion audit](m1-validation.md) preserves the entire outstanding exit gate.

## Local validation — 2026-11-13 UTC

Linux Clang 20.1.8 / CMake 3.31.10 passes all 50 headless CTests: 47 Catch2 cases,
two metadata checks and execution of the twenty X02 vectors. A separate ASan/UBSan
build passes the same 50 tests with leak detection enabled (outside the local
ptrace sandbox). Emscripten 4.0.23 / Clang 22.0.0 passes four CTests under Node:
the 47 Catch2 cases, two host metadata checks and the twenty adapter vectors.
The Windows/macOS CI jobs are configured to run the new tests, but these M1
changes have not yet run on those hosts.

The library-only build also passes with GUI, tests, Python discovery and
dependency fetching disabled and an intentionally missing asset directory.

All 99 Python tests and pinned clang-format checks pass. Negative cases include
forged/stale execution evidence, bad integer encodings, duplicate JSON keys,
unsupported state/providers/normalization, missing dependencies, reordered or
duplicated events, mismatched results, adapter failure, stale or missing embedded
build identities, source changes during execution, incomplete/ill-typed state,
changed slot order, absence versus slot/tile zero, invalid geometry dimensions,
untyped boolean values and watchdog exhaustion.
The unchanged WASM Catch2 macros still emit their upstream `__COUNTER__` warnings.
