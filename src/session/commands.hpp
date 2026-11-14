#pragma once

#include "base/random.hpp"
#include "base/session_ids.hpp"
#include "state/game.hpp"
#include <compare>
#include <functional>
#include <memory>
#include <string>
#include <variant>

namespace openciv::session {
enum class AccessClass { player, developer };
struct UnitTarget {
    state::UnitSlot slot{0};
    state::UnitToken token{0};
    bool operator==(const UnitTarget&) const = default;
};
struct CityTarget {
    state::CitySlot slot{0};
    state::CityToken token{0};
    bool operator==(const CityTarget&) const = default;
};
using CommandValue =
    std::variant<std::int32_t, bool, std::string, state::CivilizationSlot, UnitTarget, CityTarget>;
struct NamedValue {
    std::string name;
    CommandValue value;
    bool operator==(const NamedValue&) const = default;
};
using CommandValues = std::vector<NamedValue>;
enum class ValueKind { integer, boolean, text, civilization, unit, city };
struct ValueSchema {
    std::string name;
    ValueKind kind = ValueKind::integer;
    bool required = true;
    bool operator==(const ValueSchema&) const = default;
};
struct CommandDescriptor {
    unsigned version = 1;
    std::string name, help;
    AccessClass access = AccessClass::player;
    std::vector<ValueSchema> arguments, result;
    bool operator==(const CommandDescriptor&) const = default;
};
// Architecture §5: structural validation, not availability or execution checks.
void validate_descriptor(const CommandDescriptor& descriptor);
std::optional<std::string> validate_values(std::span<const ValueSchema> schema,
                                           std::span<const NamedValue> values);
const CommandValue& command_value(std::span<const NamedValue> values, std::string_view name);
struct CommandRequest {
    std::string name;
    CommandValues arguments;
    bool operator==(const CommandRequest&) const = default;
};
struct CommandCorrelation {
    base::RuntimeId runtime{0};
    base::SessionId session{0};
    base::CommandId command{0};
    auto operator<=>(const CommandCorrelation&) const = default;
};
struct CommandActor {
    base::ParticipantId participant{0};
    state::CivilizationSlot civilization{0};
    std::optional<base::ControllerId> controller;
    AccessClass access = AccessClass::player;
    std::optional<base::ConnectionId> source; // Present for an admitted peer request.
};
enum class CommandStatus { rejected, pending, awaiting_interaction, completed, cancelled, failed };
bool terminal(CommandStatus status) noexcept;
struct Effect {
    std::uint64_t index = 0; // One-based within this command; cumulative update lists reuse IDs.
    std::string name;
    CommandValues values;
    bool operator==(const Effect&) const = default;
};
struct InteractionSpec {
    std::string prompt;
    std::optional<base::ParticipantId> recipient; // Absent selects the admitted actor.
    std::optional<CommandValue> target;
    std::vector<std::int32_t> choices;
    std::optional<std::uint64_t> deadline;
    std::optional<std::int32_t> default_choice;
    bool cancellable = true;
    bool operator==(const InteractionSpec&) const = default;
};
struct Interaction {
    base::InteractionId id{0};
    base::ParticipantId recipient{0};
    state::CivilizationSlot observer{0};
    InteractionSpec spec;
    bool operator==(const Interaction&) const = default;
};
struct CommandUpdate {
    CommandCorrelation correlation;
    CommandStatus status = CommandStatus::rejected;
    std::string code;
    CommandValues result;
    std::optional<Interaction> interaction;
    std::vector<Effect> committed;
    bool operator==(const CommandUpdate&) const = default;
};
struct InputReceipt {
    bool accepted = false;
    std::string code;
};
enum class OperationInputKind { begin, proceed, choice, cancel, timeout };
struct OperationInput {
    OperationInputKind kind = OperationInputKind::begin;
    std::optional<std::int32_t> choice;
    std::optional<CommandActor> source =
        {}; // Captured validated reply/cancel provider; absent for engine inputs.
};
struct OperationStep {
    CommandStatus status = CommandStatus::completed;
    CommandValues result;
    std::string code;
    std::optional<InteractionSpec> interaction;
};
// Engine-only services. An operation invokes callbacks at its specified
// publication boundaries and rereads live world values afterwards. Clients
// receive only owned updates, never this context or the continuation object.
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
class Continuation {
public:
    virtual ~Continuation() = default;
    virtual OperationStep resume(OperationContext& context, OperationInput input) = 0;
};
enum class CommandRoute { execute, await_peer };
struct Admission {
    std::optional<std::string> rejection;
    CommandRoute route = CommandRoute::execute;
    std::optional<base::ConnectionId> executor;
};
struct CommandDefinition {
    CommandDescriptor descriptor;
    // Trusted mode/authority admission over a const world view. Rechecked leases prevent this
    // callback from admitting a client it revoked while deciding the route.
    std::function<Admission(const CommandActor&, const CommandRequest&, const state::GameState&)>
        admit;
    // Factories capture semantic inputs, not native stacks. The owning operation
    // checks actor/busy/target state at execution and each required resume point.
    std::function<std::unique_ptr<Continuation>(const CommandRequest&)> execute;
    std::function<std::unique_ptr<Continuation>(const CommandRequest&, const CommandValues&)>
        apply_result;
};
// Semantic adapter ports, not classic packets or a new wire protocol. Local
// execution never passes through them. The mode adapter owns wire translation,
// endpoint generations, send retries and which incoming messages use these ports.
struct RoutedRequest {
    base::ConnectionId destination{0};
    CommandCorrelation correlation;
    CommandRequest request;
};
struct RoutedReply {
    base::ConnectionId destination{0};
    CommandCorrelation correlation;
    CommandStatus status = CommandStatus::completed;
    CommandValues result;
    std::string code;
};
} // namespace openciv::session
