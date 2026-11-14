#pragma once

#include "session/command_types.hpp"
#include "query/live_records.hpp"
#include <memory>
#include <stdexcept>

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
    // Ordered notifications contain only effects not previously delivered to
    // this lease. result() remains a cumulative snapshot until owner retirement.
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
} // namespace openciv::session
