#pragma once
#include "classic_text_reader.hpp"
#include "rules.hpp"
#include "state/city_preferences.hpp"
#include "state/rule_state.hpp"

namespace openciv::formats::classic {
// F03 section 8, live name lookup and incremental publication. Missing files
// empty the active list but retain NoDefend. Failure preserves prior effects.
void load_city_preferences(TextReader& reader, const state::RuleDefinitions& live_rules,
                           state::CityPreferences& retained, const TextCase& comparison);
// Process-owned lookup reads current pool bindings, not cached decoded text.
void load_city_preferences(TextReader& reader, const state::RuleState& live_rules,
                           state::CityPreferences& retained, const TextCase& comparison);
} // namespace openciv::formats::classic

namespace openciv::formats::content {
struct CityPreferenceContent {
    state::CityPreferenceUpdate update;
    std::shared_ptr<const RuleContent> rules;
    std::shared_ptr<const ResolvedFile> source;
};
// data-format.md section 5 / Appendix B. Resolves IDs against the same pack's
// admitted rules and rejects first-name-match collisions. No live mutation.
std::shared_ptr<const CityPreferenceContent>
load_city_preferences(const ResolvedPack& pack, const classic::TextCase& comparison);
} // namespace openciv::formats::content
