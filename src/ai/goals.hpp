#pragma once

#include "state/game.hpp"

namespace openciv::ai {
struct GoalServices {
    // U04 owning queries, required only at the actual redirection boundary.
    // Read-only queries on stable state. They must not mutate records/rules,
    // dispatch events or reenter goals. Missing input may report an error.
    std::function<bool(state::UnitSlot)> awake_with_moves;
    std::function<std::int32_t(state::UnitSlot)> moves_left;
};
// A01 section 2 / Appendix A. No expiry for zero; -128 remains -128.
void negate_goal_priority(state::CivilizationGoal& goal) noexcept;
void reset_standing_goals(state::Civilization& civilization) noexcept;
// The role argument is already a retained signed byte. Coordinate comparisons
// use full incoming values. Result 128 is narrowed only for the next comparison.
std::int32_t goal_priority_at(const state::Civilization& civilization, world::Position position,
                              std::int8_t role) noexcept;

// A01 section 2 shared goal operations, including real W01 map/distance reads
// and immediate unit redirection. These are engine operations, not authority or
// client-session admission. Owners supply the U04 queries and publish at their
// external boundaries as needed. Missing services/backing stop where consumed,
// retaining preceding changes; no capacity, coordinate or priority normalization.
class SharedGoals {
public:
    SharedGoals(state::GameState& world, const state::RuleDefinitions& rules,
                GoalServices services);
    void add_current(std::int32_t civilization, world::Position position, std::int32_t role,
                     std::int32_t priority);
    void add_standing(std::int32_t civilization, world::Position position, std::int32_t role,
                      std::int32_t priority);
    void refresh(std::int32_t civilization);
    void clear_standing_near(std::int32_t civilization, std::int32_t role, world::Position position,
                             std::int32_t limit);

private:
    state::Civilization& civilization(std::int32_t slot);
    state::GameState& world_;
    const state::RuleDefinitions& rules_;
    GoalServices services_;
};
} // namespace openciv::ai
