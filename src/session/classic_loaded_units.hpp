#pragma once

#include "unit/deletion.hpp"
#include "unit/stack_operations.hpp"

namespace openciv::session {
// F06 section 11, accepted-file wrapper step 5 only. The parent must already
// have completed successful core input, event/options/audio/title operations
// and returned home. This is not load admission or a replacement for that wrapper.
// Real U01 deletion runs with notify=false, including required V01 checks.
// Placement children inside validation recheck live authority independently.
void cleanup_classic_loaded_units(state::GameState& world, const state::RuleDefinitions& rules,
                                  unit::StackBudget& budget, unit::StackServices stacks,
                                  unit::StackOperationServices placement,
                                  unit::DeletionServices deletion);
} // namespace openciv::session
