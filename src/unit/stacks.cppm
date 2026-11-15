export module openciv.unit.stacks;

import std;
export import openciv.base.callback_ref;
export import openciv.state.game;
export import openciv.state.records;
export import openciv.world.map;

export namespace openciv::unit {
class StackOperations;
/// Kinds of malformed link or traversal condition diagnosed by stack repair.
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
/// A stack problem with the current and optional neighboring slot.
struct StackDiagnostic {
    StackProblem problem;
    std::int32_t current;
    std::int32_t neighbor = -1;
    bool operator==(const StackDiagnostic&) const = default;
};
// Optional passive output, consumed after the operation; no diagnostic reentry.
using StackDiagnostics = std::vector<StackDiagnostic>*;
/// Repair's placement children use sync=false. LocalStacks defaults to its local
/// bodies; StackOperations supplies the real independently gated operations.
struct RepairPlacement {
    std::function<void(std::int32_t)> detach;
    std::function<void(std::int32_t, world::Position)> attach;
};
/// Host-resource reasons for stopping a stack operation.
enum class StackStop { steps, validation_depth };
/// An execution-budget stop that leaves earlier stack effects committed.
class StackResourceStop : public std::runtime_error {
public:
    /// Describe the exhausted stack execution resource.
    explicit StackResourceStop(StackStop reason);
    /// Return the resource limit that stopped execution.
    StackStop reason() const noexcept { return reason_; }

private:
    StackStop reason_;
};
/// Host execution limits, not game state or a successful gameplay return. Shared
/// by nested calls; a stop retains all earlier record/tile/diagnostic effects.
struct StackBudget {
    std::size_t steps;
    std::size_t validation_depth;
};

/// U01 sections 2–4, 9–10: traversal, repair and the local, unsynchronized
/// placement path. The caller must establish local/host authority. This is not
/// the network-facing U01 command boundary; false sync does not confer authority.
class LocalStacks {
public:
    /// Borrow pool/map state, shared execution limits, and optional repair/diagnostic outputs.
    LocalStacks(state::UnitPool& units, world::MapStorage& map, state::UnitStackState& retained,
                StackBudget& budget, StackDiagnostics services, RepairPlacement placement = {});
    /// Count forward links using the raw U01 traversal and shared step budget.
    std::int32_t raw_forward_count(std::int32_t slot);
    /// Validate and repair a stack link, retaining each specified repair effect.
    std::int32_t validate(std::int32_t slot);
    /// Return the validated forward neighbor or a terminal link.
    std::int32_t next(std::int32_t slot);
    /// Traverse and repair backward links to locate the first unit.
    std::int32_t first(std::int32_t slot);
    /// Traverse and repair forward links to locate the last unit.
    std::int32_t last(std::int32_t slot);
    /// Find the first unit of the requested owner at a map position.
    std::int32_t first_for_owner_at(std::int32_t owner, world::Position position);
    /// Find the first unit at a map position.
    std::int32_t first_at(world::Position position);
    /// Detach a unit locally and update its links and tile presence.
    void detach(std::int32_t slot);
    /// Attach a unit at the front of the destination stack without synchronization.
    void attach_at_front(std::int32_t slot, world::Position position);
    /// Bounded U01 record access. No liveness/scan filter; indices outside the
    /// owned pool are rejected, never resolved through adjacent memory.
    state::Unit& backed_unit(std::int32_t slot) { return record(slot); }

private:
    friend class StackOperations;
    /// Perform the local detach body used by placement and repair.
    void detach_body(std::int32_t slot);
    /// Perform the local front-attachment body used by placement and repair.
    void attach_body(std::int32_t slot, world::Position position);
    /// Return unit backing for a checked index without imposing a liveness filter.
    state::Unit& record(std::int32_t slot);
    /// Consume one traversal step or throw a resource stop.
    void step();
    /// Append a passive diagnostic when a diagnostic sink is supplied.
    void diagnose(StackDiagnostic diagnostic);
    /// Traverse toward the requested end, repairing invalid links along the way.
    std::int32_t end_of_chain(std::int32_t slot, bool backward);
    /// Cut an invalid neighbor link in the selected traversal direction.
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
/// Select the retained busy/reply controls for a placement operation.
enum class PlacementOperation { detach, attach, relocate, bottom };
// Semantic completion notification. Transport scheduling belongs to the session.
using StackSynchronization = base::CallbackRef<void()>;
/// U01 sections 2–4, 9: independently gated placement, including the children
/// reached from repairing traversals. Local/host authority is required; remote
/// requests use session::UnitOperation. No terrain revelation or move cost is added.
class StackOperations {
public:
    /// Borrow world state and wire independently gated placement into local stack repair.
    StackOperations(state::GameState& world, StackBudget& budget, StackDiagnostics stack_services,
                    StackSynchronization services);
    StackOperations(const StackOperations&) = delete;
    StackOperations& operator=(const StackOperations&) = delete;
    StackOperations(StackOperations&&) = delete;
    StackOperations& operator=(StackOperations&&) = delete;

    /// Validate and repair through the shared local traversal implementation.
    std::int32_t validate(std::int32_t slot) { return local_.validate(slot); }
    /// Count raw forward links using the shared local traversal budget.
    std::int32_t raw_forward_count(std::int32_t slot) { return local_.raw_forward_count(slot); }
    /// Return the validated forward neighbor through the local traversal.
    std::int32_t next(std::int32_t slot) { return local_.next(slot); }
    /// Locate the first unit with the local repairing traversal.
    std::int32_t first(std::int32_t slot) { return local_.first(slot); }
    /// Locate the last unit with the local repairing traversal.
    std::int32_t last(std::int32_t slot) { return local_.last(slot); }
    /// Find an owner-matching unit at the supplied position.
    std::int32_t first_for_owner_at(std::int32_t owner, world::Position position) {
        return local_.first_for_owner_at(owner, position);
    }
    /// Find the first unit at the supplied position.
    std::int32_t first_at(world::Position position) { return local_.first_at(position); }
    /// Perform gated detachment and optionally synchronize its completion.
    void detach(std::int32_t slot, bool sync);
    /// Perform gated front attachment and optionally synchronize its completion.
    void attach_at_front(std::int32_t slot, world::Position position, bool sync);
    /// Move a unit to a destination through the U01 placement sequence.
    void relocate(std::int32_t slot, world::Position position, bool sync);
    /// Reapply placement at the unit current coordinates.
    void relocate_in_place(std::int32_t slot, bool sync);
    /// Move a unit to the bottom of its current stack.
    void move_to_bottom(std::int32_t slot, bool sync);
    /// Relocate the stack through its owning placement operations.
    void relocate_stack(std::int32_t slot, world::Position position, bool sync);

private:
    /// Borrow the retained busy flag and reply for one placement operation.
    struct OperationState {
        bool& active;
        std::int32_t& reply;
    };
    /// Select retained controls for an operation kind.
    OperationState state(PlacementOperation operation);
    /// Mark the selected placement operation active.
    void begin(PlacementOperation operation);
    /// Invoke completion synchronization when requested.
    void synchronize(bool sync);
    /// Finish the selected operation and perform requested synchronization.
    void finish(PlacementOperation operation, bool sync);
    state::GameState& world_;
    StackSynchronization synchronize_;
    LocalStacks local_;
};
} // namespace openciv::unit
