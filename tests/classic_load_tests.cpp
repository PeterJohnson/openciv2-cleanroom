// Independently authored F06 core ordering cases. Most tests supply explicit
// F03 child effects; the final integration case runs the real classic rule parsers.
#include "session/classic_load.hpp"
#include "session/runtime.hpp"
#include "formats/classic_save.hpp"
#include "formats/classic_rules.hpp"
#include "classic_rule_fixture.hpp"
#include <catch2/catch_test_macros.hpp>
#include <limits>

using namespace openciv;
using namespace openciv::formats;
using namespace openciv::session;
namespace {
StoredSave saved(bool scenario = false, bool multiplayer = false) {
    StoredSave value;
    value.globals.civs_alive = 7;
    value.globals.num_enemies = 99;
    value.globals.player_civ = value.globals.saved_current_civ = value.globals.turn_civ = 1;
    value.globals.unit_end = value.globals.city_end = 1;
    value.globals.multiplayer = multiplayer ? 3 : 0;
    value.globals.human_players = multiplayer ? 2 : 0;
    value.globals.game_flags = scenario ? 0x50 : 0;
    value.globals.game_options = 0x8000;
    value.globals.reveal_map = 1;
    value.globals.turn = 31;
    value.civilizations[1].units_active.fill(55);
    value.civilizations[1].units_lost.fill(77);
    value.civilizations[1].units_in_production.fill(66);
    value.map.header = {4, 2, 4, 0, 19, 1, 1};
    for (auto& layer : value.map.remembered)
        layer = Bytes(4, 9);
    value.map.tiles.resize(4);
    value.map.coarse = {Bytes{3}, Bytes{4}};
    value.map.regions[0].land = 4;
    value.units.resize(1);
    value.units[0].token = state::UnitToken{7};
    value.units[0].owner = state::CivilizationSlot{1};
    value.units[0].type = state::UnitType{2};
    value.cities.resize(1);
    value.cities[0].token = state::CityToken{8};
    value.cities[0].owner = state::CivilizationSlot{1};
    value.cities[0].production = 3;
    value.leaders.resize(1);
    value.leaders[0].plural = -1;
    value.cursor_x = -2;
    value.cursor_y = 7;
    value.power_history[149][7] = 81;
    value.layout.rectangles[0] = {0, 0, 640, 480};
    value.views[7].zoom = -7;
    value.eliminations.names[11].bytes[23] = 91;
    if (scenario) {
        value.scenario.emplace();
        value.scenario->technology_paradigm = -11;
    }
    if (multiplayer) {
        value.multiplayer.emplace();
        value.multiplayer->resume_civ = 0;
        value.multiplayer->double_movement = value.multiplayer->double_production = 1;
    }
    return value;
}
Bytes legacy(const StoredSave& save, std::uint16_t version) {
    const auto current = encode_stored_save(save);
    ByteWriter output;
    auto head = encode_game_header();
    head[10] = static_cast<Byte>(version);
    output.append(head);
    const auto globals = encode_globals(save.globals);
    if (version < 0x28)
        for (const auto [offset, length] :
             {std::pair{0x000U, 0x036U}, {0x036U, 0x05dU}, {0x09aU, 0x05dU}, {0x0feU, 0x04cU}})
            output.append(std::span{globals}.subspan(offset, length));
    else
        output.append(globals);
    for (const auto& name : save.names)
        output.append(encode_civilization_names(name));
    for (const auto& civ : save.civilizations) {
        const auto bytes = encode_civilization(civ);
        if (version < 0x28) {
            for (const auto [offset, length] : {std::pair{0x000U, 0x058U},
                                                {0x058U, 0x00cU},
                                                {0x066U, 0x00eU},
                                                {0x074U, 0x05dU},
                                                {0x0d8U, 0x036U},
                                                {0x116U, 0x036U},
                                                {0x154U, 0x036U}})
                output.append(std::span{bytes}.subspan(offset, length));
            output.u8(0x43); // Required discarded byte, not the retained byte at 0x65.
            output.append(std::span{bytes}.subspan(0x192));
        } else
            output.append(bytes);
    }
    constexpr auto map_offset = 12 + 330 + 8 * 242 + 8 * 1428;
    constexpr auto map_length = 14 + 7 * 4 + 6 * 4 + 2 + 1024;
    output.append(std::span{current}.subspan(map_offset, map_length));
    const auto unit = encode_unit(save.units[0]), city = encode_city(save.cities[0]);
    output.append(std::span{unit}.first(26));
    output.append(std::span{city}.first(84));
    output.append(std::span{current}.subspan(map_offset + map_length + 32 + 88));
    return std::move(output.bytes);
}
struct Fixture {
    state::ProcessContext process;
    SessionRuntime runtime{process, base::RuntimeId{71}};
    ClassicFileState files;
    CoreLoadServices services;
    Bytes bytes = encode_stored_save(saved());
    std::size_t position = 0, chars = 0, closes = 0;
    std::vector<std::pair<std::size_t, std::size_t>> items;
    std::vector<std::string> trace;
    std::optional<std::size_t> fail_item;
    std::size_t fail_prefix = 0;
    std::function<void(std::size_t)> before_item;
    Fixture() {
        runtime.begin_client_session({10, 20});
        process.tokens = {100, 200};
        process.rules->text.initialize();
        const auto id = static_cast<std::int16_t>(process.rules->text.store("Label"));
        for (auto& leader : process.rules->leaders) {
            leader.names.fill(id);
            leader.plural = leader.adjective = id;
            for (auto& titles : leader.titles)
                titles.fill(id);
        }
        runtime.world().mood = {state::CitySlot{3}, 21, 22};
        files.text = {"C:\\caller", "C:\\home", "C:\\home", "TXT"};
        files.current_screen = {10, 20, 650, 500};
        files.default_game_stem = "C:\\home\\GAME";
        files.remembered_subdirectory = "retained";
        services.open = [&](std::string_view path) {
            trace.push_back("open:" + std::string(path));
            return std::make_unique<CoreInput>(CoreInput{
                [&](std::size_t n) {
                    if (before_item)
                        before_item(items.size());
                    const auto index = items.size();
                    items.emplace_back(position, n);
                    auto count = std::min(n, bytes.size() - position);
                    const bool failed = fail_item == index;
                    if (failed)
                        count = std::min(count, fail_prefix);
                    Bytes prefix(bytes.begin() + static_cast<std::ptrdiff_t>(position),
                                 bytes.begin() + static_cast<std::ptrdiff_t>(position + count));
                    position += count;
                    return ReadResult{std::move(prefix), !failed && count == n};
                },
                [&]() -> std::optional<Byte> {
                    ++chars;
                    if (position == bytes.size())
                        return {};
                    return bytes[position++];
                },
                [&] {
                    ++closes;
                    trace.push_back("close");
                    return false; // The original ignores close failure.
                }});
        };
        services.flush = [&] { trace.push_back("flush"); };
        services.change_directory = [&](std::string_view directory) {
            trace.push_back("cd:" + std::string(directory));
        };
        services.exists = [&](std::string_view directory, std::string_view name) {
            trace.push_back("probe:" + std::string(directory) + "\\" + std::string(name));
            return true;
        };
        services.comparison = classic::ascii_text_case();
        services.load_rules = [&](bool initialize) {
            REQUIRE(initialize);
            trace.push_back("rules");
            process.rules->definitions.cosmic.values[0] = 9;
        };
        services.reload_table = [&](LoadedRuleTable table, bool initialize) {
            REQUIRE(initialize);
            REQUIRE_FALSE(runtime.world().mood.city);
            trace.push_back("reload:" + std::to_string(static_cast<int>(table)));
        };
        services.close_rules = [&] { trace.push_back("rules-close"); };
    }
    CoreLoadStatus load(std::string_view path = "C:\\home\\Scenario\\Example") {
        return load_classic_core(runtime, files, path, services, "load/1");
    }
};
} // namespace
TEST_CASE("F06 core load sequences all stages and preserves process client and random ownership",
          "[session][core-load]") {
    Fixture f;
    const auto participant = f.runtime.add_participant(state::CivilizationSlot{1});
    const auto client = f.runtime.bind(participant, ControllerKind::frontend);
    const auto identity = client.identity();
    const auto presentation = f.runtime.presentation_random().checkpoint();
    auto old_event = f.runtime.world().events.collection.append();
    f.runtime.world().events.loaded = true;
    REQUIRE(f.load() == CoreLoadStatus::success);
    REQUIRE(f.closes == 1);
    REQUIRE(f.position == f.bytes.size());
    REQUIRE(f.trace == std::vector<std::string>{
                           "open:C:\\home\\Scenario\\Example.SAV", "flush", "cd:C:\\home\\Scenario",
                           "probe:C:\\home\\Scenario\\GAME.TXT", "cd:C:\\home", "flush", "rules",
                           "flush", "flush", "flush", "flush", "flush", "reload:0", "reload:1",
                           "reload:2", "rules-close", "close"});
    const auto& world = f.runtime.world();
    REQUIRE(world.globals.num_enemies == 1);
    REQUIRE(world.entities.units.at(state::UnitSlot{0}).token == state::UnitToken{100});
    REQUIRE(world.entities.cities.at(state::CitySlot{0}).token == state::CityToken{200});
    REQUIRE(f.process.tokens == state::ProcessTokens{101, 201});
    REQUIRE(world.civilizations[1].units_active[2] == 1);
    REQUIRE(world.civilizations[1].units_active[1] == 0);
    REQUIRE(world.civilizations[1].units_in_production[3] == 1);
    REQUIRE(world.civilizations[1].units_lost[2] == 77);
    REQUIRE(world.mood.happy == 21);
    REQUIRE(world.mood.unhappy == 22);
    REQUIRE(world.layout.valid == 1);
    REQUIRE(world.power_history[149][7] == 81);
    REQUIRE(world.views[7].zoom == -7);
    REQUIRE(f.files.cursor.x == -2);
    REQUIRE(f.files.cursor.y == 7);
    REQUIRE(f.files.remembered_subdirectory == "Scenario");
    REQUIRE(f.files.loaded_filename == "Example");
    REQUIRE(f.files.game_stem == "C:\\home\\Scenario\\GAME");
    REQUIRE(f.runtime.gameplay_random().checkpoint().draws == 255);
    REQUIRE(f.runtime.presentation_random().checkpoint() == presentation);
    REQUIRE_FALSE(world.events.loaded);
    REQUIRE(world.events.collection.at(0) == old_event);
    REQUIRE(client.identity() == identity);
    REQUIRE(client.own_units().records.size() == 1);
}
TEST_CASE("F06 core header gate closes once before rule or world effects", "[session][core-load]") {
    for (const auto version : {0x25U, 0x29U, 0x2aU, 0x2bU, 0x2dU, 0xffffU}) {
        Fixture f;
        f.bytes[10] = static_cast<Byte>(version);
        f.bytes[11] = static_cast<Byte>(version >> 8);
        REQUIRE(f.load() == (version > 0x2c ? CoreLoadStatus::newer : CoreLoadStatus::unsupported));
        REQUIRE(f.closes == 1);
        REQUIRE(f.trace.size() == 2);
        REQUIRE(f.runtime.world().globals.turn == 0);
        REQUIRE(f.runtime.gameplay_random().checkpoint().draws == 0);
    }
    for (const unsigned length : {10U, 11U}) {
        Fixture f;
        f.bytes.resize(length);
        REQUIRE(f.load() == CoreLoadStatus::input);
        REQUIRE(f.closes == 1);
        REQUIRE(f.trace.size() == 2);
    }
    Fixture wrong;
    wrong.bytes[0] = 'X';
    REQUIRE(wrong.load() == CoreLoadStatus::input);
    REQUIRE(wrong.items.empty());
    REQUIRE(wrong.chars == 10);
    Fixture separator;
    separator.bytes[9] = '?';
    REQUIRE(separator.load() == CoreLoadStatus::success);
    Fixture unbacked;
    unbacked.bytes = Bytes(79, 'X');
    REQUIRE_THROWS_AS(unbacked.load(), std::out_of_range);
    REQUIRE(unbacked.chars == 80);
    REQUIRE(unbacked.closes == 1);
    Fixture absent;
    absent.services.open = [&](std::string_view) { return std::unique_ptr<CoreInput>{}; };
    REQUIRE(absent.load() == CoreLoadStatus::input);
    REQUIRE(absent.closes == 0);
}
TEST_CASE("F06 core filename selection uses any period and retains unknown mode extension",
          "[session][core-load]") {
    for (unsigned mode = 0; mode < 8; ++mode) {
        Fixture f;
        f.runtime.world().globals.multiplayer = static_cast<Byte>(mode);
        f.files.extension = ".OLD";
        f.fail_item = 0; // Stop at version input: path selection alone.
        REQUIRE(f.load("C:\\folder\\Name") == CoreLoadStatus::input);
        const auto expected = mode == 0   ? ".SAV"
                              : mode == 1 ? ".HOT"
                              : mode == 2 ? ".EML"
                              : mode < 7  ? ".NET"
                                          : ".OLD";
        REQUIRE(f.files.opened_path == "C:\\folder\\Name" + std::string(expected));
    }
    Fixture period;
    period.fail_item = 0;
    REQUIRE(period.load("C:\\folder.with.dot\\Name") == CoreLoadStatus::input);
    REQUIRE(period.files.opened_path == "C:\\folder.with.dot\\Name");
    Fixture path;
    REQUIRE_THROWS_AS(path.load("Name.SAV"), std::out_of_range);
    REQUIRE(path.closes == 1);
    REQUIRE(path.trace == std::vector<std::string>{"open:Name.SAV", "flush", "close"});
}
TEST_CASE("F06 core path selection retains unrelated subdirectories and uses case folding",
          "[session][core-load]") {
    for (const std::string path :
         {"c:\\HOME\\Mods\\save.sav", "C:\\elsewhere\\save.sav", "C:\\home\\save.sav"}) {
        Fixture f;
        f.fail_item = 1;
        f.services.exists = [&](std::string_view, std::string_view) { return false; };
        REQUIRE(f.load(path) == CoreLoadStatus::data);
        REQUIRE(f.files.remembered_subdirectory == (path[0] == 'c' ? "Mods" : "retained"));
        REQUIRE(f.files.game_stem == f.files.default_game_stem);
        REQUIRE(f.files.text.working == "C:\\home");
        REQUIRE(f.files.loaded_filename == "save.sav");
        REQUIRE(f.process.rules->definitions.cosmic.values[0] == 9);
    }
}
TEST_CASE("F06 core stops at every required item and never runs completion on failed input",
          "[session][core-load]") {
    Fixture baseline;
    REQUIRE(baseline.load() == CoreLoadStatus::success);
    // Last item is optional EVNT. Its failure is absence, not a required failure.
    for (std::size_t fail = 0; fail + 1 < baseline.items.size(); ++fail) {
        Fixture f;
        f.fail_item = fail;
        f.fail_prefix = 1;
        REQUIRE(f.load() == (fail == 0 ? CoreLoadStatus::input : CoreLoadStatus::data));
        REQUIRE(f.items.size() == fail + 1);
        REQUIRE(f.closes == 1);
        REQUIRE(std::find(f.trace.begin(), f.trace.end(), "reload:0") == f.trace.end());
        REQUIRE(f.runtime.world().mood.city == state::CitySlot{3});
        REQUIRE(f.runtime.world().layout.valid == 0);
    }
}
TEST_CASE("F06 core load supports all admitted legacy layouts with renewal before census",
          "[session][core-load]") {
    for (const auto version : {0x26U, 0x27U, 0x28U}) {
        Fixture f;
        f.bytes = legacy(saved(), static_cast<std::uint16_t>(version));
        f.process.tokens.next_unit = 0;
        f.runtime.world().civilizations[1].retained_65 = 91;
        REQUIRE(f.load() == CoreLoadStatus::success);
        REQUIRE(f.position == f.bytes.size());
        REQUIRE(f.runtime.world().entities.units.at(state::UnitSlot{0}).token ==
                state::UnitToken{0});
        REQUIRE(f.runtime.world().civilizations[1].units_active[2] == 0);
        REQUIRE(f.runtime.world().civilizations[1].units_in_production[3] == 1);
        REQUIRE(f.runtime.world().civilizations[1].retained_65 == (version < 0x28 ? 91 : 0));
        REQUIRE(f.runtime.gameplay_random().checkpoint().draws == 255);
    }
}
TEST_CASE("F06 core multiplayer adapts only after close and preserves loaded mode for entry zero",
          "[session][core-load]") {
    for (const Byte entry : {Byte{0}, Byte{1}}) {
        Fixture f;
        auto value = saved(false, true);
        value.globals.human_players = 0;
        value.leaders.clear();
        f.bytes = encode_stored_save(value);
        f.runtime.world().globals.multiplayer = entry;
        const auto open = f.services.open;
        f.services.open = [&](std::string_view path) {
            auto stream = open(path);
            stream->close = [&] {
                ++f.closes;
                REQUIRE(f.runtime.world().globals.multiplayer == 3);
                REQUIRE(f.runtime.world().globals.human_players == 0);
                REQUIRE(f.runtime.world().multiplayer.saved.double_movement == 1);
                REQUIRE(f.runtime.world().layout.valid == 1);
                return false;
            };
            return stream;
        };
        REQUIRE(f.load() == CoreLoadStatus::success);
        REQUIRE(f.runtime.world().globals.multiplayer == (entry == 0 ? 3 : 1));
        REQUIRE(f.runtime.world().globals.human_players == 2);
        REQUIRE(f.runtime.world().multiplayer.saved.double_movement == (entry == 0 ? 0 : 1));
        REQUIRE(f.runtime.gameplay_random().checkpoint().draws == 0);
    }
}
TEST_CASE("F06 core scenario transforms and events precede names census and rule reload",
          "[session][core-load]") {
    Fixture f;
    auto value = saved(true);
    value.events.emplace();
    StoredEvent event;
    event.record.turn = 19;
    event.record.unit_name = 1;
    event.strings.names[0] = Bytes{'U'};
    value.events->entries.push_back(event);
    f.bytes = encode_stored_save(value);
    const auto reload = f.services.reload_table;
    f.services.reload_table = [&](LoadedRuleTable table, bool initialization) {
        REQUIRE(f.runtime.world().events.loaded);
        REQUIRE(f.runtime.world().events.collection.at(0)->strings[0].value() == "U");
        REQUIRE(f.runtime.world().civilizations[1].units_active[2] == 1);
        REQUIRE(f.process.rules->leaders[0].leader_name < 0);
        reload(table, initialization);
    };
    REQUIRE(f.load() == CoreLoadStatus::success);
    REQUIRE(f.runtime.world().globals.game_flags == 0x80);
    REQUIRE((f.runtime.world().globals.game_options & 0x8000) == 0);
    REQUIRE(f.runtime.world().globals.reveal_map == 0);
    REQUIRE(f.runtime.world().scenario.technology_paradigm == -11);
    REQUIRE(f.runtime.world().layout.valid == 0);
    REQUIRE(f.runtime.world().events.collection.at(0)->values.turn == 19);
}
TEST_CASE("F06 core rule failures close input and preserve the exact preceding completion prefix",
          "[session][core-load]") {
    for (unsigned fail = 0; fail < 4; ++fail) {
        Fixture f;
        if (fail == 0)
            f.services.load_rules = [&](bool) {
                f.process.rules->leaders[0].times_used = 91;
                throw std::runtime_error("supplied full rule failure");
            };
        else
            f.services.reload_table = [&](LoadedRuleTable table, bool) {
                f.trace.push_back("reload:" + std::to_string(static_cast<unsigned>(table)));
                if (static_cast<unsigned>(table) + 1 == fail)
                    throw std::runtime_error("supplied table failure");
            };
        REQUIRE_THROWS_AS(f.load(), std::runtime_error);
        REQUIRE(f.closes == 1);
        REQUIRE(f.runtime.world().layout.valid == 0);
        REQUIRE(std::find(f.trace.begin(), f.trace.end(), "rules-close") == f.trace.end());
        if (fail == 0) {
            REQUIRE(f.items.size() == 1);
            REQUIRE(f.process.rules->leaders[0].times_used == 91);
            REQUIRE(f.runtime.world().mood.city == state::CitySlot{3});
        } else {
            REQUIRE_FALSE(f.runtime.world().mood.city);
            REQUIRE(f.runtime.world().civilizations[1].units_active[2] == 1);
        }
    }
}
TEST_CASE("F06 core requires explicit service and random bindings before opening input",
          "[session][core-load]") {
    Fixture f;
    f.services.load_rules = {};
    REQUIRE_THROWS_AS(f.load(), std::invalid_argument);
    REQUIRE(f.trace.empty());
    Fixture inactive;
    inactive.runtime.end_client_session();
    REQUIRE_THROWS_AS(inactive.load(), AccessError);
    REQUIRE(inactive.trace.empty());
}
TEST_CASE("F06 core composes real F03 tables and name preserving final reloads",
          "[session][core-load]") {
    Fixture f;
    std::string rules;
    for (const auto& section : classic_rule_fixture::complete_sections())
        rules += section;
    classic::TextWorkspace workspace;
    classic::TextReader reader(
        workspace, f.files.text,
        {[&](std::string_view directory, std::string_view name) -> classic::OpenedText {
             return {std::string(directory) + "\\" + std::string(name),
                     std::make_unique<classic::MemoryTextStream>(rules)};
         },
         {}},
        classic::ascii_text_case());
    classic::RuleLoader loader(reader, *f.process.rules, classic::ascii_text_case());
    std::size_t text_count = 0;
    f.services.load_rules = [&](bool initialize) {
        loader.load("RULES", initialize);
        text_count = f.process.rules->text.size();
    };
    f.services.reload_table = [&](LoadedRuleTable table, bool keep) {
        const auto section = table == LoadedRuleTable::advances       ? "CIVILIZE"
                             : table == LoadedRuleTable::improvements ? "IMPROVE"
                                                                      : "UNITS";
        REQUIRE(reader.open("RULES", section));
        if (table == LoadedRuleTable::advances)
            loader.advances(keep);
        else if (table == LoadedRuleTable::improvements)
            loader.improvements(keep);
        else
            loader.units(keep);
    };
    f.services.close_rules = [&] { reader.close(); };
    REQUIRE(f.load() == CoreLoadStatus::success);
    REQUIRE(f.process.rules->text.size() == text_count);
    REQUIRE_FALSE(reader.is_open());
    REQUIRE(f.process.rules->definitions.cosmic.values[0] == 3);
    REQUIRE(f.process.rules->leaders[0].leader_name < 0);
    REQUIRE(f.process.rules->leaders[0].plural < 0);
}
TEST_CASE("F06 composed map and entity failures preserve post callback untouched record bytes",
          "[session][core-load]") {
    Fixture baseline;
    REQUIRE(baseline.load() == CoreLoadStatus::success);
    for (const auto size : {std::size_t{24}, std::size_t{32}, std::size_t{88}}) {
        const auto found = std::find_if(baseline.items.begin(), baseline.items.end(),
                                        [&](const auto& item) { return item.second == size; });
        REQUIRE(found != baseline.items.end());
        const auto index = static_cast<std::size_t>(found - baseline.items.begin());
        Fixture f;
        f.fail_item = index;
        f.fail_prefix = 1;
        f.before_item = [&](std::size_t current) {
            if (current != index)
                return;
            f.runtime.world().map.tiles()[3].revelation = 91;
            f.runtime.world().entities.units.at(state::UnitSlot{0}).damage = 13;
            f.runtime.world().entities.cities.at(state::CitySlot{0}).food_box = 1234;
        };
        REQUIRE(f.load() == CoreLoadStatus::data);
        REQUIRE(f.runtime.world().map.tiles()[3].revelation == 91);
        REQUIRE(f.runtime.world().entities.units.at(state::UnitSlot{0}).damage == 13);
        REQUIRE(f.runtime.world().entities.cities.at(state::CitySlot{0}).food_box == 1234);
        REQUIRE(f.process.tokens == state::ProcessTokens{100, 200});
    }
    Fixture detached;
    detached.before_item = [&](std::size_t index) {
        if (index == 5) // First remembered layer after map allocation.
            detached.runtime.world().map.release();
    };
    REQUIRE_THROWS_AS(detached.load(), std::out_of_range);
    REQUIRE(detached.closes == 1);
}
TEST_CASE("F06 event failure keeps core status and earlier password effects without completion",
          "[session][core-load]") {
    Fixture f;
    f.bytes.insert(f.bytes.end(), {'E', 'V', 'N', 'T', 1, 0, 0, 0, 0x55});
    REQUIRE(f.load() == CoreLoadStatus::data);
    REQUIRE(f.runtime.gameplay_random().checkpoint().draws == 255);
    REQUIRE(f.runtime.world().events.collection.size() == 1);
    REQUIRE(f.runtime.world().events.collection.at(0)->values.triggers == 0x55);
    REQUIRE_FALSE(f.runtime.world().events.loaded);
    REQUIRE(f.runtime.world().mood.city == state::CitySlot{3});
    REQUIRE(f.runtime.world().civilizations[1].units_active[2] == 55);
    REQUIRE(f.closes == 1);
    REQUIRE(std::find(f.trace.begin(), f.trace.end(), "reload:0") == f.trace.end());
}
TEST_CASE("F06 core publishes loaded scan bounds before names and after a failed global prefix",
          "[session][core-load]") {
    for (const bool partial : {false, true}) {
        Fixture f;
        f.fail_item = partial ? 1U : 2U;    // Globals or following names item.
        f.fail_prefix = partial ? 50U : 0U; // Through both i16 scan-bound fields.
        f.before_item = [&](std::size_t index) {
            if (index == 2) {
                REQUIRE(f.runtime.world().entities.units.end() == 1);
                REQUIRE(f.runtime.world().entities.cities.end() == 1);
            }
        };
        REQUIRE(f.load() == CoreLoadStatus::data);
        REQUIRE(f.runtime.world().entities.units.end() == 1);
        REQUIRE(f.runtime.world().entities.cities.end() == 1);
        REQUIRE(f.process.tokens == state::ProcessTokens{100, 200});
    }
}
