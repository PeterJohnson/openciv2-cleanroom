#pragma once

#include "state/records.hpp"
#include "world/map.hpp"
#include <stdexcept>
#include <vector>

namespace openciv::unit {
enum class StackProblem {
    entry_range,
    infinite,
    backward_dead,
    forward_dead,
    backward_x,
    backward_y,
    forward_x,
    forward_y
};
struct StackDiagnostic {
    StackProblem problem;
    std::int32_t current;
    std::int32_t neighbor = -1;
    bool operator==(const StackDiagnostic&) const = default;
};
struct StackServices {
    // Optional passive output, consumed after the operation; no diagnostic reentry.
    std::vector<StackDiagnostic>* diagnostics = nullptr;
};
// Repair's placement children use sync=false. LocalStacks defaults to its local
// bodies; StackOperations supplies the real independently gated operations.
struct RepairPlacement {
    std::function<void(std::int32_t)> detach;
    std::function<void(std::int32_t, world::Position)> attach;
};
enum class StackStop { steps, validation_depth };
class StackResourceStop : public std::runtime_error {
public:
    explicit StackResourceStop(StackStop reason);
    StackStop reason() const noexcept { return reason_; }

private:
    StackStop reason_;
};
// Host execution limits, not game state or a successful gameplay return. Shared
// by nested calls; a stop retains all earlier record/tile/diagnostic effects.
struct StackBudget {
    std::size_t steps;
    std::size_t validation_depth;
};

// U01 sections 2–4, 9–10: traversal, repair and the local, unsynchronized
// placement path. The caller must establish local/host authority. This is not
// the network-facing U01 command boundary; false sync does not confer authority.
class LocalStacks {
public:
    LocalStacks(state::UnitPool& units, world::MapStorage& map, state::UnitStackState& retained,
                StackBudget& budget, StackServices services, RepairPlacement placement = {});
    std::int32_t raw_forward_count(std::int32_t slot);
    std::int32_t validate(std::int32_t slot);
    std::int32_t next(std::int32_t slot);
    std::int32_t first(std::int32_t slot);
    std::int32_t last(std::int32_t slot);
    std::int32_t first_for_owner_at(std::int32_t owner, world::Position position);
    std::int32_t first_at(world::Position position);
    void detach(std::int32_t slot);
    void attach_at_front(std::int32_t slot, world::Position position);
    // Bounded U01 record access. No liveness/scan filter; indices outside the
    // owned pool are rejected, never resolved through adjacent memory.
    state::Unit& backed_unit(std::int32_t slot) { return record(slot); }

private:
    friend class StackOperations;
    void detach_body(std::int32_t slot);
    void attach_body(std::int32_t slot, world::Position position);
    state::Unit& record(std::int32_t slot);
    void step();
    void diagnose(StackDiagnostic diagnostic);
    std::int32_t end_of_chain(std::int32_t slot, bool backward);
    void cut(std::int32_t slot, std::int32_t neighbor, bool backward);
    state::UnitPool& units_;
    world::MapStorage& map_;
    state::UnitStackState& retained_;
    StackBudget& budget_;
    StackServices services_;
    RepairPlacement placement_;
    std::size_t depth_ = 0;
};
} // namespace openciv::unit
