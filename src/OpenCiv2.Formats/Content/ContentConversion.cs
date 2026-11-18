using System.Text.Json;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Core.City;
using OpenCiv2.Formats.Classic;
using ClassicReader = OpenCiv2.Formats.Classic.TextReader;

namespace OpenCiv2.Formats.Content;

public sealed record ContentConversion(OwnedList<byte> Component, JsonElement Receipt);
public sealed record RuleConversion(OwnedList<byte> Rules, OwnedList<byte> Compatibility, JsonElement Receipt);

/// <summary>M1-02 explicit component conversions. No filesystem access or publication.</summary>
public static partial class ContentConversions
{
    private static void Admit(ContentSource source)
    {
        if (source.Identity.Length == 0 || source.Bytes.Count > 16 * 1024 * 1024)
            throw new ContentException("source-error", source.Identity, "conversion requires an identified source of at most 16 MiB");
    }
    private static byte[] Serialize(JsonNode node) => [.. ContentJson.CanonicalBytes(node), (byte)'\n'];
    private static JsonElement Freeze(JsonNode node) { using var document = JsonDocument.Parse(node.ToJsonString()); return document.RootElement.Clone(); }
    private static JsonArray Strings(IEnumerable<string> values) => new(values.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray());
    private static JsonObject SourceReceipt(ContentSource source) => new() { ["identity"] = source.Identity, ["sha256"] = ContentIdentity.Sha256(source.Bytes.ToArray()) };
    private static NameComparison Names(TextCase comparison) => (a, b) =>
    {
        try { return comparison.Equal(a, b); }
        catch (TextException error) when (error.Fault == TextFault.ComparisonUnavailable) { throw new ComparisonUnavailableException(); }
    };
    private static ContentException TextError(TextException error, string location) =>
        new(error.Fault == TextFault.ComparisonUnavailable ? "comparison-unavailable" : "unsupported-classic-text", location, error.Message);
    private static ClassicReader Reader(ContentSource source, TextCase comparison) => new(new(), new("", "", ""), new((_, _) => new(source.Identity, new MemoryTextStream(source.Bytes.ToArray()))), comparison);
    private static ResolvedDocument Document(string path, byte[] bytes)
    {
        string hash = ContentIdentity.Sha256(bytes); var owner = new PackIdentity("conversion.output", "1", hash);
        return new(new(owner, new(path, path, hash), new(bytes)), ContentJson.Parse(bytes, path)!);
    }
    private static JsonObject ComponentReceipt(ContentSource source, ResolvedPack selected, RuleContent rules, byte[] output, string conversion, string format, JsonArray unsupported, IEnumerable<string> limitations)
    {
        var dependencies = new JsonArray();
        void Dependency(string role, ResolvedFile? file)
        {
            if (file is null) return;
            dependencies.Add((JsonNode)new JsonObject
            {
                ["role"] = role,
                ["identity"] = file.Receipt.Identity,
                ["path"] = file.Receipt.Path,
                ["sha256"] = file.Receipt.Sha256,
                ["owner"] = new JsonObject { ["id"] = file.Owner.Id, ["revision"] = file.Owner.Revision, ["sha256"] = file.Owner.Sha256 }
            });
        }
        Dependency("rules", rules.RulesSource); Dependency("compatibility", rules.CompatibilitySource);
        return new()
        {
            ["format"] = "openciv2-content-conversion/1",
            ["conversion"] = conversion,
            ["profile"] = selected.Profile,
            ["source"] = SourceReceipt(source),
            ["output"] = new JsonObject { ["format"] = format, ["sha256"] = ContentIdentity.Sha256(output) },
            ["dependencies"] = dependencies,
            ["gameplaySemanticSha256"] = selected.Gameplay.Bundle.SemanticSha256,
            ["unsupportedFields"] = unsupported,
            ["lossyFields"] = new JsonArray(),
            ["limitations"] = Strings(limitations)
        };
    }
    public static ContentConversion CityPreferences(ContentSource source, ResolvedPack selected, TextCase comparison)
    {
        Admit(source); var rules = NativeRules.Load(selected); ClassicText.DecodeWindows1252(source.Bytes.ToArray(), source.Identity);
        using var reader = Reader(source, comparison); CityPreferenceUpdate parsed;
        try
        {
            parsed = ClassicCityContent.LoadPreferences(reader, rules.Input.Definitions, comparison); reader.Close();
            // Loading stops at a miss; conversion diagnoses an ordinary unresolved entry.
            if (reader.Open("CITYPREF.TXT", "AUTOBUILD"u8.ToArray()))
                for (int i = 0; i < 32 && reader.NextLine(); ++i)
                {
                    if (!reader.Line.IsEmpty && reader.Line[0] == '@') break;
                    byte[] token = reader.NextToken(); if (token.Length == 0 || token[0] == ';') break;
                    bool found = false;
                    for (int slot = 1; slot <= 38; ++slot)
                        if (comparison.Equal(token, rules.Input.Definitions.Improvements[slot].Identity.Name.Classic.ToArray())) { found = true; break; }
                    if (!found) throw new ContentException("unresolved-classic-reference", source.Identity, "AUTOBUILD entry does not resolve to improvement slot 1..38");
                }
            reader.Close();
        }
        catch (TextException error) { throw TextError(error, source.Identity); }
        var component = new JsonObject
        {
            ["format"] = "openciv2-city-preferences/1",
            ["humanCityDefensePriority"] = parsed.DeprioritizeDefense ? "deprioritize" : "unchanged",
            ["buildPreferences"] = Strings(parsed.BuildOrder.Select(slot => rules.Input.Definitions.Improvements[slot].Identity.Id))
        };
        byte[] output = Serialize(component); var verification = selected with { Components = selected.Components.SetItem("cityPreferences", Document("converted-city-preferences", output)) };
        var native = NativeCityContent.LoadPreferences(verification, rules.Input.Definitions, Names(comparison));
        if (native.Update != parsed) throw new InvalidOperationException("Converted preference semantics disagree with classic input");
        var receipt = ComponentReceipt(source, selected, rules, output, "classic-city-preferences/1", "openciv2-city-preferences/1", new(), [
            "Component output, not a complete playable pack.", "Semantic conversion preserves the first 32 AUTOBUILD entries; classic comments, formatting and unused sections are not exported.",
            "ASCII folding and exact byte equality only; non-ASCII comparison equivalence is not established.", "Absent NODEFEND preserves the caller's prior NoDefend value."]);
        return new(new(output), Freeze(receipt));
    }
    public static ContentConversion CityNames(ContentSource source, ResolvedPack selected, TextCase comparison)
    {
        Admit(source); var rules = NativeRules.Load(selected); ClassicText.DecodeWindows1252(source.Bytes.ToArray(), source.Identity);
        using var reader = Reader(source, comparison); var civilizations = new JsonArray(); var unsupported = new JsonArray();
        var component = new JsonObject { ["format"] = "openciv2-city-names/1", ["civilizations"] = civilizations, ["fallbackNames"] = null };
        try
        {
            reader.OpenLow("CITY", null); var parsed = ClassicCityContent.ReadNames(reader, comparison); var selectors = new List<byte[]>();
            foreach (var list in parsed.Civilizations)
            {
                byte[] selector = list.Plural.Classic.ToArray(); bool stop = selector.Length >= 4 && comparison.Equal(selector.AsSpan(0, 4), "STOP"u8);
                if (selectors.Any(previous => comparison.Equal(previous, selector)))
                {
                    if (stop) continue;
                    throw new ContentException("ambiguous-classic-selector", source.Identity, "CITY contains repeated section selectors");
                }
                selectors.Add(selector); var names = Strings(list.Names.Select(n => n.Unicode)); bool extra = comparison.Equal(selector, "EXTRA"u8);
                if (extra) component["fallbackNames"] = names.DeepClone();
                if (!list.Complete)
                {
                    if (stop || extra) { unsupported.Add((JsonNode)JsonValue.Create("Exhausted-stream custom plural selector: " + list.Plural.Unicode)!); continue; }
                    throw new ContentException("unsupported-classic-text", source.Identity, "CITY list reaches EOF without a blank or STOP terminator; classic exhausted-stream behavior is not a native list");
                }
                civilizations.Add((JsonNode)new JsonObject { ["pluralName"] = list.Plural.Unicode, ["cityNames"] = names });
            }
        }
        catch (TextException error) { throw TextError(error, source.Identity); }
        catch (ArgumentException error) { throw new ContentException("unsupported-classic-text", source.Identity, error.Message); }
        byte[] output = Serialize(component); var verification = selected with { Components = selected.Components.SetItem("cityNames", Document("converted-city-names", output)) };
        NativeCityContent.LoadNames(verification, rules.Input.Definitions, Names(comparison));
        var receipt = ComponentReceipt(source, selected, rules, output, "classic-city-names/1", "openciv2-city-names/1", unsupported, [
            "Component output from one explicit CITY source; directory fallback is not inferred.", "Repeated structural STOP selectors use the first occurrence; any unrepresentable terminal-marker selector is reported.",
            "ASCII folding and exact byte equality only; non-ASCII comparison equivalence is not established."]);
        return new(new(output), Freeze(receipt));
    }
}
