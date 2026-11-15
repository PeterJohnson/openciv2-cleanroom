module;
#include <cfenv>

export module openciv.base.arithmetic;

import std;

export namespace openciv::base {
/// Interpret a 32-bit bit pattern as a signed value without numeric conversion.
/// F01 §1: explicit arithmetic boundaries, independent of native signed overflow.
constexpr std::int32_t signed32(std::uint32_t value) {
    return std::bit_cast<std::int32_t>(value);
}
/// Add modulo 2^32 and return the signed interpretation (F01 §1).
constexpr std::int32_t add32(std::int32_t a, std::int32_t b) {
    return signed32(static_cast<std::uint32_t>(a) + static_cast<std::uint32_t>(b));
}
/// Subtract modulo 2^32 and return the signed interpretation (F01 §1).
constexpr std::int32_t subtract32(std::int32_t a, std::int32_t b) {
    return signed32(static_cast<std::uint32_t>(a) - static_cast<std::uint32_t>(b));
}
/// Multiply modulo 2^32 and return the signed interpretation (F01 §1).
constexpr std::int32_t multiply32(std::int32_t a, std::int32_t b) {
    return signed32(static_cast<std::uint32_t>(a) * static_cast<std::uint32_t>(b));
}
/// Divide with truncation toward zero.
/// @throws std::domain_error For a zero divisor or INT32_MIN / -1.
std::int32_t divide32(std::int32_t a, std::int32_t b);
/// Return the signed remainder after division toward zero.
/// @throws std::domain_error For a zero divisor or INT32_MIN / -1.
std::int32_t remainder32(std::int32_t a, std::int32_t b);
/// Apply the lower bound before the upper bound; reversed bounds return hi.
std::int32_t clamp_ab(std::int32_t value, std::int32_t lo, std::int32_t hi);
/// Test the upper bound first, then apply the lower bound (F01 clamp C).
std::int32_t clamp_c(std::int32_t value, std::int32_t lo, std::int32_t hi);
/// Shift left for nonnegative counts, arithmetically right for negative counts.
/// The magnitude is masked to five bits; left shifts wrap modulo 2^32.
std::int32_t shift_signed(std::int32_t value, std::int32_t shift);

/// F01 §1: expressions stay in the signed 32-bit domain, including intermediates.
/// Conversions at API/codec boundaries are explicit; this is not a wire layout.
class WrappingI32 {
public:
    constexpr WrappingI32() noexcept = default;
    /// Integer inputs reduce modulo 2^32, then take the signed view. No floating
    /// or boolean conversions: their interpretation belongs to the caller.
    template <std::integral T>
        requires(!std::same_as<T, bool>)
    explicit constexpr WrappingI32(T value) noexcept
        : value_(signed32(static_cast<std::uint32_t>(value))) {}

    /// Return the signed numeric interpretation of the stored bits.
    explicit constexpr operator std::int32_t() const noexcept { return value_; }
    /// Return the unsigned interpretation of the stored bits.
    explicit constexpr operator std::uint32_t() const noexcept {
        return static_cast<std::uint32_t>(value_);
    }
    constexpr auto operator<=>(const WrappingI32&) const noexcept = default;

    /// Return this value unchanged.
    constexpr WrappingI32 operator+() const noexcept { return *this; }
    /// Negate modulo 2^32, including INT32_MIN.
    constexpr WrappingI32 operator-() const noexcept { return WrappingI32{subtract32(0, value_)}; }
    /// Add two values with a wrapping intermediate.
    friend constexpr WrappingI32 operator+(WrappingI32 a, WrappingI32 b) noexcept {
        return WrappingI32{add32(a.value_, b.value_)};
    }
    /// Subtract two values with a wrapping intermediate.
    friend constexpr WrappingI32 operator-(WrappingI32 a, WrappingI32 b) noexcept {
        return WrappingI32{subtract32(a.value_, b.value_)};
    }
    /// Multiply two values with a wrapping intermediate.
    friend constexpr WrappingI32 operator*(WrappingI32 a, WrappingI32 b) noexcept {
        return WrappingI32{multiply32(a.value_, b.value_)};
    }
    /// Divide with the same domain checks as divide32().
    friend WrappingI32 operator/(WrappingI32 a, WrappingI32 b) {
        return WrappingI32{divide32(a.value_, b.value_)};
    }
    /// Take the remainder with the same domain checks as remainder32().
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

/// Apply F01 clamp A/B while retaining the wrapping value type.
/// Preserve the distinct F01 rules; do not give them conventional shift/clamp syntax.
inline WrappingI32 clamp_ab(WrappingI32 value, WrappingI32 lo, WrappingI32 hi) {
    return WrappingI32{clamp_ab(static_cast<std::int32_t>(value), static_cast<std::int32_t>(lo),
                                static_cast<std::int32_t>(hi))};
}
/// Apply F01 clamp C while retaining the wrapping value type.
inline WrappingI32 clamp_c(WrappingI32 value, WrappingI32 lo, WrappingI32 hi) {
    return WrappingI32{clamp_c(static_cast<std::int32_t>(value), static_cast<std::int32_t>(lo),
                               static_cast<std::int32_t>(hi))};
}
/// Apply the signed-count F01 shift while retaining the wrapping value type.
inline WrappingI32 shift_signed(WrappingI32 value, WrappingI32 shift) {
    return WrappingI32{
        shift_signed(static_cast<std::int32_t>(value), static_cast<std::int32_t>(shift))};
}

/// Map a primary code in 0..32767 to code modulo a positive n.
/// F01 §§3–4: pure mappings, not generators or ideal uniform integer ranges.
std::int32_t primary_modulo_from_code(std::int32_t code, std::int32_t n);
/// Map a raw 32-bit code to the inclusive binary64 interval [0, 1].
/// @throws std::domain_error If floating arithmetic is not round-to-nearest.
double secondary_unit_from_code(std::uint32_t code);
/// Map a secondary code using F01 rounding points and wrapping range arithmetic.
/// Equal bounds return immediately; reversed bounds are exchanged.
std::int32_t secondary_range_from_code(std::int32_t lo, std::int32_t hi, std::uint32_t code);

/// Convert milliseconds to 60 Hz game ticks using wrapping 32-bit arithmetic.
/// F01 §5: the caller owns clock reads and the decision to change map state.
constexpr std::uint32_t game_ticks_from_milliseconds(std::uint32_t milliseconds) {
    return (milliseconds * std::uint32_t{6}) / 100;
}
/// Derive a nonzero 15-bit map pattern seed from a game tick value.
constexpr std::uint16_t map_pattern_seed_from_ticks(std::uint32_t ticks) {
    const auto masked = static_cast<std::uint16_t>(ticks & 32767U);
    return masked == 0 ? 1 : masked;
}
} // namespace openciv::base

module :private;
namespace openciv::base {
namespace {
/// Reject the two signed-division cases that have no representable i32 result.
void check_division(std::int32_t a, std::int32_t b) {
    if (b == 0 || (a == std::numeric_limits<std::int32_t>::min() && b == -1))
        throw std::domain_error("F01 signed division has no ordinary i32 result");
}
/// Require the binary64 representation and rounding mode used by F01 sampling.
void check_rounding() {
    static_assert(std::numeric_limits<double>::is_iec559 &&
                  std::numeric_limits<double>::digits == 53 && sizeof(double) == 8);
    if (std::fegetround() != FE_TONEAREST)
        throw std::domain_error("F01 sampling requires round-to-nearest floating arithmetic");
}
/// Force a binary64 rounding point even when the host uses wider intermediates.
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
