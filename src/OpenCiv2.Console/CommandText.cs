using System.Globalization;
using System.Text;
using OpenCiv2.Base;
using OpenCiv2.Contracts;

namespace OpenCiv2.Console;

/// <summary>Bounded descriptor-driven command input, independent of a GUI or Lua.
/// Grammar: command name=value ...; quoted tokens support escaped quote and backslash.</summary>
public static class CommandText
{
    /// <summary>
    /// Parses bounded console input against the commands visible to the current client.
    /// </summary>
    /// <remarks>
    /// Descriptor validation checks required, duplicate and typed arguments. Parsing creates a request;
    /// authority and live target validation remain the runtime's responsibility.
    /// </remarks>
    /// <exception cref="ArgumentException">The text, descriptor or supplied values are outside the command
    /// grammar.</exception>
    public static CommandRequest Parse(string line, IReadOnlyList<CommandDescriptor> descriptors)
    {
        var tokens = Tokenize(line);
        if (tokens.Count == 0)
        {
            throw new ArgumentException("Enter a command.");
        }

        var descriptor = descriptors.FirstOrDefault(d => d.Name == tokens[0])
            ?? throw new ArgumentException("Command is unavailable.");
        descriptor.Validate();
        var values = new List<NamedValue>();
        foreach (string token in tokens.Skip(1))
        {
            int split = token.IndexOf('=');
            if (split <= 0)
            {
                throw new ArgumentException("Arguments use name=value.");
            }

            string name = token[..split], text = token[(split + 1)..];
            var field = descriptor.Arguments.FirstOrDefault(a => a.Name == name)
                ?? throw new ArgumentException("Unknown argument: " + name);
            values.Add(new(name, ParseValue(field.Kind, text)));
        }
        if (CommandDescriptor.ValidateValues(descriptor.Arguments, values) is { } error)
        {
            throw new ArgumentException(error);
        }

        return new(descriptor.Name, new(values));
    }

    /// <summary>Completes descriptor names, argument names and already disclosed owned targets.</summary>
    public static OwnedList<string> Complete(string line, IReadOnlyList<CommandDescriptor> descriptors,
        LiveRecords<OwnUnit>? units, LiveRecords<OwnCity>? cities)
    {
        int firstSpace = line.IndexOf(' ');
        if (firstSpace < 0)
        {
            return new(descriptors.Select(d => d.Name).Where(n => n.StartsWith(line, StringComparison.Ordinal)));
        }

        var descriptor = descriptors.FirstOrDefault(d => d.Name == line[..firstSpace]);
        if (descriptor is null || line.Contains('"'))
        {
            return OwnedList<string>.Empty;
        }

        int argumentStart = line.LastIndexOf(' ') + 1;
        string prefix = line[..argumentStart], tail = line[argumentStart..];
        int equals = tail.IndexOf('=');
        IEnumerable<string> candidates;
        if (equals < 0)
        {
            candidates = descriptor.Arguments.Select(a => a.Name + "=");
        }
        else
        {
            string argument = tail[..equals];
            candidates = descriptor.Arguments.FirstOrDefault(a => a.Name == argument)?.Kind switch
            {
                ValueKind.Unit => units?.Records.Select(u => FormattableString.Invariant(
                    $"{argument}={u.Slot.Value}:{u.Token.Value}")) ?? [],
                ValueKind.City => cities?.Records.Select(c => FormattableString.Invariant(
                    $"{argument}={c.Slot.Value}:{c.Token.Value}")) ?? [],
                _ => []
            };
        }

        return new(candidates.Where(c => c.StartsWith(tail, StringComparison.Ordinal)).Select(c => prefix + c));
    }

    /// <summary>Parses a reply to the currently disclosed interaction. The client owns input admission.</summary>
    public static int? Decision(string line)
    {
        var tokens = Tokenize(line);
        if (tokens.Count == 1 && tokens[0] == "cancel")
        {
            return null;
        }

        if (tokens.Count != 2 || tokens[0] != "reply")
        {
            throw new ArgumentException("Use reply <choice> or cancel.");
        }

        return Integer(tokens[1]);
    }

    /// <summary>
    /// Formats a descriptor's argument names, value kinds, optional markers and help text for the console.
    /// </summary>
    public static string Help(CommandDescriptor descriptor) =>
        descriptor.Name + " " + string.Join(" ", descriptor.Arguments.Select(a =>
        $"{a.Name}=<{a.Kind.ToString().ToLowerInvariant()}>{(a.Required ? "" : "?")}")) + " — " + descriptor.Help;

    /// <summary>Formats a controller-scoped result and its ordered committed effects.</summary>
    public static string Result(CommandUpdate update)
    {
        var fields = new List<string> { update.Status.ToString() };
        if (update.Code.Length != 0)
        {
            fields.Add(update.Code);
        }

        fields.AddRange(update.Result.Select(value => $"{value.Name}={FormatValue(value.Value)}"));
        fields.AddRange(update.Committed.Select(effect => $"#{effect.Index} {effect.Name}"));
        return string.Join(" · ", fields);
    }

    private static string FormatValue(CommandValue value) => value switch
    {
        CommandValue.Integer integer => integer.Value.ToString(CultureInfo.InvariantCulture),
        CommandValue.Boolean boolean => boolean.Value ? "true" : "false",
        CommandValue.Text text => text.Value,
        CommandValue.Civilization civilization => civilization.Value.Value.ToString(CultureInfo.InvariantCulture),
        CommandValue.Unit unit => FormattableString.Invariant($"{unit.Value.Slot.Value}:{unit.Value.Token.Value}"),
        CommandValue.City city => FormattableString.Invariant($"{city.Value.Slot.Value}:{city.Value.Token.Value}"),
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    /// <summary>
    /// Splits bounded input on unquoted whitespace and handles only escaped quote/backslash inside quoted text.
    /// </summary>
    private static List<string> Tokenize(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Length > 4096)
        {
            throw new ArgumentException("Command exceeds 4096 characters.");
        }

        var tokens = new List<string>();
        var current = new StringBuilder();
        bool quoted = false, started = false;
        for (int i = 0; i < line.Length; ++i)
        {
            char c = line[i];
            if (c == '"')
            {
                quoted = !quoted;
                started = true;
            }
            else if (c == '\\' && quoted)
            {
                if (++i == line.Length || line[i] is not ('"' or '\\'))
                {
                    throw new ArgumentException("Invalid quoted escape.");
                }

                current.Append(line[i]);
            }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (started)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    started = false;
                }
            }
            else
            {
                current.Append(c);
                started = true;
            }
            if (tokens.Count > 64)
            {
                throw new ArgumentException("Too many command tokens.");
            }
        }
        if (quoted)
        {
            throw new ArgumentException("Unclosed quoted argument.");
        }

        if (started)
        {
            tokens.Add(current.ToString());
        }

        if (tokens.Count > 64)
        {
            throw new ArgumentException("Too many command tokens.");
        }

        return tokens;
    }

    /// <summary>
    /// Requires invariant canonical integer text so alternate spellings such as leading zeros are rejected.
    /// </summary>
    private static int Integer(string text)
    {
        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value)
            || value.ToString(CultureInfo.InvariantCulture) != text)
        {
            throw new ArgumentException("Expected a canonical signed 32-bit integer.");
        }

        return value;
    }

    /// <summary>
    /// Requires invariant canonical unsigned text for slot indices and lifetime tokens.
    /// </summary>
    private static uint Unsigned(string text)
    {
        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint value)
            || value.ToString(CultureInfo.InvariantCulture) != text)
        {
            throw new ArgumentException("Expected a canonical unsigned 32-bit integer.");
        }

        return value;
    }

    /// <summary>
    /// Converts one token to its descriptor value kind, checking only the declared scalar and target domains.
    /// </summary>
    private static CommandValue ParseValue(ValueKind kind, string text)
    {
        if (kind == ValueKind.Text)
        {
            return new CommandValue.Text(text);
        }

        if (kind == ValueKind.Integer)
        {
            return new CommandValue.Integer(Integer(text));
        }

        if (kind == ValueKind.Boolean)
        {
            return text switch
            {
                "true" => new CommandValue.Boolean(
                    true),
                "false" => new CommandValue.Boolean(
                    false),
                _ => throw new ArgumentException(
                    "Expected true or false.")
            };
        }

        if (kind == ValueKind.Civilization)
        {
            uint id = Unsigned(text);
            if (id >= EntityLimits.Civilizations)
            {
                throw new ArgumentException("Civilization is outside 0..7.");
            }

            return new CommandValue.Civilization(new((byte)id));
        }
        string[] parts = text.Split(':');
        if (parts.Length != 2)
        {
            throw new ArgumentException("Targets use slot:token.");
        }

        uint slot = Unsigned(parts[0]), token = Unsigned(parts[1]);
        if (token == 0 || slot >= (kind == ValueKind.Unit ? EntityLimits.Units : EntityLimits.Cities))
        {
            throw new ArgumentException("Target is outside the live identity domain.");
        }

        return kind switch
        {
            ValueKind.Unit => new CommandValue.Unit(new(new((ushort)slot), new(token))),
            ValueKind.City => new CommandValue.City(new(new((ushort)slot), new(token))),
            _ => throw new ArgumentException("Unsupported value kind.")
        };
    }
}
