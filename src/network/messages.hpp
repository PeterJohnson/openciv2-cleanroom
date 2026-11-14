#pragma once

#include "formats/bytes.hpp"
#include <array>
#include <functional>

namespace openciv::network {
inline constexpr std::uint32_t message_magic = 0x66606660;
inline constexpr std::size_t message_header_size = 16, argument_envelope_size = 44;
struct MessageHeader {
    std::uint32_t kind = 0, size = 16, sequence = 0;
    bool operator==(const MessageHeader&) const = default;
};
struct Message {
    MessageHeader header;
    formats::Bytes body;
    bool operator==(const Message&) const = default;
};
struct ArgumentMessage {
    MessageHeader header;
    std::array<std::int32_t, 6> arguments{};
    formats::Bytes payload;
    bool operator==(const ArgumentMessage&) const = default;
};
// N02 §2 / Appendix A. Pure owned wire codecs, not command authorization or
// allocator-size/alias emulation. Header initialization preserves sequence.
void initialize_message_header(std::span<formats::Byte> backing, std::uint32_t kind,
                               std::uint32_t size);
MessageHeader decode_message_header(std::span<const formats::Byte> bytes);
Message decode_message(std::span<const formats::Byte> bytes);
formats::Bytes encode_message(const Message& message);
ArgumentMessage decode_argument_message(std::span<const formats::Byte> bytes);
formats::Bytes encode_argument_message(std::uint32_t kind,
                                       const std::array<std::int32_t, 6>& arguments,
                                       std::span<const formats::Byte> payload,
                                       std::uint32_t sequence);
struct MessageSendState {
    std::uint32_t next_sequence = 0;
    std::int32_t last_result = 0;
};
using SecureSend =
    std::function<std::int32_t(std::uint16_t, std::span<formats::Byte>, std::uint32_t)>;
using BroadcastSend = std::function<std::int32_t(std::span<formats::Byte>, std::uint32_t)>;
// Retain sequence allocation on failure. Services may change packet bytes; the
// broadcast completion test reads its live declared size after the callback.
bool send_directed(formats::Bytes& packet, std::uint16_t recipient, MessageSendState& state,
                   const SecureSend& send);
bool send_broadcast(formats::Bytes& packet, MessageSendState& state, const BroadcastSend& send);
} // namespace openciv::network
