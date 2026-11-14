// Authored specification vectors: N02 sections 6-7. These expected tokens and
// failure prefixes are independent of any historical harness or executable.
#include "network/compression.hpp"
#include "network/sections.hpp"
#include <catch2/catch_test_macros.hpp>

using namespace openciv;
using namespace openciv::network;
using formats::Byte;
using formats::Bytes;

TEST_CASE("N02 encoders retain lookahead-dependent token boundaries", "[network][N02]") {
    Bytes input(100, 7);
    input.insert(input.end(), {1, 2, 3});
    for (const auto code : {RunCode::byte, RunCode::word}) {
        Bytes output(input.size(), 0xa5);
        std::size_t length = 777;
        const auto missing = encode_runs(code, {input, {}}, output, length);
        CHECK(missing.stop == CodecStop::missing_lookahead);
        CHECK(missing.written == (code == RunCode::byte ? 2 : 3));
        CHECK(length == 777);
        CHECK(output[missing.written] == 0xa5);
        CHECK(encode_runs(code, {input, Byte{0}}, output, length).complete());
        const Bytes different =
            code == RunCode::byte ? Bytes{100, 7, 131, 1, 2, 3} : Bytes{100, 0, 7, 3, 128, 1, 2, 3};
        CHECK(Bytes(output.begin(), output.begin() + static_cast<std::ptrdiff_t>(length)) ==
              different);
        CHECK(encode_runs(code, {input, Byte{3}}, output, length).complete());
        const Bytes equal = code == RunCode::byte ? Bytes{100, 7, 130, 1, 2, 129, 3}
                                                  : Bytes{100, 0, 7, 2, 128, 1, 2, 1, 128, 3};
        CHECK(Bytes(output.begin(), output.begin() + static_cast<std::ptrdiff_t>(length)) == equal);
    }
}

TEST_CASE("N02 run thresholds differ and unsuccessful encoding keeps output length",
          "[network][N02]") {
    Bytes output(10, 0xa5);
    std::size_t length = 999;
    CHECK(encode_runs(RunCode::byte, {Bytes{8, 8}, {}}, output, length).stop == CodecStop::no_gain);
    CHECK(length == 999);
    CHECK(output[0] == 130);
    CHECK(output[1] == 8); // Byte encoder already emitted the byte reaching input length.
    CHECK(encode_runs(RunCode::byte, {Bytes{8, 8, 8}, {}}, output, length).complete());
    CHECK(length == 2);
    CHECK(output[0] == 3);
    CHECK(output[1] == 8);
    length = 999;
    CHECK(encode_runs(RunCode::word, {Bytes{8, 8, 8}, {}}, output, length).stop ==
          CodecStop::no_gain);
    CHECK(length == 999);
    CHECK(output[0] == 3);
    CHECK(output[1] == 128); // Three equal bytes remain a literal for word codes.
    CHECK(encode_runs(RunCode::word, {Bytes{8, 8, 8, 8}, {}}, output, length).complete());
    CHECK(length == 3);
    CHECK(Bytes(output.begin(), output.begin() + 3) == Bytes{4, 0, 8});
    length = 999;
    output.assign(2, 0xa5);
    const auto short_output = encode_runs(RunCode::word, {Bytes(10, 9), {}}, output, length);
    CHECK(short_output.stop == CodecStop::output_capacity);
    CHECK(short_output.written == 2);
    CHECK(output == Bytes{10, 0});
    CHECK(length == 999);
    CHECK(encode_runs(RunCode::word, {{}, {}}, {}, length).complete());
    CHECK(length == 0);
}

TEST_CASE("N02 run and literal chunks retain their exact maximum widths", "[network][N02]") {
    Bytes input(200, 0xff), literal;
    for (unsigned i = 0; i < 132; ++i)
        literal.push_back(static_cast<Byte>(i));
    input.insert(input.end(), literal.begin(), literal.end());
    Bytes output(input.size());
    std::size_t length = 0;
    CHECK(encode_runs(RunCode::byte, {input, Byte{0}}, output, length).complete());
    Bytes expected{127, 0xff, 73, 0xff, 255};
    expected.insert(expected.end(), literal.begin(), literal.begin() + 127);
    expected.push_back(133);
    expected.insert(expected.end(), literal.begin() + 127, literal.end());
    CHECK(Bytes(output.begin(), output.begin() + static_cast<std::ptrdiff_t>(length)) == expected);
    input.assign(32768, 0x42);
    output.resize(input.size());
    CHECK(encode_runs(RunCode::word, {input, {}}, output, length).complete());
    CHECK(length == 6);
    CHECK(Bytes(output.begin(), output.begin() + 6) == Bytes{255, 127, 0x42, 1, 0, 0x42});
    std::uint32_t measured = 0;
    CHECK(measure_word_runs(std::span{output}.first(length), measured).complete());
    CHECK(measured == 32768);
    Bytes destination(32768, 0xa5);
    const auto decoded = decode_word_runs(std::span{output}.first(length), destination, length);
    CHECK(decoded.stop == CodecStop::rejected_token); // Never silently repair original code 32767.
    CHECK(decoded.written == 0);
    CHECK(length == 6);
    CHECK(destination == Bytes(32768, 0xa5));
}

TEST_CASE("N02 word decoding separates the size pass from bounded payload writes",
          "[network][N02]") {
    const Bytes valid{0, 0, 0xcc, 0, 128, 3, 0, 0x41, 2, 128, 0x42, 0x43};
    Bytes output(5, 0xa5);
    std::size_t length = 777;
    CHECK(decode_word_runs(valid, output, length).complete());
    CHECK(output == Bytes{0x41, 0x41, 0x41, 0x42, 0x43});
    CHECK(length == 5);
    const Bytes truncated{3, 0, 0x41, 2, 128};
    std::uint32_t measured = 777;
    CHECK(measure_word_runs(truncated, measured).complete());
    CHECK(measured == 5); // The skipped literal does not have backing.
    length = 777;
    output.assign(5, 0xa5);
    const auto result = decode_word_runs(truncated, output, length);
    CHECK(result.stop == CodecStop::truncated_input);
    CHECK(result.written == 3);
    CHECK(output == Bytes{0x41, 0x41, 0x41, 0xa5, 0xa5});
    CHECK(length == 777);
    output.assign(4, 0xa5);
    CHECK(decode_word_runs(truncated, output, length).stop == CodecStop::output_capacity);
    CHECK(output == Bytes(4, 0xa5)); // Initial size failure precedes all writes.
    measured = 777;
    CHECK(measure_word_runs(Bytes{1}, measured).stop == CodecStop::truncated_input);
    CHECK(measured == 777);
    // A size pass may wrap without authorizing a decoder overwrite.
    Bytes wrapped;
    for (unsigned i = 0; i < 131081; ++i)
        wrapped.insert(wrapped.end(), {254, 127, 0x41});
    CHECK(measure_word_runs(wrapped, measured).complete());
    CHECK(measured == 32750);
    output.assign(measured, 0xa5);
    const auto bounded = decode_word_runs(wrapped, output, length);
    CHECK(bounded.stop == CodecStop::output_capacity);
    CHECK(bounded.written == 0);
}

TEST_CASE("N02 byte expansion consumes zero runs and ignores a final unmatched byte",
          "[network][N02]") {
    ImageBuffer image = Bytes{0, 99, 129, 88, 0xaa};
    const auto result = expand_byte_image(image);
    CHECK(result.codec.complete());
    CHECK(result.original_result == 1);
    CHECK(image == ImageBuffer{Bytes{88}});
    for (auto data : {Bytes{}, Bytes{0xcc}, Bytes{0, 8}, Bytes{128, 0xcc}}) {
        image = data;
        CHECK(expand_byte_image(image).codec.complete());
        REQUIRE(image.has_value());
        CHECK(image->empty());
    }
    image = Bytes{127, 65};
    std::vector<std::pair<CodecAllocation, std::size_t>> calls;
    CHECK(expand_byte_image(image, [&](auto kind, auto size) {
              calls.emplace_back(kind, size);
              return true;
          }).codec.complete());
    CHECK(image == ImageBuffer{Bytes(127, 65)});
    CHECK(calls == std::vector<std::pair<CodecAllocation, std::size_t>>{
                       {CodecAllocation::byte_decode_scratch, 2},
                       {CodecAllocation::byte_decode_initial, 4},
                       {CodecAllocation::byte_decode_grow, 1004},
                       {CodecAllocation::byte_decode_final, 127}});
    image = Bytes{2, 7, 131, 8};
    const auto bad = expand_byte_image(image);
    CHECK(bad.codec.stop == CodecStop::truncated_input);
    CHECK(bad.codec.written == 2);
    CHECK_FALSE(bad.original_result.has_value());
    REQUIRE(image.has_value());
    CHECK(image->size() == 8);
    CHECK((*image)[0] == 7);
    CHECK((*image)[1] == 7);
}

TEST_CASE("N02 image allocation failures preserve their distinct publication boundaries",
          "[network][N02]") {
    ImageBuffer image = Bytes(10, 7);
    const auto original = image;
    auto result = compress_byte_image(image, {}, [](auto, auto) { return false; });
    CHECK(result.original_result == -1);
    CHECK(image == original);
    result = compress_byte_image(image, {}, [&](auto kind, auto) {
        if (kind == CodecAllocation::byte_encode_result) {
            CHECK_FALSE(image.has_value());
            return false;
        }
        return true;
    });
    CHECK(result.codec.stop == CodecStop::allocation_failed);
    CHECK_FALSE(result.original_result.has_value());
    CHECK_FALSE(image.has_value());
    image = Bytes{1, 2, 3, 4};
    result = compress_byte_image(image, Byte{0});
    CHECK(result.original_result == -1);
    CHECK(image == ImageBuffer{Bytes{1, 2, 3, 4}});
    result = compress_full_image(image, Byte{0});
    CHECK(result.original_result == -1);
    CHECK(image ==
          ImageBuffer{Bytes{4, 128, 3, 4}}); // Failed full encoding never restores raw bytes.
    image = original;
    result = compress_full_image(image, {}, [](auto, auto) { return false; });
    CHECK(result.original_result == 0);
    CHECK(image == original);
    result = compress_full_image(
        image, {}, [](auto kind, auto) { return kind != CodecAllocation::full_encoded_resize; });
    CHECK(result.codec.stop == CodecStop::allocation_failed);
    CHECK(result.original_result == 3); // Successful encoder length survives a null resize.
    CHECK_FALSE(image.has_value());
    image = Bytes{3, 65, 127, 66};
    result = expand_byte_image(image, [&](auto kind, auto) {
        if (kind == CodecAllocation::byte_decode_grow) {
            REQUIRE(image.has_value());
            CHECK(Bytes(image->begin(), image->begin() + 3) == Bytes(3, 65));
            return false;
        }
        return true;
    });
    CHECK(result.original_result == 0);
    CHECK(result.codec.written == 3);
    CHECK_FALSE(image.has_value());
    for (auto stage : {CodecAllocation::byte_decode_scratch, CodecAllocation::byte_decode_initial,
                       CodecAllocation::byte_decode_final}) {
        image = Bytes{3, 65};
        result = expand_byte_image(image, [&](auto kind, auto) { return kind != stage; });
        CHECK(result.original_result == 0);
        if (stage == CodecAllocation::byte_decode_scratch)
            CHECK(image == ImageBuffer{Bytes{3, 65}});
        else
            CHECK_FALSE(image.has_value());
    }
}

TEST_CASE("N02 shared image codec selection uses the live full-state receipt count",
          "[network][N02]") {
    for (bool compressed : {false, true}) {
        ImageBuffer image = Bytes{4, 0, 65};
        CHECK(expand_shared_image(image, 1, compressed).codec.complete());
        CHECK(image == ImageBuffer{Bytes(4, 65)});
    }
    ImageBuffer image = Bytes{4, 0, 65};
    CHECK(expand_shared_image(image, 0, false).codec.complete());
    CHECK(image == ImageBuffer{Bytes{4, 0, 65}});
    CHECK(expand_shared_image(image, 0, true).codec.complete());
    CHECK(image == ImageBuffer{Bytes(4, 0)});
    for (auto data : {Bytes{4, 0, 65}, Bytes{255, 127, 65}}) {
        image = data;
        CHECK(expand_shared_image(image, 1, true, [](auto, auto) { return false; }).codec.stop ==
              CodecStop::allocation_failed);
        CHECK(image == ImageBuffer{data});
    }
    image = Bytes{255, 127, 65};
    CHECK(expand_shared_image(image, 1, false).codec.stop == CodecStop::rejected_token);
    CHECK(image == ImageBuffer{Bytes{255, 127, 65}});
    image = Bytes{4, 0, 65};
    CHECK(expand_shared_image(image, 1, true, {}, 3).codec.stop == CodecStop::backing_limit);
    CHECK(image == ImageBuffer{Bytes{4, 0, 65}});
}

TEST_CASE("N02 section image expansion and ordered application retain earlier blocks",
          "[network][N02]") {
    const std::array blocks{ImageBlock{{64, -1, 2, 0xdeadbeef, -77}, Bytes(64, 0x41)},
                            ImageBlock{{32, 999, 3, 0x12345678, -88}, Bytes(32, 0x42)}};
    const auto raw = encode_raw_image(blocks);
    for (bool full : {false, true}) {
        ImageBuffer image = raw;
        const auto encoded = full ? compress_full_image(image, {}) : compress_byte_image(image, {});
        REQUIRE(encoded.codec.complete());
        CHECK(image->size() < raw.size());
        CHECK(expand_shared_image(image, full ? 1 : 0, true).codec.complete());
        CHECK(image == ImageBuffer{raw});
        StateSections sections;
        sections[2] = {{64, 64, 2, 7, 0}, Bytes(64, 0)};
        sections[3] = {{32, 32, 3, 8, 64}, Bytes(32, 0)};
        const auto applied = apply_raw_image(sections, *image);
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
