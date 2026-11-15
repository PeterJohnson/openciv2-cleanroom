export module openciv.network.compression;

import std;
export import openciv.formats.bytes;

export namespace openciv::network {
enum class RunCode { byte, word };
enum class CodecStop {
    complete,
    no_gain,
    output_capacity,
    truncated_input,
    rejected_token,
    backing_limit
};
struct CodecResult {
    CodecStop stop = CodecStop::complete;
    std::size_t written = 0;
    bool complete() const noexcept { return stop == CodecStop::complete; }
};
// N02 section 7 bounded production codec. Only logical payload bytes are inputs.
// Word runs exclude unsafe code 32767. Disjoint input/output; output_length is
// assigned only on success. Failed encoding may leave a partial scratch output.
CodecResult encode_runs(RunCode code, std::span<const formats::Byte> input,
                        std::span<formats::Byte> output, std::size_t& output_length);
// Validate complete tokens and bounded decoded length before writing output.
CodecResult measure_word_runs(std::span<const formats::Byte> input, std::uint32_t& decoded_size);
CodecResult decode_word_runs(std::span<const formats::Byte> input, std::span<formats::Byte> output,
                             std::size_t& output_length);
// Owned image transforms replace input only on success. No native allocator
// traces, original return integers, or out-of-span lookahead are exposed.
CodecResult compress_byte_image(formats::Bytes& image);
CodecResult compress_full_image(formats::Bytes& image);
CodecResult expand_byte_image(formats::Bytes& image, std::size_t limit = 8 * 1024 * 1024);
// N02 section 6: a nonzero FullGameState receipt count selects word decoding.
CodecResult expand_shared_image(formats::Bytes& image, std::uint32_t full_receipt_count,
                                bool compressed, std::size_t limit = 8 * 1024 * 1024);
} // namespace openciv::network
