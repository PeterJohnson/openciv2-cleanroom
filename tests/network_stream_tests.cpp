#include <catch2/catch_test_macros.hpp>

import std;
import openciv.tests.service_callbacks;
import openciv.network.stream;

// Specification-derived vectors: N05 sections 4-6. These are authored fake
// streams/services, not original-program captures or a historical harness.

using namespace openciv;
using namespace openciv::network;
namespace {
void put(Bytes& bytes, std::size_t offset, std::uint32_t value) {
    for (unsigned i = 0; i < 4; ++i)
        bytes[offset + i] = static_cast<Byte>(value >> (i * 8));
}
} // namespace

TEST_CASE("N05 frames have exact little endian headers and opaque supplied padding",
          "[network][N05]") {
    const auto tag = route_tag(0x102, 0x103, 0x12345678);
    CHECK(tag == 0x02035678);
    CHECK(route_tag(-1, 65535, 0xffff0001) == 0xffff0001);
    const auto wire = encode_frame(id_tag, Bytes{3, 0}, Bytes(50, 0xa5));
    const Bytes expected{64, 0, 0, 0, 0, 0, 0, 0xfa, 2, 0, 0, 0, 3, 0};
    CHECK(Bytes(wire.begin(), wire.begin() + 14) == expected);
    CHECK(Bytes(wire.begin() + 14, wire.end()) == Bytes(50, 0xa5));
    CHECK(encode_frame(0, Bytes(100)).size() == 128);
    CHECK(encode_frame(0, Bytes(308)).size() == 320);
    CHECK(encode_frame(0, Bytes(3002)).size() == 3072);
    CHECK_THROWS(encode_frame(0, Bytes(100), Bytes(15)));
}

TEST_CASE("N05 stream assembly survives every split and coalesced records", "[network][N05]") {
    const Bytes payload(100, 0x93);
    const auto first = encode_frame(0x01020003, payload, Bytes(16, 0x81));
    const auto second = encode_frame(id_tag, Bytes{6, 0});
    auto wire = first;
    wire.insert(wire.end(), second.begin(), second.end());
    for (std::size_t split = 0; split <= wire.size(); ++split) {
        FrameReceiver receiver;
        std::vector<Bytes> frames, payloads;
        std::vector<std::uint32_t> tags;
        ReceiveCallbackFixture callbacks;
        callbacks.frame = [&](FrameView frame) {
            frames.emplace_back(frame.wire.begin(), frame.wire.end());
            payloads.emplace_back(frame.payload.begin(), frame.payload.end());
            tags.push_back(frame.tag);
        };
        CHECK(receiver.feed(std::span{wire}.first(split), callbacks).stop ==
              ReceiveStop::exhausted);
        CHECK(receiver.feed(std::span{wire}.subspan(split), callbacks).stop ==
              ReceiveStop::exhausted);
        CHECK(frames == std::vector<Bytes>{first, second});
        CHECK(payloads == std::vector<Bytes>{payload, Bytes{6, 0}});
        CHECK(tags == std::vector<std::uint32_t>{0x01020003, id_tag});
        CHECK_FALSE(receiver.incomplete());
    }
    FrameReceiver receiver;
    std::size_t delivered = 0;
    ReceiveCallbackFixture callbacks;
    callbacks.frame = [&](FrameView) { ++delivered; };
    for (auto byte : wire)
        receiver.feed(std::span{&byte, 1}, callbacks);
    CHECK(delivered == 2);
    CHECK_FALSE(receiver.incomplete());
}

TEST_CASE("N05 oversize reports depend on header fragmentation and discard one receive",
          "[network][N05]") {
    const auto oversized = encode_frame(0, Bytes(100)); // Total 128.
    const auto good = encode_frame(0, Bytes{9});        // Total 64, limit 76 allows equality.
    auto combined = oversized;
    combined.insert(combined.end(), good.begin(), good.end());
    for (std::size_t split : {0U, 1U, 2U, 3U}) {
        FrameReceiver receiver;
        receiver.oversized_limit = 76;
        std::vector<std::uint32_t> reports;
        std::size_t delivered = 0;
        ReceiveCallbackFixture callbacks;
        callbacks.oversized = [&](auto size) { reports.push_back(size); };
        callbacks.frame = [&](FrameView) { ++delivered; };
        receiver.feed(std::span{combined}.first(split), callbacks);
        CHECK(receiver.feed(std::span{combined}.subspan(split), callbacks).stop ==
              ReceiveStop::oversized);
        CHECK(reports == std::vector<std::uint32_t>{split ? 128U : 116U});
        CHECK(delivered == 0); // Does not recover at the next frame in this receive.
        CHECK_FALSE(receiver.incomplete());
        CHECK(receiver.feed(good, callbacks).delivered == 1);
    }
    FrameReceiver receiver;
    receiver.oversized_limit = 76;
    ReceiveCallbackFixture callbacks;
    callbacks.frame = [](FrameView f) { CHECK(f.payload.size() == 100); };
    CHECK(receiver.feed(oversized, callbacks).delivered == 1); // No callback, no size threshold.
}

TEST_CASE("N05 receiver admission and callback reset preserve bounded failure effects",
          "[network][N05]") {
    const auto good = encode_frame(0, Bytes{1});
    FrameReceiver receiver;
    ReceiveCallbackFixture callbacks;
    CHECK_FALSE(receiver.incomplete());
    auto malformed = good;
    put(malformed, 8, 60); // More payload than total-12.
    CHECK(receiver.feed(malformed, callbacks).stop == ReceiveStop::malformed);
    CHECK(receiver.feed(Bytes{0, 0, 0, 0, 9, 9}, callbacks).stop == ReceiveStop::zero_total);
    CHECK(receiver.feed(Bytes{8, 0, 0, 0, 9, 9}, callbacks).stop == ReceiveStop::malformed);
    receiver.backing_limit = 32;
    CHECK(receiver.feed(good, callbacks).stop == ReceiveStop::backing_limit);
    receiver.backing_limit = 1024;
    callbacks.frame = [&](FrameView f) {
        CHECK(f.payload[0] == 1);
        CHECK_THROWS(receiver.feed({}, callbacks));
        receiver.close();
    };
    auto doubled = good;
    doubled.insert(doubled.end(), good.begin(), good.end());
    const auto result = receiver.feed(doubled, callbacks);
    CHECK(result.delivered == 1);
    CHECK(result.stop == ReceiveStop::reset_in_callback);
    CHECK(receiver.feed(good, callbacks).stop == ReceiveStop::closed);
    receiver.reset();
    callbacks.frame = {};
    CHECK(receiver.feed(good, callbacks).stop == ReceiveStop::exhausted);
    // A nominal backed frame need not be a multiple of 64 on receive.
    Bytes short_frame{13, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 7};
    callbacks.frame = [](FrameView f) {
        CHECK(f.payload[0] == 7);
        CHECK(f.padding.empty());
    };
    CHECK(receiver.feed(short_frame, callbacks).delivered == 1);
}

TEST_CASE("N05 partial sends own padding and keep FIFO across live chunk changes",
          "[network][N05]") {
    SendQueue queue;
    SendCallbacks services;
    Bytes sent, payload(100, 0x31), padding(16, 0x99);
    const auto expected_first = encode_frame(0x01020003, payload, padding);
    const auto expected_second = encode_frame(id_tag, Bytes{6, 0});
    unsigned sends = 0, posts = 0;
    services.post_write = [&] {
        ++posts;
        CHECK(queue.size() != 0);
    };
    services.send = [&](std::span<const Byte> bytes) {
        const auto count = sends++ == 0 ? 25U : bytes.size();
        sent.insert(sent.end(), bytes.data(), bytes.data() + count);
        return SocketResult{count};
    };
    CHECK(queue.send(0x01020003, payload, services, padding) == SendResult::queued);
    CHECK(queue.front_offset() == 25);
    CHECK(queue.send(id_tag, Bytes{6, 0}, services) == SendResult::queued);
    CHECK(sends == 1);
    std::fill(payload.begin(), payload.end(), Byte{0});
    std::fill(padding.begin(), padding.end(), Byte{0});
    services.chunk_capacity = 16;
    while (queue.size())
        queue.write_event(services, {});
    auto expected = expected_first;
    expected.insert(expected.end(), expected_second.begin(), expected_second.end());
    CHECK(sent == expected);
    CHECK(posts > 2);
}

TEST_CASE("N05 immediate and queued socket errors have different retry contracts",
          "[network][N05]") {
    for (auto error : {SocketError::would_block, SocketError::in_progress, SocketError::interrupted,
                       SocketError::no_buffers}) {
        SendQueue queue;
        SendCallbacks services;
        services.send = [&](auto) { return SocketResult{0, error}; };
        CHECK(queue.send(0, Bytes{1}, services) == SendResult::queued);
        CHECK(queue.front_offset() == 0);
        services.send = [&](auto) {
            CHECK(queue.size() == 1); // Retained until the socket supplies an outcome.
            return SocketResult{0, error};
        };
        const auto result = queue.drain_one(services);
        CHECK(result ==
              (error == SocketError::would_block ? DrainResult::pending : DrainResult::fatal));
        CHECK(queue.size() == (error == SocketError::would_block ? 1U : 0U));
    }
    SendQueue queue;
    SendCallbacks services;
    services.send = [](auto) { return SocketResult{0, SocketError::is_connected}; };
    CHECK(queue.send(0, Bytes{}, services) == SendResult::is_connected);
    services.send = [](auto) { return SocketResult{0, SocketError::would_block}; };
    CHECK(queue.send(0, Bytes{}, services) == SendResult::queued);
    services.send = [](auto) { return SocketResult{0, SocketError::is_connected}; };
    CHECK(queue.drain_one(services) == DrainResult::excluded_retired_descriptor);
    CHECK(queue.size() == 0);
    services.oversized_limit = 76;
    CHECK(queue.send(0, Bytes(64), services) == SendResult::oversized);
}

TEST_CASE("N05 write event stops on partial or fatal progress and tracks backlog",
          "[network][N05]") {
    SendQueue queue;
    SendCallbacks services;
    services.send = [](auto) { return SocketResult{0, SocketError::would_block}; };
    for (int i = 0; i < 51; ++i)
        CHECK(queue.send(0, {}, services) == SendResult::queued);
    std::vector<std::size_t> counts;
    services.backlog = [&](auto count) { counts.push_back(count); };
    CHECK(queue.write_event(services, {}) == DrainResult::pending);
    CHECK(counts == std::vector<std::size_t>{51});
    CHECK(queue.backlog_threshold() == 101);
    services.send = [](auto bytes) { return SocketResult{bytes.size()}; };
    CHECK(queue.write_event(services, {}) == DrainResult::complete);
    CHECK(queue.size() == 0);
    CHECK(queue.backlog_threshold() == 50);
    services.chunk_capacity = 0;
    CHECK(queue.send(0, {}, services) == SendResult::queued);
    services.send = [](auto) { return SocketResult{0, SocketError::fatal}; };
    bool closed = false;
    auto close_connection = [&] {
        closed = true;
        queue.clear();
    };
    CHECK(queue.write_event(services, close_connection) == DrainResult::fatal);
    CHECK(closed);
}

TEST_CASE("N05 datagrams narrow size before capping and retain null payload bytes",
          "[network][N05]") {
    Bytes backing(516, 0xa7), payload(600, 0x42);
    const std::span<const Byte> view{payload};
    CHECK(encode_datagram(0x12345678, 65536, &view, backing) == Bytes{0x78, 0x56, 0x34, 0x12});
    CHECK(encode_datagram(0, 65537, &view, backing) == Bytes{0, 0, 0, 0, 0x42});
    const auto retained = encode_datagram(9, 3, nullptr, backing);
    CHECK(retained == Bytes{9, 0, 0, 0, 0x42, 0xa7, 0xa7});
    CHECK(encode_datagram(9, 600, &view, backing).size() == 516);
    const auto decoded = decode_datagram(retained);
    CHECK(decoded.header == 9);
    CHECK(decoded.payload == Bytes{0x42, 0xa7, 0xa7});
    for (std::size_t size = 0; size < 4; ++size)
        CHECK_THROWS_AS(decode_datagram(std::span{retained}.first(size)), formats::DecodeError);
}

TEST_CASE("N05 send queue retains pending bytes on transport exception", "[network][N05]") {
    SendQueue queue;
    SendCallbacks services;
    services.send = [](auto) { return SocketResult{5, SocketError::none}; };
    const auto wire = encode_frame(id_tag, Bytes{1, 2, 3});
    REQUIRE(queue.send(id_tag, Bytes{1, 2, 3}, services) == SendResult::queued);
    REQUIRE(queue.front_offset() == 5);
    services.send = [](auto) -> SocketResult { throw std::runtime_error("before send"); };
    CHECK_THROWS_AS(queue.drain_one(services), std::runtime_error);
    REQUIRE(queue.size() == 1);
    REQUIRE(queue.front_offset() == 5);
    services.send = [&](std::span<const Byte> bytes) {
        CHECK(Bytes(bytes.begin(), bytes.end()) == Bytes(wire.begin() + 5, wire.end()));
        return SocketResult{bytes.size(), SocketError::none};
    };
    CHECK(queue.drain_one(services) == DrainResult::complete);
    CHECK(queue.size() == 0);
}

TEST_CASE("N05 frame callbacks preserve state across deliveries", "[network][N05]") {
    FrameReceiver receiver;
    // The state resides inside the bound callable, not an external reference.
    struct Counter {
        int calls = 0;
        void operator()(FrameView) { ++calls; }
    } counter;
    ReceiveCallbacks callbacks{counter, {}};
    const auto wire = encode_frame(0, Bytes{});
    auto two = wire;
    two.insert(two.end(), wire.begin(), wire.end());
    CHECK(receiver.feed(two, callbacks).delivered == 2);
    CHECK(counter.calls == 2);
    struct Oversized {
        int calls = 0;
        void operator()(std::uint32_t) { ++calls; }
    } oversized;
    callbacks = {{}, oversized};
    receiver.oversized_limit = 20;
    CHECK(receiver.feed(wire, callbacks).stop == ReceiveStop::oversized);
    CHECK(receiver.feed(wire, callbacks).stop == ReceiveStop::oversized);
    CHECK(oversized.calls == 2);
}
