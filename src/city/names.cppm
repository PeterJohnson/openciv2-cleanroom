export module openciv.city.names;

import std;
export import openciv.state.civilization;
export import openciv.state.rule_state;
export import openciv.city.name_catalog;

export extern "C++" {
    namespace openciv::city {
    // C01 "Default city naming". The resolver receives the magnitude of the live
    // signed plural label, including 32768 for -32768. No pool layout is assumed.
    // Resolves the current plural and applies the cursor/retry rules to owned data.
    // Publishes at most 15 classic bytes and a cleared tail.
    bool default_name(state::City& city, std::span<const state::Civilization> civilizations,
                      std::span<state::Leader> leaders,
                      const std::function<std::string(state::TextId)>& resolve_plural,
                      const NameCatalog& catalog);
    // Process-owned rule/leader/pool binding of the same operation.
    bool default_name(state::City& city, std::span<const state::Civilization> civilizations,
                      state::RuleState& rules, const NameCatalog& catalog);
    } // namespace openciv::city
}
