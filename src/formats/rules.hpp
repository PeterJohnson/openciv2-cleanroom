#pragma once
#include "content_pack.hpp"
#include "state/rules.hpp"
#include <vector>

namespace openciv::formats::content {
struct LeaderTitleOverride {
    std::uint8_t government = 0;
    state::TitlePair titles;
};
struct RuleContent {
    state::RuleDefinitions definitions;
    // Installation operands that a final snapshot cannot recover. Preserve
    // ordered duplicate overrides and live F03 fixup/scaling dependencies.
    std::array<std::array<state::AdvanceReference, 2>, 100> advance_prerequisites{};
    std::array<state::AdvanceReference, 67> improvement_prerequisites{};
    std::array<std::int32_t, 62> movement_points{};
    std::array<std::vector<LeaderTitleOverride>, 21> title_overrides;
    std::array<std::optional<state::AdvanceReference>, 7> government_advances;
    std::shared_ptr<const ResolvedFile> rules_source, compatibility_source;
};
// Complete native rule definitions and rule-related compatibility mappings.
// Requires resolved bytes; does not install/reload tables or reset retained state.
// Text lookup/scenario metadata in compatibility still needs its owning validator.
std::shared_ptr<const RuleContent> load_rules(const ResolvedPack& pack);
} // namespace openciv::formats::content
