using OpenCiv2.Base;
using OpenCiv2.Core.Rules;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static string LabelTable(int count, string prefix = "Label") => "@LABELS\n" + count + "\n" + string.Concat(Enumerable.Range(0, Math.Max(0, count)).Select(i => prefix + i + "\n"));
    private static void ClassicLabelCatalog()
    {
        var f = new ClassicTextInput(); f.Files["scenario/LABELS.TXT"] = "@STRINGHEAP\n50000\n" + LabelTable(888) + "@POPUPS\nOK\nHelp\nCancel\nUnused\n";
        using var reader = f.Reader(); var catalog = ClassicLabels.Load(reader, f.Directories); var labels = new Labels();
        Equal(false, reader.IsOpen); Equal(888, catalog.DeclaredCount); Equal(888, catalog.Entries.Count); Equal("Label887", catalog.Entries[887]); Equal("caller", f.Directories.Working); Equal(new OwnedList<string>(["scenario/LABELS.TXT", "scenario/LABELS.TXT"]), new(f.Opens));
        f.Files.Clear(); catalog.Install(labels); Equal(888, labels.DeclaredCount); Equal("Label887", labels.Label(887)); Equal("Help", labels.Popup(1));
        var entries = catalog.Entries.ToArray(); entries[0] = "X"; Equal("Label0", labels.Label(0)); Equal("Label0", catalog.Entries[0]);
    }
    private static void ClassicLabelFallback()
    {
        var f = new ClassicTextInput(); f.Files["scenario/LABELS.TXT"] = "@LABELS\n2\nScenario,whole;label\n\n"; f.Files["home/LABELS.TXT"] = LabelTable(4, "Home");
        using var reader = f.Reader(); var catalog = ClassicLabels.Load(reader, f.Directories); var labels = new Labels(); catalog.Install(labels);
        Equal(2, labels.DeclaredCount); Equal(888, labels.Entries.Count); Equal("Scenario,whole;label", labels.Label(0)); Equal("<nil>", labels.Label(1)); Equal("Home2", labels.Label(2)); Equal("Home3", labels.Label(3)); Equal("[ERROR:LABELS.TXT]", labels.Label(887)); Equal(labels.Entries[4], labels.Entries[887]); Equal("scenario", f.Directories.Search); Equal(false, reader.IsOpen);
        foreach (int count in new[] { 0, -3 })
        {
            f.Files["scenario/LABELS.TXT"] = LabelTable(count); f.Files["home/LABELS.TXT"] = LabelTable(2, "Home"); catalog = ClassicLabels.Load(reader, f.Directories); catalog.Install(labels);
            Equal(count, labels.DeclaredCount); Equal("Home0", labels.Label(0)); Equal("Home1", labels.Label(1));
        }
    }
    private static void ClassicLabelMissingAndRetainedNames()
    {
        var f = new ClassicTextInput(); using var reader = f.Reader(); var oldRule = NameText("Old rule"); var labels = new Labels { DeclaredCount = 7, StatusLanguage = 2 }; labels.Entries.Add(oldRule); labels.Popups[1] = oldRule; int reports = 0;
        Equal("OK", labels.Popup(0)); Equal("Cancel", labels.Popup(2));
        ClassicLabels.Load(reader, f.Directories, () => { ++reports; Equal(7, labels.DeclaredCount); Equal(1, labels.Entries.Count); }).Install(labels);
        Equal(1, reports); Equal(0, labels.DeclaredCount); Equal(0, labels.Entries.Count); Equal("Old rule", labels.Popup(1)); Throws<ArgumentOutOfRangeException>(() => labels.Label(0));
        f.Directories.Extension = "GER"; f.Files["scenario/LABELS.GER"] = LabelTable(888, "New"); ClassicLabels.Load(reader, f.Directories).Install(labels);
        Equal(2, labels.StatusLanguage); Equal("GER", f.Directories.Extension); Equal("Old rule", oldRule); Equal("Old rule", labels.Popup(1)); Equal("New0", labels.Label(0));
    }
    private static void ClassicLabelAdmissionAndCleanup()
    {
        foreach (bool fallback in new[] { false, true })
        {
            var f = new ClassicTextInput(); f.Files["scenario/LABELS.TXT"] = LabelTable(fallback ? 1 : 889); f.Files["home/LABELS.TXT"] = LabelTable(889, "Home"); using var reader = f.Reader();
            Throws<ArgumentOutOfRangeException>(() => ClassicLabels.Load(reader, f.Directories)); Equal(false, reader.IsOpen); Equal("scenario", f.Directories.Search); Equal("caller", f.Directories.Working);
        }
        var missing = new ClassicTextInput(); using var closed = missing.Reader(); Throws<IOException>(() => ClassicLabels.Load(closed, missing.Directories, () => throw new IOException("missing"))); Equal(false, closed.IsOpen); Equal("scenario", missing.Directories.Search);
        var malformed = new ClassicTextInput(); malformed.Files["scenario/LABELS.TXT"] = "@LABELS\n3\nOne\n"; using var shortReader = malformed.Reader(); TextError(TextFault.UnavailableStream, () => ClassicLabels.Load(shortReader, malformed.Directories)); Equal(false, shortReader.IsOpen); Equal("scenario", malformed.Directories.Search);
        var labels = new Labels(); Throws<ArgumentException>(() => new LabelCatalog(889, new(Enumerable.Repeat("", 889)), new(new string?[3])).Install(labels)); Equal(0, labels.Entries.Count);
        Throws<ArgumentException>(() => new LabelCatalog(0, new([]), new([])).Install(labels)); Equal(0, labels.Entries.Count);
    }
    private static void LabelInstallationIndependentNames()
    {
        var rules = Installed(new RulesInput().Load().Input); var labels = rules.Labels; labels.StatusLanguage = -2;
        rules.Leaders[0].Plural = new(NameText("Custom people"), true);
        var before = rules.Definitions.Snapshot(); var leader = rules.Leaders[0].Snapshot();
        var a = NameText("A");
        var catalog = new LabelCatalog(-9, new([a, NameText("A\0ignored"), ""]), new([a, null, ""]));
        for (int i = 0; i < 2; ++i)
        {
            catalog.Install(labels);
            Equal(a, labels.Label(0)); Equal("A\0ignored", labels.Entries[1]); Equal(a, labels.Popup(0));
            Equal(-9, labels.DeclaredCount); Equal(-2, labels.StatusLanguage);
            Equal("Help", labels.Popup(1)); Equal("<nil>", labels.Popup(2));
            Equal(before, rules.Definitions.Snapshot()); Equal(leader, rules.Leaders[0].Snapshot());
        }
    }
    private static void TextLanguageConfiguration()
    {
        foreach (int language in new[] { int.MinValue, -1, 0, 1, 2, 3, 4, 5, 6 })
        {
            var f = new ClassicTextInput(); var old = NameText("Old"); var labels = new Labels(); labels.Entries.Add(old); int caches = 0;
            string extension = language switch { 2 => "FRE", 3 => "GER", 4 => "ITA", 5 => "SPA", _ => "TXT" };
            TextConfiguration.Initialize(language, f.Directories, labels, () => { ++caches; Equal(extension, f.Directories.Extension); Equal(0, f.Opens.Count); });
            Equal(1, caches); Equal(language is >= 1 and <= 3 ? language - 1 : language == -1 ? -2 : language == 0 ? -1 : 0, labels.StatusLanguage); Equal(1, labels.Entries.Count); Equal(old, labels.Label(0));
            Throws<IOException>(() => TextConfiguration.Initialize(2, f.Directories, labels, () => throw new IOException())); Equal("FRE", f.Directories.Extension);
            Throws<ArgumentNullException>(() => TextConfiguration.Initialize(3, f.Directories, labels, null!)); Equal("FRE", f.Directories.Extension);
        }
    }
}
