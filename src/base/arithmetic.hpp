#pragma once

#include <bit>
#include <compare>
#include <concepts>
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

// F01 §1: expressions stay in the signed 32-bit domain, including intermediates.
// Conversions at API/codec boundaries are explicit; this is not a wire layout.
class WrappingI32 {
public:
    constexpr WrappingI32() noexcept = default;
    // Integer inputs reduce modulo 2^32, then take the signed view. No floating
    // or boolean conversions: their interpretation belongs to the caller.
    template <std::integral T>
        requires(!std::same_as<T, bool>)
    explicit constexpr WrappingI32(T value) noexcept
        : value_(signed32(static_cast<std::uint32_t>(value))) {}

    explicit constexpr operator std::int32_t() const noexcept { return value_; }
    explicit constexpr operator std::uint32_t() const noexcept {
        return static_cast<std::uint32_t>(value_);
    }
    constexpr auto operator<=>(const WrappingI32&) const noexcept = default;

    constexpr WrappingI32 operator+() const noexcept { return *this; }
    constexpr WrappingI32 operator-() const noexcept { return WrappingI32{subtract32(0, value_)}; }
    friend constexpr WrappingI32 operator+(WrappingI32 a, WrappingI32 b) noexcept {
        return WrappingI32{add32(a.value_, b.value_)};
    }
    friend constexpr WrappingI32 operator-(WrappingI32 a, WrappingI32 b) noexcept {
        return WrappingI32{subtract32(a.value_, b.value_)};
    }
    friend constexpr WrappingI32 operator*(WrappingI32 a, WrappingI32 b) noexcept {
        return WrappingI32{multiply32(a.value_, b.value_)};
    }
    friend WrappingI32 operator/(WrappingI32 a, WrappingI32 b) {
        return WrappingI32{divide32(a.value_, b.value_)};
    }
    friend WrappingI32 operator%(WrappingI32 a, WrappingI32 b) {
        return WrappingI32{remainder32(a.value_, b.value_)};
    }
    constexpr WrappingI32& operator+=(WrappingI32 other) noexcept { return *this = *this + other; }
    constexpr WrappingI32& operator-=(WrappingI32 other) noexcept { return *this = *this - other; }
    constexpr WrappingI32& operator*=(WrappingI32 other) noexcept { return *this = *this * other; }
    WrappingI32& operator/=(WrappingI32 other) { return *this = *this / other; }
    WrappingI32& operator%=(WrappingI32 other) { return *this = *this % other; }

private:
    std::int32_t value_ = 0;
};

// Preserve the distinct F01 rules; do not give them conventional shift/clamp syntax.
inline WrappingI32 clamp_ab(WrappingI32 value, WrappingI32 lo, WrappingI32 hi) {
    return WrappingI32{clamp_ab(static_cast<std::int32_t>(value), static_cast<std::int32_t>(lo),
                                static_cast<std::int32_t>(hi))};
}
inline WrappingI32 clamp_c(WrappingI32 value, WrappingI32 lo, WrappingI32 hi) {
    return WrappingI32{clamp_c(static_cast<std::int32_t>(value), static_cast<std::int32_t>(lo),
                               static_cast<std::int32_t>(hi))};
}
inline WrappingI32 shift_signed(WrappingI32 value, WrappingI32 shift) {
    return WrappingI32{
        shift_signed(static_cast<std::int32_t>(value), static_cast<std::int32_t>(shift))};
}

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
