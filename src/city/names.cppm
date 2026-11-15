export module openciv.city.names;

import std;
export import openciv.base.callback_ref;
export import openciv.state.civilization;
export import openciv.state.rule_state;
export import openciv.city.name_catalog;

export namespace openciv::city {
/// C01 "Default city naming". The resolver receives the magnitude of the live
/// signed plural label, including 32768 for -32768. No pool layout is assumed.
/// Resolves the current plural and applies the cursor/retry rules to owned data.
/// Publishes at most 15 classic bytes and a cleared tail.
bool default_name(state::City& city, std::span<const state::Civilization> civilizations,
                  std::span<state::Leader> leaders,
                  const base::CallbackRef<std::string(state::TextId)>& resolve_plural,
                  const NameCatalog& catalog);
/// Process-owned rule/leader/pool binding of the same operation.
bool default_name(state::City& city, std::span<const state::Civilization> civilizations,
                  state::RuleState& rules, const NameCatalog& catalog);
} // namespace openciv::city

module :private;
namespace openciv::city {
bool default_name(state::City& city, std::span<const state::Civilization> civilizations,
                  std::span<state::Leader> leaders,
                  const base::CallbackRef<std::string(state::TextId)>& resolve_plural,
                  const NameCatalog& catalog) {
    if (!resolve_plural)
        throw std::invalid_argument("City naming requires a plural resolver");
    const auto owner = static_cast<std::size_t>(city.owner);
    auto live_leader = [&]() -> state::Leader& {
        if (owner >= civilizations.size())
            throw std::out_of_range("City naming owner has no civilization backing");
        const auto tribe = civilizations[owner].tribe;
        if (tribe < 0 || static_cast<std::size_t>(tribe) >= leaders.size())
            throw std::out_of_range("City naming tribe has no leader backing");
        return leaders[static_cast<std::size_t>(tribe)];
    };
    const std::string plural = resolve_plural(live_leader().plural.id);
    bool published = false;
    for (unsigned attempt = 0; attempt < 2; ++attempt) {
        auto& counter = live_leader().city_name_index;
        counter = static_cast<std::uint8_t>(counter + 1);
        const auto remaining = counter;
        const auto result = catalog.select(plural, remaining);
        if (result.outcome == NameOutcome::unavailable)
            break;
        if (result.outcome == NameOutcome::selected) {
            const auto end = result.bytes.find('\0');
            const auto count = std::min<std::size_t>(15, std::min(result.bytes.size(), end));
            city.name = result.bytes.substr(0, count);
            published = true;
            break;
        }
        live_leader().city_name_index = 0;
    }
    return published;
}
bool default_name(state::City& city, std::span<const state::Civilization> civilizations,
                  state::RuleState& rules, const NameCatalog& catalog) {
    const auto resolve = [&](state::TextId id) { return std::string(rules.text.text(id)); };
    return default_name(city, civilizations, rules.leaders, resolve, catalog);
}
} // namespace openciv::city
