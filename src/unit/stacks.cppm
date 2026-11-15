export module openciv.unit.stacks;

import std;
export import openciv.base.callback_ref;
export import openciv.state.game;
export import openciv.state.records;
export import openciv.world.map;

export namespace openciv::unit {
class StackOperations;
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
// Optional passive output, consumed after the operation; no diagnostic reentry.
using StackDiagnostics = std::vector<StackDiagnostic>*;
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
                StackBudget& budget, StackDiagnostics services, RepairPlacement placement = {});
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
    StackDiagnostics diagnostics_;
    RepairPlacement placement_;
    std::size_t depth_ = 0;
};
} // namespace openciv::unit

export namespace openciv::unit {
enum class PlacementOperation { detach, attach, relocate, bottom };
// Semantic completion notification. Transport scheduling belongs to the session.
using StackSynchronization = base::CallbackRef<void()>;
// U01 sections 2–4, 9: independently gated placement, including the children
// reached from repairing traversals. Local/host authority is required; remote
// requests use session::UnitOperation. No terrain revelation or move cost is added.
class StackOperations {
public:
    StackOperations(state::GameState& world, StackBudget& budget, StackDiagnostics stack_services,
                    StackSynchronization services);
    StackOperations(const StackOperations&) = delete;
    StackOperations& operator=(const StackOperations&) = delete;
    StackOperations(StackOperations&&) = delete;
    StackOperations& operator=(StackOperations&&) = delete;

    std::int32_t validate(std::int32_t slot) { return local_.validate(slot); }
    std::int32_t raw_forward_count(std::int32_t slot) { return local_.raw_forward_count(slot); }
    std::int32_t next(std::int32_t slot) { return local_.next(slot); }
    std::int32_t first(std::int32_t slot) { return local_.first(slot); }
    std::int32_t last(std::int32_t slot) { return local_.last(slot); }
    std::int32_t first_for_owner_at(std::int32_t owner, world::Position position) {
        return local_.first_for_owner_at(owner, position);
    }
    std::int32_t first_at(world::Position position) { return local_.first_at(position); }
    void detach(std::int32_t slot, bool sync);
    void attach_at_front(std::int32_t slot, world::Position position, bool sync);
    void relocate(std::int32_t slot, world::Position position, bool sync);
    void relocate_in_place(std::int32_t slot, bool sync);
    void move_to_bottom(std::int32_t slot, bool sync);
    void relocate_stack(std::int32_t slot, world::Position position, bool sync);

private:
    struct OperationState {
        bool& active;
        std::int32_t& reply;
    };
    OperationState state(PlacementOperation operation);
    void begin(PlacementOperation operation);
    void synchronize(bool sync);
    void finish(PlacementOperation operation, bool sync);
    state::GameState& world_;
    StackSynchronization synchronize_;
    LocalStacks local_;
};
} // namespace openciv::unit
