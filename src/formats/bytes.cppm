export module openciv.formats.bytes;

import std;

export namespace openciv::formats {
using Byte = std::uint8_t;
using Bytes = std::vector<Byte>;
/// File offsets refer to the supplied input, never host object offsets.
class DecodeError : public std::runtime_error {
public:
    /// Report a decoding failure at a byte offset in the supplied input.
    DecodeError(std::size_t offset, const std::string& detail)
        : std::runtime_error(detail + " at byte " + std::to_string(offset)), offset_(offset) {}
    /// Return the failing input offset.
    std::size_t offset() const noexcept { return offset_; }

private:
    std::size_t offset_;
};
/// Read explicit little-endian fields from borrowed bytes with bounds checks.
class ByteReader {
public:
    /// Borrow an input span and start reading at offset zero.
    explicit ByteReader(std::span<const Byte> input) : input_(input) {}
    /// Return the number of input bytes consumed.
    std::size_t position() const noexcept { return position_; }
    /// Return the unread suffix without advancing the cursor.
    std::span<const Byte> remaining() const noexcept { return input_.subspan(position_); }
    /// Consume count bytes and return a borrowed view.
    /// @throws DecodeError If count exceeds the remaining input; the cursor is unchanged.
    std::span<const Byte> take(std::size_t count) {
        if (count > input_.size() - position_)
            throw DecodeError(position_, "Truncated record");
        const auto result = input_.subspan(position_, count);
        position_ += count;
        return result;
    }
    /// Read a 8-bit unsigned little-endian field; throw DecodeError on truncation.
    std::uint8_t u8() { return take(1)[0]; }
    /// Read a 8-bit signed little-endian field; throw DecodeError on truncation.
    std::int8_t i8() { return std::bit_cast<std::int8_t>(u8()); }
    /// Read a 16-bit unsigned little-endian field; throw DecodeError on truncation.
    std::uint16_t u16() {
        const auto bytes = take(2);
        return static_cast<std::uint16_t>(bytes[0] | (static_cast<std::uint16_t>(bytes[1]) << 8));
    }
    /// Read a 16-bit signed little-endian field; throw DecodeError on truncation.
    std::int16_t i16() { return std::bit_cast<std::int16_t>(u16()); }
    /// Read a 32-bit unsigned little-endian field; throw DecodeError on truncation.
    std::uint32_t u32() {
        const auto bytes = take(4);
        return std::uint32_t{bytes[0]} | (std::uint32_t{bytes[1]} << 8) |
               (std::uint32_t{bytes[2]} << 16) | (std::uint32_t{bytes[3]} << 24);
    }
    /// Read a 64-bit unsigned little-endian field; throw DecodeError on truncation.
    std::uint64_t u64() {
        const auto low = u32();
        return low | (std::uint64_t{u32()} << 32);
    }
    /// Read a 32-bit signed little-endian field; throw DecodeError on truncation.
    std::int32_t i32() { return std::bit_cast<std::int32_t>(u32()); }

private:
    std::span<const Byte> input_;
    std::size_t position_ = 0;
};
/// Accumulate explicit little-endian fields in an owned byte vector.
class ByteWriter {
public:
    Bytes bytes;
    /// Append bytes verbatim to the output.
    void append(std::span<const Byte> value) {
        bytes.insert(bytes.end(), value.begin(), value.end());
    }
    /// Append the 8-bit unsigned value in little-endian order.
    void u8(std::uint8_t value) { bytes.push_back(value); }
    /// Append the 8-bit signed value in little-endian order.
    void i8(std::int8_t value) { u8(static_cast<std::uint8_t>(value)); }
    /// Append the 16-bit unsigned value in little-endian order.
    void u16(std::uint16_t value) {
        u8(static_cast<Byte>(value));
        u8(static_cast<Byte>(value >> 8));
    }
    /// Append the 16-bit signed value in little-endian order.
    void i16(std::int16_t value) { u16(static_cast<std::uint16_t>(value)); }
    /// Append the 32-bit unsigned value in little-endian order.
    void u32(std::uint32_t value) {
        u16(static_cast<std::uint16_t>(value));
        u16(static_cast<std::uint16_t>(value >> 16));
    }
    /// Append the 64-bit unsigned value in little-endian order.
    void u64(std::uint64_t value) {
        u32(static_cast<std::uint32_t>(value));
        u32(static_cast<std::uint32_t>(value >> 32));
    }
    /// Append the 32-bit signed value in little-endian order.
    void i32(std::int32_t value) { u32(static_cast<std::uint32_t>(value)); }
};
} // namespace openciv::formats
