#include "network/stream.hpp"
#include <limits>

namespace openciv::network {
namespace {
std::uint32_t word(std::span<const Byte> bytes) {
    return formats::ByteReader{bytes}.u32();
}
void post(SendServices& services) {
    if (services.post_write)
        services.post_write();
}
SocketResult attempt(std::span<const Byte> bytes, SendServices& services) {
    if (!services.send)
        throw std::invalid_argument("Missing socket send service");
    const auto part = bytes.first(std::min<std::size_t>(bytes.size(), services.chunk_capacity));
    const auto result = services.send(part);
    if (result.error == SocketError::none && result.accepted > part.size())
        throw std::invalid_argument("Socket accepted more than its supplied span");
    return result;
}
} // namespace
Bytes encode_frame(std::uint32_t tag, std::span<const Byte> payload,
                   std::span<const Byte> padding) {
    if (payload.size() > std::numeric_limits<std::uint32_t>::max() - 75U)
        throw std::length_error("Frame length exceeds nonwrapping backing domain");
    const auto total = (payload.size() + 12 + 63) & ~std::size_t{63};
    const auto tail = total - payload.size() - 12;
    if (!padding.empty() && padding.size() != tail)
        throw std::invalid_argument("Exact frame padding must cover the whole tail");
    formats::ByteWriter writer;
    writer.bytes.reserve(total);
    writer.u32(static_cast<std::uint32_t>(total));
    writer.u32(tag);
    writer.u32(static_cast<std::uint32_t>(payload.size()));
    writer.append(payload);
    if (padding.empty())
        writer.bytes.resize(total, 0);
    else
        writer.append(padding);
    return std::move(writer.bytes);
}
void FrameReceiver::clear_pending() noexcept {
    header_used_ = received_ = total_ = 0;
    frame_.clear();
}
void FrameReceiver::reset() noexcept {
    clear_pending();
    closed_ = false;
    ++epoch_;
}
void FrameReceiver::close() noexcept {
    reset();
    closed_ = true;
}
ReceiveResult FrameReceiver::feed(std::span<const Byte> input, ReceiveCallbacks& callbacks) {
    if (feeding_)
        throw std::logic_error("Recursive stream feed is outside the admitted dispatch domain");
    if (closed_)
        return {0, ReceiveStop::closed};
    struct Guard {
        bool& flag;
        ~Guard() { flag = false; }
    } guard{feeding_};
    feeding_ = true;
    ReceiveResult result;
    const auto discard = [&](ReceiveStop stop) {
        clear_pending();
        result.stop = stop;
        return result;
    };
    while (!input.empty()) {
        if (frame_.empty()) {
            const bool split = header_used_ != 0;
            const auto count = std::min<std::size_t>(4 - header_used_, input.size());
            std::copy_n(input.begin(), count, header_.data() + header_used_);
            header_used_ += count;
            input = input.subspan(count);
            if (header_used_ < 4)
                return result;
            total_ = word(header_);
            if (callbacks.oversized && total_ > std::uint32_t(oversized_limit - 12U)) {
                const auto epoch = epoch_;
                const auto callback = callbacks.oversized;
                callback(split ? total_ : std::uint32_t(total_ - 12U));
                if (epoch != epoch_)
                    return {result.delivered, ReceiveStop::reset_in_callback};
                return discard(ReceiveStop::oversized);
            }
            if (total_ == 0)
                return discard(ReceiveStop::zero_total);
            if (total_ < 12)
                return discard(ReceiveStop::malformed);
            if (total_ > backing_limit)
                return discard(ReceiveStop::backing_limit);
            try {
                if (callbacks.allocate) {
                    const auto epoch = epoch_;
                    const bool allocated = callbacks.allocate(total_);
                    if (epoch != epoch_)
                        return {result.delivered, ReceiveStop::reset_in_callback};
                    if (!allocated)
                        return discard(ReceiveStop::allocation_failed);
                }
                frame_.resize(total_);
            } catch (const std::bad_alloc&) {
                return discard(ReceiveStop::allocation_failed);
            }
            std::copy(header_.begin(), header_.end(), frame_.begin());
            header_used_ = 0;
            received_ = 4;
        }
        const auto count = std::min<std::size_t>(total_ - received_, input.size());
        std::copy_n(input.begin(), count, frame_.data() + received_);
        received_ += count;
        input = input.subspan(count);
        if (received_ != total_)
            return result;
        const auto tag = word(std::span{frame_}.subspan(4));
        const auto length = word(std::span{frame_}.subspan(8));
        if (length > total_ - 12)
            return discard(ReceiveStop::malformed);
        if (callbacks.frame) {
            const auto epoch = epoch_;
            const auto callback = callbacks.frame;
            ++result.delivered;
            callback({tag, std::span{frame_}.subspan(12, length),
                      std::span{frame_}.subspan(12 + length), frame_});
            if (epoch != epoch_)
                return {result.delivered, ReceiveStop::reset_in_callback};
        }
        clear_pending();
    }
    return result;
}
SendResult SendQueue::send(std::uint32_t tag, std::span<const Byte> payload, SendServices& services,
                           std::span<const Byte> padding) {
    if (payload.size() >= std::uint32_t(services.oversized_limit - 12U))
        return SendResult::oversized;
    if (!services.send)
        throw std::invalid_argument("Missing socket send service");
    Bytes record;
    try {
        record = encode_frame(tag, payload, padding);
    } catch (const std::bad_alloc&) {
        return SendResult::allocation_failed;
    }
    if (!queue_.empty()) {
        queue_.push_back({std::move(record), 0});
        post(services);
        return SendResult::queued;
    }
    auto sent = attempt(record, services);
    switch (sent.error) {
    case SocketError::none:
        break;
    case SocketError::would_block:
    case SocketError::in_progress:
    case SocketError::interrupted:
    case SocketError::no_buffers:
        sent.accepted = 0;
        break;
    case SocketError::is_connected:
        return SendResult::is_connected;
    case SocketError::fatal:
        return SendResult::fatal;
    }
    if (sent.accepted >= record.size())
        return SendResult::immediate;
    // Retention failure propagates; N05 does not establish a common -2 here.
    queue_.push_back({std::move(record), sent.accepted});
    post(services);
    return SendResult::queued;
}
DrainResult SendQueue::drain_one(SendServices& services) {
    if (queue_.empty())
        return DrainResult::empty;
    if (!services.send)
        throw std::invalid_argument("Missing socket send service");
    const auto count = queue_.size();
    if (count > backlog_threshold_) {
        if (services.backlog)
            services.backlog(count);
        backlog_threshold_ = count + 50;
    } else if (count < 50) {
        backlog_threshold_ = 50;
    }
    auto& pending = queue_.front();
    auto sent = attempt(std::span{pending.bytes}.subspan(pending.sent), services);
    if (sent.error == SocketError::would_block)
        sent.accepted = 0;
    else if (sent.error == SocketError::is_connected) {
        queue_.pop_front();
        return DrainResult::excluded_retired_descriptor;
    } else if (sent.error != SocketError::none) {
        queue_.pop_front();
        return DrainResult::fatal;
    }
    pending.sent += sent.accepted;
    if (pending.sent == pending.bytes.size()) {
        queue_.pop_front();
        return DrainResult::complete;
    }
    post(services);
    return DrainResult::pending;
}
DrainResult SendQueue::write_event(SendServices& services,
                                   const std::function<void()>& close_handler) {
    auto result = DrainResult::empty;
    while (!queue_.empty()) {
        result = drain_one(services);
        if (result == DrainResult::fatal && close_handler)
            close_handler();
        if (result != DrainResult::complete)
            break;
    }
    return result;
}
Bytes encode_datagram(std::uint32_t header, std::uint32_t requested,
                      const std::span<const Byte>* payload, Bytes& retained_backing) {
    if (retained_backing.size() < 4)
        throw std::invalid_argument("Datagram backing lacks its header");
    const auto count =
        std::min<std::size_t>(static_cast<std::uint16_t>(requested), retained_backing.size() - 4);
    if (payload && payload->size() < count)
        throw std::invalid_argument("Datagram payload lacks requested backing");
    for (unsigned i = 0; i < 4; ++i)
        retained_backing[i] = static_cast<Byte>(header >> (8 * i));
    if (payload)
        std::copy_n(payload->begin(), count, retained_backing.begin() + 4);
    const auto result = std::span{retained_backing}.first(4 + count);
    return {result.begin(), result.end()};
}
Datagram decode_datagram(std::span<const Byte> bytes) {
    formats::ByteReader reader{bytes};
    const auto header = reader.u32(); // Short datagrams are rejected by this safe profile.
    return {header, Bytes{reader.remaining().begin(), reader.remaining().end()}};
}
} // namespace openciv::network
