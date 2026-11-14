#pragma once

#include "base/random.hpp"
#include "base/session_ids.hpp"
#include "query/live.hpp"
#include "session/commands.hpp"
#include "state/game.hpp"
#include <memory>

namespace openciv::session {
using base::CommandId;
using base::ConnectionId;
using base::ControllerId;
using base::InteractionId;
using base::ParticipantId;
using base::RuntimeId;
using base::SessionId;
enum class ControllerKind { frontend, automation, scripted, engine_policy };
enum class AccessFailure { session_ended, revoked };
class AccessError : public std::runtime_error {
public:
    explicit AccessError(AccessFailure failure);
    AccessFailure failure() const noexcept { return failure_; }

private:
    AccessFailure failure_;
};
struct ClientIdentity {
    RuntimeId runtime{0};
    SessionId session{0};
    ParticipantId participant{0};
    ControllerId controller{0};
    state::CivilizationSlot observer{0};
    ControllerKind kind = ControllerKind::frontend;
    AccessClass access = AccessClass::player;
    bool operator==(const ClientIdentity&) const = default;
};
struct Participant {
    ParticipantId id{0};
    state::CivilizationSlot civilization{0};
    std::optional<ConnectionId> connection;
    bool operator==(const Participant&) const = default;
};
struct Seeds {
    std::uint64_t gameplay = 0, presentation_content = 0;
};
namespace detail {
struct RuntimeState;
struct Lease;
} // namespace detail
// The only surface handed to clients. Copying shares the same lease; no public
// constructor can manufacture or alter authority. Owned query results may be
// retained, but a revoked client can obtain no subsequent observation.
class PlayerClient {
public:
    PlayerClient(const PlayerClient&) = default;
    PlayerClient& operator=(const PlayerClient&) = default;
    ClientIdentity identity() const;
    query::LiveRecords<query::OwnUnit> own_units() const;
    query::LiveRecords<query::OwnCity> own_cities() const;
    std::vector<CommandDescriptor> commands() const;
    CommandUpdate submit(CommandRequest request) const;
    std::vector<CommandUpdate> updates() const;
    std::optional<CommandUpdate> result(CommandId command) const;
    InputReceipt reply(InteractionId interaction, std::int32_t choice) const;
    InputReceipt cancel(CommandId command) const;

private:
    friend class SessionRuntime;
    PlayerClient(std::weak_ptr<detail::RuntimeState> state, std::weak_ptr<detail::Lease> lease);
    std::shared_ptr<detail::Lease>
    checked(const std::shared_ptr<detail::RuntimeState>& state) const;
    std::weak_ptr<detail::RuntimeState> state_;
    std::weak_ptr<detail::Lease> lease_;
};
// Trusted composition/engine owner, never given to player code. One cooperative
// mutation lane; not thread safe. Process state must outlive this runtime.
class SessionRuntime {
public:
    // The composition root supplies a stable, nonzero identity unique among
    // runtimes whose observations may be compared; never derive it from an address.
    SessionRuntime(state::ProcessContext& process, RuntimeId identity);
    ~SessionRuntime();
    SessionRuntime(const SessionRuntime&) = delete;
    SessionRuntime& operator=(const SessionRuntime&) = delete;

    // Client-session lifetimes, separate from F05 new-game/F06 load operations.
    // No world/process reset is implied. The owner performs specified setup/load
    // steps explicitly, then issues new participants and controller bindings.
    SessionId begin_client_session(Seeds seeds);
    void end_client_session();
    // X01 fresh-game isolation / F05 section 2. Before loading chosen content or
    // running setup, replace all world/workspace state and mutable rule/text
    // state. Requires completed/cancelled commands and drained transport outboxes;
    // revoke old clients and clear their queues. Preferences,
    // identity generators and explicitly supplied random contexts survive.
    // Invalidates engine references into the previous world and rule contents.
    // Load/resume and within-game civilization creation do not call this.
    void prepare_new_game();
    bool active() const noexcept;
    ParticipantId add_participant(state::CivilizationSlot civilization,
                                  std::optional<ConnectionId> connection = {});
    Participant participant(ParticipantId id) const;
    void reassign_participant(ParticipantId id, state::CivilizationSlot civilization,
                              std::optional<ConnectionId> connection = {});
    void remove_participant(ParticipantId id);
    // Revokes mapped input access while retaining the seats/world for their
    // owning mode's disconnect continuation. Does not perform N01 host transfer.
    void disconnect(ConnectionId connection);
    PlayerClient bind(ParticipantId participant, ControllerKind kind,
                      AccessClass access = AccessClass::player);
    void revoke(ControllerId controller);

    // Register trusted owning operations while inactive. No built-in fake game
    // commands are installed. Local work is an owned FIFO driven at explicit
    // engine boundaries; one call resumes at most one continuation.
    void register_command(CommandDefinition definition);
    bool advance_one();
    bool continue_command(CommandId command);
    InputReceipt
    cancel_command(CommandId command); // Owning lifecycle/mode input, no client lease required.
    std::size_t expire_interactions(std::uint64_t now);
    bool has_pending_commands() const noexcept;
    CommandUpdate receive_request(ConnectionId source, ParticipantId participant,
                                  CommandCorrelation correlation, CommandRequest request);
    InputReceipt receive_result(ConnectionId source, const RoutedReply& reply);
    // Taking an outbox transfers responsibility to the mode/transport adapter.
    std::vector<RoutedRequest> take_requests();
    std::vector<RoutedReply> take_replies();

    // Engine-only ownership ports. Call publish after each specified publication
    // and before exposing a query boundary; no automatic recomputation occurs.
    state::GameState& world() noexcept;
    const state::GameState& world() const noexcept;
    state::ProcessContext& process() noexcept;
    void publish();
    base::GameplayRandom& gameplay_random();
    base::PresentationRandom& presentation_random();

private:
    std::shared_ptr<detail::RuntimeState> state_;
};
} // namespace openciv::session
