#include "civilization/knowledge.hpp"
#include <stdexcept>

namespace openciv::civilization {
bool knows_advance(std::span<const state::Civilization> civilizations, std::int32_t civilization,
                   std::int32_t advance) {
    if (advance == -2)
        return false;
    if (advance < 0)
        return true;
    if (advance == future_technology || advance >= 100 || civilization < 1)
        return false;
    if (static_cast<std::size_t>(civilization) >= civilizations.size())
        throw std::out_of_range("Advance query has no civilization backing");
    const auto& known = civilizations[static_cast<std::size_t>(civilization)].advances;
    const auto index = static_cast<std::size_t>(advance);
    return (known[index / 8] & (1U << (index % 8))) != 0;
}
} // namespace openciv::civilization
