using System.Globalization;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.Formats;

/// <summary>M1-01 RNG component, not a session snapshot or classic file field. Version 2 binds PCG32's
/// logical seed/stream/draw position. Replay, JSON size, record count and semantic key lengths are bounded.</summary>
public static class RandomCheckpointCodec
{
    public const string Format = "openciv2-random-checkpoint/2";
    public const int MaximumRecords = 16384, MaximumName = 4096;
    public const ulong DefaultReplayLimit = 1_000_000;

    private static string Number<T>(T value) where T : IFormattable =>
        value.ToString(null, CultureInfo.InvariantCulture);

    private static T Number<T>(ContentReader reader) where T : struct, System.Numerics.IBinaryInteger<T>
    {
        string text = reader.Text();
        if (!T.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            || Number(value) != text)
        {
            reader.Error("expected canonical decimal string in range");
        }

        return value;
    }

    // Four digits per UTF-16 code unit preserve every managed semantic key, including NUL and lone surrogates.
    private static string Key(string value) =>
        string.Concat(value.Select(c => ((ushort)c).ToString("x4", CultureInfo.InvariantCulture)));

    private static int Digit(char c) => c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'f' ? c - 'a' + 10 : -1;

    private static ulong Hex(ContentReader reader, int length)
    {
        string text = reader.Text();
        if (text.Length != length || text.Any(c => Digit(c) < 0))
        {
            reader.Error("invalid lowercase hexadecimal");
        }

        ulong value = 0;
        foreach (char c in text)
        {
            value = (value << 4) | (uint)Digit(c);
        }

        return value;
    }

    private static string Key(ContentReader reader)
    {
        string text = reader.Text();
        if (text.Length % 4 != 0 || text.Length / 4 > MaximumName || text.Any(c => Digit(c) < 0))
        {
            reader.Error("invalid UTF-16 key hexadecimal");
        }

        var chars = new char[text.Length / 4];
        for (int i = 0; i < chars.Length; ++i)
        {
            int value = 0;
            for (int j = 0; j < 4; ++j)
            {
                value = value * 16 + Digit(text[4 * i + j]);
            }

            chars[i] = (char)value;
        }
        return new(chars);
    }

    private static RandomDomain Domain(ContentReader reader) => reader.Text() switch
    {
        "gameplay" => RandomDomain.Gameplay,
        "presentation-content" => RandomDomain.PresentationContent,
        "cosmetic" => RandomDomain.Cosmetic,
        _ => throw new ContentException(
            "invalid-content",
            reader.Path,
            "unknown random domain")
    };

    private static void Validate(RandomCheckpoint checkpoint, ulong replayLimit)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (checkpoint.Context is null || checkpoint.Context.Length > MaximumName)
        {
            throw new ArgumentException("Checkpoint context exceeds name limit");
        }

        int count = 0, budget = 4096 + checkpoint.Context.Length;
        void Records(OwnedList<Choice> choices)
        {
            if (choices.Count > MaximumRecords - count)
            {
                throw new ArgumentException("Checkpoint exceeds choice record limit");
            }

            count += choices.Count;
            foreach (var choice in choices)
            {
                if (choice.Key.Site is null || choice.Key.Call is null || choice.Key.Site.Length > MaximumName
                    || choice.Key.Call.Length > MaximumName)
                {
                    throw new ArgumentException("Checkpoint choice name exceeds limit");
                }

                int size = 512 + 4 * (choice.Key.Site.Length + choice.Key.Call.Length);
                if (size > ContentJson.MaximumBytes - budget)
                {
                    throw new ArgumentException("Checkpoint exceeds encoding budget");
                }

                budget += size;
            }
        }
        if (checkpoint.Controlled is { } tape)
        {
            Records(tape.Records);
        }

        if (checkpoint.Trace is { } trace)
        {
            Records(trace.Events);
        }

        RandomContext prepared = checkpoint.Domain switch
        {
            RandomDomain.Gameplay => new GameplayRandom(
                0,
                checkpoint.Context),
            RandomDomain.PresentationContent => new PresentationRandom(
                0,
                checkpoint.Context),
            RandomDomain.Cosmetic => new CosmeticRandom(
                0,
                checkpoint.Context),
            _ => throw new ArgumentException(
                "Unknown random domain")
        };
        prepared.Restore(checkpoint, replayLimit);
    }

    private static JsonObject Encode(Choice choice)
    {
        JsonObject law = choice.Law switch
        {
            SamplingLaw.PrimaryCode => new() { ["kind"] = "primary-code" },
            SamplingLaw.PrimaryModulo m => new() { ["kind"] = "primary-modulo", ["n"] = Number(m.N) },
            SamplingLaw.SecondaryUnit => new() { ["kind"] = "secondary-unit" },
            SamplingLaw.SecondaryRange r => new()
            {
                ["kind"] = "secondary-range",
                ["lo"] = Number(r.Lo),
                ["hi"] = Number(r.Hi)
            },
            _ => throw new ArgumentException("Unknown sampling law")
        };
        JsonObject result = choice.Result.IsInteger
            ? new() { ["i32"] = Number(choice.Result.AsInteger()) }
            : new() { ["f64"] = unchecked((ulong)choice.Result.Bits).ToString("x16", CultureInfo.InvariantCulture) };
        return new()
        {
            ["siteUtf16"] = Key(choice.Key.Site),
            ["callUtf16"] = Key(choice.Key.Call),
            ["attempt"] = Number(choice.Key.Attempt),
            ["law"] = law,
            ["code"] = Number(choice.Code.Bits),
            ["result"] = result
        };
    }

    private static JsonArray Encode(OwnedList<Choice> choices) =>
        new(choices.Select(c => (JsonNode)Encode(c)).ToArray());

    public static byte[] Encode(RandomCheckpoint checkpoint, ulong replayLimit = DefaultReplayLimit)
    {
        Validate(checkpoint, replayLimit);
        var root = new JsonObject
        {
            ["format"] = Format,
            ["policy"] = new JsonObject
            {
                ["id"] = checkpoint.PolicyId,
                ["version"] = Number(checkpoint.PolicyVersion)
            },
            ["domain"] = RandomPolicy.DomainLabel(checkpoint.Domain),
            ["context"] = checkpoint.Context,
            ["engineSeed"] = Number(checkpoint.Position.Seed),
            ["stream"] = Number(checkpoint.Position.Stream),
            ["draws"] = Number(checkpoint.Position.Draws),
            ["controlled"] = checkpoint.Controlled is { } tape ? Encode(tape.Records) : null,
            ["trace"] = checkpoint.Trace is { } trace
                ? new JsonObject { ["limit"] = Number(trace.Limit), ["events"] = Encode(trace.Events) }
                : null
        };
        return ContentJson.CanonicalBytes(root);
    }

    private static Choice DecodeChoice(ContentReader reader)
    {
        reader.Fields(["siteUtf16", "callUtf16", "attempt", "law", "code", "result"]);
        var key = new ChoiceKey(
            Key(reader.At("siteUtf16")),
            Key(reader.At("callUtf16")),
            Number<int>(reader.At("attempt")));
        var law = reader.At("law");
        SamplingLaw sampling;
        switch (law.At("kind").Text())
        {
            case "primary-code":
                law.Fields(["kind"]);
                sampling = new SamplingLaw.PrimaryCode();
                break;
            case "primary-modulo":
                law.Fields(["kind", "n"]);
                sampling = new SamplingLaw.PrimaryModulo(Number<int>(law.At("n")));
                break;
            case "secondary-unit":
                law.Fields(["kind"]);
                sampling = new SamplingLaw.SecondaryUnit();
                break;
            case "secondary-range":
                law.Fields(["kind", "lo", "hi"]);
                sampling = new SamplingLaw.SecondaryRange(Number<int>(law.At("lo")), Number<int>(law.At("hi")));
                break;
            default:
                throw new ContentException("invalid-content", law.Path, "unknown sampling law");
        }
        var code = sampling is SamplingLaw.PrimaryCode or SamplingLaw.PrimaryModulo
            ? ChoiceCode.Primary(Number<int>(reader.At("code")))
            : ChoiceCode.Secondary(Number<uint>(reader.At("code")));
        var observed = reader.At("result");
        ChoiceResult result;
        if (sampling is SamplingLaw.SecondaryUnit)
        {
            observed.Fields(["f64"]);
            result = ChoiceResult.Binary64(
                BitConverter.Int64BitsToDouble(unchecked((long)Hex(observed.At("f64"), 16))));
        }
        else
        {
            observed.Fields(["i32"]);
            result = ChoiceResult.Integer(Number<int>(observed.At("i32")));
        }
        var choice = new Choice(key, sampling, code, result);
        choice.Validate();
        return choice;
    }

    private static OwnedList<Choice> DecodeChoices(ContentReader reader, ref int remaining)
    {
        int count = reader.ArraySize();
        if (count > remaining)
        {
            reader.Error("too many checkpoint choice records");
        }

        remaining -= count;
        var choices = new Choice[count];
        for (int i = 0; i < count; ++i)
        {
            choices[i] = DecodeChoice(reader.Element(i));
        }

        return new(choices);
    }

    public static RandomCheckpoint Decode(ReadOnlySpan<byte> json, ulong replayLimit = DefaultReplayLimit)
    {
        var root = new ContentReader(ContentJson.Parse(json, "random-checkpoint"), "random-checkpoint");
        root.Fields(["format", "policy", "domain", "context", "engineSeed", "stream", "draws", "controlled", "trace"]);
        root.At("format").Constant(Format);
        var policy = root.At("policy");
        policy.Fields(["id", "version"]);
        policy.At("id").Constant(RandomPolicy.Id);
        int version = Number<int>(policy.At("version"));
        if (version != RandomPolicy.Version)
        {
            policy.Error("unsupported random policy version");
        }

        var position = new Pcg32Position(
            Number<ulong>(root.At("engineSeed")),
            Number<ulong>(root.At("stream")),
            Number<ulong>(root.At("draws")));
        if (position.Draws > replayLimit)
        {
            root.Error("checkpoint exceeds replay budget");
        }

        int remaining = MaximumRecords;
        var controlled = root.At("controlled");
        var trace = root.At("trace");
        ControlledChoices? tape = controlled.Value is null ? null : new(DecodeChoices(controlled, ref remaining));
        ChoiceTrace? history = null;
        if (trace.Value is not null)
        {
            trace.Fields(["limit", "events"]);
            history = new(Number<uint>(trace.At("limit")), DecodeChoices(trace.At("events"), ref remaining));
        }
        var checkpoint = new RandomCheckpoint(
            RandomPolicy.Id,
            version,
            Domain(root.At("domain")),
            root.At("context").Text(),
            position,
            tape,
            history);
        Validate(checkpoint, replayLimit);
        return checkpoint;
    }
}
