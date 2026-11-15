#pragma once
#include "service_callbacks.hpp"
#include "classic_stream_fixture.hpp"
#include "session/classic_load.hpp"
#include "formats/classic_save.hpp"
#include <catch2/catch_test_macros.hpp>
namespace load_fixture {
using namespace openciv;
using namespace openciv::formats;
using namespace openciv::session;
inline StoredSave saved(bool scenario = false, bool multiplayer = false) {
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
inline Bytes legacy(const StoredSave& save, std::uint16_t version) {
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
    CoreLoadCallbacks services;
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
        const auto id = state::NameReference{process.rules->text.store("Label"), false};
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
            return test::input_file(
                {[&](std::size_t n) {
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
                     return true;
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
        services.load_content = [&] {
            trace.push_back("content");
            auto input = std::make_unique<state::RuleInput>();
            input->definitions.cosmic.values[0] = 9;
            return state::LoadedContent{std::move(input), {}};
        };
    }
    CoreLoadStatus load(std::string_view path = "C:\\home\\Scenario\\Example") {
        return load_classic_core(runtime, files, path, services, "load/1");
    }
};
} // namespace load_fixture
