module openciv.session.runtime:state;

import std;
import openciv.session.runtime;
import openciv.session.client_connection;

namespace openciv::session::detail {
struct Lease {
    ClientIdentity identity;
    bool active = true;
    std::deque<CommandUpdate> updates;
    std::map<CommandId, std::size_t> delivered_effects;
};
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
struct Work {
    CommandId command;
    OperationInput input;
};
struct RuntimeState {
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
bool pending(const RuntimeState& state) noexcept;
void require_quiescent(const RuntimeState& state);
void restore_pending_updates(RuntimeState& state, Lease& lease);
class RuntimeClientConnection final : public ClientConnection {
public:
    RuntimeClientConnection(std::weak_ptr<RuntimeState> state, std::weak_ptr<Lease> lease);
    static PlayerClient bind(std::shared_ptr<RuntimeState> state, std::shared_ptr<Lease> lease) {
        return issue<PlayerClient>(
            std::make_shared<RuntimeClientConnection>(std::move(state), std::move(lease)));
    }
    ClientIdentity identity() const override;
    query::LiveRecords<query::OwnUnit> own_units() const override;
    query::LiveRecords<query::OwnCity> own_cities() const override;
    std::vector<CommandDescriptor> commands() const override;
    CommandUpdate submit(CommandRequest request) const override;
    std::vector<CommandUpdate> updates() const override;
    std::optional<CommandUpdate> result(CommandId command) const override;
    InputReceipt reply(InteractionId interaction, std::int32_t choice) const override;
    InputReceipt cancel(CommandId command) const override;

private:
    std::shared_ptr<Lease> checked(const std::shared_ptr<RuntimeState>& state) const;
    std::weak_ptr<RuntimeState> state_;
    std::weak_ptr<Lease> lease_;
};
} // namespace openciv::session::detail
