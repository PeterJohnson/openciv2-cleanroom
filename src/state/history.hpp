#pragma once
#include <string>

#include "state/civilization.hpp"

namespace openciv::state {
// F02 world history / F06 section 2. Every sample and elimination row is
// retained independently of the current turn/count; loading does not rebuild it.
using PowerHistory = std::array<std::array<std::uint8_t, 8>, 150>;
struct EliminationHistory {
    std::int16_t count = 0;
    std::array<std::int16_t, 12> turns{};
    std::array<std::uint8_t, 12> killers{}, portraits{};
    std::array<std::string, 12> names;
    bool operator==(const EliminationHistory&) const = default;
};
} // namespace openciv::state
