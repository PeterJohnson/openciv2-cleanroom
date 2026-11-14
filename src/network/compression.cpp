#include "network/compression.hpp"
#include <limits>
#include <new>

namespace openciv::network {
namespace {
using formats::Byte;
using formats::ByteReader;
using formats::Bytes;

void admit_size(std::size_t size) {
    if (size > static_cast<std::size_t>(std::numeric_limits<std::int32_t>::max()))
        throw std::length_error("Codec input exceeds admitted nonnegative i32 extent");
}
void admit_image(const ImageBuffer& image) {
    if (!image)
        throw std::invalid_argument("Codec requires an established image allocation");
    admit_size(image->size());
}
ImageBuffer storage(std::size_t size, CodecAllocation kind, const CodecAllocator& allocate) {
    if (allocate && !allocate(kind, size))
        return {};
    try {
        return Bytes(size);
    } catch (const std::bad_alloc&) {
        return {};
    }
}
bool resize_published(ImageBuffer& image, std::size_t size, CodecAllocation kind,
                      const CodecAllocator& allocate) {
    auto replacement = storage(size, kind, allocate);
    if (replacement && image)
        std::copy_n(image->begin(), std::min(image->size(), size), replacement->begin());
    image = std::move(replacement);
    return image.has_value();
}
TransformResult stopped(CodecStop stop, std::size_t written = 0,
                        std::optional<std::int32_t> original = {}) {
    return {{stop, written}, original};
}
} // namespace

CodecResult encode_runs(RunCode code, CompressionInput input, std::span<Byte> output,
                        std::size_t& output_length) {
    admit_size(input.bytes.size());
    if (code != RunCode::byte && code != RunCode::word)
        throw std::invalid_argument("Unknown run-length code width");
    const bool word = code == RunCode::word;
    const std::size_t threshold = word ? 3U : 2U, maximum = word ? 32767U : 127U;
    const auto size = input.bytes.size();
    std::size_t written = 0;
    CodecStop stop = CodecStop::complete;
    const auto room = [&](std::size_t count) {
        if (count > output.size() - written) {
            stop = CodecStop::output_capacity;
            return false;
        }
        if (written + count >= size) {
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
        if (written >= size) {
            stop = CodecStop::no_gain;
            return false;
        }
        return true;
    };
    const auto token = [&](std::size_t count, bool literal, std::size_t begin) {
        const auto header = count + (literal ? maximum + 1 : 0);
        if (word) {
            if (!room(2))
                return false;
            output[written++] = static_cast<Byte>(header);
            output[written++] = static_cast<Byte>(header >> 8);
            const auto body = literal ? count : 1;
            if (!room(body))
                return false;
            if (literal)
                std::copy_n(input.bytes.data() + begin, count, output.data() + written);
            else
                output[written] = input.bytes[begin];
            written += body;
            return true;
        }
        if (!emit_byte(static_cast<Byte>(header)))
            return false;
        if (!literal)
            return emit_byte(input.bytes[begin]);
        for (std::size_t i = 0; i < count; ++i)
            if (!emit_byte(input.bytes[begin + i]))
                return false;
        return true;
    };
    std::size_t begin = 0, scan = 1;
    while (begin < size) {
        while (scan < size && input.bytes[scan] == input.bytes[begin])
            ++scan;
        if (scan - begin > threshold) {
            while (begin < scan) {
                const auto count = std::min(maximum, scan - begin);
                if (!token(count, false, begin))
                    return {stop, written};
                begin += count;
            }
        }
        while (scan < size) {
            Byte next;
            if (scan + 1 == size) {
                if (!input.following)
                    return {CodecStop::missing_lookahead, written};
                next = *input.following;
            } else {
                next = input.bytes[scan + 1];
            }
            if (next == input.bytes[scan])
                break;
            ++scan;
        }
        while (begin < scan) {
            const auto count = std::min(maximum, scan - begin);
            if (!token(count, true, begin))
                return {stop, written};
            begin += count;
        }
    }
    output_length = written;
    return {CodecStop::complete, written};
}

CodecResult measure_word_runs(std::span<const Byte> input, std::uint32_t& decoded_size) {
    admit_size(input.size());
    std::size_t position = 0;
    std::uint32_t size = 0;
    while (position < input.size()) {
        if (input.size() - position < 2)
            return {CodecStop::truncated_input};
        const auto code = ByteReader{input.subspan(position)}.u16();
        size += code & 32767U;
        position += 2 + (code <= 32767 ? 1U : (code & 32767U));
    }
    decoded_size = size;
    return {};
}
CodecResult decode_word_runs(std::span<const Byte> input, std::span<Byte> output,
                             std::size_t& output_length) {
    std::uint32_t measured = 0;
    const auto measurement = measure_word_runs(input, measured);
    if (!measurement.complete())
        return measurement;
    if (measured > output.size())
        return {CodecStop::output_capacity};
    std::size_t position = 0, written = 0;
    while (position < input.size()) {
        // The size pass established each header, but never its payload.
        const auto code = ByteReader{input.subspan(position)}.u16();
        position += 2;
        if (code == 32767)
            return {CodecStop::rejected_token, written};
        const bool literal = code >= 32768;
        const auto count = static_cast<std::size_t>(literal ? code - 32768 : code);
        const auto read = literal ? count : 1U;
        if (read > input.size() - position)
            return {CodecStop::truncated_input, written};
        // Includes a bounded stop if the size pass's u32 accumulation wrapped.
        if (count > output.size() - written)
            return {CodecStop::output_capacity, written};
        if (literal)
            std::copy_n(input.data() + position, count, output.data() + written);
        else
            std::fill_n(output.data() + written, count, input[position]);
        position += read;
        written += count;
    }
    output_length = written;
    return {CodecStop::complete, written};
}

TransformResult compress_byte_image(ImageBuffer& image, std::optional<Byte> following,
                                    const CodecAllocator& allocate) {
    admit_image(image);
    const auto size = image->size(), capacity = size + size / 128 + 5;
    if (capacity > static_cast<std::size_t>(std::numeric_limits<std::int32_t>::max()))
        return stopped(CodecStop::backing_limit);
    auto scratch = storage(capacity, CodecAllocation::byte_encode_scratch, allocate);
    if (!scratch)
        return stopped(CodecStop::allocation_failed, 0, -1);
    std::size_t length = 0;
    const auto result = encode_runs(RunCode::byte, {*image, following}, *scratch, length);
    if (!result.complete())
        return {result,
                result.stop == CodecStop::no_gain ? std::optional<std::int32_t>{-1} : std::nullopt};
    image.reset(); // Original allocation is freed before the final allocation.
    image = storage(length, CodecAllocation::byte_encode_result, allocate);
    if (!image)
        return stopped(CodecStop::allocation_failed, length);
    std::copy_n(scratch->begin(), length, image->begin());
    return {result, static_cast<std::int32_t>(length)};
}

TransformResult expand_byte_image(ImageBuffer& image, const CodecAllocator& allocate) {
    admit_image(image);
    const auto size = image->size();
    auto scratch = storage(size, CodecAllocation::byte_decode_scratch, allocate);
    if (!scratch)
        return stopped(CodecStop::allocation_failed, 0, 0);
    std::copy(image->begin(), image->end(), scratch->begin());
    if (size > static_cast<std::size_t>(std::numeric_limits<std::int32_t>::max()) / 2)
        return stopped(CodecStop::backing_limit);
    if (!resize_published(image, size * 2, CodecAllocation::byte_decode_initial, allocate))
        return stopped(CodecStop::allocation_failed, 0, 0);
    std::size_t position = 0, written = 0;
    while (size - position > 1) {
        const auto code = (*scratch)[position++];
        const auto count = static_cast<std::size_t>(code & 127);
        if (image->size() - written < count + 1) {
            if (image->size() >
                static_cast<std::size_t>(std::numeric_limits<std::int32_t>::max()) - 1000)
                return stopped(CodecStop::backing_limit, written);
            if (!resize_published(image, image->size() + 1000, CodecAllocation::byte_decode_grow,
                                  allocate))
                return stopped(CodecStop::allocation_failed, written, 0);
        }
        const bool literal = code >= 128;
        const auto read = literal ? count : 1U;
        if (read > size - position)
            return stopped(CodecStop::truncated_input, written);
        if (literal)
            std::copy_n(scratch->data() + position, count, image->data() + written);
        else
            std::fill_n(image->data() + written, count, (*scratch)[position]);
        position += read;
        written += count;
    }
    if (!resize_published(image, written, CodecAllocation::byte_decode_final, allocate))
        return stopped(CodecStop::allocation_failed, written, 0);
    return {{CodecStop::complete, written}, static_cast<std::int32_t>(written)};
}

TransformResult compress_full_image(ImageBuffer& image, std::optional<Byte> following,
                                    const CodecAllocator& allocate) {
    admit_image(image);
    auto raw = storage(image->size(), CodecAllocation::full_raw_copy, allocate);
    if (!raw)
        return stopped(CodecStop::allocation_failed, 0, 0);
    std::copy(image->begin(), image->end(), raw->begin());
    std::size_t length = 0;
    const auto result = encode_runs(RunCode::word, {*raw, following}, *image, length);
    if (!result.complete()) {
        const bool ordinary_failure =
            result.stop == CodecStop::no_gain || result.stop == CodecStop::output_capacity;
        return {result, ordinary_failure ? std::optional<std::int32_t>{-1} : std::nullopt};
    }
    if (!resize_published(image, length, CodecAllocation::full_encoded_resize, allocate))
        return stopped(CodecStop::allocation_failed, length, static_cast<std::int32_t>(length));
    return {result, static_cast<std::int32_t>(length)};
}

TransformResult expand_shared_image(ImageBuffer& image, std::uint32_t full_receipt_count,
                                    bool compressed, const CodecAllocator& allocate,
                                    std::size_t backing_limit) {
    admit_image(image);
    if (!full_receipt_count) {
        if (compressed)
            return expand_byte_image(image, allocate);
        return {{CodecStop::complete, image->size()}, {}};
    }
    std::uint32_t measured = 0;
    const auto measurement = measure_word_runs(*image, measured);
    if (!measurement.complete())
        return {measurement, {}};
    if (measured > backing_limit)
        return stopped(CodecStop::backing_limit);
    auto expanded = storage(measured, CodecAllocation::word_decode_result, allocate);
    if (!expanded)
        return stopped(CodecStop::allocation_failed);
    std::size_t length = 0;
    const auto result = decode_word_runs(*image, *expanded, length);
    if (!result.complete())
        return {result, {}};
    image = std::move(expanded);
    return {result, {}};
}
} // namespace openciv::network
