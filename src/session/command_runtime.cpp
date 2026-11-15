import std;
import openciv.session.runtime_internal;

namespace openciv::session {
namespace {
using detail::CommandRecord;
using detail::Lease;
using detail::RuntimeState;
std::uint64_t next(std::uint64_t& value) {
    if (value == std::numeric_limits<std::uint64_t>::max())
        throw std::overflow_error("Command identity space exhausted");
    return value++;
}
void require_active_commands(const RuntimeState& state) {
    if (!state.active)
        throw AccessError(AccessFailure::session_ended);
}
bool permitted(AccessClass grant, AccessClass needed) {
    return needed == AccessClass::player || grant == AccessClass::developer;
}
bool owner(const Lease& lease, const CommandRecord& record) {
    return lease.identity.participant == record.actor.participant &&
           lease.identity.observer == record.actor.civilization &&
           permitted(lease.identity.access, record.definition->descriptor.access);
}
bool recipient(const Lease& lease, const Interaction& interaction) {
    return lease.identity.participant == interaction.recipient &&
           lease.identity.observer == interaction.observer;
}
bool decision_access(const Lease& lease, const CommandRecord& record) {
    const auto& interaction = record.update.interaction;
    return interaction && recipient(lease, *interaction) &&
           (interaction->recipient != record.actor.participant ||
            permitted(lease.identity.access, record.definition->descriptor.access));
}
std::optional<CommandUpdate> visible(const Lease& lease, const CommandRecord& record,
                                     std::size_t first_effect = 0) {
    if (owner(lease, record)) {
        const auto& stored = record.update;
        CommandUpdate update{stored.correlation, stored.status,      stored.code,
                             stored.result,      stored.interaction, {}};
        update.committed.assign(stored.committed.begin() +
                                    static_cast<std::ptrdiff_t>(first_effect),
                                stored.committed.end());
        if (update.interaction && !recipient(lease, *update.interaction))
            update.interaction.reset();
        return update;
    }
    if (decision_access(lease, record)) {
        // A different decision recipient sees its prompt, not the actor's effects/results.
        CommandUpdate update;
        update.correlation = record.update.correlation;
        update.status = CommandStatus::awaiting_interaction;
        update.interaction = record.update.interaction;
        return update;
    }
    return {};
}
void deliver(RuntimeState& state, const CommandRecord& record) {
    const auto command = record.update.correlation.command;
    for (auto& [id, lease] : state.leases) {
        const auto found = lease->delivered_effects.find(command);
        const auto first = found == lease->delivered_effects.end() ? 0 : found->second;
        if (auto update = visible(*lease, record, first)) {
            if (owner(*lease, record)) {
                // Allocate the cursor before enqueueing, then advance it only
                // after the notification is retained. Failed delivery cannot
                // silently skip a committed effect on the next attempt.
                auto& cursor = lease->delivered_effects.try_emplace(command, first).first->second;
                lease->updates.push_back(std::move(*update));
                cursor = record.update.committed.size();
            } else {
                lease->updates.push_back(std::move(*update));
            }
        }
    }
}
void clear_interaction(RuntimeState& state, CommandRecord& record) {
    if (record.update.interaction) {
        for (auto& [id, lease] : state.leases) {
            if (decision_access(*lease, record) && !owner(*lease, record)) {
                CommandUpdate withdrawn;
                withdrawn.correlation = record.update.correlation;
                withdrawn.status = record.update.status;
                lease->updates.push_back(std::move(withdrawn));
            }
        }
        record.update.interaction.reset();
    }
}
void publish_revision(RuntimeState& state) {
    if (state.revision == std::numeric_limits<std::uint64_t>::max())
        throw std::overflow_error("World observation revision exhausted");
    ++state.revision;
}
CommandCorrelation correlation(RuntimeState& state) {
    return {state.identity, state.session, CommandId{next(state.next_command)}};
}
CommandUpdate rejected(CommandCorrelation id, std::string code) {
    CommandUpdate update;
    update.correlation = id;
    update.code = std::move(code);
    return update;
}
void finish(RuntimeState& state, CommandRecord& record, CommandStatus status, CommandValues result,
            std::string code) {
    record.update.status = status;
    record.update.result = std::move(result);
    record.update.code = std::move(code);
    clear_interaction(state, record);
    record.continuation.reset();
    if (record.reply_to) {
        state.replies.push_back({record.reply_to->first, record.reply_to->second, status,
                                 record.update.result, record.update.code});
        record.reply_to.reset();
    }
    deliver(state, record);
}
CommandUpdate submit(RuntimeState& state, CommandActor actor, CommandRequest request,
                     CommandCorrelation id, const std::shared_ptr<Lease>& lease,
                     std::optional<std::pair<ConnectionId, CommandCorrelation>> reply_to = {}) {
    const auto found = state.definitions.find(request.name);
    if (found == state.definitions.end() ||
        !permitted(actor.access, found->second.descriptor.access))
        return rejected(id, "command_not_available");
    const auto& definition = found->second;
    if (const auto error = validate_values(definition.descriptor.arguments, request.arguments))
        return rejected(id, *error);
    Admission admission;
    try {
        admission = definition.admit(actor, request, *state.world);
    } catch (const std::bad_alloc&) {
        throw;
    } catch (...) {
        return rejected(id, "admission_exception");
    }
    if (!state.active || state.session != id.session || (lease && !lease->active))
        return rejected(id, "binding_changed_during_admission");
    if (actor.source) {
        const auto seat = state.participants.find(actor.participant);
        if (seat == state.participants.end() || seat->second.connection != actor.source ||
            seat->second.civilization != actor.civilization)
            return rejected(id, "peer_binding_changed");
    }
    if (admission.rejection)
        return rejected(id, *admission.rejection);
    if (admission.route != CommandRoute::execute && admission.route != CommandRoute::await_peer)
        return rejected(id, "invalid_authority_route");
    if (admission.route == CommandRoute::await_peer &&
        (!admission.executor || !definition.apply_result || actor.source))
        return rejected(id, "unsupported_authority_route");
    CommandRecord record;
    record.actor = actor;
    record.request = std::move(request);
    record.definition = &definition;
    record.update.correlation = id;
    record.update.status = CommandStatus::pending;
    record.reply_to = reply_to;
    if (admission.route == CommandRoute::await_peer)
        record.executor = admission.executor;
    auto& installed = state.commands.emplace(id.command, std::move(record)).first->second;
    if (installed.executor)
        state.requests.push_back({*installed.executor, id, installed.request});
    else
        state.ready.push_back({id.command, {OperationInputKind::begin, {}}});
    deliver(state, installed);
    return installed.update;
}
bool interaction_valid(const InteractionSpec& spec) {
    if (spec.prompt.empty() || spec.choices.empty())
        return false;
    const std::set<std::int32_t> choices(spec.choices.begin(), spec.choices.end());
    return choices.size() == spec.choices.size() &&
           (!spec.default_choice || (spec.deadline && choices.contains(*spec.default_choice)));
}
void apply_step(RuntimeState& state, CommandRecord& record, OperationStep step) {
    if (step.status == CommandStatus::completed || step.status == CommandStatus::failed ||
        step.status == CommandStatus::cancelled) {
        if (step.status == CommandStatus::completed) {
            if (const auto error =
                    validate_values(record.definition->descriptor.result, step.result)) {
                finish(state, record, CommandStatus::failed, {},
                       "invalid_operation_result:" + *error);
                return;
            }
        }
        finish(state, record, step.status, std::move(step.result), std::move(step.code));
        return;
    }
    if (step.status != CommandStatus::pending && step.status != CommandStatus::awaiting_interaction)
        throw std::logic_error("Continuation returned an invalid lifecycle state");
    record.update.status = step.status;
    record.update.code = std::move(step.code);
    clear_interaction(state, record);
    if (step.status == CommandStatus::awaiting_interaction) {
        if (!step.interaction || !interaction_valid(*step.interaction))
            throw std::logic_error("Continuation returned an invalid interaction");
        const auto participant = step.interaction->recipient.value_or(record.actor.participant);
        state::CivilizationSlot observer = record.actor.civilization;
        if (participant != record.actor.participant)
            observer = state.participants.at(participant).civilization;
        record.update.interaction =
            Interaction{InteractionId{next(state.next_interaction)}, participant, observer,
                        std::move(*step.interaction)};
    }
    deliver(state, record);
}
} // namespace

namespace detail {
bool pending(const RuntimeState& state) noexcept {
    return std::any_of(state.commands.begin(), state.commands.end(),
                       [](const auto& entry) { return !terminal(entry.second.update.status); });
}
void require_quiescent(const RuntimeState& state) {
    if (state.advancing || state.activities || pending(state) || !state.requests.empty() ||
        !state.replies.empty())
        throw std::logic_error("Finish owner workflows/commands and transfer required outboxes "
                               "before replacing a session");
}
void restore_pending_updates(RuntimeState& state, Lease& lease) {
    for (const auto& [id, record] : state.commands)
        if (!terminal(record.update.status))
            if (auto update = visible(lease, record)) {
                if (owner(lease, record))
                    lease.delivered_effects[id] = record.update.committed.size();
                lease.updates.push_back(std::move(*update));
            }
}
} // namespace detail

std::vector<CommandDescriptor> PlayerClient::commands() const {
    const auto state = state_.lock();
    const auto lease = checked(state);
    std::vector<CommandDescriptor> result;
    for (const auto& [name, definition] : state->definitions)
        if (permitted(lease->identity.access, definition.descriptor.access))
            result.push_back(definition.descriptor);
    return result;
}
CommandUpdate PlayerClient::submit(CommandRequest request) const {
    const auto state = state_.lock();
    const auto lease = checked(state);
    const auto& identity = lease->identity;
    return session::submit(
        *state, {identity.participant, identity.observer, identity.controller, identity.access, {}},
        std::move(request), correlation(*state), lease);
}
std::vector<CommandUpdate> PlayerClient::updates() const {
    const auto state = state_.lock();
    const auto lease = checked(state);
    std::vector<CommandUpdate> result;
    result.reserve(lease->updates.size());
    while (!lease->updates.empty()) {
        result.push_back(std::move(lease->updates.front()));
        lease->updates.pop_front();
    }
    return result;
}
std::optional<CommandUpdate> PlayerClient::result(CommandId command) const {
    const auto state = state_.lock();
    const auto lease = checked(state);
    const auto record = state->commands.find(command);
    return record == state->commands.end() ? std::nullopt : visible(*lease, record->second);
}
InputReceipt PlayerClient::reply(InteractionId interaction, std::int32_t choice) const {
    const auto state = state_.lock();
    const auto lease = checked(state);
    for (auto& [id, record] : state->commands) {
        const auto& pending = record.update.interaction;
        if (!pending || pending->id != interaction || !decision_access(*lease, record))
            continue;
        if (std::find(pending->spec.choices.begin(), pending->spec.choices.end(), choice) ==
            pending->spec.choices.end())
            return {false, "choice_not_allowed"};
        const auto& sender = lease->identity;
        state->ready.push_back(
            {id,
             {OperationInputKind::choice, choice,
              CommandActor{
                  sender.participant, sender.observer, sender.controller, sender.access, {}}}});
        record.update.status = CommandStatus::pending;
        clear_interaction(*state, record);
        deliver(*state, record);
        return {true, "queued"};
    }
    return {false, "interaction_not_available"};
}
InputReceipt PlayerClient::cancel(CommandId command) const {
    const auto state = state_.lock();
    const auto lease = checked(state);
    const auto found = state->commands.find(command);
    if (found == state->commands.end() || terminal(found->second.update.status))
        return {false, "command_not_available"};
    auto& record = found->second;
    if (!owner(*lease, record) && !decision_access(*lease, record))
        return {false, "command_not_available"};
    if (record.executor)
        return {false, "peer_cancellation_requires_mode_adapter"};
    if (record.update.interaction && !record.update.interaction->spec.cancellable)
        return {false, "not_cancellable"};
    const auto& sender = lease->identity;
    state->ready.push_back(
        {command,
         {OperationInputKind::cancel,
          {},
          CommandActor{
              sender.participant, sender.observer, sender.controller, sender.access, {}}}});
    record.update.status = CommandStatus::pending;
    clear_interaction(*state, record);
    deliver(*state, record);
    return {true, "queued"};
}
void SessionRuntime::register_command(CommandDefinition definition) {
    if (state_->active)
        throw std::logic_error("Register commands before starting the client session");
    validate_descriptor(definition.descriptor);
    if (!definition.admit || !definition.execute)
        throw std::invalid_argument("Command requires explicit authority admission and execution");
    const auto name = definition.descriptor.name;
    if (!state_->definitions.emplace(name, std::move(definition)).second)
        throw std::invalid_argument("Duplicate command name");
}
bool SessionRuntime::advance_one() {
    require_active_commands(*state_);
    if (state_->advancing)
        throw std::logic_error("Continuation execution cannot recursively advance the runtime");
    if (state_->ready.empty())
        return false;
    const auto work = state_->ready.front();
    state_->ready.pop_front();
    auto& record = state_->commands.at(work.command);
    if (terminal(record.update.status))
        return true; // A later queued cancellation cannot undo an earlier completion.
    state_->advancing = true;
    struct Guard {
        bool& flag;
        ~Guard() { flag = false; }
    } guard{state_->advancing};
    OperationStep step;
    try {
        if (!record.continuation) {
            record.continuation =
                record.result_application
                    ? record.definition->apply_result(record.request, record.resolved)
                    : record.definition->execute(record.request);
            if (!record.continuation)
                throw std::logic_error("Missing continuation");
        }
        OperationContext context{
            *state_->world,
            state_->process,
            *state_->gameplay,
            *state_->presentation,
            record.actor,
            record.update.correlation,
            [&](std::string name, CommandValues values) {
                const auto index = static_cast<std::uint64_t>(record.update.committed.size()) + 1;
                record.update.committed.push_back({index, std::move(name), std::move(values)});
                publish_revision(*state_);
                deliver(*state_, record);
            },
            [&] { publish_revision(*state_); }};
        step = record.continuation->resume(context, work.input);
    } catch (const base::ChoiceError& error) {
        finish(*state_, record, CommandStatus::failed, {},
               "random_choice_" + std::string(base::choice_failure_code(error.failure())));
        return true;
    } catch (const std::bad_alloc&) {
        throw;
    } catch (...) {
        finish(*state_, record, CommandStatus::failed, {}, "operation_exception");
        return true;
    }
    try {
        apply_step(*state_, record, std::move(step));
    } catch (const std::logic_error&) {
        finish(*state_, record, CommandStatus::failed, {}, "invalid_operation_step");
    }
    return true;
}
bool SessionRuntime::continue_command(CommandId command) {
    require_active_commands(*state_);
    const auto found = state_->commands.find(command);
    if (found == state_->commands.end() || terminal(found->second.update.status) ||
        found->second.update.interaction || found->second.executor ||
        std::any_of(state_->ready.begin(), state_->ready.end(),
                    [&](const auto& w) { return w.command == command; }))
        return false;
    state_->ready.push_back({command, {OperationInputKind::proceed, {}}});
    return true;
}
InputReceipt SessionRuntime::cancel_command(CommandId command) {
    require_active_commands(*state_);
    const auto found = state_->commands.find(command);
    if (found == state_->commands.end() || terminal(found->second.update.status))
        return {false, "command_not_available"};
    auto& record = found->second;
    if (record.executor)
        return {false, "peer_cancellation_requires_mode_adapter"};
    state_->ready.push_back({command, {OperationInputKind::cancel, {}}});
    record.update.status = CommandStatus::pending;
    clear_interaction(*state_, record);
    deliver(*state_, record);
    return {true, "queued"};
}
std::size_t SessionRuntime::expire_interactions(std::uint64_t now) {
    require_active_commands(*state_);
    std::size_t count = 0;
    for (auto& [id, record] : state_->commands) {
        const auto& interaction = record.update.interaction;
        if (!interaction || !interaction->spec.deadline || now < *interaction->spec.deadline)
            continue;
        state_->ready.push_back(
            {id, {OperationInputKind::timeout, interaction->spec.default_choice}});
        record.update.status = CommandStatus::pending;
        clear_interaction(*state_, record);
        deliver(*state_, record);
        ++count;
    }
    return count;
}
bool SessionRuntime::retire_command(CommandId command) {
    require_active_commands(*state_);
    if (state_->advancing)
        throw std::logic_error("Retire commands only between operation steps");
    const auto found = state_->commands.find(command);
    if (found == state_->commands.end() || !terminal(found->second.update.status))
        return false;
    // Peer receipts remain valid for the entire session. A mode adapter must
    // establish a protocol-specific acknowledgement/epoch boundary before a
    // future API can retire them; forgetting one here could reexecute a duplicate.
    for (const auto& [origin, retained] : state_->received)
        if (retained == command)
            return false;
    for (const auto& request : state_->requests)
        if (request.correlation == found->second.update.correlation)
            return false;
    if (found->second.reply_to)
        return false;
    for (const auto& [id, lease] : state_->leases)
        for (const auto& update : lease->updates)
            if (update.correlation.command == command)
                return false;
    // A terminal command may still have a late queued cancellation. It cannot
    // execute any work and must not leave an index into the retired record.
    std::erase_if(state_->ready, [&](const auto& work) { return work.command == command; });
    for (auto& [id, lease] : state_->leases)
        lease->delivered_effects.erase(command);
    state_->commands.erase(found);
    return true;
}
bool SessionRuntime::has_pending_commands() const noexcept {
    return detail::pending(*state_);
}
CommandUpdate SessionRuntime::receive_request(ConnectionId source, ParticipantId participant,
                                              CommandCorrelation origin, CommandRequest request) {
    require_active_commands(*state_);
    const auto key = std::pair{source, origin};
    const auto seat = state_->participants.find(participant);
    CommandUpdate update;
    if (origin.runtime == RuntimeId{0} || origin.session == SessionId{0} ||
        origin.command == CommandId{0})
        return rejected(correlation(*state_), "invalid_peer_correlation");
    else if (seat == state_->participants.end() || seat->second.connection != source)
        update = rejected(correlation(*state_), "peer_not_bound");
    else if (const auto old = state_->received.find(key); old != state_->received.end()) {
        const auto& record = state_->commands.at(old->second);
        if (record.actor.participant != participant ||
            record.actor.civilization != seat->second.civilization || record.request != request)
            update = rejected(correlation(*state_), "peer_correlation_reused");
        else {
            if (terminal(record.update.status))
                state_->replies.push_back({source, origin, record.update.status,
                                           record.update.result, record.update.code});
            return record.update;
        }
    } else {
        const auto id = correlation(*state_);
        update = session::submit(
            *state_, {participant, seat->second.civilization, {}, AccessClass::player, source},
            std::move(request), id, {}, key);
    }
    if (update.status == CommandStatus::rejected)
        state_->replies.push_back({source, origin, CommandStatus::rejected, {}, update.code});
    else
        state_->received.emplace(key, update.correlation.command);
    return update;
}
InputReceipt SessionRuntime::receive_result(ConnectionId source, const RoutedReply& reply) {
    require_active_commands(*state_);
    const auto found = state_->commands.find(reply.correlation.command);
    if (found == state_->commands.end() || found->second.update.correlation != reply.correlation ||
        found->second.executor != source)
        return {false, "unexpected_result"};
    auto& record = found->second;
    if (!terminal(reply.status))
        return {false, "result_not_terminal"};
    if (reply.status == CommandStatus::completed) {
        if (const auto error = validate_values(record.definition->descriptor.result, reply.result))
            return {false, *error};
        record.resolved = reply.result;
        record.result_application = true;
        record.executor.reset();
        state_->ready.push_back({reply.correlation.command, {OperationInputKind::begin, {}}});
    } else {
        record.executor.reset();
        finish(*state_, record,
               reply.status == CommandStatus::cancelled ? CommandStatus::cancelled
                                                        : CommandStatus::failed,
               {}, reply.code);
    }
    return {true, "accepted"};
}
std::vector<RoutedRequest> SessionRuntime::take_requests() {
    std::vector<RoutedRequest> result;
    result.swap(state_->requests);
    return result;
}
std::vector<RoutedReply> SessionRuntime::take_replies() {
    std::vector<RoutedReply> result;
    result.swap(state_->replies);
    return result;
}
} // namespace openciv::session
