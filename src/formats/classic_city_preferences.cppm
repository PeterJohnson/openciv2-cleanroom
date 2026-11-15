export module openciv.formats.classic_city_preferences;

import std;
export import openciv.formats.classic_text_reader;
export import openciv.state.city_preferences;
export import openciv.state.rule_state;

export extern "C++" {
    namespace openciv::formats::classic {
    // Decode an update against a snapshot of the supplied names. Installation is
    // shared with native content in rules::apply_city_preferences.
    state::CityPreferenceUpdate load_city_preferences(TextReader& reader,
                                                      const state::RuleDefinitions& rules,
                                                      const TextCase& comparison);
    state::CityPreferenceUpdate load_city_preferences(TextReader& reader,
                                                      const state::RuleState& rules,
                                                      const TextCase& comparison);
    } // namespace openciv::formats::classic
}
