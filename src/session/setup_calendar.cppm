export module openciv.session.setup_calendar;
import openciv.turn.calendar;

import std;
export import openciv.session.runtime;
export import openciv.state.gameplay;

export namespace openciv::session {
/// F05 section 4's acceleration labels, before the new-game scenario reset.
/// Writes first-launch turn Absent, then computes magnitudes at turns 0, 21, 41
/// through the real F04 consumer of the selected calendar. Other state and all
/// random contexts are retained. The outer wizard owns the choice/retry policy.
std::array<std::uint32_t, 3> prepare_acceleration_years(SessionRuntime& runtime,
                                                        const state::CalendarValues& calendar);
} // namespace openciv::session

module :private;
namespace openciv::session {
std::array<std::uint32_t, 3> prepare_acceleration_years(SessionRuntime& runtime,
                                                        const state::CalendarValues& calendar) {
    auto& world = runtime.world();
    world.globals.first_launch_turn = -1;
    std::array<std::uint32_t, 3> years{};
    constexpr std::array<std::int32_t, 3> turns{0, 21, 41};
    for (std::size_t i = 0; i < years.size(); ++i) {
        const auto year = turn::calendar_year(turns[i], world.globals, world.scenario, calendar);
        const auto bits = static_cast<std::uint32_t>(year);
        years[i] = year < 0 ? 0U - bits : bits;
    }
    if (runtime.active())
        runtime.publish();
    return years;
}
} // namespace openciv::session
