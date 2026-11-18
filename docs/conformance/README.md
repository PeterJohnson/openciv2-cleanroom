# Conformance inventory and execution

The inventory registers fifteen operation contracts, 32 state keys and all 65
specification families. The C# adapter executes a bounded foundation selection of
57 cases on native .NET and Chromium WASM. These are authored contract/policy
vectors, not whole-game or original-peer compatibility evidence. See
[current validation](../validation/README.md) for actual runs and platform gaps.

The [registry](registry.json) records operations, state closure, supplied providers
and reference fixture provenance. Its fixture execution fields make no runtime
claim: source-bound execution reports come from `tools/dotnet/run_conformance.py`.
The [selection](selection.json) identifies the 57 active cases: fifty controlled
vectors and seven PCG32 replay variants. The reference MT replay vectors retain
independent input provenance; they are not the current production RNG policy.

The portable adapter is `tests/OpenCiv2.Conformance`; its native and browser hosts
are `OpenCiv2.ConformanceAdapter` and `OpenCiv2.ConformanceBrowser`. It runs actual
Core/Session code and never reads expected results. The independent Python
fixture validator/comparator is `tools/conformance.py`.

## Run the checks

From the repository root:

```sh
python3 tools/check_conformance.py
python3 tools/dotnet/pcg_fixtures.py
python3 tools/dotnet/run_conformance.py --report build/test-results/conformance-native.json --adapter dotnet tests/OpenCiv2.ConformanceAdapter/bin/Release/net10.0/OpenCiv2.ConformanceAdapter.dll
```

Use the [test guide](../testing/README.md#x02-execution) for builds, browser runs,
negative cases and native/browser comparisons. Metadata validation checks
bookkeeping and does not execute gameplay. The C# runner verifies SDK/runtime,
compiled inputs, fixtures/policy and invoked artifacts before accepting results;
changes during execution prevent report publication. Fresh code needs a rebuild
and fresh execution, not relabeled hashes from an older report.

## Registered scope

F01 covers pure mappings, sampling laws, controlled semantic choices and versioned
PCG32 replay. F02 covers token allocation/reservation, supplied-child city dispatch
and three fresh-game projections. W01 covers scalar map geometry. City dispatch's
C03 child is explicitly supplied; neither that algorithm nor its normal gameplay
callback schedule is implemented by these fixtures. Fresh-game projections retain
their declared exclusions. See [the finite foundation contract](../m1-closeout.md).

Operations declare typed arguments/results, preconditions, reads/writes, callbacks,
supplied children, partial effects, stop boundaries, event dependencies and
independence claims. State keys declare type, lifetime, ordering, references and
publication. `implementation` names a current C# source file; `null` means no
implementation. A supplied provider cannot also claim to implement its gameplay
operation. Full pool/workspace registrations and complete gameplay remain future work.

The logical-value validator supports exact scalar tags, nested records, ordered
arrays, f64 bits and bounded blob references. Accepting a value tag does not imply
that every corresponding composite operation, external blob source or queued
provider is implemented. Missing supplied values stop explicitly rather than
falling back to RNG, clocks or guessed outputs.

The [coverage ledger](coverage.json) maps registered operations to X02 dispositions,
comparison surfaces, evidence entries and bounded gaps. G01–G23 integration
families remain unexecuted until their actual gameplay exists and has evidence.
A document, registration, implementation, vector and passing execution are separate
facts; do not report a percentage across those different denominators.

Scripted city fixtures exercise absence, caching, wrapping, selection order,
partial failure, nested entries and missing results. Provider request IDs are
`<call>/pipeline`; nested calls use `<parent>/deliver/<action-id>` with JSON-pointer
escaping. The validator checks schedules independently of expected event ordering.
Only completed provider returns count as consumed. The adapter reserves two event
slots for diagnostic snapshot/stop; limits include 10,000 provider records,
10,000 actions per record and 64 active entry levels. Budget exhaustion is an
operational failure, never a passing gameplay result.

## Updating an operation

1. Read the owning repository specification. Register the admitted arguments,
   results, full observable state closure, aliasing, callbacks, partial effects,
   stop boundaries and justified ordering/independence. State keys have one owner.
2. Add the coverage row with bounded domain, evidence, comparison surfaces and
   concrete remaining gaps. Split mixed dispositions and preserve supplied-child
   exclusions. The milestone label does not authorize every future algorithm.
3. Author expectations independently, record provenance and pin exact fixture/spec
   bytes. Do not relabel implementation snapshots as original-game observations.
4. Implement the C# operation and adapter boundary, plus validation/rejection cases.
   Set `implementation` to its current source and keep the operation ID stable
   across internal refactors. Register portable regression cases in FoundationSuite.
5. Run native/browser X02 and rejection suites and compare results/consumption.
   Preserve source identities, invoked artifacts, SDK/runtime, input hashes and
   report status. The inventory itself must not claim execution.
6. Review contract/fixture changes before updating hashes. Update
   `inventoryVersion` when changing the registered denominator and schema version
   when changing its format. Independently verify any policy vectors; never edit
   expected output merely to make an implementation pass.

External source links and reconstruction harness names in specification provenance
are not implementation/test inputs. Resolve missing behavior through the behavioral
contract or independent original-game observations. Original-file/peer evidence and
full integration checks remain necessary for compatibility claims.
