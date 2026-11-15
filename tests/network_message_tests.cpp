#include <catch2/catch_test_macros.hpp>

import std;
import openciv.tests.service_callbacks;
import openciv.network.messages;
import openciv.network.sections;
import openciv.network.stream;

// Specification-derived vectors: N02 sections 2, 5-6 and Appendices A/C;
// envelope composition uses N05 sections 4-5. No original capture is inferred.

using namespace openciv;
using namespace openciv::network;
using namespace openciv::formats;
namespace {
void put(std::span<Byte> bytes, std::size_t offset, std::uint32_t value) {
    for (unsigned i = 0; i < 4; ++i)
        bytes[offset + i] = static_cast<Byte>(value >> (8 * i));
}
StateSections synthetic_sections() {
    // Bounded section-service fixture. Real session producers must supply all
    // classic extents from classic_section_sizes and established wire padding.
    StateSections sections;
    for (std::size_t i = 0; i < 24; ++i) {
        auto& section = sections[i];
        section.descriptor.size = 3;
        if (i == 5)
            section.descriptor.size = 96;
        if (i == 6)
            section.descriptor.size = 176;
        section.backing.resize(static_cast<std::size_t>((section.descriptor.size + 3) & ~3),
                               static_cast<Byte>(i + 1));
    }
    sections[23].descriptor.mirror_offset = -71;
    register_sections(sections);
    return sections;
}
} // namespace

TEST_CASE("N02 header initialization preserves sequence and argument packets are exact",
          "[network][N02]") {
    Bytes header(16, 0xab);
    initialize_message_header(header, 0x12, 44);
    CHECK(header ==
          Bytes{0x60, 0x66, 0x60, 0x66, 0x12, 0, 0, 0, 44, 0, 0, 0, 0xab, 0xab, 0xab, 0xab});
    const auto decoded_header = decode_message_header(header);
    CHECK(decoded_header.sequence == 0xabababab);
    const std::array<std::int32_t, 6> arguments{
        -1, std::numeric_limits<std::int32_t>::min(), 0x12345678, 7, 0, 32768};
    const auto packet = encode_argument_message(21, arguments, Bytes{0, 0xff, 0x7f}, 0xffffffff);
    const Bytes expected{0x60, 0x66, 0x60, 0x66, 21,   0,    0,    0,    47, 0,    0,   0,
                         0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0,  0,    0,   0x80,
                         0x78, 0x56, 0x34, 0x12, 7,    0,    0,    0,    0,  0,    0,   0,
                         0,    0x80, 0,    0,    3,    0,    0,    0,    0,  0xff, 0x7f};
    CHECK(packet == expected);
    const auto message = decode_argument_message(expected);
    CHECK(message.arguments == arguments);
    CHECK(message.payload == Bytes{0, 0xff, 0x7f});
    CHECK(encode_message(decode_message(expected)) == expected);
    for (std::size_t n = 0; n < expected.size(); ++n)
        CHECK_THROWS_AS(decode_argument_message(std::span{expected}.first(n)), DecodeError);
    auto bad = expected;
    put(bad, 40, 4);
    CHECK_THROWS_AS(decode_argument_message(bad), DecodeError);
    put(bad, 0, 0);
    CHECK_THROWS_AS(decode_message_header(bad), DecodeError);
}

TEST_CASE("N02 send sequences survive failure and broadcast reads the live post-send size",
          "[network][N02]") {
    auto packet = encode_argument_message(21, {}, {}, 0x1234);
    MessageSendState state{0xffffffff, -99};
    CHECK(send_directed(packet, 255, state, [&](auto destination, auto bytes, auto size) {
        CHECK(destination == 255);
        CHECK(size == 44);
        CHECK(decode_message_header(bytes).sequence == 0xffffffff);
        CHECK(state.next_sequence == 0);
        return 0;
    }));
    CHECK(state.last_result == 0);
    CHECK_FALSE(send_directed(packet, 1, state, [](auto, auto, auto) { return -4; }));
    CHECK(state.next_sequence == 1);
    CHECK(state.last_result == -4);
    CHECK_FALSE(send_broadcast(packet, state, [](auto, auto) { return 0; }));
    CHECK(send_broadcast(packet, state, [](auto bytes, auto size) {
        CHECK(size == 44);
        put(bytes, 8, 20);
        return 20;
    }));
    CHECK(state.next_sequence == 3);
    CHECK(decode_message_header(packet).size == 20);
}

TEST_CASE("N02 message passes N05 fragmented FIFO transport without adding RNG data",
          "[network][N02][N05]") {
    const auto message = encode_argument_message(21, {2, -1, 0, 0, 0, 0}, Bytes{0xfe, 1}, 7);
    SendQueue outgoing;
    SendCallbacks services;
    services.chunk_capacity = 13;
    Bytes wire;
    services.send = [&](auto bytes) {
        const auto accepted = std::min<std::size_t>(bytes.size(), 7);
        wire.insert(wire.end(), bytes.data(), bytes.data() + accepted);
        return SocketResult{accepted};
    };
    CHECK(outgoing.send(route_tag(2, 0, 0), message, services) == SendResult::queued);
    while (outgoing.size())
        outgoing.write_event(services, {});
    FrameReceiver incoming;
    ReceiveCallbacks callbacks;
    std::vector<ArgumentMessage> delivered;
    callbacks.frame = [&](FrameView frame) {
        CHECK(frame.tag == 0x02000000);
        delivered.push_back(decode_argument_message(frame.payload));
    };
    for (std::size_t offset = 0; offset < wire.size(); offset += 3)
        incoming.feed(
            std::span{wire}.subspan(offset, std::min<std::size_t>(3, wire.size() - offset)),
            callbacks);
    REQUIRE(delivered.size() == 1);
    CHECK(delivered[0] == decode_argument_message(message));
}

TEST_CASE("N02 checksum preserves its short even-word summation and wrapping", "[network][N02]") {
    const Bytes data{1, 2, 3, 4, 5, 6};
    CHECK(section_checksum(data, 6) == 513);
    CHECK(section_checksum({}, 2) == 0); // No source read at size two.
    CHECK(section_checksum({}, -4) == 0);
    CHECK(section_checksum({}, 0) == 0);
    CHECK(section_checksum(data, 5) == 15);
    CHECK(section_checksum(data, 4) == 0x04030201);
    CHECK(section_checksum(Bytes{0xff, 0xff, 0xff, 0xff, 2, 0, 0, 0}, 8) == 1);
    CHECK_THROWS_AS(section_checksum(Bytes{1}, 6), DecodeError);
}

TEST_CASE("N02 raw image descriptors retain ancillary words and exact block order",
          "[network][N02]") {
    const Bytes expected{3,    0,    0,    0,    0xff, 0xff, 0xff, 0xff, 2, 0, 0, 0,
                         0x78, 0x56, 0x34, 0x12, 0xfe, 0xff, 0xff, 0xff, 1, 2, 3};
    const auto blocks = decode_raw_image(expected);
    REQUIRE(blocks.size() == 1);
    CHECK(blocks[0].descriptor == SectionDescriptor{3, -1, 2, 0x12345678, -2});
    CHECK(blocks[0].data == Bytes{1, 2, 3});
    CHECK(encode_raw_image(blocks) == expected);
    for (std::size_t n = 1; n < expected.size(); ++n)
        CHECK_THROWS_AS(decode_raw_image(std::span{expected}.first(n)), DecodeError);
    auto invalid = expected;
    put(invalid, 8, 24);
    CHECK_THROWS_AS(decode_raw_image(invalid), DecodeError);
    put(invalid, 8, 2);
    put(invalid, 0, 0xffffffff);
    CHECK_THROWS_AS(decode_raw_image(invalid), DecodeError);
}

TEST_CASE("N02 registration and mirrors retain event offset and padded scan semantics",
          "[network][N02]") {
    auto sections = synthetic_sections();
    CHECK(sections[23].descriptor.mirror_offset == -71);
    CHECK(sections[1].descriptor.mirror_offset == 4);
    CHECK(sections[0].descriptor.padded_size == 4);
    StateMirror host, client;
    allocate_mirror(host, sections, true);
    allocate_mirror(client, sections, false);
    CHECK(host.bytes == client.bytes);
    CHECK(host.scan_end == host.bytes.size());
    CHECK(client.scan_end == static_cast<std::size_t>(sections[15].descriptor.mirror_offset));
    CHECK(client.scan_end < client.bytes.size());
    CHECK(host.bytes[3] == 0); // Live backing padding is one, but allocation copies only data.
    sections[0].backing[0] = 0x20;
    host.scan = 99;
    invalidate_mirror(host, sections);
    CHECK(host.bytes[0] == 0xdf);
    CHECK(host.bytes[3] == 0xff);
    CHECK(host.scan == 0);
    invalidate_mirror(host, sections);
    CHECK(host.bytes[0] == 0xdf);
    CHECK(host.bytes[3] == 0); // Existing mirror padding is complemented again.
    auto sizes = classic_section_sizes(world::Geometry{80, 50});
    CHECK(sizes == std::array<std::int32_t, 24>{
                       4,  332, 1936, 11424, 1024, 65536, 22528, 1008, 1200, 100,  340,   1172,
                       16, 260, 260,  2000,  2000, 2000,  2000,  2000, 2000, 2000, 12000, 50000});
}

TEST_CASE("N02 raw image production separates captured checksums from live updates",
          "[network][N02]") {
    auto sections = synthetic_sections();
    for (auto& section : sections)
        section.descriptor.checksum = 0x12345678;
    const auto full = decode_raw_image(build_raw_image(sections, ImageKind::full, 2, 1));
    REQUIRE(full.size() == 24);
    for (std::size_t i = 0; i < full.size(); ++i) {
        CHECK(full[i].descriptor.id == static_cast<std::int32_t>(i));
        CHECK(full[i].descriptor.checksum == 0x12345678);
        CHECK(sections[i].descriptor.checksum ==
              section_checksum(full[i].data, full[i].descriptor.size));
    }
    CHECK(full[5].data.size() == 64);
    CHECK(full[5].descriptor.padded_size == 96);
    CHECK(full[6].data.size() == 88);
    CHECK(build_raw_image(sections, ImageKind::delta, 2, 1).empty());
    const auto old = sections[1].descriptor.checksum;
    sections[1].backing[0] = 99;
    const auto delta = decode_raw_image(build_raw_image(sections, ImageKind::delta, 2, 1));
    REQUIRE(delta.size() == 1);
    CHECK(delta[0].descriptor.id == 1);
    CHECK(delta[0].descriptor.checksum == old);
    const auto join = decode_raw_image(build_raw_image(sections, ImageKind::join, 0, 0));
    std::vector<std::int32_t> order;
    for (const auto& block : join) {
        order.push_back(block.descriptor.id);
        CHECK(block.descriptor.checksum == section_checksum(block.data, block.descriptor.size));
    }
    CHECK(order == std::vector<std::int32_t>{1, 7, 3, 2, 9, 11, 23});
    const auto map = decode_raw_image(build_raw_image(sections, ImageKind::map_header, 0, 0));
    REQUIRE(map.size() == 2);
    CHECK(map[0].descriptor.id == 1);
    CHECK(map[1].descriptor.id == 12);
    sections[5].descriptor.checksum = 7;
    const auto zero = decode_raw_image(build_raw_image(sections, ImageKind::delta, 0, 1));
    CHECK(sections[5].descriptor.checksum == 7);
    CHECK(
        std::none_of(zero.begin(), zero.end(), [](const auto& b) { return b.descriptor.id == 5; }));
}
