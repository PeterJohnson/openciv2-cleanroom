#pragma once

#include "state/rule_state.hpp"
#include <functional>

namespace openciv::rules {
// F03 section 8. No services occur between reading mode/requested and finishing
// the 62 table entries, so the caller supplies those scalar operands by value.
// A changed multiplayer request publishes the retained flag before the table,
// then invokes required redrawStatus(1). Single-player and repeated requests
// leave both flag and table untouched. Existing unit movement is never changed.
void apply_double_movement(std::uint8_t mode, std::int16_t requested, state::RuleState& rules,
                           const std::function<void(std::int32_t)>& redraw_status);
} // namespace openciv::rules
