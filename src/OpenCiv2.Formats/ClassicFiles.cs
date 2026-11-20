using System.Numerics;
using OpenCiv2.Base;
using OpenCiv2.Core;
using OpenCiv2.IO;

namespace OpenCiv2.Formats;

/// <summary>F06 explicit stored codecs. No live install, repair, reseeding or host filesystem access.</summary>
public static class ClassicFiles
{
    public delegate T Decoder<T>(ReadOnlySpan<byte> bytes);

    public static VersionStatus GameVersionStatus(ushort version) => version > 0x2c ? VersionStatus.Newer :
        version < 0x26 || version is >= 0x29 and <= 0x2b ? VersionStatus.UnsupportedOld : VersionStatus.Supported;

    public static GameHeader DecodeGameHeader(ReadOnlySpan<byte> bytes)
    {
        var r = new ByteReader(bytes);
        if (!r.Take(9).SequenceEqual("CIVILIZE\0"u8))
        {
            throw new DecodeException(0, "Unrecognized game header");
        }

        return new(r.U8(), r.U16());
    }

    public static byte[] EncodeGameHeader() => [67, 73, 86, 73, 76, 73, 90, 69, 0, 0x1a, 0x2c, 0];

    private static T Read<T>(ref ByteReader r, int size, Decoder<T> decode)
    {
        int offset = r.Position;
        var bytes = r.Take(size);
        try
        {
            return decode(bytes);
        }
        catch (DecodeException error)
        {
            throw new DecodeException(offset + error.Offset, error.Message);
        }
    }

    private static OwnedList<T> ReadMany<T>(ref ByteReader r, int count, int size, Decoder<T> decode)
    {
        var values = new T[count];
        for (int i = 0; i < count; ++i)
        {
            values[i] = Read(ref r, size, decode);
        }

        return new(values);
    }

    private static OwnedList<byte> Bytes(ref ByteReader r, int count) => new(r.Take(count).ToArray());

    private static Geometry Shape(MapHeader header, int offset)
    {
        try
        {
            return new(header.Width, header.Height);
        }
        catch (ArgumentException error)
        {
            throw new DecodeException(offset, error.Message);
        }
    }

    private static OwnedList<short> Starts(ReadOnlySpan<byte> bytes)
    {
        var r = new ByteReader(bytes);
        var values = new short[21];
        for (int i = 0; i < values.Length; ++i)
        {
            values[i] = r.I16();
        }

        return new(values);
    }

    public static RawMapFile DecodeRawMap(ReadOnlySpan<byte> bytes)
    {
        var r = new ByteReader(bytes);
        var header = Read(ref r, 14, ClassicRecords.DecodeMapHeader);
        var shape = Shape(header, 0);
        var x = Starts(r.Take(42));
        var y = Starts(r.Take(42));
        // F06 raw-map item admission checks the complete tile extent first.
        var tileBytes = new ByteReader(r.Take(shape.Area * 6));
        var tiles = ReadMany(ref tileBytes, shape.Area, 6, ClassicRecords.DecodeTile);
        return new(header, x, y, tiles, Bytes(ref r, r.Remaining.Length));
    }

    public static byte[] EncodeRawMap(RawMapFile map)
    {
        var shape = Shape(map.Header, 0);
        ClassicRecords.RequireCount(map.StartX, 21);
        ClassicRecords.RequireCount(map.StartY, 21);
        ClassicRecords.RequireCount(map.Tiles, shape.Area);
        var w = new ByteWriter();
        w.Append(ClassicRecords.Encode(map.Header));
        foreach (short x in map.StartX)
        {
            w.I16(x);
        }

        foreach (short y in map.StartY)
        {
            w.I16(y);
        }

        foreach (var tile in map.Tiles)
        {
            w.Append(ClassicRecords.Encode(tile));
        }

        Append(w, map.Trailing);
        return w.ToArray();
    }

    private static byte[] Fragments(ref ByteReader r, int size, params (int Offset, int Length)[] parts)
    {
        var result = new byte[size];
        foreach (var (offset, length) in parts)
        {
            r.Take(length).CopyTo(result.AsSpan(offset));
        }

        return result;
    }

    private static int Count(short value, int limit, string label)
    {
        if (value < 0 || value > limit)
        {
            throw new ArgumentException(label + " exceeds supported stored-save backing");
        }

        return value;
    }

    private static int LeaderCount(StoredGlobals globals) =>
        globals.Multiplayer == 0 ? 1 : BitOperations.PopCount(globals.HumanPlayers);

    private static bool HasScenario(StoredGlobals globals) => (globals.GameFlags & 0xc0) != 0;

    private static bool HasEventTag(ReadOnlySpan<byte> bytes) => bytes.StartsWith("EVNT"u8);

    public static OwnedList<uint> EventMarkers(EventRecord e)
    {
        ClassicRecords.RequireCount(e.Text, 20);
        return new(new uint[] { e.UnitName, e.CityName, e.WhoName, e.WhomName }.Concat(e.Text).Concat(
            new uint[] { e.MoveOwnerName, e.MoveUnitName, e.AggressionWhomName, e.AggressionWhoName,
                e.CreateOwnerName, e.CreateUnitName, e.CreateHomecity, e.AuxiliaryCivName,
                e.AuxiliaryUnitName, e.MoneyReceiverName, e.WaveFile }));
    }

    private static void CheckTextMarker(uint marker, int index, ref bool ended)
    {
        if (index is >= 4 and < 24)
        {
            if (ended && marker != 0)
            {
                throw new ArgumentException("Unsupported noncontiguous event text markers");
            }

            ended |= marker == 0;
        }
    }

    public static StoredSave DecodeStoredSave(ReadOnlySpan<byte> bytes)
    {
        var r = new ByteReader(bytes);
        var header = Read(ref r, 12, DecodeGameHeader);
        ushort version = header.Version;
        if (GameVersionStatus(version) != VersionStatus.Supported)
        {
            throw new DecodeException(10, "Unsupported classic save version");
        }

        var globals = version < 0x28
            ? ClassicRecords.DecodeStoredGlobals(
                Fragments(ref r, 330, (0, 0x36), (0x36, 0x5d), (0x9a, 0x5d), (0xfe, 0x4c)))
            : Read(ref r, 330, ClassicRecords.DecodeStoredGlobals);
        int unitCount, cityCount;
        try
        {
            unitCount = Count(globals.UnitEnd, 2048, "Unit scan bound");
            cityCount = Count(globals.CityEnd, 256, "City scan bound");
        }
        catch (ArgumentException error)
        {
            throw new DecodeException(12 + (globals.UnitEnd is < 0 or > 2048 ? 0x2e : 0x30), error.Message);
        }
        var names = ReadMany(ref r, 8, 242, ClassicRecords.DecodeStoredCivilizationNames);
        var civilizations = new StoredCivilization[8];
        for (int i = 0; i < 8; ++i)
        {
            if (version >= 0x28)
            {
                civilizations[i] = Read(ref r, 1428, ClassicRecords.DecodeStoredCivilization);
            }
            else
            {
                var record = Fragments(ref r, 1428, (0, 0x58), (0x58, 0xc), (0x66, 0xe), (0x74, 0x5d),
                    (0xd8, 0x36), (0x116, 0x36), (0x154, 0x36));
                r.Take(1);
                r.Take(0x402).CopyTo(record.AsSpan(0x192));
                civilizations[i] = ClassicRecords.DecodeStoredCivilization(record);
            }
        }
        int mapOffset = r.Position;
        var mapHeader = Read(ref r, 14, ClassicRecords.DecodeMapHeader);
        var shape = Shape(mapHeader, mapOffset);
        var remembered = new OwnedList<byte>[7];
        for (int i = 0; i < 7; ++i)
        {
            remembered[i] = Bytes(ref r, shape.Area);
        }

        var tiles = ReadMany(ref r, shape.Area, 6, ClassicRecords.DecodeTile);
        int coarseSize = shape.CoarseWidth * shape.CoarseHeight;
        var coarse = new OwnedList<OwnedList<byte>>([Bytes(ref r, coarseSize), Bytes(ref r, coarseSize)]);
        var regions = ReadMany(ref r, 64, 16, ClassicRecords.DecodeRegion);
        var units = new StoredUnit[unitCount];
        var cities = new StoredCity[cityCount];
        for (int i = 0; i < unitCount; ++i)
        {
            units[i] = version < 0x29
                        ? ClassicRecords.DecodeStoredUnit(Fragments(ref r, 32, (0, 26)))
                : Read(ref r, 32, ClassicRecords.DecodeStoredUnit);
        }

        for (int i = 0; i < cityCount; ++i)
        {
            cities[i] = version < 0x29
                        ? ClassicRecords.DecodeStoredCity(Fragments(ref r, 88, (0, 84)))
                : Read(ref r, 88, ClassicRecords.DecodeStoredCity);
        }

        var tribes = new TribeUsage[21];
        for (int i = 0; i < 21; ++i)
        {
            tribes[i] = new(r.U8(), r.U8(), r.U8());
        }

        short cursorX = r.I16(), cursorY = r.I16();
        var leaders = ReadMany(ref r, LeaderCount(globals), 48, ClassicRecords.DecodeStoredLeader);
        var history = new OwnedList<byte>[150];
        for (int i = 0; i < 150; ++i)
        {
            history[i] = Bytes(ref r, 8);
        }

        var layout = Read(ref r, 106, ClassicRecords.DecodeSavedLayout);
        var views = ReadMany(ref r, 8, 10, ClassicRecords.DecodeSavedMapView);
        var scenario = HasScenario(globals) ? Read(ref r, 100, ClassicRecords.DecodeScenarioParameters) : null;
        var eliminations = Read(ref r, 338, ClassicRecords.DecodeEliminationHistory);
        var multiplayer = version >= 0x29 && globals.Multiplayer != 0
            ? Read(ref r, 1172, ClassicRecords.DecodeMultiplayerRecord)
            : null;
        OwnedList<StoredEvent>? events = null;
        if (HasEventTag(r.Remaining))
        {
            r.Take(4);
            int countOffset = r.Position, count = r.I32();
            if (count < 0)
            {
                throw new DecodeException(countOffset, "Unsupported negative stored event count");
            }

            if (count > r.Remaining.Length / 444)
            {
                throw new DecodeException(r.Position, "Truncated event record block");
            }

            var records = ReadMany(ref r, count, 444, ClassicRecords.DecodeEventRecord);
            var entries = new StoredEvent[count];
            for (int i = 0; i < count; ++i)
            {
                var markers = EventMarkers(records[i]);
                var strings = new OwnedList<byte>?[35];
                bool ended = false;
                int stringOffset = r.Position;
                for (int j = 0; j < 35; ++j)
                {
                    try
                    {
                        CheckTextMarker(markers[j], j, ref ended);
                    }
                    catch (ArgumentException error)
                    {
                        throw new DecodeException(stringOffset, error.Message);
                    }
                    if (markers[j] == 0)
                    {
                        continue;
                    }

                    var text = new List<byte>();
                    while (true)
                    {
                        byte next = r.U8();
                        if (next == 0)
                        {
                            break;
                        }

                        if (text.Count == 255)
                        {
                            throw new DecodeException(r.Position - 1, "Unsupported event string over 255 bytes");
                        }

                        text.Add(next);
                    }
                    strings[j] = new(text);
                }
                entries[i] = new(records[i], new(strings));
            }
            events = new(entries);
        }
        return new(header, globals, names, new(civilizations), new(mapHeader, new(remembered), tiles, coarse, regions),
            new(units), new(cities), new(tribes), cursorX, cursorY, leaders, new(history), layout, views, scenario,
            eliminations, multiplayer, events, Bytes(ref r, r.Remaining.Length));
    }

    private static void Check(bool valid, string message)
    {
        if (!valid)
        {
            throw new ArgumentException(message);
        }
    }

    private static void Append(ByteWriter w, IEnumerable<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            w.U8(value);
        }
    }

    public static byte[] EncodeStoredSave(StoredSave save)
    {
        Check(save.Header.Version == 0x2c, "Stored-save encoding supports only version 0x2c");
        ClassicRecords.RequireCount(save.Names, 8);
        ClassicRecords.RequireCount(save.Civilizations, 8);
        ClassicRecords.RequireCount(save.Units, Count(save.Globals.UnitEnd, 2048, "Unit scan bound"));
        ClassicRecords.RequireCount(save.Cities, Count(save.Globals.CityEnd, 256, "City scan bound"));
        ClassicRecords.RequireCount(save.Leaders, LeaderCount(save.Globals));
        ClassicRecords.RequireCount(save.Tribes, 21);
        ClassicRecords.RequireCount(save.PowerHistory, 150);
        ClassicRecords.RequireCount(save.Views, 8);
        Check((save.Scenario is not null) == HasScenario(save.Globals), "Scenario block/flags disagree");
        Check((save.Multiplayer is not null) == (save.Globals.Multiplayer != 0), "Multiplayer block/mode disagree");
        Check(
            save.Events is not null || !HasEventTag(save.Trailing.ToArray()),
            "Trailing EVNT tag needs a decoded event block");
        var map = save.Map;
        var shape = new Geometry(map.Header.Width, map.Header.Height);
        ClassicRecords.RequireCount(map.Tiles, shape.Area);
        ClassicRecords.RequireCount(map.Remembered, 7);
        foreach (var layer in map.Remembered)
        {
            ClassicRecords.RequireCount(layer, shape.Area);
        }

        ClassicRecords.RequireCount(map.Coarse, 2);
        ClassicRecords.RequireCount(map.Regions, 64);
        foreach (var grid in map.Coarse)
        {
            ClassicRecords.RequireCount(grid, shape.CoarseWidth * shape.CoarseHeight);
        }

        var w = new ByteWriter();
        var header = EncodeGameHeader();
        header[9] = save.Header.Separator;
        w.Append(header);
        w.Append(ClassicRecords.Encode(save.Globals));
        foreach (var value in save.Names)
        {
            w.Append(ClassicRecords.Encode(value));
        }

        foreach (var value in save.Civilizations)
        {
            w.Append(ClassicRecords.Encode(value));
        }

        w.Append(ClassicRecords.Encode(map.Header));
        foreach (var layer in map.Remembered)
        {
            Append(w, layer);
        }

        foreach (var tile in map.Tiles)
        {
            w.Append(ClassicRecords.Encode(tile));
        }

        foreach (var grid in map.Coarse)
        {
            Append(w, grid);
        }

        foreach (var region in map.Regions)
        {
            w.Append(ClassicRecords.Encode(region));
        }

        foreach (var unit in save.Units)
        {
            w.Append(ClassicRecords.Encode(unit));
        }

        foreach (var city in save.Cities)
        {
            w.Append(ClassicRecords.Encode(city));
        }

        foreach (var tribe in save.Tribes)
        {
            w.U8(tribe.TimesUsed);
            w.U8(tribe.CityNameIndex);
            w.U8(tribe.Female);
        }
        w.I16(save.CursorX);
        w.I16(save.CursorY);
        foreach (var leader in save.Leaders)
        {
            w.Append(ClassicRecords.Encode(leader));
        }

        foreach (var sample in save.PowerHistory)
        {
            ClassicRecords.RequireCount(sample, 8);
            Append(w, sample);
        }
        w.Append(ClassicRecords.Encode(save.Layout));
        foreach (var view in save.Views)
        {
            w.Append(ClassicRecords.Encode(view));
        }

        if (save.Scenario is not null)
        {
            w.Append(ClassicRecords.Encode(save.Scenario));
        }

        w.Append(ClassicRecords.Encode(save.Eliminations));
        if (save.Multiplayer is not null)
        {
            w.Append(ClassicRecords.Encode(save.Multiplayer));
        }

        if (save.Events is not null)
        {
            w.Append("EVNT"u8);
            w.I32(save.Events.Count);
            foreach (var e in save.Events)
            {
                w.Append(ClassicRecords.Encode(e.Record));
            }

            foreach (var e in save.Events)
            {
                var markers = EventMarkers(e.Record);
                ClassicRecords.RequireCount(e.Strings, 35);
                bool ended = false;
                for (int i = 0; i < 35; ++i)
                {
                    CheckTextMarker(markers[i], i, ref ended);
                    var text = e.Strings[i];
                    Check((markers[i] != 0) == (text is not null), "Event presence marker/string disagree");
                    if (text is null)
                    {
                        continue;
                    }

                    Check(text.Count <= 255, "Unsupported event string over 255 bytes");
                    Check(!text.Contains((byte)0), "Embedded event string NUL");
                    Append(w, text);
                    w.U8(0);
                }
            }
        }
        Append(w, save.Trailing);
        return w.ToArray();
    }
}
