export module openciv.formats.fixed_text;

import std;
export import openciv.formats.bytes;

export namespace openciv::formats {
/// Classic byte strings are not UTF-8. Keep the terminator and all retained tail
/// bytes independently of any presentation conversion performed by the client.
template <std::size_t N> struct FixedText {
    std::array<std::uint8_t, N> bytes{};
    /// Return the prefix before the first NUL, or the whole field if unterminated.
    std::span<const std::uint8_t> text() const noexcept {
        return {bytes.begin(), std::find(bytes.begin(), bytes.end(), std::uint8_t{0})};
    }
    /// Report whether a NUL occurs inside the fixed-width field.
    bool terminated() const noexcept { return text().size() < N; }
    bool operator==(const FixedText&) const = default;
};

/// Stored bytes have no implicit Unicode interpretation. Live text retains the
/// admitted byte spelling, without its terminator/padding. Export never truncates.
template <std::size_t N> std::string to_live(const FixedText<N>& text) {
    const auto bytes = text.text();
    return {bytes.begin(), bytes.end()};
}
/// Copy classic text bytes into a zero-filled field; reject overflow or embedded NULs.
template <std::size_t N> FixedText<N> stored_text(std::string_view text) {
    if (text.size() > N || text.find('\0') != std::string_view::npos)
        throw std::invalid_argument("Text cannot be represented in the classic field");
    FixedText<N> result;
    std::copy(text.begin(), text.end(), result.bytes.begin());
    return result;
}
} // namespace openciv::formats
