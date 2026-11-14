#include "session/classic_load_dialog.hpp"
#include "session/runtime.hpp"
#include <utility>

namespace openciv::session {
namespace {
void publish(SessionRuntime& runtime) {
    if (runtime.active())
        runtime.publish();
}
template <typename R, typename... Args>
std::function<R(Args...)> published(SessionRuntime& runtime, std::function<R(Args...)> service) {
    if (!service)
        return {};
    return [&runtime, service = std::move(service)](Args... args) -> R {
        publish(runtime);
        return service(std::forward<Args>(args)...);
    };
}
void wrap_network(SessionRuntime& runtime, unit::UnitNetworkServices& services) {
    services.state_difference = published(runtime, services.state_difference);
    services.flush = published(runtime, services.flush);
    services.ticks = published(runtime, services.ticks);
    services.process_messages = published(runtime, services.process_messages);
    services.priority_messages = published(runtime, services.priority_messages);
    services.normal_messages = published(runtime, services.normal_messages);
    services.log_timeout = published(runtime, services.log_timeout);
    services.show_timeout = published(runtime, services.show_timeout);
}
void cleanup(SessionRuntime& runtime, unit::StackBudget& budget,
             const LoadDialogServices& services) {
    auto stacks = services.stacks;
    auto placement = services.placement;
    wrap_network(runtime, placement);
    placement.request = published(runtime, placement.request);
    auto deletion = services.deletion;
    wrap_network(runtime, deletion);
    deletion.removed_unit = published(runtime, deletion.removed_unit);
    deletion.refresh_home = published(runtime, deletion.refresh_home);
    deletion.check_destroyed = published(runtime, deletion.check_destroyed);
    deletion.unit_change = published(runtime, deletion.unit_change);
    deletion.home_change = published(runtime, deletion.home_change);
    deletion.request_delete = published(runtime, deletion.request_delete);
    cleanup_classic_loaded_units(runtime.world(), runtime.process().rules->definitions, budget,
                                 std::move(stacks), std::move(placement), std::move(deletion));
}
void choose_alternative(SessionRuntime& runtime, std::string& path,
                        const LoadDialogServices& services, const std::string& call) {
    publish(runtime);
    if (services.input.comparison.uppercase_path(path).find(".SCN") == std::string::npos)
        return;
    const auto directory = path.substr(0, path.rfind('\\'));
    publish(runtime);
    const auto entries = services.enumerate(directory, "*.ALT");
    if (entries.empty())
        return;
    // Keep the specified product in the nonnegative signed-32 domain. This
    // declared resource bound does not reinterpret overflow as a file choice.
    if (entries.size() > 65535)
        throw std::length_error("Scenario alternatives exceed admitted enumeration backing");
    const auto draw = runtime.gameplay_random().primary_code(
        base::ChoiceKey{"F06.scenario-alternative", call, 0});
    const auto choice = (draw * (static_cast<std::int32_t>(entries.size()) + 1)) / 32768;
    if (choice == 0)
        return;
    publish(runtime);
    const auto selected = services.enumerate(directory, "*.ALT");
    if (selected != entries)
        throw std::invalid_argument("Scenario alternative enumerations are inconsistent");
    const auto& name = selected.at(static_cast<std::size_t>(choice - 1));
    if (name.empty() || name.find_first_of("\\/:") != std::string::npos ||
        name.find('\0') != std::string::npos || name == "." || name == "..")
        throw std::invalid_argument("Scenario alternative is not an unqualified filename");
    path = directory + "\\" + name;
}
} // namespace

std::optional<CoreLoadStatus>
load_classic_dialog(SessionRuntime& runtime, ClassicFileState& files,
                    ClassicLoadDialogState& dialog, std::optional<std::string> selected_path,
                    bool show_title, const LoadDialogServices& services,
                    unit::StackBudget& stack_budget, std::string_view call) {
    const auto home = [&] {
        files.text.working = files.text.home;
        publish(runtime);
        services.input.change_directory(files.text.working);
    };
    try {
        if (!selected_path) {
            home();
            publish(runtime);
            return {};
        }
        if (selected_path->find('\0') != std::string::npos ||
            selected_path->rfind('\\') == std::string::npos)
            throw std::invalid_argument("Load dialog requires a backed full path without NUL");
        if (runtime.has_pending_commands())
            throw std::logic_error("Load dialog requires settled session commands");
        (void)runtime.gameplay_random();
        const std::string call_identity(call);
        for (std::int32_t i = 1; i < 8; ++i) {
            publish(runtime);
            services.close_map_window(i);
        }
        choose_alternative(runtime, *selected_path, services, call_identity);
        const auto status =
            load_classic_core(runtime, files, *selected_path, services.input, call_identity);
        if (status != CoreLoadStatus::success) {
            publish(runtime);
            services.load_notice(status);
            dialog.loaded_file_kind = 0;
            publish(runtime);
            return status;
        }
        publish(runtime);
        services.input.flush();
        auto& world = runtime.world();
        auto& events = world.events;
        if (!events.loaded && (world.globals.game_flags & 0x80U)) {
            publish(runtime);
            if (services.input.exists(files.scenario_directory, "EVENTS." + files.text.extension)) {
                events.collection.reset();
                publish(runtime);
                if (services.parse_events("EVENTS", "BEGINEVENTS")) {
                    auto resources = services.input.events;
                    resources.allocate = published(runtime, resources.allocate);
                    resources.report = published(runtime, resources.report);
                    if (!state::compact_events(events.collection, resources))
                        throw std::bad_alloc();
                    events.loaded = true;
                }
            }
        }
        if (!events.loaded)
            events.collection.reset();
        world.globals.game_options = runtime.process().ordinary_preferences.game_options;
        publish(runtime);
        services.configure_sound((world.globals.game_options & 0x10U) != 0);
        if ((world.globals.game_options & 0x8U) && events.loaded) {
            publish(runtime);
            services.scenario_loaded();
        }
        publish(runtime);
        services.synchronize_music();
        if (show_title) {
            publish(runtime);
            services.present_title();
        }
        home();
        cleanup(runtime, stack_budget, services);
        dialog.chosen_path = *selected_path;
        publish(runtime);
        return status;
    } catch (...) {
        publish(runtime);
        throw;
    }
}
} // namespace openciv::session
