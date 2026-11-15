export module openciv.state.city_preferences;

import std;

export namespace openciv::state {
/// F03 section 8: active prefix and retained payload are distinct. NoDefend is
/// retained verbatim by an unchanged update; a present marker writes exactly 1.
struct CityPreferences {
    std::array<std::int32_t, 32> build_order{};
    std::int32_t count = 0, no_defend = 0;
    bool operator==(const CityPreferences&) const = default;
};
/// An owned city-preference update prepared before installation.
struct CityPreferenceUpdate {
    bool deprioritize_defense = false;
    std::vector<std::uint8_t> build_order;
};
} // namespace openciv::state
