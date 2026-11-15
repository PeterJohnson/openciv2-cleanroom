#include "unit/deletion.hpp"
#include "base/arithmetic.hpp"
#include <bit>
#include <utility>

namespace openciv::unit {
UnitDeletion::UnitDeletion(state::GameState& world, const state::RuleDefinitions& rules,
                           StackBudget& budget, StackDiagnostics stacks,
                           const UnitDeletionEffects& services)
    : world_(world), rules_(rules),
      stacks_(world.entities.units, world.map, world.unit_stacks, budget, std::move(stacks)),
      effects_(services) {}
std::int32_t UnitDeletion::role(std::size_t type) const {
    return std::bit_cast<std::int8_t>(static_cast<std::uint8_t>(rules_.units.at(type).role));
}
state::Civilization& UnitDeletion::civilization(std::int32_t owner) {
    if (owner < 0 || static_cast<std::size_t>(owner) >= world_.civilizations.size())
        throw std::out_of_range("Unit deletion has no civilization backing");
    return world_.civilizations[static_cast<std::size_t>(owner)];
}
void UnitDeletion::erase(std::int32_t slot, bool notify) {
    if (slot < 0 || !state::occupied(stacks_.backed_unit(slot).token))
        return;
    if (world_.globals.multiplayer >= state::GameMode::tcp && !world_.multiplayer.network_host)
        throw std::logic_error("Local deletion requires authority; route through UnitOperation");
    world_.unit_deletion.active = true;
    auto& unit = stacks_.backed_unit(slot);
    const world::Position position{unit.x, unit.y};
    const auto type = static_cast<std::size_t>(unit.type);
    const auto owner = std::bit_cast<std::int8_t>(static_cast<std::uint8_t>(unit.owner));
    const auto home = unit.home;
    if (owner >= 0 && slot < 2048) {
        auto& civ = civilization(owner);
        if (civ.military_count != 0 && role(type) < 5)
            civ.military_count = std::bit_cast<std::int16_t>(
                static_cast<std::uint16_t>(static_cast<std::uint16_t>(civ.military_count) - 1U));
        auto& active = civ.units_active.at(type);
        if (active != 0)
            --active;
        auto& total = world_.unit_totals[static_cast<std::size_t>(owner)];
        if (total != 0)
            total = base::subtract32(total, 1);
    }
    stacks_.detach(slot);
    stacks_.backed_unit(slot).token = state::UnitToken{0};
    auto& pool = world_.entities.units;
    if (slot == static_cast<std::int32_t>(pool.end()) - 1) {
        pool.set_end(pool.end() - 1);
        world_.globals.unit_end = static_cast<std::int16_t>(pool.end());
    }
    for (std::size_t i = 0; i < pool.end(); ++i) {
        auto& candidate = pool.storage()[i];
        if (candidate.orders == state::UnitOrder::sleeping && candidate.destination_x == slot)
            candidate.orders = state::UnitOrder::none;
    }
    if (notify) {
        effects_.unit_removed(position);
        if (home)
            effects_.refresh_home_city(static_cast<std::int32_t>(*home));
    }
    if (role(type) == 5 && civilization(owner).city_count == 0 &&
        civilization(owner).units_active.at(type) == 0)
        effects_.check_civilization(owner, -1);
    if (world_.globals.multiplayer >= state::GameMode::tcp && notify) {
        effects_.synchronize_deletion(position, home);
    }
    world_.unit_deletion.active = false;
}
} // namespace openciv::unit
