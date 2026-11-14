#pragma once
#include "session/classic_file_choice.hpp"
#include "session/classic_save.hpp"

namespace openciv::session {
struct ClassicSaveMenuState {
    bool session_active = false;
    std::string filename;
};
struct LocalCalendar {
    std::int32_t month = 0, day = 0, year = 0, hour = 0, minute = 0, second = 0;
};
struct SaveNotice {
    std::string key;
    std::array<std::string, 5> arguments;
};
struct SaveMenuServices {
    FileChoiceServices chooser;
    CoreSaveServices output;
    // N03 start is distinct from resume. These are real timer boundaries.
    std::function<void(bool notify)> stop_countdown, start_countdown;
    // Pure presentation queries. They do not mutate game state or admit reentry.
    std::function<LocalCalendar()> local_time;
    std::function<std::string(std::string_view)> lowercase;
    FileLabel label;
    std::function<std::string(std::int32_t)> ruler_name, ruler_title, civilization_plural;
    std::function<std::string(std::int32_t)> format_year;
    std::function<std::string()> error_text;
    std::function<void(const SaveNotice&)> notice;
};
enum class SaveMenuResult { inactive, cancelled, saved, failed };
// F06 production save UI. Captured presentation values and owned notice arguments;
// only an accepted path starts saving. Every completed timer stop has one restart
// attempt, including cancellation, chooser errors and exceptional providers.
SaveMenuResult save_classic_menu(SessionRuntime& runtime, ClassicFileState& files,
                                 ClassicSaveMenuState& menu, bool scenario,
                                 const SaveMenuServices& services);
} // namespace openciv::session
