#include "base/arithmetic.hpp"
#include "base/random.hpp"
#include <catch2/catch_test_macros.hpp>
#include <array>
#include <bit>
#include <cfenv>
#include <limits>
#include <stdexcept>
#include <type_traits>

using namespace openciv::base;
namespace {
constexpr auto minimum = std::numeric_limits<std::int32_t>::min();
constexpr auto maximum = std::numeric_limits<std::int32_t>::max();
} // namespace

TEST_CASE("F01 arithmetic retains wrap, signed division and distinct clamp rules", "[base][F01]") {
    // F01 §1: boundary values and the documented reversed-bound examples.
    CHECK(add32(maximum, 1) == minimum);
    CHECK(subtract32(minimum, 1) == maximum);
    CHECK(multiply32(maximum, 2) == -2);
    CHECK(divide32(-7, 3) == -2);
    CHECK(remainder32(-7, 3) == -1);
    CHECK(divide32(7, -3) == -2);
    CHECK(remainder32(7, -3) == 1);
    CHECK_THROWS_AS(divide32(1, 0), std::domain_error);
    CHECK_THROWS_AS(divide32(minimum, -1), std::domain_error);
    CHECK_THROWS_AS(remainder32(minimum, -1), std::domain_error);
    CHECK(clamp_ab(-5, 10, 0) == 0);
    CHECK(clamp_c(-5, 10, 0) == 10);
    CHECK(clamp_ab(5, 10, 0) == 0);
    CHECK(clamp_c(5, 10, 0) == 0);
    CHECK(shift_signed(-7, -1) == -4);
    CHECK(shift_signed(0x40000000, 1) == minimum);
    CHECK(shift_signed(-7, -32) == -7);
    CHECK(shift_signed(-7, minimum) == -7);
    CHECK(shift_signed(-7, 32) == -7);
    CHECK(shift_signed(minimum, -31) == -1);
    CHECK(shift_signed(maximum, -31) == 0);
}

TEST_CASE("F01 primary mapping preserves the full modulo-biased code population", "[base][F01]") {
    // F01 §3 specifies these exact population counts, not uniform residues.
    std::array<int, 3> counts{};
    for (std::int32_t code = 0; code <= 32767; ++code) {
        ++counts[static_cast<std::size_t>(primary_modulo_from_code(code, 3))];
        REQUIRE(primary_modulo_from_code(code, maximum) == code);
    }
    CHECK(counts == std::array{10923, 10923, 10922});
    CHECK(primary_modulo_from_code(32767, 1) == 0);
    CHECK_THROWS_AS(primary_modulo_from_code(-1, 3), std::domain_error);
    CHECK_THROWS_AS(primary_modulo_from_code(32768, 3), std::domain_error);
    CHECK_THROWS_AS(primary_modulo_from_code(1, 0), std::domain_error);
    CHECK_THROWS_AS(primary_modulo_from_code(1, -3), std::domain_error);
}

TEST_CASE("F01 secondary range preserves endpoint rounding and accepted wrapped intervals",
          "[base][F01]") {
    // F01 §4's independently specified vectors, including signed wrap.
    constexpr std::uint32_t code = 1015568748;
    CHECK(secondary_unit_from_code(0) == 0.0);
    CHECK(secondary_unit_from_code(UINT32_MAX) == 1.0);
    CHECK(secondary_range_from_code(0, 2, code) == 0);
    CHECK(secondary_range_from_code(2, 0, code) == 0);
    CHECK(secondary_range_from_code(5, 5, code) == 5);
    CHECK(secondary_range_from_code(-4, 7, code) == -2);
    CHECK(secondary_range_from_code(-4, 7, 0) == -4);
    CHECK(secondary_range_from_code(-4, 7, UINT32_MAX) == 7);
    CHECK(secondary_range_from_code(minimum, maximum, code) == minimum);
    CHECK(secondary_range_from_code(minimum, 1, code) == 1639699275);
    CHECK(secondary_range_from_code(1, minimum, code) == 1639699275);
    // The adjustment can round away for large spans: do not clamp the result.
    CHECK(secondary_range_from_code(0, maximum, UINT32_MAX) == maximum);
    CHECK(secondary_range_from_code(minimum, 0, UINT32_MAX) == 1);
}

TEST_CASE("F01 game ticks wrap before division and map seed conversion is separate",
          "[base][F01]") {
    // F01 §5 table: clock discontinuity is not an elapsed-time correction.
    for (const auto pair :
         std::array<std::array<std::uint32_t, 3>, 7>{{{0, 0, 1},
                                                      {16, 0, 1},
                                                      {17, 1, 1},
                                                      {1000, 60, 60},
                                                      {715827882, 42949672, 23592},
                                                      {715827883, 0, 1},
                                                      {UINT32_MAX, 42949672, 23592}}}) {
        CHECK(game_ticks_from_milliseconds(pair[0]) == pair[1]);
        CHECK(map_pattern_seed_from_ticks(pair[1]) == pair[2]);
    }
    CHECK(map_pattern_seed_from_ticks(32768) == 1);
    CHECK(map_pattern_seed_from_ticks(32769) == 1);
    CHECK(map_pattern_seed_from_ticks(65535) == 32767);
}

TEST_CASE("Versioned random contexts replay and restore without presentation coupling",
          "[base][random]") {
    // F01 §§2,6; architecture randomness ownership; random-policy.md v2.
    static_assert(!std::is_convertible_v<CosmeticRandom&, GameplayRandom&>);
    static_assert(!std::is_convertible_v<PresentationRandom&, GameplayRandom&>);
    GameplayRandom game(0, "session/main"), same(0, "session/main");
    const auto entry = game.checkpoint();
    CHECK(entry.version == 2);
    CHECK(random_policy_id == "oc2-mt19937-64");
    CHECK(entry.engine == std::mt19937_64(UINT64_C(0xba8342c79a147ecb)));
    PresentationRandom presentation(0, "session/main");
    CosmeticRandom cosmetic(0, "session/main");
    REQUIRE(entry.engine != presentation.checkpoint().engine);
    REQUIRE(entry.engine != cosmetic.checkpoint().engine);
    for (int i = 0; i < 50; ++i) {
        presentation.secondary_range("P07.historian", 0, 4);
        cosmetic.primary_modulo("UI.stars", 7);
        CHECK(game.secondary_range("F01.test", -4, 7) == same.secondary_range("F01.test", -4, 7));
    }
    const auto saved = game.checkpoint();
    // Resume from partway through a state block, across multiple 312-word blocks.
    std::array<std::uint32_t, 700> expected{};
    for (auto& code : expected)
        code = game.secondary_code("F01.continuation");
    game.restore(saved);
    for (const auto code : expected)
        CHECK(game.secondary_code("F01.continuation") == code);
    CHECK(game.checkpoint().draws == saved.draws + expected.size());
    const auto unchanged = presentation.checkpoint();
    for (int i = 0; i < 100; ++i)
        cosmetic.secondary_code("UI.animation");
    CHECK(presentation.checkpoint() == unchanged);
    game.restore(entry);
    CHECK(game.primary_modulo("F01.singleton", 1) == 0);
    CHECK(game.secondary_range("F01.equal", 5, 5) == 5);
    CHECK(game.checkpoint() == entry);
    CHECK_THROWS_AS(game.primary_modulo("F01.invalid", 0), std::domain_error);
    CHECK_THROWS_AS(game.secondary_code(""), std::invalid_argument);
    CHECK(game.checkpoint() == entry);
    auto invalid = entry;
    invalid.version = 1; // Reject the superseded SplitMix64 policy.
    CHECK_THROWS_AS(game.restore(invalid), std::invalid_argument);
    CHECK(game.checkpoint() == entry);
    invalid.version = 99;
    CHECK_THROWS_AS(game.restore(invalid), std::invalid_argument);
    CHECK_THROWS_AS(game.restore(presentation.checkpoint()), std::invalid_argument);
    CHECK(game.checkpoint() == entry);
    invalid = entry;
    invalid.draws = UINT64_MAX;
    game.restore(invalid);
    CHECK_THROWS_AS(game.primary_code("F01.exhausted"), std::overflow_error);
    CHECK(game.checkpoint() == invalid);
}

TEST_CASE("Random policy v2 code projections match the standard engine reference vector",
          "[base][random]") {
    // C++ [rand.predef]: default mt19937_64's 10,000th output, not game evidence.
    // The policy's domain/context hash xor this seed yields the standard seed 5489.
    GameplayRandom game(UINT64_C(0xba8342c79a147ecb) ^ UINT64_C(5489), "session/main");
    REQUIRE(game.checkpoint().engine == std::mt19937_64{});
    for (int i = 0; i < 9999; ++i)
        game.secondary_code("F01.code");
    const auto saved = game.checkpoint();
    constexpr std::uint64_t reference = UINT64_C(9981545732273789042);
    CHECK(game.secondary_code("F01.code") == static_cast<std::uint32_t>(reference >> 32));
    CHECK(game.checkpoint().draws == 10000);
    game.restore(saved);
    CHECK(game.primary_code("F01.code") == static_cast<std::int32_t>(reference >> 49));
    CHECK(game.checkpoint().draws == 10000);
    const auto before = game.checkpoint();
    auto wrong_context = before;
    wrong_context.context = "another/context";
    CHECK_THROWS_AS(game.restore(wrong_context), std::invalid_argument);
    CHECK(game.checkpoint() == before);
    CHECK_THROWS_AS(GameplayRandom(0, ""), std::invalid_argument);
    CHECK_THROWS_AS(GameplayRandom(0, "unstable address!"), std::invalid_argument);
}

TEST_CASE("Sampling rejects a nondefault floating environment before advancing state",
          "[base][random]") {
    // F01 §1: no ambient rounding-mode dependence. WASM supports nearest only.
    struct Restore {
        int mode = std::fegetround();
        ~Restore() { std::fesetround(mode); }
    } restore;
    if (std::fesetround(FE_DOWNWARD) == 0 && std::fegetround() == FE_DOWNWARD) {
        GameplayRandom game(1, "session/main");
        const auto before = game.checkpoint();
        CHECK_THROWS_AS(secondary_unit_from_code(1), std::domain_error);
        CHECK_THROWS_AS(game.secondary_range("F01.range", 1, 2), std::domain_error);
        CHECK(game.checkpoint() == before);
    } else {
        // Emscripten's fenv stub accepts the call but keeps nearest rounding.
        CHECK(std::fegetround() == FE_TONEAREST);
        CHECK(secondary_range_from_code(-4, 7, UINT32_MAX) == 7);
    }
}
