#pragma once
#include "session/runtime.hpp"
#include "state/game_input.hpp"

namespace openciv::session {
// Pure admission precedes installation. Callers own the activity guard and input I/O.
void validate_game_input(const state::GameInput& input);
void install_game(const state::GameInput& input, SessionRuntime& runtime,
                  const state::RuleInput& rules, const state::Rectangle& screen,
                  const world::AllocationCheck& allocate, const state::EventServices& events,
                  std::string_view password_reset_call);
void restore_leader_names(const state::GameInput& input, state::GameState& world,
                          state::RuleState& rules);
void prepare_scenario_names(bool scenario_export, const state::GlobalValues& globals,
                            std::span<const state::Civilization> civilizations,
                            std::span<state::CivilizationNames> names,
                            const state::RuleState& rules);
} // namespace openciv::session
