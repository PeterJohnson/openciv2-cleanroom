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
enum class ControllerKind { frontend, automation, scripted, engine_policy };
enum class AccessFailure { session_ended, revoked };
class AccessError : public std::runtime_error {
public:
    explicit AccessError(AccessFailure failure)
        : std::runtime_error(failure == AccessFailure::session_ended
                                 ? "Client session has ended"
                                 : "Client binding was revoked"),
          failure_(failure) {}
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
} // namespace openciv::session
