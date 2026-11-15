export module openciv.rules.double_movement;

import std;
export import openciv.base.callback_ref;
export import openciv.state.rule_state;

export namespace openciv::rules {
// F03 section 8. No services occur between reading mode/requested and finishing
// the 62 table entries, so the caller supplies those scalar operands by value.
// A changed multiplayer request publishes the retained flag before the table,
// then invokes required redrawStatus(1). Single-player and repeated requests
// leave both flag and table untouched. Existing unit movement is never changed.
void apply_double_movement(state::GameMode mode, bool enabled, state::RuleState& rules,
                           const base::CallbackRef<void(std::int32_t)>& redraw_status);
} // namespace openciv::rules

module :private;
namespace openciv::rules {
void apply_double_movement(state::GameMode mode, bool enabled, state::RuleState& rules,
                           const base::CallbackRef<void(std::int32_t)>& redraw_status) {
    if (mode == state::GameMode::single_player || enabled == rules.double_movement_applied)
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
