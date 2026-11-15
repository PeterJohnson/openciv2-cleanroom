export module openciv.session.client_types;
import std;
export import openciv.session.command_types;
export import openciv.state.identities;
export namespace openciv::session {
using base::CommandId;
using base::ConnectionId;
using base::ControllerId;
using base::InteractionId;
using base::ParticipantId;
using base::RuntimeId;
using base::SessionId;
/// Identify the kind of controller attached to a participant seat.
enum class ControllerKind { frontend, automation, scripted, engine_policy };
/// Explain why a client lease can no longer access its session.
enum class AccessFailure { session_ended, revoked };
/// A client access failure caused by session expiry or binding revocation.
class AccessError : public std::runtime_error {
public:
    /// Build the diagnostic corresponding to a stale or revoked client lease.
    explicit AccessError(AccessFailure failure)
        : std::runtime_error(failure == AccessFailure::session_ended
                                 ? "Client session has ended"
                                 : "Client binding was revoked"),
          failure_(failure) {}
    /// Return the machine-readable access-failure category.
    AccessFailure failure() const noexcept { return failure_; }

private:
    AccessFailure failure_;
};
/// The session, controller, participant, and observer identities of a client lease.
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
} // namespace openciv::session
