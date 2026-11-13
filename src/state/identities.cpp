#include "state/identities.hpp"
#include <stdexcept>

namespace openciv::state {
UnitToken ProcessTokens::assign_unit() noexcept {
    const auto token = UnitToken{next_unit};
    ++next_unit; // F02 §3: modulo 2^32, including zero; no collision search.
    return token;
}
CityToken ProcessTokens::assign_city() noexcept {
    const auto token = CityToken{next_city};
    ++next_city;
    return token;
}

std::optional<UnitSlot> reserve_unit_slot(std::span<const UnitToken> scanned_tokens,
                                          std::uint32_t& unit_end, CivilizationSlot owner,
                                          std::int16_t military_count,
                                          std::uint8_t effective_humans) {
    const auto owner_index = static_cast<std::uint8_t>(owner);
    if (owner_index >= 8 || unit_end > unit_capacity || scanned_tokens.size() != unit_end)
        throw std::invalid_argument(
            "Unit reservation requires owner 0..7 and a complete scan view");
    std::uint32_t slot = 0;
    while (slot < unit_end && occupied(scanned_tokens[slot]))
        ++slot;
    if (slot == unit_end) {
        const bool human = (effective_humans & (1U << owner_index)) != 0;
        if (unit_end >= unit_capacity || military_count > 1948 || (!human && unit_end >= 2040))
            return std::nullopt;
        ++unit_end;
    }
    return UnitSlot{static_cast<std::uint16_t>(slot)};
}
} // namespace openciv::state
