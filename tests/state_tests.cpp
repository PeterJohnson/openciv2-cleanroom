#include <cstdint>

#include <catch2/catch_test_macros.hpp>

import std;
import openciv.state.entity_allocation;
import openciv.state.identities;

using namespace openciv::state;

TEST_CASE("F02 process tokens keep identity domains and wrap without skipping zero",
          "[state][F02]") {
    // F02 §§2–3: no process globals, separate counters, no everlasting uniqueness.
    static_assert(!std::is_convertible_v<UnitToken, CityToken>);
    static_assert(!std::is_convertible_v<UnitSlot, CitySlot>);
    static_assert(!std::is_convertible_v<UnitToken, UnitSlot>);
    ProcessTokens process, independent_peer;
    CHECK(process.assign_unit() == UnitToken{1});
    CHECK(process.assign_unit() == UnitToken{2});
    CHECK(process.assign_city() == CityToken{1});
    CHECK(independent_peer == ProcessTokens{});
    process.next_unit = UINT32_MAX;
    CHECK(process.assign_unit() == UnitToken{UINT32_MAX});
    CHECK(process.next_unit == 0);
    CHECK(process.assign_unit() == UnitToken{0});
    CHECK(process.assign_unit() == UnitToken{1});
    CHECK(process.next_city == 2);
    process.next_city = UINT32_MAX;
    CHECK(process.assign_city() == CityToken{UINT32_MAX});
    CHECK(process.assign_city() == CityToken{0});
    CHECK(process.next_city == 1);
    CHECK(process.next_unit == 2);
    CHECK_FALSE(occupied(UnitToken{0}));
    CHECK_FALSE(occupied(CityToken{0}));
    CHECK(occupied(UnitToken{0x80000000U}));
    CHECK(occupied(CityToken{UINT32_MAX}));
}

TEST_CASE("F02 unit reservation reuses the first hole before applying extension limits",
          "[state][F02]") {
    // F02 §10's first two worked cases: recorded military count is not a census.
    std::array<UnitToken, unit_capacity> tokens;
    tokens.fill(UnitToken{0x80000000U});
    tokens[7] = UnitToken{0};
    tokens[9] = UnitToken{0};
    const auto before = tokens;
    std::uint32_t end = 2040;
    const auto scan = std::span{tokens}.first(end);
    CHECK(reserve_unit_slot(scan, end, CivilizationSlot{1}, 2000, 0) == UnitSlot{7});
    CHECK(end == 2040);
    CHECK(tokens == before);
    // Reservation does not itself make the hole occupied.
    CHECK(reserve_unit_slot(scan, end, CivilizationSlot{1}, 2000, 0) == UnitSlot{7});
    tokens.fill(UnitToken{1});
    CHECK_FALSE(reserve_unit_slot(scan, end, CivilizationSlot{1}, 2000, 0));
    CHECK(end == 2040);
    end = unit_capacity;
    tokens[2047] = UnitToken{0};
    CHECK(reserve_unit_slot(tokens, end, CivilizationSlot{7}, 32767, 0) == UnitSlot{2047});
    CHECK(end == unit_capacity);
}

TEST_CASE("F02 unit extension honors exact thresholds and the effective human set",
          "[state][F02]") {
    // F02 §3 and §10: 1948 is accepted, 1949 rejected; nonhumans stop at 2040.
    std::array<UnitToken, unit_capacity> tokens;
    tokens.fill(UnitToken{1});
    std::uint32_t end = 2039;
    CHECK(reserve_unit_slot(std::span{tokens}.first(end), end, CivilizationSlot{1}, 1948, 0) ==
          UnitSlot{2039});
    CHECK(end == 2040);
    CHECK_FALSE(reserve_unit_slot(std::span{tokens}.first(end), end, CivilizationSlot{1}, 1948, 0));
    CHECK(end == 2040);
    end = 2039;
    CHECK_FALSE(
        reserve_unit_slot(std::span{tokens}.first(end), end, CivilizationSlot{1}, 1949, 0xff));
    CHECK(end == 2039);
    end = 2047;
    CHECK(reserve_unit_slot(std::span{tokens}.first(end), end, CivilizationSlot{7}, 1948, 0x80) ==
          UnitSlot{2047});
    CHECK(end == unit_capacity);
    CHECK_FALSE(reserve_unit_slot(tokens, end, CivilizationSlot{7}, 0, 0xff));
    CHECK(end == unit_capacity);
    end = 2040;
    CHECK_FALSE(reserve_unit_slot(std::span{tokens}.first(end), end, CivilizationSlot{6}, 0, 0x80));
    CHECK(end == 2040);
    // Signed retained counts are inputs, even when they disagree with live slots.
    CHECK(reserve_unit_slot(std::span{tokens}.first(end), end, CivilizationSlot{0}, -32768, 1) ==
          UnitSlot{2040});
    CHECK(end == 2041);
}

TEST_CASE("F02 scan reset retains old records and process token positions", "[state][F02]") {
    // F02 §2: emulate only the stated scan-bound reset, not full F05 game setup.
    std::array<UnitToken, unit_capacity> tokens;
    tokens.fill(UnitToken{99});
    ProcessTokens process{100, 200};
    std::uint32_t end = 0;
    CHECK(reserve_unit_slot(std::span{tokens}.first(end), end, CivilizationSlot{1}, 0, 2) ==
          UnitSlot{0});
    CHECK(end == 1);
    CHECK(tokens[0] == UnitToken{99});
    tokens[0] = process.assign_unit(); // Caller-owned token assignment, after reservation.
    CHECK(tokens[0] == UnitToken{100});
    end = 0; // A subsequent game's scan reset must not reconstruct ProcessTokens.
    CHECK(reserve_unit_slot(std::span{tokens}.first(end), end, CivilizationSlot{1}, 0, 2) ==
          UnitSlot{0});
    CHECK(tokens[0] == UnitToken{100});
    tokens[0] = process.assign_unit();
    CHECK(tokens[0] == UnitToken{101});
    CHECK(tokens[1] == UnitToken{99});
    CHECK(process.next_city == 200);
}

TEST_CASE("F02 unit reservation rejects unsupported views before mutating the bound",
          "[state][F02]") {
    std::array<UnitToken, 2> tokens{UnitToken{1}, UnitToken{0}};
    std::uint32_t end = 2;
    CHECK_THROWS_AS(reserve_unit_slot(tokens, end, CivilizationSlot{8}, 0, 0),
                    std::invalid_argument);
    CHECK(end == 2);
    CHECK_THROWS_AS(reserve_unit_slot(std::span{tokens}.first(1), end, CivilizationSlot{1}, 0, 2),
                    std::invalid_argument);
    CHECK(end == 2);
    end = 1;
    CHECK_THROWS_AS(reserve_unit_slot(tokens, end, CivilizationSlot{1}, 0, 2),
                    std::invalid_argument);
    CHECK(end == 1);
    end = unit_capacity + 1;
    CHECK_THROWS_AS(reserve_unit_slot(tokens, end, CivilizationSlot{1}, 0, 2),
                    std::invalid_argument);
    CHECK(end == unit_capacity + 1);
    CHECK(tokens[0] == UnitToken{1});
    CHECK(tokens[1] == UnitToken{0});
}
