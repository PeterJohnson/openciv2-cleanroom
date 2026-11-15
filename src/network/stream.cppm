export module openciv.network.stream;

import std;
export import openciv.base.callback_ref;
export import openciv.formats.bytes;

export namespace openciv::network {
using formats::Byte;
using formats::Bytes;
inline constexpr std::uint32_t id_tag = 0xfa000000;
inline constexpr std::uint32_t ping_request_tag = 0xfc000000;
inline constexpr std::uint32_t ping_reply_tag = 0xfd000000;
/// Pack source, destination, and flags into the classic transport routing tag.
constexpr std::uint32_t route_tag(std::int32_t source, std::uint16_t destination,
                                  std::uint32_t flags) {
    return (static_cast<std::uint32_t>(source) << 24) |
           ((static_cast<std::uint32_t>(destination) & 255) << 16) | (flags & 65535);
}
/// N05 §4. Padding defaults to zero for payload interoperability. Exact wire
/// comparisons may instead supply every padding byte; received padding is opaque.
Bytes encode_frame(std::uint32_t tag, std::span<const Byte> payload,
                   std::span<const Byte> padding = {});
/// Borrowed views of one decoded frame; valid only while its backing remains unchanged.
struct FrameView {
    std::uint32_t tag;
    std::span<const Byte> payload, padding, wire;
};
/// Borrowed handlers for complete frames and oversized-frame notifications.
struct ReceiveCallbacks {
    base::CallbackRef<void(FrameView)> frame;
    base::CallbackRef<void(std::uint32_t)> oversized;
};
/// Reason an incremental frame feed stopped consuming its input.
enum class ReceiveStop {
    exhausted,
    oversized,
    zero_total,
    malformed,
    backing_limit,
    reset_in_callback,
    closed
};
/// The number of delivered frames and the feed stop reason.
struct ReceiveResult {
    std::size_t delivered = 0;
    ReceiveStop stop = ReceiveStop::exhausted;
};
/// N05 §5. Caller owns dispatch and read boundaries. Rejected frames discard the
/// rest of this feed, never an inferred number of future socket bytes. Ordinary
/// receive callbacks may reset/close this object; the object itself must outlive
/// feed. Direct recursive feed are not admitted.
class FrameReceiver {
public:
    std::uint32_t oversized_limit = 50000;
    // Explicit safe backing profile, independent of callback-based N05 threshold.
    std::uint32_t backing_limit = 8 * 1024 * 1024;
    /// Consume a fragment and synchronously deliver complete frames to the supplied handlers.
    ReceiveResult feed(std::span<const Byte> input, const ReceiveCallbacks& callbacks);
    /// Discard pending frame bytes and reopen the receiver for subsequent feeds.
    void reset() noexcept;
    /// Discard pending bytes and reject subsequent feeds until reset.
    void close() noexcept;
    /// Return the bytes retained for the currently incomplete frame.
    std::size_t received() const noexcept { return frame_.empty() ? header_used_ : received_; }
    /// Return the declared total size of the pending frame, or zero before its header.
    std::uint32_t pending_total() const noexcept { return total_; }
    /// Report whether any bytes of an incomplete frame are retained.
    bool incomplete() const noexcept { return received() != 0; }

private:
    /// Clear partial header, payload, and declared-length state.
    void clear_pending() noexcept;
    std::array<Byte, 4> header_{};
    std::size_t header_used_ = 0, received_ = 0;
    std::uint32_t total_ = 0;
    Bytes frame_;
    std::uint64_t epoch_ = 0;
    bool closed_ = false, feeding_ = false;
};

/// Portable transport results used by the classic send-queue contract.
enum class SocketError {
    none,
    would_block,
    in_progress,
    interrupted,
    no_buffers,
    is_connected,
    fatal
};
/// The count of accepted bytes and a portable socket error.
struct SocketResult {
    std::size_t accepted = 0;
    SocketError error = SocketError::none;
};
/// Borrowed socket service reporting accepted bytes explicitly.
class SocketTransport {
public:
    SocketTransport() = default;
    virtual ~SocketTransport() = default;
    SocketTransport(const SocketTransport&) = delete;
    SocketTransport& operator=(const SocketTransport&) = delete;
    /// Exceptions mean no bytes were accepted. Accepted bytes require a result.
    virtual SocketResult transmit(std::span<const Byte> bytes) const = 0;
};
/// Current transport chunk and oversized-frame limits.
struct SendLimits {
    std::uint32_t chunk_capacity = 8192, oversized_limit = 50000;
};
/// Borrowed transport, live limits, and queue notification callbacks.
struct SendDependencies {
    const SocketTransport& transport;
    // Borrow current library-wide limits across queued records.
    const SendLimits& limits;
    base::CallbackRef<void()> post_write;
    base::CallbackRef<void(std::size_t)> backlog;
};
/// Outcome of the initial attempt to send or queue a frame.
enum class SendResult { immediate, queued, oversized, is_connected, fatal };
/// Outcome of draining a queued transport record.
enum class DrainResult { empty, complete, pending, fatal, excluded_retired_descriptor };
/// Own unsent frame bytes and resume partial writes without duplicating accepted data.
class SendQueue {
public:
    /// N05 §4; socket service calls do not reenter this queue. Event posting may
    /// observe it after publication. All queued bytes, including padding, are owned.
    SendResult send(std::uint32_t tag, std::span<const Byte> payload,
                    const SendDependencies& services, std::span<const Byte> padding = {});
    /// Attempt to advance the first queued record using current transport limits.
    DrainResult drain_one(const SendDependencies& services);
    /// Handle a writable event, draining pending output and invoking required close handling.
    DrainResult write_event(const SendDependencies& services,
                            const base::CallbackRef<void()>& close_handler);
    /// Return the number of retained output records.
    std::size_t size() const noexcept { return queue_.size(); }
    /// Return the accepted prefix length of the first queued record.
    std::size_t front_offset() const noexcept { return queue_.empty() ? 0 : queue_.front().sent; }
    /// Return the current queue-depth notification threshold.
    std::size_t backlog_threshold() const noexcept { return backlog_threshold_; }
    /// Discard all pending output records.
    void clear() noexcept { queue_.clear(); }

private:
    /// Owned wire bytes and the prefix already accepted by the transport.
    struct Pending {
        Bytes bytes;
        std::size_t sent = 0;
    };
    std::deque<Pending> queue_;
    std::size_t backlog_threshold_ = 50;
};

/// N05 §6. Backing contains the four-byte header plus retained payload capacity.
/// A null payload retains prior bytes; requested length narrows to u16 before cap.
Bytes encode_datagram(std::uint32_t header, std::uint32_t requested,
                      const std::span<const Byte>* payload, Bytes& retained_backing);
/// An owned datagram header and payload.
struct Datagram {
    std::uint32_t header;
    Bytes payload;
};
/// Decode a datagram header and retain its remaining bytes as payload.
Datagram decode_datagram(std::span<const Byte> bytes);
} // namespace openciv::network
