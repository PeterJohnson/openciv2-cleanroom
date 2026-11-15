#pragma once
#include "formats/bytes.hpp"
#include <algorithm>
#include <array>
#include <string>

namespace openciv::formats {
// Classic byte strings are not UTF-8. Keep the terminator and all retained tail
// bytes independently of any presentation conversion performed by the client.
template <std::size_t N> struct FixedText {
    std::array<std::uint8_t, N> bytes{};
    std::span<const std::uint8_t> text() const noexcept {
        return {bytes.begin(), std::find(bytes.begin(), bytes.end(), std::uint8_t{0})};
    }
    bool terminated() const noexcept { return text().size() < N; }
    bool operator==(const FixedText&) const = default;
};

// Stored bytes have no implicit Unicode interpretation. Live text retains the
// admitted byte spelling, without its terminator/padding. Export never truncates.
template <std::size_t N> std::string to_live(const FixedText<N>& text) {
    const auto bytes = text.text();
    return {bytes.begin(), bytes.end()};
}
template <std::size_t N> FixedText<N> stored_text(std::string_view text) {
    if (text.size() > N || text.find('\0') != std::string_view::npos)
        throw std::invalid_argument("Text cannot be represented in the classic field");
    FixedText<N> result;
    std::copy(text.begin(), text.end(), result.bytes.begin());
    return result;
}
} // namespace openciv::formats
