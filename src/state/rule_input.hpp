#pragma once
#include "state/rules.hpp"
#include <vector>

namespace openciv::state {
struct LeaderTitleOverride {
    Government government = Government::anarchy;
    state::TitlePair titles;
};
struct RuleInput {
    // Decoded table values. Prerequisites are unresolved operands; movement,
    // default titles and enabled counts are established by RuleInstaller.
    // Empty identity IDs preserve existing bindings on classic reload.
    state::RuleDefinitions definitions;
    // Installation operands that a final snapshot cannot recover. Preserve
    // ordered duplicate overrides and live F03 fixup/scaling dependencies.
    std::array<std::int32_t, 62> movement_points{};
    std::array<std::vector<LeaderTitleOverride>, 21> title_overrides;
    std::array<std::optional<state::AdvanceReference>, 7> government_advances;
};
} // namespace openciv::state
