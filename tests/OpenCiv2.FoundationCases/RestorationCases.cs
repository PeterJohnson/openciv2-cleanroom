using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F06 successful restoration operands / W01 map installation. Installation into a Session is separate.
    private static void MapInputAdmission()
    {
        var input = RestorationInput.Game(ClassicFiles.DecodeStoredSave(SaveVector())).Map;
        input.Validate(true); input.Validate(false);
        foreach (var invalid in new[] { input with { Width = 3 }, input with { Height = 0 }, input with { Tiles = new(input.Tiles.Take(3)) } })
        { Throws<ArgumentException>(() => invalid.Validate(false)); Throws<ArgumentException>(() => invalid.Validate(true)); }
        foreach (var invalid in new[]
        {
            input with { Remembered = new([]) }, input with { Remembered = new(Enumerable.Repeat(new OwnedList<MapFeatures>(new MapFeatures[3]), 7)) },
            input with { Coarse = new([]) }, input with { Coarse = new(Enumerable.Repeat(new OwnedList<byte>(new byte[2]), 2)) }, input with { Regions = new([]) },
        }) { invalid.Validate(false); Throws<ArgumentException>(() => invalid.Validate(true)); }
    }
    private static void MapInputStagedInstallation()
    {
        var input = RestorationInput.Game(ClassicFiles.DecodeStoredSave(SaveVector())).Map; var world = WriterWorld(); var destination = world.Map;
        destination.TileAt(new(-1, -1)).Region = 99; destination.RememberedBarbarian = [MapFeatures.Unit | MapFeatures.CitySite, MapFeatures.Irrigation]; var oldBarbarians = destination.RememberedBarbarian;
        var oldTiles = destination.Tiles; var oldMemory = destination.RememberedLayer(1); var oldRegions = destination.Regions.ToArray();
        var invalid = input with { Coarse = new([]) }; Throws<ArgumentException>(() => invalid.Install(destination, true));
        Equal(8, destination.Geometry.Width); Equal((ushort)0xabcd, destination.PatternSeed); Equal(MapFeatures.Unit | MapFeatures.CitySite | MapFeatures.Mine, oldMemory[0]);
        input.Install(destination, true); Equal(4, destination.Geometry.Width); Equal(2, destination.Geometry.Height); Equal((ushort)24, destination.PatternSeed);
        Equal(new OwnedList<Tile>(destination.Tiles.ToArray()), input.Tiles); Equal(input.Regions, new(destination.Regions.ToArray()));
        for (int civ = 0; civ < 7; ++civ) Equal(input.Remembered[civ], new(destination.RememberedLayer(civ + 1).ToArray()));
        for (int grid = 0; grid < 2; ++grid) for (int i = 0; i < input.Coarse[grid].Count; ++i) Equal(input.Coarse[grid][i], destination.CoarseGrid(grid)[i]!.Value);
        Equal(true, destination.CoarseGrid(2).ToArray().All(v => v is null)); Equal(true, destination.CoarseGrid(3).ToArray().All(v => v is null));
        Equal((byte)99, destination.TileAt(new(-1, -1)).Region); Equal(new OwnedList<MapFeatures>([MapFeatures.Unit | MapFeatures.CitySite, MapFeatures.Irrigation]), new(destination.RememberedBarbarian));
        oldBarbarians[0] = MapFeatures.Unit | MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Fortification; oldTiles[0].Region = 77; oldMemory[0] = MapFeatures.Unit | MapFeatures.Irrigation | MapFeatures.Mine | MapFeatures.Fortification;
        Equal(MapFeatures.Unit | MapFeatures.CitySite, destination.RememberedBarbarian[0]); Equal(input.Tiles[0], destination.Tiles[0]); Equal(input.Remembered[0][0], destination.RememberedLayer(1)[0]);
        destination.Regions[0] = oldRegions[0] with { Land = 123 }; var priorRegions = destination.Regions.ToArray();
        (input with { Remembered = new([]), Coarse = new([]), Regions = new([]) }).Install(destination, false);
        Equal(new OwnedList<RegionSummary>(priorRegions), new(destination.Regions.ToArray())); Equal(true, destination.RememberedLayer(1).ToArray().All(v => v == 0));
        Equal(true, destination.CoarseGrid(0).ToArray().All(v => v is null)); Equal((byte)99, destination.TileAt(new(-1, -1)).Region);
        destination.Release(); input.Install(destination, true); Equal(true, destination.Allocated); Equal(input.Tiles.Count, destination.Tiles.Length);
    }
    private static void RawMapRestoration()
    {
        var world = WriterWorld(); var starts = new StartingPositions(); starts.Positions[0] = starts.Positions[0] with { X = 3 }; starts.Positions[0] = starts.Positions[0] with { Y = 5 }; var output = new ItemCapture();
        ClassicLive.WriteMapItems(output.Write, MapStream.Raw, world.Map, starts); var stored = ClassicFiles.DecodeRawMap([.. output.Bytes, 0x81, 0x82]);
        var input = RestorationInput.Map(stored); Equal(stored.Header.Width, (short)input.Width); Equal(stored.Header.Height, (short)input.Height); Equal(stored.Header.Seed, input.Seed); Equal(new OwnedList<Tile>(stored.Tiles.Select(StateConversion.ToLive)), input.Tiles);
        Equal(true, input.Remembered.All(v => v.Count == 0)); Throws<ArgumentException>(() => input.Validate(true));
        input.Install(world.Map, false); Equal((short)3, stored.StartX[0]); Equal(new OwnedList<byte>([0x81, 0x82]), stored.Trailing);
        Throws<ArgumentException>(() => RestorationInput.Map(stored with { Tiles = new([]) }));
    }
    private static void GameRestorationValues()
    {
        var bytes = SaveVector(); var stored = ClassicFiles.DecodeStoredSave(bytes); var input = RestorationInput.Game(stored);
        Equal(StateConversion.ToLive(stored.Globals), input.Globals); Equal(8, input.Names.Count); Equal(8, input.Civilizations.Count); Equal(21, input.Tribes.Count); Equal(21, input.Leaders.Count);
        for (int i = 0; i < 8; ++i) { Equal(StateConversion.ToLive(stored.Names[i]), input.Names[i]); Equal(StateConversion.ToLive(stored.Civilizations[i]), input.Civilizations[i]); Equal(StateConversion.ToLive(stored.Views[i]), input.Views[i]); }
        Equal((byte)13, input.Tribes[0].TimesUsed); Equal((byte)29, input.Tribes[0].CityNameIndex); Equal(true, input.Tribes[0].Female); Equal(false, input.Tribes[1].Female);
        Equal((short)-1, input.Cursor.X); Equal((short)2, input.Cursor.Y); Equal((byte)0xfe, input.PowerHistory[149][7]); Equal(-1, input.Layout.Rectangles[0].Left);
        Equal(StateConversion.ToLive(stored.Eliminations), input.Eliminations); Equal(true, input.Scenario is null); Equal(true, input.Multiplayer is null); Equal(true, input.Events is null);
        Equal(2, input.Units.Count); Equal(1, input.Cities.Count); Equal(0x80000001U, input.Units[0].Token.Value); Equal(0U, input.Units[1].Token.Value);
        var unit = input.Units[0]; unit.Damage = 77; Equal((byte)0, input.Units[0].Damage);
        var globals = input.Globals; globals.FirstDiscoverer[0] = 255; Equal((byte)0, input.Globals.FirstDiscoverer[0]);
        bytes[EntitiesOffset] = 99; Equal((short)2, input.Units[0].Position.X);
    }
    private static void GameRestorationLegacyOccupancy()
    {
        var stored = ClassicFiles.DecodeStoredSave(SaveVector());
        foreach (ushort version in new ushort[] { 0x26, 0x27, 0x28, 0x2c })
        {
            var input = RestorationInput.Game(stored with { Header = new(0x1a, version) });
            Equal(version < 0x29 ? 1U : 0x80000001U, input.Units[0].Token.Value); Equal(version < 0x29 ? 1U : 0U, input.Units[1].Token.Value);
            Equal(version < 0x29 ? 1U : uint.MaxValue, input.Cities[0].Token.Value); Equal((byte)0xee, input.Units[1].MovesUsed);
        }
        Equal(0U, stored.Units[1].Token.Value); // No mutation/renewal of source tokens or any process generator.
    }
    private static void GameRestorationLeaderSelection()
    {
        var stored = ClassicFiles.DecodeStoredSave(SaveVector()); var civilizations = stored.Civilizations.ToArray(); civilizations[1] = civilizations[1] with { Tribe = 3 }; civilizations[2] = civilizations[2] with { Tribe = 3 };
        var first = new StoredLeader { Plural = -1, Adjective = 1, LeaderName = -1 };
        var last = first with { Plural = 0, Adjective = short.MinValue, LeaderName = 0, Titles = new(Enumerable.Range(0, 7).Select(_ => new OwnedList<short>([short.MinValue, 0]))) };
        for (byte mode = 1; mode < 7; ++mode)
        {
            var input = RestorationInput.Game(stored with { Globals = stored.Globals with { Multiplayer = mode, HumanPlayers = 6, PlayerCiv = 7 }, Civilizations = new(civilizations), Leaders = new([first, last]) });
            Equal(true, input.Leaders[0] is null); var selected = input.Leaders[3]!; Equal(false, selected.Plural); Equal(true, selected.Adjective);
            foreach (var pair in selected.Titles) Equal(new OwnedList<bool>([true, false]), pair);
        }
        var single = RestorationInput.Game(stored with { Globals = stored.Globals with { PlayerCiv = 2 }, Civilizations = new(civilizations), Leaders = new([first]) }); Equal(true, single.Leaders[3]!.Plural); Equal(false, single.Leaders[3]!.Adjective);
        var none = RestorationInput.Game(stored with { Globals = stored.Globals with { Multiplayer = 3, HumanPlayers = 0 }, Leaders = new([]) }); Equal(true, none.Leaders.All(value => value is null));
        Throws<ArgumentException>(() => RestorationInput.Game(stored with { Globals = stored.Globals with { PlayerCiv = -1 } }));
        Throws<ArgumentException>(() => RestorationInput.Game(stored with { Leaders = new([]) }));
        civilizations[0] = civilizations[0] with { Tribe = 21 }; Throws<ArgumentException>(() => RestorationInput.Game(stored with { Civilizations = new(civilizations) }));
    }
    private static void GameRestorationOptionalInputs()
    {
        var stored = ClassicFiles.DecodeStoredSave(SaveVector()); var strings = new OwnedList<byte>?[35]; strings[0] = new([]); strings[34] = new("event.wav"u8.ToArray());
        var eventRecord = new EventRecord { Actions = 0x80000001, UnitName = 1, WaveFile = 1, CreateVeteran = -1, Retained = new(Enumerable.Repeat((byte)0xa5, 40)) };
        stored = stored with { Scenario = new() { Name = new(new byte[80]), YearIncrement = -12 }, Multiplayer = new() { OpenGame = -3, ResumeCiv = 7 }, Events = new([new(eventRecord, new(strings))]) };
        var input = RestorationInput.Game(stored); Equal((short)-12, input.Scenario!.Value.YearIncrement); Equal(true, input.Multiplayer!.Value.OpenGame); Equal((byte)7, input.Multiplayer.Value.ResumeCiv);
        Equal(1, input.Events!.Count); Equal(StateConversion.DecodeEventActions(0x80000001U), input.Events[0].Values.Actions); Equal(true, input.Events[0].Values.CreateVeteran); Equal(true, input.Events[0].Strings[1] is null); Equal(0, input.Events[0].Strings[0]!.Length);
        strings[34] = new("changed"u8.ToArray()); Equal("event.wav", input.Events[0].Strings[34]!);
        var live = new EventCollection(); live.Install(input.Events); Equal(true, live.At(0).Strings[0].Present); Equal(false, live.At(0).Strings[1].Present); Equal(0, live.At(0).Strings[0].Value.Length);
        Equal(true, RestorationInput.Game(stored with { Events = new([]) }).Events is { Count: 0 });
    }
    private static void GameRestorationAdmission()
    {
        var stored = ClassicFiles.DecodeStoredSave(SaveVector());
        foreach (var invalid in new[]
        {
            stored with { Names = new([]) }, stored with { Tribes = new([]) }, stored with { Views = new([]) }, stored with { PowerHistory = new([]) },
            stored with { PowerHistory = new(Enumerable.Repeat(new OwnedList<byte>(new byte[7]), 150)) }, stored with { Layout = new() { Rectangles = new([]) } },
            stored with { Map = stored.Map with { Tiles = new([]) } }, stored with { Map = stored.Map with { Remembered = new([]) } },
            stored with { Events = new([new(new(), new([]))]) }, stored with { Leaders = new([new() { Titles = new([]) }]) },
        }) Throws<ArgumentException>(() => RestorationInput.Game(invalid));
        Equal(new OwnedList<byte>(SaveVector()), new(ClassicFiles.EncodeStoredSave(stored)));
    }
}
