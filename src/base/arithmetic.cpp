#include "base/arithmetic.hpp"
#include <algorithm>
#include <cfenv>
#include <limits>
#include <stdexcept>

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
