import std;
import openciv.state.civilization;

namespace openciv::state {
void rebuild_loaded_unit_census(std::span<Civilization, 8> civilizations,
                                const EntityPools& pools) {
    // F06 section 6 stage 17: each civilization's two counters are rebuilt in
    // order. Other retained summaries (including losses) are not repaired.
    for (std::size_t civ = 0; civ < civilizations.size(); ++civ) {
        auto& current = civilizations[civ];
        current.units_active.fill(0);
        current.units_in_production.fill(0);
        for (const auto& unit : pools.units.scanned()) {
            if (!occupied(unit.token) || static_cast<std::size_t>(unit.owner) != civ)
                continue;
            const auto type = static_cast<std::size_t>(unit.type);
            if (type >= current.units_active.size())
                throw std::out_of_range("Loaded unit census has no type backing");
            ++current.units_active[type];
        }
        for (const auto& city : pools.cities.scanned()) {
            if (!occupied(city.token) || static_cast<std::size_t>(city.owner) != civ ||
                city.production < 0)
                continue;
            const auto type = static_cast<std::size_t>(city.production);
            if (type >= current.units_in_production.size())
                throw std::out_of_range("Loaded production census has no type backing");
            ++current.units_in_production[type];
        }
    }
}
} // namespace openciv::state
