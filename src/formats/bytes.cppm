export module openciv.formats.bytes;

import std;

export namespace openciv::formats {
using Byte = std::uint8_t;
using Bytes = std::vector<Byte>;
// File offsets refer to the supplied input, never host object offsets.
class DecodeError : public std::runtime_error {
public:
    DecodeError(std::size_t offset, const std::string& detail)
        : std::runtime_error(detail + " at byte " + std::to_string(offset)), offset_(offset) {}
    std::size_t offset() const noexcept { return offset_; }

private:
    std::size_t offset_;
};
class ByteReader {
public:
    explicit ByteReader(std::span<const Byte> input) : input_(input) {}
    std::size_t position() const noexcept { return position_; }
    std::span<const Byte> remaining() const noexcept { return input_.subspan(position_); }
    std::span<const Byte> take(std::size_t count) {
        if (count > input_.size() - position_)
            throw DecodeError(position_, "Truncated record");
        const auto result = input_.subspan(position_, count);
        position_ += count;
        return result;
    }
    std::uint8_t u8() { return take(1)[0]; }
    std::int8_t i8() { return std::bit_cast<std::int8_t>(u8()); }
    std::uint16_t u16() {
        const auto b = take(2);
        return static_cast<std::uint16_t>(b[0] | (static_cast<std::uint16_t>(b[1]) << 8));
    }
    std::int16_t i16() { return std::bit_cast<std::int16_t>(u16()); }
    std::uint32_t u32() {
        const auto b = take(4);
        return std::uint32_t{b[0]} | (std::uint32_t{b[1]} << 8) | (std::uint32_t{b[2]} << 16) |
               (std::uint32_t{b[3]} << 24);
    }
    std::uint64_t u64() {
        const auto low = u32();
        return low | (std::uint64_t{u32()} << 32);
    }
    std::int32_t i32() { return std::bit_cast<std::int32_t>(u32()); }

private:
    std::span<const Byte> input_;
    std::size_t position_ = 0;
};
class ByteWriter {
public:
    Bytes bytes;
    void append(std::span<const Byte> value) {
        bytes.insert(bytes.end(), value.begin(), value.end());
    }
    void u8(std::uint8_t value) { bytes.push_back(value); }
    void i8(std::int8_t value) { u8(static_cast<std::uint8_t>(value)); }
    void u16(std::uint16_t value) {
        u8(static_cast<Byte>(value));
        u8(static_cast<Byte>(value >> 8));
    }
    void i16(std::int16_t value) { u16(static_cast<std::uint16_t>(value)); }
    void u32(std::uint32_t value) {
        u16(static_cast<std::uint16_t>(value));
        u16(static_cast<std::uint16_t>(value >> 16));
    }
    void u64(std::uint64_t value) {
        u32(static_cast<std::uint32_t>(value));
        u32(static_cast<std::uint32_t>(value >> 32));
    }
    void i32(std::int32_t value) { u32(static_cast<std::uint32_t>(value)); }
};
} // namespace openciv::formats
