#include "session/classic_load_dialog.hpp"
#include "session/runtime.hpp"
#include <utility>

namespace openciv::session {
namespace {
void publish(SessionRuntime& runtime) {
    if (runtime.active())
        runtime.publish();
}
class PublishedDeletion final : public unit::UnitDeletionEffects {
public:
    PublishedDeletion(SessionRuntime& runtime, const unit::UnitDeletionEffects& effects)
        : runtime_(runtime), effects_(effects) {}
    void unit_removed(world::Position position) const override {
        publish(runtime_);
        effects_.unit_removed(position);
    }
    void refresh_home_city(std::int32_t city) const override {
        publish(runtime_);
        effects_.refresh_home_city(city);
    }
    void check_civilization(std::int32_t owner, std::int32_t killer) const override {
        publish(runtime_);
        effects_.check_civilization(owner, killer);
    }
    void synchronize_deletion(world::Position position,
                              std::optional<state::CitySlot> home) const override {
        publish(runtime_);
        effects_.synchronize_deletion(position, home);
    }

private:
    SessionRuntime& runtime_;
    const unit::UnitDeletionEffects& effects_;
};
void cleanup(SessionRuntime& runtime, unit::StackBudget& budget,
             const LoadDialogDependencies& services) {
    auto synchronization = [&] {
        publish(runtime);
        services.placement();
    };
    unit::StackSynchronization placement;
    if (services.placement)
        placement = synchronization;
    PublishedDeletion deletion(runtime, *services.deletion);
    cleanup_classic_loaded_units(runtime.world(), runtime.process().rules->definitions, budget,
                                 services.stacks, placement, deletion);
}
void choose_alternative(SessionRuntime& runtime, std::string& path,
                        const LoadDialogDependencies& services, const std::string& call) {
    publish(runtime);
    if (services.input.comparison->uppercase_path(path).find(".SCN") == std::string::npos)
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
    const auto& name = entries.at(static_cast<std::size_t>(choice - 1));
    if (name.empty() || name.find_first_of("\\/:") != std::string::npos ||
        name.find('\0') != std::string::npos || name == "." || name == "..")
        throw std::invalid_argument("Scenario alternative is not an unqualified filename");
    path = directory + "\\" + name;
}
} // namespace

std::optional<CoreLoadStatus>
load_classic_dialog(SessionRuntime& runtime, ClassicFileState& files,
                    ClassicLoadDialogState& dialog, std::optional<std::string> selected_path,
                    bool show_title, const LoadDialogDependencies& supplied,
                    unit::StackBudget& stack_budget, std::string_view call) {
    const auto& services = supplied;
    if (!services.input.change_directory)
        throw std::invalid_argument("Load workflow requires directory service");
    if (selected_path) {
        services.input.validate();
        if (!services.close_map_window || !services.enumerate ||
            !services.input.comparison->uppercase_path || !services.load_events ||
            !services.scenario_loaded || !services.synchronize_music || !services.configure_sound ||
            !services.load_notice || !services.deletion || (show_title && !services.present_title))
            throw std::invalid_argument("Load workflow requires complete semantic services");
    }
    std::optional<ClassicLoadOperation> operation;
    if (selected_path)
        operation.emplace(runtime, files, services.input);
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
        const auto status = operation->load(*selected_path, call_identity);
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
                publish(runtime);
                if (auto catalog = services.load_events()) {
                    state::install_events(*catalog, events.collection);
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
