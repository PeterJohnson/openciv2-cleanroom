#include "ai/goals.hpp"
#include "base/arithmetic.hpp"
#include <bit>
#include <utility>

namespace openciv::ai {
namespace {
constexpr std::int8_t free_role = -1;
std::int8_t byte(std::int32_t value) {
    return std::bit_cast<std::int8_t>(static_cast<std::uint8_t>(value));
}
std::int16_t short_value(std::int32_t value) {
    return std::bit_cast<std::int16_t>(static_cast<std::uint16_t>(value));
}
std::int32_t magnitude(std::int8_t value) {
    return value < 0 ? -static_cast<std::int32_t>(value) : value;
}
bool duplicate(std::span<const state::CivilizationGoal> goals, world::Position position,
               std::int8_t role, std::int8_t priority) {
    for (const auto& goal : goals)
        if (goal.x == position.x && goal.y == position.y && goal.role == role &&
            goal.priority >= priority)
            return true;
    return false;
}
void insert(std::span<state::CivilizationGoal> goals, world::Position position, std::int8_t role,
            std::int8_t priority, bool standing) {
    for (std::size_t i = 0; i < goals.size(); ++i) {
        if (goals[i].role != free_role && goals[i].priority >= priority)
            continue;
        auto end = goals.size() - 1;
        if (!standing) {
            end = i;
            while (end + 1 < goals.size() && goals[end].role != free_role)
                ++end;
        }
        for (; end > i; --end)
            goals[end] = goals[end - 1];
        goals[i] = {short_value(position.x), short_value(position.y), role, priority};
        return;
    }
}
} // namespace
void negate_goal_priority(state::CivilizationGoal& goal) noexcept {
    goal.priority = byte(-magnitude(goal.priority));
}
void reset_standing_goals(state::Civilization& civilization) noexcept {
    for (auto& goal : civilization.standing_goals) {
        goal.role = free_role;
        goal.priority = 0;
    }
}
std::int32_t goal_priority_at(const state::Civilization& civilization, world::Position position,
                              std::int8_t role) noexcept {
    std::int32_t result = 0;
    for (const auto& goal : civilization.goals)
        if (goal.x == position.x && goal.y == position.y && goal.role == role &&
            byte(result) <= magnitude(goal.priority))
            result = magnitude(goal.priority);
    return result;
}
SharedGoals::SharedGoals(state::GameState& world, const state::RuleDefinitions& rules,
                         const UnitMovementQueries& services)
    : world_(world), rules_(rules), queries_(services) {}
state::Civilization& SharedGoals::civilization(std::int32_t slot) {
    return world_.civilizations.at(static_cast<std::size_t>(slot));
}
void SharedGoals::add_current(std::int32_t civ, world::Position position, std::int32_t role,
                              std::int32_t priority) {
    auto& goals = civilization(civ).goals;
    const auto retained_role = byte(role), retained_priority = byte(priority);
    if (duplicate(goals, position, retained_role, retained_priority))
        return;
    if (civ == world_.globals.turn_civ && (world_.globals.human_players & (1U << civ)) == 0 &&
        (role == 2 || role == 3)) {
        auto& pool = world_.entities.units;
        const auto end = pool.end(); // Movement queries cannot mutate this scan.
        for (std::size_t i = 0; i < end; ++i) {
            auto& unit = pool.storage()[i];
            if (!state::occupied(unit.token) || static_cast<std::uint8_t>(unit.owner) != civ)
                continue;
            const auto slot = static_cast<state::UnitSlot>(i);
            if (!queries_.can_act(slot))
                continue;
            const auto& rule = rules_.units.at(static_cast<std::size_t>(unit.type));
            if (byte(static_cast<std::uint8_t>(rule.role)) != role)
                continue;
            const auto region = world_.map.tile({unit.x, unit.y}).region;
            if (region != world_.map.tile(position).region)
                continue;
            const auto distance = world_.map.geometry().weighted_distance(
                position, {unit.x, unit.y}, (world_.globals.rules & 0x8000U) != 0);
            const auto moves = queries_.remaining_moves(slot);
            if (base::multiply32(2, distance) <= moves) {
                unit.orders = state::UnitOrder::go_to;
                unit.destination_x = short_value(position.x);
                unit.destination_y = short_value(position.y);
            }
        }
    }
    insert(goals, position, retained_role, retained_priority, false);
}
void SharedGoals::add_standing(std::int32_t civ, world::Position position, std::int32_t role,
                               std::int32_t priority) {
    auto& goals = civilization(civ).standing_goals;
    const auto retained_role = byte(role), retained_priority = byte(priority);
    if (!duplicate(goals, position, retained_role, retained_priority))
        insert(goals, position, retained_role, retained_priority, true);
}
void SharedGoals::refresh(std::int32_t civ) {
    auto& state = civilization(civ);
    for (auto& goal : state.goals) {
        if (goal.priority < 0)
            goal.role = free_role;
        negate_goal_priority(goal);
    }
    for (const auto& goal : state.standing_goals)
        if (goal.role >= 0)
            add_current(civ, {goal.x, goal.y}, goal.role, goal.priority);
}
void SharedGoals::clear_standing_near(std::int32_t civ, std::int32_t role, world::Position position,
                                      std::int32_t limit) {
    for (auto& goal : civilization(civ).standing_goals) {
        if (goal.role != byte(role))
            continue;
        const auto distance = world_.map.geometry().weighted_distance(
            position, {goal.x, goal.y}, (world_.globals.rules & 0x8000U) != 0);
        if (distance <= limit) {
            goal.role = free_role;
            goal.priority = 0;
        }
    }
}
} // namespace openciv::ai
