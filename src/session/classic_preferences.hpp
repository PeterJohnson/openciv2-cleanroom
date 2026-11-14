#pragma once

#include "session/classic_save.hpp"
#include "state/game.hpp"

namespace openciv::session {
struct PreferenceFileServices {
    std::function<std::unique_ptr<CoreInput>(std::string_view)> open_input;
    std::function<std::unique_ptr<CoreOutput>(std::string_view)> open_output;
    std::function<bool(std::string_view)> remove_file;
};
// F03 section 8, F06 CIV2.DAT: exactly two independent item attempts on load;
// each failed item gets its own defaults over its transferred prefix. No seek,
// general validation, world-options copy or session initialization is added.
void load_classic_preferences(state::ProcessContext& process,
                              const state::PreferenceDefaults& defaults,
                              const PreferenceFileServices& services);
enum class PreferenceSaveStatus { success = 0, output = 1 };
// Binary replacement, stop at first failed write, close, then delete partial
// output on returned write failure. Open failure does not delete. Close/delete
// failure is ignored. Required service exceptions retain their reached prefix.
PreferenceSaveStatus save_classic_preferences(const state::ProcessContext& process,
                                              const PreferenceFileServices& services);
} // namespace openciv::session
