export module openciv.session.setup_calendar;

import std;
export import openciv.session.runtime;
export import openciv.state.gameplay;

export extern "C++" {
    namespace openciv::session {
    // F05 section 4's acceleration labels, before the new-game scenario reset.
    // Writes first-launch turn Absent, then computes magnitudes at turns 0, 21, 41
    // through the real F04 consumer of the selected calendar. Other state and all
    // random contexts are retained. The outer wizard owns the choice/retry policy.
    std::array<std::uint32_t, 3> prepare_acceleration_years(SessionRuntime& runtime,
                                                            const state::CalendarValues& calendar);
    } // namespace openciv::session
}
