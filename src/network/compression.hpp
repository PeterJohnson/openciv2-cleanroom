#pragma once

#include "formats/bytes.hpp"
#include <functional>
#include <optional>

namespace openciv::network {
enum class RunCode { byte, word };
enum class CodecStop {
    complete,
    no_gain,
    output_capacity,
    missing_lookahead,
    truncated_input,
    rejected_token,
    allocation_failed,
    backing_limit
};
struct CodecResult {
    CodecStop stop = CodecStop::complete;
    std::size_t written = 0;
    bool complete() const noexcept { return stop == CodecStop::complete; }
};
// N02 section 7: the optional byte supplies the original encoder's read one
// past the logical extent. It is consulted only if token selection reaches it.
struct CompressionInput {
    std::span<const formats::Byte> bytes;
    std::optional<formats::Byte> following;
};
// Input/output do not overlap. On failure the written prefix remains, but the
// caller's output_length is unchanged. Byte encoding writes/checks each byte;
// word encoding checks header and body separately before publishing each.
CodecResult encode_runs(RunCode code, CompressionInput input, std::span<formats::Byte> output,
                        std::size_t& output_length);
// The size pass deliberately does not establish payload backing. A skipped
// payload may go past input end; an actually read two-byte code must fit.
// Size accumulation wraps to u32, as required by N02 section 7.
CodecResult measure_word_runs(std::span<const formats::Byte> input, std::uint32_t& decoded_size);
// The decoder repeats the size pass before writing and keeps partial writes on
// later read failure. Strict safe profile rejects ambiguous/unsafe code 32767.
CodecResult decode_word_runs(std::span<const formats::Byte> input, std::span<formats::Byte> output,
                             std::size_t& output_length);

using ImageBuffer = std::optional<formats::Bytes>; // Null differs from a zero-byte allocation.
enum class CodecAllocation {
    byte_encode_scratch,
    byte_encode_result,
    byte_decode_scratch,
    byte_decode_initial,
    byte_decode_grow,
    byte_decode_final,
    full_raw_copy,
    full_encoded_resize,
    word_decode_result
};
// Fault/observation service only: it must not mutate or reenter the operation.
// False simulates allocation failure. Published null/replacement and earlier
// byte/checksum changes remain visible; these are not transactional operations.
using CodecAllocator = std::function<bool(CodecAllocation, std::size_t)>;
struct TransformResult {
    CodecResult codec;
    // Present for transforms with a specified integer return. Absent at bounded
    // stops before unsafe/unknown original operations, and for the shared word
    // decode/raw selection (whose enclosing application has no integer result).
    std::optional<std::int32_t> original_result;
};
TransformResult compress_byte_image(ImageBuffer& image, std::optional<formats::Byte> following,
                                    const CodecAllocator& allocate = {});
TransformResult expand_byte_image(ImageBuffer& image, const CodecAllocator& allocate = {});
// N02 section 6 full-image compression tail. The raw producer already updated
// checksums. Failed encoding leaves its partial encoded bytes in the original
// image allocation. Raw-copy failure preserves that allocation and returns zero.
TransformResult compress_full_image(ImageBuffer& image, std::optional<formats::Byte> following,
                                    const CodecAllocator& allocate = {});
// N02 section 6 shared image codec selection, not world/option application.
// A nonzero global FullGameState receipt count selects word decode regardless
// of the current kind/compression flag. Failed word allocation/decode retains
// the old image. The configurable bound is explicit safe-profile admission.
TransformResult expand_shared_image(ImageBuffer& image, std::uint32_t full_receipt_count,
                                    bool compressed, const CodecAllocator& allocate = {},
                                    std::size_t backing_limit = 8 * 1024 * 1024);
} // namespace openciv::network
