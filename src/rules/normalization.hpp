#pragma once
#include "state/rules.hpp"
#include <bit>

namespace openciv::rules {
// F03 sections 1,4-7: explicit consumer codes, independent of source syntax.
constexpr std::uint8_t code8(std::int32_t value) {
    return static_cast<std::uint8_t>(value);
}
constexpr std::int8_t signed8(std::int32_t value) {
    return std::bit_cast<std::int8_t>(code8(value));
}
constexpr std::int16_t signed16(std::int32_t value) {
    return std::bit_cast<std::int16_t>(static_cast<std::uint16_t>(value));
}
state::CosmicRules normalize_cosmic(const std::array<std::int32_t, 22>& input);
std::uint8_t normalize_movement(std::int32_t whole_points, std::uint8_t road_multiplier);
std::uint8_t normalize_classic_hp(std::int32_t groups_of_ten);
std::uint8_t normalize_unit_cost(std::int32_t input);
std::uint8_t normalize_leader_color(std::int32_t input);
std::uint8_t normalize_leader_style(std::int32_t input);
state::AdvanceReference follow_disabled(state::AdvanceReference reference,
                                        const std::array<state::AdvanceRule, 100>& advances);
// All rows must have their original enabled flag set before this ordered pass.
// It never recomputes enablement after replacing a prerequisite with Never.
std::uint16_t fix_advance_references(std::array<state::AdvanceRule, 100>& advances);
} // namespace openciv::rules
