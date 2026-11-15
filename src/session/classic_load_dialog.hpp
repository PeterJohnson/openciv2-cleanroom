#pragma once

#include "session/classic_load.hpp"
#include "session/classic_loaded_units.hpp"

namespace openciv::session {
struct ClassicLoadDialogState {
    std::string chosen_path;
    // Opaque caller classification. This wrapper only clears it on core failure.
    std::int32_t loaded_file_kind = 0;
};
struct LoadDialogServices {
    CoreLoadServices input;
    std::function<void(std::int32_t index)> close_map_window;
    // Return one owned listing of matching, unqualified filenames in service order.
    std::function<std::vector<std::string>(std::string_view directory, std::string_view pattern)>
        enumerate;
    // Required S02 and P10/P01 operations when reached. Parsing appends to the
    // live event collection and returns success; the parent owns reset/compact.
    std::function<bool(std::string_view stem, std::string_view section)> parse_events;
    std::function<void()> scenario_loaded, synchronize_music, present_title;
    std::function<void(bool enabled)> configure_sound;
    std::function<void(CoreLoadStatus)> load_notice;
    unit::StackServices stacks;
    unit::StackOperationServices placement;
    unit::DeletionServices deletion;
};
// F06 section 11 accepted-file load dialog, composed with real core input,
// S02 compaction and U01 cleanup. The native chooser is a separate boundary.
// nullopt input/return means cancellation; otherwise return the core status.
// Accepted input requires initialized randomness and settled commands. Paths
// have no embedded NUL and a final backslash. ALT enumerations above 65535
// entries or unqualified-name violations stop safely. Required wiring is
// validated before effects; bound semantic children retain their completed prefix. Compaction
// allocation failure stops before loaded=true.
std::optional<CoreLoadStatus>
load_classic_dialog(SessionRuntime& runtime, ClassicFileState& files,
                    ClassicLoadDialogState& dialog, std::optional<std::string> selected_path,
                    bool show_title, const LoadDialogServices& services,
                    unit::StackBudget& stack_budget, std::string_view call);
} // namespace openciv::session
