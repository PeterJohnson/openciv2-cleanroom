#pragma once
#include "content_pack.hpp"
#include "state/city_preferences.hpp"
#include "state/rules.hpp"
#include "base/text_comparison.hpp"

namespace openciv::formats::content {
struct CityPreferenceContent {
    state::CityPreferenceUpdate update;
    std::shared_ptr<const ResolvedFile> source;
};
// data-format.md section 5 / Appendix B. Resolves IDs against the same pack's
// admitted rules and rejects first-name-match collisions. No live mutation.
std::shared_ptr<const CityPreferenceContent>
load_city_preferences(const ResolvedPack& pack, const state::RuleDefinitions& rules,
                      const base::NameComparison& comparison);
} // namespace openciv::formats::content
