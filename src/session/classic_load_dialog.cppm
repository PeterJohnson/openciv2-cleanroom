export module openciv.session.classic_load_dialog;

import std;
export import openciv.base.callback_ref;
export import openciv.session.classic_load;
export import openciv.session.classic_loaded_units;

export namespace openciv::session {
/// Retained file-dialog selections and options for classic game loading.
struct ClassicLoadDialogState {
    std::string chosen_path;
    // Opaque caller classification. This wrapper only clears it on core failure.
    std::int32_t loaded_file_kind = 0;
};
/// Borrowed providers and callable owners must outlive the consuming operation.
struct LoadDialogDependencies {
    CoreLoadDependencies input;
    base::CallbackRef<void(std::int32_t index)> close_map_window;
    /// Return one owned listing of matching, unqualified filenames in service order.
    base::CallbackRef<std::vector<std::string>(std::string_view directory,
                                               std::string_view pattern)>
        enumerate;
    /// Source-specific selection/parsing returns owned data; nullopt means failure.
    /// The source never receives or modifies the installed event collection.
    base::CallbackRef<std::optional<state::EventCatalog>()> load_events;
    base::CallbackRef<void()> scenario_loaded, synchronize_music, present_title;
    base::CallbackRef<void(bool enabled)> configure_sound;
    base::CallbackRef<void(CoreLoadStatus)> load_notice;
    unit::StackDiagnostics stacks = nullptr;
    unit::StackSynchronization placement;
    const unit::UnitDeletionEffects* deletion = nullptr;
};
/// F06 section 11 accepted-file load dialog, composed with real core input,
/// shared event installation and U01 cleanup. The native chooser is a separate boundary.
/// nullopt input/return means cancellation; otherwise return the core status.
/// Accepted input requires initialized randomness and settled commands. Paths
/// have no embedded NUL and a final backslash. ALT enumerations above 65535
/// entries or unqualified-name violations stop safely. Required wiring is
/// validated before effects; bound semantic children retain their completed prefix. Event
/// installation failure stops before loaded=true.
std::optional<CoreLoadStatus>
load_classic_dialog(SessionRuntime& runtime, ClassicFileState& files,
                    ClassicLoadDialogState& dialog, std::optional<std::string> selected_path,
                    bool show_title, const LoadDialogDependencies& services,
                    unit::StackBudget& stack_budget, std::string_view call);
} // namespace openciv::session
