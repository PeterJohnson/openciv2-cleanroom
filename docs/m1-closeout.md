# M1 finite closeout contract

Status: closeout scope defined; implementation and final evidence are still open.
Baseline: `b125df5` (2026-11-14). This document fixes the remaining M1 scope at
that revision. It does not start an implementation run or declare M1 complete.

## Authority and stopping rule

This is the authoritative **M1 work list**. The implementation plan describes
the milestone, the [audit](m1-validation.md) records evidence, and the operation
registry describes implemented coverage. Older slice documents, generic coverage
gaps and references to an entire specification do not add work to this list.
X01 and the owning specifications still govern correctness; this document limits
delivery scope, not the eventual gameplay or interchange contract.

Only M1-01 through M1-07 below are completion gates. Each is either `open`,
`passed`, or `blocked-external`, with a revision and evidence reference. Existing
implemented features are regression obligations, not invitations to expand them.
Implement the smallest production boundary needed by each listed gate. Test-only
operations/providers remain in tests; original executable trace emulation stays
outside production. No graphical flow, allocator trace or native ABI is a gate.

Complete M1 only when all seven gates pass. If a required original-game run,
independent review or hosted platform is unavailable, finish the other gates and
report the exact external blocker; do not mark M1 complete or invent substitute
evidence. Retry external work when access or evidence changes, not indefinitely.
Missing code, a failing test or difficult implementation is not an external blocker.

New findings are handled as follows:

- Fix defects needed to pass a listed gate or preserve the frozen regression
  surface. Add the smallest meaningful regression case.
- Record other work with its owning later milestone. Do not recursively implement
  a gameplay child, add another file version, register every helper with X02, or
  enlarge a supported profile merely because it is reachable from M1 code.
- If a listed gate cannot be completed without changing its declared domain,
  document that specific dependency for a scope decision. Do not silently widen
  the task or weaken an owning behavioral requirement.
- Run the required checks on the final candidate. Repeat affected checks after
  changes; do not keep adding demonstrations after the gates pass.

## Closed work list

| ID | Deliverable | Baseline status | Completion evidence |
| --- | --- | --- | --- |
| M1-01 | Bounded local continuation checkpoint proof | Open: RNG snapshots exist; suspended commands are not serialized | The checkpoint profile and eight scenarios below pass on native and WASM |
| M1-02 | Three classic-to-native content conversions | Open: readers, typed models and validators exist | Each matrix row has paired semantic comparisons, rejection cases and source/output receipts |
| M1-03 | Review and inventory the frozen conformance surface | Open: 57 X02 vectors execute; independent review remains | Review disposition for each vector, family-to-test mapping, fresh native/WASM reports |
| M1-04 | Current-candidate original-file evidence | Open: retained original observations predate the latest save refactor | The finite file procedure below passes; an external blocker is not a pass |
| M1-05 | Preserve implemented foundation contracts | Open: implemented, final regression verification required | Existing suites plus listed repair/isolation checks pass without adding gameplay children |
| M1-06 | Fixed build and test matrix | Open: local Windows/WASM evidence exists; final matrix is outstanding | Revision-bound results for every required configuration below |
| M1-07 | Final closeout audit | Open; last step | One evidence table for M1-01–M1-06, unresolved/deferred issues and an explicit completion decision |

## M1-01: checkpoint profile

Prove serialization and resumption through the production session/continuation
boundary using **one declared foundation profile**, not a whole-game checkpoint.
The profile has one local participant controlling civilization 1, at most one
accepted command, no incoming/outgoing peer traffic, no queued command inputs,
and a checkpoint boundary between command steps. Support an idle session and a
command awaiting one owned decision. The authored command commits a treasury
change and effect, retains a resolved random choice, waits, then completes,
cancels or takes its declared timeout. Its implementation/codec is test-only;
do not install a fake game command in production.

The supported state closure is explicit: civilization 1 treasury; process token
generators and ordinary/multiplayer preferences; gameplay and presentation RNG
policy/context/engine or controlled-tape state, consumed prefix and trace;
participant/actor identity; command arguments, phase and retained choice;
committed effects; interaction identity, choices, recipient and deadline; and
the counters/correlations required to continue without identity reuse. The
selected immutable content is identified by a hash supplied again on restore.
Other world fields must equal the profile's declared fresh defaults. Live client
leases are not serialized; restore requires a fresh binding and old leases cannot
control the restored session. Cosmetic renderer state is outside this profile.

Use owned, versioned bytes and explicit owner codecs; do not serialize pointers,
closures, native stacks or original memory layouts. Decode/admit before replacing
state. Unsupported nondefault world state, additional commands, queued inputs or
peer work must be rejected explicitly, never omitted. This is a foundation
projection, not a save-game feature or proof that an arbitrary session restores.
Full world/mode/queue checkpoint coverage joins its mechanics in M3–M6.

The eight required scenario groups are:

1. Idle round trip into a separately constructed runtime/process.
2. Pending decision round trip, then the same reply produces the same treasury,
   effects, result and RNG suffix as uninterrupted execution; the prefix runs once.
3. Cancellation after restore preserves the committed prefix.
4. Timeout after restore uses the retained deadline/default and produces the same result.
5. Generated and controlled RNG variants preserve all included contexts; no reseed,
   reroll or reuse of a consumed controlled choice occurs.
6. Fresh bindings can answer; old bindings and duplicate/stale decision replies cannot.
7. Truncated/corrupt bytes, unknown format/policy/operation codec or mismatched content
   fail without replacing the destination or consuming its RNG.
8. Every unsupported-state category above rejects without changing the source.

An owner-supplied codec for this projection is sufficient. General serialization
of every `GameState` field, efficient arbitrary-engine persistence, remote pending
commands, F05 wizard continuations and application save/resume are not M1 gates.

## M1-02: content and conversion matrix

The existing manifest/resolver, six gameplay components, native rules, city names,
city preferences, F03 rules/preferences and P04 label/popup readers are the frozen
validation surface. Preserve their current APIs' declared domains and diagnostics.
Add explicit conversion tooling for exactly these three inputs:

| Explicit classic input | Native output | Required comparison |
| --- | --- | --- |
| RULES text, all twelve F03 tables | Rules component and classic-slot identity mapping | Load the output with the existing native validator/installer; resolved rule values and names equal the classic path, including once-only movement/HP normalization, prerequisite fixup and retained-name policy |
| CITY text plus selected rules | City-name component with explicit selectors and fallback | Native/classic name sequences agree for normal selection, duplicate names, cursor wrap and exhausted/missing fallback |
| CITYPREF text plus selected rules | City-preference component | Native/classic selection preserves duplicates, order, first-match resolution and retained NoDefend semantics |

These are component outputs, not a complete playable pack. Command spelling and
internal models are implementation choices. Accept explicit sources/mounts and a
new output destination; preserve inputs and refuse an existing destination or
source alias. Publish a receipt with source/output hashes, conversion/profile
version and unsupported/lossy fields. Never claim success for silent approximation.

Use one complete authored input per row, its native equivalent, and focused
variants for non-ASCII reversible text, identity/reference errors, conversion
boundaries, duplicate/ambiguous selectors, malformed input and output failure.
Support the nominal representable domain of the existing models. A classic input
outside that native domain may still load through its classic adapter; conversion
must report the unsupported feature without narrowing classic loading.

Non-ASCII comparison equivalence that F03/C01 leave unresolved remains an explicit
gap/diagnostic; encoding identity is not evidence of case equivalence. Completing
the entire byte/locale comparison table is deferred to M3's owning content users.
Native-to-classic text export, complete locale packs, full text/event/media
conversion, GAME-cache construction and every schema's semantic admission are
M2–M5 work. Existing label/popup parsing remains regression coverage; exact prompts,
navigation, drawing and text storage are not conversion acceptance criteria.

## M1-03: finite conformance and review

The X02 denominator is the **57 fixture IDs** in the baseline registry and these
**15 operation IDs**. Corrections may revise a fixture and add a narrowly required
regression; they do not authorize new operation families.

- F01: `primaryModuloFromCode`, `secondaryRangeFromCode`, `primaryCode`,
  `primaryModulo`, `secondaryUnit`, `secondaryRange`.
- F02: `assignUnitToken`, `assignCityToken`, `reserveUnitSlot`,
  `cityCalculationEntry`, `prepareNewGame`.
- W01: `wrapX`, `stepDistance`, `weightedDistance`, `tileIndex`.

Record a review of each fixture's domain, expected values, provenance and excluded
state, with reviewer identity, revision and disposition. The reviewer must not be
the author of the reviewed expectations. A separate review can be human or agent;
if unavailable, record an external review blocker. Close actionable findings in
this fixed surface. Passing the implementation against its own output is not review.

Map the existing integration tests to exactly eight evidence families: fresh-game
isolation; commands/authority/queries and M1-01; content/parsing and M1-02;
save/preferences/semantic file choice; bounded stack repair; stored inspection and
SQLite export; N05 framing/envelopes; N02 sections/checksums/compression. Identify
test files and checks, not merely assertion totals. Catch2/CLI evidence is valid
for these families; no additional X02 adapter is required for every helper.

Run the 57 vectors on native and WASM and compare observations and consumed prefixes.
Preserve the `prepareNewGame` projection exclusions and the supplied C03-child
disclaimer. G01–G23 full gameplay integration and comprehensive operation/provider
registration belong to M3–M7. Refresh stale gap descriptions without erasing gaps.

## M1-04: bounded file interoperability

Keep these support claims separate:

| Surface | M1 boundary |
| --- | --- |
| Stored inspection/re-encoding | Existing raw-map format and current `0x2c` saves, including their stored optional blocks; legacy stored views remain explicitly unsupported |
| Core load/export | Preserve core input versions `0x26`, `0x27`, `0x28`, `0x2c` and current `0x2c` live export, with supplied post-load/gameplay children explicitly labeled |
| Actual original-game execution | One current-format single-player live export and one raw-map export on the final candidate, using the procedure below |
| Peer evidence | Retain existing ID greeting/discovery observations and the failed TCP ping; full TCP sessions, mixed-peer codec/state application and reverse-role play are M6 |

Recheck the seven raw maps and 21 current saves named by the retained
[map](conformance/reports/original-maps-native.json) and
[save](conformance/reports/original-saves-windows.json) reports through current
stored codecs; keep source hashes unchanged. The earlier
[live-save report](conformance/reports/original-live-save-windows.json) records
the existing observation method, not a pass for a changed exporter. For actual
execution, export one of those maps and one single-player save with a declared
treasury edit. Have the unmodified target original accept the map through its
normal map-selection workflow and the save through its normal load workflow.
Verify the edit, advance one original turn, resave, and decode that resave with our
current codec. Record the exact candidate/source/executable hashes, observed
outcomes and limitations. The original supplies its own gameplay; no missing
cleanroom mechanic must be implemented to run this check.

Retained pre-refactor observations remain historical evidence, not a pass for the
current save exporter. Missing executable/files/UI access is an external blocker.
Do not change the original executable, consult reconstruction code, repair its
TCP implementation or acquire more versions to close this gate. No new scenario,
hotseat, email or legacy executable run is required by M1.

## M1-05 and M1-06: regression and build matrix

M1-05 preserves the baseline arithmetic/records/map, content and query contracts,
fresh-game/reset and authority guards, classic codecs, inspection/export and
network primitives. In particular, retain cold/reused/cancelled/failed-entry
equivalence, stale-client rejection, pending-command/output guards, RNG isolation,
coherent save snapshots with honest finalization, passive stack diagnostics,
bounded codecs and semantic file choices. Supplied gameplay children stay supplied;
no live world, AI turn, renderer or protocol handler completion is implied.

M1-06 requires this finite matrix, using the pinned tools in `dependencies.json`:

| Configuration | Required checks |
| --- | --- |
| Windows, Linux, macOS native headless, SQLite enabled | Complete applicable CTest suite, inspection/SQLite CLI checks and X02 reports |
| Windows native headless, SQLite disabled | Complete applicable CTest suite; configuration/linkage excludes SQLite |
| Pinned WASM/Node headless | Portable Catch2 suite, metadata and X02 execution/rejection checks; compare X02 results with native |
| Linux ASan/UBSan headless | Applicable foundation and added checkpoint/conversion tests without sanitizer findings |
| One native tools-only configuration | GUI, tests, dependency fetching and Python discovery disabled; absent runtime assets; inspect a supported authored map/save and run content validation |
| Developer checks | Pinned formatter, metadata/content-schema checks and the existing Python test suite; record justified OS-dependent skips |

Existing CI jobs may supply these results. A GUI job, browser UI automation,
shipping bundle, extra compiler or additional OS version is not an M1 gate.
Hosted execution can be an external blocker; a compile/test failure cannot be
reclassified as missing evidence. Identify the tested source revision and relevant
artifacts. Evidence may be reused when its implementation/build inputs are
unchanged; documentation-only edits do not force another platform run.

## M1-07: final audit and execution instruction

Update [m1-validation.md](m1-validation.md) with one row per gate containing status,
tested revision, evidence and any remaining blocker. Reconcile the coverage ledger
and slice documents with that table. Mark deferred issues with the owning milestone;
retain unknown `view_civ` sentinel interpretation, non-ASCII comparison coverage,
F05 application reload-policy arguments, broader loaded-state diagnostics and
incomplete original-peer work honestly. These remain M3–M6 owning work, not extra
M1 implementation gates.
No percentage over mixed fixture/test/operation counts is a completion argument.

The bounded instruction for a subsequent implementation run is:

> Complete only M1-01 through M1-07 in docs/m1-closeout.md, starting from their
> recorded status. Preserve the baseline regression surface. Use owning specs for
> correctness, but do not expand the closed domains or implement later gameplay
> dependencies. Finish available work, report exact external blockers, and stop
> when every gate passes or only external evidence remains. Report those two
> outcomes distinctly; only the first completes M1.
