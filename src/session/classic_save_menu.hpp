#pragma once
#include "session/classic_file_choice.hpp"
#include "session/classic_save.hpp"

namespace openciv::session {
struct ClassicSaveMenuState {
    // Classic session-active latch, distinct from the runtime's client lifetime.
    bool session_active = false;
    std::optional<std::uint64_t> main_window;
    std::string filename, private_filter, shared_filter;
};
struct LocalCalendar {
    std::int32_t month = 0, day = 0, year = 0, hour = 0, minute = 0, second = 0;
};
struct SaveMenuServices {
    FileChoiceServices chooser;
    CoreSaveServices output;
    // Required owning operations, not optional success notifications. N03 start
    // is distinct from resume. P04/name operations read their supplied live civ
    // or year at invocation; callers bind their shared text effects explicitly.
    std::function<void(bool notify)> stop_countdown, start_countdown;
    std::function<LocalCalendar()> local_time;
    std::function<std::string(std::string_view)> lowercase;
    std::function<void(std::string_view)> change_directory;
    FileLabel label;
    std::function<std::string(std::int32_t)> ruler_name, ruler_title, civilization_plural;
    std::function<std::string(std::int32_t)> format_year;
    std::function<std::string()> error_text;
    // Notice consumes process.text_parameters, as do other P04 callers.
    std::function<void(std::string_view)> notice;
};
enum class SaveMenuResult { inactive, cancelled, saved, failed };
// F06 section 11 Save-menu wrapper, including real section 5 saving. No native
// widget, N03 timer implementation or P04 formatter is fabricated. Services must
// be supplied; an exception stops at that boundary retaining prior effects.
// Normal success/error/cancel starts countdown; mode-2 cancel rebuilds/retries.
SaveMenuResult save_classic_menu(SessionRuntime& runtime, ClassicFileState& files,
                                 ClassicSaveMenuState& menu, bool scenario,
                                 const SaveMenuServices& services,
                                 std::optional<std::uint8_t> incoming_cleanup_mask = {});
} // namespace openciv::session
