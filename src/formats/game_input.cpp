#include "formats/game_input.hpp"
#include "formats/save_aux_io.hpp"

namespace openciv::formats {
world::MapInput map_input(RawMapFile map) {
    world::MapInput result;
    result.width = map.header.width;
    result.height = map.header.height;
    result.topology = map.header.topology;
    result.seed = map.header.seed;
    result.tiles = std::move(map.tiles);
    world::validate_map_input(result, false);
    return result;
}
state::GameInput game_input(StoredSave save) {
    state::GameInput result;
    result.globals = save.globals;
    result.names = save.names;
    result.civilizations = std::move(save.civilizations);
    result.map.width = save.map.header.width;
    result.map.height = save.map.header.height;
    result.map.topology = save.map.header.topology;
    result.map.seed = save.map.header.seed;
    result.map.tiles = std::move(save.map.tiles);
    result.map.remembered = std::move(save.map.remembered);
    result.map.coarse = std::move(save.map.coarse);
    result.map.regions = save.map.regions;
    result.units = std::move(save.units);
    result.cities = std::move(save.cities);
    // Older records omit occupancy tokens; every scanned record is occupied.
    // Installation always replaces these source markers with fresh live identities.
    if (save.header.version < 0x29) {
        for (auto& unit : result.units)
            unit.token = state::UnitToken{1};
        for (auto& city : result.cities)
            city.token = state::CityToken{1};
    }
    for (std::size_t i = 0; i < result.tribes.size(); ++i)
        result.tribes[i] = {save.tribes[i].times_used, save.tribes[i].city_name_index,
                            save.tribes[i].female};
    std::size_t index = 0;
    const auto leader = [&](std::size_t civ) {
        const auto tribe = result.civilizations.at(civ).tribe;
        if (tribe < 0 || tribe >= 21)
            throw std::invalid_argument("Saved civilization has an invalid tribe");
        const auto& stored = save.leaders.at(index++);
        state::LeaderCustomization value;
        value.plural = stored.plural < 0;
        value.adjective = stored.adjective < 0;
        for (std::size_t g = 0; g < 7; ++g)
            for (std::size_t sex = 0; sex < 2; ++sex)
                value.titles[g][sex] = stored.titles[g][sex] < 0;
        result.leaders[static_cast<std::size_t>(tribe)] = value;
    };
    if (save.globals.multiplayer == 0)
        leader(static_cast<std::size_t>(save.globals.player_civ));
    else
        for (std::size_t civ = 0; civ < 8; ++civ)
            if (save.globals.human_players & (1U << civ))
                leader(civ);
    result.cursor_x = save.cursor_x;
    result.cursor_y = save.cursor_y;
    result.power_history = save.power_history;
    unpack_layout(save.layout, result.layout);
    result.views = save.views;
    result.scenario = save.scenario;
    result.eliminations = save.eliminations;
    result.multiplayer = save.multiplayer;
    if (save.events) {
        result.events.emplace();
        for (auto& event : save.events->entries) {
            state::EventInput input;
            input.values = event.record;
            std::size_t field = 0;
            const auto strings = [&](auto& values) {
                for (auto& value : values) {
                    if (value)
                        input.strings[field] = std::string(value->begin(), value->end());
                    ++field;
                }
            };
            strings(event.strings.names);
            strings(event.strings.text);
            strings(event.strings.other);
            result.events->push_back(std::move(input));
        }
    }
    world::validate_map_input(result.map, true);
    return result;
}
} // namespace openciv::formats
