import std;
import openciv.session.classic_loaded_units;

namespace openciv::session {
void cleanup_classic_loaded_units(state::GameState& world, const state::RuleDefinitions& rules,
                                  unit::StackBudget& budget, unit::StackDiagnostics services,
                                  unit::StackSynchronization placement,
                                  const unit::UnitDeletionEffects& deletion_services) {
    world.multiplayer.network_host = true;
    unit::StackOperations stacks(world, budget, services, std::move(placement));
    unit::UnitDeletion deletion(world, rules, budget, std::move(services), deletion_services);
    for (std::size_t i = 0; i < world.entities.units.end(); ++i) {
        auto& current = world.entities.units.storage()[i];
        if (!state::occupied(current.token)) {
            current.previous.value = current.next.value = -1;
        } else {
            const auto slot = static_cast<std::int32_t>(i);
            stacks.validate(slot);
            if (current.x < 0 || current.y < 0)
                deletion.erase(slot, false);
        }
    }
}
} // namespace openciv::session
