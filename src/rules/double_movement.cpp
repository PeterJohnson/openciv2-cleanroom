#include "rules/double_movement.hpp"
#include <bit>

namespace openciv::rules {
void apply_double_movement(std::uint8_t mode, std::int16_t requested, state::RuleState& rules,
                           const std::function<void(std::int32_t)>& redraw_status) {
    const bool enabled = requested != 0;
    if (mode == 0 || enabled == rules.double_movement_applied)
        return;
    rules.double_movement_applied = enabled;
    for (auto& unit : rules.definitions.units) {
        if (unit.domain != state::RuleDomain::land)
            continue;
        if (enabled)
            unit.movement = static_cast<std::uint8_t>(unit.movement * 2);
        else
            unit.movement =
                static_cast<std::uint8_t>(std::bit_cast<std::int8_t>(unit.movement) / 2);
    }
    redraw_status(1);
}
} // namespace openciv::rules
