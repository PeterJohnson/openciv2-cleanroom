using OpenCiv2.Base;

namespace OpenCiv2.Contracts;

public enum AccessClass { Player, Developer }
public readonly record struct UnitTarget(UnitSlot Slot, UnitToken Token);
public readonly record struct CityTarget(CitySlot Slot, CityToken Token);
public enum ValueKind { Integer, Boolean, Text, Civilization, Unit, City }

/// <summary>Closed immutable value alternatives; engine objects cannot enter command observations.</summary>
public abstract record CommandValue
{
    private protected CommandValue() { }

    public abstract ValueKind Kind { get; }

    public sealed record Integer(int Value) : CommandValue { public override ValueKind Kind => ValueKind.Integer; }

    public sealed record Boolean(bool Value) : CommandValue { public override ValueKind Kind => ValueKind.Boolean; }

    public sealed record Text(string Value) : CommandValue { public override ValueKind Kind => ValueKind.Text; }

    public sealed record Civilization(CivilizationSlot Value) : CommandValue
    {
        public override ValueKind Kind =>
        ValueKind.Civilization;
    }

    public sealed record Unit(UnitTarget Value) : CommandValue { public override ValueKind Kind => ValueKind.Unit; }

    public sealed record City(CityTarget Value) : CommandValue { public override ValueKind Kind => ValueKind.City; }
}

public sealed record NamedValue(string Name, CommandValue Value);
public sealed record ValueSchema(string Name, ValueKind Kind = ValueKind.Integer, bool Required = true);
public sealed record CommandDescriptor(uint Version, string Name, string Help, AccessClass Access,
    OwnedList<ValueSchema> Arguments, OwnedList<ValueSchema> Result)
{
    public void Validate()
    {
        if (Version != 1 || !NameValid(Name) || string.IsNullOrEmpty(Help) || !Enum.IsDefined(Access))
        {
            throw new ArgumentException("Invalid command descriptor.");
        }

        ValidateSchema(Arguments);
        ValidateSchema(Result);
    }

    private static bool NameValid(string name) => !string.IsNullOrEmpty(name) && name.All(c =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '_' or '-');

    private static void ValidateSchema(OwnedList<ValueSchema> schema)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in schema)
        {
            if (!NameValid(field.Name) || !names.Add(field.Name) || !Enum.IsDefined(field.Kind))
            {
                throw new ArgumentException("Invalid command value schema.");
            }
        }
    }

    public static string? ValidateValues(IReadOnlyList<ValueSchema> schema, IReadOnlyList<NamedValue> values)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!found.Add(value.Name))
            {
                return "duplicate_value:" + value.Name;
            }

            var field = schema.FirstOrDefault(s => s.Name == value.Name);
            if (field is null)
            {
                return "unknown_value:" + value.Name;
            }

            if (value.Value is null || value.Value.Kind != field.Kind)
            {
                return "wrong_type:" + value.Name;
            }

            if (value.Value is CommandValue.Civilization civ && civ.Value.Value >= EntityLimits.Civilizations)
            {
                return "invalid_civilization:" + value.Name;
            }

            if (value.Value is CommandValue.Unit unit && unit.Value.Slot.Value >= EntityLimits.Units)
            {
                return "invalid_unit_slot:" + value.Name;
            }

            if (value.Value is CommandValue.City city && city.Value.Slot.Value >= EntityLimits.Cities)
            {
                return "invalid_city_slot:" + value.Name;
            }
        }
        foreach (var field in schema)
        {
            if (field.Required && !found.Contains(field.Name))
            {
                return "missing_value:" + field.Name;
            }
        }

        return null;
    }

    public static CommandValue Value(IReadOnlyList<NamedValue> values, string name) =>
        values.FirstOrDefault(v => v.Name == name)?.Value ?? throw new KeyNotFoundException("Missing command value.");
}

public sealed record CommandRequest(string Name, OwnedList<NamedValue> Arguments);
public readonly record struct CommandCorrelation(RuntimeId Runtime, SessionId Session, CommandId Command);
public enum CommandStatus { Rejected, Pending, AwaitingInteraction, Completed, Cancelled, Failed }
public static class CommandStatuses
{
    public static bool Terminal(this CommandStatus status) => status is CommandStatus.Rejected or
        CommandStatus.Completed or CommandStatus.Cancelled or CommandStatus.Failed;
}
public sealed record Effect(ulong Index, string Name, OwnedList<NamedValue> Values);
public sealed record InteractionSpec(string Prompt, ParticipantId? Recipient, CommandValue? Target,
    OwnedList<int> Choices, ulong? Deadline = null, int? DefaultChoice = null, bool Cancellable = true);
public sealed record Interaction(
    InteractionId Id,
    ParticipantId Recipient,
    CivilizationSlot Observer,
    InteractionSpec Spec);
public sealed record CommandUpdate(CommandCorrelation Correlation, CommandStatus Status, string Code,
    OwnedList<NamedValue> Result, Interaction? Interaction, OwnedList<Effect> Committed);
public sealed record InputReceipt(bool Accepted, string Code);
