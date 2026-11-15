#include "names.hpp"
#include <algorithm>

namespace openciv::city {
bool default_name(state::City& city, std::span<const state::Civilization> civilizations,
                  std::span<state::Leader> leaders,
                  const std::function<std::string(state::TextId)>& resolve_plural,
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
            city.name.bytes.fill(0);
            const auto end = result.bytes.find('\0');
            const auto count = std::min<std::size_t>(15, std::min(result.bytes.size(), end));
            for (std::size_t i = 0; i < count; ++i)
                city.name.bytes[i] = static_cast<std::uint8_t>(result.bytes[i]);
            published = true;
            break;
        }
        live_leader().city_name_index = 0;
    }
    return published;
}
bool default_name(state::City& city, std::span<const state::Civilization> civilizations,
                  state::RuleState& rules, const NameCatalog& catalog) {
    return default_name(
        city, civilizations, rules.leaders,
        [&](state::TextId id) {
            return std::string(rules.text.text(static_cast<state::TextId>(id)));
        },
        catalog);
}
} // namespace openciv::city
