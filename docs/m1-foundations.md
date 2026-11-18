# M1 initial foundations

M1 is complete within its [finite scope](m1-validation.md). The C# foundations
support the [stored-save codecs](m1-stored-saves.md), [inspection CLI](m1-inspection.md),
[gameplay content loading](m1-content-runtime.md) and [fresh-game boundary](m1-fresh-game.md).
Execution evidence is recorded in [validation status](validation/README.md).
Implemented foundations include F01 numeric
primitives and isolated random contexts in `OpenCiv2.Base`,
F02 entity records/pools and W01 map foundations in `OpenCiv2.Core`, initial F06 codecs
in `OpenCiv2.Formats`, and a bounded developer runner for fifteen registered X02 operations. No reconstruction
source, legacy harness, original executable or asset checkout is a code input.

## Implemented boundary

- F01 §1: explicit wrapping i32 addition/subtraction/multiplication, truncating
  signed division/remainder with exceptional-input rejection, both clamp rules
  and signed variable shifts including the minimum signed shift count.
  `WrappingI32` composes these helpers into typed expressions with explicit
  native-integer conversions.
- F01 §§3–4: primary-code modulo bias and the exact binary64 secondary unit/range
  mapping, including reversed, equal and accepted wrapped intervals. Separate
  rounding points preserve the specified evaluation under the normal .NET binary64 environment.
- F01 §5: pure millisecond-to-game-tick and tick-to-map-pattern conversions.
  The library does not read a clock or decide when to reseed a map.
- F01 §§2,6 and the architecture: [versioned random policy](random-policy-pcg32.md)
  using PCG32 behind an owned, mockable raw-source interface,
  explicit seeds, distinct gameplay/presentation-content/cosmetic context types,
  in-memory checkpoints, keyed controlled choices and bounded semantic traces,
  repeat/restore and isolation tests. No frontend links
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

The fifteen registered adapters produce actual entry/return/exit traces,
conditional random events for the four semantic sampling operations, and ordered
provider/reentry/stop traces for F02's shared workspace entry with a supplied C03 child.
`OpenCiv2.ConformanceAdapter` is a test executable, not a save-inspection CLI or game
console. It receives operation names and canonical typed input encodings; it
never reads expected results. C# implementations are identical under native
and browser WebAssembly builds.

## Wrapping integer expressions

`OpenCiv2.Base.WrappingI32` in `src/OpenCiv2.Base/WrappingI32.cs` is a readonly
record struct with an `int Value`, defaulting to zero. Explicit conversions from
`int`, `uint`, `long` and `ulong` take the signed low-32-bit view. Explicit casts
to `int` and `uint` expose the signed value or unsigned bits.

```csharp
using OpenCiv2.Base;
var result = ((WrappingI32)value * (WrappingI32)multiplier
              + (WrappingI32)adjustment) / (WrappingI32)divisor;
int stored = (int)result;
```

Addition, subtraction, multiplication and negation wrap at each operation;
comparisons are signed. Division/remainder delegate to the explicit F01 helpers
and reject zero or the unrepresentable minimum-integer/negative-one boundary.
There are no implicit boolean conversions or shift operators. Use `Arithmetic`
for the specified clamps and signed-variable-shift laws. Checked project defaults
do not change these explicitly wrapping intermediates.

## F02 identity and reservation boundary

`src/OpenCiv2.Contracts/Identities.cs` defines distinct civilization/unit/city slots and unit/city
lifetime tokens. Zero tokens mean free records; any nonzero token is occupied,
including values with the high bit set. `ProcessTokens` owns separate u32 counters
starting at 1. Assignment returns the current counter and increments modulo 2^32,
without skipping zero or searching for collisions. A process owner retains this
object across games; independent peers own separate objects. `ProcessContext` retains preferences and tokens while
`SessionRuntime.PrepareNewGame` replaces the world; the setup and load contracts
define their bounded compositions.

`EntityAllocation.ReserveUnit` accepts a read-only token view of exactly `[0, unit_end)` at
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
xUnit additionally covers the 2039/2040/2047/2048 bounds, both sides of 1948,
effective-human bits, negative retained counts, invalid views and a caller-driven
scan reset retaining records and token positions. These are leaf-operation tests,
not full U01 creation, C01 city reservation, deletion or F06 token-remapping tests.

The subsequent record slice adds five pool/workspace and eleven codec/I/O xUnit
cases. These cover deletion finalization, entity reset portions, current/legacy
lifetime renewal, full unit/city payloads, explicit little-endian field vectors,
retained bytes, truncated records and short-item publication prefixes. Full
creation/deletion policy, civilization/global records and session load remain open.

## W01 geometry and map boundary

`src/OpenCiv2.Core/Geometry.cs` provides rectangular lookup, canonical row-order coordinates,
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

`src/OpenCiv2.Core/MapStorage.cs` owns owned tile records, remembered-improvement layers
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

Eleven shared cases exercise these contracts, retained state, allocator failure,
fallback/parity aliasing, unsupported backing, sync routes and callback failures.
Ten new X02 vectors cover wrap, step/weighted distance and nullable tile indices,
with the dimensions/current topology included in the registered state closure.
The expectations come from W01 §§2,4,9 and Appendix A, not implementation output
or original-game captures. Region labeling/border rebuilding, the treaty-based
foreign-radius query, bulk field adapters and complete W03 memory policy remain
unimplemented; this slice does not claim all of W01 or its gameplay consumers.

## X02 runner scope

`tools/dotnet/run_conformance.py` is a Python-standard-library developer host driver;
it invokes the adapter directly or through Node and compares its JSON output.
The game library, ordinary builds and xUnit tests have no Python dependency.
The driver obtains actual operation results from C#; it separately validates
supplied choice codes/results against F01 before execution.

The `f01-f02-w01-script-v3` scope validates exact X02 v3 structure, target/specification
hashes and evidence anchors, registered argument types/preconditions and the exact
scalar/ordered-array state closure, canonical integers, typed booleans, mandatory trace identities
and dependencies. It compares typed results, absence and complete registered state,
matches events by identity, and checks chronological dependency edges. Unknown fields, duplicate keys,
stale pins, omitted edges and unconsumed declared inputs cannot produce a pass.
Extensions at the fixture root are metadata only. The adapter subprocess obeys
the fixture's host-time watchdog. Leaf event budgets are checked before entry;
the scripted boundary retains an actual prefix when an execution budget is reached.

The four semantic F01 adapters execute keyed choice tapes and generated policy v2
replay in all three ownership domains. They validate typed codes/results and
record actual sampling events. Constants omit the event and consume no code.
Replay compares the complete registered origin/position context, with exact policy
file identity and a final-engine reconstruction check. See the
[replay schema and its linear restoration cost](random-policy.md#portable-fixture-replay-contexts).

F02 shared-city-workspace dispatch supports ordered supplied-child output,
observation, failure and synchronous delivery actions. Nested calls use the real
F02 entry and one live workspace. Missing child results stop at consumption with
retained effects and no invented returns. Eight authored specification vectors
exercise these boundaries, including cached signed32 wrap and slot-zero selection.
The C03 pipeline is explicitly supplied, not implemented. Its normal reentry
schedule is not established by these cases. See the
[script contract and harness limits](conformance/README.md).

Unregistered state types/blobs, other supplied-child/provider boundaries, queues,
legacy-reference fixtures, projections/normalizations,
independence declarations and invalid-access safe-stop outcomes remain unsupported.
They are not silently discarded or treated as successful comparisons. General
logical-value validation supports every scalar tag and nested records/arrays,
but executing additional value closures, blob loading, broader stop-prefix capture and
the other features remain `x02-general-runner` work. F01 code arguments in the three original vectors are controlled inputs,
not seeds or a test of PRNG quality.

Stateful adapter inputs use canonical numeric fields on stdin in the registered
protocol order. This avoids command-line size limits and carries no expected
result or snapshot. Typed fixture validation precedes execution; the C# adapter
also rejects missing, out-of-range and trailing state input. The original three
F01 and seven F02 fixtures and their expectations are unchanged. Boolean inputs
use exact JSON true/false in fixtures and 0/1 only in the internal adapter protocol.

Reports retain raw fixture hashes/revisions, target/spec/evidence identities,
implementation revision plus exact source hashes and dirty status, adapter
binary hashes, build identity, actual traces, consumption, comparison scope,
explicit randomness/policy inputs, limits and first mismatch. The current C# selection has 57 cases, including seven explicit PCG32 policy
variants. Reports use `openciv2-managed-conformance-run/1`, with compiled input
manifests embedded by MSBuild and checked by the runner. Reporting-code identities
are separate from compiled inputs. Source or artifact changes during execution
prevent publication; rebuild before rerunning after any input change.

## Running and extending

Use the C# adapter in `tests/OpenCiv2.ConformanceAdapter` and the browser host in
`tests/OpenCiv2.ConformanceBrowser`. The [test guide](testing/README.md#x02-execution)
provides execution, rejection and native/browser comparison commands. The runner
embeds and validates compiled input identities, binds invoked artifacts and
rejects changed inputs before publishing a report. It never passes expected
observations to the adapter.

Register new operations and state closures under [conformance](conformance/README.md).
Keep policy-specific PCG32 replay fixtures separate from controlled semantic
choices. A fixed implementation test does not establish original-game equivalence.
