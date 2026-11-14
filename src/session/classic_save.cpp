#include "session/classic_save.hpp"
#include "session/classic_file_choice.hpp"
#include "session/runtime.hpp"
#include "civilization/knowledge.hpp"
#include "formats/multiplayer_io.hpp"
#include "formats/world_record_io.hpp"
#include <bit>

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
                                 const CoreSaveServices& services,
                                 std::optional<std::uint8_t> incoming_cleanup_mask) {
    if (!services.open)
        throw std::invalid_argument("Core save requires an output-open binding");
    if (!runtime.active() || runtime.has_pending_commands())
        throw std::logic_error("Core save requires an active runtime with settled commands");
    const std::string request(requested_path);
    if (request.find('\0') != std::string::npos)
        throw std::invalid_argument("Core path must not contain NUL");
    auto& world = runtime.world();
    auto& globals = world.globals;
    auto restore_mask = incoming_cleanup_mask;
    if (globals.multiplayer == 1) {
        restore_mask = globals.human_players;
        globals.human_players = world.multiplayer.hotseat_participants;
    }
    std::unique_ptr<CoreOutput> output;
    bool cleaned = false;
    const auto cleanup = [&] {
        if (cleaned)
            return;
        cleaned = true;
        const bool unresolved = globals.multiplayer == 1 && !restore_mask;
        if (globals.multiplayer == 1 && restore_mask)
            globals.human_players = *restore_mask;
        if (runtime.active())
            runtime.publish();
        if (output && output->close)
            (void)output->close();
        if (unresolved)
            throw std::out_of_range("Hotseat cleanup has no supplied incoming human-mask backing");
    };
    try {
        select_classic_extension(scenario_export, globals.multiplayer, retained.extension);
        retained.opened_path = request;
        if (request.find('.') == std::string::npos)
            retained.opened_path += retained.extension;
        const auto path = retained.opened_path;
        runtime.publish();
        output = services.open(path);
        auto status = CoreSaveStatus::output;
        if (output) {
            if (!output->item || !output->text || !output->byte || !output->close)
                throw std::invalid_argument("Opened core output has incomplete service bindings");
            const WriteItem write = [&](std::span<const Byte> bytes) {
                runtime.publish();
                return output->item(bytes);
            };
            const WriteEventText text = [&](std::span<const Byte> bytes) {
                runtime.publish();
                return output->text(bytes);
            };
            const WriteEventByte byte = [&](Byte value) {
                runtime.publish();
                return output->byte(value);
            };
            const auto header = encode_game_header();
            for (std::size_t i = 0; i < 10; ++i)
                (void)byte(header[i]); // Header-character write failures are ignored.
            globals.saved_current_civ = std::bit_cast<std::int8_t>(
                static_cast<std::uint8_t>(world.multiplayer.local_human));
            const auto body = [&] {
                if (!write(std::span{header}.subspan(10)))
                    return false;
                prepare_scenario(scenario_export, world);
                prepare_scenario_names(scenario_export, globals, world.civilizations, world.names,
                                       *runtime.process().rules);
                check_scans(world);
                if (!write_persistent_items(write, globals, world.names, world.civilizations) ||
                    !write_map_items(write, MapStream::embedded, world.map, retained.starts) ||
                    !write_map_summary_items(write, world.map))
                    return false;
                check_scans(world);
                if (!write_entity_items(write, world.entities) ||
                    !write_leader_items(write, globals, world.civilizations,
                                        runtime.process().rules->leaders, retained.cursor) ||
                    !write_history_layout_items(write, world.power_history, retained.current_screen,
                                                retained.packed_layout, world.layout,
                                                world.views) ||
                    !write_scenario_history_items(write, scenario_export, globals, world.scenario,
                                                  world.eliminations))
                    return false;
                if (globals.multiplayer != 0 &&
                    !write_multiplayer_item(write, world.multiplayer,
                                            world.multiplayer.network_turn_civ))
                    return false;
                return !(world.events.loaded && !scenario_export) ||
                       write_event_items(write, text, byte, world.events.collection);
            };
            if (body())
                status = CoreSaveStatus::success;
        }
        cleanup();
        if (runtime.active())
            runtime.publish();
        return status;
    } catch (...) {
        cleanup();
        if (runtime.active())
            runtime.publish();
        throw;
    }
}
} // namespace openciv::session
