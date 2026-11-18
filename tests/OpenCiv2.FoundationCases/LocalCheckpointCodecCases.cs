using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Contracts;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // M1-01 authored portable representation cases. No owner codec or gameplay compatibility claim.
    private static LocalCheckpoint LocalCodecFixture(bool pending = true)
    {
        var checkpoint = new LocalCheckpoint("test.local-foundation/1", ContentIdentity.Sha256("authored content"u8), new(11), new(12), new(13),
            new(14, 15, 16, 17, 18, ulong.MaxValue), new([0, 255, 128]), new GameplayRandom(23, "game/context").Capture(), new PresentationRandom(24, "presentation/context").Capture(), null);
        if (!pending) return checkpoint;
        OwnedList<NamedValue> values = new([new("i", new CommandValue.Integer(int.MinValue)), new("b", new CommandValue.Boolean(true)), new("t", new CommandValue.Text("\0é世界\ud800\udfff")),
            new("v", new CommandValue.Civilization(new(7))), new("u", new CommandValue.Unit(new(new(ushort.MaxValue), new(uint.MaxValue)))), new("c", new CommandValue.City(new(new(ushort.MaxValue), new(uint.MaxValue))))]);
        var interaction = new Interaction(new(8), new(13), new(1), new("pick\0\ud800", new(13), new CommandValue.Text("owned target"), new([int.MinValue, 0, int.MaxValue]), ulong.MaxValue, int.MinValue));
        var update = new CommandUpdate(new(new(99), new(12), new(7)), CommandStatus.AwaitingInteraction, "code\udfff", values, interaction, new([new(1, "debit", values), new(2, "retained", OwnedList<NamedValue>.Empty)]));
        return checkpoint with { Command = new(new("test.checkpoint", values), uint.MaxValue, new(5), AccessClass.Developer, update, "authored/1", uint.MaxValue, new([0, 1, 254, 255])) };
    }
    private static JsonNode LocalCodecPayload(byte[] bytes) => JsonNode.Parse(JsonNode.Parse(bytes)!["payload"]!.GetValue<string>())!;
    private static byte[] LocalCodecEnvelope(JsonNode payload)
    {
        string text = payload.ToJsonString(); return CheckpointJsonBytes(new JsonObject { ["format"] = LocalCheckpointCodec.Format, ["payload"] = text, ["sha256"] = ContentIdentity.Sha256(Encoding.UTF8.GetBytes(text)) });
    }
    private static void LocalCodecReject(byte[] bytes)
    {
        bool rejected = false; try { _ = LocalCheckpointCodec.Decode(bytes); } catch (Exception error) when (error is ContentException or ArgumentException or ChoiceException) { rejected = true; }
        Equal(true, rejected);
    }
    private static void LocalCheckpointCodecRoundTrips()
    {
        foreach (bool pending in new[] { false, true })
        {
            var value = LocalCodecFixture(pending); byte[] encoded = LocalCheckpointCodec.Encode(value); Equal(value, LocalCheckpointCodec.Decode(encoded)); BytesEqual(encoded, LocalCheckpointCodec.Encode(LocalCheckpointCodec.Decode(encoded)));
            var payload = LocalCodecPayload(encoded); Equal("11", payload["origin"]!.GetValue<string>()); Equal("18446744073709551615", payload["counters"]!["revision"]!.GetValue<string>());
            Equal("00ff80", payload["world"]!.GetValue<string>()); Equal(true, payload["gameplay"]!.GetValue<string>().Contains("openciv2-random-checkpoint/2", StringComparison.Ordinal));
            // Field order/whitespace is immaterial; integrity covers exact payload bytes, not normalized JSON.
            Equal(value, LocalCheckpointCodec.Decode(LocalCodecEnvelope(payload)));
        }
        var original = LocalCodecFixture(); var command = original.Command!;
        var noOptionals = original with { Command = command with { Controller = null, Access = AccessClass.Player, Update = command.Update with { Interaction = command.Update.Interaction! with { Spec = command.Update.Interaction.Spec with { Recipient = null, Target = null, Deadline = null, DefaultChoice = null, Cancellable = false } } } } };
        Equal(noOptionals, LocalCheckpointCodec.Decode(LocalCheckpointCodec.Encode(noOptionals)));
        var exhausted = original with { Origin = new(ulong.MaxValue), Session = new(ulong.MaxValue - 1), Participant = new(ulong.MaxValue - 1), Counters = new(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue), Command = null };
        Equal(exhausted, LocalCheckpointCodec.Decode(LocalCheckpointCodec.Encode(exhausted)));
        var culture = CultureInfo.CurrentCulture;
        try { var custom = (CultureInfo)CultureInfo.InvariantCulture.Clone(); custom.NumberFormat.NegativeSign = "~"; CultureInfo.CurrentCulture = custom; Equal(original, LocalCheckpointCodec.Decode(LocalCheckpointCodec.Encode(original))); }
        finally { CultureInfo.CurrentCulture = culture; }
    }
    private static void LocalCheckpointCodecRng()
    {
        var key = new ChoiceKey("test", "prefix"); var suffix = new ChoiceKey("test", "suffix");
        var game = new GameplayRandom(new ControlledChoices(new([Choice.Create(key, new SamplingLaw.PrimaryModulo(7), ChoiceCode.Primary(18)), Choice.Create(suffix, new SamplingLaw.PrimaryModulo(5), ChoiceCode.Primary(12))])), "controlled/game");
        Equal(4, game.PrimaryModulo(key, 7)); var presentation = new PresentationRandom(999, "generated/presentation"); presentation.RecordChoices(2); presentation.SecondaryUnit(key);
        var saved = LocalCodecFixture() with { Gameplay = game.Capture(), Presentation = presentation.Capture() }; var decoded = LocalCheckpointCodec.Decode(LocalCheckpointCodec.Encode(saved)); Equal(saved, decoded);
        var restoredGame = new GameplayRandom(0, decoded.Gameplay.Context); restoredGame.Restore(decoded.Gameplay, 1); ChoiceFails(ChoiceFailure.AlreadyConsumed, () => restoredGame.PrimaryModulo(key, 7), key);
        Equal(game.PrimaryModulo(suffix, 5), restoredGame.PrimaryModulo(suffix, 5)); restoredGame.RequireChoicesConsumed();
        var restoredPresentation = new PresentationRandom(0, decoded.Presentation.Context); restoredPresentation.Restore(decoded.Presentation, 1); Equal(presentation.PrimaryCode(suffix), restoredPresentation.PrimaryCode(suffix));
        Equal(presentation.Capture(), restoredPresentation.Capture()); Throws<ArgumentException>(() => LocalCheckpointCodec.Encode(saved, 0));
        Throws<ContentException>(() => LocalCheckpointCodec.Decode(LocalCheckpointCodec.Encode(saved), 0));
    }
    private static void LocalCheckpointCodecMalformedEnvelope()
    {
        byte[] bytes = LocalCheckpointCodec.Encode(LocalCodecFixture());
        Action<JsonNode>[] mutations = [n => n["format"] = "openciv2-local-checkpoint/1", n => n["format"] = null, n => n["extra"] = true,
            n => n.AsObject().Remove("sha256"), n => n["sha256"] = new string('0', 64), n => n["sha256"] = new string('A', 64), n => n["payload"] = "{}", n => n["payload"] = 1];
        foreach (var change in mutations) { var envelope = JsonNode.Parse(bytes)!; change(envelope); LocalCodecReject(CheckpointJsonBytes(envelope)); }
        foreach (byte[] malformed in new[] { bytes[..(bytes.Length / 2)], "{\"format\":\"a\",\"format\":\"b\"}"u8.ToArray(), new byte[] { 0xff }, "{\"x\":\"\\ud800\"}"u8.ToArray() }) LocalCodecReject(malformed);
        string duplicate = "{\"profile\":\"a\",\"profile\":\"b\"}";
        LocalCodecReject(CheckpointJsonBytes(new JsonObject { ["format"] = LocalCheckpointCodec.Format, ["payload"] = duplicate, ["sha256"] = ContentIdentity.Sha256(Encoding.UTF8.GetBytes(duplicate)) }));
    }
    private static void LocalCheckpointCodecMalformedPayload()
    {
        byte[] bytes = LocalCheckpointCodec.Encode(LocalCodecFixture());
        Action<JsonNode>[] mutations =
        [
            n => n["extra"] = true, n => n.AsObject().Remove("world"), n => n["profile"] = "", n => n["profile"] = new string('a', 516), n => n["profile"] = "FFFF", n => n["content"] = "hash",
            n => n["origin"] = "0", n => n["session"] = "0", n => n["participant"] = "0", n => n["origin"] = "18446744073709551616", n => n["origin"] = 1,
            n => n["counters"]!["session"] = "12", n => n["counters"]!["participant"] = "13", n => n["counters"]!["controller"] = "0", n => n["counters"]!["command"] = "0", n => n["counters"]!["interaction"] = "0",
            n => n["counters"]!["revision"] = "01", n => n["counters"]!["revision"] = "-0", n => n["counters"]!["revision"] = "+1", n => n["world"] = "0", n => n["world"] = "FF", n => n["world"] = "gg",
            n => n["command"]!["id"] = "17", n => n["command"]!["id"] = "0", n => n["command"]!["controller"] = "16", n => n["command"]!["controller"] = "0", n => n["command"]!["access"] = "root",
            n => n["command"]!["name"] = "", n => n["command"]!["name"] = "000", n => n["command"]!["descriptorVersion"] = "0", n => n["command"]!["codec"] = "", n => n["command"]!["codecVersion"] = "0",
            n => n["command"]!["origin"] = "0", n => n["command"]!["session"] = "13", n => n["command"]!["locals"] = "no", n => n["command"]!["effects"]![0]!["index"] = "2", n => n["command"]!["effects"]![1]!["index"] = "1", n => n["command"]!["effects"]![0]!["name"] = "",
            n => n["command"]!["arguments"]![0]!["name"] = "", n => n["command"]!["arguments"]!.AsArray().Add(n["command"]!["arguments"]![0]!.DeepClone()),
            n => n["command"]!["arguments"]![0]!["value"]!["kind"] = "object", n => n["command"]!["arguments"]![0]!["value"]!["value"] = "2147483648", n => n["command"]!["arguments"]![1]!["value"]!["value"] = "true",
            n => n["command"]!["arguments"]![2]!["value"]!["value"] = "zzzz", n => n["command"]!["arguments"]![3]!["value"]!["value"] = "8", n => n["command"]!["arguments"]![4]!["value"]!["slot"] = "65536",
            n => n["command"]!["arguments"]![5]!["value"]!["token"] = "4294967296", n => n["command"]!["arguments"]![4]!["value"]!["extra"] = true,
            n => n["command"]!["interaction"]!["id"] = "18", n => n["command"]!["interaction"]!["recipient"] = "14", n => n["command"]!["interaction"]!["observer"] = "2", n => n["command"]!["interaction"]!["specifiedRecipient"] = "14",
            n => n["command"]!["interaction"]!["prompt"] = "", n => n["command"]!["interaction"]!["choices"] = new JsonArray(), n => n["command"]!["interaction"]!["choices"]!.AsArray().Add((JsonNode)JsonValue.Create("0")),
            n => n["command"]!["interaction"]!["deadline"] = null, n => n["command"]!["interaction"]!["default"] = "3", n => n["command"]!["interaction"]!["cancellable"] = "true",
            n => n["gameplay"] = n["presentation"]!.GetValue<string>(), n => n["presentation"] = n["gameplay"]!.GetValue<string>(), n => n["gameplay"] = n["gameplay"]!.GetValue<string>().Replace("oc2-pcg32", "oc2-mt19937-64", StringComparison.Ordinal)
        ];
        foreach (var change in mutations) { var payload = LocalCodecPayload(bytes); change(payload); LocalCodecReject(LocalCodecEnvelope(payload)); }
    }
    private static void LocalCheckpointCodecResourceLimits()
    {
        var original = LocalCodecFixture(); var command = original.Command!; var limits = original with { World = new(new byte[LocalCheckpointCodec.MaximumOwnerBytes]), Command = command with { Locals = new(new byte[LocalCheckpointCodec.MaximumOwnerBytes]) } };
        Equal(limits, LocalCheckpointCodec.Decode(LocalCheckpointCodec.Encode(limits)));
        var many = new OwnedList<NamedValue>(Enumerable.Range(0, 4096).Select(i => new NamedValue(i.ToString(CultureInfo.InvariantCulture), new CommandValue.Integer(i))));
        var allowed = original with { Command = command with { Request = command.Request with { Arguments = many }, Update = command.Update with { Code = new string('\ud800', 4096), Committed = new(Enumerable.Range(1, 4096).Select(i => new Effect((ulong)i, "effect", OwnedList<NamedValue>.Empty))) } } };
        Equal(allowed, LocalCheckpointCodec.Decode(LocalCheckpointCodec.Encode(allowed)));
        Throws<ArgumentException>(() => LocalCheckpointCodec.Encode(original with { World = new(new byte[LocalCheckpointCodec.MaximumOwnerBytes + 1]) }));
        Throws<ArgumentException>(() => LocalCheckpointCodec.Encode(original with { Command = command with { Update = command.Update with { Code = new string('a', 4097) } } }));
        Throws<ArgumentException>(() => LocalCheckpointCodec.Encode(original with { Command = command with { Request = command.Request with { Arguments = new(many.Append(many[0])) } } }));
        var huge = new OwnedList<NamedValue>(Enumerable.Range(0, 1000).Select(i => new NamedValue(i.ToString(CultureInfo.InvariantCulture), new CommandValue.Text(new string('a', 4096)))));
        Throws<ArgumentException>(() => LocalCheckpointCodec.Encode(original with { Command = command with { Request = command.Request with { Arguments = huge } } }));
        LocalCodecReject(new byte[ContentJson.MaximumBytes + 1]);
        var payload = LocalCodecPayload(LocalCheckpointCodec.Encode(original)); payload["world"] = new string('0', (LocalCheckpointCodec.MaximumOwnerBytes + 1) * 2); LocalCodecReject(LocalCodecEnvelope(payload));
        payload = LocalCodecPayload(LocalCheckpointCodec.Encode(original)); payload["command"]!["code"] = new string('0', 4097 * 4); LocalCodecReject(LocalCodecEnvelope(payload));
        payload = LocalCodecPayload(LocalCheckpointCodec.Encode(original)); payload["command"]!["effects"] = new JsonArray(Enumerable.Range(0, 4097).Select(_ => (JsonNode)new JsonObject()).ToArray()); LocalCodecReject(LocalCodecEnvelope(payload));
        string excessive = new(' ', LocalCheckpointCodec.MaximumPayload + 1);
        LocalCodecReject(CheckpointJsonBytes(new JsonObject { ["format"] = LocalCheckpointCodec.Format, ["payload"] = excessive, ["sha256"] = ContentIdentity.Sha256(Encoding.UTF8.GetBytes(excessive)) }));
        foreach (var bad in new[] { original with { Command = command with { Access = (AccessClass)9 } }, original with { Command = command with { Update = command.Update with { Status = CommandStatus.Pending } } }, original with { Command = command with { Update = command.Update with { Interaction = null } } } })
            Throws<ArgumentException>(() => LocalCheckpointCodec.Encode(bad));
    }
}
