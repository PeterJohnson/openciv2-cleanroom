using OpenCiv2.Base;
using OpenCiv2.Contracts;
using OpenCiv2.Core;
using OpenCiv2.Core.Rules;
using OpenCiv2.Core.State;
using OpenCiv2.IO;
using RuntimeEliminations = OpenCiv2.Core.State.EliminationHistory;
using RuntimeScenario = OpenCiv2.Core.State.ScenarioParameters;

namespace OpenCiv2.Formats;

public delegate bool WriteItem(ReadOnlySpan<byte> bytes);
public enum MapStream { Raw, Embedded }

/// <summary>F06 §§3–5,8 encoding over stable state. Writers do not admit game reentry; Session owns coherent preparation and publication.</summary>
public static class ClassicLive
{
    private static byte[] Word(short value) { var w = new ByteWriter(); w.I16(value); return w.ToArray(); }
    private static byte[] Shorts(ReadOnlySpan<short> values) { var w = new ByteWriter(); foreach (short value in values) w.I16(value); return w.ToArray(); }
    public static bool WriteMapItems(WriteItem write, MapStream kind, MapStorage map, in StartingPositions starts)
    {
        if (!map.Allocated || map.Tiles.Length != map.Geometry.Area) throw new ArgumentException("Map output requires complete allocated backing", nameof(map));
        var shape = map.Geometry;
        var header = new MapHeader { Width = unchecked((short)shape.Width), Height = unchecked((short)shape.Height), Area = unchecked((short)shape.Area), Topology = map.StoredTopology, Seed = map.PatternSeed, CoarseWidth = unchecked((short)shape.CoarseWidth), CoarseHeight = unchecked((short)shape.CoarseHeight) };
        if (!write(ClassicRecords.Encode(header))) return false;
        if (kind == MapStream.Raw)
        {
            if (!write(Shorts(starts.X)) || !write(Shorts(starts.Y))) return false;
        }
        else
        {
            for (int civilization = 1; civilization < 8; ++civilization)
            {
                var layer = map.RememberedLayer(civilization);
                if (layer.Length != shape.Area) throw new ArgumentException("Map memory layer does not match allocated geometry", nameof(map));
                if (!write(layer)) return false;
            }
        }
        var tiles = new ByteWriter(); foreach (var tile in map.Tiles) tiles.Append(ClassicRecords.Encode(tile)); return write(tiles.ToArray());
    }
    public static bool WriteEntityItems(WriteItem write, EntityPools pools, RecordCompatibility? compatibility = null)
    {
        if (pools.Units.End != 0)
        {
            var bytes = new ByteWriter();
            foreach (var value in pools.Units.Scanned)
            {
                var record = StateConversion.ToStored(value);
                if (compatibility is not null && compatibility.Units.TryGetValue(value.Token, out var imported)) record = StateConversion.PreserveOpaque(record, imported);
                bytes.Append(ClassicRecords.Encode(record));
            }
            if (!write(bytes.ToArray())) return false;
        }
        if (pools.Cities.End != 0)
        {
            var bytes = new ByteWriter();
            foreach (var value in pools.Cities.Scanned)
            {
                var record = StateConversion.ToStored(value);
                if (compatibility is not null && compatibility.Cities.TryGetValue(value.Token, out var imported)) record = StateConversion.PreserveOpaque(record, imported);
                bytes.Append(ClassicRecords.Encode(record));
            }
            if (!write(bytes.ToArray())) return false;
        }
        return true;
    }
    public static bool WritePersistentItems(WriteItem write, in GlobalValues globals, ReadOnlySpan<CivilizationNames> names, ReadOnlySpan<Civilization> civilizations, RecordCompatibility? compatibility = null)
    {
        if (names.Length != 8 || civilizations.Length != 8) throw new ArgumentException("Persistent output requires eight civilization records and names");
        var record = StateConversion.ToStored(globals); if (compatibility is not null) record = StateConversion.PreserveOpaque(record, compatibility.Globals);
        if (!write(ClassicRecords.Encode(record))) return false;
        var bytes = new ByteWriter(); foreach (var value in names) bytes.Append(ClassicRecords.Encode(StateConversion.ToStored(value)));
        if (!write(bytes.ToArray())) return false;
        bytes = new();
        for (int i = 0; i < civilizations.Length; ++i)
        {
            var value = StateConversion.ToStored(civilizations[i]); if (compatibility is not null) value = StateConversion.PreserveOpaque(value, compatibility.Civilizations[i]);
            bytes.Append(ClassicRecords.Encode(value));
        }
        return write(bytes.ToArray());
    }
    public static bool WriteMapSummaryItems(WriteItem write, MapStorage map)
    {
        for (int grid = 0; grid < 2; ++grid)
        {
            var cells = map.CoarseGrid(grid); var bytes = new byte[cells.Length];
            for (int i = 0; i < cells.Length; ++i) bytes[i] = cells[i] ?? throw new InvalidOperationException("Coarse grid output requires initialized cells");
            if (!write(bytes)) return false;
        }
        var regions = new ByteWriter(); foreach (var value in map.Regions) regions.Append(ClassicRecords.Encode(value)); return write(regions.ToArray());
    }
    public static bool WriteHistoryLayoutItems(WriteItem write, in Fixed150<Fixed8<byte>> history, Rectangle currentScreen, ref SavedLayout packed, in Layout layout, ReadOnlySpan<MapView> views)
    {
        if (views.Length != 8) throw new ArgumentException("History output requires eight map views", nameof(views));
        var bytes = new ByteWriter(); foreach (var sample in (ReadOnlySpan<Fixed8<byte>>)history) foreach (byte value in (ReadOnlySpan<byte>)sample) bytes.U8(value);
        if (!write(bytes.ToArray())) return false;
        var snapshot = layout; snapshot.Rectangles[0] = currentScreen; packed = StateConversion.ToStored(snapshot);
        if (!write(ClassicRecords.Encode(packed))) return false;
        foreach (var view in views)
        {
            var stored = StateConversion.ToStored(view);
            if (!write(Word(stored.Open)) || !write(Word(stored.TrackMoves)) || !write(Word(stored.X)) || !write(Word(stored.Y)) || !write(Word(stored.Zoom))) return false;
        }
        return true;
    }
    public static bool WriteScenarioHistoryItems(WriteItem write, bool scenarioExport, in GlobalValues globals, in RuntimeScenario scenario, in RuntimeEliminations eliminations)
    {
        if ((scenarioExport || (globals.GameFlags & 0x80) != 0) && !write(ClassicRecords.Encode(StateConversion.ToStored(scenario)))) return false;
        return write(ClassicRecords.Encode(StateConversion.ToStored(eliminations)));
    }
    public static bool WriteMultiplayerItem(WriteItem write, in MultiplayerState state, int networkTurnCivilization, RecordCompatibility? compatibility = null)
    {
        var saved = StateConversion.ToStored(state.Saved);
        if (compatibility?.Multiplayer is { } imported) saved = StateConversion.PreserveOpaque(saved, imported);
        saved = saved with { ResumeCiv = unchecked((byte)networkTurnCivilization) }; return write(ClassicRecords.Encode(saved));
    }
    public static bool WriteLeaderItems(WriteItem write, in GlobalValues globals, ReadOnlySpan<Civilization> civilizations, ReadOnlySpan<Leader> leaders, short cursorX, short cursorY)
    {
        for (int i = 0; i < 21; ++i)
        {
            if (i >= leaders.Length) throw new ArgumentOutOfRangeException(nameof(leaders), "Tribe triplet has no leader backing");
            if (!write([leaders[i].TimesUsed]) || !write([leaders[i].CityNameIndex]) || !write([leaders[i].Female ? (byte)1 : (byte)0])) return false;
        }
        if (!write(Word(cursorX)) || !write(Word(cursorY))) return false;
        if (globals.Multiplayer == GameMode.SinglePlayer) return WriteSelectedLeader(write, globals.PlayerCiv, civilizations, leaders);
        for (int civ = 0; civ < 8; ++civ) if ((globals.HumanPlayers & (1U << civ)) != 0 && !WriteSelectedLeader(write, civ, civilizations, leaders)) return false;
        return true;
    }
    private static bool WriteSelectedLeader(WriteItem write, int civilization, ReadOnlySpan<Civilization> civilizations, ReadOnlySpan<Leader> leaders)
    {
        if (civilization < 0 || civilization >= civilizations.Length) throw new ArgumentOutOfRangeException(nameof(civilization), "Leader selection has no civilization backing");
        int tribe = civilizations[civilization].Tribe;
        if (tribe < 0 || tribe >= 21 || tribe >= leaders.Length) throw new ArgumentOutOfRangeException(nameof(civilizations), "Leader selection has no tribe backing");
        return write(ClassicRecords.Encode(StateConversion.ToStored(leaders[tribe])));
    }
    public static bool WriteEventItems(WriteItem write, WriteItem text, Func<byte, bool> writeByte, EventCollection events, IReadOnlyDictionary<ulong, ImportedEventBytes>? compatibility = null)
    {
        if (!write("EVNT"u8)) return false;
        var count = new ByteWriter(); count.I32(events.Count); if (!write(count.ToArray())) return false;
        for (int i = 0; i < events.Count; ++i)
        {
            var entry = events.At(i); var record = StateConversion.ToStored(entry);
            if (compatibility is not null && compatibility.TryGetValue(entry.Identity, out var imported) && imported.BelongsTo(entry)) record = record with { Retained = imported.Retained };
            if (!write(ClassicRecords.Encode(record))) return false;
        }
        for (int i = 0; i < events.Count; ++i)
        {
            var entry = events.At(i);
            for (int field = 0; field < Event.StringCount; ++field)
            {
                if (field >= Event.TextBegin && field < Event.OtherBegin && !entry.Strings[field].Present) { field = Event.OtherBegin - 1; continue; }
                if (!entry.Strings[field].Present) continue;
                byte[] bytes = entry.Strings[field].Value.TakeWhile(b => b != 0).ToArray();
                if (!text(bytes) || !writeByte(0)) return false;
            }
        }
        return true;
    }
}
