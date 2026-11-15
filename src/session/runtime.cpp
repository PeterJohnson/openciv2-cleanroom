module openciv.session.runtime;

import std;
import openciv.query.live;
import :state;

namespace openciv::session {
namespace {
std::uint64_t assign(std::uint64_t& next) {
    if (next == std::numeric_limits<std::uint64_t>::max())
        throw std::overflow_error("Session identity space exhausted");
    return next++;
}
void require_active(const detail::RuntimeState& state) {
    if (!state.active)
        throw AccessError(AccessFailure::session_ended);
}
void admit(state::CivilizationSlot civilization) {
    if (static_cast<unsigned>(civilization) >= 8)
        throw std::invalid_argument("Participant civilization must be 0..7");
}
void revoke_participant(detail::RuntimeState& state, ParticipantId participant) {
    for (auto it = state.leases.begin(); it != state.leases.end();) {
        if (it->second->identity.participant == participant) {
            it->second->active = false;
            it = state.leases.erase(it);
        } else {
            ++it;
        }
    }
}
} // namespace
detail::RuntimeClientConnection::RuntimeClientConnection(std::weak_ptr<detail::RuntimeState> state,
                                                         std::weak_ptr<detail::Lease> lease)
    : state_(std::move(state)), lease_(std::move(lease)) {}
std::shared_ptr<detail::Lease>
detail::RuntimeClientConnection::checked(const std::shared_ptr<detail::RuntimeState>& state) const {
    if (!state || !state->active)
        throw AccessError(AccessFailure::session_ended);
    auto lease = lease_.lock();
    if (!lease || !lease->active || lease->identity.session != state->session)
        throw AccessError(AccessFailure::revoked);
    return lease;
}
ClientIdentity detail::RuntimeClientConnection::identity() const {
    const auto state = state_.lock();
    return checked(state)->identity;
}
query::LiveRecords<query::OwnUnit> detail::RuntimeClientConnection::own_units() const {
    const auto state = state_.lock();
    const auto lease = checked(state);
    return query::project_own_units(
        state->world->entities,
        {state->identity, state->session, state->revision, lease->identity.observer});
}
query::LiveRecords<query::OwnCity> detail::RuntimeClientConnection::own_cities() const {
    const auto state = state_.lock();
    const auto lease = checked(state);
    return query::project_own_cities(
        state->world->entities,
        {state->identity, state->session, state->revision, lease->identity.observer});
}
SessionRuntime::SessionRuntime(state::ProcessContext& process, RuntimeId identity) {
    if (identity == RuntimeId{0})
        throw std::invalid_argument("Runtime identity must be nonzero");
    state_ = std::make_shared<detail::RuntimeState>(process, identity);
}
SessionRuntime::~SessionRuntime() = default;
SessionActivity::SessionActivity(std::shared_ptr<detail::RuntimeState> state)
    : state_(std::move(state)) {
    ++state_->activities;
}
SessionActivity::~SessionActivity() {
    if (state_)
        --state_->activities;
}
SessionActivity SessionRuntime::begin_activity() {
    require_active(*state_);
    detail::require_quiescent(*state_);
    return SessionActivity{state_};
}
SessionActivity SessionRuntime::pin_world() {
    return SessionActivity{state_};
}
SessionId SessionRuntime::begin_client_session(Seeds seeds) {
    detail::require_quiescent(*state_);
    // Prepare contexts before revoking old access. A failed preparation has not
    // started a new lifetime; world setup/load effects are a separate operation.
    base::GameplayRandom gameplay(seeds.gameplay, "session");
    base::PresentationRandom presentation(seeds.presentation_content, "session");
    const auto session = SessionId{assign(state_->next_session)};
    end_client_session();
    state_->session = session;
    state_->gameplay = std::move(gameplay);
    state_->presentation = std::move(presentation);
    state_->revision = 0;
    state_->active = true;
    return session;
}
void SessionRuntime::end_client_session() {
    detail::require_quiescent(*state_);
    for (auto& [id, lease] : state_->leases)
        lease->active = false;
    state_->leases.clear();
    state_->participants.clear();
    state_->commands.clear();
    state_->ready.clear();
    state_->received.clear();
    state_->active = false;
}
void SessionRuntime::prepare_new_game() {
    detail::require_quiescent(*state_);
    // Allocate before revoking access. RuleState keeps its address so content
    // adapters can bind it once; none of its old contents are new-game inputs.
    auto world = std::make_unique<state::GameState>(state_->process);
    auto rules = std::make_unique<state::RuleState>();
    rules->text.initialize();
    for (auto& civilization : world->civilizations) {
        civilization.goals.fill({0, 0, -1, 0});
        civilization.standing_goals.fill({0, 0, -1, 0});
    }
    end_client_session();
    state_->requests.clear();
    state_->replies.clear();
    state_->world = std::move(world);
    *state_->process.rules = std::move(*rules);
    state_->process.text_parameters = {};
    state_->process.fallback = {};
    state_->revision = 0;
}
bool SessionRuntime::active() const noexcept {
    return state_->active;
}
ParticipantId SessionRuntime::add_participant(state::CivilizationSlot civilization,
                                              std::optional<ConnectionId> connection) {
    require_active(*state_);
    admit(civilization);
    const auto id = ParticipantId{assign(state_->next_participant)};
    state_->participants.emplace(id, Participant{id, civilization, connection});
    return id;
}
Participant SessionRuntime::participant(ParticipantId id) const {
    require_active(*state_);
    return state_->participants.at(id);
}
void SessionRuntime::reassign_participant(ParticipantId id, state::CivilizationSlot civilization,
                                          std::optional<ConnectionId> connection) {
    require_active(*state_);
    admit(civilization);
    auto& participant = state_->participants.at(id);
    revoke_participant(*state_, id);
    participant.civilization = civilization;
    participant.connection = connection;
}
void SessionRuntime::remove_participant(ParticipantId id) {
    require_active(*state_);
    (void)state_->participants.at(id);
    revoke_participant(*state_, id);
    state_->participants.erase(id);
}
void SessionRuntime::disconnect(ConnectionId connection) {
    require_active(*state_);
    for (auto& [id, participant] : state_->participants) {
        if (participant.connection == connection) {
            revoke_participant(*state_, id);
            participant.connection.reset();
        }
    }
}
PlayerClient SessionRuntime::bind(ParticipantId participant, ControllerKind kind,
                                  AccessClass access) {
    require_active(*state_);
    if (kind != ControllerKind::frontend && kind != ControllerKind::automation &&
        kind != ControllerKind::scripted && kind != ControllerKind::engine_policy)
        throw std::invalid_argument("Unknown controller kind");
    if (access != AccessClass::player && access != AccessClass::developer)
        throw std::invalid_argument("Unknown client access class");
    const auto observer = state_->participants.at(participant).civilization;
    const auto controller = ControllerId{assign(state_->next_controller)};
    auto lease = std::make_shared<detail::Lease>(detail::Lease{
        {state_->identity, state_->session, participant, controller, observer, kind, access},
        true,
        {},
        {}});
    detail::restore_pending_updates(*state_, *lease);
    state_->leases.emplace(controller, lease);
    return detail::RuntimeClientConnection::bind(state_, std::move(lease));
}
void SessionRuntime::revoke(ControllerId controller) {
    const auto found = state_->leases.find(controller);
    if (found == state_->leases.end())
        return;
    found->second->active = false;
    state_->leases.erase(found);
}
state::GameState& SessionRuntime::world() noexcept {
    return *state_->world;
}
const state::GameState& SessionRuntime::world() const noexcept {
    return *state_->world;
}
state::ProcessContext& SessionRuntime::process() noexcept {
    return state_->process;
}
void SessionRuntime::publish() {
    require_active(*state_);
    if (state_->revision == std::numeric_limits<std::uint64_t>::max())
        throw std::overflow_error("World observation revision exhausted");
    ++state_->revision;
}
base::GameplayRandom& SessionRuntime::gameplay_random() {
    require_active(*state_);
    return *state_->gameplay;
}
base::PresentationRandom& SessionRuntime::presentation_random() {
    require_active(*state_);
    return *state_->presentation;
}
} // namespace openciv::session
