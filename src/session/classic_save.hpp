#pragma once

#include "session/classic_load.hpp"

namespace openciv::session {
enum class CoreSaveStatus { success = 0, output = 1 };
struct CoreOutput {
    formats::WriteItem item;
    formats::WriteEventText text;
    formats::WriteEventByte byte;
    std::function<bool()> close;
};
struct CoreSaveServices {
    // Binary replacement: a native provider must truncate an existing target.
    // Failed writes leave a partial file. Close failure does not change status.
    std::function<std::unique_ptr<CoreOutput>(std::string_view)> open;
};
// F06 section 5 live operation. Requires an active runtime with settled commands
// and matching global/pool scan bounds. No whole-world snapshot or direct RNG
// draw. Saved-current, scenario metadata/names and layout mutations persist.
// If a callback enters hotseat from another mode, original incoming cleanup
// backing must be supplied; otherwise cleanup closes the file and stops safely.
CoreSaveStatus save_classic_core(SessionRuntime& runtime, ClassicFileState& retained,
                                 std::string_view requested_path, bool scenario_export,
                                 const CoreSaveServices& services,
                                 std::optional<std::uint8_t> incoming_cleanup_mask = {});
} // namespace openciv::session
