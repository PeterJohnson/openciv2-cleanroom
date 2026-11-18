using System.Buffers.Binary;
using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.Formats;
using OpenCiv2.IO;

namespace OpenCiv2.FoundationCases;

public static partial class FoundationSuite
{
    // F06 §§3–5,8: authored byte expectations and cleanroom regression providers.
    // Item boundaries test this adapter's failure prefix, not a required native I/O ABI.
    private sealed class ItemCapture(int failAt = -1, bool throws = false)
    {
        public List<byte[]> Items { get; } = [];
        public bool Write(ReadOnlySpan<byte> value)
        {
            Items.Add(value.ToArray());
            if (Items.Count - 1 != failAt) return true;
            if (throws) throw new IOException("Authored output failure");
            return false;
        }
        public byte[] Bytes => Items.SelectMany(v => v).ToArray();
    }
    private static GameState WriterWorld()
    {
        var world = new GameState(new ProcessContext()); world.Map.Allocate(new(8, 6));
        world.Map.StoredTopology = 0x8123; world.Map.PatternSeed = 0xabcd;
        for (int civ = 1; civ < 8; ++civ) world.Map.RememberedLayer(civ).Fill((byte)(civ + 10));
        world.Map.RememberedBarbarian = [255];
        for (int grid = 0; grid < 2; ++grid) world.Map.CoarseGrid(grid).Fill((byte)(41 + grid));
        world.Map.Tiles[0].Features = 0xf1; world.Map.Tiles[^1].Region = 0xff;
        return world;
    }
    private static void LiveMapWriters()
    {
        var world = WriterWorld(); var starts = new StartingPositions(); starts.X[0] = short.MinValue; starts.Y[20] = short.MaxValue;
        var raw = new ItemCapture(); Equal(true, ClassicLive.WriteMapItems(raw.Write, MapStream.Raw, world.Map, starts));
        Equal(new OwnedList<int>([14, 42, 42, 144]), new(raw.Items.Select(i => i.Length)));
        var decoded = ClassicFiles.DecodeRawMap(raw.Bytes); Equal((short)24, decoded.Header.Area); Equal((ushort)0x8123, decoded.Header.Topology); Equal((ushort)0xabcd, decoded.Header.Seed);
        Equal(short.MinValue, decoded.StartX[0]); Equal(short.MaxValue, decoded.StartY[20]); Equal(new OwnedList<Tile>(world.Map.Tiles.ToArray()), decoded.Tiles);
        var embedded = new ItemCapture(); Equal(true, ClassicLive.WriteMapItems(embedded.Write, MapStream.Embedded, world.Map, starts)); Equal(9, embedded.Items.Count);
        for (int civ = 1; civ < 8; ++civ) Equal(new OwnedList<byte>(Enumerable.Repeat((byte)(civ + 10), 24)), new(embedded.Items[civ]));
        Equal(new OwnedList<byte>(raw.Items[3]), new(embedded.Items[8]));
        var summary = new ItemCapture(); Equal(true, ClassicLive.WriteMapSummaryItems(summary.Write, world.Map));
        Equal(new OwnedList<int>([4, 4, 1024]), new(summary.Items.Select(i => i.Length)));
        Equal(new OwnedList<byte>([41, 41, 41, 41]), new(summary.Items[0])); Equal(new OwnedList<byte>([42, 42, 42, 42]), new(summary.Items[1]));
        for (int i = 0; i < 64; ++i) Equal(world.Map.Regions[i], ClassicRecords.DecodeRegion(summary.Items[2].AsSpan(i * 16, 16)));
    }
    private static void LiveMapWriterFailures()
    {
        var world = WriterWorld(); var starts = new StartingPositions();
        foreach (var kind in new[] { MapStream.Raw, MapStream.Embedded })
        {
            var good = new ItemCapture(); ClassicLive.WriteMapItems(good.Write, kind, world.Map, starts);
            for (int fail = 0; fail < good.Items.Count; ++fail)
            {
                var output = new ItemCapture(fail); Equal(false, ClassicLive.WriteMapItems(output.Write, kind, world.Map, starts)); Equal(fail + 1, output.Items.Count);
                Equal(new OwnedList<byte>(good.Items.Take(fail + 1).SelectMany(i => i)), new(output.Bytes));
            }
        }
        for (int fail = 0; fail < 3; ++fail) { var output = new ItemCapture(fail); Equal(false, ClassicLive.WriteMapSummaryItems(output.Write, world.Map)); Equal(fail + 1, output.Items.Count); }
        world.Map.CoarseGrid(1)[3] = null; var prefix = new ItemCapture(); Throws<InvalidOperationException>(() => ClassicLive.WriteMapSummaryItems(prefix.Write, world.Map)); Equal(1, prefix.Items.Count);
        world.Map.Release(); var noOutput = new ItemCapture(); Throws<ArgumentException>(() => ClassicLive.WriteMapItems(noOutput.Write, MapStream.Raw, world.Map, starts)); Equal(0, noOutput.Items.Count);
    }
    private static void LiveEntityWriters()
    {
        var world = WriterWorld(); var pools = world.Entities; pools.Units.SetEnd(2); pools.Cities.SetEnd(1);
        pools.Units.Storage[0].Token = new(19); pools.Units.Storage[0].Damage = 7; pools.Units.Storage[1].Damage = 99;
        pools.Cities.Storage[0].Token = new(27); pools.Cities.Storage[0].Name = new("City"u8.ToArray());
        var imported = new RecordCompatibility(world.Lifetime); imported.Units[new(19)] = new() { Damage = 255, RetainedByte = 0x81, RetainedWord = 0x9023 }; imported.Cities[new(27)] = new() { RetainedByte = 0xa2 };
        Equal(true, ReferenceEquals(imported, imported.For(world.Lifetime))); Equal(true, imported.For(new WorldLifetime()) is null);
        var output = new ItemCapture(); Equal(true, ClassicLive.WriteEntityItems(output.Write, pools, imported)); Equal(2, output.Items.Count);
        var unit = ClassicRecords.DecodeStoredUnit(output.Items[0].AsSpan(0, 32)); Equal((byte)7, unit.Damage); Equal((byte)0x81, unit.RetainedByte); Equal((ushort)0x9023, unit.RetainedWord);
        var hole = ClassicRecords.DecodeStoredUnit(output.Items[0].AsSpan(32)); Equal((byte)99, hole.Damage); Equal(0U, hole.Token.Value); Equal((byte)0, hole.RetainedByte);
        Equal((byte)0xa2, ClassicRecords.DecodeStoredCity(output.Items[1]).RetainedByte);
        pools.Units.Storage[0].Token = new(20); output = new(); ClassicLive.WriteEntityItems(output.Write, pools, imported); Equal((byte)0, ClassicRecords.DecodeStoredUnit(output.Items[0].AsSpan(0, 32)).RetainedByte);
        for (int fail = 0; fail < 2; ++fail) { output = new(fail); Equal(false, ClassicLive.WriteEntityItems(output.Write, pools, imported)); Equal(fail + 1, output.Items.Count); }
        pools.Units.SetEnd(0); output = new(); Equal(true, ClassicLive.WriteEntityItems(output.Write, pools)); Equal(1, output.Items.Count);
        pools.Cities.SetEnd(0); output = new(0); Equal(true, ClassicLive.WriteEntityItems(output.Write, pools)); Equal(0, output.Items.Count);
    }
    private static void LivePersistentWriters()
    {
        var world = WriterWorld(); world.Globals.Turn = 21; world.Names[3].Leader = new("Ruler"u8.ToArray()); world.Civilizations[3].Gold = 800;
        var imported = new RecordCompatibility(world.Lifetime) { Globals = new() { Turn = 999, Retained0e = 0x1234, Retained34 = -17 } };
        imported.Civilizations[3] = new() { Gold = -1, Retained1b = 0x81, Retained65 = 0x82, Retained3fd = 0x83, Retained401 = 0x84 };
        var output = new ItemCapture(); Equal(true, ClassicLive.WritePersistentItems(output.Write, world.Globals, world.Names, world.Civilizations, imported));
        Equal(new OwnedList<int>([330, 1936, 11424]), new(output.Items.Select(i => i.Length)));
        var globals = ClassicRecords.DecodeStoredGlobals(output.Items[0]); Equal((short)21, globals.Turn); Equal((ushort)0x1234, globals.Retained0e); Equal((short)-17, globals.Retained34);
        Equal(world.Names[3], StateConversion.ToLive(ClassicRecords.DecodeStoredCivilizationNames(output.Items[1].AsSpan(3 * 242, 242))));
        var civ = ClassicRecords.DecodeStoredCivilization(output.Items[2].AsSpan(3 * 1428, 1428)); Equal(800, civ.Gold); Equal((byte)0x81, civ.Retained1b); Equal((byte)0x84, civ.Retained401);
        for (int fail = 0; fail < 3; ++fail) { output = new(fail); Equal(false, ClassicLive.WritePersistentItems(output.Write, world.Globals, world.Names, world.Civilizations, imported)); Equal(fail + 1, output.Items.Count); }
        world.Names[0].Leader = new(new byte[25]); output = new(); Throws<ArgumentException>(() => ClassicLive.WritePersistentItems(output.Write, world.Globals, world.Names, world.Civilizations)); Equal(1, output.Items.Count);
    }
    private static void LiveLeaderWriters()
    {
        var process = new ProcessContext(); var world = new GameState(process); var leaders = process.Rules.Leaders;
        for (int i = 0; i < 21; ++i)
        {
            var leader = leaders[i]; leader.TimesUsed = (byte)(i + 1); leader.CityNameIndex = (byte)(200 + i); leader.Female = i % 2 == 0;
            leader.LeaderName = new(uint.MaxValue, true); leader.Plural = new(70000, false); leader.Adjective = new(0x80000001, true);
            leader.Names[0] = new(0, true); leader.Names[1] = new(65536); for (int g = 0; g < 7; ++g) { leader.Titles[g][0] = new((uint)g, true); leader.Titles[g][1] = new(uint.MaxValue); }
        }
        world.Globals.PlayerCiv = 7; world.Globals.HumanPlayers = 6; world.Civilizations[1].Tribe = 3; world.Civilizations[2].Tribe = 3; world.Civilizations[7].Tribe = 5;
        for (int mode = 0; mode < 7; ++mode)
        {
            world.Globals.Multiplayer = (GameMode)mode; var before = world.Globals; var output = new ItemCapture();
            Equal(true, ClassicLive.WriteLeaderItems(output.Write, world.Globals, world.Civilizations, leaders, -7, 31)); Equal(before, world.Globals);
            Equal(mode == 0 ? 66 : 67, output.Items.Count); Equal((short)-7, BinaryPrimitives.ReadInt16LittleEndian(output.Items[63])); Equal((short)31, BinaryPrimitives.ReadInt16LittleEndian(output.Items[64]));
            for (int i = 0; i < 21; ++i) { Equal((byte)(i + 1), output.Items[3 * i][0]); Equal((byte)(200 + i), output.Items[3 * i + 1][0]); Equal(i % 2 == 0 ? (byte)1 : (byte)0, output.Items[3 * i + 2][0]); }
            var record = ClassicRecords.DecodeStoredLeader(output.Items[65]); Equal((byte)(mode == 0 ? 6 : 4), record.TimesUsed); Equal((short)-1, record.LeaderName); Equal((short)1, record.Plural); Equal((short)-1, record.Adjective);
            Equal(new OwnedList<short>([-1, 1]), record.Names); foreach (var title in record.Titles) Equal(new OwnedList<short>([-1, 1]), title);
            if (mode != 0) Equal(new OwnedList<byte>(output.Items[65]), new(output.Items[66]));
        }
        for (int fail = 0; fail < 67; ++fail) { var output = new ItemCapture(fail); Equal(false, ClassicLive.WriteLeaderItems(output.Write, world.Globals, world.Civilizations, leaders, 0, 0)); Equal(fail + 1, output.Items.Count); }
        world.Civilizations[1].Tribe = 21; var prefix = new ItemCapture(); Throws<ArgumentOutOfRangeException>(() => ClassicLive.WriteLeaderItems(prefix.Write, world.Globals, world.Civilizations, leaders, 0, 0)); Equal(65, prefix.Items.Count);
    }
    private static void LiveLayoutWriters()
    {
        var world = WriterWorld(); world.Layout.Valid = true; world.Layout.Rectangles[1] = new(70000, -70000, 80000, -80000); world.PowerHistory[149][7] = 81;
        for (short i = 0; i < 8; ++i) world.Views[i] = new(i % 2 == 0, i % 2 != 0, (short)(i + 10), (short)-i, (short)(i - 4));
        var before = world.Layout; var packed = new SavedLayout(); var output = new ItemCapture();
        Equal(true, ClassicLive.WriteHistoryLayoutItems(output.Write, world.PowerHistory, new(0, 0, 1280, 800), ref packed, world.Layout, world.Views));
        Equal(before, world.Layout); Equal(42, output.Items.Count); Equal(1200, output.Items[0].Length); Equal((byte)81, output.Items[0][^1]); Equal(packed, ClassicRecords.DecodeSavedLayout(output.Items[1]));
        Equal(new SavedRectangle { Right = 1280, Bottom = 800 }, packed.Rectangles[0]); Equal(unchecked((short)70000), packed.Rectangles[1].Left); Equal(unchecked((short)-80000), packed.Rectangles[1].Bottom);
        var restored = StateConversion.ToLive(packed with { Valid = -5 }); Equal(true, restored.Valid); Equal((int)unchecked((short)-70000), restored.Rectangles[1].Top);
        for (int i = 0; i < 8; ++i) Equal(world.Views[i], StateConversion.ToLive(ClassicRecords.DecodeSavedMapView(output.Items.Skip(2 + i * 5).Take(5).SelectMany(b => b).ToArray())));
        for (int fail = 0; fail < 42; ++fail)
        {
            var old = new SavedLayout { Valid = -19 }; packed = old; output = new(fail);
            Equal(false, ClassicLive.WriteHistoryLayoutItems(output.Write, world.PowerHistory, new(0, 0, 1280, 800), ref packed, world.Layout, world.Views)); Equal(fail + 1, output.Items.Count);
            Equal(fail == 0, ReferenceEquals(old, packed)); Equal(before, world.Layout);
        }
        Throws<ArgumentException>(() => StateConversion.ToLive(new SavedLayout { Rectangles = new([]) }));
    }
    private static void LiveScenarioMultiplayerWriters()
    {
        var world = WriterWorld(); world.Scenario.Name = new("Scenario"u8.ToArray()); world.Eliminations.Count = 7;
        foreach (bool export in new[] { false, true }) foreach (ushort flags in new ushort[] { 0, 0x80, 0xffff })
        {
            world.Globals.GameFlags = flags; var output = new ItemCapture(); Equal(true, ClassicLive.WriteScenarioHistoryItems(output.Write, export, world.Globals, world.Scenario, world.Eliminations));
            bool hasScenario = export || (flags & 0x80) != 0; Equal(hasScenario ? 2 : 1, output.Items.Count); Equal((short)7, ClassicRecords.DecodeEliminationHistory(output.Items[^1]).Count);
            if (hasScenario) Equal(world.Scenario, StateConversion.ToLive(ClassicRecords.DecodeScenarioParameters(output.Items[0])));
        }
        world.Multiplayer.Saved.ResumeCiv = 2; var before = world.Multiplayer; var opaque = new RecordCompatibility(world.Lifetime) { Multiplayer = new() { Retained465 = 0xa7 } };
        foreach (int value in new[] { -1, 0, 261, int.MinValue, int.MaxValue }) foreach (bool fails in new[] { false, true })
        {
            var output = new ItemCapture(fails ? 0 : -1); Equal(!fails, ClassicLive.WriteMultiplayerItem(output.Write, world.Multiplayer, value, opaque)); Equal(before, world.Multiplayer);
            Equal(unchecked((byte)value), output.Items[0][0x464]); Equal((byte)0xa7, output.Items[0][0x465]);
        }
        var broken = new ItemCapture(0, true); Throws<IOException>(() => ClassicLive.WriteMultiplayerItem(broken.Write, world.Multiplayer, -1)); Equal(before, world.Multiplayer);
    }
    private static void LiveEventWriters()
    {
        var events = new EventCollection(); var first = events.Append(); var second = events.Append(); first.Values.Actions = 0x800001;
        first.SetString(0, new("Unit"u8.ToArray())); first.SetString(1, new([])); first.SetString(4, new("Line"u8.ToArray())); first.SetString(6, new("After hole"u8.ToArray())); first.SetString(34, new("wave.wav"u8.ToArray()));
        second.SetString(0, new(Enumerable.Repeat((byte)'L', 300))); second.Strings[0].Value = new(second.Strings[0].Value.Concat(new byte[] { 0, 81 }));
        var opaque = new Dictionary<ulong, ImportedEventBytes> { [first.Identity] = new(first, new(Enumerable.Repeat((byte)0x7e, 40))) };
        var records = new ItemCapture(); var strings = new ItemCapture(); var terminators = new List<byte>();
        Equal(true, ClassicLive.WriteEventItems(records.Write, strings.Write, b => { terminators.Add(b); return true; }, events, opaque));
        Equal(4, records.Items.Count); Equal("EVNT", System.Text.Encoding.ASCII.GetString(records.Items[0])); Equal(2, BinaryPrimitives.ReadInt32LittleEndian(records.Items[1]));
        var record = ClassicRecords.DecodeEventRecord(records.Items[2]); Equal(0x800001U, record.Actions); Equal(1U, record.UnitName); Equal(1U, record.CityName); Equal(1U, record.Text[2]); Equal(0U, record.Text[1]); Equal(opaque[first.Identity].Retained, record.Retained);
        Equal(new OwnedList<int>([4, 0, 4, 8, 300]), new(strings.Items.Select(b => b.Length))); Equal(5, terminators.Count); Equal(true, terminators.All(b => b == 0));
        events.Compact(); Equal(true, opaque[first.Identity].BelongsTo(events.At(0)));
        var fresh = new EventCollection(); var replacement = fresh.Append(); Equal(first.Identity, replacement.Identity); records = new();
        ClassicLive.WriteEventItems(records.Write, strings.Write, _ => true, fresh, opaque); Equal(true, ClassicRecords.DecodeEventRecord(records.Items[2]).Retained.All(b => b == 0));
        Throws<ArgumentException>(() => new ImportedEventBytes(first, new(new byte[39])));
        // Every field maps to its own presence word, including absent/empty distinction.
        for (int field = 0; field < 35; ++field)
        {
            var one = new EventCollection().Append(); one.SetString(field, new([])); var markers = ClassicFiles.EventMarkers(StateConversion.ToStored(one));
            for (int i = 0; i < 35; ++i) Equal(i == field ? 1U : 0U, markers[i]);
        }
    }
    private static void LiveEventFailures()
    {
        var events = new EventCollection(); events.Append().SetString(0, new("one"u8.ToArray())); events.Append().SetString(34, new("two"u8.ToArray()));
        var success = new ItemCapture(); Equal(true, ClassicLive.WriteEventItems(success.Write, success.Write, b => success.Write([b]), events)); Equal(8, success.Items.Count);
        for (int fail = 0; fail < success.Items.Count; ++fail) foreach (bool throws in new[] { false, true })
        {
            var output = new ItemCapture(fail, throws);
            bool Run() => ClassicLive.WriteEventItems(output.Write, output.Write, b => output.Write([b]), events);
            if (throws) Throws<IOException>(() => Run()); else Equal(false, Run());
            Equal(fail + 1, output.Items.Count); Equal(new OwnedList<byte>(success.Items.Take(fail + 1).SelectMany(b => b)), new(output.Bytes));
            Equal(2, events.Count); Equal(new OwnedList<byte>("one"u8.ToArray()), events.At(0).Strings[0].Value);
        }
        var empty = new ItemCapture(); Equal(true, ClassicLive.WriteEventItems(empty.Write, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), new())); Equal(2, empty.Items.Count);
    }
    private static void LiveSaveComposition()
    {
        var process = new ProcessContext(); var world = WriterWorld(); world.Globals.Multiplayer = GameMode.Tcp; world.Globals.HumanPlayers = 6; world.Globals.GameFlags = 0x80;
        world.Globals.UnitEnd = 2; world.Globals.CityEnd = 1; world.Entities.Units.SetEnd(2); world.Entities.Cities.SetEnd(1);
        world.Entities.Units.Storage[0].Token = new(17); world.Entities.Cities.Storage[0].Token = new(19); world.Entities.Cities.Storage[0].Name = new("Alpha"u8.ToArray());
        world.PowerHistory[149][7] = 81; world.Events.Collection.Append().SetString(0, new("Unit"u8.ToArray()));
        var output = new ItemCapture(); var packed = new SavedLayout(); var starts = new StartingPositions();
        output.Write(ClassicFiles.EncodeGameHeader());
        Equal(true, ClassicLive.WritePersistentItems(output.Write, world.Globals, world.Names, world.Civilizations));
        Equal(true, ClassicLive.WriteMapItems(output.Write, MapStream.Embedded, world.Map, starts)); Equal(true, ClassicLive.WriteMapSummaryItems(output.Write, world.Map));
        Equal(true, ClassicLive.WriteEntityItems(output.Write, world.Entities)); Equal(true, ClassicLive.WriteLeaderItems(output.Write, world.Globals, world.Civilizations, process.Rules.Leaders, -3, 5));
        Equal(true, ClassicLive.WriteHistoryLayoutItems(output.Write, world.PowerHistory, new(0, 0, 1280, 800), ref packed, world.Layout, world.Views));
        Equal(true, ClassicLive.WriteScenarioHistoryItems(output.Write, false, world.Globals, world.Scenario, world.Eliminations)); Equal(true, ClassicLive.WriteMultiplayerItem(output.Write, world.Multiplayer, 261));
        Equal(true, ClassicLive.WriteEventItems(output.Write, output.Write, b => output.Write([b]), world.Events.Collection));
        var decoded = ClassicFiles.DecodeStoredSave(output.Bytes); Equal(new OwnedList<byte>(output.Bytes), new(ClassicFiles.EncodeStoredSave(decoded)));
        Equal(2, decoded.Units.Count); Equal(1, decoded.Cities.Count); Equal(2, decoded.Leaders.Count); Equal((short)-3, decoded.CursorX); Equal((short)5, decoded.CursorY);
        Equal((byte)81, decoded.PowerHistory[149][7]); Equal((byte)5, decoded.Multiplayer!.ResumeCiv); Equal(new OwnedList<byte>("Unit"u8.ToArray()), decoded.Events![0].Strings[0]!);
    }
}
