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
    // The provider owns replacement/commit policy. A false write or close is
    // failure; providers must not reenter or mutate game state.
    std::function<std::unique_ptr<CoreOutput>(std::string_view)> open;
};
// F06 save/load production boundary: prepare a coherent image before opening
// output, retaining accepted preparation effects. No RNG or per-write game events.
// An opened output is closed once, including failure and exception paths.
CoreSaveStatus save_classic_core(SessionRuntime& runtime, ClassicFileState& retained,
                                 std::string_view requested_path, bool scenario_export,
                                 const CoreSaveServices& services);
} // namespace openciv::session
