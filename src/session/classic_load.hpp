#pragma once
#include "formats/record_compatibility.hpp"

#include "session/classic_stream.hpp"
#include "session/runtime.hpp"
#include "state/game_input.hpp"
#include "formats/classic_text_reader.hpp"
#include "formats/event_io.hpp"
#include "formats/leader_io.hpp"
#include "formats/save_aux_io.hpp"
#include <memory>

namespace openciv::session {
class SessionRuntime;
enum class CoreLoadStatus { success = 0, input = 1, unsupported = 2, data = 3, newer = 4 };
// Process/operation-retained F06 values, separate from client leases and from
// the authoritative world. The owner supplies home/text directories before use.
struct ClassicFileState {
    formats::classic::TextDirectories text;
    std::string extension = ".SAV", opened_path, scenario_directory;
    std::string default_game_stem = "GAME", game_stem = "GAME";
    std::string loaded_filename, remembered_subdirectory;
    formats::StartingPositions starts;
    formats::SavedCursor cursor;
    formats::SavedLayout packed_layout;
    state::Rectangle current_screen;
    formats::EventCompatibility event_bytes;
    formats::RecordCompatibility record_bytes;
};
struct CoreLoadServices {
    std::function<std::unique_ptr<CoreInput>(std::string_view)> open;
    std::function<void()> flush;
    std::function<void(std::string_view)> change_directory;
    std::function<bool(std::string_view directory, std::string_view name)> exists;
    formats::classic::TextCase comparison;
    // One factory returns owned, format-independent inputs; no open/reload/close protocol.
    std::function<state::LoadedContent()> load_content;
    std::function<void()> content_installed;
    void validate() const;
};
// One guarded load lifetime. The runtime, file context and captured services must
// outlive it. Parsing completes before any live rule/world installation.
class ClassicLoadOperation {
public:
    ClassicLoadOperation(SessionRuntime& runtime, ClassicFileState& retained,
                         CoreLoadServices services);
    ClassicLoadOperation(const ClassicLoadOperation&) = delete;
    ClassicLoadOperation& operator=(const ClassicLoadOperation&) = delete;
    CoreLoadStatus load(std::string_view path, std::string_view password_reset_call);

private:
    SessionRuntime& runtime_;
    ClassicFileState& retained_;
    CoreLoadServices services_;
    SessionActivity activity_;
    bool started_ = false;
};
CoreLoadStatus load_classic_core(SessionRuntime& runtime, ClassicFileState& retained,
                                 std::string_view requested_path, const CoreLoadServices& services,
                                 std::string_view password_reset_call);
} // namespace openciv::session
