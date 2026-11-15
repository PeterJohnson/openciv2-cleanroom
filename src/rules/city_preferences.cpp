import std;
import openciv.rules.city_preferences;

namespace openciv::rules {
void apply_city_preferences(const state::CityPreferenceUpdate& update,
                            state::CityPreferences& retained) {
    if (update.build_order.size() > retained.build_order.size() ||
        std::any_of(update.build_order.begin(), update.build_order.end(),
                    [](auto slot) { return slot < 1 || slot > 38; }))
        throw std::invalid_argument("City preferences require at most 32 improvement slots 1..38");
    retained.count = 0;
    if (update.deprioritize_defense)
        retained.no_defend = 1;
    for (const auto slot : update.build_order)
        retained.build_order[static_cast<std::size_t>(retained.count++)] = slot;
}
} // namespace openciv::rules
