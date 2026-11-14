// F05 sections 2-3, F01 cyclic arithmetic/sampling and F06 preference fields.
// Expectations are independently authored from the contract. The explicitly
// named Windows observation case also cites original UI/file evidence.
#include "service_fixture.hpp"
#include "configuration_fixture.hpp"
#include "session/world_setup.hpp"
#include <limits>

using namespace openciv;
using namespace openciv::session;
namespace {
using Parameter = state::WorldParameter;
constexpr std::array<Parameter, 5> custom_order{Parameter::land_mass, Parameter::land_form,
                                                Parameter::climate, Parameter::temperature,
                                                Parameter::age};
constexpr std::array<std::string_view, 5> sites{"F05.world.land-mass", "F05.world.land-form",
                                                "F05.world.temperature", "F05.world.climate",
                                                "F05.world.age"};
std::vector<base::Choice> choices(std::array<std::int32_t, 5> codes,
                                  std::string call = "world/one") {
    std::vector<base::Choice> result;
    for (std::size_t i = 0; i < codes.size(); ++i)
        result.push_back(
            base::make_choice({std::string(sites[i]), call, 0}, base::PrimaryModulo{3}, codes[i]));
    return result;
}
struct Fixture : test::ConfigurationFixture {
    WorldSizeServices size;
    WorldParameterServices parameters;
    std::vector<Parameter> visits;
    Fixture() {
        test::fail_service(size.custom);

        process.ordinary_preferences.map_size = 2;
        process.ordinary_preferences.world_parameters = {0, 0, 0, -1, 1};
        runtime.world().world_setup = {31, 37, {11, 12, 13, 14, 15}};
        runtime.world().globals.game_flags = 0xffff;
        size.preset = [](std::int16_t) -> WorldSizeChoice { return 1; };
        parameters.preferences = preferences;
        parameters.choose = [&](Parameter parameter, std::int16_t) -> WorldParameterChoice {
            visits.push_back(parameter);
            return 1;
        };
    }
    base::GameplayRandom controlled(std::array<std::int32_t, 5> codes = {2, 3, 4, 5, 6}) {
        return base::GameplayRandom{base::ControlledChoices{choices(codes)}, "setup"};
    }
    base::GameplayRandom no_choices() {
        return base::GameplayRandom{base::ControlledChoices{}, "setup"};
    }
};
} // namespace

TEST_CASE("F05 size presets classify area clear status and remember ordinary choices",
          "[world-setup][session]") {
    struct Expected {
        std::int32_t choice;
        std::int16_t width, height;
        std::uint16_t flags;
    };
    for (const auto e : {Expected{0, 80, 50, 8}, Expected{1, 100, 80, 0}, Expected{2, 150, 120, 4},
                         Expected{7, 100, 80, 0}}) {
        Fixture f;
        f.size.preset = [&](std::int16_t remembered) -> WorldSizeChoice {
            CHECK(remembered == 2);
            CHECK(f.runtime.world().globals.game_flags == 0);
            return e.choice;
        };
        CHECK(select_world_size(f.runtime, f.size));
        CHECK(f.runtime.world().world_setup.width == e.width);
        CHECK(f.runtime.world().world_setup.height == e.height);
        CHECK(f.runtime.world().globals.game_flags == e.flags);
        CHECK(f.process.ordinary_preferences.map_size == e.choice);
        CHECK(f.trace.empty());
        CHECK(f.runtime.world().world_setup.parameters ==
              std::array<std::int32_t, 5>{11, 12, 13, 14, 15});
    }
}

TEST_CASE("F05 custom sizes reduce decimal input before limits and classify exact boundaries",
          "[world-setup][session]") {
    struct Expected {
        std::string width, height;
        std::int16_t w, h;
        std::uint16_t flags;
    };
    for (const auto& e :
         {Expected{"65586", "80", 100, 80, 0}, Expected{"-65486", "80", 100, 80, 0},
          Expected{"20", "50", 40, 50, 8}, Expected{"250", "40", 500, 40, 4},
          Expected{"40", "250", 80, 250, 4}, Expected{"60", "50", 120, 50, 0},
          Expected{"100", "60", 200, 60, 4}, Expected{"59", "101", 118, 101, 0},
          Expected{"  +50suffix", "\t80", 100, 80, 0}, Expected{"4294967346", "80", 100, 80, 0}}) {
        Fixture f;
        f.size.preset = [](auto) -> WorldSizeChoice { return CustomWorldSize{}; };
        f.size.custom = [&](auto width, auto height) {
            CHECK(width == "50");
            CHECK(height == "80");
            return CustomSizeResponse{CustomSizeStatus::accepted, e.width, e.height};
        };
        REQUIRE(select_world_size(f.runtime, f.size));
        CHECK(f.runtime.world().world_setup.width == e.w);
        CHECK(f.runtime.world().world_setup.height == e.h);
        CHECK(f.runtime.world().globals.game_flags == e.flags);
        CHECK(f.process.ordinary_preferences.map_size == 2);
    }
}

TEST_CASE("F05 invalid custom sizes retry with fresh defaults without reopening presets",
          "[world-setup][session]") {
    for (const auto& input : {std::pair{"19", "80"},
                              {"50", "19"},
                              {"251", "20"},
                              {"20", "251"},
                              {"20", "49"},
                              {"101", "100"},
                              {"65535", "80"},
                              {"0x32", "80"},
                              {"0b110010", "80"},
                              {"", "80"},
                              {"word", "80"}}) {
        Fixture f;
        unsigned presets = 0, customs = 0;
        f.size.preset = [&](auto) -> WorldSizeChoice {
            ++presets;
            return CustomWorldSize{};
        };
        f.size.custom = [&](auto width, auto height) {
            CHECK(width == "50");
            CHECK(height == "80");
            if (++customs == 1)
                return CustomSizeResponse{CustomSizeStatus::accepted, input.first, input.second};
            return CustomSizeResponse{CustomSizeStatus::accepted, "50", "80"};
        };
        REQUIRE(select_world_size(f.runtime, f.size));
        CHECK(presets == 1);
        CHECK(customs == 2);
        CHECK(f.runtime.world().world_setup.width == 100);
        CHECK(f.runtime.world().world_setup.height == 80);
    }
}

TEST_CASE("F05 custom cancel reopens presets and subsequent size cancel retains invalid dimensions",
          "[world-setup][session]") {
    Fixture f;
    unsigned presets = 0, customs = 0;
    f.size.preset = [&](auto remembered) -> WorldSizeChoice {
        CHECK(remembered == 2);
        if (++presets == 1)
            return CustomWorldSize{};
        CHECK(f.runtime.world().world_setup.width == 19);
        CHECK(f.runtime.world().world_setup.height == 80);
        return -1;
    };
    f.size.custom = [&](auto width, auto height) {
        CHECK(width == "50");
        CHECK(height == "80");
        if (++customs == 1)
            return CustomSizeResponse{CustomSizeStatus::accepted, "19", "80"};
        return CustomSizeResponse{};
    };
    CHECK_FALSE(select_world_size(f.runtime, f.size));
    CHECK(presets == 2);
    CHECK(customs == 2);
    CHECK(f.runtime.world().world_setup.width == 38);
    CHECK(f.runtime.world().globals.game_flags == 8);
    CHECK(f.process.ordinary_preferences.map_size == 2);
    f.size.preset = [](auto) -> WorldSizeChoice { return -1; };
    CHECK_FALSE(select_world_size(f.runtime, f.size));
    CHECK(f.runtime.world().world_setup.width == 76);
    CHECK(f.runtime.world().globals.game_flags == 0); // 38*80 at second exit.
}

TEST_CASE(
    "F05 unavailable custom dialog finalizes retained size but a service exception stops there",
    "[world-setup][session]") {
    Fixture f;
    f.runtime.world().world_setup.width = 20000;
    f.runtime.world().world_setup.height = 2;
    f.size.preset = [](auto) -> WorldSizeChoice { return CustomWorldSize{}; };
    f.size.custom = [](auto, auto) {
        return CustomSizeResponse{CustomSizeStatus::unavailable, {}, {}};
    };
    CHECK_FALSE(select_world_size(f.runtime, f.size));
    CHECK(f.runtime.world().world_setup.width == -25536);
    CHECK(f.runtime.world().globals.game_flags == 4);
    f.size.custom = [](auto, auto) -> CustomSizeResponse { throw std::runtime_error("dialog"); };
    CHECK_THROWS_AS(select_world_size(f.runtime, f.size), std::runtime_error);
    CHECK(f.runtime.world().world_setup.width == -25536);
    CHECK(f.runtime.world().globals.game_flags == 0);
}

TEST_CASE("F05 ordinary parameters persist normalized values matching the Windows observation",
          "[world-setup][session]") {
    // docs/m1-original-setup-observations.md: Large, Archipelago, Wet, Cool,
    // 4 billion; the file's order is L,F,T,C,A. No capture payload is a test input.
    Fixture f;
    auto random = f.no_choices();
    const auto before = random.checkpoint();
    const std::array<std::int32_t, 5> rows{2, 0, 2, 0, 1};
    f.parameters.choose = [&](Parameter parameter,
                              std::int16_t remembered) -> WorldParameterChoice {
        const std::array<std::int16_t, 5> defaults{0, 0, -1, 0, 1};
        REQUIRE(f.visits.size() < 5);
        CHECK(parameter == custom_order[f.visits.size()]);
        CHECK(remembered == defaults[f.visits.size()]);
        f.visits.push_back(parameter);
        return rows[f.visits.size() - 1];
    };
    CHECK(customize_world_parameters(f.runtime, random, "world/one", f.parameters));
    CHECK(random.checkpoint() == before);
    CHECK(f.runtime.world().world_setup.parameters == std::array<std::int32_t, 5>{1, -1, -1, 1, 0});
    CHECK(f.process.ordinary_preferences.world_parameters ==
          std::array<std::int16_t, 5>{1, -1, -1, 1, 0});
    CHECK(f.trace == std::vector<std::string>{"open", "write:72", "write:444", "close"});
    REQUIRE(f.written.size() == 2);
    CHECK(formats::decode_ordinary_preferences(f.written[0]) == f.process.ordinary_preferences);
    CHECK(formats::decode_ordinary_preferences(f.written[0]).map_size == 2);
    CHECK(formats::Bytes(f.written[0].begin() + 0x1a, f.written[0].begin() + 0x24) ==
          formats::Bytes{1, 0, 255, 255, 255, 255, 1, 0, 0, 0});
}

TEST_CASE("F05 automatic parameters consume L F T C A and do not write preferences",
          "[world-setup][session]") {
    Fixture f;
    auto random = f.controlled();
    const auto preferences = f.process.ordinary_preferences;
    randomize_world_parameters(f.runtime, random, "world/one");
    random.require_choices_consumed();
    CHECK(f.runtime.world().world_setup.parameters == std::array<std::int32_t, 5>{1, 1, 0, 1, -1});
    CHECK(f.process.ordinary_preferences == preferences);
    CHECK(f.trace.empty());
    const auto consumed = random.checkpoint().trace->events;
    CHECK(consumed.size() == 5);
    for (std::size_t i = 0; i < 5; ++i)
        CHECK(consumed[i].key.site == sites[i]);
}

TEST_CASE("F05 randomized land form uses zero or the current land mass for all residue pairs",
          "[world-setup][session]") {
    constexpr std::array<std::array<std::int32_t, 3>, 3> expected{
        {{-1, 0, 0}, {0, 0, 0}, {1, 0, 0}}};
    for (std::int32_t mass = 0; mass < 3; ++mass)
        for (std::int32_t form = 0; form < 3; ++form) {
            Fixture f;
            auto random = f.controlled({mass, form, 32767, 32766, 32765});
            randomize_world_parameters(f.runtime, random, "world/one");
            random.require_choices_consumed();
            CHECK(f.runtime.world().world_setup.parameters[1] == expected[mass][form]);
            CHECK(f.runtime.world().world_setup.parameters[2] == 0);
            CHECK(f.runtime.world().world_setup.parameters[3] == -1);
            CHECK(f.runtime.world().world_setup.parameters[4] == 1);
        }
}

TEST_CASE("F05 custom Random consumes L F C T A and saves unchanged remembered parameters",
          "[world-setup][session]") {
    Fixture f;
    auto random = f.controlled();
    const auto remembered = f.process.ordinary_preferences.world_parameters;
    f.parameters.choose = [&](Parameter parameter, auto) -> WorldParameterChoice {
        f.visits.push_back(parameter);
        return RandomWorldParameter{};
    };
    CHECK(customize_world_parameters(f.runtime, random, "world/one", f.parameters));
    random.require_choices_consumed();
    CHECK(f.visits == std::vector<Parameter>(custom_order.begin(), custom_order.end()));
    CHECK(f.runtime.world().world_setup.parameters == std::array<std::int32_t, 5>{1, 1, 0, 1, -1});
    CHECK(f.process.ordinary_preferences.world_parameters == remembered);
    REQUIRE(f.written.size() == 2);
    CHECK(formats::decode_ordinary_preferences(f.written[0]).world_parameters == remembered);
    const auto consumed = random.checkpoint().trace->events;
    REQUIRE(consumed.size() == 5);
    CHECK(consumed[2].key.site == "F05.world.climate");
    CHECK(consumed[3].key.site == "F05.world.temperature");
}

TEST_CASE("F05 custom cancellation retains the written negative value and earlier choices only",
          "[world-setup][session]") {
    for (std::size_t stop = 0; stop < 5; ++stop)
        for (const std::int32_t cancel : {-1, -7, std::numeric_limits<std::int32_t>::min()}) {
            Fixture f;
            auto random = f.no_choices();
            auto expected = f.runtime.world().world_setup.parameters;
            auto remembered = f.process.ordinary_preferences.world_parameters;
            std::size_t count = 0;
            f.parameters.choose = [&](Parameter parameter, auto) -> WorldParameterChoice {
                CHECK(parameter == custom_order[count]);
                return count++ == stop ? cancel : 2;
            };
            CHECK_FALSE(
                select_world_parameters(f.runtime, random, "world/one", true, true, f.parameters));
            CHECK(count == stop + 1);
            for (std::size_t i = 0; i < stop; ++i) {
                expected[static_cast<std::size_t>(custom_order[i])] = 1;
                remembered[static_cast<std::size_t>(custom_order[i])] = 1;
            }
            expected[static_cast<std::size_t>(custom_order[stop])] =
                cancel == std::numeric_limits<std::int32_t>::min() ? 2147483647 : cancel - 1;
            CHECK(f.runtime.world().world_setup.parameters == expected);
            CHECK(f.process.ordinary_preferences.world_parameters == remembered);
            CHECK(f.trace.empty());
        }
}

TEST_CASE("F05 failed size or map leaves parameter state and randomness untouched",
          "[world-setup][session]") {
    for (const bool custom : {false, true}) {
        Fixture f;
        auto random = f.no_choices();
        const auto before = f.runtime.world().world_setup.parameters;
        CHECK_FALSE(select_world_parameters(f.runtime, random, "world/one", custom, false, {}));
        random.require_choices_consumed();
        CHECK(f.runtime.world().world_setup.parameters == before);
        CHECK(f.trace.empty());
    }
    Fixture f;
    auto random = f.controlled();
    CHECK(select_world_parameters(f.runtime, random, "world/one", false, true, {}));
    random.require_choices_consumed();
}

TEST_CASE("F05 unavailable random choices retain the exact reached parameter prefix",
          "[world-setup][session]") {
    const std::array<std::array<std::int32_t, 5>, 5> expected{{{11, 12, 13, 14, 15},
                                                               {1, 0, 13, 14, 15},
                                                               {1, 1, 13, 14, 15},
                                                               {1, 1, 0, 14, 15},
                                                               {1, 1, 0, 1, 15}}};
    for (std::size_t stop = 0; stop < 5; ++stop) {
        Fixture f;
        auto supplied = choices({2, 3, 4, 5, 6});
        supplied.resize(stop);
        base::GameplayRandom random{base::ControlledChoices{std::move(supplied)}, "setup"};
        CHECK_THROWS(randomize_world_parameters(f.runtime, random, "world/one"));
        CHECK(f.runtime.world().world_setup.parameters == expected[stop]);
        CHECK(random.checkpoint().trace->events.size() == stop);
    }
}

TEST_CASE("F05 custom choices use live earlier state and consume only requested randomness",
          "[world-setup][session]") {
    Fixture f;
    auto supplied = choices({0, 3, 0, 2, 0});
    supplied.erase(supplied.begin() + 4);
    supplied.erase(supplied.begin() + 2);
    supplied.erase(supplied.begin());
    base::GameplayRandom random{base::ControlledChoices{std::move(supplied)}, "setup"};
    f.parameters.choose = [&](Parameter parameter,
                              std::int16_t remembered) -> WorldParameterChoice {
        if (parameter == Parameter::land_form) {
            CHECK(f.runtime.world().world_setup.parameters[0] == -1);
            f.runtime.world().world_setup.parameters[0] = 1;
            f.process.ordinary_preferences.world_parameters[3] = -9;
            return RandomWorldParameter{};
        }
        if (parameter == Parameter::climate) {
            CHECK(remembered == -9);
            return RandomWorldParameter{};
        }
        return 0;
    };
    CHECK(customize_world_parameters(f.runtime, random, "world/one", f.parameters));
    random.require_choices_consumed();
    CHECK(f.runtime.world().world_setup.parameters == std::array<std::int32_t, 5>{1, 1, -1, 1, -1});
    CHECK(f.process.ordinary_preferences.world_parameters ==
          std::array<std::int16_t, 5>{-1, 0, -1, -9, -1});
}

TEST_CASE("F05 preference write failures retain accepted choices and follow real F03 cleanup",
          "[world-setup][session]") {
    for (int failure = -1; failure < 2; ++failure) {
        Fixture f;
        auto random = f.no_choices();
        f.fail_open = failure == -1;
        if (failure >= 0)
            f.fail_item = static_cast<std::size_t>(failure);
        CHECK(customize_world_parameters(f.runtime, random, "world/one", f.parameters));
        CHECK(f.process.ordinary_preferences.world_parameters == std::array<std::int16_t, 5>{});
        if (failure == -1)
            CHECK(f.trace == std::vector<std::string>{"open"});
        else {
            CHECK(f.trace.back() == "delete");
            CHECK(f.written.size() == failure + 1);
        }
    }
}

TEST_CASE("F05 child failures and exceptional saves preserve reached world and preference state",
          "[world-setup][session]") {
    Fixture f;
    auto random = f.no_choices();
    f.parameters.choose = [&](Parameter p, auto) -> WorldParameterChoice {
        if (p == Parameter::climate)
            throw std::runtime_error("modal");
        return 2;
    };
    CHECK_THROWS_AS(customize_world_parameters(f.runtime, random, "first", f.parameters),
                    std::runtime_error);
    CHECK(f.runtime.world().world_setup.parameters ==
          std::array<std::int32_t, 5>{1, 1, 13, 14, 15});
    CHECK(f.process.ordinary_preferences.world_parameters ==
          std::array<std::int16_t, 5>{1, 1, 0, -1, 1});
    CHECK(f.trace.empty());
    f.parameters.choose = [](auto, auto) -> WorldParameterChoice { return 0; };
    test::fail_service(f.parameters.preferences.open_output);
    CHECK_THROWS_AS(customize_world_parameters(f.runtime, random, "second", f.parameters),
                    std::runtime_error);
    CHECK(f.runtime.world().world_setup.parameters ==
          std::array<std::int32_t, 5>{-1, -1, -1, -1, -1});
    CHECK(f.process.ordinary_preferences.world_parameters ==
          std::array<std::int16_t, 5>{-1, -1, -1, -1, -1});
}

TEST_CASE("F05 setup publishes before interactions persistence and exceptional completion",
          "[world-setup][session]") {
    Fixture f;
    auto random = f.no_choices();
    auto& unit = f.runtime.world().entities.units.storage()[0];
    unit.token = state::UnitToken{1};
    unit.owner = state::CivilizationSlot{1};
    f.runtime.world().entities.units.set_end(1);
    const auto client = f.runtime.bind(f.runtime.add_participant(state::CivilizationSlot{1}),
                                       ControllerKind::frontend);
    unit.x = 7;
    std::int16_t next = 7;
    const auto check = [&] {
        REQUIRE(client.own_units().records.size() == 1);
        CHECK(client.own_units().records[0].x == next);
        unit.x = ++next;
    };
    f.size.preset = [&](auto) -> WorldSizeChoice {
        check();
        return 0;
    };
    CHECK(select_world_size(f.runtime, f.size));
    CHECK(client.own_units().records[0].x == next);
    f.parameters.choose = [&](auto, auto) -> WorldParameterChoice {
        check();
        return 1;
    };
    f.after_open = check;
    f.after_write = [&](auto) { check(); };
    f.after_close = check;
    CHECK(customize_world_parameters(f.runtime, random, "first", f.parameters));
    CHECK(client.own_units().records[0].x == next);
    f.parameters.choose = [&](auto, auto) -> WorldParameterChoice {
        check();
        throw std::runtime_error("stop");
    };
    CHECK_THROWS(customize_world_parameters(f.runtime, random, "second", f.parameters));
    CHECK(client.own_units().records[0].x == next);
}

TEST_CASE("F05 empty parameter identity rejects before mutation and inactive setup remains usable",
          "[world-setup][session]") {
    state::ProcessContext process;
    SessionRuntime runtime{process, base::RuntimeId{88}};
    base::GameplayRandom random{base::ControlledChoices{choices({0, 0, 0, 0, 0})}, "setup"};
    const auto before = runtime.world().world_setup;
    CHECK_THROWS_AS(randomize_world_parameters(runtime, random, ""), std::invalid_argument);
    CHECK_THROWS_AS(customize_world_parameters(runtime, random, "", {}), std::invalid_argument);
    CHECK(runtime.world().world_setup == before);
    CHECK(random.checkpoint().trace->events.empty());
    WorldSizeServices size;
    test::fail_service(size.custom);
    size.preset = [](auto) -> WorldSizeChoice { return 1; };
    CHECK(select_world_size(runtime, size));
    randomize_world_parameters(runtime, random, "world/one");
    random.require_choices_consumed();
    CHECK_FALSE(runtime.active());
}

TEST_CASE("F05 unsupported parameter rows stop at consumption after earlier accepted effects",
          "[world-setup][session]") {
    Fixture f;
    auto random = f.no_choices();
    f.parameters.choose = [](Parameter p, auto) -> WorldParameterChoice {
        return p == Parameter::climate ? 3 : 2;
    };
    CHECK_THROWS_AS(customize_world_parameters(f.runtime, random, "world/one", f.parameters),
                    std::invalid_argument);
    CHECK(f.runtime.world().world_setup.parameters ==
          std::array<std::int32_t, 5>{1, 1, 13, 14, 15});
    CHECK(f.process.ordinary_preferences.world_parameters ==
          std::array<std::int16_t, 5>{1, 1, 0, -1, 1});
    CHECK(f.trace.empty());
    CHECK(random.checkpoint().trace->events.empty());
}
