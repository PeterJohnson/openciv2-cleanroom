export module openciv.query.live;

import std;
export import openciv.query.live_records;
export import openciv.state.records;

export namespace openciv::query {
// Architecture §§4–5 / F02 §3: projections, not authority issuers. The runtime
// supplies the observer after validating its private client lease. Stable slot
// order, occupied records below the live scan bound, and owned values only.
// These specifically query own entities; enemy/remembered views require W03.
LiveRecords<OwnUnit> project_own_units(const state::EntityPools& pools,
                                       LiveObservation observation);
LiveRecords<OwnCity> project_own_cities(const state::EntityPools& pools,
                                        LiveObservation observation);
} // namespace openciv::query

module :private;
namespace openciv::query {
namespace {
void admit(const LiveObservation& observation) {
    if (observation.runtime == base::RuntimeId{0} || observation.session == base::SessionId{0} ||
        static_cast<unsigned>(observation.observer) >= 8 ||
        observation.provenance != LiveProvenance::owned_current)
        throw std::invalid_argument("Own-entity projection requires an admitted observer");
}
} // namespace
LiveRecords<OwnUnit> project_own_units(const state::EntityPools& pools,
                                       LiveObservation observation) {
    admit(observation);
    LiveRecords<OwnUnit> result{observation, {}};
    const auto units = pools.units.scanned();
    for (std::size_t i = 0; i < units.size(); ++i) {
        const auto& unit = units[i];
        if (state::occupied(unit.token) && unit.owner == observation.observer)
            result.records.push_back({static_cast<state::UnitSlot>(i), unit.token, unit.x, unit.y,
                                      unit.type, unit.moves_used, unit.damage, unit.orders,
                                      unit.home});
    }
    return result;
}
LiveRecords<OwnCity> project_own_cities(const state::EntityPools& pools,
                                        LiveObservation observation) {
    admit(observation);
    LiveRecords<OwnCity> result{observation, {}};
    const auto cities = pools.cities.scanned();
    for (std::size_t i = 0; i < cities.size(); ++i) {
        const auto& city = cities[i];
        if (state::occupied(city.token) && city.owner == observation.observer) {
            const auto name = city.name;
            result.records.push_back({static_cast<state::CitySlot>(i),
                                      city.token,
                                      city.x,
                                      city.y,
                                      {name.begin(), name.end()},
                                      city.size,
                                      city.production,
                                      city.food_box,
                                      city.shield_box});
        }
    }
    return result;
}
} // namespace openciv::query
