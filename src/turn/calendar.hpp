#pragma once

#include "state/gameplay.hpp"
#include "state/scenario.hpp"

namespace openciv::turn {
// F04 section 5, with gameplay-format section 2's selected, validated operands.
// Pure signed-32 calculation; does not narrow to a stored year, mutate the turn,
// consult map geometry, draw randomness or select a fallback content bundle.
std::int32_t calendar_year(std::int32_t input_turn, const state::GlobalValues& globals,
                           const state::ScenarioParameters& scenario,
                           const state::CalendarValues& calendar) noexcept;
} // namespace openciv::turn
