#pragma once
#include "content_pack.hpp"
#include "state/rules.hpp"

namespace openciv::formats::content {
struct RuleContent {
    state::RuleDefinitions definitions;
    std::array<std::optional<state::AdvanceReference>, 7> government_advances;
    std::shared_ptr<const ResolvedFile> rules_source, compatibility_source;
};
// Complete native rule definitions and rule-related compatibility mappings.
// Requires resolved bytes; does not install/reload tables or reset retained state.
// Text lookup/scenario metadata in compatibility still needs its owning validator.
std::shared_ptr<const RuleContent> load_rules(const ResolvedPack& pack);
} // namespace openciv::formats::content
