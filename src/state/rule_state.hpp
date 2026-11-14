#pragma once
#include "civilization.hpp"
#include "labels.hpp"
#include "rules.hpp"
#include "text_pool.hpp"

namespace openciv::state {
struct RuleNameBindings {
    std::array<TextId, 100> advances{};
    std::array<TextId, 67> improvements{};
    std::array<TextId, 62> units{};
    std::array<std::optional<TextId>, 62> interface_units{};
    std::array<TextId, 33> terrain{};
    std::array<TextId, 7> governments{};
    std::array<std::array<TextId, 2>, 7> government_titles{};
    std::array<TextId, 16> commodities{};
    std::array<TextId, 13> orders{}; // External slot zero is retained.
    std::array<TextId, 6> difficulties{};
    std::array<TextId, 9> attitudes{};
};
// Process-retained F02/F03 state. Definitions contain decoded content values;
// live name consumers resolve these bindings (leader labels additionally carry
// signed-16 views/sign markers). Do not treat decoded tribe text as a substitute
// for current leader labels after reload/load/narrowing. Construction is not an
// original startup/reset operation; initialize the shared pool explicitly.
struct RuleState {
    RuleDefinitions definitions;
    TextPool text;
    Labels labels;
    RuleNameBindings names;
    std::array<Leader, 21> leaders{};
    bool double_movement_applied = false;
};
} // namespace openciv::state
