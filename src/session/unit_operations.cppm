export module openciv.session.unit_operations;

import std;
export import openciv.base.callback_ref;
export import openciv.session.runtime;
export import openciv.unit.stacks;
export import openciv.unit.deletion;

export namespace openciv::session {
enum class UnitOperationKind { detach, attach, relocate, bottom, erase, relocate_stack };
struct UnitRequest {
    UnitOperationKind operation;
    std::array<std::int32_t, 6> arguments{};
    bool operator==(const UnitRequest&) const = default;
};
struct UnitReply {
    std::optional<std::int32_t> value;
    bool drained = true;
};
// A uniquely owned channel for sequential requests. A completed queue drain
// allows the next send; only primitive requests reach the channel. poll performs bounded,
// nonblocking work; waiting distinguishes reply waiting from the final queued-message drain.
// Destruction/cancel must release registrations without retaining world references.
class UnitRequestChannel {
public:
    UnitRequestChannel() = default;
    virtual ~UnitRequestChannel() = default;
    UnitRequestChannel(const UnitRequestChannel&) = delete;
    UnitRequestChannel& operator=(const UnitRequestChannel&) = delete;
    virtual void send(UnitRequest request) = 0;
    virtual UnitReply poll(bool waiting) = 0;
    virtual void cancel() noexcept = 0;
};
struct UnitOperationDependencies {
    unit::StackDiagnostics stacks = nullptr;
    unit::StackSynchronization placement;
    const unit::UnitDeletionEffects* deletion = nullptr;
    base::CallbackRef<std::uint32_t()> ticks;
    base::CallbackRef<void()> timed_out;
};
struct UnitOperationResult {
    std::int32_t reply = 0;
    bool timed_out = false, cancelled = false;
};
// Providers and callback owners must outlive this operation. Dependency copies
// retain their identities; only the request channel is transferred into ownership.
// Owns local work or a suspended remote request. Standalone construction pins
// the world; command construction borrows the continuation owner's lifetime.
class UnitOperation {
public:
    UnitOperation(SessionRuntime& runtime, UnitRequest request, bool notify,
                  unit::StackBudget budget, UnitOperationDependencies services,
                  std::unique_ptr<UnitRequestChannel> channel = {});
    // The enclosing command owns world/process/publication lifetimes until this
    // child is destroyed. Copy callbacks, never retain the stack context itself.
    UnitOperation(OperationContext& context, UnitRequest request, bool notify,
                  unit::StackBudget budget, UnitOperationDependencies services,
                  std::unique_ptr<UnitRequestChannel> channel = {});
    ~UnitOperation();
    UnitOperation(const UnitOperation&) = delete;
    UnitOperation& operator=(const UnitOperation&) = delete;
    std::optional<UnitOperationResult> poll();
    void cancel() noexcept;

private:
    UnitOperation(state::GameState& world, state::RuleState& rules, std::function<void()> publish,
                  UnitRequest request, bool notify, unit::StackBudget budget,
                  UnitOperationDependencies services, std::unique_ptr<UnitRequestChannel> channel);
    bool& active();
    std::int32_t& reply();
    void finish(UnitOperationResult result) noexcept;
    std::optional<UnitOperationResult> complete_child(UnitOperationResult result);
    state::GameState& world_;
    state::RuleState& rules_;
    std::function<void()> publish_;
    UnitRequest request_;
    bool notify_, started_ = false, remote_ = false, timed_out_ = false;
    bool polling_ = false, cancel_requested_ = false;
    bool stack_ = false, stack_initialized_ = false, any_timeout_ = false;
    std::int32_t saved_next_ = -1;
    unit::StackBudget budget_;
    UnitOperationDependencies services_;
    std::unique_ptr<UnitRequestChannel> channel_;
    std::optional<SessionActivity> activity_;
    std::optional<UnitOperationResult> result_;
    std::uint32_t started_at_ = 0;
};
} // namespace openciv::session
