#pragma once

#include "formats/classic_text_reader.hpp"
#include "formats/event_io.hpp"
#include "formats/leader_io.hpp"
#include "formats/save_aux_io.hpp"
#include <memory>

namespace openciv::session {
class SessionRuntime;
enum class CoreLoadStatus { success = 0, input = 1, unsupported = 2, data = 3, newer = 4 };
enum class LoadedRuleTable { advances, improvements, units };
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
    formats::LeaderLoadScratch leaders;
};
struct CoreInput {
    formats::ReadItem item;
    formats::ReadEventByte byte;
    std::function<bool()> close; // Failure is ignored, but the call occurs once.
};
struct CoreLoadServices {
    std::function<std::unique_ptr<CoreInput>(std::string_view)> open;
    std::function<void()> flush;
    std::function<void(std::string_view)> change_directory;
    std::function<bool(std::string_view directory, std::string_view name)> exists;
    formats::classic::TextCase comparison;
    // Owning F03 operations, not optional notifications. A normal return means
    // that operation completed. Exceptions retain prior effects and close input.
    std::function<void(bool initialization)> load_rules;
    std::function<void(LoadedRuleTable, bool initialization)> reload_table;
    std::function<void()> close_rules;
    world::AllocationCheck allocate_map;
    state::EventServices events;
};
// F06 section 6 core load, including version/path setup and post-close mode
// adaptation. Does not execute the post-load wrapper, events, visibility rebuild,
// connection setup or participant admission. All external services above are
// required except resource hooks. The runtime must already have an initialized
// random context and no pending commands; classic files carry no RNG checkpoint.
// Unterminated headers/missing backslashes and unbacked accesses stop safely,
// rather than inventing an ordinary error status. Valid short items retain effects.
CoreLoadStatus load_classic_core(SessionRuntime& runtime, ClassicFileState& retained,
                                 std::string_view requested_path, const CoreLoadServices& services,
                                 std::string_view password_reset_call);
} // namespace openciv::session
