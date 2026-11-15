module openciv.session.unit_operations;

import std;
import openciv.base.arithmetic;

namespace openciv::session {
UnitOperation::UnitOperation(SessionRuntime& runtime, UnitRequest request, bool notify,
                             unit::StackBudget budget, UnitOperationDependencies services,
                             std::unique_ptr<UnitRequestChannel> channel)
    : UnitOperation(
          runtime.world(), *runtime.process().rules, [&runtime] { runtime.publish(); }, request,
          notify, budget, std::move(services), std::move(channel)) {
    activity_.emplace(runtime.begin_activity());
}
UnitOperation::UnitOperation(OperationContext& context, UnitRequest request, bool notify,
                             unit::StackBudget budget, UnitOperationDependencies services,
                             std::unique_ptr<UnitRequestChannel> channel)
    : UnitOperation(context.world, *context.process.rules, context.publish, request, notify, budget,
                    std::move(services), std::move(channel)) {}
UnitOperation::UnitOperation(state::GameState& world, state::RuleState& rules,
                             std::function<void()> publish, UnitRequest request, bool notify,
                             unit::StackBudget budget, UnitOperationDependencies services,
                             std::unique_ptr<UnitRequestChannel> channel)
    : world_(world), rules_(rules), publish_(std::move(publish)), request_(request),
      notify_(notify), budget_(budget), services_(services), channel_(std::move(channel)) {
    if (!publish_)
        throw std::invalid_argument("Unit operation requires publication");
    switch (request.operation) {
    case UnitOperationKind::detach:
    case UnitOperationKind::attach:
    case UnitOperationKind::relocate:
    case UnitOperationKind::bottom:
    case UnitOperationKind::erase:
    case UnitOperationKind::relocate_stack:
        break;
    default:
        throw std::invalid_argument("Unknown unit operation");
    }
    stack_ = request.operation == UnitOperationKind::relocate_stack;
    if (stack_)
        request_.operation = UnitOperationKind::relocate;
    const auto used = request_.operation == UnitOperationKind::attach ||
                              request_.operation == UnitOperationKind::relocate
                          ? 3
                          : 1;
    std::fill(request_.arguments.begin() + used, request_.arguments.end(), 0);
    remote_ = world.globals.multiplayer >= state::GameMode::tcp && !world.multiplayer.network_host;
    if (remote_ && (!channel_ || !services_.ticks || !services_.timed_out))
        throw std::invalid_argument(
            "Remote unit operation requires channel, clock and timeout service");
    if (stack_ && notify && world.globals.multiplayer >= state::GameMode::tcp &&
        !services_.placement)
        throw std::invalid_argument("Stack relocation requires synchronization notification");
    if (!remote_) {
        if (request.operation == UnitOperationKind::erase && !services_.deletion)
            throw std::invalid_argument("Unit deletion requires its effect provider");
        if (request.operation != UnitOperationKind::erase && notify &&
            world.globals.multiplayer >= state::GameMode::tcp && !services_.placement)
            throw std::invalid_argument("Placement requires synchronization notification");
    }
}
UnitOperation::~UnitOperation() {
    cancel();
}
bool& UnitOperation::active() {
    auto& world = world_;
    switch (request_.operation) {
    case UnitOperationKind::detach:
        return world.unit_stacks.detach_active;
    case UnitOperationKind::attach:
        return world.unit_stacks.attach_active;
    case UnitOperationKind::relocate:
        return world.unit_stacks.relocate_active;
    case UnitOperationKind::bottom:
        return world.unit_stacks.bottom_active;
    case UnitOperationKind::erase:
        return world.unit_deletion.active;
    case UnitOperationKind::relocate_stack:
        break;
    }
    throw std::logic_error("Unknown operation");
}
std::int32_t& UnitOperation::reply() {
    auto& world = world_;
    switch (request_.operation) {
    case UnitOperationKind::detach:
        return world.unit_stacks.detach_reply;
    case UnitOperationKind::attach:
        return world.unit_stacks.attach_reply;
    case UnitOperationKind::relocate:
        return world.unit_stacks.relocate_reply;
    case UnitOperationKind::bottom:
        return world.unit_stacks.bottom_reply;
    case UnitOperationKind::erase:
        return world.unit_deletion.reply;
    case UnitOperationKind::relocate_stack:
        break;
    }
    throw std::logic_error("Unknown operation");
}
void UnitOperation::finish(UnitOperationResult result) noexcept {
    result_ = result;
    auto channel = std::move(channel_);
    if (channel)
        channel->cancel();
    activity_.reset();
}
std::optional<UnitOperationResult> UnitOperation::complete_child(UnitOperationResult result) {
    any_timeout_ |= result.timed_out;
    if (stack_ && saved_next_ >= 0) {
        request_.arguments[0] = saved_next_;
        started_ = false;
        timed_out_ = false;
        return {}; // Preserve the captured successor across the suspended child.
    }
    if (stack_ && notify_ && world_.globals.multiplayer >= state::GameMode::tcp) {
        if (!services_.placement)
            throw std::invalid_argument("Stack relocation requires synchronization notification");
        services_.placement();
        publish_();
    }
    result.timed_out = any_timeout_;
    finish(result);
    return result_;
}
void UnitOperation::cancel() noexcept {
    if (polling_) {
        cancel_requested_ = true;
        return;
    }
    if (!result_)
        finish({-2, timed_out_ || any_timeout_, true});
}
std::optional<UnitOperationResult> UnitOperation::poll() {
    if (polling_)
        throw std::logic_error("Unit operation poll cannot reenter");
    if (result_)
        return result_;
    polling_ = true;
    struct Exit {
        UnitOperation& operation;
        ~Exit() {
            operation.polling_ = false;
            if (operation.cancel_requested_)
                operation.cancel();
        }
    } exit{*this};
    const auto cancelled = [&] {
        if (!cancel_requested_)
            return false;
        finish({-2, timed_out_ || any_timeout_, true});
        return true;
    };
    try {
        auto& world = world_;
        if (!started_ && stack_) {
            unit::StackOperations traversal(world, budget_, services_.stacks, services_.placement);
            if (!stack_initialized_) {
                request_.arguments[0] = traversal.first(request_.arguments[0]);
                stack_initialized_ = true;
            }
            if (request_.arguments[0] < 0)
                return complete_child({});
            saved_next_ = traversal.next(request_.arguments[0]);
            remote_ = world.globals.multiplayer >= state::GameMode::tcp &&
                      !world.multiplayer.network_host;
            if (remote_ && (!channel_ || !services_.ticks || !services_.timed_out))
                throw std::invalid_argument(
                    "Remote stack child requires channel, clock and timeout service");
        }
        const auto slot = request_.arguments[0];
        if (!started_) {
            if (request_.operation == UnitOperationKind::erase &&
                (slot < 0 ||
                 !state::occupied(
                     world.entities.units.at(static_cast<state::UnitSlot>(slot)).token))) {
                finish({});
                return result_;
            }
            if (request_.operation == UnitOperationKind::bottom &&
                world.entities.units.at(static_cast<state::UnitSlot>(slot)).next.value < 0) {
                finish({});
                return result_;
            }
            started_ = true;
            if (!remote_) {
                unit::StackOperations placement(world, budget_, services_.stacks,
                                                services_.placement);
                const world::Position position{request_.arguments[1], request_.arguments[2]};
                switch (request_.operation) {
                case UnitOperationKind::detach:
                    placement.detach(slot, notify_);
                    break;
                case UnitOperationKind::attach:
                    placement.attach_at_front(slot, position, notify_);
                    break;
                case UnitOperationKind::relocate:
                    placement.relocate(slot, position, notify_ && !stack_);
                    break;
                case UnitOperationKind::bottom:
                    placement.move_to_bottom(slot, notify_);
                    break;
                case UnitOperationKind::erase:
                    unit::UnitDeletion(world, rules_.definitions, budget_, services_.stacks,
                                       *services_.deletion)
                        .erase(slot, notify_);
                    break;
                case UnitOperationKind::relocate_stack:
                    throw std::logic_error("Composite request was not reduced");
                }
                publish_();
                return complete_child({});
            }
            active() = false;
            reply() = -2;
            publish_();
            channel_->send(request_);
            if (cancelled())
                return result_;
            started_at_ = services_.ticks();
            if (cancelled())
                return result_;
            return {}; // Always yield after sending, including synchronous adapters.
        }
        if (!timed_out_ && reply() == -2 &&
            base::signed32(services_.ticks() - started_at_) >= 3600) {
            timed_out_ = true;
            services_.timed_out();
            world.turn_loop_active = false;
            if (cancelled())
                return result_;
        }
        if (cancelled())
            return result_;
        const auto incoming = channel_->poll(!timed_out_ && reply() == -2);
        if (cancelled())
            return result_;
        if (incoming.value)
            reply() = *incoming.value;
        publish_();
        if ((reply() != -2 || timed_out_) && incoming.drained)
            return complete_child({reply(), timed_out_, false});
        return result_;
    } catch (...) {
        publish_();
        cancel();
        throw;
    }
}
} // namespace openciv::session
