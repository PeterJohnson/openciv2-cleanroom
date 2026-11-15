export module openciv.state.scenario;

import std;
export import openciv.state.civilization;

export namespace openciv::state {
// F02 section 8 / S01 section 2. Scenario-active, export and events-loaded
// indicators live separately. Construction does not perform the reset operation.
struct ScenarioParameters {
    std::uint16_t flags = 0;
    std::string name;
    std::int16_t technology_paradigm = 0, year_increment = 0, start_year = 0;
    std::int16_t maximum_turns = 0, objective_civ = 0;
    std::array<std::int16_t, 4> victory_thresholds{};
    bool operator==(const ScenarioParameters&) const = default;
};
// S01 section 2 / F06 section 3: retain the name tail; reset no world/event flags.
void reset_scenario_parameters(ScenarioParameters& parameters);
} // namespace openciv::state

module :private;
namespace openciv::state {
void reset_scenario_parameters(ScenarioParameters& parameters) {
    parameters.flags = 0;
    parameters.name.clear();
    parameters.technology_paradigm = 10;
    parameters.year_increment = 0;
    parameters.start_year = 0;
    parameters.maximum_turns = 0;
    parameters.objective_civ = 0;
    parameters.victory_thresholds.fill(0);
}
} // namespace openciv::state
