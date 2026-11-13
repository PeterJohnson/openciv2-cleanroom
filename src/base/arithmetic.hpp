#pragma once

#include <bit>
#include <cstdint>

namespace openciv::base {
// F01 §1: explicit arithmetic boundaries, independent of native signed overflow.
constexpr std::int32_t signed32(std::uint32_t value) {
    return std::bit_cast<std::int32_t>(value);
}
constexpr std::int32_t add32(std::int32_t a, std::int32_t b) {
    return signed32(static_cast<std::uint32_t>(a) + static_cast<std::uint32_t>(b));
}
constexpr std::int32_t subtract32(std::int32_t a, std::int32_t b) {
    return signed32(static_cast<std::uint32_t>(a) - static_cast<std::uint32_t>(b));
}
constexpr std::int32_t multiply32(std::int32_t a, std::int32_t b) {
    return signed32(static_cast<std::uint32_t>(a) * static_cast<std::uint32_t>(b));
}
std::int32_t divide32(std::int32_t a, std::int32_t b);
std::int32_t remainder32(std::int32_t a, std::int32_t b);
std::int32_t clamp_ab(std::int32_t value, std::int32_t lo, std::int32_t hi);
std::int32_t clamp_c(std::int32_t value, std::int32_t lo, std::int32_t hi);
std::int32_t shift_signed(std::int32_t value, std::int32_t shift);

// F01 §§3–4: pure mappings, not generators or ideal uniform integer ranges.
std::int32_t primary_modulo_from_code(std::int32_t code, std::int32_t n);
double secondary_unit_from_code(std::uint32_t code);
std::int32_t secondary_range_from_code(std::int32_t lo, std::int32_t hi, std::uint32_t code);

// F01 §5: the caller owns clock reads and the decision to change map state.
constexpr std::uint32_t game_ticks_from_milliseconds(std::uint32_t milliseconds) {
    return (milliseconds * std::uint32_t{6}) / 100;
}
constexpr std::uint16_t map_pattern_seed_from_ticks(std::uint32_t ticks) {
    const auto masked = static_cast<std::uint16_t>(ticks & 32767U);
    return masked == 0 ? 1 : masked;
}
} // namespace openciv::base
