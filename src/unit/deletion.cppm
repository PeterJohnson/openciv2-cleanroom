export module openciv.unit.deletion;

import std;
export import openciv.state.game;
export import openciv.unit.stacks;

export extern "C++" {
    namespace openciv::unit {
    // U01 sections 6, 9: owning children/transport services, not optional observers.
    // The provider is borrowed and must outlive UnitDeletion. Required operations
    // are interface methods; a child failure retains preceding effects.
    class UnitDeletionEffects {
    public:
        UnitDeletionEffects() = default;
        virtual ~UnitDeletionEffects() = default;
        UnitDeletionEffects(const UnitDeletionEffects&) = delete;
        UnitDeletionEffects& operator=(const UnitDeletionEffects&) = delete;
        virtual void unit_removed(world::Position position) const = 0;
        virtual void refresh_home_city(std::int32_t city) const = 0;
        virtual void check_civilization(std::int32_t owner, std::int32_t killer) const = 0;
        virtual void synchronize_deletion(world::Position position,
                                          std::optional<state::CitySlot> home) const = 0;
    };

    // Local bookkeeping; session::UnitOperation owns remote request lifetimes. N02/N03 still
    // own wire encoding, dispatch and synchronization; V01 owns checkDestroyed.
    // Rules, world and supplied backing must remain alive across child calls.
    class UnitDeletion {
    public:
        UnitDeletion(state::GameState& world, const state::RuleDefinitions& rules,
                     StackBudget& budget, StackDiagnostics stacks,
                     const UnitDeletionEffects& services);
        void erase(std::int32_t slot, bool notify);

    private:
        std::int32_t role(std::size_t type) const;
        state::Civilization& civilization(std::int32_t owner);
        state::GameState& world_;
        const state::RuleDefinitions& rules_;
        LocalStacks stacks_;
        const UnitDeletionEffects& effects_;
    };
    } // namespace openciv::unit
}
