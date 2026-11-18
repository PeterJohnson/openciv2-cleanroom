using OpenCiv2.Base;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Formats.Classic;
using ClassicReader = OpenCiv2.Formats.Classic.TextReader;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static void ClassicPreferenceDecoding()
    {
        var fixture = new CityContentInput(); var definitions = fixture.Rules.Load().Input.Definitions;
        foreach (bool deprioritize in new[] { false, true })
        {
            fixture.Preferences["humanCityDefensePriority"] = deprioritize ? "deprioritize" : "unchanged";
            var f = new ClassicTextInput(); f.Files["scenario/CITYPREF.TXT"] = (deprioritize ? "@NODEFEND\r\n" : "") + "@AUTOBUILD\r\nImPrOvEmEnTs 2,ignored\r\nimprovements 1\r\nimprovements 2";
            using var reader = f.Reader(); var update = ClassicCityContent.LoadPreferences(reader, definitions, TextCase.Ascii);
            Equal(fixture.LoadPreferences().Update, update); Equal(false, reader.IsOpen); Equal("caller", f.Directories.Working);
        }
        foreach (string? text in new[] { null, "@AUTOBUILD\n;not a comment to skip\nimprovements 1", "@AUTOBUILD\nimprovements 39\nimprovements 1" })
        {
            var f = new ClassicTextInput(); if (text is not null) f.Files["scenario/CITYPREF.TXT"] = text;
            using var reader = f.Reader(); var update = ClassicCityContent.LoadPreferences(reader, definitions, TextCase.Ascii);
            Equal(false, update.DeprioritizeDefense); Equal(0, update.BuildOrder.Count); Equal(false, reader.IsOpen);
        }
        var marker = new ClassicTextInput(); marker.Files["scenario/CITYPREF.TXT"] = "@NODEFEND";
        using var markerReader = marker.Reader(); var markerUpdate = ClassicCityContent.LoadPreferences(markerReader, definitions, TextCase.Ascii); Equal(true, markerUpdate.DeprioritizeDefense); Equal(0, markerUpdate.BuildOrder.Count);
        var full = new ClassicTextInput(); full.Files["scenario/CITYPREF.TXT"] = "@NODEFEND\n@AUTOBUILD\n" + RepeatLines("improvements 1", 40);
        using var fullReader = full.Reader(); var values = ClassicCityContent.LoadPreferences(fullReader, definitions, TextCase.Ascii); Equal(32, values.BuildOrder.Count); Equal(true, values.BuildOrder.All(v => v == 1)); Equal(35, full.Traces["scenario/CITYPREF.TXT"].Reads);
        var duplicate = definitions.Improvements.ToArray(); duplicate[2] = duplicate[2] with { Identity = duplicate[2].Identity with { Name = duplicate[1].Identity.Name } };
        full.Files["scenario/CITYPREF.TXT"] = "@AUTOBUILD\nimprovements 1\n";
        Equal(new OwnedList<byte>([1]), ClassicCityContent.LoadPreferences(fullReader, definitions with { Improvements = new(duplicate) }, TextCase.Ascii).BuildOrder);
    }
    private static void ClassicPreferenceSnapshotAndCleanup()
    {
        var state = Installed(new RulesInput().Load().Input); var f = new ClassicTextInput(); f.Files["scenario/CITYPREF.TXT"] = "@NODEFEND\n@AUTOBUILD\nimprovements 2\nimprovements 1\n";
        var trace = new TextTrace(); f.Traces["scenario/CITYPREF.TXT"] = trace; int count = state.Text.Count;
        trace.OnRead = () => { state.Names.Improvements[2] = state.Text.Store("Changed"u8); };
        using var reader = f.Reader(); var preferences = new CityPreferences { Count = 17, NoDefend = 7 }; Array.Fill(preferences.BuildOrder, 1234);
        var update = ClassicCityContent.LoadPreferences(reader, state, TextCase.Ascii);
        Equal(new OwnedList<byte>([2, 1]), update.BuildOrder); Equal(17, preferences.Count); Equal(7, preferences.NoDefend); Equal(1234, preferences.BuildOrder[0]); Equal(false, reader.IsOpen); Equal(true, state.Text.Count > count);
        preferences.Apply(update); Equal(2, preferences.Count); Equal(1, preferences.NoDefend); Equal(1234, preferences.BuildOrder[2]);
        trace.OnRead = null; state.Names.Improvements[1] = state.Text.Store("New binding"u8); state.Definitions.Improvements[1] = state.Definitions.Improvements[1] with { Identity = new("id", new("Cached input", new("Cached input"u8.ToArray()))) };
        f.Files["scenario/CITYPREF.TXT"] = "@AUTOBUILD\nNew binding\n"; Equal(new OwnedList<byte>([1]), ClassicCityContent.LoadPreferences(reader, state, TextCase.Ascii).BuildOrder);
        trace.FailClose = true; int closes = trace.Closes; f.Files["scenario/CITYPREF.TXT"] = "@NODEFEND\n";
        Throws<InvalidOperationException>(() => ClassicCityContent.LoadPreferences(reader, state, TextCase.Ascii)); Equal(closes + 1, trace.Closes); Equal(false, reader.IsOpen);
        trace.FailClose = false; f.Files["scenario/CITYPREF.TXT"] = "@NODEFEND\n@AUTOBUILD\nCaf\xe9\n";
        state.Names.Improvements[1] = state.Text.Store(ByteText("Caf\xc9")); closes = trace.Closes; trace.FailClose = true;
        // The initial marker close fails before comparison, and remains the observed failure.
        Throws<InvalidOperationException>(() => ClassicCityContent.LoadPreferences(reader, state, TextCase.Ascii)); Equal(closes + 1, trace.Closes);
        trace.FailClose = false; TextError(TextFault.ComparisonUnavailable, () => ClassicCityContent.LoadPreferences(reader, state, TextCase.Ascii)); Equal(false, reader.IsOpen); Equal("caller", f.Directories.Working);
    }
    private static void ClassicNameLayers()
    {
        var f = new ClassicTextInput(); f.Files["scenario/CITY.TXT"] = "@TrIbEs 0\r\nFirst\r\nSecond\r\n@stop ends\r\n@EXTRA\r\nFallback One\r\nFallback Two\r\n@STOP\r\n";
        using var reader = f.Reader(); var native = new CityContentInput(); native.Names["civilizations"]![0]!["pluralName"] = "TrIbEs 0"; var expected = native.LoadNames().Catalog;
        var catalog = ClassicCityContent.LoadNames(reader, f.Directories, TextCase.Ascii);
        foreach (byte index in new byte[] { 0, 1, 2, 3, 4, 5, 254, 255 }) Equal(expected.Select("tribes 0"u8, index), catalog.Select("tribes 0"u8, index));
        Equal(false, reader.IsOpen); Equal("home", f.Directories.Working); Equal("scenario", f.Directories.Search);
        f.Files.Clear(); Equal("First", ByteString(catalog.Select("tribes 0"u8, 1).Bytes));
        f.Files["scenario/CITY.TXT"] = "@tribes 0\nScenario\n@STOP\n"; f.Files["home/CITY.TXT"] = "@tribes 0\nWrong override\n@STOP\n@Home only\nHome\n@STOP\n@EXTRA\nHome extra\n@STOP\n";
        catalog = ClassicCityContent.LoadNames(reader, f.Directories, TextCase.Ascii); Equal(2, catalog.Layers.Count); Equal("Scenario", ByteString(catalog.Select("tribes 0"u8, 1).Bytes)); Equal((int)NameOutcome.Exhausted, (int)catalog.Select("tribes 0"u8, 2).Outcome); Equal("Home extra", ByteString(catalog.Select("Home only"u8, 2).Bytes));
        f.Files.Remove("scenario/CITY.TXT"); f.Opens.Clear(); Equal(1, ClassicCityContent.LoadNames(reader, f.Directories, TextCase.Ascii).Layers.Count); Equal(2, f.Opens.Count);
        f.Directories.Search = "home"; f.Opens.Clear(); Equal(1, ClassicCityContent.LoadNames(reader, f.Directories, TextCase.Ascii).Layers.Count); Equal(1, f.Opens.Count);
    }
    private static void ClassicNamePrefixesAndCleanup()
    {
        var f = new ClassicTextInput(); f.Files["scenario/CITY.TXT"] = "@tribes 0\nOnly\n"; using var reader = f.Reader();
        var catalog = ClassicCityContent.LoadNames(reader, f.Directories, TextCase.Ascii); Equal("Only", ByteString(catalog.Select("tribes 0"u8, 1).Bytes)); Throws<ArgumentException>(() => catalog.Select("tribes 0"u8, 2));
        f.Files["scenario/CITY.TXT"] = "@tribes 0\nFirst\n@Other\n;comment\n@STOP\n@tribes 0\nLater\n@STOP\n@EXTRA\nOne\n@STOP\n@extra\nTwo\n@STOP\n";
        catalog = ClassicCityContent.LoadNames(reader, f.Directories, TextCase.Ascii); Equal("First", ByteString(catalog.Select("tribes 0"u8, 1).Bytes)); Equal("@Other", ByteString(catalog.Select("tribes 0"u8, 2).Bytes)); Equal(";comment", ByteString(catalog.Select("tribes 0"u8, 3).Bytes)); Equal("One", ByteString(catalog.Select("tribes 0"u8, 4).Bytes));
        f.Files["scenario/CITY.TXT"] = "@tribes 0\n" + RepeatLines("Entry", 255); catalog = ClassicCityContent.LoadNames(reader, f.Directories, TextCase.Ascii); Equal(true, catalog.Layers[0].Civilizations[0].Complete);
        f.Files["scenario/CITY.TXT"] = "@tribes 0\nCaf\x81\n@STOP\n"; ContentError("text-encoding", () => ClassicCityContent.LoadNames(reader, f.Directories, TextCase.Ascii)); Equal(false, reader.IsOpen); Equal("home", f.Directories.Working);
        f.Files["scenario/CITY.TXT"] = "@tribes 0\n\0bad\n"; var trace = f.Traces["scenario/CITY.TXT"]; trace.FailClose = true; int closes = trace.Closes;
        TextError(TextFault.UnsupportedControl, () => ClassicCityContent.LoadNames(reader, f.Directories, TextCase.Ascii)); Equal(closes + 1, trace.Closes); Equal(false, reader.IsOpen); Equal("home", f.Directories.Working); Equal("scenario", f.Directories.Search);
    }
}
