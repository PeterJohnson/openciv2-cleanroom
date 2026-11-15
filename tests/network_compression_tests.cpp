#include <catch2/catch_test_macros.hpp>

import std;
import openciv.network.compression;
import openciv.network.sections;

// Specification-derived N02 bounded codec vectors, not executable trace tests.

using namespace openciv;
using namespace openciv::network;
using formats::Byte;
using formats::Bytes;

TEST_CASE("N02 encoding consumes only the payload including distinct tails", "[network][N02]") {
    Bytes raw(100, 7);
    raw.insert(raw.end(), {1, 2, 3});
    for (const auto code : {RunCode::byte, RunCode::word}) {
        Bytes output(raw.size());
        std::size_t length = 0;
        REQUIRE(encode_runs(code, raw, output, length).complete());
        output.resize(length);
        const Bytes expected =
            code == RunCode::byte ? Bytes{100, 7, 131, 1, 2, 3} : Bytes{100, 0, 7, 3, 128, 1, 2, 3};
        CHECK(output == expected);
        REQUIRE(expand_shared_image(output, code == RunCode::word ? 1 : 0, true).complete());
        CHECK(output == raw);
    }
}
TEST_CASE("N02 codecs preserve thresholds and unsuccessful output length", "[network][N02]") {
    Bytes output(10);
    std::size_t length = 999;
    CHECK(encode_runs(RunCode::byte, Bytes{8, 8}, output, length).stop == CodecStop::no_gain);
    CHECK(length == 999);
    CHECK(encode_runs(RunCode::byte, Bytes{8, 8, 8}, output, length).complete());
    CHECK(length == 2);
    CHECK(output[0] == 3);
    CHECK(output[1] == 8);
    length = 999;
    CHECK(encode_runs(RunCode::word, Bytes{8, 8, 8}, output, length).stop == CodecStop::no_gain);
    CHECK(length == 999);
    CHECK(encode_runs(RunCode::word, Bytes{8, 8, 8, 8}, output, length).complete());
    CHECK(length == 3);
    CHECK(Bytes(output.begin(), output.begin() + 3) == Bytes{4, 0, 8});
    length = 999;
    CHECK(encode_runs(RunCode::word, Bytes(10, 9), std::span{output}.first(2), length).stop ==
          CodecStop::output_capacity);
    CHECK(length == 999);
    CHECK(encode_runs(RunCode::word, {}, {}, length).complete());
    CHECK(length == 0);
}
TEST_CASE("N02 production word runs avoid unsafe token 32767", "[network][N02]") {
    for (const std::size_t count : {32766U, 32767U, 32768U, 65535U}) {
        Bytes image(count, 0x42);
        REQUIRE(compress_full_image(image).complete());
        if (count == 32768)
            CHECK(image == Bytes{254, 127, 0x42, 2, 0, 0x42});
        REQUIRE(expand_shared_image(image, 1, true).complete());
        CHECK(image == Bytes(count, 0x42));
    }
}
TEST_CASE("N02 round trips chunk boundaries and every possible tail byte", "[network][N02]") {
    for (const std::size_t count : {0U, 1U, 2U, 3U, 4U, 126U, 127U, 128U, 129U, 32767U}) {
        Bytes raw(500, 0xff);
        for (std::size_t i = 0; i < count; ++i)
            raw.push_back(static_cast<Byte>(i));
        for (bool full : {false, true}) {
            auto image = raw;
            const auto result = full ? compress_full_image(image) : compress_byte_image(image);
            REQUIRE(result.complete());
            REQUIRE(expand_shared_image(image, full ? 1 : 0, true).complete());
            CHECK(image == raw);
        }
    }
    for (unsigned tail = 0; tail < 256; ++tail) {
        Bytes raw(100, 7);
        raw.insert(raw.end(), {1, 2, static_cast<Byte>(tail)});
        for (bool full : {false, true}) {
            auto image = raw;
            REQUIRE((full ? compress_full_image(image) : compress_byte_image(image)).complete());
            REQUIRE(expand_shared_image(image, full ? 1 : 0, true).complete());
            CHECK(image == raw);
        }
    }
}
TEST_CASE("N02 failed image transforms preserve their input", "[network][N02]") {
    for (bool full : {false, true}) {
        Bytes image{1, 2, 3, 4};
        CHECK((full ? compress_full_image(image) : compress_byte_image(image)).stop ==
              CodecStop::no_gain);
        CHECK(image == Bytes{1, 2, 3, 4});
    }
    for (const auto& raw : {Bytes{1}, Bytes{3, 0}, Bytes{2, 128, 7}, Bytes{255, 127, 7}}) {
        auto image = raw;
        CHECK_FALSE(expand_shared_image(image, 1, true).complete());
        CHECK(image == raw);
    }
    Bytes image{3, 0, 7};
    CHECK(expand_shared_image(image, 1, true, 2).stop == CodecStop::backing_limit);
    CHECK(image == Bytes{3, 0, 7});
    image = {127, 7};
    CHECK(expand_byte_image(image, 126).stop == CodecStop::backing_limit);
    CHECK(image == Bytes{127, 7});
    image = {2, 7, 131, 8};
    CHECK(expand_byte_image(image).stop == CodecStop::truncated_input);
    CHECK(image == Bytes{2, 7, 131, 8});
}
TEST_CASE("N02 word validation rejects truncated and overflowing decoded lengths",
          "[network][N02]") {
    std::uint32_t measured = 777;
    CHECK(measure_word_runs(Bytes{3, 0, 65, 2, 128}, measured).stop == CodecStop::truncated_input);
    CHECK(measured == 777);
    Bytes output(5, 0xa5);
    std::size_t length = 999;
    CHECK_FALSE(decode_word_runs(Bytes{3, 0, 65, 2, 128}, output, length).complete());
    CHECK(output == Bytes(5, 0xa5));
    CHECK(length == 999);
    Bytes wrapped;
    for (unsigned i = 0; i < 131081; ++i)
        wrapped.insert(wrapped.end(), {254, 127, 65});
    CHECK(measure_word_runs(wrapped, measured).stop == CodecStop::backing_limit);
    CHECK(measured == 777);
}
TEST_CASE("N02 byte decode retains zero tokens and unmatched tail semantics", "[network][N02]") {
    Bytes image{0, 99, 129, 88, 0xaa};
    REQUIRE(expand_byte_image(image).complete());
    CHECK(image == Bytes{88});
    for (auto raw : {Bytes{}, Bytes{0xcc}, Bytes{0, 8}, Bytes{128, 0xcc}}) {
        REQUIRE(expand_byte_image(raw).complete());
        CHECK(raw.empty());
    }
    for (bool compressed : {false, true}) {
        image = {4, 0, 65};
        REQUIRE(expand_shared_image(image, 1, compressed).complete());
        CHECK(image == Bytes(4, 65));
    }
    image = {4, 0, 65};
    REQUIRE(expand_shared_image(image, 0, false).complete());
    CHECK(image == Bytes{4, 0, 65});
    REQUIRE(expand_shared_image(image, 0, true).complete());
    CHECK(image == Bytes(4, 0));
}

TEST_CASE("N02 section image expansion and ordered application retain earlier blocks",
          "[network][N02]") {
    const std::array blocks{ImageBlock{{64, -1, 2, 0xdeadbeef, -77}, Bytes(64, 0x41)},
                            ImageBlock{{32, 999, 3, 0x12345678, -88}, Bytes(32, 0x42)}};
    const auto raw = encode_raw_image(blocks);
    for (bool full : {false, true}) {
        Bytes image = raw;
        const auto encoded = full ? compress_full_image(image) : compress_byte_image(image);
        REQUIRE(encoded.complete());
        CHECK(image.size() < raw.size());
        CHECK(expand_shared_image(image, full ? 1 : 0, true).complete());
        CHECK(image == raw);
        StateSections sections;
        sections[2] = {{64, 64, 2, 7, 0}, Bytes(64, 0)};
        sections[3] = {{32, 32, 3, 8, 64}, Bytes(32, 0)};
        const auto applied = apply_raw_image(sections, image);
        CHECK(applied.stop == ImageApplyStop::complete);
        CHECK(applied.applied == 2);
        CHECK(applied.consumed == raw.size());
        CHECK(sections[2].backing == blocks[0].data);
        CHECK(sections[3].backing == blocks[1].data);
        CHECK(sections[2].descriptor == SectionDescriptor{64, 64, 2, 0x14141410, 0});
        CHECK(sections[3].descriptor == SectionDescriptor{32, 32, 3, 0x12121210, 64});
    }
    for (auto reason : {ImageApplyStop::truncated, ImageApplyStop::invalid_section,
                        ImageApplyStop::invalid_size, ImageApplyStop::destination_extent}) {
        StateSections sections;
        sections[2] = {{64, 64, 2, 7, 0}, Bytes(64, 0)};
        sections[3] = {{32, 32, 3, 8, 64}, Bytes(32, 0)};
        auto bad = raw;
        constexpr std::size_t second = 20 + 64;
        if (reason == ImageApplyStop::truncated)
            bad.resize(raw.size() - 1);
        if (reason == ImageApplyStop::invalid_section)
            bad[second + 8] = 24;
        if (reason == ImageApplyStop::invalid_size)
            bad[second + 3] = 0x80;
        if (reason == ImageApplyStop::destination_extent)
            sections[3].backing.resize(31);
        const auto applied = apply_raw_image(sections, bad);
        CHECK(applied.stop == reason);
        CHECK(applied.applied == 1);
        CHECK(sections[2].backing == blocks[0].data);
        CHECK(sections[2].descriptor.checksum == 0x14141410);
        CHECK(sections[3].descriptor.checksum == 8);
        CHECK(std::all_of(sections[3].backing.begin(), sections[3].backing.end(),
                          [](Byte b) { return b == 0; }));
    }
}
