# M1 completion audit

The compatibility review replaces previous-game retention with fresh-game
isolation. `prepare_new_game` and the F05 entry path clear world/mutable content
and file scratch, revoke previous clients, and preserve explicit preferences and
identity counters. Cold/reused/cancelled/failed-entry comparisons are M1 gates. The
[fresh-game evidence](m1-fresh-game.md) now includes a registered projection
and wider world/content/pending-work integration checks.
Full W02/V01/balancing/AI and graphical setup integration remain M3/M4 work;
supplied-child foundation tests do not establish those mechanics. Historical test
counts below describe their recorded revisions, not current review verification.

The subsequent interface review fixes five remaining issues and the deferred
new-world content inconsistency: nested setup/configuration lifetime guards,
unit operations inside command continuations, staged preference loading, owned
scenario event catalogs, asynchronous option votes, and one shared content
installation path for new games and loaded games. Regression cases cover callback
replacement attempts, command completion/cancellation, failed finalization,
event allocation failure, bounded vote polling, and reuse of closed content inputs.

Validation: 531/531 Linux native CTests (518 Catch2 cases), 531/531 ASan/UBSan
CTests with LeakSanitizer disabled for this host, 8/8 Emscripten CTests (505 portable
cases, 209,132 assertions), 121 Python tests, and a rebuilt SDL desktop smoke test.
All 57 native conformance vectors pass and the source-bound report is refreshed.
Pinned clang-format 20.1.8, include boundaries and conformance metadata pass.
These checks add implementation evidence, not new original-game, Windows or
macOS observations. GUI and transport adapters remain later-milestone work.

The 2026-11-14 ownership and format-boundary follow-up separates owned save/map
inputs from installation, guards complete load lifetimes, loads rules once,
separates label parsing from text setup, removes serialized presence words from
live events, and moves unit request waiting into session continuations. Tests now
assert admitted values and gameplay effects instead of private partial-I/O or
allocator traces; retained codec tests still check stored byte fidelity.

Validation of that follow-up: 530/530 Linux native CTests (517 Catch2 cases),
530/530 ASan/UBSan CTests with LeakSanitizer disabled for the host environment,
8/8 Emscripten CTests (504 portable cases, 209,649 assertions), 121 Python tests,
and a rebuilt SDL desktop smoke test. Pinned clang-format 20.1.8, include-boundary
and conformance metadata checks pass. The current native conformance report is
refreshed for the changed source identity. These are implementation regressions;
no new original-game, Windows or macOS execution evidence is claimed.

The five-finding implementation follow-up passes 546 native CTests (534 Catch2
cases) and all seven WASM CTest targets (522 portable Catch2 cases), including
57 foundation conformance vectors on each platform. Regression coverage now
checks honest save finalization, coherent export snapshots, bounded compression
and stack repair, passive diagnostics, semantic file choices and owned notices.
Specifications F03/F06/N02/P04/U01 and the M1 exit gate describe these contracts.
Those historical checks were specification-derived; they did not include new
original-game or mixed-peer comparisons. The later M1-04 observation below adds
only its explicit current-save/raw-map acceptance evidence.

Status: **complete** at source revision `56be096`, 2026-11-14. The [finite closeout contract](m1-closeout.md) is the
authoritative work list, frozen at `b125df5`. Its seven gates and explicit stopping
rule govern M1 completion. This audit records evidence; historical slice documents
and broad coverage gaps do not enlarge the goal. Owning specs still govern correctness.

## Current closeout status

| Gate | Status | Evidence or remaining deliverable |
| --- | --- | --- |
| M1-01: local checkpoint profile | Passed | `3b135a6`: [eight-group evidence](m1-checkpoint.md#eight-group-acceptance-evidence), seven session cases/241 assertions each on Windows and WASM; explicit owner codecs, state rejection and atomic admission |
| M1-02: content conversions | Passed | `d65e659`, portable core `597dec1`: [three-row acceptance evidence](m1-content-conversion.md#m1-02-acceptance-evidence), paired semantics and native admission, receipts, rejection/output failure; 570 native CTests and seven WASM targets |
| M1-03: fixed conformance review | Passed | Independent review at `2827676` plus verified revision-7 corrections; [57-fixture review and eight-family mapping](m1-conformance-review.md); final `56be096` native/WASM observations and consumed prefixes match |
| M1-04: original-file evidence | Passed | `56be096`: [current original-file evidence](m1-original-closeout.md), seven maps/21 saves byte-exact; original accepted live treasury edit and map, advanced/resaved one turn; current codec decoded resave |
| M1-05: foundation regressions | Passed | `56be096`: [final hosted regression matrix](m1-build-matrix.md), full applicable native/WASM/sanitizer suites including repair, isolation, cancellation and conversion checks |
| M1-06: build matrix | Passed | `56be096`: [pinned hosted matrix](m1-build-matrix.md), all required Windows/Linux/macOS/SQLite/WASM/sanitizer/tools-only configurations and developer checks |
| M1-07: completion audit | Passed | `56be096`: all six gates above verified, coverage ledger and slice status reconciled, deferred issues retained below; no remaining M1 blocker |

The initial hosted failures were fixed and the required configurations rerun.
Evidence is bound to the tested source revision; subsequent audit/report-only
edits do not change the implementation or build inputs.

## Deferred issues and current completion decision

M1 is **complete within the finite contract**. The checkpoint and conversion
acceptance mappings were checked against the final suites; all independent-review
fixture hashes match the current files; the original-file procedure was repeated
after portability fixes; all required hosted artifacts pass and agree on X02
observations and prefixes. The coverage ledger and implementation-plan row reflect
these gates without widening their domains. No complete gameplay, arbitrary-world
checkpoint, full loaded-state diagnostic or original-peer session is claimed.

| Deferred issue | Owner and retained limitation |
| --- | --- |
| Unknown `view_civ` sentinel interpretation | M3-M5 inspection/loaded-state users; retain the diagnostic and raw stored value until the domain is specified |
| Non-ASCII comparison coverage | M3 content consumers; reversible Windows-1252 bytes do not establish a complete case/locale comparison table |
| F05 application reload-policy arguments | M3/M4 application/setup integration; foundation callbacks do not prove the application's eventual policy choices |
| Broader loaded-state diagnostics | M3-M5 loaded queries and mode admission; stored inspection and bounded repairs do not imply complete live-state validation |
| Incomplete original-peer execution | M6; retain ID/discovery observations and the failed TCP ping, with no full-session or reverse-role play claim |

No deferred item is silently treated as an M1 pass. Their exclusion follows the
finite closeout contract; they retain their owning behavioral requirements.

## Baseline evidence and later work

The table below preserves implementation evidence and maps it to the finite gates.
Original-game observations, specification-derived tests and independent review are
separate claims. Historical test counts describe their recorded revisions.

| Requirement | Current evidence | Closeout gate or later milestone |
| --- | --- | --- |
| F01 arithmetic and logical types | `oc2_base`, `WrappingI32`; native/WASM boundary tests | M1-05/M1-06: preserve existing arithmetic checks. New mechanic consumers join their later milestones. |
| F02 records, pools, retained state and lifecycle | Full unit/city records, stable pools, reservation/deletion finalization, entity reset portions, ordered renewal and shared mood workspace; [U01 traversal, stack repair, routed placement, basic deletion and extracted F06 cleanup](m1-unit-stacks.md), with bounded pool access, passive diagnostics and explicit resource stops; civilization/global/name/leader values with Windows/WASM codec tests; [ordered F05 inner new-game initialization](m1-new-game.md), with explicit creation/balancing children | M1-03/M1-05: review the fixed reset projection and preserve isolation/identity tests. M3/M4: remaining workspaces, creation and gameplay children. |
| W01 topology, coordinates and contiguous map state | Geometry, parity/aliasing, fallback, knowledge/coarse layers, retained summaries, ordered allocation/release and tile primitives; native/WASM tests | M1-05: preserve existing ownership/geometry tests. M3/M4: region rebuilding and gameplay consumers. |
| Versioned randomness and controlled choices | `std::mt19937_64` policy behind an owned mockable source interface, isolated contexts and independently cloned checkpoints; raw mock mapping/failure/restore checks; validated keyed choice tapes, bounded semantic traces and consumed-prefix checkpoints; authored retry/tie/live-read and presentation-consumer scenarios across a suspended command; X02 JSON sampling/replay cases | M1-01/M1-05: the declared local checkpoint profile and current RNG regressions only. M3–M6: full pending-world/queue persistence, optimization and owning sampling sites. |
| X02 v3 execution | Fifteen registered foundation adapters including the fresh-game isolation projection; complete scalar/array/boolean, sampler-context and shared-city-workspace closures; 57 fixtures including ordered supplied-child actions, real-entry reentry, unresolved-result prefixes and seven policy regression cases; identical native/WASM observations and adapter rejection tests. Generic logical-value validation covers all scalar tags and nested records. | M1-03: review and execute the enumerated 15 operations/57 baseline fixture IDs. No unbounded remaining-adapter requirement. M3–M7: broader state/providers/gameplay joins. |
| Six built-in gameplay components | Complete JSON defaults, schemas, offline validation and portable C++ loading into immutable typed operands; exact source/value hashes and classic/custom gate; native CLI and native/WASM tests. Government advance references now bind through the [native rule decoder](m1-rule-content.md). The [F04 calendar consumer and F05 acceleration labels](m1-calendar.md) read selected operands, including real decoded custom bundles. See [runtime content boundary](m1-content-runtime.md). | M1-01/M1-02/M1-05: retain current typed admission; bind content identity in the bounded checkpoint. M3/M4: other consumers; M6: original-peer admission. |
| Native content and classic text | Strict JSON through pinned jart/json.cpp; gameplay loading; pack/overlay manifest and dependency resolution with explicit mounts, exact source hashes, owner receipts, locale grouping, builtin/nested gameplay selection; typed native rules with fixed identity mappings, exact normalization, rule/gameplay references and Windows-1252 text; F03 byte/section reader and all twelve ordered classic rule tables with name/leader preservation, owned text and local references, P04 labels/popups and logical name references and custom-name flags with partial-failure tests; native rule installation with retained policies/live operands and paired classic/native semantic results; native/classic city-production preferences with retained state, paired inputs, first-name-match checks and a validation CLI; native city-name admission and paired C01 providers with live counters, wrap, fallback and partial effects; complete F03 process preference records/defaults and CIV2.DAT item codecs, native load/save/delete services, process/world lifetime separation and exact re-encoding of the original runtime's 516-byte preference file; accepted game/graphics/message changes with real persistence, N03 multiplayer proposals/shared vote callback and F03 double-movement transformation with 28 portable cases; bounded native/WASM/CLI tests | M1-02: the closed validation surface and three classic-to-native converters; M1-05: mutable-content isolation. M2–M5: full catalogs, comparison profile, reverse conversion, media and playable content. |
| Classic codecs and first save path | All current fixed records and complete `0x2c` stored-save decode/re-encode including optional scenario/multiplayer/events; raw maps and ordered map/entity/leader item helpers, tribe-indexed sign scratch and ordinary/scenario name phases; current/legacy persistent-record input with partial publication, enemy/scenario transformations and an explicit post-renewal census phase; password reset/refresh, retained multiplayer input/write stages and completion-mode adaptation; ordered event I/O with explicit unresolved strings and S02 copy/compaction; coarse/region, history/layout/view and scenario/elimination stream stages, with 21 original-save auxiliary comparisons; composed F06 core load with required file/path/F03 bindings, all four admitted versions and post-close mode adaptation; composed live saving with V03 knowledge, scenario metadata/names, coherent encoded snapshots, file-only hotseat values and truthful write/close results; native file/text/directory services and F03 label/sprite/directory reload context; authored disk load-edit-save-reload with real rule parsers; original acceptance of edited live-session exports, turn advancement and resave/reload; portable F06 save-menu wrapper and file-choice contract with real core output, semantic file types/results, owned notice arguments and countdown cleanup; accepted-file load caller with ALT selection, event replacement/compaction, preference/audio/title ordering and real routed U01 cleanup, with required S02/P10/V01 children; partial I/O and byte-preservation tests; bounded stored semantic diagnostics | M1-04/M1-05: preserve the fixed codec/core-load matrix and perform bounded current-export checks. M2–M6: browser storage, gameplay children and application/mode joins. |
| Inspection CLI | `oc2 inspect`, `validate`, `diff` for current saves/raw maps; human/lossless JSON, stable slots, exact bytes, explicit limitations and missing rules; eight independent CLI/driver integration tests and 28 original-file reconstructions | M1-04/M1-05: retain current raw-map/0x2c stored views and explicit unknown view_civ diagnostics. M3–M5: broader semantic/loaded views and sentinel interpretation; no extra version is an M1 gate. |
| Scoped queries | Immutable owned stored records and explicit offline access; live own-unit/city projections through private session leases, stable slot/token ordering, observation provenance and no state/RNG mutation; native/WASM tests | M1-05: preserve own-entity ownership and query purity. M3–M5: remembered views, action availability and graphical integration. |
| Optional SQLite inspection export | Default-off pinned SQLite 3.54.1 adapter; versioned exact stored rows, raw BLOBs, source/coverage/diagnostic metadata; independent Python row comparisons; cancellation and atomic no-overwrite publication tests; 28 original-file exports checked | M1-05/M1-06: current stored rows and fixed platform matrix. M3–M5: live/loaded exports and wider formats. |
| SessionRuntime and lifecycle | `oc2_session` owns world records, pools/map, multiplayer values/selectors/password flags, ordered events, histories/scenario/layout/views, RNG contexts and admitted continuations; injected process tokens/rules/shared text/leader bindings/fallback, P04 substitution parameters and complete ordinary/multiplayer preferences with explicit defaults and persistence, accepted option changes and retained vote state, ordered F05 inner initialization with live rate adjustment and required V01/balancing children; [world-size/parameter helpers](m1-world-setup.md) with retained working dimensions, cancellation effects, distinct draw orders, real preference saves and independent original UI/file observations; [accepted-file premade preparation](m1-premade-world.md) with real F06 input, ordered partial failures, seed/hint choices and original radius-byte evidence; [player/rules setup](m1-player-setup.md) with retained retry locals, immediate preferences/identity/custom names, controlled roster selection and opponent overrides, real F04 acceleration labels and independent original UI/file comparisons; [new-world entry composition](m1-world-entry.md) with actual size/map/parameter/player helpers, preference writes, ordered reloads, inner initialization and the final advance list, with required gameplay children; distinct identities and private revocable client leases; native/WASM ownership, core-load/save and command cases | M1-01/M1-05: bounded local checkpoint profile and existing fresh-game/authority guards. M3/M4: playable admission and setup children; M5/M6: modes and peer queues. |
| Commands, effects and continuations | Versioned typed descriptors, owned FIFO requests/inputs, correlated lifecycle results/effects, scoped interactions, timeout/cancellation and retained continuations; twelve authored native/WASM command cases, including controlled decisions and explicit mismatch failures after committed effects. See [command contract](m1-command-contract.md). | M1-01/M1-05: serialize only the named foundation profile and preserve current command contracts. M3–M6: actual gameplay/wizard/remote continuation codecs. |
| Authority and client bindings | Private leases and scoped direct calls, explicit authority admission, distinct request/result factories, mapped fake-peer requests, source/correlation checks and retained replies through disconnect/removal; role-change and no-reroll tests | M1-05: retain existing fake-peer and scoped authority tests. M6: protocol routing, peer cancellation/timeouts and endpoint generations. |
| N05/N02 foundations | Portable incremental framing, owned partial-send FIFO, UDP/envelope codecs, section checksums/mirrors/raw images, byte/word compression and bounded sequential wire-block application; 23 native/WASM Catch2 cases; independently authored original discovery and ID/ping drivers. See [network boundary](m1-network-foundations.md). | M1-03: map existing framing/envelope and section/checksum/compression tests as evidence; no extra per-helper X02 adapters. M1-05: regression checks. M6: handlers, sockets and world application. |
| Original file/peer checks | Seven raw maps and 21 current saves reproduce exactly; three legacy files rejected. The original accepted two byte-exact C++ stored exports and two [live-session exports](m1-original-live-save.md), including a declared treasury edit, original turn advancement and resave/reload. An actual original LAN host's ID greeting and UDP advertisement reproduce through the C++ codecs. The subsequent TCP ping timed out, with the original log reporting receive capacity 65536. | M1-04: fixed file corpus and one current single-player/map original acceptance run. Retain failed peer evidence. M5/M6: broader modes, mixed-peer codecs and full TCP sessions. |
| Build/test exit gate | F01/F02/W01 native, WASM and earlier sanitizer checks; Windows MSVC passes 563 default-off and 565 SQLite-enabled CTests; pinned WASM passes seven CTests including 539 portable Catch2 cases (194,977 assertions); 115 Python tests pass with two Windows skips; tools-only inspection/content validation without SDL/ImGui/assets/Lua/SQLite | M1-06: exactly the declared matrix. M1-07: final evidence audit. Additional GUI/platform/release configurations are not M1 gates. |

Implementation detail and retained reports are in [m1-foundations.md](m1-foundations.md).
The operation registry and coverage ledger remain the operation-level coverage
inventory. They are not an expanding M1 task list: [M1-03](m1-closeout.md#m1-03-finite-conformance-and-review)
fixes the review denominator and identifies valid non-X02 integration evidence.
