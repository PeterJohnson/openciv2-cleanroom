#include <cstdint>
#include <catch2/catch_test_macros.hpp>

import std;
import openciv.base.arithmetic;

using namespace openciv::base;
namespace {
constexpr auto minimum = std::numeric_limits<std::int32_t>::min();
constexpr auto maximum = std::numeric_limits<std::int32_t>::max();

template <typename L, typename R>
concept HasAddition = requires(L a, R b) { a + b; };
template <typename L, typename R>
concept HasComparison = requires(L a, R b) {
    a == b;
    a < b;
};
template <typename L, typename R>
concept HasCompoundAddition = requires(L a, R b) { a += b; };
template <typename T>
concept HasShiftOperators = requires(T a) {
    a << a;
    a >> a;
};

template <typename T> constexpr bool explicit_boundary() {
    return std::is_constructible_v<WrappingI32, T> && !std::is_convertible_v<T, WrappingI32> &&
           !std::is_convertible_v<WrappingI32, T> && !HasAddition<WrappingI32, T> &&
           !HasAddition<T, WrappingI32> && !HasComparison<WrappingI32, T> &&
           !HasComparison<T, WrappingI32> && !HasCompoundAddition<WrappingI32, T> &&
           !HasCompoundAddition<T, WrappingI32>;
}
static_assert(explicit_boundary<std::int32_t>());
static_assert(explicit_boundary<std::uint32_t>());
static_assert(explicit_boundary<std::int64_t>());
static_assert(explicit_boundary<std::uint64_t>());
static_assert(!std::is_constructible_v<WrappingI32, double>);
static_assert(!std::is_constructible_v<WrappingI32, bool>);
static_assert(!std::is_convertible_v<WrappingI32, bool>);
static_assert(!HasShiftOperators<WrappingI32>);
static_assert(std::is_trivially_copyable_v<WrappingI32>);
static_assert(std::same_as<decltype(WrappingI32{1} + WrappingI32{2}), WrappingI32>);
static_assert(WrappingI32{maximum} + WrappingI32{1} == WrappingI32{minimum});
static_assert(WrappingI32{minimum} - WrappingI32{1} == WrappingI32{maximum});
static_assert(WrappingI32{maximum} * WrappingI32{2} == WrappingI32{-2});
static_assert(-WrappingI32{minimum} == WrappingI32{minimum});
static_assert(WrappingI32{-1} < WrappingI32{0});
} // namespace

TEST_CASE("WrappingI32 retains intermediate wrap and explicit signed or bit views",
          "[base][F01][wrapping]") {
    // F01 §1: wrap before subsequent comparisons/division, not just at output.
    CHECK(static_cast<std::int32_t>(WrappingI32{}) == 0);
    CHECK(static_cast<std::int32_t>(WrappingI32{UINT32_MAX}) == -1);
    CHECK(static_cast<std::uint32_t>(WrappingI32{-1}) == UINT32_MAX);
    CHECK(WrappingI32{UINT64_C(0x100000001)} == WrappingI32{1});
    CHECK(WrappingI32{INT64_C(-4294967297)} == WrappingI32{-1});
    const auto wrapped = (WrappingI32{maximum} * WrappingI32{2} + WrappingI32{1}) / WrappingI32{3};
    CHECK(wrapped == WrappingI32{0});
    CHECK((WrappingI32{maximum} + WrappingI32{1}) / WrappingI32{2} == WrappingI32{-1073741824});
    CHECK(WrappingI32{UINT32_MAX} < WrappingI32{0});
    CHECK(WrappingI32{minimum} <= WrappingI32{minimum});
    CHECK(WrappingI32{maximum} > WrappingI32{minimum});
    CHECK(WrappingI32{maximum} >= WrappingI32{maximum});
    CHECK(WrappingI32{-1} != WrappingI32{1});
    CHECK(+WrappingI32{-7} == WrappingI32{-7});
    CHECK(-WrappingI32{-7} == WrappingI32{7});

    auto value = WrappingI32{maximum};
    CHECK(&(value += WrappingI32{1}) == &value);
    CHECK(value == WrappingI32{minimum});
    value -= WrappingI32{1};
    CHECK(value == WrappingI32{maximum});
    value *= WrappingI32{2};
    CHECK(value == WrappingI32{-2});
}

TEST_CASE("WrappingI32 division faults preserve the compound assignment destination",
          "[base][F01][wrapping]") {
    // F01 §1: truncation toward zero; exceptional inputs have no ordinary result.
    CHECK(WrappingI32{-7} / WrappingI32{3} == WrappingI32{-2});
    CHECK(WrappingI32{-7} % WrappingI32{3} == WrappingI32{-1});
    CHECK(WrappingI32{7} / WrappingI32{-3} == WrappingI32{-2});
    CHECK(WrappingI32{7} % WrappingI32{-3} == WrappingI32{1});
    CHECK_THROWS_AS(WrappingI32{1} / WrappingI32{0}, std::domain_error);
    CHECK_THROWS_AS(WrappingI32{1} % WrappingI32{0}, std::domain_error);
    CHECK_THROWS_AS(WrappingI32{minimum} / WrappingI32{-1}, std::domain_error);
    CHECK_THROWS_AS(WrappingI32{minimum} % WrappingI32{-1}, std::domain_error);
    auto value = WrappingI32{minimum};
    CHECK_THROWS_AS(value /= WrappingI32{-1}, std::domain_error);
    CHECK(value == WrappingI32{minimum});
    CHECK_THROWS_AS(value %= WrappingI32{-1}, std::domain_error);
    CHECK(value == WrappingI32{minimum});
    CHECK_THROWS_AS(value /= WrappingI32{0}, std::domain_error);
    CHECK(value == WrappingI32{minimum});
    CHECK_THROWS_AS(value %= WrappingI32{0}, std::domain_error);
    CHECK(value == WrappingI32{minimum});
    value = WrappingI32{-7};
    CHECK(&(value /= WrappingI32{3}) == &value);
    CHECK(value == WrappingI32{-2});
    CHECK(&(value %= WrappingI32{2}) == &value);
    CHECK(value == WrappingI32{0});
}

TEST_CASE("WrappingI32 named operations retain F01 reversed clamps and signed shift counts",
          "[base][F01][wrapping]") {
    // F01 §1's reversed-bound examples and count reduction, including INT_MIN.
    CHECK(clamp_ab(WrappingI32{-5}, WrappingI32{10}, WrappingI32{0}) == WrappingI32{0});
    CHECK(clamp_c(WrappingI32{-5}, WrappingI32{10}, WrappingI32{0}) == WrappingI32{10});
    CHECK(clamp_ab(WrappingI32{5}, WrappingI32{10}, WrappingI32{0}) == WrappingI32{0});
    CHECK(clamp_c(WrappingI32{5}, WrappingI32{10}, WrappingI32{0}) == WrappingI32{0});
    CHECK(shift_signed(WrappingI32{-7}, WrappingI32{-1}) == WrappingI32{-4});
    CHECK(shift_signed(WrappingI32{0x40000000}, WrappingI32{1}) == WrappingI32{minimum});
    CHECK(shift_signed(WrappingI32{-7}, WrappingI32{-32}) == WrappingI32{-7});
    CHECK(shift_signed(WrappingI32{-7}, WrappingI32{32}) == WrappingI32{-7});
    CHECK(shift_signed(WrappingI32{-7}, WrappingI32{minimum}) == WrappingI32{-7});
    CHECK(shift_signed(WrappingI32{minimum}, WrappingI32{-31}) == WrappingI32{-1});
}
