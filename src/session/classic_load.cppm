export module openciv.session.classic_load;

import std;
export import openciv.base.callback_ref;
export import openciv.formats.record_compatibility;
export import openciv.io.storage;
export import openciv.session.runtime;
export import openciv.state.game_input;
export import openciv.formats.classic_text_reader;
export import openciv.formats.event_io;
export import openciv.formats.leader_io;
export import openciv.formats.save_aux_io;

export namespace openciv::session {
enum class CoreLoadStatus { success = 0, input = 1, unsupported = 2, data = 3, newer = 4 };
// Process/operation-retained F06 values, separate from client leases and from
// the authoritative world. The owner supplies home/text directories before use.
struct ClassicFileState {
    formats::classic::TextDirectories text;
    std::string extension = ".SAV", opened_path, scenario_directory;
    std::string default_game_stem = "GAME", game_stem = "GAME";
    std::string loaded_filename, remembered_subdirectory;
    state::StartingPositions starts;
    formats::SavedCursor cursor;
    formats::SavedLayout packed_layout;
    state::Rectangle current_screen;
    formats::EventCompatibility event_bytes;
    formats::RecordCompatibility record_bytes;
};
// Borrowed providers and callable owners must outlive the consuming operation.
struct CoreLoadDependencies {
    const io::FileStorage* storage = nullptr;
    base::CallbackRef<void()> flush;
    base::CallbackRef<void(std::string_view)> change_directory;
    base::CallbackRef<bool(std::string_view directory, std::string_view name)> exists;
    const formats::classic::TextCase* comparison = nullptr;
    // One factory returns owned, format-independent inputs; no open/reload/close protocol.
    base::CallbackRef<state::LoadedContent()> load_content;
    base::CallbackRef<void()> content_installed;
    void validate() const;
};
// One guarded load lifetime. The runtime, file context and captured services must
// outlive it. Parsing completes before any live rule/world installation.
class ClassicLoadOperation {
public:
    ClassicLoadOperation(SessionRuntime& runtime, ClassicFileState& retained,
                         CoreLoadDependencies services);
    ClassicLoadOperation(const ClassicLoadOperation&) = delete;
    ClassicLoadOperation& operator=(const ClassicLoadOperation&) = delete;
    CoreLoadStatus load(std::string_view path, std::string_view password_reset_call);

private:
    SessionRuntime& runtime_;
    ClassicFileState& retained_;
    CoreLoadDependencies services_;
    SessionActivity activity_;
    bool started_ = false;
};
CoreLoadStatus load_classic_core(SessionRuntime& runtime, ClassicFileState& retained,
                                 std::string_view requested_path,
                                 const CoreLoadDependencies& services,
                                 std::string_view password_reset_call);
} // namespace openciv::session
