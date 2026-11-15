module openciv.network.compression;

import std;

namespace openciv::network {
namespace {
using formats::Byte;
using formats::ByteReader;
using formats::Bytes;
/// Reject codec inputs outside the admitted nonnegative signed-32-bit extent.
void admit_size(std::size_t size) {
    if (size > static_cast<std::size_t>(std::numeric_limits<std::int32_t>::max()))
        throw std::length_error("Codec input exceeds admitted nonnegative i32 extent");
}
} // namespace

CodecResult encode_runs(RunCode code, std::span<const Byte> input, std::span<Byte> output,
                        std::size_t& output_length) {
    admit_size(input.size());
    if (code != RunCode::byte && code != RunCode::word)
        throw std::invalid_argument("Unknown run-length code width");
    const bool word_tokens = code == RunCode::word;
    // N02 uses distinct repeat thresholds and count fields for the two token widths.
    const std::size_t repeat_threshold = word_tokens ? 3U : 2U;
    const std::size_t maximum_count = word_tokens ? 32767U : 127U;
    const auto input_size = input.size();
    std::size_t written = 0;
    CodecStop stop = CodecStop::complete;
    const auto has_room = [&](std::size_t count) {
        if (count > output.size() - written) {
            stop = CodecStop::output_capacity;
            return false;
        }
        if (written + count >= input_size) {
            stop = CodecStop::no_gain;
            return false;
        }
        return true;
    };
    const auto emit_byte = [&](Byte value) {
        if (written == output.size()) {
            stop = CodecStop::output_capacity;
            return false;
        }
        output[written++] = value;
        if (written >= input_size) {
            stop = CodecStop::no_gain;
            return false;
        }
        return true;
    };
    // The high count bit marks literal bytes; a repeat token carries one payload byte.
    const auto emit_token = [&](std::size_t count, bool literal, std::size_t segment_begin) {
        const auto header = count + (literal ? maximum_count + 1 : 0);
        if (word_tokens) {
            // Word-token writes validate whole header/body extents before copying each part.
            if (!has_room(2))
                return false;
            output[written++] = static_cast<Byte>(header);
            output[written++] = static_cast<Byte>(header >> 8);
            const auto body = literal ? count : 1;
            if (!has_room(body))
                return false;
            if (literal)
                std::copy_n(input.data() + segment_begin, count, output.data() + written);
            else
                output[written] = input[segment_begin];
            written += body;
            return true;
        }
        // Byte-token failure retains the byte-by-byte scratch prefix required by N02.
        if (!emit_byte(static_cast<Byte>(header)))
            return false;
        if (!literal)
            return emit_byte(input[segment_begin]);
        for (std::size_t i = 0; i < count; ++i)
            if (!emit_byte(input[segment_begin + i]))
                return false;
        return true;
    };
    std::size_t segment_begin = 0, scan_end = 1;
    while (segment_begin < input_size) {
        while (scan_end < input_size && input[scan_end] == input[segment_begin])
            ++scan_end;
        if (scan_end - segment_begin > repeat_threshold) {
            while (segment_begin < scan_end) {
                const auto count = std::min(word_tokens ? maximum_count - 1 : maximum_count,
                                            scan_end - segment_begin);
                if (!emit_token(count, false, segment_begin))
                    return {stop, written};
                segment_begin += count;
            }
        }
        while (scan_end < input_size) {
            if (scan_end + 1 == input_size) {
                ++scan_end;
                break;
            }
            const auto next = input[scan_end + 1];
            if (next == input[scan_end])
                break;
            ++scan_end;
        }
        while (segment_begin < scan_end) {
            const auto count = std::min(maximum_count, scan_end - segment_begin);
            if (!emit_token(count, true, segment_begin))
                return {stop, written};
            segment_begin += count;
        }
    }
    output_length = written;
    return {CodecStop::complete, written};
}

CodecResult measure_word_runs(std::span<const Byte> input, std::uint32_t& decoded_size) {
    admit_size(input.size());
    std::size_t position = 0;
    std::uint64_t size = 0;
    while (position < input.size()) {
        if (input.size() - position < 2)
            return {CodecStop::truncated_input};
        const auto code = ByteReader{input.subspan(position)}.u16();
        if (code == 32767)
            return {CodecStop::rejected_token};
        position += 2;
        const auto count = code & 32767U;
        const auto payload = code < 32768 ? 1U : count;
        if (payload > input.size() - position)
            return {CodecStop::truncated_input};
        position += payload;
        size += count;
        if (size > std::numeric_limits<std::uint32_t>::max())
            return {CodecStop::backing_limit};
    }
    decoded_size = static_cast<std::uint32_t>(size);
    return {};
}
CodecResult decode_word_runs(std::span<const Byte> input, std::span<Byte> output,
                             std::size_t& output_length) {
    std::uint32_t measured = 0;
    const auto checked = measure_word_runs(input, measured);
    if (!checked.complete())
        return checked;
    if (measured > output.size())
        return {CodecStop::output_capacity};
    std::size_t position = 0, written = 0;
    while (position < input.size()) {
        const auto code = ByteReader{input.subspan(position)}.u16();
        position += 2;
        const auto count = code & 32767U;
        if (code >= 32768) {
            if (count)
                std::copy_n(input.data() + position, count, output.data() + written);
            position += count;
        } else {
            if (count)
                std::fill_n(output.data() + written, count, input[position]);
            ++position;
        }
        written += count;
    }
    output_length = written;
    return {CodecStop::complete, written};
}
namespace {
/// Prepare compressed bytes separately and replace the owned image only on success.
CodecResult compress(RunCode code, Bytes& image) {
    admit_size(image.size());

    Bytes encoded(image.size());
    std::size_t length = 0;
    const auto result = encode_runs(code, image, encoded, length);
    if (!result.complete())
        return {result.stop == CodecStop::output_capacity ? CodecStop::no_gain : result.stop,
                result.written};
    encoded.resize(length);
    image.swap(encoded);
    return result;
}
} // namespace
CodecResult compress_byte_image(Bytes& image) {
    return compress(RunCode::byte, image);
}
CodecResult compress_full_image(Bytes& image) {
    return compress(RunCode::word, image);
}
CodecResult expand_byte_image(Bytes& image, std::size_t limit) {
    admit_size(image.size());
    std::size_t position = 0, size = 0;
    // N02: a final unmatched byte is ignored; zero tokens still follow their
    // run/literal payload grammar. Check all consumed payload before allocation.
    while (image.size() - position > 1) {
        const auto code = image[position++];
        const auto count = static_cast<std::size_t>(code & 127);
        const auto payload = code < 128 ? 1U : count;
        if (payload > image.size() - position)
            return {CodecStop::truncated_input};
        if (count > limit - size)
            return {CodecStop::backing_limit};
        size += count;
        position += payload;
    }

    Bytes decoded(size);
    position = 0;
    std::size_t written = 0;
    while (image.size() - position > 1) {
        const auto code = image[position++];
        const auto count = code & 127;
        if (code >= 128) {
            if (count)
                std::copy_n(image.data() + position, count, decoded.data() + written);
            position += count;
        } else {
            if (count)
                std::fill_n(decoded.data() + written, count, image[position]);
            ++position;
        }
        written += count;
    }
    image.swap(decoded);
    return {CodecStop::complete, size};
}
CodecResult expand_shared_image(Bytes& image, std::uint32_t full_receipt_count, bool compressed,
                                std::size_t limit) {
    admit_size(image.size());
    if (!full_receipt_count)
        return compressed ? expand_byte_image(image, limit)
                          : CodecResult{CodecStop::complete, image.size()};
    std::uint32_t measured = 0;
    const auto checked = measure_word_runs(image, measured);
    if (!checked.complete())
        return checked;
    if (measured > limit)
        return {CodecStop::backing_limit};

    Bytes decoded(measured);
    std::size_t length = 0;
    const auto result = decode_word_runs(image, decoded, length);
    if (result.complete())
        image.swap(decoded);
    return result;
}
} // namespace openciv::network
