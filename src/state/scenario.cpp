import std;
import openciv.state.scenario;

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
