using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class CityContentInput
    {
        public RulesInput Rules { get; } = new();
        public JsonObject Preferences { get; } = JsonNode.Parse("""{"format":"openciv2-city-preferences/1","humanCityDefensePriority":"unchanged","buildPreferences":["improvements.authored.2","improvements.authored.1","improvements.authored.2"]}""")!.AsObject();
        public JsonObject Names { get; } = JsonNode.Parse("""{"format":"openciv2-city-names/1","civilizations":[],"fallbackNames":["Fallback One","Fallback Two"]}""")!.AsObject();
        public CityContentInput()
        {
            for (int i = 0; i < 21; ++i) Names["civilizations"]!.AsArray().Add((JsonNode)new JsonObject { ["pluralName"] = "tribes " + i, ["cityNames"] = JsonNode.Parse("""["First","Second"]""") });
        }
        public PackInput Pack()
        {
            var pack = Rules.Pack(); pack.Manifest["files"]!["cityNames"] = pack.Source("names.json", Names.ToJsonString());
            pack.Manifest["files"]!["cityPreferences"] = pack.Source("preferences.json", Preferences.ToJsonString()); return pack;
        }
        public ResolvedPack Resolve(bool overlay = false)
        {
            var pack = Pack(); if (!overlay) return ContentPacks.Resolve(pack.Mount());
            var child = new PackInput("authored.overlay", false); child.Parent(pack);
            child.Manifest["files"]!["cityNames"] = child.Source("names.json", Names.ToJsonString());
            return ContentPacks.Resolve(child.Mount(), [pack.Mount()]);
        }
        public CityPreferenceContent LoadPreferences() => NativeCityContent.LoadPreferences(Resolve(), Rules.Load().Input.Definitions, TextComparison.AsciiNameEqual);
        public CityNameContent LoadNames(bool overlay = false) => NativeCityContent.LoadNames(Resolve(overlay), Rules.Load().Input.Definitions, TextComparison.AsciiNameEqual);
    }
    private static void CityPreferenceUpdates()
    {
        var input = new CityContentInput();
        foreach (bool deprioritize in new[] { false, true })
        {
            input.Preferences["humanCityDefensePriority"] = deprioritize ? "deprioritize" : "unchanged";
            var content = input.LoadPreferences(); Equal(new OwnedList<byte>([2, 1, 2]), content.Update.BuildOrder); Equal("preferences.json", content.Source.Receipt.Path);
            var state = new CityPreferences { Count = 9, NoDefend = 7 }; Array.Fill(state.BuildOrder, 1234); state.Apply(content.Update);
            Equal(3, state.Count); Equal(deprioritize ? 1 : 7, state.NoDefend); Equal(2, state.BuildOrder[0]); Equal(1, state.BuildOrder[1]); Equal(2, state.BuildOrder[2]); Equal(1234, state.BuildOrder[3]);
            state.Apply(new(false, new([]))); Equal(0, state.Count); Equal(deprioritize ? 1 : 7, state.NoDefend); Equal(2, state.BuildOrder[0]); Equal(1234, state.BuildOrder[31]);
            var before = new OwnedList<int>(state.BuildOrder);
            foreach (var update in new[] { new CityPreferenceUpdate(true, new([1, 39])), new(true, new([0])), new(true, new(Enumerable.Repeat((byte)1, 33))) })
            {
                Throws<ArgumentException>(() => state.Apply(update)); Equal(0, state.Count); Equal(deprioritize ? 1 : 7, state.NoDefend); Equal(before, new(state.BuildOrder));
            }
        }
        input.Preferences["buildPreferences"] = new JsonArray(); Equal(0, input.LoadPreferences().Update.BuildOrder.Count);
        input.Preferences["buildPreferences"] = new JsonArray(Enumerable.Repeat("improvements.authored.38", 32).Select(s => (JsonNode)JsonValue.Create(s)!).ToArray());
        Equal(32, input.LoadPreferences().Update.BuildOrder.Count);
        input.Preferences["buildPreferences"]!.AsArray().Add((JsonNode)JsonValue.Create("improvements.authored.1")!); ContentError("invalid-content", () => input.LoadPreferences());
    }
    private static void CityPreferenceAdmission()
    {
        var input = new CityContentInput();
        foreach (string id in new[] { "missing", "improvements.authored.0", "improvements.authored.39" })
        { input.Preferences["buildPreferences"] = new JsonArray(JsonValue.Create(id)); ContentError("invalid-content", () => input.LoadPreferences()); }
        input.Preferences["buildPreferences"] = JsonNode.Parse("""["improvements.authored.2"]"""); input.Rules.Rules["improvements"]![2]!["name"] = "IMPROVEMENTS 1";
        ContentError("invalid-content", () => input.LoadPreferences());
        input.Preferences["buildPreferences"] = JsonNode.Parse("""["improvements.authored.1","improvements.authored.1"]"""); Equal(new OwnedList<byte>([1, 1]), input.LoadPreferences().Update.BuildOrder);
        input.Preferences["buildPreferences"] = JsonNode.Parse("""["improvements.authored.2"]"""); input.Rules.Rules["improvements"]![2]!["name"] = ""; ContentError("invalid-content", () => input.LoadPreferences());
        input.Rules.Rules["improvements"]![1]!["name"] = "Café"; input.Rules.Rules["improvements"]![2]!["name"] = "CafÉ";
        ContentError("comparison-unavailable", () => input.LoadPreferences());
        input = new(); input.Preferences["humanCityDefensePriority"] = "invented"; ContentError("invalid-content", () => input.LoadPreferences());
        input = new(); input.Preferences["unexpected"] = 0; ContentError("invalid-content", () => input.LoadPreferences());
    }
    private static void CityNameCatalogSelection()
    {
        var input = new CityContentInput(); input.Names["civilizations"]![0]!["pluralName"] = "TrIbEs 0";
        var content = input.LoadNames(); Equal(true, content.StartingListsComplete); Equal("names.json", content.Source.Receipt.Path);
        var catalog = content.Catalog;
        string[] expected = ["@TrIbEs 0", "First", "Second", "Fallback One", "Fallback Two"];
        for (byte i = 0; i < expected.Length; ++i)
        { var result = catalog.Select("tribes 0"u8, i); Equal((int)NameOutcome.Selected, (int)result.Outcome); Equal(expected[i], ClassicText.DecodeWindows1252(result.Bytes.ToArray(), "name")); }
        foreach (byte index in new byte[] { 5, 254, 255 }) Equal((int)NameOutcome.Exhausted, (int)catalog.Select("tribes 0"u8, index).Outcome);
        Equal((int)NameOutcome.Unavailable, (int)catalog.Select("missing"u8, 1).Outcome);
        input.Names["civilizations"]![0]!["cityNames"] = JsonNode.Parse("""["Duplicate","Duplicate"]""");
        var duplicates = input.LoadNames().Catalog; BytesEqual(duplicates.Select("tribes 0"u8, 1).Bytes, duplicates.Select("tribes 0"u8, 2).Bytes);
        Equal("First", ClassicText.DecodeWindows1252(catalog.Select("tribes 0"u8, 1).Bytes.ToArray(), "name"));
        var source = content.Catalog.Layers[0]; var first = source.Civilizations[0];
        var incomplete = new NameCatalog([new(new([first with { Names = new([first.Names[0]]), Complete = false }]), source.Fallback)], TextComparison.AsciiNameEqual);
        Equal((int)NameOutcome.Selected, (int)incomplete.Select("tribes 0"u8, 1).Outcome);
        Throws<ArgumentException>(() => incomplete.Select("tribes 0"u8, 2));
    }
    private static void CityNameLayerOwnership()
    {
        static RuleText Text(string value) => new(value, new(ClassicText.EncodeWindows1252(value, "fixture")));
        var first = new CityNameLists(new([new(Text("Scenario"), new([Text("Scenario one")]))]), null);
        var second = new CityNameLists(new([new(Text("Scenario"), new([Text("Wrong override")])), new(Text("Home only"), new([Text("Home")]))]), new([Text("Home extra")]));
        var layers = new[] { first, second }; var catalog = new NameCatalog(layers, TextComparison.AsciiNameEqual); layers[0] = second;
        Equal("Scenario one", ClassicText.DecodeWindows1252(catalog.Select("Scenario"u8, 1).Bytes.ToArray(), "name"));
        Equal((int)NameOutcome.Exhausted, (int)catalog.Select("Scenario"u8, 2).Outcome);
        Equal("Home extra", ClassicText.DecodeWindows1252(catalog.Select("Home only"u8, 2).Bytes.ToArray(), "name"));
        var input = new CityContentInput(); input.Names["civilizations"] = new JsonArray(); input.Names["fallbackNames"] = new JsonArray();
        ContentError("invalid-content", () => input.LoadNames()); var overlay = input.LoadNames(true);
        Equal(false, overlay.StartingListsComplete); Equal("authored.overlay", overlay.Source.Owner.Id); Equal(0, overlay.Catalog.Layers[0].Civilizations.Count);
        Equal(true, overlay.Catalog.Layers[0].Fallback is not null); Equal(0, overlay.Catalog.Layers[0].Fallback!.Count);
        input.Names["fallbackNames"] = null; Equal(true, input.LoadNames(true).Catalog.Layers[0].Fallback is null);
    }
    private static void CityNameAdmission()
    {
        Action<CityContentInput>[] changes = [i => i.Names["civilizations"]![1]!["pluralName"] = "TRIBES 0",
            i => i.Names["civilizations"]![0]!["cityNames"] = new JsonArray(), i => i.Names["civilizations"]!.AsArray().RemoveAt(0),
            i => i.Names["extra"] = 0];
        foreach (var change in changes) { var input = new CityContentInput(); change(input); ContentError("invalid-content", () => input.LoadNames()); }
        foreach (string invalid in new[] { "", "@sToP and more", " padded", "padded\t", "Two\nLines", "Null\0Byte", "EOF\u001a", new string('x', 255) })
        { var input = new CityContentInput(); input.Names["fallbackNames"]![0] = invalid; ContentError("invalid-content", () => input.LoadNames()); }
        var text = new CityContentInput(); text.Names["fallbackNames"]![0] = "Unrepresentable ☃"; ContentError("text-encoding", () => text.LoadNames());
        text = new(); text.Names["civilizations"]![0]!["pluralName"] = "Café"; text.Names["civilizations"]![1]!["pluralName"] = "CAFÉ"; ContentError("comparison-unavailable", () => text.LoadNames());
        text = new(); text.Names["fallbackNames"]![0] = new string('x', 254); Equal(254, text.LoadNames().Catalog.Layers[0].Fallback![0].Classic.Count);
        Throws<ArgumentNullException>(() => new NameCatalog([], null!));
    }
}
