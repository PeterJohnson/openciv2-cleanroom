#pragma once

#include "formats/bytes.hpp"
#include <array>
#include <deque>
#include <functional>

namespace openciv::network {
using formats::Byte;
using formats::Bytes;
inline constexpr std::uint32_t id_tag = 0xfa000000;
inline constexpr std::uint32_t ping_request_tag = 0xfc000000;
inline constexpr std::uint32_t ping_reply_tag = 0xfd000000;
constexpr std::uint32_t route_tag(std::int32_t source, std::uint16_t destination,
                                  std::uint32_t flags) {
    return (static_cast<std::uint32_t>(source) << 24) |
           ((static_cast<std::uint32_t>(destination) & 255) << 16) | (flags & 65535);
}
// N05 §4. Padding defaults to zero for payload interoperability. Exact wire
// comparisons may instead supply every padding byte; received padding is opaque.
Bytes encode_frame(std::uint32_t tag, std::span<const Byte> payload,
                   std::span<const Byte> padding = {});
struct FrameView {
    std::uint32_t tag;
    std::span<const Byte> payload, padding, wire;
};
struct ReceiveCallbacks {
    std::function<void(FrameView)> frame;
    std::function<void(std::uint32_t)> oversized;
};
enum class ReceiveStop {
    exhausted,
    oversized,
    zero_total,
    malformed,
    backing_limit,
    reset_in_callback,
    closed
};
struct ReceiveResult {
    std::size_t delivered = 0;
    ReceiveStop stop = ReceiveStop::exhausted;
};
// N05 §5. Caller owns dispatch and read boundaries. Rejected frames discard the
// rest of this feed, never an inferred number of future socket bytes. Ordinary
// receive callbacks may reset/close this object; the object itself must outlive
// feed. Direct recursive feed are not admitted.
class FrameReceiver {
public:
    std::uint32_t oversized_limit = 50000;
    // Explicit safe backing profile, independent of callback-based N05 threshold.
    std::uint32_t backing_limit = 8 * 1024 * 1024;
    ReceiveResult feed(std::span<const Byte> input, ReceiveCallbacks& callbacks);
    void reset() noexcept;
    void close() noexcept;
    std::size_t received() const noexcept { return frame_.empty() ? header_used_ : received_; }
    std::uint32_t pending_total() const noexcept { return total_; }
    bool incomplete() const noexcept { return received() != 0; }

private:
    void clear_pending() noexcept;
    std::array<Byte, 4> header_{};
    std::size_t header_used_ = 0, received_ = 0;
    std::uint32_t total_ = 0;
    Bytes frame_;
    std::uint64_t epoch_ = 0;
    bool closed_ = false, feeding_ = false;
};

enum class SocketError {
    none,
    would_block,
    in_progress,
    interrupted,
    no_buffers,
    is_connected,
    fatal
};
struct SocketResult {
    std::size_t accepted = 0;
    SocketError error = SocketError::none;
};
struct SendServices {
    // Live library-wide capacity, not a value captured by each queued record.
    std::uint32_t chunk_capacity = 8192, oversized_limit = 50000;
    // Required. Exceptions mean no bytes were accepted; after accepting bytes,
    // return a SocketResult. A throwing call leaves queued bytes/offset intact.
    std::function<SocketResult(std::span<const Byte>)> send;
    std::function<void()> post_write;
    std::function<void(std::size_t)> backlog;
};
enum class SendResult { immediate, queued, oversized, is_connected, fatal };
enum class DrainResult { empty, complete, pending, fatal, excluded_retired_descriptor };
class SendQueue {
public:
    // N05 §4; socket service calls do not reenter this queue. Event posting may
    // observe it after publication. All queued bytes, including padding, are owned.
    SendResult send(std::uint32_t tag, std::span<const Byte> payload, SendServices& services,
                    std::span<const Byte> padding = {});
    DrainResult drain_one(SendServices& services);
    DrainResult write_event(SendServices& services, const std::function<void()>& close_handler);
    std::size_t size() const noexcept { return queue_.size(); }
    std::size_t front_offset() const noexcept { return queue_.empty() ? 0 : queue_.front().sent; }
    std::size_t backlog_threshold() const noexcept { return backlog_threshold_; }
    void clear() noexcept { queue_.clear(); }

private:
    struct Pending {
        Bytes bytes;
        std::size_t sent = 0;
    };
    std::deque<Pending> queue_;
    std::size_t backlog_threshold_ = 50;
};

// N05 §6. Backing contains the four-byte header plus retained payload capacity.
// A null payload retains prior bytes; requested length narrows to u16 before cap.
Bytes encode_datagram(std::uint32_t header, std::uint32_t requested,
                      const std::span<const Byte>* payload, Bytes& retained_backing);
struct Datagram {
    std::uint32_t header;
    Bytes payload;
};
Datagram decode_datagram(std::span<const Byte> bytes);
} // namespace openciv::network
