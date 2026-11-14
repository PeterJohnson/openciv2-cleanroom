#pragma once

#include "state/civilization.hpp"

namespace openciv::state {
// F02 section 8 / S01 section 2. Scenario-active, export and events-loaded
// indicators live separately. Construction does not perform the reset operation.
struct ScenarioParameters {
    std::uint16_t flags = 0;
    FixedText<80> name;
    std::int16_t technology_paradigm = 0, year_increment = 0, start_year = 0;
    std::int16_t maximum_turns = 0, objective_civ = 0;
    std::array<std::int16_t, 4> victory_thresholds{};
    bool operator==(const ScenarioParameters&) const = default;
};
// S01 section 2 / F06 section 3: retain the name tail; reset no world/event flags.
void reset_scenario_parameters(ScenarioParameters& parameters);
} // namespace openciv::state
