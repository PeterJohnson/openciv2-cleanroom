export module openciv.session.classic_loaded_units;

import std;
export import openciv.unit.deletion;
export import openciv.unit.stack_operations;

export extern "C++" {
    namespace openciv::session {
    // F06 section 11, accepted-file wrapper step 5 only. The parent must already
    // have completed successful core input, event/options/audio/title operations
    // and returned home. This is not load admission or a replacement for that wrapper.
    // Real U01 deletion runs with notify=false, including required V01 checks.
    // Placement children inside validation recheck live authority independently.
    void cleanup_classic_loaded_units(state::GameState& world, const state::RuleDefinitions& rules,
                                      unit::StackBudget& budget, unit::StackDiagnostics stacks,
                                      unit::StackSynchronization placement,
                                      const unit::UnitDeletionEffects& deletion);
    } // namespace openciv::session
}
