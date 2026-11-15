import std;
import openciv.session.classic_save;
import openciv.session.classic_file_choice;
import openciv.session.runtime;
import openciv.session.game_install;
import openciv.civilization.knowledge;
import openciv.formats.multiplayer_io;
import openciv.formats.world_record_io;

namespace openciv::session {
namespace {
using namespace formats;
void prepare_scenario(bool scenario, state::GameState& world) {
    auto& globals = world.globals;
    if (!scenario) {
        globals.game_flags &= static_cast<std::uint16_t>(~0x40U);
        return;
    }
    globals.game_flags |= 0x40;
    for (std::int32_t advance = 0; advance < 100; ++advance) {
        const auto index = static_cast<std::size_t>(advance);
        globals.advance_known_by[index] = 0;
        unsigned possessors = 0;
        std::uint8_t first = 0;
        for (std::int32_t civ = 1; civ < 8; ++civ)
            if (civilization::knows_advance(world.civilizations, civ, advance)) {
                globals.advance_known_by[index] |= static_cast<std::uint8_t>(1U << civ);
                first = static_cast<std::uint8_t>(civ);
                ++possessors;
            }
        if (possessors != 0)
            globals.first_discoverer[index] = possessors == 1 ? first : 8;
    }
    for (std::size_t civ = 1; civ < 8; ++civ)
        world.civilizations[civ].flags &= static_cast<std::uint16_t>(~0x69U);
}
void check_scans(const state::GameState& world) {
    if (world.globals.unit_end < 0 || world.globals.city_end < 0 ||
        static_cast<std::size_t>(world.globals.unit_end) != world.entities.units.end() ||
        static_cast<std::size_t>(world.globals.city_end) != world.entities.cities.end())
        throw std::out_of_range("Live save requires matching global and pool scan bounds");
}
} // namespace
CoreSaveStatus save_classic_core(SessionRuntime& runtime, ClassicFileState& retained,
                                 std::string_view requested_path, bool scenario_export,
                                 const FileStorage& storage) {
    if (!runtime.active() || runtime.has_pending_commands())
        throw std::logic_error("Core save requires an active runtime with settled commands");
    const std::string request(requested_path);
    if (request.find('\0') != std::string::npos)
        throw std::invalid_argument("Core path must not contain NUL");
    auto& world = runtime.world();
    auto& globals = world.globals;
    check_scans(world);
    select_classic_extension(scenario_export, globals.multiplayer, retained.extension);
    retained.opened_path = request;
    if (request.find('.') == std::string::npos)
        retained.opened_path += retained.extension;

    Bytes bytes;
    try {
        // F06: accepted preparation effects are explicit; encoding has no external
        // callbacks. Temporary file-only values never enter the authoritative world.
        globals.saved_current_civ =
            std::bit_cast<std::int8_t>(static_cast<std::uint8_t>(world.multiplayer.local_human));
        prepare_scenario(scenario_export, world);
        prepare_scenario_names(scenario_export, globals, world.civilizations, world.names,
                               *runtime.process().rules);
        auto exported = globals;
        if (exported.multiplayer == state::GameMode::hotseat)
            exported.human_players = world.multiplayer.hotseat_participants;
        bytes = encode_game_header();
        const WriteItem append = [&](std::span<const Byte> value) {
            bytes.insert(bytes.end(), value.begin(), value.end());
            return true;
        };
        if (retained.record_bytes.lifetime.lock() != world.lifetime)
            retained.record_bytes = {};
        write_persistent_items(append, exported, world.names, world.civilizations,
                               retained.record_bytes);
        write_map_items(append, MapStream::embedded, world.map, retained.starts);
        write_map_summary_items(append, world.map);
        write_entity_items(append, world.entities, retained.record_bytes);
        write_leader_items(append, exported, world.civilizations, runtime.process().rules->leaders,
                           retained.cursor);
        write_history_layout_items(append, world.power_history, retained.current_screen,
                                   retained.packed_layout, world.layout, world.views);
        write_scenario_history_items(append, scenario_export, exported, world.scenario,
                                     world.eliminations);
        if (exported.multiplayer != state::GameMode::single_player)
            write_multiplayer_item(append, world.multiplayer, world.multiplayer.network_turn_civ,
                                   retained.record_bytes);
        if (world.events.loaded && !scenario_export)
            write_event_items(
                append, append,
                [&](Byte value) {
                    bytes.push_back(value);
                    return true;
                },
                world.events.collection, retained.event_bytes);
    } catch (...) {
        runtime.publish();
        throw;
    }
    runtime.publish();

    auto output = storage.replace(retained.opened_path);
    if (!output)
        return CoreSaveStatus::output;
    const bool written = output->item(bytes);
    const bool finalized = output->close();
    return written && finalized ? CoreSaveStatus::success : CoreSaveStatus::output;
}
} // namespace openciv::session
