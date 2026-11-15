export module openciv.session.classic_save;

import std;
export import openciv.session.classic_load;

export extern "C++" {
    namespace openciv::session {
    enum class CoreSaveStatus { success = 0, output = 1 };
    // F06 save/load production boundary: prepare a coherent image before opening
    // output, retaining accepted preparation effects. No RNG or per-write game events.
    // An opened output is closed once, including failure and exception paths.
    // Storage owns replacement/commit policy. A false write or close is failure;
    // providers must not reenter or mutate game state.
    CoreSaveStatus save_classic_core(SessionRuntime& runtime, ClassicFileState& retained,
                                     std::string_view requested_path, bool scenario_export,
                                     const FileStorage& storage);
    } // namespace openciv::session
}
