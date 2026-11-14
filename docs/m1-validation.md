# M1 completion audit

Status: **in progress**. The goal is the complete M1 scope in
[implementation-plan.md](implementation-plan.md#m1--build-the-portable-foundations),
including its exit gate. Completed primitives do not close the milestone.

This checklist records the evidence needed for completion. A partial row is not
an exemption from its remaining requirements. Original-game observations and
independent review must be identified honestly; specification-derived expectations
are not original-program captures.

| Requirement | Current evidence | Remaining acceptance evidence |
| --- | --- | --- |
| F01 arithmetic and logical types | `oc2_base`, `WrappingI32`; native/WASM boundary tests | Preserve these checks as consumers arrive. |
| F02 records, pools, retained state and lifecycle | Full unit/city records, stable pools, reservation/deletion finalization, entity reset portions, ordered renewal and shared mood workspace; civilization/global/name/leader values with Windows/WASM codec tests | Broader retained workspaces and complete process/new-game/load transitions; full creation/deletion belongs to its owning mechanics. |
| W01 topology, coordinates and contiguous map state | Geometry, parity/aliasing, fallback, knowledge/coarse layers, retained summaries, ordered allocation/release and tile primitives; native/WASM tests | Integration with explicit new-game/load ownership; complete W01 region rebuilding and gameplay consumers remain later world work. |
| Versioned randomness and controlled choices | `std::mt19937_64` policy, isolated contexts and in-memory checkpoints | Semantic injection, retries/ties, pending-interaction checkpoint/restore and presentation-consumer invariance. |
| X02 v3 execution | Bounded F01/F02/W01 adapter, scalar/array/boolean closure, twenty fixtures, native/WASM reports | General typed values/blobs, scripted services/reentry, choice tapes, implementation replay, stop prefixes and independent fixture review. |
| Six built-in gameplay components | Complete JSON defaults, schemas and offline validation | C++ loading, owning consumer bindings, profile/checkpoint admission and original-peer gates. |
| Native content and classic text | Schemas and developer checks | Runtime manifests/dependencies/hashes, semantic rules/names/preferences validation, F03/C01 adapters, equivalent-input tests and conversion/validation commands with receipts. |
| Classic codecs and first save path | All current fixed records and complete `0x2c` stored-save decode/re-encode including optional scenario/multiplayer/events; raw maps and ordered map/entity item helpers; partial I/O and byte-preservation tests; bounded stored semantic diagnostics | Ordered session load/live save integration, legacy stored views, rule/scenario/event validation and original resume checks. See [stored-save boundary](m1-stored-saves.md). |
| Inspection CLI | `oc2 inspect`, `validate`, `diff` for current saves/raw maps; human/lossless JSON, stable slots, exact bytes, explicit limitations and missing rules; seven independent CLI integration tests and 28 original-file reconstructions | Extend admitted formats and semantic coverage with their owning implementations; resolve the observed saved `view_civ` sentinel domain. See [CLI contract](m1-inspection.md). |
| Scoped queries | Immutable owned stored records, SHA-256 observation identity, stable ordering and explicit offline access; no world, process counters or gameplay RNG access; native/WASM tests | Live observer/authority checks and local session bindings usable by graphical and other clients. |
| Optional SQLite inspection export | Default-off pinned SQLite 3.54.1 adapter; versioned exact stored rows, raw BLOBs, source/coverage/diagnostic metadata; independent Python row comparisons; cancellation and atomic no-overwrite publication tests; 28 original-file exports checked | Preserve the [stored export contract](m1-sqlite-export.md) as admitted formats grow; live/loaded export waits for session/query implementations. Hosted cross-platform checks pending. |
| SessionRuntime and lifecycle | Architecture only | Local world/session ownership, injected process context, distinct civilization/participant/controller/connection IDs and access/lifetime scopes. |
| Commands, effects and continuations | Architecture only | Typed descriptors/help/access metadata; acceptance/pending/interaction/completion correlation; fake decisions, callback live reads and cancellation preserving effects. |
| Authority and client bindings | Architecture only | Local owned queues/direct calls, distinct execution/result application, stale binding/revocation and pending/reply tests against fake peers. |
| N05/N02 foundations | Portable incremental framing, owned partial-send FIFO, UDP/envelope codecs, section checksums/mirrors/raw images, byte/word compression and bounded sequential wire-block application; 23 native/WASM Catch2 cases; independently authored original discovery and ID/ping drivers. See [network boundary](m1-network-foundations.md). | Complete producer/handler and world application, incremental differences, socket/routing/session integration, X02 registration and broader callback domains. |
| Original file/peer checks | Seven raw maps and 21 current saves reproduce exactly; three legacy files rejected. An actual original LAN host's ID greeting and UDP advertisement reproduce through the C++ codecs. The subsequent TCP ping timed out, with the original log reporting receive capacity 65536. | Bidirectional original file checks, reverse-role/full TCP sessions and ordered saved-game loading. The [peer report](conformance/reports/original-peer-windows.json) preserves the failed ping separately from the discovery pass; [stored-save evidence](m1-stored-saves.md) remains a file-only comparison. |
| Build/test exit gate | F01/F02/W01 native, WASM and sanitizer checks; Windows MSVC passes 93 default-off and 95 SQLite-enabled CTests; pinned WASM passes four CTests including 87 portable Catch2 cases; tools-only inspection without SDL/ImGui/assets/Lua/SQLite | Preserve native/WASM checks for further work; hosted cross-platform exporter checks; full completion audit. |

Implementation detail and retained reports are in [m1-foundations.md](m1-foundations.md).
The operation registry and coverage ledger remain the authoritative operation-level
inventory; this checklist covers the larger milestone deliverables.
