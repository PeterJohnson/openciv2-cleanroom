export module openciv.session.commands;

import std;
export import openciv.session.command_types;
export import openciv.base.random;
export import openciv.state.game;

export namespace openciv::session {
/// The admitted actor and source authority for command execution.
struct CommandActor {
    base::ParticipantId participant{0};
    state::CivilizationSlot civilization{0};
    std::optional<base::ControllerId> controller;
    AccessClass access = AccessClass::player;
    std::optional<base::ConnectionId> source; // Present for an admitted peer request.
};
/// Inputs that may start, resume, answer, cancel, or expire an operation.
enum class OperationInputKind { begin, proceed, choice, cancel, timeout };
/// One validated continuation input with an optional choice and source actor.
struct OperationInput {
    OperationInputKind kind = OperationInputKind::begin;
    std::optional<std::int32_t> choice;
    std::optional<CommandActor> source =
        {}; // Captured validated reply/cancel provider; absent for engine inputs.
};
/// The next command status, result, or interaction produced by a continuation.
struct OperationStep {
    CommandStatus status = CommandStatus::completed;
    CommandValues result;
    std::string code;
    std::optional<InteractionSpec> interaction;
};
/// Engine-only services. An operation invokes callbacks at its specified
/// publication boundaries and rereads live world values afterwards. Clients
/// receive only owned updates, never this context or the continuation object.
struct OperationContext {
    state::GameState& world;
    state::ProcessContext& process;
    base::GameplayRandom& gameplay;
    base::PresentationRandom& presentation;
    CommandActor actor;
    CommandCorrelation correlation;
    std::function<void(std::string, CommandValues)> effect;
    std::function<void()> publish;
};
/// Owned resumable command state, advanced only at explicit runtime boundaries.
class Continuation {
public:
    virtual ~Continuation() = default;
    /// Advance one operation step using the supplied engine context and validated input.
    virtual OperationStep resume(OperationContext& context, OperationInput input) = 0;
};
using CheckpointBytes = std::vector<std::uint8_t>;
/// Trusted owner codec for semantic continuation locals, never captured closures.
/// Decode prepares a new continuation without executing its command prefix.
struct ContinuationCodec {
    std::string id;
    unsigned version = 1;
    std::function<CheckpointBytes(const Continuation&)> encode;
    std::function<std::unique_ptr<Continuation>(std::span<const std::uint8_t>,
                                                const CommandRequest&, const CommandUpdate&)>
        decode;
};
/// Choose local execution or a designated peer executor.
enum class CommandRoute { execute, await_peer };
/// An admission decision with an optional rejection or peer execution route.
struct Admission {
    std::optional<std::string> rejection;
    CommandRoute route = CommandRoute::execute;
    std::optional<base::ConnectionId> executor;
};
/// Trusted command schema, admission policy, execution factories, and optional checkpoint codec.
struct CommandDefinition {
    CommandDescriptor descriptor;
    /// Trusted mode/authority admission over a const world view. Rechecked leases prevent this
    /// callback from admitting a client it revoked while deciding the route.
    std::function<Admission(const CommandActor&, const CommandRequest&, const state::GameState&)>
        admit;
    /// Factories capture semantic inputs, not native stacks. The owning operation
    /// checks actor/busy/target state at execution and each required resume point.
    std::function<std::unique_ptr<Continuation>(const CommandRequest&)> execute;
    std::function<std::unique_ptr<Continuation>(const CommandRequest&, const CommandValues&)>
        apply_result;
    std::optional<ContinuationCodec> checkpoint;
};
/// Semantic adapter ports, not classic packets or a new wire protocol. Local
/// execution never passes through them. The mode adapter owns wire translation,
/// endpoint generations, send retries and which incoming messages use these ports.
struct RoutedRequest {
    base::ConnectionId destination{0};
    CommandCorrelation correlation;
    CommandRequest request;
};
/// A semantic peer result awaiting translation by the transport/mode adapter.
struct RoutedReply {
    base::ConnectionId destination{0};
    CommandCorrelation correlation;
    CommandStatus status = CommandStatus::completed;
    CommandValues result;
    std::string code;
};
} // namespace openciv::session
