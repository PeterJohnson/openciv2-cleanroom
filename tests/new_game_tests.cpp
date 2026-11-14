// F05 section 5, F02 retained state and F06 field encodings. Expectations and
// child outcomes are independently authored, not original-program captures.
#include "service_fixture.hpp"
#include "session/new_game.hpp"
#include "formats/save_records.hpp"
#include <catch2/catch_test_macros.hpp>
#include <algorithm>

using namespace openciv;
using namespace openciv::session;
namespace {
base::Choice history(std::string call = "start/one", std::int32_t code = 149) {
    return base::make_choice({"F05.new-game.history", std::move(call), 0}, base::PrimaryModulo{50},
                             code);
}
struct Fixture {
    state::ProcessContext process;
    SessionRuntime runtime{process, base::RuntimeId{91}};
    base::GameplayRandom random;
    NewGameServices services;
    std::vector<std::int32_t> calls;
    explicit Fixture(std::vector<base::Choice> choices = {history()})
        : random(base::ControlledChoices{std::move(choices)}, "setup") {
        auto& world = runtime.world();
        world.globals = formats::decode_globals(formats::Bytes(formats::global_record_size, 0xa5));
        world.globals.difficulty = 2;
        world.globals.player_civ = 3;
        world.globals.human_players = 2;
        world.globals.civs_alive = 0x23;
        world.scenario = formats::decode_scenario_parameters(
            formats::Bytes(formats::scenario_parameters_size, 0x71));
        world.eliminations = formats::decode_elimination_history(
            formats::Bytes(formats::elimination_record_size, 0x72));
        world.civilization_color_flags.fill(0x81234567U);
        world.unit_totals.fill(-1);
        world.movement_mode = -19;
        for (auto& row : world.power_history)
            row.fill(123);
        for (std::size_t i = 0; i < world.civilizations.size(); ++i)
            world.civilizations[i] = formats::decode_civilization(formats::Bytes(
                formats::civilization_record_size, static_cast<formats::Byte>(0xa5 + i)));
        for (std::size_t i = 0; i < process.rules->leaders.size(); ++i) {
            auto& leader = process.rules->leaders[i];
            leader.attack = -12;
            leader.times_used = static_cast<std::uint8_t>(33 + i);
            leader.city_name_index = static_cast<std::uint8_t>(66 + i);
            leader.female = 1;
            leader.color = 7;
            leader.city_style = 3;
            leader.leader_name = leader.plural = leader.adjective = {70000, true};
            leader.names.fill({80000, false});
            leader.titles.fill(
                {state::NameReference{90000, true}, state::NameReference{100000, false}});
        }
        process.ordinary_preferences.game_options = 0x812387ffU;
        process.ordinary_preferences.disabled_messages = 0xf123;
        process.ordinary_preferences.science = 5;
        process.ordinary_preferences.tax = 4;
        services.create_civilization = [&](std::int32_t slot) {
            calls.push_back(slot);
            return -1; // Returned creation failure does not end the parent's loop.
        };
        services.balance_starts = [&] { calls.push_back(8); };
    }
    void run(std::string_view call = "start/one") {
        initialize_new_game(runtime, random, call, services);
    }
};
} // namespace

TEST_CASE("F05 inner reset clears specified fields and preserves retained civilization bytes",
          "[new-game][session]") {
    Fixture f;
    auto& world = f.runtime.world();
    const auto globals = world.globals;
    const auto leaders = f.process.rules->leaders;
    const auto preferences = f.process.ordinary_preferences;
    f.run();
    CHECK(f.calls == std::vector<std::int32_t>{0, 1, 2, 3, 4, 5, 6, 7, 8});
    f.random.require_choices_consumed();
    CHECK_FALSE(f.runtime.active()); // Setup can precede client-session admission.
    CHECK(world.scenario.flags == 0);
    CHECK(world.scenario.technology_paradigm == 10);
    CHECK(world.scenario.year_increment == 0);
    CHECK(world.scenario.start_year == 0);
    CHECK(world.scenario.maximum_turns == 0);
    CHECK(world.scenario.objective_civ == 0);
    CHECK(world.scenario.victory_thresholds == std::array<std::int16_t, 4>{});
    CHECK(world.scenario.name.bytes[0] == 0);
    CHECK(std::all_of(world.scenario.name.bytes.begin() + 1, world.scenario.name.bytes.end(),
                      [](auto byte) { return byte == 0x71; }));
    CHECK(world.eliminations.count == 0);
    CHECK(world.eliminations.turns == std::array<std::int16_t, 12>{});
    CHECK(world.eliminations.killers == std::array<std::uint8_t, 12>{});
    for (std::size_t i = 0; i < 12; ++i) {
        CHECK(world.eliminations.portraits[i] == 0x72);
        CHECK(world.eliminations.names[i].bytes[0] == 0);
        CHECK(std::all_of(world.eliminations.names[i].bytes.begin() + 1,
                          world.eliminations.names[i].bytes.end(),
                          [](auto byte) { return byte == 0x72; }));
    }
    CHECK(world.civilization_color_flags == std::array<std::uint32_t, 8>{});
    for (std::size_t i = 0; i < leaders.size(); ++i) {
        auto expected = leaders[i];
        expected.times_used = 0;
        expected.city_name_index = 0;
        CHECK(f.process.rules->leaders[i] == expected);
    }
    CHECK(world.globals.turn == 0);
    CHECK(world.globals.year == -4000);
    CHECK(world.globals.first_launch_turn == -1);
    CHECK(world.globals.game_options == 0x812306ffU);
    CHECK(world.globals.message_options == 0xf123);
    CHECK(world.globals.transient_flags == 0);
    CHECK(world.globals.shown_event_flags == 0);
    CHECK(world.movement_mode == 1);
    CHECK(world.globals.active_unit.value == 0);
    CHECK(world.globals.turn_civ == 0);
    CHECK(world.globals.view_civ == -1);
    CHECK(world.globals.reveal_map == 0);
    CHECK(world.globals.warming_counter == 0);
    CHECK(world.globals.global_warmings == 0);
    CHECK(world.globals.warming_pollution == 0);
    CHECK(world.globals.polluted_tiles == 0);
    CHECK(world.globals.retained_34 == 0);
    CHECK(world.globals.first_discoverer == std::array<std::uint8_t, 100>{});
    CHECK(world.globals.advance_known_by == std::array<std::uint8_t, 100>{});
    CHECK(std::all_of(world.globals.wonder_city.begin(), world.globals.wonder_city.end(),
                      [](auto slot) { return slot == -1; }));
    CHECK(world.globals.next_history_turn == 49);
    CHECK(world.power_history == state::PowerHistory{});
    CHECK(world.globals.civs_at_start == 0x23);
    CHECK(world.unit_totals == std::array<std::int32_t, 8>{});
    // Independent F06 byte ranges make retention of all other civilization
    // fields visible, including research progress, flags and region policy.
    for (std::size_t slot = 0; slot < 8; ++slot) {
        CAPTURE(slot);
        formats::Bytes expected(formats::civilization_record_size,
                                static_cast<formats::Byte>(0xa5 + slot));
        for (const auto [start, end] :
             {std::pair{0x00a, 0x00c}, std::pair{0x066, 0x074}, std::pair{0x192, 0x312},
              std::pair{0x400, 0x401}, std::pair{0x402, 0x414}})
            std::fill(expected.begin() + start, expected.begin() + end, 0);
        if (slot == 3) {
            expected[0x13] = 5;
            expected[0x14] = 4;
        }
        CHECK(formats::encode_civilization(world.civilizations[slot]) == expected);
    }
    CHECK(world.globals.rules == globals.rules);
    CHECK(world.globals.game_flags == globals.game_flags);
    CHECK(world.globals.retained_0e == globals.retained_0e);
    CHECK(world.globals.moving_unit == globals.moving_unit);
    CHECK(world.globals.multiplayer == globals.multiplayer);
    CHECK(world.globals.saved_current_civ == globals.saved_current_civ);
    CHECK(world.globals.peace_turns == globals.peace_turns);
    CHECK(world.globals.num_advances == globals.num_advances);
    CHECK(world.globals.power_rank == globals.power_rank);
    CHECK(world.globals.civ_by_rank == globals.civ_by_rank);
    CHECK(f.process.ordinary_preferences == preferences);
}

TEST_CASE(
    "F05 inner reset changes scan bounds without invalidating records or resetting identities",
    "[new-game][session]") {
    Fixture f;
    auto& world = f.runtime.world();
    f.process.tokens = {0xfffffffeU, 765};
    const auto tokens = f.process.tokens;
    const auto unit = formats::decode_unit(formats::Bytes(formats::unit_record_size, 0xa3));
    const auto city = formats::decode_city(formats::Bytes(formats::city_record_size, 0xa4));
    for (auto& record : world.entities.units.storage())
        record = unit;
    for (auto& record : world.entities.cities.storage())
        record = city;
    world.entities.units.set_end(2048);
    world.entities.cities.set_end(256);
    world.unit_stacks.suppression = 77;
    world.unit_deletion.active = true;
    world.turn_loop_active = true;
    world.multiplayer.saved.resume_event = -7;
    const auto multiplayer = world.multiplayer;
    const auto leaders = f.process.rules->leaders;
    f.run();
    CHECK(world.entities.units.end() == 0);
    CHECK(world.entities.cities.end() == 0);
    CHECK(world.globals.unit_end == 0);
    CHECK(world.globals.city_end == 0);
    CHECK(std::all_of(world.entities.units.storage().begin(), world.entities.units.storage().end(),
                      [&](const auto& record) { return record == unit; }));
    CHECK(std::all_of(world.entities.cities.storage().begin(),
                      world.entities.cities.storage().end(),
                      [&](const auto& record) { return record == city; }));
    CHECK(f.process.tokens == tokens);
    CHECK(world.unit_stacks.suppression == 77);
    CHECK(world.unit_deletion.active);
    CHECK(world.turn_loop_active);
    CHECK(world.multiplayer == multiplayer);
    for (std::size_t i = 0; i < leaders.size(); ++i)
        CHECK(f.process.rules->leaders[i].city_name_index == 0);
}

TEST_CASE("F05 all civilization attempts precede balancing and final live human adjustments",
          "[new-game][session]") {
    Fixture f;
    auto& world = f.runtime.world();
    f.services.create_civilization = [&](std::int32_t slot) {
        CHECK(world.globals.turn == (slot == 0 ? 0 : 7));
        CHECK(world.globals.next_history_turn == 49);
        CHECK(world.power_history == state::PowerHistory{});
        CHECK(world.globals.civs_at_start == 0xa5);
        CHECK(world.civilizations[3].science_rate == 0xa8);
        world.globals.turn = 7; // Later children read live state, no per-slot reset.
        world.globals.civs_alive = static_cast<std::uint8_t>(1U << slot);
        world.civilizations[static_cast<std::size_t>(slot)].gold = slot + 100;
        f.calls.push_back(slot);
        return slot % 2 ? 0 : -1;
    };
    f.services.balance_starts = [&] {
        CHECK(world.globals.civs_at_start == 0x80);
        CHECK(world.civilizations[7].gold == 107);
        CHECK(world.civilizations[3].science_rate == 0xa8);
        world.globals.civs_alive = 3;
        world.globals.human_players = 0x50;
        world.globals.difficulty = 0;
        f.process.ordinary_preferences.science = 7;
        f.process.ordinary_preferences.tax = -100;
        f.calls.push_back(8);
    };
    f.run();
    CHECK(f.calls == std::vector<std::int32_t>{0, 1, 2, 3, 4, 5, 6, 7, 8});
    CHECK(world.globals.civs_at_start == 0x80);
    CHECK(world.globals.civs_alive == 3);
    CHECK(world.globals.turn == 7);
    CHECK(f.process.ordinary_preferences.science == 6);
    CHECK(f.process.ordinary_preferences.tax == 4);
    CHECK(world.civilizations[4].science_rate == 6);
    CHECK(world.civilizations[4].tax_rate == 4);
    CHECK(world.civilizations[4].gold == 50);
    CHECK(world.civilizations[6].gold == 50);
    CHECK(world.civilizations[3].science_rate == 0xa8);
    CHECK(world.civilizations[3].gold == 103);
}

TEST_CASE("F05 difficulty options clear cheat and distinguish tutorial from advice",
          "[new-game][session]") {
    for (const auto difficulty : {0, 1, 5, 255})
        for (const auto preference : {0U, 0x100U, 0x200U, 0xffffffffU}) {
            Fixture f;
            f.runtime.world().globals.difficulty = static_cast<std::uint8_t>(difficulty);
            f.process.ordinary_preferences.game_options = preference;
            f.run();
            const auto expected =
                difficulty == 0 ? (preference & 0xffff7fffU) | 0x300U : preference & 0xffff7effU;
            CHECK(f.runtime.world().globals.game_options == expected);
            CHECK(f.process.ordinary_preferences.game_options == preference);
        }
}

TEST_CASE("F05 rates reject upper bounds only and narrow negative preferences to bytes",
          "[new-game][session]") {
    struct Rates {
        std::int16_t science, tax;
        bool reset;
    };
    for (const auto rates :
         {Rates{6, 4, false}, Rates{5, 6, true}, Rates{7, -100, true}, Rates{-100, 7, true},
          Rates{-1, -257, false}, Rates{-32768, 6, false}, Rates{32767, 32767, true}}) {
        Fixture f;
        f.process.ordinary_preferences.science = rates.science;
        f.process.ordinary_preferences.tax = rates.tax;
        f.run();
        CHECK(f.process.ordinary_preferences.science == (rates.reset ? 6 : rates.science));
        CHECK(f.process.ordinary_preferences.tax == (rates.reset ? 4 : rates.tax));
        CHECK(f.runtime.world().civilizations[3].science_rate ==
              static_cast<std::uint8_t>(rates.reset ? 6 : rates.science));
        CHECK(f.runtime.world().civilizations[3].tax_rate ==
              static_cast<std::uint8_t>(rates.reset ? 4 : rates.tax));
    }
}

TEST_CASE("F05 human-mask cardinality controls rates but gold follows actual human bits",
          "[new-game][session]") {
    for (const auto mask : {0U, 1U, 2U, 3U, 0x82U, 0xffU}) {
        Fixture f;
        auto& world = f.runtime.world();
        world.globals.human_players = static_cast<std::uint8_t>(mask);
        world.globals.difficulty = 0;
        f.run();
        const bool single = mask == 1 || mask == 2;
        for (std::size_t slot = 0; slot < 8; ++slot) {
            const bool human = slot != 0 && (mask & (1U << slot));
            const auto expected_science = single && slot == 3 ? 5U
                                          : !single && human  ? 6U
                                                              : 0xa5U + slot;
            CHECK(world.civilizations[slot].science_rate == expected_science);
            if (human)
                CHECK(world.civilizations[slot].gold == 50);
            else
                CHECK(world.civilizations[slot].gold != 50);
        }
    }
}

TEST_CASE("F05 unavailable history choice retains reset prefix and incoming history samples",
          "[new-game][session]") {
    Fixture f(std::vector<base::Choice>{});
    auto& world = f.runtime.world();
    const auto history_before = world.power_history;
    CHECK_THROWS_AS(f.run(), base::ChoiceError);
    CHECK(world.globals.turn == 0);
    CHECK(world.globals.next_history_turn == static_cast<std::int16_t>(-23131));
    CHECK(world.civilizations[7].spaceship_parts == std::array<std::int16_t, 6>{});
    CHECK(world.power_history == history_before);
    CHECK(world.globals.civs_at_start == 0xa5);
    CHECK(f.calls.empty());
}

TEST_CASE("F05 required creation failure retains completed prefix and skips balancing and rates",
          "[new-game][session]") {
    for (const auto stop : {0, 4, 7}) {
        Fixture f;
        auto& world = f.runtime.world();
        f.runtime.begin_client_session({11, 22});
        const auto client = f.runtime.bind(f.runtime.add_participant(state::CivilizationSlot{1}),
                                           ControllerKind::frontend);
        auto last_revision = client.own_units().observation.revision;
        f.services.create_civilization = [&](std::int32_t slot) {
            last_revision = client.own_units().observation.revision;
            f.calls.push_back(slot);
            world.civilizations[static_cast<std::size_t>(slot)].gold = slot;
            if (slot == stop)
                throw std::runtime_error("creation unavailable");
            return 0;
        };
        CHECK_THROWS_AS(f.run(), std::runtime_error);
        CHECK(client.own_units().observation.revision > last_revision);
        CHECK(f.calls.size() == static_cast<std::size_t>(stop + 1));
        CHECK(world.civilizations[static_cast<std::size_t>(stop)].gold == stop);
        CHECK(world.power_history == state::PowerHistory{});
        CHECK(world.globals.civs_at_start == 0xa5);
        CHECK(world.civilizations[3].science_rate == 0xa8);
        f.random.require_choices_consumed();
    }
    Fixture f;
    test::fail_service(f.services.create_civilization);
    CHECK_THROWS_AS(f.run(), std::runtime_error);
    CHECK(f.runtime.world().power_history == state::PowerHistory{});
    CHECK(f.calls.empty());
}

TEST_CASE("F05 failed balancing preserves creation and at-start mask before rates",
          "[new-game][session]") {
    Fixture f;
    test::fail_service(f.services.balance_starts);
    CHECK_THROWS_AS(f.run(), std::runtime_error);
    CHECK(f.calls == std::vector<std::int32_t>{0, 1, 2, 3, 4, 5, 6, 7});
    CHECK(f.runtime.world().globals.civs_at_start == 0x23);
    CHECK(f.runtime.world().civilizations[3].science_rate == 0xa8);
}

TEST_CASE("F05 unsupported player selector stops only when single-human rates consume it",
          "[new-game][session]") {
    for (const auto player : {-128, -1, 8, 127}) {
        Fixture f;
        auto& world = f.runtime.world();
        world.globals.player_civ = static_cast<std::int8_t>(player);
        f.process.ordinary_preferences.science = 7;
        CHECK_THROWS_AS(f.run(), std::out_of_range);
        CHECK(f.calls.back() == 8);
        CHECK(f.process.ordinary_preferences.science == 6);
        CHECK(f.process.ordinary_preferences.tax == 4);
        CHECK(world.civilizations[3].science_rate == 0xa8);
    }
    Fixture f;
    f.runtime.world().globals.player_civ = -1;
    f.runtime.world().globals.human_players = 6;
    CHECK_NOTHROW(f.run());
}

TEST_CASE("F05 initialization publishes across children without revoking clients or reseeding",
          "[new-game][session]") {
    Fixture f;
    auto& world = f.runtime.world();
    f.runtime.begin_client_session({11, 22});
    const auto client = f.runtime.bind(f.runtime.add_participant(state::CivilizationSlot{1}),
                                       ControllerKind::frontend);
    const auto identity = client.identity();
    auto last_revision = client.own_units().observation.revision;
    const auto presentation = f.runtime.presentation_random().checkpoint();
    const auto runtime_random = f.runtime.gameplay_random().checkpoint();
    auto& unit = world.entities.units.storage()[0];
    unit.token = state::UnitToken{1};
    unit.owner = state::CivilizationSlot{1};
    world.entities.units.set_end(1);
    f.services.create_civilization = [&](std::int32_t slot) {
        const auto revision = client.own_units().observation.revision;
        CHECK(revision > last_revision);
        last_revision = revision;
        if (slot == 0) {
            CHECK(client.own_units().records.empty());
            world.entities.units.set_end(1);
        } else {
            REQUIRE(client.own_units().records.size() == 1);
            CHECK(client.own_units().records[0].x == slot - 1);
        }
        unit.x = static_cast<std::int16_t>(slot);
        return 0;
    };
    f.services.balance_starts = [&] {
        const auto revision = client.own_units().observation.revision;
        CHECK(revision > last_revision);
        last_revision = revision;
        CHECK(client.own_units().records[0].x == 7);
        unit.x = 12;
    };
    f.run();
    CHECK(client.own_units().records[0].x == 12);
    CHECK(client.own_units().observation.revision > last_revision);
    CHECK(client.identity() == identity);
    CHECK(f.runtime.presentation_random().checkpoint() == presentation);
    CHECK(f.runtime.gameplay_random().checkpoint() == runtime_random);
    CHECK(f.random.checkpoint().controlled->records.size() == 1);
}

TEST_CASE("F05 inner helper preserves current-attempt child state but resets naming cursors",
          "[new-game][session]") {
    Fixture f({history(), history("start/two", 51)});
    f.run();
    f.process.rules->leaders[3].city_name_index = 12;
    f.runtime.world().civilizations[3].future_techs = 19;
    f.runtime.world().globals.peace_turns = 87;
    f.runtime.world().globals.turn = 99;
    f.run("start/two");
    CHECK(f.runtime.world().globals.turn == 0);
    CHECK(f.runtime.world().globals.next_history_turn == 1);
    CHECK(f.process.rules->leaders[3].city_name_index == 0);
    CHECK(f.runtime.world().civilizations[3].future_techs == 19);
    CHECK(f.runtime.world().globals.peace_turns == 87);
    f.random.require_choices_consumed();
}

TEST_CASE("F05 empty semantic call is rejected before initialization effects",
          "[new-game][session]") {
    Fixture f;
    const auto globals = f.runtime.world().globals;
    const auto random = f.random.checkpoint();
    CHECK_THROWS_AS(f.run(""), std::invalid_argument);
    CHECK(f.runtime.world().globals == globals);
    CHECK(f.random.checkpoint() == random);
    CHECK(f.calls.empty());
}

TEST_CASE("F05 inner initialization retains the finalized map and process fallback",
          "[new-game][session]") {
    Fixture f;
    auto& map = f.runtime.world().map;
    map.allocate(world::Geometry{10, 6});
    const auto tile = formats::decode_tile(formats::Bytes(formats::tile_record_size, 0x73));
    for (auto& value : map.tiles())
        value = tile;
    map.remembered(3, {2, 2}) = 0x84;
    map.coarse_grid(0)[0] = 0xa7;
    f.process.fallback = tile;
    const auto revision = map.storage_revision();
    f.run();
    CHECK(map.storage_revision() == revision);
    CHECK(map.geometry().width() == 10);
    CHECK(map.geometry().height() == 6);
    CHECK(std::all_of(map.tiles().begin(), map.tiles().end(),
                      [&](const auto& value) { return value == tile; }));
    CHECK(map.remembered(3, {2, 2}) == 0x84);
    CHECK(map.coarse_grid(0)[0] == 0xa7);
    CHECK(f.process.fallback == tile);
}

TEST_CASE("F05 history decision precedes supplied child choices on the same gameplay context",
          "[new-game][session]") {
    std::vector<base::Choice> choices{history()};
    for (std::int32_t slot = 0; slot < 8; ++slot)
        choices.push_back(base::make_choice({"fixture.creation", std::to_string(slot), 0},
                                            base::PrimaryCode{}, slot));
    Fixture f(choices);
    f.services.create_civilization = [&](std::int32_t slot) {
        CHECK(f.random.primary_code(base::ChoiceKey{"fixture.creation", std::to_string(slot), 0}) ==
              slot);
        return 0;
    };
    f.run();
    f.random.require_choices_consumed();
    REQUIRE(f.random.checkpoint().trace.has_value());
    CHECK(f.random.checkpoint().trace->events == choices);
}
