using System.Globalization;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Base.Randomness;

namespace OpenCiv2.Conformance;

public static partial class Adapter
{
    private static JsonNode ChoiceResultJson(ChoiceResult result) => result.IsInteger ? S("i32", result.AsInteger()) : O(("$type", "f64"), ("bits", unchecked((ulong)result.Bits).ToString("x16", CultureInfo.InvariantCulture)));
    private static string LawName(SamplingLaw law) => law switch { SamplingLaw.PrimaryCode => "primary-code", SamplingLaw.PrimaryModulo => "primary-modulo", SamplingLaw.SecondaryUnit => "secondary-unit", SamplingLaw.SecondaryRange => "secondary-range", _ => throw new ArgumentException("Unknown law") };
    private static JsonObject LawArguments(SamplingLaw law) => law switch { SamplingLaw.PrimaryModulo m => O(("n", S("i32", m.N))), SamplingLaw.SecondaryRange r => O(("lo", S("i32", r.Lo)), ("hi", S("i32", r.Hi))), _ => new() };
    private static ControlledChoices Choices(Input input)
    {
        int count = input.Count(100000); var choices = new Choice[count];
        for (int i = 0; i < count; ++i)
        {
            var key = new ChoiceKey(TextInput(input), TextInput(input), input.Read<int>()); uint kind = input.Read<uint>();
            SamplingLaw law = kind switch { 0 => new SamplingLaw.PrimaryCode(), 1 => new SamplingLaw.PrimaryModulo(input.Read<int>()), 2 => new SamplingLaw.SecondaryUnit(), 3 => new SamplingLaw.SecondaryRange(input.Read<int>(), input.Read<int>()), _ => throw new ArgumentException("Unknown sampling law") };
            var code = kind < 2 ? ChoiceCode.Primary(input.Read<int>()) : ChoiceCode.Secondary(input.Read<uint>()); choices[i] = Choice.Create(key, law, code);
        }
        return new(new(choices));
    }
    private static JsonNode Random(IReadOnlyList<string> args, Input input)
    {
        string operation = args[0]; int count = operation == "F01.primaryModulo" ? 3 : operation == "F01.secondaryRange" ? 4 : 2; Require(args.Count == count, "Wrong sampler argument count");
        SamplingLaw law = operation switch { "F01.primaryCode" => new SamplingLaw.PrimaryCode(), "F01.primaryModulo" => new SamplingLaw.PrimaryModulo(Number<int>(args[2])), "F01.secondaryUnit" => new SamplingLaw.SecondaryUnit(), _ => new SamplingLaw.SecondaryRange(Number<int>(args[2]), Number<int>(args[3])) };
        string mode = input.Word(); uint domain = input.Read<uint>(); string[] domains = ["gameplay", "presentation-content", "cosmetic"];
        Require(mode is "controlled-choices" or "implementation-replay" && domain < 3 && args[1] == domains[domain], "Unknown/inconsistent sampler mode/domain"); bool replay = mode == "implementation-replay";
        ulong seed = replay ? input.Read<ulong>() : 0, draws = replay ? input.Read<ulong>() : 0; ControlledChoices? tape = replay ? null : Choices(input); input.End();
        RandomContext random = domain switch
        {
            0 => tape is null ? new GameplayRandom(seed, "fixture") : new GameplayRandom(tape, "fixture"),
            1 => tape is null ? new PresentationRandom(seed, "fixture") : new PresentationRandom(tape, "fixture"),
            _ => tape is null ? new CosmeticRandom(seed, "fixture") : new CosmeticRandom(tape, "fixture")
        };
        if (replay) { var initial = random.Capture(); random.Restore(initial with { Position = initial.Position with { Draws = draws } }, 1_000_000); random.RecordChoices(1); }
        var arguments = LawArguments(law); arguments["domain"] = T(domains[domain]); var trace = new JsonArray(Entry(operation, arguments)); var key = new ChoiceKey(operation, "root");
        ChoiceResult value = law switch { SamplingLaw.PrimaryCode => ChoiceResult.Integer(random.PrimaryCode(key)), SamplingLaw.PrimaryModulo m => ChoiceResult.Integer(random.PrimaryModulo(key, m.N)), SamplingLaw.SecondaryUnit => ChoiceResult.Binary64(random.SecondaryUnit(key)), SamplingLaw.SecondaryRange r => ChoiceResult.Integer(random.SecondaryRange(key, r.Lo, r.Hi)), _ => throw new ArgumentException("Unknown law") };
        random.RequireChoicesConsumed(); var final = random.Capture(); string context = domains[domain] + "/fixture"; var state = new JsonObject();
        if (replay) state["F01.randomContexts"] = O((context, O(("domain", T(domains[domain])), ("name", T("fixture")), ("seed", S("u64", seed)), ("draws", S("u64", final.Position.Draws)))));
        foreach (var choice in final.Trace!.Events)
        {
            var payload = O(("site", choice.Key.Site), ("attempt", choice.Key.Attempt), ("law", LawName(choice.Law)), ("arguments", LawArguments(choice.Law)), ("code", choice.Law is SamplingLaw.PrimaryCode or SamplingLaw.PrimaryModulo ? S("i32", (int)choice.Code.Bits) : S("u32", choice.Code.Bits)), ("result", ChoiceResultJson(choice.Result)));
            if (replay) payload["context"] = T(context); trace.Add((JsonNode)Event(trace.Count, "root.sample", "random", "root", payload));
        }
        var result = ChoiceResultJson(value); trace.Add((JsonNode)Event(trace.Count, "root.return", "return", "root", O(("result", result)))); trace.Add((JsonNode)Event(trace.Count, "root.exit", "snapshot", "root", O(("label", "exit"), ("state", state))));
        return O(("outcome", "return"), ("result", result), ("state", state), ("trace", trace));
    }
}
