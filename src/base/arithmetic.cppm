module;
#include <cfenv>

export module openciv.base.arithmetic;

import std;

export namespace openciv::base {
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

module :private;
namespace openciv::base {
namespace {
void check_division(std::int32_t a, std::int32_t b) {
    if (b == 0 || (a == std::numeric_limits<std::int32_t>::min() && b == -1))
        throw std::domain_error("F01 signed division has no ordinary i32 result");
}
void check_rounding() {
    static_assert(std::numeric_limits<double>::is_iec559 &&
                  std::numeric_limits<double>::digits == 53 && sizeof(double) == 8);
    if (std::fegetround() != FE_TONEAREST)
        throw std::domain_error("F01 sampling requires round-to-nearest floating arithmetic");
}
double round53(double value) {
    // Materialize each specified binary64 rounding point (including x87 hosts).
    volatile double result = value;
    return result;
}
} // namespace

std::int32_t divide32(std::int32_t a, std::int32_t b) {
    check_division(a, b);
    return a / b;
}
std::int32_t remainder32(std::int32_t a, std::int32_t b) {
    check_division(a, b);
    return a % b;
}
std::int32_t clamp_ab(std::int32_t value, std::int32_t lo, std::int32_t hi) {
    return std::min(std::max(value, lo), hi);
}
std::int32_t clamp_c(std::int32_t value, std::int32_t lo, std::int32_t hi) {
    return value > hi ? hi : std::max(value, lo);
}
std::int32_t shift_signed(std::int32_t value, std::int32_t shift) {
    const auto bits = static_cast<std::uint32_t>(value);
    if (shift >= 0)
        return signed32(bits << (static_cast<std::uint32_t>(shift) & 31U));
    const auto count = (0U - static_cast<std::uint32_t>(shift)) & 31U;
    if (count == 0)
        return value;
    const auto extension = value < 0 ? (~std::uint32_t{0} << (32U - count)) : 0U;
    return signed32((bits >> count) | extension);
}
std::int32_t primary_modulo_from_code(std::int32_t code, std::int32_t n) {
    if (code < 0 || code > 32767 || n <= 0)
        throw std::domain_error("F01 primary mapping requires code in 0..32767 and n > 0");
    return code % n;
}
double secondary_unit_from_code(std::uint32_t code) {
    check_rounding();
    return round53(static_cast<double>(code) / 4294967295.0);
}
std::int32_t secondary_range_from_code(std::int32_t lo, std::int32_t hi, std::uint32_t code) {
    if (lo == hi)
        return lo;
    check_rounding();
    if (hi < lo)
        std::swap(lo, hi);
    const auto difference = subtract32(hi, lo);
    const double span = round53(static_cast<double>(difference) + 0x1.ffffde7210be9p-1);
    const double fraction = secondary_unit_from_code(code);
    const double product = round53(fraction * span);
    const auto integral = static_cast<std::int64_t>(product); // F01: in range; truncates to zero.
    return signed32(static_cast<std::uint32_t>(integral) + static_cast<std::uint32_t>(lo));
}
} // namespace openciv::base
