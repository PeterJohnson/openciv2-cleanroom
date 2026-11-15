export module openciv.rules.normalization;

import std;
export import openciv.state.rules;

export namespace openciv::rules {
/// F03 sections 1,4-7: explicit consumer codes, independent of source syntax.
constexpr std::uint8_t code8(std::int32_t value) {
    return static_cast<std::uint8_t>(value);
}
/// Narrow to the low eight bits and interpret them as a signed value.
constexpr std::int8_t signed8(std::int32_t value) {
    return std::bit_cast<std::int8_t>(code8(value));
}
/// Narrow to the low sixteen bits and interpret them as a signed value.
constexpr std::int16_t signed16(std::int32_t value) {
    return std::bit_cast<std::int16_t>(static_cast<std::uint16_t>(value));
}
/// Apply the F03 domain rule for one cosmic parameter before storage.
std::uint8_t normalize_cosmic_value(state::CosmicParameter parameter, std::int32_t input);
/// Normalize the complete ordered cosmic input table.
state::CosmicRules normalize_cosmic(const std::array<std::int32_t, 22>& input);
/// Convert whole movement points to retained road-scaled movement units.
std::uint8_t normalize_movement(std::int32_t whole_points, std::uint8_t road_multiplier);
/// Convert classic groups of ten hit points to the retained unit value.
std::uint8_t normalize_classic_hp(std::int32_t groups_of_ten);
/// Apply the classic unit-cost normalization rule.
std::uint8_t normalize_unit_cost(std::int32_t input);
/// Apply the classic leader-color normalization rule.
std::uint8_t normalize_leader_color(std::int32_t input);
/// Apply the classic leader-style normalization rule.
std::uint8_t normalize_leader_style(std::int32_t input);
/// Follow disabled-advance references according to the F03 prerequisite rule.
state::AdvanceReference follow_disabled(state::AdvanceReference reference,
                                        const std::array<state::AdvanceRule, 100>& advances);
/// All rows must have their original enabled flag set before this ordered pass.
/// It never recomputes enablement after replacing a prerequisite with Never.
std::uint16_t fix_advance_references(std::array<state::AdvanceRule, 100>& advances);
} // namespace openciv::rules

module :private;
namespace openciv::rules {
std::uint8_t normalize_cosmic_value(state::CosmicParameter parameter, std::int32_t input) {
    constexpr std::array<std::int32_t, 22> low{1, 1, 0, 4, 4, 0, 0, 4, 10, 4,  4,
                                               3, 5, 0, 0, 0, 1, 0, 0, 4,  25, 0};
    constexpr std::array<std::int32_t, 22> high{10, 100, 10, 20, 20, 10, 10,  12,  100, 50,  50,
                                                10, 100, 8,  8,  8,  20, 100, 100, 100, 200, 10};
    const auto i = static_cast<std::size_t>(parameter);
    auto out = code8(std::clamp(input, low.at(i), high.at(i)));
    if (parameter == state::CosmicParameter::food_box_rows && out % 2)
        ++out;
    return out;
}
state::CosmicRules normalize_cosmic(const std::array<std::int32_t, 22>& input) {
    state::CosmicRules out;
    for (std::size_t i = 0; i < input.size(); ++i)
        out.values[i] = normalize_cosmic_value(static_cast<state::CosmicParameter>(i), input[i]);
    return out;
}
std::uint8_t normalize_movement(std::int32_t points, std::uint8_t road) {
    return code8(code8(points) * road);
}
std::uint8_t normalize_classic_hp(std::int32_t groups) {
    return code8(code8(groups) * 10);
}
std::uint8_t normalize_unit_cost(std::int32_t input) {
    return code8(std::clamp(input, 1, 200));
}
std::uint8_t normalize_leader_color(std::int32_t input) {
    return code8(std::clamp<std::int32_t>(signed16(input), 1, 7));
}
std::uint8_t normalize_leader_style(std::int32_t input) {
    return code8(std::clamp<std::int32_t>(signed16(input), 0, 3));
}
state::AdvanceReference follow_disabled(state::AdvanceReference reference,
                                        const std::array<state::AdvanceRule, 100>& advances) {
    std::bitset<100> visited;
    auto index = static_cast<std::int16_t>(reference);
    while (index >= 0) {
        if (index >= 100)
            throw std::domain_error("advance reference outside table");
        const auto i = static_cast<std::size_t>(index);
        if (advances[i].enabled)
            break;
        if (visited.test(i))
            throw std::domain_error("disabled advance chain does not terminate");
        visited.set(i);
        reference = advances[i].prerequisites[0];
        index = static_cast<std::int16_t>(reference);
    }
    return reference;
}
std::uint16_t fix_advance_references(std::array<state::AdvanceRule, 100>& advances) {
    std::uint16_t count = 0;
    for (auto& advance : advances)
        if (advance.enabled) {
            ++count;
            for (auto& prerequisite : advance.prerequisites)
                prerequisite = follow_disabled(prerequisite, advances);
        }
    return count;
}
} // namespace openciv::rules
