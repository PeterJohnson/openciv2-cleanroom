# Conformance inventory

M0 provides checked metadata. It does **not** execute game algorithms. The
[X02 version 3 fixtures](../algorithms/conformance/) remain unchanged and the
actual loader, adapters and trace comparator are M1 work.

```sh
python3 tools/check_conformance.py
python3 tools/check_assets.py
python3 -m unittest discover -s tests -p 'test_*.py' -v
```

Use `python` on Windows. Python 3.9+ and the standard library suffice for the
metadata checks. The existing formatter tests additionally use pinned
clang-format. With Python available, CMake exposes `inventory-check` and CTest
metadata tests; normal builds and headless Catch2 tests do not require Python.

## Versioned registries

[registry.json](registry.json) schema 1, inventory 1 contains:

- All 65 specification families, exact file hashes, specification status and
  separate registration status. This denominator counts documents, not behavior.
- Two F01 pure operation contracts, with typed arguments/results, state closure,
  preconditions, callbacks, stop/event boundaries, dependency edges and explicit
  empty independence claims. `implementation: null` means no adapter exists.
- Two F02 process token state keys, with type, lifetime, ordering and reference
  rules. These names register shared logical state, not C++ globals or save bytes.
  Pool records, retained workspaces and random contexts still need registrations.
- Three fixture identities/revisions/hashes and their provenance. Each is a
  specification vector, has `executionStatus: not-run`, and has no run report.
  The target executable hash in a vector is not proof of access to that image.

[coverage.json](coverage.json) uses X02 §8's required row fields and dispositions.
Rows own the two registered operation contracts. `evidenceEntries` identify the
local specification path/hash and operation; no original function-address or
source-fragment inventory is claimed. Do not follow the specs' reconstruction
source links to fill these fields. Specification clarification is the route for
missing behavior/evidence. A future independently supplied source-coverage audit
must remain distinguishable from this cleanroom operation inventory.

The ledger also records all G01–G23 integration families, with their required
comparisons and no executed cases. Each gap has a bounded domain, known prefix,
closure evidence and delivery milestone. A family description, registration,
implementation, supplied vector and successful execution are separate facts.
Never convert one into another or report a percentage over different denominators.

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
   retain the operation ID independently of source layout. M1 must add callable
   registration and full X02 fixture validation; this metadata validator does not
   substitute for either.
5. Only set execution status and `lastRun` after an actual run. Store its report
   with implementation revision, fixture ID/revision/hash, spec hashes, policy,
   platform and bounded observations, following X02. Metadata checks currently
   require referenced reports to exist; the M1 runner must validate their contents.
6. Review spec/fixture changes before updating hashes. Bump `inventoryVersion`
   when changing the inventory denominator; schema changes bump `schemaVersion`.
   Run the checks above and the operation's Catch2/conformance tests.

The validator rejects stale specification/fixture bytes, duplicate JSON keys,
missing owners/anchors/state references/cases/gaps, cyclic event dependencies,
inconsistent implementation statuses and execution claims without an adapter.
These are bookkeeping checks, not a v3 gameplay fixture parser or compatibility
claim. Original interoperability and integration cases remain mandatory.
