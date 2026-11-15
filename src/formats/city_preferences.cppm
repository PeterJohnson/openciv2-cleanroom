export module openciv.formats.city_preferences;

import std;
export import openciv.formats.content_pack;
export import openciv.state.city_preferences;
export import openciv.state.rules;
export import openciv.base.text_comparison;

export extern "C++" {
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
}
