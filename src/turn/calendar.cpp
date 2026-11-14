#include "turn/calendar.hpp"
#include "base/arithmetic.hpp"
#include <algorithm>

namespace openciv::turn {
std::int32_t calendar_year(std::int32_t input_turn, const state::GlobalValues& globals,
                           const state::ScenarioParameters& scenario,
                           const state::CalendarValues& calendar) noexcept {
    using base::add32;
    using base::multiply32;
    using base::subtract32;
    auto level = static_cast<std::int32_t>(globals.difficulty);
    if (globals.game_flags & 0x0004U)
        --level;
    if (globals.game_flags & 0x0008U)
        ++level;
    const auto profile = static_cast<std::size_t>(std::clamp(level - 1, 0, 3));
    const auto extra = globals.first_launch_turn >= 0
                           ? std::max(subtract32(input_turn, globals.first_launch_turn), 0)
                           : 0;
    const auto working_turn = subtract32(input_turn, extra);
    if ((globals.game_flags & 0x0080U) && scenario.year_increment != 0 &&
        scenario.start_year != 0) {
        const auto increment = static_cast<std::int32_t>(scenario.year_increment);
        const auto step = increment < 0 ? -increment : increment;
        const auto year = subtract32(add32(scenario.start_year, multiply32(working_turn, step)), 1);
        return add32(year, extra);
    }
    auto remaining = working_turn == 0 ? 0 : subtract32(working_turn, 1);
    const auto& periods = calendar.profiles[profile];
    std::size_t index = 0;
    while (index < periods.size() && remaining > periods[index].turn_count) {
        remaining = subtract32(remaining, periods[index].turn_count);
        ++index;
    }
    std::int32_t year;
    if (index < periods.size()) {
        const auto& period = periods[index];
        year = add32(period.start_year, multiply32(period.year_step, remaining));
    } else {
        const auto& last = periods.back();
        year =
            add32(add32(last.start_year, multiply32(last.year_step, last.turn_count)), remaining);
    }
    if (year == 0)
        year = 1;
    return add32(year, extra);
}
} // namespace openciv::turn
