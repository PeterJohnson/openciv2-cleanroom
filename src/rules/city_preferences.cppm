export module openciv.rules.city_preferences;

import std;
export import openciv.state.city_preferences;

export extern "C++" {
    namespace openciv::rules {
    // Native application, data-format.md section 5 / Appendix B. Validate the
    // complete logical update before publication; preserve the unused payload.
    void apply_city_preferences(const state::CityPreferenceUpdate& update,
                                state::CityPreferences& retained);
    } // namespace openciv::rules
}
