#pragma once

#include "session/classic_save.hpp"
#include "state/game.hpp"

namespace openciv::session {
struct PreferenceFileServices {
    std::function<std::unique_ptr<CoreInput>(std::string_view)> open_input;
    std::function<std::unique_ptr<CoreOutput>(std::string_view)> open_output;
    std::function<bool(std::string_view)> remove_file;
    void validate_output() const;
};
enum class PreferenceLoadStatus { success, defaults, input };
// Stage both independent items against entry snapshots and close before install.
// Short items receive their specified fallback defaults; close failure and
// exceptions leave installed preferences unchanged. Missing files default both.
PreferenceLoadStatus load_classic_preferences(state::ProcessContext& process,
                                              const state::PreferenceDefaults& defaults,
                                              const PreferenceFileServices& services);
enum class PreferenceSaveStatus { success = 0, output = 1 };
// F03 production preference contract: coherent configuration, truthful write
// and close results, exactly-once close. Failed output is removed when possible;
// accepted preferences remain. Providers do not admit configuration mutations.
PreferenceSaveStatus save_classic_preferences(const state::ProcessContext& process,
                                              const PreferenceFileServices& services);
// Synchronous owner adapter: publish before open/write/close/remove.
PreferenceSaveStatus save_classic_preferences(SessionRuntime& runtime,
                                              const PreferenceFileServices& services);
} // namespace openciv::session
