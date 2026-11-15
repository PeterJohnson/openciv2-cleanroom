export module openciv.session.game_install;

import std;
export import openciv.session.runtime;
export import openciv.state.game_input;

export namespace openciv::session {
// Pure admission precedes installation. Callers own the activity guard and input I/O.
void validate_game_input(const state::GameInput& input);
void install_game(const state::GameInput& input, SessionRuntime& runtime,
                  const state::RuleInput& rules, const state::Rectangle& screen,
                  std::string_view password_reset_call);
void restore_leader_names(const state::GameInput& input, state::GameState& world,
                          state::RuleState& rules);
void prepare_scenario_names(bool scenario_export, const state::GlobalValues& globals,
                            std::span<const state::Civilization> civilizations,
                            std::span<state::CivilizationNames> names,
                            const state::RuleState& rules);
} // namespace openciv::session
