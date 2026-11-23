using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.Formats;

/// <summary>M1-01 strict, bounded, versioned local checkpoint representation. Integrity hashes detect
/// damage, not authorship. Admission to an owner profile and continuation codec belongs to Session.</summary>
public static class LocalCheckpointCodec
{
    public const string Format = "openciv2-local-checkpoint/2";
    public const int MaximumPayload = 4 * 1024 * 1024,
        MaximumOwnerBytes = 512 * 1024,
        MaximumText = 4096,
        MaximumItems = 4096;

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ArgumentException(message);
        }
    }

    internal static string Number<T>(T value) where T : IFormattable =>
        value.ToString(null, CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads a canonical decimal string, preserving full integer precision across JSON consumers.
    /// </summary>
    internal static T Number<T>(ContentReader reader) where T : struct, System.Numerics.IBinaryInteger<T>
    {
        string text = reader.Text();
        if (!T.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            || Number(value) != text)
        {
            reader.Error("expected canonical decimal string in range");
        }

        return value;
    }

    internal static ulong Id(ContentReader reader)
    {
        ulong value = Number<ulong>(reader);
        if (value == 0)
        {
            reader.Error("zero identity");
        }

        return value;
    }

    internal static bool Boolean(ContentReader reader)
    {
        if (reader.Value?.GetValueKind() is not (JsonValueKind.True or JsonValueKind.False))
        {
            reader.Error("expected boolean");
        }

        return reader.Value!.GetValue<bool>();
    }

    internal static int Count(ContentReader reader)
    {
        int count = reader.ArraySize();
        if (count > MaximumItems)
        {
            reader.Error("checkpoint array limit exceeded");
        }

        return count;
    }

    private static int Digit(char c) => c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'f' ? c - 'a' + 10 : -1;

    private static string Hex(ContentReader reader, int digits, int limit)
    {
        string text = reader.Text(false);
        if (text.Length % digits != 0 || text.Length / digits > limit || text.Any(c => Digit(c) < 0))
        {
            reader.Error("invalid lowercase hexadecimal");
        }

        return text;
    }

    internal static OwnedList<byte> Blob(ContentReader reader) =>
        new(Convert.FromHexString(Hex(reader, 2, MaximumOwnerBytes)));

    /// <summary>
    /// Decodes bounded hexadecimal UTF-16 code units without normalizing or repairing the original text.
    /// </summary>
    internal static string Text(ContentReader reader)
    {
        string text = Hex(reader, 4, MaximumText);
        var chars = new char[text.Length / 4];
        for (int i = 0; i < chars.Length; ++i)
        {
            int value = 0;
            for (int j = 0; j < 4; ++j)
            {
                value = value * 16 + Digit(text[i * 4 + j]);
            }

            chars[i] = (char)value;
        }
        return new(chars);
    }

    /// <summary>
    /// Conservatively accounts for text, blobs and collections before building an oversized JSON payload.
    /// </summary>
    internal sealed class Budget
    {
        private int remaining = MaximumPayload;

        public void Add(int count)
        {
            Require(count <= remaining, "Checkpoint exceeds encoding budget");
            remaining -= count;
        }

        public string Text(string value)
        {
            Require(value is not null && value.Length <= MaximumText, "Checkpoint text exceeds limit");
            Add(32 + value!.Length * 4);
            return string.Concat(value.Select(c => ((ushort)c).ToString("x4", CultureInfo.InvariantCulture)));
        }

        public string Blob(OwnedList<byte> value)
        {
            Require(value.Count <= MaximumOwnerBytes, "Checkpoint owner payload too large");
            Add(32 + 2 * value.Count);
            return Convert.ToHexStringLower(value.ToArray());
        }

        public void Items(int count)
        {
            Require(count <= MaximumItems, "Too many checkpoint array elements");
            Add(32 + count * 64);
        }
    }

    private static JsonObject Value(CommandValue value, Budget budget)
    {
        budget.Add(128);
        return value switch
        {
            CommandValue.Integer v => new() { ["kind"] = "i32", ["value"] = Number(v.Value) },
            CommandValue.Boolean v => new() { ["kind"] = "bool", ["value"] = v.Value },
            CommandValue.Text v => new() { ["kind"] = "text", ["value"] = budget.Text(v.Value) },
            CommandValue.Civilization v => new() { ["kind"] = "civilization", ["value"] = Number(v.Value.Value) },
            CommandValue.Unit v => new()
            {
                ["kind"] = "unit",
                ["slot"] = Number(v.Value.Slot.Value),
                ["token"] = Number(v.Value.Token.Value)
            },
            CommandValue.City v => new()
            {
                ["kind"] = "city",
                ["slot"] = Number(v.Value.Slot.Value),
                ["token"] = Number(v.Value.Token.Value)
            },
            _ => throw new ArgumentException("Unknown checkpoint value")
        };
    }

    private static CommandValue Value(ContentReader reader, bool validate = true)
    {
        string kind = reader.At("kind").Text();
        if (kind is "unit" or "city")
        {
            reader.Fields(["kind", "slot", "token"]);
            ushort slot = Number<ushort>(reader.At("slot"));
            uint token = Number<uint>(reader.At("token"));
            return kind == "unit"
                ? new CommandValue.Unit(new(new(slot), new(token)))
                : new CommandValue.City(new(new(slot), new(token)));
        }
        reader.Fields(["kind", "value"]);
        var v = reader.At("value");
        switch (kind)
        {
            case "i32":
                return new CommandValue.Integer(Number<int>(v));
            case "bool":
                return new CommandValue.Boolean(Boolean(v));
            case "text":
                return new CommandValue.Text(Text(v));
            case "civilization":
                byte slot = Number<byte>(v);
                if (validate && slot >= EntityLimits.Civilizations)
                {
                    v.Error("invalid civilization");
                }

                return new CommandValue.Civilization(new(slot));
            default:
                throw new ContentException("invalid-content", reader.Path, "unknown checkpoint value kind");
        }
    }

    internal static JsonArray Values(OwnedList<NamedValue> values, Budget budget)
    {
        budget.Items(values.Count);
        var result = new JsonArray();
        foreach (var v in values)
        {
            result.Add((JsonNode)new JsonObject { ["name"] = budget.Text(v.Name), ["value"] = Value(v.Value, budget) });
        }

        return result;
    }

    internal static OwnedList<NamedValue> Values(ContentReader reader, bool validate = true)
    {
        var values = new NamedValue[Count(reader)];
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < values.Length; ++i)
        {
            var row = reader.Element(i);
            row.Fields(["name", "value"]);
            string name = Text(row.At("name"));
            if (validate && (name.Length == 0 || !names.Add(name)))
            {
                row.Error("empty or duplicate named value");
            }

            values[i] = new(name, Value(row.At("value"), validate));
        }
        return new(values);
    }

    private static JsonObject Interaction(Interaction interaction, Budget budget)
    {
        var spec = interaction.Spec;
        budget.Add(1024);
        budget.Items(spec.Choices.Count);
        return new()
        {
            ["id"] = Number(interaction.Id.Value),
            ["recipient"] = Number(interaction.Recipient.Value),
            ["observer"] = Number(interaction.Observer.Value),
            ["prompt"] = budget.Text(spec.Prompt),
            ["specifiedRecipient"] = spec.Recipient is { } recipient ? Number(recipient.Value) : null,
            ["target"] = spec.Target is { } target ? Value(target, budget) : null,
            ["deadline"] = spec.Deadline is { } deadline ? Number(deadline) : null,
            ["default"] = spec.DefaultChoice is { } choice ? Number(choice) : null,
            ["cancellable"] = spec.Cancellable,
            ["choices"] = new JsonArray(spec.Choices.Select(c => (JsonNode?)JsonValue.Create(Number(c))).ToArray())
        };
    }

    private static Interaction Interaction(ContentReader reader, ParticipantId participant, ulong nextInteraction)
    {
        reader.Fields(
            [
                "id",
                "recipient",
                "observer",
                "prompt",
                "specifiedRecipient",
                "target",
                "deadline",
                "default",
                "cancellable",
                "choices"
            ]);
        var id = new InteractionId(Id(reader.At("id")));
        var recipient = new ParticipantId(Id(reader.At("recipient")));
        Require(
            id.Value < nextInteraction && recipient == participant && Number<byte>(reader.At("observer")) == 1,
            "Invalid checkpoint interaction identity");
        ParticipantId? specified = reader.At("specifiedRecipient").Value is null
            ? null
            : new(Id(reader.At("specifiedRecipient")));
        Require(specified is null || specified == participant, "Unsupported checkpoint recipient");
        var target = reader.At("target").Value is null ? null : Value(reader.At("target"));
        ulong? deadline = reader.At("deadline").Value is null ? null : Number<ulong>(reader.At("deadline"));
        int? defaultChoice = reader.At("default").Value is null ? null : Number<int>(reader.At("default"));
        var choices = reader.At("choices");
        var values = new int[Count(choices)];
        var unique = new HashSet<int>();
        for (int i = 0; i < values.Length; ++i)
        {
            values[i] = Number<int>(choices.Element(i));
            Require(unique.Add(values[i]), "Duplicate checkpoint choice");
        }
        string prompt = Text(reader.At("prompt"));
        Require(
            prompt.Length != 0 && values.Length != 0
                && (defaultChoice is null || (deadline is not null && unique.Contains(defaultChoice.Value))),
            "Invalid checkpoint interaction");
        return new(
            id,
            recipient,
            new(1),
            new(prompt, specified, target, new(values), deadline, defaultChoice, Boolean(reader.At("cancellable"))));
    }

    private static JsonObject Command(CheckpointCommand command, Budget budget)
    {
        var update = command.Update;
        Require(
            update.Status == CommandStatus.AwaitingInteraction && update.Interaction is not null,
            "Checkpoint requires a suspended interaction");
        Require(command.Access is AccessClass.Player or AccessClass.Developer, "Invalid checkpoint access");
        budget.Add(2048);
        budget.Items(update.Committed.Count);
        var effects = new JsonArray();
        foreach (var effect in update.Committed)
        {
            effects.Add(
                (JsonNode)new JsonObject
                {
                    ["index"] = Number(effect.Index),
                    ["name"] = budget.Text(effect.Name),
                    ["values"] = Values(effect.Values, budget)
                });
        }

        return new()
        {
            ["id"] = Number(update.Correlation.Command.Value),
            ["name"] = budget.Text(command.Request.Name),
            ["descriptorVersion"] = Number(command.DescriptorVersion),
            ["arguments"] = Values(command.Request.Arguments, budget),
            ["controller"] = command.Controller is { } controller ? Number(controller.Value) : null,
            ["access"] = command.Access == AccessClass.Player ? "player" : "developer",
            ["origin"] = Number(update.Correlation.Runtime.Value),
            ["session"] = Number(update.Correlation.Session.Value),
            ["code"] = budget.Text(update.Code),
            ["result"] = Values(update.Result, budget),
            ["interaction"] = Interaction(update.Interaction!, budget),
            ["effects"] = effects,
            ["codec"] = budget.Text(command.Codec),
            ["codecVersion"] = Number(command.CodecVersion),
            ["locals"] = budget.Blob(command.Locals)
        };
    }

    private static CheckpointCommand Command(
        ContentReader reader,
        SessionId session,
        ParticipantId participant,
        CheckpointCounters counters)
    {
        reader.Fields(
            [
                "id",
                "name",
                "descriptorVersion",
                "arguments",
                "controller",
                "access",
                "origin",
                "session",
                "code",
                "result",
                "interaction",
                "effects",
                "codec",
                "codecVersion",
                "locals"
            ]);
        var id = new CommandId(Id(reader.At("id")));
        Require(id.Value < counters.Command, "Invalid checkpoint command counter");
        string name = Text(reader.At("name")), codec = Text(reader.At("codec"));
        uint version = Number<uint>(
            reader.At("descriptorVersion")), codecVersion = Number<uint>(
                reader.At("codecVersion"));
        Require(
            name.Length != 0 && codec.Length != 0 && version != 0 && codecVersion != 0,
            "Checkpoint requires named versioned operation and codec");
        ControllerId? controller = reader.At("controller").Value is null ? null : new(Id(reader.At("controller")));
        Require(controller is null || controller.Value.Value < counters.Controller, "Invalid actor controller");
        string access = reader.At("access").Text();
        Require(access is "player" or "developer", "Invalid checkpoint access");
        var correlation = new CommandCorrelation(new(Id(reader.At("origin"))), new(Id(reader.At("session"))), id);
        Require(correlation.Session == session, "Checkpoint correlation session mismatch");
        var effects = reader.At("effects");
        var committed = new Effect[Count(effects)];
        for (int i = 0; i < committed.Length; ++i)
        {
            var row = effects.Element(i);
            row.Fields(["index", "name", "values"]);
            ulong index = Number<ulong>(row.At("index"));
            string effectName = Text(row.At("name"));
            Require(index == (ulong)i + 1 && effectName.Length != 0, "Invalid checkpoint effect sequence/name");
            committed[i] = new(index, effectName, Values(row.At("values")));
        }
        var update = new CommandUpdate(
            correlation,
            CommandStatus.AwaitingInteraction,
            Text(reader.At("code")),
            Values(reader.At("result")),
            Interaction(reader.At("interaction"), participant, counters.Interaction),
            new(committed));
        return new(
            new(name, Values(reader.At("arguments"))),
            version,
            controller,
            access == "player" ? AccessClass.Player : AccessClass.Developer,
            update,
            codec,
            codecVersion,
            Blob(reader.At("locals")));
    }

    /// <summary>
    /// Serializes and validates a bounded checkpoint payload before enclosing its exact bytes with an integrity
    /// hash.
    /// </summary>
    /// <remarks>
    /// The shared payload reader applies the same representation checks to encoded and decoded data. Session
    /// separately admits the profile and continuation codec.
    /// </remarks>
    public static byte[] Encode(
        LocalCheckpoint checkpoint,
        ulong replayLimit = RandomCheckpointCodec.DefaultReplayLimit)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var budget = new Budget();
        budget.Add(2048);
        var c = checkpoint.Counters;
        string gameplay = Encoding.UTF8.GetString(RandomCheckpointCodec.Encode(checkpoint.Gameplay, replayLimit));
        budget.Add(2 * gameplay.Length);
        string presentation = Encoding.UTF8.GetString(
            RandomCheckpointCodec.Encode(checkpoint.Presentation, replayLimit));
        budget.Add(2 * presentation.Length);
        var root = new JsonObject
        {
            ["profile"] = budget.Text(checkpoint.Profile),
            ["content"] = checkpoint.ContentHash,
            ["origin"] = Number(checkpoint.Origin.Value),
            ["session"] = Number(checkpoint.Session.Value),
            ["participant"] = Number(checkpoint.Participant.Value),
            ["counters"] = new JsonObject
            {
                ["session"] = Number(c.Session),
                ["participant"] = Number(c.Participant),
                ["controller"] = Number(c.Controller),
                ["command"] = Number(c.Command),
                ["interaction"] = Number(c.Interaction),
                ["revision"] = Number(c.Revision)
            },
            ["world"] = budget.Blob(checkpoint.World),
            ["gameplay"] = gameplay,
            ["presentation"] = presentation,
            ["command"] = checkpoint.Command is { } command ? Command(command, budget) : null
        };
        // One reader defines semantic admission in both directions. Nothing is published until it succeeds.
        _ = Payload(new(root, "local-checkpoint/payload"), replayLimit);
        byte[] payload = ContentJson.CanonicalBytes(root);
        Require(payload.Length <= MaximumPayload, "Checkpoint payload exceeds limit");
        return ContentJson.CanonicalBytes(
            new JsonObject
            {
                ["format"] = Format,
                ["payload"] = Encoding.UTF8.GetString(payload),
                ["sha256"] = ContentIdentity.Sha256(payload)
            });
    }

    /// <summary>
    /// Validates the checkpoint structure, identity counters, suspended command and independent random domains.
    /// </summary>
    private static LocalCheckpoint Payload(ContentReader root, ulong replayLimit)
    {
        root.Fields(
            [
                "profile",
                "content",
                "origin",
                "session",
                "participant",
                "counters",
                "world",
                "gameplay",
                "presentation",
                "command"
            ]);
        string profile = Text(root.At("profile"));
        Require(profile.Length is > 0 and <= 128, "Invalid checkpoint profile identity");
        string hash = root.At("content").Sha256();
        var origin = new RuntimeId(Id(root.At("origin")));
        var session = new SessionId(Id(root.At("session")));
        var participant = new ParticipantId(Id(root.At("participant")));
        var c = root.At("counters");
        c.Fields(["session", "participant", "controller", "command", "interaction", "revision"]);
        var counters = new CheckpointCounters(
            Number<ulong>(c.At("session")),
            Number<ulong>(c.At("participant")),
            Id(c.At("controller")),
            Id(c.At("command")),
            Id(c.At("interaction")),
            Number<ulong>(c.At("revision")));
        Require(
            counters.Session > session.Value && counters.Participant > participant.Value,
            "Invalid checkpoint identity counters");
        var world = Blob(root.At("world"));
        var command = root.At("command").Value is null
            ? null
            : Command(root.At("command"), session, participant, counters);
        var gameplay = RandomCheckpointCodec.Decode(Encoding.UTF8.GetBytes(root.At("gameplay").Text()), replayLimit);
        var presentation = RandomCheckpointCodec.Decode(
            Encoding.UTF8.GetBytes(root.At("presentation").Text()),
            replayLimit);
        Require(
            gameplay.Domain == RandomDomain.Gameplay && presentation.Domain == RandomDomain.PresentationContent,
            "Invalid session RNG domains");
        return new(profile, hash, origin, session, participant, counters, world, gameplay, presentation, command);
    }

    /// <summary>
    /// Checks the envelope version, payload bound and byte integrity before decoding owned checkpoint values.
    /// </summary>
    /// <remarks>
    /// The hash detects damage; it does not authenticate the producer. This method does not install a session
    /// or invoke owner codecs.
    /// </remarks>
    public static LocalCheckpoint Decode(
        ReadOnlySpan<byte> encoded,
        ulong replayLimit = RandomCheckpointCodec.DefaultReplayLimit)
    {
        var envelope = new ContentReader(ContentJson.Parse(encoded, "local-checkpoint"), "local-checkpoint");
        envelope.Fields(["format", "payload", "sha256"]);
        envelope.At("format").Constant(Format);
        string payload = envelope.At("payload").Text();
        Require(Encoding.UTF8.GetByteCount(payload) <= MaximumPayload, "Checkpoint payload exceeds limit");
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        Require(
            ContentIdentity.Sha256(bytes) == envelope.At("sha256").Sha256(),
            "Checkpoint payload integrity failure");
        return Payload(
            new(ContentJson.Parse(bytes, "local-checkpoint/payload"), "local-checkpoint/payload"),
            replayLimit);
    }
}
