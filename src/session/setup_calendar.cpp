import std;
import openciv.session.setup_calendar;
import openciv.turn.calendar;

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
