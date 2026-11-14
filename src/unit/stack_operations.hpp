#pragma once

#include "state/game.hpp"
#include "unit/request.hpp"
#include "unit/stacks.hpp"

namespace openciv::unit {
enum class PlacementOperation { detach, attach, relocate, bottom };
struct PlacementRequest {
    PlacementOperation operation;
    // U01 section 9: unit, optionally x/y, with all unused arguments zero.
    // N02 owns the wire envelope. The caller's sync flag is never forwarded.
    std::array<std::int32_t, 6> arguments{};
    bool operator==(const PlacementRequest&) const = default;
};
struct StackOperationServices : UnitNetworkServices {
    std::function<void(PlacementRequest)> request;
};
// U01 sections 2–4, 9: independently gated placement, including the children
// reached from repairing traversals. No terrain revelation or move cost is added.
class StackOperations {
public:
    StackOperations(state::GameState& world, StackBudget& budget, StackServices stack_services,
                    StackOperationServices services);
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
    bool begin(PlacementRequest request);
    void synchronize(bool sync);
    void finish(PlacementOperation operation, bool sync);
    state::GameState& world_;
    StackOperationServices services_;
    LocalStacks local_;
};
} // namespace openciv::unit
