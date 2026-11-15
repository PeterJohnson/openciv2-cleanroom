export module openciv.session.classic_preferences;

import std;
export import openciv.session.file_storage;
export import openciv.state.game;

export extern "C++" {
    namespace openciv::session {
    class SessionRuntime;

    enum class PreferenceLoadStatus { success, defaults, input };
    // Stage both independent items against data snapshots and close before install.
    // The storage provider is borrowed for this synchronous call.
    // Short items receive their specified fallback defaults; close failure and
    // exceptions leave installed preferences unchanged. Missing files default both.
    PreferenceLoadStatus load_classic_preferences(state::ProcessContext& process,
                                                  const state::PreferenceDefaults& defaults,
                                                  const FileStorage& services);
    enum class PreferenceSaveStatus { success = 0, output = 1 };
    // F03 production preference contract: coherent configuration, truthful write
    // and close results, exactly-once close. Failed output is removed when possible;
    // accepted preferences remain. Providers do not admit configuration mutations.
    PreferenceSaveStatus save_classic_preferences(const state::ProcessContext& process,
                                                  const FileStorage& services);
    // Synchronous owner adapter: publish before open/write/close/remove.
    PreferenceSaveStatus save_classic_preferences(SessionRuntime& runtime,
                                                  const FileStorage& services);
    } // namespace openciv::session
}
