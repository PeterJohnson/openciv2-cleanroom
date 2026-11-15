#include "service_callbacks.hpp"
// F05 section 2 composes F03/F06, F05 sections 3-5 and V03 section 2.
// Authored rules, binary maps and controlled choices; W02, V01, balancing,
// acceleration and presentation children are explicit supplied test services.
#include "service_fixture.hpp"
#include "classic_stream_fixture.hpp"
#include "session/world_entry.hpp"
#include "formats/classic_rules.hpp"
#include "rules/rules_install.hpp"
#include "formats/content_pack.hpp"
#include "formats/preference_io.hpp"
#include "classic_rule_fixture.hpp"
#include <catch2/catch_test_macros.hpp>
#include <algorithm>

using namespace openciv;
using namespace openciv::session;
namespace classic = formats::classic;
namespace {
struct Fixture {
    state::ProcessContext process;
    SessionRuntime runtime{process, base::RuntimeId{99}};
    ClassicFileState files;
    WorldEntryCallbacks services;
    classic::TextWorkspace workspace;
    classic::TextDirectories directories{"home", "home", "home"};
    std::string rules_text;
    std::unique_ptr<classic::TextReader> reader;
    std::vector<std::string> trace;
    std::vector<formats::Bytes> saved;
    formats::Bytes map_input;
    std::function<void(std::string_view)> observe;
    std::string introduced;
    Fixture() {
        test::fail_service(services.size.custom);
        test::fail_service(services.players.advanced);
        test::fail_service(services.players.acceleration);
        test::fail_service(services.players.titles);
        test::fail_service(services.players.portrait);
        test::fail_service(services.players.opponent_tribe);
        test::fail_service(services.premade.choices.use_start_locations);
        test::fail_service(services.premade.choices.ticks);

        state::default_ordinary_preferences(process.ordinary_preferences, {});
        process.rules->text.initialize();
        for (const auto& section : classic_rule_fixture::complete_sections())
            rules_text += section;
        reader = std::make_unique<classic::TextReader>(
            workspace, directories,
            classic::TextFiles{[&](auto, auto) {
                                   return classic::OpenedText{
                                       "authored",
                                       std::make_unique<classic::MemoryTextStream>(rules_text)};
                               },
                               {}},
            classic::ascii_text_case());
        services.load_content = [&] {
            event("rules");
            auto input = std::make_unique<state::RuleInput>();
            classic::RuleLoader(*reader, *input, classic::ascii_text_case()).load("RULES");
            reader->close();
            return state::LoadedContent{std::move(input), {}};
        };
        services.size.preset = [&](auto) -> WorldSizeChoice {
            event("size");
            return 0;
        };
        services.parameters.choose = [&](auto parameter, auto) -> WorldParameterChoice {
            event("parameter:" + std::to_string(static_cast<unsigned>(parameter)));
            return 2;
        };
        services.parameters.preferences.open_output = [&](auto path) {
            CHECK(path == "CIV2.DAT");
            event("save");
            saved.emplace_back();
            return test::output_file({[&](std::span<const formats::Byte> bytes) {
                                          event("write:" + std::to_string(bytes.size()));
                                          saved.back().insert(saved.back().end(), bytes.begin(),
                                                              bytes.end());
                                          return true;
                                      },
                                      [&] {
                                          event("close-preferences");
                                          return true;
                                      }});
        };
        services.parameters.preferences.remove_file = [&](auto) {
            event("delete");
            return false;
        };
        auto& players = services.players;
        players.difficulty = [&](auto) {
            event("difficulty");
            return 2;
        };
        players.opponents = [&](auto) -> SetupNumericChoice {
            event("opponents");
            return 4;
        };
        players.barbarians = [&](auto) -> SetupNumericChoice {
            event("barbarians");
            return 1;
        };
        players.rules = [&](auto) {
            event("player-rules");
            return 0;
        };
        players.gender = [&](auto) {
            event("gender");
            return 0;
        };
        players.tribe = [&](auto, auto) {
            event("tribe");
            return SetupTribeChoice{0, false};
        };
        players.names = [&](auto, auto names) {
            event("names");
            return SetupNameResponse{SetupNameAction::accept, names};
        };
        players.city_style = [&](auto, auto) {
            event("style");
            return 1;
        };
        services.stop_menu_sound = [&] { event("stop-sound"); };
        services.generate_world = [&](bool keep) {
            event(keep ? "retain-world" : "generate-world");
            if (!keep)
                runtime.world().map.allocate(
                    {runtime.world().world_setup.width, runtime.world().world_setup.height});
        };
        services.initialize.create_civilization = [&](auto slot) {
            event("create:" + std::to_string(slot));
            auto& civ = runtime.world().civilizations[static_cast<std::size_t>(slot)];
            if (slot == runtime.world().globals.player_civ)
                for (unsigned advance : {0U, 1U, 89U, 95U})
                    civ.advances[advance / 8] |= static_cast<std::uint8_t>(1U << (advance % 8));
            return -1; // Parent must continue despite a child's returned failure.
        };
        services.initialize.balance_starts = [&] { event("balance"); };
        services.accelerate = [&](auto selection) {
            event("accelerate:" + std::to_string(selection));
            runtime.world().globals.turn = 41; // Declared supplied-child effect.
        };
        services.introduce = [&](auto slot, std::string advances) {
            CHECK(slot == state::CivilizationSlot{1});
            introduced = std::move(advances);
            event("introduce");
        };
        services.choose_map = [&]() -> std::optional<std::string> {
            event("choose-map");
            return "authored.mp";
        };
        services.premade.open = [&](auto path) {
            CHECK(path == "authored.mp");
            event("open-map");
            auto input = std::make_shared<formats::MemoryItems>(map_input);
            return test::input_file({[input](auto count) { return input->read(count); },
                                     {},
                                     [&] {
                                         event("close-map");
                                         return true;
                                     }});
        };
        services.premade.load_failure = [&] { event("map-failure"); };
        services.premade.choices.use_seed = [&] {
            event("use-seed");
            return 1;
        };
        formats::RawMapFile map;
        map.header = {4, 2, 4, 0, 29, 1, 1};
        map.tiles.resize(4);
        map_input = formats::encode_raw_map(map);
    }
    void event(std::string name) {
        trace.push_back(name);
        if (observe)
            observe(name);
    }
    bool run(base::GameplayRandom& random, NewWorldChoice choice = NewWorldChoice::random,
             std::string_view call = "entry") {
        return enter_new_world(runtime, files, random, call, choice,
                               formats::content::builtin_gameplay()->values.calendar, services);
    }
};
base::ControlledChoices choices(bool parameters = true, bool players = true, bool initialize = true,
                                std::string call = "entry") {
    base::ControlledChoices result;
    if (parameters) {
        unsigned code = 0;
        for (const auto site : {"land-mass", "land-form", "temperature", "climate", "age"})
            result.records.push_back(
                base::make_choice({"F05.world." + std::string(site), call + "/parameters", 0},
                                  base::PrimaryModulo{3}, static_cast<std::int32_t>(code++)));
    }
    if (players) {
        unsigned ordinal = 0;
        for (const auto code : {1, 0, 3, 3, 5})
            result.records.push_back(base::make_choice(
                {"F05.setup.opponent-slot", call + "/players/" + std::to_string(ordinal++), 0},
                base::PrimaryModulo{8}, code));
    }
    if (initialize)
        result.records.push_back(base::make_choice(
            {"F05.new-game.history", call + "/initialize", 0}, base::PrimaryModulo{50}, 49));
    return result;
}
} // namespace

TEST_CASE("F05 world entry orders real rules preferences wizard and inner initialization",
          "[world-entry][session]") {
    Fixture f;
    f.process.ordinary_preferences.science = f.process.ordinary_preferences.tax = 8;
    f.runtime.world().globals.turn = 77;
    base::GameplayRandom random{choices(), "entry-test"};
    REQUIRE(f.run(random));
    random.require_choices_consumed();
    CHECK(f.trace == std::vector<std::string>{"rules",      "size",
                                              "difficulty", "opponents",
                                              "barbarians", "player-rules",
                                              "gender",     "tribe",
                                              "names",      "style",
                                              "save",       "write:72",
                                              "write:444",  "close-preferences",
                                              "stop-sound", "generate-world",
                                              "create:0",   "create:1",
                                              "create:2",   "create:3",
                                              "create:4",   "create:5",
                                              "create:6",   "create:7",
                                              "balance",    "introduce"});
    REQUIRE(f.saved.size() == 1);
    REQUIRE(f.saved[0].size() == 516);
    const auto saved = formats::decode_ordinary_preferences(std::span{f.saved[0]}.first(72));
    CHECK(saved.science == 8); // Save precedes the initializer's rate correction.
    CHECK(saved.tax == 8);
    CHECK(saved.difficulty == 2);
    CHECK(saved.opponents == 4);
    CHECK(f.process.ordinary_preferences.science == 6);
    CHECK(f.process.ordinary_preferences.tax == 4);
    CHECK(f.runtime.world().globals.turn == 0);
    CHECK(f.runtime.world().globals.year == -4000);
    CHECK(f.runtime.world().globals.civs_at_start == 0x2b);
    CHECK(f.runtime.world().globals.next_history_turn == 49);
    CHECK(f.runtime.world().world_setup.parameters == std::array<std::int32_t, 5>{-1, 0, 1, -1, 0});
    CHECK(f.runtime.world().map.geometry().area() == 2000);
    CHECK(f.introduced == "advances 1, advances 95, ");
    CHECK_FALSE(f.runtime.active());
}

TEST_CASE("F05 custom world has distinct parameter and player acceptance preference saves",
          "[world-entry][session]") {
    Fixture f;
    base::GameplayRandom random{choices(false), "entry-test"};
    REQUIRE(f.run(random, NewWorldChoice::customize));
    random.require_choices_consumed();
    REQUIRE(f.saved.size() == 2);
    const auto early = formats::decode_ordinary_preferences(std::span{f.saved[0]}.first(72));
    const auto late = formats::decode_ordinary_preferences(std::span{f.saved[1]}.first(72));
    CHECK(early.world_parameters == std::array<std::int16_t, 5>{1, 1, 1, 1, 1});
    CHECK(early.difficulty == 0);
    CHECK(late.difficulty == 2);
    CHECK(std::vector<std::string>(f.trace.begin() + 2, f.trace.begin() + 7) ==
          std::vector<std::string>{"parameter:0", "parameter:1", "parameter:3", "parameter:2",
                                   "parameter:4"});
}

TEST_CASE("F05 failed size and premade selection return without discarded parameter choices",
          "[world-entry][session]") {
    for (const auto kind :
         {NewWorldChoice::random, NewWorldChoice::customize, NewWorldChoice::premade}) {
        Fixture f;
        f.services.size.preset = [&](auto) -> WorldSizeChoice {
            f.event("cancel-size");
            return -1;
        };
        f.services.choose_map = [&]() -> std::optional<std::string> {
            f.event("cancel-map");
            return {};
        };
        f.runtime.world().world_setup.width = 50;
        f.runtime.world().world_setup.height = 80;
        base::GameplayRandom random{choices(false, false, false), "entry-test"};
        CHECK_FALSE(f.run(random, kind));
        random.require_choices_consumed();
        CHECK(f.trace == std::vector<std::string>{"rules", kind == NewWorldChoice::premade
                                                               ? "cancel-map"
                                                               : "cancel-size"});
        CHECK(f.saved.empty());
        CHECK(f.runtime.world().world_setup.parameters == std::array<std::int32_t, 5>{});
        CHECK(f.runtime.world().world_setup.width == 0);
    }
}

TEST_CASE("F05 returned premade read failure closes and reports without parameter draws",
          "[world-entry][session]") {
    Fixture f;
    f.map_input.resize(20);
    f.runtime.world().map.pattern_seed = 73;
    base::GameplayRandom random{choices(false, false, false), "entry-test"};
    CHECK_FALSE(f.run(random, NewWorldChoice::premade));
    random.require_choices_consumed();
    CHECK(f.trace ==
          std::vector<std::string>{"rules", "choose-map", "open-map", "close-map", "map-failure"});
    CHECK(f.runtime.world().map.stored_topology == 0);
    CHECK(f.runtime.world().map.pattern_seed == 0);
    CHECK(f.saved.empty());
}

TEST_CASE("F05 premade topology reaches wizard defaults and both sources retain terrain in W02",
          "[world-entry][session]") {
    for (const auto flat : {false, true}) {
        Fixture f;
        auto map = formats::decode_raw_map(f.map_input);
        map.header.topology = flat ? 1 : 0;
        f.map_input = formats::encode_raw_map(map);
        f.process.ordinary_preferences.rules = flat ? 0x3f : 0x803f;
        f.services.players.rules = [](auto) { return 1; };
        f.services.players.advanced = [flat](auto defaults) {
            CHECK(defaults[1] == flat);
            defaults[1] = !flat; // The accepted active rule, not source topology, drives wrapping.
            return SetupAdvancedResponse{0, defaults};
        };
        bool generated = false;
        f.services.generate_world = [&](bool keep) {
            generated = true;
            CHECK(keep);
            CHECK(((f.runtime.world().globals.rules & 0x8000) != 0) == !flat);
            CHECK(f.runtime.world().map.stored_topology == (flat ? 1 : 0));
            CHECK(f.runtime.world().map.geometry().area() == 4);
        };
        base::GameplayRandom random{choices(), "entry-test"};
        REQUIRE(f.run(random, NewWorldChoice::premade));
        random.require_choices_consumed();
        CHECK(generated);
        CHECK(std::find(f.trace.begin(), f.trace.end(), "size") == f.trace.end());
        CHECK((f.process.ordinary_preferences.rules & 0x8000) == (flat ? 0 : 0x8000));
    }
}

TEST_CASE("F05 custom parameter and player cancellations retain their different save prefixes",
          "[world-entry][session]") {
    for (bool cancel_parameters : {false, true}) {
        Fixture f;
        if (cancel_parameters)
            f.services.parameters.choose = [](auto parameter, auto) -> WorldParameterChoice {
                return parameter == state::WorldParameter::climate ? -2 : 2;
            };
        else
            f.services.players.difficulty = [](auto) { return -1; };
        base::GameplayRandom random{choices(false, false, false), "entry-test"};
        CHECK_FALSE(f.run(random, NewWorldChoice::customize));
        random.require_choices_consumed();
        CHECK(f.saved.size() == (cancel_parameters ? 0 : 1));
        CHECK(f.runtime.world().world_setup.parameters[0] == 1);
        CHECK(f.runtime.world().world_setup.parameters[3] == (cancel_parameters ? -3 : 1));
        CHECK(std::find(f.trace.begin(), f.trace.end(), "CIVILIZE") == f.trace.end());
    }
}

TEST_CASE("F05 preference open and write failures do not cancel accepted setup",
          "[world-entry][session]") {
    for (bool fail_open : {false, true}) {
        Fixture f;
        f.services.parameters.preferences.open_output = [&](auto) -> std::unique_ptr<CoreOutput> {
            f.event("save");
            if (fail_open)
                return {};
            return test::output_file({[&](auto) {
                                          f.event("failed-write");
                                          return false;
                                      },
                                      [&] {
                                          f.event("close-preferences");
                                          return false;
                                      }});
        };
        base::GameplayRandom random{choices(), "entry-test"};
        REQUIRE(f.run(random));
        random.require_choices_consumed();
        CHECK(f.trace.back() == "introduce");
        CHECK(std::count(f.trace.begin(), f.trace.end(), "delete") == (fail_open ? 0 : 1));
    }
}

TEST_CASE("F05 final table installation reuses owned source data after setup changes",
          "[world-entry][session]") {
    Fixture f;
    unsigned sources = 0;
    f.observe = [&](auto stage) {
        if (stage == "rules")
            ++sources;
        if (stage == "save") {
            f.process.rules->definitions.advances[1].ai_value = 99;
            f.process.rules->definitions.units[1].attack = 77;
            f.rules_text.clear(); // No reader/reload protocol survives source return.
        }
        if (stage == "stop-sound") {
            CHECK(f.process.rules->definitions.advances[1].ai_value == 3);
            CHECK(f.process.rules->definitions.units[1].attack != 77);
            CHECK_FALSE(f.reader->is_open());
        }
    };
    base::GameplayRandom random{choices(), "entry-test"};
    REQUIRE(f.run(random));
    CHECK(sources == 1);
    random.require_choices_consumed();
}

TEST_CASE("F05 retained negative acceleration reaches its required child after initialization",
          "[world-entry][session]") {
    Fixture f;
    unsigned rules = 0;
    f.services.players.rules = [&](auto) { return ++rules == 1 ? 1 : 0; };
    f.services.players.advanced = [](auto defaults) {
        defaults[3] = true;
        return SetupAdvancedResponse{0, defaults};
    };
    f.services.players.acceleration = [](auto years, auto) {
        CHECK(years == std::array<std::uint32_t, 3>{4000, 3000, 2000});
        return -7;
    };
    f.services.accelerate = [&](auto choice) {
        CHECK(choice == -7);
        CHECK(f.runtime.world().globals.turn == 0);
        CHECK(f.runtime.world().globals.civs_at_start == 0x2b);
        f.runtime.world().globals.turn = 41;
        f.runtime.world().civilizations[1].advances[12] |= 8; // Advance 99.
        f.event("acceleration");
    };
    base::GameplayRandom random{choices(), "entry-test"};
    REQUIRE(f.run(random));
    random.require_choices_consumed();
    CHECK(f.trace[f.trace.size() - 2] == "acceleration");
    CHECK(f.runtime.world().globals.turn == 41);
    CHECK(f.runtime.world().globals.year == -4000); // Introduction does not update it.
    CHECK(f.introduced == "advances 1, advances 95, advances 99, ");
}

TEST_CASE("F05 child failures retain current-attempt effects and revoke previous clients",
          "[world-entry][session]") {
    for (const auto stop : {"generate-world", "create:3", "balance", "introduce"}) {
        Fixture f;
        f.runtime.begin_client_session({17, 29});
        auto& unit = f.runtime.world().entities.units.storage()[0];
        unit.token = state::UnitToken{1};
        unit.owner = state::CivilizationSlot{1};
        f.runtime.world().entities.units.set_end(1);
        const auto client = f.runtime.bind(f.runtime.add_participant(state::CivilizationSlot{1}),
                                           ControllerKind::frontend);
        f.observe = [&](auto stage) {
            if (stage == stop) {
                f.runtime.world().entities.units.set_end(1);
                f.runtime.world().entities.units.storage()[0].x = 123;
                throw std::runtime_error("authored child failure");
            }
        };
        const bool generated = std::string_view(stop) != "generate-world";
        base::GameplayRandom random{choices(true, true, generated), "entry-test"};
        CHECK_THROWS_AS(f.run(random), std::runtime_error);
        random.require_choices_consumed();
        CHECK(f.trace.back() == stop);
        CHECK_THROWS_AS(client.own_units(), AccessError);
        CHECK(f.runtime.world().entities.units.storage()[0].x == 123);
        CHECK_FALSE(f.runtime.active());
        CHECK_THROWS_AS(f.runtime.gameplay_random(), AccessError);
        CHECK_THROWS_AS(f.runtime.presentation_random(), AccessError);
    }
}

TEST_CASE("F05 second entry uses distinct choice identities and fresh name cursors",
          "[world-entry][session]") {
    Fixture f;
    auto tape = choices();
    const auto second = choices(true, true, true, "second");
    tape.records.insert(tape.records.end(), second.records.begin(), second.records.end());
    base::GameplayRandom random{std::move(tape), "entry-test"};
    REQUIRE(f.run(random));
    f.process.rules->leaders[0].city_name_index = 12;
    REQUIRE(f.run(random, NewWorldChoice::random, "second"));
    random.require_choices_consumed();
    CHECK(f.process.rules->leaders[0].city_name_index == 0);
    CHECK(f.saved.size() == 2);
}

TEST_CASE("F05 fresh and reused processes produce the same new world from identical inputs",
          "[world-entry][session][isolation]") {
    Fixture fresh, reused;
    SECTION("previous completed game") {
        base::GameplayRandom previous{choices(), "entry-test"};
        REQUIRE(reused.run(previous));
    }
    SECTION("cancelled setup") {
        const auto choose = reused.services.size.preset;
        reused.services.size.preset = [](auto) -> WorldSizeChoice { return -1; };
        base::GameplayRandom cancelled{choices(false, false, false), "entry-test"};
        CHECK_FALSE(reused.run(cancelled));
        reused.services.size.preset = choose;
    }
    SECTION("failed premade load") {
        const auto input = reused.map_input;
        reused.map_input.resize(20);
        base::GameplayRandom failed{choices(false, false, false), "entry-test"};
        CHECK_FALSE(reused.run(failed, NewWorldChoice::premade));
        reused.map_input = input;
    }
    // Poison both live and out-of-scan backing, including state historically
    // classified as process-retained. Only explicit preferences/content survive.
    auto& old = reused.runtime.world();
    old.globals.peace_turns = 87;
    old.globals.saved_current_civ = 6;
    old.civilizations[1].future_techs = 19;
    old.civilizations[1].goals.fill({8, 8, 3, 90});
    old.civilizations[1].standing_goals.fill({4, 4, 2, 70});
    old.entities.units.storage()[2047].damage = 9;
    old.entities.cities.storage()[255].shield_box = 999;
    old.multiplayer.saved.resume_event = 12;
    old.multiplayer.saved.encoded_passwords.fill(0x7f);
    old.eliminations.portraits.fill(29);
    old.pending_leader_overrides.fill(7);
    old.map.pattern_seed = 123;
    reused.process.fallback.region = 17;
    reused.process.rules->leaders[0].city_name_index = 12;
    reused.process.rules->double_movement_applied = true;
    reused.process.text_parameters.line = "previous game";
    reused.files.loaded_filename = "OLD.SAV";
    reused.files.scenario_directory = "old scenario";
    reused.files.game_stem = "OLD";
    reused.files.text = {"old scenario", "old scenario", "home"};
    fresh.files.text = {"home", "home", "home"};
    // Persisted user preferences are explicit inputs, equal on both paths.
    reused.process.ordinary_preferences = fresh.process.ordinary_preferences;
    reused.process.multiplayer_preferences = fresh.process.multiplayer_preferences;
    reused.process.tokens.next_unit = 500;
    reused.process.tokens.next_city = 90;
    const auto tokens = reused.process.tokens;
    base::GameplayRandom first{choices(), "entry-test"}, second{choices(), "entry-test"};
    REQUIRE(fresh.run(first));
    REQUIRE(reused.run(second));
    first.require_choices_consumed();
    second.require_choices_consumed();
    const auto& a = fresh.runtime.world();
    const auto& b = reused.runtime.world();
    CHECK(a.globals == b.globals);
    CHECK(a.civilizations == b.civilizations);
    CHECK(a.world_setup == b.world_setup);
    CHECK(a.eliminations == b.eliminations);
    CHECK(a.power_history == b.power_history);
    CHECK(a.multiplayer.saved == b.multiplayer.saved);
    CHECK(a.pending_leader_overrides == b.pending_leader_overrides);
    CHECK(std::equal(a.entities.units.storage().begin(), a.entities.units.storage().end(),
                     b.entities.units.storage().begin()));
    CHECK(std::equal(a.entities.cities.storage().begin(), a.entities.cities.storage().end(),
                     b.entities.cities.storage().begin()));
    CHECK(std::equal(a.map.tiles().begin(), a.map.tiles().end(), b.map.tiles().begin()));
    CHECK(a.map.pattern_seed == b.map.pattern_seed);
    CHECK(reused.process.fallback == fresh.process.fallback);
    CHECK(reused.process.rules->definitions == fresh.process.rules->definitions);
    CHECK(reused.process.rules->leaders == fresh.process.rules->leaders);
    CHECK_FALSE(reused.process.rules->double_movement_applied);
    CHECK(reused.process.text_parameters.line == fresh.process.text_parameters.line);
    CHECK(reused.files.loaded_filename.empty());
    CHECK(reused.files.scenario_directory.empty());
    CHECK(reused.files.game_stem == fresh.files.game_stem);
    CHECK(reused.files.text.working == "home");
    CHECK(reused.files.text.search == "home");
    CHECK(reused.process.tokens == tokens);
    CHECK(reused.introduced == fresh.introduced);
}

TEST_CASE("F05 preparation keeps an explicitly supplied runtime random context valid",
          "[world-entry][session][isolation]") {
    Fixture f;
    f.runtime.begin_client_session({17, 29});
    auto& random = f.runtime.gameplay_random();
    const auto before = random.checkpoint();
    REQUIRE(f.run(random));
    CHECK_FALSE(f.runtime.active());
    CHECK(random.checkpoint() != before);
    CHECK(f.runtime.world().globals.turn == 0);
}

TEST_CASE("F05 introduction selects highest twelve eligible known advances then reverses display",
          "[world-entry][session]") {
    Fixture f;
    auto content = f.services.load_content();
    rules::RuleInstaller(*content.rules, *f.process.rules).load(true);
    auto& known = f.runtime.world().civilizations[1].advances;
    known.fill(0xff); // Includes future technology and the extra high stored bits.
    f.process.rules->definitions.advances[98].prerequisites.fill(state::AdvanceReference::never);
    f.process.rules->definitions.advances[97].prerequisites = {
        state::AdvanceReference::never, state::AdvanceReference::none}; // Not both No.
    const auto civ = f.runtime.world().civilizations[1];
    const auto globals = f.runtime.world().globals;
    CHECK(starting_advance_list(f.runtime.world(), *f.process.rules, 1) ==
          "advances 86, advances 87, advances 88, advances 90, advances 91, advances 92, "
          "advances 93, advances 94, advances 95, advances 96, advances 97, advances 99, ");
    CHECK(f.runtime.world().civilizations[1] == civ);
    CHECK(f.runtime.world().globals == globals);
    known.fill(0);
    known[0] = 1; // Advance zero is never listed.
    CHECK(starting_advance_list(f.runtime.world(), *f.process.rules, 1).empty());
    known[0] = 2;
    f.process.rules->names.advances[1] = f.process.rules->text.store("Live label");
    CHECK(starting_advance_list(f.runtime.world(), *f.process.rules, 1) == "Live label, ");
    CHECK(starting_advance_list(f.runtime.world(), *f.process.rules, 0).empty());
    CHECK_THROWS_AS(starting_advance_list(f.runtime.world(), *f.process.rules, 8),
                    std::out_of_range);
}

TEST_CASE("F05 world entry validates the selected service graph before replacing a game",
          "[world-entry][session]") {
    const std::array<std::function<void(WorldEntryDependencies&)>, 5> remove{
        [](auto& services) { services.load_content = {}; },
        [](auto& services) { services.size = nullptr; },
        [](auto& services) { services.players = nullptr; },
        [](auto& services) { services.initialize.balance_starts = {}; },
        [](auto& services) { services.parameters.preferences = nullptr; }};
    for (const auto& missing : remove) {
        Fixture f;
        f.runtime.begin_client_session({1, 2});
        const auto client = f.runtime.bind(f.runtime.add_participant(state::CivilizationSlot{1}),
                                           ControllerKind::frontend);
        const auto observation = client.own_units().observation;
        f.runtime.world().globals.turn = 77;
        f.files.game_stem = "existing";
        const auto* world = &f.runtime.world();
        base::GameplayRandom random{choices(), "setup"};
        const auto before = random.checkpoint();
        WorldEntryDependencies dependencies = f.services;
        missing(dependencies);
        CHECK_THROWS_AS(enter_new_world(f.runtime, f.files, random, "entry", NewWorldChoice::random,
                                        formats::content::builtin_gameplay()->values.calendar,
                                        dependencies),
                        std::invalid_argument);
        CHECK(&f.runtime.world() == world);
        CHECK(f.runtime.world().globals.turn == 77);
        CHECK(f.files.game_stem == "existing");
        CHECK(client.own_units().observation == observation);
        CHECK(random.checkpoint() == before);
        CHECK(f.trace.empty());
    }
}

TEST_CASE("F05 new-world setup pins inactive storage across nested choice and gameplay children",
          "[world-entry][session][lifetime]") {
    Fixture f;
    unsigned callbacks = 0;
    f.observe = [&](auto) {
        ++callbacks;
        CHECK_FALSE(f.runtime.active());
        CHECK_THROWS_AS(f.runtime.prepare_new_game(), std::logic_error);
        CHECK_THROWS_AS(f.runtime.begin_client_session({1, 2}), std::logic_error);
    };
    base::GameplayRandom random{choices(), "entry-test"};
    REQUIRE(f.run(random));
    CHECK(callbacks > 10);
    CHECK_NOTHROW(f.runtime.prepare_new_game());
}
