export module openciv.city.name_catalog;

import std;
export import openciv.state.city_names;
export import openciv.base.text_comparison;

export namespace openciv::city {
enum class NameOutcome { unavailable, exhausted, selected };
struct NameAttempt {
    NameOutcome outcome = NameOutcome::unavailable;
    std::string bytes;
};
// Owned data, independent of source syntax and I/O. Earlier layers take precedence;
// a matching list uses only the fallback from its own layer.
class NameCatalog {
public:
    NameCatalog(std::vector<state::CityNameLists> layers, base::NameComparison comparison);
    NameAttempt select(std::string_view plural, std::uint8_t remaining) const;
    const std::vector<state::CityNameLists>& layers() const noexcept { return layers_; }

private:
    std::vector<state::CityNameLists> layers_;
    base::NameComparison comparison_;
};
} // namespace openciv::city

module :private;
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
