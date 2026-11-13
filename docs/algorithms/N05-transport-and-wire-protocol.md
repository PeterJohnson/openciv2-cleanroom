# N05: Transport and wire protocol

Target: Civilization II Multiplayer Gold Edition 5.4.0f,
patch 3, with its XDaemon **3.5.0** library. This document specifies transport
initialization, TCP/SPX framing, datagrams, connection IDs, routing, queued
sends, receive assembly, callbacks, timeouts and teardown. It also specifies
the modem/serial and lobby adapter contracts, separating recovered algorithms
from the availability of native legacy providers.

[N01](N01-multiplayer-session-and-lobby.md) owns game discovery, player/session
identities, connection-setting choices and host migration. [N02](N02-network-commands-and-state-synchronization.md)
owns application messages, queues, dispatch, compression and synchronization.
[N03](N03-multiplayer-turn-sequencing.md) owns turn/heartbeat/timer policy.
Transport payloads are opaque here. A connection ID is not a civilization ID.
P05 owns the application's native message pump; this contract states when that
pump admits transport callbacks. F01 owns shared arithmetic conventions.

Historical protocol captures, proposals and reconstruction comparisons are
recorded in Appendix C; Appendix B lists their material differences. The framing
and delivery contracts below are the implementation inputs.

The required TCP/IP path must connect to unmodified original-game peers and
preserve the framing, routing and delivery contracts below. RNG replacement
requires no transport extension or RNG-version negotiation. Do not require an
original peer to install an adapter or understand new packet fields; optional
extended modes cannot replace the classic path. Mixed-peer TCP/IP validation
with both host/client arrangements remains required. Optional provider availability and
evidence gaps are tracked separately. N01–N03 own application admission,
operation authority and state agreement after transport delivery.

**Equivalence conventions:** [F01's numeric rule](F01-determinism-and-arithmetic.md#behavioral-equivalence-and-logical-ranges)
and [X01's ordering rule](X01-classic-compatibility-contract.md#behaviorally-significant-ordering)
apply. Widths and written sequences define reference evaluation, not mandatory
internal types or scheduling. Equivalent evaluation may omit redundant conversions
and reorder independent work; preserve significant arithmetic, dependencies,
state publications and failure effects. Without an equivalence argument, use
the reference order. External file/wire encodings remain exact.

Handshake, framing, routing, queue/accept barriers, callbacks and deadline
comparisons retain their order and failure effects. Equivalent buffer preparation
does not permit reordering required bytes, deliveries or completion notifications.

## Compatibility policy

X01's [transport policy](X01-classic-compatibility-contract.md#required-and-optional-transports)
requires original-compatible TCP/IP streams and UDP discovery. TCP portions of
§§2–8, their supporting initialization/teardown and N01–N03 application joins are
required. IPX/SPX-specific branches, §9 modem/serial/launch-lobby providers and
provider-specific native registration/launch services are optional reference
extensions. Shared code still supplies required behavior when used by TCP/IP;
optional-provider absence must not disable that path or hotseat/file loading.
A shared original startup routine invoking a legacy provider does not make that
provider mandatory: implement its required TCP/IP effects without requiring the
legacy library, registry setup or external launch service.

The [input-admission policy](X01-classic-compatibility-contract.md#input-admission-and-rejection-policy)
permits early rejection of malformed frames, unsupported lengths or invalid
provider requests outside the declared nominal domain. It does not eliminate
normal fragmentation/coalescing, short socket operations, retries, original
routing or valid peer replies. Original malformed-packet traces below apply only
to explicitly supported off-nominal domains. Harmless logging/error-dialog
presentation may differ under X01, but required transport errors, disconnects,
queue effects and protocol cleanup remain specified.

## Implementation boundary

The main sections use logical values, identities and operations. Original memory,
allocation and platform mechanics are recorded in [Appendix D](#appendix-d-original-implementation-reference). Preserve required
state changes and externally visible results without reproducing those mechanisms.
Exact numeric domains and file/wire encodings remain required wherever specified.

## 1. Domain, state and observations

### Nominal application use

The game registers broadcast, secure, connected, new-client, connection-lost
and oversized callbacks. The oversized limit is 50000. Its TCP/IP setup requests
both stream and datagram facilities, stream port 4993, datagram port 4994 and
seven slots including the host. IPX/SPX uses the same requested port values;
its stream address uses the fixed SPX socket described below. Local hotseat
bypasses these transports.

Nominal frames have complete headers, payloads within their records, available
storage, initialized callbacks and valid peer identities. Nominal caller strings
are terminated and fit their destination fields. OS services and allocations
can fail; callbacks can change library or game state. A cleanroom implementation
needs explicit equivalents for those effects, not assumed-success services.

Accepted off-nominal scalar behavior is stated separately: wrapped lengths,
zero timeouts, missing callbacks, unusual capacities, partial records and provider
failures. A stricter profile may reject malformed inputs before admitting them.
For admitted inputs, do not silently substitute clamps, inferred initial values
or new retries for the operations below. [X01](X01-classic-compatibility-contract.md#out-of-bounds-writes-are-excluded-bugs)
excludes out-of-bounds writes, including cross-field string corruption. Stop or
omit unsafe operations while retaining preceding valid effects. Reads without
produced backing require supplied input or a bounded stop; a reconstruction
repair is not proof of an original value.

### Logical state

Keep these identities and lifetimes distinct:

- Library protocol: uninitialized −1, TCP 0, IPX/SPX 1, modem 2, serial 3.
- Server role, independent broadcast/secure enables, and signed local connection
  ID, initially −1. A server normally publishes ID 0.
- Registered callbacks, oversized limit, last-error value and in-flush flag.
- Listener/client stream, optional datagram socket, connection table and live
  table count. Each table entry holds a socket identity and a stream reference.
- Per stream: connection identity, protocol/port, receive capacity,
  partial-header bytes, current frame length/received count/storage, send FIFO
  and its backlog-report threshold, plus accept/receive/close callbacks.
- Global send-chunk capacity: initially 512; creating a TCP stream sets 8192,
  creating an SPX stream sets 512. It is not captured permanently per stream.
- Connection start tick, timeout and logical timeout registration; latency tick/callback;
  address history; modem/serial provider, session and worker state.

Logical connections use their assigned slot IDs, with −1 for an unassigned
local ID. The normal TCP table has the admitted capacity up to 248; this is an
identity domain, not a requirement for a signed16 variable. Preserve `s16` when
decoding the ID handshake, `u16` destination conversion and the low-eight-bit
source/destination routing fields. In particular, unassigned −1 and destination
65535 are different values. Exact field widths remain in the wire tables.

Counts, lengths, tags and clock differences use F01 reference results; preserve
modulo-2³² differences and signed interpretations at their stated tests. Ordinary
bounded calculations may use sufficient-range integers. Original scalar
annotations are in [Appendix E](#appendix-e-original-numeric-annotations).

### Callback and error model

Serialize required transport callbacks at the application dispatch boundaries.
A flush can admit both transport and application interaction events, including
reentrant work; preserve their selected order and live-state reads. A different
threading/event framework must not introduce arbitrary background callbacks that
change those observations. Native window dispatch and optional legacy workers
are described in [Appendix D](#appendix-d-original-implementation-reference).

The six callback setters—broadcast, secure, new client, lost connection,
client connected and invalid self-send—first clear last-error, then store the
callback. Setting an oversized callback stores it first and uses the supplied
limit when nonnull, otherwise `0xfffffff0`; it returns 0 and preserves last-error.
Ordinary library reset retains all callback registrations and that limit.

Callback payloads are borrowed for the duration of the call. N02 owns any copy
into an application queue. A callback may reset or destroy transport state;
continuations re-read live fields at their stated sites. Destruction followed
by original accesses to retired storage is a separately bounded bug path,
not permission to manufacture replacement-frame contents.

Do not treat every negative return as a new last-error assignment. Operations
below identify stored errors. Internal send results, several provider returns,
self-send rejection and successful StopConnections have distinct conventions.
N01/N02 own how game callers interpret them.

## 2. Initialization, slots and address production

### TCP and IPX/SPX library initialization

For `(facilities, streamPort, datagramPort, asServer, capacity, broadcastCallback)`:

1. Clear last-error. If already initialized, store/return −4. If unsigned
   capacity exceeds 248, store/return −11.
2. Require either an already registered broadcast callback or the supplied
   callback, even for secure-only mode; otherwise store/return −9. If there
   was none, register the supplied callback **before** validating facilities.
3. Facilities 1 selects secure only, 2 broadcast only, 3 both. Other values
   store/return −9. A server without secure service stores/returns −9 after
   publishing the selected enables.
4. For TCP, publish both ports only when broadcast is enabled. Secure-only TCP
   therefore uses the retained stream port. IPX/SPX publishes both ports here
   regardless of facilities.
5. If broadcast is enabled, close and retire the previous datagram service if any,
   create its replacement, and initialize it with protocol 0 or 1, nominal
   payload capacity 512, the datagram port, broadcast enabled and the registered
   callback. Allocation or initialization failure stores/returns −5.
6. If secure is enabled, close and retire the previous stream if any, create its
   replacement and initialize it with protocol, the retained stream port's low
   word and the supplied server flag. Failure stores/returns −5.
7. A client publishes server-role 0 and installs the shared receive and lost
   handlers. A server installs the accept handler, publishes server-role 1 and
   ID 0, allocates the connection table and assigns the listener to slot 0.
   Neither table-allocation nor slot-assignment result gates final success.
8. Publish protocol 0 or 1 and return 0.

Failures retain preceding registrations, enables, ports and partially created
objects. There is no general transaction rolling them back. In particular,
protocol is published only at final success, while object/enabled state can
already exist. Zero capacity is accepted by the outer scalar checks; it does
not create a usable listener slot. A stricter profile can reject that input.

### Stream initialization and acceptance

For required TCP/IP, create a logical IPv4 stream with its selected port and
server flag, backlog-report threshold 50, and global send-chunk capacity 8192.
A server binds to any local address at that port; a client does not bind here.
Failure to establish the stream or its receive service returns false, retaining
preceding published state. Accepting a stream requires an unused connection
identity; on success enable Read, Write and Close delivery. Failed acceptance
returns false. Reinitialization recreates these services using the retained
protocol, port and server flag and reestablishes the global chunk capacity.

Receive capacity governs individual reads, not frame size. Preserve ordinary
fragmentation/coalescing, complete record assembly and the event-order contract.
The original OS buffer-size query, low-word narrowing, differing fallback sizes,
Windows targets, socket-library startup and optional SPX setup are in [Appendix D](#appendix-d-original-implementation-reference).
They describe a reference backend, not a mandated host buffer allocator. In
particular, the OS-size-65536/zero-read failure is a native-backend case, not a
requirement that a new backend prevent an otherwise valid peer from connecting.
An adapter claiming that exact reference case must retain its narrowed limit.

### Connection table

Creating the logical connection table initializes every slot as unused and
publishes capacity on success; it does not independently reset the used count.
Retiring the table clears its presence, capacity and count. Assigning a specific slot succeeds
only when the table exists, the index is below capacity and its socket is invalid;
write socket, then stream, increment count, return index. Failure returns −1 bits.

Assign-next rejects a missing table or `capacity == count`, then scans from 0
for the first invalid socket. Publish it as above. There is no random allocation
or monotonic ID counter; released IDs are reused. Removing a stream scans from
0, clears the first matching entry and decrements count. Lookup by index requires
index below capacity. Lookup of ID by socket rejects the invalid-socket sentinel;
lookup of stream by socket does not add that rejection. Normal accepted peers
use 1–6 because slot 0 already contains the listener.

### Local protocol address

Clear last-error and the shared output string first. If both current protocol
and requested selector are −1, store error −1 and return null. When current
protocol is uninitialized, request address-service initialization; its result
does not gate the following branch. The explicit selector determines the
branch; −1 is not an instruction to infer the current protocol.

For selector 0, request the hostname, resolve it, and scan the returned address
list by ordinal. A null resolver result returns null. At the requested index,
convert the IPv4 address to text and copy it into shared output. A missing ordinal
returns the still-empty, nonnull output. The original ignores hostname-production
failure and can consume unproduced local bytes; supply that input or stop before
resolution. BF-300's immediate null return is a repair. There is no original
first-non-loopback preference or environment-variable override.

Selector 1's optional IPX address production is recorded in [Appendix D](#appendix-d-original-implementation-reference).
Other selectors return null. Provider results remain external inputs.

## 3. Listening, connect and the ID handshake

ActivateServer clears last-error; uninitialized stores/returns −1, a nonserver
−6, and a missing secure stream −3. For TCP, enable accepting and listener-close
notifications and begin listening. Success returns 0; service failure returns
−5 while last-error retains what preceding operations produced. Optional
providers use the reference branches in [Appendix D](#appendix-d-original-implementation-reference).

StopConnections clears last-error; uninitialized stores/returns −1, nonserver
−6. For TCP, a missing stream stores/returns −1; otherwise disable listener event
delivery and return **1**, leaving the listener open. Other protocols return 0.
Existing peers are not shut down. Native event registration and listen-backlog
parameters are reference mechanisms.

### Open a client connection

1. Clear last-error. A server stores/returns −7. A missing connected callback
   stores/returns −15. Then set local ID to −1.
2. For TCP/SPX, require secure enabled, otherwise store/return −14. A missing
   secure object selects the common failed-connect continuation below.
3. TCP converts the supplied address using the original IPv4 address service;
   it does not perform hostname resolution at this site. Register Read, Write,
   Connect and Close events before the nonblocking connect call.
4. SPX parses the writable text and calls its provider connect operation as
   described below. Both branches select this stream as the global connecting
   object before event registration.
5. An immediate successful connect, or a socket error equal to WouldBlock,
   means the connect operation was accepted. Other socket errors fail.
6. On acceptance, request a timeout callback with the supplied duration and
   publish its registration result, then capture the current tick and timeout.
   Return 0 even if scheduling failed.
7. On failure, retire the current temporary stream, create/publish its
   replacement, install receive/lost handlers and attempt client initialization.
   Then store/return −10. The earlier failure does not bypass replacement.

A timeout is a wrapping unsigned tick duration. N01 owns the configured LAN and
Internet values and their conversion. For an unsupported protocol selector,
the original's local result can be unproduced; do not infer a uniform −1 return.
Modem/serial uses §9 and returns 0 after its successful connected callback, else −10.

Native autodial effects and optional SPX address parsing are in [Appendix D](#appendix-d-original-implementation-reference).

### Host accept

Allocate a stream, accept and initialize it, then assign the first free table
slot. Allocation, accept or registration failure cleans up the produced stream
as applicable and returns without a new-client notification. A full table closes
the accepted connection.

After registration, **prevent further application/transport event dispatch for
500 ms**. Attach receive/lost
handlers, then send one framed record with exact tag `0xfa000000` and payload
the assigned ID encoded as an unsigned 16-bit little-endian value.

The internal send must return **exactly 2**, meaning sent immediately. Any other
result, including 1 for queued, removes and destroys the accepted connection.
Nevertheless, continue to the new-client callback with the then-current table
count and the assigned ID. On send result 2, query/store the peer address first,
then invoke that callback if registered. A missing callback only causes the
original diagnostic message. The count includes the listener when still present.

A scheduler barrier can implement the delay without blocking an OS thread,
but must preserve the same exclusion of intervening dispatch. Merely delaying
the greeting while processing other events is not equivalent. Queued greeting
failure retains its cleanup/notification sequence.

### Receiving an ID and timeout

An exact `0xfa000000` tag is an ID handshake only when currently a client and
local ID is −1. If `u32(now-start) < timeout`, read the first two payload bytes
into signed local ID, with no separate payload-length guard. If a connection
timeout is registered, cancel that registration and clear it. Query/store the peer
address, then invoke connected(localID) if registered and return.

The equality deadline is already late. A late ID destroys/replaces the temporary
stream and invokes connected(−1) if registered. The original then continues
through ordinary routing with live post-callback state. For an externally owned
payload, that continuation can be specified with its valid backing. When called
by the frame parser, the replacement destroyed the very frame and receiver still
in use: stop after the preceding valid reset/failure effects before retired
access. BF-302's safe return/deferred storage release is a separate repair.

The timeout callback, when dispatched, replaces the stream and calls
connected(−1) only if ID is still −1. It then cancels the timeout registration
and clears it regardless.
It does not independently compare elapsed time at that callback. An ID arriving
out of handshake state is an ordinary routed record, not automatically discarded.

## 4. Stream wire representation and sending

These bytes are part of interoperability, not a required internal object layout.
TCP and SPX share this application record envelope. All three words are unsigned
32-bit little-endian:

| Offset | Value |
| --- | --- |
| 0 | Total record length, including header and padding. |
| 4 | Tag: `sourceLow8 << 24 | destinationLow8 << 16 | flagsLow16`. |
| 8 | Payload length. |
| 12 | Payload bytes, followed by padding through total length. |

For a normal sender, `total = roundUp64(u32(payloadLength + 12))`.
Examples: payload 2 → total 64; 100 → 128; 308 → 320; 3002 → 3072.
Records form a byte stream: one OS receive may contain part of one record or
several records. No TCP-segment boundary is a record boundary. The library does
not encrypt, authenticate or add a checksum to this envelope.

Padding has no receiver-level meaning. The original shared send storage starts
zeroed but retains earlier bytes; dynamically allocated padding has allocator
backing. For an exact wire-byte comparison, establish/supply those bytes or
exclude padding from comparison. A replacement may use deterministic padding
when targeting payload/callback interoperability; it must not assume received
padding is zero. N02 independently owns meaningful application payload bytes.

### Build and attempt one record

Given payload length `n`, bytes and tag:

1. If unsigned `n >= u32(oversizedLimit - 12)`, log and return −1 without calling
   the oversized callback or copying payload. The threshold is used even when
   the callback is null.
2. Compute `length = u32(n+12)` and, if not divisible by 64, replace it with
   `u32((length & ~63) + 64)`. Wrapped small allocations followed by oversized
   writes are excluded; arithmetic wrap does not authorize their corruption.
3. Use shared send storage only if length fits its capacity, fits the **live
   global chunk capacity**, and this stream's queue is empty. Otherwise allocate
   length bytes; failure returns −2.
4. Store total, tag and payload length in that order, then copy the payload
   forward in four-byte groups and trailing bytes. This is not a backwards
   overlap-safe move. With separately backed nonoverlapping input the result is
   the ordinary byte copy; accepted aliases observe the stated write/read order.
5. If the queue is already nonempty, append the entire record with offset 0,
   post a Write event and return 1. There is no direct socket send in this branch.
6. Otherwise request a socket send of `min(length, liveChunkCapacity)` bytes.
   Handle its result below.

On a socket error, WouldBlock, InProgress, Interrupted and NoBuffers are treated
as zero bytes sent. For other errors, release newly allocated record storage;
IsConnected (10056) returns −3, all other such errors return −4. This internal
helper does not itself invoke connection-lost or close the connection.

If fewer than length bytes were sent, retain the **whole record**, including
its padding, independently of subsequent sends; record the sent offset, append
to the FIFO, enqueue Write and return 1. Otherwise retire temporary record state
and return 2. Failure to retain the pending record cannot fabricate successful
queueing or an invented common −2 return. Native descriptor allocation and
shared-to-private copying are in the implementation appendix.

### Draining the queue

If empty, the one-item drain returns 1. Otherwise sample the queue count. When
it exceeds the reporting threshold, log and set threshold to sampled count+50;
when it is below 50, set threshold to 50. Then take the first record and remove
its queue node **before** calling send on up to the current global chunk capacity
of its remaining bytes.

Only WouldBlock is retryable in this queued path. A different socket error logs
and frees the packet bytes and descriptor. Ordinary fatal errors return −4.
For IsConnected, the original falls through to reads and repeated frees of the
retired descriptor. Stop before that invalid continuation; BF-303's return 0
after one cleanup is a documented repair. Do not turn the immediate-send −3 rule
into a blanket rule for queued sends.

For zero/partial progress, increase the packet's sent offset by the accepted
count, reinsert it at the **front**, post Write and return 3. For a completed
record, free it and return 0. FIFO order remains stable across partial sends.
A Write-event handler repeatedly drains while records remain and results are 0.
On −4, invoke the stream's close handler if present, then stop; any other nonzero
result also stops that event's loop.

### Public secure-send routing

Clear last-error. Uninitialized stores/returns −1; secure disabled −14. Compare
signed local ID with the unsigned-16 destination: equal means log, invoke the
optional invalid-self callback and return −9 without assigning that error.
The unassigned ID −1 is not equal to destination 65535.

For TCP/SPX a missing main secure object stores/returns −5. A client sends every
accepted destination through its host stream. A server sends a specific target
through that table slot; a missing target stores/returns −13. Build the tag from
the low byte of live local ID, destination's low byte and flags' low word.
Return the internal send result. On −4, call the connection-lost handler with
the socket identity before returning; callbacks may change the live transport.

A server destination 255 scans client slots 1 through the live table extent in
ascending order. For each extant stream, send a distinct tag naming that slot;
there is no host-generated destination-255 frame. Continue after negative
results, invoking loss on −4 and remembering that any failure occurred. Any
failure stores/returns −13; otherwise return the original payload size's bits
as a signed result, including when no client existed. Thus broadcast success
returns payload size, not necessarily 1 or 2.

For modem/serial, the public call delegates to §9's reliable provider send.
Other initialized protocols store/return −13.

## 5. Receive assembly and routing

### Incremental frame parser

The parser takes the number of available bytes in the stream's receive buffer.
Zero bytes or a null buffer returns immediately. Keep an independent received
count, partial four-byte total field, pending total and pending allocation.
Repeat while bytes from this receive remain:

1. If received count is zero and fewer than four bytes remain, copy them to
   partial-header storage, set received count to that amount, set pending total
   to zero and return.
2. If received count is zero with at least four bytes, read total directly.
   If an oversized callback exists and unsigned
   `total > u32(oversizedLimit-12)`, call it with `u32(total-12)`, clear pending
   total/received count and return. Total zero also clears those counts and
   returns. Otherwise allocate total bytes; allocation failure clears the two
   counts and returns.
3. If a previous receive supplied one to three header bytes, complete those
   four bytes first. If still incomplete, append, increase received count and
   return. On completion, advance input by the bytes just used, set received
   count to 4 and read total from the partial header. The same oversize test
   calls its callback with **total**, without subtracting 12, then clears both
   counts and returns. Allocate total bytes; failure clears counts and returns.
   Copy the complete four-byte total into the new allocation. A total below
   four would make that copy an excluded write.
4. Compute `needed = u32(total-receivedCount)`. If available is below needed,
   copy all available bytes into the frame at receivedCount, increase that
   count and return. Otherwise copy exactly needed to complete the frame.
5. If the stream has a receive callback, read tag first, then payload length,
   and call it with payload position, declared payload length and tag. There
   is no additional multiple-of-64 check or clamp of payload length to total−12.
6. After a valid returning callback, advance the current input and reduce
   available by the **captured needed count**. Clear received/pending counts,
   free the then-live pending allocation, null it and continue with remaining
   input. With no callback, skip the tag/length reads but perform this cleanup.

Copies use forward groups of four bytes and trailing bytes. Ordinary buffers
are distinct. A callback that replaces pending storage affects the later live
free; a destroyed parser cannot be treated as the old live object. Appendix B
separates the original retired-object path from BF-302.

An oversize/zero/allocation-failure return discards the rest of **this receive
buffer**, not a calculated complete wire record. Later socket bytes start a
new total field. There is no drain-until-total recovery. Fragmentation therefore
affects both the oversize callback value and subsequent stream alignment.

Nominal total is at least 12 and declared payload fits. With accepted shorter
records, a callback first needs backed tag bytes, then backed length bytes;
missing bytes are read boundaries. A length exceeding total−12 can reach a
callback as a scalar, but reading/copying that claimed payload requires its own
backing. Do not manufacture tail bytes or require a resulting overwrite. A safe
implementation may reject that record at its admission boundary.

### Ordinary record routing

First test the control tags in the next subsection and §3. For an ordinary tag,
extract sender from the top byte and destination from the next byte. These are
**claimed tag values**; this algorithm does not authenticate them against the
receiving socket's table entry.

For a server:

- Destination 0 with a registered secure callback is delivered locally with
  claimed sender, payload, declared length and tag-derived flags; then return.
- Destination 255 with a registered secure callback first scans client slots
  1 through the live table extent, excluding the claimed sender. For each
  existing stream, relay with a new tag formed from the **live local connection
  ID**, that slot and the incoming low-word flags. Ignore internal relay send
  results; this route does not add the public sender's loss handling. Re-read
  table extent after each iteration. Then deliver locally with the original
  claimed sender and original tag.
- Otherwise look up destination and forward the original tag unchanged if a
  stream exists; if not, only emit the original diagnostic. Do not deliver
  locally in this branch.

The registered-callback tests are part of the branch selection. With no secure
callback, destination 255 is not broadcast-relayed; destination 0 can fall into
ordinary table lookup instead. A client delivers every ordinary record to its
secure callback when present, irrespective of destination. With no callback,
it discards application delivery.

A relayed client broadcast therefore normally appears to other clients as
sender 0, while the host's local callback sees the original sender. A direct
client-to-client relay retains its sender byte. N02 decides what the received
application payload means; transport does not infer its authority.

### Control records and ping

Exact tags `0xfc000000` and `0xfd000000` are ping request/reply controls on both
roles. A ping request reads an unsigned ID from the first two payload bytes.
A server looks up that ID and, if found, sends the same payload/length with the
reply tag; a client sends it through its main stream. Ignore the send result.
Do not deliver the control as an application message.

For a reply, compute wrapped elapsed ticks from the retained latency start.
The reported packet count starts at 1. Only if global chunk capacity is below
payload size, replace it with integer `size / capacity`; if capacity is zero,
use size as the divisor instead. Call the retained latency callback when
registered, then return. For example, size 16385 with capacity 8192 reports 2,
not a ceiling of 3.

The game's imported surface does not initiate XDaemon ping requests, but peers
can send controls. The library-only request export sets latency start before
checking initialization, rejects disabled secure service and destination 255,
changes a requested size below 32 to 64, allocates the payload and writes only
its local ID word. Remaining payload bytes need allocator backing for an exact
comparison. A supplied nonnull callback replaces the retained latency callback.
Select host destination/client main stream, send the request tag, release the
payload and return the internal result. No independent retry or timeout is added.

An exact ID tag has special handling only in §3's unassigned-client state.
An out-of-state ID tag takes ordinary routing, including delivery to a client.
The three reserved high-byte patterns are not a blanket discard rule.

## 6. Datagrams and socket events

### Broadcast send

Clear last-error. Uninitialized stores/returns −1; broadcast disabled −14.
TCP/IPX with no datagram object stores/returns −3. Reduce the caller's payload
length to unsigned 16 bits **before** limiting it to the object's capacity minus
four, nominally 512. Thus 65536 requests zero payload bytes and 65537 requests
one, not 512. A datagram object that is inactive returns 0.

Store the header as a four-byte little-endian signed/unsigned bit pattern. If
payload pointer is nonnull, copy the selected bytes forward in four-byte groups
and trailing bytes. If null, leave those payload positions unchanged in the
object's retained send buffer. Send header plus selected payload to its broadcast
address, using the native datagram service's return value directly. Successful
returns include the four header bytes. Negative socket returns are not converted
into another XDaemon error here.

TCP broadcasts to all IPv4 addresses on its configured datagram port; the sender
can receive its own packet. IPX uses its local network, all-ones node and configured
datagram socket. N01 filters game discovery messages; this layer does not suppress
self-delivery. Modem/serial delegates to §9 with unreliable delivery; other
initialized protocols store/return −13.

### Broadcast receive

An admitted Read event resolves an active datagram object, captures its receive
buffer/capacity, clears **all** of that buffer, then receives one datagram and
stores its sender address. A socket error or absent callback returns without
application delivery. Otherwise read the first four bytes as header and invoke
the callback with payload position and `u16(receivedBytes-4)`.

There is no four-byte minimum guard in the original. A short datagram's header
contains zeroes from the preceding buffer clear, and its callback length can
wrap: zero received bytes yields 65532. Preserve the scalar or reject the packet
under a declared stricter profile; a child's invalid payload read/write remains
separately bounded. Do not assume the claimed wrapped bytes were received.

A special comparison is attempted when exactly six bytes were received: compare
six bytes at payload offset four with `XDPING`. With the nominal cleared buffer,
only two bytes at that position came from the six-byte datagram; the following
four are zero. It cannot match that token on this nominal path. The comparison
uses real cleared backing, not an out-of-bounds six-byte datagram object. Its
unused response path does not create a general discovery protocol.

### Datagram initialization and partial failures

The logical datagram manager has five slots. Reject a full manager, zero
payload-capacity scalar, unsupported protocol or TCP port above 5000. For TCP,
a zero port defaults to 4700. Publish protocol/port and clear sender address;
establish send/receive capacity, UDP delivery and requested broadcast support.
When broadcast was requested, enable address reuse. Bind, enable Read delivery
and register the first free manager slot. Query local address and build the
broadcast destination; only then publish active=1 and the receive callback.

Failures retain preceding logical publications and service effects; the outer
initializer maps failure to −5. No failed step fabricates an active endpoint.
Close unregisters the endpoint, disables delivery, shuts down/closes it and
sets active=0. Destruction closes if still active and retires its storage.
Native context checks, exact internal error codes, wrapped backing-allocation
sizes, hidden windows and SPX-specific steps are reference details in [Appendix D](#appendix-d-original-implementation-reference).
The wire length reductions and callback arguments in this section remain
required; insufficient backing never permits an out-of-bounds operation.

### Stream-event dispatch

The original event dispatcher chooses the global connecting stream when nonnull;
otherwise it looks up the incoming socket identity. It does not additionally
require that a chosen connecting object's socket match the event parameter.
Missing object returns without transport delivery. Non-transport native
messages are handled by the implementation's presentation/event adapter.

| Socket event | Ordered action |
| --- | --- |
| Read | Receive once using the event's socket identity and chosen object's receive capacity (low-word limit in the reference backend). On socket error, log unless WouldBlock; otherwise parse the returned count, including zero. |
| Write | Drain as specified in §4. Only a −4 result invokes close handling here. |
| Accept | Call the object's accept handler if present. |
| Connect | Log success/error. An error closes that object; then attempt autodial restoration. It does not itself call connected(−1); the timer/ID path does that. |
| Close | Log, then invoke the object's close handler if present. |

Read returning zero does not itself synthesize a lost-connection callback;
Close is a separate event. The native message order is input to this algorithm.
Do not invent a receive-before-close priority across independently delivered
messages or process every ready socket in a new fixed polling order and claim
that all callback interleavings are unchanged.

## 7. Flush, closure and reset

### Flush

Clear last-error; uninitialized stores/returns −1. Set remaining=1, capture the
tick service used for this operation, set in-flush=1 and obtain the starting tick.
Then repeat:

```text
if timeout <= u32(tick() - started): break
remaining = 0
if one thread message can be removed:
    translate it
    dispatch it
if now a client:
    if main secure stream exists: remaining = its queue count
else:
    for slots 0 through the live table extent:
        if stream exists: remaining = u32(remaining + stream.queueCount)
repeat only while remaining != 0
```

Return remaining's bits as signed 32-bit, logging positive remaining counts,
after setting in-flush=0. Each iteration removes at most one message. Timeout
zero returns **1**, without inspecting a queue or dispatching a message. Expiry
after an iteration returns its last sampled count. Queue counts, role and table
extent are live after dispatch. A nested flush shares the same flag rather than
a depth counter; its return can clear the flag while an outer flush continues.
InFlush simply returns the current flag and does not clear last-error.

### Stream resource close and loss notification

Closing a stream disables further delivery and sending, discards unread
transport bytes without application delivery and closes the connection. Clear
receive capacity and partial-frame total; discard pending packets in FIFO order.
Mark the connection invalid and restore backlog-report threshold 50. Do not
independently reset the received-byte count. Destroying a stream also clears
the global connecting reference if it names that stream and removes its table
entry. Explicit destruction does not add an application lost notification.
Native shutdown/drain/free/target-destruction calls are recorded in [Appendix D](#appendix-d-original-implementation-reference).

The shared loss handler first distinguishes live role:

- Client: call the registered lost callback with ID 0, **then** set local ID −1.
  This branch has no null-callback guard and does not itself explicitly close
  the main stream. Missing callback is a callable-input boundary, not a silent
  original success. A callback observes the previous ID.
- Server: look up the full ID by socket. If found, close the matching stream,
  clear the main secure reference if it is that stream, remove its table entry
  and destroy it. If signed local ID equals the looked-up ID bits, clear local
  ID to −1. Log and invoke the optional lost callback with the lookup result,
  even if lookup returned −1 bits. Do not infer a valid peer from that result.

No XDaemon heartbeat is added. N03 supplies the game's heartbeat policy; normal
network failure detection here comes from provider events and send errors.

### Public close and shutdown

CloseConnection clears last-error; uninitialized stores/returns −1. For a TCP/SPX
client, close the main stream and reinitialize it. If that fails, destroy/null
it and set protocol −1. For a server, close/destroy client slots 1 through the
live extent, retaining the listener. Set local ID −1 and return 0. It does not
add a universal connection-timer clear. Modem/serial calls its player-close
operation and returns 0; other initialized protocols return 0.

ServerCloseConnection clears last-error and checks initialized, server-role and
ID below table extent, storing −1, −6 or −9 on those failures. TCP/SPX also
requires an extant slot, otherwise −9, then closes/destroys it. The scalar
contract does not forbid ID 0. Modem/serial calls its shared close child after
the same outer checks. Return 0.

ShutdownSockets clears last-error. Close/delete/null the datagram object first.
For a server, destroy client slots 1 through the live table extent, then clear
server-role. Close/delete/null the main secure object, free the table, set local
ID and protocol to −1. Callback registrations and configured ports remain.

ResetLibrary first disables debug behavior, then dispatches shutdown according
to current protocol: sockets for 0/1, modem for 2, serial for 3. Set ID/protocol
to −1, server/enables to zero, clear the main socket, locator and connection-window
references, clear locator-enabled, reset locator timer ID to 201 and clear
server-found. Retain callback registrations, oversized settings, ports and other
fields not explicitly reset. It does not independently close the launch-lobby
interface. Merely nulling a reference does not prove an earlier partial resource
was cleaned up; a replacement can manage ownership safely without inventing
extra game callbacks or successful completion of failed operations.

## 8. Library queries, optional services and native effects

GetXDaemonVersion writes major=3, minor=5, patch=0 in that order and returns 0.
GetCurrentProtocol clears last-error and returns the current selector. The main
game imports version but does not import most optional query/debug exports.
Appendix A inventories all 48 exports so absent callers do not become invented
game features.

Unused UDP-to-address exports, the separate library server locator, debug/noise
services and DLL attach/detach effects are reference extensions in [Appendix D](#appendix-d-original-implementation-reference).
N01's UDP discovery remains required and does not use those services. Their
absence adds no game RNG draws, new discovery frames or dependency on a legacy
platform library.

## 9. Modem, serial and launch-lobby providers

Modem, serial, IPX/SPX and external launch-lobby support are optional under X01.
Their recovered algorithms, provider failures and native resource lifecycles
are retained in the reference appendix for implementations choosing to support
those extensions. They are not prerequisites for TCP/IP, UDP discovery,
hotseat, saves or scenarios. A product must not advertise an unavailable legacy
provider as a working path. Shared algorithms used by TCP remain required.

Protocol identities 2/3 for modem/serial remain distinct from TCP 0 and SPX 1.
References to §9 from shared branches mean the optional provider contract in
the appendix; they impose no requirement to load its native API.

Original implementation: [Appendix D](#appendix-d-original-implementation-reference).

## 10. Conformance cases and integration obligations

For the required TCP/IP domain, observe ordered wire records, callback identities,
protocol-relevant callback timing, live queue
counts, role/ID publication, error assignment and cleanup prefixes. Successful
payload delivery alone cannot distinguish several original failure paths.

Cases outside the admitted domain are optional original-reference cases; test
safe rejection instead. Legacy-provider rows apply only to that extension.

| Case | Observation within the applicable profile |
| --- | --- |
| Invalid facilities with a newly supplied broadcast callback | Callback can remain registered despite −9. |
| Secure-only TCP after a prior port setting | Uses retained stream port, not the new unused argument. |
| Accepted greeting send queues | Destroy/remove that peer, then still report new-client with current count and assigned ID. |
| ID arrives exactly at deadline | Failed-connect continuation; externally owned payload can then route ordinarily. |
| ID arrives after assignment | Ordinary record routing, not blanket control discard. |
| Payload 100, ordinary send | Total 128; only the declared 100 payload bytes are delivered. |
| Partial first send followed by another record | First record stays ahead of the second; retries never interleave their bytes. |
| Immediate Interrupted versus queued Interrupted | Immediate queues as zero progress; queued frees/removes and returns −4. |
| Oversized total 128, limit 76 | Fresh header reports 116; split header reports 128. Discard current receive remainder. |
| Oversized callback absent | Receive-size check is absent; sender's threshold check still exists. |
| Declared payload exceeds total−12 | Parser does not clamp the scalar; child payload access needs backing or rejection. |
| Client broadcast through host | Other clients see host-source tags; host callback retains incoming sender. |
| Host has no secure callback | Destination 255 is not relayed as a broadcast. |
| One peer send fails during host broadcast | Later peers are still visited; aggregate result is −13. |
| UDP length 65536 or 65537 | Payload becomes zero or one before the 512-byte cap. |
| Null UDP payload pointer | Retained send-buffer payload bytes are transmitted. |
| Datagram shorter than four | Cleared header backing plus wrapped callback length; no invented received tail. |
| Flush timeout zero | Return 1 with no message dispatch or queue sample. |
| Flush dispatch changes role/queues | Count using live post-callback state. |
| Client loss callback | Observes old local ID; ID becomes −1 afterward. |
| Reference native backend only: OS receive capacity 65536 | Original receive chunk is zero; see the implementation appendix. |
| Legacy provider Send fails | Inner send still returns payload size after logging. |
| Lobby long name exceeds its field | Excluded overwrite, even if it fits the larger launch record. |

A full implementation join requires N01's registration/session transitions,
N02's actual enqueue/dispatch and state producers, N03's turn/heartbeat behavior,
and P05's real message ordering. Exercise fragmented/coalesced traffic, callback
reentry, failure after partial send, slot reuse, host migration and close during
receive. Compare padding separately from payload semantics. Optional legacy claims require
real provider availability and worker/event behavior, not only supplied HRESULTs.

## Appendix A. ABI, native constants and validation mappings

### External callbacks and private layouts

The original exports and callbacks use x86 cdecl. These widths are required for
an adapter loading the original DLL; a standalone implementation can use typed
logical values. Wire integers retain §4/§6's exact encodings.

| Callback | Declared arguments |
| --- | --- |
| Broadcast | payload pointer, unsigned 16-bit length, signed 32-bit header. |
| Secure | unsigned 16-bit sender, payload pointer, unsigned 32-bit length, signed 16-bit flags. |
| New client | unsigned 16-bit used count, unsigned 16-bit assigned ID. |
| Connected | signed 16-bit ID or −1. |
| Lost | unsigned 16-bit ID. |
| Oversized | unsigned 32-bit size, with the parser-branch difference in §5. |
| Latency | unsigned 32-bit elapsed ticks, unsigned 32-bit packet count. |

Original machine calls push full argument slots even for narrow declarations.
Some upper halves retain register/exception-chain bits: accepted count/ID,
connected ID, routed sender and invalid-self ID have such evidence in the DLL
reconstruction. UDP subtraction changes only the low word of its length slot.
DirectPlay passes its full provider sender/type slots. The game consumes the
narrow ID/length forms documented by N01/N02 and ignores transport flags; these
upper bits do not require invented extra logical players. A raw ABI validation
that observes full slots must supply/capture their original backing separately.
The Linux fixture uses declared-width callback adapters and does not validate
physical stack/register fidelity.

| Private item | Original validation mapping |
| --- | --- |
| Protocol, role, enables, ID | `10013030`, `10013034`, `10013038`, `1001303c`; signed ID word `10013040`. |
| Oversize state | Limit `10013044`, callback `1001305c`. Default limit `fffffff0`. |
| Main socket references | Datagram `10013068`, secure `1001306c`; connecting stream `100137c8`. |
| Chunk and table | Global chunk `100137c0`; table capacity/count `100137d4`/`100137d0`; slot storage reference `100188f0`. |
| Stream | Private size `1ec`; receive capacity word `+04`, FIFO `+06`, pending buffer `+16`, partial total `+1a`, pending total `+1e`, received count `+22`, socket `+1b4`, callback `+1e4`. |
| Stream startup output | Native WSADATA size `190` written at `+26`; SDK padding overlaps the low socket word within the backed receiver. Its later live reads remain an adapter detail, not extra game state. |
| Queue storage | Packet descriptor and node each 12 bytes on x86; queue header 16 bytes. Copies/cleanup operate through their logical ownership in this specification. |
| Shared stream send storage | 10240 bytes at `100188f8`, initially zero. |
| Datagram | Private size `52`; capacity word `+04`; receive/send pointers `+0a/+0e`; callback `+12`; active `+16`; socket `+1a`; sender address `+42`. |
| Datagrams and providers | Five manager slots; usual buffer capacity 516. Modem directory has 32 GUID entries followed by eight 50-byte names. |
| Address history | 249 rows of 25 bytes at `10016848`; copy does not guarantee terminator for a full-width source. Invalid IDs return the empty-address backing in the library-only query. |
| Protocol-address output | 50 bytes at `100167f8`; original hostname local has capacity 100. |
| Launch info | Total 196: address `[0,32)`, long name `[32,96)`, short name `[96,128)`, session name `[128,192)`, host DWORD at 192. Each named string has its own write bound. |
| DirectPlay storage | Separate 10240-byte receive/send buffers at `1001b0f8`/`1001d8f8`; system type at 0 and joined-player ID at 8. |
| Timer/event messages | Datagram socket message `401`, stream `402`; connection timer 501; optional locator timer initially 201. |
| Windows | `QSocketWndClass`, `StreamWindowClass`, `ServerSearchWindow`, `XDaemonModemClass`. Stream titles `XDSControlWindow-%d` use an incrementing word and at most two FindWindow checks before failure. |

The original stream window creation checks for existing titles across processes.
The independent reconstruction's diagnostic title suffix, port remapping and
local-address override are testing adapters. They are not classic configuration
options or evidence that the original event timing was unchanged.

### Native provider values

| Purpose | Value |
| --- | --- |
| TCP | IPv4, stream, TCP; socket addresses use network-order port/address. |
| IPX datagram | Family 6, datagram type 2, protocol 1000. |
| SPX stream | Family 6, type 5, protocol `04e8`; fixed socket word `a791` in x86 memory, bytes `91 a7` on the provider address. |
| SPX reconnect option | Level 1000, option `4010`, enabled integer 1. |
| IPX adapter queries | Level 1000: count `400d`, 24-byte adapter entry `4007`, max packet `4006`. |
| DirectPlay BufferTooSmall | `8877001e`. |
| Lobby NotLobbied | `8877042e`. |
| Missing session lobby | `88770082`. |
| Provider send diagnostics | Busy `8877010e`, InvalidParams `80070057`, InvalidPlayer `88770096`, NotLoggedIn `88770816`, SendTooBig `887700e6`; other failures use the generic diagnostic. Return remains payload size. |
| Modem provider GUID | `44eaa760-cb68-11cf-9c4e-00a0c905425e`. |
| Serial provider GUID | `0f1d6860-88d9-11cf-9c4e-00a0c905425e`. |
| Provider address GUID | `07d916c0-e0af-11cf-9c4e-00a0c905425e`. |
| Phone address GUID | `78ec89a0-e0af-11cf-9c4e-00a0c905425e`. |
| Modem-name address GUID | `f6dcc200-a2fe-11d0-9c4f-00a0c905425e`. |
| Internet-address GUID | `c4a54da0-e0af-11cf-9c4e-00a0c905425e`. |
| Play class / interface | `d1eb6d20-8923-11d0-9d97-00a0c90a43cb` / `133efe41-32dc-11d0-9cfb-00a0c90a43cb`. |
| Lobby interface | `1bb4af80-a303-11d0-9c4f-00a0c905425e`. |
| Session application GUID | `ba644f27-d6d1-11d1-80c8-00609703e270`. |

GUID text above identifies the native interfaces; native binary GUID fields use
their established structure encoding, not raw ASCII or uniformly reversed bytes.
Provider methods and HRESULTs are external service boundaries. The DLL describes
how it drives them; it does not implement the underlying modem or serial framing.

Datagram initialization internal failures are: −1 parameters/manager capacity,
−5 buffer allocation, −2 class registration, −3 window creation, −4 socket-service
startup, −6 socket creation, −7 broadcast/reuse option, −9 bind or manager
registration, −8 asynchronous selection. The library initializer maps them to −5;
retaining a diagnostic code does not imply additional cleanup.

### Native registry and error-query details

The autodial helper reads `EnableAutodial` under
`HKEY_USERS\.Default\Software\Microsoft\Windows\CurrentVersion\Internet Settings`.
Before TCP connect, a produced nonzero value is copied to `ModifiedNet` under
`HKEY_LOCAL_MACHINE\Software\MicroProse Software\XDaemonNet`, then autodial is
written as zero. A produced zero value clears ModifiedNet. Missing query output
skips those stores. Writes use four bytes and REG_BINARY. Connect-event handling
and module attach/detach read ModifiedNet, restore a produced nonzero value,
then clear ModifiedNet. Ignore write return values at these callers.

The registry reader clears its 2048-byte shared query backing and caller size/type
outputs first. Failed open or query closes the local handle and returns null;
success publishes type then size, closes and returns the shared bytes. The writer
creates/opens the selected key, sets the value on success, closes, and returns
whether setting succeeded. Original failure paths can close an unproduced handle;
BF-301 supplies null as a repair. Preserve preceding clears and require supplied
handle input or a stop for an original comparison.

The non-game error-string lookup has a signed-clamp defect: ordinary negative
errors resolve through four ASCII bytes of `XDERR_NULLSOCKET`, producing pointer
bits `54454b43`; nonnegative and minimum-signed-int inputs return the retained
success-string pointer. A pointer value does not make its pointee readable.
GetLastError optionally stores last-error through its output, then reloads
last-error before lookup, so an admitted alias can affect the result. Do not
invent a correct error-string table as an original behavior claim.

### Export inventory

`Game` means an exact import of the pinned executable; `Internal` means an
export reached by library dependencies; `Provider` records the separate legacy
provider surface; `Optional` is library-only, with no established game caller.
The inventory does not require a cleanroom game to expose unused DLL exports.

| Ordinal | Export (unmangled name) | Surface |
| --- | --- | --- |
| 1 | `XD_ActivateServer` | Game |
| 2 | `XD_CloseConnection` | Game |
| 3 | `XD_EnableDebugFunctions` | Internal |
| 4 | `XD_EnableServerLocator` | Optional |
| 5 | `XD_FlushSendBuffer` | Game |
| 6 | `XD_GeneratePortNoise` | Internal |
| 7 | `XD_GetConnectedSocketAddr` | Optional |
| 8 | `XD_GetCurrentProtoAddr` | Game |
| 9 | `XD_GetCurrentProtocol` | Optional |
| 10 | `XD_GetLastError` | Optional |
| 11 | `XD_GetLoggedAddress` | Optional |
| 12 | `XD_GetModemName` | Provider |
| 13 | `XD_GetNumEnumeratedAdapters` | Optional |
| 14 | `XD_GetNumModems` | Provider |
| 15 | `XD_GetXDaemonVersion` | Game |
| 16 | `XD_InFlushSendBuffer` | Game |
| 17 | `XD_InitializeModem` | Game |
| 18 | `XD_InitializeSerial` | Game |
| 19 | `XD_InitializeSocketsIPXSPX` | Game |
| 20 | `XD_InitializeSocketsTCP` | Game |
| 21 | `XD_LaunchZone` | Provider |
| 22 | `XD_LaunchedByLobby` | Game |
| 23 | `XD_LobbyClose` | Internal |
| 24 | `XD_LobbySendMessage` | Game |
| 25 | `XD_LookupErrorCode` | Internal |
| 26 | `XD_OpenConnection` | Game |
| 27 | `XD_ResetLibrary` | Game |
| 28 | `XD_SelectModem` | Provider |
| 29 | `XD_SendBroadcastData` | Game |
| 30 | `XD_SendDirectBroadcastData` | Optional |
| 31 | `XD_SendPingRequest` | Optional |
| 32 | `XD_SendSecureData` | Game |
| 33 | `XD_SendUDPBroadcastData` | Optional |
| 34 | `XD_ServerCloseConnection` | Game |
| 35 | `XD_SetBroadcastReceive` | Game |
| 36 | `XD_SetDebugPacketDropRate` | Internal |
| 37 | `XD_SetNewClientConnection` | Game |
| 38 | `XD_SetOnClientConnectionToServer` | Game |
| 39 | `XD_SetOnConnectionLost` | Game |
| 40 | `XD_SetOnInvalidSendToSelf` | Optional |
| 41 | `XD_SetOversizedMessageCB` | Game |
| 42 | `XD_SetPacketDelay` | Internal |
| 43 | `XD_SetSecureReceive` | Game |
| 44 | `XD_ShutdownModem` | Game |
| 45 | `XD_ShutdownSerial` | Internal |
| 46 | `XD_ShutdownSockets` | Game |
| 47 | `XD_ShutdownTEN` | Game |
| 48 | `XD_StopConnections` | Game |

## Appendix B. Original bugs, repairs and older-port differences

| Evidence case | Classic boundary and permitted implementation distinction |
| --- | --- |
| BF-294 receive capacity | Original low-word narrowing can produce zero. A declared modern-host correction clamps the receive chunk; it must not be described as the original operation. |
| BF-300 hostname failure | Original ignores a producer failure. Require produced/supplied hostname bytes or stop; the repaired null return is distinct. |
| BF-301 registry failure handles | Original cleanup can consume an unproduced handle. Supplied input or a stop preserves the evidence; zero initialization is a separate repair. |
| BF-302 late parser-owned ID | Stream destruction/replacement and failure notification precede invalid payload/receiver access. Preserve that prefix; prevent retired accesses. A direct externally owned payload has a separate, valid ordinary-routing continuation. |
| BF-303 queued IsConnected | Queue removal and the first packet cleanup precede retired reads and repeated cleanup. Do not reproduce double-free/corruption. The repair returns 0 after one cleanup; original comparison stops at the invalid continuation unless backed historical observation is being analyzed separately. |
| Tiny split-header total | Copying the completed four-byte total into a smaller allocation is an excluded write, even if allocator padding exists. |
| Wrapped send length | A small wrapped allocation does not authorize copying the huge original payload over neighboring memory. |
| Short/overclaimed receive payload | Header reads and callback payload reads require backing. A valid scalar length is not proof of that backing. The safe-write exclusion alone does not define a clamped read result. |
| Lobby/name/phone copies | Cross-field and cross-record writes are excluded, including corruption within a larger allocated owner. Preserve earlier accepted text/flag effects only. |
| Modem enumeration overflow | Count increment precedes the unsafe name-row write. The overwrite is excluded; no adjacent logging/provider state mutation is required. |
| Null allocations/callbacks | Stop before an unbacked original access. Do not turn reconstructed diagnostics into original API return codes. |
| Diagnostic formatting | Original unbounded formatting can overwrite neighboring log filename/format fields or consume missing format arguments. These are excluded writes/unsupported reads, not new game or transport state transitions. |

These cases can arise in ordinary service failures as well as malformed inputs.
Classifying a memory overwrite as a bug does not erase safe preceding scalar
changes, callback order or wire output. Conversely, deterministic adjacency is
not a reason to require corruption in a new representation.

The TCP-only OpenCiv2 port and older protocol notes use several deliberate
simplifications. They remain useful interoperability evidence, but these are
not substituted into the classic contract:

- Zero-filled output records replace original retained/allocator padding.
- A queued, delayed greeting replaces blocking acceptance and its exact-2 gate.
- The port consumes complete oversize records, clamps declared payload lengths,
  and rejects short totals; the original parser's fragment-dependent continuation
  is different.
- Out-of-state ID tags are discarded by the port; the original can route them.
- The port's client-loss state is updated before notification; the original
  publishes ID −1 afterward.
- IPv4 input scanning, selected local-address policy, receive-buffer sizing,
  polling budgets and callback scheduling differ from the original socket API.
- The port's IPX, modem, serial and launch-lobby stubs do not describe successful
  original provider paths.

The historical protocol document also mixes captured game-level observations
with a proposed replacement design. Its proposal that a nonblocking accept delay
is unobservable is not adopted here. Capture claims are attributed to that
existing evidence; they were not reproduced during this documentation work.

## Appendix C. Evidence and validation limits

> **Validation/provenance only.** Source links, filenames and historical harnesses
> in this section are not implementation inputs. Implement from the behavioral
> contracts and declared formats; do not consult or reuse reconstruction code
> or source-derived harnesses for implementation or tests. Missing behavior is
> a specification gap. This boundary does not make file/wire encodings optional.

The older [XDaemon protocol notes](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/xdaemon-protocol.md)
provide historical captures and a TCP port proposal. They are evidence, not a
second algorithm owner. This specification reconciles them with the separate
DLL reconstruction and focused original instructions; Appendix B lists material
differences. No new network session or original DLL execution was performed for
this document.

The main-game/portable-port reference is pinned to
`a71e8aefffe04e6f719e0e213b027b128a747711`, researched checkout
`fc2155ccdb2123c1137babab6fa39e38152278c9`:

| Evidence | Navigation and use |
| --- | --- |
| Game integration | [network.cpp](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/auto/network.cpp), `NetSession::init_transport`, shutdown and callback registration/consumers; algorithm owners N01/N02. |
| Portable replacement | [xdaemon.c](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/platform/xdaemon.c), complete implementation; differences retained in Appendix B. |
| Declarations | [xdaemon.h](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/src/include/xdaemon.h), application-facing declarations, checked against the DLL's own declarations. |
| Historical capture/proposal | [xdaemon-protocol.md](https://github.com/PeterJohnson/openciv2/blob/a71e8aefffe04e6f719e0e213b027b128a747711/docs/xdaemon-protocol.md), recorded LAN/Internet observations and their limitations. |

The more detailed library evidence comes from `civ2` revision
`4cfdd2a4b43e1a99983cc6af3e993a52b8ad8752`, independently of the portable port:

| Contract | Local source navigation under `dlls/xdaemon/src/` |
| --- | --- |
| Registration, init, reset | `state.cpp`, `lifecycle.cpp`; original `10001050`, `100014d0`, `10001830`, `100018d0`, `10003870`. |
| Connections and slots | `connect.cpp`, `accept.cpp`, `clients.cpp`, `secure_socket.cpp`; `10001940`, `10002820`, `10004d90`, `10004fe0`, `10005f60`. |
| Record creation/queue | `send.cpp`, `secure_api.cpp`; `10001f50`, `10005490`, `10005851`, queue helpers `10007540` onward. |
| Parser, routing, events | `stream.cpp`, `receive.cpp`; `10005ac0`, `10002a20`, `10005740`, `10002e00`. |
| Datagram service | `udp_api.cpp`, `quick_socket.cpp`; `10002570`, `100042d0`, `100047d0`. |
| Provider/session/lobby | `legacy.cpp`, `dplay.cpp`, `lobby.cpp`, `protocol_address.cpp`; provider GUIDs, typed native interfaces and bounded source continuations. |
| Platform effects | `module.cpp`, `platform_exports.cpp`, `noise.cpp`; native registry/log lifecycle, version/TEN/Zone and reset's disabling dependency. |

`analysis/dlls/xdaemon/application-surface.json` classifies all 48 exports:
27 game imports, seven internal dependencies, four provider exports and ten
library-only exports. `src/network_library.cpp` provides the executable's typed
import adapters. The DLL README, integration review and BF-294/BF-300–303 ledger
record prior source/native checks and their limits. Those historical acceptance
claims are not promoted to new complete game-session conformance here.

Original binary in the `civ2` repository: `Civ2/XDaemon.dll`, 95744 bytes,
SHA-256 `ae6c1c142cebc04b5cd2daee75b31a52ed0d16b5492fd9a96fa992b4b5fef157`.
Ten pinned instruction probes verify the blocking delay, exact-2 greeting gate,
two oversize callback argument forms, low-word receive capacity, initial flush
count and pre-dispatch timeout gate, queued IsConnected branch, split-header
store and tag-before-length callback loads. This is direct instruction evidence,
not execution of the DLL.

The documentation check compared **34 pinned DLL source/header/export/test files**,
four main reference files, all 48 export classifications and the original binary
hash. A finite Linux fixture extracted **30 complete DLL function bodies** with
explicit Win32 integer-width and allocator adapters, and passed **47 assertions**
in optimized and AddressSanitizer/UndefinedBehaviorSanitizer builds. LeakSanitizer
was disabled for the execution environment.

The fixture joins actual queue/record construction to secure-send routing and
actual incremental parsing to the shared receive router. It uses logical native
object layouts, supplied allocation bytes, declared-width callback bridges and
explicit socket, message, clock, logging, lost/reset and legacy-provider adapters.
It does not emulate x86 exception-chain/register remnants. The allocator's fill
bytes are supplied test input, not a claim about original malloc contents.

Checks cover record/padding interpretation, partial FIFO sends, immediate versus
queued retryable errors, chunking, split/coalesced input, fragment-dependent
oversize reports, callback-gated receive limits, un-clamped declared length with
no unsafe payload read, a short-header read stop, host relay/broadcast aggregation,
self/missing-target errors, ID deadlines and externally owned late-ID continuation,
ping elapsed wrap, flush zero/dispatch/count ordering, UDP length reduction and
retained null payload, callback registration and slot reuse.

The fixture does not execute the unsafe BF-302/BF-303 continuations, native
accept/window teardown, actual socket service/registry failure paths, COM provider
sessions, modem worker races or launch-lobby native UI. Those algorithms were
source-reviewed with explicit external-input/bug boundaries. Native legacy
provider availability, complete game-peer/save/resume runs and raw ABI conformance
remain integration obligations. The source contracts and bounded checks
do not establish live operation of every transport.

## Appendix D. Original implementation reference

This appendix preserves original storage, allocation, native API and adapter
procedures for reference validation. The main text defines required behavior.
A different internal representation or platform is permitted; semantic identities,
ordered game effects, accepted input interpretation, file/wire values and required
failure/completion results remain governed by the main algorithms and X01.
Mixed excerpts retain context: their required effects are not made optional by
appearing here. Original memory/handle traces are not a second normative algorithm.
Optional providers and harmless UI failures retain X01's existing scope limits;
this reorganization introduces no new gameplay or interoperability relaxation.

### Original stream event target

Per stream: socket, protocol/port, native event target, receive capacity,

### Original connection timer representation

Connection start tick, timeout, timer handle/window; latency tick/callback;

### Original callback dispatch mechanisms

TCP/IP and IPX/SPX socket callbacks run on the thread processing their native
window messages. There is no independent TCP receive worker. A flush can process
all types of messages on that thread, including application UI messages. Legacy
DirectPlay receives use a separate worker, described in §9. Do not replace both
models with an arbitrary background callback order.

### Original datagram object replacement

close/delete the previous datagram object if any,
   allocate its replacement,

### Original stream object replacement

close/delete the previous stream if any, allocate its
   replacement

### Original stream initialization and receive allocation

Creating a stream accepts selected protocol 0 or 1; another selector fails.
Publish its protocol, server flag, port, backlog threshold 50 and module/context,
then create its hidden event target. Failure returns false. Initialize the
socket service using version 1.1; failure returns false. TCP creates an IPv4
stream socket and sets the global chunk capacity to 8192. SPX creates its stream
provider socket and sets that capacity to 512. Invalid socket creation fails.

For a server, bind the TCP listener to any local address and its selected port.
SPX binds a 14-byte provider address with the fixed stream socket in Appendix A.
Bind failure returns false. A client does not bind here. Query the OS receive
buffer size; on query failure use 8192. Allocate the full reported/fallback
size, fail on allocation failure, then retain its **low 16 bits** as the maximum
size passed to each receive. Return true.

An accepted stream first requires an invalid existing socket and protocol 0/1.
Create its event target, accept using address length 16 for TCP or 14 for SPX,
and fail if accept fails. Query receive size, but use **8000 decimal** on failure
in this path. Allocate the full size, retain the low 16-bit receive chunk, then
register Read, Write and Close events. This path adds no socket-service startup.

Reinitialization after a client close creates a new event target and starts the
socket service again, recreates the socket and global chunk capacity, binds if
its retained server flag requires it, and allocates receive storage using the
8192 fallback. SPX reinitialization additionally requests its provider-specific
option in Appendix A. Preserve the same low-word receive capacity.

A reported size of 65536 can allocate successfully yet produce receive length
zero. The original then cannot assemble its incoming handshake through that
socket. BF-294's clamp to 65535 is an explicit modern-host correction, not the
classic narrowing rule. Restrictive or corrected profiles must declare this
choice separately.

### Original connection table allocation

Allocation initializes each entry to invalid socket/null stream and publishes
the capacity on success; it does not independently reset the used count. Freeing
the table nulls it and zeros capacity/count.

### Original address-service startup

When current
protocol is uninitialized, request socket-service startup without checking its
result or adding a paired cleanup here.

### Original IPX address production

For selector 1, open an IPX datagram socket, bind it, query adapter count and
inspect adapters in ascending order. Query the requested adapter and maximum
packet size. A failed requested-adapter query returns null; success formats
four network bytes, a colon and six node bytes as uppercase hexadecimal. The
query socket is not closed by this original path. Other selectors return null.
Native adapter/provider results remain external inputs.

### Original listener activation and event registration

ActivateServer clears last-error; uninitialized stores/returns −1, a nonserver
−6, and a missing secure socket −3. For TCP/SPX, require valid socket/event target,
register Accept/Close and listen with backlog 5. Success returns 0; helper failure
returns −5 while last-error remains whatever the preceding operations produced.
For modem/serial, use §9's host-session operation with the same boolean-to-0/−5
mapping. Other initialized protocols return 0 without those children.

StopConnections clears last-error; uninitialized stores/returns −1, nonserver −6.
For TCP/SPX, a missing socket stores/returns −1; otherwise disable asynchronous
selection on the listener and return **1**, leaving it open. Other protocols
return 0. This is not a socket shutdown or a universal stop of existing peers.

### Original connection timer and replacement allocation

6. On acceptance, create the connection-timer event target if absent; request
   timer 501 with the supplied timeout, store its returned handle, then capture
   the current tick and timeout. Return 0 even if timer creation returned zero.
7. On failure, destroy the current temporary stream, allocate/publish a fresh
   one, install receive/lost handlers and attempt client initialization. Then
   store/return −10. The earlier failure does not bypass this replacement.

### Original autodial and SPX connect procedures

Before TCP connect, the native adapter attempts to save/disable system autodial;
Connect-event handling and module attach/detach attempt restoration. Appendix A
records these platform effects and their failure-input boundaries. They are
separate from game state and transport messages.

For SPX text, search for a separator in this precedence: colon, otherwise dot,
otherwise space. Text length below nine fails into the common −10 continuation.
Otherwise choose the suffix after that separator, or byte 8 if none exists, and
write zero into the immediately preceding byte. Thus a separator-free accepted
string loses its eighth network character. Parse four network bytes and six
node bytes from pairs of characters, append the fixed SPX socket and connect.
Each hex digit recognizes 0–9/A–F/a–f, otherwise −1; each output byte first stores
`highDigit << 4`, then adds the low digit modulo 256. There is no implicit invalid
hex rejection. Unavailable input bytes or invalid output destinations have their
read/write boundaries; the caller's address text mutation is retained on failure.

### Original accept sleep

After registration, **block the event thread for 500 ms**. Attach receive/lost

### Original accept-thread exclusion

Do not move the delay to an independent timer and assume equivalence: other
message dispatch is blocked during the original sleep, and queued greeting
failure has an observable cleanup/notification sequence.

### Original handshake timer cancellation

timer handle is nonzero, kill timer 501 and clear it.

### Original timeout callback timer ID

Timer 501, when dispatched, replaces the stream and calls connected(−1) only
if ID is still −1. It then kills timer 501 and clears the handle regardless.

### Original datagram allocation and native initialization

The datagram manager has five slots. Initialization rejects a full manager,
missing module/context, zero payload-capacity scalar, unsupported protocol,
or TCP port above 5000. A zero selected port defaults to 4700 for TCP and 8181
for IPX; IPX's zero-port branch also enables broadcast.

Publish protocol/port, clear sender address, allocate receive and send buffers
of `u16(payloadCapacity+4)`, then create the hidden event target and start the
socket service. Create UDP or IPX datagram socket, set broadcast as requested
(or required by IPX), and reuse-address when broadcast was requested. Bind,
register Read events and register the first free manager slot. Query local
address and build the broadcast destination; only then publish active=1 and
its receive callback.

Failures return their internal codes in Appendix A. Earlier allocations,
window/socket/service effects are not universally undone. The outer initializer
maps any nonzero result to −5. A tiny wrapped capacity cannot justify a header
write beyond its allocation. Close unregisters the socket, disables events,
shuts down both directions, closes it, destroys its event target, clears that
target and sets active=0. Destruction closes only if still active, then releases
the two buffers.

### Original non-socket window dispatch

Missing object returns after a diagnostic. Destroy/Paint messages return 0;
other non-socket messages go to the native default handler.

### Original stream resource teardown

Closing a stream disables asynchronous selection, shuts down sending, drains
receives into temporary storage while the native result is positive, closes the
socket and destroys its event target. Release receive storage and clear its
capacity, release pending frame storage and clear its total, then remove/free
queued packet records in FIFO order. Set socket invalid and backlog threshold
50. The original does not independently reset the received-byte count here.
Its destroy operation also clears the global connecting reference if it names
this stream, removes its table entry, closes a still-valid socket, destroys the
event target again and clears remaining queue nodes. Avoid imposing an unrelated
application lost notification on explicit destruction.

### Original optional library services

The library-only UDP-to-address operation uses the broadcast destination when
host text is null or its broadcast argument is nonzero; otherwise it converts
the requested IPv4 address and uses the object's port. DirectBroadcastData uses
header 0 and a zero-initialized address with the selected IPv4 address but **no
port assignment**. Both use §6's low-word reduction and send algorithm, after
their protocol/enabled checks. They are not what N01 calls to discover games.

The optional server locator requires initialized secure service, nonzero port,
server-role and TCP/SPX selector. Enabling creates a separate TCP datagram object
with a 512-byte payload capacity and a 500 ms timer. Each admitted timer event
sends only the four-byte signature `0xffaabbcc`; a matching receive on a client
copies the sender address and marks server-found. The exported enable operation
itself rejects a client. The game does not enable this service; its discovery
messages and retry interval remain N01's algorithm.

Debug setters and the noise generator are reached from normal reset only with
disabling arguments. A disabled debug setter returns −18 without changing its
rate/delay fields. Disabling calls noise-stop, drop-rate(0,0), delay(0,0) **before**
publishing debug=0; if already disabled, those setters can leave prior values.
The game does not request a noise worker. Its unrelated random draws must not
be inserted into a game RNG stream.

Original DLL attach stores its module identity, starts its diagnostic log and
attempts autodial restoration. Detach resets the library, closes the log and
attempts restoration. Diagnostic formatting/registry failures have the bounded
input and excluded-write cases in Appendix B. A native adapter can provide
these services explicitly; they do not alter application packet formats.

### Original §9. Modem, serial and launch-lobby providers

These are original supported entry paths whose algorithms depend on native
providers. The TCP-only port's failure stubs do not establish original failure
behavior. The independent reconstruction supplies their source contracts, but
this document's fixture does not establish a working modem, serial or DirectPlay
session. A cleanroom implementation may declare an unavailable provider rather
than advertise a completed compatibility path.

#### Legacy initialization and modem directory

InitializeModem/InitializeSerial preserves last-error. If protocol is already
initialized, return −4. Otherwise publish protocol 2/3 **before** creating the
control window. Set secure and broadcast enables to 1. Missing window returns
−5. Publish the supplied server flag, initialize COM, create a manual-reset stop
event and an automatic-reset receive event, then create the receive worker.
Failure at any stage returns −5 with preceding effects retained. The worker is
created before the DirectPlay interfaces below.

Create a lobby interface and query the required version. With a produced base
interface, clear the modem directory and provider/modem counts before the query.
On successful query, release the base, enumerate providers, enumerate modem
addresses and retain the queried lobby interface. Provider enumeration records
matching modem-provider GUIDs in order up to 32. Modem-address enumeration reads
a terminated list of names, increments the wrapped modem count **before** copying
each name into its 50-byte directory row, then advances by the source string's
length plus one. More than eight names would overwrite the original directory;
that write is excluded. A 50-byte copy need not append a terminator when the
source is longer. Later string reads need their actual backing.

To enumerate modem addresses, create the modem DirectPlay base, query the required
play interface, query player-address size, allocate on BufferTooSmall and repeat
the query. On success ask the lobby to enumerate that address using the name
callback. Release produced base/play interfaces and allocated address memory
at the common tail. The higher creation operation ignores enumeration failures
when deciding its final result: modem mode returns the resulting modem count;
serial returns 1. Interface creation/query failure returns −1.

A negative creation result makes the outer initializer set protocol −1 and
return −5. Modem count zero does likewise. Serial success does not require a
modem name. These returns do not undo the already created worker/events/COM
state. Normal reset later dispatches according to the then-current protocol;
do not infer a hidden rollback for a failure that already set it to −1.

GetNumModems returns the count only in mode 2, otherwise −14. SelectModem returns
−14 outside mode 2, −9 for a negative/out-of-range index, otherwise stores the
selected index and returns 0. GetModemName has an original **inverted guard**:
wrong mode or an index inside the valid count returns null; an invalid index
forms a directory-relative pointer. That pointer requires backing before use
and does not define an extra modem. The selected index is not consulted by the
compound-address construction below.

#### Provider address and session creation

Construct a compound-address request with the modem or serial provider GUID.
Add a phone-number element only when mode is 2, phone is nonnull/nonempty and
modem count is exactly 1. The local phone copy has its original bounded-storage
obligation; overflow is excluded. Request compound-address size, allocate on
BufferTooSmall, repeat with the same captured provider method, and publish the
address pointer/size only on success. Free a failed produced allocation.

For session creation, a missing lobby returns the provider's invalid-parameter
result in Appendix A. After successful address construction, release/null an
existing play output, create the play interface and initialize its connection.
On success prepare a session description with the application GUID:

- Host: flags 64, maximum players 2, name `XCOM XDaemon`.
- Client: clear the retained session count and enumerate once. The enumeration
  callback returns false on the completion flag, otherwise logs and records an
  instance GUID if below the 16-entry capacity, then also returns false. There
  is no local retry-until-timeout loop. The timeout argument is not consumed
  by this session-creation algorithm. If no session was recorded, fail; otherwise
  select the first retained GUID in a fresh description.

Open with host/create selector 2 or client/join selector 1. On success publish
the play interface. A still-private interface is closed/released on failure;
release the compound-address allocation afterward. Preserve the provider's
result and preceding output changes.

ActivateServer calls this with host=true and an empty phone string. On successful
session creation, create a local player with host flag 256 and the receive event;
then publish connection ID 0. OpenConnection calls it as a client with the supplied
phone and timeout, creates a player with flags 0, and publishes ID 1. Failure
returns false to their outer API mappings. A successful client then invokes the
connected callback if present and returns 0.

#### Legacy send, receive and worker order

A provider send uses `[type: u32 little-endian][payload]`, with no TCP envelope
or 64-byte padding. Secure send passes its signed flags extended to a full word
and reliable=true; broadcast passes its full header and reliable=false. The
requested destination is ignored by this inner operation. Actual provider target
is retained remote player for a server, and provider player ID 1 for a client.

Compute wrapped `payloadSize+4`. Use shared storage when it fits, otherwise
allocate/lock a block; allocation failure returns 0. Write type then forward-copy
payload in words and trailing bytes. Wrapped sizes that would exceed the chosen
storage are excluded-write paths. Call provider Send with local player, selected
target and reliable flag. Release owned storage, log recognized failure results
as applicable, then return **payload size even when the provider returned an
error**. Do not introduce success/failure propagation that the original caller
did not receive.

The receive worker captures the receive and stop event handles once, in that
order. Wait for either; only selection of the receive event invokes the receive
loop and waits again. Stop or another wait result exits the worker. If both are
ready, the first event has native wait priority. Provider callbacks here run on
this worker, unlike socket-window callbacks.

Each receive-loop entry starts with shared capacity 10240 and shared storage.
The provider receives with from/to outputs initialized to zero. Its size output
is retained as the **next call's size input**, not reset to allocation capacity
on each iteration. BufferTooSmall frees a previously owned block, allocates/locks
the newly requested size and retries. Allocation failure returns. Other failed
provider results release owned storage and return.

For success with size at least four:

- Sender 0 is a system message. Type 3 reads the player ID at offset 8, publishes
  remote player, and calls optional new-client(2,1). This read has no separate
  twelve-byte size gate. Type 5 clears remote player and calls optional lost(1).
  Other types have no action here.
- Nonzero sender with size greater than four and a registered secure callback
  reads type, then calls that callback with the **provider's full sender ID**,
  payload bytes and size−4. Both reliable and broadcast provider records use
  this secure callback path.

Continue receiving until failure/size-growth handling returns. Do not conflate
provider player IDs with the two local connection IDs, or convert legacy worker
callbacks into the TCP tag-routing algorithm. Appendix A records ABI narrowing.

Close destroys the local provider player when a play interface exists, sets
connection ID −1 and returns whether the provider result succeeded. It does not
close/release every interface or terminate the worker. Mode-specific shutdown
only runs when current protocol matches: signal stop, wait up to 1000 ms, close
stop-event and thread handles, destroy/null the control window and uninitialize
COM. The original retains protocol, stale closed handles and the unclosed receive
event. Reset subsequently clears its own selected fields. ShutdownTEN is an
original empty operation, distinct from ShutdownSerial; N01's caller order must
preserve that distinction.

#### Launch-lobby exchange

LaunchedByLobby creates a lobby base; factory failure returns −4. Query the
required lobby interface, then release the base; query failure returns −5.
Ask for connection-settings size with no output buffer. The outer operation
requires BufferTooSmall: NotLobbied closes the retained lobby and returns −3;
any other result closes it and returns −7.

The inner launch-info reader requires a lobby, otherwise −2. Ask for size again;
S_OK or BufferTooSmall admits allocation and a second settings query. On successful
query, clear the whole logical launch-info record, then copy long player name,
short player name and session name **in that order**. Set host=1 when connection
flag 2 is set. Otherwise set host=0 and enumerate address elements; the internet
address GUID copies the address and returns false to stop enumeration, while
other GUIDs return true. Release settings storage and return 0. Failure releases
produced storage and returns −1.

The outer operation closes the lobby and returns −6 if this reader fails.
Success retains the lobby and returns 2 for a host, 1 for a client. Output field
capacities are interoperability boundaries in Appendix A. The original unbounded
string copies can cross fields; those writes are excluded bugs even when they
stay inside the larger launch record. Do not specify corruption of short name,
session name or host as required behavior.

LobbySendMessage without a lobby returns −2. Otherwise copy the caller's word
to a local, send exactly four bytes with flags 2 and player 0, then return 0 for
S_OK or log/return −1 otherwise. LobbyClose releases and nulls the retained
interface. Neither operation independently resets all transport state.

The Zone-launch export asks the shell to open the supplied target and returns
0 only when the signed returned handle/result is greater than 32, otherwise −1.
Availability and UI of that external service are outside the recovered algorithm;
no account, remote login or network request is implicitly added to game startup.

### Original native-backend conformance case

| OS receive capacity 65536 | Original receive chunk is zero; BF-294 is a separate correction. |

### Original queued-record descriptor and promotion

If fewer than length bytes were sent, create a queued-record descriptor. If
shared storage was used, allocate private storage and copy the **whole record**,
including retained padding, before recording the sent offset. Append to the
FIFO, post Write, return 1. Otherwise release private storage if used and return
2. Unchecked descriptor/node/promotion allocation failures require a safe stop
before the original null/out-of-bounds write; they are not recovered successful
allocations or an invented common −2 return.

## Appendix E. Original numeric annotations

These original annotations retain storage widths and reference-evaluation context.
They do not prescribe internal types or redundant casts. The main contract and
F01 determine logical ranges and behaviorally significant conversions; external
file/wire encodings remain normative in their owning mappings. Where an excerpt
includes an off-nominal conversion, it applies to that explicitly admitted profile,
not as a new requirement to accept unsupported inputs.

### Reference annotation 1

Ordinary counts, lengths, tags and tick subtraction use unsigned 32-bit wrap.
Returned integer results use the same bits interpreted as signed 32-bit where
specified. Connection IDs retain signed 16-bit state; destination arguments
retain unsigned 16 bits, and routing fields retain eight bits. These widths
change behavior and wire compatibility; private record layouts remain free.
