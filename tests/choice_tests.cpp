// F01 §§2–4,6 and X02 §§2,5–6; specification vectors and policy regression
// checks. These authored choices are not original-game observations.
#include "base/random.hpp"
#include <catch2/catch_test_macros.hpp>
#include <array>
#include <cfenv>
#include <functional>

using namespace openciv::base;
namespace {
ChoiceKey key(std::int32_t attempt = 0, std::string site = "test.candidate",
              std::string call = "test.operation/one") {
    return {std::move(site), std::move(call), attempt};
}
void failure(ChoiceFailure expected, const std::function<void()>& operation,
             std::optional<ChoiceKey> expected_key = {}) {
    try {
        operation();
        FAIL("Expected explicit random-choice rejection");
    } catch (const ChoiceError& error) {
        CHECK(error.failure() == expected);
        CHECK(error.key() == expected_key);
    }
}
ControlledChoices inputs() {
    return {{make_choice(key(0), PrimaryModulo{3}, std::int32_t{32767}),
             make_choice(key(1), SecondaryRange{-4, 7}, UINT32_MAX),
             make_choice(key(2), SecondaryUnit{}, std::uint32_t{0}),
             make_choice(key(3), PrimaryCode{}, std::int32_t{32767})}};
}
template <RandomDomain Domain> void domain_laws() {
    RandomContext<Domain> random(inputs(), "session");
    const auto entry = random.checkpoint();
    // Keyed input deliberately consumed out of array order. No PRNG is sampled.
    CHECK(random.secondary_range(key(1), -4, 7) == 7);
    CHECK(random.primary_modulo(key(), 3) == 1);
    CHECK(random.primary_code(key(3)) == 32767);
    CHECK(random.secondary_unit(key(2)) == 0.0);
    CHECK_NOTHROW(random.require_choices_consumed());
    const auto after = random.checkpoint();
    CHECK(after.domain == Domain);
    CHECK(after.context == "session");
    CHECK(after.draws == 0);
    CHECK(after.engine == entry.engine);
    REQUIRE(after.trace->events.size() == 4);
    CHECK(after.trace->events[0] == inputs().records[1]);
    CHECK(after.trace->events[1] == inputs().records[0]);
    random.restore(entry);
    CHECK(random.checkpoint() == entry);
    CHECK(random.primary_modulo(key(), 3) == 1);
}
} // namespace

TEST_CASE("Controlled choices retain F01 laws and keyed order in every domain",
          "[base][random][choices]") {
    domain_laws<RandomDomain::gameplay>();
    domain_laws<RandomDomain::presentation_content>();
    domain_laws<RandomDomain::cosmetic>();
    // Numeric boundaries remain the existing F01 pure mapping, not a clamp or
    // uniform-residue substitute. Signed and unsigned code types are distinct.
    CHECK(std::get<std::int32_t>(
              make_choice(key(), PrimaryModulo{40000}, std::int32_t{32767}).result) == 32767);
    CHECK(std::get<std::int32_t>(
              make_choice(key(), SecondaryRange{INT32_MIN, 1}, std::uint32_t{1015568748}).result) ==
          1639699275);
    CHECK(std::get<std::int32_t>(
              make_choice(key(), SecondaryRange{1, INT32_MIN}, std::uint32_t{1015568748}).result) ==
          1639699275);
    CHECK(std::get<double>(make_choice(key(), SecondaryUnit{}, UINT32_MAX).result) == 1.0);
}

TEST_CASE("Controlled choices reject mismatches without fallback or prefix mutation",
          "[base][random][choices]") {
    GameplayRandom random(inputs(), "session");
    const auto entry = random.checkpoint();
    failure(ChoiceFailure::missing, [&] { random.primary_code(key(99)); }, key(99));
    failure(
        ChoiceFailure::missing,
        [&] { random.primary_code(key(0, "test.candidate", "another/call")); },
        key(0, "test.candidate", "another/call"));
    failure(ChoiceFailure::law_mismatch, [&] { random.primary_code(key()); }, key());
    failure(ChoiceFailure::law_mismatch, [&] { random.primary_modulo(key(), 4); }, key());
    failure(ChoiceFailure::law_mismatch, [&] { random.secondary_range(key(1), 7, -4); }, key(1));
    failure(ChoiceFailure::semantic_key_required,
            [&] { random.primary_modulo("test.unkeyed", 3); });
    failure(ChoiceFailure::semantic_key_required, [&] { random.secondary_code("test.unkeyed"); });
    failure(ChoiceFailure::unused, [&] { random.require_choices_consumed(); }, key());
    CHECK(random.checkpoint() == entry);
    CHECK(random.primary_modulo(key(), 3) == 1);
    const auto prefix = random.checkpoint();
    failure(ChoiceFailure::already_consumed, [&] { random.primary_modulo(key(), 3); }, key());
    failure(ChoiceFailure::unused, [&] { random.require_choices_consumed(); }, key(1));
    CHECK(random.checkpoint() == prefix);
}

TEST_CASE("Choice installation validates typed codes results and unique semantic keys",
          "[base][random][choices]") {
    auto supplied = inputs();
    supplied.records.push_back(supplied.records.front());
    failure(ChoiceFailure::duplicate_key, [&] { GameplayRandom(supplied, "session"); }, key());
    for (const auto code : std::array<std::int32_t, 2>{-1, 32768})
        failure(
            ChoiceFailure::invalid_record, [&] { make_choice(key(), PrimaryCode{}, code); }, key());
    failure(
        ChoiceFailure::invalid_record, [&] { make_choice(key(), PrimaryCode{}, std::uint32_t{1}); },
        key());
    failure(
        ChoiceFailure::invalid_record,
        [&] { make_choice(key(), SecondaryUnit{}, std::int32_t{1}); }, key());
    failure(
        ChoiceFailure::invalid_record,
        [&] { make_choice(key(), PrimaryModulo{0}, std::int32_t{1}); }, key());
    for (auto bad_key : {key(-1), key(0, ""), key(0, "site", "")})
        failure(
            ChoiceFailure::invalid_record,
            [&] { make_choice(bad_key, PrimaryCode{}, std::int32_t{0}); }, bad_key);
    supplied = inputs();
    supplied.records[0].result = std::int32_t{2};
    failure(ChoiceFailure::invalid_record, [&] { GameplayRandom(supplied, "session"); }, key());
    supplied = inputs();
    supplied.records[2].result = -0.0;
    failure(ChoiceFailure::invalid_record, [&] { GameplayRandom(supplied, "session"); }, key(2));
    supplied.records[2].result = std::int32_t{0};
    failure(ChoiceFailure::invalid_record, [&] { GameplayRandom(supplied, "session"); }, key(2));
}

TEST_CASE("Constant decisions do not consume a tape record or need a key",
          "[base][random][choices]") {
    GameplayRandom random(ControlledChoices{}, "session");
    const auto entry = random.checkpoint();
    CHECK(random.primary_modulo(ChoiceKey{}, 1) == 0);
    CHECK(random.secondary_range(ChoiceKey{}, INT32_MIN, INT32_MIN) == INT32_MIN);
    CHECK(random.primary_modulo("", 1) == 0);
    CHECK(random.secondary_range("", 7, 7) == 7);
    CHECK_NOTHROW(random.require_choices_consumed());
    CHECK(random.checkpoint() == entry);
    auto redundant = make_choice(key(), PrimaryModulo{1}, std::int32_t{9});
    GameplayRandom extra(ControlledChoices{{redundant}}, "session");
    CHECK(extra.primary_modulo(key(), 1) == 0);
    failure(ChoiceFailure::unused, [&] { extra.require_choices_consumed(); }, key());
}

TEST_CASE("Semantic recording preserves policy v2 and restores its bounded trace",
          "[base][random][choices]") {
    GameplayRandom observed(77, "session"), baseline(77, "session");
    observed.record_choices(705);
    for (std::int32_t i = 0; i < 700; ++i) {
        CHECK(observed.primary_modulo(key(i), 3) == baseline.primary_modulo("test.code", 3));
    }
    auto prefix = observed.checkpoint();
    const auto first = observed.secondary_range(key(700), -4, 7);
    CHECK(first == baseline.secondary_range("test.code", -4, 7));
    const auto second = observed.secondary_unit(key(701));
    CHECK(second == baseline.secondary_unit("test.code"));
    const auto end = observed.checkpoint();
    CHECK(end.engine == baseline.checkpoint().engine);
    CHECK(end.draws == baseline.checkpoint().draws);
    REQUIRE(end.trace->events.size() == 702);
    observed.restore(prefix);
    CHECK(observed.secondary_range(key(700), -4, 7) == first);
    CHECK(observed.secondary_unit(key(701)) == second);
    CHECK(observed.checkpoint() == end);
    failure(ChoiceFailure::semantic_key_required, [&] { observed.primary_code("test.raw"); });
    failure(ChoiceFailure::already_consumed, [&] { observed.secondary_unit(key(701)); }, key(701));
    CHECK(observed.checkpoint() == end);
    GameplayRandom bounded(77, "session");
    bounded.record_choices(1);
    bounded.primary_code(key());
    const auto full = bounded.checkpoint();
    failure(ChoiceFailure::trace_limit, [&] { bounded.primary_code(key(1)); }, key(1));
    CHECK(bounded.checkpoint() == full);
    CHECK_THROWS_AS(bounded.record_choices(2), std::logic_error);
    CHECK(bounded.checkpoint() == full);
    // Recording is opt-in: ordinary semantic calls retain no unbounded history.
    CHECK_FALSE(baseline.checkpoint().trace.has_value());
    CHECK_NOTHROW(baseline.require_choices_consumed());
}

TEST_CASE("Controlled checkpoint restore retains consumption and validates before replacement",
          "[base][random][choices]") {
    GameplayRandom random(inputs(), "session");
    random.primary_modulo(key(), 3);
    const auto prefix = random.checkpoint();
    random.secondary_range(key(1), -4, 7);
    const auto after = random.checkpoint();
    // Owned checkpoint can install controlled mode into a fresh matching context.
    GameplayRandom restored(999, "session");
    restored.restore(prefix);
    CHECK(restored.secondary_range(key(1), -4, 7) == 7);
    CHECK(restored.checkpoint() == after);
    failure(ChoiceFailure::already_consumed, [&] { restored.primary_modulo(key(), 3); }, key());
    auto invalid = prefix;
    invalid.trace.reset();
    failure(ChoiceFailure::invalid_checkpoint, [&] { restored.restore(invalid); });
    invalid = prefix;
    invalid.draws = 1;
    failure(ChoiceFailure::invalid_checkpoint, [&] { restored.restore(invalid); });
    invalid = prefix;
    invalid.trace->limit = 0;
    failure(ChoiceFailure::invalid_checkpoint, [&] { restored.restore(invalid); });
    invalid = prefix;
    invalid.trace->events[0] = make_choice(key(), PrimaryModulo{3}, std::int32_t{8});
    failure(ChoiceFailure::invalid_checkpoint, [&] { restored.restore(invalid); }, key());
    invalid = prefix;
    invalid.trace->events.push_back(invalid.trace->events.front());
    failure(ChoiceFailure::duplicate_key, [&] { restored.restore(invalid); }, key());
    invalid = prefix;
    invalid.domain = RandomDomain::cosmetic;
    CHECK_THROWS_AS(restored.restore(invalid), std::invalid_argument);
    invalid = prefix;
    invalid.context = "other";
    CHECK_THROWS_AS(restored.restore(invalid), std::invalid_argument);
    CHECK(restored.checkpoint() == after);
    GameplayRandom generated(3, "session");
    generated.record_choices(1);
    invalid = generated.checkpoint();
    invalid.trace->events.push_back(inputs().records.front());
    failure(ChoiceFailure::invalid_checkpoint, [&] { restored.restore(invalid); });
    CHECK(restored.checkpoint() == after);
}

TEST_CASE("Unsupported floating mode cannot consume controlled or generated choices",
          "[base][random][choices]") {
    GameplayRandom controlled(inputs(), "session"), generated(7, "session");
    generated.record_choices(4);
    const auto controlled_entry = controlled.checkpoint(), generated_entry = generated.checkpoint();
    struct RestoreRounding {
        int value = std::fegetround();
        ~RestoreRounding() { std::fesetround(value); }
    } rounding;
    if (std::fesetround(FE_DOWNWARD) == 0 && std::fegetround() == FE_DOWNWARD) {
        CHECK_THROWS_AS(controlled.secondary_range(key(1), -4, 7), std::domain_error);
        CHECK_THROWS_AS(generated.secondary_unit(key()), std::domain_error);
        CHECK(controlled.checkpoint() == controlled_entry);
        CHECK(generated.checkpoint() == generated_entry);
    } else {
        CHECK(std::fegetround() == FE_TONEAREST); // WASM supports nearest only.
    }
}
