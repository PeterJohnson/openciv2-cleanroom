#include <cstdint>
#include <catch2/catch_test_macros.hpp>

import std;
import openciv.base.random;

using namespace openciv::base;
namespace {
// Authored raw inputs, independent of a generator sequence. Semantic tape tests
// separately check X02 keys/laws; this mock tests the source/context boundary.
class SequenceSource final : public RandomSource {
public:
    explicit SequenceSource(std::vector<std::uint64_t> words) : words_(std::move(words)) {}
    std::string_view id() const noexcept override { return "test.sequence/1"; }
    std::uint64_t next() override {
        const auto value = words_.at(position_);
        ++position_;
        return value;
    }
    std::unique_ptr<RandomSource> clone() const override {
        return std::make_unique<SequenceSource>(*this);
    }
    bool same_state(const RandomSource& other) const noexcept override {
        const auto* source = dynamic_cast<const SequenceSource*>(&other);
        return source && words_ == source->words_ && position_ == source->position_;
    }

private:
    std::vector<std::uint64_t> words_;
    std::size_t position_ = 0;
};
auto sequence(std::initializer_list<std::uint64_t> words) {
    return std::make_unique<SequenceSource>(std::vector<std::uint64_t>(words));
}
} // namespace

TEST_CASE("Production random source retains the standard engine sequence",
          "[base][random][source]") {
    // random-policy.md v2 and C++ [rand.eng.mers]: retain the chosen engine,
    // including multiple state-block boundaries, zero and high-bit seeds.
    for (auto seed : std::array<std::uint64_t, 5>{0, 1, 5489, UINT64_C(1) << 63, UINT64_MAX}) {
        auto source = make_mt19937_source(seed);
        std::mt19937_64 reference(seed);
        CHECK(source->id() == random_policy_id);
        for (int i = 0; i < 1250; ++i)
            REQUIRE(source->next() == reference());
        auto saved = source->clone();
        source->discard(625);
        reference.discard(625);
        CHECK(source->next() == reference());
        saved->discard(626);
        CHECK(source->same_state(*saved));
    }
}

TEST_CASE("Injected random sources retain F01 mappings in every domain", "[base][random][source]") {
    // F01 §§2–4,6: domains do not change projection/mapping or singleton draws.
    const auto check = []<RandomDomain Domain>() {
        RandomContext<Domain> random(sequence({0, UINT64_MAX, UINT64_MAX, 0}), "test/context");
        random.record_choices(4);
        const auto before = random.checkpoint();
        CHECK(random.primary_modulo(ChoiceKey{}, 1) == 0);
        CHECK(random.secondary_range(ChoiceKey{}, 7, 7) == 7);
        CHECK_THROWS_AS(random.primary_modulo({"test", "invalid", 0}, 0), std::domain_error);
        CHECK(random.checkpoint() == before);
        CHECK(random.primary_code({"test", "primary", 0}) == 0);
        CHECK(random.primary_modulo({"test", "modulo", 0}, 5) == 2);
        CHECK(random.secondary_unit({"test", "unit", 0}) == 1.0);
        CHECK(random.secondary_range({"test", "range", 0}, -4, 7) == -4);
        const auto after = random.checkpoint();
        CHECK(after.draws == 4);
        REQUIRE(after.trace->events.size() == 4);
        CHECK(after.trace->events[1].code == ChoiceCode{std::int32_t{32767}});
        CHECK(after.trace->events[2].code == ChoiceCode{UINT32_MAX});
        CHECK(after.engine->id() == "test.sequence/1");
    };
    check.template operator()<RandomDomain::gameplay>();
    check.template operator()<RandomDomain::presentation_content>();
    check.template operator()<RandomDomain::cosmetic>();
}

TEST_CASE("Mock checkpoints and copied contexts own independent source positions",
          "[base][random][source]") {
    GameplayRandom random(sequence({UINT64_MAX, 0, UINT64_C(1) << 63}), "test/context");
    random.record_choices(3);
    CHECK(random.primary_code({"test", "first", 0}) == 32767);
    auto saved = random.checkpoint();
    const auto copied = saved;
    CHECK(saved == copied);
    CHECK(saved.engine.get() != copied.engine.get());
    CHECK(saved.engine->next() == 0);
    CHECK(saved != copied);
    CHECK(random.checkpoint() == copied);
    auto independent = random;
    CHECK(independent.primary_code({"test", "next", 0}) == 0);
    CHECK(random.checkpoint() == copied);
    saved = copied; // Copy assignment also clones.
    CHECK(saved == copied);
    CHECK(saved.engine.get() != copied.engine.get());
    CHECK(random.primary_code({"test", "next", 0}) == 0);
    CHECK(random.primary_code({"test", "last", 0}) == 16384);
    const auto completed = random.checkpoint();
    random.restore(saved);
    CHECK_THROWS_AS(random.primary_code({"test", "first", 0}), ChoiceError);
    CHECK(random.checkpoint() == copied);
    CHECK(random.primary_code({"test", "next", 0}) == 0);
    CHECK(random.primary_code({"test", "last", 0}) == 16384);
    CHECK(random.checkpoint() == completed);
}

TEST_CASE("Source failure preserves draw accounting and semantic key availability",
          "[base][random][source]") {
    GameplayRandom plain(sequence({}), "test/context");
    const auto plain_before = plain.checkpoint();
    CHECK_THROWS_AS(plain.primary_code("test.site"), std::out_of_range);
    CHECK(plain.checkpoint() == plain_before);
    GameplayRandom traced(sequence({UINT64_MAX}), "test/context");
    traced.record_choices(3);
    traced.primary_code({"test", "first", 0});
    const auto before = traced.checkpoint();
    // Repeating the failed key must still reach the exhausted mock, rather than
    // failing as already consumed or appending a fabricated event.
    for (int i = 0; i < 2; ++i) {
        CHECK_THROWS_AS(traced.secondary_unit({"test", "next", 0}), std::out_of_range);
        CHECK(traced.checkpoint() == before);
    }
}

TEST_CASE("Checkpoint restore rejects absent and incompatible sources atomically",
          "[base][random][source]") {
    CHECK_THROWS_AS(GameplayRandom(std::unique_ptr<RandomSource>{}, "test/context"),
                    std::invalid_argument);
    GameplayRandom real(7, "test/context"), mock(sequence({0}), "test/context");
    const auto real_before = real.checkpoint(), mock_before = mock.checkpoint();
    CHECK_THROWS_AS(real.restore(mock_before), std::invalid_argument);
    CHECK_THROWS_AS(mock.restore(real_before), std::invalid_argument);
    CHECK(real.checkpoint() == real_before);
    CHECK(mock.checkpoint() == mock_before);
    auto invalid = real_before;
    invalid.engine.reset();
    CHECK_THROWS_AS(real.restore(invalid), std::invalid_argument);
    CHECK(real.checkpoint() == real_before);
}
