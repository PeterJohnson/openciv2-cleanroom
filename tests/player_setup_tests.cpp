// F05 section 4 / Appendix A, F03 preferences and F01 sampling. Expectations
// are authored from the contracts; original UI/file comparisons are identified
// separately in docs/m1-original-player-setup-observations.md.
#include "session/player_setup.hpp"
#include "formats/content_pack.hpp"
#include "session/runtime.hpp"
#include <catch2/catch_test_macros.hpp>
#include <deque>

using namespace openciv;
using namespace openciv::session;
namespace {
const state::CalendarValues& calendar() {
    return formats::content::builtin_gameplay()->values.calendar;
}
std::string text(const state::FixedText<24>& value) {
    const auto bytes = value.text();
    return {reinterpret_cast<const char*>(bytes.data()), bytes.size()};
}
base::Choice choice(std::string site, unsigned ordinal, std::int32_t modulus, std::int32_t code) {
    return base::make_choice({"F05.setup." + site, "wizard/" + std::to_string(ordinal), 0},
                             base::PrimaryModulo{modulus}, code);
}
struct Fixture {
    state::ProcessContext process;
    SessionRuntime runtime{process, base::RuntimeId{98}};
    PlayerSetupServices services;
    std::vector<std::string> visits;
    Fixture() {
        state::default_ordinary_preferences(process.ordinary_preferences, {});
        auto& rules = *process.rules;
        rules.text.initialize();
        auto label = [&](const std::string& value) {
            return static_cast<std::int16_t>(rules.text.store(value));
        };
        for (unsigned i = 0; i < 21; ++i) {
            auto& leader = rules.leaders[i];
            const auto suffix = std::to_string(i);
            leader.names = {label("Male" + suffix), label("Female" + suffix)};
            leader.leader_name = leader.names[0];
            leader.plural = label("Tribe" + suffix);
            leader.adjective = label("Adjective" + suffix);
            leader.color = static_cast<std::int16_t>(i % 7 + 1);
            leader.city_style = static_cast<std::int16_t>(i % 4);
            leader.times_used = 9;
            leader.city_name_index = 11;
            for (unsigned g = 0; g < 7; ++g)
                leader.titles[g] = {label("M" + std::to_string(g) + "/" + suffix),
                                    label("F" + std::to_string(g) + "/" + suffix)};
        }
        services.difficulty = [&](auto) {
            visits.push_back("difficulty");
            return 2;
        };
        services.opponents = [&](auto) -> SetupNumericChoice {
            visits.push_back("opponents");
            return 4;
        };
        services.barbarians = [&](auto) -> SetupNumericChoice {
            visits.push_back("barbarians");
            return 1;
        };
        services.rules = [&](auto) {
            visits.push_back("rules");
            return 0;
        };
        services.gender = [&](auto) {
            visits.push_back("gender");
            return 1;
        };
        services.tribe = [&](auto options, auto) {
            visits.push_back("tribe");
            CHECK(options.size() == 21);
            CHECK(options[3].tribe == 3);
            CHECK(options[3].plural == "Tribe3");
            CHECK(options[3].preferred_color == 4);
            return SetupTribeChoice{3, false};
        };
        services.names = [&](bool custom, auto current) {
            visits.push_back(custom ? "custom-names" : "name");
            return SetupNameResponse{SetupNameAction::accept, current};
        };
        services.city_style = [&](auto, auto) {
            visits.push_back("style");
            return 2;
        };
        services.opponent_tribe = [&](auto slot, auto) -> SetupNumericChoice {
            visits.push_back("opponent:" + std::to_string(static_cast<unsigned>(slot)));
            return RandomSetupChoice{};
        };
    }
    void custom_rules(SetupAdvancedChoices checks) {
        services.rules = [&](auto) {
            visits.push_back("rules");
            return 1;
        };
        services.advanced = [&, checks](auto) {
            visits.push_back("advanced");
            return SetupAdvancedResponse{-1, checks};
        };
    }
    void custom_tribe() {
        services.tribe = [&](auto, auto) {
            visits.push_back("tribe");
            return SetupTribeChoice{3, true};
        };
    }
    base::GameplayRandom slots(std::vector<std::int32_t> codes = {4, 0, 2, 2, 7}) {
        base::ControlledChoices supplied;
        for (unsigned i = 0; i < codes.size(); ++i)
            supplied.records.push_back(choice("opponent-slot", i, 8, codes[i]));
        return base::GameplayRandom{std::move(supplied), "setup"};
    }
    PlayerSetupResult run(base::GameplayRandom& random,
                          WorldSource source = WorldSource::generated) {
        return select_players(runtime, random, "wizard", source, calendar(), services);
    }
};
} // namespace

TEST_CASE("F05 ordinary player setup publishes identity preferences and a sampled roster",
          "[player-setup][session]") {
    Fixture f;
    auto& world = f.runtime.world();
    auto& globals = world.globals;
    globals.retained_0e = 0x1234;
    globals.retained_34 = 3456;
    globals.turn = 71;
    globals.year = 1900;
    globals.first_launch_turn = 21;
    world.scenario.start_year = -100;
    world.scenario.year_increment = -7;
    const auto scenario = world.scenario;
    world.civilizations[4].flags = 0xabcd;
    world.names[4].tribe.bytes.fill(0xff); // Ordinary Name must not read this.
    world.names[4].leader.bytes.fill(0xee);
    auto random = f.slots();
    const auto result = f.run(random);
    CHECK(result.accepted);
    CHECK_FALSE(result.pick_opponents);
    CHECK(result.acceleration == 0);
    CHECK(globals.retained_0e == 0);
    CHECK(globals.retained_34 == 3456);
    CHECK(globals.rules == 0x3f);
    CHECK(globals.difficulty == 2);
    CHECK(globals.num_enemies == 2);
    CHECK(globals.barbarian_activity == 1);
    CHECK(globals.player_civ == 4);
    CHECK(globals.human_players == 0x10);
    CHECK(globals.civs_alive == 0x95);
    CHECK(globals.turn == 71);
    CHECK(globals.year == 1900);
    CHECK(globals.first_launch_turn == 21);
    CHECK(world.scenario == scenario);
    CHECK(world.multiplayer.local_human == 4);
    CHECK(world.civilizations[4].tribe == 3);
    CHECK(world.civilizations[4].leader_portrait == 24);
    CHECK(world.civilizations[4].flags == 0xabcd);
    CHECK(world.names[4].city_style == 2);
    CHECK(text(world.names[4].leader) == "Female3");
    CHECK(world.names[4].leader.bytes[8] == 0xee);
    CHECK(world.names[4].tribe.bytes[0] == 0xff);
    const auto& leader = f.process.rules->leaders[3];
    CHECK(leader.leader_name == -leader.names[1]);
    CHECK(leader.female == 1);
    CHECK(leader.plural > 0);
    CHECK(leader.city_style == 3);
    CHECK(leader.times_used == 9);
    CHECK(leader.city_name_index == 11);
    const auto& p = f.process.ordinary_preferences;
    CHECK(p.difficulty == 2);
    CHECK(p.opponents == 4);
    CHECK(p.barbarians == 1);
    CHECK(p.gender == 1);
    CHECK(p.tribe == 3);
    CHECK(p.rules_choice == 0);
    CHECK(world.pending_leader_overrides ==
          std::array<std::int8_t, 8>{-1, -1, -1, -1, -1, -1, -1, -1});
    CHECK(f.visits == std::vector<std::string>{"difficulty", "opponents", "barbarians", "rules",
                                               "gender", "tribe", "name", "style"});
    random.require_choices_consumed();
    CHECK_FALSE(f.runtime.active());
}

TEST_CASE("F05 entry restores marked labels and clears only its reserved game field",
          "[player-setup][session]") {
    Fixture f;
    auto& leaders = f.process.rules->leaders;
    const auto expected = leaders;
    for (auto& l : leaders) {
        l.leader_name = -l.leader_name;
        l.plural = -l.plural;
        l.adjective = -l.adjective;
        for (auto& pair : l.titles)
            for (auto& id : pair)
                id = -id;
    }
    f.runtime.world().globals.retained_0e = 0xffff;
    f.runtime.world().globals.retained_34 = 123;
    auto globals = f.runtime.world().globals;
    globals.retained_0e = 0;
    f.services.difficulty = [&](auto) {
        CHECK(leaders == expected);
        return -42;
    };
    auto random = f.slots({});
    CHECK_FALSE(f.run(random).accepted);
    CHECK(f.runtime.world().globals == globals);
    CHECK(leaders == expected);
    random.require_choices_consumed();
}

TEST_CASE("F05 early cancellations return to their specified stage without undoing choices",
          "[player-setup][session]") {
    for (const auto cancel : {"opponents", "barbarians", "gender"}) {
        Fixture f;
        unsigned difficulties = 0;
        f.services.difficulty = [&](auto remembered) {
            CHECK(remembered == (difficulties == 0 ? 0 : 5));
            return ++difficulties == 1 ? 5 : -1;
        };
        if (std::string_view(cancel) == "opponents")
            f.services.opponents = [](auto) -> SetupNumericChoice { return -2; };
        if (std::string_view(cancel) == "barbarians")
            f.services.barbarians = [](auto) -> SetupNumericChoice { return -3; };
        if (std::string_view(cancel) == "gender")
            f.services.gender = [](auto) { return -4; };
        auto random = f.slots({});
        CHECK_FALSE(f.run(random).accepted);
        CHECK(difficulties == 2);
        CHECK(f.runtime.world().globals.difficulty == 5);
        if (std::string_view(cancel) != "opponents") {
            CHECK(f.runtime.world().globals.num_enemies == 2);
            CHECK(f.process.ordinary_preferences.opponents == 4);
        }
        random.require_choices_consumed();
    }
    Fixture f;
    f.services.rules = [](auto) { return -1; };
    auto random = f.slots({});
    CHECK_FALSE(f.run(random).accepted);
    CHECK(f.runtime.world().globals.barbarian_activity == 1);
    CHECK(f.process.ordinary_preferences.barbarians == 1);
    CHECK(f.runtime.world().globals.human_players == 0);
}

TEST_CASE("F05 all random difficulty branches preserve remembered ordinary rows",
          "[player-setup][session][random]") {
    for (std::int32_t level = 0; level < 6; ++level)
        for (const auto code : {0, 32767}) {
            CAPTURE(level, code);
            Fixture f;
            f.services.difficulty = [=](auto) { return level; };
            f.services.opponents = [](auto) -> SetupNumericChoice { return RandomSetupChoice{}; };
            f.services.barbarians = [](auto) -> SetupNumericChoice { return RandomSetupChoice{}; };
            f.services.rules = [](auto) { return -1; };
            base::GameplayRandom random{
                base::ControlledChoices{
                    {choice("opponent-count", 0, 5, code),
                     choice("barbarians", 0, level == 2 || level == 3 || level == 4 ? 3 : 2,
                            code)}},
                "setup"};
            CHECK_FALSE(f.run(random).accepted);
            CHECK(f.runtime.world().globals.num_enemies == code % 5 + 2);
            const std::array<int, 6> zero{0, 1, 1, 1, 1, 2}, maximum{1, 2, 2, 2, 2, 3};
            CHECK(f.runtime.world().globals.barbarian_activity ==
                  (code == 0 ? zero : maximum)[static_cast<std::size_t>(level)]);
            CHECK(f.process.ordinary_preferences.opponents == 0);
            CHECK(f.process.ordinary_preferences.barbarians == 2);
            random.require_choices_consumed();
        }
}

TEST_CASE("F05 random choice identities advance across difficulty retries",
          "[player-setup][session][random]") {
    Fixture f;
    f.services.opponents = [](auto) -> SetupNumericChoice { return RandomSetupChoice{}; };
    unsigned activities = 0;
    f.services.barbarians = [&](auto) -> SetupNumericChoice {
        return ++activities == 1 ? SetupNumericChoice{-1} : SetupNumericChoice{RandomSetupChoice{}};
    };
    f.services.rules = [](auto) { return -1; };
    base::GameplayRandom random{
        base::ControlledChoices{{choice("opponent-count", 0, 5, 4),
                                 choice("opponent-count", 1, 5, 5), choice("barbarians", 0, 3, 2)}},
        "setup"};
    CHECK_FALSE(f.run(random).accepted);
    CHECK(f.runtime.world().globals.num_enemies == 2);
    CHECK(f.runtime.world().globals.barbarian_activity == 3);
    random.require_choices_consumed();
}

TEST_CASE("F05 advanced checks map every rule combination and preserve premade preference topology",
          "[player-setup][session]") {
    for (const auto source :
         {WorldSource::generated, WorldSource::premade_cylindrical, WorldSource::premade_flat})
        for (const auto old_flat : {false, true})
            for (unsigned mask = 0; mask < 32; ++mask) {
                Fixture f;
                auto& p = f.process.ordinary_preferences;
                p.rules = static_cast<std::uint16_t>(old_flat ? 0x819f : 0x019f);
                p.accelerated_start = 2;
                const SetupAdvancedChoices checks{(mask & 1) != 0, (mask & 2) != 0,
                                                  (mask & 4) != 0, false,
                                                  (mask & 8) != 0, (mask & 16) != 0};
                f.custom_rules(checks);
                f.services.advanced = [&](auto defaults) {
                    CHECK(defaults ==
                          SetupAdvancedChoices{false,
                                               source == WorldSource::generated
                                                   ? old_flat
                                                   : source == WorldSource::premade_flat,
                                               false, true, true, true});
                    return SetupAdvancedResponse{-1, checks};
                };
                unsigned levels = 0;
                f.services.difficulty = [&](auto) { return ++levels == 1 ? 2 : -1; };
                f.services.gender = [](auto) { return -1; };
                auto random = f.slots({});
                const auto result = f.run(random, source);
                const auto active = 0x2fU | ((mask & 1) ? 0 : 0x10U) | ((mask & 2) ? 0x8000U : 0) |
                                    ((mask & 8) ? 0x80U : 0) | ((mask & 16) ? 0x100U : 0);
                CHECK_FALSE(result.accepted);
                CHECK(result.pick_opponents == ((mask & 4) != 0));
                CHECK(result.acceleration == 0);
                CHECK(f.runtime.world().globals.rules == active);
                CHECK(p.rules == (source == WorldSource::generated
                                      ? active
                                      : (active & 0x7fffU) | (old_flat ? 0x8000U : 0)));
                CHECK(p.rules_choice == 1);
                CHECK(p.accelerated_start == 0);
            }
}

TEST_CASE("F05 acceleration cancel then Standard retains negative local and remembered values",
          "[player-setup][session][calendar]") {
    Fixture f;
    auto& p = f.process.ordinary_preferences;
    p.rules = 0xbf;
    p.accelerated_start = 2;
    f.runtime.world().globals.game_flags = 4;
    f.runtime.world().globals.first_launch_turn = 44;
    f.custom_rules({false, false, true, true, true, false});
    unsigned rules = 0;
    f.services.rules = [&](auto remembered) {
        CHECK(remembered == (rules == 0 ? 0 : 1));
        return ++rules == 1 ? 1 : 0;
    };
    f.services.acceleration = [&](auto years, auto remembered) {
        CHECK(years == std::array<std::uint32_t, 3>{4000, 3600, 3200});
        CHECK(remembered == 2);
        CHECK(f.runtime.world().globals.first_launch_turn == -1);
        return -7;
    };
    auto random = f.slots();
    const auto result = f.run(random);
    CHECK(result.accepted);
    CHECK(result.pick_opponents);
    CHECK(result.acceleration == -7);
    CHECK(p.accelerated_start == 2);
    CHECK(p.rules_choice == 0);
    CHECK(p.rules == 0xbf);
    CHECK(f.runtime.world().globals.rules == 0x3f);
    CHECK(f.visits[f.visits.size() - 2] == "opponent:2");
    CHECK(f.visits.back() == "opponent:7");
    random.require_choices_consumed();
}

TEST_CASE("F05 accepted acceleration and pick flags survive Gender returning to Difficulty",
          "[player-setup][session]") {
    Fixture f;
    f.custom_rules({false, false, true, true, false, false});
    unsigned rules = 0, genders = 0;
    f.services.rules = [&](auto) { return ++rules == 1 ? 1 : 0; };
    f.services.acceleration = [](auto, auto) { return 2; };
    f.services.gender = [&](auto) { return ++genders == 1 ? -1 : 1; };
    auto random = f.slots();
    const auto result = f.run(random);
    CHECK(result.accepted);
    CHECK(result.pick_opponents);
    CHECK(result.acceleration == 2);
    CHECK(f.process.ordinary_preferences.accelerated_start == 2);
    CHECK(rules == 2);
    CHECK(genders == 2);
    random.require_choices_consumed();
}

TEST_CASE("F05 repeated advanced entry starts Pick Opponents unchecked and replaces local choices",
          "[player-setup][session]") {
    Fixture f;
    f.custom_rules({false, true, true, true, true, true});
    unsigned advanced = 0;
    f.services.advanced = [&](auto defaults) {
        CHECK_FALSE(defaults[2]);
        if (++advanced == 1)
            return SetupAdvancedResponse{0, {false, true, true, true, true, true}};
        CHECK(defaults == SetupAdvancedChoices{false, true, false, false, true, true});
        return SetupAdvancedResponse{0, {true, false, false, false, false, false}};
    };
    f.services.acceleration = [](auto, auto) { return -1; };
    auto random = f.slots();
    const auto result = f.run(random);
    CHECK(result.accepted);
    CHECK_FALSE(result.pick_opponents);
    CHECK(result.acceleration == 0);
    CHECK(f.runtime.world().globals.rules == 0x2f);
    CHECK(advanced == 2);
    random.require_choices_consumed();
}

TEST_CASE("F05 custom name title and portrait children retain accepted edits in order",
          "[player-setup][session]") {
    Fixture f;
    f.custom_tribe();
    unsigned names = 0, titles = 0;
    f.services.names = [&](bool custom, auto current) {
        REQUIRE(custom);
        auto& leader = f.process.rules->leaders[3];
        CHECK(leader.plural < 0);
        CHECK(leader.adjective < 0);
        for (unsigned g = 0; g < 7; ++g) {
            CHECK(leader.titles[g][0] > 0);
            CHECK(leader.titles[g][1] < 0);
        }
        if (++names == 1) {
            CHECK(current == std::array<std::string, 3>{"Female3", "Tribe3", "Adjective3"});
            return SetupNameResponse{SetupNameAction::titles, {"Probe", "People", "Descriptive"}};
        }
        CHECK(current == std::array<std::string, 3>{"Probe", "People", "Descriptive"});
        return SetupNameResponse{names == 2   ? SetupNameAction::titles
                                 : names == 3 ? SetupNameAction::portrait
                                              : SetupNameAction::accept,
                                 current};
    };
    f.services.titles = [&](auto current) {
        CHECK(text(f.runtime.world().names[4].leader) == "Probe");
        CHECK(current[0] == "F0/3");
        if (++titles == 1)
            return SetupTitleResponse{SetupTitleResponse::Status::cancel, {"Discard"}};
        return SetupTitleResponse{SetupTitleResponse::Status::accept,
                                  {"Zero", "One", "Two", "Three", "Four", "Five", "Six"}};
    };
    f.services.portrait = [&](auto slot) {
        CHECK(slot == state::CivilizationSlot{4});
        CHECK(text(f.runtime.world().names[4].titles[6]) == "Six");
        f.runtime.world().civilizations[4].leader_portrait = 17;
    };
    auto random = f.slots();
    CHECK(f.run(random).accepted);
    CHECK(names == 4);
    CHECK(titles == 2);
    CHECK(f.runtime.world().civilizations[4].leader_portrait == 17);
    CHECK(text(f.runtime.world().names[4].titles[0]) == "Zero");
    CHECK(text(f.runtime.world().names[4].adjective) == "Descriptive");
    random.require_choices_consumed();
}

TEST_CASE("F05 canceling names restores markers at Gender while keeping custom text buffers",
          "[player-setup][session]") {
    Fixture f;
    f.custom_tribe();
    unsigned genders = 0, names = 0, difficulties = 0;
    f.services.difficulty = [&](auto) { return ++difficulties == 1 ? 2 : -1; };
    f.services.gender = [&](auto) {
        if (++genders == 1)
            return 1;
        const auto& leader = f.process.rules->leaders[3];
        CHECK(leader.leader_name == leader.names[1]);
        CHECK(leader.plural > 0);
        CHECK(leader.adjective > 0);
        CHECK(leader.titles[3][1] > 0);
        CHECK(text(f.runtime.world().names[4].leader) == "BeforeCancel");
        return -1;
    };
    f.services.names = [&](auto, auto) {
        if (++names == 1)
            return SetupNameResponse{SetupNameAction::titles,
                                     {"BeforeCancel", "People", "Descriptive"}};
        return SetupNameResponse{SetupNameAction::cancel, {"Discard", "Discard", "Discard"}};
    };
    f.services.titles = [](auto) { return SetupTitleResponse{}; };
    auto random = f.slots({});
    CHECK_FALSE(f.run(random).accepted);
    CHECK(genders == 2);
    CHECK(text(f.runtime.world().names[4].tribe) == "People");
    CHECK(f.runtime.world().globals.player_civ == 4);
}

TEST_CASE("F05 tribe and city-style cancellation return to Gender without roster draws",
          "[player-setup][session]") {
    for (bool cancel_style : {false, true}) {
        Fixture f;
        unsigned genders = 0, difficulties = 0;
        f.services.difficulty = [&](auto) { return ++difficulties == 1 ? 2 : -1; };
        f.services.gender = [&](auto) { return ++genders == 1 ? 1 : -1; };
        if (cancel_style)
            f.services.city_style = [](auto, auto) { return -1; };
        else
            f.services.tribe = [](auto, auto) { return SetupTribeChoice{-1, false}; };
        auto random = f.slots({});
        CHECK_FALSE(f.run(random).accepted);
        CHECK(genders == 2);
        CHECK(f.runtime.world().globals.civs_alive == 0);
        CHECK(f.process.ordinary_preferences.gender == 1);
        CHECK(f.process.ordinary_preferences.tribe == (cancel_style ? 3 : 0));
        random.require_choices_consumed();
    }
}

TEST_CASE("F05 text-load failures stop with preceding identity and name effects retained",
          "[player-setup][session]") {
    for (bool title_failure : {false, true}) {
        Fixture f;
        f.custom_tribe();
        f.services.names = [=](auto, auto current) {
            current[0] = "BeforeFailure";
            return SetupNameResponse{
                title_failure ? SetupNameAction::titles : SetupNameAction::unavailable, current};
        };
        f.services.titles = [](auto) {
            return SetupTitleResponse{SetupTitleResponse::Status::unavailable, {}};
        };
        auto random = f.slots({});
        CHECK_FALSE(f.run(random).accepted);
        CHECK(f.runtime.world().globals.player_civ == 4);
        CHECK(f.process.rules->leaders[3].leader_name < 0);
        CHECK(text(f.runtime.world().names[4].leader) ==
              (title_failure ? "BeforeFailure" : "Female3"));
        CHECK(f.runtime.world().globals.civs_alive == 0);
    }
}

TEST_CASE("F05 names accept exact classic byte limits and retain reached copies on invalid input",
          "[player-setup][session]") {
    Fixture f;
    f.custom_tribe();
    const std::string maximum(23, static_cast<char>(0xff));
    f.services.names = [&](auto, auto current) {
        current[0] = maximum;
        current[1] = std::string(24, 'x');
        return SetupNameResponse{SetupNameAction::accept, current};
    };
    auto random = f.slots({});
    CHECK_THROWS_AS(f.run(random), std::out_of_range);
    CHECK(text(f.runtime.world().names[4].leader) == maximum);
    CHECK(f.runtime.world().names[4].leader.bytes[23] == 0);
    CHECK(text(f.runtime.world().names[4].tribe) == "Tribe3");
    f.services.names = [](auto, auto current) {
        current[0] = std::string{"a\0b", 3};
        return SetupNameResponse{SetupNameAction::accept, current};
    };
    CHECK_THROWS_AS(f.run(random), std::out_of_range);
    CHECK(text(f.runtime.world().names[4].leader) == "Female3");
}

TEST_CASE("F05 opponent cancellation redraws slots and Random preserves earlier overrides",
          "[player-setup][session][random]") {
    Fixture f;
    f.custom_rules({false, false, true, false, false, false});
    auto& world = f.runtime.world();
    world.pending_leader_overrides[6] = 12;
    unsigned styles = 0, opponents = 0;
    f.services.city_style = [&](auto preferred, auto current) {
        CHECK(preferred == 3);
        if (++styles == 1) {
            CHECK(current == 0);
            return 3;
        }
        CHECK(current == 3);
        CHECK(world.globals.civs_alive == 0x95);
        CHECK(world.pending_leader_overrides[2] == 8);
        return 1;
    };
    f.services.opponent_tribe = [&](auto slot, auto options) -> SetupNumericChoice {
        ++opponents;
        CHECK(options.size() == 3);
        for (std::size_t i = 0; i < options.size(); ++i) {
            CHECK(options[i].preferred_color == static_cast<std::int16_t>(slot));
            CHECK(options[i].tribe ==
                  static_cast<std::int32_t>(slot) - 1 + 7 * static_cast<std::int32_t>(i));
        }
        if (opponents == 1) {
            CHECK(slot == state::CivilizationSlot{2});
            return 8;
        }
        if (opponents == 2) {
            CHECK(slot == state::CivilizationSlot{7});
            return -1;
        }
        if (opponents == 3) {
            CHECK(slot == state::CivilizationSlot{2});
            return RandomSetupChoice{};
        }
        CHECK(slot == state::CivilizationSlot{3});
        return 9;
    };
    auto random = f.slots({2, 7, 0, 4, 2, 2, 3});
    CHECK(f.run(random).accepted);
    CHECK(styles == 2);
    CHECK(opponents == 4);
    CHECK(world.globals.civs_alive == 0x1d);
    CHECK(world.names[4].city_style == 1);
    CHECK(world.pending_leader_overrides ==
          std::array<std::int8_t, 8>{-1, -1, 8, 9, -1, -1, 12, -1});
    random.require_choices_consumed();
}

TEST_CASE("F05 maximum opponents retain all rejected draws and select all eight alive bits",
          "[player-setup][session][random]") {
    Fixture f;
    f.services.opponents = [](auto) -> SetupNumericChoice { return 0; };
    auto random = f.slots({0, 4, 1, 1, 2, 3, 5, 6, 7});
    CHECK(f.run(random).accepted);
    CHECK(f.runtime.world().globals.civs_alive == 0xff);
    random.require_choices_consumed();
}

TEST_CASE("F05 unavailable controlled choices preserve the admitted roster prefix",
          "[player-setup][session][random]") {
    Fixture f;
    auto random = f.slots({2, 2, 0});
    CHECK_THROWS_AS(f.run(random), base::ChoiceError);
    CHECK(f.runtime.world().globals.civs_alive == 0x15);
    CHECK(f.runtime.world().names[4].city_style == 2);
    CHECK(f.runtime.world().globals.player_civ == 4);
    random.require_choices_consumed();
}

TEST_CASE("F05 unsupported choices stop at consumption without undoing prior state",
          "[player-setup][session]") {
    Fixture f;
    f.services.barbarians = [](auto) -> SetupNumericChoice { return 4; };
    auto random = f.slots({});
    CHECK_THROWS_AS(f.run(random), std::out_of_range);
    CHECK(f.process.ordinary_preferences.opponents == 4);
    CHECK(f.runtime.world().globals.num_enemies == 2);
    CHECK(f.runtime.world().globals.barbarian_activity == 0);
    Fixture g;
    g.process.rules->leaders[3].color = 8;
    g.services.tribe = [](auto, auto) { return SetupTribeChoice{3, false}; };
    CHECK_THROWS_AS(g.run(random), std::out_of_range);
    CHECK(g.process.ordinary_preferences.tribe == 3);
    CHECK(g.runtime.world().globals.human_players == 0);
    Fixture h;
    h.custom_rules({false, false, true, false, false, false});
    h.services.opponent_tribe = [](auto, auto) -> SetupNumericChoice { return 3; };
    auto roster = h.slots();
    CHECK_THROWS_AS(h.run(roster), std::out_of_range);
    CHECK(h.runtime.world().globals.civs_alive == 0x95);
    CHECK(h.runtime.world().pending_leader_overrides[2] == -1);
}

TEST_CASE(
    "F05 setup publishes at child and exceptional exits without drawing presentation randomness",
    "[player-setup][session]") {
    Fixture f;
    f.runtime.begin_client_session({10, 20});
    auto& unit = f.runtime.world().entities.units.storage()[0];
    unit.token = state::UnitToken{1};
    unit.owner = state::CivilizationSlot{4};
    f.runtime.world().entities.units.set_end(1);
    const auto client = f.runtime.bind(f.runtime.add_participant(state::CivilizationSlot{4}),
                                       ControllerKind::frontend);
    const auto presentation = f.runtime.presentation_random().checkpoint();
    const auto gameplay = f.runtime.gameplay_random().checkpoint();
    f.services.names = [&](auto, auto current) {
        unit.x = 123;
        return SetupNameResponse{SetupNameAction::accept, current};
    };
    f.services.city_style = [&](auto, auto) -> std::int32_t {
        REQUIRE(client.own_units().records.size() == 1);
        CHECK(client.own_units().records[0].x == 123);
        unit.x = 234;
        throw std::runtime_error("authored child interruption");
    };
    auto random = f.slots({});
    CHECK_THROWS_AS(f.run(random), std::runtime_error);
    CHECK(client.own_units().records[0].x == 234);
    CHECK(f.runtime.presentation_random().checkpoint() == presentation);
    CHECK(f.runtime.gameplay_random().checkpoint() == gameplay);
    CHECK(f.runtime.active());
}
