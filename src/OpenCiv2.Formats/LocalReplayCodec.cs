using System.Text;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Formats.Content;
using static OpenCiv2.Formats.LocalCheckpointCodec;

namespace OpenCiv2.Formats;

/// <summary>Bounded local input journal. The digest detects damage, rather than establishing trust.</summary>
public static class LocalReplayCodec
{
    public const string Format = "openciv2-local-replay/1";
    public const int MaximumBytes = 2 * MaximumPayload + 1024;
    private const string Profile = "alder-valley/1";

    public static byte[] Encode(LocalReplayTape tape)
    {
        var budget = new Budget();
        budget.Items(tape.Steps.Count);
        var root = new JsonObject
        {
            ["profile"] = Profile,
            ["scriptSha256"] = tape.ScriptSha256,
            ["steps"] = new JsonArray(tape.Steps.Select(step => (JsonNode)Write(step, budget)).ToArray())
        };
        _ = Payload(new(root, "local-replay/payload"));
        byte[] payload = ContentJson.CanonicalBytes(root);
        if (payload.Length > MaximumPayload)
        {
            throw new ArgumentException("Replay payload exceeds limit");
        }
        return ContentJson.CanonicalBytes(new JsonObject
        {
            ["format"] = Format,
            ["payload"] = Encoding.UTF8.GetString(payload),
            ["sha256"] = ContentIdentity.Sha256(payload)
        });
    }

    public static LocalReplayTape Decode(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length > MaximumBytes)
        {
            throw new ArgumentException("Replay document exceeds limit");
        }
        var envelope = new ContentReader(ContentJson.Parse(encoded, "local-replay"), "local-replay");
        envelope.Fields(["format", "payload", "sha256"]);
        envelope.At("format").Constant(Format);
        string payload = envelope.At("payload").Text();
        if (Encoding.UTF8.GetByteCount(payload) > MaximumPayload)
        {
            throw new ArgumentException("Replay payload exceeds limit");
        }
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        if (ContentIdentity.Sha256(bytes) != envelope.At("sha256").Sha256())
        {
            throw new ArgumentException("Replay payload integrity failure");
        }
        return Payload(new(ContentJson.Parse(bytes, "local-replay/payload"), "local-replay/payload"));
    }

    private static LocalReplayTape Payload(ContentReader root)
    {
        root.Fields(["profile", "scriptSha256", "steps"]);
        root.At("profile").Constant(Profile);
        var steps = root.At("steps");
        var values = new LocalReplayStep[Count(steps)];
        for (int i = 0; i < values.Length; ++i)
        {
            values[i] = Read(steps.Element(i));
        }
        return new(root.At("scriptSha256").Sha256(), new(values));
    }

    private static JsonObject Write(LocalReplayStep step, Budget budget)
    {
        budget.Add(256);
        return step switch
        {
            LocalReplayStep.Submit v => new()
            {
                ["kind"] = "submit",
                ["controller"] = v.Controller.ToString(),
                ["name"] = budget.Text(v.Request.Name),
                ["arguments"] = Values(v.Request.Arguments, budget),
                ["command"] = Number(v.Command.Value),
                ["status"] = v.Status.ToString(),
                ["code"] = budget.Text(v.Code)
            },
            LocalReplayStep.Reply v => new()
            {
                ["kind"] = "reply",
                ["controller"] = v.Controller.ToString(),
                ["interaction"] = Number(v.Interaction.Value),
                ["choice"] = Number(v.Choice),
                ["accepted"] = v.Receipt.Accepted,
                ["code"] = budget.Text(v.Receipt.Code)
            },
            LocalReplayStep.Cancel v => new()
            {
                ["kind"] = "cancel",
                ["controller"] = v.Controller.ToString(),
                ["command"] = Number(v.Command.Value),
                ["accepted"] = v.Receipt.Accepted,
                ["code"] = budget.Text(v.Receipt.Code)
            },
            LocalReplayStep.Advance v => new()
            {
                ["kind"] = "advance",
                ["command"] = Number(v.Command.Value),
                ["status"] = v.Status.ToString(),
                ["code"] = budget.Text(v.Code),
                ["interaction"] = v.Interaction is { } i ? Number(i.Value) : null,
                ["effects"] = Number(v.Effects)
            },
            LocalReplayStep.Retire v => new() { ["kind"] = "retire", ["command"] = Number(v.Command.Value) },
            LocalReplayStep.Save v => new() { ["kind"] = "save", ["sha256"] = v.Sha256 },
            LocalReplayStep.Load v => new() { ["kind"] = "load", ["bytes"] = budget.Blob(v.Bytes), ["status"] = v.Status },
            _ => throw new ArgumentException("Unknown replay step")
        };
    }

    private static T Enumeration<T>(ContentReader reader) where T : struct, Enum
    {
        string name = reader.Text();
        if (!Enum.TryParse<T>(name, out var value) || !Enum.IsDefined(value) || value.ToString() != name)
        {
            reader.Error("unknown replay value");
        }
        return value;
    }

    private static LocalReplayStep Read(ContentReader row)
    {
        ControllerKind Controller() => Enumeration<ControllerKind>(row.At("controller"));
        CommandStatus Status() => Enumeration<CommandStatus>(row.At("status"));
        string Code() => Text(row.At("code"));
        InputReceipt Receipt() => new(Boolean(row.At("accepted")), Code());
        switch (row.At("kind").Text())
        {
            case "submit":
                row.Fields(["kind", "controller", "name", "arguments", "command", "status", "code"]);
                return new LocalReplayStep.Submit(Controller(), new(Text(row.At("name")), Values(row.At("arguments"), validate: false)),
                    new(Id(row.At("command"))), Status(), Code());
            case "reply":
                row.Fields(["kind", "controller", "interaction", "choice", "accepted", "code"]);
                return new LocalReplayStep.Reply(Controller(), new(Number<ulong>(row.At("interaction"))),
                    Number<int>(row.At("choice")), Receipt());
            case "cancel":
                row.Fields(["kind", "controller", "command", "accepted", "code"]);
                return new LocalReplayStep.Cancel(Controller(), new(Number<ulong>(row.At("command"))), Receipt());
            case "advance":
                row.Fields(["kind", "command", "status", "code", "interaction", "effects"]);
                int effects = Number<int>(row.At("effects"));
                if (effects < 0)
                {
                    row.Error("negative effect count");
                }
                return new LocalReplayStep.Advance(new(Id(row.At("command"))), Status(), Code(),
                    row.At("interaction").Value is null ? null : new(Id(row.At("interaction"))), effects);
            case "retire":
                row.Fields(["kind", "command"]);
                return new LocalReplayStep.Retire(new(Id(row.At("command"))));
            case "save":
                row.Fields(["kind", "sha256"]);
                return new LocalReplayStep.Save(row.At("sha256").Sha256());
            case "load":
                row.Fields(["kind", "bytes", "status"]);
                string status = row.At("status").Text();
                if (status is not ("Loaded" or "Busy" or "InvalidFile" or "UnsupportedVersion" or "UnsupportedGame"))
                {
                    row.Error("unknown load status");
                }
                return new LocalReplayStep.Load(Blob(row.At("bytes")), status);
            default:
                throw new ArgumentException("Unknown replay step");
        }
    }
}
