using OpenCiv2.Base;
using OpenCiv2.Core.Rules;
using OpenCiv2.Formats.Classic;
using OpenCiv2.Session;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private static string LabelTable(int count, string prefix = "Label") => "@LABELS\n" + count + "\n" + string.Concat(Enumerable.Range(0, Math.Max(0, count)).Select(i => prefix + i + "\n"));
    private static void ClassicLabelCatalog()
    {
        var f = new ClassicTextInput(); f.Files["scenario/LABELS.TXT"] = "@STRINGHEAP\n50000\n" + LabelTable(888) + "@POPUPS\nOK\nHelp\nCancel\nUnused\n";
        using var reader = f.Reader(); var catalog = ClassicLabels.Load(reader, f.Directories); var pool = new TextPool(); var labels = new Labels();
        Equal(false, reader.IsOpen); Equal(888, catalog.DeclaredCount); Equal(888, catalog.Entries.Count); Equal("Label887", ByteString(catalog.Entries[887])); Equal("caller", f.Directories.Working); Equal(new OwnedList<string>(["scenario/LABELS.TXT", "scenario/LABELS.TXT"]), new(f.Opens));
        f.Files.Clear(); catalog.Install(pool, labels); Equal(888, labels.DeclaredCount); Equal("Label887", ByteString(labels.Label(887, pool))); Equal("Help", ByteString(labels.Popup(1, pool)));
        var entries = catalog.Entries.ToArray(); entries[0] = new([88]); Equal("Label0", ByteString(labels.Label(0, pool))); Equal("Label0", ByteString(catalog.Entries[0]));
    }
    private static void ClassicLabelFallback()
    {
        var f = new ClassicTextInput(); f.Files["scenario/LABELS.TXT"] = "@LABELS\n2\nScenario,whole;label\n\n"; f.Files["home/LABELS.TXT"] = LabelTable(4, "Home");
        using var reader = f.Reader(); var catalog = ClassicLabels.Load(reader, f.Directories); var pool = new TextPool(); var labels = new Labels(); catalog.Install(pool, labels);
        Equal(2, labels.DeclaredCount); Equal(888, labels.Entries.Count); Equal("Scenario,whole;label", ByteString(labels.Label(0, pool))); Equal(0U, labels.Entries[1]!.Value); Equal("<nil>", ByteString(labels.Label(1, pool))); Equal("Home2", ByteString(labels.Label(2, pool))); Equal("Home3", ByteString(labels.Label(3, pool))); Equal("[ERROR:LABELS.TXT]", ByteString(labels.Label(887, pool))); Equal(labels.Entries[4]!.Value, labels.Entries[887]!.Value); Equal("scenario", f.Directories.Search); Equal(false, reader.IsOpen);
        foreach (int count in new[] { 0, -3 })
        {
            f.Files["scenario/LABELS.TXT"] = LabelTable(count); f.Files["home/LABELS.TXT"] = LabelTable(2, "Home"); catalog = ClassicLabels.Load(reader, f.Directories); catalog.Install(pool, labels);
            Equal(count, labels.DeclaredCount); Equal("Home0", ByteString(labels.Label(0, pool))); Equal("Home1", ByteString(labels.Label(1, pool)));
        }
    }
    private static void ClassicLabelMissingAndRetainedNames()
    {
        var f = new ClassicTextInput(); using var reader = f.Reader(); var pool = new TextPool(); pool.Initialize(); var oldRule = pool.Store("Old rule"u8); var labels = new Labels { DeclaredCount = 7, StatusLanguage = 2 }; labels.Entries.Add(oldRule); labels.Popups[1] = oldRule; int reports = 0;
        Equal("OK", ByteString(labels.Popup(0, pool))); Equal("Cancel", ByteString(labels.Popup(2, pool)));
        ClassicLabels.Load(reader, f.Directories, () => { ++reports; Equal(7, labels.DeclaredCount); Equal(1, labels.Entries.Count); }).Install(pool, labels);
        Equal(1, reports); Equal(0, labels.DeclaredCount); Equal(0, labels.Entries.Count); Equal(2, pool.Count); Equal("Old rule", ByteString(labels.Popup(1, pool))); Throws<ArgumentOutOfRangeException>(() => labels.Label(0, pool));
        f.Directories.Extension = "GER"; f.Files["scenario/LABELS.GER"] = LabelTable(888, "New"); ClassicLabels.Load(reader, f.Directories).Install(pool, labels);
        Equal(2, labels.StatusLanguage); Equal("GER", f.Directories.Extension); Equal("Old rule", ByteString(pool.Text(oldRule))); Equal("Old rule", ByteString(labels.Popup(1, pool))); Equal("New0", ByteString(labels.Label(0, pool)));
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
        var pool = new TextPool(); var labels = new Labels(); Throws<ArgumentException>(() => new LabelCatalog(889, new(Enumerable.Repeat(OwnedList<byte>.Empty, 889)), new(new OwnedList<byte>?[3])).Install(pool, labels)); Equal(0, pool.Count);
        Throws<ArgumentException>(() => new LabelCatalog(0, new([]), new([])).Install(pool, labels)); Equal(0, pool.Count);
    }
    private static void LabelInstallationFullWidthAndDeduplication()
    {
        var pool = new TextPool(); pool.Initialize(); for (int i = 0; i < 70000; ++i) pool.Store("earlier"u8); var labels = new Labels { StatusLanguage = -2 };
        var a = new OwnedList<byte>("A"u8.ToArray()); var catalog = new LabelCatalog(-9, new([a, new("A"u8.ToArray()), OwnedList<byte>.Empty]), new([a, null, OwnedList<byte>.Empty])); catalog.Install(pool, labels);
        Equal(70001U, labels.Entries[0]!.Value); Equal(labels.Entries[0]!.Value, labels.Entries[1]!.Value); Equal(labels.Entries[0]!.Value, labels.Popups[0]!.Value); Equal(0U, labels.Popups[2]!.Value); Equal(-9, labels.DeclaredCount); Equal(-2, labels.StatusLanguage); Equal(70002, pool.Count); Equal("Help", ByteString(labels.Popup(1, pool))); Equal("<nil>", ByteString(labels.Popup(2, pool)));
        Equal("earlier", ByteString(pool.Text(70000))); catalog.Install(pool, labels); Equal(70002U, labels.Entries[0]!.Value); Equal("A", ByteString(pool.Text(70001)));
    }
    private static void TextLanguageConfiguration()
    {
        foreach (int language in new[] { int.MinValue, -1, 0, 1, 2, 3, 4, 5, 6 })
        {
            var f = new ClassicTextInput(); var pool = new TextPool(); pool.Initialize(); uint old = pool.Store("Old"u8); var labels = new Labels(); labels.Entries.Add(old); int caches = 0;
            string extension = language switch { 2 => "FRE", 3 => "GER", 4 => "ITA", 5 => "SPA", _ => "TXT" };
            TextConfiguration.Initialize(language, f.Directories, pool, labels, () => { ++caches; Equal(extension, f.Directories.Extension); Equal(2, pool.Count); Equal(0, f.Opens.Count); });
            Equal(1, caches); Equal(language is >= 1 and <= 3 ? language - 1 : language == -1 ? -2 : language == 0 ? -1 : 0, labels.StatusLanguage); Equal(1, pool.Count); Equal(1, labels.Entries.Count); Throws<ArgumentOutOfRangeException>(() => labels.Label(0, pool));
            pool.Store("Keep"u8); Throws<IOException>(() => TextConfiguration.Initialize(2, f.Directories, pool, labels, () => throw new IOException())); Equal(2, pool.Count); Equal("FRE", f.Directories.Extension);
            Throws<ArgumentNullException>(() => TextConfiguration.Initialize(3, f.Directories, pool, labels, null!)); Equal("FRE", f.Directories.Extension);
        }
    }
}
