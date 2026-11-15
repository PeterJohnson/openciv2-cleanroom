import std;
import openciv.state.records;
import openciv.base.arithmetic;

namespace openciv::state {
std::optional<UnitSlot> reserve_unit_slot(UnitPool& pool, CivilizationSlot owner,
                                          std::int16_t military_count, std::uint8_t humans) {
    // Keep the existing F02 primitive as the single owner of reservation policy.
    std::array<UnitToken, unit_capacity> tokens;
    for (std::size_t i = 0; i < pool.end(); ++i)
        tokens[i] = pool.storage()[i].token;
    auto end = static_cast<std::uint32_t>(pool.end());
    const auto slot =
        reserve_unit_slot(std::span{tokens}.first(end), end, owner, military_count, humans);
    pool.set_end(end);
    return slot;
}
void invalidate_entities_for_new_session(EntityPools& pools) {
    for (auto& unit : pools.units.storage()) {
        unit.token = UnitToken{0};
        unit.previous = {};
        unit.next = {};
    }
    for (auto& city : pools.cities.storage())
        city.token = CityToken{0};
    pools.units.set_end(0);
    pools.cities.set_end(0);
}
void reset_entity_scans(EntityPools& pools, std::optional<UnitSlot>& selected) {
    pools.units.set_end(0);
    pools.cities.set_end(0);
    selected = UnitSlot{0};
}
void renew_loaded_lifetimes(EntityPools& pools, ProcessTokens& tokens, bool legacy_without_ids) {
    for (auto& unit : pools.units.scanned())
        if (legacy_without_ids || occupied(unit.token))
            unit.token = tokens.assign_unit();
    for (auto& city : pools.cities.scanned())
        if (legacy_without_ids || occupied(city.token))
            city.token = tokens.assign_city();
}
std::int32_t city_calculation_entry(std::optional<CitySlot> city, bool full,
                                    CityMoodWorkspace& workspace,
                                    const std::function<std::int32_t()>& pipeline) {
    if (!city)
        return 0;
    if (!full)
        return base::subtract32(workspace.happy, workspace.unhappy);
    workspace.city = city;
    if (!pipeline)
        throw std::invalid_argument("Full city calculation requires its owning pipeline");
    return pipeline();
}
} // namespace openciv::state
