// F06 production save boundary and V03 knowledge; authored semantic/codec vectors.
#include "classic_save_fixture.hpp"
#include "civilization/knowledge.hpp"
#include <catch2/catch_test_macros.hpp>
using namespace openciv;
using namespace openciv::formats;
using namespace openciv::session;
using Fixture = test::ClassicSaveFixture;
TEST_CASE("V03 knowledge handles sentinels before civilization backing and excludes future tech",
          "[civilization][knowledge]") {
    std::vector<state::Civilization> civs(8);
    civs[1].advances.fill(0xff);
    for (const auto civ : {-1, 0, 1, 8, std::numeric_limits<int>::max()}) {
        REQUIRE_FALSE(civilization::knows_advance(civs, civ, -2));
        REQUIRE(civilization::knows_advance(civs, civ, -1));
        REQUIRE(civilization::knows_advance(civs, civ, std::numeric_limits<int>::min()));
        REQUIRE_FALSE(civilization::knows_advance(civs, civ, 89));
        REQUIRE_FALSE(civilization::knows_advance(civs, civ, 100));
    }
    for (int advance = 0; advance < 100; ++advance) {
        REQUIRE(civilization::knows_advance(civs, 1, advance) == (advance != 89));
        REQUIRE_FALSE(civilization::knows_advance(civs, 0, advance));
        REQUIRE_FALSE(civilization::knows_advance(civs, 2, advance));
    }
    REQUIRE_THROWS_AS(civilization::knows_advance(civs, 8, 0), std::out_of_range);
}
TEST_CASE(
    "F06 core save writes the complete ordered stream without census tokens or random effects",
    "[session][core-save]") {
    Fixture f;
    const auto participant = f.runtime.add_participant(state::CivilizationSlot{1});
    const auto client = f.runtime.bind(participant, ControllerKind::frontend);
    const auto identity = client.identity();
    const auto gameplay = f.runtime.gameplay_random().checkpoint();
    const auto presentation = f.runtime.presentation_random().checkpoint();
    REQUIRE(f.save() == CoreSaveStatus::success);
    REQUIRE(f.path == "C:\\saves\\Name.SAV");
    REQUIRE(f.closes == 1);
    const auto stored = decode_stored_save(f.bytes);
    REQUIRE(encode_stored_save(stored) == f.bytes);
    REQUIRE(stored.globals.saved_current_civ == 2);
    REQUIRE(stored.globals.turn_civ == 3);
    REQUIRE(stored.globals.game_flags == 0);
    REQUIRE(stored.globals.num_enemies == 99);
    REQUIRE(stored.civilizations[1].units_active[0] == 55);
    REQUIRE(stored.civilizations[1].units_in_production[0] == 66);
    REQUIRE(stored.map.regions[0].land == 91);
    REQUIRE(stored.units[0].token == state::UnitToken{7});
    REQUIRE(stored.cities[0].token == state::CityToken{8});
    REQUIRE(stored.cursor_x == -2);
    REQUIRE(stored.cursor_y == 7);
    REQUIRE(stored.layout.rectangles[0] == SavedRectangle{10, 20, 650, 500});
    REQUIRE(stored.power_history[149][7] == 81);
    REQUIRE(stored.views[7].zoom == -7);
    REQUIRE(stored.eliminations.names[11].bytes[23] == 91);
    REQUIRE(f.process.tokens == state::ProcessTokens{100, 200});
    REQUIRE(f.runtime.gameplay_random().checkpoint() == gameplay);
    REQUIRE(f.runtime.presentation_random().checkpoint() == presentation);
    REQUIRE(client.identity() == identity);
    REQUIRE(client.own_units().records.size() == 1);
}
TEST_CASE("F06 scenario save rebuilds knowledge and flags before names and omits events",
          "[session][core-save]") {
    Fixture f;
    f.events();
    auto& w = f.runtime.world();
    w.globals.first_discoverer.fill(7);
    w.globals.advance_known_by.fill(0xff);
    for (auto& civ : w.civilizations)
        civ.flags = 0xffff;
    w.civilizations[1].advances[0] = 3;
    w.civilizations[2].advances[0] = 2;
    w.civilizations[0].advances[0] = 4;
    w.civilizations[1].advances[11] = 2; // Raw Future Technology bit must be ignored.
    w.scenario.technology_paradigm = -11;
    f.process.rules->leaders[0].plural = state::NameReference{1, true};
    w.names[1].tribe.bytes[0] = 'X';
    REQUIRE(f.save(true) == CoreSaveStatus::success);
    REQUIRE(f.path == "C:\\saves\\Name.SCN");
    const auto stored = decode_stored_save(f.bytes);
    REQUIRE(stored.globals.game_flags == 0x40);
    REQUIRE(stored.globals.advance_known_by[0] == 2);
    REQUIRE(stored.globals.advance_known_by[1] == 6);
    REQUIRE(stored.globals.first_discoverer[0] == 1);
    REQUIRE(stored.globals.first_discoverer[1] == 8);
    for (std::size_t i = 2; i < 100; ++i) {
        REQUIRE(stored.globals.advance_known_by[i] == 0);
        REQUIRE(stored.globals.first_discoverer[i] == 7);
    }
    REQUIRE(stored.civilizations[0].flags == 0xffff);
    for (std::size_t i = 1; i < 8; ++i) {
        REQUIRE(stored.civilizations[i].flags == 0xff96);
        REQUIRE(stored.names[i].adjective.bytes[0] == 'L');
        REQUIRE(stored.names[i].leader.bytes[5] == 0);
    }
    REQUIRE(stored.names[1].tribe.bytes[0] == 'X');
    REQUIRE(stored.scenario->technology_paradigm == -11);
    REQUIRE_FALSE(stored.events);
    REQUIRE(w.events.loaded);
    REQUIRE(w.globals.advance_known_by == stored.globals.advance_known_by);
    REQUIRE(w.civilizations[1].flags == 0xff96);
}
TEST_CASE("F06 ordinary active scenario keeps knowledge and flags but prepares names and events",
          "[session][core-save]") {
    Fixture f;
    auto& w = f.runtime.world();
    w.globals.game_flags = 0xc0;
    w.globals.advance_known_by.fill(0xaa);
    w.civilizations[1].flags = 0x69;
    f.events();
    REQUIRE(f.save() == CoreSaveStatus::success);
    const auto stored = decode_stored_save(f.bytes);
    REQUIRE(stored.globals.game_flags == 0x80);
    REQUIRE(stored.globals.advance_known_by[0] == 0xaa);
    REQUIRE(stored.civilizations[1].flags == 0x69);
    REQUIRE(stored.names[1].adjective.bytes[0] == 'L');
    REQUIRE(stored.scenario);
    REQUIRE(stored.events->entries.size() == 1);
    REQUIRE(stored.events->entries[0].record.turn == 17);
}

TEST_CASE("F06 partial output including headers never reports save success",
          "[session][core-save]") {
    Fixture complete;
    REQUIRE(complete.save() == CoreSaveStatus::success);
    for (auto prefix : {std::size_t{0}, std::size_t{1}, std::size_t{9}, std::size_t{10},
                        std::size_t{12}, complete.bytes.size() / 2, complete.bytes.size() - 1}) {
        Fixture f;
        f.write_limit = prefix;
        CHECK(f.save() == CoreSaveStatus::output);
        CHECK(f.bytes == Bytes(complete.bytes.begin(), complete.bytes.begin() + prefix));
        CHECK(f.closes == 1);
    }
    Fixture reported;
    reported.fail_write = true;
    CHECK(reported.save() == CoreSaveStatus::output);
    CHECK(reported.closes == 1);
}
TEST_CASE("F06 failed final close and open are reported truthfully", "[session][core-save]") {
    Fixture f;
    f.fail_close = true;
    CHECK(f.save() == CoreSaveStatus::output);
    CHECK(f.closes == 1);
    CHECK_FALSE(f.bytes.empty());
    Fixture unopened;
    unopened.fail_open = true;
    CHECK(unopened.save() == CoreSaveStatus::output);
    CHECK(unopened.closes == 0);
    CHECK(unopened.writes == 0);
}
TEST_CASE("F06 write and close exceptions close an output only once", "[session][core-save]") {
    for (bool during_write : {false, true}) {
        Fixture f;
        f.throw_write = during_write;
        f.throw_close = true;
        CHECK_THROWS_AS(f.save(), std::runtime_error);
        CHECK(f.closes == 1);
    }
}
TEST_CASE("F06 hotseat export values never replace live masks or selectors",
          "[session][core-save]") {
    for (bool fail : {false, true}) {
        Fixture f;
        auto& w = f.runtime.world();
        w.globals.multiplayer = 1;
        w.globals.human_players = 2;
        f.fail_close = fail;
        f.observe = [&] {
            CHECK(w.globals.human_players == 2);
            CHECK(w.globals.player_civ == 1);
            CHECK(w.multiplayer.saved.resume_civ == 6);
        };
        CHECK(f.save() == (fail ? CoreSaveStatus::output : CoreSaveStatus::success));
        const auto stored = decode_stored_save(f.bytes);
        CHECK(stored.globals.human_players == 6);
        CHECK(stored.multiplayer->resume_civ == 5);
        CHECK(w.globals.human_players == 2);
        CHECK(w.globals.player_civ == 1);
        CHECK(w.multiplayer.saved.resume_civ == 6);
    }
}
TEST_CASE("F06 preparation errors open no output and accepted save effects survive I/O failure",
          "[session][core-save]") {
    Fixture invalid;
    invalid.runtime.world().globals.unit_end = 2;
    CHECK_THROWS_AS(invalid.save(), std::out_of_range);
    CHECK(invalid.opens == 0);
    Fixture f;
    f.fail_write = true;
    CHECK(f.save(true) == CoreSaveStatus::output);
    CHECK((f.runtime.world().globals.game_flags & 0x40) != 0);
    CHECK(f.runtime.world().names[1].leader.bytes[0] == 'L');
    CHECK(f.runtime.world().layout.rectangles[0] == f.files.current_screen);
    CHECK(f.closes == 1);
}
