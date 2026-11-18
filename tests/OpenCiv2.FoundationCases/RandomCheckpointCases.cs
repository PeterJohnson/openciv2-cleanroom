using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static RandomContext CheckpointRandom(RandomDomain domain, ulong seed = 0) => domain switch
    { RandomDomain.Gameplay => new GameplayRandom(seed, "checkpoint/context"), RandomDomain.PresentationContent => new PresentationRandom(seed, "checkpoint/context"), _ => new CosmeticRandom(seed, "checkpoint/context") };
    private static void RandomCheckpointGeneratedDomains()
    {
        foreach (var domain in new[] { RandomDomain.Gameplay, RandomDomain.PresentationContent, RandomDomain.Cosmetic })
        {
            var source = CheckpointRandom(domain, ulong.MaxValue);
            foreach (ulong position in new ulong[] { 0, 1, 31, 32, 33, 311, 312, 313, 1000 })
            {
                while (source.Draws < position) source.SecondaryCode("advance"); var saved = source.Capture(); var bytes = RandomCheckpointCodec.Encode(saved);
                var restored = RandomCheckpointCodec.Decode(bytes); Equal(saved, restored); BytesEqual(bytes, RandomCheckpointCodec.Encode(restored));
                var destination = CheckpointRandom(domain, 77); destination.Restore(restored, 1000); var expected = CheckpointRandom(domain); expected.Restore(saved, 1000);
                for (int i = 0; i < 700; ++i) Equal(expected.SecondaryCode("suffix"), destination.SecondaryCode("suffix")); Equal(saved, source.Capture());
            }
        }
    }
    private static void RandomCheckpointControlledKeysAndPrefix()
    {
        var first = new ChoiceKey("a\0\u00ff\ud800", "世界\udfff", 0); var second = new ChoiceKey("test", "second", 1); var third = new ChoiceKey("test", "third", 2); var fourth = new ChoiceKey("test", "fourth", int.MaxValue);
        var tape = new ControlledChoices(new([Choice.Create(first, new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(32767)), Choice.Create(second, new SamplingLaw.PrimaryModulo(7), ChoiceCode.Primary(9)),
            Choice.Create(third, new SamplingLaw.SecondaryUnit(), ChoiceCode.Secondary(uint.MaxValue)), Choice.Create(fourth, new SamplingLaw.SecondaryRange(-4, 7), ChoiceCode.Secondary(0))]));
        var source = new GameplayRandom(tape, "checkpoint/context"); Equal(2, source.PrimaryModulo(second, 7)); var saved = source.Capture(); var bytes = RandomCheckpointCodec.Encode(saved, 0);
        Equal(true, Encoding.UTF8.GetString(bytes).Contains("0061000000ffd800", StringComparison.Ordinal)); var restored = RandomCheckpointCodec.Decode(bytes, 0); Equal(saved, restored);
        var destination = new GameplayRandom(0, "checkpoint/context"); destination.Restore(restored, 0); ChoiceFails(ChoiceFailure.AlreadyConsumed, () => destination.PrimaryModulo(second, 7), second); Equal(saved, destination.Capture());
        Equal(32767, destination.PrimaryCode(first)); Equal(1.0, destination.SecondaryUnit(third)); Equal(-4, destination.SecondaryRange(fourth, -4, 7)); destination.RequireChoicesConsumed(); Equal(0UL, destination.Draws);
        Equal(destination.Capture(), RandomCheckpointCodec.Decode(RandomCheckpointCodec.Encode(destination.Capture(), 0), 0));
    }
    private static void RandomCheckpointTraceCapacityAndReplay()
    {
        var schema = new RandomCheckpoint(RandomPolicy.Id, 1, RandomDomain.Gameplay, "checkpoint/context", new(ulong.MaxValue, 0, 0));
        Equal("{\"context\":\"checkpoint/context\",\"controlled\":null,\"domain\":\"gameplay\",\"draws\":\"0\",\"engineSeed\":\"18446744073709551615\",\"format\":\"openciv2-random-checkpoint/2\",\"policy\":{\"id\":\"oc2-pcg32\",\"version\":\"1\"},\"stream\":\"0\",\"trace\":null}", Encoding.UTF8.GetString(RandomCheckpointCodec.Encode(schema, 0)));
        var source = new GameplayRandom(31, "checkpoint/context"); source.RecordChoices(2); source.PrimaryModulo(new ChoiceKey("test", "prefix"), 5);
        var saved = source.Capture(); var restored = new GameplayRandom(0, "checkpoint/context"); restored.Restore(RandomCheckpointCodec.Decode(RandomCheckpointCodec.Encode(saved)), 1000);
        ChoiceFails(ChoiceFailure.AlreadyConsumed, () => restored.PrimaryModulo(new ChoiceKey("test", "prefix"), 5), new("test", "prefix"));
        Equal(source.SecondaryUnit(new ChoiceKey("test", "suffix")), restored.SecondaryUnit(new ChoiceKey("test", "suffix"))); Equal(source.Capture(), restored.Capture());
        ChoiceFails(ChoiceFailure.TraceLimit, () => restored.PrimaryCode(new ChoiceKey("test", "extra")), new("test", "extra"));
        var wide = new GameplayRandom(0, "checkpoint/context"); wide.RecordChoices(uint.MaxValue); wide.PrimaryCode(new ChoiceKey("wide", "one")); Equal((long)uint.MaxValue, RandomCheckpointCodec.Decode(RandomCheckpointCodec.Encode(wide.Capture())).Trace!.Limit);
        Throws<ArgumentOutOfRangeException>(() => new GameplayRandom(0, "valid").RecordChoices((long)uint.MaxValue + 1));
        var beforeEncoding = source.Capture(); var exact = saved with { Position = new(ulong.MaxValue, 0x7fffffffffffffff, 1), Trace = null }; var bytes = RandomCheckpointCodec.Encode(exact, 1); Equal(exact, RandomCheckpointCodec.Decode(bytes, 1));
        Equal(true, Encoding.UTF8.GetString(bytes).Contains("18446744073709551615", StringComparison.Ordinal)); Throws<ArgumentException>(() => RandomCheckpointCodec.Encode(exact, 0)); Throws<ContentException>(() => RandomCheckpointCodec.Decode(bytes, 0));
        Throws<ArgumentException>(() => RandomCheckpointCodec.Encode(exact with { Position = exact.Position with { Stream = ulong.MaxValue } }));
        Throws<ArgumentException>(() => RandomCheckpointCodec.Encode(exact with { Position = exact.Position with { Draws = ulong.MaxValue } }));
        Equal(beforeEncoding, source.Capture());
    }
    private static byte[] CheckpointJsonBytes(JsonNode value) => Encoding.UTF8.GetBytes(value.ToJsonString());
    private static void RandomCheckpointMalformedAdmission()
    {
        var key = new ChoiceKey("test", "one"); var source = new GameplayRandom(new ControlledChoices(new([Choice.Create(key, new SamplingLaw.SecondaryUnit(), ChoiceCode.Secondary(0))])), "checkpoint/context"); source.SecondaryUnit(key);
        var bytes = RandomCheckpointCodec.Encode(source.Capture()); var destination = new GameplayRandom(44, "checkpoint/context"); var unchanged = destination.Capture();
        Action<JsonNode>[] mutations =
        [
            n => n["format"] = "openciv2-random-checkpoint/1", n => n["policy"]!["id"] = "oc2-mt19937-64", n => n["policy"]!["version"] = "2",
            n => n["domain"] = "unknown", n => n["context"] = "bad context", n => n["engineSeed"] = "18446744073709551616", n => n["draws"] = "01", n => n["draws"] = "1",
            n => n["stream"] = "9223372036854775808", n => n["extra"] = true, n => n["trace"]!["limit"] = "0", n => n["trace"]!["limit"] = "4294967296",
            n => n["trace"]!["events"]![0]!["result"]!["f64"] = "8000000000000000", n => n["controlled"]![0]!["law"]!["kind"] = "unknown",
            n => n["controlled"]![0]!["siteUtf16"] = "gggg", n => n["controlled"]![0]!["siteUtf16"] = "000", n => n["controlled"]![0]!["siteUtf16"] = "00FF",
            n => n["controlled"]!.AsArray().Add(n["controlled"]![0]!.DeepClone()), n => n["controlled"]![0]!["code"] = "-1", n => n["controlled"]![0]!["attempt"] = "-1",
            n => n["engineSeed"] = 1, n => n["stream"] = "+1", n => n["draws"] = "-0", n => n["controlled"]![0]!["law"]!["extra"] = "x"
        ];
        foreach (var mutate in mutations)
        {
            var node = JsonNode.Parse(bytes)!; mutate(node); bool rejected = false;
            try { destination.Restore(RandomCheckpointCodec.Decode(CheckpointJsonBytes(node)), 1000); } catch (Exception error) when (error is ContentException or ArgumentException or ChoiceException) { rejected = true; }
            Equal(true, rejected); Equal(unchanged, destination.Capture());
        }
        foreach (byte[] malformed in new[] { bytes[..(bytes.Length / 2)], "{\"format\":\"a\",\"format\":\"b\"}"u8.ToArray(), "{\"x\":\"\\ud800\"}"u8.ToArray(), new byte[] { 0xff } }) Throws<ContentException>(() => RandomCheckpointCodec.Decode(malformed));
    }
    private static void RandomCheckpointResourceLimits()
    {
        var saved = new GameplayRandom(0, "checkpoint/context").Capture(); Throws<ArgumentException>(() => RandomCheckpointCodec.Encode(saved with { Context = new string('a', 4097) }));
        Equal(new string('a', 4096), RandomCheckpointCodec.Decode(RandomCheckpointCodec.Encode(saved with { Context = new string('a', 4096) })).Context);
        var longKey = Choice.Create(new(new string('a', 4097), "call"), new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(0));
        Throws<ArgumentException>(() => RandomCheckpointCodec.Encode(saved with { Controlled = new(new([longKey])), Trace = new(1, new([])) }));
        var choices = new OwnedList<Choice>(Enumerable.Range(0, RandomCheckpointCodec.MaximumRecords + 1).Select(i => Choice.Create(new("test", i.ToString(CultureInfo.InvariantCulture)), new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(0))));
        Throws<ArgumentException>(() => RandomCheckpointCodec.Encode(saved with { Controlled = new(choices), Trace = new(choices.Count, new([])) }));
        var admitted = new OwnedList<Choice>(choices.Take(8192)); var boundary = saved with { Controlled = new(admitted), Trace = new(8192, admitted) }; Equal(boundary, RandomCheckpointCodec.Decode(RandomCheckpointCodec.Encode(boundary, 0), 0));
        var expanded = Choice.Create(new(new string('a', 4096), new string('b', 4096)), new SamplingLaw.PrimaryCode(), ChoiceCode.Primary(0));
        Throws<ArgumentException>(() => RandomCheckpointCodec.Encode(saved with { Controlled = new(new(Enumerable.Repeat(expanded, 600))), Trace = new(600, new([])) }));
        Throws<ContentException>(() => RandomCheckpointCodec.Decode(new byte[ContentJson.MaximumBytes + 1]));
        Throws<ArgumentException>(() => RandomCheckpointCodec.Encode(saved with { PolicyId = "oc2-mt19937-64", PolicyVersion = 2 }));
    }
}
