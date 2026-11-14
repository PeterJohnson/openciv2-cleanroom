// M1-01's RNG component; this does not establish whole-session persistence.
#include "formats/random_checkpoint.hpp"
#include "formats/content_json.hpp"
#include <catch2/catch_test_macros.hpp>
#include <array>
#include <functional>

using namespace openciv;
using namespace openciv::base;
using formats::decode_random_checkpoint;
using formats::encode_random_checkpoint;

TEST_CASE("Portable generated checkpoints preserve all domains across engine blocks",
          "[random][checkpoint]") {
    const auto check = []<RandomDomain Domain>() {
        RandomContext<Domain> source(UINT64_MAX, "checkpoint/context");
        for (auto position : std::array<std::uint64_t, 6>{0, 1, 311, 312, 313, 1000}) {
            while (source.checkpoint().draws < position)
                source.secondary_code("test.advance");
            const auto saved = source.checkpoint();
            const auto bytes = encode_random_checkpoint(saved);
            const auto restored = decode_random_checkpoint(bytes);
            CHECK(restored == saved);
            CHECK(encode_random_checkpoint(restored) == bytes);
            RandomContext<Domain> destination(77, "checkpoint/context");
            destination.restore(restored);
            auto expected = source;
            for (int i = 0; i < 700; ++i)
                REQUIRE(destination.secondary_code("test.suffix") ==
                        expected.secondary_code("test.suffix"));
            CHECK(source.checkpoint() == saved);
        }
    };
    check.template operator()<RandomDomain::gameplay>();
    check.template operator()<RandomDomain::presentation_content>();
    check.template operator()<RandomDomain::cosmetic>();
}

TEST_CASE("Portable controlled checkpoint retains exact keys and consumed prefix",
          "[random][checkpoint]") {
    const ChoiceKey first{std::string("a\0\xff", 3), "first", 0}, second{"test", "second", 1},
        third{"test", "third", 2}, fourth{"test", "fourth", 3};
    ControlledChoices tape{{make_choice(first, PrimaryCode{}, std::int32_t{32767}),
                            make_choice(second, PrimaryModulo{7}, std::int32_t{9}),
                            make_choice(third, SecondaryUnit{}, UINT32_MAX),
                            make_choice(fourth, SecondaryRange{-4, 7}, std::uint32_t{0})}};
    GameplayRandom source(tape, "checkpoint/context");
    CHECK(source.primary_modulo(second, 7) == 2);
    const auto saved = source.checkpoint();
    const auto restored = decode_random_checkpoint(encode_random_checkpoint(saved), 0);
    CHECK(restored == saved);
    GameplayRandom destination(0, "checkpoint/context");
    destination.restore(restored);
    CHECK_THROWS_AS(destination.primary_modulo(second, 7), ChoiceError);
    CHECK(destination.checkpoint() == saved);
    CHECK(destination.primary_code(first) == 32767);
    CHECK(destination.secondary_unit(third) == 1.0);
    CHECK(destination.secondary_range(fourth, -4, 7) == -4);
    CHECK_NOTHROW(destination.require_choices_consumed());
    CHECK(destination.checkpoint().draws == 0);
    CHECK(decode_random_checkpoint(encode_random_checkpoint(destination.checkpoint())) ==
          destination.checkpoint());
}

TEST_CASE("Portable generated traces preserve budgets and resolved choices",
          "[random][checkpoint]") {
    GameplayRandom source(31, "checkpoint/context");
    source.record_choices(2);
    source.primary_modulo({"test", "prefix", 0}, 5);
    const auto saved = source.checkpoint();
    GameplayRandom restored(0, "checkpoint/context");
    restored.restore(decode_random_checkpoint(encode_random_checkpoint(saved)));
    CHECK_THROWS_AS(restored.primary_modulo({"test", "prefix", 0}, 5), ChoiceError);
    CHECK(restored.secondary_unit({"test", "suffix", 0}) ==
          source.secondary_unit({"test", "suffix", 0}));
    CHECK(restored.checkpoint() == source.checkpoint());
    CHECK_THROWS_AS(restored.primary_code({"test", "over-budget", 0}), ChoiceError);
}

TEST_CASE("Random checkpoint admission rejects malformed metadata and tape changes",
          "[random][checkpoint]") {
    ControlledChoices tape{{make_choice({"test", "one", 0}, SecondaryUnit{}, std::uint32_t{0})}};
    GameplayRandom source(tape, "checkpoint/context"), destination(44, "checkpoint/context");
    source.secondary_unit({"test", "one", 0});
    const auto bytes = encode_random_checkpoint(source.checkpoint());
    const auto unchanged = destination.checkpoint();
    const std::array<std::function<void(jt::Json&)>, 14> corrupt{
        [](auto& v) { v["format"] = "unknown"; },
        [](auto& v) { v["policy"]["id"] = "unknown"; },
        [](auto& v) { v["policy"]["version"] = "1"; },
        [](auto& v) { v["domain"] = "unknown"; },
        [](auto& v) { v["context"] = "bad context"; },
        [](auto& v) { v["engineSeed"] = "18446744073709551616"; },
        [](auto& v) { v["draws"] = "01"; },
        [](auto& v) { v["draws"] = "1"; },
        [](auto& v) { v["extra"] = true; },
        [](auto& v) { v["trace"]["limit"] = "0"; },
        [](auto& v) { v["trace"]["events"][0]["result"]["f64"] = "8000000000000000"; },
        [](auto& v) { v["controlled"][0]["law"]["kind"] = "unknown"; },
        [](auto& v) { v["controlled"][0]["siteHex"] = "gg"; },
        [](auto& v) { v["controlled"].getArray().push_back(v["controlled"][0]); }};
    for (const auto& mutation : corrupt) {
        auto parsed = formats::content::parse_json(bytes, "test");
        mutation(parsed.value);
        CHECK_THROWS(destination.restore(decode_random_checkpoint(parsed.value.toString())));
        CHECK(destination.checkpoint() == unchanged);
    }
    CHECK_THROWS(decode_random_checkpoint(bytes.substr(0, bytes.size() / 2)));
    CHECK_THROWS(decode_random_checkpoint("{\"format\":\"a\",\"format\":\"b\"}"));
    CHECK(destination.checkpoint() == unchanged);
}

TEST_CASE("Random checkpoint replay is bounded and exact integer seeds survive",
          "[random][checkpoint]") {
    GameplayRandom source(make_mt19937_source(UINT64_MAX), "checkpoint/context");
    source.primary_code("test.advance");
    const auto saved = source.checkpoint();
    CHECK(seeded_random_state(*saved.engine) == SeededRandomState{UINT64_MAX, 1});
    const auto bytes = encode_random_checkpoint(saved, 1);
    CHECK(bytes.find("18446744073709551615") != std::string::npos);
    CHECK(decode_random_checkpoint(bytes, 1) == saved);
    CHECK_THROWS(encode_random_checkpoint(saved, 0));
    CHECK_THROWS(decode_random_checkpoint(bytes, 0));
    auto invalid = saved;
    invalid.draws = 0;
    CHECK_THROWS(encode_random_checkpoint(invalid));
    invalid = saved;
    invalid.trace = ChoiceTrace{0, {}};
    invalid.context.assign(4097, 'a');
    CHECK_THROWS(encode_random_checkpoint(invalid));
    CHECK(source.checkpoint() == saved);
    class Unsupported final : public RandomSource {
    public:
        std::string_view id() const noexcept override { return random_policy_id; }
        std::uint64_t next() override { return 0; }
        std::unique_ptr<RandomSource> clone() const override {
            return std::make_unique<Unsupported>();
        }
        bool same_state(const RandomSource& other) const noexcept override {
            return dynamic_cast<const Unsupported*>(&other);
        }
    };
    GameplayRandom mock(std::make_unique<Unsupported>(), "checkpoint/context");
    CHECK_THROWS(encode_random_checkpoint(mock.checkpoint()));
}
