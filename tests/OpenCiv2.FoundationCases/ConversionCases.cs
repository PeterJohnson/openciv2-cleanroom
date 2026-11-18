using System.Text.Json;
using System.Text.Json.Nodes;
using OpenCiv2.Base;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Formats.Content;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // M1-02 / F03 §8 / C01: authored input, retained effects and all byte cursor values.
    // These authored conversion vectors are regression fixtures, not an original-game oracle.
    private static ContentSource ConversionInput(string text) => new("authored/classic.txt", new(ByteText(text)));
    private static RuleContent ConvertedRules(RuleConversion converted)
    {
        var pack = new RulesInput().Pack();
        pack.Manifest["files"]!["rules"] = pack.Source("converted-rules.json", converted.Rules.ToArray());
        pack.Manifest["compatibility"] = pack.Source("converted-mapping.json", converted.Compatibility.ToArray());
        return NativeRules.Load(ContentPacks.Resolve(pack.Mount()));
    }
    private static ResolvedPack ConvertedCity(ContentConversion converted, CityContentInput input, bool names, bool overlay = false)
    {
        string key = names ? "cityNames" : "cityPreferences";
        var pack = input.Pack();
        if (!overlay) { pack.Manifest["files"]![key] = pack.Source("converted.json", converted.Component.ToArray()); return ContentPacks.Resolve(pack.Mount()); }
        var child = new PackInput("converted.overlay", false); child.Parent(pack);
        child.Manifest["files"]![key] = child.Source("converted.json", converted.Component.ToArray());
        return ContentPacks.Resolve(child.Mount(), [pack.Mount()]);
    }
    private static CityPreferenceUpdate ConvertedPreferences(ContentConversion converted, CityContentInput input) =>
        NativeCityContent.LoadPreferences(ConvertedCity(converted, input, false), input.Rules.Load().Input.Definitions, TextComparison.AsciiNameEqual).Update;
    private static void ConversionHash(JsonElement receipt, string role, IEnumerable<byte> bytes) => Equal(ContentIdentity.Sha256(bytes.ToArray()), receipt.GetProperty(role).GetProperty("sha256").GetString()!);
    private static void ConversionRulesComplete()
    {
        var source = ConversionInput(string.Concat(ClassicRuleSections())); var converted = ContentConversions.Rules(source, TextCase.Ascii);
        var actual = Installed(ConvertedRules(converted).Input).Definitions; var expected = Installed(new RulesInput().Load().Input).Definitions;
        for (int i = 0; i < 100; ++i) expected.Advances[i] = expected.Advances[i] with { Identity = expected.Advances[i].Identity with { Id = "classic.advances." + i } };
        for (int i = 0; i < 67; ++i) expected.Improvements[i] = expected.Improvements[i] with { Identity = expected.Improvements[i].Identity with { Id = "classic.improvements." + i } };
        for (int i = 0; i < 62; ++i) expected.Units[i] = expected.Units[i] with { Identity = expected.Units[i].Identity with { Id = "classic.units." + i } };
        for (int i = 0; i < 33; ++i) expected.Terrain[i] = expected.Terrain[i] with { Identity = expected.Terrain[i].Identity with { Id = "classic.terrain." + i } };
        for (int i = 0; i < 7; ++i) expected.Governments[i] = expected.Governments[i] with { Identity = expected.Governments[i].Identity with { Id = "classic.governments." + i } };
        for (int i = 0; i < 21; ++i) expected.Tribes[i] = expected.Tribes[i] with { Id = "classic.tribes." + i };
        for (int i = 0; i < 12; ++i) expected.Orders[i] = expected.Orders[i] with { Identity = expected.Orders[i].Identity with { Id = "classic.orders." + i } };
        for (int i = 0; i < 16; ++i) expected.Commodities[i] = expected.Commodities[i] with { Id = "classic.commodities." + i };
        for (int i = 0; i < 6; ++i) expected.Difficulties[i] = expected.Difficulties[i] with { Id = "classic.difficulties." + i };
        for (int i = 0; i < 9; ++i) expected.DiplomaticAttitudes[i] = expected.DiplomaticAttitudes[i] with { Id = "classic.diplomaticattitudes." + i };
        Equal(expected.Snapshot(), actual.Snapshot()); Equal("classic-rules/1", converted.Receipt.GetProperty("conversion").GetString()!);
        ConversionHash(converted.Receipt, "source", source.Bytes); ConversionHash(converted.Receipt.GetProperty("outputs"), "rules", converted.Rules); ConversionHash(converted.Receipt.GetProperty("outputs"), "compatibility", converted.Compatibility);
        var mapping = JsonNode.Parse(converted.Compatibility.ToArray())!; Equal(1, mapping["ruleSlots"]!["orders"]![0]!["slot"]!.GetValue<int>()); Equal(12, mapping["ruleSlots"]!["orders"]![11]!["slot"]!.GetValue<int>());
    }
    private static void ConversionRulesReload()
    {
        var sections = ClassicRuleSections();
        sections[1] = "@CIVILIZE\nDisabled,3,1,no,nil,0,3\nDependent,257,1,AFl,nil,0,3\n; stop\n";
        sections[2] = "@IMPROVE\nCaf\xe9,266,257,AFl\n" + RepeatLines("Building,10,1,nil", 66);
        sections[4] = "@UNITS\nWrapped,no,0,259,0,1,1,3,1,10,0,0,nil,1000000000000001\n; stop\n";
        sections[7] = "@LEADERS\n" + RepeatLines("Leader,Leader,1,1,0,Plural,Example,0,0,0,1,First,Lady,1,Last,Last Lady", 21);
        var input = ConvertedRules(ContentConversions.Rules(ConversionInput(string.Concat(sections)), TextCase.Ascii)).Input;
        Equal(259, input.MovementPoints[0]); Equal(0, (int)input.Definitions.Advances[1].Prerequisites[0]); Equal(0, (int)input.Definitions.Improvements[0].Prerequisite);
        Equal("Café", input.Definitions.Improvements[0].Identity.Name.Unicode); Equal("Caf\xe9", ByteString(input.Definitions.Improvements[0].Identity.Name.Classic));
        Equal((byte)10, input.Definitions.Improvements[0].CostRows); Equal(0x8001U, input.Definitions.Units[0].Abilities); Equal("Unit 2", input.Definitions.Units[1].Identity.Name.Unicode);
        Equal(2, input.TitleOverrides[0].Count); Equal("First", input.TitleOverrides[0][0].Titles.Male.Unicode); Equal("Last", input.TitleOverrides[0][1].Titles.Male.Unicode);
        var retained = Installed(input); var values = retained.Definitions; var loader = new RuleInstaller(input, retained);
        Equal((byte)9, values.Units[0].Movement); Equal(-2, (int)values.Advances[1].Prerequisites[0]); Equal(-2, (int)values.Improvements[0].Prerequisite);
        var kept = new RuleText("Kept", new("Kept"u8.ToArray())); values.Units[0] = values.Units[0] with { Identity = values.Units[0].Identity with { Name = kept } };
        values.Improvements[0] = values.Improvements[0] with { Identity = values.Improvements[0].Identity with { Name = kept } };
        values.Advances[1] = values.Advances[1] with { Identity = values.Advances[1].Identity with { Name = kept } }; loader.Advances(true);
        values.Advances[0] = values.Advances[0] with { Enabled = true }; var cosmic = values.Cosmic.Values.ToArray(); cosmic[0] = 5; values.Cosmic = new(new(cosmic));
        values.Governments[2] = values.Governments[2] with { Titles = values.Governments[2].Titles with { Male = kept } };
        var bindings = new OwnedList<uint>(retained.Names.Units); loader.Improvements(true); loader.Units(true); loader.Leaders(true);
        Equal((byte)15, values.Units[0].Movement); Equal((byte)30, values.Units[0].HitPoints); Equal(0, (int)values.Improvements[0].Prerequisite);
        Equal(kept, values.Units[0].Identity.Name); Equal(kept, values.Improvements[0].Identity.Name); Equal(kept, values.Advances[1].Identity.Name); Equal(kept, values.Tribes[0].Titles[2].Male); Equal(bindings, new(retained.Names.Units));
    }
    private static void ConversionRulesRejection()
    {
        foreach (var (row, code) in new[] { ("Unit,no,3,2,0,1,1,3,1,10,0,0,nil,0", "unsupported-classic-value"), ("Unit,no,0,2,0,1,1,26,1,10,0,0,nil,0", "invalid-content"), ("Unit,unknown,0,2,0,1,1,3,1,10,0,0,nil,0", "invalid-classic-rules") })
        {
            var sections = ClassicRuleSections(); sections[4] = "@UNITS\n" + row + "\n; stop\n";
            ContentError(code, () => ContentConversions.Rules(ConversionInput(string.Concat(sections)), TextCase.Ascii));
        }
        ContentError("unsupported-classic-text", () => ContentConversions.Rules(ConversionInput("@COSMIC\n"), TextCase.Ascii));
        ContentError("source-error", () => ContentConversions.Rules(new("", new([])), TextCase.Ascii));
    }
    private static void ConversionNamesCursors()
    {
        var input = new CityContentInput(); var selected = input.Resolve(); var definitions = input.Rules.Load().Input.Definitions;
        for (int fallback = 0; fallback < 5; ++fallback)
        {
            bool populated = fallback is 2 or 3; string text = "";
            for (int i = 0; i < 21; ++i) text += $"@tribes {i}\r\nFirst\r\nFirst\r\nCaf\xe9\r\n@STOP\r\n";
            if (fallback != 0) text += "@EXTRA\n" + (populated ? "Extra One\nExtra Two\n" : "") + (fallback < 3 ? "@STOP\n" : "");
            var source = ConversionInput(text); var converted = ContentConversions.CityNames(source, selected, TextCase.Ascii);
            var native = NativeCityContent.LoadNames(ConvertedCity(converted, input, true), definitions, TextComparison.AsciiNameEqual).Catalog;
            var f = new ClassicTextInput(); f.Files["scenario/CITY.TXT"] = text; using var reader = f.Reader(); var classic = ClassicCityContent.LoadNames(reader, f.Directories, TextCase.Ascii);
            Equal(fallback != 0, native.Layers[0].Fallback is not null);
            for (int tribe = 0; tribe < 21; ++tribe)
                for (int cursor = 0; cursor < 256; ++cursor)
                {
                    string plural = "tribes " + tribe; var actual = native.Select(ByteText(plural), (byte)cursor); Equal(classic.Select(ByteText(plural), (byte)cursor), actual);
                    string expected = cursor == 0 ? "@" + plural : cursor <= 2 ? "First" : cursor == 3 ? "Caf\xe9" : populated && cursor == 4 ? "Extra One" : populated && cursor == 5 ? "Extra Two" : "";
                    Equal(expected, ByteString(actual.Bytes));
                }
            foreach (var list in native.Layers[0].Civilizations.Where(v => v.Plural.Unicode is "STOP" or "EXTRA"))
                for (int cursor = 0; cursor < 256; ++cursor) Equal(classic.Select(list.Plural.Classic.ToArray(), (byte)cursor), native.Select(list.Plural.Classic.ToArray(), (byte)cursor));
            Equal((int)NameOutcome.Unavailable, (int)native.Select("Missing"u8, 1).Outcome);
            ConversionHash(converted.Receipt, "source", source.Bytes); ConversionHash(converted.Receipt, "output", converted.Component);
            Equal("classic-city-names/1", converted.Receipt.GetProperty("conversion").GetString()!);
        }
    }
    private static void ConversionNamesAdmission()
    {
        var input = new CityContentInput(); var selected = input.Resolve(true);
        foreach (var (text, code) in new[] { ("@Custom\nOne\n@STOP\n@CUSTOM\nTwo\n@STOP\n", "ambiguous-classic-selector"), ("@Custom\nOne\n", "unsupported-classic-text"), ("@Custom\n\x81\n@STOP\n", "text-encoding"), ("@Custom\nOne\n@STOP\n@EXTRA\nOne\n@STOP\n@extra\nTwo\n@STOP\n", "ambiguous-classic-selector"), ("@Custom\n\0bad\n", "unsupported-classic-text") })
            ContentError(code, () => ContentConversions.CityNames(ConversionInput(text), selected, TextCase.Ascii));
        ContentError("invalid-content", () => ContentConversions.CityNames(ConversionInput("@Custom\nOne\n@STOP\n"), input.Resolve(), TextCase.Ascii));
        var converted = ContentConversions.CityNames(ConversionInput("@Custom\nOne\n@Other\nTwo\n@STOP\n"), selected, TextCase.Ascii);
        var json = JsonNode.Parse(converted.Component.ToArray())!; Equal(3, json["civilizations"]![0]!["cityNames"]!.AsArray().Count); Equal("@Other", json["civilizations"]![0]!["cityNames"]![1]!.GetValue<string>());
    }
    private static void ConversionNamesWindows()
    {
        var input = new CityContentInput(); var selected = input.Resolve(true); string text = "@Caf\xe9\r\n" + new string('x', 255) + "\r\n@STOP\r\n";
        var converted = ContentConversions.CityNames(ConversionInput(text), selected, TextCase.Ascii);
        var catalog = NativeCityContent.LoadNames(ConvertedCity(converted, input, true, true), input.Rules.Load().Input.Definitions, TextComparison.AsciiNameEqual).Catalog;
        Equal(1, catalog.Layers[0].Civilizations.Count); var list = catalog.Layers[0].Civilizations[0];
        Equal("Caf\xe9", ByteString(list.Plural.Classic)); Equal(2, list.Names.Count); Equal(new string('x', 254), ByteString(list.Names[0].Classic)); Equal("x", ByteString(list.Names[1].Classic));
        ContentError("comparison-unavailable", () => ContentConversions.CityNames(ConversionInput(text + "@CAF\xc9\nTwo\n@STOP\n"), selected, TextCase.Ascii));
    }
    private static void ConversionPreferenceEffects()
    {
        var input = new CityContentInput(); var selected = input.Resolve();
        foreach (bool flag in new[] { false, true })
        {
            string text = (flag ? "@NODEFEND\r\n" : "") + "@AUTOBUILD\r\nImPrOvEmEnTs 2,ignored\r\nimprovements 1\r\nimprovements 2\r\n";
            var source = ConversionInput(text); var before = source.Bytes; var converted = ContentConversions.CityPreferences(source, selected, TextCase.Ascii); var update = ConvertedPreferences(converted, input);
            Equal(new OwnedList<byte>([2, 1, 2]), update.BuildOrder); Equal(flag, update.DeprioritizeDefense);
            var f = new ClassicTextInput(); f.Files["scenario/CITYPREF.TXT"] = text; using var reader = f.Reader(); var classic = ClassicCityContent.LoadPreferences(reader, input.Rules.Load().Input.Definitions, TextCase.Ascii);
            foreach (int retained in new[] { 0, 7 })
            {
                var actual = new CityPreferences { NoDefend = retained }; var expected = new CityPreferences { NoDefend = retained }; Array.Fill(actual.BuildOrder, 97); Array.Fill(expected.BuildOrder, 97);
                actual.Apply(update); expected.Apply(classic); Equal(expected.Count, actual.Count); Equal(flag ? 1 : retained, actual.NoDefend); Equal(expected.NoDefend, actual.NoDefend); Equal(new OwnedList<int>(expected.BuildOrder), new(actual.BuildOrder)); Equal(97, actual.BuildOrder[31]);
            }
            ConversionHash(converted.Receipt, "source", source.Bytes); ConversionHash(converted.Receipt, "output", converted.Component); Equal(before, source.Bytes);
            Equal(0, converted.Receipt.GetProperty("unsupportedFields").GetArrayLength()); Equal(0, converted.Receipt.GetProperty("lossyFields").GetArrayLength());
        }
    }
    private static void ConversionPreferenceLimits()
    {
        var input = new CityContentInput(); input.Rules.Rules["improvements"]![2]!["name"] = "improvements 1"; var selected = input.Resolve();
        CityPreferenceUpdate Convert(string text) => ConvertedPreferences(ContentConversions.CityPreferences(ConversionInput(text), selected, TextCase.Ascii), input);
        Equal(new OwnedList<byte>([1, 1]), Convert("@AUTOBUILD\nimprovements 1\nimprovements 1\n").BuildOrder);
        Equal(new OwnedList<byte>(Enumerable.Repeat((byte)1, 32)), Convert("@AUTOBUILD\n" + RepeatLines("improvements 1", 32) + "ignored after limit\n").BuildOrder);
        Equal(new CityPreferenceUpdate(false, new([])), Convert("; no sections\n")); Equal(new CityPreferenceUpdate(true, new([])), Convert("@NODEFEND\n"));
        foreach (string ending in new[] { "\n", "; comment\n", "@NEXT\n" }) Equal(new OwnedList<byte>([1]), Convert("@AUTOBUILD\nimprovements 1\n" + ending + "improvements 1\n").BuildOrder);
    }
    private static void ConversionPreferenceText()
    {
        var input = new CityContentInput(); input.Rules.Rules["improvements"]![1]!["name"] = "Café"; var selected = input.Resolve();
        var converted = ContentConversions.CityPreferences(ConversionInput("@AUTOBUILD\nCaf\xe9\n"), selected, TextCase.Ascii); Equal(new OwnedList<byte>([1]), ConvertedPreferences(converted, input).BuildOrder);
        ContentError("comparison-unavailable", () => ContentConversions.CityPreferences(ConversionInput("@AUTOBUILD\nCAF\xc9\n"), selected, TextCase.Ascii));
    }
    private static void ConversionPreferenceRejection()
    {
        var input = new CityContentInput(); var selected = input.Resolve(); string bad = "@AUTOBUILD\nimprovements 1\nmisspelled reference\n";
        var f = new ClassicTextInput(); f.Files["scenario/CITYPREF.TXT"] = bad; using var reader = f.Reader(); Equal(1, ClassicCityContent.LoadPreferences(reader, input.Rules.Load().Input.Definitions, TextCase.Ascii).BuildOrder.Count);
        ContentError("unresolved-classic-reference", () => ContentConversions.CityPreferences(ConversionInput(bad), selected, TextCase.Ascii));
        ContentError("unsupported-classic-text", () => ContentConversions.CityPreferences(ConversionInput("@AUTOBUILD\n\0bad"), selected, TextCase.Ascii));
        ContentError("text-encoding", () => ContentConversions.CityPreferences(ConversionInput("@AUTOBUILD\n\x81\n"), selected, TextCase.Ascii));
        input.Rules.Rules["improvements"]![1]!["id"] = "unmapped.identity";
        ContentError("invalid-content", () => ContentConversions.CityPreferences(ConversionInput("@AUTOBUILD\nimprovements 1\n"), input.Resolve(), TextCase.Ascii));
    }
}
