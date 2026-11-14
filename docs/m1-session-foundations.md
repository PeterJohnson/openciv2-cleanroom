# M1 session ownership and client bindings

Status: local world/process ownership, scoped client lifetimes and own-entity
queries are implemented. This is part of the [M1 completion audit](m1-validation.md),
not a playable session or a completed setup/load, turn-loop or authority adapter.
The contract comes from [architecture §§3–5](architecture.md#3-state-and-deterministic-execution)
and [F02 §§2–3](algorithms/F02-game-state-and-invariants.md).

## Ownership and lifecycle

`oc2_session` depends on portable query/formats/game/base libraries. Local access
uses C++ calls without a transport, GUI, Lua, SQLite or runtime assets.
`SessionRuntime` is the trusted composition/engine surface; applications give
clients only a `PlayerClient`. All calls run on one cooperative mutation lane.

The runtime owns `state::GameState`: the existing stable entity pools, global,
civilization and name records, retained mood workspace and map storage. It uses
an injected `ProcessContext` containing lifetime tokens, leader usage/name
cursors and the shared fallback tile. That context must outlive the runtime.
Independent virtual peers supply separate contexts. Rules/preferences, further
retained workspaces and full classic initialization remain owning-spec work.
Construction defaults are not claimed as original setup behavior.

`begin_client_session` replaces the client lifetime, revokes all old bindings,
clears participant mappings and initializes independent gameplay and presentation
contexts from explicit seeds. Their policy context name is `session`; cosmetic
randomness remains frontend-owned. Rebinding or changing controllers does not
reset either context. Restoring a pending checkpoint must eventually restore its
contexts and continuation explicitly, rather than call this fresh-context entry.

This lifetime operation deliberately does not perform F05 new-game reset or F06
load transformations. World, map and process values remain installed until their
owning operation changes them. Ending a client session similarly revokes access
without clearing those values. Existing F02 entity-reset helpers can be called
at their specified boundaries without resetting process tokens or name cursors;
their integration into the complete ordered setup/load paths remains open.

The composition root supplies a stable nonzero `RuntimeId`, unique among
runtimes whose observations will be compared. `SessionId`, `ParticipantId` and
`ControllerId` are separate nonwrapping identities allocated within that runtime.
`ConnectionId` is a separate optional endpoint mapping. These identities never
enter classic payloads and are not derived from pointers or native handles.

## Access and queries

The runtime admits a participant's civilization and optional connection, then
issues each controller a private lease containing its session, participant,
observer and access class. A client cannot construct a lease or change its grant
by editing the returned identity value. Copies of a client share its lifetime.
Multiple frontend/script/automation sources can bind the same participant.
Controller kind is metadata: attaching an engine policy or automation never
changes the world's effective human-player set.

Revocation invalidates only that controller. Participant reassignment/removal
revokes all its bindings. Disconnect revokes all bindings mapped to that endpoint
and clears the connection mappings while retaining the seats and world effects.
The mode's disconnect continuation and required protocol replies are not yet
implemented. Runtime destruction, session end and replacement invalidate all
old clients. A new binding is a trusted owner action; the later mode/authority
adapter must establish its current admission before issuing it.

`own_units()` and `own_cities()` return owned projections with runtime/session,
observer, publication revision and `owned_current` provenance. They enumerate
occupied records below the actual pool scan bound in slot order, preserve
slot/token identity, and omit other owners and retained records outside that
bound. They read no RNG context and do not recompute retained counts or workspaces.
Unit fields cover position, type, movement/damage, order and home. City fields
cover position, name, size, production and food/shield stores. Names contain
classic bytes before NUL, excluding retained tail bytes; internal visibility
masks, AI workspace and unrelated record fields are not exposed.

The engine calls `publish()` at its specified publication boundary before
allowing a client query; it increments the observation revision without computing
or repairing state. Trusted world access is available only through the owner,
not through `PlayerClient`. Query results retain their original values after
later changes. Revocation cannot erase copies a consumer already received, so
frontends must also clear/isolate their presentation caches on a handoff.

These are specifically own-entity queries, not a claim that W03 enemy visibility,
remembered map queries, omniscient live inspection, semantic report operations
or graphical integration are complete. The developer access label does not
broaden these queries. Offline save inspection remains a separate API/context.

## Validation and remaining work

Eight authored Catch2 cases cover private grants, own-record filtering and stable
order, non-mutation of state/RNG, owned observations, stale client copies,
reassignment, removal, disconnect, session replacement/destruction, controller
independence from human flags, retained process values and isolated virtual peers.
They exercise the separate F02 entity-reset helper and independent presentation
consumers, without claiming a completed F05 setup or original mode transcript.

Windows MSVC passes 101 default-off and 103 SQLite-enabled CTests. The pinned
Emscripten 4.0.23 build passes four CTests, including 95 portable Catch2 cases
and the existing twenty X02 vectors. The tools-only configuration also builds
the session library without GUI, tests, dependency fetching, Python discovery
or assets. Retained native/WASM reports were refreshed for build/source identity;
X02 execution coverage is still F01/F02/W01, not these session cases.

Next session work is the command descriptor/admission contract, owned command
ordering, explicit effects/continuations and correlated pending results; decision
replies, cancellation, callbacks and revocation must preserve committed effects
and required replies. Mode-specific authority and request/result application,
complete lifecycle/setup/load, controlled-choice and pending-checkpoint replay,
and broader live queries remain required by M1.
