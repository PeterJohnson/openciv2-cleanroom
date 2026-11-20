using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core.City;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.Formats.Classic;
using RuntimeCity = OpenCiv2.Core.State.City;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    private sealed class NamingFixture
    {
        public RuntimeCity City = new() { Owner = new(1), Name = "Retained name payload" };
        public Civilization[] Civilizations = new Civilization[8];
        public Leader[] Leaders = Enumerable.Range(0, 21).Select(_ => new Leader()).ToArray();
        public NamingFixture() => Leaders[0].Plural = new(NameText("tribes 0"), true);
        public bool Run(NameCatalog catalog) => CityNames.DefaultName(ref City, Civilizations, Leaders, catalog);
    }
    private static string NameText(string text) => text;
    private static NameCatalog NamingCatalog(string[] names, string[]? fallback = null, bool complete = true) => new([new(new([new(NameText("tribes 0"), new(names.Select(NameText)), complete)]), fallback is null ? null : new(fallback.Select(NameText)))], ClassicText.Compare(TextComparison.AsciiNameEqual));
    private static void CityNamingCursors()
    {
        var input = new CityContentInput(); input.Names["civilizations"]![0]!["pluralName"] = "TrIbEs 0"; var native = input.LoadNames().Catalog;
        var files = new ClassicTextInput(); files.Files["scenario/CITY.TXT"] = "@TrIbEs 0\nFirst\nSecond\n@STOP\n@EXTRA\nFallback One\nFallback Two\n@STOP\n";
        using var reader = files.Reader(); var classic = ClassicCityContent.LoadNames(reader, files.Directories, TextCase.Ascii);
        for (int count = 0; count < 256; ++count)
        {
            var a = new NamingFixture(); var b = new NamingFixture(); a.Leaders[0].CityNameIndex = b.Leaders[0].CityNameIndex = (byte)count;
            Equal(true, a.Run(native)); Equal(true, b.Run(classic)); Equal(a.City, b.City); Equal(a.Leaders[0].Snapshot(), b.Leaders[0].Snapshot()); Equal(NameText("tribes 0"), a.Leaders[0].Plural.Text);
            string expected = count == 255 ? "@TrIbEs 0" : count == 1 ? "Second" : count == 2 ? "Fallback One" : count == 3 ? "Fallback Two" : "First";
            Equal(expected, a.City.Name); Equal((byte)(count == 255 ? 0 : count >= 4 ? 1 : count + 1), a.Leaders[0].CityNameIndex);
        }
        Equal(false, reader.IsOpen);
    }
    private static void CityNamingMissingAndRetry()
    {
        var world = new NamingFixture(); world.Leaders[0].Plural = new(NameText("Missing")); world.Leaders[0].CityNameIndex = 9; var before = world.City;
        Equal(false, world.Run(NamingCatalog(["First"]))); Equal(before, world.City); Equal((byte)10, world.Leaders[0].CityNameIndex);
        foreach (string[]? fallback in new string[]?[] { null, [] })
        {
            world = new(); world.Leaders[0].CityNameIndex = 11; before = world.City; Equal(false, world.Run(NamingCatalog([], fallback))); Equal(before, world.City); Equal((byte)0, world.Leaders[0].CityNameIndex);
        }
        var catalog = new NameCatalog([new(new([new(NameText("tribes 0"), new([NameText("Scenario")]))]), null), new(new([new(NameText("tribes 0"), new([NameText("Home")]))]), new([NameText("Wrong fallback")]))], ClassicText.Compare(TextComparison.AsciiNameEqual));
        world = new(); world.Leaders[0].CityNameIndex = 1; Equal(true, world.Run(catalog)); Equal("Scenario", world.City.Name); Equal((byte)1, world.Leaders[0].CityNameIndex);
    }
    private static void CityNamingTextAndFailure()
    {
        var world = new NamingFixture(); var catalog = NamingCatalog(["12345678901234\xe9 tail", "12345678901234\xe9 tail", "X"]);
        Equal(true, world.Run(catalog)); Equal("12345678901234é", world.City.Name); Equal(15, world.City.Name.Length); var first = world.City.Name;
        Equal(true, world.Run(catalog)); Equal(first, world.City.Name); Equal(true, world.Run(catalog)); Equal("X", world.City.Name); Equal(true, StateConversion.ToStored(world.City).Name.Skip(1).All(b => b == 0));
        world = new(); catalog = NamingCatalog(["A\0tail"]); Equal(true, world.Run(catalog)); Equal("A\0tail", world.City.Name); Throws<ArgumentException>(() => StateConversion.ToStored(world.City));
        world = new(); catalog = NamingCatalog(["Only"], complete: false); Equal(true, world.Run(catalog)); var before = world.City;
        Throws<ArgumentException>(() => world.Run(catalog)); Equal((byte)2, world.Leaders[0].CityNameIndex); Equal(before, world.City);
        var comparisonFailure = new InvalidOperationException("comparison unavailable"); var faulty = new NameCatalog(catalog.Layers, (left, right) => throw comparisonFailure);
        try { world.Run(faulty); throw new Exception("Expected comparison failure"); } catch (InvalidOperationException error) { Equal(true, ReferenceEquals(comparisonFailure, error)); }
        Equal((byte)3, world.Leaders[0].CityNameIndex); Equal(before, world.City);
    }
    private static void CityNamingLiveReferences()
    {
        var world = new NamingFixture(); var catalog = new NameCatalog([new(new([new(NameText("tribes 0"), new([NameText("First")])), new(NameText("Renamed"), new([NameText("Changed")]))]), null)], ClassicText.Compare(TextComparison.AsciiNameEqual));
        Equal(true, world.Run(catalog)); world.Leaders[2].Plural = new(NameText("Renamed")); world.Civilizations[1].Tribe = 2; Equal(true, world.Run(catalog)); Equal("Changed", world.City.Name); Equal((byte)1, world.Leaders[0].CityNameIndex); Equal((byte)1, world.Leaders[2].CityNameIndex);
        var before = world.City;
        foreach (short tribe in new short[] { -1, 21 }) { world.Civilizations[1].Tribe = tribe; Throws<ArgumentOutOfRangeException>(() => world.Run(catalog)); Equal(before, world.City); }
        world.Civilizations[1].Tribe = 0; world.City.Owner = new(8); Throws<ArgumentOutOfRangeException>(() => world.Run(catalog));
        world.City.Owner = new(1); var leader = world.Leaders[0].Snapshot(); Throws<ArgumentNullException>(() => CityNames.DefaultName(ref world.City, world.Civilizations, world.Leaders, null!)); Equal(leader, world.Leaders[0].Snapshot());
    }
    private static void CityNamingInstalledRules()
    {
        var input = new CityContentInput(); var content = input.Rules.Load().Input; var rules = Installed(content); var catalog = input.LoadNames().Catalog; var world = new NamingFixture(); var installer = new RuleInstaller(content, rules);
        Equal(true, CityNames.DefaultName(ref world.City, world.Civilizations, rules, catalog)); Equal("First", world.City.Name); Equal((byte)1, rules.Leaders[0].CityNameIndex); var plural = rules.Leaders[0].Plural;
        installer.Load(false); Equal(plural, rules.Leaders[0].Plural); Equal((byte)1, rules.Leaders[0].CityNameIndex); Equal(true, CityNames.DefaultName(ref world.City, world.Civilizations, rules, catalog)); Equal("Second", world.City.Name);
        installer.Load(true); Equal(plural, rules.Leaders[0].Plural); Equal((byte)2, rules.Leaders[0].CityNameIndex); Equal(true, CityNames.DefaultName(ref world.City, world.Civilizations, rules, catalog)); Equal("Fallback One", world.City.Name);
        rules.Leaders[0].Plural = new(NameText("tribes 0"), true); new LabelCatalog().Install(rules.Labels); rules.Leaders[0].CityNameIndex = 0;
        Equal(true, CityNames.DefaultName(ref world.City, world.Civilizations, rules, catalog)); Equal("First", world.City.Name); Equal(true, rules.Leaders[0].Plural.Custom);
    }
}
