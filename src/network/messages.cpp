#include "network/messages.hpp"
#include <limits>

namespace openciv::network {
namespace {
using namespace formats;
void put(std::span<Byte> bytes, std::size_t offset, std::uint32_t value) {
    if (offset > bytes.size() || bytes.size() - offset < 4)
        throw std::invalid_argument("Message word lacks backing");
    for (unsigned i = 0; i < 4; ++i)
        bytes[offset + i] = static_cast<Byte>(value >> (i * 8));
}
std::uint32_t admit_send(const Bytes& packet) {
    const auto header = decode_message_header(packet);
    if (header.size != packet.size() ||
        header.size > static_cast<std::uint32_t>(std::numeric_limits<std::int32_t>::max()))
        throw std::invalid_argument("Message send requires exact positive declared backing");
    return header.size;
}
} // namespace
void initialize_message_header(std::span<formats::Byte> backing, std::uint32_t kind,
                               std::uint32_t size) {
    if (backing.size() < 16 || size < 16)
        throw std::invalid_argument("Message header requires sixteen bytes");
    put(backing, 8, size);
    put(backing, 0, message_magic);
    put(backing, 4, kind);
}
MessageHeader decode_message_header(std::span<const formats::Byte> bytes) {
    ByteReader reader{bytes};
    if (reader.u32() != message_magic)
        throw DecodeError(0, "Incorrect application message magic");
    MessageHeader header{reader.u32(), reader.u32(), reader.u32()};
    if (header.size < 16)
        throw DecodeError(8, "Application message shorter than its header");
    return header;
}
Message decode_message(std::span<const formats::Byte> bytes) {
    const auto header = decode_message_header(bytes);
    if (header.size != bytes.size())
        throw DecodeError(8, "Captured application size differs from declared size");
    return {header, Bytes{bytes.begin() + 16, bytes.end()}};
}
formats::Bytes encode_message(const Message& message) {
    if (message.header.size < 16 || message.body.size() != message.header.size - 16)
        throw std::invalid_argument("Message body differs from declared size");
    ByteWriter writer;
    writer.u32(message_magic);
    writer.u32(message.header.kind);
    writer.u32(message.header.size);
    writer.u32(message.header.sequence);
    writer.append(message.body);
    return std::move(writer.bytes);
}
ArgumentMessage decode_argument_message(std::span<const formats::Byte> bytes) {
    const auto message = decode_message(bytes);
    if (message.body.size() < 28)
        throw DecodeError(bytes.size(), "Truncated seven-argument envelope");
    ByteReader reader{message.body};
    ArgumentMessage result;
    result.header = message.header;
    for (auto& argument : result.arguments)
        argument = reader.i32();
    const auto size = reader.i32();
    if (size < 0 || static_cast<std::size_t>(size) != reader.remaining().size())
        throw DecodeError(40, "Application payload length differs from captured extent");
    result.payload.assign(reader.remaining().begin(), reader.remaining().end());
    return result;
}
formats::Bytes encode_argument_message(std::uint32_t kind,
                                       const std::array<std::int32_t, 6>& arguments,
                                       std::span<const formats::Byte> payload,
                                       std::uint32_t sequence) {
    if (payload.size() > std::numeric_limits<std::int32_t>::max() - 44U)
        throw std::length_error("Application payload exceeds admitted size domain");
    ByteWriter writer;
    writer.u32(message_magic);
    writer.u32(kind);
    writer.u32(static_cast<std::uint32_t>(44 + payload.size()));
    writer.u32(sequence);
    for (auto argument : arguments)
        writer.i32(argument);
    writer.i32(static_cast<std::int32_t>(payload.size()));
    writer.append(payload);
    return std::move(writer.bytes);
}
bool send_directed(formats::Bytes& packet, std::uint16_t recipient, MessageSendState& state,
                   const SecureSend& send) {
    if ((recipient > 7 && recipient != 255) || !send)
        throw std::invalid_argument("Invalid directed message service or recipient");
    const auto size = admit_send(packet);
    put(packet, 12, state.next_sequence++);
    state.last_result = send(recipient, packet, size);
    return state.last_result >= 0;
}
bool send_broadcast(formats::Bytes& packet, MessageSendState& state, const BroadcastSend& send) {
    if (!send)
        throw std::invalid_argument("Missing broadcast message service");
    const auto size = admit_send(packet);
    put(packet, 12, state.next_sequence++);
    state.last_result = send(packet, size);
    // Read just the live size word. A callback can intentionally change other
    // header bytes; this completion test is not another packet-validation stage.
    const auto live_size = ByteReader{std::span{packet}.subspan(8)}.i32();
    return state.last_result >= live_size;
}
} // namespace openciv::network
