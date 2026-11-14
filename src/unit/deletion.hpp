#pragma once

#include "state/game.hpp"
#include "unit/stacks.hpp"
#include "unit/request.hpp"

namespace openciv::unit {
// U01 sections 6, 9: owning children/transport services, not optional observers.
// A missing binding stops at its invocation after preceding effects. Inert
// bindings are appropriate only when the supplied child contract is inert.
struct DeletionServices : UnitNetworkServices {
    std::function<void(world::Position)> removed_unit;
    std::function<void(std::int32_t)> refresh_home;
    std::function<void(std::int32_t owner, std::int32_t killer)> check_destroyed;
    std::function<void(world::Position)> unit_change;
    std::function<void(std::int32_t)> home_change;
    std::function<void(std::int32_t)> request_delete;
};

// Real local bookkeeping and U01 client request/wait boundary. N02/N03 still
// own wire encoding, dispatch and synchronization; V01 owns checkDestroyed.
// Rules, world and supplied backing must remain alive across child calls.
class UnitDeletion {
public:
    UnitDeletion(state::GameState& world, const state::RuleDefinitions& rules, StackBudget& budget,
                 StackServices stacks, DeletionServices services);
    void erase(std::int32_t slot, bool notify);

private:
    void remote(std::int32_t slot);
    std::int32_t role(std::size_t type) const;
    state::Civilization& civilization(std::int32_t owner);
    state::GameState& world_;
    const state::RuleDefinitions& rules_;
    LocalStacks stacks_;
    DeletionServices services_;
};
} // namespace openciv::unit
