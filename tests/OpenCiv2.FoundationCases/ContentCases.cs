using System.Text;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Core;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class BundleInput
    {
        public JsonNode Manifest { get; } = ContentJson.Parse(GameplayContent.BuiltinBytes("gameplay.json"), "manifest")!;
        public Dictionary<string, byte[]> Files { get; } = GameplayNames.Components.ToDictionary(name => name + ".json", name => GameplayContent.BuiltinBytes(name + ".json"), StringComparer.Ordinal);
        public List<string> Requests { get; } = [];
        public void Raw(string name, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text); Files[name + ".json"] = bytes;
            Manifest["components"]![name]!["sha256"] = ContentIdentity.Sha256(bytes);
        }
        public void Edit(string name, Action<JsonNode> change)
        {
            var value = ContentJson.Parse(Files[name + ".json"], name)!; change(value); Raw(name, value.ToJsonString());
        }
        public GameplayBundle Load(bool classic = false, bool alias = false) => GameplayContent.Load(Encoding.UTF8.GetBytes(Manifest.ToJsonString()), path =>
        {
            Requests.Add(path); return new(alias ? "same-backing" : path, new(Files[path]));
        }, classic);
        public void Custom() { Manifest["mode"] = "custom"; Manifest["id"] = "authored.variant"; Manifest["revision"] = 2; }
    }
    private static void ContentError(string code, Action action)
    {
        try { action(); } catch (ContentException error) { Equal(code, error.Code); return; }
        throw new InvalidOperationException("Expected content error " + code);
    }
    private static void StrictJsonAdmission()
    {
        foreach (string raw in new[] { "{\"a\":1,\"a\":2}", "{\"a\":1,\"\\u0061\":2}", "{\"nested\":{\"a\":1,\"a\":1}}",
            "{\"value\":\"\\x41\"}", "{\"value\":\"\\ud800\"}", "{\"value\":\"\\udc00\"}", "{\"value\":\"\\ud800\\u0041\"}",
            "{\"\\udc00\":1}", "{\"a\":NaN}", "{\"a\":Infinity}", "{\"a\":01}", "[1,]", "{} {}" })
            ContentError("invalid-json", () => ContentJson.Parse(Encoding.UTF8.GetBytes(raw), "authored"));
        foreach (byte[] invalid in new byte[][] { [0xc0, 0x80], [0xed, 0xa0, 0x80], [0xff] })
            ContentError("invalid-json", () => ContentJson.Parse([.. "{\"a\":\""u8, .. invalid, .. "\"}"u8], "authored"));
        var parsed = ContentJson.Parse("{\"a\":\"\\ud83d\\ude00\",\"b\":\"\\u0000\",\"meta\":{\"same\":1},\"other\":{\"same\":2}}"u8, "authored")!;
        Equal("😀", parsed["a"]!.GetValue<string>()); Equal("\0", parsed["b"]!.GetValue<string>());
        ContentError("resource-limit", () => ContentJson.Parse(Encoding.UTF8.GetBytes(new string('[', 25) + "0" + new string(']', 25)), "deep metadata"));
        ContentError("resource-limit", () => ContentJson.Parse(new byte[ContentJson.MaximumBytes + 1], "large"));
    }
    private static void ExactContentIntegers()
    {
        Equal(int.MaxValue, ContentJson.Integer32("2147483647.000e0", "value")); Equal(int.MinValue, ContentJson.Integer32("-2147483648e0", "value"));
        Equal(1, ContentJson.Integer32("1000e-3", "value")); Equal(0, ContentJson.Integer32("-0e9999999999999999999999", "value"));
        Equal("9007199254740993", ContentJson.IntegerText("9007199254740993", "revision")); Equal("1" + new string('0', 100), ContentJson.IntegerText("1e100", "revision"));
        foreach (string token in new[] { "1.00000000000000000000000001", "0.99999999999999999999999999", "1e-9999", "01", "1.", "1e", "true", "NaN", "-" })
            ContentError("invalid-integer", () => ContentJson.Integer32(token, "value"));
        foreach (string token in new[] { "2147483648", "-2147483649" }) ContentError("integer-range", () => ContentJson.Integer32(token, "value"));
        ContentError("resource-limit", () => ContentJson.IntegerText("1e9999999999999999999999", "value"));
        var parsed = ContentJson.Parse("{\"revision\":9007199254740993,\"value\":1.00000000000000000000000001}"u8, "exact")!;
        Equal("9007199254740993", parsed["revision"]!.ToJsonString()); ContentError("invalid-integer", () => ContentJson.Integer32(parsed["value"]!.ToJsonString(), "value"));
    }
    private static void GameplayTrustedValues()
    {
        var input = new BundleInput(); var bundle = input.Load(true);
        Equal(true, bundle.ClassicValues); Equal(false, bundle.CustomMode); Equal(GameplayContent.ClassicSha256, bundle.SemanticSha256);
        Equal(GameplayContent.Target, bundle.Id); Equal("1", bundle.Revision); Equal(6, input.Requests.Count);
        var v = bundle.Values;
        Equal(-4000, v.Calendar.Profiles[0][0].StartYear); Equal(2000, v.Calendar.WarningYear); Equal(2020, v.Calendar.EndYear);
        Equal(1, v.Spaceship.Parts[0].MassHundredsOfTons); Equal(4, v.Spaceship.Parts[1].MassHundredsOfTons); Equal(16, v.Spaceship.Parts[3].MassHundredsOfTons);
        Equal(39, v.Spaceship.Parts[0].MaximumCount); Equal("advance.monarchy", v.Governments[2].RequiredAdvance!); Equal(true, v.Governments[0].RequiredAdvance is null);
        Equal(10, v.Governments[6].MaximumRateTenths); Equal(15, v.Difficulties[0].AiBoxFactor); Equal(new Rational(1, 2), v.Combat.VeteranBonus);
        Equal(3, v.Combat.FortificationFactor); Equal(6, v.Combat.CityWallsFactor); Equal(-1, v.Scoring.PerUnhappyCitizen);
        Equal(new Rational(5, 4), v.Scoring.Conquest.LargeMapMultiplier); Equal(13, v.Scoring.RatingDifficultyFactors[5]);
        Equal(v, GameplayContent.Builtin.Values);
        foreach (string name in GameplayNames.Components) BytesEqual(GameplayContent.BuiltinBytes(name + ".json"), input.Files[name + ".json"]);
    }
    private static void GameplayCanonicalNormalization()
    {
        var input = new BundleInput(); var baseline = input.Load(); input.Custom();
        input.Edit("scoring", doc =>
        {
            doc["conquest"]!["largeMapMultiplier"]!["numerator"] = 10; doc["conquest"]!["largeMapMultiplier"]!["denominator"] = 8;
            doc["extensions"] = new JsonObject { ["org.example.note"] = "metadata only" };
        });
        input.Raw("combat", Encoding.UTF8.GetString(input.Files["combat.json"]).Replace("\"partisansAgainstZeroAttackMultiplier\": 8", "\"partisansAgainstZeroAttackMultiplier\": 8.000e0", StringComparison.Ordinal));
        var equivalent = input.Load(true); Equal(true, equivalent.CustomMode); Equal(true, equivalent.ClassicValues);
        Equal(baseline.SemanticSha256, equivalent.SemanticSha256); Equal(baseline.Values, equivalent.Values);
        Equal(false, baseline.Files[5].Sha256 == equivalent.Files[5].Sha256);
        input.Manifest["revision"] = ContentJson.Parse("9007199254740993"u8, "revision"); Equal("9007199254740993", input.Load().Revision);
    }
    private static void GameplayCustomGate()
    {
        var input = new BundleInput(); var baseline = input.Load();
        input.Edit("spaceship", doc => doc["parts"]!["structural"]!["massTons"] = 500);
        ContentError("classic-values-required", () => input.Load()); input.Custom();
        var custom = input.Load(); Equal(false, custom.ClassicValues); Equal(5, custom.Values.Spaceship.Parts[0].MassHundredsOfTons);
        ContentError("classic-values-required", () => input.Load(true)); Equal(1, baseline.Values.Spaceship.Parts[0].MassHundredsOfTons);
    }
    private static void GameplayDescriptorAdmission()
    {
        var input = new BundleInput(); input.Manifest["components"]!["scoring"]!["path"] = "../escape.json";
        ContentError("invalid-path", () => input.Load()); Equal(0, input.Requests.Count);
        input = new(); input.Files["calendar.json"] = [.. input.Files["calendar.json"], (byte)' '];
        ContentError("hash-mismatch", () => input.Load()); Equal(1, input.Requests.Count);
        input = new(); ContentError("source-alias", () => input.Load(alias: true)); Equal(2, input.Requests.Count);
        foreach (string path in new[] { "", "/absolute", "C:/drive", "https://server/file", "a\\b", "a//b", "./file", "a/../b", "a/", "a\nfile" })
            ContentError("invalid-path", () => ContentPaths.ValidateRelative(path));
        ContentPaths.ValidateRelative("content café/data.json"); ContentPaths.ValidateRelative("1:file.json");
        input = new(); input.Files.Remove("calendar.json"); ContentError("source-error", () => input.Load());
    }
    private static void GameplayInvalidDocuments()
    {
        (string Name, Action<JsonNode> Change)[] changes = [
            ("calendar", d => d["profiles"]!["prince"]!["periods"]![1]!["startYear"] = 123),
            ("calendar", d => d["profiles"]!["prince"]!["periods"]![0]!["turnCount"] = int.MaxValue),
            ("calendar", d => d["retirement"]!["warningYear"] = d["retirement"]!["endYear"]!.DeepClone()),
            ("spaceship", d => d["parts"]!["fuel"]!["massTons"] = 401),
            ("spaceship", d => d["parts"]!["fuel"]!["group"] = "module"),
            ("governments", d => d["governments"]!["anarchy"]!["requiredAdvance"] = "advance.monarchy"),
            ("difficulty", d => d["difficulties"]!["king"]!["aiBoxFactor"] = false),
            ("combat", d => d["defenseMultipliers"]!["fortress"]!["denominator"] = 3),
            ("combat", d => d["strengthBonuses"]!["veteran"]!["denominator"] = 0),
            ("scoring", d => d["objectiveValue"]!["city"] = -1), ("scoring", d => d["extra"] = 1),
            ("scoring", d => d["extensions"] = new JsonObject { ["invalid"] = 1 }), ("scoring", d => ((JsonObject)d).Remove("peace")) ];
        foreach (var (name, change) in changes) { var input = new BundleInput(); input.Custom(); input.Edit(name, change); ContentError("invalid-content", () => input.Load()); }
        foreach (string number in new[] { "1.00000000000000000000001", "1e-400", "2147483648" })
        {
            var input = new BundleInput(); input.Custom();
            input.Raw("combat", Encoding.UTF8.GetString(input.Files["combat.json"]).Replace("\"partisansAgainstZeroAttackMultiplier\": 8", "\"partisansAgainstZeroAttackMultiplier\": " + number, StringComparison.Ordinal));
            ContentError(number == "2147483648" ? "integer-range" : "invalid-integer", () => input.Load());
        }
    }
}
