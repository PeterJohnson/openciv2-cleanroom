#include "name_catalog.hpp"
#include <algorithm>

namespace openciv::city {
NameCatalog::NameCatalog(std::vector<state::CityNameLists> layers, base::NameComparison comparison)
    : layers_(std::move(layers)), comparison_(std::move(comparison)) {
    if (!comparison_)
        throw std::invalid_argument("City names require a comparison policy");
}
NameAttempt NameCatalog::select(std::string_view plural, std::uint8_t remaining) const {
    for (const auto& lists : layers_) {
        const auto found = std::find_if(
            lists.civilizations.begin(), lists.civilizations.end(),
            [&](const auto& list) { return comparison_(plural, list.plural.classic); });
        if (found == lists.civilizations.end())
            continue;
        if (!remaining)
            return {NameOutcome::selected, "@" + found->plural.classic};
        const auto offset = static_cast<std::size_t>(remaining - 1);
        if (offset < found->names.size())
            return {NameOutcome::selected, found->names[offset].classic};
        if (!found->complete)
            throw std::invalid_argument("City name selection exceeds an incomplete imported list");
        const auto extra = offset - found->names.size();
        if (lists.fallback && extra < lists.fallback->size())
            return {NameOutcome::selected, (*lists.fallback)[extra].classic};
        return {NameOutcome::exhausted, {}};
    }
    return {};
}

} // namespace openciv::city
