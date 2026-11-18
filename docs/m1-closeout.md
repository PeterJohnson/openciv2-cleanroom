# M1 finite closeout contract

## Authority and stopping rule

This document fixes the bounded foundation contract. The current C# implementation
and tests are mapped in [coverage](testing/coverage.md), with executed platform
results in [validation](validation/README.md). Continue with M2 and later milestones
without recursively implementing every gameplay child reachable from these APIs.
X01 and the owning specifications govern correctness; this page limits delivered
scope, not the eventual gameplay or interchange contract.

Preserve meaningful failure prefixes, continuation/cancellation behavior and
independent evidence. Fix defects within the admitted surface. Record additional
features under their owning milestone rather than weakening a contract or silently
expanding a completed foundation task. Test-only services remain in tests.

## Closed work list

| Contract | Current scope |
| --- | --- |
| M1-01 | Bounded local checkpoint profile and suspended authored continuation |
| M1-02 | Three classic-to-native content conversions with receipts and rejection checks |
| M1-03 | Fifteen registered X02 operations, 57 selected fixtures and eight integration evidence families |
| M1-04 | Bounded original-file procedure; repeat against the current binary before claiming fresh interoperability |
| M1-05 | Arithmetic/state/map, content, queries, I/O and session regression obligations |
| M1-06 | Native .NET and browser matrix; actual platform UX has separate M2 gates |
| M1-07 | Explicit status, coverage and evidence gaps in the validation/implementation guides |

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

The X02 denominator is the **57 fixture IDs** in the selected registry and these
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
test files and checks, not merely assertion totals. shared-case/CLI evidence is valid
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
[map](https://github.com/PeterJohnson/openciv2-cleanroom/blob/6e58a59e841760e7d3a5706e727d4bb8090bb224/docs/conformance/reports/original-maps-native.json) and
[save](https://github.com/PeterJohnson/openciv2-cleanroom/blob/6e58a59e841760e7d3a5706e727d4bb8090bb224/docs/conformance/reports/original-saves-windows.json) reports through current
stored codecs; keep source hashes unchanged. The earlier
[live-save observation](m1-original-live-save.md) records
the existing observation method, not a pass for a changed exporter. For actual
execution, export one of those maps and one single-player save with a declared
treasury edit. Have the unmodified target original accept the map through its
normal map-selection workflow and the save through its normal load workflow.
Verify the edit, advance one original turn, resave, and decode that resave with our
current codec. Record the exact candidate/source/executable hashes, observed
outcomes and limitations. The original supplies its own gameplay; no missing
cleanroom mechanic must be implemented to run this check.

Recorded original-program observations remain dated evidence, not a pass for the
current save exporter. Missing executable/files/UI access is an external blocker.
Do not change the original executable, consult reconstruction code, repair its
TCP implementation or acquire more versions to close this gate. No new scenario,
hotseat, email or legacy executable run is required by M1.

## M1-05 and M1-06: regression and build matrix

M1-05 preserves the implemented arithmetic/records/map, content and query contracts,
fresh-game/reset and authority guards, classic codecs, inspection/export and
network primitives. In particular, retain cold/reused/cancelled/failed-entry
equivalence, stale-client rejection, pending-command/output guards, RNG isolation,
coherent save snapshots with honest finalization, passive stack diagnostics,
bounded codecs and semantic file choices. Supplied gameplay children stay supplied;
no live world, AI turn, renderer or protocol handler completion is implied.

M1-06 requires this finite matrix, using the pinned tools in `dependencies.json`:

Existing CI jobs may supply these results. A GUI job, browser UI automation,
shipping bundle, extra compiler or additional OS version is not an M1 gate.
Hosted execution can be an external blocker; a compile/test failure cannot be
reclassified as missing evidence. Identify the tested source revision and relevant
artifacts. Evidence may be reused when its implementation/build inputs are
unchanged; documentation-only edits do not force another platform run.

## M1-07: status and continuation

The [validation guide](validation/README.md) records executed C# evidence and
remaining platform gates. The [implementation plan](implementation-plan.md) is
the next-work entry point. Preserve distinct denominators for shared cases, X02
fixtures, CLI tests and external observations; do not report whole-game completion
from this finite foundation surface.
