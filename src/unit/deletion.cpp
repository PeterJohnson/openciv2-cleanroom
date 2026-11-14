#include "unit/deletion.hpp"
#include "base/arithmetic.hpp"
#include <bit>
#include <utility>

namespace openciv::unit {
UnitDeletion::UnitDeletion(state::GameState& world, const state::RuleDefinitions& rules,
                           StackBudget& budget, StackServices stacks, DeletionServices services)
    : world_(world), rules_(rules),
      stacks_(world.entities.units, world.map, world.unit_stacks, budget, std::move(stacks)),
      services_(std::move(services)) {}
std::int32_t UnitDeletion::role(std::size_t type) const {
    return std::bit_cast<std::int8_t>(static_cast<std::uint8_t>(rules_.units.at(type).role));
}
state::Civilization& UnitDeletion::civilization(std::int32_t owner) {
    if (owner < 0 || static_cast<std::size_t>(owner) >= world_.civilizations.size())
        throw std::out_of_range("Unit deletion has no civilization backing");
    return world_.civilizations[static_cast<std::size_t>(owner)];
}
void UnitDeletion::remote(std::int32_t slot) {
    auto& operation = world_.unit_deletion;
    request_unit_operation(
        operation.active, operation.reply, world_.turn_loop_active,
        [&] { services_.request_delete(slot); }, services_);
}
void UnitDeletion::erase(std::int32_t slot, bool notify) {
    if (slot < 0 || !state::occupied(stacks_.backed_unit(slot).token))
        return;
    world_.unit_deletion.active = true;
    if (world_.globals.multiplayer >= 3 && !world_.multiplayer.network_host) {
        remote(slot);
        return;
    }
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
        services_.removed_unit(position);
        if (home)
            services_.refresh_home(static_cast<std::int32_t>(*home));
    }
    if (role(type) == 5 && civilization(owner).city_count == 0 &&
        civilization(owner).units_active.at(type) == 0)
        services_.check_destroyed(owner, -1);
    if (world_.globals.multiplayer >= 3 && notify) {
        services_.state_difference();
        services_.unit_change(position);
        if (home && static_cast<std::int32_t>(*home) > 0)
            services_.home_change(static_cast<std::int32_t>(*home));
        services_.flush(5000);
    }
    world_.unit_deletion.active = false;
}
} // namespace openciv::unit
