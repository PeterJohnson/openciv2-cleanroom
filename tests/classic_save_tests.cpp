// Independently authored expectations from F06 section 5 and V03 section 2.
#include "session/classic_save.hpp"
#include "session/runtime.hpp"
#include "formats/classic_save.hpp"
#include "civilization/knowledge.hpp"
#include <catch2/catch_test_macros.hpp>
#include <algorithm>
#include <limits>

using namespace openciv;
using namespace openciv::formats;
using namespace openciv::session;
namespace {
struct Fixture {
    state::ProcessContext process;
    SessionRuntime runtime{process, base::RuntimeId{72}};
    ClassicFileState files;
    CoreSaveServices services;
    Bytes bytes;
    std::vector<Bytes> items, texts;
    std::vector<Byte> characters;
    std::vector<char> operations;
    std::string path;
    std::size_t closes = 0;
    std::optional<std::size_t> fail_item, fail_byte, fail_text;
    std::size_t fail_prefix = 0;
    std::function<void(std::size_t)> after_item, after_byte;
    std::function<void()> on_close;
    Fixture() {
        runtime.begin_client_session({10, 20});
        process.tokens = {100, 200};
        process.rules->text.initialize();
        const auto label = state::NameReference{process.rules->text.store("Label"), false};
        for (auto& leader : process.rules->leaders) {
            leader.leader_name = leader.plural = leader.adjective = label;
        }
        auto& w = runtime.world();
        w.globals.player_civ = 1;
        w.globals.saved_current_civ = -7;
        w.globals.turn_civ = 3;
        w.globals.turn = 31;
        w.globals.unit_end = w.globals.city_end = 1;
        w.globals.num_enemies = 99;
        w.globals.game_flags = 0x40;
        w.civilizations[1].units_active.fill(55);
        w.civilizations[1].units_in_production.fill(66);
        w.entities.units.set_end(1);
        w.entities.cities.set_end(1);
        auto& unit = w.entities.units.at(state::UnitSlot{0});
        unit.token = state::UnitToken{7};
        unit.owner = state::CivilizationSlot{1};
        auto& city = w.entities.cities.at(state::CitySlot{0});
        city.token = state::CityToken{8};
        city.owner = state::CivilizationSlot{1};
        w.map.allocate(world::Geometry{4, 2});
        w.map.pattern_seed = 19;
        for (unsigned civ = 1; civ < 8; ++civ)
            std::ranges::fill(w.map.remembered_layer(civ), 9);
        w.map.coarse_grid(0)[0] = 3;
        w.map.coarse_grid(1)[0] = 4;
        w.map.regions[0].land = 91; // Deliberately stale: saving must not rebuild it.
        w.power_history[149][7] = 81;
        w.views[7].zoom = -7;
        w.eliminations.names[11].bytes[23] = 91;
        w.multiplayer.local_human = 2;
        w.multiplayer.network_turn_civ = 5;
        w.multiplayer.saved.resume_civ = 6;
        w.multiplayer.hotseat_participants = 6;
        files.cursor = {-2, 7};
        files.current_screen = {10, 20, 650, 500};
        services.open = [&](std::string_view selected) {
            path = selected;
            return std::make_unique<CoreOutput>(CoreOutput{
                [&](std::span<const Byte> value) {
                    const auto index = items.size();
                    operations.push_back('i');
                    items.emplace_back(value.begin(), value.end());
                    const auto count =
                        fail_item == index ? std::min(fail_prefix, value.size()) : value.size();
                    bytes.insert(bytes.end(), value.begin(),
                                 value.begin() + static_cast<std::ptrdiff_t>(count));
                    if (after_item)
                        after_item(index);
                    return fail_item != index;
                },
                [&](std::span<const Byte> value) {
                    const auto index = texts.size();
                    operations.push_back('t');
                    texts.emplace_back(value.begin(), value.end());
                    if (fail_text == index)
                        return false;
                    bytes.insert(bytes.end(), value.begin(), value.end());
                    return true;
                },
                [&](Byte value) {
                    const auto index = characters.size();
                    operations.push_back('b');
                    characters.push_back(value);
                    if (fail_byte != index)
                        bytes.push_back(value);
                    if (after_byte)
                        after_byte(index);
                    return fail_byte != index;
                },
                [&] {
                    ++closes;
                    operations.push_back('c');
                    if (on_close)
                        on_close();
                    return false; // Close failure does not change the original status.
                }});
        };
    }
    CoreSaveStatus save(bool scenario = false, std::string_view requested = "C:\\saves\\Name",
                        std::optional<Byte> cleanup = {}) {
        return save_classic_core(runtime, files, requested, scenario, services, cleanup);
    }
    void events() {
        auto& events = runtime.world().events;
        events.loaded = true;
        auto event = events.collection.append();
        event->values.turn = 17;
        event->set_string(0, state::EventString{"Settlers"});
        event->set_string(4, state::EventString{""});
        event->set_string(34, state::EventString{"sound.wav"});
    }
};
std::vector<std::size_t> ordinary_sizes() {
    std::vector<std::size_t> sizes{2, 330, 1936, 11424, 14};
    sizes.insert(sizes.end(), 7, 4);
    for (const auto n : {24U, 1U, 1U, 1024U, 32U, 88U})
        sizes.push_back(n);
    sizes.insert(sizes.end(), 63, 1);
    for (const auto n : {2U, 2U, 48U, 1200U, 106U})
        sizes.push_back(n);
    sizes.insert(sizes.end(), 40, 2);
    sizes.push_back(338);
    return sizes;
}
} // namespace

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
    REQUIRE(f.characters == Bytes{'C', 'I', 'V', 'I', 'L', 'I', 'Z', 'E', 0, 0x1a});
    const auto sizes = ordinary_sizes();
    REQUIRE(f.items.size() == sizes.size());
    for (std::size_t i = 0; i < sizes.size(); ++i)
        REQUIRE(f.items[i].size() == sizes[i]);
    REQUIRE(
        std::ranges::all_of(std::span{f.operations}.first(10), [](char c) { return c == 'b'; }));
    REQUIRE(f.items[0] == Bytes{0x2c, 0});
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
    REQUIRE(f.texts.empty());
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
    REQUIRE(f.texts == std::vector<Bytes>{{'S', 'e', 't', 't', 'l', 'e', 'r', 's'},
                                          {},
                                          {'s', 'o', 'u', 'n', 'd', '.', 'w', 'a', 'v'}});
    REQUIRE(f.characters.size() == 13);
    REQUIRE(std::vector<char>(f.operations.end() - 7, f.operations.end()) ==
            std::vector<char>{'t', 'b', 't', 'b', 't', 'b', 'c'});
}
TEST_CASE("F06 every required save item failure retains its prefix and runs hotseat cleanup once",
          "[session][core-save]") {
    const auto configure = [](Fixture& f) {
        f.runtime.world().globals.multiplayer = 1;
        f.runtime.world().globals.human_players = 2;
        f.runtime.world().globals.game_flags = 0xc0;
        f.events();
    };
    Fixture baseline;
    configure(baseline);
    REQUIRE(baseline.save() == CoreSaveStatus::success);
    for (std::size_t item = 0; item < baseline.items.size(); ++item) {
        CAPTURE(item);
        Fixture f;
        configure(f);
        f.fail_item = item;
        f.fail_prefix = baseline.items[item].size() / 2;
        f.on_close = [&] { REQUIRE(f.runtime.world().globals.human_players == 2); };
        REQUIRE(f.save() == CoreSaveStatus::output);
        REQUIRE(f.items.size() == item + 1);
        REQUIRE(f.closes == 1);
        REQUIRE(f.characters.size() == 10);
        REQUIRE(f.texts.empty());
        std::size_t length = 10 + f.fail_prefix;
        for (std::size_t prior = 0; prior < item; ++prior)
            length += baseline.items[prior].size();
        REQUIRE(f.bytes == Bytes(baseline.bytes.begin(),
                                 baseline.bytes.begin() + static_cast<std::ptrdiff_t>(length)));
        REQUIRE(f.runtime.world().globals.player_civ == 1);
        REQUIRE(f.runtime.world().globals.saved_current_civ == 2);
        REQUIRE(f.runtime.world().globals.game_flags == (item == 0 ? 0xc0 : 0x80));
        const auto layout = std::find_if(baseline.items.begin(), baseline.items.end(),
                                         [](const Bytes& b) { return b.size() == 106; });
        REQUIRE(f.runtime.world().layout.rectangles[0] ==
                (item >= static_cast<std::size_t>(layout - baseline.items.begin())
                     ? f.files.current_screen
                     : state::Rectangle{}));
        const auto mp = std::find_if(baseline.items.begin(), baseline.items.end(),
                                     [](const Bytes& b) { return b.size() == 1172; });
        REQUIRE(f.runtime.world().multiplayer.saved.resume_civ ==
                (item == static_cast<std::size_t>(mp - baseline.items.begin()) ? 5 : 6));
    }
}
TEST_CASE("F06 save ignores header character failure but checks version before preparation",
          "[session][core-save]") {
    for (std::size_t i = 0; i < 10; ++i) {
        Fixture f;
        f.fail_byte = i;
        REQUIRE(f.save() == CoreSaveStatus::success);
        REQUIRE(f.characters.size() == 10);
        REQUIRE(f.items.size() == ordinary_sizes().size());
    }
    Fixture version;
    version.after_byte = [&](std::size_t index) {
        if (index == 9)
            version.runtime.world().multiplayer.local_human = 255;
    };
    version.fail_item = 0;
    REQUIRE(version.save(true) == CoreSaveStatus::output);
    REQUIRE(version.runtime.world().globals.saved_current_civ == -1);
    REQUIRE(version.runtime.world().names[1].leader.bytes[0] == 0);
}
TEST_CASE("F06 save filename selection and open failure preserve entry hotseat effects",
          "[session][core-save]") {
    for (unsigned mode = 0; mode < 8; ++mode) {
        Fixture f;
        auto& w = f.runtime.world();
        w.globals.multiplayer = static_cast<Byte>(mode);
        w.globals.human_players = 2;
        f.files.extension = ".OLD";
        f.services.open = [&](std::string_view path) {
            f.path = path;
            REQUIRE(w.globals.human_players == (mode == 1 ? 6 : 2));
            return std::unique_ptr<CoreOutput>{};
        };
        REQUIRE(f.save() == CoreSaveStatus::output);
        const auto extension = mode == 0   ? ".SAV"
                               : mode == 1 ? ".HOT"
                               : mode == 2 ? ".EML"
                               : mode <= 6 ? ".NET"
                                           : ".OLD";
        REQUIRE(f.path == "C:\\saves\\Name" + std::string(extension));
        REQUIRE(w.globals.human_players == 2);
        REQUIRE(w.globals.saved_current_civ == -7);
        REQUIRE(f.closes == 0);
    }
    Fixture f;
    f.fail_item = 0;
    REQUIRE(f.save(true, "C:\\folder.with.dot\\Name") == CoreSaveStatus::output);
    REQUIRE(f.path == "C:\\folder.with.dot\\Name");
    REQUIRE(f.files.extension == ".SCN");
}
TEST_CASE("F06 save cleanup uses live mode and requires backing when entering hotseat",
          "[session][core-save]") {
    for (const bool supplied : {false, true}) {
        Fixture f;
        f.fail_item = 0;
        f.after_item = [&](std::size_t) {
            f.runtime.world().globals.multiplayer = 1;
            f.runtime.world().globals.human_players = 0x80;
        };
        if (supplied)
            REQUIRE(f.save(false, "Name", 0x24) == CoreSaveStatus::output);
        else
            REQUIRE_THROWS_AS(f.save(), std::out_of_range);
        REQUIRE(f.runtime.world().globals.human_players == (supplied ? 0x24 : 0x80));
        REQUIRE(f.closes == 1);
    }
    Fixture leaving;
    leaving.runtime.world().globals.multiplayer = 1;
    leaving.runtime.world().globals.human_players = 2;
    leaving.fail_item = 0;
    leaving.after_item = [&](std::size_t) { leaving.runtime.world().globals.multiplayer = 0; };
    REQUIRE(leaving.save() == CoreSaveStatus::output);
    REQUIRE(leaving.runtime.world().globals.human_players == 6);
}
TEST_CASE("F06 save later blocks observe callback changes without rewriting earlier bytes",
          "[session][core-save]") {
    Fixture f;
    auto& w = f.runtime.world();
    w.globals.multiplayer = 3;
    w.globals.human_players = 2;
    f.after_item = [&](std::size_t index) {
        if (index == 1) {
            w.globals.turn = 92;
            w.names[1].leader.bytes[0] = 'Q';
            w.civilizations[1].gold = 1234;
            w.map.regions[0].land = 77;
        }
        if (f.items[index].size() == 1200)
            f.files.current_screen = {-32769, 65536, 65537, 32768};
        if (f.items[index].size() == 338)
            w.multiplayer.network_turn_civ = 258;
    };
    REQUIRE(f.save() == CoreSaveStatus::success);
    const auto stored = decode_stored_save(f.bytes);
    REQUIRE(stored.globals.turn == 31);
    REQUIRE(w.globals.turn == 92);
    REQUIRE(stored.names[1].leader.bytes[0] == 'Q');
    REQUIRE(stored.civilizations[1].gold == 1234);
    REQUIRE(stored.map.regions[0].land == 77);
    REQUIRE(stored.layout.rectangles[0] == SavedRectangle{32767, 0, 1, -32768});
    REQUIRE(stored.multiplayer->resume_civ == 2);
    REQUIRE(w.multiplayer.saved.resume_civ == 6);
}
TEST_CASE("F06 save event text and terminator failures keep completed multiplayer restore",
          "[session][core-save]") {
    for (const bool text : {false, true}) {
        Fixture f;
        f.runtime.world().globals.multiplayer = 3;
        f.runtime.world().globals.human_players = 2;
        f.events();
        if (text)
            f.fail_text = 1;
        else
            f.fail_byte = 11;
        REQUIRE(f.save() == CoreSaveStatus::output);
        REQUIRE(f.texts.size() == 2);
        REQUIRE(f.characters.size() == (text ? 11 : 12));
        REQUIRE(f.runtime.world().multiplayer.saved.resume_civ == 6);
        REQUIRE(f.closes == 1);
        REQUIRE(f.runtime.world().events.loaded);
    }
}
TEST_CASE("F06 save backing failures retain preceding preparation and close output",
          "[session][core-save]") {
    Fixture names;
    names.process.rules->leaders[0].plural = state::NameReference{32767, false};
    REQUIRE_THROWS_AS(names.save(true), std::out_of_range);
    REQUIRE(names.items.size() == 1);
    REQUIRE(names.runtime.world().names[1].adjective.bytes[0] == 'L');
    REQUIRE(names.runtime.world().names[1].leader.bytes[0] == 0);
    REQUIRE(names.closes == 1);
    Fixture scans;
    scans.after_item = [&](std::size_t index) {
        if (scans.items[index].size() == 1024)
            scans.runtime.world().globals.unit_end = 2;
    };
    REQUIRE_THROWS_AS(scans.save(), std::out_of_range);
    REQUIRE(scans.items.back().size() == 1024);
    REQUIRE(scans.runtime.world().entities.units.end() == 1);
    REQUIRE(scans.closes == 1);
}
TEST_CASE("F06 save skips empty entity blocks and unloaded events without implicit repair",
          "[session][core-save]") {
    Fixture f;
    auto& w = f.runtime.world();
    w.globals.unit_end = w.globals.city_end = 0;
    w.entities.units.set_end(0);
    w.entities.cities.set_end(0);
    f.events();
    w.events.loaded = false;
    REQUIRE(f.save() == CoreSaveStatus::success);
    const auto stored = decode_stored_save(f.bytes);
    REQUIRE(stored.units.empty());
    REQUIRE(stored.cities.empty());
    REQUIRE_FALSE(stored.events);
    REQUIRE(f.items.size() == ordinary_sizes().size() - 2);
    REQUIRE(w.events.collection.size() == 1);
}
