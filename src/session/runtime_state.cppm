module openciv.session.runtime:state;

import std;
import openciv.session.runtime;
import openciv.session.client_connection;

namespace openciv::session::detail {
/// A revocable client identity with queued notifications and delivered-effect cursors.
struct Lease {
    ClientIdentity identity;
    bool active = true;
    std::deque<CommandUpdate> updates;
    std::map<CommandId, std::size_t> delivered_effects;
};
/// Owned admitted command state, continuation, routing, and retained result.
struct CommandRecord {
    CommandActor actor;
    CommandRequest request;
    const CommandDefinition* definition = nullptr;
    CommandUpdate update;
    std::unique_ptr<Continuation> continuation;
    std::optional<ConnectionId> executor;
    std::optional<std::pair<ConnectionId, CommandCorrelation>> reply_to;
    bool result_application = false;
    CommandValues resolved;
};
/// One queued command continuation input.
struct Work {
    CommandId command;
    OperationInput input;
};
/// Shared runtime backing for world state, client leases, commands, and transport outboxes.
struct RuntimeState {
    /// Borrow process state and allocate a fresh world under the supplied runtime identity.
    RuntimeState(state::ProcessContext& supplied, RuntimeId identity)
        : process(supplied), world(std::make_unique<state::GameState>(supplied)),
          identity(identity) {}
    state::ProcessContext& process;
    std::unique_ptr<state::GameState> world;
    RuntimeId identity;
    bool active = false, advancing = false;
    unsigned activities = 0;
    SessionId session{0};
    std::uint64_t next_session = 1, next_participant = 1, next_controller = 1, revision = 0;
    std::uint64_t next_command = 1, next_interaction = 1;
    std::map<ParticipantId, Participant> participants;
    std::map<ControllerId, std::shared_ptr<Lease>> leases;
    std::optional<base::GameplayRandom> gameplay;
    std::optional<base::PresentationRandom> presentation;
    std::map<std::string, CommandDefinition, std::less<>> definitions;
    std::map<CommandId, CommandRecord> commands;
    std::deque<Work> ready;
    std::vector<RoutedRequest> requests;
    std::vector<RoutedReply> replies;
    std::map<std::pair<ConnectionId, CommandCorrelation>, CommandId> received;
};
/// Report whether any command remains nonterminal.
bool pending(const RuntimeState& state) noexcept;
/// Reject replacement while workflows, pending commands, or transport outboxes remain.
void require_quiescent(const RuntimeState& state);
/// Populate a newly bound lease with the pending updates visible to it.
void restore_pending_updates(RuntimeState& state, Lease& lease);
/// Implement a revocable client port through weak runtime and lease references.
class RuntimeClientConnection final : public ClientConnection {
public:
    /// Bind weak references so a client cannot keep an ended runtime alive.
    RuntimeClientConnection(std::weak_ptr<RuntimeState> state, std::weak_ptr<Lease> lease);
    /// Issue a PlayerClient backed by a runtime lease.
    static PlayerClient bind(std::shared_ptr<RuntimeState> state, std::shared_ptr<Lease> lease) {
        return issue<PlayerClient>(
            std::make_shared<RuntimeClientConnection>(std::move(state), std::move(lease)));
    }
    /// Validate the lease and return its identity.
    ClientIdentity identity() const override;
    /// Project the lease civilization units at the current published revision.
    query::LiveRecords<query::OwnUnit> own_units() const override;
    /// Project the lease civilization cities at the current published revision.
    query::LiveRecords<query::OwnCity> own_cities() const override;
    /// List command descriptors admitted by the lease access class.
    std::vector<CommandDescriptor> commands() const override;
    /// Validate this lease and submit a command to its runtime.
    CommandUpdate submit(CommandRequest request) const override;
    /// Drain notifications belonging to this lease.
    std::vector<CommandUpdate> updates() const override;
    /// Inspect a retained result accessible to this lease.
    std::optional<CommandUpdate> result(CommandId command) const override;
    /// Admit this lease reply to a pending interaction.
    InputReceipt reply(InteractionId interaction, std::int32_t choice) const override;
    /// Admit this lease cancellation request.
    InputReceipt cancel(CommandId command) const override;

private:
    /// Resolve and validate the lease against the current runtime session.
    std::shared_ptr<Lease> checked(const std::shared_ptr<RuntimeState>& state) const;
    std::weak_ptr<RuntimeState> state_;
    std::weak_ptr<Lease> lease_;
};
} // namespace openciv::session::detail
