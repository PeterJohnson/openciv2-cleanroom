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
| F02 records, pools, retained state and lifecycle | `ProcessTokens`, typed identities, unit reservation; F02 fixtures | Complete pool records, deletion/load lifetime rules, retained workspaces and explicit process/new-game/load transitions. |
| W01 topology, coordinates and contiguous map state | Geometry, parity/aliasing, fallback, knowledge/coarse layers, retained summaries, ordered allocation/release and tile primitives; native/WASM tests | Integration with explicit new-game/load ownership; complete W01 region rebuilding and gameplay consumers remain later world work. |
| Versioned randomness and controlled choices | `std::mt19937_64` policy, isolated contexts and in-memory checkpoints | Semantic injection, retries/ties, pending-interaction checkpoint/restore and presentation-consumer invariance. |
| X02 v3 execution | Bounded F01/F02/W01 adapter, scalar/array/boolean closure, twenty fixtures, native/WASM reports | General typed values/blobs, scripted services/reentry, choice tapes, implementation replay, stop prefixes and independent fixture review. |
| Six built-in gameplay components | Complete JSON defaults, schemas and offline validation | C++ loading, owning consumer bindings, profile/checkpoint admission and original-peer gates. |
| Native content and classic text | Schemas and developer checks | Runtime manifests/dependencies/hashes, semantic rules/names/preferences validation, F03/C01 adapters, equivalent-input tests and conversion/validation commands with receipts. |
| Classic codecs and first save path | Specifications only | Raw maps and current save records, load/export, version/mode diagnostics, truncated/unsupported inputs and preservation tests. |
| Inspection CLI | Not implemented | `oc2 inspect`, `validate`, `diff`; human/lossless JSON output, stable slot ordering, unchanged source bytes, explicit unsupported sections and missing rules. |
| Scoped queries | Not implemented | Owned stable records/provenance, observer/authority checks, no mutation/RNG consumption, local session bindings usable by graphical and other clients. |
| Optional SQLite inspection export | Not implemented | Pinned optional dependency, OFF build without linkage, versioned exact rows/metadata, failure/cancellation and source-preservation tests. |
| SessionRuntime and lifecycle | Architecture only | Local world/session ownership, injected process context, distinct civilization/participant/controller/connection IDs and access/lifetime scopes. |
| Commands, effects and continuations | Architecture only | Typed descriptors/help/access metadata; acceptance/pending/interaction/completion correlation; fake decisions, callback live reads and cancellation preserving effects. |
| Authority and client bindings | Architecture only | Local owned queues/direct calls, distinct execution/result application, stale binding/revocation and pending/reply tests against fake peers. |
| N05/N02 foundations | Specifications only | Incremental framing/envelopes/state images, partial and fragmented/coalesced I/O, independently authored discovery/handshake comparison procedure. |
| Original file/peer checks | Target identity known; original-access gap recorded | Begin bidirectional checks for implemented formats, retain outputs where available and explicitly record unavailable captures/unimplemented fields. |
| Build/test exit gate | F01/F02/W01 native, WASM and sanitizer checks; dependency-free library build | All new portable gates on native/WASM; tools-only inspection without SDL/ImGui/assets/Lua/SQLite; SQLite-enabled cross-checks; full completion audit. |

Implementation detail and retained reports are in [m1-foundations.md](m1-foundations.md).
The operation registry and coverage ledger remain the authoritative operation-level
inventory; this checklist covers the larger milestone deliverables.
