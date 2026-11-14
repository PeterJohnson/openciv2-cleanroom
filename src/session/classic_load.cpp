#include "session/classic_load.hpp"
#include "session/classic_file_choice.hpp"
#include "session/runtime.hpp"
#include "formats/multiplayer_io.hpp"
#include "formats/world_record_io.hpp"

namespace openciv::session {
namespace {
using namespace formats;
CoreLoadStatus header(const ReadItem& read, const ReadEventByte& byte, std::uint16_t& version) {
    std::string text;
    bool terminated = false;
    for (unsigned i = 0; i < 79; ++i) {
        const auto value = byte().value_or(0xff);
        if (value == 0) {
            terminated = true;
            break;
        }
        text.push_back(static_cast<char>(value));
    }
    (void)byte(); // Separator is consumed even for a bad/unterminated header.
    if (!terminated)
        throw std::out_of_range("Core header comparison has no terminated supplied backing");
    if (text != "CIVILIZE")
        return CoreLoadStatus::input;
    const auto item = read(2);
    if (item.prefix.size() > 2 || (item.complete && item.prefix.size() != 2))
        throw std::invalid_argument("Version service returned an inconsistent transferred prefix");
    if (!item.complete)
        return CoreLoadStatus::input;
    version = ByteReader(item.prefix).u16();
    switch (game_version_status(version)) {
    case VersionStatus::unsupported_old:
        return CoreLoadStatus::unsupported;
    case VersionStatus::newer:
        return CoreLoadStatus::newer;
    case VersionStatus::supported:
        return CoreLoadStatus::success;
    }
    throw std::logic_error("Unknown version status");
}
void paths(ClassicFileState& retained, const std::string& request,
           const CoreLoadServices& services) {
    const auto separator = request.rfind('\\');
    if (separator == std::string::npos)
        throw std::out_of_range("Core load requires an established final backslash");
    retained.scenario_directory = request.substr(0, separator);
    retained.text.working = retained.scenario_directory;
    services.change_directory(retained.text.working);
    const auto resource = "GAME." + retained.text.extension;
    const bool custom = services.exists(retained.text.working, resource);
    retained.game_stem =
        custom ? retained.scenario_directory + "\\GAME" : retained.default_game_stem;
    retained.loaded_filename = request.substr(separator + 1);
    const auto home = retained.text.home;
    if (request.size() >= home.size() &&
        services.comparison.equal(request.substr(0, home.size()), home) &&
        separator > home.size()) {
        auto relative = request.substr(home.size(), separator - home.size());
        if (!relative.empty() && relative.front() == '\\')
            relative.erase(0, 1);
        if (!relative.empty())
            retained.remembered_subdirectory = std::move(relative);
    }
    retained.text.working = retained.text.home;
    services.change_directory(retained.text.working);
}
} // namespace
CoreLoadStatus load_classic_core(SessionRuntime& runtime, ClassicFileState& retained,
                                 std::string_view requested_path, const CoreLoadServices& services,
                                 std::string_view password_reset_call) {
    if (!services.open || !services.flush || !services.change_directory || !services.exists ||
        !services.comparison.equal || !services.load_rules || !services.reload_table ||
        !services.close_rules)
        throw std::invalid_argument("Core load requires complete file/path/rule service bindings");
    if (runtime.has_pending_commands())
        throw std::logic_error("Core load requires settled session commands");
    auto& random = runtime.gameplay_random(); // Check initialization before any effects.
    auto& world = runtime.world();
    auto& process = runtime.process();
    const std::string request(requested_path), reset_call(password_reset_call);
    if (request.find('\0') != std::string::npos)
        throw std::invalid_argument("Core path must not contain NUL");
    const auto entry_mode = world.globals.multiplayer;
    select_classic_extension(false, entry_mode, retained.extension);
    retained.opened_path = request;
    if (request.find('.') == std::string::npos)
        retained.opened_path += retained.extension;
    const auto opened_path = retained.opened_path;
    auto input = services.open(opened_path);
    if (!input) {
        runtime.publish();
        return CoreLoadStatus::input;
    }
    bool closed = false;
    const auto close = [&] {
        if (!closed) {
            closed = true;
            if (runtime.active())
                runtime.publish();
            if (input->close)
                (void)input->close();
        }
    };
    try {
        if (!input->item || !input->byte || !input->close)
            throw std::invalid_argument("Opened core input has incomplete service bindings");
        const auto flush = [&] {
            runtime.publish();
            services.flush();
        };
        const ReadItem read = [&](std::size_t n) {
            runtime.publish();
            return input->item(n);
        };
        const ReadEventByte byte = [&] {
            runtime.publish();
            return input->byte();
        };
        std::uint16_t version = 0;
        auto status = header(read, byte, version);
        if (status != CoreLoadStatus::success) {
            close();
            runtime.publish();
            return status;
        }
        flush();
        paths(retained, request, services);
        flush();
        services.load_rules(true);
        status = CoreLoadStatus::data;
        flush();
        const auto body = [&] {
            bool scenario = false;
            const auto scans = [&] {
                world.entities.units.set_end(static_cast<std::size_t>(world.globals.unit_end));
                world.entities.cities.set_end(static_cast<std::size_t>(world.globals.city_end));
            };
            if (!read_persistent_items(read, version, world.globals, world.names,
                                       world.civilizations, scenario, scans))
                return false;
            if (!read_map_items(read, MapStream::embedded, world.map, retained.starts,
                                services.allocate_map) ||
                !read_map_summary_items(read, world.map, flush))
                return false;
            scans(); // Retain admitted later callback changes before entity input.
            if (!read_entity_items(read, version, world.entities, process.tokens) ||
                !read_leader_items(read, world.globals, world.civilizations, process.rules->leaders,
                                   retained.cursor, retained.leaders) ||
                !read_history_layout_items(read, world.power_history, retained.packed_layout,
                                           world.layout, world.views, flush) ||
                !read_scenario_history_items(read, scenario, world.globals, world.scenario,
                                             world.eliminations) ||
                !read_multiplayer_items(read, version, world.globals, world.multiplayer,
                                        process.multiplayer_preferences, random, reset_call) ||
                !read_event_items(read, byte, world.events, services.events))
                return false;
            flush();
            restore_leader_names(scenario, world.globals, world.civilizations, world.names,
                                 *process.rules, retained.leaders);
            flush();
            state::rebuild_loaded_unit_census(world.civilizations, world.entities);
            world.mood.city.reset();
            for (const auto table : {LoadedRuleTable::advances, LoadedRuleTable::improvements,
                                     LoadedRuleTable::units}) {
                runtime.publish();
                services.reload_table(table, true);
            }
            services.close_rules();
            finish_loaded_layout(world.layout, retained.current_screen, scenario);
            return true;
        };
        if (body())
            status = CoreLoadStatus::success;
        close();
        if (status == CoreLoadStatus::success)
            state::adapt_loaded_mode(entry_mode, world.globals, world.multiplayer,
                                     process.multiplayer_preferences);
        runtime.publish();
        return status;
    } catch (...) {
        close();
        if (runtime.active())
            runtime.publish();
        throw;
    }
}
} // namespace openciv::session
