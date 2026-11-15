// Authored F06 bytes and semantic restoration expectations.
#include "classic_load_fixture.hpp"
#include "session/classic_save.hpp"
#include "formats/game_input.hpp"
using namespace openciv;
using namespace openciv::formats;
using namespace openciv::session;
using namespace load_fixture;

TEST_CASE("F06 staged load installs current and legacy files through the same domain input",
          "[session][core-load]") {
    for (const auto version : {0x26, 0x27, 0x28, 0x2c}) {
        Fixture f;
        if (version != 0x2c)
            f.bytes = legacy(saved(), static_cast<std::uint16_t>(version));
        const auto participant = f.runtime.add_participant(state::CivilizationSlot{1});
        const auto client = f.runtime.bind(participant, ControllerKind::frontend);
        const auto identity = client.identity();
        const auto presentation = f.runtime.presentation_random().checkpoint();
        CHECK(f.load() == CoreLoadStatus::success);
        CHECK(f.closes == 1);
        CHECK(client.identity() == identity);
        CHECK(f.runtime.presentation_random().checkpoint() == presentation);
        const auto& world = f.runtime.world();
        CHECK(world.globals.turn == 31);
        CHECK(world.globals.num_enemies == 1);
        CHECK(world.entities.units.at(state::UnitSlot{0}).token == state::UnitToken{100});
        CHECK(world.entities.cities.at(state::CitySlot{0}).token == state::CityToken{200});
        CHECK(world.civilizations[1].units_active[2] == 1);
        CHECK(world.civilizations[1].units_in_production[3] == 1);
        CHECK(world.power_history[149][7] == 81);
        CHECK(world.views[7].zoom == -7);
        CHECK(f.files.cursor.x == -2);
        CHECK(f.runtime.gameplay_random().checkpoint().draws == 255);
    }
}
TEST_CASE("F06 decoding failures leave world rules tokens and randomness unchanged",
          "[session][core-load]") {
    for (const auto length : {0U, 8U, 11U, 30U, 340U, 3000U, 14000U}) {
        Fixture f;
        f.runtime.world().globals.turn = 123;
        const auto rng = f.runtime.gameplay_random().checkpoint();
        f.bytes.resize(std::min<std::size_t>(length, f.bytes.size() - 1));
        CHECK(f.load() != CoreLoadStatus::success);
        CHECK(f.closes == 1);
        CHECK(f.runtime.world().globals.turn == 123);
        CHECK(f.process.tokens == state::ProcessTokens{100, 200});
        CHECK(f.runtime.gameplay_random().checkpoint() == rng);
        CHECK(std::find(f.trace.begin(), f.trace.end(), "content") == f.trace.end());
    }
}
TEST_CASE("F06 load validates services and pins the world through all callbacks",
          "[session][core-load]") {
    Fixture missing;
    missing.services.load_content = {};
    CHECK_THROWS_AS(missing.load(), std::invalid_argument);
    CHECK(missing.trace.empty());
    Fixture f;
    f.before_item = [&](std::size_t) {
        CHECK(f.runtime.world().globals.turn == 0);
        CHECK_THROWS_AS(f.runtime.prepare_new_game(), std::logic_error);
        f.services.load_content = {}; // Operation owns its wiring snapshot.
    };
    f.services.flush = [&] { CHECK_THROWS_AS(f.runtime.prepare_new_game(), std::logic_error); };
    CHECK(f.load() == CoreLoadStatus::success);
    CHECK_NOTHROW(f.runtime.prepare_new_game());
}
TEST_CASE("F06 rejected headers preserve status and close once", "[session][core-load]") {
    for (const auto [version, expected] : {std::pair{0x25, CoreLoadStatus::unsupported},
                                           {0x29, CoreLoadStatus::unsupported},
                                           {0x2d, CoreLoadStatus::newer}}) {
        Fixture f;
        f.bytes[10] = static_cast<Byte>(version);
        CHECK(f.load() == expected);
        CHECK(f.closes == 1);
        CHECK(f.items.empty());
    }
}
TEST_CASE("F06 scenario and multiplayer installation retain required mode and name transforms",
          "[session][core-load]") {
    Fixture f;
    auto save = saved(true, true);
    save.events.emplace();
    StoredEvent event;
    event.record.unit_name = 0x1234;
    event.record.actions = 0x80000001;
    event.strings.names[0] = Bytes{'U'};
    save.events->entries.push_back(event);
    f.bytes = encode_stored_save(save);
    CHECK(f.load() == CoreLoadStatus::success);
    const auto& world = f.runtime.world();
    CHECK(world.globals.game_flags == 0x80);
    CHECK((world.globals.game_options & 0x8000) == 0);
    CHECK(world.globals.reveal_map == 0);
    CHECK(world.scenario.technology_paradigm == -11);
    CHECK(world.layout.valid == 0);
    CHECK(world.events.loaded);
    CHECK(world.events.collection.at(0)->strings[0].value() == "U");
    CHECK(world.events.collection.at(0)->values.actions == 0x80000001);
    CHECK(f.runtime.gameplay_random().checkpoint().draws == 0);
}
TEST_CASE("F06 live edit exports and reloads through owned restoration data",
          "[session][core-load]") {
    Fixture f;
    REQUIRE(f.load() == CoreLoadStatus::success);
    f.runtime.world().globals.turn = 77;
    f.runtime.world().entities.cities.at(state::CitySlot{0}).food_box = 1234;
    Bytes bytes;
    CoreSaveServices output;
    output.open = [&](std::string_view) {
        return test::output_file({[&](std::span<const Byte> data) {
                                      bytes.insert(bytes.end(), data.begin(), data.end());
                                      return true;
                                  },
                                  [] { return true; }});
    };
    REQUIRE(save_classic_core(f.runtime, f.files, "C:\\save.sav", false, output) ==
            CoreSaveStatus::success);
    Fixture next;
    next.bytes = std::move(bytes);
    REQUIRE(next.load() == CoreLoadStatus::success);
    CHECK(next.runtime.world().globals.turn == 77);
    CHECK(next.runtime.world().entities.cities.at(state::CitySlot{0}).food_box == 1234);
}

TEST_CASE("F06 input finalization failure does not install decoded state", "[session][core-load]") {
    Fixture f;
    const auto random = f.runtime.gameplay_random().checkpoint();
    MemoryItems source(f.bytes);
    f.services.open = [&](std::string_view) {
        return test::input_file({[&](std::size_t size) { return source.read(size); },
                                 {},
                                 [&] {
                                     ++f.closes;
                                     return false;
                                 }});
    };
    CHECK(f.load() == CoreLoadStatus::input);
    CHECK(f.closes == 1);
    CHECK(f.runtime.world().globals.turn == 0);
    CHECK(f.runtime.gameplay_random().checkpoint() == random);
    CHECK(std::find(f.trace.begin(), f.trace.end(), "content") == f.trace.end());
}
