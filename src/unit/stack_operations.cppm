export module openciv.unit.stack_operations;

import std;
export import openciv.base.callback_ref;
export import openciv.state.game;
export import openciv.unit.stacks;

export extern "C++" {
    namespace openciv::unit {
    enum class PlacementOperation { detach, attach, relocate, bottom };
    // Semantic completion notification. Transport scheduling belongs to the session.
    using StackSynchronization = base::CallbackRef<void()>;
    // U01 sections 2–4, 9: independently gated placement, including the children
    // reached from repairing traversals. Local/host authority is required; remote
    // requests use session::UnitOperation. No terrain revelation or move cost is added.
    class StackOperations {
    public:
        StackOperations(state::GameState& world, StackBudget& budget,
                        StackDiagnostics stack_services, StackSynchronization services);
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
}
