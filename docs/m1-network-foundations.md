# M1 network foundations

Status: portable N05 framing and N02 envelope, image and codec primitives implemented.
This is a bounded part of the [M1 completion audit](m1-validation.md), not a
completed transport backend, lobby, synchronization service or playable session.

## Portable implementation

`OpenCiv2.Network` depends on portable formats/game/base libraries. It has no sockets,
OS event pump, GUI, Lua or database dependency. Implementations and authored test
expectations come from [N05 §§4–6](algorithms/N05-transport-and-wire-protocol.md)
and [N02 §§2,5–7 and Appendices A,C](algorithms/N02-network-commands-and-state-synchronization.md).
No reconstruction code or historical source-derived execution harness is used.

| Interface | Implemented behavior |
| --- | --- |
| `src/OpenCiv2.Network/Stream.cs` | Little-endian 12-byte headers, 64-byte sender rounding, exact supplied padding; incremental fragmented/coalesced receive; header-fragment-dependent oversize callbacks and discard of only the current receive; reset/close callback boundary; owned partial-send FIFO with live chunk capacity, immediate/drain retry differences and backlog thresholds; UDP header, u16 length narrowing and retained null-payload bytes. |
| `src/OpenCiv2.Network/Messages.cs` | Owned 16-byte message and 44-byte argument envelopes, sequence-preserving header initialization, directed/broadcast send sequencing, failure retention and live post-callback broadcast-size comparison. |
| `src/OpenCiv2.Network/Sections.cs` | Twenty-byte section prefixes, exact raw-image decoding/encoding, checksum width quirks, all 24 nominal section extents, registration, mirror allocation/invalidation, full/delta/join/map-header raw-image construction and specified old/new checksum ordering; sequential incoming block copies and checksum updates that preserve earlier blocks on failure. |
| `src/OpenCiv2.Network/Compression.cs` | Bounded byte/word run-length codecs; logical-input-only tail handling; safe word-run chunks through 32766; validated decoded sizes; owned image replacement on success; semantic statuses and full-state receipt-count codec selection. |

Input admission is explicit. Frames require at least 12 bytes and a payload that
fits the frame. The configurable 8 MiB receive backing limit is a safe profile,
separate from N05's callback-dependent oversized threshold. Receive does not
require the sender's 64-byte rounding. Short UDP packets are rejected; no wrapped
length fabricates received bytes. Message codecs require exact declared extents;
raw images require nonnegative data sizes and section IDs 0–23, while preserving
ancillary prefix fields. The state-section producer requires established backing
through each padded extent and nonwrapping sizes. It does not infer extra bytes
from the F06 layouts: the global section is 332 bytes and the map-header section
is 16 bytes, unlike their stored-file records.

Receive callback views are borrowed until reset or callback return. A callback
may reset/close its receiver, which must remain alive through `feed`; recursive
feed is excluded. The allocation hook is a fault-injection service, not an
application callback. Send and backlog services do not reenter their queue;
event posting occurs after queue publication. framework exceptions are not simulated
original failure codes and do not promise rollback. The queued IsConnected case
stops at the explicitly excluded retired-descriptor boundary. No false common
error result is invented for it or for retention-allocation failure.

Raw-image construction requires the caller to materialize wire sections and lock
the event pool. Its compression tail is a separate operation. Initial producer
allocation, delta resize and append-report callback effects remain open. The block
application primitive copies into materialized wire backing, ignores ancillary
received prefix fields and recomputes checksums from the incoming payload. It
does not read runtime object bytes or publish wire fields to a live world.
Its consumed count records inspected header/payload bytes, not the original
application's signed consumed-offset workspace.

Production encoding consumes only the logical payload. Distinct tails finish as
literals; word runs are split at 32766 to avoid unsafe code 32767. These documented
N02 repairs use existing wire tokens but do not claim original allocation-dependent
byte equality. Word decoding rejects ambiguous code 32767, truncated payloads and
decoded-size overflow. Image decoding has an explicit 8 MiB default bound.

Image transforms stage owned results and preserve input on no gain, invalid input,
resource limits or allocation failure. Interfaces have no original lookahead,
null-publication stages, allocator callbacks or native integer return values.
Tests cover exact supported tokens, long runs, literal chunk boundaries, all 256
possible final bytes, failure retention and round trips. Original mixed-peer
compression validation remains open; existing framing captures do not cover it.

Shared codec selection uses the supplied full-state receipt count, regardless
of the current message kind or compression flag when that count is nonzero.
These helpers do not implement the enclosing handler's ignored child return,
packet resize, option restoration, map release/reallocation, event relocation or
session ownership. Live mirror scanning/diff application, native sockets,
connection IDs, accept/timeout barriers, routing, control handling and N01–N03
admission remain open.

## Validation

Specification-derived shared native/browser cases cover every split of a two-record stream, one-byte feeds, coalescing,
partial sends, retries/failures, callback reset, retained padding, sequence wrap,
live callback reads, checksum quirks, section/mirror ordering, exact compression
tokens and ordered partial image application. These bounded-codec tests are
not registered X02 execution fixtures or
original-program observations.

## Original-program procedure

Use an isolated copy of the original installation, launch `civ2.exe` with that
copy as its working directory, and retain SHA-256 identities for the executable
and XDaemon DLL. The Windows host's runtime/setup notes were consulted only for
operational guidance; its reconstruction sources and harnesses are excluded.
The original executable is never linked into a cleanroom target.

The first launch can display **Civ2 Diplomatic Heralds**. Accept the chosen
display preference, then use Multiplayer Game → Network Game → TCP/IP → Start
a New Multiplayer Game. Complete ordinary world/rule/name options and leave the
host in its waiting lobby. LAN mode matters for a later discovery capture:
N01 deliberately suppresses GetServers replies for an Internet-mode host.
Use fictional player/game names for the experiment. Host execution is required
for socket checks on this Windows machine; do not alter security settings to
work around a sandbox failure.

The independently authored driver connects to an explicit endpoint, captures
the ID greeting, sends a fully authored ping request in three-byte writes, and
requires an equal ping payload in the reply. Writes do not establish the OS's
receive boundaries. `OpenCiv2.WireRoundTrip` then checks exact reconstruction through
one-byte and single-buffer feeds, including opaque original padding and any
complete N02 message envelopes. No original process memory or internal function
entry points are used.

```powershell
.\.venv\Scripts\python.exe tools/probe_original_peer.py `
  --host 127.0.0.1 --port 4993 `
  --original-exe build/original-peer-runtime/civ2.exe `
  --original-transport build/original-peer-runtime/XDaemon.dll `
  --checker tests/OpenCiv2.WireRoundTrip/bin/Release/net10.0/OpenCiv2.WireRoundTrip `
  --output build/original-peer-capture
```

`tools/probe_original_discovery.py` supplies one N02 GetServers request over
loopback and listens on the shared discovery port for the original's broadcast
ServerInfo response. It requires the explicit game label and runs the wire
checker's `--datagram` mode. Use the same original/checker/output arguments as
above, replacing the script and port with `--port 4994 --game-name 'M1 Wire Test'`.
Its N05 header zero and empty target/exclusion fields are authored test inputs.
It does not establish every N01 address-filter branch.

## Windows original observations — 2026-11-14 UTC

The [retained report](validation/README.md) records an
actual unmodified original-program run from the isolated copy. The TCP listener
on port 4993 belonged to that original process. The host sent a 64-byte ID frame
assigning connection 1 about 512 ms after connection initiation. The wire checker
reconstructed it exactly with one-byte and coalesced supply.

The subsequent authored ping received **no reply before the 10-second timeout**;
the combined TCP probe remains failed. The original XDaemon log reports a
65,536-byte receive buffer for the accepted stream. This is consistent with the
low-16-bit receive-capacity issue documented in N05 Appendix D; no internal
receive call was inspected and no binary, socket shim or system setting was
changed. A greeting observation is not a completed bidirectional TCP session.

UDP discovery passed: the original replied to the authored request with a
412-byte datagram (four-byte N05 prefix plus 408-byte ServerInfo), advertising
the expected test name and `5.4.0f Multiplayer 26-March-99`. The datagram and
message codecs reproduced every byte. Raw captures remain under ignored `build/`;
the checked-in report retains hashes/results and omits the original advertisement
bytes, which contain local addresses and retained backing.

Reverse-role handshakes, bidirectional TCP after resolving the original-host
receive limitation, full application admission, original state images and
gameplay agreement remain separate acceptance work.


Current C# sources and tests are mapped in [implementation coverage](testing/coverage.md).
Run the [native/browser and CLI checks](testing/README.md);
[validation status](validation/README.md) records executed evidence and remaining gates.
