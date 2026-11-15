export module openciv.session.command_types;

import std;
export import openciv.base.session_ids;
export import openciv.state.identities;

export extern "C++" {
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
    using CommandValue = std::variant<std::int32_t, bool, std::string, state::CivilizationSlot,
                                      UnitTarget, CityTarget>;
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
    enum class CommandStatus {
        rejected,
        pending,
        awaiting_interaction,
        completed,
        cancelled,
        failed
    };
    bool terminal(CommandStatus status) noexcept;
    struct Effect {
        std::uint64_t index =
            0; // One-based within this command; stable across notifications and snapshots.
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
    } // namespace openciv::session
}
