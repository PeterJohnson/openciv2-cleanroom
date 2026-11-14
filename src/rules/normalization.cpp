#include "normalization.hpp"
#include <algorithm>
#include <bitset>
#include <stdexcept>

namespace openciv::rules {
state::CosmicRules normalize_cosmic(const std::array<std::int32_t, 22>& input) {
    constexpr std::array<std::int32_t, 22> low{1, 1, 0, 4, 4, 0, 0, 4, 10, 4,  4,
                                               3, 5, 0, 0, 0, 1, 0, 0, 4,  25, 0};
    constexpr std::array<std::int32_t, 22> high{10, 100, 10, 20, 20, 10, 10,  12,  100, 50,  50,
                                                10, 100, 8,  8,  8,  20, 100, 100, 100, 200, 10};
    state::CosmicRules out;
    for (std::size_t i = 0; i < input.size(); ++i)
        out.values[i] = code8(std::clamp(input[i], low[i], high[i]));
    auto& food = out.values[static_cast<std::size_t>(state::CosmicParameter::food_box_rows)];
    if (food % 2)
        ++food;
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
