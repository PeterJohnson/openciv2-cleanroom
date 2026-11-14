#pragma once
#include "rules.hpp"
#include <vector>

namespace openciv::state {
// data-format.md section 5. Selectors are live plural names, never tribe IDs.
struct CityNameList {
    RuleText plural;
    std::vector<RuleText> names;
};
struct CityNameLists {
    std::vector<CityNameList> civilizations;
    // Missing and present-but-exhausted are different provider states.
    std::optional<std::vector<RuleText>> fallback;
};
} // namespace openciv::state
